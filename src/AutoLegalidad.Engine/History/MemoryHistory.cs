// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Engine.Changes;
using AutoLegalidad.Engine.Context;
using AutoLegalidad.Engine.Validation;
using PKHeX.Core;

namespace AutoLegalidad.Engine.History;

/// <summary>
/// R-010 (ADR 0042): recuerdos (memorias) del entrenador original (OT) y del anterior (HT) como los pondria el juego, no un recuerdo vacio ni el mismo siempre. Todo sale de datos del Core:
/// <list type="bullet">
/// <item>OT, capturado por el propio entrenador: recuerdo 1 («conocio a {1} en {2} y le lanzo una Poke Ball») en Gen6 y Gen8 (SWSH); si viene de un regalo, 3 («conocio a {1} en {2}»); si eclosiono de un huevo en Gen8, 2.
/// El Core exige un recuerdo de OT en Gen6 (<c>MemoryVerifier.VerifyOTMemory</c>: 0 es «MemoryMissingOT» invalido) y lo marca dudoso en Gen8; el generador de Gen6 pone siempre el 63 (<c>SetRandomMemory6</c>: «for lack of better randomization»), el de Gen8 ninguno.</item>
/// <item>HT, entrenador anterior con el que el Pokemon ya convive (Gen8 SWSH): al entrar en una partida ajena <c>PKH.UpdateHandler</c> borra los recuerdos de HT y deja su asignacion «al juego» (SW/SH no los pone al momento), asi que
/// el juego los da DESPUES, por azar con las probabilidades de su tabla (<see cref="Gen8RandChance"/>). Se elige uno con esa tabla entre los que no llevan argumentos propios del Pokemon (sin objeto, movimiento ni especie), y siempre lo valida el Core.</item>
/// </list>
/// Determinista: el azar sale del PID, la EC y la especie, asi que repetir la operacion da el mismo recuerdo. Nunca cambia un recuerdo que el Pokemon ya tenga (salvo el 63 de relleno del generador de Gen6 en un Pokemon que el motor construye).
/// </summary>
internal static class MemoryHistory
{
    /// <summary>
    /// Probabilidad (sobre 100) con la que el juego asigna cada recuerdo (Gen6; Gen8 igual hasta el 69 y propia de SWSH del 70 al 89). Fuente: <c>MemoryContext8Data.MemoryRandChance</c> (bloque <c>#if FALSE</c> del Core, comentado
    /// con el analisis del juego) y <c>MemoryContext6Data.MemoryRandChance</c>.
    /// </summary>
    internal static readonly byte[] Gen8RandChance =
    [
        000, 100, 100, 100, 100, 005, 005, 005, 005, 005,
        005, 005, 005, 005, 010, 020, 010, 001, 050, 030,
        005, 005, 020, 005, 005, 005, 001, 050, 100, 050,
        050, 002, 002, 005, 005, 005, 005, 005, 005, 002,
        020, 020, 005, 010, 001, 001, 050, 030, 020, 020,
        010, 010, 001, 010, 001, 050, 030, 030, 030, 002,
        050, 020, 020, 020, 020, 010, 010, 050, 020, 005,
        005, 010, 010, 020, 020, 010, 100, 010, 005, 010,
        010, 010, 010, 010, 010, 010, 010, 001, 001, 001,
    ];

    private const byte MaxIntensity = 7;

    private enum OtKind { None, Caught, Gift, Hatched }

    private static Random RngFor(PKM pk) => new(unchecked((int)(pk.PID * 2654435761u ^ pk.EncryptionConstant ^ (uint)pk.Species * 40503u)));

    private static bool OtMemoryEmpty(IMemoryOT m) => m.OriginalTrainerMemory == 0 && m.OriginalTrainerMemoryVariable == 0 && m.OriginalTrainerMemoryIntensity == 0 && m.OriginalTrainerMemoryFeeling == 0;

    /// <summary>El 63 de relleno del generador de Gen6 (<c>SetRandomMemory6</c>: variable 0 e intensidad al maximo).</summary>
    private static bool IsGen6Placeholder(IMemoryOT m) => m.OriginalTrainerMemory == 63 && m.OriginalTrainerMemoryVariable == 0 && m.OriginalTrainerMemoryIntensity == MaxIntensity;

    private static OtKind ExpectedOt(PKM pk, IEncounterTemplate enc)
    {
        if (pk.Format < 6 || pk.IsEgg || pk is not IMemoryOT) return OtKind.None;
        if (enc is MysteryGift or IMemoryOTReadOnly) return OtKind.None;   // la tarjeta o la plantilla fijan el recuerdo
        switch (enc.Context)
        {
            case EntityContext.Gen6:
                return enc.IsEgg ? OtKind.None : OtKind.Caught;   // el huevo de Gen6 ya trae su recuerdo de eclosion (MemoryApplicator.SetHatchMemory6)
            case EntityContext.Gen8 when enc.Version is GameVersion.SW or GameVersion.SH or GameVersion.SWSH:
                if (pk.GO_HOME || pk.MetLocation == Locations.HOME8) return OtKind.None;   // HOME no pone recuerdos (MemoryVerifier.CanHaveMemoryForOT)
                if (enc.IsEgg) return OtKind.Hatched;
                return enc is EncounterStatic8 { Gift: true } or EncounterTrade8 ? OtKind.Gift : OtKind.Caught;
            default:
                return OtKind.None;
        }
    }

