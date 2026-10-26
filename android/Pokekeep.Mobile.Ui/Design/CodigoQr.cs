// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using QRCoder;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Design;

/// <summary>
/// Codigo QR de un mensaje como imagen: la misma llamada de PKHeX (<c>QRCodeGenerator.GenerateQrCode</c>, nivel de correccion Q) pero dibujada por la matriz de modulos en un bitmap de Avalonia,
/// negro sobre blanco con zona de silencio (un QR de colores o sobre tema oscuro no lo leen todos los lectores).
/// </summary>
public static class CodigoQr
{
    /// <summary>Matriz de modulos (verdadero = oscuro), sin zona de silencio, para las pruebas que comparan con el generador de PKHeX.</summary>
    public static bool[][] Matriz(string mensaje)
    {
        using var datos = QRCodeGenerator.GenerateQrCode(mensaje, QRCodeGenerator.ECCLevel.Q);
        var m = datos.ModuleMatrix;
        var tam = m.Count;
        var r = new bool[tam][];
        for (var y = 0; y < tam; y++) { r[y] = new bool[tam]; for (var x = 0; x < tam; x++) r[y][x] = m[y][x]; }
        return r;
    }

    /// <summary>
    /// <see cref="Crear"/> sin lanzar: el generador de QRCoder lanza <c>DataTooLongException</c> (que no es ni <see cref="ArgumentException"/> ni <see cref="InvalidOperationException"/>) si el mensaje no cabe en el
    /// QR de mayor versión. Devuelve null y el motivo en castellano en vez de dejar la excepción sin atrapar en el hilo de interfaz.
    /// </summary>
    public static Bitmap? TryCrear(string mensaje, out string? error, int pixelesPorModulo = 8, int zonaDeSilencio = 4)
    {
        try { error = null; return Crear(mensaje, pixelesPorModulo, zonaDeSilencio); }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            error = ex is QRCoder.Exceptions.DataTooLongException ? IdiomaInterfaz.T("CodigoQr.MensajeDemasiadoLargoCodigo") : ex.Message;
            return null;
        }
    }

    public static Bitmap Crear(string mensaje, int pixelesPorModulo = 8, int zonaDeSilencio = 4)
    {
        var m = Matriz(mensaje);
        var n = m.Length;
        var lado = (n + 2 * zonaDeSilencio) * pixelesPorModulo;
        var wb = new WriteableBitmap(new PixelSize(lado, lado), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        using var fb = wb.Lock();
        var fila = new byte[lado * 4];
        for (var y = 0; y < lado; y++)
        {
            var my = y / pixelesPorModulo - zonaDeSilencio;
            for (var x = 0; x < lado; x++)
            {
                var mx = x / pixelesPorModulo - zonaDeSilencio;
                var oscuro = my >= 0 && my < n && mx >= 0 && mx < n && m[my][mx];
                var v = (byte)(oscuro ? 0 : 255);
                fila[x * 4] = v; fila[x * 4 + 1] = v; fila[x * 4 + 2] = v; fila[x * 4 + 3] = 255;
            }
            System.Runtime.InteropServices.Marshal.Copy(fila, 0, fb.Address + y * fb.RowBytes, fila.Length);
        }
        return wb;
    }
}
