// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Engine.Generation;
using AutoLegalidad.Engine.Infra;
using PKHeX.Core;

namespace AutoLegalidad.Engine.Optimization;

/// <summary>
/// PID/IV correlacionados que <see cref="ClassicLcrngSearch"/> no cubre (metodos 3, Colosseum/XD, Canal, cria, eventos...): se pide al generador del PROPIO
/// encuentro (el que conoce fotogramas, bloqueos de equipo y reglas especiales) que construya un Pokemon con los IV de cada vector, enumerados por suma
/// descendente, y con la misma naturaleza, sexo, variocolor y habilidad. Solo se trasplantan al Pokemon original los campos ligados a la semilla (PID, IV,
/// habilidad, sexo, forma de Unown): el origen queda intacto. Presupuesto acotado: si se agota, el veredicto NO es «sin margen», es «no encontrado».
/// </summary>
internal static class CriteriaRegenOptimizer
{
    private const int MaxVectors = 3000;
    private const int MaxVectorsSeeded = 600;
    private static readonly TimeSpan MaxTime = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan MaxTimeSeeded = TimeSpan.FromSeconds(8);

    public static SubResult Run(OptimizeRequest r, PKM pk, LegalityAnalysis la, ref int attempts, bool seeded = false)
    {
        const string Name = "CoreCriteriaSearch";
        if (FixedIvSets.Specified(la.EncounterMatch))
            return new SubResult(null, "FixedIVs", true, $"Este encuentro ({la.EncounterMatch.GetType().Name}) define IV fijos: no hay margen de mejora.", Name);
        if (la.EncounterMatch is not IEncounterConvertible conv)
            return new SubResult(null, "BudgetExhausted", false, "El encuentro no admite regeneración por criterios.", Name);

        var enc = la.EncounterMatch;
        var type = la.Info.PIDIV.Type;
        var cur = PkmFieldsAccess.IVs(pk);
        var curSum = cur.Sum();
        var max = pk.MaxIV;
        var knowsHp = StatOptimizer.KnowsHiddenPower(pk);

        // Entrenador con los datos del ORIGINAL (el variocolor depende de TID/SID y el generador los toma del entrenador que recibe).
        var tr = new SimpleTrainerInfo(pk.Version)
        {
            OT = pk.OriginalTrainerName,
            TID16 = pk.TID16,
            SID16 = pk.SID16,
            Gender = pk.OriginalTrainerGender,
            Language = pk.Language,
        };

        var isWild = enc.GetType().Name.StartsWith("EncounterSlot", StringComparison.Ordinal);
        var baseCriteria = new EncounterCriteria
        {
            Gender = pk.Gender <= 1 ? (Gender)pk.Gender : Gender.Random,
            Ability = pk.AbilityNumber switch { 1 => AbilityPermission.OnlyFirst, 2 => AbilityPermission.OnlySecond, 4 => AbilityPermission.OnlyHidden, _ => AbilityPermission.Any12H },
            Nature = pk.Nature,
            Shiny = pk.IsShiny ? Shiny.Always : Shiny.Never,
            LevelMin = isWild ? pk.MetLevel : (byte)0,
            LevelMax = isWild ? pk.MetLevel : (byte)0,
            Form = pk.Species == (ushort)Species.Unown ? (sbyte)pk.Form : (sbyte)-1,
            HiddenPowerType = knowsHp ? (sbyte)pk.HPType : (sbyte)-1,
        };

        var allowed = new int[6][];
        for (var i = 0; i < 6; i++)
            allowed[i] = knowsHp && (cur[i] & 1) == 0 ? IvEnumerator.Range(max - 1, 0, 2) : knowsHp ? IvEnumerator.Range(max, 1, 2) : IvEnumerator.Range(max);
        var relevance = StatPriority.MostRelevantFirst(pk);

        var limit = DateTime.UtcNow + (seeded ? MaxTimeSeeded : MaxTime);
        var maxVectors = seeded ? MaxVectorsSeeded : MaxVectors;
        var tried = 0;
        foreach (var v in IvEnumerator.BySumDescending(allowed, relevance, curSum))
        {
            if (r.Cancellation.IsCancellationRequested || DateTime.UtcNow >= r.DeadlineUtc || DateTime.UtcNow >= limit || ++tried > maxVectors)
                return new SubResult(null, "BudgetExhausted", false,
                    $"Se agotó el presupuesto del generador de PKHeX ({tried - 1:N0} vectores de IV) sin hallar una mejora que conserve naturaleza, sexo, variocolor y habilidad dentro del mismo método.", Name);
            attempts++;
            var criteria = baseCriteria with
            {
                IV_HP = (sbyte)v[0], IV_ATK = (sbyte)v[1], IV_DEF = (sbyte)v[2], IV_SPA = (sbyte)v[3], IV_SPD = (sbyte)v[4], IV_SPE = (sbyte)v[5],
            };
            PKM? gen;
            try
            {
                // Algunos generadores del Core no terminan con criterios imposibles (errantes de Gen3...): llamada con plazo (GuardedCore).
                if (!GuardedCore.TryCall(() => conv.ConvertToPKM(tr, criteria), TimeSpan.FromMilliseconds(400), out gen))
                    return new SubResult(null, "BudgetExhausted", false, $"El generador de PKHeX no termina para este encuentro ({enc.GetType().Name}) con IV pedidos: este método no admite esa combinación; no se pudo optimizar.", Name);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException) { continue; }
            if (gen is null) continue;

            // El generador cae a un resultado aleatorio si no encuentra semilla: se exige que cumpla EXACTAMENTE lo pedido.
            if (!v.AsSpan().SequenceEqual(PkmFieldsAccess.IVs(gen))) continue;
            if (gen.Nature != pk.Nature || gen.Gender != pk.Gender || gen.IsShiny != pk.IsShiny || gen.Ability != pk.Ability) continue;
            if (pk.Species == (ushort)Species.Unown && gen.Form != pk.Form) continue;
            if (knowsHp && gen.HPType != pk.HPType) continue;

            if (seeded && pk is ITeraType tpk && gen is ITeraType tgen && tpk.TeraTypeOriginal != tgen.TeraTypeOriginal) continue; // el Teratipo original es parte del encuentro
            var cand = Transplant(pk, gen, seeded);
            if (!StatOptimizer.QuickValid(cand, r.Dest, pk)) continue;
            if (StatOptimizer.Analyze(cand, r.Dest).Info.PIDIV.Type != type) continue; // mismo metodo
            return new SubResult(cand, "Enumeration", false, null, Name);
        }
        return new SubResult(null, "Enumeration", true, null, Name);
    }