    /// <summary>Copia con el recuerdo de OT que daria el juego, o null si no hay nada que poner (ya lo tiene, o el origen no da recuerdos).</summary>
    internal static PKM? PlanOt(PKM pk, IEncounterTemplate enc)
    {
        var kind = ExpectedOt(pk, enc);
        if (kind == OtKind.None) return null;
        var m = (IMemoryOT)pk;
        var placeholder = enc.Context == EntityContext.Gen6 && IsGen6Placeholder(m);
        if (!OtMemoryEmpty(m) && !placeholder) return null;

        var copy = pk.Clone();
        var rng = RngFor(copy);
        var mem = (IMemoryOT)copy;
        byte id = kind switch { OtKind.Caught => 1, OtKind.Gift => 3, _ => 2 };
        ushort variable;
        byte feeling;
        if (enc.Context == EntityContext.Gen6)
        {
            variable = copy.MetLocation;
            feeling = MemoryContext6.GetRandomFeeling6(id);
        }
        else
        {
            var candidates = new List<ushort>();
            for (ushort v = 1; v < 256; v++)
                if (MemoryContext8.IsGeneralLocation8((byte)v) && !MemoryContext8.Instance.IsInvalidGeneralLocationMemoryValue(id, v, enc, copy)) candidates.Add(v);
            if (candidates.Count == 0) return null;
            variable = candidates[rng.Next(candidates.Count)];
            feeling = MemoryContext8.GetRandomFeeling8(id);
        }
        var min = enc.Context == EntityContext.Gen6 ? MemoryContext6.GetMinimumIntensity6(id) : MemoryContext8.GetMinimumIntensity8(id);
        mem.OriginalTrainerMemory = id;
        mem.OriginalTrainerMemoryVariable = variable;
        mem.OriginalTrainerMemoryFeeling = feeling;
        mem.OriginalTrainerMemoryIntensity = (byte)rng.Next(Math.Max(min, (byte)1), MaxIntensity + 1);
        return copy;
    }

    private static bool HtMemoryEmpty(IMemoryHT m) => m.HandlingTrainerMemory == 0 && m.HandlingTrainerMemoryVariable == 0 && m.HandlingTrainerMemoryIntensity == 0 && m.HandlingTrainerMemoryFeeling == 0;

    private static bool WantsHtMemory(PKM pk) => pk is PK8 && !pk.IsEgg && pk.CurrentHandler == 1 && pk.HandlingTrainerName.Length != 0 && pk is IMemoryHT h && HtMemoryEmpty(h)
        && !pk.GO_HOME && pk.MetLocation != Locations.HOME8;

    /// <summary>
    /// Candidatos de recuerdo de HT para Gen8, ya ordenados al azar ponderado por la tabla del juego: solo los que no llevan argumento propio del Pokemon (ninguno o un lugar general) y que el Core admite como HT.
    /// El llamador prueba cada uno con la validacion completa y se queda con el primero que pasa.
    /// </summary>
    internal static IEnumerable<PKM> HtCandidates(PKM pk, Random rng)
    {
        if (!WantsHtMemory(pk)) yield break;
        var mc = MemoryContext8.Instance;
        var pool = new List<(byte Id, double Weight)>();
        for (byte id = 4; id < Gen8RandChance.Length; id++)
        {
            if (!mc.CanObtainMemoryHT(pk.Version, id)) continue;
            var type = Memories.GetMemoryArgType(id, 8);
            if (type is not (MemoryArgType.None or MemoryArgType.GeneralLocation or MemoryArgType.SpecificLocation)) continue;
            pool.Add((id, Gen8RandChance[id]));
        }
        var locations = Enumerable.Range(1, 255).Where(v => MemoryContext8.IsGeneralLocation8((byte)v) && v != 79).Select(v => (ushort)v).ToArray();
        while (pool.Count > 0)
        {
            // Sorteo ponderado sin reposicion.
            var total = pool.Sum(p => p.Weight);
            var roll = rng.NextDouble() * total;
            var at = 0;
            for (; at < pool.Count - 1; at++) { roll -= pool[at].Weight; if (roll < 0) break; }
            var id = pool[at].Id;
            pool.RemoveAt(at);
            var type = Memories.GetMemoryArgType(id, 8);
            var tries = type == MemoryArgType.None ? 1 : 6;
            for (var t = 0; t < tries; t++)
            {
                var copy = pk.Clone();
                var ht = (IMemoryHT)copy;
                ht.HandlingTrainerMemory = id;
                ht.HandlingTrainerMemoryVariable = type == MemoryArgType.None ? (ushort)0 : locations[rng.Next(locations.Length)];
                var min = MemoryContext8.GetMinimumIntensity8(id);
                ht.HandlingTrainerMemoryIntensity = (byte)rng.Next(Math.Max(min, (byte)1), MaxIntensity + 1);
                ht.HandlingTrainerMemoryFeeling = MemoryContext8.GetRandomFeeling8(id);
                yield return copy;
            }
        }
    }

