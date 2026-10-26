// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Text;
using AutoLegalidad.Contracts;

namespace Pokekeep.Mobile.Engine.Ipc;

/// <summary>
/// Lado del proceso <c>:motor</c> (plan 5.3): acepta UNA conexion de la interfaz a la vez, saluda con su PID y atiende marcos. Una solicitud activa como maximo (el motor guarda estado estatico:
/// <c>ParseSettings</c>, caches). Cada solicitud corre <see cref="EjecutorMotor"/> (en produccion <c>WorkerHost.Run</c> tal cual) sobre flujos en memoria; el diagnostico viaja como marcos de registro.
/// Cancelar es cooperativo (el vigilante duro del Worker, que mata el proceso tras responder Timeout, sigue dentro del motor: es el comportamiento deseado).
/// </summary>
public sealed class MotorServidor(IAceptador aceptador, EjecutorMotor ejecutor, Func<string?> manifiestoJson, Saludo saludo, Action<int> salirDelProceso)
{
    /// <summary>Atiende conexiones hasta que <paramref name="parar"/> se cancela o la interfaz pide apagar.</summary>
    public void Servir(CancellationToken parar)
    {
        while (!parar.IsCancellationRequested)
        {
            Stream? flujo;
            try { flujo = aceptador.Aceptar(parar); }
            catch (Exception) when (parar.IsCancellationRequested) { return; }
            if (flujo is null) return;
            using (flujo)
            {
                if (!AtenderConexion(flujo, parar)) return;   // false = apagado ordenado
            }
        }
    }

    private bool AtenderConexion(Stream flujo, CancellationToken parar)
    {
        using var cts = new CancellationTokenSource();
        using var enlace = parar.Register(static c => ((CancellationTokenSource)c!).Cancel(), cts);
        Task? activa = null;
        try
        {
            Marcos.EscribirSaludo(flujo, saludo);
            while (true)
            {
                (byte Tipo, byte[] Carga)? marco;
                try { marco = Marcos.Leer(flujo); }
                catch (Exception ex) when (ex is IOException or InvalidDataException or ObjectDisposedException) { return true; }
                if (marco is null) return true;                       // la interfaz cerro: se vuelve a aceptar (el motor sigue vivo)
                switch (marco.Value.Tipo)
                {
                    case TipoMarco.Solicitud:
                        if (Interlocked.CompareExchange(ref _ocupado, 1, 0) != 0)
                        {
                            Marcos.Escribir(flujo, TipoMarco.Respuesta, RespuestaFallo(marco.Value.Carga, ResponseStatus.InternalError, "El motor está ocupado con otra solicitud.", "Solo hay una solicitud activa."));
                            break;
                        }
                        activa = AtenderSolicitud(flujo, marco.Value.Carga, cts);
                        break;
                    case TipoMarco.Cancelar:
                        try { Volatile.Read(ref _cancelActual)?.Cancel(); } catch (ObjectDisposedException) { }
                        break;
                    case TipoMarco.PedirManifiesto:
                        _ = Task.Run(() =>
                        {
                            try { Marcos.EscribirTexto(flujo, TipoMarco.Manifiesto, manifiestoJson() ?? "{\"error\":\"manifiesto no disponible\"}"); }
                            catch (Exception ex) when (ex is IOException or ObjectDisposedException) { }
                            catch (Exception ex) when (ex is not OutOfMemoryException) { try { Marcos.EscribirTexto(flujo, TipoMarco.Manifiesto, "{\"error\":\"" + ex.GetType().Name + "\"}"); } catch { } }
                        });
                        break;
                    case TipoMarco.Apagar:
                        cts.Cancel();
                        return false;
                }
            }
        }
        finally
        {
            cts.Cancel();   // la conexion se perdio o se cerro: lo que corriera se cancela (cooperativo)
        }
    }

