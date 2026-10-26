// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Diagnostics;
using AutoLegalidad.Registro;

namespace AutoLegalidad.Ui.Diagnostico;

/// <summary>Abre la carpeta de registros en el Explorador (opción «Abrir carpeta de registros»).</summary>
public static class CarpetaRegistros
{
    public static void Abrir(IWin32Window? owner)
    {
        var carpeta = RegistroFallos.CarpetaParaAbrir();
        if (carpeta is null)
        {
            MessageBox.Show(owner, TextosRegistro.SinCarpeta, "Pokekeep", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        try { Process.Start(new ProcessStartInfo(carpeta) { UseShellExecute = true }); }   // la carpeta se abre con el programa que Windows asocia (el Explorador)
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            RegistroFallos.Aviso("No se pudo abrir la carpeta de registros.", ex, "CarpetaRegistros.Abrir");
            MessageBox.Show(owner, "No se ha podido abrir la carpeta de registros:\n" + Saneador.Sanear(carpeta), "Pokekeep", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
