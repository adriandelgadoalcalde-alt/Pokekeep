// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen4;

/// <summary>El título de un editor de Gen. 4 es SIEMPRE el de su entrada del registro (el de PKHeX en castellano): así la pantalla y la lista nunca discrepan.</summary>
internal static class TituloGen4
{
    public static string De(string entrada) => CatalogoEspecificos.Todas.First(e => e.Id == entrada).Titulo;
}
