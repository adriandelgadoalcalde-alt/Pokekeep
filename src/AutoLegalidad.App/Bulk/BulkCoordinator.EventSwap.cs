// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Catalog.Library;
using AutoLegalidad.Contracts;
using AutoLegalidad.Registro;
using AutoLegalidad.Ui.Library;
using AutoLegalidad.Ui.Operation;
using PKHeX.Core;

namespace AutoLegalidad.Ui.Bulk;

/// <summary>Qué pasó con un Pokémon legendario, sublegendario o singular al buscar su versión de evento (Hito 5d, ADR 0046).</summary>
public enum EventSwapStatus
{
    /// <summary>Hay una tarjeta de evento legal y generada por el motor: se propone el cambio (el usuario puede desmarcarlo).</summary>
    Proposed,
    /// <summary>Ya es de evento (tarjeta o regalo de Gen 3 según el Core): no se toca nunca.</summary>
    AlreadyEvent,
    /// <summary>El catálogo no tiene ninguna tarjeta de evento ofrecible para esa especie y forma en este juego.</summary>
    NoEvent,
    /// <summary>El Pokémon es ilegal y «Legalizar los ilegales primero» no está activado.</summary>
    NotLegalFirst,
    /// <summary>Hay tarjetas pero ninguna salió legal con el motor y el Core: el Pokémon se queda como estaba.</summary>
    NotGenerable,
    TimedOut,
    EngineError,
    Cancelled,
}

/// <summary>Una fila de la vista previa de «Preferir la versión de evento»: un Pokémon de la selección cuya especie entra en la función.</summary>
public sealed class EventSwapItem
{
    /// <summary>Posición en <see cref="BulkSelection.Included"/>.</summary>
    public required int Index { get; init; }
    public required ISlotInfo Slot { get; init; }
    public required PKM Original { get; init; }
    /// <summary>De dónde viene hoy («salvaje», «estático», «regalo»...), según el análisis oficial del Core.</summary>
    public required string CurrentOrigin { get; init; }
    public required EventSwapStatus Status { get; init; }
    public string? Reason { get; init; }
    /// <summary>La tarjeta elegida (la primera de la lista ordenada que el motor generó legal).</summary>
    public LivingDexOrigin? Event { get; init; }
    /// <summary>El Pokémon de evento ya generado y validado por el motor y por el Core (nunca un Pokémon sin validar).</summary>
    public PKM? Replacement { get; init; }
    public bool NeedsHome { get; init; }
    /// <summary>Tarjetas de evento que el catálogo ofrece / las que se probaron (cota <see cref="EventPreference.MaxAttemptsPerPokemon"/>).</summary>
    public int EventsOffered { get; init; }
    public int EventsTried { get; init; }
    /// <summary>Por qué esta tarjeta y no otra (criterio del ADR 0046).</summary>
    public string? WhyThisCard { get; init; }
    /// <summary>Marcado en la vista previa: solo lo marcado se aplica. Por defecto, todo lo propuesto.</summary>
    public bool Include { get; set; }

    public string DataLanguage { get; init; } = "es";
    public string PokemonName => PokekeepCoordinator.SpeciesName(Original.Species, DataLanguage) + (Original.Form != 0 ? $" ({BulkCoordinator.FormNameOf(Original, DataLanguage)})" : string.Empty);
    /// <summary>Año de la distribución que declara la fuente de la tarjeta (nunca la fecha de captura que el motor pone dentro de la ventana, que puede ser cualquiera): null si la fuente no lo trae.</summary>
    public int? Year => Event?.Event?.Year;
    /// <summary>«japonés (evento exclusivo de Japón)» si la tarjeta es exclusiva de un país con idioma libre; si no, el idioma sin más.</summary>
    public string LanguageDisplay => Event is { } o && EventPreference.RequestedLanguageOf(o) > 0 && LanguageName != "—"
        ? AppIdioma.F("{0} (evento exclusivo de {1})", "{0} (event exclusive to {1})", LanguageName.ToLowerInvariant(), EventInfo.RegionLabel(o.Event!.RegionCodes[0])) : LanguageName;
    public string LanguageName => Replacement is { Format: > 2 } r ? BulkCoordinator.LanguageText(r.Language) : "—";
    /// <summary>Nombre legible en castellano respaldado por la fuente (título en español, o «Evento de Japón (tarjeta WC6 n.º 156)»). El título original en otro idioma no se muestra aquí: sigue en la ficha de la Librería.</summary>
    public string EventTitle => Event?.Event?.DisplayTitle ?? string.Empty;

