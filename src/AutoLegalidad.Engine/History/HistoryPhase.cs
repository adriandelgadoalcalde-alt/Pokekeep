// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using AutoLegalidad.Engine.Catalog;
using AutoLegalidad.Engine.Changes;
using AutoLegalidad.Engine.Context;
using AutoLegalidad.Engine.Infra;
using AutoLegalidad.Engine.Validation;
using PKHeX.Core;

namespace AutoLegalidad.Engine.History;

/// <summary>Datos que necesita la fase «Historia» (todo lo demas sale del propio Pokemon y de su analisis oficial).</summary>
public sealed record HistoryContext(PKM Original, Destination Dest, IOfficialValidator Validator, bool RequireCatalogGate, CatalogReader? Catalog, DateOnly Today, ChangeLog Log);

/// <summary>
/// Hito 4c (R-010, ADR 0028): fase «Historia». Un Pokemon que Pokekeep CONSTRUYE (Legalizar con origen reconstruido, Libreria, Completar el juego, Edicion masiva) debe parecer uno obtenido de verdad, no solo ser legal.
/// Cada paso trabaja sobre una COPIA, y solo se acepta si el candidato resultante sigue pasando la validacion completa (Core, destino y round-trip: el mismo <see cref="IOfficialValidator"/> del motor):
/// la plausibilidad nunca puede romper la legalidad. Un paso que no se puede aplicar deja el Pokemon como estaba.
/// NO se aplica a «Optimizar stats» (que conserva toda la historia del Pokemon del usuario: <c>OriginGuard</c>) ni a una reparacion minima de un Pokemon del usuario.
/// </summary>
public static class HistoryPhase
{
    /// <summary>Diagnostico opcional (bancos y pruebas): cada paso rechazado por el Core explica por que.</summary>
    public static Action<string>? Trace { get; set; }

    /// <summary>Intentos de fecha distintos si el Core rechaza la primera (p. ej. una cinta o regla que depende del dia).</summary>
    private const int DateAttempts = 4;

    [RequiresUnreferencedCode("Cuenta las cintas por reflexion (RibbonInfo.GetRibbonInfo).")]
    public static (PKM Candidate, ValidationOutcome Outcome) Apply(PKM candidate, ValidationOutcome outcome, HistoryContext ctx)
    {
        var cur = candidate;
        var curOutcome = outcome;

        // 1) Cintas y marcas: solo las que el origen otorga.
        TryStep(ref cur, ref curOutcome, ctx, pk =>
        {
            var la = Analyze(pk, ctx.Dest);
            if (!la.Parsed) return null;
            var removed = RibbonHistory.KeepOnlyOriginRibbons(pk, la.EncounterMatch, la.Info.EvoChainsAllGens);
            return removed == 0 ? null : (ReasonCodes.HistoryRibbons, "Cintas", string.Empty, string.Empty,
                $"se quitaron {removed} {(removed == 1 ? "cinta o marca que su origen no otorga" : "cintas o marcas que su origen no otorga")}.", removed);
        });

        // 2) Fechas de captura y de eclosion.
        for (var variant = 0; variant < DateAttempts; variant++)
        {
            var copy = cur.Clone();
            var la = Analyze(copy, ctx.Dest);
            if (!la.Parsed) break;
            var enc = la.EncounterMatch;
            DateOnly? cardStart = null, cardEnd = null;
            var fromTracking = false;
            if (ctx.Catalog is { } cat && EncounterKeys.For(la.EncounterOriginal) is { } key && key.StartsWith("core-mgdb:", StringComparison.Ordinal) && cat.EventWindow(key) is { } ew)
            {
                cardStart = ew.Start;
                cardEnd = ew.End;
                fromTracking = ew.FromTracking;
            }
            if (DatePolicy.Plan(copy, enc, ctx.Today, cardStart, variant, cardEnd, fromTracking) is not { } plan) break;
            var beforeMet = copy.MetDate;
            var beforeEgg = copy.EggMetDate;
            DatePolicy.Apply(copy, plan);
            // Regalos de SV con escala fija (WC9): las tarjetas de antes del parche 1.2.0 (Flabebe de PokeCenter...) tienen una hermana con la escala fija; el Core solo mira la escala si la fecha es posterior al
            // parche, asi que al mover la fecha a una posterior el Pokemon dejaba de encajar con SU tarjeta y pasaba a encajar con la hermana de escala aleatoria (mismo contenido, otro origen). Se lleva la escala de la tarjeta.
            if (copy is IScaledSize3 scaled && enc is WC9 { Scale: < 256 } card && scaled.Scale != card.Scale) scaled.Scale = (byte)card.Scale;
            Settle(copy);
            if (copy.MetDate == beforeMet && copy.EggMetDate == beforeEgg) break;   // ya estaba en una fecha plausible
            var v = ctx.Validator.Validate(copy, ctx.Original, ctx.Dest, ctx.RequireCatalogGate);
            if (!v.Valid) { Trace?.Invoke($"[historia] fechas rechazadas (intento {variant}): {string.Join(", ", v.Failures)} :: {string.Join(" | ", v.Proof.Report.Where(l => l.Contains("nvál", StringComparison.OrdinalIgnoreCase) || l.Contains("nval", StringComparison.OrdinalIgnoreCase)).Take(3))}"); continue; }
            cur = copy;
            curOutcome = v;
            ctx.Log.Reason("MetDate", ReasonCodes.HistoryDates, "Historia: fecha de captura dentro de la ventana real del encuentro y anterior a hoy.");
            ctx.Log.Reason("EggMetDate", ReasonCodes.HistoryDates, "Historia: el huevo se recogió unos días antes de eclosionar.");
            ctx.Log.Note($"Historia: fechas elegidas {plan.Source}.");
            break;
        }

        // 3) Entrenador anterior y memorias coherentes con el recorrido.
        TrainerHistory.Apply(ref cur, ref curOutcome, ctx);
        MemoryHistory.Apply(ref cur, ref curOutcome, ctx);   // ADR 0042: recuerdos de OT y de HT como los daria el juego

        // 4) Valores de historial: marcas aplicadas, amistad, nivel de Dynamax...
        MiscHistory.Apply(ref cur, ref curOutcome, ctx);
        return (cur, curOutcome);
    }