    /// <summary>Primer candidato de HT que el validador acepta, o null.</summary>
    internal static PKM? PlanHt(PKM pk, Func<PKM, bool> valid)
    {
        if (!WantsHtMemory(pk)) return null;
        var rng = RngFor(pk);
        var budget = 40;
        foreach (var c in HtCandidates(pk, rng))
        {
            if (valid(c)) return c;
            if (--budget == 0) break;
        }
        return null;
    }

    /// <summary>Paso de la fase «Historia» para el recuerdo de OT y, si hay entrenador anterior, el de HT. Cada uno se acepta solo si el Core sigue dando el Pokemon por valido.</summary>
    internal static void Apply(ref PKM cur, ref ValidationOutcome curOutcome, HistoryContext ctx)
    {
        ApplyOt(ref cur, ref curOutcome, ctx);
        ApplyHt(ref cur, ref curOutcome, ctx);
    }

    internal static void ApplyOt(ref PKM cur, ref ValidationOutcome curOutcome, HistoryContext ctx)
    {
        if (cur.Format < 6) return;
        var la = HistoryPhase.Analyze(cur, ctx.Dest);
        if (!la.Parsed) return;
        var enc = la.EncounterMatch;
        var plan = PlanOt(cur, enc);
        if (plan is null) return;
        var before = Describe((IMemoryOT)cur);
        HistoryPhase.TryStep(ref cur, ref curOutcome, ctx, pk =>
        {
            ((IMemoryOT)pk).OriginalTrainerMemory = ((IMemoryOT)plan).OriginalTrainerMemory;
            var o = (IMemoryOT)pk; var p = (IMemoryOT)plan;
            o.OriginalTrainerMemoryVariable = p.OriginalTrainerMemoryVariable;
            o.OriginalTrainerMemoryIntensity = p.OriginalTrainerMemoryIntensity;
            o.OriginalTrainerMemoryFeeling = p.OriginalTrainerMemoryFeeling;
            return (ReasonCodes.HistoryTrainer, "Recuerdo del entrenador original", before, Describe(o),
                "recuerdo propio de un Pokémon capturado por su entrenador, como lo habría dado el juego (no vacío ni el mismo para todos).", 1);
        });
    }

    internal static void ApplyHt(ref PKM cur, ref ValidationOutcome curOutcome, HistoryContext ctx)
    {
        if (!WantsHtMemory(cur)) return;
        var plan = PlanHt(cur, c => ctx.Validator.Validate(c, ctx.Original, ctx.Dest, ctx.RequireCatalogGate).Valid);
        if (plan is null) return;
        var h = (IMemoryHT)plan;
        HistoryPhase.TryStep(ref cur, ref curOutcome, ctx, pk =>
        {
            var t = (IMemoryHT)pk;
            t.HandlingTrainerMemory = h.HandlingTrainerMemory;
            t.HandlingTrainerMemoryVariable = h.HandlingTrainerMemoryVariable;
            t.HandlingTrainerMemoryIntensity = h.HandlingTrainerMemoryIntensity;
            t.HandlingTrainerMemoryFeeling = h.HandlingTrainerMemoryFeeling;
            return (ReasonCodes.HistoryTrainer, "Recuerdo del entrenador anterior", "ninguno", $"recuerdo {h.HandlingTrainerMemory}",
                "recuerdo con el entrenador actual, del tipo que el juego da con el tiempo (al entrar en la partida de otro entrenador el juego lo deja vacío y lo asigna después, por azar).", 1);
        });
    }

    private static string Describe(IMemoryOT m) => m.OriginalTrainerMemory == 0 ? "ninguno" : $"recuerdo {m.OriginalTrainerMemory}";

    /// <summary>Incidencia de «recuerdo vacio donde el juego real habria puesto uno» (<see cref="PlausibilityCodes.MemoryEmpty"/>). Solo se senala lo que Pokekeep podria poner y el Core aceptaria.</summary>
    internal static void Analyze(PKM pk, LegalityAnalysis la, Destination dest, List<PlausibilityIssue> issues)
    {
        var enc = la.EncounterMatch;
        if (PlanOt(pk, enc) is { } ot && HistoryPhase.Analyze(ot, dest).Valid && ((IMemoryOT)pk) is var cur && OtMemoryEmpty(cur))
            issues.Add(new PlausibilityIssue(PlausibilityCodes.MemoryEmpty, "recuerdo del entrenador original vacío en un Pokémon que el juego habría capturado con recuerdo"));
        if (WantsHtMemory(pk) && PlanHt(pk, c => HistoryPhase.Analyze(c, dest).Valid) is not null)
            issues.Add(new PlausibilityIssue(PlausibilityCodes.MemoryEmpty, "recuerdo del entrenador anterior vacío con un entrenador anterior que ya convive con el Pokémon"));
    }
}
