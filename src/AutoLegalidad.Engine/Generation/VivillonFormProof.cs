// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Engine.Context;
using PKHeX.Core;

namespace AutoLegalidad.Engine.Generation;

/// <summary>
/// Hito 3b: los patrones de Vivillon Fancy (18) y Poke Ball (19) no se obtienen de un encuentro salvaje o estatico: <c>FormVerifier.cs:91-119</c> da Invalido a Scatterbug/Spewpa con una forma
/// mayor que <c>Vivillon3DS.MaxWildFormID</c> (17) y a Vivillon con ella salvo que el encuentro sea un regalo misterioso; en Gen9 solo la forma 18 (Fancy) tiene encuentro propio y la 19
/// exige que la forma del encuentro sea esa. Dato finito y estatico del Core: es una demostracion, no un muestreo. Solo se consulta con un origen fijado.
/// </summary>
public static class VivillonFormProof
{
    public static bool TryDisprove(IEncounterable? enc, PKM wish, Destination dest, out string evidence)
    {
        evidence = string.Empty;
        if (enc is null or MysteryGift || enc is not IEncounterTemplate tpl) return false;
        if (wish.Species is not ((ushort)Species.Scatterbug or (ushort)Species.Spewpa or (ushort)Species.Vivillon)) return false;
        if (dest.Context == EntityContext.Gen9)
        {
            if (wish.Form > Vivillon3DS.FancyFormID && tpl.Form != wish.Form)
            {
                evidence = $"En Gen9 la forma {wish.Form} de {wish.Species} (Poké Ball) exige que la forma del encuentro sea la misma y la plantilla es de forma {tpl.Form}. Referencia técnica: PKHeX.Core Legality/Verifiers/FormVerifier.cs:91-93 (form > 18 && enc.Form != form => FormVivillonEventPre).";
                return true;
            }
            return false;
        }
        if (wish.Form > Vivillon3DS.MaxWildFormID)
        {
            evidence = $"La forma {wish.Form} de {wish.Species} (Fancy/Poké Ball) es inválida salvo en un regalo misterioso y la plantilla es un {tpl.GetType().Name}. Referencia técnica: PKHeX.Core Legality/Verifiers/FormVerifier.cs:96-111 (form > Vivillon3DS.MaxWildFormID = 17 => FormVivillonEventPre / FormVivillonInvalid si el encuentro no es un MysteryGift).";
            return true;
        }
        return false;
    }
}
