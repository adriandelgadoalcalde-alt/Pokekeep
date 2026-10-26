// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Contracts;
using AutoLegalidad.Engine.Changes;
using AutoLegalidad.Engine.Context;
using AutoLegalidad.Engine.Generation;
using AutoLegalidad.Engine.Infra;
using PKHeX.Core;

namespace AutoLegalidad.Engine.Optimization;

/// <summary>
/// Optimizacion de stats dentro de la legalidad (R-001 y accion B de R-002). Metrica fijada: maximizar la SUMA de IV; desempate por los IV
/// que afectan a las stats relevantes (<see cref="StatPriority"/>) y por indice. Nunca rompe la legalidad: cada variante se valida con el Core antes de
/// aceptarla y el resultado final pasa de nuevo por el validador independiente del motor. Conserva siempre naturaleza, sexo, variocolor y habilidad
/// (y el tipo de Poder Oculto si el Pokemon conoce ese movimiento) y los EV del usuario.
/// Estrategias: DV (Gen1/2), semilla LCRNG propia (Gen3/4 metodos 1/2/4) y generador por criterios del Core (resto de correlaciones), oraculo del Core
/// (IV libres) y Entrenamiento extremo (Gen7+). Los encuentros que generan los IV desde una semilla moderna (Gen8+) aun no se optimizan (Hito 3).
/// </summary>
public sealed class StatOptimizer : IStatOptimizer
{
    public OptimizeOutcome Optimize(OptimizeRequest r)
    {
        var pk = r.Candidate;
        var before = PkmFieldsAccess.IVs(pk);

        var la = Analyze(pk, r.Dest);
        var kind = Classify(pk, la);
        OptimizerSwitches.Trace?.Invoke($"[opt] clase={kind} encuentro={la.EncounterMatch.GetType().Name} pidiv={la.Info.PIDIV.Type}");
        var attempts = 0;
        SubResult sub;

        switch (kind)
        {
            case Kind.Gb:
                sub = GbDvOptimizer.Run(r, pk, la, ref attempts);
                break;
            case Kind.Classic:
            {
                // Errantes de Gen3 de RS/FRLG: solo se guardan 8 bits de IV (EncounterStatic3.IsRoamingTruncatedIVs); el Core los clasifica como Method_1_Roamer o, si coinciden, como
                // Method_1 con IV32 <= 0xFF (EncounterStatic3.cs:307-320). En el segundo caso el vector 6x31 jamas es valido: se recorre directamente todo el LCRNG (2^32 semillas).
                var roamer = pk is PK3 && OptimizerSwitches.SeedSpaceSearch
                             && (la.Info.PIDIV.Type == PIDType.Method_1_Roamer || la.EncounterMatch is EncounterStatic3 { IsRoamingTruncatedIVs: true });
                SubResult? own = roamer ? null : ClassicLcrngSearch.Run(r, pk, la, ref attempts); // null si el metodo no es 1/2/4 o es Unown
                if (own is null && roamer)
                    own = SeedSpaceSearch.Run(r, pk, la, ref attempts, new Roamer3Model());
                // GameCube (sombras, PokeSpot, iniciales, estaticos e intercambios de XD) y regalo del Rancho de Gen4: busqueda propia sobre el dominio finito de su RNG.
                var gameCube = false;
                if (own is null && OptimizerSwitches.SeedSpaceSearch)
                {
                    own = GameCubeSearch.Run(r, pk, la, ref attempts);
                    gameCube = own is not null;
                    if (own is null) OptimizerSwitches.Trace?.Invoke($"[opt] GameCubeSearch no aplica: entidad={pk.GetType().Name} pidiv={la.Info.PIDIV.Type} encuentro={la.EncounterMatch}");
                }
                OptimizerSwitches.Trace?.Invoke($"[opt] busqueda LCRNG propia: {(own is null ? "no aplica" : own.Basis + "/" + (own.Best is null ? "sin mejora" : "mejora"))}");
                if (own is not null && (own.Best is not null || gameCube))
                    sub = own;
                else
                {
                    // Metodo no cubierto por la busqueda propia (o sin exito): se prueba el generador por criterios del Core.
                    OptimizerSwitches.Trace?.Invoke("[opt] generador por criterios del Core");
                    var alt = CriteriaRegenOptimizer.Run(r, pk, la, ref attempts);
                    sub = alt.Best is not null || alt.Basis == "FixedIVs" ? alt : own is { Exhaustive: true } ? own : (own ?? alt);
                }
                break;
            }
            case Kind.SeededModern:
            {
                // Encuentros cuyos IV salen de una semilla (incursiones, mundo abierto...).
                // 1) Si el Core acepta IV mas altos SIN tocar la semilla (p. ej. Leyendas Arceus: su correlacion es solo informativa), ese es el resultado.
                var probe = OptimizerSwitches.OracleFirstForSeeded ? OracleOptimizer.Run(r, pk, la, ref attempts) : null;
                if (probe?.Best is not null) { sub = probe; break; }
                // 2) Busqueda exhaustiva de semillas de 32 bits (paralela, acotada y cancelable) para las familias con modelo (Hito 3, ADR 0013).
                var seedSearch = OptimizerSwitches.SeedSpaceSearch ? SeedSpaceSearch.Run(r, pk, la, ref attempts) : null;
                if (seedSearch is not null) { sub = seedSearch; break; }
                // 3) Resto: se pide al generador del propio encuentro, que busca semillas por criterios (presupuesto acotado).
                sub = CriteriaRegenOptimizer.Run(r, pk, la, ref attempts, seeded: true);
                if (sub.Best is null && sub.Basis == "BudgetExhausted" && sub.Limit is not null)
                    sub = sub with { Limit = sub.Limit + " (el tamaño, el PID y la constante de cifrado salen de la semilla y cambian con ella)" };
                break;
            }
            default:
                sub = OracleOptimizer.Run(r, pk, la, ref attempts);
                if (sub.Best is null && sub.Limit is null)
                    sub = sub with { Limit = FixedIvReason(la) ?? "PKHeX no admite IV más altos para este encuentro: el vector completo y cada IV por separado invalidan el Pokémon." };
                break;
        }

        var best = sub.Best ?? pk;

        // Entrenamiento extremo (Gen7+) solo donde el Core lo da por valido.
        var ht = HyperTrain(r, best, ref attempts);
        var htApplied = !ReferenceEquals(ht, best);
        best = ht;

        var after = PkmFieldsAccess.IVs(best);
        var improved = !before.AsSpan().SequenceEqual(after) || htApplied;

        // Guarda de origen: nada fuera de las propiedades de stats puede diferir respecto al candidato de partida.
        var unexpected = OriginGuard.UnexpectedChanges(pk, best, kind == Kind.SeededModern ? OriginGuard.SeedDerivedExtra : null);
        if (unexpected.Count > 0)
        {
            var rep = BuildReport(sub.Strategy, before, before, "La mejora cambiaba campos de origen (" + string.Join(", ", unexpected) + "): descartada.", false, false, "BudgetExhausted");
            return new OptimizeOutcome(pk, rep, false, OptimizeStatus.OriginGuard, attempts);
        }

        if (improved)
        {
            r.Log.ReasonFor(ChangeLog.IvFields, ReasonCodes.IvOptimized, $"{ChangeBuilder.StatWord(pk)} maximizados dentro de lo que permite el encuentro.");
            r.Log.Reason("PID", ReasonCodes.IvOptimized, "PID de la misma semilla y método que da los IV mejorados.");
            r.Log.Reason("EncryptionConstant", ReasonCodes.IvOptimized, "Constante de cifrado de la misma semilla y método.");
            r.Log.ReasonFor(["HeightScalar", "WeightScalar", "Scale"], ReasonCodes.IvOptimized, "El tamaño sale de la misma semilla que los IV y cambia con ella.");
            if (r.PreserveOrigin)
            {
                // En «Optimizar stats» todo lo que cambie sin razon propia es consecuencia de la semilla/metodo nuevos, nunca una reconstruccion de origen.
                r.Log.DefaultCode = ReasonCodes.IvOptimized;
                r.Log.DefaultText = "Cambia junto con los IV (misma semilla y método del mismo encuentro).";
            }
        }

        var maxSum = pk.MaxIV * 6;
        var limit = improved ? (after.Sum() >= maxSum ? null : sub.Limit ?? $"No todos los {ChangeBuilder.StatWord(pk)} pueden ser {pk.MaxIV} en este encuentro: se aplica la mejor combinación legal.") : sub.Limit;
        var rpt = BuildReport(sub.Strategy, before, after, limit, htApplied, sub.Exhaustive, sub.Basis);
        var status = improved || sub.Basis != "BudgetExhausted" ? OptimizeStatus.Done : OptimizeStatus.NoImprovementFound;
        return new OptimizeOutcome(best, rpt, improved, status, attempts);
    }

