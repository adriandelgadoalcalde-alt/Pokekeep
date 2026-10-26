// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Diagnostics;
using AutoLegalidad.Registro;
using Pokekeep.Mobile.Ui.Masivas;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Operaciones.OperacionLarga;

/// <summary>Por qué se paró una operación larga sin que el usuario pulsara «Cancelar» en la pantalla.</summary>
public enum ParadaOperacion { Ninguna, CancelarDuro, LimiteDeTiempo, Temperatura }

/// <summary>
/// Vigila una operación larga del móvil («Edición masiva» y, desde el bloque 9b, «Completar el juego»): F9X-02 a F9X-07, ADR 0070. Es neutro (sin tipos de Android) y se prueba en el PC con una plataforma falsa.
/// <list type="number">
/// <item>Energía (F9X-07): decide los procesos con <see cref="PoliticaEnergia"/> antes de empezar y, durante la operación, la detiene si el móvil se calienta demasiado.</item>
/// <item>Servicio en primer plano (F9X-02): <see cref="IPlataformaOperacionLarga.Iniciar"/> al empezar y <see cref="IPlataformaOperacionLarga.Terminar"/> al acabar, pase lo que pase.</item>
/// <item>Notificación (F9X-03): progreso REAL por unidad (el del núcleo), sin saturar a Android (como mucho una actualización cada <c>intervaloNotificacion</c> salvo al avanzar una unidad).</item>
/// <item>Cancelar duro (F9X-04): desde la notificación o al agotar Android el tiempo del servicio, cancela el token del coordinador (cancelar cooperativo: la pasarela da al motor <c>HardStopGrace</c> = 2 s), NO coloca nada
/// (la transacción se escribe entera al final: no llega a escribirse; si la pregunta «¿colocar los N ya preparados?» está abierta, se responde «no» y la pantalla la cierra; la pantalla pasa a «Cancelando…»)
/// y, tras <c>graciaCancelar</c> (3 s, algo más que la de la pasarela), mata los procesos del motor que sigan vivos (<c>matarMotores</c>: en Android, <c>IMotoresMatables.MatarTodos</c> de la pasarela o del pool), de modo que la siguiente solicitud arranca con un PID nuevo.</item>
/// <item>Checkpoint (F9X-05): <see cref="RegistroOperacionLarga"/> se crea al empezar, avanza una línea por unidad y se borra al terminar; si Android mata la app, al reabrir queda y se avisa (F9X-06).</item>
/// </list>
/// </summary>
public sealed class SupervisorOperacionLarga(IPlataformaOperacionLarga plataforma, RegistroOperacionLarga registro, Func<int>? matarMotores = null,
    TimeSpan? graciaCancelar = null, TimeSpan? intervaloNotificacion = null, TimeSpan? intervaloTermico = null)
{
    public static string DetalleCancelarDuro => IdiomaInterfaz.T("SupervisorOperacionLarga.CanceladoDesdeLaNotificacion");
    public static string DetalleLimiteDeTiempo => IdiomaInterfaz.T("SupervisorOperacionLarga.AndroidHaAgotadoEl");
    public static string TitularNoIniciada => IdiomaInterfaz.T("SupervisorOperacionLarga.NoSeHaEmpezado");

    private readonly TimeSpan _gracia = graciaCancelar ?? TimeSpan.FromSeconds(3);
    private readonly TimeSpan _intervalo = intervaloNotificacion ?? TimeSpan.FromMilliseconds(500);
    private readonly TimeSpan _intervaloTermico = intervaloTermico ?? TimeSpan.FromSeconds(5);
    private readonly object _cerrojo = new();
    private CancellationTokenSource? _cts;
    private Task? _operacion;
    private string _titulo = string.Empty;
    private string? _aviso;
    private ProgresoMasivo? _ultimo;
    private int _parada;
    private int _generacion;   // una por EjecutarAsync: el Cancelar duro tardío no debe matar los motores de una operación posterior
    private TaskCompletionSource<bool>? _duro;
    private IProgress<ProgresoMasivo>? _destino;

    public IPlataformaOperacionLarga Plataforma => plataforma;
    public RegistroOperacionLarga Registro => registro;
    public bool EnCurso { get { lock (_cerrojo) return _cts is not null; } }
    public ParadaOperacion Parada => (ParadaOperacion)Volatile.Read(ref _parada);
    private bool ParadaDura => Parada is ParadaOperacion.CancelarDuro or ParadaOperacion.LimiteDeTiempo;

    /// <summary>Procesos que se usarán con la energía de ahora (para la vista previa y el estimado).</summary>
    public DecisionEnergia Decidir(int procesosDelAjuste, int procesosMaximos) => PoliticaEnergia.Decidir(procesosDelAjuste, procesosMaximos, plataforma.LeerEnergia());

    /// <summary>Acción «Cancelar» de la notificación (cualquier hilo).</summary>
    public void CancelarDuro() => Parar(ParadaOperacion.CancelarDuro);

    /// <summary>Android agotó el tiempo del servicio <c>dataSync</c> (<c>onTimeout</c>, Android 15+): se cancela como un Cancelar duro (cualquier hilo).</summary>
    public void LimiteDeTiempo() => Parar(ParadaOperacion.LimiteDeTiempo);

    private void Parar(ParadaOperacion motivo)
    {
        CancellationTokenSource? cts;
        Task? operacion;
        TaskCompletionSource<bool>? paradaDura;
        IProgress<ProgresoMasivo>? destino;
        int generacion;
        lock (_cerrojo) { cts = _cts; operacion = _operacion; paradaDura = _duro; destino = _destino; generacion = _generacion; }
        if (cts is null) return;
        var duro = motivo is ParadaOperacion.CancelarDuro or ParadaOperacion.LimiteDeTiempo;
        // Una parada dura gana también a la de temperatura (B2 de la auditoría: el «Cancelar» de la notificación sigue funcionando mientras se pregunta si colocar).
        int previo;
        do
        {
            previo = Volatile.Read(ref _parada);
            if (previo != (int)ParadaOperacion.Ninguna && !(duro && previo == (int)ParadaOperacion.Temperatura)) return;
        }
        while (Interlocked.CompareExchange(ref _parada, (int)motivo, previo) != previo);
        RegistroFallos.Info(IdiomaInterfaz.F("SupervisorOperacionLarga.OperacionLargaDetenida", motivo), "SupervisorOperacionLarga");
        try { cts.Cancel(); } catch (ObjectDisposedException) { return; }
        if (!duro) return;   // parada cooperativa: lo ya preparado se puede colocar con la pregunta de siempre
        paradaDura?.TrySetResult(true);   // la pregunta «¿colocar?» abierta se responde «no» (M1 de la auditoría)
        Notificar(forzar: true);
        try { destino?.Report(PantallaCancelando(_ultimo)); }   // la pantalla se entera (B1 de la auditoría)
        catch (Exception ex) when (ex is not OutOfMemoryException) { RegistroFallos.Aviso(IdiomaInterfaz.T("SupervisorOperacionLarga.NoSePudoAvisar"), ex, "SupervisorOperacionLarga"); }
        // Cancelar duro: tras la gracia, los motores que sigan vivos se matan; si ya terminó, se mata el proceso ocioso igualmente (PID reciclado: la siguiente solicitud empieza limpia).
        _ = Task.Run(async () =>
        {
            // Sin tarea todavía (la operación aún no devolvió el control): se espera la gracia entera.
            await Task.WhenAny(operacion ?? Task.Delay(_gracia), Task.Delay(_gracia)).ConfigureAwait(false);
            lock (_cerrojo) { if (_generacion != generacion) return; }   // ya empezó otra operación: sus motores no son de este Cancelar
            var muertos = matarMotores?.Invoke() ?? 0;
            RegistroFallos.Info(IdiomaInterfaz.F("SupervisorOperacionLarga.CancelarDuroProcesoS", muertos), "SupervisorOperacionLarga");
        });
    }

    private static ProgresoMasivo PantallaCancelando(ProgresoMasivo? p) =>
        p is null ? new ProgresoMasivo(EstadoNotificacion.TextoCancelando, 0, 0, 0, string.Empty, 0, TimeSpan.Zero, null, []) : p with { Texto = EstadoNotificacion.TextoCancelando };

    private long _ultimaNotificacion = long.MinValue / 2;
    private int _ultimosHechos = -1;
    private long _ultimaLecturaTermica;
    private readonly Stopwatch _reloj = Stopwatch.StartNew();

    private void Notificar(bool forzar)
    {
        var p = _ultimo;
        var cancelando = Parada is ParadaOperacion.CancelarDuro or ParadaOperacion.LimiteDeTiempo;
        var estado = p is null ? EstadoNotificacion.Inicial(_titulo, _aviso) with { Cancelando = cancelando, Texto = cancelando ? EstadoNotificacion.TextoCancelando : EstadoNotificacion.TextoPreparando }
            : EstadoNotificacion.De(_titulo, p, _aviso, cancelando);
        var ahora = _reloj.ElapsedMilliseconds;
        var avanzo = p is not null && p.Hechos != _ultimosHechos;
        if (!forzar && !(avanzo && ahora - Interlocked.Read(ref _ultimaNotificacion) >= _intervalo.TotalMilliseconds) && ahora - Interlocked.Read(ref _ultimaNotificacion) < Math.Max(1000, _intervalo.TotalMilliseconds))
            return;
        Interlocked.Exchange(ref _ultimaNotificacion, ahora);
        if (p is not null) _ultimosHechos = p.Hechos;
        try { plataforma.Actualizar(estado); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { RegistroFallos.Aviso(IdiomaInterfaz.T("SupervisorOperacionLarga.NoSePudoActualizar"), ex, "SupervisorOperacionLarga"); }
    }

    private sealed class Progreso(SupervisorOperacionLarga s, IProgress<ProgresoMasivo>? destino) : IProgress<ProgresoMasivo>
    {
        public void Report(ProgresoMasivo value) => s.AlProgresar(value, destino);
    }

    private void AlProgresar(ProgresoMasivo p, IProgress<ProgresoMasivo>? destino)
    {
        _ultimo = p;
        try { registro.Unidades(p.Hechos); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { RegistroFallos.Aviso(IdiomaInterfaz.T("SupervisorOperacionLarga.NoSePudoAnotar"), ex, "SupervisorOperacionLarga"); }
        Notificar(forzar: false);
        var ahora = _reloj.ElapsedMilliseconds;
        if (ahora - Interlocked.Read(ref _ultimaLecturaTermica) >= _intervaloTermico.TotalMilliseconds)
        {
            Interlocked.Exchange(ref _ultimaLecturaTermica, ahora);
            if (plataforma.LeerEnergia().Termico >= EstadoTermico.Grave) Parar(ParadaOperacion.Temperatura);
        }
        destino?.Report(ParadaDura ? PantallaCancelando(p) : _aviso is null ? p : p with { Texto = $"{p.Texto}\n{_aviso}" });
    }

    /// <summary>
    /// Ejecuta <paramref name="operacion"/> (que recibe los procesos decididos, el progreso, la pregunta de colocar y el token) bajo vigilancia. Devuelve SIEMPRE el resultado honesto de la operación,
    /// con el motivo de la parada en el detalle si no se aplicó nada por ella.
    /// </summary>
    public async Task<ResultadoMasivo> EjecutarAsync(string tipo, string ambito, int total, int procesosDelAjuste, int procesosMaximos,
        Func<int, IProgress<ProgresoMasivo>, Func<int, Task<bool>>?, CancellationToken, Task<ResultadoMasivo>> operacion,
        IProgress<ProgresoMasivo>? progreso, Func<int, Task<bool>>? colocarGenerados, CancellationToken cancelar)
    {
        var decision = Decidir(procesosDelAjuste, procesosMaximos);
        if (decision.Detener)
            return new ResultadoMasivo(ClaseMasiva.Cancelado, TitularNoIniciada, IdiomaInterfaz.F("SupervisorOperacionLarga.NoSeHaModificado", decision.Aviso), InformeMasivo.Vacio(tipo), 0, TimeSpan.Zero, null);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancelar);
        var paradaDura = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_cerrojo)
        {
            if (_cts is not null)
                return new ResultadoMasivo(ClaseMasiva.Ocupado, AutoLegalidad.Ui.Textos.OperacionEnCurso, null, InformeMasivo.Vacio(tipo), 0, TimeSpan.Zero, null);
            // Todo el estado de la operación se deja listo ANTES de publicar el token: un Cancelar que llegue justo después ya encuentra su parada y su pregunta (revisión del coordinador, 9c).
            Volatile.Write(ref _parada, (int)ParadaOperacion.Ninguna);
            _titulo = tipo; _aviso = decision.Aviso; _ultimo = null; _ultimosHechos = -1; _destino = progreso;
            _duro = paradaDura;
            _generacion++;
            _operacion = null;
            _cts = cts;
        }
        Interlocked.Exchange(ref _ultimaLecturaTermica, _reloj.ElapsedMilliseconds);
        try
        {
            try { registro.Empezar(tipo, ambito, total); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { RegistroFallos.Aviso(IdiomaInterfaz.T("SupervisorOperacionLarga.NoSePudoCrear"), ex, "SupervisorOperacionLarga"); }
            try { plataforma.Iniciar(EstadoNotificacion.Inicial(tipo, decision.Aviso), CancelarDuro, LimiteDeTiempo); }
            catch (Exception ex) when (ex is not OutOfMemoryException) { RegistroFallos.Aviso(IdiomaInterfaz.T("SupervisorOperacionLarga.NoSePudoPasar"), ex, "SupervisorOperacionLarga"); }

            // Nunca null: con delegado null el núcleo colocaría sin preguntar, también tras un Cancelar duro (M1 de la auditoría). La pregunta corre en carrera con la parada dura: si gana la parada, «no».
            Func<int, Task<bool>> colocar = async n =>
            {
                if (ParadaDura) return false;
                if (colocarGenerados is null) return true;
                var pregunta = colocarGenerados(n);
                var gana = await Task.WhenAny(pregunta, paradaDura.Task).ConfigureAwait(true);
                return gana == pregunta && !ParadaDura && await pregunta.ConfigureAwait(true);
            };
            var tarea = operacion(decision.Procesos, new Progreso(this, progreso), colocar, cts.Token);
            lock (_cerrojo) _operacion = tarea;
            if (Parada != ParadaOperacion.Ninguna && !cts.IsCancellationRequested) cts.Cancel();
            var r = await tarea.ConfigureAwait(true);
            return Explicar(r);
        }
        finally
        {
            lock (_cerrojo) { _cts = null; _operacion = null; _destino = null; _duro = null; }
            try { registro.Cerrar(); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            try { plataforma.Terminar(); }
            catch (Exception ex) when (ex is not OutOfMemoryException) { RegistroFallos.Aviso(IdiomaInterfaz.T("SupervisorOperacionLarga.NoSePudoParar"), ex, "SupervisorOperacionLarga"); }
        }
    }

    private ResultadoMasivo Explicar(ResultadoMasivo r)
    {
        if (r.Aplicado) return r;   // honesto: si ya se había aplicado, se dice lo que pasó
        if (r.Clase != ClaseMasiva.Cancelado) return r;   // un fallo del motor (u otro resultado) conserva su detalle propio
        var motivo = Parada switch
        {
            ParadaOperacion.CancelarDuro => DetalleCancelarDuro,
            ParadaOperacion.LimiteDeTiempo => DetalleLimiteDeTiempo,
            ParadaOperacion.Temperatura => PoliticaEnergia.AvisoDetener + IdiomaInterfaz.T("SupervisorOperacionLarga.NoSeHaAplicado"),
            _ => null,
        };
        return motivo is null ? r : r with { Detalle = motivo };
    }
}
