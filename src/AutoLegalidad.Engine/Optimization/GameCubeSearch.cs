// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Engine.Generation;
using PKHeX.Core;

namespace AutoLegalidad.Engine.Optimization;

/// <summary>
/// Familia de metodo de un PIDIV: el Core clasifica un mismo Pokemon como CXD o CXDAnti (PID relanzado por variocolor, MethodFinder.cs:254-300) y un errante de
/// Gen3 con IV truncados como Method_1_Roamer o como Method_1 cuando coinciden (EncounterStatic3.cs:307-320); ambos son el MISMO metodo del juego.
/// </summary>
internal static class PidivFamily
{
    public static bool Same(PIDType a, PIDType b)
        => a == b || (IsCxd(a) && IsCxd(b)) || (IsMethod1(a) && IsMethod1(b));

    private static bool IsCxd(PIDType t) => t is PIDType.CXD or PIDType.CXDAnti or PIDType.CXD_ColoStarter;
    private static bool IsMethod1(PIDType t) => t is PIDType.Method_1 or PIDType.Method_1_Roamer;
}

/// <summary>
/// «Optimizar stats» de los encuentros de GameCube (Colosseum/XD) y del regalo del Rancho de Gen4: recorre el dominio finito del RNG del juego en lugar de pedirle
/// al generador por criterios del Core (que cae a una busqueda aleatoria de hasta un millon de iteraciones por vector y, ademas, ignora la habilidad:
/// <c>MethodCXD.SetFromIVs</c> escribe <c>pk.Ability</c>, cuyo setter es vacio en <c>G3PKM</c>, G3PKM.cs:33).
/// <list type="bullet">
/// <item>Sombras (Colo/XD) y estaticos/intercambios de XD: PID, IV y bit de habilidad salen de UNA semilla de 32 bits del XDRNG (IV, IV, habilidad, PID, PID:
/// <c>MethodCXD.SetRandom</c>, MethodCXD.cs:396-438). Se enumeran los vectores de IV por suma descendente, se recuperan sus semillas con <see cref="XDRNG.GetSeedsIVs"/> y se
/// conserva naturaleza, sexo, variocolor y habilidad; en las sombras ademas debe cumplirse el bloqueo de equipo (<see cref="LockFinder"/>). Si no basta, se recorren
/// las 2^32 semillas (<see cref="SeedSpaceSearch"/>).</item>
/// <item>PokeSpot (XD): el PID y los IV salen de semillas INDEPENDIENTES (MethodPokeSpot.cs:60-68): el PID no se toca y los IV se buscan con la animacion y el nivel de la semilla de IV.</item>
/// <item>Iniciales (Umbreon/Espeon de Colosseum, Eevee de XD): la semilla sale del TID/SID del entrenador (MethodCXD.cs:571-636): dominio de como mucho 2 semillas.</item>
/// <item>Regalo del Rancho (Gen4): los IV son tres llamadas del MRNG (IndividualValueVerifier.cs:95-105): solo valen los vectores con semilla (<see cref="MRNGReversal.HasSeeds(uint)"/>).</item>
/// </list>
/// Cada candidato se valida con el analisis oficial del Core; el motor lo vuelve a validar despues (destino y ida y vuelta).
/// </summary>
internal static class GameCubeSearch
{
    private const int MaxVectors = 2_000_000;
    private static readonly TimeSpan Stage1Time = TimeSpan.FromSeconds(6);

