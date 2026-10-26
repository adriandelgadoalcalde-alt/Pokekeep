// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Registro;
using System.Diagnostics;
using AutoLegalidad.Contracts;

namespace AutoLegalidad.Ui.Bulk;

/// <summary>Un intento de un trabajo: una solicitud al motor (con su origen, si lo hay). El plazo lo pone el <see cref="BulkRunner"/>.</summary>
public sealed record BulkAttemptSpec(string? OriginKey, Func<DateTimeOffset, LegalizeRequest> Build);

/// <summary>
/// Un trabajo del lote (un Pokémon): uno o varios intentos en orden de prioridad (en «Completar el juego», los hasta 4 orígenes posibles de la especie-forma;
/// en «Edición masiva», uno solo). Los intentos siguientes solo se prueban si el anterior terminó sin un resultado utilizable.
/// </summary>
public sealed class BulkJob
{
    public required int Id { get; init; }
    /// <summary>Texto para el progreso («Pikachu»).</summary>
    public required string Label { get; init; }
    public required IReadOnlyList<BulkAttemptSpec> Attempts { get; init; }
    /// <summary>
    /// Comprobación opcional de una respuesta que lleva candidato (p. ej. «es la especie y la forma pedidas»). Si devuelve false, el intento NO cuenta como utilizable y se prueba el siguiente origen
    /// (el motor puede devolver con éxito otra forma cuando el origen no admite la pedida; con otro origen sí puede darla).
    /// </summary>
    public Func<LegalizeResponse, bool>? Accepts { get; init; }
}

public sealed record BulkAttempt(string? OriginKey, LegalizeResponse Response, TimeSpan Elapsed);

public enum BulkRunState
{
    /// <summary>Se ejecutó (con uno o varios intentos).</summary>
    Done,
    /// <summary>No llegó a ejecutarse porque el usuario canceló o el lote se detuvo antes.</summary>
    NotRun,
    /// <summary>Se interrumpió a mitad por la cancelación del usuario.</summary>
    Cancelled,
}

public sealed class BulkJobResult(BulkJob job, BulkRunState state, IReadOnlyList<BulkAttempt> attempts)
{
    public BulkJob Job { get; } = job;
    public BulkRunState State { get; } = state;
    public IReadOnlyList<BulkAttempt> Attempts { get; } = attempts;
    /// <summary>Respuesta que se queda: la primera con resultado utilizable; si ninguna, la última; si hubo un agotamiento de tiempo y ningún éxito, se prefiere el tiempo agotado (regla 5: nunca se convierte en «imposible»).</summary>
    public LegalizeResponse? Final { get; init; }
    /// <summary>Intento cuya respuesta es <see cref="Final"/>.</summary>
    public BulkAttempt? FinalAttempt => Final is null ? null : Attempts.LastOrDefault(a => ReferenceEquals(a.Response, Final));
    public TimeSpan Elapsed => TimeSpan.FromTicks(Attempts.Sum(a => a.Elapsed.Ticks));
    /// <summary>Algún intento se quedó sin tiempo.</summary>
    public bool AnyTimedOut => Attempts.Any(a => a.Response.Status == ResponseStatus.Timeout);
}

/// <summary>Estado del lote para la barra de progreso.</summary>
public sealed record BulkProgress(int Done, int Total, int InFlight, string Current, TimeSpan Elapsed, TimeSpan? Remaining, int Workers)
{
    public double Fraction => Total == 0 ? 1 : (double)Done / Total;
}

public sealed record BulkRunnerOptions
{
    /// <summary>Workers en paralelo (un proceso por solicitud, ADR 0011).</summary>
    public int Workers { get; init; } = DefaultWorkers();
    /// <summary>Plazo por solicitud (se escribe en <c>DeadlineUtc</c>): el cliente mata el árbol del Worker poco después.</summary>
    public TimeSpan PerCaseTimeout { get; init; } = TimeSpan.FromSeconds(45);
    /// <summary>Margen tras el plazo antes de que el propio vigilante del lote mate el proceso (diseño 3.5: «plazo + 5 s»).</summary>
    public TimeSpan WatchdogGrace { get; init; } = TimeSpan.FromSeconds(5);
    /// <summary>Máximo de agotamientos de tiempo por Pokémon antes de rendirse (reintento único con el siguiente origen, diseño 1.8).</summary>
    public int MaxTimeoutsPerJob { get; init; } = 2;

    /// <summary>
    /// Politica del numero de Workers por defecto (Android F1, ADR 0054: configurable). Escritorio, la de siempre: la mitad de los nucleos, entre 1 y 4 procesos, uno por solicitud (ADR 0011).
    /// Android la sustituye por 1: un unico proceso <c>:motor</c> que atiende una solicitud a la vez y reparte el trabajo en hilos internos (<c>OptimizerSwitches.SeedSearchThreads</c>).
    /// </summary>
    public static Func<int> DefaultWorkersPolicy { get; set; } = static () => Math.Clamp(Environment.ProcessorCount / 2, 1, 4);

