// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Engine.Context;
using AutoLegalidad.Engine.Infra;
using AutoLegalidad.Engine.Repair;
using PKHeX.Core;

namespace AutoLegalidad.Engine.Generation;

/// <summary>
/// Hito 3b: DEMOSTRACION con el Core de que una especie y forma no se pueden obtener en un destino (para una forma distinta de la base, la relajacion de la forma, escalon
/// <c>BaseForm</c>, es entonces obligada y no un fallo de la busqueda; para la forma base, el <c>NoSolution</c> del motor pasa de «no se hallo» a «no existe»). Recorre TODOS los encuentros que el Core enumera para esa especie y forma en el destino
/// (<c>EncounterMovesetGenerator.GenerateEncounters</c>): los que exigen el rastreador de HOME (regla 6) se cuentan como bloqueados; cada uno de los demas se convierte con
/// <c>ConvertToPKM</c> varias veces, se evoluciona hasta la especie y forma pedidas (arbol de evolucion del Core) y se analiza con el analisis oficial. La forma es INALCANZABLE si
/// ningun candidato resulta valido y TODOS los fallos son estructurales (el Core no encuentra ningun encuentro para la entidad o rechaza la forma/evolucion/sexo), es decir, no
/// dependen del PID, los IV ni los movimientos; cualquier otro fallo impide afirmar nada.
/// </summary>
public static class FormReachability
{
    public sealed record Verdict(bool Unreachable, int Encounters, int HomeBlocked, int Tried, string Evidence);

    /// <param name="homeAllowed">
    /// True: los encuentros que exigen la matrícula de HOME no se cuentan como bloqueados sino que se prueban igual, con la severidad Fishy del ajuste R-009 (el rastreador sigue valiendo 0). Sirve para demostrar que una forma
    /// no existe NI con el ajuste (el Core la rechaza por motivos estructurales aunque se acepte la falta de matrícula).
    /// </param>
    public static Verdict Prove(PKM wish, Destination dest, int samplesPerEncounter = 4, bool homeAllowed = false)
    {
        var severityBefore = dest.TrackerSeverity;
        if (homeAllowed) dest.TrackerSeverity = Severity.Fishy;
        try { return ProveCore(wish, dest, samplesPerEncounter, homeAllowed); }
        finally { dest.TrackerSeverity = severityBefore; }
    }

    private static Verdict ProveCore(PKM wish, Destination dest, int samplesPerEncounter, bool homeAllowed)
    {
        List<IEncounterable> encounters;
        lock (EngineGlobalState.Gate)
        {
            dest.ApplyParseSettings();
            var blank = dest.Sav.BlankPKM;
            blank.Species = wish.Species;
            blank.Form = wish.Form;
            blank.Gender = SpeciesCategory.IsFormGenderSpecific(wish.Species) ? (byte)(wish.Form & 1) : blank.GetSaneGender();
            encounters = [.. CoreVersions.Encounters(blank, dest.Trainer, ReadOnlyMemory<ushort>.Empty)];
        }
        var blocked = 0;
        var tried = 0;
        var nonStructural = 0;
        var reasons = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var evidenceCtx = Context(wish, dest);
        foreach (var enc in encounters)
        {
            if (enc is EncounterInvalid) continue;
            if (!homeAllowed && HomeTrackerRule.IsRequired(enc, dest.Context)) { blocked++; continue; }
            if (enc is not IEncounterConvertible conv) { nonStructural++; continue; }
            for (var i = 0; i < samplesPerEncounter; i++)
            {
                PKM? pk;
                lock (EngineGlobalState.Gate)
                {
                    dest.ApplyParseSettings();
                    pk = CoreTemplateStrategy.Create(conv, evidenceCtx, EncounterCriteria.Unrestricted);
                }
                if (pk is null) { nonStructural++; break; }
                pk = CoreTemplateStrategy.EvolveToWish(pk, evidenceCtx, conv);
                tried++;
                if (pk.Species != wish.Species || pk.Form != wish.Form)
                {
                    Add(reasons, $"{enc.GetType().Name}: la especie {enc.Species} (forma {enc.Form}) no evoluciona a {wish.Species} forma {wish.Form} según el árbol de evolución de PKHeX");
                    break; // no depende de la muestra
                }
                pk = MinimalRepair.Run(pk, dest, new Changes.ChangeLog());
                if (pk.Species != wish.Species || pk.Form != wish.Form)
                {
                    // La reparacion minima solo lo deja valido CAMBIANDO la forma: el Core rechaza la forma pedida para ese encuentro (analisis oficial, check Form).
                    Add(reasons, $"{enc.GetType().Name}: PKHeX rechaza la forma {wish.Form} (comprobación «Form») y solo valida la entidad con otra forma");
                    break;
                }
                LegalityAnalysis la;
                lock (EngineGlobalState.Gate)
                {
                    dest.ApplyParseSettings();
                    la = new LegalityAnalysis(pk, dest.Personal, dest.SlotType);
                }
                if (la.Valid && la.Parsed)
                    return new Verdict(false, encounters.Count, blocked, tried, $"Alcanzable: {enc.GetType().Name} ({EncounterKeysName(enc)}) produce una entidad válida con esa especie y forma.");
                var structural = la.EncounterMatch is EncounterInvalid || la.Results.Any(r => !r.Valid && r.Identifier is CheckIdentifier.Form or CheckIdentifier.Gender or CheckIdentifier.Evolution);
                if (!structural) nonStructural++;
                var other = string.Join("+", la.Results.Where(r => !r.Valid).Select(r => r.Identifier.ToString()).Distinct().Take(4));
                Add(reasons, $"{enc.GetType().Name}: {(structural ? "PKHeX no halla encuentro, forma o evolución válidos" : "falla por otro motivo (" + other + ")")}");
            }
        }
        var unreachable = nonStructural == 0 && tried + blocked > 0;
        var detail = string.Join("; ", reasons.Select(kv => $"{kv.Key} (x{kv.Value})"));
        var evidence = unreachable
            ? $"PKHeX enumera {encounters.Count} encuentros para la especie {wish.Species} forma {wish.Form} en {dest.Version}: {blocked} exigen la matrícula (rastreador de HOME) (regla 6) y {tried} candidatos creados desde los demás fallan TODOS por motivos estructurales (sin encuentro, forma o evolución válidos): {detail}."
            : $"No se demuestra: {encounters.Count} encuentros, {blocked} bloqueados por la matrícula de HOME, {tried} candidatos, {nonStructural} fallos no estructurales. {detail}";
        return new Verdict(unreachable, encounters.Count, blocked, tried, evidence);
    }

    private static string EncounterKeysName(IEncounterable enc) => Catalog.EncounterKeys.For(enc) ?? enc.GetType().Name;

    private static void Add(IDictionary<string, int> d, string key) { d.TryGetValue(key, out var n); d[key] = n + 1; }

    private static GenerationContext Context(PKM wish, Destination dest)
    {
        var traits = Traits.From(wish);
        var step = new RelaxationStep(0, "Prueba", "Demostracion de alcanzabilidad de la forma", traits with { Level = 100 });
        return new GenerationContext
        {
            Original = wish,
            Dest = dest,
            Traits = step.Traits,
            Step = step,
            DeadlineUtc = DateTime.UtcNow.AddMinutes(2),
            Cancellation = CancellationToken.None,
        };
    }
}
