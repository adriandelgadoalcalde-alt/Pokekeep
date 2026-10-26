// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;

namespace AutoLegalidad.Engine.Generation;

/// <summary>
/// Cuanto conserva un candidato valido de lo que queria el usuario (plan 7, prioridad lexicografica): variocolor, sexo y naturaleza;
/// despues movimientos y habilidad; despues IV. Los generadores (ALM incluido) NO garantizan haber cumplido el criterio pedido: se mide aqui.
/// </summary>
public readonly record struct TraitScore(bool ShinyKept, bool NatureKept, bool GenderKept, int MovesKept, bool AbilityKept, int IvSum) : IComparable<TraitScore>
{
    public static TraitScore Of(PKM original, PKM candidate)
    {
        var moves = new ushort[4];
        original.GetMoves(moves);
        var cand = new ushort[4];
        candidate.GetMoves(cand);
        var kept = 0;
        foreach (var m in moves)
            if (m != 0 && Array.IndexOf(cand, m) >= 0) kept++;
        return new TraitScore(
            original.IsShiny == candidate.IsShiny,
            original.Nature == candidate.Nature,
            original.Gender == candidate.Gender,
            kept,
            original.Ability == candidate.Ability,
            candidate.IV_HP + candidate.IV_ATK + candidate.IV_DEF + candidate.IV_SPA + candidate.IV_SPD + candidate.IV_SPE);
    }

    public int CompareTo(TraitScore o)
    {
        int c;
        if ((c = ShinyKept.CompareTo(o.ShinyKept)) != 0) return c;
        if ((c = NatureKept.CompareTo(o.NatureKept)) != 0) return c;
        if ((c = GenderKept.CompareTo(o.GenderKept)) != 0) return c;
        if ((c = MovesKept.CompareTo(o.MovesKept)) != 0) return c;
        if ((c = AbilityKept.CompareTo(o.AbilityKept)) != 0) return c;
        return IvSum.CompareTo(o.IvSum);
    }

    /// <summary>True si se cumple TODO lo que se pidio en el escalon (<paramref name="requested"/>): lo ya relajado no cuenta.</summary>
    public bool Satisfies(Traits requested, PKM original)
    {
        var shinyReq = requested.Shiny == original.IsShiny;
        var natureReq = requested.Nature == original.Nature;
        var genderReq = requested.Gender == original.Gender;
        var abilityReq = requested.Ability == original.Ability;
        var movesReq = requested.Moves.Count(m => m != 0);
        return (!shinyReq || ShinyKept) && (!natureReq || NatureKept) && (!genderReq || GenderKept)
            && (!abilityReq || AbilityKept) && MovesKept >= movesReq;
    }
}
