// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Engine.Changes;
using AutoLegalidad.Engine.Context;
using AutoLegalidad.Engine.Validation;
using PKHeX.Core;

namespace AutoLegalidad.Engine.Optimization;

public enum MaxLevelStatus
{
    /// <summary>Se subio el nivel (al 100 o, si el Core no admite mas, al mayor hallado).</summary>
    Changed,
    /// <summary>El nivel ya es el mayor que el Core admite para este Pokemon (100, o el limite que el Core impone).</summary>
    AlreadyMax,
    /// <summary>No aplica (huevo: su nivel es fijo).</summary>
    NotApplicable,
}

/// <param name="Candidate">Entidad resultante (la misma instancia de entrada si no hubo cambio). Siempre valida.</param>
/// <param name="Outcome">Resultado de la validacion del candidato resultante.</param>
/// <param name="Limit">Motivo literal del Core por el que no se admite el nivel siguiente (null si se llego a 100 o no aplica).</param>
/// <param name="Exhaustive">False si el plazo corto la busqueda antes de confirmar el maximo.</param>
public sealed record MaxLevelResult(MaxLevelStatus Status, PKM Candidate, ValidationOutcome Outcome, int OriginalLevel, int FinalLevel, string Message, string? Limit, bool Exhaustive = true);

/// <summary>
/// Hito 4b (R-006 y operacion «Nivel maximo legal» de R-007): sube el nivel al MAYOR (hasta 100) con el que el validador del motor (Core + destino + ida y vuelta) da el Pokemon por valido,
/// sin tocar nada mas que nivel, experiencia y estadisticas derivadas. <c>MetLevel</c> nunca cambia. Un huevo no aplica (nivel fijo). Se prueba 100; si el Core lo rechaza, 99 y despues una
/// busqueda binaria entre el nivel actual (valido) y 99 con el Core como oraculo: el resultado siempre es un nivel que el Core acaba de validar, nunca un valor supuesto.
/// </summary>
public static class MaxLevelStep
{
    public const byte MaxLevel = 100;

    /// <param name="mayRefreshMoves">
    /// True solo al GENERAR un Pokemon nuevo (Completar el juego, Libreria): si el Core rechaza el nivel por los movimientos (Leyendas Arceus exige los movimientos que da el encuentro para ese nivel)
    /// se prueba tambien con los movimientos que el propio Core sugiere para ese encuentro. Con el Pokemon de un usuario NUNCA se cambian sus movimientos: se queda en el mayor nivel que el Core admite.
    /// </param>
    public static MaxLevelResult Apply(PKM candidate, PKM original, Destination dest, IOfficialValidator validator, ValidationOutcome current, bool requireCatalogGate,
        DateTime deadlineUtc, CancellationToken ct, bool mayRefreshMoves = false)
    {
        var from = candidate.CurrentLevel;
        if (candidate.IsEgg)
            return new MaxLevelResult(MaxLevelStatus.NotApplicable, candidate, current, from, from, "Un huevo no tiene nivel que subir.", null);
        if (from >= MaxLevel)
            return new MaxLevelResult(MaxLevelStatus.AlreadyMax, candidate, current, from, from, "Nivel máximo legal: 100.", null);

        PKM? best = null;
        ValidationOutcome? bestOutcome = null;
        string? limit = null;
        var cut = false;
        var movesRefreshed = false;

        bool TryLevel(int level, out string? failure)
        {
            failure = null;
            var c = WithLevel(candidate, (byte)level, dest);
            var o = validator.Validate(c, original, dest, requireCatalogGate);
            if (!o.Valid && mayRefreshMoves)
            {
                // Solo al generar: los movimientos que el Core sugiere para el encuentro a este nivel (SetMoveset: nivel de aprendizaje, sin azar).
                var withMoves = c.Clone();
                lock (Infra.EngineGlobalState.Gate)
                {
                    dest.ApplyParseSettings();
                    withMoves.SetMoveset(random: false);
                }
                withMoves.RefreshChecksum();
                var o2 = validator.Validate(withMoves, original, dest, requireCatalogGate);
                if (o2.Valid) { best = withMoves; bestOutcome = o2; movesRefreshed = true; return true; }
            }
            if (!o.Valid) { failure = FirstFailure(o); return false; }
            best = c; bestOutcome = o; movesRefreshed = false;
            return true;
        }

        bool TimeUp() => ct.IsCancellationRequested || DateTime.UtcNow >= deadlineUtc;

        if (!TryLevel(MaxLevel, out var f100))
        {
            limit = f100;
            // Sin nivel 100: el mayor valido entre el actual (valido) y 99. Se prueba 99 antes de bisecar (el caso comun es «solo el 100 no vale»).
            var lo = (int)from;
            var hi = 99;
            best = null; bestOutcome = null;
            if (lo < 99 && !TimeUp())
            {
                if (TryLevel(99, out var f99)) lo = 99;
                else { hi = 98; limit = f99; }
            }
            while (lo < hi)
            {
                if (TimeUp()) { cut = true; break; }
                var mid = (lo + hi + 1) / 2;
                if (TryLevel(mid, out var fm)) lo = mid;
                else { hi = mid - 1; limit = fm; }
            }
            if (best is null)
                return new MaxLevelResult(MaxLevelStatus.AlreadyMax, candidate, current, from, from,
                    $"Nivel {from}: PKHeX no admite más para este origen ({limit}).", limit, !cut);
        }

        var final = best!.CurrentLevel;
        var message = final >= MaxLevel
            ? "Nivel máximo legal: 100."
            : cut ? $"Nivel {final}: el máximo hallado antes de agotar el tiempo (PKHeX no admite el 100: {f100})."
                  : $"Nivel {final}: PKHeX no admite más para este origen ({limit}).";
        if (AwakeningRaised(candidate, best))
            message += " En Let's Go también suben los valores de despertar al mínimo que PKHeX exige para ese nivel.";
        if (candidate is PA9 && best is PA9)
            message += " En Leyendas Z-A las marcas de Movimiento + se completan con las de los movimientos que ya se aprenden de forma natural a ese nivel.";
        if (movesRefreshed)
            message += " Los movimientos son los que PKHeX sugiere para ese encuentro a ese nivel.";
        return new MaxLevelResult(MaxLevelStatus.Changed, best, bestOutcome!, from, final, message, final >= MaxLevel ? null : limit, !cut);
    }

