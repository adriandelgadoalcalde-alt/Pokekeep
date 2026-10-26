// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace Pokekeep.Mobile.Ui.Ajustes.Pkhex;

public static partial class ConsumidoresPkhex
{
    static partial void DeclararB10_6(Action<string, string> d)
    {
        // Base de datos de encuentros (SAV_Encounters): se leen al buscar, al generar y al pintar cada página, siempre de Ajustes.Actual.
        d("LocalResources.TrainerPath", "Más > Herramientas > Base de datos de encuentros: EncuentrosViewModel carga los entrenadores de la carpeta vigente y ModeloEncuentros.Generar usa sus datos de EO");
        d("EncounterDb.ReturnNoneIfEmptySearch", "Más > Herramientas > Base de datos de encuentros: sin especie ni movimientos no se busca (ModeloEncuentros.MotivoNoBuscar)");
        d("EncounterDb.FilterUnavailableSpecies", "Más > Herramientas > Base de datos de encuentros: oculta los encuentros que la partida no puede importar (EntityPresenceFilters)");
        d("EncounterDb.UseTabsAsCriteria", "Más > Herramientas > Base de datos de encuentros: el Pokémon generado sigue los criterios (ModeloEncuentros.CriteriosPara)");
        d("EncounterDb.UseTabsAsCriteriaAnySpecies", "Más > Herramientas > Base de datos de encuentros: los criterios valen también fuera de la línea evolutiva del Pokémon de referencia");
        d("EncounterDb.ResultsGridRowCount", "Más > Herramientas > Base de datos de encuentros: resultados por página = 6 × filas, como la cuadrícula de PKHeX");
        // Base de datos de regalos misteriosos (SAV_MysteryGiftDB): el filtro se lee al cargar la base (al abrir, como LoadDatabase) y las filas al pintar cada página.
        d("MysteryDb.FilterUnavailableSpecies", "Más > Herramientas > Base de datos de regalos misteriosos: oculta las tarjetas que la partida no puede recibir (EntityPresenceFilters.GetFilterGift)");
        d("MysteryDb.ResultsGridRowCount", "Más > Herramientas > Base de datos de regalos misteriosos: resultados por página = 6 × filas, como la cuadrícula de PKHeX");
    }
}
