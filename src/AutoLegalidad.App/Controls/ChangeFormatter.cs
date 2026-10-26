// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Globalization;
using PKHeX.Core;

namespace AutoLegalidad.Ui.Controls;

/// <summary>Convierte los valores crudos de <c>changes[]</c> (ids numéricos, nombres de enum en inglés) a texto legible en castellano para «Ver cambios» (en inglés con <see cref="AppIdioma"/>). Los NOMBRES de campo («Especie», «Habilidad»...) son identificadores del motor y no se traducen aquí.</summary>
public static class ChangeFormatter
{
    public static string Describe(string field, string? raw, string idioma = "es")
    {
        if (raw is null) return "—";
        var strings = Strings(idioma);
        try
        {
            if (field.StartsWith("Movimiento", StringComparison.Ordinal) && field != "Movimientos recordables" && ushort.TryParse(raw, out var move))
                return move == 0 ? AppIdioma.T("ninguno", "none") : Lookup(strings?.movelist, move, raw);   // Hito 5b: la casilla vacía no es «0»
            switch (field)
            {
                case "Especie" when ushort.TryParse(raw, out var sp):
                    return Lookup(strings?.Species, sp, raw);
                case "Habilidad" when int.TryParse(raw, out var ab):
                    return ab == 0 ? AppIdioma.T("ninguna", "none") : Lookup(strings?.abilitylist, ab, raw);
                case "Objeto" when int.TryParse(raw, out var item):
                    return item == 0 ? AppIdioma.T("ninguno", "none") : Lookup(strings?.itemlist, item, raw);
                case "Poke Ball" when int.TryParse(raw, out var ball):
                    return Lookup(strings?.balllist, ball, raw);
                case "Naturaleza" when Enum.TryParse<Nature>(raw, out var nature):
                    return Lookup(strings?.natures, (int)nature, raw);
                case "Sexo" when int.TryParse(raw, out var g):
                    return g switch { 0 => AppIdioma.T("♂ macho", "♂ male"), 1 => AppIdioma.T("♀ hembra", "♀ female"), _ => AppIdioma.T("sin sexo", "genderless") };
                case "Juego de origen" when Enum.TryParse<GameVersion>(raw, out var v):
                    return idioma != "es" && strings is not null && (uint)v < strings.gamelist.Length ? strings.gamelist[(int)v] : GameInfo.GetVersionName(v);
                case "Idioma" when int.TryParse(raw, out var lang):
                    return Enum.IsDefined((LanguageID)lang) ? Language((LanguageID)lang) : raw;
                case "Movimientos recordables":
                    return string.IsNullOrEmpty(raw) ? AppIdioma.T("ninguno", "none") : string.Join(", ", raw.Split(',').Select(m => ushort.TryParse(m, out var mv) && mv != 0 ? Lookup(strings?.movelist, mv, m) : "—"));
                case "Variocolor" or "Es huevo":
                    return bool.TryParse(raw, out var b) ? (b ? AppIdioma.T("sí", "yes") : AppIdioma.T("no", "no")) : raw;
                case "PID" or "Constante de cifrado" when uint.TryParse(raw, CultureInfo.InvariantCulture, out var u):
                    return u.ToString("X8", CultureInfo.InvariantCulture);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return raw;
        }
        return raw;
    }

    private static string Lookup(IReadOnlyList<string>? names, int index, string fallback)
        => names is not null && index >= 0 && index < names.Count && names[index].Length > 0 ? names[index] : fallback;

    private static string Language(LanguageID l) => l switch
    {
        LanguageID.Japanese => AppIdioma.T("japonés", "Japanese"),
        LanguageID.English => AppIdioma.T("inglés", "English"),
        LanguageID.French => AppIdioma.T("francés", "French"),
        LanguageID.Italian => AppIdioma.T("italiano", "Italian"),
        LanguageID.German => AppIdioma.T("alemán", "German"),
        LanguageID.Spanish => AppIdioma.T("español", "Spanish"),
        LanguageID.Korean => AppIdioma.T("coreano", "Korean"),
        LanguageID.ChineseS => AppIdioma.T("chino simplificado", "Chinese (Simplified)"),
        LanguageID.ChineseT => AppIdioma.T("chino tradicional", "Chinese (Traditional)"),
        _ => l.ToString(),
    };

    private static GameStrings? Strings(string idioma)
    {
        try { return GameInfo.GetStrings(idioma); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return null; }
    }

}
