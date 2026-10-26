// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Engine.Generation;
using AutoLegalidad.Engine.SolutionCache;
using PKHeX.Core;

namespace AutoLegalidad.Engine.Optimization;

/// <summary>
/// Un modelo de semilla: como el juego deriva EC, PID, IV, habilidad, sexo, naturaleza y tamano a partir de una semilla para UNA familia de encuentros.
/// Siempre se apoya en las rutinas PUBLICAS del Core (nunca en una tabla propia de legalidad); el candidato final se valida igualmente con el analisis oficial.
/// </summary>
internal interface ISeedModel
{
    string Name { get; }

    /// <summary>True si la semilla del juego tiene 32 bits y <c>[0, 2^32)</c> es TODO su dominio (la busqueda puede demostrar un maximo).</summary>
    bool IsFullDomain { get; }

    /// <summary>Escribe en <paramref name="scratch"/> lo que fija la semilla. False si la semilla no puede producir este encuentro (p. ej. otra incursion).</summary>
    bool Apply(PKM scratch, ulong seed);

    /// <summary>Suma de IV que produciria la semilla, calculada sin escribir ningun campo (filtro barato); <c>-1</c> si el modelo no la sabe calcular. Si <see cref="Apply"/> acepta la semilla, ambas sumas deben coincidir (lo comprueban las pruebas).</summary>
    int FastIvSum(ulong seed);

    /// <summary>
    /// Como <see cref="FastIvSum"/> pero sabiendo que solo interesan sumas MAYORES que <paramref name="threshold"/>: puede cortar el cálculo en cuanto la suma ya no pueda superarla y devolver 0
    /// (el recorrido descarta cualquier valor menor o igual que el umbral, así que el resultado de la búsqueda es idéntico y solo se ahorra trabajo). Por defecto no corta.
    /// </summary>
    int FastIvSumAbove(ulong seed, int threshold) => FastIvSum(seed);

    /// <summary>
    /// Como <see cref="FastIvSumAbove"/> cuando el llamador YA sabe que la semilla es del encuentro (el selector por grupo del precalculo, comprobado contra <c>CanBeEncountered</c> del Core): puede omitir esa
    /// comprobacion, que es lo mas caro de la funcion. Por defecto, igual que <see cref="FastIvSumAbove"/>.
    /// </summary>
    int FastIvSumAboveForSelected(ulong seed, int threshold) => FastIvSumAbove(seed, threshold);

    /// <summary>
    /// Clave de reparto del flujo de IV para el precalculo por grupo: los modelos con la MISMA clave (&gt;= 0, &lt; 8) dan, con una semilla ya elegida por el selector, la MISMA suma de IV (mismos IV perfectos), asi que se
    /// calcula una vez por semilla y no una por plantilla. -1 = sin reparto.
    /// </summary>
    int IvStreamKeyForSelected => -1;

    /// <summary>Suma de IV completa (sin cota) de una semilla ya elegida por el selector; solo se llama si <see cref="IvStreamKeyForSelected"/> &gt;= 0.</summary>
    int FastIvSumForSelected(ulong seed) => FastIvSum(seed);

    /// <summary>
    /// Identidad de CACHE (R-011, ADR 0037): TODO lo que condiciona el comportamiento del modelo (huella del encuentro, IV perfectos, PID forzado, Teratipo original, parametros...) y que no esta ya
    /// en los criterios del Pokemon. Dos modelos con la misma identidad dan la misma semilla -&gt; resultado. Por defecto, el nombre (valido solo para un modelo sin estado).
    /// </summary>
    string CacheIdentity => Name;

    /// <summary>True si el resultado del modelo depende del id32 del entrenador (PID forzado, SID dentro de la semilla): entra HASHEADO en la clave de la cache.</summary>
    bool DependsOnTrainerId => false;

    /// <summary>
    /// True si la semilla FIJA la naturaleza, el sexo y la habilidad (Teratipo, incursiones, Z-A, LCRNG): entonces la busqueda los filtra y entran en la clave de la cache. False si <see cref="Apply"/> no los
    /// toca (salvajes de Espada/Escudo: la semilla solo da EC, PID, IV y tamano), con lo que la busqueda es la misma para cualquier valor y la clave los omite.
    /// </summary>
    bool SeedFixesNatureGenderAbility => true;

    /// <summary>True si el resultado de la busqueda no depende del nivel, del lugar ni de la version de origen del Pokemon (la clave de la cache los omite y comparten entradas Espada y Escudo).</summary>
    bool SeedIndependentOfOrigin => false;

    /// <summary>
    /// True (por defecto) si la relevancia de los stats del candidato (desempate entre semillas de igual suma; depende de la especie y de la naturaleza) entra en la clave de la cache. False en los
    /// salvajes de Espada/Escudo (ADR 0042): la entrada guarda TODAS las semillas de la suma maxima (el grupo de empate completo) y el servicio las reordena con la relevancia del candidato, asi que una
    /// misma entrada sirve a cualquier naturaleza y el resultado es identico al de la busqueda por candidato.
    /// </summary>
    bool RelevanceInKey => true;
}

