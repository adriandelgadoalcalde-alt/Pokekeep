// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Engine.Changes;
using AutoLegalidad.Engine.Context;
using AutoLegalidad.Engine.Validation;
using PKHeX.Core;

namespace AutoLegalidad.Engine.History;

/// <summary>
/// Hito 4c (R-010): valores de historial que ALM deja al maximo o aplicados sin que ningun jugador los haya puesto (informe del Core, ADR 0028 seccion 6):
/// marcas de aspecto (circulo, triangulo... las rellena <c>MarkingApplicator.SetMarkings</c> segun los IV: un Pokemon recien obtenido no las lleva), amistad al maximo (ALM usa 255 por defecto: el valor
/// creible es el que sugiere el propio Core, <see cref="HistoryVerifier.GetSuggestedFriendshipCurrent"/>: la amistad base de la especie, 255 con Retorno, 0 con Frustracion) y nivel de Dynamax 10 en toda
/// captura de Espada/Escudo (se baja a 0, el de un Pokemon que nadie ha entrenado, si el Core lo valida).
/// Pokerus y estadisticas de concurso no se tocan: ningun generador ni ALM los ponen (el estado «susceptible» es el creible) y el analizador lo comprueba.
/// </summary>
internal static class MiscHistory
{
    public static void Apply(ref PKM cur, ref ValidationOutcome curOutcome, HistoryContext ctx)
    {
        // Marcas aplicadas por el jugador (adornos del cuadro de estado).
        HistoryPhase.TryStep(ref cur, ref curOutcome, ctx, pk =>
        {
            var cleared = ClearAppliedMarkings(pk);
            return cleared == 0 ? null : (ReasonCodes.HistoryMisc, "Marcas aplicadas", cleared.ToString(), "0", "se quitaron las marcas de aspecto (círculo, triángulo...): un Pokémon recién obtenido no las lleva.", cleared);
        });

        // Amistad y afecto: los de un Pokemon que no se ha entrenado (OT y HT por separado).
        HistoryPhase.TryStep(ref cur, ref curOutcome, ctx, pk =>
        {
            if (pk.IsEgg || pk.Format < 3) return null;
            var la = HistoryPhase.Analyze(pk, ctx.Dest);
            if (!la.Parsed) return null;
            var (beforeOt, beforeHt) = (pk.OriginalTrainerFriendship, pk.HandlingTrainerFriendship);
            var changed = ApplyFriendship(pk, la.EncounterMatch);
            return !changed ? null : (ReasonCodes.HistoryMisc, "Amistad", $"{beforeOt}/{beforeHt}", $"{pk.OriginalTrainerFriendship}/{pk.HandlingTrainerFriendship}",
                "amistad y afecto de un Pokémon recién obtenido (la amistad base de su especie, no el máximo).", 1);
        });

        // Tamaño de un brote de SV con rango forzado: ALM sobrescribe la escala con la derivada del PID (SimpleEdits.cs:326-335) y el brote la fija dentro de [ScaleMin, ScaleMax] (EncounterOutbreak9.cs:128-132).
        HistoryPhase.TryStep(ref cur, ref curOutcome, ctx, pk =>
        {
            if (pk is not IScaledSize3 sz) return null;
            var la = HistoryPhase.Analyze(pk, ctx.Dest);
            if (!la.Parsed || OutbreakScaleOutOfRange(pk, la.EncounterMatch) is not { } range) return null;
            var before = sz.Scale;
            sz.Scale = (byte)(range.Min + (int)(((ulong)pk.EncryptionConstant * 2654435761UL) % (ulong)(range.Max - range.Min + 1)));
            return (ReasonCodes.HistoryMisc, "Tamaño (escala)", before.ToString(), sz.Scale.ToString(), $"escala {sz.Scale} dentro del rango que fija su brote ({range.Min}-{range.Max}), como lo genera el juego.", 1);
        });

        // Altura y peso a 0 (Gen8+): el Core lo marca «sospechoso» (HOMETransferSettings.ZeroHeightWeight = Fishy, MiscScaleVerifier.cs:43-47); el juego los genera con la distribucion triangular rand(0x81) + rand(0x80).
        HistoryPhase.TryStep(ref cur, ref curOutcome, ctx, pk =>
        {
            var la = HistoryPhase.Analyze(pk, ctx.Dest);
            if (!la.Parsed || !ZeroSize(pk, la.EncounterMatch)) return null;
            var s2 = (IScaledSize)pk;
            s2.HeightScalar = Triangular(pk, 11);
            s2.WeightScalar = Triangular(pk, 23);
            return (ReasonCodes.HistoryMisc, "Altura y peso", "0/0", $"{s2.HeightScalar}/{s2.WeightScalar}", "altura y peso generados como los genera el juego (suma de dos tiradas, 0-255), no a 0.", 1);
        });

        // Nivel de Dynamax de Espada/Escudo: el menor que el Core valida (los encuentros fijan un minimo; un Pokemon salvaje, 0).
        if (cur is IDynamaxLevel cd && cd.DynamaxLevel > 0)
        {
            var current = cd.DynamaxLevel;
            for (byte lvl = 0; lvl < current; lvl++)
            {
                var copy = cur.Clone();
                ((IDynamaxLevel)copy).DynamaxLevel = lvl;
                var v = ctx.Validator.Validate(copy, ctx.Original, ctx.Dest, ctx.RequireCatalogGate);
                if (!v.Valid) continue;
                cur = copy;
                curOutcome = v;
                ctx.Log.History(ReasonCodes.HistoryMisc, "Nivel Dynamax", current.ToString(), lvl.ToString(), $"nivel Dynamax {lvl} (el menor que PKHeX admite en su encuentro: nadie lo ha subido con caramelos Dynamax).");
                break;
            }
        }
    }

