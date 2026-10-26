// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Buffers.Binary;
using System.IO.Compression;

namespace Pokekeep.Sprites;

/// <summary>
/// Lector y escritor de PNG minimos y 100 % gestionados (sin SkiaSharp ni System.Drawing): lo justo para los sprites de PKHeX.
/// Lee color tipos 0, 2, 3, 4 y 6 de 1 a 8 bits sin entrelazado (con tRNS), siempre a BGRA no premultiplicado sin gestion de color ni gamma, que es lo que ve
/// <c>LockBits(Format32bppArgb)</c> en PKHeX. Escribe siempre RGBA de 8 bits con el filtro de menor suma por fila. La igualdad con GDI+ de los 6.411 recursos
/// la comprueba <c>RecursosTests</c>; no hay dependencia nativa, asi que se comporta igual en el PC y en Android.
/// </summary>
public static class PngLite
{
    private static ReadOnlySpan<byte> Firma => [137, 80, 78, 71, 13, 10, 26, 10];

    public static SpriteImage Decodificar(ReadOnlySpan<byte> png)
    {
        if (png.Length < 8 || !png[..8].SequenceEqual(Firma)) throw new InvalidDataException("No es un PNG.");
        int ancho = 0, alto = 0, profundidad = 0, tipo = -1;
        byte[]? paleta = null, transparencia = null;
        using var idat = new MemoryStream();
        var p = 8;
        while (p + 8 <= png.Length)
        {
            var len = (int)BinaryPrimitives.ReadUInt32BigEndian(png.Slice(p, 4));
            var nombre = png.Slice(p + 4, 4);
            var datos = png.Slice(p + 8, len);
            if (nombre.SequenceEqual("IHDR"u8))
            {
                ancho = (int)BinaryPrimitives.ReadUInt32BigEndian(datos); alto = (int)BinaryPrimitives.ReadUInt32BigEndian(datos[4..]);
                profundidad = datos[8]; tipo = datos[9];
                if (datos[10] != 0 || datos[11] != 0) throw new InvalidDataException("PNG con compresion o filtro desconocidos.");
                if (datos[12] != 0) throw new NotSupportedException("PNG entrelazado.");
            }
            else if (nombre.SequenceEqual("PLTE"u8)) paleta = datos.ToArray();
            else if (nombre.SequenceEqual("tRNS"u8)) transparencia = datos.ToArray();
            else if (nombre.SequenceEqual("IDAT"u8)) idat.Write(datos);
            else if (nombre.SequenceEqual("IEND"u8)) break;
            p += 12 + len;
        }
        if (ancho <= 0 || alto <= 0 || tipo < 0) throw new InvalidDataException("PNG sin cabecera.");
        if (profundidad > 8) throw new NotSupportedException("PNG de 16 bits.");

        var canales = tipo switch { 0 => 1, 2 => 3, 3 => 1, 4 => 2, 6 => 4, _ => throw new NotSupportedException($"Tipo de color {tipo}.") };
        var bitsPixel = canales * profundidad;
        var bytesFila = (ancho * bitsPixel + 7) / 8;
        var bpp = Math.Max(1, bitsPixel / 8); // bytes por pixel para el filtro
        var crudo = new byte[(bytesFila + 1) * alto];
        idat.Position = 0;
        using (var z = new ZLibStream(idat, CompressionMode.Decompress)) z.ReadExactly(crudo);

        // Quitar el filtro en el sitio.
        for (var y = 0; y < alto; y++)
        {
            var f = crudo[y * (bytesFila + 1)];
            var fila = crudo.AsSpan(y * (bytesFila + 1) + 1, bytesFila);
            var previa = y == 0 ? default : crudo.AsSpan((y - 1) * (bytesFila + 1) + 1, bytesFila);
            switch (f)
            {
                case 0: break;
                case 1: for (var i = bpp; i < bytesFila; i++) fila[i] += fila[i - bpp]; break;
                case 2: if (y > 0) for (var i = 0; i < bytesFila; i++) fila[i] += previa[i]; break;
                case 3:
                    for (var i = 0; i < bytesFila; i++)
                    {
                        var a = i >= bpp ? fila[i - bpp] : 0; var b = y > 0 ? previa[i] : 0;
                        fila[i] += (byte)((a + b) >> 1);
                    }
                    break;
                case 4:
                    for (var i = 0; i < bytesFila; i++)
                    {
                        int a = i >= bpp ? fila[i - bpp] : 0, b = y > 0 ? previa[i] : 0, c = (i >= bpp && y > 0) ? previa[i - bpp] : 0;
                        var pa = Math.Abs(b - c); var pb = Math.Abs(a - c); var pc = Math.Abs(a + b - 2 * c);
                        fila[i] += (byte)(pa <= pb && pa <= pc ? a : pb <= pc ? b : c);
                    }
                    break;
                default: throw new InvalidDataException($"Filtro PNG {f}.");
            }
        }

        var img = new SpriteImage(ancho, alto);
        var dst = img.Pixels;
        int claveR = -1, claveG = -1, claveB = -1;
        if (transparencia is not null && tipo is 0 && transparencia.Length >= 2) claveR = BinaryPrimitives.ReadUInt16BigEndian(transparencia);
        if (transparencia is not null && tipo is 2 && transparencia.Length >= 6)
        {
            claveR = BinaryPrimitives.ReadUInt16BigEndian(transparencia); claveG = BinaryPrimitives.ReadUInt16BigEndian(transparencia.AsSpan(2)); claveB = BinaryPrimitives.ReadUInt16BigEndian(transparencia.AsSpan(4));
        }
        var maxVal = (1 << profundidad) - 1;
        for (var y = 0; y < alto; y++)
        {
            var fila = crudo.AsSpan(y * (bytesFila + 1) + 1, bytesFila);
            for (var x = 0; x < ancho; x++)
            {
                var o = (y * ancho + x) * 4;
                int r, g, b, a = 255;
                switch (tipo)
                {
                    case 6: { var i = x * 4; r = fila[i]; g = fila[i + 1]; b = fila[i + 2]; a = fila[i + 3]; break; }
                    case 2:
                    {
                        var i = x * 3; r = fila[i]; g = fila[i + 1]; b = fila[i + 2];
                        if (claveR >= 0 && r == claveR && g == claveG && b == claveB) a = 0;
                        break;
                    }
                    case 4: { var i = x * 2; r = g = b = fila[i]; a = fila[i + 1]; break; }
                    case 0:
                    {
                        var v = Muestra(fila, x, profundidad); r = g = b = profundidad == 8 ? v : v * 255 / maxVal;
                        if (claveR >= 0 && v == claveR) a = 0;
                        break;
                    }
                    default:
                    {
                        var idx = Muestra(fila, x, profundidad);
                        if (paleta is null || idx * 3 + 2 >= paleta.Length) throw new InvalidDataException("Indice de paleta fuera de rango.");
                        r = paleta[idx * 3]; g = paleta[idx * 3 + 1]; b = paleta[idx * 3 + 2];
                        if (transparencia is not null && idx < transparencia.Length) a = transparencia[idx];
                        break;
                    }
                }
                dst[o] = (byte)b; dst[o + 1] = (byte)g; dst[o + 2] = (byte)r; dst[o + 3] = (byte)a;
            }
        }
        return img;
    }