/// <summary>
/// Optimizacion de IV por busqueda EXHAUSTIVA de semillas de 32 bits (Hito 3, propuesta 1 del Hito 2): recorre el dominio en lotes, en paralelo, acotada por
/// tiempo y cancelable, conservando naturaleza, sexo, variocolor, habilidad (y Teratipo y tipo de Poder Oculto). El orden de seleccion es determinista:
/// maxima suma de IV, desempate por la relevancia de cada stat y, por ultimo, la semilla menor. Un recorrido COMPLETO de un dominio de 32 bits demuestra el maximo
/// (<c>Exhaustive</c>); un recorrido parcial devuelve lo mejor hallado y lo dice (<c>BudgetExhausted</c>), nunca «sin margen».
/// </summary>
internal static partial class SeedSpaceSearch
{
    private const ulong Domain = 1UL << 32;
    private const ulong BatchSize = 1UL << 24;
    private const int TopK = 48;
    // El plazo manda la solicitud (el usuario autorizo mas tiempo para Optimizar: 5 min por defecto); este tope solo evita busquedas sin fin si no hay plazo.
    private static readonly TimeSpan MaxTime = TimeSpan.FromMinutes(10);
    // Tiempo maximo de la busqueda dirigida por restricciones antes de caer al recorrido de 2^32 semillas.
    private static readonly TimeSpan SolverBudget = TimeSpan.FromSeconds(8);

    /// <summary>Hilos de la busqueda (se acota para no ahogar la maquina del usuario: la UI sigue respondiendo). Configurable con <see cref="OptimizerSwitches.SeedSearchThreads"/>.</summary>
    public static int Threads
    {
        get => OptimizerSwitches.SeedSearchThreads ?? OptimizerSwitches.DefaultSeedSearchThreads;
        set => OptimizerSwitches.SeedSearchThreads = Math.Max(1, value);
    }

    /// <summary>Contadores de la ultima busqueda (solo diagnostico y pruebas).</summary>
    /// <param name="FromCache">True si la respuesta salio de la cache de soluciones: NO hubo busqueda (<c>Scanned</c> = 0, <c>Complete</c> = la entrada era exhaustiva).</param>
    public sealed record Stats(ulong Scanned, bool Complete, double Seconds, int Hits, bool FromCache = false);

    public static Stats? Last { get; private set; }

    private sealed record Hit(ulong Seed, int Sum, int[] Ivs, PKM Pk);

    public static SubResult? Run(OptimizeRequest r, PKM pk, LegalityAnalysis la, ref int attempts, ISeedModel? modelOverride = null)
    {
        var model = modelOverride ?? SeedModels.For(la.EncounterMatch, pk, r.Dest);
        if (model is null) return null;

        var cur = PkmFieldsAccess.IVs(pk);
        var curSum = cur.Sum();
        var knowsHp = StatOptimizer.KnowsHiddenPower(pk);
        var relevance = StatPriority.MostRelevantFirst(pk);
        var maxIv = pk.MaxIV;
        var target = knowsHp ? TargetWithHiddenPower(cur, maxIv) : maxIv * 6;
        var type = la.Info.PIDIV.Type;
        if (curSum >= target)
            return new SubResult(null, "Enumeration", true, "Los IV ya están al máximo posible: no hay nada que buscar.", model.Name);

        var limit = Min(r.DeadlineUtc, DateTime.UtcNow + MaxTime);

        // R-011 (ADR 0037): cache persistente de soluciones. Primero se consulta (con revalidacion completa); la busqueda real solo si falta o no sirve.
        var cache = SolutionCacheHost.Current;
        // ADR 0041: en los encuentros cuya semilla fija los rasgos del candidato (Teratipo), UNA entrada por ENCUENTRO sirve a cualquier candidato; sin entrada y sin modo registro, camino por candidato.
        if (cache.Enabled && model is IEncounterUniverseModel universeModel && UniverseEligible(universeModel, pk, knowsHp)
            && TryUniverse(cache, universeModel, r, pk, curSum, target, type, limit) is { } fromUniverse)
            return fromUniverse;
        string? cacheKey = null;
        if (cache.Enabled)
        {
            cacheKey = SolutionKey.ForSeed(model, pk, r.Dest.Trainer, knowsHp, cur, relevance);
            if (TryServeFromCache(cache, cacheKey, r, pk, model, curSum, target, knowsHp, type, limit, relevance) is { } served) return served;
        }

        var hits = new List<Hit>(TopK * 2);
        var start = DateTime.UtcNow;
        ulong scanned = 0;
        var complete = true;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(r.Cancellation);

        // Hito 3b (ADR 0018): en las familias de 64 bits, primero la busqueda DIRIGIDA por restricciones (todo el dominio, milisegundos); el recorrido de 2^32 solo si no basta.
        if (OptimizerSwitches.SeedConstraintSolver && model is ISeedConstraintModel constraintModel)
            hits.AddRange(SolveForTarget(constraintModel, pk, knowsHp, cur, maxIv, limit, r.Cancellation, relevance));
        var solved = hits.Count > 0 && hits[0].Sum >= target;

        var threads = Threads;   // una vez por busqueda: si alguien cambia el ajuste a mitad, esta busqueda sigue con los hilos con que empezo
        for (ulong batch = 0; batch < Domain && !solved; batch += BatchSize)
        {
            if (cts.IsCancellationRequested || DateTime.UtcNow >= limit) { complete = false; break; }
            var local = new List<Hit>[threads];
            var slice = BatchSize / (ulong)threads;
            var done = new bool[threads];
            Parallel.For(0, threads, new ParallelOptions { MaxDegreeOfParallelism = threads, CancellationToken = CancellationToken.None }, t =>
            {
                var from = batch + slice * (ulong)t;
                var to = t == threads - 1 ? batch + BatchSize : from + slice;
                local[t] = ScanRange(model, pk, knowsHp, from, to, limit, r.Cancellation, curSum, relevance, out var finished);
                done[t] = finished;
            });
            scanned += BatchSize;
            foreach (var l in local) hits.AddRange(l);
            hits = Trim(hits, relevance);
            r.Progress?.Invoke(new OptimizeProgress(model.Name, Math.Min(1.0, (batch + BatchSize) / (double)Domain), hits.Count > 0 ? hits[0].Sum : curSum, target, DateTime.UtcNow - start));
            if (done.Any(d => !d)) { complete = false; break; }
            if (hits.Count > 0 && hits[0].Sum >= target) break;
        }
        attempts += (int)Math.Min(int.MaxValue, scanned / 1024);
        Last = new Stats(scanned, complete && scanned >= Domain, (DateTime.UtcNow - start).TotalSeconds, hits.Count);

        var exhaustive = model.IsFullDomain && complete && scanned >= Domain;
        var rejected = 0;
        foreach (var h in hits)
        {
            if (h.Sum <= curSum) break; // los siguientes son peores: nada que mejorar
            var cand = Finish(h.Pk);
            if (!StatOptimizer.QuickValid(cand, r.Dest, pk) || !PidivFamily.Same(StatOptimizer.Analyze(cand, r.Dest).Info.PIDIV.Type, type)) { rejected++; continue; }
            var proven = exhaustive || h.Sum >= target; // el maximo teorico (o el de todo el dominio) esta demostrado
            RecordToCache(cache, cacheKey, r, model, hits, curSum, target, exhaustive, scanned, start, hasValid: true);
            return new SubResult(cand, proven ? "Enumeration" : "BudgetExhausted", proven, LimitText(h.Sum, target, exhaustive, scanned), model.Name);
        }

        if (rejected > 0)
            return new SubResult(null, "BudgetExhausted", false,
                $"El recorrido halló {rejected} combinaciones de IV mejores por semilla, pero el análisis oficial de PKHeX no acepta ninguna (reglas del encuentro que la semilla no resume).", model.Name);
        RecordToCache(cache, cacheKey, r, model, hits, curSum, target, exhaustive, scanned, start, hasValid: false);
        return NoImprovement(exhaustive, scanned, model.Name);
    }

