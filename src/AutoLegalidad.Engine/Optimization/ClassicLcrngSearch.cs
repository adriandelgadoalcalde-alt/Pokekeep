// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Engine.Generation;
using AutoLegalidad.Engine.SolutionCache;
using PKHeX.Core;

namespace AutoLegalidad.Engine.Optimization;

/// <summary>Resultado de una sub-estrategia de optimizacion, con la BASE del veredicto (ver <c>OptimizationReport.Basis</c>).</summary>
internal sealed record SubResult(PKM? Best, string Basis, bool Exhaustive, string? Limit, string Strategy);

/// <summary>
/// Busqueda de semilla de los metodos clasicos 1, 2 y 4 del LCRNG (Gen3/4). Es la version completa del requisito R-002: enumera los vectores de IV por
/// suma DESCENDENTE (6x31 primero), recupera TODAS las semillas que los producen con las rutinas de inversion del Core (<see cref="LCRNGReversal"/>), deriva
/// el PID de cada una segun el metodo del Pokemon y se queda solo con las que CONSERVAN naturaleza, sexo, variocolor y habilidad (y el tipo de Poder Oculto).
/// El primer candidato que el analisis oficial acepta (fotogramas de ranura/nivel/naturaleza incluidos) y que sigue siendo del MISMO metodo es el maximo del dominio:
/// todos los vectores con mas suma ya se descartaron. El generador por criterios del Core no comprueba variocolor ni habilidad en su busqueda por IV, por eso esta
/// busqueda propia es la que cubre esos rasgos.
/// </summary>
internal static class ClassicLcrngSearch
{
    private const int MaxVectors = 6_000_000;
    private static readonly TimeSpan MaxTime = TimeSpan.FromSeconds(9);

