// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Engine.Generation;
using AutoLegalidad.Engine.SolutionCache;
using PKHeX.Core;

namespace AutoLegalidad.Engine.Optimization;

/// <summary>
/// Modelo de semilla cuya busqueda se puede resolver UNA vez por ENCUENTRO para todos los candidatos posibles (ADR 0041): la semilla fija los rasgos que el candidato conserva (naturaleza, sexo,
/// habilidad, Teratipo...), asi que un unico barrido de las 2^32 semillas, agrupando por esos rasgos («casillas»), contiene la respuesta de cualquier candidato del mismo encuentro.
/// </summary>
internal interface IEncounterUniverseModel : ISeedModel
{
    /// <summary>Identidad del ENCUENTRO: como <see cref="ISeedModel.CacheIdentity"/> pero sin ningun rasgo del candidato.</summary>
    string UniverseIdentity { get; }

    /// <summary>Modelo equivalente que NO filtra por los rasgos del candidato (los deja en la casilla); es el que recorre el barrido universal.</summary>
    ISeedModel UniverseModel { get; }

    /// <summary>Casilla en la que cae una semilla ya aplicada a <paramref name="scratch"/> (<see cref="UniverseBuckets.Pack"/>).</summary>
    uint BucketOf(PKM scratch, ulong seed);

    /// <summary>Casilla de los rasgos ACTUALES del candidato.</summary>
    uint BucketOfCandidate(PKM pk);
}

/// <summary>Empaquetado de la casilla de rasgos: naturaleza (5 bits), sexo (2), habilidad (9), Teratipo (7) y clase de EC de Dudunsparce/Maushold (1).</summary>
internal static class UniverseBuckets
{
    public static uint Pack(int nature, int gender, int ability, int tera, int ecClass)
        => (uint)(nature & 0x1F) | (uint)(gender & 0x3) << 5 | (uint)(ability & 0x1FF) << 7 | (uint)(tera & 0x7F) << 16 | (uint)(ecClass & 1) << 23;

    /// <summary>1 si la especie tiene regla de EC (Dudunsparce/Maushold) y la EC es de la clase «rara»; 0 en cualquier otro caso.</summary>
    public static int EcClass(PKM pk, uint ec) => EvolutionEc.Applies(pk.Species) && pk.Format >= 6 && EvolutionEc.IsRare(ec) ? 1 : 0;
}

internal static partial class SeedSpaceSearch
{
    /// <summary>Plazo minimo que debe quedar para que el modo registro intente el barrido universal.</summary>
    private static readonly TimeSpan UniverseMinBudget = TimeSpan.FromSeconds(10);

    private readonly record struct UHit(ulong Seed, int Sum, int[] Ivs);

    /// <summary>Resultado de un barrido universal: por casilla, la lista EXACTA (prefijo del orden del motor) de las mejores semillas.</summary>
    private sealed record UniverseTable(Dictionary<uint, UniversePayload.Bucket> Buckets, ulong Scanned, long CostMs);

    /// <summary>La busqueda universal solo se usa para Pokemon no variocolor y sin Poder Oculto conocido (esos casos dependen del entrenador o fijan el tipo y siguen el camino por candidato).</summary>
    private static bool UniverseEligible(IEncounterUniverseModel model, PKM pk, bool knowsHp)
        => OptimizerSwitches.EncounterUniverse && !knowsHp && !pk.IsShiny && !model.DependsOnTrainerId;

    private static int CompareHits(UHit a, UHit b, int[] relevance)
    {
        if (a.Sum != b.Sum) return b.Sum.CompareTo(a.Sum);
        foreach (var i in relevance)
            if (a.Ivs[i] != b.Ivs[i]) return b.Ivs[i].CompareTo(a.Ivs[i]);
        return a.Seed.CompareTo(b.Seed);
    }

    private static void AddHit(List<UHit> list, UHit hit, int[] relevance)
    {
        var at = list.Count;
        while (at > 0 && CompareHits(hit, list[at - 1], relevance) < 0) at--;
        if (at >= UniversePayload.MaxSeedsPerBucket) return;
        list.Insert(at, hit);
        if (list.Count > UniversePayload.MaxSeedsPerBucket) list.RemoveAt(list.Count - 1);
    }

