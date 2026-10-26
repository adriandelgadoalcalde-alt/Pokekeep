// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Registro;
using System.ComponentModel;
using System.Diagnostics;
using AutoLegalidad.Contracts;
using AutoLegalidad.Ui.Library;
using AutoLegalidad.Ui.Operation;
using PKHeX.Core;

namespace AutoLegalidad.Ui.Bulk;

public enum BulkKind
{
    LivingDex,
    Edit,
}

/// <summary>Cómo terminó una operación masiva.</summary>
public sealed record BulkOutcome
{
    public required BulkKind Kind { get; init; }
    /// <summary><see cref="OutcomeKind.Applied"/> si se escribió algo; <see cref="OutcomeKind.Cancelled"/> si se descartó todo; <see cref="OutcomeKind.TargetChanged"/>; <see cref="OutcomeKind.EngineFailure"/>; <see cref="OutcomeKind.NotApplied"/> si no había nada que hacer.</summary>
    public required OutcomeKind Result { get; init; }
    public required string Headline { get; init; }
    public string? Detail { get; init; }
    public required BulkReport Report { get; init; }
    /// <summary>Deshace TODO el lote de una vez (null si no se escribió nada).</summary>
    public BulkUndoToken? Undo { get; init; }
    public TimeSpan Elapsed { get; init; }
    public int Written { get; init; }
    /// <summary>Respuestas del motor por trabajo (Id del trabajo -> resultado), para estadísticas y el banco.</summary>
    public IReadOnlyList<BulkJobResult> Jobs { get; init; } = [];

    public bool Applied => Result is OutcomeKind.Applied or OutcomeKind.AppliedNeedsHome;

    public OperationResult ToOperationResult() => new()
    {
        Action = Kind == BulkKind.LivingDex ? PokekeepAction.CompleteGame : PokekeepAction.BulkEdit,
        Origin = OperationOrigin.Bulk,
        Kind = Result,
        Status = Result switch
        {
            OutcomeKind.Applied or OutcomeKind.AppliedNeedsHome => ResponseStatus.Success,
            OutcomeKind.Cancelled => ResponseStatus.Cancelled,
            OutcomeKind.TargetChanged => ResponseStatus.TargetChanged,
            OutcomeKind.EngineFailure => ResponseStatus.InternalError,
            _ => ResponseStatus.AlreadyOptimal,
        },
        Headline = Headline,
        Detail = Detail,
        Elapsed = Elapsed,
        Undo = Undo?.AsUndoToken(),
        Bulk = this,
    };
}

/// <summary>Progreso para la ventana de la operación: «Generando 213 de 866 · Pikachu · 4 en paralelo · quedan ~3 min», porcentaje y los últimos resultados.</summary>
public sealed record BulkProgressInfo(string Phase, BulkProgress? Progress, IReadOnlyList<string> Recent);

/// <summary>Plan de una edición masiva (nada se ha tocado todavía): qué Pokémon, con qué operaciones y qué se omite.</summary>
public sealed class BulkEditPlan
{
    public required BulkSelection Selection { get; init; }
    public required AdjustOptions Operations { get; init; }
    /// <summary>Hito 5d (ADR 0046): la opción «Preferir la versión de evento» está activada para esta edición.</summary>
    public bool PreferEvent { get; init; }
    /// <summary>Hito 5d: propuestas de la vista previa de «Preferir la versión de evento» (solo se aplican las marcadas). Null si la opción no se pidió.</summary>
    public EventSwapPlan? EventSwaps { get; init; }

    /// <summary>Hay algo que hacer: alguna operación, o alguna sustitución por su versión de evento aceptada.</summary>
    public bool HasWork => Operations.Any || EventSwaps?.Accepted.Count > 0;

    public int Count => Selection.Included.Count;
    public TimeSpan Estimate(int workers) => EstimateFor(Count, workers);

    /// <summary>Tiempo estimado de editar <paramref name="count"/> Pokémon con <paramref name="workers"/> procesos (1,2 s por Pokémon entre los procesos). Lo usa también el móvil, sin tener el plan delante (F9-0).</summary>
    public static TimeSpan EstimateFor(int count, int workers) => TimeSpan.FromSeconds(count * 1.2 / Math.Max(1, workers));
}

/// <summary>
/// Coordinador de las operaciones masivas (Hito 4b, ADR 0025). Una operación a la vez; el destino (la partida) se FIJA al empezar y la comparación con lo que hay decide <c>TargetChanged</c>;
/// el motor (un proceso por solicitud, N en paralelo) no escribe nada: la partida solo se toca al final, en UNA transacción con un único deshacer (<see cref="BulkTransaction"/>). Un fallo de un
/// Pokémon no aplica ese Pokémon y no aborta el resto; todo Pokémon se revalida con el Core, el destino y la ida y vuelta en el motor y otra vez aquí antes de escribirse.
/// </summary>
public sealed partial class BulkCoordinator : IDisposable
{
    private readonly IPokekeepHost _host;
    private readonly IEngineGateway _engine;
    private readonly Func<PokekeepSettings> _settings;
    private readonly Func<string>? _dataLanguage;
    private string DataLanguage => _dataLanguage?.Invoke() ?? "es";
    private readonly LibraryData? _library;
    private readonly PokekeepCoordinator? _single;
    private readonly TimeSpan _pollInterval;
    private readonly SynchronizationContext? _ui;
    private WorkerManifest? _manifest;
    private RunState? _current;

    public BulkCoordinator(IPokekeepHost host, IEngineGateway engine, Func<PokekeepSettings> settings, LibraryData? library, PokekeepCoordinator? single = null, TimeSpan? pollInterval = null, Func<string>? dataLanguage = null)
    {
        _host = host;
        _engine = engine;
        _settings = settings;
        _dataLanguage = dataLanguage;
        _library = library;
        _single = single;
        _pollInterval = pollInterval ?? TimeSpan.FromMilliseconds(200);
        _ui = SynchronizationContext.Current;
    }

    public bool IsBusy => _current is not null;
    public IPokekeepHost Host => _host;
    public PokekeepSettings Settings => _settings();
    public BulkOutcome? LastOutcome { get; private set; }
    /// <summary>Presupuesto (s) de la optimizacion de IV por Pokemon en las operaciones masivas (0 = el del motor, 20 s).</summary>
    public int OptimizeBudgetSeconds { get; set; } = 2;

    /// <summary>Sustituye las opciones del runner (workers, plazos): lo usan las pruebas y el banco.</summary>
    public BulkRunnerOptions? RunnerOptionsOverride { get; set; }

    public event EventHandler<BulkProgressInfo>? ProgressChanged;
    public event EventHandler<OperationResult>? Completed;

    // ============================================================================================ Disponibilidad
    public Availability GetLivingDexAvailability()
    {
        var sav = _host.Env.SAV;
        if (!RequestBuilder.IsKnownTarget(sav)) return Availability.No(Textos.SinPartida);
        if (!RequestBuilder.HasTrainerName(sav)) return Availability.No(Textos.SinNombreDeEntrenador);
        if (IsBusy || _single?.IsBusy == true) return Availability.No(Textos.OperacionEnCurso);
        if (!sav.HasBox) return Availability.No(AppIdioma.T("Este guardado no tiene cajas.", "This save has no boxes."));
        if (_library is null || !_library.IsAvailable) return Availability.No(Textos.CatalogoNoEncontrado);
        return _engine.UnavailableReason is { } why ? Availability.No(why) : Availability.Yes;
    }

    public Availability GetEditAvailability()
    {
        var sav = _host.Env.SAV;
        if (!RequestBuilder.IsKnownTarget(sav)) return Availability.No(Textos.SinPartida);
        if (!RequestBuilder.HasTrainerName(sav)) return Availability.No(Textos.SinNombreDeEntrenador);
        if (IsBusy || _single?.IsBusy == true) return Availability.No(Textos.OperacionEnCurso);
        if (!sav.HasBox && !sav.HasParty) return Availability.No(AppIdioma.T("Este guardado no tiene cajas ni equipo.", "This save has no boxes or party."));
        return _engine.UnavailableReason is { } why ? Availability.No(why) : Availability.Yes;
    }