    public static SubResult? Run(OptimizeRequest r, PKM pk, LegalityAnalysis la, ref int attempts)
    {
        var enc = la.EncounterMatch;
        var type = la.Info.PIDIV.Type;

        if (enc is EncounterTrade4RanchGift && pk is PK4 && type == PIDType.Ranch)
            return RunRanch(r, pk, la, ref attempts);

        if (pk is not (CK3 or XK3) || type is not (PIDType.CXD or PIDType.CXDAnti or PIDType.CXD_ColoStarter or PIDType.PokeSpot))
            return null;

        switch (enc)
        {
            case EncounterStarter3Colo:
            case EncounterStatic3XD { Species: (ushort)Species.Eevee }:
                return RunStarter(r, pk, la, ref attempts, enc);
            case EncounterSlot3XD slot:
                return RunPokeSpot(r, pk, la, ref attempts, slot);
            case EncounterShadow3Colo { IsEReader: true }:
                // Las tarjetas e-Reader (solo juegos japoneses) NO tienen correlacion PID-IV: sus IV son siempre 0 por diseno del juego y el generador del Core no los toca
                // (EncounterShadow3Colo.cs:101-139, EncounterGenerator3GC.cs:83-99 «E-Reader have fixed IVs»). No hay semilla que buscar: nada que optimizar.
                return new SubResult(null, "FixedIVs", true,
                    "Las tarjetas e-Reader de Colosseum tienen los IV fijados a 0 por el juego: no hay margen de mejora. Referencia técnica: EncounterShadow3Colo.cs:103-104.", "GameCubeSeedSearch/EReader");
            case EncounterShadow3Colo shadow:
                return RunSeeded(r, pk, la, ref attempts, shadow, noShiny: false, "GameCubeSeedSearch/Shadow3Colo");
            case EncounterShadow3XD shadow:
                return RunSeeded(r, pk, la, ref attempts, shadow, noShiny: true, "GameCubeSeedSearch/Shadow3XD");
            case EncounterStatic3XD:
                return RunSeeded(r, pk, la, ref attempts, null, noShiny: false, "GameCubeSeedSearch/Static3XD");
            case EncounterTrade3XD:
                return RunSeeded(r, pk, la, ref attempts, null, noShiny: false, "GameCubeSeedSearch/Trade3XD");
            default:
                return null;
        }
    }

    // ------------------------------------------------------------------------------------------------ sombras, estaticos e intercambios (CXD)

    private static SubResult RunSeeded(OptimizeRequest r, PKM pk, LegalityAnalysis la, ref int attempts, IShadow3? shadow, bool noShiny, string name)
    {
        var type = la.Info.PIDIV.Type;
        var (cur, curSum, allowed, relevance) = Domain(pk);
        var rules = new CxdRules(pk, noShiny);

        var limit = Min(r.DeadlineUtc, DateTime.UtcNow + Stage1Time);
        Span<uint> seeds = stackalloc uint[XDRNG.MaxCountSeedsIV];
        long vectors = 0;
        var finished = true;
        foreach (var v in IvEnumerator.BySumDescending(allowed, relevance, curSum))
        {
            if (++vectors > MaxVectors || ((vectors & 0x3FF) == 0 && (r.Cancellation.IsCancellationRequested || DateTime.UtcNow >= limit)))
            {
                finished = false;
                break;
            }
            var iv1 = (uint)(v[0] | (v[1] << 5) | (v[2] << 10));
            var iv2 = (uint)(v[5] | (v[3] << 5) | (v[4] << 10));
            var count = XDRNG.GetSeedsIVs(seeds, iv1 << 16, iv2 << 16);
            for (var k = 0; k < count; k++)
            {
                if (!rules.TryDerive(seeds[k], out var pid, out var bit))
                    continue;
                attempts++;
                var cand = Build(pk, pid, v, bit);
                if (shadow is not null && shadow.PartyPrior.Length != 0 && !LockFinder.IsAllShadowLockValid(shadow, seeds[k], cand))
                    continue;
                if (!StatOptimizer.QuickValid(cand, r.Dest, pk)) continue;
                if (!PidivFamily.Same(StatOptimizer.Analyze(cand, r.Dest).Info.PIDIV.Type, type)) continue;
                return new SubResult(cand, "Enumeration", true, null, name);
            }
        }
        if (finished)
            return new SubResult(null, "Enumeration", true,
                "Recorridos todos los vectores de IV por encima de los actuales y sus semillas del XDRNG: ninguno conserva naturaleza, sexo, variocolor y habilidad" + (shadow is { PartyPrior.Length: > 0 } ? " y cumple los bloqueos de equipo del entrenador (referencia técnica: TeamLockResult.cs)." : "."), name);

        // Etapa 2: las 2^32 semillas del XDRNG (todo el dominio), en paralelo y con el mismo orden determinista.
        var model = new CxdSeedModel(pk, shadow, noShiny, name);
        return SeedSpaceSearch.Run(r, pk, la, ref attempts, model)
               ?? new SubResult(null, "BudgetExhausted", false, "No se pudo iniciar la búsqueda por semillas del XDRNG.", name);
    }

    // ------------------------------------------------------------------------------------------------ PokeSpot (XD)

