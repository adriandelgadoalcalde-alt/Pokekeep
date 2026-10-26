// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Runtime.InteropServices;

namespace Pokekeep.Sprites;

/// <summary>
/// Operaciones de imagen de <c>PKHeX.Drawing/ImageUtil.cs</c> reescritas sobre <see cref="SpriteImage"/> (BGRA no premultiplicado), con el MISMO redondeo que GDI+.
/// <para>
/// El unico punto delicado es <see cref="BlendPixel"/>: <c>Graphics.DrawImage</c> sobre un <c>Format32bppArgb</c> NO calcula la mezcla en espacio recto, sino en premultiplicado con
/// redondeo a mas cercano en cada producto y des-premultiplicado con un reciproco truncado (<c>(255&lt;&lt;16)/alfa</c>). Se dedujo con la tabla completa de GDI+ (15 alfas de destino
/// x 256 de origen x 256 x 256 colores: 0 diferencias) y la suite <c>MezclaGdiTests</c> lo vuelve a comprobar contra GDI+ en cada ejecucion. Consecuencia visible: incluso un
/// pixel con alfa de origen 0 sobre un destino semitransparente cambia ligeramente de color, y una copia (<c>new Bitmap(Image)</c>) cuantiza los colores semitransparentes.
/// </para>
/// </summary>
public static class PixelOps
{
    private static readonly int[] Reciproco = CrearReciprocos();

    private static int[] CrearReciprocos()
    {
        var r = new int[256];
        for (var a = 1; a < 256; a++) r[a] = (255 << 16) / a;
        return r;
    }

    private static int Div255(int x) => (x + 127) / 255;

    /// <summary>Mezcla un pixel de origen sobre uno de destino (SourceOver de GDI+ en ARGB no premultiplicado). Escribe el resultado en <paramref name="d"/>.</summary>
    public static void BlendPixel(Span<byte> d, ReadOnlySpan<byte> s)
    {
        int sa = s[3], da = d[3];
        var ra = sa + Div255(da * (255 - sa));
        if (ra == 0) { d[0] = d[1] = d[2] = d[3] = 0; return; }
        var rec = Reciproco[ra];
        var inv = 255 - sa;
        for (var c = 0; c < 3; c++)
        {
            var sp = Div255(s[c] * sa);
            var dp = Div255(d[c] * da);
            var rp = sp + Div255(dp * inv);
            var v = (int)(((long)rp * rec) >> 16);
            d[c] = (byte)(v > 255 ? 255 : v);
        }
        d[3] = (byte)ra;
    }

    /// <summary>Pinta <paramref name="origen"/> sobre <paramref name="destino"/> (modificandolo) en (x, y), recortando lo que se salga: <c>Graphics.DrawImage(img, x, y, w, h)</c> a escala 1:1.</summary>
    public static void DrawOver(SpriteImage destino, SpriteImage origen, int x, int y)
    {
        var x0 = Math.Max(0, x); var y0 = Math.Max(0, y);
        var x1 = Math.Min(destino.Width, x + origen.Width); var y1 = Math.Min(destino.Height, y + origen.Height);
        if (x1 <= x0 || y1 <= y0) return;
        var dp = destino.Pixels.AsSpan(); var sp = origen.Pixels.AsSpan();
        for (var py = y0; py < y1; py++)
        {
            var di = (py * destino.Width + x0) * 4;
            var si = ((py - y) * origen.Width + (x0 - x)) * 4;
            for (var px = x0; px < x1; px++, di += 4, si += 4)
                BlendPixel(dp.Slice(di, 4), sp.Slice(si, 4));
        }
    }

    /// <summary><c>new Bitmap(Image)</c>: copia por <c>DrawImage</c> sobre un lienzo transparente (cuantiza los pixeles semitransparentes y pone a 0 los del todo transparentes).</summary>
    public static SpriteImage Copia(SpriteImage img)
    {
        var r = new SpriteImage(img.Width, img.Height);
        DrawOver(r, img, 0, 0);
        return r;
    }