    // ============================================================================================ Completar el juego
    /// <summary>Calcula la vista previa: qué se generaría, en qué orden y dónde. No llama al motor ni toca la partida. «Omitir lo que ya tengo» analiza los Pokémon existentes fuera del hilo de la interfaz.</summary>
    public async Task<LivingDexPlan> PlanLivingDexAsync(LivingDexOptions options, CancellationToken cancellation = default)
    {
        var sav = _host.Env.SAV;
        var game = LibraryData.GameIdOf(sav);
        var effective = options with { IncludeHome = options.IncludeHome && _settings().AllowHomeTrackerless };
        var candidates = _library?.LivingDexCandidates(game, effective.IncludeHome, LibraryData.GcDestinationOf(sav)) ?? [];
        IReadOnlySet<(int, int)>? owned = null;
        if (effective.SkipOwned)
            owned = await Task.Run(() => LivingDexPlanner.OwnedLegal(sav, cancellation), cancellation).ConfigureAwait(true);
        return LivingDexPlanner.Plan(sav, candidates, effective, owned);
    }

    /// <summary>Especie-forma de los Pokémon legales que ya hay (análisis oficial de cada uno; fuera del hilo de la interfaz).</summary>
    public Task<IReadOnlySet<(int Species, int Form)>> ScanOwnedAsync(CancellationToken cancellation = default)
    {
        var sav = _host.Env.SAV;
        return Task.Run(() => LivingDexPlanner.OwnedLegal(sav, cancellation), cancellation);
    }

    /// <summary>Plan síncrono con un conjunto de «ya tengo» ya calculado (la vista previa lo recalcula al cambiar una opción, sin esperar).</summary>
    public LivingDexPlan PlanLivingDex(LivingDexOptions options, IReadOnlySet<(int Species, int Form)>? owned)
    {
        var sav = _host.Env.SAV;
        var effective = options with { IncludeHome = options.IncludeHome && _settings().AllowHomeTrackerless };
        var candidates = _library?.LivingDexCandidates(LibraryData.GameIdOf(sav), effective.IncludeHome, LibraryData.GcDestinationOf(sav)) ?? [];
        return LivingDexPlanner.Plan(sav, candidates, effective, owned);
    }

    public BulkEditPlan PlanEdit(BulkSelection selection, AdjustOptions operations, EventSwapPlan? eventSwaps = null) => new() { Selection = selection, Operations = operations, PreferEvent = eventSwaps is not null, EventSwaps = eventSwaps };

    public void Cancel()
    {
        var run = _current;
        if (run is null || run.UserCancelled) return;
        run.UserCancelled = true;
        try { run.Cts.Cancel(); } catch (ObjectDisposedException) { }
        Report(run, Textos.Cancelando, null);
    }

    private sealed class RunState
    {
        public CancellationTokenSource Cts { get; } = new();
        public bool UserCancelled { get; set; }
        public bool TargetChanged { get; set; }
        public Stopwatch Clock { get; } = Stopwatch.StartNew();
        public List<string> Recent { get; } = [];
        public BulkProgress? Last { get; set; }
    }

    /// <summary>
    /// Genera el plan y lo coloca. <paramref name="decidePlacement"/>: si el usuario cancela (o el lote se detiene solo) con Pokémon ya generados, se pregunta UNA cosa:
    /// «¿Colocar los N ya generados?» (true = colocar, false = descartar); sin delegado se coloca lo ya generado.
    /// </summary>
    public async Task<BulkOutcome> RunLivingDexAsync(LivingDexPlan plan, int? workers = null, Func<int, Task<bool>>? decidePlacement = null)
    {
        if (_current is not null || _single?.IsBusy == true)
            return Finish(BusyOutcome(BulkKind.LivingDex), publish: false);
        var run = new RunState();
        _current = run;
        try
        {
            return Finish(await RunLivingDexCoreAsync(run, plan, workers, decidePlacement).ConfigureAwait(true));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Diagnostico.RegistroOperaciones.Excepcion(AppIdioma.T("Completar el juego", "Complete the game"), ex);
            return Finish(Failure(BulkKind.LivingDex, AppIdioma.T("Error inesperado en Pokekeep", "Unexpected error in Pokekeep"), AppIdioma.F("No se ha modificado nada. {0}: {1}", "Nothing was modified. {0}: {1}", ex.GetType().Name, ex.Message), run));
        }
        finally
        {
            _current = null;
            run.Cts.Dispose();
        }
    }

    private async Task<BulkOutcome> RunLivingDexCoreAsync(RunState run, LivingDexPlan plan, int? workers, Func<int, Task<bool>>? decidePlacement)
    {
        var env = _host.Env;
        var sav = env.SAV;
        var settings = _settings();
        Report(run, Textos.Analizando, null);
        var (manifest, problem) = await EnsureManifestAsync(run).ConfigureAwait(true);
        if (run.UserCancelled) return CancelledBeforeStart(BulkKind.LivingDex, plan, run);
        if (manifest is null) return Failure(BulkKind.LivingDex, AppIdioma.T("No se pudo iniciar el motor de Pokekeep", "Couldn’t start the Pokekeep engine"), problem!, run);

        var game = LibraryData.GameIdOf(sav);
        var target = RequestBuilder.TargetFor(sav, StorageSlotType.Box);   // el destino de una casilla de CAJA (en Let's Go, p. ej., Espíritu y Humor a 100 solo se exigen fuera del equipo)
        var allowHome = plan.Options.IncludeHome && settings.AllowHomeTrackerless;   // el ajuste se fija al planificar (diseño 3.6)
        var toGenerate = plan.ToGenerate;
        var jobs = new List<BulkJob>(toGenerate.Count);
        for (var i = 0; i < toGenerate.Count; i++)
        {
            var t = toGenerate[i];
            var slot = (SlotInfoBox)t.Slot!;
            var template = RequestBuilder.BlankFor(sav, (ushort)t.Candidate.Species, (byte)t.Candidate.Form);
            var wantedSpecies = t.Candidate.Species;
            var wantedForm = t.Candidate.Form;
            jobs.Add(new BulkJob
            {
                Id = i,
                Accepts = response => ParseCandidate(response, sav, wantedSpecies, wantedForm, out _) is not null,
                Label = $"{PokekeepCoordinator.SpeciesName((ushort)t.Candidate.Species, DataLanguage)}{(t.Candidate.FormName is { } f && t.Candidate.Form != 0 ? $" ({(DataLanguage == "es" ? f : FormLabel(t.Candidate, DataLanguage))})" : string.Empty)}",
                Attempts = [.. t.Origins.Select(o => new BulkAttemptSpec(o.EntryKey, deadline => RequestBuilder.Create(manifest, template.Clone(), false, target, LegalizeAction.Legalize, deadline,
                    SelectionInfoFactory.Box(slot.Box, slot.Slot), optimizeStats: true, originEventKey: o.Unpinned ? null : o.EncounterKey, allowHomeTrackerless: allowHome, maxLevel: true, newPokemon: true, optimizeBudgetSeconds: OptimizeBudgetSeconds, coherentHistory: settings.CoherentHistory, requestedLanguage: Catalog.Library.EventPreference.RequestedLanguageOf(o))))],
            });
        }

        var (results, stopped) = await RunJobsAsync(run, env, jobs, workers, "Generando").ConfigureAwait(true);
        if (run.TargetChanged)
            return Outcome(BulkKind.LivingDex, OutcomeKind.TargetChanged, AppIdioma.T("Resultado descartado", "Result discarded"), Textos.PartidaCambio, BuildLivingDexReport(sav, plan, results, null, null), null, run, 0, results);

        var positive = results.Where(r => r.State == BulkRunState.Done && r.Final is { } f && ResponseValidator.CarriesCandidate(f.Status) && f.Status != ResponseStatus.AlreadyLegal).ToList();
        if ((run.UserCancelled || stopped is not null) && positive.Count > 0)
        {
            var place = decidePlacement is null || await decidePlacement(positive.Count).ConfigureAwait(true);
            if (!place)
                return Outcome(BulkKind.LivingDex, OutcomeKind.Cancelled, Textos.OperacionCancelada, AppIdioma.F("Se han descartado los {0} Pokémon generados: no se ha cambiado nada.", "The {0} generated Pokémon were discarded: nothing was changed.", positive.Count),
                    BuildLivingDexReport(sav, plan, results, null, null, discarded: true), null, run, 0, results);
        }
        else if (run.UserCancelled)
            return Outcome(BulkKind.LivingDex, OutcomeKind.Cancelled, Textos.OperacionCancelada, AppIdioma.T("No se ha generado ningún Pokémon: no se ha cambiado nada.", "No Pokémon was generated: nothing was changed."), BuildLivingDexReport(sav, plan, results, null, null), null, run, 0, results);

        // ---- Aplicar: segunda opinión (fuera del hilo de la interfaz) y UNA transacción ----
        Report(run, Textos.Comprobando, null);
        var writes = new List<BulkWrite>();
        var rejected = new Dictionary<int, (BulkStatus, string)>();
        // Tras cancelar (o detenerse el lote) los generados no son seguidos (varios Workers acaban desordenados): se colocan CONSECUTIVOS, en orden de Pokédex, desde la primera casilla libre (sin huecos).
        var compact = run.UserCancelled || stopped is not null;
        var nextFree = 0;
        foreach (var r in positive.OrderBy(x => x.Job.Id))
        {
            var t = toGenerate[r.Job.Id];
            var parsed = ParseCandidate(r.Final!, sav, t.Candidate.Species, t.Candidate.Form, out var why);
            if (parsed is null) { rejected[r.Job.Id] = (BulkStatus.CannotGenerate, why!); continue; }
            var slot = compact ? plan.FreeSlots[nextFree++] : t.Slot!;
            writes.Add(new BulkWrite { Id = r.Job.Id, Slot = slot, Candidate = parsed, NeedsHome = r.Final!.Status == ResponseStatus.SuccessNeedsHome });
        }
        var validated = await Task.Run(() => BulkTransaction.Validate(sav, writes)).ConfigureAwait(true);
        foreach (var w in validated.Rejected) rejected[w.Id] = (BulkStatus.FailedFinalCheck, Textos.ValidacionFallida);
        Report(run, Textos.Aplicando, null);
        var spare = plan.FreeSlots.Skip(compact ? writes.Count : toGenerate.Count);
        var applied = BulkTransaction.Apply(_host, env, validated.Valid, spare, registerDex: settings.RegisterInPokedex);

        var report = BuildLivingDexReport(sav, plan, results, applied, rejected);
        var written = applied.WrittenCount;
        var needsHome = applied.Results.Count(x => x.Written && x.Write.NeedsHome);
        if (applied.Results.Count > 0 && applied.Results.All(x => x.Status == BulkWriteStatus.TargetChanged))
            return Outcome(BulkKind.LivingDex, OutcomeKind.TargetChanged, AppIdioma.T("Resultado descartado", "Result discarded"), Textos.PartidaCambio, report, null, run, 0, results);
        var headline = LivingDexSummary(report, written);
        return Outcome(BulkKind.LivingDex, written == 0 ? OutcomeKind.NotApplied : needsHome > 0 ? OutcomeKind.AppliedNeedsHome : OutcomeKind.Applied, headline,
            needsHome > 0 ? AppIdioma.F("{0} necesitan pasar por HOME: sin matrícula, PKHeX los da por inválidos hasta entonces.", "{0} need to pass through HOME: without a tracker, PKHeX considers them invalid until then.", needsHome) : null, report, applied.Undo, run, written, results);
    }