    public static SubResult? Run(OptimizeRequest r, PKM pk, LegalityAnalysis la, ref int attempts)
    {
        var type = la.Info.PIDIV.Type;
        // BACD (Gen3, regalos de evento): como el metodo 1 pero con las dos mitades del PID en orden inverso y SIN restriccion de semilla de origen (hito 3).
        // BACD_AX (regalos de Nueva York, Hito 3b): como BACD pero con el PID antivariocolor ((a ^ TID ^ b) << 16) | b y la primera llamada > 7 (CommonEvent3.GetAntishiny).
        if (type is not (PIDType.Method_1 or PIDType.Method_2 or PIDType.Method_4 or PIDType.BACD or PIDType.BACD_AX)) return null;
        if (pk.Format is not (3 or 4) || pk.Species == (ushort)Species.Unown) return null;
        if (pk.PersonalInfo is not IPersonalAbility12 abilities) return null;

        var cur = PkmFieldsAccess.IVs(pk);
        var curSum = cur.Sum();
        var knowsHp = StatOptimizer.KnowsHiddenPower(pk);
        var max = pk.MaxIV;
        var allowed = new int[6][];
        for (var i = 0; i < 6; i++)
            allowed[i] = knowsHp && (cur[i] & 1) == 0 ? IvEnumerator.Range(max - 1, 0, 2) : knowsHp ? IvEnumerator.Range(max, 1, 2) : IvEnumerator.Range(max);
        var relevance = StatPriority.MostRelevantFirst(pk);

        var nature = (uint)pk.Nature;
        var gender = pk.Gender;
        var ratio = pk.PersonalInfo.Gender;
        var id32 = pk.ID32;
        var wantShiny = pk.IsShiny;
        int abilityBit0 = abilities.Ability1, abilityBit1 = abilities.Ability2;

        var started = DateTime.UtcNow;
        var limitTime = started + MaxTime;

        // R-011 (ADR 0037): cache persistente de soluciones; se revalida con el Core igual que un candidato recien buscado.
        var cache = SolutionCacheHost.Current;
        string? cacheKey = null;
        if (cache.Enabled)
        {
            cacheKey = SolutionKey.ForLcrng(la.EncounterMatch, type, pk, knowsHp, cur, relevance);
            if (TryServeFromCache(cache, cacheKey, r, pk, type, allowed, curSum, Min(limitTime, r.DeadlineUtc)) is { } served) return served;
        }

        Span<uint> seeds = stackalloc uint[LCRNG.MaxCountSeedsIV];
        long vectors = 0;
        foreach (var v in IvEnumerator.BySumDescending(allowed, relevance, curSum))
        {
            if (++vectors > MaxVectors || ((vectors & 0x3FFF) == 0 && (r.Cancellation.IsCancellationRequested || DateTime.UtcNow >= limitTime || DateTime.UtcNow >= r.DeadlineUtc)))
            {
                RecordBound(cache, cacheKey, r, curSum, exhaustive: false, (ulong)vectors, started);
                return new SubResult(null, "BudgetExhausted", false, BudgetText(vectors), "LcrngSeedSearch");
            }

            var iv1 = (uint)(v[0] | (v[1] << 5) | (v[2] << 10));
            var iv2 = (uint)(v[5] | (v[3] << 5) | (v[4] << 10));
            var count = type == PIDType.Method_4 ? LCRNGReversalSkip.GetSeedsIVs(seeds, iv1 << 16, iv2 << 16) : LCRNGReversal.GetSeedsIVs(seeds, iv1 << 16, iv2 << 16);
            for (var k = 0; k < count; k++)
            {
                // Metodo 1: A B C D (IV en C, D); Metodo 4: A B C _ E (IV en C, E); Metodo 2: A B _ D E (IV en D, E).
                var s = type == PIDType.Method_2 ? LCRNG.Prev3(seeds[k]) : LCRNG.Prev2(seeds[k]);
                var a = LCRNG.Next16(ref s);
                var b = LCRNG.Next16(ref s);
                if (type == PIDType.BACD_AX && (a & ~0b111u) == 0) continue; // con a <= 7 el Core la salta (CommonEvent3.GetAntishiny: do ... while ((a16 & ~7) == 0))
                var pid = type == PIDType.BACD ? (a << 16) | b : type == PIDType.BACD_AX ? CommonEvent3.GetAntishiny(a, b, (uint)pk.TID16) : (b << 16) | a;
                if (pid % 25 != nature) continue;
                if (EntityGender.GetFromPIDAndRatio(pid, ratio) != gender) continue;
                if ((((pid & 1) == 0) ? abilityBit0 : abilityBit1) != pk.Ability) continue;
                if (ShinyUtil.GetIsShiny3(id32, pid) != wantShiny) continue;

                attempts++;
                var cand = Build(pk, pid, v);
                if (!StatOptimizer.QuickValid(cand, r.Dest, pk)) continue;
                if (StatOptimizer.Analyze(cand, r.Dest).Info.PIDIV.Type != type) continue; // mismo metodo
                RecordFound(cache, cacheKey, r, pid, v, (ulong)vectors, started);
                return new SubResult(cand, "Enumeration", true, null, "LcrngSeedSearch");
            }
        }
        RecordBound(cache, cacheKey, r, curSum, exhaustive: true, (ulong)vectors, started);
        return new SubResult(null, "Enumeration", true, null, "LcrngSeedSearch");
    }

    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;

    private static string BudgetText(long vectors)
        => $"Se agotó el presupuesto de búsqueda tras {vectors:N0} combinaciones de IV sin hallar una semilla del mismo método que conserve naturaleza, sexo, variocolor y habilidad.";

    /// <summary>Una entrada NO demostrada solo se sirve si costo al menos esta fraccion del tiempo que la solicitud actual concede.</summary>
    private const double PartialServeFraction = 0.9;

