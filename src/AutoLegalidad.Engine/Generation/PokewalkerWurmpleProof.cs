// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Engine.Context;
using PKHeX.Core;

namespace AutoLegalidad.Engine.Generation;

/// <summary>
/// Hito 3b: el Pokemon del Pokewalker no tiene PID aleatorio: <c>PokewalkerRNG.GetPID(TID, SID, naturaleza, sexo, proporcion)</c> (<c>EncounterStatic4Pokewalker.cs:109</c>, «nature-gender-trainerID
/// only yield fixed PIDs»). La rama de Wurmple (Silcoon/Cascoon) sale de <c>PID &gt;&gt; 16</c> (<c>WurmpleUtil.cs:13-18</c>). Con los TID/SID de la partida, el dominio es FINITO: 25 naturalezas x 2 sexos.
/// Si ninguno de los 50 PID da la rama pedida, esa evolucion es imposible para ESA partida. Es una enumeracion exhaustiva con el generador del Core, no un muestreo.
/// </summary>
public static class PokewalkerWurmpleProof
{
    public static bool TryDisprove(IEncounterable? enc, PKM wish, Destination dest, out string evidence)
    {
        evidence = string.Empty;
        if (enc is not EncounterStatic4Pokewalker w || w.Species != (ushort)Species.Wurmple || !WurmpleUtil.IsWurmpleEvo(wish.Species)) return false;
        var count = 0;
        lock (Infra.EngineGlobalState.Gate)
        {
            dest.ApplyParseSettings();
            for (var nature = 0; nature < 25; nature++)
            {
                foreach (var gender in new[] { Gender.Male, Gender.Female })
                {
                    PKM pk;
                    try { pk = w.ConvertToPKM(dest.Trainer, EncounterCriteria.Unrestricted with { Nature = (Nature)nature, Gender = gender }); }
                    catch (Exception ex) when (ex is not OutOfMemoryException) { return false; }
                    count++;
                    pk.Species = wish.Species;
                    if (WurmpleUtil.IsWurmpleEvoValid(pk)) return false;
                }
            }
        }
        evidence = $"Los {count} PID posibles del Wurmple del Pokéwalker con los TID/SID de la partida (25 naturalezas x 2 sexos) dan todos la otra rama de Wurmple: la especie {wish.Species} es imposible para esta partida. Referencia técnica: PokewalkerRNG.GetPID, EncounterStatic4Pokewalker.cs:109; WurmpleUtil.IsWurmpleEvoValid, WurmpleUtil.cs:13-18.";
        return true;
    }
}