    // ============================================================================================ Edición masiva
    public async Task<BulkOutcome> RunEditAsync(BulkEditPlan plan, int? workers = null, Func<int, Task<bool>>? decidePlacement = null)
    {
        if (_current is not null || _single?.IsBusy == true)
            return Finish(BusyOutcome(BulkKind.Edit), publish: false);
        var run = new RunState();
        _current = run;
        try
        {
            return Finish(await RunEditCoreAsync(run, plan, workers, decidePlacement).ConfigureAwait(true));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Diagnostico.RegistroOperaciones.Excepcion(AppIdioma.T("Edición masiva", "Bulk edit"), ex);
            return Finish(Failure(BulkKind.Edit, AppIdioma.T("Error inesperado en Pokekeep", "Unexpected error in Pokekeep"), AppIdioma.F("No se ha modificado nada. {0}: {1}", "Nothing was modified. {0}: {1}", ex.GetType().Name, ex.Message), run));
        }
        finally
        {
            _current = null;
            run.Cts.Dispose();
        }
    }

    private async Task<BulkOutcome> RunEditCoreAsync(RunState run, BulkEditPlan plan, int? workers, Func<int, Task<bool>>? decidePlacement)
    {
        var env = _host.Env;
        var sav = env.SAV;
        var settings = _settings();
        Report(run, Textos.Analizando, null);
        var (manifest, problem) = await EnsureManifestAsync(run).ConfigureAwait(true);
        var items = plan.Selection.Included;
        if (run.UserCancelled) return Outcome(BulkKind.Edit, OutcomeKind.Cancelled, Textos.OperacionCancelada, null, BuildEditReport(sav, plan, [], null, null), null, run, 0, []);
        if (manifest is null) return Failure(BulkKind.Edit, AppIdioma.T("No se pudo iniciar el motor de Pokekeep", "Couldn’t start the Pokekeep engine"), problem!, run);

        // Huella de cada casilla AL PLANIFICAR (regla 7 casilla a casilla): si cambia mientras se procesa, ese Pokémon no se aplica.
        var originals = items.Select(i => (i.Slot, Pk: i.Pokemon.Clone())).ToList();
        var hashes = originals.Select(o => EntityBytes.Sha256(EntityBytes.Export(o.Pk, EntityBytes.UsesPartySize(o.Pk, o.Slot.Type)))).ToList();
        // Hito 5d (ADR 0046): lo aceptado en la vista previa de «Preferir la versión de evento». El Pokémon de evento YA está generado y validado por el motor: las demás operaciones se aplican sobre ÉL,
        // y la casilla se comprueba contra el Pokémon ORIGINAL (huella de arriba), así que si el usuario la toca mientras tanto no se escribe nada en ella.
        var swaps = plan.EventSwaps?.Accepted.ToDictionary(x => x.Index) ?? [];
        // 0.6.6 (B7): el ajuste de HOME se lee al EJECUTAR, no al preparar la vista previa: si el usuario lo desactivó entre las dos cosas, las sustituciones que necesitan pasar por HOME no se aplican.
        var droppedByHome = settings.AllowHomeTrackerless ? [] : swaps.Where(kv => kv.Value.NeedsHome).Select(kv => kv.Key).ToHashSet();
        foreach (var i in droppedByHome) swaps.Remove(i);
        // uso10 (decisión del propietario): con «Legalizar», el Pokémon que solo es ilegal por bytes de basura va al motor ya con ella borrada (legal): el motor solo aplica las demás operaciones y no
        // gasta su plazo en regenerarlo (un Weezing de Ultrasol agotaba los 60 s). La casilla se sigue comprobando contra el ORIGINAL (huella de arriba).
        var limpios = plan.Operations.LegalizeIfIllegal
            ? await Task.Run(() => originals.Select(o => LegalityCheck.CleanedOfTrash(o.Pk, sav, o.Slot.Type, settings.AllowHomeTrackerless)).ToList()).ConfigureAwait(true)
            : null;
        var jobs = new List<BulkJob>(items.Count);
        if (plan.Operations.Any)
            for (var i = 0; i < items.Count; i++)
            {
                var (slot, original) = originals[i];
                var pk = swaps.TryGetValue(i, out var sw) ? sw.Replacement!.Clone() : limpios?[i] ?? original;
                var partySize = EntityBytes.UsesPartySize(pk, slot.Type);
                var target = RequestBuilder.TargetFor(sav, slot.Type);
                var selection = slot is SlotInfoBox b ? SelectionInfoFactory.Box(b.Box, b.Slot) : new SelectionInfo { Kind = "Party", Slot = slot.Slot };
                jobs.Add(new BulkJob
                {
                    Id = i,
                    Label = $"{PokekeepCoordinator.SpeciesName(pk.Species, DataLanguage)} · {BulkScope.Describe(slot)}",
                    Attempts = [new BulkAttemptSpec(null, deadline => RequestBuilder.Create(manifest, pk.Clone(), partySize, target, LegalizeAction.Adjust, deadline, selection,
                        optimizeStats: plan.Operations.Optimize, allowHomeTrackerless: settings.AllowHomeTrackerless || sw is { NeedsHome: true }, adjust: plan.Operations, optimizeBudgetSeconds: OptimizeBudgetSeconds, coherentHistory: settings.CoherentHistory))],
                });
            }

        IReadOnlyList<BulkJobResult> results = [];
        string? stopped = null;
        if (jobs.Count > 0)
            (results, stopped) = await RunJobsAsync(run, env, jobs, workers, "Procesando", perCase: TimeSpan.FromSeconds(60)).ConfigureAwait(true);
        if (run.TargetChanged)
            return Outcome(BulkKind.Edit, OutcomeKind.TargetChanged, AppIdioma.T("Resultado descartado", "Result discarded"), Textos.PartidaCambio, BuildEditReport(sav, plan, results, null, null), null, run, 0, results);

        var positive = results.Where(r => r.State == BulkRunState.Done && r.Final is { } f && f.Status is ResponseStatus.Success or ResponseStatus.SuccessNeedsHome).ToList();
        var positiveIds = positive.Select(r => r.Job.Id).ToHashSet();
        var swapOnly = swaps.Keys.Where(i => !positiveIds.Contains(i)).Order().ToList();       // sustitución aceptada cuyas demás operaciones no cambiaron nada (o no había otras): se escribe el Pokémon de evento tal cual
        var ready = positive.Count + swapOnly.Count;
        if ((run.UserCancelled || stopped is not null) && ready > 0)
        {
            var place = decidePlacement is null || await decidePlacement(ready).ConfigureAwait(true);
            if (!place)
                return Outcome(BulkKind.Edit, OutcomeKind.Cancelled, Textos.OperacionCancelada, AppIdioma.F("Se han descartado los {0} cambios preparados: no se ha cambiado nada.", "The {0} prepared changes were discarded: nothing was changed.", ready), BuildEditReport(sav, plan, results, null, null, discarded: true), null, run, 0, results);
        }
        else if (run.UserCancelled)
            return Outcome(BulkKind.Edit, OutcomeKind.Cancelled, Textos.OperacionCancelada, AppIdioma.T("No se ha aplicado ningún cambio.", "No change was applied."), BuildEditReport(sav, plan, results, null, null), null, run, 0, results);

        Report(run, Textos.Comprobando, null);
        var writes = new List<BulkWrite>();
        var rejected = new Dictionary<int, (BulkStatus, string)>();
        foreach (var r in positive)
        {
            var (slot, original) = originals[r.Job.Id];
            var expected = swaps.TryGetValue(r.Job.Id, out var sw) ? sw.Replacement! : original;
            var parsed = ParseCandidate(r.Final!, sav, expected.Species, expected.Form, out var why);
            if (parsed is null) { rejected[r.Job.Id] = (BulkStatus.FailedFinalCheck, why!); continue; }
            writes.Add(new BulkWrite { Id = r.Job.Id, Slot = slot, Candidate = parsed, NeedsHome = r.Final!.Status == ResponseStatus.SuccessNeedsHome || sw is { NeedsHome: true }, ExpectedOriginalSha256 = hashes[r.Job.Id] });
        }
        foreach (var i in swapOnly)
        {
            var sw = swaps[i];
            writes.Add(new BulkWrite { Id = i, Slot = originals[i].Slot, Candidate = sw.Replacement!.Clone(), NeedsHome = sw.NeedsHome, ExpectedOriginalSha256 = hashes[i] });
        }
        // uso10 (decisión del propietario, 20-oct-2026): con «Legalizar», barrido de los bytes de basura en el mismo lote. El candidato del motor que solo queda ilegal por basura se escribe con ella
        // borrada; y el Pokémon que el motor no llegó a dejar legal (tiempo agotado, sin solución, candidato rechazado) pero que lo es con solo borrarla, también. Mismo Deshacer, mismo informe.
        var onlyTrash = new HashSet<int>();
        if (plan.Operations.LegalizeIfIllegal && !run.UserCancelled && stopped is null)
            await Task.Run(() =>
            {
                for (var k = 0; k < writes.Count; k++)
                {
                    var w = writes[k];
                    if (LegalityCheck.IsValid(w.Candidate, sav, w.Slot.Type, w.NeedsHome)) continue;
                    var clean = LegalityCheck.CleanedOfTrash(w.Candidate, sav, w.Slot.Type, w.NeedsHome) ?? LegalityCheck.CleanedOfTrash(LegalityCheck.AdaptedClone(w.Candidate, sav, w.Slot.Type), sav, w.Slot.Type, w.NeedsHome);
                    if (clean is not null) writes[k] = new BulkWrite { Id = w.Id, Slot = w.Slot, Candidate = clean, NeedsHome = w.NeedsHome, ExpectedOriginalSha256 = w.ExpectedOriginalSha256 };
                }
                var conEscritura = writes.Select(w => w.Id).ToHashSet();
                foreach (var r in results)
                {
                    var id = r.Job.Id;
                    if (r.State != BulkRunState.Done || conEscritura.Contains(id) || swaps.ContainsKey(id)) continue;
                    var (slot, original) = originals[id];
                    if (LegalityCheck.CleanedOfTrash(original, sav, slot.Type, settings.AllowHomeTrackerless) is not { } clean) continue;
                    writes.Add(new BulkWrite { Id = id, Slot = slot, Candidate = clean, NeedsHome = !LegalityCheck.IsValid(clean, sav, slot.Type, false), ExpectedOriginalSha256 = hashes[id] });
                    rejected.Remove(id);
                    onlyTrash.Add(id);
                }
            }).ConfigureAwait(true);
        var validated = await Task.Run(() => BulkTransaction.Validate(sav, writes)).ConfigureAwait(true);
        foreach (var w in validated.Rejected) rejected[w.Id] = (BulkStatus.FailedFinalCheck, Textos.ValidacionFallida);
        Report(run, Textos.Aplicando, null);
        var applied = BulkTransaction.Apply(_host, env, validated.Valid);
        var report = BuildEditReport(sav, plan, results, applied, rejected, droppedByHome: droppedByHome, onlyTrash: onlyTrash);
        if (applied.Results.Count > 0 && applied.Results.All(x => x.Status == BulkWriteStatus.TargetChanged))
            return Outcome(BulkKind.Edit, OutcomeKind.TargetChanged, AppIdioma.T("Resultado descartado", "Result discarded"), Textos.PartidaCambio, report, null, run, 0, results);
        var written = applied.WrittenCount;
        var headline = EditSummary(report, written, applied.Results.Count(x => x.Written && swaps.ContainsKey(x.Write.Id)));
        return Outcome(BulkKind.Edit, written == 0 ? OutcomeKind.NotApplied : applied.Results.Any(x => x.Written && x.Write.NeedsHome) ? OutcomeKind.AppliedNeedsHome : OutcomeKind.Applied, headline, null, report, applied.Undo, run, written, results);
    }

