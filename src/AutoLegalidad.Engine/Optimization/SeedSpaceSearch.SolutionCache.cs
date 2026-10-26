// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Engine.Generation;
using AutoLegalidad.Engine.SolutionCache;
using PKHeX.Core;

namespace AutoLegalidad.Engine.Optimization;

/// <summary>
/// Ganchos de la cache persistente de soluciones (R-011, ADR 0037) en <see cref="SeedSpaceSearch.Run"/>. Consultar ANTES de buscar y registrar al final; en un acierto se reconstruye el
/// candidato con la semilla guardada y se le aplican EXACTAMENTE los mismos filtros de aceptacion que al bucle real (<see cref="PassesFilters"/>) mas la revalidacion del Core; si algo falla
/// se cae a la busqueda normal (<c>Rejected++</c>). Una entrada nunca se acepta sin pasar por el Core.
/// </summary>
internal static partial class SeedSpaceSearch
{
    /// <summary>Una entrada NO demostrada solo se sirve si costo al menos esta fraccion del tiempo que la solicitud actual concede (si no, se busca otra vez y se mejora la entrada).</summary>
    private const double PartialServeFraction = 0.9;

    /// <summary>Mismos filtros de aceptacion que <see cref="ScanRange"/>/<c>SolveForTarget</c> (naturaleza, sexo, habilidad, variocolor, EC de Dudunsparce/Maushold y tipo de Poder Oculto).</summary>
    private static bool PassesFilters(PKM scratch, PKM original, bool knowsHp)
    {
        if (scratch.Nature != original.Nature || scratch.Gender != original.Gender || scratch.Ability != original.Ability || scratch.IsShiny != original.IsShiny) return false;
        if (EvolutionEc.Applies(original.Species) && original.Format >= 6 && EvolutionEc.IsRare(scratch.EncryptionConstant) != EvolutionEc.IsRare(original.EncryptionConstant)) return false;
        if (knowsHp && scratch.HPType != original.HPType) return false;
        return true;
    }

    private static int SumOf(PKM p) => p.IV_HP + p.IV_ATK + p.IV_DEF + p.IV_SPA + p.IV_SPD + p.IV_SPE;

    /// <summary>Resultado de la busqueda cuando no hay mejora (identico al del final de la busqueda real).</summary>
    private static SubResult NoImprovement(bool exhaustive, ulong scanned, string strategy)
        => exhaustive
            ? new SubResult(null, "Enumeration", true, "Recorridas las 2^32 semillas posibles de este encuentro: ninguna mejora los IV conservando naturaleza, sexo, variocolor y habilidad.", strategy)
            : new SubResult(null, "BudgetExhausted", false, $"Se recorrieron {scanned:N0} de las 2^32 semillas sin hallar una mejora que conserve naturaleza, sexo, variocolor y habilidad (búsqueda acotada por tiempo).", strategy);

    /// <summary>Texto de limite de un resultado con mejora (identico al de la busqueda real).</summary>
    private static string? LimitText(int sum, int target, bool exhaustive, ulong scanned)
        => sum >= target ? null : exhaustive
            ? $"Recorridas las 2^32 semillas del encuentro: la mejor combinación legal que conserva naturaleza, sexo, variocolor y habilidad suma {sum}."
            : $"Mejor combinación hallada en {scanned:N0} semillas ({(scanned * 100.0 / Domain):0.#} % del dominio de 32 bits); puede existir una mejor.";

    /// <summary>
    /// Reordena el grupo de empate guardado (semillas de la MISMA suma) con el orden de la busqueda para el candidato actual: relevancia de sus stats y, por ultimo, la semilla menor. <c>null</c> si alguna semilla
    /// no se reproduce, no pasa los filtros del candidato o no suma lo guardado (entrada corrupta: se busca de cero).
    /// </summary>
    private static ulong[]? OrderTieGroup(ISeedModel model, PKM pk, ulong[] seeds, int expectedSum, bool knowsHp, int[] relevance)
    {
        var items = new List<Hit>(seeds.Length);
        foreach (var seed in seeds)
        {
            var scratch = pk.Clone();
            if (!model.Apply(scratch, seed) || !PassesFilters(scratch, pk, knowsHp)) return null;
            var sum = SumOf(scratch);
            if (sum != expectedSum) return null;
            items.Add(new Hit(seed, sum, PkmFieldsAccess.IVs(scratch), scratch));
        }
        items.Sort((a, b) => Compare(a, b, relevance));
        return [.. items.Select(h => h.Seed)];
    }

