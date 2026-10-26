// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace Pokekeep.Mobile.Ui.Ajustes.Pkhex;

public static partial class RespaldoAjustesPkhex
{
    static partial void AnadirEtiquetasB10_3(Action<string, string> a)
    {
        // «Language» y «Version» de SaveLanguage.OverrideGen*: lang_es.txt solo trae las etiquetas genéricas «Idioma» y «Versión», que en cuatro grupos iguales no dicen de qué generación son.
        foreach (var (g, nombre) in Generaciones)
        {
            a($"SaveLanguage.{g}.Language", $"Idioma por defecto ({nombre})");
            a($"SaveLanguage.{g}.Version", $"Versión por defecto ({nombre})");
        }
        // lang_es.txt dice «Balls» (anglicismo): en castellano de España, «Poké Balls», como en el resto del editor.
        a("ShowLegalBallsFirst", "Mostrar primero las Poké Balls legales");
        // lang_es.txt dice «género»: el editor y los juegos en castellano dicen «sexo».
        a("ShowGenderGen1", "Mostrar el sexo en Gen. 1");
        // lang_es.txt no trae la etiqueta del ajuste nuevo de PKHeX 26.10.18.
        a("IsPreferredTransferVirtualHOME", "Pasar de Gen. 3 a HOME directamente");
    }

    static partial void AnadirDescripcionesB10_3(Action<string, string> a)
    {
        // lang_es.txt describe «Language» como el idioma de las plantillas de combate y «Version» como la última versión del programa: no valen para estos ajustes.
        foreach (var (g, nombre) in Generaciones)
        {
            a($"SaveLanguage.{g}.Language", $"Idioma que se usa al abrir un guardado de {nombre} si no se puede deducir el suyo (las partidas japonesas se reconocen solas).");
            a($"SaveLanguage.{g}.Version", $"Versión que se usa al abrir un guardado de {nombre} si no se puede deducir la suya (por ejemplo, por el nombre del archivo). «Sin elegir» deja la que esté en uso: la de fábrica al abrir la app.");
        }
        // lang_es.txt: «Notificar cambios no hechos» no dice lo que hace el ajuste.
        a("ModifyUnset", "Al abrir otra partida, avisar si en la actual hay cambios sin guardar.");
        // lang_es.txt: «Variación de características» no es como se llama en castellano la naturaleza que modifica las estadísticas (Gen. 8 en adelante).
        a("ApplyStatAlignment", "Al importar un set, la naturaleza pasa a ser también la naturaleza que modifica las estadísticas (Gen. 8 en adelante).");
        // lang_es.txt: errata «maximixar».
        a("HiddenPowerOnChangeMaxPower", "Al cambiar el tipo de Poder Oculto, poner los IV al máximo para que tenga la mayor potencia. Si no, los IV quedan lo más cerca posible de los actuales.");
        // lang_es.txt: «Balls» y abreviaturas «1a/2a/4a/5a/7a» en lugar de los ordinales «1.ª»...
        a("ShowLegalBallsFirst", "En la lista de Poké Balls, poner primero las legales para este Pokémon en lugar de ordenarlas por número.");
        a("ShowGenderGen1", "En un Pokémon con formato de la 1.ª generación, mostrar el sexo que tendría al transferirlo a otras generaciones.");
        a("VirtualConsoleSourceGen1", "Versión que se pone al transferir un Pokémon de la 1.ª generación de la Consola Virtual de 3DS a la 7.ª generación.");
        a("VirtualConsoleSourceGen2", "Versión que se pone al transferir un Pokémon de la 2.ª generación de la Consola Virtual de 3DS a la 7.ª generación.");
        a("IsPreferredTransferVirtualHOME", "Al llevar un Pokémon de la 3.ª generación a una partida de la 8.ª, saltar directamente a HOME en vez de pasar por las generaciones 4 a 7. Es la ruta de Rojo Fuego y Verde Hoja de la Consola Virtual de Switch: el Pokémon queda con ese origen.");
    }

    private static readonly (string Grupo, string Nombre)[] Generaciones =
        [("OverrideGen1", "Gen. 1"), ("OverrideGen2", "Gen. 2"), ("OverrideGen3RS", "Rubí y Zafiro"), ("OverrideGen3FRLG", "Rojo Fuego y Verde Hoja")];
}
