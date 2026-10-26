// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using AutoLegalidad.Contracts;
using AutoLegalidad.Ui;

namespace Pokekeep.Mobile.Engine.Ipc;

/// <summary>Plazos del gateway (inyectables para las pruebas; los de produccion son los del plan 5.3).</summary>
public sealed record OpcionesGateway
{
    /// <summary>Margen que se concede, tras cancelar, a que el motor devuelva el control por su cuenta antes de matar el proceso (<c>HardStopGrace</c> del Worker, 2 s).</summary>
    public TimeSpan HardStopGrace { get; init; } = TimeSpan.FromSeconds(2);
    /// <summary>Margen sobre el plazo de la solicitud antes de que la interfaz dé el motor por muerto (el vigilante duro del propio motor actua a plazo + <c>HardStopGrace</c>).</summary>
    public TimeSpan MargenSobrePlazo { get; init; } = TimeSpan.FromSeconds(6);
    public TimeSpan EsperaManifiesto { get; init; } = TimeSpan.FromSeconds(30);
    public int VersionCodeEsperado { get; init; }
    /// <summary>Plan 5.3: el proceso se recicla al cancelar o vencer el plazo (hilos abandonados, estado estatico del Core): la siguiente solicitud empieza siempre limpia.</summary>
    public bool ReciclarTrasCancelar { get; init; } = true;
}

/// <summary>
/// <see cref="IEngineGateway"/> de Android (plan 5.3): habla con el servicio <c>:motor</c> por un socket local con marcos de longitud. Cancelar = marco de cancelacion cooperativa y, si el motor no responde en
/// <see cref="OpcionesGateway.HardStopGrace"/>, <see cref="IMotorLanzador.Matar"/> sobre el PID del saludo y reinicio limpio en la siguiente solicitud. Una respuesta de otra generacion de sesion
/// (se abrio otro guardado o se recreo la actividad) se descarta (regla 7); la UI decide <c>TargetChanged</c>. El PID del motor se anota en el registro en cada reinicio.
/// </summary>
public sealed class AndroidEngineGateway(IMotorLanzador lanzador, OpcionesGateway? opciones = null, Action<string>? registro = null) : IEngineGateway, IMotoresMatables, IDisposable
{
    private readonly OpcionesGateway _op = opciones ?? new();
    private readonly SemaphoreSlim _una = new(1, 1);
    // Cada pendiente guarda la conexión a la que se envió: el hilo lector de una conexión muerta solo puede cerrar SUS pendientes, nunca los de la conexión nueva (C10).
    private readonly ConcurrentDictionary<string, (Conexion Con, TaskCompletionSource<byte[]> Tcs)> _pendientes = new();
    private Conexion? _con;
    private long _generacion;
    private int _arranques;
    private bool _liberado;

    public event Action<string>? DiagnosticLine;

    public string? UnavailableReason => lanzador.NoDisponible;

    /// <summary>PID del motor al que se esta conectado (0 si no hay conexion). Lo usan las pruebas de aislamiento.</summary>
    public int MotorPid => _con?.Saludo.Pid ?? 0;
    public int Arranques => _arranques;
    public long Generacion => Interlocked.Read(ref _generacion);

    public IReadOnlyList<int> Pids => [MotorPid];

    /// <summary>
    /// Cancelar duro (F9X-04): mata el proceso del motor conectado, si lo hay. La solicitud en vuelo recibe «conexión rota» y la siguiente arranca un proceso limpio (PID reciclado). Devuelve 1 si había
    /// un proceso que matar.
    /// </summary>
    public int MatarTodos()
    {
        if (_con is not { } c) return 0;
        registro?.Invoke($"cancelar duro: se mata el motor (pid {c.Saludo.Pid})");
        Descartar(c, matar: true);
        return 1;
    }

    /// <summary>Se abrio otro guardado o se recreo la actividad: las respuestas de las solicitudes anteriores ya no valen.</summary>
    public long NuevaGeneracion() => Interlocked.Increment(ref _generacion);

    private sealed class Conexion(Stream flujo, Saludo saludo, string nombre)
    {
        public Stream Flujo { get; } = flujo;
        public Saludo Saludo { get; } = saludo;
        public string Nombre { get; } = nombre;
        public volatile bool Rota;
    }

    private static int _contadorNombres;