    private static SubResult RunPokeSpot(OptimizeRequest r, PKM pk, LegalityAnalysis la, ref int attempts, EncounterSlot3XD slot)
    {
        const string name = "GameCubeSeedSearch/PokeSpot";
        var type = la.Info.PIDIV.Type;
        var (cur, curSum, allowed, relevance) = Domain(pk);
        var rules = new PokeSpotRules(pk, slot);

        var limit = Min(r.DeadlineUtc, DateTime.UtcNow + Stage1Time);
        Span<uint> seeds = stackalloc uint[XDRNG.MaxCountSeedsIV];
        long vectors = 0;
        var finished = true;
        foreach (var v in IvEnumerator.BySumDescending(allowed, relevance, curSum))
        {
            if (++vectors > MaxVectors || ((vectors & 0x3FF) == 0 && (r.Cancellation.IsCancellationRequested || DateTime.UtcNow >= limit)))
            {
                finished = false;
                break;
            }
            var iv1 = (uint)(v[0] | (v[1] << 5) | (v[2] << 10));
            var iv2 = (uint)(v[5] | (v[3] << 5) | (v[4] << 10));
            var count = XDRNG.GetSeedsIVs(seeds, iv1 << 16, iv2 << 16);
            for (var k = 0; k < count; k++)
            {
                if (!rules.IsValid(seeds[k]))
                    continue;
                attempts++;
                var cand = BuildIvsOnly(pk, v);
                if (!StatOptimizer.QuickValid(cand, r.Dest, pk)) continue;
                if (!PidivFamily.Same(StatOptimizer.Analyze(cand, r.Dest).Info.PIDIV.Type, type)) continue;
                return new SubResult(cand, "Enumeration", true, null, name);
            }
        }
        if (finished)
            return new SubResult(null, "Enumeration", true,
                "Recorridos todos los vectores de IV por encima de los actuales y sus semillas: ninguno cuadra con la animación y el nivel de captura del PokéSpot (referencia técnica: MethodPokeSpot.TryGetOriginSeedIVs).", name);

        var model = new PokeSpotSeedModel(pk, slot);
        return SeedSpaceSearch.Run(r, pk, la, ref attempts, model)
               ?? new SubResult(null, "BudgetExhausted", false, "No se pudo iniciar la búsqueda por semillas del PokéSpot.", name);
    }

    // ------------------------------------------------------------------------------------------------ iniciales: la semilla sale del TID/SID

