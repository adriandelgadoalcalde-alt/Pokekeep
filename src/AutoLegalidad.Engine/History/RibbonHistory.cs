// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Diagnostics.CodeAnalysis;
using PKHeX.Core;

namespace AutoLegalidad.Engine.History;

/// <summary>
/// Hito 4c (R-010): cintas y marcas que un Pokemon recien obtenido PUEDE llevar. ALM pone TODAS las que el Core tolera (<c>APILegality.SetAllLegalRibbons = true</c>, <c>RibbonApplicator.SetAllValidRibbons</c>),
/// que es el maximo legal y no una historia: un Pokemon capturado hoy no tiene Campeon, Esfuerzo ni Maestro de Rango. Lo que SI lleva son las que el origen otorga o exige. El propio Core las calcula:
/// <see cref="RibbonApplicator.RemoveAllValidRibbons(PKM, IEncounterTemplate, EvolutionHistory)"/> apaga todas y vuelve a encender solo las que <see cref="RibbonVerifier"/> marca como FALTANTES
/// (las de la tarjeta de evento, la Clasica, la de Premio, Alola, Compañero, las derivadas de memorias de campeon...: ver ADR 0028).
/// </summary>
public static class RibbonHistory
{
    /// <summary>Cintas y marcas que el Pokemon lleva puestas (las de contador cuentan una por cada unidad: 4 cintas de concurso de memoria = 4).</summary>
    [RequiresUnreferencedCode("Enumera por reflexion las propiedades de cintas del formato (RibbonInfo.GetRibbonInfo).")]
    public static int Count(PKM pk)
    {
        var n = 0;
        foreach (var r in RibbonInfo.GetRibbonInfo(pk))
            n += r.Type == RibbonValueType.Boolean ? (r.HasRibbon ? 1 : 0) : r.RibbonCount;
        return n;
    }

    /// <summary>
    /// Deja solo las cintas y marcas que el origen otorga o exige, EN el Pokemon recibido (que debe ser una copia). Devuelve cuantas cintas se quitaron (0 si no habia sobrantes).
    /// <paramref name="enc"/> es el encuentro que el analisis oficial da por bueno para ese Pokemon.
    /// </summary>
    [RequiresUnreferencedCode("Enumera por reflexion las propiedades de cintas del formato (RibbonInfo.GetRibbonInfo).")]
    public static int KeepOnlyOriginRibbons(PKM pk, IEncounterTemplate enc, EvolutionHistory history)
    {
        var before = Count(pk);
        RibbonApplicator.RemoveAllValidRibbons(pk, enc, history);
        RepairRequired(pk, enc, history);
        // Hallazgo del 4c (informe del Core): RemoveAllValidRibbons tambien apaga las marcas que el encuentro exige por otra via (IEncounterMarkExtra: Crafty del Munchlax estatico, marcas de los regalos WC9/WA9;
        // MarkRules.cs:315-327, WC9.cs:936-946) y no las vuelve a poner: el Core diria «falta la marca». Se restituyen.
        if (pk is IRibbonIndex rm && enc is IEncounterMarkExtra extra && extra.IsMissingExtraMark(pk, out var missing))
            rm.SetRibbonIndex(missing);
        // El titulo (cinta o marca elegida para mostrar) debe seguir apuntando a algo que el Pokemon tenga (MarkVerifier.cs:71-106).
        if (pk is IRibbonSetAffixed aff && pk is IRibbonIndex ra && aff.AffixedRibbon >= 0 && !ra.GetRibbon(aff.AffixedRibbon))
            aff.AffixedRibbon = AffixedRibbon.None;
        return Math.Max(0, before - Count(pk));
    }

    /// <summary>
    /// Tras apagarlo todo, <c>RibbonVerifier</c> puede seguir marcando una cinta que el origen EXIGE pero que quedo apagada: el Core la lista sin la bandera «falta» (p. ej. la cinta Companero de un intercambio de Gen9,
    /// RibbonVerifierCommon9.cs:19-20, que <c>FixInvalidRibbons</c> apagaria otra vez). Una cinta marcada que esta APAGADA es una cinta exigida: se enciende; una marcada y encendida sobra: se apaga.
    /// </summary>
    private static void RepairRequired(PKM pk, IEncounterTemplate enc, EvolutionHistory history)
    {
        Span<RibbonResult> results = stackalloc RibbonResult[RibbonVerifier.MaxRibbonCount];
        for (var round = 0; round < 3; round++)
        {
            var n = RibbonVerifier.GetRibbonResults(new RibbonVerifierArguments(pk, enc, history), results);
            if (n == 0) return;
            var changed = false;
            foreach (var r in results[..n])
            {
                var prop = pk.GetType().GetProperty(r.PropertyName);
                if (prop is null || prop.PropertyType != typeof(bool)) continue;   // las de contador (memorias de concurso/combate) las gestiona el Core
                var on = (bool)prop.GetValue(pk)!;
                var want = r.IsMissing || !on;   // faltante, o marcada estando apagada = exigida
                if (want == on) continue;
                prop.SetValue(pk, want);
                changed = true;
            }
            if (!changed) return;
        }
    }

    /// <summary>Cintas y marcas de MAS que lleva el Pokemon respecto de las que su origen exige (0 = historia limpia). No modifica el Pokemon.</summary>
    [RequiresUnreferencedCode("Enumera por reflexion las propiedades de cintas del formato (RibbonInfo.GetRibbonInfo).")]
    public static int Extra(PKM pk, IEncounterTemplate enc, EvolutionHistory history)
    {
        var clone = pk.Clone();
        var have = Count(pk);
        KeepOnlyOriginRibbons(clone, enc, history);
        return Math.Max(0, have - Count(clone));
    }
}