    /// <summary>Copia con el nivel pedido: el Core ajusta la experiencia; en un Pokemon con datos de equipo se recalculan las stats (sin curar el estado ni los PS actuales); en Let's Go, la PC.</summary>
    public static PKM WithLevel(PKM source, byte level, Destination? dest = null)
    {
        var c = source.Clone();
        var partyData = c.PartyStatsPresent;
        var wasFull = c.Stat_HPCurrent >= c.Stat_HPMax;
        var hp = c.Stat_HPCurrent;
        var status = c.Status_Condition;
        c.CurrentLevel = level;
        if (partyData)
        {
            c.ResetPartyStats();
            c.Status_Condition = status;
            c.Stat_HPCurrent = wasFull ? c.Stat_HPMax : Math.Min(hp, c.Stat_HPMax);
        }
        // Let's Go: al subir de nivel el juego reparte valores de despertar (AV) y el Core exige un minimo segun el nivel (AwakenedValueVerifier: «when leveling up, AVs are randomly granted, so a mon
        // must be at or above»). Se SUBEN solo los que faltan hasta ese minimo (nunca se bajan): es una consecuencia del nivel, no un build inventado.
        if (c is PB7 pb7) RaiseAwakeningToMinimum(pb7);
        StatOptimizer.RefreshDerivedFromIvs(c); // PB7: la PC guardada depende del nivel
        // Leyendas Z-A: al subir de nivel los movimientos que se aprenden de forma natural pasan a tener su marca de Movimiento + (el Core la exige: «La marca de Movimiento + para X debe estar
        // activada»). Solo se AÑADEN marcas (nunca se quitan las que ya tenia) y con la rutina oficial del propio Core (PlusRecordApplicator).
        if (dest is not null && c is PA9 pa9 && c.PersonalInfo is IPermitPlus permit)
        {
            lock (Infra.EngineGlobalState.Gate)
            {
                dest.ApplyParseSettings();
                var la = new LegalityAnalysis(c, dest.Personal, dest.SlotType);
                pa9.SetPlusFlags(permit, la, seedOfMastery: false, tm: false);
            }
        }
        c.RefreshChecksum();
        return c;
    }

    private static void RaiseAwakeningToMinimum(PB7 pb7)
    {
        var minimum = (PB7)pb7.Clone();
        minimum.AwakeningMinimum();
        Span<byte> now = stackalloc byte[6];
        Span<byte> required = stackalloc byte[6];
        pb7.AwakeningGetVisual(now);
        minimum.AwakeningGetVisual(required);
        for (var i = 0; i < 6; i++) now[i] = Math.Max(now[i], required[i]);
        pb7.AwakeningSetVisual(now);
    }

    /// <summary>True si subir el nivel obligo a subir tambien los valores de despertar de Let's Go (se avisa en el mensaje).</summary>
    public static bool AwakeningRaised(PKM before, PKM after)
    {
        if (before is not PB7 b || after is not PB7 a) return false;
        return b.AV_HP != a.AV_HP || b.AV_ATK != a.AV_ATK || b.AV_DEF != a.AV_DEF || b.AV_SPE != a.AV_SPE || b.AV_SPA != a.AV_SPA || b.AV_SPD != a.AV_SPD;
    }

    /// <summary>Primer motivo de rechazo en palabras del Core (linea del informe oficial), o el detalle de la primera comprobacion propia que fallo.</summary>
    public static string FirstFailure(ValidationOutcome o)
    {
        var line = o.Proof.Report.FirstOrDefault(l => l.Contains("nvalid", StringComparison.OrdinalIgnoreCase) || l.Contains("nválid", StringComparison.OrdinalIgnoreCase));
        if (line is not null) return line.Trim();
        var check = o.Proof.Checks.FirstOrDefault(c => !c.Passed);
        return check is null ? "el Core no lo da por válido" : check.Detail is { Length: > 0 } d ? d : check.Name;
    }
}
