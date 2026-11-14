// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;

namespace AutoLegalidad.Desafios;

/// <summary>Las instalaciones de desafío que tiene una partida y las acciones «Todo al jefe» y «Todo al máximo» sobre todas ellas.</summary>
public static class CatalogoDesafios
{
    /// <summary>Todas las instalaciones que conoce la biblioteca, tenga o no la partida.</summary>
    public static IReadOnlyList<InstalacionDesafio> Todas { get; } =
    [
        .. Frente3Esmeralda.Todas,
        ColinaEntrenador3.Instancia,
        .. Frente4.DeDp,
        .. Frente4.DePlatino,
        .. Frente4.DeHgss,
        Pokeathlon4Maximo.Instancia,
        Metro5.Instancia,
        PuntosMetro5.Instancia,
        Pwt5.Instancia,
        Maison6.Instancia,
        Arbol7.Instancia,
        Torre8.Instancia,
        Torre8b.Instancia,
        Royale9a.Instancia,
    ];

    /// <summary>Instalaciones de la partida abierta, en el orden en que las enseña la interfaz.</summary>
    public static IReadOnlyList<InstalacionDesafio> Para(SaveFile sav) => [.. Todas.Where(i => i.Aplica(sav))];

    /// <summary>«Todo al jefe» en la partida: cada modo de cada instalación, con el primer jefe disponible (o el indicado). Lo que no tiene base verificada sale como no disponible.</summary>
    public static IReadOnlyList<ResultadoDesafio> IrAlJefeTodo(SaveFile sav, JefeDesafio? jefe = null) =>
        [.. Para(sav).SelectMany(i => i.IrAlJefeTodo(sav, jefe))];

    /// <summary>«Todo al máximo» en la partida.</summary>
    public static IReadOnlyList<ResultadoDesafio> MaximoTodo(SaveFile sav) =>
        [.. Para(sav).SelectMany(i => i.MaximoTodo(sav))];
}