    /// <summary>«Rayquaza (salvaje) → evento «Título» · 2008 · Español».</summary>
    public string Describe()
    {
        if (Event is null) return $"{PokemonName} ({CurrentOrigin}): {StatusText}";
        var parts = new List<string> { EventTitle.StartsWith(AppIdioma.T("Evento", "Event"), StringComparison.Ordinal) ? EventTitle : AppIdioma.F("evento «{0}»", "event “{0}”", EventTitle) };
        if (Year is { } y) parts.Add(y.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (LanguageName != "—") parts.Add(LanguageDisplay);
        return $"{PokemonName} ({CurrentOrigin}) → {string.Join(" · ", parts)}";
    }

    /// <summary>Etiqueta de la especie en la función: «legendario», «sublegendario» o «singular» (<see cref="EventPreference.CategoryName"/>).</summary>
    public string Category => EventPreference.CategoryName(Original.Species);

    /// <summary>Columna «Estado» de la vista previa: lo propuesto dice por qué esa tarjeta (o que necesita HOME); lo demás, su estado.</summary>
    public string PreviewStatus => Status == EventSwapStatus.Proposed ? (NeedsHome ? StatusText : WhyThisCard is { Length: > 0 } w ? AppIdioma.F("Elegida por: {0}.", "Chosen for: {0}.", w) : StatusText) : StatusText;

    /// <summary>El detalle de la fila en la vista previa (lo que el escritorio enseña debajo de la lista y el móvil al desplegar la fila).</summary>
    public IReadOnlyList<string> DetailLines()
    {
        var lines = new List<string> { Describe() };
        if (Status == EventSwapStatus.Proposed)
        {
            if (WhyThisCard is { Length: > 0 } why) lines.Add(AppIdioma.F("Por qué esta tarjeta: {0}. El catálogo ofrece {1} {2} para él en este juego; se probaron {3}.", "Why this card: {0}. The catalog offers {1} {2} for it in this game; {3} were tried.", why, EventsOffered, (EventsOffered == 1 ? AppIdioma.T("tarjeta", "card") : AppIdioma.T("tarjetas", "cards")), EventsTried));
            lines.Add($"Idioma: {LanguageDisplay}.");
        }
        lines.Add(StatusText);
        return lines;
    }

    public string StatusText => Status switch
    {
        EventSwapStatus.Proposed => NeedsHome ? AppIdioma.T("Necesita pasar por HOME (sin su matrícula PKHeX lo da por inválido hasta entonces).", "Needs to pass through HOME (without its tracker PKHeX considers it invalid until then).") : AppIdioma.T("Se propone cambiarlo por su versión de evento.", "It is proposed to switch it to its event version."),
        EventSwapStatus.AlreadyEvent => AppIdioma.T("Ya es de evento: no se toca.", "It is already from an event: left alone."),
        EventSwapStatus.NoEvent => Reason ?? AppIdioma.T("No hay ninguna tarjeta de evento legal para él en este juego.", "There is no legal event card for it in this game."),
        EventSwapStatus.NotLegalFirst => AppIdioma.T("Es ilegal: primero hay que legalizarlo (activa «Legalizar los ilegales primero»).", "It is illegal: it has to be legalized first (turn on “Legalize the illegal ones first”)."),
        EventSwapStatus.TimedOut => AppIdioma.T("Se agotó el tiempo buscando su evento: esto no prueba que no exista. Se queda como estaba.", "Time ran out searching for its event: this doesn’t prove it doesn’t exist. It stays as it was."),
        EventSwapStatus.EngineError => Reason ?? AppIdioma.T("Error del motor: se queda como estaba.", "Engine error: it stays as it was."),
        EventSwapStatus.Cancelled => AppIdioma.T("No llegó a procesarse.", "It was never processed."),
        _ => Reason ?? AppIdioma.T("Ninguna tarjeta de evento sale legal con PKHeX en este juego: se queda como estaba.", "No event card comes out legal with PKHeX in this game: it stays as it was."),
    };
}

/// <summary>Resultado de buscar la versión de evento de los legendarios, sublegendarios y singulares de una selección. NO ha tocado la partida.</summary>
public sealed class EventSwapPlan
{
    public required IReadOnlyList<EventSwapItem> Items { get; init; }
    /// <summary>El usuario canceló la búsqueda.</summary>
    public bool Cancelled { get; init; }
    public string? Problem { get; init; }

