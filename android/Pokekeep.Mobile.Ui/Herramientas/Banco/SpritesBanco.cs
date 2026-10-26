// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Sprites;

namespace Pokekeep.Mobile.Ui.Herramientas.Banco;

/// <summary>
/// Con qué juego de sprites se dibuja un Pokémon del banco (X38). El banco no pertenece a ninguna partida: cada Pokémon se dibuja con el sprite de SU especie y forma, no con el de la partida abierta.
/// El juego de sprites que PKHeX elige para esa partida (clásico en Gen. 1 a 8) no tiene imagen para las especies de Gen. 9 (<c>b_unknown</c>); en ese caso se usa el primero de los otros dos que sí la tiene
/// (ilustración y, si tampoco, mugshot), que es lo mejor que PKHeX trae para esa especie y forma.
/// </summary>
public static class SpritesBanco
{
    public static ModoSprites Modo(ModoSprites preferido, int especie, int forma, int contexto, bool variocolor)
    {
        if (especie <= 0 || !Design.Sprites.Configurado) return preferido;
        foreach (var m in new[] { preferido, ModoSprites.Artwork, ModoSprites.Mugshot, ModoSprites.Clasico })
        {
            var nombre = Design.Sprites.Compartido(m, GameVersion.Any).NombreRecursoBase((ushort)especie, (byte)Math.Max(0, forma), 0, 0, variocolor, (EntityContext)contexto);
            if (nombre is not null && !nombre.EndsWith("unknown", StringComparison.Ordinal)) return m;
        }
        return preferido;
    }
}
