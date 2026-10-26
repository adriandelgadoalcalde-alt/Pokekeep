// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Registro;
using System.ComponentModel;
using System.Diagnostics;
using AutoLegalidad.Catalog.Library;
using AutoLegalidad.Contracts;
using PKHeX.Core;

namespace AutoLegalidad.Ui.Operation;

/// <summary>Si una acción se puede lanzar ahora y, si no, por qué (el motivo se muestra como ayuda del elemento desactivado).</summary>
public readonly record struct Availability(bool Enabled, string? Reason)
{
    public static Availability Yes => new(true, null);
    public static Availability No(string reason) => new(false, reason);
}

/// <summary>Progreso mostrado en la barra de operación.</summary>
public sealed record OperationProgress(PokekeepAction Action, string Text, double? Fraction, string Target);

/// <summary>
/// Coordinador de UI de Pokekeep (Hito 4; plan 5 punto 6). Una sola operación a la vez; el destino (casilla, partida, editor) se FIJA al empezar; el motor
/// (Worker) corre fuera del hilo de la interfaz con progreso y Cancelar; si el destino cambia mientras tanto el resultado se DESCARTA (<c>TargetChanged</c>);
/// el candidato se aplica con el historial de PKHeX (<c>Slots.Set</c> o <c>PopulateFields</c>), se relee, se revalida y, si no es válido, se deshace
/// (<c>ValidationFailed</c>). Nunca se sobrescribe una casilla de la Librería ni se aplica nada que no haya superado la validación oficial.
/// </summary>
public sealed class PokekeepCoordinator : IDisposable
{
    private readonly IPokekeepHost _host;
    private readonly IEngineGateway _engine;
    private readonly Func<PokekeepSettings> _settings;
    private readonly Func<string>? _dataLanguage;
    private string DataLanguage => _dataLanguage?.Invoke() ?? "es";
    private readonly TimeSpan _pollInterval;
    private readonly SynchronizationContext? _ui;

    private WorkerManifest? _manifest;
    private OperationState? _current;

    public PokekeepCoordinator(IPokekeepHost host, IEngineGateway engine, Func<PokekeepSettings> settings, TimeSpan? pollInterval = null, Func<string>? dataLanguage = null)
    {
        _host = host;
        _engine = engine;
        _settings = settings;
        _dataLanguage = dataLanguage;
        _pollInterval = pollInterval ?? TimeSpan.FromMilliseconds(200);
        _ui = SynchronizationContext.Current;
    }

    /// <summary>Hay una operación en curso (solo se admite una a la vez).</summary>
    public bool IsBusy => _current is not null || ExternalBusy?.Invoke() == true;
    /// <summary>Hito 4b: otra operación de Pokekeep (la masiva) en curso; las acciones individuales esperan a que termine.</summary>
    public Func<bool>? ExternalBusy { get; set; }
    public OperationResult? LastResult { get; private set; }
    /// <summary>SOLO PRUEBAS: sustituye el límite de tiempo de los ajustes (que no baja de 10 s) para probar el Timeout sin esperar.</summary>
    internal TimeSpan? TimeoutOverride { get; set; }
    public IPokekeepHost Host => _host;
    public IEngineGateway Engine => _engine;
    public PokekeepSettings Settings => _settings();

    public event EventHandler<OperationProgress>? ProgressChanged;
    public event EventHandler<OperationResult>? Completed;

    // ============================================================================================ Disponibilidad
    public Availability GetSlotAvailability(ISlotInfo slot)
    {
        var sav = _host.Env.SAV;
        if (!RequestBuilder.IsKnownTarget(sav)) return Availability.No(Textos.SinPartida);
        if (!RequestBuilder.HasTrainerName(sav)) return Availability.No(Textos.SinNombreDeEntrenador);
        if (IsBusy) return Availability.No(Textos.OperacionEnCurso);
        if (!slot.CanWriteTo(sav)) return Availability.No(Textos.CasillaBloqueada);
        if (slot.IsEmpty(sav)) return Availability.No(Textos.CasillaVacia);
        return _engine.UnavailableReason is { } why ? Availability.No(why) : Availability.Yes;
    }

    public Availability GetEditorAvailability()
    {
        var env = _host.Env;
        if (!RequestBuilder.IsKnownTarget(env.SAV)) return Availability.No(Textos.SinPartida);
        if (!RequestBuilder.HasTrainerName(env.SAV)) return Availability.No(Textos.SinNombreDeEntrenador);
        if (IsBusy) return Availability.No(Textos.OperacionEnCurso);
        if (env.Editor.Data.Species == 0) return Availability.No(Textos.SinEspecie);
        return _engine.UnavailableReason is { } why ? Availability.No(why) : Availability.Yes;
    }