    private async Task<Conexion> AsegurarAsync(CancellationToken cancelar)
    {
        if (_con is { Rota: false } viva) return viva;
        if (_con is { } vieja) Descartar(vieja, matar: false);   // conexion ya inservible (corte o cierre avisado por el motor): se suelta antes de abrir otra
        // Un reintento: un motor que acaba de morir o esta arrancando puede cortar el saludo; con otro nombre la segunda vez solo conecta con el proceso que recibio ESA orden de arranque.
        try { return await ConectarUnaVezAsync(cancelar).ConfigureAwait(false); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or TimeoutException or System.Net.Sockets.SocketException)
        {
            registro?.Invoke("primer intento de conexion con el motor fallido (" + ex.GetType().Name + "): se reintenta");
            await Task.Delay(500, cancelar).ConfigureAwait(false);
            return await ConectarUnaVezAsync(cancelar).ConfigureAwait(false);
        }
    }

    private async Task<Conexion> ConectarUnaVezAsync(CancellationToken cancelar)
    {
        // Nombre unico por arranque en TODO el proceso de la interfaz (varios gateways seguidos no reutilizan un socket de un motor que se esta muriendo).
        Interlocked.Increment(ref _arranques);
        var nombre = $"pokekeep-motor-{Environment.ProcessId}-{Interlocked.Increment(ref _contadorNombres)}-{Environment.TickCount64 % 100000}";
        var generacion = Generacion;
        var flujo = await lanzador.ConectarAsync(nombre, generacion, cancelar).ConfigureAwait(false);
        (byte Tipo, byte[] Carga)? primero;
        try { primero = await Task.Run(() => Marcos.Leer(flujo), cancelar).ConfigureAwait(false); }
        catch { flujo.Dispose(); throw; }
        if (primero is not { Tipo: TipoMarco.Saludo })
        {
            flujo.Dispose();
            throw new InvalidDataException("El motor no saludó.");
        }
        var saludo = Marcos.LeerSaludo(primero.Value.Carga);
        if (saludo.Protocolo != Protocol.Version || (_op.VersionCodeEsperado != 0 && saludo.VersionCode != _op.VersionCodeEsperado))
        {
            flujo.Dispose();
            lanzador.Matar(saludo.Pid);
            throw new InvalidOperationException($"El motor es de otra versión (protocolo {saludo.Protocolo}, versionCode {saludo.VersionCode}).");
        }
        var con = new Conexion(flujo, saludo, nombre);
        _con = con;
        registro?.Invoke($"motor conectado: pid {saludo.Pid}, generación de sesión {saludo.Generacion}, arranque {_arranques}");
        _ = Task.Run(() => Lectura(con));
        return con;
    }

