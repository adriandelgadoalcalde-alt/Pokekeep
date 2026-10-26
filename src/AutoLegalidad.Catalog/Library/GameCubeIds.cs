// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;

namespace AutoLegalidad.Catalog.Library;

/// <summary>
/// Identificadores de Colosseum y XD como ORIGEN y como destino de la Libreria. Colosseum y XD no son juegos guardables sueltos (comparten <see cref="GameVersion.CXD"/>): son valores del enum
/// que el Core coloca DESPUES de los guardables y cuyo numero se desplaza cada vez que el Core anade juegos (62 y 63 con PKHeX 26.08.26; 66 y 67 desde la 26.10.18, que anadio WI, WA, FRX y LGX).
/// El catalogo guarda <c>(int)GameVersion</c> en <c>encounter.origin_game</c> y las consultas lo comparan con estos valores, asi que nunca se escriben a mano.
/// </summary>
public static class GameCubeIds
{
    public const int Colosseum = (int)GameVersion.COLO;
    public const int Xd = (int)GameVersion.XD;

    public static bool IsGameCube(int id) => id is Colosseum or Xd;
}
