// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace Pokekeep.Mobile.Ui.Partida.Formularios;

/// <summary>
/// Etiquetas a mano del bloque 8b-2 (Gen. 8-9). Clave exacta de PKHeX (<c>Formulario.Control</c>) y su texto en castellano de Espana: SOLO lo que <c>lang_es.txt</c> no trae o deja en ingles (<c>EtiquetasLang.Control</c> da prioridad a este diccionario).
/// Cada entrada lleva un comentario con el motivo. Este fichero lo edita unicamente el bloque 8b-2.
/// </summary>
internal static class RespaldoGen8y9
{
    public static readonly IReadOnlyDictionary<string, string> Textos = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        // Pokochos de BDSP: las cabeceras de la cuadricula (SAV_Poffin8b.Setup) estan escritas en ingles en el codigo del formulario y no son controles traducibles; aqui van con el nombre oficial de cada sabor en castellano.
        ["SAV_Poffin8b.Col_Type"] = "Tipo",
        ["SAV_Poffin8b.Col_Level"] = "Nivel",
        ["SAV_Poffin8b.Col_Smooth"] = "Suavidad",
        ["SAV_Poffin8b.Col_New"] = "Nuevo",
        ["SAV_Poffin8b.Col_Spicy"] = "Picante",
        ["SAV_Poffin8b.Col_Dry"] = "Seco",
        ["SAV_Poffin8b.Col_Sweet"] = "Dulce",
        ["SAV_Poffin8b.Col_Bitter"] = "Amargo",
        ["SAV_Poffin8b.Col_Sour"] = "Ácido",
        // Sellos de BDSP: igual, las cabeceras de la cuadricula estan en ingles en el codigo del formulario.
        ["SAV_SealStickers8b.Col_Count"] = "Cantidad",
        ["SAV_SealStickers8b.Col_Total"] = "Total",
        ["SAV_SealStickers8b.Col_Obtained"] = "Obtenido",
        // Subsuelo de BDSP: cabeceras de la cuadricula en ingles en el codigo del formulario, y los tipos de objeto son el nombre del enum (UgItemType) en ingles.
        ["SAV_Underground8b.Col_Count"] = "Cantidad",
        ["SAV_Underground8b.Col_New"] = "Nuevo",
        ["SAV_Underground8b.Col_Favorite"] = "Favorito",
        ["SAV_Underground8b.Tipo_Esfera"] = "Esferas",
        ["SAV_Underground8b.Tipo_Objeto"] = "Objetos",
        ["SAV_Underground8b.Tipo_Estatua"] = "Estatuas",
        ["SAV_Underground8b.Tipo_Pedestal"] = "Pedestales",
        // Eventos de BDSP: lang_es.txt deja «Work Values» en ingles; en el juego son las constantes de los scripts (como «Constante:» de L_CustomWork).
        ["SAV_FlagWork8b.GB_Work"] = "Constantes",
        // Incursiones: el cuadro de propiedades de PKHeX (PropertyGrid) enseña los nombres de las propiedades del Core en ingles; no son controles traducibles. Una clave Prop_<Propiedad> por propiedad.
        ["SAV_Raid8.Prop_Hash"] = "Hash",
        ["SAV_Raid8.Prop_Seed"] = "Semilla",
        ["SAV_Raid8.Prop_Stars"] = "Estrellas",
        ["SAV_Raid8.Prop_RandRoll"] = "Tirada aleatoria",
        ["SAV_Raid8.Prop_DenType"] = "Tipo de guarida",
        ["SAV_Raid8.Prop_Flags"] = "Marcas",
        ["SAV_Raid8.Prop_IsActive"] = "Activa",
        ["SAV_Raid8.Prop_IsRare"] = "Rara",
        ["SAV_Raid8.Prop_IsWishingPiece"] = "Con Trozo Deseo",
        ["SAV_Raid8.Prop_WattsHarvested"] = "Vatios recogidos",
        ["SAV_Raid8.Prop_IsEvent"] = "De evento",
        ["SAV_Raid9.Prop_IsEnabled"] = "Activo",
        ["SAV_Raid9.Prop_AreaID"] = "Zona",
        ["SAV_Raid9.Prop_LotteryGroup"] = "Grupo de sorteo",
        ["SAV_Raid9.Prop_SpawnPointID"] = "Punto de aparición",
        ["SAV_Raid9.Prop_ScenePointName"] = "Nombre del punto",
        ["SAV_Raid9.Prop_Seed"] = "Semilla",
        ["SAV_Raid9.Prop_Unused"] = "Sin usar",
        ["SAV_Raid9.Prop_Content"] = "Contenido",
        ["SAV_Raid9.Prop_IsClaimedLeaguePoints"] = "Puntos de Liga reclamados",
        ["SAV_RaidSevenStar9.Prop_Identifier"] = "Identificador",
        ["SAV_RaidSevenStar9.Prop_Captured"] = "Capturado",
        ["SAV_RaidSevenStar9.Prop_Defeated"] = "Derrotado",
        // Moda: las cabeceras de las cuadriculas son los nombres de las propiedades (Value, IsNew...) en el codigo del formulario.
        ["SAV_Fashion9.Col_Value"] = "Código del objeto",
        ["SAV_Fashion9.Col_IsNew"] = "Nuevo",
        ["SAV_Fashion9.Col_IsNewShop"] = "Nuevo en la tienda",
        ["SAV_Fashion9.Col_IsNewGroup"] = "Nuevo en el grupo",
        ["SAV_Fashion9.Col_IsEquipped"] = "Equipado",
        ["SAV_Fashion9.Col_IsOwned"] = "Poseído",
        // Marcas y variables de Z-A: lang_es.txt no trae nada de SAV_FlagWork9a (el formulario entero queda en ingles); las cabeceras de las cuadriculas y los botones, en castellano.
        ["SAV_FlagWork9a.Col_Key"] = "Clave",
        ["SAV_FlagWork9a.Col_KeyA"] = "Clave A",
        ["SAV_FlagWork9a.Col_KeyB"] = "Clave B",
        ["SAV_FlagWork9a.Col_KeyC"] = "Clave C",
        ["SAV_FlagWork9a.Col_Value"] = "Valor",
        ["SAV_FlagWork9a.Col_Value1"] = "Valor 1",
        ["SAV_FlagWork9a.Col_Value2"] = "Valor 2",
        ["SAV_FlagWork9a.GB_Research"] = "Investigar",
        ["SAV_FlagWork9a.B_LoadOld"] = "Cargar anterior",
        ["SAV_FlagWork9a.B_LoadNew"] = "Cargar nuevo",
    };
}
