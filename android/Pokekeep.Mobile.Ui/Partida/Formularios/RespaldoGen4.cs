// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace Pokekeep.Mobile.Ui.Partida.Formularios;

/// <summary>
/// Etiquetas a mano del bloque 8a-2 (Gen. 4). Clave exacta de PKHeX (<c>Formulario.Control</c>) y su texto en castellano de Espana: SOLO lo que <c>lang_es.txt</c> no trae o deja en ingles (<c>EtiquetasLang.Control</c> da prioridad a este diccionario).
/// Cada entrada lleva un comentario con el motivo. Este fichero lo edita unicamente el bloque 8a-2.
/// </summary>
internal static class RespaldoGen4
{
    public static readonly IReadOnlyDictionary<string, string> Textos = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        // Geonet: lang_es.txt deja «Point» (la columna del punto de color) en ingles; en castellano es «Punto».
        ["SAV_Geonet4.DGV_Item_Point"] = "Punto",

        // Misc4: lang_es.txt deja «Arcade» (la Ruleta Batalla) como en ingles; el nombre oficial es el de Gen4.Met0 («Ruleta Batalla», como «Torre Batalla» o «Fábrica Batalla» de sus hermanos).
        ["SAV_Misc4.BTN_PrintArcade"] = "Ruleta",

        // Subsuelo: lang_es.txt (SAV_Underground.GB_UScores) dice «Puntajes», que es latinoamericano (es-419); en español de España son «Puntuaciones».
        ["SAV_Underground.GB_UScores"] = "Puntuaciones",

        // Pokéathlon: los controles de especie, forma y genero del participante no traen etiqueta propia en WinForms (son un desplegable y un icono); en el movil cada campo lleva la suya.
        ["SAV_Pokeathlon4.L_Form"] = "Forma",

        // Pases de Battle Revolution: «Pokémon 1..6» es igual en castellano y en inglés.
        ["SAV_BattlePass.GB_PKM1"] = "Pokémon 1", ["SAV_BattlePass.GB_PKM2"] = "Pokémon 2", ["SAV_BattlePass.GB_PKM3"] = "Pokémon 3",
        ["SAV_BattlePass.GB_PKM4"] = "Pokémon 4", ["SAV_BattlePass.GB_PKM5"] = "Pokémon 5", ["SAV_BattlePass.GB_PKM6"] = "Pokémon 6",

        // Accesorios de Battle Revolution: los seis disfraces variocolor llevan el nombre del Pokémon, que es igual en castellano y en inglés (lang_es.txt lo repite y la prueba de localizacion lo señalaria como sin traducir).
        ["SAV_Gear.CHK_Groudon"] = "Groudon", ["SAV_Gear.CHK_Lucario"] = "Lucario", ["SAV_Gear.CHK_Electivire"] = "Electivire",
        ["SAV_Gear.CHK_Kyogre"] = "Kyogre", ["SAV_Gear.CHK_Roserade"] = "Roserade", ["SAV_Gear.CHK_Pachirisu"] = "Pachirisu",

        // «General» es igual en castellano y en ingles: lang_es.txt lo deja asi y la prueba de localizacion lo señalaria como sin traducir.
        ["SAV_Pokeathlon4.Tab_General"] = "General",
        ["SAV_Pokeathlon4.L_Gender"] = "Género",
    };
}
