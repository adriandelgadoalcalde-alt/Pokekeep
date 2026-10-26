// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace Pokekeep.Mobile.Ui.Ajustes.Pkhex;

public static partial class AplicadorPkhex
{
    /// <summary>Bloque 10-4: <c>WinFormsUtil.AddSaveFileExtensions</c> al recargar los ajustes (las extensiones propias quitan «(1)» y « - Copy» del nombre de las copias).</summary>
    static partial void TrasRecargarB10_4(AjustesPkhex a) => Guardados.ExtensionesPkhex.Aplicar(a);
}
