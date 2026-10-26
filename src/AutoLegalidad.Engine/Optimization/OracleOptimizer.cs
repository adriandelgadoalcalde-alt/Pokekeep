// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Engine.Generation;
using PKHeX.Core;

namespace AutoLegalidad.Engine.Optimization;

/// <summary>
/// IV sin correlacion con la semilla (Gen5+ no ligados): se sube cada IV al maximo y el ORACULO es el analisis oficial del Core, que ya conoce los IV
/// fijos de eventos, las exigencias de IV perfectos de cada encuentro, las reglas de GO, etc. Primero se prueba el vector completo y, si el Core lo
/// rechaza, se sube estadistica a estadistica en orden de relevancia y se queda con las que el Core admite. Conserva el tipo de Poder Oculto si el
/// Pokemon conoce ese movimiento (solo cambia la magnitud, no la paridad de cada IV).
/// </summary>
internal static class OracleOptimizer
{
    public static SubResult Run(OptimizeRequest r, PKM pk, LegalityAnalysis la, ref int attempts)
    {
        var cur = PkmFieldsAccess.IVs(pk);
        var max = pk.MaxIV;
        var knowsHp = StatOptimizer.KnowsHiddenPower(pk) && pk.Format >= 3;
        var target = new int[6];
        for (var i = 0; i < 6; i++)
            target[i] = knowsHp && (cur[i] & 1) == 0 ? max - 1 : max; // misma paridad => mismo tipo de Poder Oculto
        if (cur.Sum() >= target.Sum() && !cur.Where((v, i) => v < target[i]).Any())
            return new SubResult(null, "Enumeration", true, "Los IV ya están al máximo permitido.", "OracleGreedy");

        // 1) vector completo de una vez
        var all = pk.Clone();
        SetAll(all, target, cur);
        attempts++;
        if (!cur.AsSpan().SequenceEqual(PkmFieldsAccess.IVs(all)) && StatOptimizer.QuickValid(all, r.Dest, pk))
            return new SubResult(all, "CoreOracleProbe", false, null, "OracleGreedy");

        // 2) estadistica a estadistica (por relevancia)
        PKM best = pk;
        var improved = false;
        foreach (var stat in StatPriority.MostRelevantFirst(pk))
        {
            if (r.Cancellation.IsCancellationRequested || DateTime.UtcNow >= r.DeadlineUtc) break;
            var now = PkmFieldsAccess.IVs(best);
            if (now[stat] >= target[stat]) continue;
            var c = best.Clone();
            SetIv(c, stat, target[stat]);
            attempts++;
            if (StatOptimizer.QuickValid(c, r.Dest, pk))
            {
                best = c;
                improved = true;
            }
        }
        return improved ? new SubResult(best, "CoreOracleProbe", false, null, "OracleGreedy") : new SubResult(null, FixedIvs(la) ? "FixedIVs" : "CoreOracleProbe", FixedIvs(la), null, "OracleGreedy");
    }

    private static void SetAll(PKM pk, int[] target, int[] cur)
    {
        for (var i = 0; i < 6; i++)
            if (target[i] > cur[i]) SetIv(pk, i, target[i]);
        pk.RefreshChecksum();
    }

    /// <summary>Fija un IV por indice VISUAL (PS, Ata, Def, AtE, DeE, Vel).</summary>
    internal static void SetIv(PKM pk, int visual, int value)
    {
        switch (visual)
        {
            case 0: pk.IV_HP = value; break;
            case 1: pk.IV_ATK = value; break;
            case 2: pk.IV_DEF = value; break;
            case 3: pk.IV_SPA = value; break;
            case 4: pk.IV_SPD = value; break;
            default: pk.IV_SPE = value; break;
        }
        StatOptimizer.RefreshDerivedFromIvs(pk);
        pk.RefreshChecksum();
    }

    private static bool FixedIvs(LegalityAnalysis la) => FixedIvSets.Specified(la.EncounterMatch);
}
