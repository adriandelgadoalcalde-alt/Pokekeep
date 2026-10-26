// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;

namespace AutoLegalidad.Engine.Generation;

/// <summary>
/// Hito 3b: la forma de Dudunsparce (forma 1 = tres segmentos) y de Maushold (forma 0 = familia de cuatro... forma 1 = familia de tres) depende de la CONSTANTE DE CIFRADO al evolucionar:
/// Core <c>EvolutionRestrictions.IsEvolvedSpeciesFormRare</c> (EC % 100 == 0) y <c>GetIsExpectedEvolveFormEC100</c> (Legality/Restrictions/EvolutionRestrictions.cs:50-71). Una EC fija
/// la forma, asi que cualquier cambio de EC (o de semilla) debe conservar su clase.
/// </summary>
public static class EvolutionEc
{
    public static bool Applies(ushort species) => species is (ushort)Species.Dudunsparce or (ushort)Species.Maushold;

    /// <summary>La clase de EC que exige la forma pedida (true = «rara», EC % 100 == 0).</summary>
    public static bool WantsRare(ushort species, byte form) => species == (ushort)Species.Dudunsparce ? form == 1 : form == 0;

    /// <summary>Misma regla que el Core (<c>EvolutionRestrictions.IsEvolvedSpeciesFormRare</c>, interna): EC % 100 == 0.</summary>
    public static bool IsRare(uint ec) => ec % 100 == 0;

    /// <summary>EC cercana a <paramref name="ec"/> de la clase pedida.</summary>
    public static uint Nearest(uint ec, bool rare)
    {
        if (IsRare(ec) == rare) return ec;
        return rare ? ec - (ec % 100) : ec + 1;
    }
}