    /// <summary>Disponibilidad de «Añadir a la caja» para una fila de la Librería (la entrada debe estar disponible y ser añadible en el destino).</summary>
    public Availability GetLibraryAvailability(LibraryRow? row)
    {
        var sav = _host.Env.SAV;
        if (row is null) return Availability.No(AppIdioma.T("Elige una entrada de la Librería.", "Choose a Library entry."));
        if (!RequestBuilder.IsKnownTarget(sav) || !sav.HasBox) return Availability.No(sav.HasBox ? Textos.SinPartida : AppIdioma.T("Este guardado no tiene cajas.", "This save has no boxes."));
        if (!RequestBuilder.HasTrainerName(sav)) return Availability.No(Textos.SinNombreDeEntrenador);
        if (IsBusy) return Availability.No(Textos.OperacionEnCurso);
        if (!row.Available) return Availability.No(row.DimReason ?? AppIdioma.T("No existe en la partida abierta.", "It doesn’t exist in the open save."));
        // R-009: lo que solo existe pasando por HOME se ofrece únicamente con el ajuste explícito (desactivado por defecto).
        if (row.HomeRequired && !Settings.AllowHomeTrackerless) return Availability.No(Textos.NecesitaHomeSinAjuste);
        // Las entradas de cría (huevo) no llevan clave de encuentro: el origen lo ofrece el Core (EncounterEgg*) y se comprueba al terminar.
        if (!row.Addable || (string.IsNullOrEmpty(row.EncounterKey) && row.Kind != "Egg")) return Availability.No(row.DimReason ?? AppIdioma.T("Esta entrada todavía no se puede generar: su origen está pendiente de verificar.", "This entry can’t be generated yet: its origin is pending verification."));
        return _engine.UnavailableReason is { } why ? Availability.No(why) : Availability.Yes;
    }

    // ============================================================================================ Puntos de entrada
    /// <summary>Acción sobre el Pokémon del editor.</summary>
    public Task<OperationResult> RunOnEditorAsync(PokekeepAction action)
    {
        var av = GetEditorAvailability();
        if (!av.Enabled) return Task.FromResult(Refuse(action, OperationOrigin.Editor, av.Reason!));
        var env = _host.Env;
        if (!_host.EditorIsCompleteIgnoringStats)
            return Task.FromResult(Refuse(action, OperationOrigin.Editor, Textos.EditorIncompleto));
        var pk = env.Editor.PreparePKM();
        if (pk.Species == 0)
            return Task.FromResult(Refuse(action, OperationOrigin.Editor, Textos.SinEspecie));
        var partySize = pk.SIZE_PARTY != pk.SIZE_STORED;
        var cap = new EditorCapture
        {
            Origin = OperationOrigin.Editor,
            Env = env,
            Original = pk,
            OriginalSha256 = EntityBytes.Sha256(EntityBytes.Export(pk, partySize)),
        };
        return ExecuteAsync(action, cap, originKey: null);
    }

    /// <summary>Acción sobre una casilla. La casilla se FIJA aquí (la que había al abrir el menú), no la del último clic ni la del editor.</summary>
    public Task<OperationResult> RunOnSlotAsync(PokekeepAction action, ISlotInfo slot)
    {
        var av = GetSlotAvailability(slot);
        if (!av.Enabled) return Task.FromResult(Refuse(action, OperationOrigin.Slot, av.Reason!));
        var env = _host.Env;
        var pk = slot.Read(env.SAV);
        var cap = new SlotCapture
        {
            Origin = OperationOrigin.Slot,
            Env = env,
            Slot = slot,
            Original = pk,
            OriginalSha256 = EntityBytes.Sha256(EntityBytes.Export(pk, EntityBytes.UsesPartySize(pk, slot.Type))),
        };
        return ExecuteAsync(action, cap, originKey: null);
    }

