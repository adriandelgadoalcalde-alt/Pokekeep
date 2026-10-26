// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace Pokekeep.Mobile.Ui.Ajustes.Pkhex;

public static partial class ConsumidoresPkhex
{
    static partial void DeclararB10_11(Action<string, string> d)
    {
        d("Display.Unicode", "Editor: símbolos de sexo y nombres de formas Unicode o ASCII");
        d("Display.IgnoreLegalPopup", "Informe de legalidad de cajas y editor: omitir cuando el Core lo considera legal");
        d("Display.ExportLegalityVerboseProperties", "Copiar informe: propiedades de EncounterOriginal.GetTextLines");
        d("Display.ExportLegalityAlwaysVerbose", "Informe de legalidad: detalle inicial e inversión del acceso detallado");
        d("Display.ExportLegalityNeverClipboard", "Informe de legalidad: disponibilidad del botón Copiar");
        d("Display.FlagIllegal", "Cuadrícula: insignias de entidades ilegales, con análisis conservado");
        d("Display.SlotLegalityAlwaysVisible", "Ficha: acceso fijo al informe; el menú por pulsación larga sigue disponible");
        d("Legality.Nickname.NicknamedAnotherSpecies", "Core: NicknameVerifier, severidad del apodo que coincide con otra especie");
        // No declarar el subárbol entero: PKHeX conserva restricciones sin consumidor
        // (regalos Gen1/2/3 y 8+, Let’s Go y juegos sin intercambios de apodo fijo).
        // Se mantiene el árbol compatible de configuración, pero no se ofrecen controles sin efecto.
        foreach (var grupo in new[] { "Nickname12", "Nickname3", "Nickname4", "Nickname5", "Nickname6", "Nickname7", "Nickname8", "Nickname8b", "Nickname9", "Nickname9a" })
            d($"Legality.Nickname.{grupo}.NicknamedTrade", "Core: NicknameVerifier, severidad del apodo cambiado de un intercambio con apodo fijo; informe e insignias invalidadas por HuellaLegalidad");
        foreach (var grupo in new[] { "Nickname4", "Nickname5", "Nickname6", "Nickname7" })
            d($"Legality.Nickname.{grupo}.NicknamedMysteryGift", "Core: NicknameVerifier, severidad del apodo cambiado de un regalo no renombrable; informe e insignias invalidadas por HuellaLegalidad");
        d("Legality.Tradeback.AllowGen1Tradeback", "Core: reglas de retorno entre primera y segunda generación en el análisis de legalidad");
        d("Legality.WordFilter.CheckWordFilter", "Core: comprobación del filtro de palabras en apodo y entrenador");
        d("Legality.WordFilter.DisableWordFilterPastGen", "Core: ámbito generacional del filtro de palabras");
    }
}