    /// <summary>
    /// Barrido UNICO de las 2^32 semillas del encuentro (modelo universal) que agrupa por casilla de rasgos y guarda, de cada una, las mejores semillas con el MISMO orden que la busqueda por candidato
    /// (suma, relevancia de los stats de la naturaleza de la casilla, semilla menor). Poda con una cota <c>T</c> que solo sube: se conservan las semillas de suma &gt;= T; una casilla vale solo si su mejor suma
    /// es &gt;= la mayor T usada (asi ninguna semilla podada pudo superarla). Devuelve <c>null</c> si no se completo (plazo o cancelacion): una tabla a medias no demuestra nada.
    /// </summary>
    private static UniverseTable? ScanUniverse(IEncounterUniverseModel model, PKM pk, DateTime limit, CancellationToken ct)
        => ScanUniverseGroup([model], [pk], null, limit, ct)?[0];

    /// <summary>
    /// Barrido UNICO de las 2^32 semillas para VARIOS encuentros a la vez (ADR 0042, solo precalculo): el <paramref name="selector"/> dice, para cada semilla, que modelos (indices de <paramref name="models"/>) pueden
    /// producirla, de modo que la eleccion de incursion se calcula una vez por semilla y no una por plantilla. Cada modelo lleva SU propio estado (cota, casillas, relevancia), con el mismo recorrido por lotes que
    /// el barrido de un solo encuentro: el resultado de cada uno es identico al de <see cref="ScanUniverse"/> (lo comprueba una prueba). Sin selector, el unico modelo recibe todas las semillas.
    /// </summary>
    private static UniverseTable?[]? ScanUniverseGroup(IEncounterUniverseModel[] models, PKM[] pks, UniverseSelector? selector, DateTime limit, CancellationToken ct)
    {
        var start = DateTime.UtcNow;
        var k = models.Length;
        var threads = Threads;
        var universals = models.Select(m => m.UniverseModel).ToArray();
        var ivKey = universals.Select(u => u.IvStreamKeyForSelected).ToArray();
        var shiny = pks.Select(p => p.IsShiny).ToArray();
        var global = Enumerable.Range(0, k).Select(_ => new Dictionary<uint, List<UHit>>()).ToArray();
        var relevanceByNature = Enumerable.Range(0, k).Select(_ => new int[25][]).ToArray();
        var floor = Enumerable.Repeat(1, k).ToArray(); // las semillas de suma 0 son «esta semilla no es de este encuentro» (FastIvSumAbove devuelve 0)
        var maxFloorUsed = Enumerable.Repeat(1, k).ToArray();

        for (ulong batch = 0; batch < Domain; batch += BatchSize)
        {
            if (ct.IsCancellationRequested || DateTime.UtcNow >= limit) return null;
            var local = new Dictionary<uint, List<UHit>>?[threads][];
            var finished = new bool[threads];
            var slice = BatchSize / (ulong)threads;
            var t0 = (int[])floor.Clone();
            for (var i = 0; i < k; i++) maxFloorUsed[i] = Math.Max(maxFloorUsed[i], t0[i]);
            Parallel.For(0, threads, new ParallelOptions { MaxDegreeOfParallelism = threads, CancellationToken = CancellationToken.None }, t =>
            {
                var from = batch + slice * (ulong)t;
                var to = t == threads - 1 ? batch + BatchSize : from + slice;
                var scratch = new PKM?[k];
                var mine = new Dictionary<uint, List<UHit>>?[k];
                var rel = Enumerable.Range(0, k).Select(_ => new int[25][]).ToArray();
                Span<int> owners = stackalloc int[UniverseSelector.MaxOwners];
                Span<ulong> stamp = stackalloc ulong[8];
                Span<int> shared = stackalloc int[8];
                var ok = true;
                for (var s = from; s < to; s++)
                {
                    if ((s & 0x3FFF) == 0 && (ct.IsCancellationRequested || DateTime.UtcNow >= limit)) { ok = false; break; }
                    var n = 1;
                    if (selector is not null)
                    {
                        n = selector.Select((uint)s, owners);
                        if (n == 0) continue;
                    }
                    for (var j = 0; j < n; j++)
                    {
                        var i = selector is null ? 0 : owners[j];
                        var universal = universals[i];
                        int fast;
                        if (selector is null) fast = universal.FastIvSumAbove(s, t0[i] - 1);
                        else if (ivKey[i] is >= 0 and < 8)
                        {
                            // Misma suma de IV para todos los modelos con los mismos IV perfectos: un solo flujo por semilla y clave.
                            var ik = ivKey[i];
                            if (stamp[ik] != s + 1) { shared[ik] = universal.FastIvSumForSelected(s); stamp[ik] = s + 1; }
                            fast = shared[ik] < t0[i] ? 0 : shared[ik];
                        }
                        else fast = universal.FastIvSumAboveForSelected(s, t0[i] - 1);
                        if (fast >= 0 && fast < t0[i]) continue;
                        var sc = scratch[i] ??= pks[i].Clone();
                        if (!universal.Apply(sc, s)) continue;
                        if (sc.IsShiny != shiny[i]) continue;
                        var sum = SumOf(sc);
                        if (sum < t0[i]) continue;
                        var key = models[i].BucketOf(sc, s);
                        var nature = (int)sc.Nature;
                        var r = (uint)nature < 25 ? rel[i][nature] ??= StatPriority.MostRelevantFirst(sc) : StatPriority.MostRelevantFirst(sc);
                        var dict = mine[i] ??= new Dictionary<uint, List<UHit>>();
                        if (!dict.TryGetValue(key, out var list)) dict[key] = list = new List<UHit>(UniversePayload.MaxSeedsPerBucket + 1);
                        AddHit(list, new UHit(s, sum, PkmFieldsAccess.IVs(sc)), r);
                    }
                }
                local[t] = mine;
                finished[t] = ok;
            });
            if (finished.Any(f => !f)) return null;

            for (var i = 0; i < k; i++)
            {
                foreach (var mine in local)
                {
                    if (mine?[i] is not { } dict) continue;
                    foreach (var (key, hits) in dict)
                    {
                        if (!global[i].TryGetValue(key, out var list)) global[i][key] = list = new List<UHit>(UniversePayload.MaxSeedsPerBucket + 1);
                        foreach (var h in hits) AddHit(list, h, RelevanceForBucket(pks[i], key, relevanceByNature[i]));
                    }
                }
                // La cota sube a la mejor suma de la peor casilla poblada: lo que quede por debajo ya no puede ser la mejor de ninguna casilla que se haya visto.
                if (global[i].Count > 0) floor[i] = Math.Max(floor[i], global[i].Values.Min(l => l[0].Sum));
            }
        }

        var cost = (long)(DateTime.UtcNow - start).TotalMilliseconds;
        var tables = new UniverseTable?[k];
        for (var i = 0; i < k; i++)
        {
            var buckets = new Dictionary<uint, UniversePayload.Bucket>();
            foreach (var (key, list) in global[i])
            {
                if (list.Count == 0 || list[0].Sum < maxFloorUsed[i]) continue; // casilla no demostrada: se omite (el motor busca por candidato)
                buckets[key] = new UniversePayload.Bucket(key, list[0].Sum, [.. list.Select(h => (uint)h.Seed)]);
            }
            tables[i] = new UniverseTable(buckets, Domain, cost);
        }
        return tables;
    }

