// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace Pokekeep.Mobile.Ui.Ajustes.Pkhex;

public static partial class AplicadorPkhex
{
    /// <summary>Bloque 10-1: el modo HaX del arranque vuelve a mandar sobre la conversión que acaba de poner <c>ReloadSettings</c> (<c>Main.ReloadProgramSettings</c>).</summary>
    static partial void TrasRecargarB10_1(AjustesPkhex a) => ProgramaPkhex.ReimponerHaX();
}
