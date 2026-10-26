// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;

namespace AutoLegalidad.Ui;

/// <summary>
/// Tipo de toque con el que Pokekeep escribe en una casilla a traves de <c>SlotEditor.Set</c>. Hasta PKHeX 26.08.26 <c>Set</c> escribia SIEMPRE con la importacion desactivada
/// (<c>EntityImportSettings.None</c>); desde 26.10.18 solo lo hace si el tipo NO es <see cref="SlotTouchType.Set"/> (<c>SlotEditor.cs:52</c>) y, con el tipo por defecto, adapta el Pokemon a la partida
/// (entrenador actual, <c>SetUpdatePKM</c>) y marca la Pokedex y los records (<c>SetUpdateDex</c>, <c>SetUpdateRecords</c>). Pokekeep necesita lo anterior: el candidato del motor ya lo valido el Core y se escribe tal cual,
/// la Pokedex se registra aparte y solo si el ajuste lo pide (<c>PokedexRegistration</c>) y «Deshacer» devuelve la partida byte a byte como estaba. <see cref="SlotTouchType.Swap"/> es el tipo que PKHeX ya
/// usa para escribir sin importar y su color de resaltado es el mismo que el de <c>Set</c>; <see cref="SlotTouchType.External"/> haria lanzar a <c>SlotUtil.GetTouchTypeBackground</c> en WinForms.
/// </summary>
public static class SlotWrites
{
    public const SlotTouchType Exact = SlotTouchType.Swap;
}
