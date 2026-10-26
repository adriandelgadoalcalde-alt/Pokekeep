// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Ajustes.Pkhex;

namespace Pokekeep.Mobile.Ui.Editor;

/// <summary>
/// La plantilla del editor de PKHeX (PK-IO-018, bloque 10-3). PKHeX, al cargar una partida, llena el editor con <c>sav.LoadTemplate(TemplatePath)</c>: si la carpeta de plantillas
/// (<c>LocalResources.TemplatePath</c>, «template» de fábrica) tiene <c>&lt;carpeta&gt;.&lt;pk*&gt;</c> del formato de la partida, ese Pokémon convertido; si no, uno en blanco con los datos del entrenador
/// (<c>EntityTemplates.TemplateFields</c>). En el móvil el editor no está siempre a la vista: la plantilla es lo que abre «Nuevo Pokémon» en una casilla vacía. La carpeta es la que diga el ajuste en el momento
/// (con el bloque 10-1, la que el usuario elija con el selector del sistema, copiada a la carpeta privada).
/// </summary>
public static class PlantillaEditor
{
    /// <summary>El Pokémon con el que empieza el editor para <paramref name="sav"/>: la llamada del Core de PKHeX, con la carpeta del ajuste vigente.</summary>
    public static PKM Crear(SaveFile sav, AjustesPkhex? ajustes = null)
    {
        var carpeta = (ajustes ?? AjustesVigentesPkhex.Actual).LocalResources.GetTemplatePath();
        lock (Pokekeep.Sprites.LegalityGate.Cerrojo) return sav.LoadTemplate(carpeta);   // la conversión de una plantilla de otro formato analiza legalidad (rejuvenecer): mismo cerrojo que el resto (ADR 0060)
    }
}
