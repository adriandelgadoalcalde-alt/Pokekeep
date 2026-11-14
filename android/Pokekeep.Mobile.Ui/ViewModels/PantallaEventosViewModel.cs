// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.ViewModels;

/// <summary>
/// R-014 «Añadir evento»: pantalla a pantalla completa que reutiliza la Librería (<see cref="LibreriaViewModel"/> en modo eventos y <c>LibreriaView</c>) con los dos grupos de eventos del juego de la partida.
/// El añadido es el mismo de la Librería (<c>AnadirDeLibreriaAsync</c>), con la casilla vacía tocada como destino.
/// </summary>
public sealed class PantallaEventosViewModel : PantallaMasViewModel
{
    public PantallaEventosViewModel(LibreriaViewModel lista, Action volver) : base(volver) => Lista = lista;
    public LibreriaViewModel Lista { get; }
    public override string Titulo => IdiomaInterfaz.T("Eventos.Titulo");
    public override void ActualizarIdioma() { base.ActualizarIdioma(); Lista.ActualizarIdioma(); }
}
