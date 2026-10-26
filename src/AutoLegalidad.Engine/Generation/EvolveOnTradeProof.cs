// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Engine.Context;
using PKHeX.Core;

namespace AutoLegalidad.Engine.Generation;

/// <summary>
/// Hito 3b: un intercambio del juego que EVOLUCIONA al Pokemon al recibirlo (<c>EvolveOnTrade</c>: el Haunter de Escarlata/Purpura, los de Sol/Luna, Rojo/Azul...) solo existe en su forma evolucionada:
/// el Core rechaza la especie sin evolucionar (<c>EncounterTrade9.cs:191</c>, <c>EncounterTrade7.cs:151</c>, <c>EncounterTrade1.cs:188</c>: <c>if (EvolveOnTrade &amp;&amp; pk.Species == Species) return false</c>).
/// Dato estatico de la plantilla: es una demostracion, no un muestreo. Solo se consulta con un origen fijado.
/// </summary>
public static class EvolveOnTradeProof
{
    public static bool TryDisprove(IEncounterable? enc, PKM wish, out string evidence)
    {
        evidence = string.Empty;
        var (evolves, file) = enc switch
        {
            EncounterTrade9 t => (t.EvolveOnTrade, "Legality/Encounters/Templates/Gen9/EncounterTrade9.cs:191"),
            EncounterTrade7 t => (t.EvolveOnTrade, "Legality/Encounters/Templates/Gen7/EncounterTrade7.cs:151"),
            EncounterTrade1 t => (t.EvolveOnTrade, "Legality/Encounters/Templates/Gen1/EncounterTrade1.cs:188"),
            _ => (false, string.Empty),
        };
        if (!evolves || enc is not IEncounterTemplate tpl || tpl.Species != wish.Species) return false;
        evidence = $"El intercambio {tpl.Species} evoluciona al Pokémon al recibirlo (EvolveOnTrade) y PKHeX rechaza la especie sin evolucionar. Referencia técnica: PKHeX.Core {file} (if (EvolveOnTrade && pk.Species == Species) return false).";
        return true;
    }
}