    private void Lectura(Conexion con)
    {
        try
        {
            while (true)
            {
                var marco = Marcos.Leer(con.Flujo);
                if (marco is null) break;
                switch (marco.Value.Tipo)
                {
                    case TipoMarco.Registro:
                        try { DiagnosticLine?.Invoke(Encoding.UTF8.GetString(marco.Value.Carga)); } catch (Exception ex) when (ex is not OutOfMemoryException) { }
                        break;
                    case TipoMarco.Respuesta:
                        {
                            var id = ExtraerId(marco.Value.Carga, "requestId");
                            if (id is not null && TomarPendiente(id, con) is { } tcs) tcs.TrySetResult(marco.Value.Carga);
                            else registro?.Invoke($"respuesta sin solicitud pendiente descartada ({id ?? "sin id"})");
                            break;
                        }
                    case TipoMarco.Cierre:
                        // El motor se termina tras la respuesta que viene a continuacion: ya no se le manda nada mas (la siguiente solicitud arranca otro).
                        con.Rota = true;
                        break;
                    case TipoMarco.Manifiesto:
                        if (TomarPendiente("manifiesto", con) is { } m) m.TrySetResult(marco.Value.Carga);
                        break;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ObjectDisposedException or System.Net.Sockets.SocketException) { }
        finally
        {
            con.Rota = true;
            foreach (var (clave, p) in _pendientes.ToArray())
                if (ReferenceEquals(p.Con, con) && TomarPendiente(clave, con) is { } tcs) tcs.TrySetException(new IOException("conexión con el motor rota"));
        }
    }

    /// <summary>Quita y devuelve el pendiente <paramref name="clave"/> solo si pertenece a <paramref name="con"/>.</summary>
    private TaskCompletionSource<byte[]>? TomarPendiente(string clave, Conexion con)
        => _pendientes.TryGetValue(clave, out var p) && ReferenceEquals(p.Con, con) && _pendientes.TryRemove(new KeyValuePair<string, (Conexion, TaskCompletionSource<byte[]>)>(clave, p)) ? p.Tcs : null;

    private static string? ExtraerId(byte[] json, string campo)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty(campo, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        }
        catch (JsonException) { return null; }
    }

    public async Task<WorkerManifest?> GetManifestAsync(CancellationToken cancelar)
    {
        if (UnavailableReason is not null) return null;
        await _una.WaitAsync(cancelar).ConfigureAwait(false);
        try
        {
            var con = await AsegurarAsync(cancelar).ConfigureAwait(false);
            var tcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendientes[Clave("manifiesto")] = (con, tcs);
            Marcos.Escribir(con.Flujo, TipoMarco.PedirManifiesto, []);
            var terminada = await Task.WhenAny(tcs.Task, Task.Delay(_op.EsperaManifiesto, cancelar)).ConfigureAwait(false);
            if (terminada != tcs.Task) { _pendientes.TryRemove("manifiesto", out _); return null; }
            var json = await tcs.Task.ConfigureAwait(false);
            if (ExtraerId(json, "error") is not null) return null;
            return Json.Deserialize<WorkerManifest>(json);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or OperationCanceledException or System.Net.Sockets.SocketException or JsonException)
        {
            registro?.Invoke("manifiesto no disponible: " + ex.GetType().Name);
            return null;
        }
        finally { _una.Release(); }
    }

    private static string Clave(string c) => c;

    public async Task<LegalizeResponse> LegalizeAsync(LegalizeRequest solicitud, CancellationToken cancelar)
    {
        var reloj = Stopwatch.StartNew();
        if (UnavailableReason is { } porque) return Fallo(solicitud, ResponseStatus.InternalError, porque, porque, reloj);
        try { await _una.WaitAsync(cancelar).ConfigureAwait(false); }
        catch (OperationCanceledException) { return Fallo(solicitud, ResponseStatus.Cancelled, "Operación cancelada.", null, reloj); }
        try
        {
            var generacionInicial = Generacion;
            Conexion con;
            try { con = await AsegurarAsync(cancelar).ConfigureAwait(false); }
            catch (OperationCanceledException) { return Fallo(solicitud, ResponseStatus.Cancelled, "Operación cancelada.", null, reloj); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or TimeoutException or System.Net.Sockets.SocketException)
            {
                registro?.Invoke("no se pudo conectar con el motor: " + ex.Message);
                return Fallo(solicitud, ResponseStatus.InternalError, "No se pudo arrancar el motor.", ex.Message, reloj);
            }

            var tcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendientes[solicitud.RequestId] = (con, tcs);
            try { Marcos.Escribir(con.Flujo, TipoMarco.Solicitud, Json.SerializeToUtf8(solicitud)); }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or System.Net.Sockets.SocketException)
            {
                _pendientes.TryRemove(solicitud.RequestId, out _);
                Descartar(con, matar: true);
                return Fallo(solicitud, ResponseStatus.InternalError, "Se perdió la conexión con el motor.", ex.Message, reloj);
            }

            var plazo = (solicitud.DeadlineUtc ?? DateTimeOffset.UtcNow.AddSeconds(solicitud.Action == LegalizeAction.Optimize ? 300 : 60)) - DateTimeOffset.UtcNow;
            var limite = (plazo < TimeSpan.Zero ? TimeSpan.Zero : plazo) + _op.HardStopGrace + _op.MargenSobrePlazo;
            using var temporizador = new CancellationTokenSource(limite);
            var espera = Task.Delay(Timeout.Infinite, CancellationTokenSource.CreateLinkedTokenSource(cancelar, temporizador.Token).Token);
            var ganadora = await Task.WhenAny(tcs.Task, espera).ConfigureAwait(false);
            byte[]? bytes = null;
            if (ganadora == tcs.Task)
            {
                try { bytes = await tcs.Task.ConfigureAwait(false); }
                catch (IOException)
                {
                    // Conexion rota (el motor murio o lo mataron): nada que cancelar; la siguiente solicitud arranca uno limpio.
                    Descartar(con, matar: false);
                    return Fallo(solicitud, ResponseStatus.InternalError, "El motor se detuvo. Vuelve a intentarlo.", "Conexión con el motor rota.", reloj);
                }
            }
            else
            {
                var porCancelar = cancelar.IsCancellationRequested;
                try { Marcos.Escribir(con.Flujo, TipoMarco.Cancelar, []); } catch (Exception ex) when (ex is IOException or ObjectDisposedException or System.Net.Sockets.SocketException) { }
                var graciosa = await Task.WhenAny(tcs.Task, Task.Delay(_op.HardStopGrace)).ConfigureAwait(false);
                if (graciosa == tcs.Task && !tcs.Task.IsFaulted)
                {
                    bytes = tcs.Task.Result;
                    if (_op.ReciclarTrasCancelar) Descartar(con, matar: true);
                    if (porCancelar)
                    {
                        // El usuario cancelo: el motor puede devolver «lo mejor hasta ahora» (Optimizar es una busqueda que se puede cortar) pero Cancelar significa que NO se aplica nada.
                        registro?.Invoke("el motor respondio " + (ExtraerId(bytes, "status") ?? "?") + " tras cancelar: se descarta");
                        return Fallo(solicitud, ResponseStatus.Cancelled, "Operación cancelada.", null, reloj);
                    }
                }
                else
                {
                    // No obedecio: se mata el proceso entero (nunca un hilo) y la siguiente solicitud empieza limpia.
                    registro?.Invoke($"el motor (pid {con.Saludo.Pid}) no devolvió el control tras {_op.HardStopGrace.TotalSeconds:0.#} s: se mata");
                    _pendientes.TryRemove(solicitud.RequestId, out _);
                    Descartar(con, matar: true);
                    return porCancelar
                        ? Fallo(solicitud, ResponseStatus.Cancelled, "Operación cancelada.", null, reloj)
                        : Fallo(solicitud, ResponseStatus.Timeout, "Se agotó el tiempo antes de terminar la búsqueda.", "El motor no devolvió el control antes del plazo; esto NO prueba que no exista solución.", reloj);
                }
            }

            if (Generacion != generacionInicial)
            {
                registro?.Invoke($"respuesta de la generación {generacionInicial} descartada (la actual es {Generacion})");
                return Fallo(solicitud, ResponseStatus.Cancelled, "El destino cambió mientras se generaba; se descarta la respuesta.", null, reloj);
            }
            LegalizeResponse respuesta;
            try { respuesta = Json.Deserialize<LegalizeResponse>(bytes!); }
            catch (JsonException ex) { return Fallo(solicitud, ResponseStatus.InternalError, "La respuesta del motor no cumple el contrato.", ex.Message, reloj); }
            var problemas = ResponseValidator.Validate(respuesta);
            if (problemas.Count > 0) return Fallo(solicitud, ResponseStatus.InternalError, "La respuesta del motor es incoherente y se descarta.", string.Join("; ", problemas), reloj);
            if (!string.Equals(respuesta.RequestId, solicitud.RequestId, StringComparison.OrdinalIgnoreCase))
                return Fallo(solicitud, ResponseStatus.InternalError, "La respuesta no corresponde a esta solicitud.", $"requestId {respuesta.RequestId}", reloj);
            return respuesta with { ElapsedMilliseconds = reloj.ElapsedMilliseconds };
        }
        finally { _pendientes.TryRemove(solicitud.RequestId, out _); _una.Release(); }
    }