    public static int DefaultWorkers() => Math.Max(1, DefaultWorkersPolicy());

    public static readonly ISet<ResponseStatus> RetryableStatuses = new HashSet<ResponseStatus>
    {
        ResponseStatus.NoSolution, ResponseStatus.UnsupportedData, ResponseStatus.Timeout, ResponseStatus.ValidationFailed, ResponseStatus.InternalError,
    };
}

/// <summary>
/// Ejecuta los trabajos de un lote con N Workers en paralelo (diseño §1.8). Un proceso por solicitud; plazo por caso con vigilante propio; reintento con el siguiente origen;
/// Cancelar mata los procesos en curso y no lanza más; un fallo de un Pokémon NUNCA para el lote. No escribe en la partida (eso es <see cref="BulkTransaction"/>).
/// </summary>
public sealed class BulkRunner
{
    private readonly IEngineGateway _engine;
    private readonly BulkRunnerOptions _options;
    private readonly SynchronizationContext? _ui;

    public BulkRunner(IEngineGateway engine, BulkRunnerOptions? options = null, SynchronizationContext? ui = null)
    {
        _engine = engine;
        _options = options ?? new BulkRunnerOptions();
        _ui = ui ?? SynchronizationContext.Current;
    }

    /// <summary>Un Pokémon terminó (en el contexto de la interfaz, si lo había al crear el runner).</summary>
    public event Action<BulkJobResult>? ItemCompleted;
    public event Action<BulkProgress>? ProgressChanged;

    /// <summary>Motivo por el que el lote se detuvo solo (p. ej. <c>VersionMismatch</c>), o null.</summary>
    public string? StoppedReason { get; private set; }

    public async Task<IReadOnlyList<BulkJobResult>> RunAsync(IReadOnlyList<BulkJob> jobs, CancellationToken cancellation)
    {
        var workers = Math.Clamp(_options.Workers, 1, 8);
        var results = new BulkJobResult?[jobs.Count];
        var clock = Stopwatch.StartNew();
        var done = 0;
        var inFlight = 0;
        long elapsedTicks = 0;
        var gate = new object();
        StoppedReason = null;
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        var queue = new Queue<int>(Enumerable.Range(0, jobs.Count));

        void Notify(string current)
        {
            BulkProgress p;
            lock (gate)
            {
                TimeSpan? remaining = null;
                if (done > 0)
                {
                    var avg = TimeSpan.FromTicks(elapsedTicks / done);
                    remaining = TimeSpan.FromTicks(avg.Ticks * (jobs.Count - done) / workers);
                }
                p = new BulkProgress(done, jobs.Count, inFlight, current, clock.Elapsed, remaining, workers);
            }
            Post(() => ProgressChanged?.Invoke(p));
        }

        async Task WorkerLoop()
        {
            while (!stop.IsCancellationRequested)
            {
                int index;
                lock (gate)
                {
                    if (queue.Count == 0) return;
                    index = queue.Dequeue();
                    inFlight++;
                }
                var job = jobs[index];
                Notify(job.Label);
                BulkJobResult result;
                try
                {
                    result = await RunJobAsync(job, stop).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    RegistroFallos.Error(AppIdioma.F("Excepción al procesar el trabajo {0} de una operación masiva.", "Exception while processing job {0} of a bulk operation.", job.Id), ex, "BulkRunner");
                    result = Failed(job, ex);
                }
                lock (gate)
                {
                    inFlight--;
                    results[index] = result;
                    if (result.State == BulkRunState.Done) { done++; elapsedTicks += result.Elapsed.Ticks; }
                    if (result.Final?.Status == ResponseStatus.VersionMismatch && StoppedReason is null)
                    {
                        StoppedReason = result.Final.Message;
                        stop.Cancel();   // diseño 3.6: la pareja de versiones cambió a mitad: se detiene el lote
                    }
                }
                Post(() => ItemCompleted?.Invoke(result));
                Notify(job.Label);
            }
        }

        var loops = Enumerable.Range(0, workers).Select(_ => Task.Run(WorkerLoop)).ToArray();
        await Task.WhenAll(loops).ConfigureAwait(false);
        return [.. results.Select((r, i) => r ?? new BulkJobResult(jobs[i], BulkRunState.NotRun, []))];
    }

