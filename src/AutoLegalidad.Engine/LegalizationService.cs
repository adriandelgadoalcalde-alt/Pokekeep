// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Diagnostics;
using System.Security.Cryptography;
using AutoLegalidad.Catalog.Core;
using AutoLegalidad.Catalog.Db;
using AutoLegalidad.Contracts;
using AutoLegalidad.Engine.Catalog;
using AutoLegalidad.Engine.Changes;
using AutoLegalidad.Engine.Context;
using AutoLegalidad.Engine.History;
using AutoLegalidad.Engine.Generation;
using AutoLegalidad.Engine.Infra;
using AutoLegalidad.Engine.Optimization;
using AutoLegalidad.Engine.Repair;
using AutoLegalidad.Engine.Validation;
using PKHeX.Core;

namespace AutoLegalidad.Engine;

public interface ILegalizationService
{
    /// <summary>Ejecuta una operacion completa (sincrona). El Worker la corre en un hilo propio con vigilante de tiempo.</summary>
    LegalizeResponse Execute(LegalizeRequest request, CancellationToken cancellation = default);
}

public sealed record ServiceSettings
{
    /// <summary>Tiempo total por defecto si la solicitud no trae <c>deadlineUtc</c> (plan 5: 60 s, a calibrar con medidas).</summary>
    public TimeSpan DefaultTimeout { get; init; } = TimeSpan.FromSeconds(60);
    /// <summary>Plazo por defecto de «Optimizar stats» (el usuario autorizo mas tiempo: la busqueda exhaustiva de semillas puede tardar minutos). Cancelable y con progreso.</summary>
    public TimeSpan DefaultOptimizeTimeout { get; init; } = TimeSpan.FromMinutes(5);
    /// <summary>Progreso de las busquedas largas (opcional).</summary>
    public Action<OptimizeProgress>? Progress { get; init; }
    /// <summary>Aviso de cambio de fase (A-E) para que el Worker lo cuente a la interfaz (Hito 4: «Analizando», «Buscando un origen valido», «Comprobando»). Opcional.</summary>
    public Action<Phase>? PhaseChanged { get; init; }
    /// <summary>Tope por escalon de relajacion para que el primero no se coma todo el presupuesto.</summary>
    public TimeSpan MaxPerStep { get; init; } = TimeSpan.FromSeconds(12);
    public int MaxAttemptsPerStep { get; init; } = 3;
    /// <summary>
    /// Hito 3b: presupuesto PROPIO de la optimizacion de IV dentro de «Legalizar» (despues de tener ya un candidato legal). Para legalizar basta cualquier semilla valida: la
    /// optimizacion exhaustiva no puede comerse el plazo de la solicitud ni hacerla fallar; si se agota, se devuelve el mejor candidato hallado y el informe lo dice.
    /// «Optimizar stats» (accion B) no se ve afectada: usa su plazo propio de 5 min.
    /// </summary>
    public TimeSpan LegalizeOptimizeBudget { get; init; } = TimeSpan.FromSeconds(20);
    /// <summary>
    /// Hilos de la busqueda exhaustiva de semillas (Android F1). <c>null</c> = no tocar el valor del proceso (por defecto, nucleos - 1). Si se da, el servicio lo aplica a
    /// <see cref="AutoLegalidad.Engine.Optimization.OptimizerSwitches.SeedSearchThreads"/> al empezar cada solicitud.
    /// </summary>
    public int? SeedSearchThreads { get; init; }
    /// <summary>Traza opcional de depuracion (una linea por candidato); nunca se escribe a disco.</summary>
    public Action<string>? Trace { get; init; }
    /// <summary>Cuenta (hasta <c>limit</c>) los encuentros del Core alcanzables para la especie en el destino. Solo se sustituye en pruebas del dominio vacio.</summary>
    public Func<PKM, Destination, int, int>? EncounterCounter { get; init; }
}

