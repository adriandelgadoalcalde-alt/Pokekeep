// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Ui;
using System.Globalization;
using PKHeX.Core;

namespace AutoLegalidad.Catalog.Library;

/// <summary>Datos de una tarjeta de evento tal como los guarda la Librería (todo sale del catálogo: Core PKHeX y EventsGallery). Nada de esto interviene en la validez: la decide siempre el Core.</summary>
/// <param name="Title">Título de la tarjeta (en inglés o el que traiga la fuente), si lo hay.</param>
/// <param name="TitleEs">Título en español de fuente fiable (la propia tarjeta, signos del castellano o la tarjeta española del mismo evento), si lo hay.</param>
/// <param name="CardType">Tipo de tarjeta del Core (PGT, PCD, PGF, WC6, WC7, WB7, WC8, WC9...; <c>EncounterGift3*</c> en Gen 3).</param>
/// <param name="CardId">Identificador de la tarjeta (0 = sin identificador).</param>
/// <param name="Language">Idioma de la tarjeta (nombre del enum <c>LanguageID</c> en inglés: «Spanish», «Japanese»...), o null si la tarjeta lo deja libre.</param>
/// <param name="RestrictLanguage">Idioma al que la tarjeta se restringe (<c>RestrictLanguage</c> del Core), o null.</param>
/// <param name="StartDate">Inicio de la distribución (ISO <c>aaaa-mm-dd</c>) si la tarjeta lo trae.</param>
/// <param name="Region">Códigos de región de la distribución (<c>,US,EU,JP,</c>) si el Core o la documentación de la tarjeta la aportan (ADR 0047), o null.</param>
/// <param name="ShinyRule">Regla de variocolor de la tarjeta: <c>Never</c>, <c>Random</c>, <c>Always</c>, <c>AlwaysStar</c>, <c>AlwaysSquare</c> o null.</param>
public sealed record EventInfo(string? Title, string? TitleEs, string? CardType, int CardId, string? Language, string? RestrictLanguage, string? StartDate, string? ShinyRule, string? Region = null)
{
    public bool IsSpanish => string.Equals(Language, "Spanish", StringComparison.Ordinal);

    /// <summary>Año de la distribución si la tarjeta trae fecha de inicio.</summary>
    public int? Year => StartDate is { Length: >= 4 } d && int.TryParse(d.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var y) ? y : null;

    /// <summary>Regiones de la distribución como lista de códigos (vacía si ninguna fuente la da).</summary>
    public IReadOnlyList<string> RegionCodes => Region?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];

    public static string RegionLabel(string code) => code switch
    {
        "US" => AppIdioma.T("EE. UU.", "the USA"), "EU" => AppIdioma.T("Europa", "Europe"), "JP" => AppIdioma.T("Japón", "Japan"), "KO" => AppIdioma.T("Corea del Sur", "South Korea"), "TW" => AppIdioma.T("Taiwán", "Taiwan"), "INT" => AppIdioma.T("el resto del mundo (no Japón)", "the rest of the world (not Japan)"), _ => code,
    };

    /// <summary>
    /// Nombre LEGIBLE en castellano, sin inventar nada: el título en español de fuente fiable; si no, una descripción compuesta solo con datos respaldados («Evento de Japón (tarjeta WC6 n.º 156)»: región de la fuente, tipo e id de la tarjeta).
    /// El título original (en su idioma) NO se usa aquí: el jugador puede no leerlo; sigue en la ficha de la Librería.
    /// </summary>
    public string DisplayTitle
    {
        get
        {
            if (TitleEs is not null) return TitleEs;
            var gen3 = CardType switch
            {
                "EncounterGift3" => AppIdioma.T("Evento de 3.ª generación", "Generation 3 event"),
                "EncounterGift3JPN" => AppIdioma.T("Evento de 3.ª generación (Pokémon Center de Japón)", "Generation 3 event (Pokémon Center Japan)"),
                "EncounterGift3NY" => AppIdioma.T("Evento de 3.ª generación (Pokémon Center de Nueva York)", "Generation 3 event (Pokémon Center New York)"),
                _ => null,
            };
            if (gen3 is not null) return gen3;
            var where = RegionCodes.Count switch { 0 => string.Empty, 1 => AppIdioma.T(" de ", " from ") + RegionLabel(RegionCodes[0]), _ => AppIdioma.T(" de ", " from ") + string.Join(", ", RegionCodes.Select(RegionLabel)) };
            var card = CardType is null ? string.Empty : CardId > 0 ? AppIdioma.F(" (tarjeta {0} n.º {1})", " (card {0} No. {1})", CardType, CardId) : AppIdioma.F(" (tarjeta {0})", " (card {0})", CardType);
            return AppIdioma.T("Evento", "Event") + where + card;
        }
    }

    /// <summary>
    /// La distribución solo existió en consolas de un país cuyo idioma no es el español (Japón, Corea, Taiwán) y la tarjeta deja el idioma libre en el Core: el motor la daría en ESPAÑOL, y una tarjeta exclusiva de un país
    /// conserva su idioma (R-005 regla 3). Hasta que el motor pueda pedir ese idioma, no se propone en español (ADR 0046 §2.5).
    /// </summary>
    public bool ExclusiveToNonSpanishRegion
        => Language is null && RestrictLanguage is null && RegionCodes.Count > 0 && RegionCodes.All(c => c is "JP" or "KO" or "TW");

    /// <summary>
    /// Idioma (nombre de <c>LanguageID</c>) que conserva una tarjeta exclusiva de un país de otro idioma con el idioma libre en el Core: Japón -> Japanese, Corea -> Korean, Taiwán -> ChineseT. null si no es exclusiva
    /// o si la distribución abarca regiones con idiomas distintos. El motor lo recibe como <c>RequestedLanguage</c>.
    /// </summary>
    public string? RegionLanguage => !ExclusiveToNonSpanishRegion ? null : RegionCodes.Distinct().Count() != 1 ? null : RegionCodes[0] switch { "JP" => "Japanese", "KO" => "Korean", "TW" => "ChineseT", _ => null };
}