    /// <summary>
    /// Semillas de 64 bits (todo el dominio) que dan EXACTAMENTE el vector de IV maximo (con el tipo de Poder Oculto conservado si se conoce) y conservan naturaleza, sexo, habilidad
    /// y variocolor del Pokemon. Cada semilla propuesta por el solver se reproduce con el generador del Core y se filtra como en el recorrido.
    /// </summary>
    private static List<Hit> SolveForTarget(ISeedConstraintModel model, PKM pk, bool knowsHp, int[] cur, int maxIv, DateTime limit, CancellationToken ct, int[] relevance)
    {
        var targetVec = new int[6];
        for (var i = 0; i < 6; i++) targetVec[i] = knowsHp ? ((cur[i] & 1) == (maxIv & 1) ? maxIv : maxIv - 1) : maxIv;
        var hits = new List<Hit>();
        var scratch = pk.Clone();
        var nature = pk.Nature;
        var gender = pk.Gender;
        var shiny = pk.IsShiny;
        var ability = pk.Ability;
        var hpType = knowsHp ? pk.HPType : -1;
        var ecRule = EvolutionEc.Applies(pk.Species) && pk.Format >= 6;
        var ecRare = EvolutionEc.IsRare(pk.EncryptionConstant);
        bool Accept(ulong seed)
        {
            if (!model.Apply(scratch, seed)) return false;
            if (scratch.Nature != nature || scratch.Gender != gender || scratch.Ability != ability || scratch.IsShiny != shiny) return false;
            var ivs = PkmFieldsAccess.IVs(scratch);
            if (!ivs.AsSpan().SequenceEqual(targetVec)) return false;
            if (ecRule && EvolutionEc.IsRare(scratch.EncryptionConstant) != ecRare) return false;
            if (hpType >= 0 && scratch.HPType != hpType) return false;
            hits.Add(new Hit(seed, ivs.Sum(), ivs, scratch.Clone()));
            return true;
        }
        var budget = Min(limit, DateTime.UtcNow + SolverBudget);
        SeedConstraintSolver.Find(model.Problems(pk, targetVec), Accept, want: 6, samplesPerSystem: shiny ? 1 << 17 : 1 << 12, budget, ct);
        return Trim(hits, relevance);
    }

    /// <summary>
    /// Primera semilla (la menor) del dominio de 32 bits del modelo que cumple <paramref name="accept"/> (sobre la entidad ya generada) y, si se da, <paramref name="seedFilter"/> (filtro
    /// barato por semilla, p. ej. la clase de EC). Para GENERAR un candidato que exige algo que el generador por criterios del Core no sabe pedir (la EC de Dudunsparce/Maushold).
    /// Determinista: lotes crecientes; dentro de un lote se queda con la semilla menor. Devuelve una copia con el modelo aplicado, o <c>null</c> si no hay (o se acabo el plazo).
    /// </summary>
    public static PKM? FindSeed(ISeedModel model, PKM template, Func<PKM, bool> accept, DateTime limit, CancellationToken ct, Func<ulong, bool>? seedFilter = null)
    {
        const ulong chunk = 1UL << 20;
        var threads = Threads;
        for (ulong batch = 0; batch < Domain; batch += chunk * (ulong)threads)
        {
            if (ct.IsCancellationRequested || DateTime.UtcNow >= limit) return null;
            var best = ulong.MaxValue;
            PKM? bestPk = null;
            var gate = new object();
            Parallel.For(0, threads, new ParallelOptions { MaxDegreeOfParallelism = threads }, t =>
            {
                var scratch = template.Clone();
                var from = batch + chunk * (ulong)t;
                var to = Math.Min(Domain, from + chunk);
                for (var s = from; s < to; s++)
                {
                    if (s >= Volatile.Read(ref best)) break;
                    if (seedFilter is not null && !seedFilter(s)) continue;
                    if (!model.Apply(scratch, s) || !accept(scratch)) continue;
                    lock (gate)
                        if (s < best) { best = s; bestPk = scratch.Clone(); }
                    break;
                }
            });
            if (bestPk is not null) return Finish(bestPk);
        }
        return null;
    }

