// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using static PKHeX.Core.MoveType;

namespace Pokekeep.Sprites;

/// <summary>Color RGB opaco (sin dependencia de System.Drawing).</summary>
public readonly record struct Rgb(byte R, byte G, byte B)
{
    public static Rgb De(int r, int g, int b) => new((byte)r, (byte)g, (byte)b);
    /// <summary>RGB de un entero ARGB (como <c>Color.FromArgb(int)</c>, descartando el alfa).</summary>
    public static Rgb DeArgb(int argb) => new((byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
}

/// <summary>Colores de los tipos para los fondos de Teratipo, de <c>PKHeX.Drawing.PokeSprite/Util/TypeColor.cs</c> (sprites del juego principal).</summary>
public static class ColorTipo
{
    public static Rgb DeTipo(byte type) => (MoveType)type switch
    {
        Normal => Rgb.De(159, 161, 159),
        Fighting => Rgb.De(255, 128, 0),
        Flying => Rgb.De(129, 185, 239),
        Poison => Rgb.De(143, 65, 203),
        Ground => Rgb.De(145, 81, 33),
        Rock => Rgb.De(175, 169, 129),
        Bug => Rgb.De(145, 161, 25),
        Ghost => Rgb.De(112, 65, 112),
        Steel => Rgb.De(96, 161, 184),
        Fire => Rgb.De(230, 40, 41),
        Water => Rgb.De(41, 128, 239),
        Grass => Rgb.De(63, 161, 41),
        Electric => Rgb.De(250, 192, 0),
        Psychic => Rgb.De(239, 65, 121),
        Ice => Rgb.De(63, 216, 255),
        Dragon => Rgb.De(80, 97, 225),
        Dark => Rgb.De(80, 65, 63),
        Fairy => Rgb.De(239, 113, 239),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    /// <summary>Color del Teratipo Astral: <c>Color.LightYellow</c>.</summary>
    public static Rgb Astral => Rgb.De(255, 255, 224);

    public static Rgb DeTera(byte elementalType) => elementalType == TeraTypeUtil.Stellar ? Astral : DeTipo(elementalType);
}
