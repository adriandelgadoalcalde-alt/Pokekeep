// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen4;

/// <summary>
/// PK-SAV-045: <c>PoketchDotMatrix</c>, la parte de <c>SAV_Misc4.cs</c> que construye la pantalla del Dot Artist del Pokétch (24×20 puntos de 4 tonos, 2 bits por punto, 120 bytes) a partir de una imagen. Es un calco del algoritmo de PKHeX
/// (<c>TryBuild</c>, <c>TryBuildBrightMap</c>, <c>Build</c> y <c>GetNextLCT</c>) sobre el mapa de brillos ya calculado, SIN <c>System.Drawing</c>: la lectura de la imagen la hace Avalonia (<see cref="Brillos"/>).
/// <para>Se conservan a propósito dos rarezas de PKHeX para que el resultado sea idéntico byte a byte: los contadores de brillo son <c>byte</c> (un tono que aparece 256 veces cuenta cero) y <c>Build</c> hace OR sobre el destino (no lo
/// borra antes), así que importar una imagen sobre un dibujo existente los mezcla. Declarado en el ADR 0065.</para>
/// </summary>
internal static class PoketchMatriz
{
    public const int Alto = 20, Ancho = 24, Puntos = Alto * Ancho;
    private static readonly byte[] TablaDeColores = [248, 168, 88, 8];

    /// <summary>El brillo (el de HSL, el de <c>Color.GetBrightness</c>) de cada punto de una imagen de 24×20, o null si no se puede leer o no mide eso.</summary>
    public static byte[]? Brillos(byte[] imagen)
    {
        if (imagen.Length > 0x80A) return null;   // TryBuild: FileUtil.GetFileSize(fileName) > 0x80A
        Bitmap bmp;
        try { bmp = new Bitmap(new MemoryStream(imagen)); } catch { return null; }
        using (bmp)
        {
            if (bmp.PixelSize.Width != Ancho || bmp.PixelSize.Height != Alto) return null;
            var pixeles = new byte[Puntos * 4];
            var h = System.Runtime.InteropServices.GCHandle.Alloc(pixeles, System.Runtime.InteropServices.GCHandleType.Pinned);
            try { bmp.CopyPixels(new PixelRect(0, 0, Ancho, Alto), h.AddrOfPinnedObject(), pixeles.Length, Ancho * 4); }
            finally { h.Free(); }
            var rgba = bmp.Format == PixelFormat.Rgba8888;
            var brillos = new byte[Puntos];
            for (var i = 0; i < Puntos; i++)
            {
                float r = pixeles[(i * 4) + (rgba ? 0 : 2)] / 255.0f, g = pixeles[(i * 4) + 1] / 255.0f, b = pixeles[(i * 4) + (rgba ? 2 : 0)] / 255.0f;
                var max = Math.Max(r, Math.Max(g, b)); var min = Math.Min(r, Math.Min(g, b));
                brillos[i] = (byte)(0xFF * ((max + min) / 2));
            }
            return brillos;
        }
    }

    /// <summary>
    /// <c>TryBuildBrightMap</c> + <c>Build</c>: si la imagen tiene como mucho 4 tonos distintos, mezcla (OR) el dibujo en <paramref name="destino"/> (120 bytes) y devuelve true; si no, no toca nada y devuelve false.
    /// </summary>
    public static bool Construir(ReadOnlySpan<byte> brillos, Span<byte> destino)
    {
        Span<byte> brightMap = stackalloc byte[Puntos];
        Span<byte> brightCount = stackalloc byte[0x100];
        for (var i = 0; i < Puntos; i++)
        {
            var ig = brillos[i];
            brightMap[i] = ig;
            unchecked { brightCount[ig]++; }   // byte, como PKHeX: 256 puntos del mismo tono dan 0
        }
        var colorCount = brightCount.Length - brightCount.Count<byte>(0);
        if ((colorCount - 1) > 3) return false;   // 1-4
        Build(colorCount, brightCount, brightMap, destino);
        return true;
    }

    private static void Build(int colorCount, Span<byte> brightCount, Span<byte> brightMap, Span<byte> result)
    {
        var errorsMin = int.MaxValue;
        Span<byte> lct = stackalloc byte[4];
        for (var i = 0; i < lct.Length; i++)
            lct[i] = (byte)(colorCount < i + 1 ? 4 : colorCount - i - 1);

        Span<byte> mLct = stackalloc byte[4];
        Span<byte> iBrightCount = stackalloc byte[0x100];
        var ee = 0;
        while (++ee < 1000)
        {
            brightCount.CopyTo(iBrightCount);
            for (int i = 0, j = 0; i < iBrightCount.Length; i++)
            {
                if (iBrightCount[i] > 0)
                    iBrightCount[i] = lct[j++];
            }

            var errorsTotal = 0;
            for (var i = 0; i < Puntos; i++)
                errorsTotal += Math.Abs(brightMap[i] - TablaDeColores[iBrightCount[brightMap[i]]]);
            if (errorsMin > errorsTotal)
            {
                errorsMin = errorsTotal;
                lct.CopyTo(mLct);
            }
            SiguienteLct(lct);
            if (lct[0] >= 4)
                break;
        }
        for (int i = 0, j = 0; i < brightCount.Length; i++)
        {
            if (brightCount[i] > 0)
                brightCount[i] = mLct[j++];
        }

        for (var i = 0; i < brightMap.Length; i++)
            brightMap[i] = brightCount[brightMap[i]];

        for (var i = 0; i < brightMap.Length; i++)
            result[i >> 2] |= (byte)((brightMap[i] & 3) << ((i % 4) << 1));
    }

    private static void SiguienteLct(Span<byte> inp)
    {
        while (true)
        {
            if (++inp[0] < 4)
                continue;

            inp[0] = 0;
            if (++inp[1] < 4)
                continue;

            inp[1] = 0;
            if (++inp[2] < 4)
                continue;

            inp[2] = 0;
            if (++inp[3] < 4)
                continue;

            inp[0] = 4;
            return;
        }
    }
}
