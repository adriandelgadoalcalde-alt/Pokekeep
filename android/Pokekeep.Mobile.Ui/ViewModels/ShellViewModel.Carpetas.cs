// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Pokekeep.Mobile.Ui.Guardados;
using Pokekeep.Mobile.Ui.Modelo;

namespace Pokekeep.Mobile.Ui.ViewModels;

/// <summary>
/// Bloque 10-4 (ADR 0076): cómo abre la Shell lo que sale de «Lista de carpetas», de «Forzar carga de SAV» y de la carga automática al iniciar. Un documento del usuario va SIEMPRE por el despachador del bloque 10-2
/// (<see cref="AbrirDocumentoAsync"/>, el <c>OpenSAV</c> de PKHeX); lo que no tiene documento (una copia de la app, una carga forzada) se abre con el mismo cerrojo de «ocupado» que cualquier apertura.
/// Aparte de <c>ShellViewModel.cs</c> para que los bloques no se pisen.
/// </summary>
public sealed partial class ShellViewModel
{
    /// <summary>Abre una partida sin documento: <paramref name="abrir"/> se ejecuta dentro del cerrojo y, si da una sesión, sustituye a la Shell. Devuelve si se abrió (false si la Shell estaba ocupada o no hubo sesión).</summary>
    public async Task<bool> AbrirSinDocumentoAsync(Func<Task<Sesion?>> abrir)
    {
        var abierta = false;
        await Proteger(async () =>
        {
            if (await abrir() is not { } s) return;
            abierta = true;
            Sustituir(s);
        });
        return abierta;
    }

    /// <summary>
    /// Un guardado de «Lista de carpetas» o de la carga al iniciar (<see cref="ModeloCarpetas.AlArrancarAsync"/>): con documento y despachador, por el despachador; si no, como sesión sin documento
    /// (<see cref="ModeloCarpetas.AbrirAsync"/>, que con documento y sin despachador usa la vía de siempre de Recientes).
    /// </summary>
    public Task AbrirGuardadoListadoAsync(GuardadoListado g)
    {
        if (g.Documento is { } d && _servicios?.Abridor is not null) return AbrirDocumentoAsync(d, Pokekeep.Mobile.Ui.Abrir.OrigenArchivo.Selector);
        return AbrirSinDocumentoAsync(() => ModeloCarpetas.AbrirAsync(g, _servicios));
    }
}