    private static SubResult RunStarter(OptimizeRequest r, PKM pk, LegalityAnalysis la, ref int attempts, IEncounterable enc)
    {
        const string name = "GameCubeSeedSearch/Starter";
        var type = la.Info.PIDIV.Type;
        var cur = PkmFieldsAccess.IVs(pk);
        var curSum = cur.Sum();
        var knowsHp = StatOptimizer.KnowsHiddenPower(pk);
        var id32 = pk.ID32;
        var tid = pk.TID16;
        var sid = pk.SID16;
        var isColo = enc is EncounterStarter3Colo;

        Span<uint> seeds = stackalloc uint[XDRNG.MaxCountSeedsPID];
        var count = XDRNG.GetSeeds(seeds, (uint)tid << 16, (uint)sid << 16);
        PKM? best = null;
        var bestSum = curSum;
        for (var k = 0; k < count; k++)
        {
            var seed = seeds[k];
            uint iv1, iv2, pid;
            if (isColo)
            {
                if (pk.Species == (ushort)Species.Umbreon)
                {
                    var ivSeed = XDRNG.Next4(seed);
                    iv1 = XDRNG.Next15(ref ivSeed);
                    iv2 = XDRNG.Next15(ref ivSeed);
                    var pidSeed = XDRNG.Next7(seed);
                    pid = MethodCXD.GetColoStarterPID(ref pidSeed, tid, sid);
                }
                else
                {
                    var pidSeedU = XDRNG.Next7(seed);
                    _ = MethodCXD.GetColoStarterPID(ref pidSeedU, tid, sid);
                    var afterFake = XDRNG.Next2(pidSeedU);
                    var ivSeed = afterFake;
                    iv1 = XDRNG.Next15(ref ivSeed);
                    iv2 = XDRNG.Next15(ref ivSeed);
                    var pidSeedE = XDRNG.Next3(afterFake);
                    pid = MethodCXD.GetColoStarterPID(ref pidSeedE, tid, sid);
                }
            }
            else
            {
                var ivSeed = XDRNG.Next4(seed);
                iv1 = XDRNG.Next15(ref ivSeed);
                iv2 = XDRNG.Next15(ref ivSeed);
                var pidSeed = XDRNG.Next7(seed);
                var a = XDRNG.Next16(ref pidSeed);
                var b = XDRNG.Next16(ref pidSeed);
                pid = (a << 16) | b;
            }

            int[] v = [(int)(iv1 & 31), (int)((iv1 >> 5) & 31), (int)((iv1 >> 10) & 31), (int)((iv2 >> 5) & 31), (int)((iv2 >> 10) & 31), (int)(iv2 & 31)];
            var sum = v.Sum();
            if (sum <= bestSum) continue;
            if (pid % 25 != (uint)pk.Nature || ShinyUtil.GetIsShiny3(id32, pid) != pk.IsShiny) continue;
            if (EntityGender.GetFromPIDAndRatio(pid, pk.PersonalInfo.Gender) != pk.Gender) continue;
            attempts++;
            var cand = Build(pk, pid, v, bit: -1);
            if (knowsHp && cand.HPType != pk.HPType) continue;
            if (!StatOptimizer.QuickValid(cand, r.Dest, pk)) continue;
            if (!PidivFamily.Same(StatOptimizer.Analyze(cand, r.Dest).Info.PIDIV.Type, type)) continue;
            best = cand;
            bestSum = sum;
        }

        var why = "Los IV y el PID del inicial salen de la semilla que genera el TID/SID del entrenador: " +
                  $"hay {count} semilla(s) posible(s) y cambiar de semilla exigiría cambiar el TID/SID, es decir, el origen del Pokémon. " +
                  $"Referencia técnica: MethodCXD.TryGetSeedStarter{(isColo ? "Colo" : "XD")}, MethodCXD.cs:{(isColo ? 571 : 614)}.";
        return best is null
            ? new SubResult(null, "Enumeration", true, why, name)
            : new SubResult(best, "Enumeration", true, bestSum >= pk.MaxIV * 6 ? null : why, name);
    }

    // ------------------------------------------------------------------------------------------------ regalo del Rancho (Gen4)

    private static SubResult RunRanch(OptimizeRequest r, PKM pk, LegalityAnalysis la, ref int attempts)
    {
        const string name = "GameCubeSeedSearch/Ranch4";
        var type = la.Info.PIDIV.Type;
        var (cur, curSum, allowed, relevance) = Domain(pk);
        var limit = Min(r.DeadlineUtc, DateTime.UtcNow + TimeSpan.FromSeconds(20));
        long vectors = 0;
        foreach (var v in IvEnumerator.BySumDescending(allowed, relevance, curSum))
        {
            if (++vectors > 100_000_000 || ((vectors & 0x3FFF) == 0 && (r.Cancellation.IsCancellationRequested || DateTime.UtcNow >= limit)))
                return new SubResult(null, "BudgetExhausted", false, $"Se agotó el presupuesto de búsqueda tras {vectors:N0} vectores de IV sin hallar uno con semilla del MRNG del Rancho.", name);
            // Las tres llamadas del MRNG (IVs de HP/Ata/Def y de Vel/AtE/DeE) fijan el vector: MRNGReversal.HasSeeds comprueba que exista alguna semilla.
            if (!MRNGReversal.HasSeeds((uint)v[0], (uint)v[1], (uint)v[2], (uint)v[3], (uint)v[4], (uint)v[5]))
                continue;
            attempts++;
            var cand = BuildIvsOnly(pk, v);
            if (!StatOptimizer.QuickValid(cand, r.Dest, pk)) continue;
            if (StatOptimizer.Analyze(cand, r.Dest).Info.PIDIV.Type != type) continue;
            return new SubResult(cand, "Enumeration", true, null, name);
        }
        return new SubResult(null, "Enumeration", true,
            "Ningún vector de IV por encima de los actuales tiene semilla del MRNG del Rancho: los actuales ya son los mejores posibles. Referencia técnica: IndividualValueVerifier.cs:95-105, MRNGReversal.HasSeeds.", name);
    }

    // ------------------------------------------------------------------------------------------------ utilidades

