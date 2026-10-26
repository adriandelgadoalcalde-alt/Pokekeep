// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace Pokekeep.Mobile.Ui.Partida.Formularios;

/// <summary>
/// Etiquetas a mano del bloque 8b-1 (Gen. 6-7). Clave exacta de PKHeX (<c>Formulario.Control</c>) y su texto en castellano de Espana: SOLO lo que <c>lang_es.txt</c> no trae o deja en ingles (<c>EtiquetasLang.Control</c> da prioridad a este diccionario).
/// Cada entrada lleva un comentario con el motivo. Este fichero lo edita unicamente el bloque 8b-1.
/// </summary>
internal static class RespaldoGen6y7
{
    public static readonly IReadOnlyDictionary<string, string> Textos = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        // Controles de PKHeX que no llevan etiqueta propia en WinForms (la etiqueta del Nexo Pokémon sale del texto de su caja; los campos de género y forma del Superentrenamiento son cajas sin rotular junto a la especie): en el movil cada campo lleva la suya.
        ["SAV_Link6.RTB_LinkSource"] = "Origen",
        ["SAV_SuperTrain.MTB_Gender1"] = "Género (1)",
        ["SAV_SuperTrain.MTB_Gender2"] = "Género (2)",
        ["SAV_SuperTrain.MTB_Form1"] = "Forma (1)",
        ["SAV_SuperTrain.MTB_Form2"] = "Forma (2)",
        // «Pokémon» es el mismo nombre propio en los dos idiomas (lang_es.txt lo deja igual que lang_en.txt): se confirma aqui para que la prueba de localizacion no lo tome por una etiqueta sin traducir.
        ["SAV_Link6.TAB_PKM"] = "Pokémon",
        // «PKM n:» queda sin traducir en lang_es.txt (las siglas de Pokémon): en la pantalla se escribe entero.
        ["SAV_HallOfFame7.L_F1"] = "Pokémon 1",
        ["SAV_HallOfFame7.L_F2"] = "Pokémon 2",
        ["SAV_HallOfFame7.L_F3"] = "Pokémon 3",
        ["SAV_HallOfFame7.L_F4"] = "Pokémon 4",
        ["SAV_HallOfFame7.L_F5"] = "Pokémon 5",
        ["SAV_HallOfFame7.L_F6"] = "Pokémon 6",
        ["SAV_HallOfFame7.L_C1"] = "Pokémon 1",
        ["SAV_HallOfFame7.L_C2"] = "Pokémon 2",
        ["SAV_HallOfFame7.L_C3"] = "Pokémon 3",
        ["SAV_HallOfFame7.L_C4"] = "Pokémon 4",
        ["SAV_HallOfFame7.L_C5"] = "Pokémon 5",
        ["SAV_HallOfFame7.L_C6"] = "Pokémon 6",
        // «X» es el boton de quitar una base (lang_es.txt lo deja como «X»); «EVs» e «IVs» son las siglas que PKHeX usa igual en castellano: se confirman aqui para que la prueba de localizacion no las tome por sin traducir.
        ["SAV_SecretBase.B_FDelete"] = "Quitar",
        ["SAV_SecretBase.L_EVs"] = "EVs",
        ["SAV_SecretBase.L_IVs"] = "IVs",
        // SAV_FestivalPlaza no lleva ningun texto en lang_es.txt (el formulario de PKHeX no se traduce: se queda en ingles aunque el idioma sea el castellano): estas son las etiquetas de su diseñador en castellano de España.
        ["SAV_FestivalPlaza.L_PlazaName"] = "Nombre de Festi Plaza",
        ["SAV_FestivalPlaza.L_Rank"] = "Rango",
        ["SAV_FestivalPlaza.GB_FC"] = "Festimonedas",
        ["SAV_FestivalPlaza.L_FC_Used"] = "Usadas",
        ["SAV_FestivalPlaza.L_FC_Current"] = "Actuales",
        ["SAV_FestivalPlaza.L_FC_CollectedL"] = "Recogidas",
        ["SAV_FestivalPlaza.GB_MyMessage"] = "Mensajes propios",
        ["SAV_FestivalPlaza.L_MyMessageMeet"] = "Encuentro",
        ["SAV_FestivalPlaza.L_MyMessagePart"] = "Despedida",
        ["SAV_FestivalPlaza.L_MyMessageMoved"] = "Emoción",
        ["SAV_FestivalPlaza.L_MyMessageDisappointed"] = "Decepción",
        ["SAV_FestivalPlaza.GB_FestaStartTime"] = "Última hora de inicio",
        ["SAV_FestivalPlaza.B_AllPhrases"] = "Marcar todas",
        ["SAV_FestivalPlaza.B_AllReceiveReward"] = "Todos recibidos",
        ["SAV_FestivalPlaza.B_AllReadyReward"] = "Todos listos para recoger",
        ["SAV_FestivalPlaza.L_FacilityType"] = "Tipo",
        ["SAV_FestivalPlaza.L_FacilityColor"] = "Color",
        ["SAV_FestivalPlaza.L_FacilityNPC"] = "PNJ",
        ["SAV_FestivalPlaza.CHK_FacilityIntroduced"] = "El visitante presentó esta instalación",
        ["SAV_FestivalPlaza.L_VisitorName"] = "Nombre del visitante",
        ["SAV_FestivalPlaza.GB_FacilityMessage"] = "Mensaje del visitante",
        ["SAV_FestivalPlaza.L_LuckyResult"] = "Resultado",
        ["SAV_FestivalPlaza.L_Exchangable"] = "Intercambios restantes",
        ["SAV_FestivalPlaza.L_UsedFlags"] = "Marcas usadas",
        ["SAV_FestivalPlaza.L_UsedStats"] = "Estadísticas usadas",
        ["SAV_FestivalPlaza.L_FestaID"] = "Valor desconocido / ID de Festi del visitante",
        ["SAV_FestivalPlaza.B_DelVisitor"] = "Borrar datos del visitante",
        ["SAV_FestivalPlaza.Tab_Overview"] = "Resumen",
        ["SAV_FestivalPlaza.GB_Phrase"] = "Frases",
        ["SAV_FestivalPlaza.GB_Reward"] = "Premios por rango",
        ["SAV_FestivalPlaza.Tab_Facility"] = "Instalaciones",
        ["SAV_FestivalPlaza.Tab_BattleAgency"] = "Agencia de batalla",
        ["SAV_FestivalPlaza.L_Grade"] = "Grado",
        ["SAV_FestivalPlaza.L_Defeated"] = "Entrenadores derrotados para el siguiente grado",
        ["SAV_FestivalPlaza.L_DefeatMon"] = "Pokémon derrotados hoy",
        ["SAV_FestivalPlaza.L_Note"] = "Próximos 3 entrenadores",
        ["SAV_FestivalPlaza.B_ImportParty"] = "Importar del equipo",
        ["SAV_FestivalPlaza.CHK_Choosed"] = "Elegido",
        ["SAV_FestivalPlaza.CHK_TrainerInvited"] = "Invitado",
        ["SAV_FestivalPlaza.B_AgentGlass"] = "Dar gafas de agente",
        // «Σ» es un símbolo (la suma): lang_es.txt lo deja igual que lang_en.txt y se confirma aqui.
        ["SAV_Capture7GG.B_SumTotal"] = "Σ",
        // L_Points no esta traducida en lang_es.txt (queda «Points:»).
        ["SAV_OPower.L_Points"] = "Puntos",
    };

    /// <summary>
    /// Etiquetas de los datos de una base secreta (BasesSecretas6): en WinForms son nombres de PROPIEDAD en ingles de un cuadro de propiedades (<c>SecretBase6</c>), no controles con clave en <c>lang_es.txt</c>, asi que no hay
    /// traduccion de PKHeX que tomar. Traduccion propia (ADR 0064); «Banderas» se mantiene como manda el coordinador. Clave = id del campo.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> PropiedadesDeLaBase = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["nueva"] = "Base nueva",
        ["lugar"] = "Ubicación de la base",
        ["boppoyama"] = "Puntuación de Boppoyama",
        ["entrenador"] = "Entrenador",
        ["equipo"] = "Nombre del equipo",
        ["lema"] = "Lema del equipo",
        ["alegre"] = "Frase de alegría",
        ["animo"] = "Frase de ánimo",
        ["pizarra"] = "Frase de la pizarra",
        ["confeti"] = "Frase de la bola de confeti",
        ["rango"] = "Rango de la base",
        ["banderas-amigos"] = "Banderas de amigos",
        ["banderas-otros"] = "Banderas de otros",
        ["banderas-hoy"] = "Banderas recogidas hoy",
        ["banderas-ayer"] = "Banderas recogidas ayer",
        ["idioma"] = "Idioma del entrenador",
        ["genero"] = "Género del entrenador",
        ["vacia"] = "Base vacía",
        ["anulada"] = "Ubicación anulada",
    };
}