/// <summary>
/// «Preferir la versión de evento» (Hito 5d, ADR 0046): qué especies entran y en qué orden se prueban las tarjetas de un mismo Pokémon. Es una ordenación OBJETIVA y reproducible de lo que el
/// catálogo ya sabe (nunca una valoración de mercado inventada); que la tarjeta elegida sea legal en el destino lo demuestra siempre el motor con el Core.
/// </summary>
public static class EventPreference
{
    /// <summary>Máximo de tarjetas que se prueban por Pokémon (cada una es una solicitud al motor): cota documentada, las siguientes quedan sin probar y el informe lo dice.</summary>
    public const int MaxAttemptsPerPokemon = 6;

    /// <summary>
    /// Especies a las que aplica la función: legendarios, sublegendarios y singulares (míticos, «Pokémon singular» en español) según <see cref="SpeciesCategory"/> del Core fijado.
    /// Las Ultraentes y las Paradoja NO entran: no son «legendarios» ni «singulares» para el Core (<c>IsLegendary</c>/<c>IsSubLegendary</c>/<c>IsMythical</c>).
    /// </summary>
    public static bool IsPreferredSpecies(int species)
        => species is > 0 and <= ushort.MaxValue && (SpeciesCategory.IsLegendary((ushort)species) || SpeciesCategory.IsSubLegendary((ushort)species) || SpeciesCategory.IsMythical((ushort)species));

    public static string CategoryName(int species)
        => species is <= 0 or > ushort.MaxValue ? string.Empty
            : SpeciesCategory.IsMythical((ushort)species) ? "singular"
            : SpeciesCategory.IsLegendary((ushort)species) ? "legendario"
            : SpeciesCategory.IsSubLegendary((ushort)species) ? "sublegendario" : string.Empty;

    /// <summary>1 = variocolor garantizado de cualquier tipo; 2 y 3 = estrella y cuadrado, que solo dan las distribuciones especiales; 0 = ninguno garantizado (nunca, al azar o sin dato).</summary>
    public static int ShinyTier(string? rule) => rule switch
    {
        "AlwaysSquare" => 3,
        "AlwaysStar" => 2,
        "Always" => 1,
        _ => 0,
    };

    /// <summary>
    /// Las tarjetas de evento de un Pokémon, de la que se prueba primero a la última. Criterio (ADR 0046 §3), en este orden y sin azar:
    /// 1) tarjeta en español (R-005); 2) repartida en Europa; 3) variocolor garantizado de la tarjeta (cuadrado, estrella, normal); 4) tarjeta exclusiva de un solo idioma (distribución regional, más escasa);
    /// 4) distribución más antigua (fecha de inicio conocida antes que desconocida); 5) clave de entrada (desempate estable). Las que exigen HOME van siempre después de las que no.
    /// </summary>
    public static IReadOnlyList<LivingDexOrigin> Ordered(IEnumerable<LivingDexOrigin> origins)
        => [.. origins.Where(o => o.Kind == "Event" && o.Event is not null)
            .OrderBy(o => o.HomeRequired ? 1 : 0)
            .ThenBy(o => o.Event!.IsSpanish ? 0 : 1)
            .ThenBy(o => o.Event!.RegionCodes.Contains("EU") ? 0 : 1)
            .ThenByDescending(o => ShinyTier(o.Event!.ShinyRule))
            .ThenBy(o => o.Event!.RestrictLanguage is { Length: > 0 } ? 0 : 1)
            .ThenBy(o => o.Event!.StartDate is null ? 1 : 0)
            .ThenBy(o => o.Event!.StartDate, StringComparer.Ordinal)
            .ThenBy(o => o.EntryKey, StringComparer.Ordinal)];

    /// <summary>Idioma pedido al motor para esta tarjeta (valor de <c>LanguageID</c>), o 0 si no hay que pedir ninguno.</summary>
    public static int RequestedLanguageOf(LivingDexOrigin origin)
        => origin.Event?.RegionLanguage is { } name && Enum.TryParse<LanguageID>(name, out var id) ? (int)id : 0;

    /// <summary>Frase que explica por qué esta tarjeta se prueba antes que otras (la vista previa la enseña).</summary>
    public static string Explain(LivingDexOrigin origin)
    {
        var e = origin.Event;
        if (e is null) return string.Empty;
        var parts = new List<string>();
        if (e.IsSpanish) parts.Add(AppIdioma.T("tarjeta en español", "Spanish card"));
        else if (e.RegionCodes.Contains("EU")) parts.Add(AppIdioma.T("repartida en Europa", "distributed in Europe"));
        if (ShinyTier(e.ShinyRule) is var t && t > 0) parts.Add(t == 3 ? AppIdioma.T("variocolor cuadrado garantizado", "guaranteed square shiny") : t == 2 ? AppIdioma.T("variocolor estrella garantizado", "guaranteed star shiny") : AppIdioma.T("variocolor garantizado", "guaranteed shiny"));
        if (e.RestrictLanguage is { Length: > 0 } r) parts.Add(AppIdioma.F("exclusiva del idioma {0}", "exclusive to the {0} language", r));
        if (e.Year is { } y) parts.Add(AppIdioma.F("la distribución más antigua que declara la fuente ({0})", "the oldest distribution the source declares ({0})", y));
        return parts.Count == 0 ? AppIdioma.T("primera tarjeta por clave", "first card by key") : string.Join(", ", parts);
    }
}
