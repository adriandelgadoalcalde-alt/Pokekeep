// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;

namespace AutoLegalidad.Ui.Bulk;

/// <summary>
/// Hito 4c (retoque del 4b): una especie que la partida no admite porque le falta una EXPANSIÓN (DLC) no «no existe en este juego»: existe, pero esa partida no la tiene.
/// El propio Core decide qué especies admite cada revisión del guardado (<c>SAV9SV.MaxSpeciesID</c>, <c>SAV8SWSH.MaxSpeciesID</c>, <c>SAV9ZA.MaxSpeciesID</c>, según <c>SaveRevision</c>: 0 = sin DLC,
/// 1 = primer DLC, 2 = segundo). Los umbrales son los de <c>Legal.cs</c> del Core fijado (internos, de ahí que se repitan aquí; <c>ExpansionInfoTests</c> los compara por reflexión con el Core).
/// </summary>
public static class ExpansionInfo
{
    /// <summary>Umbral de especie (inclusive) de cada revisión del guardado, por juego; el índice es la revisión.</summary>
    public static IReadOnlyList<ushort>? Thresholds(SaveFile sav) => sav switch
    {
        SAV9SV => [(ushort)Species.IronLeaves, (ushort)Species.Ogerpon, (ushort)Species.Pecharunt],   // Legal.cs:122,127,132
        SAV8SWSH => [890, 893, 898],                                                                     // Legal.cs:85,91,97 (Eternatus, Zarude, Calyrex)
        SAV9ZA => [(ushort)Species.Falinks, (ushort)Species.Gholdengo],                                 // Legal.cs:143,149
        _ => null,
    };

    /// <summary>Nombres de los DLC (el índice es la revisión; el 0 es el juego base). Nombres oficiales en español.</summary>
    public static IReadOnlyList<string>? Names(SaveFile sav) => sav switch
    {
        SAV9SV => ["", AppIdioma.T("La máscara turquesa", "The Teal Mask"), AppIdioma.T("El disco índigo", "The Indigo Disk")],
        SAV8SWSH => ["", AppIdioma.T("La isla solitaria de la Armadura", "The Isle of Armor"), AppIdioma.T("Las nieves de la Corona", "The Crown Tundra")],
        SAV9ZA => ["", AppIdioma.T("Megadimensión", "Mega Dimension")],
        _ => null,
    };

    public static int Revision(SaveFile sav) => sav switch
    {
        SAV9SV sv => sv.SaveRevision,
        SAV8SWSH ss => ss.SaveRevision,
        SAV9ZA za => za.SaveRevision == 0 ? 0 : 1,
        _ => 0,
    };

    /// <summary>
    /// Si la especie-forma existe en el juego pero SOLO con un DLC que esta partida no tiene, devuelve el texto llano «Requiere el DLC «…», que esta partida no tiene»; si no, null.
    /// Se decide con la tabla personal del Core (la especie-forma está en el juego completo) y el umbral de especie de la revisión del guardado (la partida no la admite).
    /// </summary>
    public static string? MissingExpansion(SaveFile sav, int species, int form)
        => Thresholds(sav) is { } thr && Names(sav) is { } names ? Missing(thr, names, Revision(sav), species) : null;

    /// <summary>Núcleo de <see cref="MissingExpansion"/> con la revisión explícita (los guardados en blanco del Core ya traen el último DLC: así se prueba el caso sin DLC).</summary>
    public static string? Missing(IReadOnlyList<ushort> thresholds, IReadOnlyList<string> names, int revision, int species)
    {
        var current = Math.Clamp(revision, 0, thresholds.Count - 1);
        if (species <= thresholds[current]) return null;   // la revisión actual ya la admite: si no existe, no es cuestión de DLC
        for (var rev = current + 1; rev < thresholds.Count; rev++)
            if (species <= thresholds[rev]) return AppIdioma.F("Requiere el DLC «{0}», que esta partida no tiene.", "Requires the “{0}” DLC, which this save doesn’t have.", names[rev]);
        return null;
    }
}