    private Task AtenderSolicitud(Stream flujo, byte[] solicitud, CancellationTokenSource conexion)
    {
        // Un token por solicitud, enlazado al de la conexion: cancelar una solicitud no deja «cancelada» la siguiente.
        var porSolicitud = CancellationTokenSource.CreateLinkedTokenSource(conexion.Token);
        Volatile.Write(ref _cancelActual, porSolicitud);
        return Task.Run(() =>
        {
            var entrada = new MemoryStream(solicitud);
            var salida = new MemoryStream();
            var registro = new RegistroPorMarcos(flujo);
            var enviada = 0;
            void EnviarRespuesta()
            {
                if (Interlocked.Exchange(ref enviada, 1) != 0) return;
                Volatile.Write(ref _ocupado, 0);   // libre ANTES de contestar: la interfaz puede mandar la siguiente solicitud en cuanto lee esta respuesta
                try { Marcos.Escribir(flujo, TipoMarco.Respuesta, salida.ToArray()); }
                catch (Exception ex) when (ex is IOException or ObjectDisposedException) { }
            }
            try
            {
                var codigo = ejecutor(entrada, salida, registro, porSolicitud.Token, paradaDura: c => { AvisarCierre(flujo); EnviarRespuesta(); salirDelProceso(c); });
                registro.Flush();
                if (salida.Length == 0)
                    salida.Write(RespuestaFallo(solicitud, ResponseStatus.InternalError, "El motor terminó sin respuesta.", $"Código de salida {codigo}."));
                EnviarRespuesta();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                if (salida.Length == 0) salida.Write(RespuestaFallo(solicitud, ResponseStatus.InternalError, "Error interno del motor.", $"{ex.GetType().Name}: {ex.Message}"));
                EnviarRespuesta();
            }
            finally { Interlocked.CompareExchange(ref _cancelActual, null, porSolicitud); porSolicitud.Dispose(); }
        });
    }

    /// <summary>El vigilante duro va a terminar el proceso tras responder: se avisa a la interfaz ANTES de la respuesta para que descarte esta conexion de inmediato (nunca se reutiliza un motor que se esta muriendo).</summary>
    private static void AvisarCierre(Stream flujo)
    {
        try { Marcos.Escribir(flujo, TipoMarco.Cierre, []); }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException) { }
    }

    private CancellationTokenSource? _cancelActual;
    private int _ocupado;

    /// <summary>Respuesta de fallo minima y valida del contrato para una solicitud (se intenta conservar su requestId).</summary>
    private static byte[] RespuestaFallo(byte[] solicitudJson, ResponseStatus estado, string mensaje, string motivo)
    {
        var id = Guid.Empty.ToString(); var accion = LegalizeAction.Legalize; var snapshot = (string?)null; var release = (string?)null;
        try
        {
            var r = Json.Deserialize<LegalizeRequest>(solicitudJson);
            id = r.RequestId; accion = r.Action; snapshot = r.DatasetSnapshotId; release = r.ReleaseId;
        }
        catch (Exception) { }
        var resp = new LegalizeResponse { ProtocolVersion = Protocol.Version, RequestId = id, ReleaseId = release ?? string.Empty, DatasetSnapshotId = snapshot ?? string.Empty, Action = accion, Status = estado, Message = mensaje, Reason = motivo };
        return Json.SerializeToUtf8(resp);
    }

    /// <summary><see cref="TextWriter"/> que convierte cada linea en un marco de registro hacia la interfaz.</summary>
    private sealed class RegistroPorMarcos(Stream flujo) : TextWriter
    {
        private readonly StringBuilder _linea = new();
        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char valor)
        {
            lock (_linea)
            {
                if (valor == '\n') { Emitir(); return; }
                if (valor != '\r') _linea.Append(valor);
            }
        }

        public override void Write(string? valor) { if (valor is null) return; foreach (var c in valor) Write(c); }
        public override void Flush() { lock (_linea) { if (_linea.Length > 0) Emitir(); } }

        private void Emitir()
        {
            var texto = _linea.ToString();
            _linea.Clear();
            try { Marcos.EscribirTexto(flujo, TipoMarco.Registro, texto); }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException) { }
        }
    }
}