    // ============================================================================================ Núcleo común
    private async Task<(IReadOnlyList<BulkJobResult> Results, string? Stopped)> RunJobsAsync(RunState run, IEditEnvironment env, IReadOnlyList<BulkJob> jobs, int? workers, string verb, TimeSpan? perCase = null)
    {
        var options = RunnerOptionsOverride ?? new BulkRunnerOptions
        {
            Workers = workers ?? Math.Clamp(_settings().BulkWorkers, 1, 8),
            PerCaseTimeout = perCase ?? TimeSpan.FromSeconds(45),
        };
        var runner = new BulkRunner(_engine, options, _ui);
        runner.ProgressChanged += p =>
        {
            run.Last = p;
            ReportProgress(run, $"{verb} {p.Done} de {p.Total}" + (p.Current.Length > 0 ? $" · {p.Current}" : string.Empty) + AppIdioma.F(" · {0} en paralelo", " · {0} in parallel", p.Workers) + (p.Remaining is { } rem ? AppIdioma.F(" · quedan ~{0}", " · ~{0} left", Textos.Duracion(rem)) : string.Empty));
        };
        runner.ItemCompleted += r =>
        {
            run.Recent.Add(RecentLine(r));
            if (run.Recent.Count > 12) run.Recent.RemoveAt(0);
        };
        var task = runner.RunAsync(jobs, run.Cts.Token);
        // Vigilancia del destino: si se abre o se cierra otra partida mientras tanto, se cancela el lote y se descarta TODO (regla 7).
        while (true)
        {
            var tick = Task.Delay(_pollInterval);
            if (await Task.WhenAny(task, tick).ConfigureAwait(true) == task) break;
            if (!run.TargetChanged && !ReferenceEquals(_host.Env, env))
            {
                run.TargetChanged = true;
                try { run.Cts.Cancel(); } catch (ObjectDisposedException) { }
            }
        }
        var results = await task.ConfigureAwait(true);
        if (!ReferenceEquals(_host.Env, env)) run.TargetChanged = true;
        return (results, runner.StoppedReason);
    }