    private static OptimizationReport BuildReport(string strategy, int[] before, int[] after, string? limit, bool ht, bool exhaustive, string basis) => new()
    {
        Strategy = strategy,
        IvsBefore = before,
        IvsAfter = after,
        SumBefore = before.Sum(),
        SumAfter = after.Sum(),
        LimitReason = limit,
        ExtremeTrainingApplied = ht,
        Exhaustive = exhaustive,
        Basis = basis,
    };

    private static string? FixedIvReason(LegalityAnalysis la)
    {
        var enc = la.EncounterMatch;
        if (FixedIvSets.Specified(enc))
            return $"Este encuentro ({enc.GetType().Name}) define IV fijos: no hay margen de mejora.";
        return null;
    }

    private enum Kind { Gb, Classic, SeededModern, Oracle }

    private static Kind Classify(PKM pk, LegalityAnalysis la)
    {
        if (pk.Format <= 2) return Kind.Gb;
        var type = la.Info.PIDIV.Type;
        // El LCRNG propio resuelve estos metodos aunque el encuentro tambien implemente «semilla» (regalos y huevos de evento de Gen3: IGenerateSeed32).
        if (type is PIDType.BACD or PIDType.Method_1 or PIDType.Method_2 or PIDType.Method_4) return Kind.Classic;
        if (type is PIDType.Xoroshiro or PIDType.Overworld8 or PIDType.Roaming8b or PIDType.Tera9) return Kind.SeededModern;
        if (la.EncounterMatch is IGenerateSeed64 or IGenerateSeed32 && type != PIDType.None) return Kind.SeededModern;
        if (la.PIDIVIsCorrelated()) return Kind.Classic;
        return Kind.Oracle;
    }