    /// <summary>Copia SOLO lo que fija la semilla (PID, IV, habilidad, sexo, forma) del Pokemon generado a una copia del original.</summary>
    private static PKM Transplant(PKM pk, PKM gen, bool seeded)
    {
        var c = pk.Clone();
        c.PID = gen.PID;
        if (c.EncryptionConstant != gen.EncryptionConstant) c.EncryptionConstant = gen.EncryptionConstant;
        c.IV_HP = gen.IV_HP; c.IV_ATK = gen.IV_ATK; c.IV_DEF = gen.IV_DEF;
        c.IV_SPA = gen.IV_SPA; c.IV_SPD = gen.IV_SPD; c.IV_SPE = gen.IV_SPE;
        if (c.Nature != gen.Nature) c.Nature = gen.Nature;
        if (c.Gender != gen.Gender) c.Gender = gen.Gender;
        if (c.Form != gen.Form) c.Form = gen.Form;
        if (c.Ability != gen.Ability || c.AbilityNumber != gen.AbilityNumber)
            c.RefreshAbility(gen.AbilityNumber >> 1);
        if (seeded)
        {
            // Con semillas modernas la semilla tambien fija el tamano (y la escala): se copian junto con el resto de lo que sale de ella.
            if (c is IScaledSize s1 && gen is IScaledSize s2) { s1.HeightScalar = s2.HeightScalar; s1.WeightScalar = s2.WeightScalar; }
            if (c is IScaledSize3 t1 && gen is IScaledSize3 t2) t1.Scale = t2.Scale;
        }
        c.RefreshChecksum();
        if (c.PartyStatsPresent) c.SetStats(c.GetStats(c.PersonalInfo)); // solo si el original ya guardaba stats de equipo
        return c;
    }
}