    private static SubResult? TryServeFromCache(ISolutionCache cache, string key, OptimizeRequest r, PKM pk, PIDType type, int[][] allowed, int curSum, DateTime limit)
    {
        var started = DateTime.UtcNow;
        if (!cache.TryGet(key, out var e) || e.Family != SolutionFamily.Lcrng) { cache.NoteMiss(); return null; }
        if (!e.Exhaustive && e.CostMs < (limit - started).TotalMilliseconds * PartialServeFraction) { cache.NoteMiss(); return null; }

        SubResult Serve(SubResult result)
        {
            cache.NoteHit();
            return result;
        }

        SubResult NoImprovement() => e.Exhaustive
            ? new SubResult(null, "Enumeration", true, null, "LcrngSeedSearch")
            : new SubResult(null, "BudgetExhausted", false, BudgetText((long)e.Scanned), "LcrngSeedSearch");

        if (e.IsBound)
        {
            if (curSum < e.Sum) { cache.NoteMiss(); return null; }
            return Serve(NoImprovement());
        }

        // Entrada exacta: (PID, vector de IV). Se comprueban los MISMOS filtros que el bucle real y, despues, el Core.
        if (e.Seeds.Length != 1 || e.Ivs is not { Length: 6 } v || e.Seeds[0] > uint.MaxValue) { cache.NoteRejected(); return null; }
        var pid = (uint)e.Seeds[0];
        for (var i = 0; i < 6; i++)
            if (Array.IndexOf(allowed[i], v[i]) < 0) { cache.NoteRejected(); return null; }
        if (v.Sum() != e.Sum) { cache.NoteRejected(); return null; }
        if (pid % 25 != (uint)pk.Nature
            || EntityGender.GetFromPIDAndRatio(pid, pk.PersonalInfo.Gender) != pk.Gender
            || pk.PersonalInfo is not IPersonalAbility12 ab || (((pid & 1) == 0) ? ab.Ability1 : ab.Ability2) != pk.Ability
            || ShinyUtil.GetIsShiny3(pk.ID32, pid) != pk.IsShiny) { cache.NoteRejected(); return null; }
        if (e.Sum <= curSum) return Serve(NoImprovement()); // la busqueda real no habria devuelto nada por encima de la suma actual

        var cand = Build(pk, pid, v);
        if (!StatOptimizer.QuickValid(cand, r.Dest, pk) || StatOptimizer.Analyze(cand, r.Dest).Info.PIDIV.Type != type) { cache.NoteRejected(); return null; }
        return Serve(new SubResult(cand, "Enumeration", true, null, "LcrngSeedSearch"));
    }

    private static void RecordFound(ISolutionCache cache, string? key, OptimizeRequest r, uint pid, int[] iv, ulong vectors, DateTime started)
    {
        if (key is null || !cache.Recording || r.Cancellation.IsCancellationRequested) return;
        cache.Put(key, new SolutionEntry(SolutionFamily.Lcrng, [pid], iv.Sum(), true, vectors, (long)(DateTime.UtcNow - started).TotalMilliseconds, (int[])iv.Clone()));
    }

    private static void RecordBound(ISolutionCache cache, string? key, OptimizeRequest r, int curSum, bool exhaustive, ulong vectors, DateTime started)
    {
        if (key is null || !cache.Recording || r.Cancellation.IsCancellationRequested) return;
        cache.Put(key, new SolutionEntry(SolutionFamily.Lcrng, [], curSum, exhaustive, vectors, (long)(DateTime.UtcNow - started).TotalMilliseconds));
    }

    private static PKM Build(PKM pk, uint pid, int[] iv)
    {
        var c = pk.Clone();
        c.PID = pid;
        if (c.EncryptionConstant != pid) c.EncryptionConstant = pid;
        c.IV_HP = iv[0]; c.IV_ATK = iv[1]; c.IV_DEF = iv[2]; c.IV_SPA = iv[3]; c.IV_SPD = iv[4]; c.IV_SPE = iv[5];
        c.RefreshAbility((int)(pid & 1));
        c.RefreshChecksum();
        if (c.PartyStatsPresent) c.SetStats(c.GetStats(c.PersonalInfo));
        return c;
    }
}
