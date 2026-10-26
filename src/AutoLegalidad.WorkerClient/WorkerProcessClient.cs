// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using AutoLegalidad.Contracts;
using AutoLegalidad.Registro;

namespace AutoLegalidad.WorkerClient;

public interface ILegalizationClient
{
    Task<LegalizeResponse> LegalizeAsync(LegalizeRequest request, CancellationToken cancellation);
}

/// <summary>
/// Cliente del Worker para la UI (Hito 4): lanza el proceso OCULTO (<c>CreateNoWindow</c>, sin shell), le manda un JSON por stdin y lee otro por stdout.
/// La cancelacion y el plazo terminan el ARBOL de procesos (<c>Kill(entireProcessTree: true)</c>); nunca se usa Thread.Abort. Un resultado que no cumple el
/// contrato (candidato en un fallo, JSON roto, proceso caido) se convierte en <see cref="ResponseStatus.InternalError"/> y se descarta.
/// </summary>
public sealed class WorkerProcessClient(string workerPath, string? dbPath = null, TimeSpan? killGrace = null, Action<string>? stderrSink = null, Action<int>? onProcessStarted = null) : ILegalizationClient
{
    private readonly TimeSpan _grace = killGrace ?? TimeSpan.FromSeconds(4);

    /// <summary>Intentos de un Worker que MUERE sin dar respuesta (ADR 0040): el original y, como mucho, UNO mas. Un resultado de dominio (incluido «sin solucion») nunca se reintenta.</summary>
    public const int MaxAttempts = 2;

    /// <summary>Un Worker que muere cuando ya queda menos plazo que esto no se reintenta (no habria tiempo de buscar nada).</summary>
    private static readonly TimeSpan MinTimeForRetry = TimeSpan.FromSeconds(5);

    /// <summary>Lo observado de UN intento: la respuesta, el codigo de salida, la cola de stderr y si el proceso murio por su cuenta sin responder (candidato a reintento).</summary>
    private sealed record Attempt(LegalizeResponse Response, int? ExitCode, string StderrTail, bool DiedWithoutResponse);

    public async Task<LegalizeResponse> LegalizeAsync(LegalizeRequest request, CancellationToken cancellation)
    {
        var clock = Stopwatch.StartNew();
        var attempt = await RunOnceAsync(request, cancellation, 1).ConfigureAwait(false);
        var deadline = request.DeadlineUtc ?? DateTimeOffset.UtcNow.AddSeconds(60);
        // Reintento acotado (el Hito 6 vio 3 Workers muertos con codigo -1 sin causa conocida): SOLO si el proceso murio solo sin respuesta, no se cancelo y queda tiempo. El segundo intento pasa por
        // el mismo camino y la misma validacion; nunca se acepta nada por haber reintentado: si vuelve a fallar, se devuelve el fallo.
        if (attempt.DiedWithoutResponse && !cancellation.IsCancellationRequested && attempt.ExitCode != Protocol.ExitInfrastructure && deadline - DateTimeOffset.UtcNow > MinTimeForRetry)
        {
            RegistroFallos.Aviso($"El Worker murió sin responder (código de salida {attempt.ExitCode}); se reintenta una vez.", contexto: Contexto(request));
            var second = await RunOnceAsync(request, cancellation, 2).ConfigureAwait(false);
            if (second.DiedWithoutResponse)
                RegistroFallos.Error($"El Worker volvió a morir sin responder tras un reintento (códigos {attempt.ExitCode} y {second.ExitCode}).", contexto: Contexto(request));
            else
                RegistroFallos.Aviso($"El reintento obtuvo respuesta ({second.Response.Status}); el primer intento había muerto con código {attempt.ExitCode}.", contexto: Contexto(request));
            attempt = second;
        }
        return attempt.Response with { ElapsedMilliseconds = clock.ElapsedMilliseconds };
    }