/// <summary>
/// Motor de AutoLegalidad (plan 7, fases A a E). Orquesta: captura y analisis (A), reparacion minima (B), regeneracion con estrategias
/// intercambiables (C), relajacion automatica ordenada (D) y validacion independiente (E); mas la optimizacion de stats (R-001) y la accion
/// «Optimizar stats» (R-002). NO decide el exito por el bool de ningun generador: todo candidato pasa por <see cref="OfficialValidator"/>.
/// </summary>
public sealed partial class LegalizationService(CatalogReader catalog, IReadOnlyList<IGenerationStrategy> strategies, IOfficialValidator? validator = null,
    IStatOptimizer? optimizer = null, ServiceSettings? settings = null) : ILegalizationService
{
    private readonly IOfficialValidator _validator = validator ?? new OfficialValidator(catalog);
    private readonly IStatOptimizer _optimizer = optimizer ?? new StatOptimizer();
    private readonly ServiceSettings _settings = settings ?? new ServiceSettings();

    /// <summary>Aviso literal que acompana a <see cref="ResponseStatus.SuccessNeedsHome"/> (R-009). El Core lo dice asi: «transfiere un pk de rastreador 0 a HOME para que se le asigne un rastreador valido a traves del juego del que procede» (TransferVerifier.cs:174-176).</summary>
    public const string NeedsHomeWarning = "Sin matrícula de HOME: PKHeX lo considera inválido hasta que pase por HOME. Llévalo a HOME desde este juego para que el servidor le asigne su matrícula. Riesgo no verificable en los servidores de Nintendo.";

    /// <summary>
    /// R-009 (ajuste explicito «Permitir Pokemon que necesitan pasar por HOME sin matricula», desactivado por defecto): primero se intenta SIEMPRE el camino legal normal; solo si no hay
    /// resultado positivo por falta del rastreador se repite la operacion con la severidad Fishy de <c>HOMETransferTrackerNotPresent</c> aplicada unicamente a esta solicitud (y restaurada
    /// al terminar, pase lo que pase). El rastreador sigue valiendo 0: nunca se inventa (regla 6) y todo lo demas se valida igual.
    /// </summary>
    public LegalizeResponse Execute(LegalizeRequest request, CancellationToken cancellation = default) => Execute(request, cancellation, keepUserLanguage: false);

    /// <param name="keepUserLanguage">ADR 0045: la solicitud fija un origen para PROBAR una alternativa a un Pokemon del usuario (<see cref="BestRoute"/>): el idioma del usuario sigue mandando, no el espanol de «generar».</param>
    private LegalizeResponse Execute(LegalizeRequest request, CancellationToken cancellation, bool keepUserLanguage)
    {
        if (_settings.SeedSearchThreads is { } threads) AutoLegalidad.Engine.Optimization.OptimizerSwitches.SeedSearchThreads = Math.Max(1, threads);
        if (request.Options?.AllowHomeTrackerless != true) return ExecuteCore(request, cancellation, keepUserLanguage);
        // 0.6.6 (M5, decision 1 del propietario, ADR 0053): un Pokemon del usuario cuyo origen exige la matricula de HOME (Pokemon GO, otra generacion) y que no la trae CONSERVA su origen con el
        // ajuste activo: se intenta primero con la relajacion de R-009 DENTRO de ese origen. Sale «Necesita pasar por HOME», nunca «Legal», y el rastreador sigue siendo 0 (regla 6). Si no se puede
        // conservar, se sigue el camino de siempre (primero el legal normal) y se avisa.
        string? keptOriginKey = null;
        var triedKeepingHomeOrigin = false;
        if (request.Action == LegalizeAction.Legalize && request.OriginEventKey is null && request.Options.NewPokemon != true && !cancellation.IsCancellationRequested
            && OriginNeedingHomeTracker(request) is { } homeOriginKey)
        {
            keptOriginKey = homeOriginKey;
            triedKeepingHomeOrigin = true;
            Severity globalBefore;
            lock (EngineGlobalState.Gate) globalBefore = ParseSettings.Settings.HOMETransfer.HOMETransferTrackerNotPresent;
            try
            {
                var kept = ExecuteCore(request, cancellation, keepUserLanguage);
                if (kept.Status is ResponseStatus.SuccessNeedsHome && EncounterKeys.SameOriginLine(kept.Proof?.EncounterKey, homeOriginKey)) return kept;
            }
            finally
            {
                lock (EngineGlobalState.Gate) ParseSettings.Settings.HOMETransfer.HOMETransferTrackerNotPresent = globalBefore;
            }
        }
        var normal = ExecuteNormalThenRelaxed(request, cancellation, keepUserLanguage);
        return triedKeepingHomeOrigin && normal.CandidateEntityBase64 is not null && normal.Status is not ResponseStatus.SuccessNeedsHome
            ? normal with { Summary = ["No se ha podido conservar el origen del Pokémon sin matrícula de HOME (por ejemplo, el de Pokémon GO): se ha reconstruido con un origen que PKHeX acepta sin pasar por HOME.", .. normal.Summary] }
            : normal;
    }

    /// <summary>
    /// 0.6.6 (M5): clave del encuentro de ORIGEN del Pokemon de la solicitud si ese encuentro exige la matricula de HOME y el Pokemon no la trae (rastreador 0) y ya tiene el formato del destino;
    /// null en cualquier otro caso (incluido cualquier problema al leer la solicitud: de eso se ocupa el camino normal).
    /// </summary>
    private static string? OriginNeedingHomeTracker(LegalizeRequest request)
    {
        try
        {
            if (RequestValidator.Validate(request, out var bytes).Count > 0 || bytes is null) return null;
            if (!Enum.TryParse<EntityContext>(request.EntityContext, false, out var context) || !context.IsValid) return null;
            var original = EntityIo.Parse(bytes, context);
            if (original is null or { Species: 0 } or { IsEgg: true } || original is not IHomeTrack { Tracker: 0 }) return null;
            if (!Destination.TryCreate(request.Target, out var dest, out _) || dest is null || original.GetType() != dest.EntityType) return null;
            LegalityAnalysis analysis;
            lock (EngineGlobalState.Gate)
            {
                dest.ApplyParseSettings();   // severidad por defecto del Core
                analysis = new LegalityAnalysis(original, dest.Personal, dest.SlotType);
            }
            var enc = OriginalEncounterResolver.Resolve(original, dest, analysis);
            return enc is not null && HomeTrackerRule.IsRequired(enc, dest.Context) ? EncounterKeys.For(enc) : null;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return null; }
    }

    private LegalizeResponse ExecuteNormalThenRelaxed(LegalizeRequest request, CancellationToken cancellation, bool keepUserLanguage)
    {
        var first = ExecuteCore(request with { Options = request.Options! with { AllowHomeTrackerless = false } }, cancellation, keepUserLanguage);
        // Hito 4b: un exito con OTRA forma (el escalon «BaseForm» suelta la forma pedida cuando el camino normal no la da) tampoco cierra la solicitud: si la forma solo existe con la matricula de HOME,
        // el segundo intento (con el ajuste) puede darla. Solo se prefiere el segundo resultado si conserva la forma pedida; si no, se queda el primero.
        var formRelaxed = first.Status is ResponseStatus.Success && FormDiffers(request, first);
        if (!formRelaxed && (first.Status is not (ResponseStatus.UnsupportedData or ResponseStatus.NoSolution or ResponseStatus.NotLegalFirst) || cancellation.IsCancellationRequested)) return first;
        if (cancellation.IsCancellationRequested) return first;
        Severity before;
        lock (EngineGlobalState.Gate) before = ParseSettings.Settings.HOMETransfer.HOMETransferTrackerNotPresent;
        try
        {
            var second = ExecuteCore(request, cancellation, keepUserLanguage);
            if (formRelaxed)
                return second.Status is ResponseStatus.SuccessNeedsHome or ResponseStatus.Success && !FormDiffers(request, second) ? second : first;
            return second.Status is ResponseStatus.SuccessNeedsHome or ResponseStatus.Success or ResponseStatus.AlreadyLegal or ResponseStatus.AlreadyOptimal ? second : first;
        }
        finally
        {
            lock (EngineGlobalState.Gate) ParseSettings.Settings.HOMETransfer.HOMETransferTrackerNotPresent = before;
        }
    }

    /// <summary>La forma del candidato de la respuesta no es la de la entidad de la solicitud (false si no se puede leer alguna de las dos).</summary>
    private static bool FormDiffers(LegalizeRequest request, LegalizeResponse response)
    {
        try
        {
            if (response.CandidateEntityBase64 is not { Length: > 0 } cand || response.EntityContext is null) return false;
            if (!Enum.TryParse<EntityContext>(response.EntityContext, false, out var candCtx) || !Enum.TryParse<EntityContext>(request.EntityContext, false, out var origCtx)) return false;
            var got = EntityIo.Parse(Convert.FromBase64String(cand), candCtx);
            var wanted = EntityIo.Parse(Convert.FromBase64String(request.OriginalEntityBase64), origCtx);
            return got is not null && wanted is not null && got.Species == wanted.Species && got.Form != wanted.Form;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException) { return false; }
    }

    private LegalizeResponse ExecuteCore(LegalizeRequest request, CancellationToken cancellation, bool keepUserLanguage = false)
    {
        var op = new Operation(request, Stopwatch.StartNew(), cancellation, _settings.PhaseChanged) { KeepUserLanguage = keepUserLanguage };
        try
        {
            return WithOperations(request, Run(op));
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            return WithOperations(request, op.Fail(ResponseStatus.Cancelled, "Operación cancelada.", null));
        }
        catch (TimeoutExpiredException)
        {
            return WithOperations(request, op.Fail(ResponseStatus.Timeout, "Se agotó el tiempo antes de terminar la búsqueda.",
                "El tiempo límite se alcanzó durante la búsqueda; esto NO prueba que no exista solución.", complete: false));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _settings.Trace?.Invoke("[error] " + ex);
            return WithOperations(request, op.Fail(ResponseStatus.InternalError, "Error interno del motor.", $"{ex.GetType().Name}: {ex.Message}"));
        }
    }

    private sealed class TimeoutExpiredException : Exception;

    /// <summary>Candidato valido de una legalizacion capturada para la accion Adjust, con su registro de razones.</summary>
    private sealed record CapturedLegalization(PKM Candidate, ValidationOutcome Outcome, ChangeLog Log, bool Regenerated);

    private sealed class Operation(LegalizeRequest request, Stopwatch clock, CancellationToken ct, Action<Phase>? phaseChanged = null)
    {
        private Phase _phase;
        public LegalizeRequest Request { get; } = request;
        public Stopwatch Clock { get; } = clock;
        public CancellationToken Ct { get; } = ct;
        public int Attempts { get; set; }
        /// <summary>ADR 0045: alternativa de <see cref="BestRoute"/> a un Pokemon del usuario: aunque lleve origen fijado, el idioma que manda es el del usuario (no el ESP de generar).</summary>
        public bool KeepUserLanguage { get; init; }
        /// <summary>Fase alcanzada; cada cambio real se notifica (una sola vez por fase).</summary>
        public Phase Phase
        {
            get => _phase;
            set
            {
                if (value == _phase) return;
                _phase = value;
                try { phaseChanged?.Invoke(value); }
                catch (Exception ex) when (ex is not OutOfMemoryException) { /* el aviso es informativo: nunca rompe la operacion */ }
            }
        }
        public DateTime Deadline { get; set; }
        public ChangeLog Log { get; } = new();
        /// <summary>Hito 4b (Adjust): <see cref="RunLegalize"/> no optimiza ni sube el nivel ni arma la respuesta: deja el candidato valido en <see cref="Captured"/> (esas operaciones son pasos aparte de Adjust).</summary>
        public bool CaptureOnly { get; set; }
        /// <summary>Release: la reparacion minima solo borro bytes de basura (el Pokemon ya era legal en todo lo demas): ni se optimiza ni se buscan origenes alternativos, PID, IV y EO se quedan tal cual.</summary>
        public bool OnlyTrashCleared { get; set; }
        public CapturedLegalization? Captured { get; set; }
        /// <summary>Hito 3b: motivo DEMOSTRADO por el que la forma pedida se normalizo antes de generar (no se puede guardar en el destino); null si no hubo normalizacion.</summary>
        public string? FormProof { get; set; }

        public void Tick()
        {
            Ct.ThrowIfCancellationRequested();
            if (DateTime.UtcNow >= Deadline) throw new TimeoutExpiredException();
        }

        public LegalizeResponse FailOptimal(OptimizationReport report, bool needsHome = false) => new()
        {
            ProtocolVersion = Protocol.Version,
            RequestId = Request.RequestId,
            ReleaseId = PinnedVersions.ReleaseId,
            DatasetSnapshotId = Request.DatasetSnapshotId,
            Action = Request.Action,
            Status = ResponseStatus.AlreadyOptimal,
            Message = "Sin margen de mejora: los stats de este Pokémon ya son los mejores posibles para su encuentro.",
            Reason = report.LimitReason,
            Optimization = report,
            Summary = needsHome ? [NeedsHomeWarning] : [],   // 0.6.6 (M4): sin mejora, pero sigue sin la matricula de HOME: el aviso no se pierde
            ElapsedMilliseconds = Clock.ElapsedMilliseconds,
            Attempts = Attempts,
            PhaseReached = Phase,
            SearchComplete = report.Exhaustive,
        };

        public LegalizeResponse Fail(ResponseStatus status, string message, string? reason, bool complete = false) => new()
        {
            ProtocolVersion = Protocol.Version,
            RequestId = Request.RequestId,
            ReleaseId = PinnedVersions.ReleaseId,
            DatasetSnapshotId = Request.DatasetSnapshotId,
            Action = Request.Action,
            Status = status,
            Message = message,
            Reason = reason,
            ElapsedMilliseconds = Clock.ElapsedMilliseconds,
            Attempts = Attempts,
            PhaseReached = Phase,
            SearchComplete = complete && status != ResponseStatus.Timeout,
        };
    }

    private LegalizeResponse Run(Operation op)
    {
        var r = op.Request;
        op.Deadline = r.DeadlineUtc?.UtcDateTime ?? DateTime.UtcNow + (r.Action == LegalizeAction.Optimize ? _settings.DefaultOptimizeTimeout : _settings.DefaultTimeout);

        // ---------- Fase A: captura y analisis ----------
        op.Phase = Phase.A_CapturaYAnalisis;
        if (r.ProtocolVersion != Protocol.Version)
            return op.Fail(ResponseStatus.VersionMismatch, "La versión del protocolo no coincide.", $"Solicitud {r.ProtocolVersion}, Worker {Protocol.Version}.");
        if (r.PolicyVersion != Protocol.PolicyVersion)
            return op.Fail(ResponseStatus.VersionMismatch, "La política de legalización no coincide.", $"Solicitud '{r.PolicyVersion}', Worker '{Protocol.PolicyVersion}'.");
        if (!string.Equals(r.ReleaseId, PinnedVersions.ReleaseId, StringComparison.Ordinal))
            return op.Fail(ResponseStatus.VersionMismatch, "La release de la solicitud no es la del Worker.", $"Solicitud '{r.ReleaseId}', Worker '{PinnedVersions.ReleaseId}'.");
        _settings.Trace?.Invoke($"[t] inicio {op.Clock.ElapsedMilliseconds} ms");
        var stale = catalog.StaleReasons(r.DatasetSnapshotId, TrustedCoreSha());
        _settings.Trace?.Invoke($"[t] snapshot comprobado {op.Clock.ElapsedMilliseconds} ms");
        if (stale.Count > 0)
            return op.Fail(ResponseStatus.VersionMismatch, "La instantánea del catálogo no coincide: no se genera nada.", string.Join(" ", stale));

        var problems = RequestValidator.Validate(r, out var bytes);
        if (problems.Count > 0 || bytes is null)
            return op.Fail(ResponseStatus.InvalidInput, "La solicitud no es válida.", string.Join(" ", problems));

        if (!Enum.TryParse<EntityContext>(r.EntityContext, false, out var entityContext) || !entityContext.IsValid)
            return op.Fail(ResponseStatus.InvalidInput, "El contexto de la entidad no es válido.", $"entityContext '{r.EntityContext}'.");

        var original = EntityIo.Parse(bytes, entityContext);
        if (original is null)
            return op.Fail(ResponseStatus.InvalidInput, "Los bytes no son una entidad legible.", $"{bytes.Length} bytes no corresponden a ningún formato de entidad.");
        if (original.GetType().Name != r.EntityRuntimeType || original.Context != entityContext)
            return op.Fail(ResponseStatus.InvalidInput, "El tipo o el contexto de la entidad no coinciden con los bytes.",
                $"Declarado {r.EntityRuntimeType}/{r.EntityContext}; detectado {original.GetType().Name}/{original.Context}.");
        if (original.Species == 0)
            return op.Fail(ResponseStatus.InvalidInput, "La casilla está vacía.", "Especie 0.");
        if (original.Species > 1025)
            return op.Fail(ResponseStatus.InvalidInput, "La especie está fuera del catálogo (1-1025).", $"Especie {original.Species}.");

        if (!Destination.TryCreate(r.Target, out var dest, out var destError))
            return op.Fail(ResponseStatus.UnsupportedSave, "El juego o formato de destino no está soportado.", destError);
        if (r.Options?.AllowHomeTrackerless == true) dest!.TrackerSeverity = Severity.Fishy; // R-009: solo esta solicitud
        var partySize = EntityIo.IsPartySize(original, bytes.Length);

        // La especie debe existir en el destino (tabla personal del destino). Sin la especie no hay correccion que la conserve.
        if (!IsSpeciesInDestination(dest!, original.Species))
            return op.Fail(ResponseStatus.SpeciesNotInDestination, "Esta especie no existe en el juego de destino.",
                $"La especie {original.Species} no está en {dest!.Version}: no hay ninguna corrección que conserve la especie; el Pokémon queda intacto.", complete: true);

        // ALM (hallazgo documentado en docs/alm-fiabilidad.md): Unown con forma 26/27 (! ?) en Gen1/2 provoca un bucle sin fin del Core al fijar la forma.
        if (original.Species == (ushort)Species.Unown && dest!.Generation <= 2 && original.Form > 25)
            return op.Fail(ResponseStatus.UnsupportedData, "Esa forma de Unown no existe en Gen1/2.", $"Unown forma {original.Form} no existe en {dest.Version}.", complete: true);

        // Formato del destino: si la entidad es de otro formato y el Core sabe convertirla (transferencia), se convierte; si no, no hay ruta.
        var working = original;
        if (original.GetType() != dest!.EntityType)
        {
            if (!EntityConverter.IsConvertibleToFormat(original, dest.Generation))
                return op.Fail(ResponseStatus.UnsupportedData, "No existe una ruta de transferencia hacia el formato del destino.",
                    $"{original.GetType().Name} no se puede convertir a {dest.EntityType.Name} con las reglas de PKHeX.");
            var converted = EntityConverter.ConvertToType(original, dest.EntityType, out var cr);
            if (converted is null)
                return op.Fail(ResponseStatus.UnsupportedData, "PKHeX no puede convertir esta entidad al formato del destino.", $"Resultado de la conversión: {cr}.");
            working = converted;
            op.Log.Reason("Format", ReasonCodes.FormatConverted, $"Convertido de {original.GetType().Name} a {dest.EntityType.Name} (transferencia).");
        }

        op.Phase = Phase.A_CapturaYAnalisis;
        _settings.Trace?.Invoke($"[t] destino y formato listos {op.Clock.ElapsedMilliseconds} ms");
        var baseline = _validator.Validate(working, original, dest, requireCatalogGate: false);
        _settings.Trace?.Invoke($"[t] analisis inicial {op.Clock.ElapsedMilliseconds} ms (valido={baseline.Valid}) {string.Join(" ; ", baseline.Proof.Checks.Where(c => !c.Passed).Select(c => c.Name + ":" + c.Detail))}");
        var isValid = baseline.Valid && ReferenceEquals(working, original);

        if (r.Action == LegalizeAction.Optimize)
            return RunOptimize(op, original, working, dest, baseline, partySize);
        if (r.Action == LegalizeAction.Adjust)
            return RunAdjust(op, original, working, dest!, baseline, partySize);

        // R-005: al GENERAR desde la Libreria (origen fijado) el idioma por defecto es ESP; un candidato ya legal en otro idioma no corta la busqueda.
        if (isValid && LanguageAcceptable(IsGenerating(r), original, dest!))
        {
            if (r.Options?.MaxLevel != true)
                return Success(op, ResponseStatus.AlreadyLegal, original, original, dest, baseline, partySize, "El Pokémon ya es legal: no se ha cambiado nada.", optimization: null, exactBytes: bytes);
            // Hito 4b: ya es legal, pero se pidio el nivel maximo legal (no hay nada que legalizar ni que optimizar: solo el nivel).
            var maxed = MaxLevelStep.Apply(original, original, dest!, _validator, baseline, requireCatalogGate: false, op.Deadline, op.Ct, mayRefreshMoves: IsGenerating(r));
            if (maxed.Status != MaxLevelStatus.NotApplicable) op.Log.Note(maxed.Message);
            if (maxed.Status != MaxLevelStatus.Changed)
                return Success(op, ResponseStatus.AlreadyLegal, original, original, dest, baseline, partySize, "El Pokémon ya es legal: no se ha cambiado nada.", optimization: null, op.Log, exactBytes: bytes,
                    finalLevel: maxed.Status == MaxLevelStatus.NotApplicable ? null : maxed.FinalLevel);
            op.Log.Reason("Level", ReasonCodes.LevelMaxed, maxed.Message);
            return Success(op, ResponseStatus.Success, original, maxed.Candidate, dest, maxed.Outcome, partySize, "Nivel subido al máximo legal; el Pokémon ya era legal.", optimization: null, op.Log, finalLevel: maxed.FinalLevel);
        }

        var first = RunLegalize(op, original, working, dest, baseline, partySize);
        return BestRoute(op, first, original, working, dest);
    }

    /// <summary>Alternativas maximas que se prueban (R-004: el motor se quedaba con el primer origen legal aunque otro permita mejores stats).</summary>
    private const int MaxAlternativeRoutes = 5;

    /// <summary>
    /// R-004, «mejores stats posibles»: «Legalizar» daba por bueno el primer origen legal y solo optimizaba DENTRO de el; si ese origen fija o limita los IV (evento, intercambio, regalo con IV
    /// fijos) y otro origen legal de la misma especie permite mas, el resultado no era el mejor alcanzable (muestra medida de R-004: 13 de 120 especies-forma, p. ej. un estatico de Espada con suma 129 frente a 186 de otro origen legal). Aqui, SOLO cuando (a) la suma de IV
    /// no llego al maximo del formato, (b) el usuario no fijo un origen ni trae el entrenador propio de un evento (su identidad se conserva, R-001) y (c) no es un huevo, se prueban hasta
    /// <see cref="MaxAlternativeRoutes"/> encuentros alternativos del Core (distintos del elegido, aprobados en el catalogo) fijando cada uno como origen, y se elige el que MEJOR puntua
    /// con la misma prioridad lexicografica de rasgos (variocolor, sexo, naturaleza, movimientos, habilidad) y, por ultimo, mas IV. Cada alternativa pasa entera por el mismo motor y validador.
    /// </summary>
    private LegalizeResponse BestRoute(Operation op, LegalizeResponse first, PKM original, PKM working, Destination dest)
    {
        var r = op.Request;
        if (first.Status != ResponseStatus.Success || r.OriginEventKey is not null || r.Action != LegalizeAction.Legalize || (r.Options?.OptimizeStats ?? true) == false || op.OnlyTrashCleared) return first;
        if (original.IsEgg || first.CandidateEntityBase64 is null || first.Proof?.EncounterKey is not { } chosenKey) return first;
        if (chosenKey.StartsWith("core-mgdb:", StringComparison.Ordinal)) return first; // una tarjeta de evento (incluso las que reparten el entrenador del receptor) conserva su origen
        if (original.OriginalTrainerName.Length != 0 && original.OriginalTrainerName != dest.Trainer.OT) return first;
        var best = EntityIo.Parse(Convert.FromBase64String(first.CandidateEntityBase64), dest.Context);
        if (best is null || best.IV_HP + best.IV_ATK + best.IV_DEF + best.IV_SPA + best.IV_SPD + best.IV_SPE >= best.MaxIV * 6) return first;

        var alternatives = new List<string>();
        var blank = dest.Sav.BlankPKM;
        blank.Species = working.Species;
        blank.Form = working.Form;
        blank.Gender = blank.GetSaneGender();
        lock (EngineGlobalState.Gate)
        {
            dest.ApplyParseSettings();
            foreach (var enc in CoreVersions.Encounters(blank, dest.Trainer, ReadOnlyMemory<ushort>.Empty))
            {
                if (enc.IsEgg || EncounterKeys.For(enc) is not { } key || !catalog.GenerableKeys.Contains(key)) continue;
                if (EncounterKeys.SameOriginLine(key, chosenKey) || alternatives.Contains(key)) continue;
                if (HomeTrackerRule.IsRequired(enc, dest.Context)) continue; // regla 6
                alternatives.Add(key);
                if (alternatives.Count >= MaxAlternativeRoutes) break;
            }
        }
        if (alternatives.Count == 0) return first;

        var bestScore = TraitScore.Of(original, best);
        var bestResponse = first;
        var userOwned = !IsGenerating(r);
        var wantedLanguage = userOwned ? original.Language : LanguagePolicy.Supports(dest, LanguagePolicy.Preferred) ? (int)LanguagePolicy.Preferred : best.Language;
        foreach (var key in alternatives)
        {
            var left = op.Deadline - DateTime.UtcNow;
            if (left < TimeSpan.FromSeconds(3) || op.Ct.IsCancellationRequested) break;
            var deadline = DateTime.UtcNow + TimeSpan.FromTicks(Math.Min(left.Ticks / 2, _settings.MaxPerStep.Ticks * 2));
            LegalizeResponse alt;
            try { alt = Execute(r with { OriginEventKey = key, RequestId = r.RequestId, DeadlineUtc = new DateTimeOffset(deadline) }, op.Ct, keepUserLanguage: userOwned); }
            catch (OperationCanceledException) { break; }
            if (alt.Status != ResponseStatus.Success || alt.CandidateEntityBase64 is null) continue;
            var cand = EntityIo.Parse(Convert.FromBase64String(alt.CandidateEntityBase64), dest.Context);
            if (cand is null) continue;
            // ADR 0045 (R-005 regla 4): una alternativa nunca cambia el idioma que ya conserva el resultado actual (una tarjeta de evento en otro idioma daria mas IV a costa del idioma del usuario).
            if (!LanguagePolicy.AlternativeKeepsLanguage(best, cand, wantedLanguage, dest)) continue;
            var score = TraitScore.Of(original, cand);
            if (score.CompareTo(bestScore) > 0) { bestScore = score; bestResponse = alt; }
            if (ReferenceEquals(bestResponse, alt) && score.IvSum >= cand.MaxIV * 6) break; // ya es el maximo del formato
        }
        if (ReferenceEquals(bestResponse, first)) return first;
        var before = first.Optimization?.SumAfter;
        var after = bestResponse.Optimization?.SumAfter;
        return bestResponse with
        {
            Summary = [.. bestResponse.Summary, $"Origen elegido por mejores stats: otro origen legal permite más IV (suma {before} -> {after})."],
            Attempts = first.Attempts + bestResponse.Attempts,
            ElapsedMilliseconds = op.Clock.ElapsedMilliseconds,
        };
    }

    /// <summary>R-005 / Hito 4b: la solicitud GENERA un Pokemon (origen fijado desde la Libreria, o <c>NewPokemon</c>) en vez de reparar uno del usuario.</summary>
    private static bool IsGenerating(LegalizeRequest r) => r.OriginEventKey is not null || r.Options?.NewPokemon == true;

    /// <summary>La accion Adjust devuelve SIEMPRE su lista de operaciones, tambien cuando falla antes de llegar a ellas (solicitud invalida, version distinta, plazo...).</summary>
    private static LegalizeResponse WithOperations(LegalizeRequest request, LegalizeResponse response)
    {
        if (request.Action != LegalizeAction.Adjust || response.Operations.Count > 0 || request.Options?.Adjust is not { Any: true } a) return response;
        var failed = response.Status is ResponseStatus.Timeout or ResponseStatus.Cancelled or ResponseStatus.InternalError;
        return response with
        {
            Operations = [.. AdjustOperationNames.Requested(a).Select(n => new OperationOutcome
            {
                Operation = n,
                Status = failed ? OperationStatus.Failed : OperationStatus.NotApplicable,
                Message = failed ? "No se ha podido terminar: " + response.Message : "No se ha aplicado: " + response.Message,
                Reason = response.Reason,
            })],
        };
    }

    /// <summary>R-005: sin origen fijado, el idioma del Pokemon del usuario se respeta tal cual; con origen fijado (generar), solo vale el espanol (o que el formato no lo admita).</summary>
    private static bool LanguageAcceptable(bool generating, PKM pk, Destination dest)
        => !generating || pk.Language == (int)LanguagePolicy.Preferred || !LanguagePolicy.Supports(dest, LanguagePolicy.Preferred);

    private static string TrustedCoreSha() => BinaryIdentity.Current.CoreSha256;

    private static bool IsSpeciesInDestination(Destination dest, ushort species)
    {
        try
        {
            var pi = dest.Personal;
            if (species > dest.Sav.MaxSpeciesID) return false;
            return pi.IsSpeciesInGame(species);
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException) { return false; }
    }

    // ============================================================ Accion A: Legalizar
    private LegalizeResponse RunLegalize(Operation op, PKM original, PKM working, Destination dest, ValidationOutcome baseline, bool partySize)
    {
        var optimizeStats = op.Request.Options?.OptimizeStats ?? true;

        // ---------- Origen fijado (Libreria / «anadir desde el catalogo»): solo se genera dentro de esa entrada aprobada ----------
        var generating = IsGenerating(op.Request); // R-005: generar desde la Libreria (con o sin encuentro concreto: las crias no lo tienen) o NewPokemon (Hito 4b)
        var pinnedKey = op.Request.OriginEventKey == LanguagePolicy.LibraryEggKey ? null : op.Request.OriginEventKey;
        if (pinnedKey is not null && !catalog.GenerableKeys.Contains(pinnedKey) && !pinnedKey.EndsWith(":egg", StringComparison.Ordinal))
            return op.Fail(ResponseStatus.UnsupportedData, "El origen pedido no está aprobado para generar.",
                $"La clave '{pinnedKey}' no está entre los orígenes aprobados del catálogo (v_encounter_generable; regla 8: se excluyen los encuentros no lanzados, de origen desconocido o solo de investigación).", complete: true);

        // ---------- Propuesta 3 (Hito 3): la forma pedida debe existir y poder guardarse en el destino ANTES de generar ----------
        var wish = original; // rasgos deseados: la entidad del usuario, con la forma ya normalizada si no existe en el destino
        var formVerdict = FormGuard.Check(working, dest, catalog);
        if (formVerdict.NoEquivalent)
            return op.Fail(ResponseStatus.UnsupportedData, "Ninguna forma de esta especie se puede guardar en el destino.", formVerdict.Reason, complete: true);
        // ---------- Hito 5c: cría de la Librería. Se fija el HUEVO que el Core enumera para la especie en el destino (sin él, el generador devolvía otro origen y la entrada prometía lo que no daba) ----------
        if (op.Request.OriginEventKey == LanguagePolicy.LibraryEggKey)
        {
            var eggWish = working;
            if (formVerdict.Changed) { eggWish = working.Clone(); eggWish.Form = formVerdict.NewForm; }
            var egg = OriginKeyResolver.FindEgg(eggWish, dest);
            if (egg is null || EncounterKeys.For(egg) is not { } eggKey)
                return op.Fail(ResponseStatus.NoSolution, "PKHeX no ofrece ninguna cría de esta especie en este juego.",
                    $"El Core no enumera ningún huevo para la especie {working.Species} (forma {eggWish.Form}) en {dest.Version}: no hay origen de cría que fijar y Pokekeep no lo sustituye por otro origen.", complete: true);
            pinnedKey = eggKey;
        }
        if (formVerdict.Changed)
        {
            wish = working.Clone();
            wish.Form = formVerdict.NewForm;
            if (wish is IFormArgument fa) fa.FormArgument = 0;
            op.FormProof = formVerdict.Reason;
            op.Log.Reason("Form", ReasonCodes.FormFixed, formVerdict.Reason!);
            op.Log.Note(formVerdict.Reason!);
        }
        // Meowstic: la forma es el sexo (FormVerifier.cs:121, (form & 1) != pk.Gender => invalido): se ajusta el sexo deseado a la forma pedida (regla 4: ajustar sin dialogo).
        if (wish.Species == (ushort)Species.Meowstic && wish.Form < 2 && wish.Gender != wish.Form)
        {
            if (ReferenceEquals(wish, original)) wish = wish.Clone();
            wish.Gender = wish.Form;
            op.Log.Reason("Gender", ReasonCodes.GenderFixed, "El sexo de Meowstic se corresponde con su forma. Referencia técnica: FormVerifier.cs:121.");
        }

        // ---------- Fase B: reparacion minima ----------
        op.Phase = Phase.B_ReparacionMinima;
        op.Tick();
        var repairLog = new ChangeLog();
        var repaired = MinimalRepair.Run(working, dest, repairLog);
        _settings.Trace?.Invoke($"[t] reparacion minima {op.Clock.ElapsedMilliseconds} ms");
        var repairedOutcome = _validator.Validate(repaired, original, dest, requireCatalogGate: false);
        _settings.Trace?.Invoke($"[t] validacion de lo reparado {op.Clock.ElapsedMilliseconds} ms (valido={repairedOutcome.Valid}) fallos={string.Join(",", repairedOutcome.Failures)} {string.Join(" ; ", repairedOutcome.Proof.Checks.Where(c => !c.Passed).Select(c => c.Name + ":" + c.Detail))}");
        op.Attempts++;
        // Una reparacion no puede esquivar el origen fijado ni dejar una forma que no existe en el destino.
        var repairKeepsOrigin = pinnedKey is null || EncounterKeys.SameOriginLine(repairedOutcome.EncounterKey, pinnedKey);
        if (repairedOutcome.Valid && repairKeepsOrigin && LanguageAcceptable(generating && !op.KeepUserLanguage, repaired, dest))
        {
            foreach (var n in repairLog.Notes) op.Log.Note(n);
            if (repairLog.TrashCleared && !repairLog.OtherRepairs) { op.OnlyTrashCleared = true; optimizeStats = false; }
            return FinishLegalize(op, original, repaired, dest, repairedOutcome, repairLog, partySize, optimizeStats, regenerated: false);
        }

        // ---------- Fases C y D: regeneracion y relajacion automatica ----------
        LegalityAnalysis? analysis;
        lock (EngineGlobalState.Gate)
        {
            dest.ApplyParseSettings();
            analysis = new LegalityAnalysis(working, dest.Personal, dest.SlotType);
        }
        var keepEncounter = OriginalEncounterResolver.Resolve(original, dest, analysis);
        if (pinnedKey is not null)
        {
            keepEncounter = OriginKeyResolver.Resolve(pinnedKey, wish, dest);
            if (keepEncounter is null)
                return op.Fail(ResponseStatus.NoSolution, "El origen pedido no existe para esta especie en este destino.",
                    $"PKHeX no enumera la entrada '{pinnedKey}' para la especie {wish.Species} (forma {wish.Form}) en {dest.Version}.", complete: true);
        }
        // Una entrada de la Libreria que es un HUEVO de evento (tarjeta de huevo): lo que se anade es el huevo, no un Pokemon eclosionado; la plantilla en blanco se convierte en huevo para que el
        // estado de huevo se conserve (validador: EggStatePreserved).
        if (generating && keepEncounter is DataMysteryGift { IsEgg: true } && keepEncounter.Species == original.Species && !original.IsEgg) // solo la entrada DIRECTA: una evolucion de un huevo es el Pokemon eclosionado
        {
            var asEgg = original.Clone();
            asEgg.IsEgg = true;
            if (ReferenceEquals(wish, original)) wish = asEgg;
            original = asEgg;
        }
        // R-005 regla 2: si el evento fijado tiene una version ESP (mismo evento, otra tarjeta por idioma), se elige SIEMPRE esa tarjeta.
        if (pinnedKey is not null && keepEncounter is not null && LanguagePolicy.SpanishSibling(keepEncounter, wish, dest, catalog) is { } esCard && EncounterKeys.For(esCard) is { } esKey)
        {
            op.Log.Note($"Se ha elegido la tarjeta en español (ESP) del mismo evento en lugar de la de {LanguagePolicy.Name(LanguagePolicy.FixedLanguageOf(keepEncounter) ?? 0)}.");
            keepEncounter = esCard;
            pinnedKey = esKey;
        }
        // Origen fijado que exige un rastreador de HOME: no hay nada que generar (regla 6); se dice ya, sin gastar la busqueda.
        if (pinnedKey is not null && keepEncounter is not null && dest.TrackerSeverity != Severity.Fishy && HomeTrackerRule.IsRequired(keepEncounter, dest.Context))
        {
            var proof = HomeTrackerRule.Prove(keepEncounter, dest, wish, _validator);
            return op.Fail(ResponseStatus.UnsupportedData, "Este origen exige una matrícula (rastreador) de HOME que el producto no puede crear.",
                "Imposible sin servidor de HOME: el origen pedido exige la matrícula (rastreador) de transferencia de Pokémon HOME y el PKHeX oficial la marca como inválida por defecto. Regla 6: nunca se inventa un identificador de servidor; solo se conserva el de una entidad que ya lo trae. "
                + proof.Evidence + " " + HomeTrackerRule.CoreReferences, complete: true);
        }
        // 0.6.5 (ADR 0052): sin pista de origen ni origen fijado, primero se prueban los encuentros de ORIGEN PROPIO (capturado o criado en este juego, o transferido con el EO = jugador) por orden de
        // preferencia; solo si ninguno da un candidato valido en ese escalon se vuelve a la busqueda general (que puede acabar en un evento o un intercambio con EO fijo). Los rasgos del usuario mandan:
        // un rasgo que solo cumple un origen ajeno no se suelta para conseguir el EO propio.
        var unfitNotes = new List<string>();
        // Un Pokemon del usuario con claves de origen AJENO (otro idioma que la partida, o un entrenador original que no es el jugador) conserva su origen: ahi no se prefiere el propio.
        var foreignClues = !generating && ((dest.Generation > 2 && original.Language != dest.Trainer.Language) || (original.OriginalTrainerName.Length != 0 && original.OriginalTrainerName != dest.Trainer.OT));
        IReadOnlyList<IEncounterable> preferred = [];
        if (OriginSwitches.PreferOwnOrigin && !foreignClues && pinnedKey is null
            && (keepEncounter is null || (keepEncounter is IEncounterTemplate kt && OriginPreference.Rank(kt, dest) > OriginRank.OtherGame)))
        {
            // La pista del analisis de un Pokemon ilegal que apunta a un encuentro con EO fijo (intercambio, regalo, evento) es solo una suposicion si el Pokemon lleva el entrenador del jugador: si hay origen propio, manda.
            preferred = PreferredHints(wish, original, dest, unfitNotes);
            if (preferred.Count > 0 && keepEncounter is not null)
            {
                _settings.Trace?.Invoke($"[origen-propio] la pista del analisis ({keepEncounter.GetType().Name}) fija el EO: se prefiere un origen propio");
                keepEncounter = null;
            }
        }
        _settings.Trace?.Invoke($"[origen-propio] pistas preferidas={preferred.Count} ({string.Join(",", preferred.Select(h => h.GetType().Name + "/" + h.Version))})");
        var ladder = Relaxation.Ladder(wish, analysis, optimizeStats, keepEncounter);
        _settings.Trace?.Invoke($"[ladder] pasos={string.Join(",", ladder.Select(s => s.Code))} keepEncounter={keepEncounter?.GetType().Name}/{(keepEncounter is null ? "" : EncounterKeys.For(keepEncounter))} analisis={analysis.EncounterOriginal.GetType().Name}");
        PKM? best = null;
        ValidationOutcome? bestOutcome = null;
        TraitScore bestScore = default;
        var encountersTried = 0;
        var trackerBlocked = 0;
        var originalKey = pinnedKey ?? (keepEncounter is null ? null : EncounterKeys.For(keepEncounter));
        var anyTimedOut = false;
        var userOwned = !generating || op.KeepUserLanguage;
        var languages = LanguagePolicy.Candidates(original, userOwned, keepEncounter, dest, op.Request.Options?.RequestedLanguage is > 0 and var rl ? (LanguageID)rl : null);
        _settings.Trace?.Invoke($"[idioma] candidatos={string.Join(",", languages)} usuario={userOwned}");
        var bestEquivalentOrigin = false;
        var bestLangKept = false;
        // Gen1/2 no guardan el idioma (solo japones o internacional): ahi nunca puntua.
        bool LanguageKept(PKM cand, LanguageID wanted) => dest.Generation <= 2 || !LanguagePolicy.Supports(dest, wanted) || cand.Language == (int)wanted;

        // Hito 4b, decision (a) del propietario «Respetar mi naturaleza» (ADR 0024, R-001 punto 3): si la naturaleza del Pokemon es legal para ALGUN origen valido, se conserva y despues
        // se buscan los mejores IV compatibles con ella (StatOptimizer ya conserva naturaleza, sexo, variocolor y habilidad). Antes, el primer candidato valido de un escalon que aun exigia la
        // naturaleza cerraba la busqueda aunque la hubiera perdido (p. ej. una plantilla con naturaleza fija). Ahora ese candidato queda de RESERVA y se sigue buscando, con las demas
        // estrategias y en los escalones que todavia exigen la naturaleza; solo si ninguno la conserva se usa la reserva y el resumen lo dice.
        // Excepciones: un origen fijado (Libreria: la entrada manda) y un Pokemon con la identidad de OTRO entrenador (evento o intercambio con su OT/TID): esa identidad se conserva antes que la
        // naturaleza (como en BestRoute), y si la naturaleza cambia se explica igual.
        var foreignIdentity = original.OriginalTrainerName.Length != 0 && original.OriginalTrainerName != dest.Trainer.OT;
        var natureFirst = pinnedKey is null && !generating && !foreignIdentity
                          && ladder.FirstOrDefault(s => s.Code == "Exact")?.Traits.Nature == wish.Nature; // Toxtricity: si la forma pedida fija otra naturaleza, la forma manda
        bool NatureDemanded(RelaxationStep step) => natureFirst && RelaxationExplainer.KeepsNature(step.Code);
        (PKM Pk, ValidationOutcome Outcome, TraitScore Score, RelaxationStep Step, bool Equivalent)? reserve = null;

        void AcceptNotes(RelaxationStep step, PKM chosen, bool equivalentOrigin)
        {
            if (equivalentOrigin)
            {
                const string text = "Origen equivalente: se creó desde la plantilla pedida y PKHeX lo valida, aunque lo identifica con otro encuentro indistinguible (el formato o el propio encuentro repiten los mismos datos).";
                op.Log.Note($"[{ReasonCodes.OriginEquivalent}] {text}");
            }
            if (step.Code is not ("KeepEncounter" or "Exact"))
            {
                op.Log.Note($"Rasgos relajados hasta '{step.Code}': {step.Description}.");
                RelaxationExplainer.Explain(step.Code, original, chosen, op.Log);
            }
            else if (step.Code == "KeepEncounter")
                op.Log.Note("Se ha conservado el mismo encuentro (su identidad de origen).");
        }

        // Un escalon de la escalera con un encuentro como pista (el del analisis o, en el respaldo, uno enumerado por el Core). Devuelve true si hallo un candidato valido.
        bool RunStep(RelaxationStep step, IEncounterable? hint, bool sameKeyRequired, bool requireOwnOrigin = false, TimeSpan? budget = null)
        {
            op.Tick();
            op.Phase = step.Code is "KeepEncounter" or "Exact" ? Phase.C_Regeneracion : Phase.D_RelajacionAutomatica;
            var stepDeadline = Min(op.Deadline, DateTime.UtcNow + _settings.MaxPerStep);
            if (budget is { } hintBudget) stepDeadline = Min(stepDeadline, DateTime.UtcNow + hintBudget);   // pasada de origen propio: cada pista tiene su propio tope
            // R-005: dentro de cada escalon se prueban los idiomas por orden de preferencia (ESP antes que el del entrenador); el plazo del escalon se reparte entre ellos.
            for (var li = 0; li < languages.Count && best is null; li++)
            {
            var genDest = LanguagePolicy.ForLanguage(dest, languages[li]);
            // El idioma preferido se queda con 3/4 del tiempo que quede (casi siempre es el bueno y puede necesitar buscar mucho: p. ej. 20.000 muestras de una distribucion de Gen3); el ultimo, con el resto.
            var langDeadline = li == languages.Count - 1 ? stepDeadline : DateTime.UtcNow + (stepDeadline - DateTime.UtcNow) * 3 / 4;
            foreach (var strategy in strategies)
            {
                // ALM (APILegality.GetPokemonFromEncounter) repite ConvertToPKM hasta que el PID da la evolucion de Wurmple pedida; con el Wurmple del Pokewalker (PID fijo, solo HGSS) no termina nunca
                // y no admite cancelacion (docs/alm-fiabilidad.md): en HGSS la linea de Wurmple se genera solo con la plantilla del Core.
                if (strategy.Name == "ALM" && IsWurmpleLineOnPokewalkerGame(wish, dest)) continue;
                var ctx = new GenerationContext
                {
                    Original = wish,
                    Dest = genDest,
                    Traits = step.Traits,
                    Step = step,
                    OriginalEncounter = hint,
                    DeadlineUtc = langDeadline,
                    Cancellation = op.Ct,
                    MaxAttempts = op.Request.Options?.MaxAttemptsPerEncounter is > 0 and var m ? m : _settings.MaxAttemptsPerStep,
                };
                foreach (var generated in strategy.Generate(ctx))
                {
                    op.Attempts++;
                    encountersTried++;
                    // Los generadores devuelven el huevo ya eclosionado: un huevo debe seguir siendo un huevo (se deshace la eclosion y el Core decide si es legal).
                    var cand = generated;
                    if (original.IsEgg && !cand.IsEgg)
                    {
                        var egg = EggRestorer.TryRestore(cand, dest);
                        if (egg is null) { _settings.Trace?.Invoke($"[{step.Code}/{strategy.Name}] huevo no restaurable: especie={cand.Species} forma={cand.Form}"); continue; }
                        cand = egg;
                    }
                    cand = ImportNormalizer.Apply(cand, dest);
                    TrackerlessRepair.StripRibbonsInvalidByDefault(cand, dest); // R-009: solo con el ajuste explicito; el Core decide despues
                    var outcome = _validator.Validate(cand, original, dest, requireCatalogGate: true);
                    if (outcome.BlockedByHomeTracker) trackerBlocked++;
                    _settings.Trace?.Invoke($"[{step.Code}/{strategy.Name}] valido={outcome.Valid} esp={cand.Species} nv={cand.CurrentLevel} mj={cand.CurrentHandler} enc={outcome.EncounterKey} fallos={string.Join(",", outcome.Failures)} bloqueoHOME={outcome.BlockedByHomeTracker}{(outcome.Valid ? "" : " informe=" + string.Join(" / ", outcome.Proof.Report.Where(l => l.Contains("nvalid", StringComparison.OrdinalIgnoreCase) || l.Contains("nválid", StringComparison.OrdinalIgnoreCase)).Take(10)) + string.Join(" | ", outcome.Proof.Checks.Where(c => !c.Passed && c.Detail is { Length: > 0 }).Select(c => c.Name + ":" + c.Detail)))}");
                    // «Mismo encuentro»: un candidato que cae en otro encuentro no cuenta como resultado de este escalon (lo regenerara el siguiente).
                    var equivalentOrigin = false;
                    if (sameKeyRequired && originalKey is not null && outcome.EncounterKey is { } ck && !EncounterKeys.SameOriginLine(ck, originalKey))
                    {
                        // Un Pokemon creado por la plantilla fijada puede ser indistinguible de otro encuentro (Gen1/2 no guardan el origen; un evento puede repetir los datos de un estatico) y el Core lo identifica con ese.
                        if (!(outcome.Valid && IsOriginEquivalent(strategy, hint, dest, pinnedKey, originalKey))) continue;
                        equivalentOrigin = true;
                    }
                    // Hito 3b: la forma pedida es obligatoria hasta el escalon que la suelta (ALM sustituye la forma en silencio cuando no puede darla): un candidato con otra forma
                    // no resuelve este escalon; solo «BaseForm» y «Defaults» admiten una forma distinta.
                    if (outcome.Valid && step.Code is not ("BaseForm" or "Defaults") && cand.Form != step.Traits.Form)
                    {
                        _settings.Trace?.Invoke($"[{step.Code}/{strategy.Name}] candidato valido descartado: forma {cand.Form} != pedida {step.Traits.Form}");
                        continue;
                    }
                    // 0.6.5 (ADR 0052): en la pasada de «origen propio primero» solo vale un candidato cuyo EO es el jugador; si el Core lo identifica con un encuentro de EO fijo o de evento, no cuenta.
                    if (outcome.Valid && requireOwnOrigin && outcome.Origin is not { OwnerIsPlayer: true })
                    {
                        _settings.Trace?.Invoke($"[{step.Code}/{strategy.Name}] candidato valido descartado: no es de origen propio ({outcome.Origin?.Rank})");
                        continue;
                    }
                    // 0.6.6 (M1, decision 5): el nombre del entrenador no se recorta en ninguna pasada. Un origen de Gen 1-5 con el EO del jugador cuyo nombre no cabe sale cortado por el Core:
                    // no vale; se explica en «Ver cambios» y se sigue buscando otro origen.
                    if (outcome.Valid && outcome.Origin is { } assessed
                        && TrainerNameFit.AltersPlayerName(cand, original, dest, assessed.Rank <= OriginRank.OtherGame))
                    {
                        var note = TrainerNameFit.Explain(dest.Trainer.OT, assessed.EncounterGeneration == 0 ? cand.Generation : assessed.EncounterGeneration, (LanguageID)dest.Trainer.Language);
                        if (!unfitNotes.Contains(note)) unfitNotes.Add(note);
                        _settings.Trace?.Invoke($"[{step.Code}/{strategy.Name}] candidato valido descartado: el nombre del entrenador saldria recortado ('{cand.OriginalTrainerName}')");
                        continue;
                    }
                    if (outcome.Valid)
                    {
                        var score = TraitScore.Of(wish, cand);
                        // R-005 regla 4 (ADR 0045): el idioma que se pedia en este turno (el del usuario, o ESP al generar) pesa MAS que cualquier rasgo: un encuentro que reparte otro idioma
                        // (p. ej. una tarjeta de evento en espanol para un Pokemon ingles) solo se acepta si ningun candidato valido conserva el idioma. Antes el idioma no puntuaba y el ganador
                        // era el de mayor suma de IV, que es azar (el Core reparte los IV): el idioma del usuario cambiaba solo una de cada pocas veces.
                        var langKept = LanguageKept(cand, languages[li]);
                        if (best is null || (langKept && !bestLangKept) || (langKept == bestLangKept && score.CompareTo(bestScore) > 0))
                        {
                            best = cand; bestOutcome = outcome; bestScore = score; bestEquivalentOrigin = equivalentOrigin; bestLangKept = langKept;
                        }
                        if (langKept && score.Satisfies(step.Traits, wish)) break;
                    }
                    if (ctx.Expired) break;
                }
                anyTimedOut |= ctx.Report.TimedOut;
                // Decision (a): un candidato que perdio la naturaleza no cierra el escalon mientras otra estrategia pueda conservarla.
                if (best is not null && bestLangKept && (bestScore.NatureKept || !NatureDemanded(step))) break;
                op.Tick();
            }
            }
            if (best is null) return false;
            if (NatureDemanded(step) && !bestScore.NatureKept)
            {
                // Reserva: valido, pero sin la naturaleza del usuario. Se sigue buscando en los escalones que aun la exigen.
                if (reserve is null || bestScore.CompareTo(reserve.Value.Score) > 0)
                    reserve = (best, bestOutcome!, bestScore, step, bestEquivalentOrigin);
                _settings.Trace?.Invoke($"[naturaleza] {step.Code}: candidato valido con {best.Nature} en vez de {wish.Nature}: queda de reserva");
                best = null; bestOutcome = null; bestScore = default; bestEquivalentOrigin = false; bestLangKept = false;
                return false;
            }
            AcceptNotes(step, best, bestEquivalentOrigin);
            return true;
        }

        var preferredClock = System.Diagnostics.Stopwatch.StartNew();
        foreach (var step in ladder)
        {
            if (reserve is not null && !NatureDemanded(step)) break; // ya hay un candidato valido: los escalones que sueltan la naturaleza no lo mejorarian
            if (preferred.Count > 0 && step.Code != "KeepEncounter")
            {
                var own = false;
                // Cada pista tiene un tope corto y la pasada entera otro: una pista que no da (p. ej. una forma de Unown que ese hueco no reparte) no puede gastar el plazo de la busqueda general.
                var timedOutBefore = anyTimedOut;
                foreach (var hint in preferred)
                {
                    if (preferredClock.Elapsed > PreferredTotalBudget || DateTime.UtcNow >= op.Deadline) break;
                    if (RunStep(step, hint, false, requireOwnOrigin: true, budget: PreferredHintBudget)) { own = true; break; }
                }
                anyTimedOut = timedOutBefore || (own && anyTimedOut);
                if (own) break;
            }
            if (RunStep(step, keepEncounter, step.Code == "KeepEncounter" || pinnedKey is not null)) break;
        }

        // Hito 3b, respaldo por enumeracion del Core: si ningun escalon dio un candidato valido y no hay origen fijado, se prueba, uno a uno, cada encuentro que el Core enumera
        // para la especie y la forma en este destino (aprobado en el catalogo y sin rastreador de HOME), pasandoselo a las estrategias como pista. El validador decide igual que siempre.
        if (best is null && pinnedKey is null && DateTime.UtcNow < op.Deadline)
        {
            var hints = EnumerateHints(wish, original, dest, keepEncounter, unfitNotes, out var blockedByHome);
            trackerBlocked += blockedByHome;
            _settings.Trace?.Invoke($"[respaldo] pistas del Core={hints.Count} (bloqueadas por HOME={blockedByHome})");
            if (hints.Count > 0)
            {
                foreach (var step in ladder)
                {
                    if (step.Code == "KeepEncounter") continue;
                    if (reserve is not null && !NatureDemanded(step)) break;
                    var found = false;
                    foreach (var hint in hints)
                    {
                        if (RunStep(step, hint, false)) { found = true; break; }
                    }
                    if (found) break;
                }
            }
        }
        // Decision (a): ningun escalon que exigia la naturaleza dio un candidato que la conserve; se usa la reserva (el mejor valido hallado) y el resumen explica el cambio.
        if (best is null && reserve is { } fallback)
        {
            best = fallback.Pk; bestOutcome = fallback.Outcome; bestScore = fallback.Score;
            AcceptNotes(fallback.Step, best, fallback.Equivalent);
        }
        // Origen fijado de sexo fijo que contradice la FORMA pedida (Oinkologne hembra desde un regalo macho): el escalon «BaseForm» devolvia un candidato legal con OTRA forma, es decir, el motor
        // relajaba lo que el usuario pidio de forma explicita. Si el Core demuestra que esa forma no puede salir de ese encuentro (sexo fijado por la tarjeta contra la forma de un solo sexo), se declara la
        // imposibilidad en vez de ofrecer otra forma. Si hay un candidato con la forma pedida (otra tarjeta de la misma linea), no se toca.
        if (best is not null && pinnedKey is not null && keepEncounter is not null && best.Form != wish.Form && FixedGenderProof.TryDisprove(keepEncounter, wish, dest, out var formGenderEvidence))
            return op.Fail(ResponseStatus.NoSolution, "La forma pedida no puede salir de un encuentro de sexo fijo distinto.", "Imposible, demostrado con PKHeX: " + formGenderEvidence, complete: true);
        if (best is null || bestOutcome is null)
        {
            if (DateTime.UtcNow >= op.Deadline || anyTimedOut)
                throw new TimeoutExpiredException();
            // Hito 3b: la especie deseada no se alcanza por evolucion desde el encuentro fijado (grafo finito del Core).
            if (pinnedKey is not null && keepEncounter is not null && EvolutionProof.TryDisprove(keepEncounter, wish, dest, out var evoEvidence))
                return op.Fail(ResponseStatus.NoSolution, "La especie pedida no es alcanzable por evolución desde el origen fijado.", "Imposible, demostrado con PKHeX: " + evoEvidence, complete: true);
            if (pinnedKey is not null && VivillonFormProof.TryDisprove(keepEncounter, wish, dest, out var vivEvidence))
                return op.Fail(ResponseStatus.NoSolution, "Ese patrón de Vivillon no se obtiene de este origen.", "Imposible, demostrado con PKHeX: " + vivEvidence, complete: true);
            if (pinnedKey is not null && EvolveOnTradeProof.TryDisprove(keepEncounter, wish, out var tradeEvidence))
                return op.Fail(ResponseStatus.NoSolution, "Este intercambio solo existe evolucionado.", "Imposible, demostrado con PKHeX: " + tradeEvidence, complete: true);
            if (pinnedKey is not null && Gen1TradeNicknameProof.TryDisprove(keepEncounter, dest, out var nickEvidence))
                return op.Fail(ResponseStatus.NoSolution, "Ese intercambio no existe en una partida de este idioma.", "Imposible, demostrado con PKHeX: " + nickEvidence, complete: true);
            if (pinnedKey is not null && KoreanGtsProof.TryDisprove(keepEncounter, dest, out var gtsEvidence))
                return op.Fail(ResponseStatus.NoSolution, "Esa tarjeta coreana con la Cinta Clásica no puede existir en una partida de Gen4 no coreana.", "Imposible, demostrado con PKHeX: " + gtsEvidence, complete: true);
            if (pinnedKey is not null && PokewalkerWurmpleProof.TryDisprove(keepEncounter, wish, dest, out var wurmpleEvidence))
                return op.Fail(ResponseStatus.NoSolution, "Esa rama de Wurmple no sale del Pokéwalker con los datos de esta partida.", "Imposible, demostrado con PKHeX: " + wurmpleEvidence, complete: true);
            if (pinnedKey is not null && FixedGenderProof.TryDisprove(keepEncounter, wish, dest, out var genderEvidence))
                return op.Fail(ResponseStatus.NoSolution, "La especie pedida no puede salir de un encuentro de sexo fijo distinto.", "Imposible, demostrado con PKHeX: " + genderEvidence, complete: true);
            var everyRoute = pinnedKey is null && dest.TrackerSeverity != Severity.Fishy && HomeTrackerRule.BlocksEveryRoute(wish, dest, out _);
            if (everyRoute)
                return op.Fail(ResponseStatus.UnsupportedData, "Este origen exige una matrícula (rastreador) de HOME que el producto no puede crear.",
                    "Imposible sin servidor de HOME: TODAS las rutas que PKHeX enumera hacia este destino exigen la matrícula (rastreador) de transferencia de Pokémon HOME y el PKHeX oficial la marca como inválida por defecto. Regla 6: nunca se inventa un identificador de servidor; solo se conserva el de una entidad que ya lo trae. "
                    + HomeTrackerRule.CoreReferences, complete: true);
            if (trackerBlocked > 0 && dest.TrackerSeverity != Severity.Fishy)
            {
                // Hito 3b: ¿hay alguna ruta SIN rastreador de HOME? Se recorren TODOS los encuentros que el Core enumera para la especie y la forma (FormReachability): los que exigen HOME se cuentan
                // como bloqueados (regla 6) y de los demas se crea el candidato, se evoluciona hasta lo pedido y se analiza; si todos fallan por motivos estructurales (sin encuentro, forma o
                // evolucion validos: no dependen del PID ni de los IV), no existe ningun origen sin HOME y la imposibilidad esta demostrada (p. ej. Wyrdeer y Ursaluna en Escarlata/Purpura).
                var reach = FormReachability.Prove(wish, dest);
                if (reach.Unreachable)
                    return op.Fail(ResponseStatus.UnsupportedData, "Sin una matrícula (rastreador) de HOME que el producto no puede crear, esta especie no tiene ningún origen legal en este destino.",
                        "Imposible sin servidor de HOME (regla 6: nunca se inventa un identificador de servidor): " + reach.Evidence + " " + HomeTrackerRule.CoreReferences, complete: true);
                return op.Fail(ResponseStatus.UnsupportedData, "Los candidatos hallados exigen una matrícula (rastreador) de HOME que el producto no puede crear.",
                    "Los candidatos que PKHeX acepta en todo salvo la matrícula (rastreador) de HOME (regla 6: no se inventa) son los únicos hallados; no se ha enumerado todo el dominio, así que no se declara imposible. " + HomeTrackerRule.CoreReferences);
            }
            if (wish.Form != 0 || original.Form != 0)
            {
                var ev = EvaluateRoutes(op, wish, dest);
                if (ev.Verdict == TransferRoutes.RouteVerdict.ProvenUnreachable)
                    return op.Fail(ResponseStatus.NoSolution, "No existe ninguna ruta legal que produzca esta especie con esta forma en el destino.",
                        "Demostrado con PKHeX: " + ev.Reason + " " + HomeTrackerRule.CoreReferences, complete: true);
            }
            // 0.6.6 (M1): si se descartaron origenes porque el nombre del entrenador no cabe, la respuesta lo dice con honestidad (nada se recorta ni se inventa).
            if (unfitNotes.Count > 0)
                return op.Fail(ResponseStatus.NoSolution, "No se ha encontrado un origen que pueda llevar el nombre de tu entrenador.",
                    string.Join(" ", unfitNotes) + " Con los demás orígenes posibles no se ha encontrado una forma de hacerlo legal. Pokekeep no recorta ni inventa el nombre del entrenador; no se ha cambiado nada.", complete: false);
            return NoSolution(op, original, dest, encountersTried);
        }

        // R-005: el idioma del resultado se explica cuando no es el esperado (ESP al generar; el del usuario si lo tenia y era legal).
        // Gen1/2 (GBPKM.cs:75-104): el idioma se deduce del nombre de la especie, que es el mismo en ES y EN; no se puede distinguir ni explicar.
        if (dest.Generation > 2 && (userOwned ? best.Language != original.Language : best.Language != (int)LanguagePolicy.Preferred && LanguagePolicy.Supports(dest, LanguagePolicy.Preferred)))
        {
            var langText = LanguagePolicy.ReasonText(best, original, userOwned, keepEncounter, dest);
            op.Log.Reason("Language", LanguagePolicy.LanguageFixed, langText);
            op.Log.Note(langText);
        }

        // Decision (a): una naturaleza distinta de la del usuario NUNCA queda sin explicar (antes, si cambiaba en el escalon del mismo encuentro, solo salia en «Origen reconstruido»).
        if (best.Nature != original.Nature && !op.Log.HasReason("Nature"))
            RelaxationExplainer.ExplainNature(original, best, dest, natureFirst, op.Log);

        var regenLog = op.Log;
        regenLog.DefaultCode = ReasonCodes.EncounterRegenerated;
        return FinishLegalize(op, original, best, dest, bestOutcome, regenLog, partySize, optimizeStats, regenerated: true, originNotes: unfitNotes);
    }

    /// <summary>Lo creado por la propia plantilla FIJADA (el Core la convierte en el Pokemon) y validado por el Core es del origen pedido aunque el analisis oficial lo identifique con otro encuentro indistinguible (Gen1/2 sin metadatos de origen, evento con los mismos datos que un estatico...: primera coincidencia).</summary>
    private static bool IsOriginEquivalent(IGenerationStrategy strategy, IEncounterable? hint, Destination dest, string? pinnedKey, string originalKey)
        => pinnedKey is not null && strategy.Name == "CoreTemplate" && hint is not null
           && string.Equals(EncounterKeys.For(hint), originalKey, StringComparison.Ordinal);

    private static bool IsWurmpleLineOnPokewalkerGame(PKM wish, Destination dest)
        => wish.Species is >= (ushort)Species.Silcoon and <= (ushort)Species.Dustox && dest.Version is GameVersion.HG or GameVersion.SS or GameVersion.HGSS;

    private const int MaxEnumeratedHints = 16;

    /// <summary>Encuentros distintos (por clave del catalogo) que el Core enumera para la especie y la forma deseadas en el destino y que se pueden generar: aprobados en el catalogo (regla 8), convertibles y sin exigir un rastreador de HOME (regla 6).</summary>
    private List<IEncounterable> EnumerateHints(PKM wish, PKM original, Destination dest, IEncounterable? already, List<string> unfitNotes, out int blockedByHome)
    {
        blockedByHome = 0;
        // 0.6.6 (M1): si el Pokemon lleva (o llevara) el nombre del jugador, un origen de una generacion anterior donde ese nombre no cabe no se prueba nunca (saldria recortado).
        var nameIsPlayers = original.OriginalTrainerName.Length == 0 || string.Equals(original.OriginalTrainerName, dest.Trainer.OT, StringComparison.Ordinal);
        var result = new List<IEncounterable>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        if (already is not null && EncounterKeys.For(already) is { } k0) seen.Add(k0);
        var gate = new CatalogGate(catalog);
        var blank = dest.Sav.BlankPKM;
        blank.Species = wish.Species;
        blank.Form = wish.Form;
        blank.Gender = blank.GetSaneGender();
        lock (EngineGlobalState.Gate)
        {
            dest.ApplyParseSettings();
            foreach (var enc in CoreVersions.Encounters(blank, dest.Trainer, ReadOnlyMemory<ushort>.Empty))
            {
                if (enc is not IEncounterConvertible || enc.IsEgg != original.IsEgg) continue;
                if (nameIsPlayers && enc is IEncounterTemplate template && enc.Generation < dest.Generation && OriginPreference.Rank(template, dest) <= OriginRank.OtherGame && !TrainerNameFit.Fits(dest, enc.Generation))
                {
                    var note = TrainerNameFit.Explain(dest.Trainer.OT, enc.Generation, (LanguageID)dest.Trainer.Language);
                    if (!unfitNotes.Contains(note)) unfitNotes.Add(note);
                    continue;
                }
                if (!gate.IsGenerable(enc, out var key) || key is null || !seen.Add(key)) continue;
                if (HomeTrackerRule.IsRequired(enc, dest.Context)) { blockedByHome++; continue; }
                result.Add(enc);
                if (result.Count >= MaxEnumeratedHints) break;
            }
        }
        return result;
    }
    /// <summary>Tope de pistas de origen propio que se prueban por escalon (la busqueda general sigue disponible si ninguna vale).</summary>
    private const int MaxPreferredHints = 6;
    private static readonly TimeSpan PreferredHintBudget = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan PreferredTotalBudget = TimeSpan.FromSeconds(8);
    private const int MaxPreferredScan = 1200;

    /// <summary>
    /// 0.6.5 (ADR 0052): encuentros de ORIGEN PROPIO que el Core enumera para la especie y la forma deseadas en el destino (aprobados en el catalogo, convertibles, sin rastreador de HOME), por orden de
    /// preferencia: primero el mismo juego (o su gemelo) y despues otro juego o transferencia con el EO = jugador; dentro de cada rango se respeta el orden del Core. Se descartan los encuentros que
    /// fijan el EO (intercambios con personajes, regalos con entrenador) y las tarjetas de evento con entrenador propio: de esos solo se trata en la busqueda general, si no hay origen propio.
    /// </summary>
    private List<IEncounterable> PreferredHints(PKM wish, PKM original, Destination dest, List<string> unfitNotes)
    {
        var picked = new List<(IEncounterable Enc, int Rank, int Order)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var gate = new CatalogGate(catalog);
        var blank = dest.Sav.BlankPKM;
        blank.Species = wish.Species;
        blank.Form = wish.Form;
        blank.Gender = blank.GetSaneGender();
        var scanned = 0;
        lock (EngineGlobalState.Gate)
        {
            dest.ApplyParseSettings();
            foreach (var enc in CoreVersions.Encounters(blank, dest.Trainer, ReadOnlyMemory<ushort>.Empty))
            {
                if (++scanned > MaxPreferredScan) break;
                if (enc is not IEncounterConvertible) continue;
                // Un huevo del usuario sigue siendo huevo; un Pokemon eclosionado puede salir de una cria (el Core la ofrece como huevo y se eclosiona).
                if (original.IsEgg ? !enc.IsEgg : enc.IsEgg && !EncounterKeys.IsCoreEgg(enc)) continue;
                var rank = OriginPreference.Rank(enc, dest);
                if (rank > OriginRank.OtherGame) continue;
                // Un encuentro que no trae la forma pedida y no puede cambiarla no la dara nunca (mismo criterio que TransferRoutes.Build): no se gasta una pista en el.
                if (enc.Form != wish.Form && enc.Form < 30 && enc is not IEncounterFormRandom { IsRandomUnspecificForm: true } && !FormInfo.IsFormChangeable(wish.Species, enc.Form, wish.Form, enc.Context, dest.Context)) continue;
                // Un origen en una generacion anterior lleva el nombre del jugador con los limites de ESA generacion (Gen 1-2: 7 caracteres, 5 en japones, juego de caracteres reducido): si no cabe,
                // no se usa (nunca se inventa otro nombre) y se explica.
                if (enc.Generation < dest.Generation && !TrainerNameFit.Fits(dest, enc.Generation))
                {
                    var note = TrainerNameFit.Explain(dest.Trainer.OT, enc.Generation, (LanguageID)dest.Trainer.Language);
                    if (!unfitNotes.Contains(note)) unfitNotes.Add(note);
                    continue;
                }
                if (!gate.IsGenerable(enc, out var key) || key is null || !seen.Add(key)) continue;
                if (HomeTrackerRule.IsRequired(enc, dest.Context)) continue;
                picked.Add((enc, (int)rank, picked.Count));
            }
        }
        return [.. picked.OrderBy(p => p.Rank).ThenBy(p => p.Order).Take(MaxPreferredHints).Select(p => p.Enc)];
    }

    private LegalizeResponse NoSolution(Operation op, PKM original, Destination dest, int tried)
    {
        // Dominio finito demostrable: el Core no tiene NINGUN encuentro que produzca esta especie para este destino.
        var count = (_settings.EncounterCounter ?? CountEncounters)(original, dest, 1);
        if (count == 0)
            return op.Fail(ResponseStatus.NoSolution, "No hay ningún origen legal para esta especie en el destino.",
                $"PKHeX no enumera ningún encuentro de la especie {original.Species} (forma {original.Form}) alcanzable en {dest.Version}.", complete: true);
        return op.Fail(ResponseStatus.NoSolution, "No se ha encontrado una forma de hacer legal este Pokémon.",
            $"Se probaron {tried} candidatos en todos los pasos de relajación y ninguno pasó la validación oficial. El generador no puede demostrar que sea imposible (muestreo, no recorrido completo).",
            complete: false);
    }

    private static int CountEncounters(PKM original, Destination dest, int limit)
    {
        var blank = dest.Sav.BlankPKM;
        blank.Species = original.Species;
        blank.Form = original.Form;
        blank.Gender = blank.GetSaneGender();
        var n = 0;
        lock (EngineGlobalState.Gate)
        {
            dest.ApplyParseSettings();
            foreach (var _ in CoreVersions.Encounters(blank, dest.Trainer, ReadOnlyMemory<ushort>.Empty))
            {
                if (++n >= limit) break;
            }
        }
        return n;
    }

    private LegalizeResponse FinishLegalize(Operation op, PKM original, PKM candidate, Destination dest, ValidationOutcome outcome, ChangeLog log, bool partySize,
        bool optimizeStats, bool regenerated, IReadOnlyList<string>? originNotes = null)
    {
        op.Phase = Phase.E_ValidacionIndependiente;
        op.Tick();
        if (op.CaptureOnly) optimizeStats = false; // Adjust: Optimizar es una operacion aparte (con su propio presupuesto)

        // Hito 4b: nivel maximo legal DESPUES de tener un candidato valido y ANTES de optimizar stats (el Entrenamiento extremo depende del nivel).
        int? finalLevel = null;
        if (!op.CaptureOnly && op.Request.Action == LegalizeAction.Legalize && op.Request.Options?.MaxLevel == true)
        {
            var maxed = MaxLevelStep.Apply(candidate, original, dest, _validator, outcome, requireCatalogGate: regenerated, op.Deadline, op.Ct, mayRefreshMoves: IsGenerating(op.Request));
            if (maxed.Status != MaxLevelStatus.NotApplicable)
            {
                finalLevel = maxed.FinalLevel;
                log.Note(maxed.Message);
                if (maxed.Status == MaxLevelStatus.Changed)
                {
                    candidate = maxed.Candidate;
                    outcome = maxed.Outcome;
                    log.Reason("Level", ReasonCodes.LevelMaxed, maxed.Message);
                }
            }
        }

        PKM final = candidate;
        var finalOutcome = outcome;
        OptimizationReport? report = null;

        _settings.Trace?.Invoke($"[t] fase E: optimizar={optimizeStats} {op.Clock.ElapsedMilliseconds} ms");
        if (optimizeStats)
        {
            var optimizeDeadline = Min(op.Deadline, DateTime.UtcNow + OptimizeBudget(op.Request));
            var result = _optimizer.Optimize(new OptimizeRequest(candidate, original, dest, _validator, optimizeDeadline, op.Ct, log, PreserveOrigin: false, RequireCatalogGate: regenerated, Progress: _settings.Progress));
            report = result.Report;
            if (result.Improved)
            {
                // Revalidacion INDEPENDIENTE del candidato optimizado: si no es valido, se queda el mejor valido anterior.
                var reval = _validator.Validate(result.Candidate, original, dest, requireCatalogGate: regenerated);
                if (reval.Valid) { final = result.Candidate; finalOutcome = reval; }
                else report = report with { LimitReason = "La optimización dejaba el candidato inválido: se conserva el mejor candidato válido. " + report.LimitReason, IvsAfter = report.IvsBefore, SumAfter = report.SumBefore };
            }
        }

        // Hito 4c (R-010): historia coherente DESPUES de optimizar (el optimizador puede regenerar el Pokemon desde el encuentro) y ANTES de la ultima barrera. Solo para lo que el motor construye.
        if ((op.Request.Options?.CoherentHistory ?? true) && finalOutcome.Valid && (regenerated || IsGenerating(op.Request)))
        {
            var story = HistoryPhase.Apply(final, finalOutcome, new HistoryContext(original, dest, _validator, regenerated, catalog, HistoryPhase.Today(), log));
            final = story.Candidate;
            finalOutcome = story.Outcome;
        }

        // Hito 5: partida REAL con un Pokémon de otro entrenador: el Core espera CurrentHandler = 1 y al jugador como entrenador actual (el destino virtual no lo comprueba). Se revalida; si no valida, se deja como estaba.
        if (finalOutcome.Valid && RealSaveHandler.Apply(final, dest) is var handled && !ReferenceEquals(handled, final))
        {
            var rehandled = _validator.Validate(handled, original, dest, requireCatalogGate: regenerated);
            if (rehandled.Valid)
            {
                final = handled; finalOutcome = rehandled; log.Note("Se ha fijado al jugador como entrenador actual: el Pokémon es de otro entrenador y el guardado lo exige.");
                // ADR 0042: PKH.UpdateHandler deja vacíos los recuerdos del nuevo entrenador anterior (el juego los asigna después): se rellena lo que Pokekeep acaba de vaciar.
                if (op.Request.Options?.CoherentHistory ?? true) MemoryHistory.ApplyHt(ref final, ref finalOutcome, new HistoryContext(original, dest, _validator, regenerated, catalog, HistoryPhase.Today(), log));
            }
        }

        // Ultima barrera: bytes -> entidad nueva -> misma firma y valida; especie igual a la original.
        if (!finalOutcome.Valid)
            return op.Fail(ResponseStatus.ValidationFailed, "El candidato no superó la revalidación independiente: se descarta.", string.Join(", ", finalOutcome.Failures));

        // 0.6.5 (ADR 0052): «Ver cambios» dice quien figura como entrenador original y por que (solo para lo que el motor construye; una reparacion minima no toca el origen).
        if ((regenerated || IsGenerating(op.Request)) && finalOutcome.Origin is { } originAssessment)
            OriginExplainer.Explain(original, final, originAssessment, dest, catalog, log, originNotes);

        var msg = regenerated ? "Pokémon legalizado: se ha reconstruido un origen válido." : "Pokémon legalizado con una reparación mínima.";
        // Hito 3b: si la forma pedida no es la final, el motor dice POR QUE (prueba con el Core) y marca la busqueda como completa; si no la puede demostrar, no la marca.
        var formProven = false;
        if (final.Form != original.Form)
        {
            string? proof = op.FormProof;
            if (proof is null)
            {
                var ev = EvaluateRoutes(op, original, dest);
                if (ev.Verdict == TransferRoutes.RouteVerdict.ProvenUnreachable) proof = ev.Reason;
            }
            if (proof is not null)
            {
                formProven = true;
                log.Note($"La forma {original.Form} no se puede obtener legalmente en {dest.Version}: {proof}");
            }
        }
        if (op.CaptureOnly)
        {
            op.Captured = new CapturedLegalization(final, finalOutcome, log, regenerated);
            return op.Fail(ResponseStatus.Success, msg, null); // marcador interno: RunAdjust lee Captured y arma su propia respuesta
        }
        return Success(op, ResponseStatus.Success, original, final, dest, finalOutcome, partySize, msg, report, log, complete: formProven, finalLevel: finalLevel);
    }

    // ============================================================ Accion B: Optimizar stats (R-002)
    private LegalizeResponse RunOptimize(Operation op, PKM original, PKM working, Destination dest, ValidationOutcome baseline, bool partySize)
    {
        op.Phase = Phase.A_CapturaYAnalisis;
        if (!ReferenceEquals(working, original))
            return op.Fail(ResponseStatus.UnsupportedData, "La entidad no es del formato del destino: conviértela primero.", $"{original.GetType().Name} no es {dest.EntityType.Name}; Optimizar stats no transfiere entre formatos.", complete: true);
        if (!baseline.Valid)
            return op.Fail(ResponseStatus.NotLegalFirst, "Primero hay que legalizarlo: usa «Legalizar automáticamente».",
                $"El Pokémon no es legal tal cual está ({string.Join(", ", baseline.Failures)}); «Optimizar stats» no legaliza.", complete: true);

        op.Phase = Phase.E_ValidacionIndependiente;
        var result = _optimizer.Optimize(new OptimizeRequest(original, original, dest, _validator, op.Deadline, op.Ct, op.Log, PreserveOrigin: true, RequireCatalogGate: false, Progress: _settings.Progress));
        var report = result.Report;
        op.Attempts += result.Attempts;
        if (result.Status == OptimizeStatus.OriginGuard)
            return op.Fail(ResponseStatus.OriginPreserved, "La única mejora posible exigía cambiar el origen del Pokémon: no se ha aplicado.", report.LimitReason, complete: report.Exhaustive);
        if (result.Status == OptimizeStatus.Unsupported)
            return op.Fail(ResponseStatus.UnsupportedData, "Este tipo de encuentro aún no se puede optimizar.", report.LimitReason);
        if (result.Status == OptimizeStatus.NoImprovementFound)
        {
            var resp = op.Fail(ResponseStatus.NoSolution, "No se ha encontrado una mejora de stats (búsqueda no exhaustiva).", report.LimitReason, complete: false);
            return resp with { Optimization = report };
        }
        if (!result.Improved)
            return op.FailOptimal(report, baseline.TrackerMissing && dest.TrackerSeverity == Severity.Fishy);

        var reval = _validator.Validate(result.Candidate, original, dest, requireCatalogGate: false);
        if (!reval.Valid)
            return op.Fail(ResponseStatus.ValidationFailed, "La optimización dejaba el Pokémon inválido: se descarta.", string.Join(", ", reval.Failures));
        return Success(op, ResponseStatus.Success, original, result.Candidate, dest, reval, partySize,
            "Stats optimizados conservando todo el origen.", report, op.Log);
    }

    // ============================================================ Construccion de la respuesta
    private static LegalizeResponse Success(Operation op, ResponseStatus status, PKM original, PKM candidate, Destination dest, ValidationOutcome outcome, bool partySize,
        string message, OptimizationReport? optimization, ChangeLog? log = null, byte[]? exactBytes = null, bool complete = false, int? finalLevel = null)
    {
        // R-009: con el ajuste activado y el rastreador ausente, el resultado NO se llama «legal»: necesita pasar por HOME.
        var needsHome = outcome.TrackerMissing && dest.TrackerSeverity == Severity.Fishy && status is ResponseStatus.Success or ResponseStatus.AlreadyLegal;
        if (needsHome) { status = ResponseStatus.SuccessNeedsHome; message = NeedsHomeWarning; }
        var bytes = exactBytes ?? EntityIo.Export(candidate, partySize);
        var changes = ReferenceEquals(original, candidate) ? [] : ChangeBuilder.Build(original, candidate, log ?? op.Log);
        var ivBefore = PkmFieldsAccess.IVs(original);
        var ivAfter = PkmFieldsAccess.IVs(candidate);
        var summary = status == ResponseStatus.AlreadyLegal ? [] : ChangeBuilder.Summarize(changes, ivBefore, ivAfter, ChangeBuilder.StatWord(candidate));
        var notes = (log ?? op.Log).Notes;
        var reason = notes.Count == 0 ? null : string.Join(" ", notes);
        if (needsHome)
        {
            reason = (reason is null ? "" : reason + " ") + "El único fallo que PKHeX marcaría es la matrícula (rastreador) de HOME ausente (severidad por defecto «inválido»; aquí «sospechoso» por tu ajuste explícito). Referencia técnica: PKHeX.Core TransferVerifier.cs:174-176 (TransferTrackerMissing), «transfiere un pk de rastreador 0 a HOME para que se le asigne un rastreador válido a través del juego del que procede; no te inventes uno».";
            summary = [NeedsHomeWarning, .. summary];
        }
        return new LegalizeResponse
        {
            ProtocolVersion = Protocol.Version,
            RequestId = op.Request.RequestId,
            ReleaseId = PinnedVersions.ReleaseId,
            DatasetSnapshotId = op.Request.DatasetSnapshotId,
            Action = op.Request.Action,
            Status = status,
            Message = message,
            Reason = reason,
            CandidateEntityBase64 = Convert.ToBase64String(bytes),
            CandidateSha256 = Convert.ToHexString(SHA256.HashData(bytes)),
            EntityContext = candidate.Context.ToString(),
            EntityRuntimeType = candidate.GetType().Name,
            Changes = changes,
            Summary = summary,
            Proof = op.Request.Options?.IncludeReport == false ? outcome.Proof with { Report = [] } : outcome.Proof,
            Optimization = optimization,
            ElapsedMilliseconds = op.Clock.ElapsedMilliseconds,
            Attempts = op.Attempts,
            PhaseReached = op.Phase,
            SearchComplete = complete,
            FinalLevel = finalLevel,
        };
    }

    /// <summary>Recorre TODAS las rutas del Core para la especie y la forma pedidas (<see cref="TransferRoutes.Evaluate"/>) con un presupuesto propio de 30 s, sin mas rasgos que la forma.</summary>
    private TransferRoutes.RouteEvaluation EvaluateRoutes(Operation op, PKM wish, Destination dest)
    {
        var until = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        var traits = Traits.From(wish) with { Ability = -1, Nature = Nature.Random, Gender = null, Shiny = false, Moves = [0, 0, 0, 0] };
        return TransferRoutes.Evaluate(wish, dest, _validator, traits, () => DateTime.UtcNow >= until || op.Ct.IsCancellationRequested);
    }

    /// <summary>Presupuesto de la optimizacion de IV dentro de Legalizar: el de la solicitud (operaciones masivas) o el del motor.</summary>
    private TimeSpan OptimizeBudget(LegalizeRequest r) => r.Options?.OptimizeBudgetSeconds is > 0 and var s ? TimeSpan.FromSeconds(Math.Min(s, 600)) : _settings.LegalizeOptimizeBudget;

    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;
}
