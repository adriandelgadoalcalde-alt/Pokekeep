// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;

namespace AutoLegalidad.Engine.Optimization;

/// <summary>
/// Orden de prioridad de las estadisticas para el desempate de R-001: «por stat final y no por el IV crudo». Cada estadistica pesa
/// (base de la especie x multiplicador de naturaleza); las menos relevantes ceden primero cuando no caben los seis 31. Ultimo desempate: indice.
/// Indices en orden VISUAL (PS, Ata, Def, AtE, DeE, Vel).
/// </summary>
public static class StatPriority
{
    // Naturaleza: Core la codifica como incremento = n/5 y decremento = n%5 sobre (Ata, Def, Vel, AtE, DeE).
    private static readonly int[] NatureAxisToVisual = [1, 2, 5, 3, 4];

    /// <summary>Indices visuales ordenados de MAS a MENOS relevantes.</summary>
    public static int[] MostRelevantFirst(PKM pk)
    {
        var pi = pk.PersonalInfo;
        // GetBaseStatValue usa el orden interno (PS, Ata, Def, Vel, AtE, DeE).
        double[] w = new double[6];
        int[] internalToVisual = [0, 1, 2, 5, 3, 4];
        for (var i = 0; i < 6; i++) w[internalToVisual[i]] = pi.GetBaseStatValue(i);
        var n = (int)pk.Nature;
        if (pk.Format >= 3 && (uint)n < 25 && n / 5 != n % 5)
        {
            w[NatureAxisToVisual[n / 5]] *= 1.1;
            w[NatureAxisToVisual[n % 5]] *= 0.9;
        }
        return Enumerable.Range(0, 6).OrderByDescending(i => w[i]).ThenBy(i => i).ToArray();
    }
}

/// <summary>
/// Enumera vectores de IV en orden de SUMA descendente (6x31 primero), de forma determinista. Para una misma suma, el deficit se carga primero sobre
/// las estadisticas menos relevantes. Cada estadistica admite una lista de valores permitidos (descendente), p. ej. {31,30,29,...} o, para conservar el
/// tipo de Poder Oculto, solo los de la misma paridad.
/// </summary>
public static class IvEnumerator
{
    /// <param name="allowed">Valores permitidos por estadistica (orden visual), cada lista en orden DESCENDENTE.</param>
    /// <param name="relevance">Indices visuales de mas a menos relevantes.</param>
    /// <param name="minSumExclusive">Solo se devuelven vectores con suma estrictamente mayor.</param>
    public static IEnumerable<int[]> BySumDescending(IReadOnlyList<int[]> allowed, int[] relevance, int minSumExclusive = -1)
    {
        var maxRank = allowed.Select(a => a.Length - 1).ToArray();
        var totalMaxRank = maxRank.Sum();
        var bestSum = allowed.Sum(a => a[0]);
        // Orden de reparto: de la MENOS relevante a la mas relevante.
        var order = relevance.Reverse().ToArray();
        for (var rankTotal = 0; rankTotal <= totalMaxRank; rankTotal++)
        {
            var ranks = new int[6];
            foreach (var v in Assign(allowed, order, maxRank, ranks, 0, rankTotal))
            {
                if (v.Sum() <= minSumExclusive) continue;
                yield return v;
            }
            // Los vectores de este rankTotal pueden tener sumas distintas (pasos desiguales): se emiten por rankTotal creciente, que equivale
            // a suma decreciente cuando los pasos son iguales (caso normal: paso 1 o paso 2 en todas).
            if (bestSum - rankTotal * StepOf(allowed) <= minSumExclusive) yield break;
        }
    }

    private static int StepOf(IReadOnlyList<int[]> allowed)
    {
        var steps = allowed.Where(a => a.Length > 1).Select(a => a[0] - a[1]).Distinct().ToArray();
        return steps.Length == 0 ? 1 : steps.Min();
    }

    private static IEnumerable<int[]> Assign(IReadOnlyList<int[]> allowed, int[] order, int[] maxRank, int[] ranks, int pos, int remaining)
    {
        if (pos == order.Length)
        {
            if (remaining != 0) yield break;
            var v = new int[6];
            for (var i = 0; i < 6; i++) v[i] = allowed[i][ranks[i]];
            yield return v;
            yield break;
        }
        var stat = order[pos];
        // Cargar el maximo posible primero en las menos relevantes.
        var hi = Math.Min(remaining, maxRank[stat]);
        var restCapacity = 0;
        for (var p = pos + 1; p < order.Length; p++) restCapacity += maxRank[order[p]];
        for (var r = hi; r >= 0; r--)
        {
            if (remaining - r > restCapacity) break;
            ranks[stat] = r;
            foreach (var x in Assign(allowed, order, maxRank, ranks, pos + 1, remaining - r))
                yield return x;
        }
        ranks[stat] = 0;
    }

    public static int[] Range(int max, int min = 0, int step = 1)
    {
        var l = new List<int>();
        for (var v = max; v >= min; v -= step) l.Add(v);
        return [.. l];
    }
}