    /// <summary>Relevancia de los stats para la casilla <paramref name="key"/> (la naturaleza sale de ella): la misma que usa la busqueda por candidato (<see cref="StatPriority.MostRelevantFirst"/>).</summary>
    private static int[] RelevanceForBucket(PKM pk, uint key, int[][] byNature)
    {
        var nature = (int)(key & 0x1F);
        if ((uint)nature >= 25) return StatPriority.MostRelevantFirst(pk);
        if (byNature[nature] is { } known) return known;
        var probe = pk.Clone();
        probe.Nature = (Nature)nature;
        return byNature[nature] = StatPriority.MostRelevantFirst(probe);
    }

    /// <summary>
    /// Responde desde una tabla universal (leida de la cache o recien barrida). <c>null</c> = la casilla del candidato no esta, o nada de lo guardado supera la revalidacion con el Core: el llamador cae a
    /// la busqueda por candidato. La respuesta es EXACTAMENTE la que daria esa busqueda (misma semilla, mismo texto).
    /// </summary>
    private static SubResult? ServeFromUniverse(ISolutionCache cache, Dictionary<uint, UniversePayload.Bucket> buckets, IEncounterUniverseModel model, OptimizeRequest r, PKM pk, int curSum, int target, PIDType type, DateTime started, bool countHit)
    {
        if (!buckets.TryGetValue(model.BucketOfCandidate(pk), out var bucket)) { cache.NoteMiss(); return null; }

        SubResult Serve(SubResult result)
        {
            Last = new Stats(countHit ? 0UL : Domain, true, (DateTime.UtcNow - started).TotalSeconds, 0, FromCache: countHit);
            if (countHit) cache.NoteHit(); // una tabla recien barrida no es un acierto de la cache
            return result;
        }

        if (bucket.Sum <= curSum) return Serve(NoImprovement(true, Domain, model.Name)); // ninguna semilla del encuentro con esos rasgos mejora la suma actual
        if (bucket.Sum > target) { cache.NoteRejected(); return null; } // imposible: entrada corrupta

        var coreRejected = 0;
        var seedsChecked = 0;
        for (var i = 0; i < bucket.Seeds.Length; i++)
        {
            var scratch = pk.Clone();
            if (!model.Apply(scratch, bucket.Seeds[i])) { cache.NoteRejected(); return null; }
            // Solo el variocolor puede variar entre el entrenador sintetico del barrido y el real: esa semilla se salta, como la saltaria la busqueda (1 de cada 4096).
            if (scratch.IsShiny != pk.IsShiny) continue;
            if (scratch.Nature != pk.Nature || scratch.Gender != pk.Gender || scratch.Ability != pk.Ability) { cache.NoteRejected(); return null; }
            if (EvolutionEc.Applies(pk.Species) && pk.Format >= 6 && EvolutionEc.IsRare(scratch.EncryptionConstant) != EvolutionEc.IsRare(pk.EncryptionConstant)) { cache.NoteRejected(); return null; }
            var sum = SumOf(scratch);
            if (i == 0 && sum != bucket.Sum) { cache.NoteRejected(); return null; } // la suma guardada no es la de la primera semilla
            seedsChecked++;
            if (sum <= curSum) break; // los siguientes son peores: nada que mejorar
            var cand = Finish(scratch);
            if (!StatOptimizer.QuickValid(cand, r.Dest, pk) || !PidivFamily.Same(StatOptimizer.Analyze(cand, r.Dest).Info.PIDIV.Type, type)) { coreRejected++; continue; }
            return Serve(new SubResult(cand, "Enumeration", true, LimitText(sum, target, true, Domain), model.Name));
        }
        if (coreRejected > 0 || seedsChecked == 0) { cache.NoteRejected(); return null; } // el Core no acepta ninguna (o todas eran variocolor para este entrenador): la busqueda real da su veredicto
        return Serve(NoImprovement(true, Domain, model.Name));
    }