    /// <summary>«Añadir a la caja»: genera la entrada de la Librería con el MISMO motor y la misma validación y la coloca en una casilla LIBRE (nunca sobrescribe).</summary>
    public Task<OperationResult> AddLibraryEntryAsync(LibraryRow row, ISlotInfo? slot = null)
    {
        var av = GetLibraryAvailability(row);
        if (!av.Enabled) return Task.FromResult(Refuse(PokekeepAction.Legalize, OperationOrigin.Library, av.Reason!));
        var env = _host.Env;
        var sav = env.SAV;
        if (slot is null)
        {
            slot = FindFreeSlot(sav, _host.CurrentBox);
            if (slot is null)
                return Task.FromResult(Refuse(PokekeepAction.Legalize, OperationOrigin.Library, Textos.NoHayHueco));
        }
        else if (!slot.CanWriteTo(sav) || (slot is SlotInfoBox protectedBox && sav.IsBoxSlotOverwriteProtected(protectedBox.Box, protectedBox.Slot)))
            return Task.FromResult(Refuse(PokekeepAction.Legalize, OperationOrigin.Library, Textos.CasillaBloqueada));
        else if (!slot.IsEmpty(sav))
            return Task.FromResult(Refuse(PokekeepAction.Legalize, OperationOrigin.Library, Textos.CasillaOcupada));

        var blank = slot.Read(sav);
        var cap = new LibrarySlotCapture
        {
            Origin = OperationOrigin.Library,
            Env = env,
            Slot = slot,
            Original = blank,
            OriginalSha256 = string.Empty,
            Template = RequestBuilder.BlankFor(sav, (ushort)row.SpeciesId, (byte)row.FormId),
            Row = row,
        };
        return ExecuteAsync(PokekeepAction.Legalize, cap, originKey: !string.IsNullOrEmpty(row.EncounterKey) ? row.EncounterKey : row.Kind == "Egg" ? Protocol.LibraryEggKey : null);
    }

    /// <summary>Primera casilla libre de la caja indicada (vacía y no bloqueada), o null.</summary>
    public static ISlotInfo? FindFreeSlot(SaveFile sav, int box)
    {
        if (!sav.HasBox || box < 0 || box >= sav.BoxCount) return null;
        for (var i = 0; i < sav.BoxSlotCount; i++)
        {
            ISlotInfo info = new SlotInfoBox(box, i, sav);
            // 0.6.6 (B9): igual que «Completar el juego», nunca una casilla protegida contra sobrescritura (equipo de combate, etc.).
            if (info.CanWriteTo(sav) && !sav.IsBoxSlotOverwriteProtected(box, i) && info.IsEmpty(sav))
                return info;
        }
        return null;
    }

    /// <summary>Cancela la operación en curso (mata el árbol del Worker; el resultado se descarta).</summary>
    public void Cancel()
    {
        var op = _current;
        if (op is null || op.UserCancelled) return;
        op.UserCancelled = true;
        Report(op, Textos.Cancelando, null);
        try { op.Cts.Cancel(); }
        catch (ObjectDisposedException) { }
    }

    /// <summary>Deshace lo último aplicado por Pokekeep si el destino sigue como lo dejó. Devuelve false (sin tocar nada) si ya no se puede.</summary>
    public bool UndoLast()
    {
        var token = LastResult?.Undo;
        if (token is null || IsBusy || !token.CanUndo()) return false;
        return token.Undo();
    }

    public void Dispose()
    {
        Cancel();
    }

    // ============================================================================================ Ejecución
    private sealed class OperationState(PokekeepAction action, TargetCapture capture)
    {
        public PokekeepAction Action { get; } = action;
        public TargetCapture Capture { get; } = capture;
        public CancellationTokenSource Cts { get; } = new();
        public bool UserCancelled { get; set; }
        public Stopwatch Clock { get; } = Stopwatch.StartNew();
        public string Target { get; set; } = string.Empty;
        public bool Optimizing { get; set; }
        /// <summary>Huella de la entidad enviada al motor (M4: un SuccessNeedsHome con esta misma huella no cambia nada).</summary>
        public string RequestSha256 { get; set; } = string.Empty;
    }

    private async Task<OperationResult> ExecuteAsync(PokekeepAction action, TargetCapture cap, string? originKey)
    {
        if (_current is not null)
            return Make(action, cap.Origin, OutcomeKind.Busy, ResponseStatus.InvalidInput, Textos.OperacionEnCurso, null, TimeSpan.Zero);

        var op = new OperationState(action, cap);
        _current = op;
        var species = SpeciesName(cap.RequestEntity.Species, DataLanguage);
        op.Target = $"{species} · {cap.Describe()}";
        void OnLine(string line) => Post(() => OnDiagnostic(op, line));
        _engine.DiagnosticLine += OnLine;
        OperationResult result;
        try
        {
            Report(op, Textos.Analizando, null);
            result = await RunAsync(op, originKey).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Diagnostico.RegistroOperaciones.Excepcion(action.ToString(), ex);
            result = Make(action, cap.Origin, OutcomeKind.EngineFailure, ResponseStatus.InternalError, AppIdioma.T("Error inesperado en la interfaz de Pokekeep", "Unexpected error in the Pokekeep interface"),
                AppIdioma.F("No se ha modificado nada. {0}: {1}", "Nothing was modified. {0}: {1}", ex.GetType().Name, ex.Message), op.Clock.Elapsed, cap);
        }
        finally
        {
            _engine.DiagnosticLine -= OnLine;
            _current = null;
            op.Cts.Dispose();
        }
        LastResult = result;
        Diagnostico.RegistroOperaciones.Registrar(result);
        Completed?.Invoke(this, result);
        return result;
    }

