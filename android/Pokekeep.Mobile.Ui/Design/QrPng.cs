// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.IO.Compression;

namespace Pokekeep.Mobile.Ui.Design;

/// <summary>
/// El código QR de un mensaje como PNG RGBA de 8 bits (negro sobre blanco opaco, con zona de silencio; en color y no en grises porque el decodificador de imágenes de Avalonia no deja leer los píxeles de un PNG de grises y el lector de QR de la aplicación los necesita), escrito a mano y sin Avalonia: la misma matriz de módulos que <see cref="CodigoQr.Matriz"/>
/// (la de <c>QRCodeGenerator</c> con corrección Q, como PKHeX). Es lo que se copia al portapapeles o se manda a la hoja de compartir; las pruebas lo decodifican sin pantalla.
/// </summary>
public static class QrPng
{
    public static byte[] Crear(string mensaje, int pixelesPorModulo = 8, int zonaDeSilencio = 4) => Crear(CodigoQr.Matriz(mensaje), pixelesPorModulo, zonaDeSilencio);

    public static byte[] Crear(bool[][] matriz, int pixelesPorModulo = 8, int zonaDeSilencio = 4)
    {
        var n = matriz.Length;
        var lado = (n + 2 * zonaDeSilencio) * pixelesPorModulo;
        using var crudo = new MemoryStream(lado * (lado * 4 + 1));
        var fila = new byte[lado * 4 + 1];   // el primer byte de cada fila es el filtro (0 = ninguno)
        for (var y = 0; y < lado; y++)
        {
            var my = y / pixelesPorModulo - zonaDeSilencio;
            for (var x = 0; x < lado; x++)
            {
                var mx = x / pixelesPorModulo - zonaDeSilencio;
                var oscuro = my >= 0 && my < n && mx >= 0 && mx < n && matriz[my][mx];
                var v = oscuro ? (byte)0 : (byte)255;
                var i = 1 + x * 4;
                fila[i] = v; fila[i + 1] = v; fila[i + 2] = v; fila[i + 3] = 255;
            }
            crudo.Write(fila);
        }
        using var comprimido = new MemoryStream();
        using (var z = new ZLibStream(comprimido, CompressionLevel.Optimal, leaveOpen: true)) z.Write(crudo.GetBuffer(), 0, (int)crudo.Length);

        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        Span<byte> ihdr = stackalloc byte[13];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(ihdr, (uint)lado);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(ihdr[4..], (uint)lado);
        ihdr[8] = 8; ihdr[9] = 6; ihdr[10] = 0; ihdr[11] = 0; ihdr[12] = 0;   // 8 bits, RGBA, sin entrelazado
        Trozo(png, "IHDR", ihdr);
        Trozo(png, "IDAT", comprimido.GetBuffer().AsSpan(0, (int)comprimido.Length));
        Trozo(png, "IEND", []);
        return png.ToArray();
    }

    private static void Trozo(Stream destino, string tipo, ReadOnlySpan<byte> datos)
    {
        Span<byte> cab = stackalloc byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(cab, (uint)datos.Length);
        for (var i = 0; i < 4; i++) cab[4 + i] = (byte)tipo[i];
        destino.Write(cab);
        destino.Write(datos);
        var crc = Crc32(cab[4..], 0xFFFFFFFF);
        crc = Crc32(datos, crc) ^ 0xFFFFFFFF;
        Span<byte> pie = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(pie, crc);
        destino.Write(pie);
    }

    private static readonly uint[] Tabla = Enumerable.Range(0, 256).Select(i => { var c = (uint)i; for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1; return c; }).ToArray();

    private static uint Crc32(ReadOnlySpan<byte> datos, uint crc)
    {
        foreach (var b in datos) crc = Tabla[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc;
    }
}