    private static (int[] Cur, int CurSum, int[][] Allowed, int[] Relevance) Domain(PKM pk)
    {
        var cur = PkmFieldsAccess.IVs(pk);
        var knowsHp = StatOptimizer.KnowsHiddenPower(pk);
        var max = pk.MaxIV;
        var allowed = new int[6][];
        for (var i = 0; i < 6; i++)
            allowed[i] = knowsHp && (cur[i] & 1) == 0 ? IvEnumerator.Range(max - 1, 0, 2) : knowsHp ? IvEnumerator.Range(max, 1, 2) : IvEnumerator.Range(max);
        return (cur, cur.Sum(), allowed, StatPriority.MostRelevantFirst(pk));
    }

    internal static PKM Build(PKM pk, uint pid, int[] iv, int bit)
    {
        var c = pk.Clone();
        c.PID = pid;
        if (c.EncryptionConstant != pid) c.EncryptionConstant = pid;
        c.IV_HP = iv[0]; c.IV_ATK = iv[1]; c.IV_DEF = iv[2]; c.IV_SPA = iv[3]; c.IV_SPD = iv[4]; c.IV_SPE = iv[5];
        if (bit >= 0) c.RefreshAbility(bit);
        c.RefreshChecksum();
        if (c.PartyStatsPresent) c.SetStats(c.GetStats(c.PersonalInfo));
        return c;
    }

    internal static PKM BuildIvsOnly(PKM pk, int[] iv)
    {
        var c = pk.Clone();
        c.IV_HP = iv[0]; c.IV_ATK = iv[1]; c.IV_DEF = iv[2]; c.IV_SPA = iv[3]; c.IV_SPD = iv[4]; c.IV_SPE = iv[5];
        c.RefreshChecksum();
        if (c.PartyStatsPresent) c.SetStats(c.GetStats(c.PersonalInfo));
        return c;
    }

    internal static int FastSum(uint origin)
    {
        var s = XDRNG.Next(origin);
        var a = (s >> 16) & 0x7FFF;
        s = XDRNG.Next(s);
        var b = (s >> 16) & 0x7FFF;
        return Five(a) + Five(b);
    }

    private static int Five(uint x) => (int)((x & 31) + ((x >> 5) & 31) + ((x >> 10) & 31));

    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;
}

/// <summary>
/// Reglas de una semilla de origen del XDRNG (cuadro de llamadas «IV, IV, habilidad, PID, PID», MethodCXD.cs:452 y MethodFinder.cs:254-300): derivan el PID y el bit de
/// habilidad y comprueban que se conservan naturaleza, sexo, variocolor y habilidad del Pokemon original.
/// </summary>
internal sealed class CxdRules
{
    private readonly uint _id32;
    private readonly bool _noShiny, _wantShiny;
    private readonly uint _nature;
    private readonly byte _gender;
    private readonly byte _ratio;
    private readonly int _ability, _ability1, _ability2;

    public CxdRules(PKM pk, bool noShiny)
    {
        _id32 = pk.ID32;
        _noShiny = noShiny;
        _wantShiny = pk.IsShiny;
        _nature = (uint)pk.Nature;
        _gender = pk.Gender;
        _ratio = pk.PersonalInfo.Gender;
        _ability = pk.Ability;
        var ab = (IPersonalAbility12)pk.PersonalInfo;
        _ability1 = ab.Ability1;
        _ability2 = ab.Ability2;
    }

    public bool TryDerive(uint origin, out uint pid, out int bit)
    {
        var s = XDRNG.Next3(origin); // frame de la habilidad (IV, IV, habilidad)
        bit = (int)((s >> 16) & 1);
        while (true)
        {
            var a = XDRNG.Next16(ref s);
            var b = XDRNG.Next16(ref s);
            pid = (a << 16) | b;
            if (!_noShiny || !ShinyUtil.GetIsShiny3(_id32, pid)) break; // el juego relanza el PID mientras sea variocolor (GetPID, MethodCXD.cs:500-508)
        }
        if (pid % 25 != _nature) return false;
        if (EntityGender.GetFromPIDAndRatio(pid, _ratio) != _gender) return false;
        if (ShinyUtil.GetIsShiny3(_id32, pid) != _wantShiny) return false;
        return (bit == 0 ? _ability1 : _ability2) == _ability;
    }
}

