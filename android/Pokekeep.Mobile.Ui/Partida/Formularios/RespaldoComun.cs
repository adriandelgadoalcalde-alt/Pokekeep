// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace Pokekeep.Mobile.Ui.Partida.Formularios;

/// <summary>
/// Etiquetas a mano que valen para varios bloques de la F8 (cabeceras y textos propios de Pokekeep). Clave exacta de PKHeX (<c>Formulario.Control</c>) y su texto en castellano de Espana: SOLO lo que <c>lang_es.txt</c> no trae o deja en ingles (<c>EtiquetasLang.Control</c> da prioridad a este diccionario).
/// Cada entrada lleva un comentario con el motivo. Este fichero lo edita unicamente el bloque F8-0 (cimientos).
/// </summary>
internal static class RespaldoComun
{
    public static readonly IReadOnlyDictionary<string, string> Textos = new Dictionary<string, string>(StringComparer.Ordinal)
    {
    };
}
