// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Contracts;

namespace AutoLegalidad.Ui.Bulk;

/// <summary>Una operación de la «Edición masiva» tal como se enseña al usuario: nombre estable, título, si va marcada por defecto y la línea de ayuda (con el AVISO cuando cambia el origen).</summary>
public sealed record BulkOperationInfo(string Key, string Label, bool DefaultOn, string Help)
{
    /// <summary>Nombre corto de la columna del informe («Stats», «PS/estado»...).</summary>
    public string ShortName => BulkStatusText.Name(Key);
}

/// <summary>
/// Catálogo ÚNICO de las operaciones de la edición masiva (F9-0, ADR 0069): el escritorio (<c>BulkEditForm</c>) y el móvil (puerto <c>IMasivas</c>) leen de aquí los títulos, los valores por defecto y las ayudas,
/// de modo que no hay dos copias de un texto que pueda diverger. El orden es el de la ventana; el orden de EJECUCIÓN de cada Pokémon es <see cref="AdjustOperationNames.Order"/>.
/// </summary>
public static class BulkOperationCatalog
{
    public static IReadOnlyList<BulkOperationInfo> All => [
        new(AdjustOperationNames.Legalize, AppIdioma.T("Legalizar los ilegales primero", "Legalize the illegal ones first"), false, AppIdioma.T("Los Pokémon ilegales se legalizan (puede cambiar su origen, conservando su naturaleza si es posible) antes de lo demás. Sin esto, los ilegales no se tocan.", "Illegal Pokémon are legalized (this may change their origin, keeping their nature if possible) before anything else. Without this, illegal ones are left alone.")),
        new(AdjustOperationNames.Optimize, AppIdioma.T("Optimizar stats", "Optimize stats"), true, AppIdioma.T("Mejora los IV al máximo que permite su encuentro, sin cambiar ningún dato de origen.", "Improves the IVs to the maximum its encounter allows, without changing any origin data.")),
        new(AdjustOperationNames.MaxLevel, AppIdioma.T("Nivel máximo legal", "Maximum legal level"), true, AppIdioma.T("Sube el nivel al más alto que PKHeX da por válido (hasta 100). No cambia el nivel de captura. Los huevos no se tocan.", "Raises the level to the highest PKHeX considers valid (up to 100). It doesn’t change the met level. Eggs are left alone.")),
        new(AdjustOperationNames.MaxFriendship, AppIdioma.T("Amistad máxima legal", "Maximum legal friendship"), true, AppIdioma.T("Pone la amistad a 255 solo si PKHeX lo valida; nunca busca otro valor.", "Sets friendship to 255 only if PKHeX validates it; it never looks for another value.")),
        new(AdjustOperationNames.SpanishLanguage, AppIdioma.T("Idioma español cuando sea legal", "Spanish language when legal"), true, AppIdioma.T("AVISO: cambia el idioma de origen del Pokémon. Los eventos de un solo idioma conservan el suyo y se informa.", "WARNING: this changes the Pokémon’s origin language. Single-language events keep theirs and this is reported.")),
        new(AdjustOperationNames.MaxPP, AppIdioma.T("PP máximos", "Maximum PP"), true, AppIdioma.T("Más PP al tope en los movimientos que lo admiten y PP restaurados. No existe en Leyendas: Arceus ni en Leyendas: Z-A.", "PP Ups maxed on the moves that allow it and PP restored. It doesn’t exist in Legends: Arceus or Legends: Z-A.")),
        new(AdjustOperationNames.HealParty, AppIdioma.T("Restaurar PS y estado", "Restore HP and status"), true, AppIdioma.T("PS al máximo y sin estado alterado. Solo aplica al equipo (y a los formatos de Game Boy).", "HP at maximum and no status condition. It only applies to the party (and to Game Boy formats).")),
        new(AdjustOperationNames.CurePokerus, AppIdioma.T("Curar Pokérus", "Cure Pokérus"), false, AppIdioma.T("Si el Pokémon está infectado, lo cura conservando su cepa. Nunca vacuna a quien nunca lo tuvo.", "If the Pokémon is infected, it cures it, keeping its strain. It never vaccinates one that never had it.")),
    ];

