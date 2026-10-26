// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace Pokekeep.Sprites;

/// <summary>
/// Escalador de pixel art propio (algoritmo Scale2x/EPX de Andrea Mazzoleni y Eric Johnston, de dominio publico, con comparacion de colores TOLERANTE): duplica la imagen suavizando las diagonales y
/// las esquinas sin desenfocar. Cada pixel nuevo es una COPIA de un pixel vecino de la imagen original: nunca se inventan colores (un tablero blanco y negro sigue siendo blanco y negro). Se encadena
/// (2 pasos = x4); la vista termina con un reescalado suave desde esa imagen, de modo que el Pokemon se ve nitido, con bordes redondeados y sin cuadros. Los sprites de PKHeX miden 68x56 y no hay arte mayor.
/// </summary>
public static class EscaladorPixelArt
{
    /// <summary>Distancia maxima (suma de |dR|+|dG|+|dB|) para considerar iguales dos colores opacos: los sprites llevan sombreado suave y un umbral de 0 dejaria sin suavizar casi todo.</summary>
    public const int Tolerancia = 36;

    /// <summary>Amplia <paramref name="pasos"/> veces por 2 (2 pasos = x4). Devuelve una imagen nueva.</summary>
    public static SpriteImage Ampliar(SpriteImage origen, int pasos = 2)
    {
        var actual = origen;
        for (var i = 0; i < pasos; i++) actual = Duplicar(actual);
        return actual;
    }

    private static bool Iguales(byte[] p, int a, int b)
    {
        int aa = p[a + 3], ab = p[b + 3];
        if (aa <= 8 || ab <= 8) return aa <= 8 && ab <= 8;
        if (Math.Abs(aa - ab) > 24) return false;
        return Math.Abs(p[a] - p[b]) + Math.Abs(p[a + 1] - p[b + 1]) + Math.Abs(p[a + 2] - p[b + 2]) <= Tolerancia;
    }

    private static SpriteImage Duplicar(SpriteImage s)
    {
        int w = s.Width, h = s.Height, w2 = w * 2;
        var src = s.Pixels; var dst = new byte[w2 * h * 2 * 4];
        void Copiar(int desdeIdx, int x, int y) => Buffer.BlockCopy(src, desdeIdx, dst, (y * w2 + x) * 4, 4);
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                int e = (y * w + x) * 4;
                // Vecinos fuera de la imagen = el propio pixel (borde repetido).
                int b = (Math.Max(y - 1, 0) * w + x) * 4, hh = (Math.Min(y + 1, h - 1) * w + x) * 4;
                int d = (y * w + Math.Max(x - 1, 0)) * 4, f = (y * w + Math.Min(x + 1, w - 1)) * 4;
                int e0 = e, e1 = e, e2 = e, e3 = e;
                if (!Iguales(src, b, hh) && !Iguales(src, d, f))
                {
                    if (Iguales(src, d, b)) e0 = d;
                    if (Iguales(src, b, f)) e1 = f;
                    if (Iguales(src, d, hh)) e2 = d;
                    if (Iguales(src, hh, f)) e3 = f;
                }
                Copiar(e0, 2 * x, 2 * y); Copiar(e1, 2 * x + 1, 2 * y); Copiar(e2, 2 * x, 2 * y + 1); Copiar(e3, 2 * x + 1, 2 * y + 1);
            }
        return new SpriteImage(w2, h * 2, dst);
    }
}
