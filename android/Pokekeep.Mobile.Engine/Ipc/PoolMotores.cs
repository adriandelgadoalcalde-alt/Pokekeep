// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Contracts;
using AutoLegalidad.Ui;

namespace Pokekeep.Mobile.Engine.Ipc;

/// <summary>
/// Motores que se pueden matar de golpe: el Cancelar duro de una operación larga (F9X-04, ADR 0070) mata los procesos <c>:motorN</c> que sigan vivos tras la gracia del cancelar cooperativo, y la siguiente
/// solicitud arranca un proceso limpio (PID reciclado).
/// </summary>
public interface IMotoresMatables
{
    /// <summary>PID de cada motor conectado ahora (0 si ese motor no tiene proceso).</summary>
    IReadOnlyList<int> Pids { get; }

    /// <summary>Mata todos los procesos del motor que vivan. Devuelve cuántos se mataron.</summary>
    int MatarTodos();
}

/// <summary>
/// Pool de N motores (F9X-01, ADR 0070): un <see cref="IEngineGateway"/> que reparte las solicitudes entre N pasarelas, cada una con su proceso <c>:motorN</c>, su semáforo y su lanzador. Una solicitud va
/// siempre a un motor LIBRE (nunca se encola detrás de otra en un semáforo ajeno, que gastaría su plazo esperando: hallazgo 1 de la F9-0). Con N = 1 es exactamente la pasarela única de la F1.
/// El número de motores lo fija la medida en emulador; hasta entonces el producto usa 1 (<c>ProcesosMaximos</c>).
/// </summary>
public sealed class PoolMotores : IEngineGateway, IMotoresMatables, IDisposable
{
    private readonly IReadOnlyList<IEngineGateway> _motores;
    private readonly SemaphoreSlim _libres;
    private readonly bool[] _ocupado;
    private readonly object _cerrojo = new();
    private bool _liberado;

    public PoolMotores(IReadOnlyList<IEngineGateway> motores)
    {
        if (motores.Count == 0) throw new ArgumentException("El pool necesita al menos un motor.", nameof(motores));
        _motores = motores;
        _libres = new SemaphoreSlim(motores.Count, motores.Count);
        _ocupado = new bool[motores.Count];
        foreach (var m in motores) m.DiagnosticLine += Reenviar;
    }

    /// <summary>Motores del pool (procesos que pueden trabajar a la vez).</summary>
    public int Tamano => _motores.Count;

    public IReadOnlyList<IEngineGateway> Motores => _motores;

    public event Action<string>? DiagnosticLine;

    private void Reenviar(string linea) => DiagnosticLine?.Invoke(linea);

    /// <summary>El pool está disponible si lo está el primer motor (todos comparten catálogo, caché y versión).</summary>
    public string? UnavailableReason => _motores[0].UnavailableReason;

    public Task<WorkerManifest?> GetManifestAsync(CancellationToken cancellation) => _motores[0].GetManifestAsync(cancellation);

    public async Task<LegalizeResponse> LegalizeAsync(LegalizeRequest request, CancellationToken cancellation)
    {
        try { await _libres.WaitAsync(cancellation).ConfigureAwait(false); }
        catch (OperationCanceledException)
        {
            return new LegalizeResponse
            {
                ProtocolVersion = Protocol.Version, RequestId = request.RequestId, ReleaseId = request.ReleaseId, DatasetSnapshotId = request.DatasetSnapshotId,
                Action = request.Action, Status = ResponseStatus.Cancelled, Message = "Operación cancelada.",
            };
        }
        int i;
        lock (_cerrojo)
        {
            i = Array.IndexOf(_ocupado, false);
            _ocupado[i] = true;   // el semáforo garantiza que hay uno libre
        }
        try { return await _motores[i].LegalizeAsync(request, cancellation).ConfigureAwait(false); }
        finally
        {
            lock (_cerrojo) _ocupado[i] = false;
            _libres.Release();
        }
    }

    /// <summary>Se abrió otra partida o se recreó la actividad (regla 7): todas las pasarelas descartan las respuestas anteriores.</summary>
    public void NuevaGeneracion() { foreach (var m in _motores.OfType<AndroidEngineGateway>()) m.NuevaGeneracion(); }

    public IReadOnlyList<int> Pids => [.. _motores.Select(m => m is IMotoresMatables mm ? mm.Pids.FirstOrDefault() : 0)];

    public int MatarTodos() => _motores.OfType<IMotoresMatables>().Sum(m => m.MatarTodos());

    public void Dispose()
    {
        if (_liberado) return;
        _liberado = true;
        foreach (var m in _motores)
        {
            m.DiagnosticLine -= Reenviar;
            (m as IDisposable)?.Dispose();
        }
    }
}