    private static string RecentLine(BulkJobResult r)
    {
        var status = r.Final?.Status switch
        {
            ResponseStatus.Success => "listo",
            ResponseStatus.SuccessNeedsHome => AppIdioma.T("necesita HOME", "needs HOME"),
            ResponseStatus.AlreadyOptimal => AppIdioma.T("ya óptimo", "already optimal"),
            ResponseStatus.Timeout => AppIdioma.T("tiempo agotado", "time ran out"),
            { } s => s.ToString(),
            null => AppIdioma.T("sin ejecutar", "not run"),
        };
        return $"{r.Job.Label}: {status}";
    }

    private static PKM? ParseCandidate(LegalizeResponse response, SaveFile sav, int species, int form, out string? why)
    {
        why = null;
        byte[] bytes;
        try { bytes = Convert.FromBase64String(response.CandidateEntityBase64!); }
        catch (FormatException) { why = AppIdioma.T("El resultado del motor no es legible.", "The engine’s result is not readable."); return null; }
        if (!Enum.TryParse<EntityContext>(response.EntityContext, false, out var context)) { why = AppIdioma.T("El resultado del motor no es legible.", "The engine’s result is not readable."); return null; }
        var pk = EntityBytes.Parse(bytes, context);
        if (pk is null) { why = AppIdioma.T("El resultado del motor no es legible.", "The engine’s result is not readable."); return null; }
        if (pk.Species != species) { why = AppIdioma.T("El resultado cambiaba la especie: Pokekeep nunca la cambia, se ha descartado.", "The result changed the species: Pokekeep never changes it, so it was discarded."); return null; }
        if (pk.Form != form) { why = AppIdioma.F("El motor solo pudo producir otra forma ({0} en vez de {1}): no se coloca un Pokémon que no es el pedido.", "The engine could only produce another form ({0} instead of {1}): a Pokémon that isn’t the requested one isn’t placed.", pk.Form, form); return null; }
        if (pk.GetType() != sav.PKMType) { why = AppIdioma.T("El resultado no tiene el formato de esta partida.", "The result doesn’t have this save’s format."); return null; }
        return pk;
    }

