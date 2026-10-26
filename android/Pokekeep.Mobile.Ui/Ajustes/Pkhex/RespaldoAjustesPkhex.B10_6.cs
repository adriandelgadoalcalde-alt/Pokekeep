// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace Pokekeep.Mobile.Ui.Ajustes.Pkhex;

public static partial class RespaldoAjustesPkhex
{
    // Bloque 10-6 (bases de datos de encuentros y de regalos). Por RUTA, no por nombre: FilterUnavailableSpecies y ResultsGridRowCount existen también en EntityDb (bloque 10-5), que decide los suyos.
    static partial void AnadirEtiquetasB10_6(Action<string, string> a)
    {
        // lang_es: «Usar pestañas del editor como criterios». En el móvil no hay pestañas de editor abiertas junto a la herramienta: hace de editor el «Pokémon de referencia» que se elige en la pestaña Criterios.
        a("EncounterDb.UseTabsAsCriteria", "Usar el Pokémon de referencia como criterio");
        // lang_es: «Usar pestañas también sin especie» (no se entiende sin ver el formulario).
        a("EncounterDb.UseTabsAsCriteriaAnySpecies", "Usar el Pokémon de referencia también con otras especies");
        // lang_es: «Ocultar especies no disponibles», pero en la base de regalos lo que se oculta son tarjetas (la descripción inglesa dice «gifts»).
        a("MysteryDb.FilterUnavailableSpecies", "Ocultar regalos no disponibles");
    }

    static partial void AnadirDescripcionesB10_6(Action<string, string> a)
    {
        // lang_es: «…si el usuario olvidó ingresar la especie/movimiento(s)…»: «ingresar» es latinoamericanismo (en España, «introducir»).
        a("EncounterDb.ReturnNoneIfEmptySearch", "No busca si no se ha elegido ninguna especie ni ningún movimiento.");
        // lang_es: «Ocultar Especie no disponible si la partida cargada no puede importarla.» (mayúscula suelta y sujeto en singular).
        a("EncounterDb.FilterUnavailableSpecies", "Oculta los encuentros de especies que la partida cargada no puede importar.");
        // lang_es: «Usar propiedades del tablero del Editor PKM…»: «tablero» traduce mal «tabs» y en el móvil el editor es el Pokémon de referencia.
        a("EncounterDb.UseTabsAsCriteria", "Al generar un encuentro usa los criterios de la pestaña Criterios (sexo, naturaleza, IV…), que salen del Pokémon de referencia.");
        a("EncounterDb.UseTabsAsCriteriaAnySpecies", "Usa esos criterios aunque el encuentro no sea de la línea evolutiva del Pokémon de referencia.");
        // lang_es: «…para la cuadrícula de sprites…»: el móvil no pinta una cuadrícula de sprites sino tarjetas; cada fila de PKHeX son 6 resultados.
        a("EncounterDb.ResultsGridRowCount", "Filas de 6 resultados que se muestran por página (en PKHeX, las filas de la cuadrícula). Entre 5 y 20.");
        a("MysteryDb.ResultsGridRowCount", "Filas de 6 resultados que se muestran por página (en PKHeX, las filas de la cuadrícula). Entre 5 y 20.");
        // lang_es da para la base de regalos la misma frase que para las especies; la inglesa habla de regalos que la partida no puede recibir (ni siquiera indirectamente).
        a("MysteryDb.FilterUnavailableSpecies", "Oculta los regalos que la partida cargada no puede recibir, ni siquiera de forma indirecta.");
    }
}