    private async Task<OperationResult> RunAsync(OperationState op, string? originKey)
    {
        var cap = op.Capture;
        var action = op.Action;

        // 1) Pareja de versiones (una vez; se repite si el Worker dice VersionMismatch).
        var manifestProblem = await EnsureManifestAsync(op).ConfigureAwait(true);
        if (manifestProblem is not null)
            return op.UserCancelled ? Cancelled(op) : manifestProblem;
        var manifest = _manifest!;

        // 2) Solicitud: entidad capturada + destino (solo datos mínimos del entrenador; nada de rutas ni del guardado).
        var settings = _settings();
        var limit = TimeoutOverride ?? settings.TimeoutFor(action.ToContract());
        var request = RequestBuilder.Create(manifest, cap.RequestEntity, cap.RequestPartySize, RequestBuilder.TargetFor(cap.Sav, cap.SlotType), action.ToContract(),
            DateTimeOffset.UtcNow + limit, cap.SelectionForRequest, optimizeStats: settings.OptimizeStatsOnLegalize, originKey, allowHomeTrackerless: settings.AllowHomeTrackerless, coherentHistory: settings.CoherentHistory);

        op.RequestSha256 = request.OriginalEntitySha256;

        // 3) Motor fuera del hilo de la interfaz; mientras tanto se vigila que el destino siga siendo el mismo.
        var engineTask = Task.Run(() => _engine.LegalizeAsync(request, op.Cts.Token));
        var unstable = Stability.Stable;
        while (true)
        {
            var tick = Task.Delay(_pollInterval);
            if (await Task.WhenAny(engineTask, tick).ConfigureAwait(true) == engineTask)
                break;
            if (op.UserCancelled)
                continue; // ya se pidió matar el Worker: se espera su respuesta Cancelled
            var st = cap.Check(_host);
            if (st != Stability.Stable)
            {
                unstable = st;
                try { op.Cts.Cancel(); } catch (ObjectDisposedException) { }
            }
        }

        LegalizeResponse response;
        try
        {
            response = await engineTask.ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            response = FailureResponse(request, ResponseStatus.Cancelled, AppIdioma.T("Operacion cancelada.", "Operation canceled."));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            RegistroFallos.Error(AppIdioma.T("El motor falló (excepción en la interfaz al llamar al motor).", "The engine failed (exception in the interface when calling the engine)."), ex, "PokekeepCoordinator.RunAsync");
            response = FailureResponse(request, ResponseStatus.InternalError, AppIdioma.T("El motor falló.", "The engine failed."), $"{ex.GetType().Name}: {ex.Message}");
        }

        if (op.UserCancelled)
            return Cancelled(op);

        // 4) Autoridad final sobre el destino: la comparación con lo que HAY ahora decide TargetChanged, aunque el motor haya acertado.
        if (unstable == Stability.Stable)
            unstable = cap.Check(_host);
        if (unstable != Stability.Stable)
        {
            var t = ResultMessages.ForStability(unstable);
            return Make(action, cap.Origin, t.Kind, ResponseStatus.TargetChanged, t.Headline, t.Detail, op.Clock.Elapsed, cap, response: response);
        }

        if (response.Status == ResponseStatus.VersionMismatch)
            _manifest = null; // la base de datos o el Worker cambiaron: se vuelve a leer el manifiesto la próxima vez

        var species = SpeciesName(cap.RequestEntity.Species, DataLanguage);
        var game = GameName(cap.Sav);
        if (!ResponseValidator.CarriesCandidate(response.Status))
        {
            var text = ResultMessages.ForResponse(response, action, cap.Origin, species, game, limit, settings.AllowHomeTrackerless);
            return Make(action, cap.Origin, text.Kind, response.Status, text.Headline, text.Detail, op.Clock.Elapsed, cap, response: response, summary: ExtraSummary(response));
        }

        if (response.Status == ResponseStatus.AlreadyLegal)
        {
            var text = ResultMessages.ForResponse(response, action, cap.Origin, species, game, limit, settings.AllowHomeTrackerless);
            return Make(action, cap.Origin, OutcomeKind.AlreadyLegal, response.Status, text.Headline, text.Detail, op.Clock.Elapsed, cap, response: response);
        }

        // 0.6.6 (M4): «necesita pasar por HOME» SIN ningun cambio (el Pokemon ya era legal salvo la matricula: el motor devuelve sus mismos bytes). No se reescribe la casilla ni se anota
        // nada en el historial de PKHeX ni se ofrece un Deshacer que no deshace nada; solo se informa.
        if (response.Status == ResponseStatus.SuccessNeedsHome && cap is not LibrarySlotCapture
            && string.Equals(response.CandidateSha256, op.RequestSha256, StringComparison.OrdinalIgnoreCase))
            return Make(action, cap.Origin, OutcomeKind.UnchangedNeedsHome, ResponseStatus.SuccessNeedsHome, Textos.NecesitaHomeSinCambios, Textos.NecesitaHomeAviso, op.Clock.Elapsed, cap, response: response, summary: [Textos.NecesitaHomeAviso]);

        // 5) Aplicar (Success): releer, revalidar y, si hace falta, deshacer.
        Report(op, Textos.Comprobando, null);
        return Apply(op, response);
    }

