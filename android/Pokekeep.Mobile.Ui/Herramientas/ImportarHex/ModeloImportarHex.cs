// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Ajustes.Pkhex;

namespace Pokekeep.Mobile.Ui.Herramientas.ImportarHex;

/// <summary>
/// «Importar hexadecimal del portapapeles» de PKHeX (<c>Troubleshooting.OpenFileFromClipboardHex</c>, PK-MENU-026 y PK-IO-021): el texto se recorta, se le quitan los espacios y se convierte con
/// <c>Convert.FromHexString</c>; vacío o no hexadecimal da los mensajes de PKHeX. Diferencia declarada: también se quitan saltos de línea y tabuladores (al pegar en el móvil el texto suele llegar partido en
/// líneas; PKHeX solo quita espacios porque en el escritorio se copia de una sola línea). Los bytes se abren después con el MISMO despachador que un archivo.
/// </summary>
public static class ModeloImportarHex
{
    /// <summary>Bytes del texto, o null con <paramref name="error"/> = el mensaje de PKHeX.</summary>
    public static byte[]? Convertir(string? texto, out string? error)
    {
        TextosNucleo.Preparar();
        var hex = (texto ?? string.Empty).Trim();
        if (hex.Length == 0) { error = MessageStrings.MsgTroubleshootingClipboardEmpty; return null; }
        hex = hex.Replace(" ", string.Empty).Replace("\r", string.Empty).Replace("\n", string.Empty).Replace("\t", string.Empty);
        try { error = null; return Convert.FromHexString(hex); }
        catch (FormatException) { error = MessageStrings.MsgTroubleshootingClipboardInvalidHex; return null; }
    }
}