    public IReadOnlyList<EventSwapItem> Proposed => [.. Items.Where(i => i.Status == EventSwapStatus.Proposed)];
    /// <summary>Lo que se aplicará: lo propuesto y marcado.</summary>
    public IReadOnlyList<EventSwapItem> Accepted => [.. Items.Where(i => i.Status == EventSwapStatus.Proposed && i.Include)];

    public string Summary()
    {
        if (Items.Count == 0) return AppIdioma.T("Ningún Pokémon de la selección es legendario, sublegendario ni singular.", "No Pokémon in the selection is legendary, sub-legendary or unique.");
        var p = Proposed.Count;
        var parts = new List<string> { AppIdioma.F("{0} {1} en la función", "{0} {1} the feature", Items.Count, Items.Count == 1 ? AppIdioma.T("Pokémon entra", "Pokémon enters") : AppIdioma.T("Pokémon entran", "Pokémon enter")), AppIdioma.F("{0} con evento legal para cambiar", "{0} with a legal event to switch to", p) };
        void Extra(EventSwapStatus s, string text) { var n = Items.Count(i => i.Status == s); if (n > 0) parts.Add($"{n} {text}"); }
        Extra(EventSwapStatus.AlreadyEvent, AppIdioma.T("ya son de evento", "are already from an event"));
        Extra(EventSwapStatus.NoEvent, AppIdioma.T("sin evento en este juego", "with no event in this game"));
        Extra(EventSwapStatus.NotGenerable, AppIdioma.T("con evento que no sale legal", "with an event that doesn’t come out legal"));
        Extra(EventSwapStatus.NotLegalFirst, AppIdioma.T("ilegales", "illegal ones"));
        Extra(EventSwapStatus.TimedOut, AppIdioma.T("con tiempo agotado", "timed out"));
        Extra(EventSwapStatus.EngineError, AppIdioma.T("con error del motor", "with an engine error"));
        return string.Join(" · ", parts) + ".";
    }
}

public sealed partial class BulkCoordinator
{
    /// <summary>
    /// «Preferir la versión de evento» (ADR 0046), fase de PLANIFICACIÓN: para cada Pokémon legendario, sublegendario o singular de la selección que NO sea ya de evento, prueba con el motor real sus tarjetas
    /// de evento (la mejor primero, como mucho <see cref="EventPreference.MaxAttemptsPerPokemon"/>) generando un Pokémon nuevo de esa tarjeta; la primera que el motor da por legal (y el Core confirma aquí) es la
    /// propuesta. No toca la partida: la vista previa deja desmarcar uno a uno y solo después se aplica, dentro de la misma transacción de la edición masiva.
    /// </summary>
    public async Task<EventSwapPlan> PlanEventSwapsAsync(BulkSelection selection, AdjustOptions operations, int? workers = null)
    {
        if (_current is not null || _single?.IsBusy == true)
            return new EventSwapPlan { Items = [], Problem = Textos.OperacionEnCurso };
        var run = new RunState();
        _current = run;
        try
        {
            return await PlanEventSwapsCoreAsync(run, selection, operations, workers).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Diagnostico.RegistroOperaciones.Excepcion(AppIdioma.T("Preferir la versión de evento", "Prefer the event version"), ex);
            return new EventSwapPlan { Items = [], Problem = AppIdioma.F("No se ha modificado nada. {0}: {1}", "Nothing was modified. {0}: {1}", ex.GetType().Name, ex.Message) };
        }
        finally
        {
            _current = null;
            run.Cts.Dispose();
        }
    }