    private static List<Hit> ScanRange(ISeedModel model, PKM original, bool knowsHp, ulong from, ulong to, DateTime limit, CancellationToken ct, int curSum, int[] relevance, out bool finished)
    {
        var scratch = original.Clone();
        var best = new List<Hit>(TopK + 1);
        var nature = original.Nature;
        var gender = original.Gender;
        var shiny = original.IsShiny;
        var ability = original.Ability;
        var hpType = knowsHp ? original.HPType : -1;
        var ecRule = EvolutionEc.Applies(original.Species) && original.Format >= 6; // la clase de EC fija la forma de Dudunsparce/Maushold
        var ecRare = EvolutionEc.IsRare(original.EncryptionConstant);
        var threshold = curSum; // solo interesan sumas que mejoren la actual
        finished = true;
        for (var s = from; s < to; s++)
        {
            if ((s & 0x3FFF) == 0 && (ct.IsCancellationRequested || DateTime.UtcNow >= limit)) { finished = false; break; }
            var fast = model.FastIvSumAbove(s, threshold);
            if (fast >= 0 && fast <= threshold) continue;
            if (!model.Apply(scratch, s)) continue;
            if (scratch.Nature != nature || scratch.Gender != gender || scratch.Ability != ability || scratch.IsShiny != shiny) continue;
            if (ecRule && EvolutionEc.IsRare(scratch.EncryptionConstant) != ecRare) continue;
            var sum = scratch.IV_HP + scratch.IV_ATK + scratch.IV_DEF + scratch.IV_SPA + scratch.IV_SPD + scratch.IV_SPE;
            if (sum <= threshold) continue;
            if (hpType >= 0 && scratch.HPType != hpType) continue;
            best.Add(new Hit(s, sum, PkmFieldsAccess.IVs(scratch), scratch.Clone()));
            if (best.Count > TopK * 4)
            {
                best = Trim(best, relevance);
                threshold = Math.Max(threshold, best.Count >= TopK ? best[^1].Sum - 1 : threshold);
            }
        }
        return Trim(best, relevance);
    }

    /// <summary>Orden total y determinista: suma descendente, relevancia de cada stat, semilla ascendente.</summary>
    private static List<Hit> Trim(List<Hit> hits, int[]? relevance)
    {
        relevance ??= [0, 1, 2, 3, 4, 5];
        hits.Sort((a, b) => Compare(a, b, relevance));
        if (hits.Count > TopK) hits.RemoveRange(TopK, hits.Count - TopK);
        return hits;
    }

    private static int Compare(Hit a, Hit b, int[] relevance)
    {
        if (a.Sum != b.Sum) return b.Sum.CompareTo(a.Sum);
        foreach (var i in relevance)
            if (a.Ivs[i] != b.Ivs[i]) return b.Ivs[i].CompareTo(a.Ivs[i]);
        return a.Seed.CompareTo(b.Seed);
    }

    private static PKM Finish(PKM scratchClone)
    {
        var c = scratchClone;
        c.RefreshChecksum();
        if (c.PartyStatsPresent) c.SetStats(c.GetStats(c.PersonalInfo));
        return c;
    }

    /// <summary>Con Poder Oculto conocido el tipo se conserva: cada IV solo admite su paridad (31 o 30).</summary>
    private static int TargetWithHiddenPower(int[] cur, int max)
    {
        var t = 0;
        foreach (var v in cur) t += (v & 1) == (max & 1) ? max : max - 1;
        return t;
    }

    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;
}

/// <summary>Elige el modelo de semilla de un encuentro (o <c>null</c> si esa familia aun no tiene busqueda exhaustiva).</summary>
internal static class SeedModels
{
    public static ISeedModel? For(IEncounterable enc, PKM pk, Engine.Context.Destination dest)
    {
        // Los encuentros con IV fijados no los saca la semilla: no hay nada que buscar (el optimizador lo declara con la base «FixedIVs»).
        if (FixedIvSets.Specified(enc)) return null;
        switch (enc)
        {
            case ITeraRaid9 raid when pk is PK9 p9:
                return new Tera9Model(raid, (byte)p9.TeraTypeOriginal);
            case EncounterSlot8 slot when pk is PK8:
                return new Overworld8Model(FlawlessFor(slot, pk));
            case EncounterStatic8 st8 when pk is PK8 p8:
                // Estaticos de SwSh que salen del mundo: misma semilla de 32 bits que los salvajes (EncounterStatic8.cs: IsOverworldCorrelation/SetPINGA -> Overworld8RNG.ApplyDetails).
                // Los regalos y guionizados admiten cualquier semilla de 128 bits: no hay dominio finito que recorrer.
                if (!st8.IsOverworldCorrelation) return null;
                return new Overworld8Model(st8.FlawlessIVCount, forcedPid: true, p8.ID32, p8.IsShiny);
            case EncounterGift3 g3 when pk is PK3 p3:
                return Gift3SeedModels.For(g3, p3);
            case IGenerateSeed64 g64 when pk is PK8 && enc.GetType().Name.StartsWith("EncounterStatic8N", StringComparison.Ordinal):
                // En las incursiones de rango (8N) la semilla tambien fija el nivel y el nivel de Dinamax: solo valen las semillas coherentes con los del Pokemon.
                Func<PKM, ulong, bool>? possible = enc is EncounterStatic8N n8 ? (p, s) => n8.IsPossibleSeed(p, s, true).Possible : null;
                return new Raid8Model(g64, dest.Trainer, (enc as IFlawlessIVCount)?.FlawlessIVCount ?? 0, possible, Raid8Param(enc));
            case IEncounter9a e9 when pk is PA9:
                if (e9.Correlation is LumioseCorrelation.PreApplyIVs or LumioseCorrelation.ReApplyIVs) return null; // los IV no salen de la semilla
                return new LumioseModel(e9, PersonalTable.ZA[pk.Species, pk.Form]);
            default:
                return null;
        }
    }

