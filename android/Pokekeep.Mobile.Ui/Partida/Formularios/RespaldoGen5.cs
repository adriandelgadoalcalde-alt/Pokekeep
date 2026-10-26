// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace Pokekeep.Mobile.Ui.Partida.Formularios;

/// <summary>
/// Etiquetas a mano del bloque 8a-3 (Gen. 5). Clave exacta de PKHeX (<c>Formulario.Control</c>) y su texto en castellano de Espana: SOLO lo que <c>lang_es.txt</c> no trae o deja en ingles (<c>EtiquetasLang.Control</c> da prioridad a este diccionario).
/// Cada entrada lleva un comentario con el motivo. Este fichero lo edita unicamente el bloque 8a-3.
/// </summary>
internal static class RespaldoGen5
{
    public static readonly IReadOnlyDictionary<string, string> Textos = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        // ---- SAV_UnityTower (PK-SAV-103) ----
        // El formulario original no tiene pestañas (dos rejillas en una ventana); en el móvil son una sola pestaña con una tabla y esta etiqueta no existe en PKHeX.
        ["SAV_UnityTower.Tab_Locations"] = "Ubicaciones",
        // ---- SAV_GlobalLink5 (PK-SAV-088 a 091) ----
        // Los cinco muebles de la pestaña "Furniture" son controles sin etiqueta en el original (NUD_Furniture1..5 y TB_Furniture1..5); en el móvil cada campo se autoexplica.
        // «General» y «Musical» se escriben igual en castellano: lang_es.txt los deja idénticos al inglés y la prueba de traducción los marca; son correctos (cognados), no una traducción que falte.
        ["SAV_GlobalLink5.Tab_General"] = "General",
        ["SAV_GlobalLink5.L_Musical"] = "Musical",
        ["SAV_GlobalLink5.L_Furniture"] = "Mueble",
        ["SAV_GlobalLink5.L_FurnitureNumber"] = "Número",
        ["SAV_GlobalLink5.L_FurnitureName"] = "Nombre",
        // ---- SAV_Medals5 (PK-SAV-092 a 094) ----
        // lang_es.txt no trae los dos valores sin identificar de la lista de hábitats.
        // «Surf» se dice igual en castellano (es el nombre de la técnica y del tipo de encuentro en las ediciones en español): lang_es.txt lo deja idéntico al inglés y la prueba lo marca; no falta traducción.
        ["SAV_Medals5.DGV_HabitatSurfColumn"] = "Surf",
        ["SAV_Medals5.L_Unknown90"] = "Valor desconocido 0x90",
        ["SAV_Medals5.L_Unknown92"] = "Valor desconocido 0x92",
        // Etiquetas de la versión del móvil que PKHeX no tiene como control: el original son dos pestañas y aquí cuatro (una pestaña es o una colección o unas secciones), y el rango se calcula pulsando su etiqueta (L_Rank_Click).
        ["SAV_Medals5.Tab_Settings"] = "Ajustes",
        ["SAV_Medals5.Tab_HabitatSettings"] = "Ajustes hábitat",
        ["SAV_Medals5.B_CalculateRank"] = "Calcular rango",
        ["SAV_Medals5.B_HabitatSetCompleteAll"] = "Marcar todos como completos",
        ["SAV_Medals5.B_HabitatClearAll"] = "Limpiar todos",
        // ---- SAV_DLC5 (PK-SAV-061 y 079 a 087) ----
        // Etiquetas de la versión del móvil que PKHeX no tiene como control: las dos ranuras del Nexo Recuerdo se llaman por sus botones («Importar Ranura 1») y el aviso de «no funciona» es un rótulo creado por código en el original.
        ["SAV_DLC5.L_MemorySlot1"] = "Ranura 1",
        ["SAV_DLC5.L_MemorySlot2"] = "Ranura 2",
        ["SAV_DLC5.L_BattleTestWarning"] = "Aviso",
        // «PWT» (Pokémon World Tournament) y «Musical» son siglas / cognado: lang_es.txt los deja idénticos al inglés y la prueba los marca; no falta traducción.
        ["SAV_DLC5.Tab_Musical"] = "Musical",
        ["SAV_DLC5.Tab_PWT"] = "PWT",
        // ---- SAV_Misc5 (PK-SAV-095 a 101) ----
        // lang_es.txt deja en inglés las marcas del Metro (Flag0...) y «Battle No.»: en España «Marca 0...» y «N.º de combate».
        ["SAV_Misc5.CHK_Subway0"] = "Marca 0",
        ["SAV_Misc5.CHK_Subway1"] = "Marca 1",
        ["SAV_Misc5.CHK_Subway2"] = "Marca 2",
        ["SAV_Misc5.CHK_Subway3"] = "Marca 3",
        ["SAV_Misc5.CHK_Subway7"] = "Marca 7",
        ["SAV_Misc5.L_CurrentBattle"] = "N.º de combate",
        // Etiquetas de la versión del móvil que PKHeX no tiene como control (el original son seis pestañas y aquí diez; el «Doblr» de CHK_DoubleSet y el «?» sin «¿» de lang_es.txt obligan a escribir los nombres de modo con sus propios rótulos).
        ["SAV_Misc5.TAB_FlyKeys"] = "Vuelo y llaves",
        ["SAV_Misc5.TAB_Records"] = "Récords",
        ["SAV_Misc5.TAB_ForestSlots"] = "Ranuras del Bosque",
        ["SAV_Misc5.L_RunActive"] = "Partida en curso",
        ["SAV_Misc5.L_Current"] = "Actual",
        // «Multi» y «Super Multi» se escriben igual en castellano (lang_es.txt los deja idénticos al inglés): cognados, no falta traducción.
        ["SAV_Misc5.GB_Multi"] = "Multi",
        // «Super Indiv.?», «Super Dobles?» y «Super Multi?» (lang_es.txt) llevan «?» sin «¿» y «Super» sin tilde: error tipografico de PKHeX. Respaldo con el rotulo del hecho que marcan (es-ES: «Súper»).
        ["SAV_Misc5.GB_SuperSingles"] = "Súper individual",
        ["SAV_Misc5.GB_SuperDoubles"] = "Súper dobles",
        ["SAV_Misc5.GB_SuperMulti"] = "Súper multi",
        ["SAV_Misc5.CHK_SuperSingle"] = "Súper individual desbloqueado",
        ["SAV_Misc5.CHK_SuperDouble"] = "Súper dobles desbloqueado",
        ["SAV_Misc5.CHK_SuperMulti"] = "Súper multi desbloqueado",
        // L_FC de lang_es.txt acaba en «!» y no abre con «¡» (error tipografico de PKHeX): se reescribe sin exclamacion.
        ["SAV_Misc5.L_FC"] = "Exporta los datos de una partida de Blanco e impórtalos en una de Negro, y viceversa: así tendrás la Ciudad Negra y el Bosque Blanco en una sola partida.",
        // Nombres propios (Tornadus, Thundurus) y el cognado «Musical»: idénticos al inglés en lang_es.txt, no falta traducción.
        ["SAV_Misc5.L_Roamer641"] = "Tornadus",
        ["SAV_Misc5.L_Roamer642"] = "Thundurus",
        ["SAV_Misc5.TAB_Muscial"] = "Musical",
        // ---- SAV_JoinAvenue (PK-SAV-062 a 078) ----
        // Los controles «sin identificar» del original se llaman por su desplazamiento (0x22...) y lang_es.txt no los trae; el género no tiene etiqueta (es un conmutador de imagen) y el aviso de «tienda deseada» se compone con las palabras del fichero.
        ["SAV_JoinAvenue.L_Gender"] = "Género",
        ["SAV_JoinAvenue.L_Unknown22"] = "Desconocido 0x22",
        ["SAV_JoinAvenue.L_Unused23"] = "Sin uso 0x23",
        ["SAV_JoinAvenue.L_Unknown26"] = "Desconocido 0x26",
        ["SAV_JoinAvenue.L_Unknown27"] = "Desconocido 0x27",
        ["SAV_JoinAvenue.L_IsFlag2C"] = "Marca 0x2C",
        ["SAV_JoinAvenue.L_Unused2D"] = "Sin uso 0x2D",
        ["SAV_JoinAvenue.L_UnknownA8"] = "Desconocido 0xA8",
        ["SAV_JoinAvenue.L_IsFlagA9_1"] = "Marca 0xA9 bit 1",
        ["SAV_JoinAvenue.L_IsFlagA9_2"] = "Marca 0xA9 bit 2",
        ["SAV_JoinAvenue.L_IsFlagAA"] = "Marca 0xAA",
        ["SAV_JoinAvenue.L_UnknownAC"] = "Desconocido 0xAC",
        ["SAV_JoinAvenue.L_UnusedB8"] = "Sin uso 0xB8",
        ["SAV_JoinAvenue.L_UnknownBits0_8"] = "Desconocido bits 0 a 8",
        ["SAV_JoinAvenue.L_UnknownBit9"] = "Desconocido bit 9",
        ["SAV_JoinAvenue.L_UnknownBits10"] = "Desconocido bits 10 a 12",
        ["SAV_JoinAvenue.L_UnknownBits13_20"] = "Desconocido bits 13 a 20",
        ["SAV_JoinAvenue.L_UnknownBits21_27"] = "Desconocido bits 21 a 27",
        ["SAV_JoinAvenue.L_UnknownBits28_31"] = "Desconocido bits 28 a 31",
        ["SAV_JoinAvenue.L_Unknown4C"] = "Desconocido 0x4C",
        ["SAV_JoinAvenue.L_Unknown4D"] = "Desconocido 0x4D",
        ["SAV_JoinAvenue.L_Unknown4E"] = "Desconocido 0x4E",
        ["SAV_JoinAvenue.L_Unknown52"] = "Desconocido 0x52",
        ["SAV_JoinAvenue.L_Unknown54"] = "Desconocido 0x54",
        ["SAV_JoinAvenue.L_Unknown56"] = "Desconocido 0x56",
        ["SAV_JoinAvenue.L_Unknown5A"] = "Desconocido 0x5A",
        ["SAV_JoinAvenue.L_UnusedPosition"] = "Posición sin uso",
        // «General» y «Fans» se escriben igual en castellano («los fans», plural admitido por la RAE): idénticos al inglés en lang_es.txt, no falta traducción.
        ["SAV_JoinAvenue.Tab_General"] = "General",
        ["SAV_JoinAvenue.Tab_SelfGeneral"] = "General",
        ["SAV_JoinAvenue.Tab_Fans"] = "Fans",
        ["SAV_DLC5.L_CGearPng"] = "Skin como imagen (PNG)",
        ["SAV_DLC5.L_CGearFile"] = "Skin como archivo (.psk o .cgb)",
        ["SAV_Misc5.L_SlotSpecies"] = "Especie",
    };
}