    private async Task<BulkJobResult> RunJobAsync(BulkJob job, CancellationTokenSource stop)
    {
        var attempts = new List<BulkAttempt>();
        var timeouts = 0;
        LegalizeResponse? usable = null;
        for (var i = 0; i < job.Attempts.Count; i++)
        {
            if (stop.IsCancellationRequested)
                return new BulkJobResult(job, attempts.Count == 0 ? BulkRunState.NotRun : BulkRunState.Cancelled, attempts) { Final = attempts.LastOrDefault()?.Response };
            var spec = job.Attempts[i];
            var sw = Stopwatch.StartNew();
            var response = await CallAsync(spec, stop.Token).ConfigureAwait(false);
            attempts.Add(new BulkAttempt(spec.OriginKey, response, sw.Elapsed));
            if (stop.IsCancellationRequested && response.Status == ResponseStatus.Cancelled)
                return new BulkJobResult(job, BulkRunState.Cancelled, attempts) { Final = response };
            if (!BulkRunnerOptions.RetryableStatuses.Contains(response.Status))
            {
                if (job.Accepts is { } accepts && ResponseValidator.CarriesCandidate(response.Status) && response.Status != ResponseStatus.AlreadyLegal && !accepts(response)) continue;
                usable = response;
                break;
            }
            if (response.Status == ResponseStatus.Timeout) timeouts++;
            if (timeouts >= _options.MaxTimeoutsPerJob) break;
        }
        // Final: la primera respuesta utilizable; si no hay, el último agotamiento de tiempo (un Timeout no prueba imposibilidad: no lo tapa un «sin solución» de otro origen) o, si no, la última.
        var final = usable
            ?? attempts.LastOrDefault(a => a.Response.Status == ResponseStatus.Timeout)?.Response
            ?? attempts.LastOrDefault()?.Response;
        return new BulkJobResult(job, BulkRunState.Done, attempts) { Final = final };
    }

    /// <summary>Una llamada al motor con plazo y vigilante: si no responde en plazo + margen, se cancela (el cliente mata el árbol de procesos) y se da por «Tiempo agotado».</summary>
    private async Task<LegalizeResponse> CallAsync(BulkAttemptSpec spec, CancellationToken userStop)
    {
        var deadline = DateTimeOffset.UtcNow + _options.PerCaseTimeout;
        var request = spec.Build(deadline);
        using var watchdog = CancellationTokenSource.CreateLinkedTokenSource(userStop);
        watchdog.CancelAfter(_options.PerCaseTimeout + _options.WatchdogGrace);
        try
        {
            var call = Task.Run(() => _engine.LegalizeAsync(request, watchdog.Token));
            var response = await call.ConfigureAwait(false);
            if (response.Status == ResponseStatus.Cancelled && watchdog.IsCancellationRequested && !userStop.IsCancellationRequested)
                return Timeout(request, AppIdioma.T("El vigilante del lote cortó la solicitud.", "The batch watchdog cut the request."));
            return response;
        }
        catch (OperationCanceledException) when (!userStop.IsCancellationRequested && watchdog.IsCancellationRequested)
        {
            return Timeout(request, AppIdioma.T("El vigilante del lote cortó la solicitud.", "The batch watchdog cut the request."));
        }
        catch (OperationCanceledException)
        {
            return Plain(request, ResponseStatus.Cancelled, AppIdioma.T("Operación cancelada.", "Operation canceled."), null);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            RegistroFallos.Error(AppIdioma.T("El motor falló en una operación masiva.", "The engine failed in a bulk operation."), ex, "BulkRunner.RunJobAsync");
            return Plain(request, ResponseStatus.InternalError, AppIdioma.T("El motor falló.", "The engine failed."), $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static LegalizeResponse Timeout(LegalizeRequest r, string why)
        => Plain(r, ResponseStatus.Timeout, AppIdioma.T("Se agotó el tiempo antes de terminar la búsqueda.", "Time ran out before the search finished."), why + AppIdioma.T(" Esto NO prueba que no exista solución.", " This does NOT prove that no solution exists."));

    private static LegalizeResponse Plain(LegalizeRequest r, ResponseStatus status, string message, string? reason) => new()
    {
        ProtocolVersion = Protocol.Version, RequestId = r.RequestId, Action = r.Action, Status = status, Message = message, Reason = reason,
    };

    private static BulkJobResult Failed(BulkJob job, Exception ex)
        => new(job, BulkRunState.Done, [])
        {
            Final = new LegalizeResponse
            {
                ProtocolVersion = Protocol.Version, RequestId = Guid.NewGuid().ToString(), Action = LegalizeAction.Legalize, Status = ResponseStatus.InternalError,
                Message = AppIdioma.T("Error inesperado al procesar este Pokémon.", "Unexpected error while processing this Pokémon."), Reason = $"{ex.GetType().Name}: {ex.Message}",
            },
        };

    private void Post(Action a)
    {
        if (_ui is null) a();
        else _ui.Post(_ => a(), null);
    }
}
