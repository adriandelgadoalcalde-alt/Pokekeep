// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Text;
using PKHeX.Core;
using static PKHeX.Core.Species;

namespace Pokekeep.Sprites;

/// <summary>
/// Nombres de recurso de los sprites, calcados de <c>PKHeX.Drawing.PokeSprite/Util/SpriteName.cs</c> (forma, sexo, Pikachu/Eevee de GG, Alcremie, brillo).
/// A diferencia del original, <c>AllowShinySprite</c> no es estatico: lo decide <see cref="OpcionesSprites.SpritesBrillantes"/>.
/// </summary>
public static class SpriteName
{
    private const char Separator = '_';
    private const char Cosplay = 'c';
    private const char Shiny = 's';
    private const char GGStarter = 'p';

    public static string GetResourceStringBall(byte ball) => $"_ball{ball}";

    public static string GetResourceStringSprite(ushort species, byte form, byte gender, uint formarg, EntityContext context, bool shiny, bool allowShinySprite)
    {
        if (SpeciesDefaultFormSprite.Contains(species)) form = 0;

        // Desde Leyendas Z-A, Xerneas ya no tiene dos formas; el icono del juego siempre lo muestra activo.
        if (species == (ushort)Xerneas && context == EntityContext.Gen9a) form = 1;

        var sb = new StringBuilder(12);
        sb.Append(Separator).Append(species);

        if (form != 0)
        {
            sb.Append(Separator).Append(form);
            if (species == (ushort)Pikachu)
            {
                if (context == EntityContext.Gen6) sb.Append(Cosplay);
                else if (form == 8) sb.Append(GGStarter);
            }
            else if (species == (ushort)Eevee)
            {
                if (form == 1) sb.Append(GGStarter);
            }
        }
        if (gender == 1 && SpeciesGenderedSprite.Contains(species)) sb.Append('f');

        if (species == (ushort)Alcremie)
        {
            if (form == 0) sb.Append(Separator).Append(form);
            sb.Append(Separator).Append(formarg);
        }

        if (shiny && allowShinySprite) sb.Append(Shiny);
        return sb.ToString();
    }

    private static readonly HashSet<ushort> SpeciesDefaultFormSprite =
    [
        (ushort)Mothim, (ushort)Scatterbug, (ushort)Spewpa, (ushort)Rockruff, (ushort)Mimikyu, (ushort)Sinistea,
        (ushort)Polteageist, (ushort)Urshifu, (ushort)Dudunsparce, (ushort)Poltchageist, (ushort)Sinistcha,
    ];

    private static readonly HashSet<ushort> SpeciesGenderedSprite =
    [
        (ushort)Hippopotas, (ushort)Hippowdon, (ushort)Unfezant, (ushort)Frillish, (ushort)Jellicent, (ushort)Pyroar,
    ];
}
