// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Avalonia.Input.Platform;

namespace Pokekeep.Mobile.Ui.Design;

/// <summary>El portapapeles del sistema (Android: ClipboardManager) por el <see cref="IClipboard"/> de Avalonia. Un fallo del sistema se devuelve como «no copiado», nunca como excepcion.</summary>
public sealed class PortapapelesAvalonia(IClipboard portapapeles) : IPortapapeles
{
    public async Task<bool> CopiarAsync(string texto)
    {
        try { await portapapeles.SetTextAsync(texto); return true; }
        catch (Exception) { return false; }
    }

    /// <summary>La imagen PNG al portapapeles de Avalonia (<c>SetBitmapAsync</c>). Android solo admite texto y URI en su portapapeles: si Avalonia no puede, se devuelve «no copiado» y la interfaz ofrece Compartir.</summary>
    public async Task<bool> CopiarImagenAsync(byte[] png)
    {
        try
        {
            using var flujo = new MemoryStream(png);
            var bmp = new Avalonia.Media.Imaging.Bitmap(flujo);   // sin Dispose: el portapapeles puede conservar la referencia (en proceso) y la libera el recolector
            await portapapeles.SetBitmapAsync(bmp);
            return true;
        }
        catch (Exception) { return false; }
    }

    public async Task<string?> LeerAsync()
    {
        try { return await portapapeles.TryGetTextAsync(); }
        catch (Exception) { return null; }
    }
}
