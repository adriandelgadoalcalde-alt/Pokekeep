// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Sprites;

namespace Pokekeep.Mobile.Ui.Design;

/// <summary>
/// «Mi fondo» de Pokémon Box Rubí y Zafiro (el fondo 21, índice 20): una imagen propia de 568×216 comprimida en CMPR que la partida guarda y que se reparte entre cada pareja de cajas (la izquierda en la caja par y la derecha
/// en la impar). PKHeX 26.10.18 la dibuja sobre el fondo «box_wp21rs» en el rectángulo (5, 5, 146, 106) (<c>WallpaperUtil.DrawMyWallpaper</c>, que escala con GDI+ y suavizado). Aquí se hace en memoria con el muestreo bilineal
/// de los centros de píxel: el marco de 5 píxeles es idéntico al de PKHeX, salvo el anillo de un píxel pegado al rectángulo que GDI+ suaviza, y el interior no es bit a bit el mismo (GDI+ no se puede calcar sin Windows; la prueba contra PKHeX mide cuánto se aparta).
/// </summary>
public static class MiFondoRsBox
{
    /// <summary>Índice (base 0) del fondo «Mi fondo» entre los de la partida.</summary>
    public const int Indice = 20;

    public const int Izquierda = 5, Arriba = 5, Ancho = 146, Alto = 106;

    /// <summary>Dibuja la mitad de «Mi fondo» que toca a <paramref name="caja"/> sobre una copia de <paramref name="fondo"/>.</summary>
    public static SpriteImage Componer(SpriteImage fondo, ReadOnlySpan<byte> comprimido, int caja)
    {
        var imagen = CMPR.Decompress(comprimido, SAV3RSBox.WP_WIDTH, SAV3RSBox.WP_HEIGHT);   // BGRA, como SpriteImage
        var resultado = fondo.Clone();
        const int mitad = SAV3RSBox.WP_WIDTH / 2, alto = SAV3RSBox.WP_HEIGHT;
        var origenX = mitad * (caja % 2);
        var escalaX = (double)mitad / Ancho; var escalaY = (double)alto / Alto;
        Span<byte> origen = stackalloc byte[4];
        for (var y = 0; y < Alto; y++)
        {
            var sy = Math.Clamp((y + 0.5) * escalaY - 0.5, 0, alto - 1);
            var y0 = (int)Math.Floor(sy); var y1 = Math.Min(y0 + 1, alto - 1); var fy = sy - y0;
            for (var x = 0; x < Ancho; x++)
            {
                var sx = Math.Clamp((x + 0.5) * escalaX - 0.5, 0, mitad - 1);
                var x0 = (int)Math.Floor(sx); var x1 = Math.Min(x0 + 1, mitad - 1); var fx = sx - x0;
                for (var c = 0; c < 4; c++)
                {
                    double V(int xx, int yy) => imagen[((yy * SAV3RSBox.WP_WIDTH) + origenX + xx) * 4 + c];
                    var arriba = (V(x0, y0) * (1 - fx)) + (V(x1, y0) * fx);
                    var abajo = (V(x0, y1) * (1 - fx)) + (V(x1, y1) * fx);
                    origen[c] = (byte)Math.Clamp(Math.Round((arriba * (1 - fy)) + (abajo * fy)), 0, 255);
                }
                var d = (((Arriba + y) * resultado.Width) + Izquierda + x) * 4;
                PixelOps.BlendPixel(resultado.Pixels.AsSpan(d, 4), origen);
            }
        }
        return resultado;
    }
}
