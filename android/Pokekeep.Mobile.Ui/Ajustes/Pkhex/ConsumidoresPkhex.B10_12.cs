// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace Pokekeep.Mobile.Ui.Ajustes.Pkhex;

public static partial class ConsumidoresPkhex
{
    static partial void DeclararB10_12(Action<string, string> d)
    {
        d("Sprite.*", "Compositor de la sesión y compartidos: preferencias, paletas, huevos, encuentros, Teratipo, experiencia y filtros; capas conservan el recuadro del Pokémon");
        d("Hover.HoverSlotShowPreview", "Pestaña Pokémon: ficha de la casilla seleccionada, equivalente táctil de la vista previa al pasar el ratón");
        d("Hover.HoverSlotShowEncounter", "Ficha: información del encuentro en panel desplegable");
        d("Hover.HoverSlotShowEncounterVerbose", "Ficha: propiedades completas del encuentro");
        d("Hover.HoverSlotShowLegalityHint", "Ficha: primeros motivos del Core si el Pokémon es ilegal");
        d("Hover.HoverSlotShowText", "Ficha: resumen localizado con BattleTemplate.Hover");
        d("Hover.HoverSlotPlayCry", "Selección de casilla: archivo WAV local por especie/forma, ruta de CryPlayer y reproductor de Android");
        d("LocalResources.SoundPath", "Selección de casilla (ShellViewModel.ReproducirGrito): carpeta de gritos elegida con el selector del sistema; GetCryPath apunta a su copia local");
        d("Hover.PreviewShowPaste", "Ficha: set de Showdown en panel desplegable");
        d("Draw.InvalidSelection", "Editor: fondo de una selección que no está en la lista de opciones vigente");
        d("Draw.MarkBlue", "Editor: muestra de la marca azul en Cosmético");
        d("Draw.MarkPink", "Editor: muestra de la marca rosa en Cosmético");
        d("Draw.ShinyUnicode", "SpritePokemon: símbolo de variocolor con Unicode");
        d("Draw.ShinyDefault", "SpritePokemon: símbolo de variocolor sin Unicode");
    }
}
