// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Engine.Context;
using PKHeX.Core;

namespace AutoLegalidad.Engine.Generation;

/// <summary>
/// Hito 3b: Gen1/2 no guardan el idioma, solo si la partida es japonesa o internacional (<c>SAV1/SAV2.Japanese</c>). Los regalos con restriccion de idioma (<c>EncounterGift1/2.Language</c>)
/// solo existen en uno de los dos grupos: si el destino es del otro, el Core los rechaza SIEMPRE (<c>IsMatchExact</c>), sin importar ningun otro dato. Dominio finito y estatico: es una
/// demostracion, no un muestreo. Solo se consulta con un origen fijado y despues de que la busqueda no hallara nada.
/// </summary>
public static class GbLanguageProof
{
    public static bool TryDisprove(IEncounterable? enc, Destination dest, out string evidence)
    {
        evidence = string.Empty;
        switch (enc)
        {
            case EncounterGift1 g1 when dest.Sav is SAV1 s1 && g1.Language != EncounterGift1.LanguageRestriction.Any && s1.Japanese != (g1.Language == EncounterGift1.LanguageRestriction.Japanese):
                evidence = $"El regalo exige idioma {g1.Language} y la partida de destino es {(s1.Japanese ? "japonesa" : "internacional")}: PKHeX lo rechaza siempre. Referencia técnica: PKHeX.Core Legality/Encounters/Templates/Gen1/EncounterGift1.cs:210 (IsMatchExact: pk.Japanese != (Language == Japanese)) y Saves/SAV1.cs:18 (Japanese es propiedad de la partida).";
                return true;
            case EncounterGift2 g2 when dest.Sav is SAV2 s2 && s2.Japanese != (g2.Language == EncounterGift2.LanguageRestriction.Japanese):
                evidence = $"El regalo exige idioma {g2.Language} y la partida de destino es {(s2.Japanese ? "japonesa" : "internacional")}: solo un Pokémon japonés puede venir de un regalo japonés. Referencia técnica: PKHeX.Core Legality/Encounters/Templates/Gen2/EncounterGift2.cs:201 y 222-226 (IsLanguageValid) y Saves/SAV2.cs:18.";
                return true;
        }
        return false;
    }
}