    /// <summary>Parametros de generacion de una plantilla de incursion de SWSH (los mismos que arma el Core en <c>EncounterStatic8Nest.GetParam</c>, que es privado).</summary>
    private static GenerateParam8? Raid8Param(IEncounterable enc) => enc switch
    {
        EncounterStatic8N e => Raid8Param(e.Species, e.Form, e.Gender, e.FlawlessIVCount, e.Ability, e.Shiny, e.IVs),
        EncounterStatic8ND e => Raid8Param(e.Species, e.Form, e.Gender, e.FlawlessIVCount, e.Ability, e.Shiny, e.IVs),
        EncounterStatic8NC e => Raid8Param(e.Species, e.Form, e.Gender, e.FlawlessIVCount, e.Ability, e.Shiny, e.IVs),
        _ => null,
    };

    private static GenerateParam8 Raid8Param(ushort species, byte form, byte gender, byte flawless, AbilityPermission ability, Shiny shiny, IndividualValueSet ivs)
    {
        var pi = PersonalTable.SWSH[species, form];
        var ratio = gender switch { 0 => PersonalInfo.RatioMagicMale, 1 => PersonalInfo.RatioMagicFemale, 2 => PersonalInfo.RatioMagicGenderless, _ => pi.Gender };
        return new GenerateParam8(species, ratio, flawless, ability, shiny, Nature.Random, ivs);
    }

    /// <summary>Lo que el Core admite como IV perfectos en un salvaje de SWSH por el aire/agua (3) frente a los de la hierba oculta (0).</summary>
    private static int FlawlessFor(EncounterSlot8 slot, PKM pk)
    {
        if (pk.MetLevel < slot.LevelMax) return 0;
        if (slot.Parent.PermitCrossover) return 3;
        if ((slot.Weather & AreaWeather8.Fishing) != 0) return 3;
        return 0;
    }
}

/// <summary>Calculo comun de la suma de IV a partir del flujo de xoroshiro tras haber consumido EC y PID (con los IV perfectos primero), identico al del Core.</summary>
internal static class IvStream
{
    public static int Sum(ref Xoroshiro128Plus xoro, int flawless)
    {
        Span<int> ivs = [-1, -1, -1, -1, -1, -1];
        for (var i = 0; i < flawless; i++)
        {
            int index;
            do { index = (int)xoro.NextInt(6); }
            while (ivs[index] != -1);
            ivs[index] = 31;
        }
        var sum = 0;
        for (var i = 0; i < 6; i++)
            sum += ivs[i] == -1 ? (int)xoro.NextInt(32) : ivs[i];
        return sum;
    }

    /// <summary>
    /// Igual que <see cref="Sum(ref Xoroshiro128Plus, int)"/> (mismas tiradas, mismo orden) pero corta y devuelve 0 en cuanto el déficit respecto a 31 por IV impide superar <paramref name="threshold"/>:
    /// la mayoría de las semillas se descartan tras 1-3 tiradas en vez de 6.
    /// </summary>
    public static int SumAbove(ref Xoroshiro128Plus xoro, int flawless, int threshold)
    {
        var allowed = 31 * 6 - threshold - 1;   // déficit máximo con el que la suma todavía supera el umbral
        if (allowed < 0) return 0;
        Span<int> ivs = [-1, -1, -1, -1, -1, -1];
        for (var i = 0; i < flawless; i++)
        {
            int index;
            do { index = (int)xoro.NextInt(6); }
            while (ivs[index] != -1);
            ivs[index] = 31;
        }
        var sum = 0;
        var deficit = 0;
        for (var i = 0; i < 6; i++)
        {
            if (ivs[i] != -1) { sum += ivs[i]; continue; }
            var v = (int)xoro.NextInt(32);
            deficit += 31 - v;
            if (deficit > allowed) return 0;
            sum += v;
        }
        return sum;
    }
}

/// <summary>
/// Incursiones de Teratipo, distribuciones y 7 estrellas de Escarlata/Purpura: la semilla de 32 bits fija EC, PID, IV, habilidad, sexo, naturaleza, Teratipo y tamano.
/// Con <paramref name="universal"/> el modelo NO filtra por el Teratipo original del candidato (lo trata como un rasgo mas del resultado): es el modelo del barrido universal por encuentro (ADR 0041).
/// </summary>
internal sealed class Tera9Model(ITeraRaid9 enc, byte originalTera, bool universal = false) : IEncounterUniverseModel
{
    private readonly int _flawless = enc.FlawlessIVCount;

    /// <summary>EC que da la semilla (primera tirada de xoroshiro; Core Tera9RNG).</summary>
    public uint EcOf(ulong seed) => (uint)new Xoroshiro128Plus((uint)seed).NextInt(uint.MaxValue);
    public string Name => "SeedSpaceSearch/Tera9";
    public bool IsFullDomain => true;
    public string CacheIdentity => $"{Name}|enc={SolutionCache.EncounterFingerprint.Of(enc)}|flawless={_flawless}|originalTera={originalTera}";