    /// <summary>Las opciones del contrato para un conjunto de claves marcadas (las no marcadas quedan apagadas).</summary>
    public static AdjustOptions Options(IEnumerable<string> keys)
    {
        var on = keys.ToHashSet(StringComparer.Ordinal);
        return new AdjustOptions
        {
            LegalizeIfIllegal = on.Contains(AdjustOperationNames.Legalize),
            Optimize = on.Contains(AdjustOperationNames.Optimize),
            MaxLevel = on.Contains(AdjustOperationNames.MaxLevel),
            MaxFriendship = on.Contains(AdjustOperationNames.MaxFriendship),
            SpanishLanguage = on.Contains(AdjustOperationNames.SpanishLanguage),
            MaxPP = on.Contains(AdjustOperationNames.MaxPP),
            HealParty = on.Contains(AdjustOperationNames.HealParty),
            CurePokerus = on.Contains(AdjustOperationNames.CurePokerus),
        };
    }
}

/// <summary>
/// Textos de la ventana «Edición masiva», del selector de Pokémon y de la vista previa de «Preferir la versión de evento» (bloque 9a, ADR 0069): vivían en los formularios WinForms y se extraen aquí para que el
/// escritorio y el móvil enseñen los MISMOS (no hay dos copias que puedan diverger). Los menús de PKHeX (parche 0006) componen los suyos con <see cref="Textos.EdicionMasiva"/> igual que estos.
/// </summary>
public static class BulkEditTexts
{
    // ---- Entradas (parche 0006: clic derecho de caja y de casilla) ----
    public static string MenuThisBox => AppIdioma.T(Textos.EdicionMasiva + " de esta caja…", "Bulk edit this box…");
    public static string MenuAllBoxes => AppIdioma.T(Textos.EdicionMasiva + " de todas las cajas…", "Bulk edit all boxes…");
    public static string MenuThisPokemon => AppIdioma.T(Textos.EdicionMasiva + " de este Pokémon…", "Bulk edit this Pokémon…");
    public static string MenuCompleteGame => Textos.CompletarElJuego + "…";
    /// <summary>Ayuda de «Edición masiva de este Pokémon…» en el menú de la casilla.</summary>
    public static string MenuThisPokemonHelp => AppIdioma.T("Elige qué mejorar de este Pokémon: stats, nivel, amistad, idioma, PP... (comprobado con PKHeX y con un único Deshacer).", "Choose what to improve on this Pokémon: stats, level, friendship, language, PP... (checked with PKHeX and with a single Undo).");

    // ---- Ámbito ----
    public static string ScopeGroup => AppIdioma.T("Ámbito", "Scope");
    public static string AllBoxes => AppIdioma.T("Todas", "All");
    public static string NoBoxes => AppIdioma.T("Ninguna", "None");
    public static string PickPokemon => AppIdioma.T("Elegir Pokémon…", "Choose Pokémon…");
    public static string NothingToEdit => AppIdioma.T("No hay ningún Pokémon que editar en este ámbito.", "There are no Pokémon to edit in this scope.");
    public static string ZonesPrefix => AppIdioma.T("Zonas que PKHeX no deja modificar (no se tocan): ", "Areas PKHeX doesn’t let you modify (left untouched):");

    /// <summary>Nombre de cada ámbito, tal como sale en la ventana.</summary>
    public static string ScopeLabel(BulkScopeKind kind) => kind switch
    {
        BulkScopeKind.All => AppIdioma.T("Todo (todas las cajas y el equipo)", "Everything (all boxes and the party)"),
        BulkScopeKind.Boxes => AppIdioma.T("Cajas concretas", "Specific boxes"),
        BulkScopeKind.Marked => AppIdioma.T("Pokémon marcados", "Marked Pokémon"),
        _ => AppIdioma.T("Este Pokémon", "This Pokémon"),
    };

    // ---- Operaciones y ejecución ----
    public static string OperationsGroup => AppIdioma.T("Operaciones (cada una se comprueba con PKHeX; si deja el Pokémon ilegal, solo esa se deshace)", "Operations (each one is checked with PKHeX; if it leaves the Pokémon illegal, only that one is undone)");
    public static string Workers => AppIdioma.T("Procesos en paralelo", "Parallel processes");
    public const int MinWorkers = 1;
    public const int MaxWorkers = 8;
    public static string Estimate(TimeSpan t) => AppIdioma.F("Tiempo estimado: ~{0}", "Estimated time: ~{0}", Textos.Duracion(t));
    public static string Apply => AppIdioma.T("Aplicar", "Apply");