/// <summary>Reglas de una semilla de IV de PokeSpot: animacion valida y nivel de captura (MethodPokeSpot.cs:137-175).</summary>
internal sealed class PokeSpotRules
{
    private readonly byte _min, _max, _metLevel;
    private readonly bool _original, _checkAbility;
    private readonly int _ability, _ability1, _ability2;

    public PokeSpotRules(PKM pk, EncounterSlot3XD slot, bool checkAbility = true)
    {
        _min = slot.LevelMin;
        _max = slot.LevelMax;
        _metLevel = pk.MetLevel;
        _original = pk.Format == 3;
        // La habilidad de la semilla solo vale mientras el Pokemon no haya evolucionado (el Core la ignora por si cambio al evolucionar: MethodPokeSpot.cs:167-168).
        _checkAbility = checkAbility && pk.Species == slot.Species && pk.PersonalInfo is IPersonalAbility12;
        _ability = pk.Ability;
        if (pk.PersonalInfo is IPersonalAbility12 ab) { _ability1 = ab.Ability1; _ability2 = ab.Ability2; }
    }

    public bool IsValid(uint preIv)
    {
        if (!MethodPokeSpot.IsValidAnimation(XDRNG.Prev6(preIv), out _, out _)) return false;
        var delta = 1u + _max - _min;
        var lvl = (byte)(_min + ((XDRNG.Prev2(preIv) >> 16) % delta));
        if (_original ? _metLevel != lvl : _metLevel < lvl) return false;
        if (!_checkAbility) return true;
        var bit = (XDRNG.Next3(preIv) >> 16) & 1;
        return (bit == 0 ? _ability1 : _ability2) == _ability;
    }
}

/// <summary>Modelo de semilla CXD (2^32 semillas del XDRNG) para <see cref="SeedSpaceSearch"/>: sombras, estaticos e intercambios de XD.</summary>
internal sealed class CxdSeedModel(PKM pk, IShadow3? shadow, bool noShiny, string name) : ISeedModel
{
    private readonly CxdRules _rules = new(pk, noShiny);
    public string Name => name;
    public bool IsFullDomain => true;

    public bool Apply(PKM scratch, ulong seed)
    {
        var origin = (uint)seed;
        if (!_rules.TryDerive(origin, out var pid, out var bit)) return false;
        scratch.PID = pid;
        if (shadow is not null && shadow.PartyPrior.Length != 0 && !LockFinder.IsAllShadowLockValid(shadow, origin, scratch)) return false;
        var s = origin;
        var iv1 = XDRNG.Next15(ref s);
        var iv2 = XDRNG.Next15(ref s);
        scratch.IV_HP = (int)(iv1 & 31); scratch.IV_ATK = (int)((iv1 >> 5) & 31); scratch.IV_DEF = (int)((iv1 >> 10) & 31);
        scratch.IV_SPE = (int)(iv2 & 31); scratch.IV_SPA = (int)((iv2 >> 5) & 31); scratch.IV_SPD = (int)((iv2 >> 10) & 31);
        scratch.RefreshAbility(bit);
        return true;
    }

    public int FastIvSum(ulong seed) => GameCubeSearch.FastSum((uint)seed);
}

/// <summary>Modelo de semilla de IV de PokeSpot (2^32 estados): el PID no cambia.</summary>
internal sealed class PokeSpotSeedModel(PKM pk, EncounterSlot3XD slot) : ISeedModel
{
    private readonly PokeSpotRules _rules = new(pk, slot);
    public string Name => "GameCubeSeedSearch/PokeSpot";
    public bool IsFullDomain => true;

    public bool Apply(PKM scratch, ulong seed)
    {
        var preIv = (uint)seed;
        if (!_rules.IsValid(preIv)) return false;
        var s = preIv;
        var iv1 = XDRNG.Next15(ref s);
        var iv2 = XDRNG.Next15(ref s);
        scratch.IV_HP = (int)(iv1 & 31); scratch.IV_ATK = (int)((iv1 >> 5) & 31); scratch.IV_DEF = (int)((iv1 >> 10) & 31);
        scratch.IV_SPE = (int)(iv2 & 31); scratch.IV_SPA = (int)((iv2 >> 5) & 31); scratch.IV_SPD = (int)((iv2 >> 10) & 31);
        return true;
    }

    public int FastIvSum(ulong seed) => GameCubeSearch.FastSum((uint)seed);
}
