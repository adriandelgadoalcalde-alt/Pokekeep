// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Catalog.Library;
using PKHeX.Core;

namespace AutoLegalidad.Ui.Library;

/// <summary>Textos en castellano de la Librería: clases de origen, rasgos fijos y variables, generaciones y regiones de la Pokédex.</summary>
public static class LibraryNames
{
    /// <summary>Clases de origen (<c>library_entry.kind</c>) en el orden del árbol.</summary>
    public static readonly (string Kind, string Label)[] Kinds =
    [
        ("Wild", "Salvaje"),
        ("Static", "Estático / legendario"),
        ("Egg", "Cría (eclosionado)"),
        ("Trade", "Intercambio en el juego"),
        ("Gift", "Regalo"),
        ("Event", "Evento / distribución"),
        ("Raid", "Incursión"),
        ("GO", "Pokémon GO"),
        ("Transfer", "Solo por transferencia"),
        ("Absent", "No existe en este juego"),
        ("FormNormalization", "Forma no almacenable"),
    ];

    /// <summary>X36e: las mismas clases en inglés (solo se usan con <see cref="AppIdioma"/> en inglés).</summary>
    private static readonly Dictionary<string, string> KindsEn = new()
    {
        ["Wild"] = "Wild", ["Static"] = "Static encounter / Legendary", ["Egg"] = "Egg (hatched)", ["Trade"] = "In-game trade", ["Gift"] = "Gift", ["Event"] = "Event / distribution",
        ["Raid"] = "Raid", ["GO"] = "Pokémon GO", ["Transfer"] = "Transfer only", ["Absent"] = "Absent from this game", ["FormNormalization"] = "Form cannot be stored",
    };

    public static string Kind(string kind) => AppIdioma.Ingles ? KindsEn.GetValueOrDefault(kind) ?? kind : Array.Find(Kinds, k => k.Kind == kind).Label ?? kind;

    public static string Variant(string kind, string variant) => variant switch
    {
        "direct" => string.Empty,
        "evolution" => AppIdioma.T("evolución", "evolution"),
        "form-absent" => AppIdioma.T("forma ausente", "form absent"),
        "no-native-anchor" => AppIdioma.T("sin origen nativo", "no native origin"),
        "to-base-form" => AppIdioma.T("se guarda como forma base", "saved as base form"),
        _ when variant.StartsWith("card:", StringComparison.Ordinal) => AppIdioma.T("tarjeta", "card"),
        _ => variant,
    };

    public static readonly (int First, int Last, string Label)[] DexRanges =
    [
        (1, 151, "Kanto (1–151)"), (152, 251, "Johto (152–251)"), (252, 386, "Hoenn (252–386)"), (387, 493, "Sinnoh (387–493)"), (494, 649, "Teselia (494–649)"),
        (650, 721, "Kalos (650–721)"), (722, 809, "Alola (722–809)"), (810, 905, "Galar y Hisui (810–905)"), (906, 1025, "Paldea (906–1025)"),
    ];

    public static string Generation(int gen) => AppIdioma.T($"{gen}.ª generación", $"Generation {gen}");

    /// <summary>Rasgo del descriptor del catálogo → texto.</summary>
    public static string Trait(string code) => AppIdioma.Ingles ? TraitEn(code) : TraitEs(code);

    private static string TraitEn(string code) => code switch
    {
        "species" => "Species",
        "form" => "Form",
        "level" => "Level",
        "level-within-range" => "Level within the encounter’s range",
        "ivs" => "IVs",
        "ivs-by-method" => "IVs according to the generation method",
        "nature-or-pid-by-generation-rules" => "Nature and PID according to the generation’s rules",
        "moves" => "Moves",
        "gender" => "Gender",
        "trainer-name" => "Trainer name",
        "held-item" => "Held item",
        "shiny" => "Shiny",
        "shiny:Never" => "Never shiny",
        "shiny:Always" => "Always shiny",
        "shiny:Random" => "Shiny at random",
        _ when code.StartsWith("shiny:", StringComparison.Ordinal) => "Shiny: " + code["shiny:".Length..],
        _ => code,
    };

