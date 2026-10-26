// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Engine.Context;
using PKHeX.Core;

namespace AutoLegalidad.Engine.Generation;

/// <summary>
/// Hito 3b: imposibilidad DEMOSTRADA con el Core de una tarjeta coreana de Gen4 con la Cinta Clasica en una partida de Gen4 NO coreana. Un Pokemon coreano solo llega a una partida
/// internacional de Gen4 por el GTS (<c>Gen4GlobalTradeRules.IsRequiredGTS</c>, Gen4GlobalTradeRules.cs:10-19: coreano e internacional solo intercambian por el GTS) y el Core prohibe
/// que la Cinta Clasica viaje por el GTS entre juegos coreanos e internacionales (<c>LanguageVerifier.CheckGTS</c>, LanguageVerifier.cs:73-76: <c>GTSDisallowedClassicRibbon</c>).
/// La tarjeta reparte la cinta (<c>IRibbonSetEvent4.RibbonClassic</c>), asi que no existe ningun Pokemon de esa tarjeta en esa partida.
/// </summary>
public static class KoreanGtsProof
{
    public static bool TryDisprove(IEncounterable? enc, Destination dest, out string evidence)
    {
        evidence = string.Empty;
        if (enc is not DataMysteryGift g || enc is not IRibbonSetEvent4 { RibbonClassic: true }) return false;
        if (LanguagePolicy.FixedLanguageOf(g) != LanguageID.Korean) return false;
        if (dest.Sav.Generation != 4 || !Gen4GlobalTradeRules.IsRequiredGTS(dest.Trainer, LanguageID.Korean)) return false;
        evidence = "Una tarjeta coreana de Gen4 que reparte la Cinta Clásica no puede existir en una partida de Gen4 no coreana: el Pokémon coreano solo llega por el GTS " +
                   "y PKHeX prohíbe la Cinta Clásica en el GTS entre juegos coreanos e internacionales. " +
                   "Referencia técnica: PKHeX.Core Legality/Verifiers/Misc/Gen4GlobalTradeRules.cs:10-19 (IsRequiredGTS) y " +
                   "Legality/Verifiers/LanguageVerifier.cs:73-76 (GTSDisallowedClassicRibbon).";
        return true;
    }
}
