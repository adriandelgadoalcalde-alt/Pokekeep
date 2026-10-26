// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace Pokekeep.Mobile.Ui.Partida.Formularios;

/// <summary>
/// Etiquetas a mano del bloque 8a-1 (Gen. 1-3). Clave exacta de PKHeX (<c>Formulario.Control</c>) y su texto en castellano de Espana: SOLO lo que <c>lang_es.txt</c> no trae o deja en ingles (<c>EtiquetasLang.Control</c> da prioridad a este diccionario).
/// Cada entrada lleva un comentario con el motivo. Este fichero lo edita unicamente el bloque 8a-1.
/// </summary>
internal static class RespaldoGen1a3
{
    public static readonly IReadOnlyDictionary<string, string> Textos = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        // El cuadro de texto del mote no lleva etiqueta propia en WinForms (la casilla «Mote:» hace de etiqueta); en el movil cada campo lleva la suya.
        ["SAV_HallOfFame1.TB_Nickname"] = Localizacion.ServicioIdioma.Recursos("es-ES")["Form.Respaldo.SAV_HallOfFame1.TB_Nickname"],

        // Roamer3: lang_es.txt deja «Level:» y «HP:» como en ingles (PKHeX usa «PS» para los PS maximos y no tiene una etiqueta propia para los actuales).
        ["SAV_Roamer3.L_Level"] = Localizacion.ServicioIdioma.Recursos("es-ES")["Form.Respaldo.SAV_Roamer3.L_Level"],
        ["SAV_Roamer3.L_HP"] = Localizacion.ServicioIdioma.Recursos("es-ES")["Form.Respaldo.SAV_Roamer3.L_HP"],

        // «Shiny» es «Variocolor» en castellano (PKHeX lo usa asi en el resto de formularios). «Def» y «Ferry» valen igual en los dos idiomas: estan en la lista de permitidos del control de localizacion, no aqui.
        ["SAV_HallOfFame3.CHK_Shiny"] = Localizacion.ServicioIdioma.Recursos("es-ES")["Form.Respaldo.SAV_HallOfFame3.CHK_Shiny"],

        // Misc3: «Caption:» (la leyenda de una pintura de Hoenn) queda en ingles en lang_es.txt.
        ["SAV_Misc3.L_Caption"] = Localizacion.ServicioIdioma.Recursos("es-ES")["Form.Respaldo.SAV_Misc3.L_Caption"],

        // SecretBase3: PKHeX no traduce este formulario (WinFormsTranslator.TranslateInterface esta comentado en su constructor), asi que lang_es.txt no trae ninguna de sus etiquetas.
        ["SAV_SecretBase3.L_Trainers"] = Localizacion.ServicioIdioma.Recursos("es-ES")["Form.Respaldo.SAV_SecretBase3.L_Trainers"],
        ["SAV_SecretBase3.CHK_Battled"] = Localizacion.ServicioIdioma.Recursos("es-ES")["Form.Respaldo.SAV_SecretBase3.CHK_Battled"],
        ["SAV_SecretBase3.CHK_Registered"] = Localizacion.ServicioIdioma.Recursos("es-ES")["Form.Respaldo.SAV_SecretBase3.CHK_Registered"],
        ["SAV_SecretBase3.L_Entered"] = Localizacion.ServicioIdioma.Recursos("es-ES")["Form.Respaldo.SAV_SecretBase3.L_Entered"],
        ["SAV_SecretBase3.L_Class"] = Localizacion.ServicioIdioma.Recursos("es-ES")["Form.Respaldo.SAV_SecretBase3.L_Class"],
        ["SAV_SecretBase3.L_TID"] = Localizacion.ServicioIdioma.Recursos("es-ES")["Form.Respaldo.SAV_SecretBase3.L_TID"],
        ["SAV_SecretBase3.L_SID"] = Localizacion.ServicioIdioma.Recursos("es-ES")["Form.Respaldo.SAV_SecretBase3.L_SID"],
        ["SAV_SecretBase3.L_Member"] = Localizacion.ServicioIdioma.Recursos("es-ES")["Form.Respaldo.SAV_SecretBase3.L_Member"],
        ["SAV_SecretBase3.L_Item"] = Localizacion.ServicioIdioma.Recursos("es-ES")["Form.Respaldo.SAV_SecretBase3.L_Item"],
        ["SAV_SecretBase3.L_PID"] = Localizacion.ServicioIdioma.Recursos("es-ES")["Form.Respaldo.SAV_SecretBase3.L_PID"],
        ["SAV_SecretBase3.L_Level"] = Localizacion.ServicioIdioma.Recursos("es-ES")["Form.Respaldo.SAV_SecretBase3.L_Level"],
        ["SAV_SecretBase3.L_EVs"] = Localizacion.ServicioIdioma.Recursos("es-ES")["Form.Respaldo.SAV_SecretBase3.L_EVs"],
        ["SAV_SecretBase3.L_Move1"] = Localizacion.ServicioIdioma.Recursos("es-ES")["Form.Respaldo.SAV_SecretBase3.L_Move1"],
        ["SAV_SecretBase3.L_Move2"] = Localizacion.ServicioIdioma.Recursos("es-ES")["Form.Respaldo.SAV_SecretBase3.L_Move2"],
        ["SAV_SecretBase3.L_Move3"] = Localizacion.ServicioIdioma.Recursos("es-ES")["Form.Respaldo.SAV_SecretBase3.L_Move3"],
        ["SAV_SecretBase3.L_Move4"] = Localizacion.ServicioIdioma.Recursos("es-ES")["Form.Respaldo.SAV_SecretBase3.L_Move4"],
    };
}
