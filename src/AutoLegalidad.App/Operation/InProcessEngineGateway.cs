// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using AutoLegalidad.Contracts;
using AutoLegalidad.Engine.Catalog;
using AutoLegalidad.Registro;
using AutoLegalidad.Worker;

namespace AutoLegalidad.Ui.Operation;

/// <summary>
/// <see cref="IEngineGateway"/> con el motor EN EL PROCESO: llama a <see cref="WorkerHost.Run"/> (el mismo código que el Worker de escritorio) sobre flujos en memoria, en un hilo propio
/// y con el contrato v2 tal cual (un JSON por solicitud, uno por respuesta; el diagnóstico del motor llega como <see cref="DiagnosticLine"/>). Android F1: es la base del servicio <c>:motor</c>
/// y, en el PC, sirve a las pruebas. El Worker de escritorio sigue siendo un proceso aparte (<see cref="WorkerGateway"/>).
/// <para>
/// Solo corre UNA solicitud a la vez (el motor guarda estado estático: <c>ParseSettings</c>, la caché ambiente). Cancelar es cooperativo; si el generador no devuelve el control,
/// la solicitud se da por cancelada tras <see cref="CancelGrace"/> pero el cerrojo no se suelta hasta que el hilo termine de verdad (nunca dos búsquedas a la vez).
/// En proceso no se puede matar un hilo (regla: nunca Thread.Abort): quien necesite cortar de verdad recicla el proceso entero, como hace Android con <c>:motor</c>.
/// </para>
/// </summary>
public sealed class InProcessEngineGateway(string? catalogPath, string? solutionsPath = null) : IEngineGateway
{
    /// <summary>Margen que se concede, tras cancelar, a que el generador devuelva el control antes de dar la solicitud por cancelada.</summary>
    public static readonly TimeSpan CancelGrace = TimeSpan.FromSeconds(2);

    private readonly SemaphoreSlim _gate = new(1, 1);

    public event Action<string>? DiagnosticLine;

    public string? UnavailableReason
        => string.IsNullOrEmpty(catalogPath) || !File.Exists(catalogPath) ? Textos.CatalogoNoEncontrado : null;

    public Task<WorkerManifest?> GetManifestAsync(CancellationToken cancellation)
    {
        if (UnavailableReason is not null) return Task.FromResult<WorkerManifest?>(null);
        return Task.Run<WorkerManifest?>(() =>
        {
            try
            {
                using var catalog = new CatalogReader(catalogPath!);
                return WorkerHost.BuildManifest(catalog);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                RegistroFallos.Error(AppIdioma.T("No se pudo construir el manifiesto del motor en proceso.", "Couldn’t build the in-process engine manifest."), ex, "InProcessEngineGateway.GetManifestAsync");
                return null;
            }
        }, cancellation);
    }

