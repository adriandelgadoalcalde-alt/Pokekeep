// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;

namespace Pokekeep.Mobile.Ui.Partida;

/// <summary>
/// Objetos de RELLENO del texto del juego que el Core admite en un bolsillo pero que no existen: en Espada/Escudo el bolsillo «Objetos» (y la lista de objeto equipado) trae los ids 1279 a 1578, que el texto
/// nombra con designaciones de estrellas («★And458», «★Gem2990», «★Gru8353»...). Solo los doce primeros (1279 a 1290, <c>ItemStorage8SWSH.IsDynamaxCrystalAvailable</c>) son cristales Dinamax reales; los
/// 288 restantes los marca el propio Core como no legales (<c>IsItemLegal</c> = falso) y no tienen objeto en el juego. PKHeX de escritorio los ofrece todos en el desplegable del bolsillo (<c>SAV_Inventory</c>:
/// <c>GetStringsForPouch(pouch.GetAllItems())</c>), sin distinguirlos; el móvil los saca de las listas normales y los deja solo en el modo avanzado (HaX de PKHeX, que da la lista completa).
/// Evidencia (QA uso 6 / X44): sondeo de todas las versiones; solo SW y SH los admiten en un bolsillo o en el objeto equipado sin HaX. En el resto de juegos solo salen en la lista completa del modo avanzado.
/// </summary>
public static class ObjetosRelleno
{
    /// <summary>¿Es un objeto de relleno de <paramref name="sav"/>? Un objeto que ya está en la partida sigue enseñándose con su nombre; esto solo decide si se OFRECE al elegir.</summary>
    public static bool EsRelleno(SaveFile sav, int id)
        => sav is SAV8SWSH && id is >= 0 and <= ushort.MaxValue && ItemStorage8SWSH.IsDynamaxCrystal((ushort)id) && !ItemStorage8SWSH.IsDynamaxCrystalAvailable((ushort)id);
}