    /// <summary>Identidad del ENCUENTRO sin el Teratipo del candidato (ADR 0041).</summary>
    public string UniverseIdentity => $"{Name}|enc={SolutionCache.EncounterFingerprint.Of(enc)}|flawless={_flawless}|universe";
    public ISeedModel UniverseModel => new Tera9Model(enc, 0, universal: true);

    public uint BucketOf(PKM scratch, ulong seed)
        => UniverseBuckets.Pack((int)scratch.Nature, scratch.Gender, scratch.Ability, Tera9RNG.GetTeraType(seed, enc.TeraType, enc.Species, enc.Form), UniverseBuckets.EcClass(scratch, scratch.EncryptionConstant));

    public uint BucketOfCandidate(PKM pk)
        => UniverseBuckets.Pack((int)pk.Nature, pk.Gender, pk.Ability, pk is ITeraType tt ? (byte)tt.TeraTypeOriginal : (byte)0, UniverseBuckets.EcClass(pk, pk.EncryptionConstant));

    public bool Apply(PKM scratch, ulong seed)
    {
        var s = (uint)seed;
        if (!enc.CanBeEncountered(s)) return false;
        if (!universal && !Tera9RNG.IsMatchTeraType(s, enc.TeraType, enc.Species, enc.Form, originalTera)) return false;
        return enc.GenerateSeed32(scratch, s);
    }

    public int FastIvSum(ulong seed)
    {
        var s = (uint)seed;
        if (!enc.CanBeEncountered(s)) return 0; // no es esta incursion: Apply la rechazara tambien
        if (!universal && !Tera9RNG.IsMatchTeraType(s, enc.TeraType, enc.Species, enc.Form, originalTera)) return 0;
        var xoro = new Xoroshiro128Plus(s);
        xoro.NextInt(uint.MaxValue); // EC
        xoro.NextInt(uint.MaxValue); // TID falso
        xoro.NextInt(uint.MaxValue); // PID (una sola tirada de variocolor: RollCount = 1 en las tres familias)
        return IvStream.Sum(ref xoro, _flawless);
    }

    public int IvStreamKeyForSelected => universal ? _flawless : -1;

    public int FastIvSumForSelected(ulong seed)
    {
        var xoro = new Xoroshiro128Plus((uint)seed);
        xoro.NextInt(uint.MaxValue); xoro.NextInt(uint.MaxValue); xoro.NextInt(uint.MaxValue);
        return IvStream.Sum(ref xoro, _flawless);
    }

    public int FastIvSumAboveForSelected(ulong seed, int threshold)
    {
        if (!universal) return FastIvSumAbove(seed, threshold); // el filtro de Teratipo del candidato sigue haciendo falta
        var xoro = new Xoroshiro128Plus((uint)seed);
        xoro.NextInt(uint.MaxValue); xoro.NextInt(uint.MaxValue); xoro.NextInt(uint.MaxValue);
        return IvStream.SumAbove(ref xoro, _flawless, threshold);
    }

    public int FastIvSumAbove(ulong seed, int threshold)
    {
        var s = (uint)seed;
        if (!enc.CanBeEncountered(s)) return 0;
        if (!universal && !Tera9RNG.IsMatchTeraType(s, enc.TeraType, enc.Species, enc.Form, originalTera)) return 0;
        var xoro = new Xoroshiro128Plus(s);
        xoro.NextInt(uint.MaxValue); xoro.NextInt(uint.MaxValue); xoro.NextInt(uint.MaxValue);
        return IvStream.SumAbove(ref xoro, _flawless, threshold);
    }
}

/// <summary>Incursiones de Espada/Escudo: la semilla de la incursion es de 64 bits; se recorre el subespacio [0, 2^32) (cualquier semilla del juego es valida).</summary>
internal sealed class Raid8Model(IGenerateSeed64 enc, ITrainerInfo trainer, int flawless, Func<PKM, ulong, bool>? possible = null, GenerateParam8? param = null) : ISeedConstraintModel
{
    public string Name => "SeedSpaceSearch/Raid8";
    public bool IsFullDomain => false;
    public string CacheIdentity => $"{Name}|enc={SolutionCache.EncounterFingerprint.Of(enc)}|flawless={flawless}|possible={(possible is not null)}|param={SolutionCache.EncounterFingerprint.Of(param)}";

    public bool Apply(PKM scratch, ulong seed)
    {
        if (possible is not null && !possible(scratch, seed)) return false;
        enc.GenerateSeed64(scratch, trainer, seed);
        return true;
    }

    public int FastIvSum(ulong seed)
    {
        var xoro = new Xoroshiro128Plus(seed);
        xoro.NextInt(uint.MaxValue); // EC
        xoro.NextInt(uint.MaxValue); // TID falso
        xoro.NextInt(uint.MaxValue); // PID
        return IvStream.Sum(ref xoro, flawless);
    }

    public int FastIvSumAbove(ulong seed, int threshold)
    {
        var xoro = new Xoroshiro128Plus(seed);
        xoro.NextInt(uint.MaxValue); xoro.NextInt(uint.MaxValue); xoro.NextInt(uint.MaxValue);
        return IvStream.SumAbove(ref xoro, flawless, threshold);
    }

