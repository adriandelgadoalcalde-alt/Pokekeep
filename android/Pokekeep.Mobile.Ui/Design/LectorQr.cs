// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ZXing;
using ZXing.Common;

namespace Pokekeep.Mobile.Ui.Design;

/// <summary>
/// Lee el mensaje de un codigo QR de una IMAGEN (una captura de pantalla de otro movil o de PKHeX, una foto de la galeria): sin camara y sin permisos, la imagen la elige el usuario con el selector del sistema.
/// El mensaje es el que <c>QRMessageUtil.GetMessage</c> de PKHeX genera (en la 7.ª generacion son bytes sueltos: se devuelven byte a byte como caracteres, igual que lo escribe PKHeX).
/// </summary>
public static class LectorQr
{
    /// <summary>El mensaje del primer QR de la imagen, o null si no hay ninguno legible.</summary>
    public static string? Leer(byte[] imagen)
    {
        using var flujo = new MemoryStream(imagen);
        Bitmap bmp;
        try { bmp = new Bitmap(flujo); } catch { return null; }
        using (bmp)
            return Leer(bmp);
    }

    public static string? Leer(Bitmap bmp)
    {
        var tam = bmp.PixelSize;
        if (tam.Width <= 0 || tam.Height <= 0) return null;
        // Una imagen enorme (foto de 48 MP) se reduce: el decodificador no necesita mas de ~1600 px de lado y asi no agota la memoria del movil.
        var escala = Math.Min(1.0, 1600.0 / Math.Max(tam.Width, tam.Height));
        var ancho = Math.Max(1, (int)(tam.Width * escala)); var alto = Math.Max(1, (int)(tam.Height * escala));
        using var reducida = escala < 1.0 ? bmp.CreateScaledBitmap(new PixelSize(ancho, alto), BitmapInterpolationMode.MediumQuality) : null;
        var origen = reducida ?? bmp;
        // Copia los pixeles en el formato nativo del bitmap (BGRA o RGBA segun la plataforma) y se lo dice al decodificador.
        var pixeles = new byte[ancho * alto * 4];
        var h = System.Runtime.InteropServices.GCHandle.Alloc(pixeles, System.Runtime.InteropServices.GCHandleType.Pinned);
        try { origen.CopyPixels(new PixelRect(0, 0, ancho, alto), h.AddrOfPinnedObject(), pixeles.Length, ancho * 4); }
        finally { h.Free(); }
        var formato = origen.Format == PixelFormat.Rgba8888 ? RGBLuminanceSource.BitmapFormat.RGBA32 : RGBLuminanceSource.BitmapFormat.BGRA32;
        var lector = new BarcodeReaderGeneric
        {
            AutoRotate = true,
            Options = new DecodingOptions { TryHarder = true, PossibleFormats = [BarcodeFormat.QR_CODE], TryInverted = true },
        };
        var r = lector.Decode(new RGBLuminanceSource(pixeles, ancho, alto, formato));
        if (r is null) return null;
        if (r.ResultMetadata is { } meta && meta.TryGetValue(ResultMetadataType.BYTE_SEGMENTS, out var seg) && seg is IEnumerable<byte[]> trozos)
        {
            var lista = trozos.SelectMany(t => t).ToArray();
            if (lista.Length > 0) return new string([.. lista.Select(b => (char)b)]);
        }
        return r.Text;
    }
}