    private static string TraitEs(string code) => code switch
    {
        "species" => "Especie",
        "form" => "Forma",
        "level" => "Nivel",
        "level-within-range" => "Nivel dentro del rango del encuentro",
        "ivs" => "IV",
        "ivs-by-method" => "IV según el método de generación",
        "nature-or-pid-by-generation-rules" => "Naturaleza y PID según las reglas de la generación",
        "moves" => "Movimientos",
        "gender" => "Sexo",
        "trainer-name" => "Nombre del entrenador",
        "held-item" => "Objeto",
        "shiny" => "Variocolor",
        "shiny:Never" => "Nunca es variocolor",
        "shiny:Always" => "Siempre es variocolor",
        "shiny:Random" => "Variocolor al azar",
        _ when code.StartsWith("shiny:", StringComparison.Ordinal) => "Variocolor: " + code["shiny:".Length..],
        _ => code,
    };

    public static string SpeciesName(int species, string idioma = "es")
    {
        try
        {
            var names = GameInfo.GetStrings(idioma).Species;
            return species >= 0 && species < names.Count ? names[species] : AppIdioma.F("Pokémon n.º {0}", "Pokémon No. {0}", species);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return AppIdioma.F("Pokémon n.º {0}", "Pokémon No. {0}", species);
        }
    }

    /// <summary>0.6.5 (ADR 0052): frase de la ficha sobre quién figura como entrenador original (rango de <c>OwnerRanks</c>).</summary>
    public static string OwnerLine(int ownerRank) => ownerRank switch
    {
        0 => AppIdioma.T("Entrenador original: tú (capturado o criado en este juego).", "Original trainer: you (caught or bred in this game)."),
        1 => AppIdioma.T("Entrenador original: tú (el Pokémon viene de otro juego o generación).", "Original trainer: you (the Pokémon comes from another game or generation)."),
        2 => AppIdioma.T("Entrenador original: tú (tarjeta de evento sin entrenador propio).", "Original trainer: you (event card without its own trainer)."),
        3 => AppIdioma.T("Entrenador original: otro entrenador (intercambio o regalo del juego con entrenador fijo).", "Original trainer: another trainer (in-game trade or gift with a fixed trainer)."),
        4 => AppIdioma.T("Entrenador original: otro entrenador (la tarjeta de evento trae el suyo).", "Original trainer: another trainer (the event card brings its own)."),
        _ => AppIdioma.T("Entrenador original: tú (el Pokémon pasa por HOME).", "Original trainer: you (the Pokémon passes through HOME)."),
    };

    public static string Origin(LibraryRow r)
    {
        var k = Kind(r.Kind);
        var v = Variant(r.Kind, r.Variant);
        return v.Length == 0 || r.Kind == "Event" ? k : $"{k} ({v})";
    }

    /// <summary>
    /// Título que se muestra de un evento, SIN inventar nada: el título en castellano si hay una fuente fiable (la propia tarjeta en español, otra tarjeta en español del mismo evento o un título
    /// con los signos del castellano); si no, el nombre del Pokémon y el juego («Pikachu · evento en Pokémon X»). El título original (en su idioma) se enseña aparte.
    /// </summary>
    public static string EventTitle(LibraryRow r)
        => AppIdioma.Ingles ? (EnglishTitle(r) ? r.EventTitle! : $"{SpeciesName(r.SpeciesId, "en")} · event in {GameFullName(r.GameId, r.GameName)}")
            : !string.IsNullOrWhiteSpace(r.EventTitleEs) ? r.EventTitleEs! : $"{SpeciesName(r.SpeciesId)} · evento en {r.GameName}";

    /// <summary>En inglés el título de la tarjeta solo se enseña como título si la propia tarjeta está en inglés (nunca se traduce ni se inventa).</summary>
    private static bool EnglishTitle(LibraryRow r) => r.EventLanguage == "English" && !string.IsNullOrWhiteSpace(r.EventTitle);

    /// <summary>El título original de la tarjeta, si es distinto del que se muestra (no se hace pasar por castellano).</summary>
    public static string? OriginalEventTitle(LibraryRow r)
        => AppIdioma.Ingles ? (!EnglishTitle(r) && !string.IsNullOrWhiteSpace(r.EventTitle) ? r.EventTitle : null)
            : !string.IsNullOrWhiteSpace(r.EventTitle) && !string.Equals(r.EventTitle, r.EventTitleEs, StringComparison.Ordinal) ? r.EventTitle : null;

