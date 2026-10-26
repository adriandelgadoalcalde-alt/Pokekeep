// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Engine.Generation;
using AutoLegalidad.Engine.Infra;
using PKHeX.Core;
using PKHeX.Core.AutoMod;

namespace AutoLegalidad.ALMAdapter;

/// <summary>
/// Adaptador AISLADO de Auto Legality Mod (ALM) 26.10.18 (commit fijado). Ningun tipo de ALM sale de este ensamblado: la salida son <see cref="PKM"/> del Core
/// fijado y el motor los revalida SIEMPRE con el validador oficial independiente (nunca se acepta por el bool de ALM). Cada intento usa una copia nueva y un
/// registro de entrenadores limpio; los ajustes ESTATICOS de ALM se fijan antes de cada llamada (ver docs/alm-fiabilidad.md): sin easter eggs, sin comandos
/// batch, sin leer ni escribir ficheros (ni trainers\ ni error_log.txt) y sin red.
/// </summary>
public sealed class AlmGenerationStrategy : IGenerationStrategy
{
    public string Name => "ALM";

    public IEnumerable<PKM> Generate(GenerationContext ctx)
    {
        for (var attempt = 0; attempt < ctx.MaxAttempts; attempt++)
        {
            if (ctx.Expired)
            {
                ctx.Report.TimedOut = DateTime.UtcNow >= ctx.DeadlineUtc;
                yield break;
            }

            PKM? produced;
            lock (EngineGlobalState.Gate)
            {
                // Hito 3b: ALM y las sugerencias de movimientos del Core pueden lanzar con un candidato raro (LearnGroup8.cs:144 en una incursion de SWSH); un intento que lanza cuenta como
                // fallido y no aborta la operacion entera (antes: InternalError en toda la solicitud).
                try { produced = RunOnce(ctx); }
                catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException or InvalidOperationException or NullReferenceException)
                {
                    ctx.Report.Notes.Add("ALM lanzó " + ex.GetType().Name + " en este intento.");
                    produced = null;
                }
            }
            if (produced is null)
            {
                // Failed o Timeout de ALM: otro intento solo aporta algo si hay azar (Gen3/4, semillas); se reintenta hasta MaxAttempts.
                continue;
            }
            ctx.Report.Produced++;
            yield return produced;
        }
    }

    private static PKM? RunOnce(GenerationContext ctx)
    {
        AlmSettings.Apply(ctx);
        var dest = ctx.Dest;
        ITrainerInfo trainer = dest.Sav;
        RecentTrainerCache.SetRecentTrainer(dest.Sav); // ShowdownSet.Context y varias conversiones leen este estatico del Core

        var template = EntityBlank.GetBlank(trainer);
        if (template.Version == GameVersion.Any || template.Version == 0)
            template.Version = trainer.Version;
        EncounterMovesetGenerator.OptimizeCriteria(template, trainer);
        var set = ctx.Traits.ToTemplate();
        template.ApplySetDetails(set);

        ctx.Dest.ApplyParseSettings();
        var result = LegalizationResult.Failed;
        var pk = trainer.GetLegalFromTemplate(template, set, out result, ctx.OriginalEncounter);
        switch (result)
        {
            case LegalizationResult.Regenerated:
                pk.SetAllTrainerData(trainer);
                return pk;
            case LegalizationResult.Timeout:
                ctx.Report.TimedOut = true;
                ctx.Report.Notes.Add("ALM agotó su tiempo en este paso.");
                return null;
            case LegalizationResult.VersionMismatch:
                ctx.Report.Notes.Add("ALM informa de VersionMismatch.");
                return null;
            default:
                return null;
        }
    }
}

/// <summary>Ajustes estaticos de ALM, fijados de forma explicita antes de CADA llamada (son estado global del proceso).</summary>
internal static class AlmSettings
{
    public static void Apply(GenerationContext ctx)
    {
        Legalizer.EnableEasterEggs = false; // regla 3: las easter eggs pueden CAMBIAR LA ESPECIE
        APILegality.UseTrainerData = true;
        APILegality.SetMatchingBalls = true;
        APILegality.ForceSpecifiedBall = false;
        APILegality.SetAllLegalRibbons = true;
        APILegality.UseCompetitiveMarkings = false;
        APILegality.UseMarkings = true;
        // DevMode=true NO es un modo de desarrollo aqui: evita la consulta de red de ALM a api.github.com (comprobacion de version), regla 15 (nada sale del equipo).
        APILegality.EnableDevMode = true;
        APILegality.SetBattleVersion = false;
        APILegality.AllowTrainerOverride = false;
        APILegality.AllowBatchCommands = false; // sin comandos batch arbitrarios
        APILegality.ForceLevel100for50 = false;
        // PKHeX 26.10.18 anade cuatro valores guardables sin datos (WI, WA, FRX, LGX); con NewestFirst ALM los recorre PRIMERO (son los de mayor valor) y el Core lanza para WI/WA
        // (ArgumentOutOfRange por el contexto None), con lo que todo intento de ALM para un Pokemon de la era HOME fallaba. Se pide el mismo orden (el mas nuevo primero) con la lista explicita,
        // sin esos cuatro; ALM la cruza con el rango del Pokemon (FilteredGameList), asi que el resultado es el de NewestFirst en 26.08.26.
        APILegality.GameVersionPriority = GameVersionPriorityType.PriorityOrder;
        APILegality.PriorityOrder = [.. GameUtil.GameVersions.Where(static v => v is not (GameVersion.WI or GameVersion.WA or GameVersion.FRX or GameVersion.LGX)).OrderByDescending(static v => v)];
        APILegality.RandTypes = [];
        var remaining = (ctx.DeadlineUtc - DateTime.UtcNow).TotalSeconds;
        APILegality.Timeout = Math.Max(1, (int)Math.Ceiling(remaining));

        // Entrenadores: el del DESTINO para todas las versiones (nombre, TID/SID, idioma y genero de la partida), nunca los de un directorio local.
        TrainerSettings.Clear();
        var t = ctx.Dest.Info.Trainer;
        foreach (var v in GameUtil.GameVersions)
        {
            var info = new SimpleTrainerInfo(v)
            {
                OT = t.Ot,
                TID16 = (ushort)t.Tid16,
                SID16 = (ushort)t.Sid16,
                Gender = (byte)t.Gender,
                Language = t.Language,
            };
            TrainerSettings.Register(info);
        }
    }
}