    /// <summary><c>ImageUtil.LayerImage(base, over, x, y)</c>: copia de <paramref name="baseLayer"/> con <paramref name="overLayer"/> encima.</summary>
    public static SpriteImage Layer(SpriteImage baseLayer, SpriteImage overLayer, int x, int y)
    {
        var r = Copia(baseLayer);
        DrawOver(r, overLayer, x, y);
        return r;
    }

    /// <summary><c>ImageUtil.LayerImage(base, over, x, y, transparency)</c>: el origen se copia con su opacidad cambiada antes de pintarlo.</summary>
    public static SpriteImage Layer(SpriteImage baseLayer, SpriteImage overLayer, int x, int y, double transparency)
    {
        var o = overLayer.Clone();
        ChangeOpacity(o, transparency);
        return Layer(baseLayer, o, x, y);
    }

    /// <summary><c>Bitmap.ChangeOpacity</c>, en el sitio: alfa = (byte)(alfa * trans). Ignora valores fuera de (0,01; 1].</summary>
    public static void ChangeOpacity(SpriteImage img, double trans)
    {
        if (trans is <= 0.01f or > 1f) return;
        var d = img.Pixels;
        for (var i = d.Length - 4; i >= 0; i -= 4) d[i + 3] = (byte)(d[i + 3] * trans);
    }

    /// <summary><c>Bitmap.ToGrayscale</c>, en el sitio (intensidad de 0 a 1).</summary>
    public static void ToGrayscale(SpriteImage img, float intensity)
    {
        if (intensity is <= 0.01f or > 1f) return;
        var data = img.Pixels;
        if (intensity >= 0.999f)
        {
            for (var i = data.Length - 4; i >= 0; i -= 4)
            {
                if (data[i + 3] == 0) continue;
                var grey = (byte)((0.3 * data[i + 2]) + (0.59 * data[i + 1]) + (0.11 * data[i + 0]));
                data[i + 0] = grey; data[i + 1] = grey; data[i + 2] = grey;
            }
            return;
        }
        var inverse = 1f - intensity;
        for (var i = data.Length - 4; i >= 0; i -= 4)
        {
            if (data[i + 3] == 0) continue;
            var greyS = (byte)((0.3 * data[i + 2]) + (0.59 * data[i + 1]) + (0.11 * data[i + 0]));
            data[i + 0] = (byte)((data[i + 0] * inverse) + (greyS * intensity));
            data[i + 1] = (byte)((data[i + 1] * inverse) + (greyS * intensity));
            data[i + 2] = (byte)((data[i + 2] * inverse) + (greyS * intensity));
        }
    }

    private const byte UmbralMitad = 0x80;

    private static int Argb(byte a, byte r, byte g, byte b) => (a << 24) | (r << 16) | (g << 8) | b;

    /// <summary><c>Bitmap.BlendTransparentTo(color, trans, start, end)</c>: los pixeles casi transparentes pasan a ser el color; los semiopacos se mezclan (20 % el original).</summary>
    public static void BlendTransparentTo(SpriteImage img, byte r, byte g, byte b, byte trans, int start = 0, int end = -1)
    {
        if (end == -1) end = img.Pixels.Length;
        var data = img.Pixels.AsSpan(start, end - start);
        var arr = MemoryMarshal.Cast<byte, int>(data);
        var value = Argb(trans, r, g, b);
        for (var i = data.Length - 4; i >= 0; i -= 4)
        {
            var alpha = data[i + 3];
            if (alpha < UmbralMitad) arr[i >> 2] = value;
            else if (alpha != 0xFF) arr[i >> 2] = BlendColor(arr[i >> 2], value);
        }
    }