    /// <summary>
    /// Camino universal de <see cref="Run"/> (ADR 0041). Primero la tabla del encuentro en la cache; si falta y la cache esta en modo REGISTRO, UN barrido que la crea (y registra todas las casillas) y se responde de
    /// ella. <c>null</c> = que busque el camino por candidato.
    /// </summary>
    private static SubResult? TryUniverse(ISolutionCache cache, IEncounterUniverseModel model, OptimizeRequest r, PKM pk, int curSum, int target, PIDType type, DateTime limit)
    {
        var started = DateTime.UtcNow;
        var key = SolutionKey.ForUniverse(model, pk);
        if (cache.TryGet(key, out var e) && e.Family == SolutionFamily.EncounterUniverse && e.Blob is not null)
        {
            var table = UniversePayload.Decode(e.Blob);
            if (table is null) { cache.NoteRejected(); return null; }
            if (ServeFromUniverse(cache, table, model, r, pk, curSum, target, type, started, countHit: true) is { } served) return served;
            if (!cache.Recording) return null;
        }
        else if (!cache.Recording) { cache.NoteMiss(); return null; }

        // El barrido cuesta como una busqueda completa (segundos): con un plazo corto no se intenta (se busca por candidato como siempre) y nunca se hace sin el modo registro (el producto es de solo lectura).
        if (limit - DateTime.UtcNow < UniverseMinBudget) return null;
        var scanned = ScanUniverse(model, pk, limit, r.Cancellation);
        if (scanned is null || r.Cancellation.IsCancellationRequested) return null;
        if (scanned.Buckets.Count > 0)
            cache.Put(key, new SolutionEntry(SolutionFamily.EncounterUniverse, [], scanned.Buckets.Values.Max(b => b.Sum), true, scanned.Scanned, scanned.CostMs, Blob: UniversePayload.Encode(scanned.Buckets.Values)));
        return ServeFromUniverse(cache, scanned.Buckets, model, r, pk, curSum, target, type, started, countHit: false);
    }

    /// <summary>Resultado del precalculo de la tabla universal de UN encuentro (<see cref="PrecomputeUniverse"/>).</summary>
    internal enum PrecomputeOutcome { Recorded, AlreadyPresent, NotEligible, Incomplete, NoBuckets }

