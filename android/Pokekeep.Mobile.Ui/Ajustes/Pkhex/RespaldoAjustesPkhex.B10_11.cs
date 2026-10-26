// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace Pokekeep.Mobile.Ui.Ajustes.Pkhex;

public static partial class RespaldoAjustesPkhex
{
    static partial void AnadirEtiquetasB10_11(Action<string, string> a)
    {
        // PKHeX no incluye PropertyGrid en castellano para estas propiedades del Core.
        a("NicknamedAnotherSpecies", "Apodo que coincide con otra especie");
        a("NicknamedTrade", "Apodo de un intercambio del juego");
        a("NicknamedMysteryGift", "Apodo de un regalo misterioso");
        a("AllowGen1Tradeback", "Permitir movimientos adquiridos al regresar de la segunda generación");
        a("CheckWordFilter", "Comprobar apodos y nombres de entrenador con el filtro de palabras");
        a("DisableWordFilterPastGen", "No aplicar el filtro de palabras a formatos anteriores");
    }

    static partial void AnadirDescripcionesB10_11(Action<string, string> a)
    {
        // Adaptación táctil de ContextMenuSAV: no se exige una tecla Ctrl inexistente en el móvil.
        a("SlotLegalityAlwaysVisible", "Muestra un acceso fijo a Legalidad en la ficha del Pokémon. El informe también está disponible en el menú de la casilla, abierto con una pulsación larga.");
    }
}
