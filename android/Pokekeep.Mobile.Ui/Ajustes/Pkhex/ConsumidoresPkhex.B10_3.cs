// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace Pokekeep.Mobile.Ui.Ajustes.Pkhex;

public static partial class ConsumidoresPkhex
{
    static partial void DeclararB10_3(Action<string, string> d)
    {
        // Conversión: StartupUtil.ReloadSettings los pone en EntityConverter; se notan al importar un Pokémon de otro formato (Cajas > Importar archivo…, Intercambio.Analizar) y en la plantilla del editor.
        // Uno a uno (sin comodín): RetainMetDateTransfer45 NO se declara porque en el Core fijado no hace nada (PK4.ConvertToPK5 descarta la fecha nueva; PK-SET-052 «no aplica»,
        // AjustesConversionTests.RetainMetDateTransfer45NoCambiaNadaEnEsteCore).
        d("Converter.AllowIncompatibleConversion", "Core: EntityConverter permite conversiones que no existen al importar un Pokémon de otro formato (Cajas > Importar archivo…)");
        d("Converter.AllowGuessRejuvenateHOME", "Core: EntityConverter recupera el encuentro original al importar un Pokémon que volvió de HOME (Cajas > Importar archivo…)");
        d("Converter.VirtualConsoleSourceGen1", "Core: versión que lleva un Pokémon de Gen. 1 al importarlo en una partida de Gen. 7 o posterior");
        d("Converter.VirtualConsoleSourceGen2", "Core: versión que lleva un Pokémon de Gen. 2 al importarlo en una partida de Gen. 7 o posterior");
        // Nuevo en PKHeX 26.10.18: EntityConverter lee IsPreferredTransferVirtualHOME en IsOnlyVC3 y lleva un Pokémon de Gen. 3 a una partida de Gen. 8 directo a HOME (origen Rojo Fuego o Verde Hoja de Switch Online) en vez de por las generaciones 4 a 7.
        d("Converter.IsPreferredTransferVirtualHOME", "Core: EntityConverter lleva un Pokémon de Gen. 3 a una partida de Gen. 8 directo a HOME, con origen de la Consola Virtual de Switch (Cajas > Importar archivo…)");
        // Importación de sets: StartupUtil.ReloadSettings los pone en CommonEdits; se notan al importar un set de Showdown (editor > Importar > Set de Showdown, Intercambio.AplicarShowdown).
        d("Import.*", "Core: CommonEdits.ApplySetDetails al importar un set de Showdown en el editor");
        // Avisar de cambios sin guardar: la Shell pregunta antes de pasar a otra partida (ShellViewModel.CambiarDeSesion).
        d("SlotWrite.ModifyUnset", "Partida: aviso antes de abrir otra partida (o una reciente o recuperada) con cambios sin guardar");
        // Privacidad: la cabecera (ShellViewModel.TituloPartida) y el editor (Catalogo.OcultarSecretos).
        d("Privacy.HideSAVDetails", "Cabecera de la partida: solo el juego, sin el nombre del archivo");
        d("Privacy.HideSecretDetails", "Editor del Pokémon: oculta el PID, la constante de cifrado y el rastreador de HOME");
        // Guardados antiguos: StartupUtil.ReloadSettings los pone en SaveLanguage; se notan al abrir un guardado de Gen. 1-3 sin idioma o versión deducibles (AperturaGuardado: SaveLanguage.TryRevise).
        d("SaveLanguage.*", "Core: SaveLanguage.TryRevise al abrir un guardado de Gen. 1-3 cuyo idioma o versión no se pueden deducir");
        d("LocalResources.TemplatePath", "Nuevo Pokémon: PlantillaEditor.Crear carga la plantilla vigente con SaveFile.LoadTemplate, desde el menú de una casilla vacía");
        // Editor: Catalogo (Principal, Encuentro, Estadísticas) lee EntityEditor en el momento, como PKMEditor/StatEditor/BallBrowser.
        // En PKHeX ShowGenderGen1 también manda en la vista previa de la casilla (PokePreview.cs:71): el detalle de ranura es del bloque 10-12, que lo leerá del mismo ajuste (ADR 0075).
        d("EntityEditor.*", "Editor del Pokémon: Poder Oculto, orden de las Poké Balls, sexo en Gen. 1, estado alterado y barra de experiencia");
    }
}
