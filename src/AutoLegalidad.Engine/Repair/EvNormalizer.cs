// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;

namespace AutoLegalidad.Engine.Repair;

/// <summary>
/// Normalizacion de EV por contexto (plan 7, fase D): recorta cada estadistica al maximo del formato, reduce el exceso total
/// PROPORCIONALMENTE conservando la distribucion relativa y reparte el resto con desempate fijo por indice. Es idempotente: si los EV ya son validos
/// no toca nada. No reparte 252 en todas las estadisticas ni inventa un «build».
/// </summary>
public static class EvNormalizer
{
    public const int MaxTotalGen3Plus = EffortValues.Max510;

    /// <summary>Maximo por estadistica del formato y del total (Gen1/2: Stat Experience 65535 por estadistica y sin total).</summary>
    public static (int PerStat, int Total) Limits(PKM pk) => pk switch
    {
        // Let's Go (PB7): no se pueden ganar EV, solo valores de despertar (AV): el Core exige EV total 0 (Legality/Verifiers/AwakenedValueVerifier.cs:26-28, EffortShouldBeZero).
        PB7 => (0, 0),
        { Format: <= 2 } => (EffortValues.Max12, int.MaxValue),
        _ => (pk.MaxEV, EffortValues.Max510),
    };

    /// <summary>Normaliza un vector de EV (orden visual: PS, Ata, Def, AtE, DeE, Vel). Devuelve un vector nuevo.</summary>
    public static int[] Normalize(ReadOnlySpan<int> evs, int perStat, int total)
    {
        var v = new int[evs.Length];
        for (var i = 0; i < v.Length; i++)
            v[i] = Math.Clamp(evs[i], 0, perStat);

        long sum = 0;
        foreach (var x in v) sum += x;
        if (sum <= total)
            return v;

        // Escala proporcional (suelo) y reparto del resto por mayor parte fraccionaria; desempate por el menor indice.
        var scaled = new int[v.Length];
        var frac = new double[v.Length];
        long scaledSum = 0;
        for (var i = 0; i < v.Length; i++)
        {
            var exact = v[i] * (double)total / sum;
            scaled[i] = (int)Math.Floor(exact);
            frac[i] = exact - scaled[i];
            scaledSum += scaled[i];
        }
        var order = Enumerable.Range(0, v.Length).OrderByDescending(i => frac[i]).ThenBy(i => i).ToArray();
        var remaining = total - scaledSum;
        foreach (var i in order)
        {
            if (remaining <= 0) break;
            if (scaled[i] >= perStat || scaled[i] >= v[i]) continue;
            scaled[i]++;
            remaining--;
        }
        return scaled;
    }

    /// <summary>Aplica la normalizacion al Pokemon. Devuelve true si cambio algun EV.</summary>
    public static bool Apply(PKM pk)
    {
        var (per, total) = Limits(pk);
        var before = PkmFieldsAccess(pk);
        var after = Normalize(before, per, total);
        if (before.AsSpan().SequenceEqual(after))
            return false;
        pk.EV_HP = after[0]; pk.EV_ATK = after[1]; pk.EV_DEF = after[2];
        pk.EV_SPA = after[3]; pk.EV_SPD = after[4]; pk.EV_SPE = after[5];
        return true;
    }

    private static int[] PkmFieldsAccess(PKM pk) => Generation.PkmFieldsAccess.EVs(pk);

    /// <summary>
    /// Candidatos de EV para reparar, de mas a menos fiel al usuario. En Gen3/4 el Core solo admite EV por encima de 100 si el Pokemon gano EXP; sin EXP
    /// extra solo valen multiplos de 10 hasta 100 (vitaminas): segundo candidato, que conserva la forma relativa del reparto. El ultimo es todo a 0.
    /// </summary>
    public static IEnumerable<int[]> Candidates(PKM pk)
    {
        var (per, total) = Limits(pk);
        var first = Normalize(Generation.PkmFieldsAccess.EVs(pk), per, total);
        yield return first;
        if (pk.Format is 3 or 4)
        {
            var max = first.Max();
            var factor = max > 100 ? 100.0 / max : 1.0;
            var vit = first.Select(v => (int)(Math.Floor(v * factor / 10.0) * 10)).ToArray();
            yield return vit;
        }
        yield return new int[6];
    }

    public static void Set(PKM pk, int[] v)
    {
        pk.EV_HP = v[0]; pk.EV_ATK = v[1]; pk.EV_DEF = v[2]; pk.EV_SPA = v[3]; pk.EV_SPD = v[4]; pk.EV_SPE = v[5];
    }
}
