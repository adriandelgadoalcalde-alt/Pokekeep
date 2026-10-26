// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Reflection;
using PKHeX.Core;

namespace AutoLegalidad.Ui.Operation;

/// <summary>
/// Hito 5b: registro de la Pokédex de la partida al añadir o completar Pokémon, con el mecanismo OFICIAL de PKHeX: el <c>SetDex</c> de cada formato (protegido en el Core) que
/// <c>SaveFile.SetBoxSlotAtIndex</c> / <c>SetPartySlotAtIndex</c> invocan cuando <see cref="EntityImportSettings.UpdatePokeDex"/> está activado. Se pide SOLO esa parte
/// (<see cref="DexOnly"/>): ni la adaptación del Pokémon al guardado (<c>UpdateToSaveFile</c>, que cambiaría bytes del Pokémon ya validado: ADR 0030 D1) ni los récords.
/// <c>SetDex</c> solo toca tablas del guardado (visto/capturado, forma, idioma, brillante...), nunca el Pokémon; el checksum lo recalcula PKHeX al exportar. Los huevos no se registran
/// (lo decide cada <c>SetDex</c>, igual que en el juego).
/// </summary>
public static class PokedexRegistration
{
    /// <summary>Solo la Pokédex: sin adaptar el Pokémon y sin récords.</summary>
    public static readonly EntityImportSettings DexOnly = new(EntityImportOption.Disable, EntityImportOption.Enable, EntityImportOption.Disable);

    /// <summary>
    /// Registra <paramref name="written"/> (lo que se acaba de escribir y releer de <paramref name="slot"/>) en la Pokédex del guardado. Reescribe la MISMA casilla con los mismos bytes
    /// y solo la Pokédex activada. Devuelve false si el formato no registra nada (Pokémon XD: su <c>SetDex</c> está desactivado en el Core) o la escritura falló.
    /// </summary>
    public static bool Register(ISlotInfo slot, SaveFile sav, PKM written)
    {
        if (!IsSupported(sav) || written.Species == 0 || written.IsEgg) return false;
        try { return slot.WriteTo(sav, written.Clone(), DexOnly); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return false; }
    }

    /// <summary>Formatos cuyo guardado guarda un registro de especies (Pokédex o, en Colosseum, la libreta de estrategia). XD no: su <c>SetDex</c> está comentado en el Core (SAV3XD.cs).</summary>
    public static bool IsSupported(SaveFile sav) => sav.HasPokeDex || sav is SAV3Colosseum;

    /// <summary>Especies vistas y capturadas del guardado (null si el formato no las registra). Colosseum: entradas de la libreta de estrategia (privada en el Core: se lee por reflexión).</summary>
    public static (int Seen, int Caught)? Counts(SaveFile sav)
    {
        if (sav.HasPokeDex)
            return (sav.SeenCount, sav.CaughtCount);
        if (sav is SAV3Colosseum colo)
        {
            var memo = typeof(SAV3Colosseum).GetField("StrategyMemo", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(colo);
            if (memo is not StrategyMemo m) return null;
            int seen = 0, caught = 0;
            for (ushort s = 1; s <= sav.MaxSpeciesID; s++)
            {
                var e = m.GetEntry(s);
                if (e.IsEmpty) continue;
                if (e.Seen) seen++;
                if (e.Owned) caught++;
            }
            return (seen, caught);
        }
        return null;
    }
}
