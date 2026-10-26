// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Engine.Context;
using AutoLegalidad.Engine.Infra;
using PKHeX.Core;

namespace AutoLegalidad.Engine.Generation;

/// <summary>
/// Hito 3b: generacion DIRECTA desde los encuentros que el propio Core enumera para la especie y forma pedidas (<c>EncounterMovesetGenerator.GenerateEncounters</c>), sin depender de
/// que el analisis oficial haya reconocido un encuentro en la entidad del usuario (el caso de un huevo o un Pokemon construido en blanco, donde no hay <c>OriginalEncounter</c>).
/// Cada encuentro se convierte con <c>ConvertToPKM</c> y criterios, se evoluciona hasta la especie y forma pedidas (arbol de evolucion del Core) y solo se ofrece lo que YA tiene esa
/// especie y forma. Va DESPUES de ALM: solo trabaja cuando las demas estrategias no han dado un candidato valido en el escalon. Nunca decide el exito (lo decide el validador oficial).
/// </summary>
public sealed class CoreEnumeratedStrategy : IGenerationStrategy
{
    private const int MaxEncounters = 800;
    /// <summary>Encuentros del mismo tipo, especie y forma que se prueban (las areas de un mismo salvaje dan el mismo resultado): cubre la enumeracion entera del Core sin repetir.</summary>
    private const int PerGroup = 3;

    public string Name => "CoreEnumerated";

    public IEnumerable<PKM> Generate(GenerationContext ctx)
    {
        var wish = ctx.Original;
        List<IEncounterable> encounters;
        lock (EngineGlobalState.Gate)
        {
            ctx.Dest.ApplyParseSettings();
            var blank = ctx.Dest.Sav.BlankPKM;
            blank.Species = wish.Species;
            blank.Form = wish.Form;
            blank.Gender = blank.GetSaneGender();
            var groups = new Dictionary<(Type, ushort, byte), int>();
            encounters = [.. CoreVersions.Encounters(blank, ctx.Dest.Trainer, ReadOnlyMemory<ushort>.Empty)
                .Where(e => { var k = (e.GetType(), e.Species, e.Form); groups.TryGetValue(k, out var n); groups[k] = n + 1; return n < PerGroup; })
                .Take(MaxEncounters)];
        }
        var criteria = CoreTemplateStrategy.BuildCriteria(ctx);
        foreach (var enc in encounters)
        {
            if (ctx.Expired) { ctx.Report.TimedOut = DateTime.UtcNow >= ctx.DeadlineUtc; yield break; }
            if (enc is EncounterInvalid || enc is not IEncounterConvertible conv) continue;
            // Un origen que exige un rastreador de HOME no se puede crear (regla 6): ni se intenta.
            if (HomeTrackerRule.IsRequired(enc, ctx.Dest.Context) && ctx.Dest.TrackerSeverity != Severity.Fishy) continue; // R-009: con el ajuste explicito (Fishy) tambien se prueban las rutas por HOME (rastreador 0)
            for (var attempt = 0; attempt < CoreTemplateStrategy.Attempts(ctx.MaxAttempts, wish); attempt++)
            {
                if (ctx.Expired) yield break;
                PKM? basePk;
                lock (EngineGlobalState.Gate)
                {
                    ctx.Dest.ApplyParseSettings();
                    basePk = CoreTemplateStrategy.Create(conv, ctx, criteria);
                }
                if (basePk is null) continue;
                basePk = CoreTemplateStrategy.EvolveToWish(basePk, ctx, conv);
                if (basePk.Species != wish.Species || basePk.Form != wish.Form) break; // este encuentro no llega a la especie/forma pedida
                if (CoreTemplateStrategy.NatureFormMismatch(basePk)) continue;
                ctx.Report.Produced++;
                foreach (var v in CoreTemplateStrategy.Variants(basePk, ctx)) yield return v;
                // Una evolucion por nivel exige un nivel minimo que el usuario puede no tener (un Feraligatr de nivel 20): se ofrecen tambien niveles que la cumplen (36 y 100).
                if (basePk.Species != enc.Species)
                {
                    foreach (var level in new byte[] { 36, 100 })
                    {
                        if (level <= basePk.CurrentLevel) continue;
                        var up = basePk.Clone();
                        up.CurrentLevel = level;
                        if (up.PartyStatsPresent) up.SetStats(up.GetStats(up.PersonalInfo));
                        foreach (var v in CoreTemplateStrategy.Variants(up, ctx)) yield return v;
                    }
                }
            }
        }
    }
}
