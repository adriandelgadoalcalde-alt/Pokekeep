// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace Pokekeep.Sprites;

/// <summary>
/// Imagen en memoria como la ve PKHeX tras <c>LockBits(Format32bppArgb)</c>: BGRA de 8 bits por canal, alfa NO premultiplicado, fila a fila sin relleno.
/// Es el unico formato intermedio del compositor: toda operacion de <see cref="PixelOps"/> trabaja sobre estos bytes para poder calcar el redondeo de GDI+.
/// </summary>
public sealed class SpriteImage
{
    public int Width { get; }
    public int Height { get; }
    /// <summary>B, G, R, A por pixel; longitud = Width*Height*4.</summary>
    public byte[] Pixels { get; }

    public SpriteImage(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        Width = width; Height = height; Pixels = new byte[width * height * 4];
    }

    public SpriteImage(int width, int height, byte[] pixels)
    {
        if (pixels.Length != width * height * 4) throw new ArgumentException("Longitud de pixeles incorrecta.", nameof(pixels));
        Width = width; Height = height; Pixels = pixels;
    }

    public SpriteImage Clone() => new(Width, Height, (byte[])Pixels.Clone());

    public override string ToString() => $"SpriteImage {Width}x{Height}";
}
