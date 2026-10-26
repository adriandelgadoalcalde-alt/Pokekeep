// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Engine.Generation;
using PKHeX.Core;

namespace AutoLegalidad.Engine.Optimization;

/// <summary>
/// Gen1/2: se trabaja con DV (0-15, el DV de PS sale de los otros), nunca con IV de 31. En Gen2 el variocolor, el sexo (por el DV de Ataque), la letra de
/// Unown y el tipo de Poder Oculto dependen de los DV: se enumeran las 65.536 combinaciones, se descartan las que cambian alguno de esos rasgos y se prueba,
/// en orden de suma descendente, cada una con el analisis oficial (los encuentros con DV fijos o acotados las rechazan solos).
/// </summary>
internal static class GbDvOptimizer
{
    public static SubResult Run(OptimizeRequest r, PKM pk, LegalityAnalysis la, ref int attempts)
    {
        if (pk is not GBPKM)
            return new SubResult(null, "Enumeration", true, "El formato no es de Game Boy.", "DvEnumeration");
        // Encuentros con DV fijados (intercambios de Gen2 con IV especificados...): el Core exige el vector exacto (EncounterTrade2.IsMatchExact -> Legal.GetIsFixedIVSequenceValidNoRand, Legal.cs:297-306),
        // asi que cualquier otro DV es invalido y no hay nada que probar con las 65.536 combinaciones.
        if (FixedIvSets.Specified(la.EncounterMatch))
            return new SubResult(null, "FixedIVs", true, $"Este encuentro ({la.EncounterMatch.GetType().Name}) define IV fijos: no hay margen de mejora.", "DvEnumeration");
        var cur = PkmFieldsAccess.IVs(pk).Sum();
        var knowsHp = StatOptimizer.KnowsHiddenPower(pk);
        var hpType = pk.HPType;
        var relevance = StatPriority.MostRelevantFirst(pk);
        // Pesos por posicion de relevancia (visual): ATK=1, DEF=2, SPC=3 (y 4), SPE=5.
        var weight = new int[6];
        for (var pos = 0; pos < 6; pos++) weight[relevance[pos]] = 6 - pos;

        var probe = (GBPKM)pk.Clone();
        var list = new List<(ushort Dv, int Sum, int Tie)>(8192);
        for (var atk = 0; atk < 16; atk++)
        for (var def = 0; def < 16; def++)
        for (var spe = 0; spe < 16; spe++)
        for (var spc = 0; spc < 16; spc++)
        {
            var dv = (ushort)((atk << 12) | (def << 8) | (spe << 4) | spc);
            probe.DV16 = dv;
            if (pk.Format == 2)
            {
                // Solo Gen2 tiene variocolor y sexo ligados a los DV (en Gen1 no existen: no deben restringir la busqueda).
                if (probe.IsShiny != pk.IsShiny) continue;
                if (probe.Gender != pk.Gender) continue;
            }
            if (pk.Format == 2 && pk.Species == (ushort)Species.Unown && probe.Form != pk.Form) continue;
            if (knowsHp && probe.HPType != hpType) continue;
            var iv = PkmFieldsAccess.IVs(probe);
            var sum = iv.Sum();
            if (sum <= cur) continue;
            var tie = 0;
            for (var i = 0; i < 6; i++) tie += iv[i] * weight[i];
            list.Add((dv, sum, tie));
        }
        list.Sort((a, b) =>
        {
            var c = b.Sum.CompareTo(a.Sum);
            if (c != 0) return c;
            c = b.Tie.CompareTo(a.Tie);
            return c != 0 ? c : a.Dv.CompareTo(b.Dv);
        });

        // Hito 3b (cola de clases): se recorre la lista ENTERA (como mucho 65.536 combinaciones) hasta el plazo; antes se cortaba a las 600 mejores y declaraba «sin mejora» sin busqueda completa.
        var cut = false;
        foreach (var cand in list)
        {
            if (r.Cancellation.IsCancellationRequested || DateTime.UtcNow >= r.DeadlineUtc) { cut = true; break; }
            attempts++;
            var c2 = (GBPKM)pk.Clone();
            c2.DV16 = cand.Dv;
            c2.RefreshChecksum();
            if (StatOptimizer.QuickValid(c2, r.Dest, pk))
                return new SubResult(c2, "Enumeration", true, null, "DvEnumeration");
        }
        // Si se agoto la lista, no existe mejora; si se corto por el plazo, no se demuestra nada.
        return cut ? new SubResult(null, "CoreOracleProbe", false, $"Se agotó el plazo tras validar parte de las {list.Count:N0} combinaciones de DV que conservan sexo, variocolor y Poder Oculto sin hallar una que acepte PKHeX.", "DvEnumeration") : new SubResult(null, "Enumeration", true, "Estos DV ya son los mejores que permiten su sexo, su variocolor y, si lo conoce, su Poder Oculto (cada combinación superior cambia alguno o no es legal para el encuentro).", "DvEnumeration");
    }
}
