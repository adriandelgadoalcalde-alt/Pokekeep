// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Ui.Operation;
using PKHeX.Core;

namespace AutoLegalidad.Ui.Bulk;

/// <summary>Una fila de «Lo que no se genera, con su motivo»: nombre, estado (etiqueta del núcleo), el motivo que se enseña en la lista y el motivo COMPLETO (detalle y ayuda emergente).</summary>
public sealed record LivingDexPreviewRow(string Name, BulkStatus Status, string StatusLabel, string Shown, string Reason);

/// <summary>
/// Los textos de la vista previa de «Completar el juego» (F9, bloque 9b: extraídos de <c>LivingDexPreviewForm</c> para que el escritorio y el móvil lean los MISMOS): resumen, capacidad, tiempo estimado,
/// botón, casilla de HOME y la lista de lo que no se genera con su motivo. Se calcula sin llamar al motor ni tocar la partida, a partir del plan del <see cref="LivingDexPlanner"/>.
/// </summary>
public sealed class LivingDexPreview
{
    /// <summary>Etiquetas de las opciones «Formas», en el orden de <see cref="LivingDexForms"/> (las del parche 0007 de PKHeX en castellano).</summary>
    public static IReadOnlyList<string> FormLabels => [AppIdioma.T("Una por especie (recomendado)", "One per species (recommended)"), AppIdioma.T("Todas las formas almacenables", "All storable forms"), AppIdioma.T("Una por especie + formas regionales", "One per species + regional forms")];
    public static string NationalLabel => AppIdioma.T("Pokédex nacional", "National Pokédex");
    public static string RegionalLabel => AppIdioma.T("Pokédex regional del juego", "The game’s regional Pokédex");
    public static string FormsLabel => AppIdioma.T("Formas", "Forms");
    public static string OrderLabel => AppIdioma.T("Orden", "Order");
    public static string StartBoxLabel => AppIdioma.T("Empezar en la caja", "Start at box");
    public static string SkipOwnedLabel => AppIdioma.T("Omitir lo que ya tengo", "Skip what I already have");
    public static string WorkersLabel => AppIdioma.T("Procesos en paralelo", "Parallel processes");
    public static string PreferEventLabel => AppIdioma.T("Preferir la versión de evento (legendarios y singulares)", "Prefer the event version (legendary and unique Pokémon)");
    public static string PreferEventHelp => AppIdioma.T("Los legendarios, sublegendarios y singulares se intentan primero desde una tarjeta de evento legal en este juego (tarjeta en español primero, luego variocolor, exclusiva de un país y distribución más antigua). Si ninguna sale legal con el motor se usa el origen de siempre. Nunca se fuerza nada.", "Legendary, sub-legendary and unique Pokémon are tried first from a legal event card in this game (Spanish card first, then shiny, country-exclusive and oldest distribution). If none comes out legal with the engine, the usual origin is used. Nothing is ever forced.");
    public static string HomeLabel => AppIdioma.T("Activar el ajuste de HOME (con riesgo): sin la matrícula de HOME, PKHeX los da por inválidos hasta pasarlos por HOME", "Turn on the HOME setting (risky): without the HOME tracker, PKHeX considers them invalid until they are passed through HOME");
    public static string HomeHelp => AppIdioma.T("Sin la matrícula que asigna Pokémon HOME, PKHeX da estos Pokémon por inválidos hasta que los pases por HOME. Pokekeep nunca se inventa la matrícula. Solo cuenta mientras no canceles este diálogo; se puede cambiar siempre en Opciones > Ajustes > Pokekeep.", "Without the tracker that Pokémon HOME assigns, PKHeX considers these Pokémon invalid until you pass them through HOME. Pokekeep never makes up the tracker. It only counts while you don’t cancel this dialog; you can always change it in Options > Settings > Pokekeep.");
    public static string ListTitleWithRows => AppIdioma.T("Lo que no se genera, con su motivo", "What isn’t generated, with its reason");
    public static string ListTitleEmpty => AppIdioma.T("Todo lo planificado se puede generar: no hay nada excluido.", "Everything planned can be generated: nothing is excluded.");
    public static string NoSelection => AppIdioma.T("Elige una fila para leer su motivo completo.", "Choose a row to read its full reason.");
    public static string NoHomeHint => AppIdioma.T("Necesita pasar por HOME: marca «Activar el ajuste de HOME (con riesgo)» en este diálogo para generarlo (sin matrícula, PKHeX lo da por inválido hasta pasarlo por HOME).", "Needs to pass through HOME: tick “Turn on the HOME setting (risky)” in this dialog to generate it (without a tracker, PKHeX considers it invalid until it is passed through HOME).");
    public static string ProvenShort => AppIdioma.T("Lo demuestra PKHeX (prueba completa abajo y en la ayuda emergente).", "PKHeX proves it (full proof below and in the tooltip).");

