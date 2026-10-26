// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;

namespace AutoLegalidad.Engine.Generation;

/// <summary>Un escalon de la relajacion automatica (plan 7, fase D). Los escalones son ACUMULATIVOS y se aplican sin preguntar al usuario.</summary>
public sealed record RelaxationStep(int Index, string Code, string Description, Traits Traits);

/// <summary>
/// Escalera de relajacion determinista (el ranking de criterios no depende del azar): primero se intenta TODO lo que pide el usuario y
/// despues se sueltan, por orden de menor a mayor importancia (plan 7, fase C): habilidad, movimientos ilegales, naturaleza, sexo, variocolor,
/// forma, nivel y, al final, rasgos cosmeticos. La especie NUNCA se suelta.
/// </summary>
public static class Relaxation
{
    public static IReadOnlyList<RelaxationStep> Ladder(PKM original, LegalityAnalysis? analysis, bool optimizeStats, IEncounterable? keepEncounter = null)
    {
        var baseTraits = Traits.From(original);
        if (optimizeStats) baseTraits = baseTraits.WithOptimizedIVs();
        // Hito 3b: en las especies cuyo sexo ES la forma (Meowstic, Indeedee, Basculegion, Oinkologne: Core FormVerifier / SpeciesCategory.IsFormGenderSpecific) la forma pedida manda y fija el sexo.
        // Toxtricity: la forma (Amped/Low Key) la fija la naturaleza (Core ToxtricityUtil.GetAmpLowKeyResult): si la naturaleza del usuario no da la forma pedida, manda la forma.
        if (original.Species == (ushort)Species.Toxtricity && ToxtricityUtil.GetAmpLowKeyResult(baseTraits.Nature) != original.Form)
            baseTraits = baseTraits with { Nature = Nature.Random };
        var genderIsForm = SpeciesCategory.IsFormGenderSpecific(original.Species);
        if (genderIsForm) baseTraits = baseTraits with { Gender = (byte)(original.Form & 1) };

        // Movimientos que el analisis oficial ya acepta (si hay analisis); el resto se pide como «libre».
        var moves = baseTraits.Moves.ToArray();
        if (analysis is not null)
        {
            for (var i = 0; i < 4; i++)
                if (!analysis.Info.Moves[i].Valid) moves[i] = 0;
        }
        var l0 = baseTraits with { Moves = Compact(moves) };

        var steps = new List<RelaxationStep>();
        void Add(string code, string desc, Traits t) => steps.Add(new RelaxationStep(steps.Count, code, desc, t));

        // Escalon 0: conservar EL MISMO ENCUENTRO (p. ej. un evento con su entrenador original). Solo si el analisis oficial lo identifico de verdad.
        if (keepEncounter is { } enc && enc is not EncounterInvalid)
            Add("KeepEncounter", "Regenerar dentro del mismo encuentro (se conserva su identidad)", l0.AdaptToEncounter(enc));
        Add("Exact", "Todos los rasgos del usuario que siguen siendo legales", l0);
        var t1 = l0 with { Ability = -1 };
        Add("NoAbility", "Sin exigir habilidad", t1);
        var t2 = t1 with { Moves = [0, 0, 0, 0] };
        Add("NoMoves", "Movimientos libres (los del encuentro)", t2);
        var t3 = t2 with { Nature = Nature.Random };
        Add("NoNature", "Sin exigir naturaleza", t3);
        var t4 = genderIsForm ? t3 : t3 with { Gender = null }; // el sexo de estas especies lo fija la forma: solo se suelta junto con ella
        Add("NoGender", "Sin exigir sexo", t4);
        var t5 = t4 with { Shiny = false };
        Add("NoShiny", "Sin variocolor", t5);
        // Hito 3b: una forma que no es la base se conserva antes que el nivel (una evolucion exige un nivel minimo que el usuario puede no tener, p. ej. un Sawsbuck de nivel 20
        // que solo sale de un huevo): primero se libera el nivel y SOLO despues se suelta la forma.
        var t6src = t5;
        if (original.Form != 0)
        {
            t6src = t5 with { Level = 100 };
            Add("LevelFree", "Forma conservada, nivel libre (nivel 100)", t6src);
        }
        var t6 = t6src with { Form = 0, Gender = genderIsForm ? null : t6src.Gender };
        Add("BaseForm", "Forma base", t6);
        var t7 = t6 with { Level = 100, Nickname = string.Empty, HeldItem = 0 };
        Add("Defaults", "Nivel 100, sin apodo ni objeto", t7);
        return steps;
    }

    private static ushort[] Compact(ushort[] moves)
    {
        var result = new ushort[4];
        var n = 0;
        foreach (var m in moves)
            if (m != 0 && Array.IndexOf(result, m) < 0) result[n++] = m;
        return result;
    }
}