    /// <summary>Altura y peso a 0 en un Pokemon de Gen8+ cuyo origen no los fija a 0 (los regalos WC9 y los HOME WC8 traen valores fijos: <c>MiscScaleVerifier.CheckHeightWeightOdds</c>, Arceus y Z-A tienen sus reglas propias).</summary>
    internal static bool ZeroSize(PKM pk, IEncounterTemplate enc)
        => pk is IScaledSize { HeightScalar: 0, WeightScalar: 0 } && pk.Format >= 8 && enc.Generation >= 8
           && enc.Context is not (EntityContext.Gen9a or EntityContext.Gen8a) && enc is not WC9 && enc is not WC8 { IsHOMEGift: true };

    /// <summary>Tirada triangular determinista (misma identidad, mismo valor) con la forma del juego: <c>rand(0x81) + rand(0x80)</c> (PokeSize.cs:50-54).</summary>
    private static byte Triangular(PKM pk, ulong salt)
    {
        static ulong Mix(ulong x) { x ^= x >> 30; x *= 0xBF58476D1CE4E5B9UL; x ^= x >> 27; x *= 0x94D049BB133111EBUL; return x ^ (x >> 31); }
        var seed = ((ulong)pk.EncryptionConstant << 32 | pk.PID) ^ (salt * 0x9E3779B97F4A7C15UL) ^ pk.Species;
        return (byte)(Mix(seed) % 0x81 + Mix(seed + 1) % 0x80);
    }

    /// <summary>Rango de escala del brote si el Pokemon (de un brote con rango forzado) lo incumple; null si no aplica o esta dentro.</summary>
    internal static (int Min, int Max)? OutbreakScaleOutOfRange(PKM pk, IEncounterTemplate enc)
        => enc is EncounterOutbreak9 { IsForcedScaleRange: true } o && pk is IScaledSize3 s && (s.Scale < o.ScaleMin || s.Scale > o.ScaleMax) ? (o.ScaleMin, o.ScaleMax) : null;

    /// <summary>Menor nivel de Dynamax con el que el analisis oficial sigue dando el Pokemon por valido (null si ya esta en el menor o no es de Espada/Escudo).</summary>
    internal static byte? LowerValidDynamax(PKM pk, Destination dest)
    {
        if (pk is not IDynamaxLevel d || d.DynamaxLevel == 0) return null;
        for (byte lvl = 0; lvl < d.DynamaxLevel; lvl++)
        {
            var copy = pk.Clone();
            ((IDynamaxLevel)copy).DynamaxLevel = lvl;
            if (HistoryPhase.Analyze(copy, dest).Valid) return lvl;
        }
        return null;
    }

    /// <summary>Pone la amistad y el afecto creibles (<see cref="WantedFriendship"/>); true si cambio algo.</summary>
    internal static bool ApplyFriendship(PKM pk, IEncounterTemplate enc)
    {
        var (wantedOt, wantedHt, affection) = WantedFriendship(pk, enc);
        var changed = false;
        if (wantedOt is { } o && pk.OriginalTrainerFriendship != o) { pk.OriginalTrainerFriendship = o; changed = true; }
        if (pk.HandlingTrainerFriendship != wantedHt) { pk.HandlingTrainerFriendship = wantedHt; changed = true; }
        if (affection && pk is IAffection af && (af.OriginalTrainerAffection != 0 || af.HandlingTrainerAffection != 0)) { af.OriginalTrainerAffection = 0; af.HandlingTrainerAffection = 0; changed = true; }
        return changed;
    }

    /// <summary>
    /// Amistad creible: la del entrenador original (la que sugiere el Core: base de la especie, 255 con Retorno, 0 con Frustracion; null si el origen es un huevo eclosionado: su valor lo fija el generador), la del
    /// entrenador anterior (0 si no lo hay) y si el afecto de Gen6/7 debe ser 0 (los generadores del Core lo dejan en 0 y solo la Cinta Afecto, exigida por el origen, lo pide alto: el Core lo validara).
    /// </summary>
    internal static (byte? Ot, byte Ht, bool ZeroAffection) WantedFriendship(PKM pk, IEncounterTemplate enc)
    {
        byte? ot = enc.IsEgg ? null : HistoryVerifier.GetSuggestedFriendshipOT(pk, enc);
        var ht = TrainerHistory.HasHandler(pk) ? HistoryVerifier.GetSuggestedFriendshipHT(pk) : (byte)0;
        return (ot, ht, true);
    }

    /// <summary>Apaga las marcas de aspecto (bool de Gen3-6, color de Gen7+). Devuelve cuantas estaban puestas.</summary>
    internal static int ClearAppliedMarkings(PKM pk)
    {
        var n = 0;
        switch (pk)
        {
            case IAppliedMarkings<MarkingColor> c:
                for (var i = 0; i < c.MarkingCount; i++)
                    if (c.GetMarking(i) != MarkingColor.None) { c.SetMarking(i, MarkingColor.None); n++; }
                break;
            case IAppliedMarkings<bool> b:
                for (var i = 0; i < b.MarkingCount; i++)
                    if (b.GetMarking(i)) { b.SetMarking(i, false); n++; }
                break;
        }
        return n;
    }

    /// <summary>Marcas de aspecto puestas (para el analizador).</summary>
    internal static int AppliedMarkings(PKM pk) => pk switch
    {
        IAppliedMarkings<MarkingColor> c => Enumerable.Range(0, c.MarkingCount).Count(i => c.GetMarking(i) != MarkingColor.None),
        IAppliedMarkings<bool> b => Enumerable.Range(0, b.MarkingCount).Count(i => b.GetMarking(i)),
        _ => 0,
    };
}
