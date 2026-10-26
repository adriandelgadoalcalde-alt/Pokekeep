// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Contracts;
using AutoLegalidad.WorkerClient;

namespace AutoLegalidad.Ui.Operation;

/// <summary>
/// Acceso al motor real: el Worker es un proceso OCULTO por solicitud (ADR 0011). Cancelar o vencer el plazo mata su árbol de procesos; un Worker
/// colgado o caído se convierte en un estado honesto (<c>Timeout</c> / <c>InternalError</c>) sin bloquear nunca la interfaz.
/// </summary>
public sealed class WorkerGateway : IEngineGateway
{
    private readonly string? _workerPath;
    private readonly string? _catalogPath;
    private readonly TimeSpan? _killGrace;
    private WorkerProcessClient? _client;

    public WorkerGateway(string? workerPath, string? catalogPath, TimeSpan? killGrace = null)
    {
        _workerPath = workerPath;
        _catalogPath = catalogPath;
        _killGrace = killGrace;
    }

    public event Action<string>? DiagnosticLine;

    public string? UnavailableReason
    {
        get
        {
            if (Portable.PortableMode.IntegrityProblem is { } damaged) return damaged;   // Hito 6: paquete portable alterado o con otro PKHeX.Core
            if (string.IsNullOrEmpty(_workerPath) || !File.Exists(_workerPath)) return Textos.MotorNoEncontrado;
            if (string.IsNullOrEmpty(_catalogPath) || !File.Exists(_catalogPath)) return Textos.CatalogoNoEncontrado;
            return null;
        }
    }

    private WorkerProcessClient Client => _client ??= new WorkerProcessClient(_workerPath!, _catalogPath, _killGrace, line => DiagnosticLine?.Invoke(line));

    public Task<WorkerManifest?> GetManifestAsync(CancellationToken cancellation)
        => UnavailableReason is not null ? Task.FromResult<WorkerManifest?>(null) : Client.GetManifestAsync(cancellation);

    public async Task<LegalizeResponse> LegalizeAsync(LegalizeRequest request, CancellationToken cancellation)
    {
        if (UnavailableReason is { } why)
            return new LegalizeResponse { ProtocolVersion = Protocol.Version, RequestId = request.RequestId, Action = request.Action, Status = ResponseStatus.InternalError, Message = why, Reason = why };
        return await Client.LegalizeAsync(request, cancellation).ConfigureAwait(false);
    }
}
