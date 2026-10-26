// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;

namespace AutoLegalidad.Engine.Context;

/// <summary>
/// Enumeracion de encuentros del Core con la lista de juegos explicita. <c>EncounterMovesetGenerator.GenerateEncounters</c> sin versiones recorre TODOS los valores guardables entre
/// <c>MinGameID</c> y <c>MaxGameID</c>; desde PKHeX 26.10.18 eso incluye <c>WI</c> y <c>WA</c> (Pokémon Winds y Waves: el enum existe pero el Core no les da contexto de entidad, y
/// <c>EvolutionGroupUtil.GetGroup</c> lanza <c>ArgumentOutOfRangeException</c> para el contexto <c>None</c>) y <c>FRX</c> y <c>LGX</c> (Rojo Fuego y Verde Hoja de Switch Online pasados a HOME).
/// Aqui se pide la misma lista que habria sin esos cuatro: es lo que el producto enumeraba con 26.08.26. No se cambia ninguna regla de legalidad (regla 12): solo se evita que el Core
/// tropiece con un juego sin datos. Generar desde un origen FRX/LGX (conversion de Gen 3 a HOME) queda como mejora futura, con su ADR y sus pruebas.
/// </summary>
public static class CoreVersions
{
    private static bool Excluded(GameVersion v) => v is GameVersion.WI or GameVersion.WA or GameVersion.FRX or GameVersion.LGX;

    /// <summary>Juegos que el Core recorrería por defecto para esa entidad, sin los valores nuevos de 26.10.18 que no tienen datos.</summary>
    public static GameVersion[] For(PKM blank) => [.. GameUtil.GetVersionsWithinRange(blank, blank.Context).Where(static v => !Excluded(v))];

    /// <inheritdoc cref="EncounterMovesetGenerator.GenerateEncounters(PKM, ITrainerInfo, ReadOnlyMemory{ushort}, GameVersion[])"/>
    public static IEnumerable<IEncounterable> Encounters(PKM blank, ITrainerInfo info, ReadOnlyMemory<ushort> moves)
    {
        var versions = For(blank);
        // Con la lista vacía el Core volvería a su recorrido por defecto: se devuelve vacío en su lugar.
        return versions.Length == 0 ? [] : EncounterMovesetGenerator.GenerateEncounters(blank, info, moves, versions);
    }
}