    // Favorece mucho al segundo color (el nuevo).
    private static int BlendColor(int color1, int color2, double amount = 0.2)
    {
        var a1 = (color1 >> 24) & 0xFF; var r1 = (color1 >> 16) & 0xFF; var g1 = (color1 >> 8) & 0xFF; var b1 = color1 & 0xFF;
        var a2 = (color2 >> 24) & 0xFF; var r2 = (color2 >> 16) & 0xFF; var g2 = (color2 >> 8) & 0xFF; var b2 = color2 & 0xFF;
        var a = (byte)((a1 * amount) + (a2 * (1 - amount)));
        var r = (byte)((r1 * amount) + (r2 * (1 - amount)));
        var g = (byte)((g1 * amount) + (g2 * (1 - amount)));
        var b = (byte)((b1 * amount) + (b2 * (1 - amount)));
        return (a << 24) | (r << 16) | (g << 8) | b;
    }

    /// <summary><c>Bitmap.ChangeTransparentTo(color, trans)</c>: los pixeles con alfa menor que 0x80 pasan a ser el color con la opacidad dada.</summary>
    public static void ChangeTransparentTo(SpriteImage img, byte r, byte g, byte b, byte trans, int start = 0, int end = -1)
    {
        if (end == -1) end = img.Pixels.Length;
        var data = img.Pixels.AsSpan(start, end - start);
        var arr = MemoryMarshal.Cast<byte, int>(data);
        var value = Argb(trans, r, g, b);
        for (var i = data.Length - 4; i >= 0; i -= 4)
            if (data[i + 3] < UmbralMitad) arr[i >> 2] = value;
    }

    /// <summary><c>Bitmap.WritePixels(color, start, end)</c>: rellena [start, end) (en bytes) con el color opaco.</summary>
    public static void WritePixels(SpriteImage img, byte r, byte g, byte b, int start, int end)
    {
        var arr = MemoryMarshal.Cast<byte, int>(img.Pixels.AsSpan(start, end - start));
        arr.Fill(Argb(255, r, g, b));
    }

    /// <summary><c>ImageUtil.SetAllUsedPixelsOpaque</c>.</summary>
    public static void SetAllUsedPixelsOpaque(Span<byte> data, byte threshold = UmbralMitad)
    {
        for (var i = data.Length - 4; i >= 0; i -= 4)
            if (data[i + 3] >= threshold) data[i + 3] = 0xFF;
    }

    /// <summary><c>ImageUtil.RemovePixels</c>.</summary>
    public static void RemovePixels(Span<byte> pixels, ReadOnlySpan<byte> original, byte threshold = UmbralMitad)
    {
        var arr = MemoryMarshal.Cast<byte, int>(pixels);
        for (var i = original.Length - 4; i >= 0; i -= 4)
            if (original[i + 3] >= threshold) arr[i >> 2] = 0;
    }

    /// <summary><c>ImageUtil.GlowEdges</c>: halo de color alrededor de los pixeles opacos (el canal azul hace de acumulador).</summary>
    public static void GlowEdges(Span<byte> data, byte blue, byte green, byte red, int width, int reach = 3, double amount = 0.0777)
    {
        for (var i = data.Length - 4; i >= 0; i -= 4) data[i] = 0;
        var stride = width * 4;
        var height = data.Length / stride;
        for (var i = data.Length - 4; i >= 0; i -= 4)
        {
            if (data[i + 3] < UmbralMitad) continue;
            var x = (i % stride) / 4;
            var y = i / stride;
            var left = Math.Max(0, x - reach); var right = Math.Min(width - 1, x + reach);
            var top = Math.Max(0, y - reach); var bottom = Math.Min(height - 1, y + reach);
            for (var ix = left; ix <= right; ix++)
                for (var iy = top; iy <= bottom; iy++)
                {
                    var c = 4 * (ix + (iy * width));
                    ref var b = ref data[c];
                    b += (byte)(amount * (0xFF - b));
                }
        }
        for (var i = data.Length - 4; i >= 0; i -= 4)
        {
            var transparency = data[i];
            if (transparency == 0) continue;
            data[i + 0] = blue; data[i + 1] = green; data[i + 2] = red; data[i + 3] = transparency;
        }
    }
}