    private static int Muestra(ReadOnlySpan<byte> fila, int x, int profundidad)
    {
        if (profundidad == 8) return fila[x];
        var bit = x * profundidad;
        var b = fila[bit >> 3];
        var desplaza = 8 - profundidad - (bit & 7);
        return (b >> desplaza) & ((1 << profundidad) - 1);
    }

    /// <summary>Escribe un PNG RGBA de 8 bits, determinista (mismos bytes para la misma imagen).</summary>
    public static byte[] Codificar(SpriteImage img)
    {
        var w = img.Width; var h = img.Height; var stride = w * 4;
        var crudo = new byte[(stride + 1) * h];
        var candidatos = new byte[5][];
        for (var k = 0; k < 5; k++) candidatos[k] = new byte[stride];
        for (var y = 0; y < h; y++)
        {
            var fila = new byte[stride];
            for (var x = 0; x < w; x++)
            {
                var o = (y * w + x) * 4;
                fila[x * 4] = img.Pixels[o + 2]; fila[x * 4 + 1] = img.Pixels[o + 1]; fila[x * 4 + 2] = img.Pixels[o]; fila[x * 4 + 3] = img.Pixels[o + 3];
            }
            // La fila previa y la actual ya vienen en RGBA; se prueba cada filtro y se queda el de menor suma de |valor con signo|.
            var previa = y == 0 ? new byte[stride] : PrevRgba(img, y - 1);
            var mejor = 0; var mejorSuma = long.MaxValue;
            for (var f = 0; f < 5; f++)
            {
                var c = candidatos[f]; long suma = 0;
                for (var i = 0; i < stride; i++)
                {
                    int a = i >= 4 ? fila[i - 4] : 0, b = previa[i], cc = i >= 4 ? previa[i - 4] : 0;
                    byte pred = f switch
                    {
                        0 => 0, 1 => (byte)a, 2 => (byte)b, 3 => (byte)((a + b) >> 1),
                        _ => (byte)Paeth(a, b, cc),
                    };
                    var v = (byte)(fila[i] - pred);
                    c[i] = v;
                    suma += v < 128 ? v : 256 - v;
                }
                if (suma < mejorSuma) { mejorSuma = suma; mejor = f; }
            }
            crudo[y * (stride + 1)] = (byte)mejor;
            candidatos[mejor].CopyTo(crudo, y * (stride + 1) + 1);
        }

        using var comprimido = new MemoryStream();
        using (var z = new ZLibStream(comprimido, CompressionLevel.SmallestSize, leaveOpen: true)) z.Write(crudo);

        using var salida = new MemoryStream();
        salida.Write(Firma);
        var ihdr = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(ihdr, (uint)w); BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(4), (uint)h);
        ihdr[8] = 8; ihdr[9] = 6;
        Trozo(salida, "IHDR"u8, ihdr);
        Trozo(salida, "IDAT"u8, comprimido.ToArray());
        Trozo(salida, "IEND"u8, []);
        return salida.ToArray();
    }

    private static byte[] PrevRgba(SpriteImage img, int y)
    {
        var w = img.Width; var fila = new byte[w * 4];
        for (var x = 0; x < w; x++)
        {
            var o = (y * w + x) * 4;
            fila[x * 4] = img.Pixels[o + 2]; fila[x * 4 + 1] = img.Pixels[o + 1]; fila[x * 4 + 2] = img.Pixels[o]; fila[x * 4 + 3] = img.Pixels[o + 3];
        }
        return fila;
    }

    private static int Paeth(int a, int b, int c)
    {
        var pa = Math.Abs(b - c); var pb = Math.Abs(a - c); var pc = Math.Abs(a + b - 2 * c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    private static void Trozo(Stream s, ReadOnlySpan<byte> nombre, ReadOnlySpan<byte> datos)
    {
        Span<byte> cab = stackalloc byte[8];
        BinaryPrimitives.WriteUInt32BigEndian(cab, (uint)datos.Length);
        nombre.CopyTo(cab[4..]);
        s.Write(cab);
        s.Write(datos);
        var crc = Crc32(Crc32(0xFFFFFFFF, nombre), datos) ^ 0xFFFFFFFF;
        Span<byte> c = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(c, crc);
        s.Write(c);
    }

    private static readonly uint[] TablaCrc = CrearTablaCrc();

    private static uint[] CrearTablaCrc()
    {
        var t = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            t[n] = c;
        }
        return t;
    }

    private static uint Crc32(uint crc, ReadOnlySpan<byte> datos)
    {
        foreach (var b in datos) crc = TablaCrc[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc;
    }
}
