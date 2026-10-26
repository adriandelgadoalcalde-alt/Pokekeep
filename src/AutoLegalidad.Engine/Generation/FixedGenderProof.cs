// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Engine.Context;
using PKHeX.Core;

namespace AutoLegalidad.Engine.Generation;

/// <summary>
/// Hito 3b: un encuentro de sexo FIJO (<c>IFixedGender</c>, p. ej. el Ralts hembra del intercambio de X/Y) no puede dar una especie o una forma que solo existe en el otro sexo (Gallade, solo macho):
/// el sexo de la plantilla se compara con el de la entidad (<c>EncounterTrade6.cs:161</c>) y la evolucion no lo cambia. Las tarjetas de regalo (WC6/WC7/WC8/WC9/WA8/WA9/WB7/WB8, campo
/// <c>Gender</c>: 0 macho, 1 hembra, 2 sin sexo, 3 aleatorio; PGF: 2 aleatorio) lo fijan igual en su <c>IsMatchExact</c> (<c>Gender != 3 &amp;&amp; Gender != pk.Gender</c>, p. ej. WC9.cs:705, WA9.cs:673) y,
/// en las especies cuya FORMA es el sexo (Oinkologne, Meowstic, Indeedee, Basculegion: <c>SpeciesCategory.IsFormGenderSpecific</c>), la tabla personal de cada forma es solo macho o solo hembra.
/// Dominio finito y estatico (dato de la plantilla y de la tabla personal del destino): es una demostracion, no un muestreo. Solo se consulta con un origen fijado.
/// </summary>
public static class FixedGenderProof
{
    /// <summary>Sexo fijado por la tarjeta o la plantilla (0 macho, 1 hembra, 2 sin sexo) y la referencia del Core que lo exige; <c>null</c> si el encuentro no lo fija.</summary>
    public static (byte Gender, string Source)? FixedGenderOf(IEncounterable enc)
    {
        if (enc is IFixedGender { IsFixedGender: true } fixedGender) return (fixedGender.Gender, "IFixedGender, p. ej. EncounterTrade6.cs:161");
        return enc switch
        {
            PGF { Gender: not 2 } pgf => (pgf.Gender, "PGF.cs:454"),
            WC6 { Gender: < 3 } wc => (wc.Gender, "WC6.cs:557"),
            WC7 { Gender: < 3 } wc => (wc.Gender, "WC7.cs:603"),
            WC8 { Gender: < 3 } wc => (wc.Gender, "WC8.cs:720"),
            WC9 { Gender: < 3 } wc => (wc.Gender, "WC9.cs:705"),
            WA8 { Gender: < 3 } wc => (wc.Gender, "WA8.cs:687"),
            WA9 { Gender: < 3 } wc => (wc.Gender, "WA9.cs:673"),
            WB7 { Gender: < 3 } wc => (wc.Gender, "WB7.cs:647"),
            WB8 { Gender: < 3 } wc => (wc.Gender, "WB8.cs:688"),
            _ => null,
        };
    }

    public static bool TryDisprove(IEncounterable? enc, PKM wish, Destination dest, out string evidence)
    {
        evidence = string.Empty;
        if (enc is not IEncounterTemplate tpl || (tpl.Species == wish.Species && tpl.Form == wish.Form)) return false;
        if (enc is null || FixedGenderOf(enc) is not { } fixedGender) return false;
        IPersonalInfo pi;
        try { pi = dest.Personal.GetFormEntry(wish.Species, wish.Form); }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException) { return false; }
        var g = fixedGender.Gender;
        string? only = null;
        if (pi.OnlyMale && g != 0) only = "solo macho";
        else if (pi.OnlyFemale && g != 1) only = "solo hembra";
        else if (pi.Genderless && g != 2) only = "sin sexo";
        if (only is null) return false;
        var sexo = g switch { 0 => "macho", 1 => "hembra", _ => "sin sexo" };
        evidence = $"La plantilla {tpl.Species}-{tpl.Form} fija el sexo ({sexo}) y la especie pedida {wish.Species}-{wish.Form} es {only} en la tabla personal de {dest.Version}; "
                   + $"el sexo del encuentro se exige igual en la entidad y la evolución no lo cambia. Referencia técnica: {fixedGender.Source}; PersonalInfo.cs:67-69.";
        return true;
    }
}