    /// <summary>Tiradas de <c>RaidRNG.TryApply</c> (Core: RaidRNG.cs): EC, TID falso, PID, IV perfectos (indices), IV restantes, habilidad, sexo y naturaleza.</summary>
    public IEnumerable<List<SeedDraw>> Problems(PKM template, int[] target)
    {
        if (param is not { } p || p.IVs.IsSpecified || p.Species == (ushort)Species.Toxtricity) yield break; // Toxtricity: la naturaleza depende ademas de la forma
        var pool = Enumerable.Range(0, 6).Where(i => target[i] == 31).ToList();
        if (pool.Count < p.FlawlessIVs) yield break;
        var abilityIndex = template.AbilityNumber switch { 1 => 0, 2 => 1, 4 => 2, _ => -1 };
        foreach (var picks in SeedConstraintSolver.OrderedPicks(pool, p.FlawlessIVs))
        {
            var draws = new List<SeedDraw> { SeedDraw.Free, SeedDraw.Free, SeedDraw.Free };
            foreach (var i in picks) draws.Add(new SeedDraw(3, [i]));
            for (var i = 0; i < 6; i++)
                if (Array.IndexOf(picks, i) < 0) draws.Add(new SeedDraw(5, [target[i]]));
            if (!SeedConstraintSolver.TryAppendTail(draws, p.Ability, abilityIndex, d => AddGender(d, p.GenderRatio, template.Gender), false, (int)template.Nature)) yield break;
            yield return draws;
        }
    }

    // RaidRNG.TryApply: gender = rng.NextInt(253) + 1 < ratio ? 1 : 0 (mascara 255).
    private static bool AddGender(List<SeedDraw> draws, byte ratio, byte gender)
    {
        if (ratio is PersonalInfo.RatioMagicGenderless or PersonalInfo.RatioMagicFemale or PersonalInfo.RatioMagicMale) return true;
        if (gender > 1) return false;
        var values = SeedConstraintSolver.Allowed(253, v => (v + 1 < ratio ? 1 : 0) == gender);
        if (values.Length == 0) return false;
        draws.Add(new SeedDraw(8, values));
        return true;
    }
}

/// <summary>Salvajes de Espada/Escudo que caminan por el mundo: semilla de 32 bits derivada de la constante de cifrado (<see cref="Overworld8RNG"/>).</summary>
internal sealed class Overworld8Model(int flawless, bool forcedPid = false, uint id32 = 0, bool wantShiny = false) : ISeedModel
{
    public string Name => "SeedSpaceSearch/Overworld8";
    public bool IsFullDomain => true;
    public string CacheIdentity => $"{Name}|flawless={flawless}|forcedPid={forcedPid}|wantShiny={wantShiny}";
    public bool DependsOnTrainerId => forcedPid;
    /// <summary>Overworld8RNG.TryApplyFromSeed: la semilla da EC, PID, IV y tamano; naturaleza, sexo y habilidad no salen de ella.</summary>
    public bool SeedFixesNatureGenderAbility => false;
    public bool SeedIndependentOfOrigin => true;
    /// <summary>La semilla no fija la naturaleza: la relevancia (que depende de ella) no entra en la clave; la entrada guarda el grupo de empate completo (ADR 0042).</summary>
    public bool RelevanceInKey => false;

    public bool Apply(PKM scratch, ulong seed)
    {
        var pk = (PK8)scratch;
        // Overworld8RNG.GetOriginalSeed/ValidateOverworldEncounter (Overworld8RNG.cs:110-114): uint.MaxValue no es una semilla determinada por Xoroshiro.
        if ((uint)seed == uint.MaxValue) return false;
        var xoro = new Xoroshiro128Plus((uint)seed);
        pk.EncryptionConstant = (uint)xoro.NextInt(uint.MaxValue);
        var pid = (uint)xoro.NextInt(uint.MaxValue);
        if (forcedPid)
        {
            // Encuentros estaticos con variocolor fijado (EncounterStatic8: Shiny distinto de Random o FixedValue): Overworld8RNG.IsPIDValid (Overworld8RNG.cs:129-153).
            // Forzado variocolor: el PID bruto no puede serlo y se le aplica GetShinyPID; forzado no variocolor: si el bruto lo es, se invierte con 0x1000_0000.
            if (wantShiny)
            {
                if (ShinyUtil.GetIsShiny6(id32, pid)) return false;
                pid = ShinyUtil.GetShinyPID((ushort)(id32 & 0xFFFF), (ushort)(id32 >> 16), pid, 0);
            }
            else if (ShinyUtil.GetIsShiny6(id32, pid))
                pid ^= 0x1000_0000;
        }
        pk.PID = pid;

        Span<int> ivs = [-1, -1, -1, -1, -1, -1];
        for (var i = 0; i < flawless; i++)
        {
            int index;
            do { index = (int)xoro.NextInt(6); }
            while (ivs[index] != -1);
            ivs[index] = 31;
        }
        for (var i = 0; i < 6; i++)
            if (ivs[i] == -1) ivs[i] = (int)xoro.NextInt(32);

        pk.IV_HP = ivs[0]; pk.IV_ATK = ivs[1]; pk.IV_DEF = ivs[2];
        pk.IV_SPE = ivs[5]; pk.IV_SPA = ivs[3]; pk.IV_SPD = ivs[4];
        pk.HeightScalar = (byte)(xoro.NextInt(0x81) + xoro.NextInt(0x80));
        pk.WeightScalar = (byte)(xoro.NextInt(0x81) + xoro.NextInt(0x80));
        return true;
    }

    public int FastIvSum(ulong seed)
    {
        var xoro = new Xoroshiro128Plus((uint)seed);
        xoro.NextInt(uint.MaxValue); // EC
        xoro.NextInt(uint.MaxValue); // PID
        return IvStream.Sum(ref xoro, flawless);
    }