    private async Task<EventSwapPlan> PlanEventSwapsCoreAsync(RunState run, BulkSelection selection, AdjustOptions operations, int? workers)
    {
        var env = _host.Env;
        var sav = env.SAV;
        var settings = _settings();
        var allowHome = settings.AllowHomeTrackerless;
        Report(run, AppIdioma.T("Buscando versiones de evento", "Searching for event versions"), null);
        if (_library is null || !_library.IsAvailable)
            return new EventSwapPlan { Items = [], Problem = Textos.CatalogoNoEncontrado };
        var (manifest, problem) = await EnsureManifestAsync(run).ConfigureAwait(true);
        if (run.UserCancelled) return new EventSwapPlan { Items = [], Cancelled = true };
        if (manifest is null) return new EventSwapPlan { Items = [], Problem = problem };

        var game = LibraryData.GameIdOf(sav);
        var candidates = _library.LivingDexCandidates(game, allowHome, LibraryData.GcDestinationOf(sav));
        var eventsByForm = new Dictionary<(int, int), IReadOnlyList<LivingDexOrigin>>();
        foreach (var c in candidates.Where(c => EventPreference.IsPreferredSpecies(c.Species)))
            eventsByForm[(c.Species, c.Form)] = EventPreference.Ordered(c.Origins);

        var items = selection.Included;
        var decided = new Dictionary<int, EventSwapItem>();
        var jobs = new List<BulkJob>();
        var jobEvents = new Dictionary<int, IReadOnlyList<LivingDexOrigin>>();
        var jobInfo = new Dictionary<int, (string Origin, int Offered)>();
        for (var i = 0; i < items.Count; i++)
        {
            var (slot, pk) = (items[i].Slot, items[i].Pokemon);
            if (!EventPreference.IsPreferredSpecies(pk.Species)) continue;
            var la = LegalityCheck.Analyze(pk, sav, slot.Type);
            var isEvent = la.Valid && la.EncounterMatch is MysteryGift or EncounterGift3;
            var origin = la.Valid ? OriginName(la.EncounterMatch) : "ilegal";
            EventSwapItem Item(EventSwapStatus status, string? reason = null) => new() { Index = i, Slot = slot, Original = pk.Clone(), CurrentOrigin = origin, Status = status, Reason = reason, DataLanguage = DataLanguage };
            if (isEvent) { decided[i] = Item(EventSwapStatus.AlreadyEvent); continue; }
            if (!la.Valid && !operations.LegalizeIfIllegal) { decided[i] = Item(EventSwapStatus.NotLegalFirst); continue; }
            if (!eventsByForm.TryGetValue((pk.Species, pk.Form), out var events) || events.Count == 0)
            {
                decided[i] = Item(EventSwapStatus.NoEvent, AppIdioma.F("El catálogo no ofrece ninguna tarjeta de evento legal de {0} para {1}", "The catalog offers no legal event card of {0} for {1}", PokekeepCoordinator.SpeciesName(pk.Species, DataLanguage), PokekeepCoordinator.GameName(sav, _dataLanguage?.Invoke())) + (allowHome ? "." : AppIdioma.T(" (las que exigen HOME solo cuentan con el ajuste de HOME).", " (the ones that require HOME only count with the HOME setting).")));
                continue;
            }
            var tried = events.Take(EventPreference.MaxAttemptsPerPokemon).ToList();
            var template = RequestBuilder.BlankFor(sav, pk.Species, pk.Form);
            var partySize = EntityBytes.UsesPartySize(template, slot.Type);
            var target = RequestBuilder.TargetFor(sav, slot.Type);
            var selectionInfo = slot is SlotInfoBox b ? SelectionInfoFactory.Box(b.Box, b.Slot) : new SelectionInfo { Kind = "Party", Slot = slot.Slot };
            var wantedSpecies = pk.Species;
            var wantedForm = pk.Form;
            jobs.Add(new BulkJob
            {
                Id = i,
                Label = $"{PokekeepCoordinator.SpeciesName(pk.Species, DataLanguage)} · {BulkScope.Describe(slot)}",
                Accepts = response => ParseCandidate(response, sav, wantedSpecies, wantedForm, out _) is not null,
                Attempts = [.. tried.Select(o => new BulkAttemptSpec(o.EntryKey, deadline => RequestBuilder.Create(manifest, template.Clone(), partySize, target, LegalizeAction.Legalize, deadline,
                    selectionInfo, optimizeStats: settings.OptimizeStatsOnLegalize, originEventKey: o.EncounterKey, allowHomeTrackerless: allowHome, maxLevel: false, newPokemon: true, optimizeBudgetSeconds: OptimizeBudgetSeconds, coherentHistory: settings.CoherentHistory, requestedLanguage: EventPreference.RequestedLanguageOf(o))))],
            });
            jobEvents[i] = tried;
            jobInfo[i] = (origin, events.Count);
        }

        IReadOnlyList<BulkJobResult> results = [];
        if (jobs.Count > 0)
            (results, _) = await RunJobsAsync(run, env, jobs, workers, AppIdioma.T("Buscando eventos", "Searching for events"), perCase: TimeSpan.FromSeconds(45)).ConfigureAwait(true);
        if (run.TargetChanged) return new EventSwapPlan { Items = [], Problem = Textos.PartidaCambio };
        if (run.UserCancelled) return new EventSwapPlan { Items = [], Cancelled = true };

        Report(run, Textos.Comprobando, null);
        var resolved = await Task.Run(() =>
        {
            var list = new Dictionary<int, EventSwapItem>();
            foreach (var r in results)
            {
                var i = r.Job.Id;
                var (slot, pk) = (items[i].Slot, items[i].Pokemon);
                var (origin, offered) = jobInfo[i];
                var tried = jobEvents[i];
                EventSwapItem Item(EventSwapStatus status, string? reason = null, LivingDexOrigin? chosen = null, PKM? replacement = null, bool needsHome = false) => new()
                {
                    Index = i, Slot = slot, Original = pk.Clone(), CurrentOrigin = origin, Status = status, Reason = reason, Event = chosen, Replacement = replacement, NeedsHome = needsHome, DataLanguage = DataLanguage,
                    EventsOffered = offered, EventsTried = tried.Count, WhyThisCard = chosen is null ? null : EventPreference.Explain(chosen), Include = status == EventSwapStatus.Proposed,
                };
                if (r.State != BulkRunState.Done || r.Final is not { } final) { list[i] = Item(EventSwapStatus.Cancelled); continue; }
                if (!ResponseValidator.CarriesCandidate(final.Status) || final.Status == ResponseStatus.AlreadyLegal)
                {
                    var detail = string.Join(" ", new[] { TextosMotor.Traducir(final.Message), TextosMotor.Traducir(final.Reason) }.Where(x => !string.IsNullOrWhiteSpace(x)));
                    list[i] = final.Status switch
                    {
                        ResponseStatus.Timeout => Item(EventSwapStatus.TimedOut),
                        ResponseStatus.InternalError or ResponseStatus.VersionMismatch => Item(EventSwapStatus.EngineError, detail),
                        ResponseStatus.Cancelled => Item(EventSwapStatus.Cancelled),
                        _ => Item(EventSwapStatus.NotGenerable, AppIdioma.F("Ninguna de las {0} tarjetas de evento probadas sale legal con PKHeX en este juego", "None of the {0} event cards tried comes out legal with PKHeX in this game", tried.Count) + (offered > tried.Count ? AppIdioma.F(" (el catálogo ofrece {0}; se prueban las {1} primeras)", " (the catalog offers {0}; the first {1} are tried)", offered, tried.Count) : string.Empty) + (detail.Length > 0 ? $": {detail}" : ".")),
                    };
                    continue;
                }
                var chosen = r.FinalAttempt is { } a ? tried.FirstOrDefault(o => o.EntryKey == a.OriginKey) : null;
                var parsed = ParseCandidate(final, sav, pk.Species, pk.Form, out var why);
                if (parsed is null) { list[i] = Item(EventSwapStatus.NotGenerable, why); continue; }
                var needsHome = final.Status == ResponseStatus.SuccessNeedsHome;
                var ok = LegalityCheck.ResolveForSave(parsed, sav, slot.Type, needsHome);
                if (ok is null) { list[i] = Item(EventSwapStatus.NotGenerable, Textos.ValidacionFallida, chosen); continue; }
                // El resultado tiene que ser DE EVENTO según el Core (si el motor devolviera otro origen, no se vende como evento).
                var la = LegalityCheck.Analyze(ok, sav, slot.Type);
                if (la.EncounterMatch is not (MysteryGift or EncounterGift3)) { list[i] = Item(EventSwapStatus.NotGenerable, AppIdioma.T("El resultado no es de evento según PKHeX: no se propone.", "The result isn’t from an event according to PKHeX: it isn’t proposed."), chosen); continue; }
                // Evento exclusivo de un país con idioma libre: tiene que haber salido en el idioma de esa región (si el formato no lo admite, no se propone en español).
                if (chosen is not null && EventPreference.RequestedLanguageOf(chosen) is > 0 and var want && ok.Language != want)
                {
                    list[i] = Item(EventSwapStatus.NotGenerable, AppIdioma.F("Es un evento exclusivo de {0} y este juego no admite su idioma ({1}): no se propone en otro idioma.", "It is an event exclusive to {0} and this game doesn’t accept its language ({1}): it isn’t proposed in another language.", EventInfo.RegionLabel(chosen.Event!.RegionCodes[0]), BulkCoordinator.LanguageText(want)), chosen);
                    continue;
                }
                list[i] = Item(EventSwapStatus.Proposed, null, chosen, ok, needsHome);
            }
            return list;
        }).ConfigureAwait(true);

        var all = new List<EventSwapItem>();
        for (var i = 0; i < items.Count; i++)
        {
            if (decided.TryGetValue(i, out var d)) all.Add(d);
            else if (resolved.TryGetValue(i, out var r)) all.Add(r);
        }
        return new EventSwapPlan { Items = all };
    }

    /// <summary>De dónde viene un Pokémon según el encuentro que el Core le asigna (para «Rayquaza (salvaje) → evento...»).</summary>
    internal static string OriginName(IEncounterable e)
    {
        if (e is MysteryGift or EncounterGift3) return "evento";
        return Catalog.Core.EncounterKinds.Classify(e.GetType().Name, string.Empty) switch
        {
            Catalog.Core.EncounterKinds.Wild => "salvaje",
            Catalog.Core.EncounterKinds.Static => AppIdioma.T("estático", "static"),
            Catalog.Core.EncounterKinds.Egg => AppIdioma.T("cría", "breeding"),
            Catalog.Core.EncounterKinds.Trade => "intercambio",
            Catalog.Core.EncounterKinds.Gift => "regalo",
            Catalog.Core.EncounterKinds.Raid => AppIdioma.T("incursión", "raid"),
            Catalog.Core.EncounterKinds.GO => "Pokémon GO",
            _ => e.IsEgg ? AppIdioma.T("cría", "breeding") : AppIdioma.T("otro origen", "another origin"),
        };
    }

    internal static string FormNameOf(PKM pk, string idioma = "es") => FormName(pk, idioma);
}
