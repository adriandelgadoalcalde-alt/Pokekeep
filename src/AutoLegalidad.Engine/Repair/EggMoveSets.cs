// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Engine.Context;
using AutoLegalidad.Engine.Infra;
using PKHeX.Core;

namespace AutoLegalidad.Engine.Repair;

/// <summary>
/// Reparacion de los movimientos de un huevo (Hito 3, propuesta 2 del Hito 2): con UN movimiento ilegal el analisis oficial pierde el encuentro del huevo y deja
/// de sugerir nada (Gen2: Placaje + Anclaje... no se reconstruyen con la reparacion minima). Aqui se parte del CONJUNTO DE HERENCIA DEL ENCUENTRO: los movimientos
/// iniciales con los que el propio Core crea el huevo de esa especie en el destino (<see cref="IEncounterEgg"/>), y se anaden, de uno en uno, los movimientos
/// del usuario que el analisis oficial admita como herencia (movimientos de huevo, del padre o de MT segun la generacion). El ORACULO es siempre el Core:
/// no se escribe ninguna regla de cria a mano (regla 11).
/// </summary>
internal static class EggMoveSets
{
    private const int MaxEggEncounters = 8;

    /// <summary>Conjuntos de movimientos candidatos (de mejor a peor), cada uno ya comprobado contra el Core como el que menos apartados invalidos deja.</summary>
    public static IReadOnlyList<ushort[]> Candidates(PKM pk, ushort[] userMoves, Destination dest)
    {
        var result = new List<ushort[]>();
        var blank = dest.Sav.BlankPKM;
        blank.Species = pk.Species;
        blank.Form = pk.Form;
        blank.Gender = blank.GetSaneGender();

        var tried = 0;
        List<IEncounterEgg> eggs = [];
        lock (EngineGlobalState.Gate)
        {
            dest.ApplyParseSettings();
            foreach (var enc in CoreVersions.Encounters(blank, dest.Trainer, ReadOnlyMemory<ushort>.Empty))
            {
                if (enc is not IEncounterEgg egg || enc is not IEncounterConvertible) continue;
                eggs.Add(egg);
                if (++tried >= MaxEggEncounters) break;
            }
        }

        foreach (var egg in eggs)
        {
            PKM template;
            try { template = ((IEncounterConvertible)egg).ConvertToPKM(dest.Trainer); }
            catch (Exception ex) when (ex is not OutOfMemoryException) { continue; }
            var baseSet = new ushort[4];
            template.GetMoves(baseSet);
            if (baseSet.All(m => m == 0)) continue;

            // Conjunto base y, a partir de el, la herencia que el Core admite de lo que pide el usuario.
            var current = baseSet.ToArray();
            AddUnique(result, current);
            foreach (var wanted in userMoves)
            {
                if (wanted == 0 || Array.IndexOf(current, wanted) >= 0) continue;
                // Se intenta ocupar un hueco libre y, si no hay, sustituir el ultimo movimiento.
                var slot = Array.IndexOf(current, (ushort)0);
                if (slot < 0) slot = 3;
                var trial = current.ToArray();
                trial[slot] = wanted;
                if (CountInvalid(pk, trial, dest) <= CountInvalid(pk, current, dest))
                {
                    current = trial;
                    AddUnique(result, current);
                }
            }
        }
        return result;
    }

    private static void AddUnique(List<ushort[]> list, ushort[] set)
    {
        if (!list.Any(x => x.AsSpan().SequenceEqual(set))) list.Add(set.ToArray());
    }

    private static int CountInvalid(PKM pk, ushort[] moves, Destination dest)
    {
        var t = pk.Clone();
        t.SetMoves(moves);
        t.HealPP();
        LegalityAnalysis la;
        lock (EngineGlobalState.Gate)
        {
            dest.ApplyParseSettings();
            la = new LegalityAnalysis(t, dest.Personal, dest.SlotType);
        }
        return la.Results.Count(r => !r.Valid) + la.Info.Moves.Count(m => !m.Valid);
    }
}