    // ------------------------------------------------------------------------------------------ manifiesto
    /// <summary>
    /// Versiones del motor para la ficha de la Librería (Hito 5d): el manifiesto en memoria o, si todavía no se ha pedido, una consulta corta al Worker (no es una operación: no toca la partida ni compara pareja).
    /// Null si el motor no está disponible o no responde a tiempo.
    /// </summary>
    public async Task<WorkerManifest?> TryGetManifestAsync(TimeSpan? timeout = null)
    {
        if (_manifest is not null) return _manifest;
        if (_engine.UnavailableReason is not null) return null;
        using var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(15));
        try { return await Task.Run(() => _engine.GetManifestAsync(cts.Token)).ConfigureAwait(true); }
        catch (Exception ex) when (ex is OperationCanceledException or Win32Exception or InvalidOperationException or IOException or System.Text.Json.JsonException) { return null; }
    }

    private async Task<OperationResult?> EnsureManifestAsync(OperationState op)
    {
        if (_manifest is not null)
            return null;
        var cap = op.Capture;
        WorkerManifest? manifest = null;
        string? error = null;
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(op.Cts.Token))
        {
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            try
            {
                manifest = await Task.Run(() => _engine.GetManifestAsync(timeout.Token)).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                error = op.UserCancelled ? null : AppIdioma.T("El motor no respondió al comprobar su versión.", "The engine didn’t respond when checking its version.");
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException or System.Text.Json.JsonException)
            {
                error = ex.Message;
            }
        }
        if (op.UserCancelled)
            return Cancelled(op);
        if (manifest is null)
            return Make(op.Action, cap.Origin, OutcomeKind.EngineFailure, ResponseStatus.InternalError, AppIdioma.T("No se pudo iniciar el motor de Pokekeep", "Couldn’t start the Pokekeep engine"),
                AppIdioma.T("No se ha modificado nada. ", "Nothing was modified.") + (_engine.UnavailableReason ?? error ?? AppIdioma.T("El motor no devolvió su manifiesto de versiones.", "The engine didn’t return its version manifest.")), op.Clock.Elapsed, cap);
        var pair = PairCheck.Evaluate(manifest);
        if (!pair.Compatible)
            return Make(op.Action, cap.Origin, OutcomeKind.EngineFailure, ResponseStatus.VersionMismatch, AppIdioma.T("El motor no coincide con esta versión de Pokekeep", "The engine doesn’t match this version of Pokekeep"),
                AppIdioma.T("No se ha generado nada. ", "Nothing was generated.") + pair.Problem, op.Clock.Elapsed, cap);
        _manifest = manifest;
        return null;
    }

    // ------------------------------------------------------------------------------------------ aplicación
    private OperationResult Apply(OperationState op, LegalizeResponse response)
    {
        var cap = op.Capture;
        var action = op.Action;
        var sav = cap.Sav;
        var expectedSpecies = cap.RequestEntity.Species;

        OperationResult Fail(string headline, string? detail, OutcomeKind kind = OutcomeKind.ValidationFailed, ResponseStatus status = ResponseStatus.ValidationFailed)
            => Make(action, cap.Origin, kind, status, headline, detail, op.Clock.Elapsed, cap, response: response);

        byte[] bytes;
        try { bytes = Convert.FromBase64String(response.CandidateEntityBase64!); }
        catch (FormatException) { return Fail(AppIdioma.T("El resultado del motor no es legible", "The engine’s result is not readable"), Textos.ValidacionFallida); }
        if (!Enum.TryParse<EntityContext>(response.EntityContext, false, out var context))
            return Fail(AppIdioma.T("El resultado del motor no es legible", "The engine’s result is not readable"), Textos.ValidacionFallida);
        var candidate = EntityBytes.Parse(bytes, context);
        if (candidate is null)
            return Fail(AppIdioma.T("El resultado del motor no es legible", "The engine’s result is not readable"), Textos.ValidacionFallida);
        if (candidate.Species != expectedSpecies)
            return Fail(AppIdioma.T("El resultado cambiaba la especie", "The result changed the species"), AppIdioma.T("Pokekeep nunca cambia la especie: se ha descartado. ", "Pokekeep never changes the species: it was discarded.") + Textos.ValidacionFallida);
        // El tipo esperado es el de la partida o, en el editor, el del borrador (nunca otro formato).
        if (candidate.GetType() != (cap.Origin == OperationOrigin.Editor ? cap.Original.GetType() : sav.PKMType))
            return Fail(AppIdioma.T("El resultado no tiene el formato de esta partida", "The result doesn’t have this save’s format"), Textos.ValidacionFallida);

        // Entrada de cría sin clave de encuentro: el motor puede devolver un Pokémon legal de OTRO origen (p. ej. una incursión). Eso no es lo que se pidió: no se aplica.
        if (cap is LibrarySlotCapture { Row: { Kind: "Egg" } } && response.Proof?.EncounterType?.StartsWith("EncounterEgg", StringComparison.Ordinal) != true && response.Proof?.EncounterKey?.EndsWith(":egg", StringComparison.Ordinal) != true)
            return Fail(AppIdioma.T("No se ha podido generar con origen de cría", "Couldn’t be generated with a breeding origin"),
                AppIdioma.F("El motor solo encontró un origen distinto ({0}) para esta entrada de cría, y Pokekeep no lo sustituye en silencio. No se ha añadido nada.", "The engine only found a different origin ({0}) for this breeding entry, and Pokekeep doesn’t substitute it silently. Nothing was added.", response.Proof?.EncounterType ?? "desconocido"),
                OutcomeKind.NotApplied, ResponseStatus.NoSolution);

        // Comprobación previa en esta ventana (segunda opinión independiente del motor, con la tabla personal y el tipo de casilla del destino).
        var needsHome = response.Status == ResponseStatus.SuccessNeedsHome;
        var resolved = cap is EditorCapture ? (LegalityCheck.IsValid(candidate, sav, cap.SlotType, needsHome) ? candidate : null) : LegalityCheck.ResolveForSave(candidate, sav, cap.SlotType, needsHome);   // Hito 5: el candidato tal cual o, si no vale, adaptado al guardado (Pokémon de otro entrenador)
        var preValid = resolved is not null;
        if (resolved is not null) candidate = resolved;
        if (!preValid)
            return Fail(AppIdioma.T("El resultado no superó la comprobación final", "The result did not pass the final check"), AppIdioma.T("No se ha aplicado nada. ", "Nothing was applied.") + Textos.ValidacionFallida);

        List<string> summary;
        IReadOnlyList<ChangeEntry> shownChanges = response.Changes;
        if (cap is LibrarySlotCapture lib)
        {
            // Una entrada nueva no tiene «rasgos del usuario» que relajar ni «cambios» respecto a nada: se resume el origen y la optimización.
            shownChanges = [];
            summary = [Textos.OrigenDeLaLibreria(lib.Row, response.Proof?.EncounterType)];
            if (response.Optimization is { SumAfter: > 0 } o)
                summary.Add($"{(candidate.Format <= 2 ? "DV" : "IV")}: {string.Join('/', o.IvsAfter)}" + (o.LimitReason is { Length: > 0 } lim ? $". {TextosMotor.Traducir(lim)}" : string.Empty));
        }
        else
        {
            summary = [.. response.Summary.Select(s => TextosMotor.Traducir(s) ?? s)];
            summary.AddRange(ExtraSummary(response));
        }
        var species = SpeciesName(expectedSpecies, DataLanguage);
        var target = cap.Describe();

        if (needsHome) summary.Insert(0, Textos.NecesitaHomeAviso);
        // R-009: sin la matrícula de HOME NUNCA se presenta como «Legalizado»: el titular y el estado son los de «necesita pasar por HOME».
        OperationResult Ok(string headline, UndoToken? undo) => Make(action, cap.Origin, needsHome ? OutcomeKind.AppliedNeedsHome : OutcomeKind.Applied,
            needsHome ? ResponseStatus.SuccessNeedsHome : ResponseStatus.Success, needsHome ? (cap.Origin == OperationOrigin.Library ? $"{Textos.NecesitaHome} · {Textos.AnadidoALaCaja}: {species} ({target})" : Textos.NecesitaHome) : headline,
            null, op.Clock.Elapsed, cap, response: response, summary: summary, undo: undo, changes: shownChanges);

        if (cap is EditorCapture)
        {
            var editor = cap.Env.Editor;
            var original = cap.Original.Clone();
            editor.PopulateFields(candidate, false);
            var after = editor.PreparePKM(false);
            var postValid = LegalityCheck.IsValid(after, sav, StorageSlotType.None, needsHome);
            if (!postValid || after.Species != expectedSpecies)
            {
                editor.PopulateFields(original, false);
                return Fail(AppIdioma.T("El resultado no superó la comprobación final", "The result did not pass the final check"), Textos.ValidacionFallida);
            }
            var appliedHash = EntityBytes.Sha256(EntityBytes.Export(after, cap.RequestPartySize));
            var env = cap.Env;
            var undo = new UndoToken(
                () => ReferenceEquals(_host.Env, env) && EntityBytes.Sha256(EntityBytes.Export(env.Editor.PreparePKM(false), cap.RequestPartySize)) == appliedHash,
                () =>
                {
                    if (!ReferenceEquals(_host.Env, env) || EntityBytes.Sha256(EntityBytes.Export(env.Editor.PreparePKM(false), cap.RequestPartySize)) != appliedHash)
                        return false;
                    env.Editor.PopulateFields(original.Clone(), false);
                    return true;
                });
            return Ok(action == PokekeepAction.Optimize ? Textos.StatsOptimizados : Textos.Legalizado, undo);
        }

        var slotCap = (SlotCapture)cap;
        var slot = slotCap.Slot;
        var slots = cap.Env.Slots;
        if (!slot.CanWriteTo(sav))
            return Fail(AppIdioma.T("La casilla está bloqueada", "The slot is locked"), Textos.CasillaBloqueada, OutcomeKind.NotApplied, ResponseStatus.InvalidInput);
        var touch = slots.Set(slot, candidate);
        if (touch != SlotTouchResult.Success)
            return Fail(AppIdioma.T("No se pudo escribir en la casilla", "Couldn’t write to the slot"), AppIdioma.T("El guardado rechazó la escritura. No se ha modificado nada.", "The save rejected the write. Nothing was modified."), OutcomeKind.NotApplied, ResponseStatus.InvalidInput);

        // Releer lo que quedó escrito y revalidarlo (puede diferir del candidato por las mutaciones de importación del guardado).
        var reread = slot.Read(sav);
        var rereadValid = LegalityCheck.IsValid(reread, sav, slot.Type, needsHome);
        if (!rereadValid || reread.Species != expectedSpecies)
        {
            slots.Undo(); // sin ningún await entre el Set y este Undo: lo último del historial es nuestro cambio
            _host.NotifyUndoRedoChanged();
            return Fail(AppIdioma.T("El resultado no superó la comprobación final", "The result did not pass the final check"), Textos.ValidacionFallida);
        }
        // Hito 5b: tras la comprobación final, la Pokédex de la partida (solo al AÑADIR desde la Librería; legalizar u optimizar un Pokémon que ya estaba no la toca).
        if (cap.Origin == OperationOrigin.Library && Settings.RegisterInPokedex && PokedexRegistration.Register(slot, sav, reread))
            summary.Add(Textos.PokedexRegistrada);
        _host.NotifyUndoRedoChanged();

        var written = EntityBytes.Sha256(EntityBytes.Export(reread, EntityBytes.UsesPartySize(reread, slot.Type)));
        var originalClone = slotCap.Original.Clone();
        var env2 = cap.Env;
        bool StillOurs() => ReferenceEquals(_host.Env, env2)
            && slot.CanWriteTo(sav)
            && EntityBytes.Sha256(EntityBytes.Export(slot.Read(sav), EntityBytes.UsesPartySize(slot.Read(sav), slot.Type))) == written;
        var slotUndo = new UndoToken(StillOurs, () =>
        {
            if (!StillOurs()) return false;
            var r = env2.Slots.Set(slot, originalClone.Clone());
            _host.NotifyUndoRedoChanged();
            _host.ClearSlotHighlights();
            return r == SlotTouchResult.Success;
        });
        var headline = cap.Origin == OperationOrigin.Library ? $"{Textos.AnadidoALaCaja}: {species} ({target})"
            : action == PokekeepAction.Optimize ? Textos.StatsOptimizados : Textos.Legalizado;
        return Ok(headline, slotUndo);
    }

    private static IEnumerable<string> ExtraSummary(LegalizeResponse r)
    {
        if (r.Optimization?.LimitReason is { Length: > 0 } limit && r.Status is ResponseStatus.Success)
            yield return TextosMotor.Traducir(limit) ?? limit;
    }

    // ------------------------------------------------------------------------------------------ progreso
    private void OnDiagnostic(OperationState op, string line)
    {
        if (!ReferenceEquals(_current, op) || op.UserCancelled) return;
        switch (DiagnosticParser.Parse(line))
        {
            case EngineSignal.PhaseChanged p:
                if (op.Action == PokekeepAction.Optimize && p.Phase == Phase.E_ValidacionIndependiente)
                    Report(op, Textos.Optimizando, null);
                else if (!op.Optimizing)
                    Report(op, DiagnosticParser.PhaseText(p.Phase), null);
                break;
            case EngineSignal.OptimizeProgress g:
                op.Optimizing = true;
                Report(op, $"{Textos.Optimizando} · {Textos.Porcentaje(g.Fraction)}", Math.Clamp(g.Fraction, 0, 1));
                break;
        }
    }

    private void Report(OperationState op, string text, double? fraction)
        => ProgressChanged?.Invoke(this, new OperationProgress(op.Action, text, fraction, op.Target));

    private void Post(Action a)
    {
        if (_ui is null) a();
        else _ui.Post(_ => a(), null);
    }

    // ------------------------------------------------------------------------------------------ resultados
    private OperationResult Refuse(PokekeepAction action, OperationOrigin origin, string reason)
    {
        var busy = reason == Textos.OperacionEnCurso;
        var result = Make(action, origin, busy ? OutcomeKind.Busy : OutcomeKind.NotApplied, ResponseStatus.InvalidInput, AppIdioma.T("No se puede hacer ahora", "Can’t be done right now"), reason, TimeSpan.Zero);
        if (!busy)
        {
            LastResult = result;
            Diagnostico.RegistroOperaciones.Registrar(result);
            Completed?.Invoke(this, result);
        }
        return result;
    }

    private OperationResult Cancelled(OperationState op)
        => Make(op.Action, op.Capture.Origin, OutcomeKind.Cancelled, ResponseStatus.Cancelled, Textos.OperacionCancelada, null, op.Clock.Elapsed, op.Capture);

    private OperationResult Make(PokekeepAction action, OperationOrigin origin, OutcomeKind kind, ResponseStatus status, string headline, string? detail, TimeSpan elapsed,
        TargetCapture? cap = null, LegalizeResponse? response = null, IEnumerable<string>? summary = null, UndoToken? undo = null, IReadOnlyList<ChangeEntry>? changes = null) => new()
    {
        Action = action,
        Origin = origin,
        Kind = kind,
        Status = status,
        Headline = headline,
        Detail = detail,
        Summary = summary is null ? [] : [.. summary],
        Changes = changes ?? [],
        TargetDescription = cap is null ? null : $"{SpeciesName(cap.RequestEntity.Species, DataLanguage)} · {cap.Describe()}",
        Elapsed = elapsed,
        Response = response,
        Undo = undo,
    };

    private static LegalizeResponse FailureResponse(LegalizeRequest r, ResponseStatus status, string message, string? reason = null) => new()
    {
        ProtocolVersion = Protocol.Version,
        RequestId = r.RequestId,
        Action = r.Action,
        Status = status,
        Message = message,
        Reason = reason,
    };

    // ------------------------------------------------------------------------------------------ nombres
    public static string SpeciesName(ushort species, string idioma = "es")
    {
        try
        {
            var names = GameInfo.GetStrings(idioma).Species;
            return species < names.Count && names[species].Length > 0 ? names[species] : AppIdioma.F("Pokémon n.º {0}", "Pokémon No. {0}", species);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return AppIdioma.F("Pokémon n.º {0}", "Pokémon No. {0}", species);
        }
    }

    public static string GameName(SaveFile sav, string? idioma = null)
    {
        try { return idioma is null ? GameInfo.GetVersionName(sav.Version) : GameInfo.GetStrings(idioma).gamelist[(int)sav.Version]; }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return sav.Version.ToString(); }
    }
}

