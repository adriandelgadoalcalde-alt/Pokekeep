// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;

namespace Pokekeep.Mobile.Ui.Almacen;

/// <summary>
/// Abre un guardado desde sus bytes como lo hace PKHeX de escritorio al abrir un ARCHIVO: <c>SaveUtil.GetSaveFile(bytes, nombre)</c> usa el nombre solo para algunos formatos y NO lo registra en
/// <c>Metadata</c> (eso lo hace unicamente <c>TryGetSaveFile(ruta)</c>). Pero el Core distingue por el NOMBRE del archivo los guardados de consola virtual de los de cartucho:
/// Rojo Fuego/Verde Hoja de Nintendo Switch Online (<c>FireRed_*.sav</c>, <c>LeafGreen_*.sav</c>, mismo tamano y estructura que el de GBA) y los de Gen. I-II de 3DS (<c>sav*.dat</c>), con otra era de legalidad
/// (<c>ParseSettings.InitFromSaveFileData</c> -> <c>IsVirtualConsole</c>). Sin registrar el nombre, un Rojo Fuego de Switch se analizaba como uno de GBA (y los Pokemon de Esmeralda salian legales).
/// </summary>
public static class AperturaGuardado
{
    /// <summary>
    /// Como <c>SaveUtil.GetSaveFile(datos, nombre)</c>, dejando el nombre del archivo en <c>Metadata</c> (solo el nombre, nunca una ruta). Null si el Core no lo reconoce.
    /// Después, como <c>Main.SanityCheckSAV</c> de PKHeX, en las generaciones 1 a 3 (que no guardan su idioma ni, a veces, su versión) <c>SaveLanguage.TryRevise</c> deduce idioma y versión del nombre del archivo o,
    /// si no puede, usa los de los ajustes «Guardados antiguos» (<c>SaveLanguage.OverrideGen*</c>, que <c>StartupUtil.ReloadSettings</c> ya aplicó al Core; bloque 10-3). Solo cambia lo que el Core tiene en
    /// memoria (idioma y versión de la partida), nunca los bytes.
    /// </summary>
    public static SaveFile? Abrir(byte[] datos, string nombre)
    {
        var sav = SaveUtil.GetSaveFile(datos, nombre);
        if (sav is { State.Exportable: true } && !string.IsNullOrWhiteSpace(nombre)) sav.Metadata.SetExtraInfo(Path.GetFileName(nombre));
        if (sav is { Generation: <= 3 }) SaveLanguage.TryRevise(sav);
        return sav;
    }
}