    public int FastIvSumAbove(ulong seed, int threshold)
    {
        var xoro = new Xoroshiro128Plus((uint)seed);
        xoro.NextInt(uint.MaxValue); // EC
        xoro.NextInt(uint.MaxValue); // PID
        return IvStream.SumAbove(ref xoro, flawless, threshold);
    }
}

/// <summary>Encuentros de Leyendas Z-A con semilla de 64 bits (se recorre [0, 2^32)): <see cref="LumioseRNG.GenerateData"/>.</summary>
internal sealed class LumioseModel(IEncounter9a enc, PersonalInfo9ZA pi) : ISeedConstraintModel
{
    private readonly GenerateParam9a _param = enc.GetParams(pi);
    public string Name => "SeedSpaceSearch/Lumiose";
    public bool IsFullDomain => false;
    public string CacheIdentity => $"{Name}|enc={SolutionCache.EncounterFingerprint.Of(enc)}|param={SolutionCache.EncounterFingerprint.Of(_param)}";

    public bool Apply(PKM scratch, ulong seed) => LumioseRNG.GenerateData((PA9)scratch, _param, EncounterCriteria.Unrestricted, seed);

    public int FastIvSum(ulong seed) => -1;

    /// <summary>Tiradas de <c>LumioseRNG.GenerateData</c> (Core: LumioseRNG.cs): EC, TID falso (salvo SkipTrainer), una o varias de PID, IV perfectos, IV restantes, habilidad, sexo (0-99) y naturaleza.</summary>
    public IEnumerable<List<SeedDraw>> Problems(PKM template, int[] target)
    {
        var p = _param;
        if (p.IVs.IsSpecified || p.Correlation is LumioseCorrelation.PreApplyIVs or LumioseCorrelation.ReApplyIVs) yield break;
        var pool = Enumerable.Range(0, 6).Where(i => target[i] == 31).ToList();
        if (pool.Count < p.FlawlessIVs) yield break;
        var abilityIndex = template.AbilityNumber switch { 1 => 0, 2 => 1, 4 => 2, _ => -1 };
        // Sin variocolor en las primeras tiradas (se comprueba despues con el Core): una tirada de PID por cada «roll».
        var pidDraws = p.Shiny == Shiny.Random ? Math.Max(1, (int)p.RollCount) : 1;
        foreach (var picks in SeedConstraintSolver.OrderedPicks(pool, p.FlawlessIVs))
        {
            var draws = new List<SeedDraw> { SeedDraw.Free };
            if (p.Correlation != LumioseCorrelation.SkipTrainer) draws.Add(SeedDraw.Free);
            for (var i = 0; i < pidDraws; i++) draws.Add(SeedDraw.Free);
            foreach (var i in picks) draws.Add(new SeedDraw(3, [i]));
            for (var i = 0; i < 6; i++)
                if (Array.IndexOf(picks, i) < 0) draws.Add(new SeedDraw(5, [target[i]]));
            if (!SeedConstraintSolver.TryAppendTail(draws, p.Ability, abilityIndex, d => AddGender(d, p.GenderRatio, template.Gender), p.Nature.IsFixed, (int)template.Nature)) yield break;
            yield return draws;
        }
    }

    private static bool AddGender(List<SeedDraw> draws, byte ratio, byte gender)
    {
        if (ratio is PersonalInfo.RatioMagicGenderless or PersonalInfo.RatioMagicFemale or PersonalInfo.RatioMagicMale) return true;
        if (gender > 1) return false;
        try
        {
            var values = SeedConstraintSolver.Allowed(100, v => LumioseRNG.GetGender(ratio, (ulong)v) == gender);
            if (values.Length == 0) return false;
            draws.Add(new SeedDraw(7, values));
            return true;
        }
        catch (ArgumentOutOfRangeException) { return false; }
    }
}

/// <summary>
/// Errantes de Gen3 (Latios/Latias de Rubi/Zafiro: <see cref="PIDType.Method_1_Roamer"/>): metodo 1 del LCRNG pero el juego solo conserva el byte bajo de la cuarta llamada como IV,
/// de modo que el campo de IV entero cabe en 8 bits (PS = 5 bits y Ataque = 3 bits; el resto vale 0). Se recorren las 2^32 semillas del LCRNG (todo el dominio). Misma regla
/// que el Core (<c>MethodFinder.GetLCRNGRoamerMatch</c>): <c>IV32 = (Next3(seed) &gt;&gt; 16) &amp; 0xFF</c> y PID = (B &lt;&lt; 16) | A.
/// </summary>
internal sealed class Roamer3Model : ISeedModel
{
    public string Name => "SeedSpaceSearch/Roamer3";
    public bool IsFullDomain => true;

    public bool Apply(PKM scratch, ulong seed)
    {
        var s = (uint)seed;
        s = LCRNG.Next(s);
        var a = s >> 16;
        s = LCRNG.Next(s);
        var b = s >> 16;
        s = LCRNG.Next(s);
        var v = (s >> 16) & 0xFF;
        var pid = (b << 16) | a;
        scratch.PID = pid;
        scratch.EncryptionConstant = pid;
        scratch.IV_HP = (int)(v & 31);
        scratch.IV_ATK = (int)((v >> 5) & 31);
        scratch.IV_DEF = 0; scratch.IV_SPA = 0; scratch.IV_SPD = 0; scratch.IV_SPE = 0;
        scratch.RefreshAbility((int)(pid & 1));
        return true;
    }

    public int FastIvSum(ulong seed)
    {
        var s = LCRNG.Next3((uint)seed);
        var v = (s >> 16) & 0xFF;
        return (int)((v & 31) + ((v >> 5) & 31));
    }
}