    /// <summary>De dónde sale el título en castellano (para la ficha), o null si no lo hay.</summary>
    public static string? EventTitleSourceText(LibraryRow r) => AppIdioma.Ingles ? (EnglishTitle(r) ? "title from the card itself, which is in English" : null) : r.EventTitleEsSource switch
    {
        "card" => "título de la propia tarjeta, que está en español",
        "spanish-marks" => "el título original ya está en castellano",
        "same-event" => "título de la tarjeta en español del mismo evento",
        _ => null,
    };

    public static string Provenance(LibraryRow r)
    {
        if (r.Kind == "Event")
        {
            var title = EventTitle(r);
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(title)) parts.Add(title!);
            if (!string.IsNullOrWhiteSpace(r.EventLanguage)) parts.Add(Language(r.EventLanguage!));
            if (!string.IsNullOrWhiteSpace(r.EventStartDate)) parts.Add(r.EventStartDate!);
            if (parts.Count > 0) return string.Join(" · ", parts);
        }
        return Source(r.SourceLabel);
    }

    /// <summary>
    /// Hito 4b (corrección menor): la etiqueta de fuente que guarda la base de datos («Core PKHeX (cría)», «Core PKHeX (eventos MGDB)»...) es jerga interna; al jugador se le
    /// enseña sin «Core» ni siglas. La base de datos no cambia (otras herramientas y pruebas la leen tal cual): solo se traduce al mostrarla.
    /// </summary>
    public static string Source(string? label)
    {
        if (string.IsNullOrWhiteSpace(label)) return string.Empty;
        if (AppIdioma.Ingles)
            return label switch
            {
                "Core PKHeX" => "PKHeX",
                "Core PKHeX (cría)" => "PKHeX (breeding)",
                "Core PKHeX (tabla personal)" => "PKHeX (species data)",
                "Core PKHeX (eventos MGDB)" => "PKHeX (event database)",
                "Core PKHeX (encuentros)" => "PKHeX (game encounters)",
                _ when label.StartsWith("Core PKHeX", StringComparison.Ordinal) => "PKHeX" + label["Core PKHeX".Length..],
                _ => label,
            };
        return label switch
        {
            "Core PKHeX" => "PKHeX",
            "Core PKHeX (cría)" => "PKHeX (cría)",
            "Core PKHeX (tabla personal)" => "PKHeX (datos de cada especie)",
            "Core PKHeX (eventos MGDB)" => "PKHeX (base de eventos)",
            "Core PKHeX (encuentros)" => "PKHeX (encuentros del juego)",
            _ when label.StartsWith("Core PKHeX", StringComparison.Ordinal) => "PKHeX" + label["Core PKHeX".Length..],
            _ => label,
        };
    }

    /// <summary>
    /// Texto de la columna «Estado» de la lista. Lo no disponible o no generable lleva su motivo (R-003: atenuado con el motivo); la ayuda emergente de la fila lo repite
    /// entero por si la columna no tiene ancho suficiente.
    /// </summary>
    public static string State(LibraryRow r, string? homeBlockedReason)
        => !r.Available ? (DimReasonText(r) ?? AppIdioma.T("No disponible", "Unavailable"))
            : homeBlockedReason is not null ? AppIdioma.T("Requiere el ajuste de HOME", "Requires “Without HOME”")
            : r.Addable ? (r.HomeRequired ? AppIdioma.T("Disponible (pasa por HOME)", "Available (goes through HOME)") : AppIdioma.T("Disponible", "Available"))
            : (DimReasonText(r) ?? AppIdioma.T("Pendiente de verificar", "Pending verification"));

    /// <summary>El motivo de atenuación de la fila (la base de datos lo guarda en castellano): tal cual en castellano; en inglés, su traducción.</summary>
    public static string? DimReasonText(LibraryRow r) => AppIdioma.Ingles ? DimReasonEn(r.DimReason, r) : r.DimReason;

    private static readonly (System.Text.RegularExpressions.Regex Rx, string En)[] ReasonTemplates =
    [
        (new(@"^No está en (.+)$"), "Not in {0}"),
        (new(@"^Esta forma no está en (.+)$"), "This form isn’t in {0}"),
        (new(@"^En (.+) solo se consigue por transferencia desde otro juego o desde HOME; la Librería todavía no la ofrece\.$"), "In {0} it can only be obtained by transfer from another game or from HOME; the Library doesn’t offer it yet."),
        (new(@"^Forma no almacenable en (.+): se guarda como forma base$"), "Form that can’t be stored in {0}: it is saved as the base form"),
    ];

    private static readonly Dictionary<string, string> ReasonsEn = new(StringComparer.Ordinal)
    {
        [LibraryBlocks.NotInCoreMgdb] = "This EventsGallery card isn’t in PKHeX’s event database: PKHeX doesn’t recognize it and Pokekeep can’t generate or validate it.",
        [LibraryBlocks.NotEnumerated] = "PKHeX doesn’t list this encounter for this species and form in this game: Pokekeep can’t generate or validate it.",
        [LibraryBlocks.KoreanClassicRibbon] = "Korean card with the Classic Ribbon: PKHeX doesn’t allow it in a non-Korean save (that ribbon can’t travel between Korean and international games).",
        [LibraryBlocks.FixedGender] = "The origin fixes the Pokémon’s gender and this species or form only exists in the other gender.",
        [LibraryBlocks.Pokewalker] = "There is no Pokéwalker Wurmple that gives this evolution with this save’s data (its possible PIDs don’t produce it).",
        [LibraryBlocks.EvolveOnTrade] = "This trade evolves the Pokémon when it is received and PKHeX rejects the unevolved species.",
        [LibraryBlocks.FormNotStorable] = "This game’s save format can’t store that form.",
        [LibraryBlocks.FancyForm] = "This Vivillon form (Fancy or Poké Ball) is only valid as a Mystery Gift.",
        [LibraryBlocks.NeedsHome] = "In this game this species can only be obtained by passing through Pokémon HOME, which assigns its tracker; Pokekeep can’t create it.",
        [LibraryBlocks.NeedsHomeEvenWithSetting] = "Needs to pass through Pokémon HOME and, even with the “without tracker” setting, PKHeX doesn’t consider any Pokémon with this origin valid without its real tracker (evolution, encounter or form that it only accepts after passing through HOME).",
        [LibraryBlocks.OtherFormWithSetting] = "With the “without tracker” setting only a different form or origin than the one requested is obtained, so Pokekeep doesn’t offer it.",
        [LibraryBlocks.RelaxedOnly] = "PKHeX only accepts this form with an origin different from this entry’s: it can’t be generated without changing it.",
        [LibraryBlocks.JapaneseTrade] = "This trade doesn’t exist in a Japanese save (PKHeX’s nicknames for that language don’t allow it).",
        [LibraryBlocks.Generic] = "PKHeX shows that it can’t be generated with this entry’s data (the technical detail is in the coverage report).",
        [LibraryQueries.OnlyXdReason] = "This origin only exists in Pokémon XD: the target is Pokémon Colosseum",
        [LibraryQueries.OnlyColosseumReason] = "This origin only exists in Pokémon Colosseum: the target is Pokémon XD",
        [CriaSinHuevo.Reason(GameCubeIds.Colosseum)] = "PKHeX doesn’t offer any bred Pokémon of this species in Pokémon Colosseum: there is no breeding origin to fix and Pokekeep doesn’t replace it with another origin",
        [CriaSinHuevo.Reason(GameCubeIds.Xd)] = "PKHeX doesn’t offer any bred Pokémon of this species in Pokémon XD: there is no breeding origin to fix and Pokekeep doesn’t replace it with another origin",
        [CriaSinHuevo.Reason(0)] = "PKHeX doesn’t offer any bred Pokémon of this species in this game: there is no breeding origin to fix and Pokekeep doesn’t replace it with another origin",
        ["Este intercambio de Gen 1 (Nidoran♂ y su línea) no existe en una partida internacional: PKHeX lo demuestra y Pokekeep no puede generarlo."] = "This Gen 1 trade (Nidoran♂ and its line) doesn’t exist in an international save: PKHeX shows it and Pokekeep can’t generate it.",
        ["Con los datos de esta partida PKHeX no valida este regalo con ningún origen que Pokekeep consiga generar (medido con el motor real): no se ofrece hasta que se pueda generar."] = "With this save’s data PKHeX doesn’t validate this gift with any origin that Pokekeep manages to generate (measured with the real engine): it isn’t offered until it can be generated.",
    };

    private static string? DimReasonEn(string? reason, LibraryRow r)
    {
        if (string.IsNullOrWhiteSpace(reason)) return reason;
        if (ReasonsEn.TryGetValue(reason, out var fixedText)) return fixedText;
        foreach (var (rx, en) in ReasonTemplates)
        {
            var m = rx.Match(reason);
            if (!m.Success) continue;
            var game = m.Groups[1].Value;
            return string.Format(en, game == r.GameName || "Pokémon " + game == r.GameName ? GameFullName(r.GameId, game) : game);
        }
        return reason;
    }

    /// <summary>Nombre oficial del juego en el idioma de los textos («Pokémon Sword», «Pokémon Legends: Arceus»), con los nombres del Core; en castellano, el de la base de datos.</summary>
    public static string GameFullName(int gameId, string spanishName)
    {
        if (!AppIdioma.Ingles) return spanishName;
        var games = GameInfo.GetStrings("en").gamelist;
        if ((uint)gameId >= games.Length || string.IsNullOrWhiteSpace(games[gameId])) return spanishName;
        var name = games[gameId].Trim();
        return gameId switch
        {
            42 or 43 => "Pokémon: " + name,
            34 => "Pokémon GO",
            15 => "Pokémon Colosseum / XD",
            35 => "Pokémon Red",
            36 => "Pokémon Green (Japan)",
            37 => "Pokémon Blue (Japan)",
            _ => "Pokémon " + name,
        };
    }

    public static string Language(string englishName) => AppIdioma.Ingles ? englishName switch
    {
        "ChineseS" => "Chinese (Simplified)",
        "ChineseT" => "Chinese (Traditional)",
        _ => englishName,
    } : englishName switch
    {
        "English" => "inglés",
        "Japanese" => "japonés",
        "Korean" => "coreano",
        "French" => "francés",
        "German" => "alemán",
        "Spanish" => "español",
        "Italian" => "italiano",
        "ChineseS" => "chino simplificado",
        "ChineseT" => "chino tradicional",
        _ => englishName,
    };

    /// <summary>Hito 5d (ADR 0047): regiones en el orden del filtro (código, texto). Solo existen las que el Core o la documentación de una tarjeta aportan.</summary>
    public static readonly (string Code, string Label)[] Regions =
    [
        ("US", "EE. UU. (América)"),
        ("EU", "Europa"),
        ("JP", "Japón"),
        ("KO", "Corea del Sur"),
        ("TW", "Taiwán"),
        ("INT", "Internacional (todo el mundo salvo Japón)"),
    ];

    /// <summary>«,US,EU,JP,» → «EE. UU. (América), Europa, Japón». Vacío si la fuente no da región (nunca se deduce del idioma).</summary>
    public static string Region(string? codes)
        => string.IsNullOrWhiteSpace(codes) ? string.Empty
            : string.Join(", ", codes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(c => (AppIdioma.Ingles ? Array.Find(RegionsEn, r => r.Code == c).Label : Array.Find(Regions, r => r.Code == c).Label) ?? c));

    private static readonly (string Code, string Label)[] RegionsEn =
    [
        ("US", "USA (Americas)"), ("EU", "Europe"), ("JP", "Japan"), ("KO", "South Korea"), ("TW", "Taiwan"), ("INT", "International (worldwide except Japan)"),
    ];

    /// <summary>De dónde sale la región (para la ficha): «tablas del Core (Pokémon Center de Japón)» o «hoja de seguimiento de EventsGallery».</summary>
    public static string RegionSource(string? sourceRef) => sourceRef switch
    {
        null or "" => string.Empty,
        _ when sourceRef.StartsWith("core:", StringComparison.Ordinal) => AppIdioma.T("tabla de PKHeX ", "PKHeX table ") + (AppIdioma.Ingles ? sourceRef["core:".Length..].Replace("Pokémon Center de Japón", "Pokémon Center Japan").Replace("Pokémon Center de Nueva York", "Pokémon Center New York") : sourceRef["core:".Length..]),
        _ when sourceRef.StartsWith("eventsgallery@", StringComparison.Ordinal) => AppIdioma.T("documentación de la tarjeta (hoja de seguimiento de EventsGallery, ", "card documentation (EventsGallery tracking sheet, ") + sourceRef.Split(':')[0] + ")",
        _ => sourceRef,
    };
}
