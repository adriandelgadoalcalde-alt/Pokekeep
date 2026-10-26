// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace Pokekeep.Mobile.Ui.Ajustes.Pkhex;

public static partial class RespaldoAjustesPkhex
{
    // Bloque 10-5 (base de datos de PKM, EntityDb). Las etiquetas de lang_es.txt valen («Buscar en copias de seguridad», «Buscar partidas adicionales», «Orden inicial»…); se corrigen solo descripciones.
    // Por RUTA: los nombres FilterUnavailableSpecies y ResultsGridRowCount se repiten en EncounterDb y MysteryDb (bloque 10-6), que decide los suyos.
    static partial void AnadirEtiquetasB10_5(Action<string, string> a)
    {
    }

    static partial void AnadirDescripcionesB10_5(Action<string, string> a)
    {
        // lang_es: «…buscar entre los archivos de guardado de respaldo»: las copias son las de la carpeta de copias de la app, que es lo que se lee.
        a("EntityDb.SearchBackups", "Al cargar la base de datos de PKM, busca también entre las copias de seguridad de partidas que guarda la app.");
        // lang_es: «…buscar entre OtherBackupPaths»: nombra la propiedad del código; en la app se llaman «Otras rutas de copias» (Más > Ajustes > Copias de seguridad).
        a("EntityDb.SearchExtraSaves", "Al cargar la base de datos de PKM, busca también en las carpetas de «Otras rutas de copias».");
        // lang_es: «…buscar subcarpetas entre OtherBackupPaths»: igual que la anterior.
        a("EntityDb.SearchExtraSavesDeep", "Al cargar la base de datos de PKM, busca también dentro de las subcarpetas de las otras rutas de copias y de la carpeta de copias de seguridad.");
        // lang_es: «…esta lista será ordenada por esta opción-» (guion suelto y «será ordenada»).
        a("EntityDb.InitialSortMode", "Cómo se ordena la lista al cargar la base de datos de PKM: sin ordenar, por especie y forma o por la posición de la casilla.");
        // lang_es: «Ocultar Especie no disponible…» (mayúscula suelta y sujeto en singular). Aquí se ocultan Pokémon, no especies sueltas.
        a("EntityDb.FilterUnavailableSpecies", "Oculta los Pokémon de especies que la partida cargada no puede importar.");
        // lang_es: «…para la cuadrícula de sprites…»: el móvil no pinta una cuadrícula de sprites sino tarjetas; cada fila de PKHeX son 6 resultados.
        a("EntityDb.ResultsGridRowCount", "Filas de 6 resultados que se muestran por página (en PKHeX, las filas de la cuadrícula). Entre 5 y 20.");
    }
}
