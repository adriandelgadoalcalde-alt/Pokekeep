// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;

namespace Pokekeep.Mobile.Ui;

/// <summary>
/// El objeto equipado de un Pokemon tal como lo trata PKHeX: <c>HeldItem</c> es el id NATIVO del formato (en Gen. I-III y en Gen. IV los ids no son los de Gen. V+) y, para ENSEÑARLO,
/// hay que usar la lista de nombres del contexto (<c>GameStrings.GetItemStrings</c>) y, para el ICONO, <c>SpriteItem</c> (que traduce a los ids modernos con <c>ItemConverter</c>).
/// Leer la lista moderna con el id nativo (o dibujar el icono con el id nativo) enseña OTRO objeto en Gen. II-III.
/// </summary>
public static class ObjetosPkm
{
    /// <summary>Id del recurso <c>bitem_N</c> del objeto equipado (<c>pk.SpriteItem</c>, como <c>SpriteUtil</c>).</summary>
    public static int IdIcono(PKM pk) => pk.SpriteItem;

    /// <summary>Nombre del objeto equipado en la lista del contexto del formato (la misma que usa el selector del editor). Cadena vacia si no lleva o no hay nombre.</summary>
    public static string Nombre(GameStrings textos, PKM pk, GameVersion version)
    {
        var id = pk.HeldItem;
        if (id <= 0) return string.Empty;
        var lista = textos.GetItemStrings(pk.Context, version);
        return (uint)id < (uint)lista.Length ? lista[id] : string.Empty;
    }
}
