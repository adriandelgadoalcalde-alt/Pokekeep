// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;

namespace AutoLegalidad.Catalog.Core;

/// <summary>
/// Datos descriptivos de una tarjeta de regalo, leidos con las APIs del Core (para la Libreria: nombre en español, fechas, idioma/OT, rasgos fijos y variables).
/// No incluye NINGUN dato del entrenador del usuario: OT/idioma/fecha son los FIJOS del evento (restricciones del evento, p. ej. eventos japoneses).
/// </summary>
public static class GiftInfo
{
    public static string Title(DataMysteryGift g)
    {
        try { return g.CardTitle.Replace('　', ' ').Trim(); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return string.Empty; }
    }



    /// <summary>Idioma fijo del evento (0 = cualquiera) y restriccion de idioma, si el formato los expone.</summary>
    public static (int Language, int RestrictLanguage) Languages(DataMysteryGift g) => g switch
    {
        PGF p => (p.Language, p.RestrictLanguage),
        WC6 w => (w.Language, w.RestrictLanguage),
        WC7 w => (w.Language, w.RestrictLanguage),
        PCD pcd => (pcd.Gift.PK.Language, 0),
        _ => (0, 0),
    };

    /// <summary>Fecha de inicio de la tarjeta si el formato la tiene y es plausible: una fecha posterior a la version del Core (<paramref name="notAfter"/>) es un marcador, no historia, y se descarta (NULL = desconocido, nunca se inventa).</summary>
    public static string? StartDate(DataMysteryGift g, DateOnly? notAfter = null)
    {
        DateOnly? d = g switch { PGF p => p.Date, WC6 w => w.Date, WC7 w => w.Date, WB7 w => w.Date, _ => null };
        if (d is { } v && notAfter is { } max && v > max) return null;
        return d?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
    }

    public static string LanguageName(int id) => id == 0 ? string.Empty : Enum.IsDefined((LanguageID)id) ? ((LanguageID)id).ToString() : id.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Descripcion completa para <c>descriptor_json</c> de un encuentro de tipo evento.</summary>
    public static Dictionary<string, object?> Describe(DataMysteryGift g, string typeName, string giftSha256, DateOnly? dateNotAfter = null)
    {
        var title = Title(g);
        // Titulo en español SOLO cuando la propia tarjeta esta en español (el Core no trae textos de tarjeta localizados al español: no se hace pasar ingles ni mezclas por español).
        var (lang, restrict) = Languages(g);
        var ot = SafeOt(g);
        var fixedIvs = g.HasFixedIVs;
        var fixedTraits = new List<object?> { "species", "form", "level" };
        var variable = new List<object?>();
        if (g.Shiny != Shiny.Random) fixedTraits.Add("shiny:" + g.Shiny); else variable.Add("shiny");
        if (g.Gender != 3) fixedTraits.Add("gender"); else variable.Add("gender");
        if (!string.IsNullOrEmpty(ot)) fixedTraits.Add("trainer-name"); else variable.Add("trainer-name");
        if (g.HeldItem > 0) fixedTraits.Add("held-item");
        if (g.Moves.HasMoves) fixedTraits.Add("moves");
        if (fixedIvs) fixedTraits.Add("ivs"); else variable.Add("ivs");
        variable.Add("nature-or-pid-by-generation-rules");
        return new Dictionary<string, object?>
        {
            ["type"] = typeName, ["kind"] = EncounterKinds.Event, ["version"] = g.Version.ToString(), ["context"] = g.Context.ToString(),
            ["generation"] = (int)g.Generation, ["cardId"] = g.CardID, ["cardTitle"] = title, ["cardTitleEs"] = LanguageName(Languages(g).Language) == "Spanish" && title.Length > 0 ? title : null,
            ["isEgg"] = g.IsEgg, ["levelMin"] = (int)g.LevelMin, ["levelMax"] = (int)g.LevelMax, ["gift_sha256"] = giftSha256,
            ["eventLanguage"] = LanguageName(lang), ["restrictLanguage"] = LanguageName(restrict), ["startDate"] = StartDate(g, dateNotAfter), ["startDateDiscardedAsMarker"] = StartDate(g) is { } raw && StartDate(g, dateNotAfter) is null ? raw : null,
            ["eventOT"] = ot, ["shinyRule"] = g.Shiny.ToString(), ["genderValue"] = (int)g.Gender, ["gigantamax"] = g is IGigantamaxReadOnly { CanGigantamax: true }, ["fixedTraits"] = fixedTraits, ["variableTraits"] = variable,
        };
    }

    private static string SafeOt(DataMysteryGift g)
    {
        try { return g.OriginalTrainerName.Replace('　', ' ').Trim(); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return string.Empty; }
    }
}