    private async Task<(WorkerManifest? Manifest, string? Problem)> EnsureManifestAsync(RunState run)
    {
        if (_manifest is not null) return (_manifest, null);
        WorkerManifest? manifest = null;
        string? error = null;
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(run.Cts.Token))
        {
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            try { manifest = await Task.Run(() => _engine.GetManifestAsync(timeout.Token)).ConfigureAwait(true); }
            catch (OperationCanceledException) { error = run.UserCancelled ? null : AppIdioma.T("El motor no respondió al comprobar su versión.", "The engine didn’t respond when checking its version."); }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException or System.Text.Json.JsonException) { error = ex.Message; }
        }
        if (run.UserCancelled) return (null, null);
        if (manifest is null) return (null, AppIdioma.T("No se ha modificado nada. ", "Nothing was modified.") + (_engine.UnavailableReason ?? error ?? AppIdioma.T("El motor no devolvió su manifiesto de versiones.", "The engine didn’t return its version manifest.")));
        var pair = PairCheck.Evaluate(manifest);
        if (!pair.Compatible) return (null, AppIdioma.T("No se ha generado nada. ", "Nothing was generated.") + pair.Problem);
        _manifest = manifest;
        return (manifest, null);
    }

    // ============================================================================================ Informes
    private BulkReport BuildLivingDexReport(SaveFile sav, LivingDexPlan plan, IReadOnlyList<BulkJobResult> results, BulkApplyResult? applied, IReadOnlyDictionary<int, (BulkStatus Status, string Reason)>? rejected, bool discarded = false)
    {
        var rows = new List<BulkReportRow>();
        var toGenerate = plan.ToGenerate;
        var byJob = results.ToDictionary(r => r.Job.Id);
        var writes = applied?.Results.ToDictionary(r => r.Write.Id);
        var n = 1;
        foreach (var t in plan.Targets)
        {
            var c = t.Candidate;
            BulkReportRow Row(BulkStatus status, string? reason, string? slot = null, string? origin = null, PKM? pk = null, LegalizeResponse? response = null) => new()
            {
                Number = n++,
                Pokemon = PokekeepCoordinator.SpeciesName((ushort)c.Species, DataLanguage),
                Form = c.Form == 0 ? null : FormLabel(c, DataLanguage),
                Status = status,
                Origin = origin,
                Level = pk?.CurrentLevel,
                Ivs = pk is null ? null : IvText(pk),
                Language = pk is null ? null : LanguageText(pk.Language),
                Slot = slot,
                Reason = reason,
                Details = response is null ? [] : ResponseDetails(response),
            };
            if (!t.Fits) { rows.Add(Row(BulkStatus.NoRoom, AppIdioma.T("Faltaban casillas libres: «Generar hasta donde quepa».", "There weren’t enough free slots: “Generate as many as fit”."))); continue; }
            var jobId = toGenerate.ToList().IndexOf(t);
            if (!byJob.TryGetValue(jobId, out var job) || job.State != BulkRunState.Done)
            {
                rows.Add(Row(BulkStatus.Cancelled, discarded ? AppIdioma.T("Descartado al cancelar.", "Discarded on cancel.") : AppIdioma.T("No llegó a procesarse.", "It was never processed.")));
                continue;
            }
            var resp = job.Final!;
            var origin = OriginText(job, t);
            if (rejected is not null && rejected.TryGetValue(jobId, out var rej))
            {
                rows.Add(Row(rej.Status, rej.Reason, origin: origin, response: resp));
                continue;
            }
            if (writes is not null && writes.TryGetValue(jobId, out var w))
            {
                var pk = w.Write.Candidate;
                var finalSlot = w.FinalSlot ?? w.Write.Slot;
                var slotText = BulkScope.Describe(finalSlot) + (w.Relocated ? AppIdioma.T(" (recolocado: la prevista se ocupó)", " (relocated: the planned slot was taken)") : string.Empty);
                BulkStatus st = w.Status switch
                {
                    BulkWriteStatus.Written => resp.Status == ResponseStatus.SuccessNeedsHome ? BulkStatus.NeedsHome : BulkStatus.Generated,
                    BulkWriteStatus.SlotTaken => BulkStatus.SlotTaken,
                    BulkWriteStatus.FailedFinalCheck or BulkWriteStatus.WriteRejected => BulkStatus.FailedFinalCheck,
                    _ => BulkStatus.Cancelled,
                };
                rows.Add(Row(st, st is BulkStatus.Generated ? LanguageReason(pk, resp) : st == BulkStatus.NeedsHome ? Textos.NecesitaHomeAviso : w.Detail, st is BulkStatus.Generated or BulkStatus.NeedsHome ? slotText : null, origin, st is BulkStatus.Generated or BulkStatus.NeedsHome ? pk : null, resp));
                continue;
            }
            // C5: al descartar lo generado tras cancelar, un Pokémon que SÍ se había generado no es «No se puede generar» (el emulador lo mostró con el texto de éxito): queda «Cancelado».
            if (discarded && ResponseValidator.CarriesCandidate(resp.Status))
            {
                rows.Add(Row(BulkStatus.Cancelled, AppIdioma.T("Descartado al cancelar.", "Discarded on cancel."), origin: origin, response: resp));
                continue;
            }
            rows.Add(FailureRow(Row, resp, job));
        }
        foreach (var s in plan.Skips)
        {
            rows.Add(new BulkReportRow
            {
                Number = n++,
                Pokemon = PokekeepCoordinator.SpeciesName((ushort)s.Candidate.Species, DataLanguage),
                Form = s.Candidate.Form == 0 ? null : FormLabel(s.Candidate, DataLanguage),
                Status = s.Kind switch
                {
                    LivingDexSkipKind.AlreadyHad => BulkStatus.AlreadyHad,
                    LivingDexSkipKind.OnlyWithHome => BulkStatus.OnlyWithHome,
                    LivingDexSkipKind.NotStorable => BulkStatus.NotStorable,
                    LivingDexSkipKind.ProvenImpossible => BulkStatus.NotInGame,
                    LivingDexSkipKind.RequiresExpansion => BulkStatus.RequiresExpansion,
                    _ => BulkStatus.CannotGenerate,
                },
                Reason = s.Reason,
            });
        }
        return new BulkReport($"{Textos.CompletarElJuego} · {PokekeepCoordinator.GameName(sav, _dataLanguage?.Invoke())}", rows, editReport: false, statWord: sav.Generation <= 2 ? "DV" : "IV");
    }

    private static BulkReportRow FailureRow(Func<BulkStatus, string?, string?, string?, PKM?, LegalizeResponse?, BulkReportRow> row, LegalizeResponse resp, BulkJobResult job)
    {
        var detail = string.Join(" ", new[] { TextosMotor.Traducir(resp.Message), TextosMotor.Traducir(resp.Reason) }.Where(x => !string.IsNullOrWhiteSpace(x)));
        var attempts = job.Attempts.Count > 1 ? AppIdioma.F(" ({0} orígenes probados)", " ({0} origins tried)", job.Attempts.Count) : string.Empty;
        return resp.Status switch
        {
            ResponseStatus.Timeout => row(BulkStatus.TimedOut, AppIdioma.T("Se agotó el tiempo: esto NO prueba que sea imposible.", "Time ran out: this does NOT prove it is impossible.") + attempts, null, null, null, resp),
            ResponseStatus.UnsupportedData when (resp.Reason ?? string.Empty).Contains("rastreador", StringComparison.OrdinalIgnoreCase) || (resp.Reason ?? string.Empty).Contains("matrícula", StringComparison.OrdinalIgnoreCase)
                => row(BulkStatus.OnlyWithHome, AppIdioma.T("Solo existe pasando por Pokémon HOME (matrícula que Pokekeep nunca se inventa).", "It only exists by passing through Pokémon HOME (a tracker that Pokekeep never makes up).") + attempts, null, null, null, resp),
            ResponseStatus.InternalError or ResponseStatus.VersionMismatch => row(BulkStatus.EngineError, detail + attempts, null, null, null, resp),
            ResponseStatus.Cancelled => row(BulkStatus.Cancelled, AppIdioma.T("Cancelado.", "Canceled."), null, null, null, resp),
            _ => row(BulkStatus.CannotGenerate, detail + attempts, null, null, null, resp),
        };
    }

    private BulkReport BuildEditReport(SaveFile sav, BulkEditPlan plan, IReadOnlyList<BulkJobResult> results, BulkApplyResult? applied, IReadOnlyDictionary<int, (BulkStatus Status, string Reason)>? rejected, bool discarded = false, IReadOnlySet<int>? droppedByHome = null, IReadOnlySet<int>? onlyTrash = null)
    {
        var rows = new List<BulkReportRow>();
        var items = plan.Selection.Included;
        var byJob = results.ToDictionary(r => r.Job.Id);
        var writes = applied?.Results.ToDictionary(r => r.Write.Id);
        var swapped = plan.EventSwaps?.Accepted.Where(x => droppedByHome is null || !droppedByHome.Contains(x.Index)).ToDictionary(x => x.Index) ?? [];
        var n = 1;
        for (var i = 0; i < items.Count; i++)
        {
            var (slot, pk) = (items[i].Slot, items[i].Pokemon);
            BulkReportRow Row(BulkStatus status, string? reason, IReadOnlyList<OperationOutcome>? ops = null, PKM? after = null, LegalizeResponse? resp = null) => new()
            {
                Number = n++,
                Pokemon = PokekeepCoordinator.SpeciesName(pk.Species, DataLanguage),
                Form = pk.Form == 0 ? null : FormName(pk, DataLanguage),
                Status = status,
                Slot = BulkScope.Describe(slot),
                Level = (after ?? pk).CurrentLevel,
                Ivs = IvText(after ?? pk),
                Language = LanguageText((after ?? pk).Language),
                Reason = reason,
                Operations = ops ?? [],
                Details = resp is null ? [] : ResponseDetails(resp),
            };
            if (swapped.TryGetValue(i, out var sw0) && !byJob.ContainsKey(i))
            {
                // Sustitución aceptada sin más operaciones: no hubo solicitud de ajuste, solo la escritura del Pokémon de evento.
                if (rejected is not null && rejected.TryGetValue(i, out var rej0)) { rows.Add(Row(rej0.Status, rej0.Reason)); continue; }
                if (writes is not null && writes.TryGetValue(i, out var w0))
                    rows.Add(w0.Status == BulkWriteStatus.Written
                        ? Row(sw0.NeedsHome ? BulkStatus.NeedsHome : BulkStatus.Changed, null, [], w0.Write.Candidate)
                        : Row(w0.Status switch { BulkWriteStatus.SlotChanged => BulkStatus.SlotChanged, BulkWriteStatus.SlotLocked => BulkStatus.Skipped, BulkWriteStatus.TargetChanged => BulkStatus.Cancelled, _ => BulkStatus.FailedFinalCheck }, w0.Detail));
                else rows.Add(Row(BulkStatus.Cancelled, discarded ? AppIdioma.T("Descartado al cancelar.", "Discarded on cancel.") : AppIdioma.T("No llegó a procesarse.", "It was never processed.")));
                continue;
            }
            if (!plan.Operations.Any && !byJob.ContainsKey(i))
            {
                // Edición con SOLO «Preferir la versión de evento»: no hubo solicitud de ajuste para este Pokémon (no hay operaciones) y no se sustituye.
                rows.Add(Row(BulkStatus.NotApplicable, discarded ? AppIdioma.T("Descartado al cancelar.", "Discarded on cancel.") : droppedByHome?.Contains(i) == true ? Textos.EventoNoAplicadaAjusteHome : plan.EventSwaps?.Items.FirstOrDefault(x => x.Index == i) is { } it ? it.StatusText : AppIdioma.T("Sin operaciones que aplicar: esta edición solo cambia la versión de evento de legendarios, sublegendarios y singulares.", "No operations to apply: this edit only changes the event version of legendary, sub-legendary and unique Pokémon.")));
                continue;
            }
            if (!byJob.TryGetValue(i, out var job) || job.State != BulkRunState.Done) { rows.Add(Row(BulkStatus.Cancelled, discarded ? AppIdioma.T("Descartado al cancelar.", "Discarded on cancel.") : AppIdioma.T("No llegó a procesarse.", "It was never processed."))); continue; }
            var r = job.Final!;
            var ops = r.Operations;
            if (rejected is not null && rejected.TryGetValue(i, out var rej)) { rows.Add(Row(rej.Status, rej.Reason, ops, resp: r)); continue; }
            if (writes is not null && writes.TryGetValue(i, out var w))
            {
                BulkStatus st = w.Status switch
                {
                    BulkWriteStatus.Written => r.Status == ResponseStatus.SuccessNeedsHome ? BulkStatus.NeedsHome : BulkStatus.Changed,
                    BulkWriteStatus.SlotChanged => BulkStatus.SlotChanged,
                    BulkWriteStatus.SlotLocked => BulkStatus.Skipped,
                    BulkWriteStatus.FailedFinalCheck or BulkWriteStatus.WriteRejected => BulkStatus.FailedFinalCheck,
                    _ => BulkStatus.Cancelled,
                };
                if (st is BulkStatus.Changed && onlyTrash?.Contains(i) == true)
                {
                    // El motor no lo dejó legal (sus operaciones no se aplicaron), pero bastaba con borrar los bytes de basura: es lo único que ha cambiado.
                    rows.Add(Row(st, AppIdioma.T("Solo se han borrado los bytes de basura de sus textos: con eso ya es legal.", "Only the trash bytes of its texts were cleared: that makes it legal."), [], w.Write.Candidate));
                    continue;
                }
                rows.Add(Row(st, st is BulkStatus.Changed ? null : w.Detail, ops, st is BulkStatus.Changed or BulkStatus.NeedsHome ? w.Write.Candidate : null, r));
                continue;
            }
            if (discarded && ResponseValidator.CarriesCandidate(r.Status) && r.Status != ResponseStatus.AlreadyLegal)
            {
                rows.Add(Row(BulkStatus.Cancelled, AppIdioma.T("Descartado al cancelar.", "Discarded on cancel."), ops, resp: r));   // C5: lo preparado y descartado no es «No se puede generar»
                continue;
            }
            rows.Add(r.Status switch
            {
                ResponseStatus.AlreadyOptimal => Row(ops.Count > 0 && ops.All(o => o.Status == OperationStatus.NotApplicable) ? BulkStatus.NotApplicable : BulkStatus.AlreadyOptimal, null, ops, resp: r),
                ResponseStatus.NotLegalFirst => Row(BulkStatus.NotLegalFirst, AppIdioma.T("Es ilegal: activa «Legalizar los ilegales primero» o usa «Legalizar automáticamente».", "It is illegal: turn on “Legalize the illegal ones first” or use “Legalize automatically”."), ops, resp: r),
                ResponseStatus.Timeout => Row(BulkStatus.TimedOut, AppIdioma.T("Se agotó el tiempo: no se ha cambiado nada en este Pokémon.", "Time ran out: nothing was changed on this Pokémon."), ops, resp: r),
                ResponseStatus.InternalError or ResponseStatus.VersionMismatch => Row(BulkStatus.EngineError, $"{TextosMotor.Traducir(r.Message)} {TextosMotor.Traducir(r.Reason)}".Trim(), ops, resp: r),
                ResponseStatus.Cancelled => Row(BulkStatus.Cancelled, AppIdioma.T("Cancelado.", "Canceled."), ops, resp: r),
                _ => Row(BulkStatus.CannotGenerate, $"{TextosMotor.Traducir(r.Message)} {TextosMotor.Traducir(r.Reason)}".Trim(), ops, resp: r),
            });
        }
        // Hito 5d (ADR 0046): lo que pasó con «Preferir la versión de evento» en cada Pokémon (una fila por casilla, en el mismo orden que los elementos).
        if (plan.EventSwaps is { } swapPlan)
            foreach (var item in swapPlan.Items.Where(x => x.Index >= 0 && x.Index < items.Count && x.Index < rows.Count))
            {
                var row = rows[item.Index];
                var accepted = swapped.ContainsKey(item.Index);
                var wrote = accepted && row.Status is BulkStatus.Changed or BulkStatus.NeedsHome;
                var dropped = droppedByHome?.Contains(item.Index) == true;
                var line = dropped ? AppIdioma.T("Versión de evento NO aplicada: ", "Event version NOT applied:") + Textos.EventoNoAplicadaAjusteHome
                    : accepted
                    ? (wrote ? AppIdioma.T("Versión de evento: ", "Event version:") + item.Describe() : AppIdioma.T("Versión de evento NO aplicada: ", "Event version NOT applied:") + item.Describe())
                    : AppIdioma.T("Versión de evento: sin cambio. ", "Event version: no change.") + item.StatusText;
                var reason = wrote ? item.Describe() : row.Reason;
                if (wrote && row.Status == BulkStatus.Changed) row = row with { Status = BulkStatus.EventVersion };
                else if (accepted && row.Status == BulkStatus.Changed && item.NeedsHome) row = row with { Status = BulkStatus.NeedsHome };
                rows[item.Index] = row with { Reason = reason, Details = [line, .. row.Details] };
            }
        foreach (var s in plan.Selection.Skipped)
        {
            var pk = s.Slot.Read(sav);
            rows.Add(new BulkReportRow
            {
                Number = n++,
                Pokemon = PokekeepCoordinator.SpeciesName(pk.Species, DataLanguage),
                Form = pk.Form == 0 ? null : FormName(pk, DataLanguage),
                Status = BulkStatus.Skipped,
                Slot = s.Description,
                Reason = s.Reason == BulkSkipReason.Egg ? AppIdioma.T("Los huevos no se editan: su legalidad depende del estado de huevo.", "Eggs aren’t edited: their legality depends on the egg state.") : AppIdioma.T("Casilla bloqueada o protegida: PKHeX no deja escribir en ella.", "Locked or protected slot: PKHeX doesn’t allow writing to it."),
            });
        }
        return new BulkReport($"{Textos.EdicionMasiva} · {PokekeepCoordinator.GameName(sav, _dataLanguage?.Invoke())}", rows, editReport: true, statWord: sav.Generation <= 2 ? "DV" : "IV");
    }

    private static IReadOnlyList<string> ResponseDetails(LegalizeResponse r)
    {
        var lines = new List<string>();
        foreach (var c in r.Changes)
            lines.Add($"{TextosMotor.Campo(c.Field)}: {Controls.ChangeFormatter.Describe(c.Field, c.Before)} → {Controls.ChangeFormatter.Describe(c.Field, c.After)} ({TextosMotor.Traducir(c.Reason)})");
        return lines;
    }

    private static string LanguageReason(PKM pk, LegalizeResponse r)
    {
        // Idioma final y, si no es español, por qué (el motor lo dice en el resumen: «idioma»).
        if (pk.Language == (int)LanguageID.Spanish) return string.Empty;
        var note = r.Summary.FirstOrDefault(s => s.Contains("idioma", StringComparison.OrdinalIgnoreCase)) ?? r.Reason;
        return pk.Format <= 2 ? AppIdioma.T("Las partidas de Game Boy no guardan idioma.", "Game Boy saves don’t store a language.") : TextosMotor.Traducir(note) ?? AppIdioma.F("No está en español: {0}.", "It isn’t Spanish: {0}.", LanguageText(pk.Language));
    }

    private static string OriginText(BulkJobResult job, LivingDexTarget t)
    {
        var chosen = job.FinalAttempt is { } a ? t.Origins.FirstOrDefault(o => o.EntryKey == a.OriginKey) : null;
        chosen ??= t.Origins.FirstOrDefault();
        if (chosen is null) return string.Empty;
        if (chosen.Event is { } ev) return ev.DisplayTitle.StartsWith(AppIdioma.T("Evento", "Event"), StringComparison.Ordinal) ? ev.DisplayTitle : AppIdioma.F("Evento «{0}»", "Event “{0}”", ev.DisplayTitle);
        return chosen.Class == Catalog.Library.LivingDexClass.Transfer ? AppIdioma.T("Transferencia", "Transfer") : LibraryNames.Kind(chosen.Kind);
    }

    public static string FormLabel(Catalog.Library.LivingDexCandidate c, string idioma = "es") => FormNameOf(c.Species, c.Form, idioma: idioma) ?? c.FormName ?? AppIdioma.F("forma {0}", "form {0}", c.Form);

    private static string FormName(PKM pk, string idioma = "es") => FormNameOf(pk.Species, pk.Form, pk.Context, idioma) ?? AppIdioma.F("forma {0}", "form {0}", pk.Form);

    private static string? FormNameOf(int species, int form, EntityContext context = EntityContext.None, string idioma = "es")
    {
        try
        {
            var s = GameInfo.GetStrings(idioma);
            var list = FormConverter.GetFormList((ushort)species, s.types, s.forms, GameInfo.GenderSymbolUnicode, context == EntityContext.None ? EntityContext.Gen9 : context);
            return form < list.Length && list[form].Length > 0 ? list[form] : null;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return null; }
    }

    private static string IvText(PKM pk) => $"{pk.IV_HP}/{pk.IV_ATK}/{pk.IV_DEF}/{pk.IV_SPA}/{pk.IV_SPD}/{pk.IV_SPE}";

    public static string LanguageText(int language) => (LanguageID)language switch
    {
        LanguageID.Japanese => AppIdioma.T("Japonés", "Japanese"),
        LanguageID.English => AppIdioma.T("Inglés", "English"),
        LanguageID.French => AppIdioma.T("Francés", "French"),
        LanguageID.Italian => AppIdioma.T("Italiano", "Italian"),
        LanguageID.German => AppIdioma.T("Alemán", "German"),
        LanguageID.Spanish => AppIdioma.T("Español", "Spanish"),
        LanguageID.Korean => AppIdioma.T("Coreano", "Korean"),
        LanguageID.ChineseS => AppIdioma.T("Chino simplificado", "Simplified Chinese"),
        LanguageID.ChineseT => AppIdioma.T("Chino tradicional", "Traditional Chinese"),
        _ => "—",
    };

    /// <summary>«Completar el juego: 862 colocados, 4 no se pueden generar, 0 con tiempo agotado.» (diseño §1.11); lo que no es cero se añade al final.</summary>
    private static string LivingDexSummary(BulkReport report, int written)
    {
        var parts = new List<string>
        {
            AppIdioma.F("{0} colocados", "{0} placed", written),
            AppIdioma.F("{0} no se pueden generar", "{0} can’t be generated", report.Count(BulkStatus.CannotGenerate) + report.Count(BulkStatus.NotStorable)),
            AppIdioma.F("{0} con tiempo agotado", "{0} timed out", report.Count(BulkStatus.TimedOut)),
        };
        void Extra(BulkStatus s, string text) { var n = report.Count(s); if (n > 0) parts.Add($"{n} {text}"); }
        Extra(BulkStatus.NeedsHome, AppIdioma.T("necesitan pasar por HOME", "need to pass through HOME"));
        Extra(BulkStatus.OnlyWithHome, AppIdioma.T("solo existen con el ajuste de HOME", "only exist with the HOME setting"));
        Extra(BulkStatus.NotInGame, AppIdioma.T("no existen en este juego (lo demuestra PKHeX)", "don’t exist in this game (PKHeX proves it)"));
        Extra(BulkStatus.RequiresExpansion, AppIdioma.T("requieren un DLC que la partida no tiene", "require DLC that the save doesn’t have"));
        Extra(BulkStatus.NoRoom, AppIdioma.T("no caben", "don’t fit"));
        Extra(BulkStatus.AlreadyHad, AppIdioma.T("ya los tenías", "you already had them"));
        Extra(BulkStatus.SlotTaken, AppIdioma.T("con la casilla ocupada", "with the slot taken"));
        Extra(BulkStatus.FailedFinalCheck, AppIdioma.T("no superaron la comprobación final", "did not pass the final check"));
        Extra(BulkStatus.EngineError, AppIdioma.T("con error del motor", "with an engine error"));
        Extra(BulkStatus.Cancelled, AppIdioma.T("sin procesar", "not processed"));
        return $"{Textos.CompletarElJuego}: {string.Join(", ", parts)}.";
    }

    private static string EditSummary(BulkReport report, int written, int eventVersions = 0)
    {
        var parts = new List<string> { $"{written} {(written == 1 ? AppIdioma.T("cambiado", "changed") : AppIdioma.T("cambiados", "changed"))}" + (eventVersions > 0 ? AppIdioma.F(" ({0} de ellos {1} a su versión de evento)", " ({0} of them {1} to their event version)", eventVersions, (eventVersions == 1 ? AppIdioma.T("pasa", "moves") : AppIdioma.T("pasan", "move"))) : string.Empty) };
        void Extra(BulkStatus s, string text) { var n = report.Count(s); if (n > 0) parts.Add($"{n} {text}"); }
        Extra(BulkStatus.NeedsHome, AppIdioma.T("necesitan pasar por HOME", "need to pass through HOME"));
        Extra(BulkStatus.AlreadyOptimal, AppIdioma.T("ya estaban óptimos", "were already optimal"));
        Extra(BulkStatus.NotApplicable, AppIdioma.T("sin nada aplicable", "with nothing applicable"));
        Extra(BulkStatus.NotLegalFirst, AppIdioma.T("ilegales sin legalizar", "illegal ones not legalized"));
        Extra(BulkStatus.SlotChanged, AppIdioma.T("cambiados por ti mientras se procesaba", "changed by you while processing"));
        Extra(BulkStatus.TimedOut, AppIdioma.T("con tiempo agotado", "timed out"));
        Extra(BulkStatus.FailedFinalCheck, AppIdioma.T("no superaron la comprobación final", "did not pass the final check"));
        Extra(BulkStatus.EngineError, AppIdioma.T("con error del motor", "with an engine error"));
        Extra(BulkStatus.CannotGenerate, AppIdioma.T("con fallo", "failed"));
        { var n = report.Count(BulkStatus.Skipped); if (n > 0) parts.Add($"{n} {(n == 1 ? AppIdioma.T("omitido", "skipped") : AppIdioma.T("omitidos", "skipped"))}"); }
        Extra(BulkStatus.Cancelled, AppIdioma.T("sin procesar", "not processed"));
        return $"{Textos.EdicionMasiva}: {string.Join(", ", parts)}.";
    }

    // ============================================================================================ Resultados y progreso
    private BulkOutcome Outcome(BulkKind kind, OutcomeKind result, string headline, string? detail, BulkReport report, BulkUndoToken? undo, RunState run, int written, IReadOnlyList<BulkJobResult> jobs)
        => new() { Kind = kind, Result = result, Headline = headline, Detail = detail, Report = report, Undo = undo, Elapsed = run.Clock.Elapsed, Written = written, Jobs = jobs };

    private BulkOutcome Failure(BulkKind kind, string headline, string detail, RunState run)
        => Outcome(kind, OutcomeKind.EngineFailure, headline, detail, new BulkReport(headline, [], kind == BulkKind.Edit), null, run, 0, []);

    private BulkOutcome CancelledBeforeStart(BulkKind kind, LivingDexPlan plan, RunState run)
        => Outcome(kind, OutcomeKind.Cancelled, Textos.OperacionCancelada, AppIdioma.T("No se ha cambiado nada.", "Nothing was changed."), new BulkReport(Textos.CompletarElJuego, [], editReport: false), null, run, 0, []);

    private static BulkOutcome BusyOutcome(BulkKind kind) => new()
    {
        Kind = kind, Result = OutcomeKind.Busy, Headline = Textos.OperacionEnCurso, Report = new BulkReport(Textos.OperacionEnCurso, [], kind == BulkKind.Edit),
    };

    private BulkOutcome Finish(BulkOutcome outcome, bool publish = true)
    {
        if (!publish) return outcome;
        LastOutcome = outcome;
        var asResult = outcome.ToOperationResult();
        Diagnostico.RegistroOperaciones.Registrar(asResult);
        Completed?.Invoke(this, asResult);
        return outcome;
    }

    private void Report(RunState run, string text, BulkProgress? p) => ReportProgress(run, text, p);

    private void ReportProgress(RunState run, string text, BulkProgress? p = null)
    {
        var info = new BulkProgressInfo(text, p ?? run.Last, [.. run.Recent]);
        Post(() => ProgressChanged?.Invoke(this, info));
    }

    private void Post(Action a)
    {
        if (_ui is null) a();
        else _ui.Post(_ => a(), null);
    }

    public void Dispose() => Cancel();
}