    internal static LegalityAnalysis Analyze(PKM pk, Destination dest)
    {
        lock (EngineGlobalState.Gate)
        {
            dest.ApplyParseSettings();
            return new LegalityAnalysis(pk, dest.Personal, dest.SlotType);
        }
    }

    internal static bool QuickValid(PKM pk, Destination dest, PKM reference)
    {
        var la = Analyze(pk, dest);
        return la.Valid && la.Parsed && pk.Species == reference.Species && pk.Form == reference.Form;
    }

    /// <summary>
    /// Let's Go (PB7): la PC guardada (<c>Stat_CP</c>) se calcula con los IV (y el Entrenamiento extremo): <c>PB7.CalcCP</c>/<c>BaseCP</c> (PB7.cs:574-598). El Core rechaza un PB7 cuya PC almacenada no coincide con la calculada
    /// («Los PC calculados no coinciden con el valor almacenado»), asi que toda variante con IV o entrenamiento distintos debe recalcularla con <c>ResetCP</c> (PB7.cs:631); sin eso «Optimizar» nunca podia mejorar un PB7 sin IV fijos.
    /// </summary>
    internal static void RefreshDerivedFromIvs(PKM pk)
    {
        if (pk is PB7 pb7) pb7.ResetCP();
    }

    internal static bool KnowsHiddenPower(PKM pk) => pk.Move1 == (ushort)Move.HiddenPower || pk.Move2 == (ushort)Move.HiddenPower
        || pk.Move3 == (ushort)Move.HiddenPower || pk.Move4 == (ushort)Move.HiddenPower;

    private static PKM HyperTrain(OptimizeRequest r, PKM pk, ref int attempts)
    {
        if (pk is not IHyperTrain || pk.Format < 7) return pk;
        if (!pk.Context.IsHyperTrainingAvailable(pk.CurrentLevel)) return pk;
        PKM current = pk;
        var changed = false;
        foreach (var stat in StatPriority.MostRelevantFirst(pk))
        {
            if (r.Cancellation.IsCancellationRequested || DateTime.UtcNow >= r.DeadlineUtc) break;
            var iv = PkmFieldsAccess.IVs(current)[stat];
            if (iv >= 31) continue;
            var c = current.Clone();
            var h = (IHyperTrain)c;
            var already = stat switch { 0 => h.HT_HP, 1 => h.HT_ATK, 2 => h.HT_DEF, 3 => h.HT_SPA, 4 => h.HT_SPD, _ => h.HT_SPE };
            if (already) continue;
            switch (stat)
            {
                case 0: h.HT_HP = true; break;
                case 1: h.HT_ATK = true; break;
                case 2: h.HT_DEF = true; break;
                case 3: h.HT_SPA = true; break;
                case 4: h.HT_SPD = true; break;
                default: h.HT_SPE = true; break;
            }
            RefreshDerivedFromIvs(c);
            attempts++;
            if (QuickValid(c, r.Dest, pk))
            {
                current = c;
                changed = true;
            }
        }
        if (changed)
        {
            current.RefreshChecksum();
            r.Log.Note("Entrenamiento extremo aplicado donde PKHeX lo da por legal.");
            r.Log.Reason("HyperTraining", ReasonCodes.HyperTrained, "Entrenamiento extremo en las estadísticas con IV inferiores a 31 (legal en este nivel y formato).");
        }
        return current;
    }
}

/// <summary>Detecta si el PID/IV del Pokemon esta ligado por una semilla de un metodo clasico (Gen3/4, GameCube, Ranch...).</summary>
internal static class PidIvExtensions
{
    public static bool PIDIVIsCorrelated(this LegalityAnalysis la)
    {
        var t = la.Info.PIDIV.Type;
        return t is not (PIDType.None or PIDType.Pokewalker or PIDType.G5MGShiny);
    }
}