    /// <summary>Contexto de una entrada del registro: operacion, juego de destino y casilla. NUNCA OT, TID/SID ni nombre de entrenador (ADR 0040).</summary>
    private static string Contexto(LegalizeRequest r)
        => $"solicitud {r.RequestId} · acción {r.Action} · destino {r.Target.GameVersion} ({r.EntityRuntimeType}, {r.Target.SlotType})" + (r.Selection is { } s ? $" · {s.Kind} caja {s.Box} casilla {s.Slot}" : "");

    private async Task<Attempt> RunOnceAsync(LegalizeRequest request, CancellationToken cancellation, int attemptNumber)
    {
        var clock = Stopwatch.StartNew();
        var psi = new ProcessStartInfo(workerPath)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardErrorEncoding = new UTF8Encoding(false),
            WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(workerPath)) ?? Environment.CurrentDirectory,
        };
        if (dbPath is not null)
        {
            psi.ArgumentList.Add("--db");
            psi.ArgumentList.Add(dbPath);
        }

        using var process = new Process { StartInfo = psi };
        var stdout = new StringBuilder();
        var tcsOut = new TaskCompletionSource();
        // Cola de stderr (ultimas lineas, acotada) para el registro y para el motivo del fallo; el progreso del Worker (POKEKEEP-PROGRESO/FASE) no cuenta.
        var tail = new Queue<string>();
        process.OutputDataReceived += (_, e) => { if (e.Data is null) tcsOut.TrySetResult(); else stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            stderrSink?.Invoke(e.Data);
            if (e.Data.StartsWith("POKEKEEP-PROGRESO", StringComparison.Ordinal) || e.Data.StartsWith("POKEKEEP-FASE", StringComparison.Ordinal)) return;
            lock (tail) { tail.Enqueue(e.Data); while (tail.Count > 25) tail.Dequeue(); }
        };
        string Tail()
        {
            string[] lines;
            lock (tail) lines = [.. tail];
            var text = string.Join(" // ", lines);
            return text.Length > 1500 ? text[^1500..] : text;
        }

        LegalizeResponse Fail(ResponseStatus status, string message, string? reason, int? exit, Exception? ex = null)
        {
            var r = Failure(request, status, message, reason, clock);
            var level = status is ResponseStatus.Cancelled ? Nivel.Info : status is ResponseStatus.Timeout ? Nivel.Aviso : Nivel.Error;
            var err = Tail();
            RegistroFallos.Escribir(level, $"Worker (intento {attemptNumber}/{MaxAttempts}): {message} Estado {status}. Código de salida: {exit?.ToString() ?? "n/d"}. Duración {clock.Elapsed.TotalSeconds:0.0} s. {reason}",
                ex, Contexto(request) + (err.Length > 0 ? " · stderr: " + err : ""));
            return r;
        }

        try
        {
            process.Start();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            return new Attempt(Fail(ResponseStatus.InternalError, "No se pudo lanzar el Worker.", ex.Message, null, ex), null, "", false);
        }
        // Aviso del PID del Worker lanzado (pruebas y diagnostico: permite comprobar SOLO los procesos propios, sin mirar los de otras sesiones).
        try { onProcessStarted?.Invoke(process.Id); } catch (Exception ex) when (ex is not OutOfMemoryException) { /* un observador defectuoso nunca rompe la solicitud */ }
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Un documento por solicitud, UTF-8 sin BOM, y se cierra stdin.
        var bytes = Json.SerializeToUtf8(request);
        try
        {
            await process.StandardInput.BaseStream.WriteAsync(bytes, cancellation).ConfigureAwait(false);
            process.StandardInput.Close();
        }
        catch (IOException)
        {
            // El Worker murio antes de leer la solicitud: se trata abajo como «murio sin respuesta» con su codigo de salida.
        }

        var deadline = request.DeadlineUtc ?? DateTimeOffset.UtcNow.AddSeconds(60);
        var wait = deadline - DateTimeOffset.UtcNow + _grace;
        if (wait < TimeSpan.Zero) wait = TimeSpan.Zero;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        linked.CancelAfter(wait);
        try
        {
            await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
            await tcsOut.Task.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            KillTree(process);
            return cancellation.IsCancellationRequested
                ? new Attempt(Fail(ResponseStatus.Cancelled, "Operación cancelada.", null, null), null, Tail(), false)
                : new Attempt(Fail(ResponseStatus.Timeout, "El Worker no respondió a tiempo y se terminó su árbol de procesos.", "Esto NO prueba que no exista solucion.", null), null, Tail(), false);
        }
        catch (TimeoutException)
        {
            // el proceso termino pero stdout no se cerro: se sigue con lo leido
        }

        var exit = process.HasExited ? process.ExitCode : (int?)null;
        var text = stdout.ToString().Trim();
        if (text.Length == 0)
        {
            var err = Tail();
            var reason = $"Código de salida {exit}." + (err.Length > 0 ? " Salida de error del Worker: " + err : "");
            return new Attempt(Fail(ResponseStatus.InternalError, "El Worker terminó sin respuesta.", reason, exit), exit, err, true);
        }
        LegalizeResponse response;
        try
        {
            response = Json.Deserialize<LegalizeResponse>(text);
        }
        catch (JsonException ex)
        {
            return new Attempt(Fail(ResponseStatus.InternalError, "La respuesta del Worker no cumple el contrato.", ex.Message, exit, ex), exit, Tail(), false);
        }
        var problems = ResponseValidator.Validate(response);
        if (problems.Count > 0)
            return new Attempt(Fail(ResponseStatus.InternalError, "La respuesta del Worker es incoherente y se descarta.", string.Join("; ", problems), exit), exit, Tail(), false);
        if (!string.Equals(response.RequestId, request.RequestId, StringComparison.OrdinalIgnoreCase))
            return new Attempt(Fail(ResponseStatus.InternalError, "La respuesta no corresponde a esta solicitud.", $"requestId {response.RequestId}", exit), exit, Tail(), false);
        // Respuesta valida pero el proceso no termino con 0: se anota (manda el estado de dominio; el codigo es diagnostico).
        if (exit is { } code && code != Protocol.ExitOk)
        {
            var err = Tail();
            RegistroFallos.Aviso($"Worker (intento {attemptNumber}): respuesta {response.Status} pero código de salida {code}.", contexto: Contexto(request) + (err.Length > 0 ? " · stderr: " + err : ""));
        }
        return new Attempt(response, exit, Tail(), false);
    }

    /// <summary>Pide al Worker su manifiesto de versiones (para comprobar la pareja antes de generar).</summary>
    public async Task<WorkerManifest?> GetManifestAsync(CancellationToken cancellation)
    {
        var psi = new ProcessStartInfo(workerPath)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            StandardOutputEncoding = new UTF8Encoding(false),
        };
        psi.ArgumentList.Add(Protocol.ManifestArgument);
        if (dbPath is not null) { psi.ArgumentList.Add("--db"); psi.ArgumentList.Add(dbPath); }
        using var p = Process.Start(psi);
        if (p is null) return null;
        try { onProcessStarted?.Invoke(p.Id); } catch (Exception ex) when (ex is not OutOfMemoryException) { /* un observador defectuoso nunca rompe la solicitud */ }
        try
        {
            var text = await p.StandardOutput.ReadToEndAsync(cancellation).ConfigureAwait(false);
            await p.WaitForExitAsync(cancellation).ConfigureAwait(false);
            return p.ExitCode == Protocol.ExitOk ? Json.Deserialize<WorkerManifest>(text) : null;
        }
        finally
        {
            // 0.6.6 (B2): cancelar (o cualquier excepcion) no deja un Worker huerfano; si ya termino, no hace nada.
            KillTree(p);
        }
    }

    private static void KillTree(Process p)
    {
        try { if (!p.HasExited) p.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
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
}
