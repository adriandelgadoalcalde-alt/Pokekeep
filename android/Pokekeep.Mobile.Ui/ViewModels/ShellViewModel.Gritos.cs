// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Cajas;

namespace Pokekeep.Mobile.Ui.ViewModels;

public sealed partial class ShellViewModel
{
    public static void PararGritoActivo() { if (_activa?.TryGetTarget(out var shell) == true) shell.PararGrito(); }
    private CancellationTokenSource? _grito;
    public IReproductorGritos? ReproductorGritos { get; set; }
    /// <summary>Los gritos que trae la app (X11). Suenan si la carpeta de gritos del usuario no tiene el de la casilla.</summary>
    public PaqueteGritos? GritosIncluidos { get; set; }
    public void PararGrito() { _grito?.Cancel(); ReproductorGritos?.Parar(); }

    public void ReproducirGrito(PKM pk)
    {
        PararGrito();
        if (ReproductorGritos is not { } reproductor || !AjustesPkhex.Actual.Hover.HoverSlotPlayCry) return;
        var ruta = ModeloGritos.RutaFinal(pk, AjustesPkhex.Actual.LocalResources.GetCryPath(), GritosIncluidos);
        if (ruta is null) return;   // PKHeX también guarda silencio si no existe el archivo
        _grito?.Dispose();
        _grito = new CancellationTokenSource();
        _ = Sonar(reproductor, ruta, _grito.Token);
    }

    private static async Task Sonar(IReproductorGritos reproductor, string ruta, CancellationToken cancelar)
    {
        try { await reproductor.ReproducirAsync(ruta, cancelar); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { }   // un grito opcional no derriba la edición
    }
}