    /// <summary>Etiquetas de «Orden» para esta partida: la regional solo si el Core trae su número (<see cref="LivingDexPlanner.HasRegionalDex"/>).</summary>
    public static IReadOnlyList<string> OrderLabels(SaveFile sav) => LivingDexPlanner.HasRegionalDex(sav) ? [NationalLabel, RegionalLabel] : [NationalLabel];

    /// <summary>«Platino · LUCAS · idioma de los Pokémon nuevos: español (o el que el origen permita)».</summary>
    public static string GameLine(SaveFile sav) => AppIdioma.F("{0} · {1} · idioma de los Pokémon nuevos: español (o el que el origen permita)", "{0} · {1} · language of the new Pokémon: Spanish (or the one the origin allows)", PokekeepCoordinator.GameName(sav), sav.OT);

    public required LivingDexPlan Plan { get; init; }
    public required string Summary { get; init; }
    public required string Capacity { get; init; }
    /// <summary>Vacío si no cabe ninguno.</summary>
    public required string Estimate { get; init; }
    /// <summary>«Generar» o «Generar hasta donde quepa».</summary>
    public required string GoText { get; init; }
    public required bool GoEnabled { get; init; }
    /// <summary>La casilla de HOME se enseña si el ajuste ya está activo o hay candidatos que solo existen con él.</summary>
    public required bool ShowHomeOption { get; init; }
    public required IReadOnlyList<LivingDexPreviewRow> Rows { get; init; }
    public bool HasRows => Rows.Count > 0;
    public string ListTitle => HasRows ? ListTitleWithRows : ListTitleEmpty;

    /// <summary>Texto del detalle de una fila (el recuadro de debajo de la lista): «Nombre: motivo completo».</summary>
    public static string Detail(LivingDexPreviewRow? row) => row is null ? NoSelection : $"{row.Name}: {row.Reason}";

