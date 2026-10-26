// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace Pokekeep.Mobile.Ui.Ajustes.Pkhex;

public static partial class ConsumidoresPkhex
{
    static partial void DeclararB10_5(Action<string, string> d)
    {
        // Base de datos de PKM (SAV_Database): los de carga se leen al cargar la base (al abrir y al «Volver a leer»), siempre de Ajustes.Actual; la cuadrícula, al pintar cada página.
        d("LocalResources.DatabasePath", "Más > Herramientas > Base de datos de PKM: BaseDatosPkmViewModel.Cargar lee la carpeta de Pokémon vigente (ModeloBaseDatosPkm.Carpeta)");
        d("EntityDb.SearchBackups", "Más > Herramientas > Base de datos de PKM: al cargar, lee también la carpeta de copias de seguridad de la app (FuentesExternas.CargarAsync)");
        d("EntityDb.SearchExtraSaves", "Más > Herramientas > Base de datos de PKM: al cargar, lee también las «Otras rutas de copias» (carpetas de SAF o rutas) sin sus copias .bak (FuentesExternas.CargarAsync)");
        d("EntityDb.SearchExtraSavesDeep", "Más > Herramientas > Base de datos de PKM: las carpetas extra y la de copias se leen con sus subcarpetas (FuentesExternas, IAlmacenCarpetas.ListarAsync)");
        d("EntityDb.InitialSortMode", "Más > Herramientas > Base de datos de PKM: orden de la lista cargada (ModeloBaseDatosPkm.Cargar)");
        d("EntityDb.FilterUnavailableSpecies", "Más > Herramientas > Base de datos de PKM: oculta los Pokémon que la partida no puede importar (EntityPresenceFilters.GetFilterEntity)");
        d("EntityDb.ResultsGridRowCount", "Más > Herramientas > Base de datos de PKM: resultados por página = 6 × filas, como la cuadrícula de PKHeX");
    }
}
