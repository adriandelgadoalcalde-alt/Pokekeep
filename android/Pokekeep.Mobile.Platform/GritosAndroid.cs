// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Android.Media;
using Pokekeep.Mobile.Ui;
using Pokekeep.Mobile.Ui.Cajas;

namespace Pokekeep.Mobile.Platform;

public sealed class GritosAndroid : IReproductorGritos
{
    private MediaPlayer? _actual;

    /// <summary>
    /// Los gritos que trae el APK (X11): <c>gritos/manifest.json</c> y <c>gritos/gritos.pkpack</c>, sin comprimir (<c>AndroidStoreUncompressedFileExtensions</c>). Cada grito se lee por rango con el descriptor del asset
    /// (nunca se carga el paquete entero: pesa ~25 MB) y se saca a la caché de la app para el MediaPlayer. Null si el APK se compiló sin paquete.
    /// </summary>
    public static PaqueteGritos? CrearPaquete(Android.Content.Context contexto)
    {
        try
        {
            using var m = contexto.Assets!.Open("gritos/manifest.json");
            using var ms = new MemoryStream();
            m.CopyTo(ms);
            return PaqueteGritos.Cargar(ms.ToArray(), (archivo, desplazamiento, longitud) => LeerRango(contexto, "gritos/" + archivo, desplazamiento, longitud), Path.Combine(contexto.CacheDir!.AbsolutePath, "gritos"));
        }
        catch (Exception ex) when (ex is Java.IO.IOException or Java.Lang.Exception or IOException) { return null; }
    }

    private static byte[] LeerRango(Android.Content.Context contexto, string asset, long desplazamiento, int longitud)
    {
        try
        {
            using var fd = contexto.Assets!.OpenFd(asset);
            // El descriptor nativo se pasa a un FileStream propio (con búsqueda real): el paquete pesa ~25 MB y solo se lee el rango del grito pedido.
            var nativo = fd.ParcelFileDescriptor!.DetachFd();
            using var s = new FileStream(new Microsoft.Win32.SafeHandles.SafeFileHandle((IntPtr)nativo, ownsHandle: true), FileAccess.Read, 1, false);
            s.Seek(fd.StartOffset + desplazamiento, SeekOrigin.Begin);
            var bytes = new byte[longitud];
            s.ReadExactly(bytes);
            return bytes;
        }
        catch (Java.Lang.Exception ex) { throw new IOException(ex.Message, ex); }
    }

    public async Task<bool> ReproducirAsync(string ruta, CancellationToken cancelar)
    {
        var actividad = PuenteActividad.Actividad;
        if (actividad is null || cancelar.IsCancellationRequested || !File.Exists(ruta)) return false;
        var listo = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        MediaPlayer? propio = null;
        actividad.RunOnUiThread(() =>
        {
            if (cancelar.IsCancellationRequested) { listo.TrySetResult(false); return; }
            Limpiar(_actual);
            try
            {
                propio = new MediaPlayer();
                _actual = propio;
                propio.Prepared += (_, _) =>
                {
                    if (cancelar.IsCancellationRequested || !ReferenceEquals(_actual, propio)) { listo.TrySetResult(false); return; }
                    try { propio!.Start(); listo.TrySetResult(true); }
                    catch (Java.Lang.Exception) { listo.TrySetResult(false); Limpiar(propio); }
                };
                propio.Completion += (_, _) => Limpiar(propio);
                propio.Error += (_, e) => { e.Handled = true; listo.TrySetResult(false); Limpiar(propio); };
                propio.SetDataSource(ruta);
                propio.PrepareAsync();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException) { listo.TrySetResult(false); Limpiar(propio); }
        });
        try { return await listo.Task.WaitAsync(TimeSpan.FromSeconds(5), cancelar).ConfigureAwait(false); }
        catch (Exception ex) when (ex is TimeoutException or OperationCanceledException)
        {
            actividad.RunOnUiThread(() => Limpiar(propio));
            return false;
        }
    }

    private void Limpiar(MediaPlayer? player)
    {
        if (player is null) return;
        if (ReferenceEquals(_actual, player)) _actual = null;
        try { player.Release(); player.Dispose(); }
        catch (Exception ex) when (ex is Java.Lang.Exception or ObjectDisposedException or InvalidOperationException) { }
    }

    public void Parar() => PuenteActividad.Actividad?.RunOnUiThread(() => Limpiar(_actual));
}