    /// <param name="homeOn">El ajuste de HOME tal como está AHORA (la casilla del diálogo lo cambia).</param>
    /// <param name="workers">Procesos con los que se estima el tiempo.</param>
    public static LivingDexPreview Build(LivingDexPlan plan, SaveFile sav, bool homeOn, int workers)
    {
        var version = RequestBuilder.ResolveVersion(sav);
        var parts = new List<string> { AppIdioma.F("Pokémon a generar: {0}", "Pokémon to generate: {0}", plan.Planned) };
        if (plan.Options.SkipOwned) parts.Add(AppIdioma.F("ya en la partida (omitidos): {0}", "already in the save (skipped): {0}", plan.AlreadyHad));
        parts.Add(AppIdioma.F("no se pueden generar: {0}", "can’t be generated: {0}", plan.CannotGenerate - plan.ProvenImpossible - plan.RequiresExpansion));
        if (plan.RequiresExpansion > 0) parts.Add(AppIdioma.F("{0} requieren un DLC que esta partida no tiene", "{0} require DLC that this save doesn’t have", plan.RequiresExpansion));
        if (plan.ProvenImpossible > 0) parts.Add(AppIdioma.F("{0} no existen en este juego (lo demuestra PKHeX)", "{0} don’t exist in this game (PKHeX proves it)", plan.ProvenImpossible));
        if (plan.Options.PreferEvent)
        {
            var special = plan.Targets.Where(t => Catalog.Library.EventPreference.IsPreferredSpecies(t.Candidate.Species)).ToList();
            var withEvent = special.Count(t => t.Origins.Count > 0 && t.Origins[0].Kind == "Event");
            parts.Add(AppIdioma.F("evento preferido: {0} de {1} legendarios, sublegendarios y singulares se intentan primero desde una tarjeta de evento", "preferred event: {0} of {1} legendary, sub-legendary and unique Pokémon are tried first from an event card", withEvent, special.Count));
        }
        var needHome = plan.Targets.Count(t => t.NeedsHome);
        if (!homeOn && plan.OnlyWithHome > 0)
            parts.Add(AppIdioma.F("{0} Pokémon solo se pueden tener activando el ajuste de HOME", "{0} Pokémon can only be had by turning on the HOME setting", plan.OnlyWithHome));
        else if (homeOn && needHome > 0)
            parts.Add(AppIdioma.F("{0} necesitan pasar por HOME (ajuste activado)", "{0} need to pass through HOME (setting on)", needHome));

        var rows = new List<LivingDexPreviewRow>();
        foreach (var s in plan.Skips.Where(s => s.Kind != LivingDexSkipKind.AlreadyHad))
        {
            var name = PokekeepCoordinator.SpeciesName((ushort)s.Candidate.Species) + (s.Candidate.Form != 0 ? $" ({BulkCoordinator.FormLabel(s.Candidate)})" : string.Empty);
            var status = s.Kind switch
            {
                LivingDexSkipKind.OnlyWithHome => BulkStatus.OnlyWithHome,
                LivingDexSkipKind.NotStorable => BulkStatus.NotStorable,
                LivingDexSkipKind.ProvenImpossible => BulkStatus.NotInGame,
                LivingDexSkipKind.RequiresExpansion => BulkStatus.RequiresExpansion,
                _ => BulkStatus.CannotGenerate,
            };
            var reason = s.Kind == LivingDexSkipKind.OnlyWithHome && !homeOn ? NoHomeHint : s.Reason;
            // Lo demostrado con PKHeX lleva la prueba técnica completa (detalle y ayuda); en la lista, una frase llana.
            var shown = s.Kind == LivingDexSkipKind.ProvenImpossible ? ProvenShort : reason;
            rows.Add(new LivingDexPreviewRow(name, status, BulkStatusText.Label(status), shown, reason));
        }
        if (plan.Missing > 0)
        {
            var text = AppIdioma.F("{0} Pokémon no caben: {1}", "{0} Pokémon don’t fit: {1}", plan.Missing, plan.FitText());
            rows.Add(new LivingDexPreviewRow(AppIdioma.T("(varios)", "(several)"), BulkStatus.NoRoom, BulkStatusText.Label(BulkStatus.NoRoom), text, text));
        }

        return new LivingDexPreview
        {
            Plan = plan,
            Summary = string.Join(" · ", parts) + ".",
            Capacity = plan.Planned == 0
                ? AppIdioma.T("No hay nada que generar.", "There is nothing to generate.")
                : AppIdioma.F("Casillas necesarias: {0} → {1} {2} · libres desde la caja {3}: {4} · {5}", "Slots needed: {0} → {1} {2} · free from box {3}: {4} · {5}", plan.Planned, plan.BoxesNeeded, (plan.BoxesNeeded == 1 ? AppIdioma.T("caja", "box") : AppIdioma.T("cajas", "boxes")), plan.Options.StartBox + 1, plan.FreeSlots.Count, plan.FitText()),
            Estimate = plan.Fit == 0 ? string.Empty : AppIdioma.F("Tiempo estimado: ~{0} con {1} {2}. Nunca se sobrescribe nada y se puede deshacer de una vez.", "Estimated time: ~{0} with {1} {2}. Nothing is ever overwritten and it can all be undone at once.", Textos.Duracion(plan.Estimate(version, workers)), workers, (workers == 1 ? AppIdioma.T("proceso", "process") : AppIdioma.T("procesos", "processes"))),
            GoText = plan.AllFit ? AppIdioma.T("Generar", "Generate") : AppIdioma.T("Generar hasta donde quepa", "Generate as many as fit"),
            GoEnabled = plan.Fit > 0,
            ShowHomeOption = homeOn || plan.OnlyWithHome > 0,
            Rows = rows,
        };
    }

    /// <summary>La UNA pregunta al cancelar con Pokémon ya generados (diseño §1.8, <c>BulkPlacementDialog</c>): el texto y los botones son los mismos en el escritorio y en el móvil.</summary>
    public static string PlacementQuestion(int generated, bool edit) => edit
        ? AppIdioma.F("¿Aplicar los {0} cambios ya preparados? El resto no se tocará. Se podrá deshacer todo de una vez.", "Apply the {0} changes already prepared? The rest won’t be touched. Everything can be undone at once.", generated)
        : AppIdioma.F("¿Colocar los {0} Pokémon ya generados? Los demás no se generarán. Se podrá deshacer todo de una vez.", "Place the {0} Pokémon already generated? The others won’t be generated. Everything can be undone at once.", generated);
}