    /// <summary>Un paso: se aplica a una copia, se valida y solo entonces sustituye al candidato. El paso devuelve null si no cambia nada.</summary>
    internal static void TryStep(ref PKM cur, ref ValidationOutcome curOutcome, HistoryContext ctx, Func<PKM, (string Code, string Field, string Before, string After, string Sentence, int Count)?> step)
    {
        var copy = cur.Clone();
        var change = step(copy);
        if (change is not { } c) return;
        Settle(copy);
        var v = ctx.Validator.Validate(copy, ctx.Original, ctx.Dest, ctx.RequireCatalogGate);
        if (!v.Valid) { Trace?.Invoke($"[historia] paso «{c.Field}» rechazado: {string.Join(", ", v.Failures)} :: {string.Join(" | ", v.Proof.Report.Where(l => l.Contains("nvál", StringComparison.OrdinalIgnoreCase) || l.Contains("nval", StringComparison.OrdinalIgnoreCase)).Take(3))}"); return; }
        cur = copy;
        curOutcome = v;
        ctx.Log.History(c.Code, c.Field, c.Before, c.After, c.Sentence);
    }

    /// <summary>
    /// Valores calculados que dependen de lo que acaba de cambiar. Let's Go: los PC (<c>ResetCP</c>); NO el tamaño absoluto (<c>ResetCalculatedValues</c> lo recalcula y los eventos de altura y peso fijos, p. ej. Meltan,
    /// lo exigen igual al de la tarjeta: <c>MiscVerifierPB7.VerifyAbsoluteSizes</c>).
    /// </summary>
    internal static void Settle(PKM pk)
    {
        if (pk is PB7 pb7) pb7.ResetCP();
    }

    internal static LegalityAnalysis Analyze(PKM pk, Destination dest)
    {
        lock (EngineGlobalState.Gate)
        {
            dest.ApplyParseSettings();
            return new LegalityAnalysis(pk, dest.Personal, dest.SlotType);
        }
    }

    /// <summary>«Hoy» segun el reloj del Core (el mismo que usan los generadores y que fijan las pruebas con <c>EncounterDate.TimeProvider</c>).</summary>
    public static DateOnly Today() => DateOnly.FromDateTime(EncounterDate.TimeProvider.GetLocalNow().DateTime);

    internal static string Iso(DateOnly? d) => d?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;
}