    /// <summary>
    /// Precalculo (solo modo REGISTRO, ADR 0042): barre las 2^32 semillas del encuentro de <paramref name="pk"/> y guarda la tabla universal con la MISMA clave que usaria <see cref="TryUniverse"/> para un candidato
    /// de esa especie y forma. No responde a ninguna solicitud: es la parte del barrido de <see cref="TryUniverse"/> sin la consulta. Si la tabla ya esta, no la recalcula.
    /// </summary>
    internal static (PrecomputeOutcome Outcome, int Buckets, int Bytes, long Ms) PrecomputeUniverse(ISolutionCache cache, IEncounterUniverseModel model, PKM pk, DateTime limit, CancellationToken ct)
    {
        if (!cache.Enabled || !cache.Recording) throw new InvalidOperationException("El precalculo universal exige una cache en modo registro.");
        if (!UniverseEligible(model, pk, knowsHp: false)) return (PrecomputeOutcome.NotEligible, 0, 0, 0);
        var key = SolutionKey.ForUniverse(model, pk);
        if (cache.TryGet(key, out var existing) && existing.Family == SolutionFamily.EncounterUniverse && existing.Blob is { } blob)
            return (PrecomputeOutcome.AlreadyPresent, UniversePayload.Decode(blob)?.Count ?? 0, blob.Length, 0);
        var scanned = ScanUniverse(model, pk, limit, ct);
        if (scanned is null) return (PrecomputeOutcome.Incomplete, 0, 0, 0);
        if (scanned.Buckets.Count == 0) return (PrecomputeOutcome.NoBuckets, 0, 0, scanned.CostMs);
        var bytes = UniversePayload.Encode(scanned.Buckets.Values);
        cache.Put(key, new SolutionEntry(SolutionFamily.EncounterUniverse, [], scanned.Buckets.Values.Max(b => b.Sum), true, scanned.Scanned, scanned.CostMs, Blob: bytes));
        return (PrecomputeOutcome.Recorded, scanned.Buckets.Count, bytes.Length, scanned.CostMs);
    }

    /// <summary>
    /// Precalculo (solo modo REGISTRO, ADR 0042) de VARIAS plantillas con UN barrido: cada modelo guarda su tabla universal con la MISMA clave que usaria <see cref="TryUniverse"/> para un candidato de su especie y
    /// forma (<paramref name="selector"/> elige que modelos pueden producir cada semilla; <c>null</c> = un unico modelo). No recalcula las tablas que ya estan; no es una solicitud, no responde a nadie.
    /// </summary>
    internal static (PrecomputeOutcome Outcome, int Buckets, int Bytes, long Ms)[] PrecomputeUniverseGroup(ISolutionCache cache, IEncounterUniverseModel[] models, PKM[] pks, UniverseSelector? selector, DateTime limit, CancellationToken ct)
    {
        if (!cache.Enabled || !cache.Recording) throw new InvalidOperationException("El precalculo universal exige una cache en modo registro.");
        var results = new (PrecomputeOutcome, int, int, long)[models.Length];
        var keys = new string?[models.Length];
        var pending = 0;
        for (var i = 0; i < models.Length; i++)
        {
            if (!UniverseEligible(models[i], pks[i], knowsHp: false)) { results[i] = (PrecomputeOutcome.NotEligible, 0, 0, 0); continue; }
            keys[i] = SolutionKey.ForUniverse(models[i], pks[i]);
            if (cache.TryGet(keys[i]!, out var existing) && existing.Family == SolutionFamily.EncounterUniverse && existing.Blob is { } blob)
            {
                results[i] = (PrecomputeOutcome.AlreadyPresent, UniversePayload.Decode(blob)?.Count ?? 0, blob.Length, 0);
                keys[i] = null;
                continue;
            }
            pending++;
        }
        if (pending == 0) return results;

        var tables = ScanUniverseGroup(models, pks, selector, limit, ct);
        for (var i = 0; i < models.Length; i++)
        {
            if (keys[i] is null) continue;
            if (tables is null) { results[i] = (PrecomputeOutcome.Incomplete, 0, 0, 0); continue; }
            var table = tables[i]!;
            if (table.Buckets.Count == 0) { results[i] = (PrecomputeOutcome.NoBuckets, 0, 0, table.CostMs); continue; }
            var bytes = UniversePayload.Encode(table.Buckets.Values);
            cache.Put(keys[i]!, new SolutionEntry(SolutionFamily.EncounterUniverse, [], table.Buckets.Values.Max(b => b.Sum), true, table.Scanned, table.CostMs, Blob: bytes));
            results[i] = (PrecomputeOutcome.Recorded, table.Buckets.Count, bytes.Length, table.CostMs);
        }
        return results;
    }
}