    public async Task<LegalizeResponse> LegalizeAsync(LegalizeRequest request, CancellationToken cancellation)
    {
        var clock = Stopwatch.StartNew();
        if (UnavailableReason is { } why) return Failure(request, ResponseStatus.InternalError, why, why, clock);
        try { await _gate.WaitAsync(cancellation).ConfigureAwait(false); }
        catch (OperationCanceledException) { return Failure(request, ResponseStatus.Cancelled, AppIdioma.T("Operación cancelada.", "Operation canceled."), null, clock); }

        var stdin = new MemoryStream(Json.SerializeToUtf8(request));
        var stdout = new MemoryStream();
        var log = new LineEventWriter(line => DiagnosticLine?.Invoke(line));
        // El cerrojo se suelta cuando el hilo del motor termina de verdad, aunque esta llamada ya haya devuelto "cancelado".
        var run = Task.Run(() => WorkerHost.Run(stdin, stdout, catalogPath!, log, solutionsPath, cancellation, hardStop: static _ => { }));
        _ = run.ContinueWith(_ => _gate.Release(), TaskScheduler.Default);

        int exit;
        try
        {
            var winner = await Task.WhenAny(run, Task.Delay(Timeout.Infinite, cancellation)).ConfigureAwait(false);
            if (winner != run)
            {
                // Cancelada: se espera el margen a que el motor devuelva el control por su cuenta (lo normal) y, si no, se da por cancelada sin soltar el cerrojo.
                if (await Task.WhenAny(run, Task.Delay(CancelGrace)).ConfigureAwait(false) != run)
                {
                    RegistroFallos.Aviso(AppIdioma.T("El motor en proceso no devolvió el control tras cancelar; se da la solicitud por cancelada y queda un hilo abandonado.", "The in-process engine didn’t return control after canceling; the request is treated as canceled and a thread is left abandoned."), contexto: $"solicitud {request.RequestId}");
                    return Failure(request, ResponseStatus.Cancelled, AppIdioma.T("Operación cancelada.", "Operation canceled."), null, clock);
                }
            }
            exit = await run.ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            RegistroFallos.Error(AppIdioma.T("Excepción del motor en proceso.", "In-process engine exception."), ex, $"solicitud {request.RequestId}");
            return Failure(request, ResponseStatus.InternalError, AppIdioma.T("Error interno del motor.", "Internal engine error."), $"{ex.GetType().Name}: {ex.Message}", clock);
        }

        var text = Encoding.UTF8.GetString(stdout.ToArray()).Trim();
        if (text.Length == 0) return Failure(request, ResponseStatus.InternalError, AppIdioma.T("El motor terminó sin respuesta.", "The engine ended without a response."), AppIdioma.F("Código de salida {0}.", "Exit code {0}.", exit), clock);
        LegalizeResponse response;
        try { response = Json.Deserialize<LegalizeResponse>(text); }
        catch (JsonException ex) { return Failure(request, ResponseStatus.InternalError, AppIdioma.T("La respuesta del motor no cumple el contrato.", "The engine’s response doesn’t meet the contract."), ex.Message, clock); }
        var problems = ResponseValidator.Validate(response);
        if (problems.Count > 0) return Failure(request, ResponseStatus.InternalError, AppIdioma.T("La respuesta del motor es incoherente y se descarta.", "The engine’s response is inconsistent and is discarded."), string.Join("; ", problems), clock);
        if (!string.Equals(response.RequestId, request.RequestId, StringComparison.OrdinalIgnoreCase))
            return Failure(request, ResponseStatus.InternalError, AppIdioma.T("La respuesta no corresponde a esta solicitud.", "The response doesn’t correspond to this request."), $"requestId {response.RequestId}", clock);
        return response with { ElapsedMilliseconds = clock.ElapsedMilliseconds };
    }

    private static LegalizeResponse Failure(LegalizeRequest r, ResponseStatus status, string message, string? reason, Stopwatch clock) => new()
    {
        ProtocolVersion = Protocol.Version,
        RequestId = r.RequestId,
        ReleaseId = r.ReleaseId,
        DatasetSnapshotId = r.DatasetSnapshotId,
        Action = r.Action,
        Status = status,
        Message = message,
        Reason = reason,
        ElapsedMilliseconds = clock.ElapsedMilliseconds,
    };

    /// <summary><see cref="TextWriter"/> que convierte cada línea escrita (stderr del Worker: <c>POKEKEEP-FASE</c>, <c>POKEKEEP-PROGRESO</c>) en un evento.</summary>
    private sealed class LineEventWriter(Action<string> onLine) : TextWriter
    {
        private readonly StringBuilder _line = new();
        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value)
        {
            lock (_line)
            {
                if (value == '\n') { Emit(); return; }
                if (value != '\r') _line.Append(value);
            }
        }

        public override void Write(string? value)
        {
            if (value is null) return;
            foreach (var c in value) Write(c);
        }

        public override void Flush() { lock (_line) { if (_line.Length > 0) Emit(); } }

        private void Emit()
        {
            var text = _line.ToString();
            _line.Clear();
            try { onLine(text); } catch (Exception ex) when (ex is not OutOfMemoryException) { /* un observador defectuoso nunca rompe la solicitud */ }
        }
    }
}
