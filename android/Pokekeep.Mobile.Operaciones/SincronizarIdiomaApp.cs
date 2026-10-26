// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Runtime.CompilerServices;
using AutoLegalidad.Ui;
using Pokekeep.Mobile.Ui.Ajustes.Pkhex;

namespace Pokekeep.Mobile.Operaciones;

/// <summary>
/// X36e: los textos del núcleo compartido (<see cref="AppIdioma"/>: mensajes de las operaciones, textos de la Librería, «Acerca de»...) siguen el idioma de los datos de la interfaz
/// (<see cref="ProgramaPkhex.Idioma"/>). Se enchufa al cargarse este ensamblado, antes de que nadie pida un texto.
/// </summary>
internal static class SincronizarIdiomaApp
{
    [ModuleInitializer]
    internal static void Iniciar() => AppIdioma.Proveedor = () => ProgramaPkhex.Idioma;
}