    private void Descartar(Conexion con, bool matar)
    {
        con.Rota = true;
        if (ReferenceEquals(_con, con)) _con = null;
        var pid = con.Saludo.Pid;
        try { con.Flujo.Dispose(); } catch { }
        if (matar) lanzador.Matar(pid);
    }

    private static LegalizeResponse Fallo(LegalizeRequest r, ResponseStatus estado, string mensaje, string? motivo, Stopwatch reloj) => new()
    {
        ProtocolVersion = Protocol.Version,
        RequestId = r.RequestId,
        ReleaseId = r.ReleaseId,
        DatasetSnapshotId = r.DatasetSnapshotId,
        Action = r.Action,
        Status = estado,
        Message = mensaje,
        Reason = motivo,
        ElapsedMilliseconds = reloj.ElapsedMilliseconds,
    };

    /// <summary>Cierra la sesion: apaga el motor de forma ordenada y para el servicio.</summary>
    public void Dispose()
    {
        if (_liberado) return;
        _liberado = true;
        if (_con is { Rota: false } c)
        {
            try { Marcos.Escribir(c.Flujo, TipoMarco.Apagar, []); } catch { }
            try { c.Flujo.Dispose(); } catch { }
        }
        _con = null;
        try { lanzador.Parar(); } catch { }
    }
}