    // ---- Selector de Pokémon marcados ----
    public static string PickerTitle => AppIdioma.T("Elegir Pokémon", "Choose Pokémon");
    public static string PickerSlot => AppIdioma.T("Casilla", "Slot");
    public const string PickerPokemon = "Pokémon";
    public const string PickerLevel = "Nv.";
    public static string MarkAll => AppIdioma.T("Marcar todos", "Mark all");
    public static string UnmarkAll => AppIdioma.T("Desmarcar todos", "Unmark all");
    public static string Accept => AppIdioma.T("Aceptar", "Accept");

    // ---- Preferir la versión de evento (Hito 5d, ADR 0046) ----
    public static string PreferEventGroup => AppIdioma.T("Versión de evento (cambia el origen)", "Event version (changes the origin)");
    public static string PreferEventLabel => AppIdioma.T("Preferir la versión de evento (legendarios y singulares)", "Prefer the event version (legendary and unique Pokémon)");
    public static string PreferEventHelp => AppIdioma.T("AVISO: si un Pokémon legendario, sublegendario o singular tiene una tarjeta de evento legal en este juego, se propone sustituirlo por su versión de evento (entrenador, fecha, ball y cintas serán los del evento). Verás una vista previa donde desmarcar uno a uno; lo que ya es de evento no se toca. Desactivado por defecto.", "WARNING: if a legendary, sub-legendary or unique Pokémon has a legal event card in this game, it is proposed to replace it with its event version (trainer, date, ball and ribbons will be the event’s). You will see a preview where you can unmark them one by one; what is already from an event is left alone. Off by default.");
    public static string EventSwapTitle => AppIdioma.T("Preferir la versión de evento", "Prefer the event version");
    public static string EventSwapWarning => AppIdioma.T("Esto CAMBIA EL ORIGEN del Pokémon: el que ya tienes se sustituye por uno nuevo generado desde la tarjeta de evento (entrenador, número de entrenador, fecha, ball, cintas y movimientos serán los del evento). Lo que ya es de evento no se toca. Todo va en una sola transacción con un único «Deshacer».", "This CHANGES THE ORIGIN of the Pokémon: the one you have is replaced by a new one generated from the event card (trainer, trainer ID, date, ball, ribbons and moves will be the event’s). What is already from an event is left alone. Everything goes in a single transaction with a single “Undo”.");

    /// <summary>Texto del botón «Aplicar» de la vista previa de eventos: cuántos cambios de origen se aplican o, si ninguno, si hay otras operaciones que hacer.</summary>
    public static string EventSwapApply(int accepted, bool otherOperations)
        => accepted > 0 ? AppIdioma.F("Aplicar ({0} {1})", "Apply ({0} {1})", accepted, (accepted == 1 ? AppIdioma.T("cambio de origen", "origin change") : AppIdioma.T("cambios de origen", "origin changes"))) : otherOperations ? AppIdioma.T("Aplicar sin cambiar orígenes", "Apply without changing origins") : AppIdioma.T("Aplicar", "Apply");

    /// <summary>«Aplicar» de la vista previa de eventos está activo si se cambia algún origen o hay otras operaciones.</summary>
    public static bool EventSwapCanApply(int accepted, bool otherOperations) => accepted > 0 || otherOperations;

    /// <summary>Se pide aplicar una búsqueda de versiones de evento que ya no corresponde a la partida abierta (otra búsqueda u otra partida): no se toca nada.</summary>
    public static string EventSearchStale => AppIdioma.T("La búsqueda de versiones de evento ya no vale.", "The search for event versions is no longer valid.");
    public static string EventSearchStaleDetail => AppIdioma.T("No se ha modificado nada. Vuelve a buscarlas.", "Nothing was modified. Search for them again.");

    /// <summary>Columnas de la lista de la vista previa de eventos.</summary>
    public static IReadOnlyList<string> EventSwapColumns => [AppIdioma.T("Casilla", "Slot"), "Pokémon", AppIdioma.T("Origen actual", "Current origin"), AppIdioma.T("Evento propuesto", "Proposed event"), AppIdioma.T("Año", "Year"), "Idioma", AppIdioma.T("Estado", "Status")];
}