    /// <summary>Intenta responder desde la cache. <c>null</c> = no hay respuesta valida (ausente, parcial insuficiente o rechazada): el llamador busca como siempre.</summary>
    private static SubResult? TryServeFromCache(ISolutionCache cache, string key, OptimizeRequest r, PKM pk, ISeedModel model, int curSum, int target, bool knowsHp, PIDType type, DateTime limit, int[] relevance)
    {
        var started = DateTime.UtcNow;
        if (!cache.TryGet(key, out var e) || e.Family != SolutionFamily.Seed) { cache.NoteMiss(); return null; }

        // Una cota/solucion sin demostrar vale solo para quien tiene un presupuesto no mayor que la busqueda que la produjo.
        var proven = e.Exhaustive || (!e.IsBound && e.Sum >= target);
        if (!proven && e.CostMs < (limit - started).TotalMilliseconds * PartialServeFraction) { cache.NoteMiss(); return null; }

        SubResult Serve(SubResult result)
        {
            Last = new Stats(0, e.Exhaustive, (DateTime.UtcNow - started).TotalSeconds, 0, FromCache: true);
            cache.NoteHit();
            return result;
        }

        if (e.IsBound)
        {
            if (curSum < e.Sum) { cache.NoteMiss(); return null; } // la cota no cubre esta suma actual
            return Serve(NoImprovement(e.Exhaustive, e.Scanned, model.Name));
        }
        if (e.Sum > target) { cache.NoteRejected(); return null; } // imposible: entrada corrupta

        // ADR 0042: sin la relevancia en la clave la entrada trae el GRUPO DE EMPATE completo de la suma maxima; se reordena con la relevancia de ESTE candidato (suma, relevancia, semilla menor: el orden de la busqueda).
        var seeds = e.Seeds;
        if (!model.RelevanceInKey && seeds.Length > 1)
        {
            var ordered = OrderTieGroup(model, pk, seeds, e.Sum, knowsHp, relevance);
            if (ordered is null) { cache.NoteRejected(); return null; }
            seeds = ordered;
        }

        var coreRejected = 0;
        for (var i = 0; i < seeds.Length; i++)
        {
            var scratch = pk.Clone();
            if (!model.Apply(scratch, seeds[i]) || !PassesFilters(scratch, pk, knowsHp)) { cache.NoteRejected(); return null; }
            var sum = SumOf(scratch);
            if (i == 0 && sum != e.Sum) { cache.NoteRejected(); return null; } // la suma guardada no es la de la semilla
            if (sum <= curSum) break; // los siguientes son peores: nada que mejorar
            var cand = Finish(scratch);
            if (!StatOptimizer.QuickValid(cand, r.Dest, pk) || !PidivFamily.Same(StatOptimizer.Analyze(cand, r.Dest).Info.PIDIV.Type, type)) { coreRejected++; continue; }
            return Serve(new SubResult(cand, e.Exhaustive || sum >= target ? "Enumeration" : "BudgetExhausted", e.Exhaustive || sum >= target, LimitText(sum, target, e.Exhaustive, e.Scanned), model.Name));
        }
        if (coreRejected > 0) { cache.NoteRejected(); return null; } // el Core no acepta ninguna: la busqueda real da su propio veredicto
        return Serve(NoImprovement(e.Exhaustive, e.Scanned, model.Name));
    }

    /// <summary>
    /// Registra el resultado de una busqueda REAL (solo en modo registro y nunca si la solicitud se cancelo). Con la relevancia en la clave se guardan las 3 mejores semillas; sin ella (ADR 0042) el grupo de
    /// empate completo de la suma maxima (hasta <see cref="TopK"/>), para poder reordenarlo con la relevancia de cada candidato. Si ese grupo se pudo truncar en una busqueda exhaustiva (la lista entera empata en
    /// la suma maxima) no se registra: el orden por otra naturaleza no estaria garantizado.
    /// </summary>
    private static void RecordToCache(ISolutionCache cache, string? key, OptimizeRequest r, ISeedModel model, List<Hit> hits, int curSum, int target, bool exhaustive, ulong scanned, DateTime start, bool hasValid)
    {
        if (key is null || !cache.Recording || r.Cancellation.IsCancellationRequested) return;
        if (!exhaustive && scanned == 0 && !(hasValid && hits.Count > 0 && hits[0].Sum > 0)) return; // nada recorrido y nada demostrado: no es informacion
        var cost = (long)(DateTime.UtcNow - start).TotalMilliseconds;
        if (hasValid && hits.Count > 0)
        {
            ulong[] seeds;
            if (model.RelevanceInKey) seeds = hits.Take(3).Select(h => h.Seed).ToArray();
            else
            {
                var top = hits[0].Sum;
                var group = hits.TakeWhile(h => h.Sum == top).ToList();
                // Con la suma al maximo teorico todos los vectores de IV son el mismo: la relevancia no cambia nada y basta con lo hallado.
                if (exhaustive && top < target && group.Count >= TopK) return;
                seeds = group.Select(h => h.Seed).ToArray();
            }
            cache.Put(key, new SolutionEntry(SolutionFamily.Seed, seeds, hits[0].Sum, exhaustive, scanned, cost));
        }
        else if (exhaustive || scanned > 0)
            cache.Put(key, new SolutionEntry(SolutionFamily.Seed, [], Math.Max(curSum, hits.Count > 0 ? hits[0].Sum : curSum), exhaustive, scanned, cost));
    }
}
