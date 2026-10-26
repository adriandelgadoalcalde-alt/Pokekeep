// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;

namespace Pokekeep.Mobile.Ui.Ajustes.Pkhex;

/// <summary>
/// Pone los textos de programa del Core (<c>MessageStrings</c>: «Sumas de verificación son válidas», «Copiar el informe al portapapeles»...) en el idioma de la interfaz con la llamada que usa PKHeX al cambiar de idioma
/// (<c>LocalizationUtil.SetLocalization</c>). De forma síncrona (PKHeX lo hace en una tarea y por eso a veces enseña un mensaje en inglés justo al arrancar) y de nuevo cada vez que cambia el idioma
/// (X36e: antes se fijaba en castellano una sola vez por proceso). Es el ÚNICO mecanismo: <c>Idioma.Asegurar</c> (F4; lo llama cada <c>EditorCasillas</c> al abrir una partida) delega aquí.
/// </summary>
public static class TextosNucleo
{
    private static readonly Lock _cerrojo = new();
    private static string? _aplicado;

    public static void Preparar()
    {
        ParcheTextosLegalidad.Aplicar();
        var idioma = ProgramaPkhex.Idioma;
        if (_aplicado == idioma) return;
        lock (_cerrojo)
        {
            if (_aplicado == idioma) return;
            _aplicado = idioma;   // si falla no se reintenta en cada llamada: quedan los textos que hubiera, nunca vacíos
            try { LocalizationUtil.SetLocalization(typeof(MessageStrings), idioma); } catch { /* sin traducción */ }
        }
    }
}