// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace Pokekeep.Mobile.Ui.Ajustes.Pkhex;

public static partial class ConsumidoresPkhex
{
    static partial void DeclararB10_1(Action<string, string> d)
    {
        // Arranque (ArranquePkhex.Iniciar → StartupUtil.FormLoadInitialActions, la misma llamada que Program.Main de PKHeX).
        d("Startup.Version", "Arranque: la última versión ejecutada decide si se enseña el registro de cambios y se actualiza al arrancar");
        d("Startup.ShowChangelogOnUpdate", "Arranque: con una versión nueva abre «Acerca de» con las novedades");
        d("Startup.ForceHaXOnLaunch", "Arranque: modo HaX (el editor abre en modo avanzado, la Mochila sin topes) y aviso de modo ilegal");
        d("Startup.DarkMode", "Toda la app: tema oscuro o claro al momento (TemaPkhex)");
        d("Startup.Language", "Nombres del juego (especies, movimientos, objetos…) en las cajas, la ficha y el editor (ProgramaPkhex.Textos)");
        d("Startup.DefaultSaveVersion", "Partida > «Empezar con una partida en blanco» (BlankSaveFile.Get)");
        // Carpetas de trabajo (CarpetasTrabajoPkhex). Las que leen otros bloques (base de datos, copias, sonidos, plantillas, entrenadores) las declara el bloque que las use.
        d("LocalResources.MGDatabasePath", "Core: StartupUtil.ReloadSettings carga los regalos de esa carpeta (EncounterEvent.RefreshMGDB) en la legalidad y la generación");
        d("Advanced.PathBlockKeyList", "Partida > Bloques del guardado: nombres de bloque de <carpeta>/<tipo de guardado>.txt (SAV_BlockDump8)");
        d("Advanced.HideEvent8Contains", "Partida > Bloques del guardado: oculta los nombres de bloque que contienen esas palabras (SAV_BlockDump8)");
        d("Advanced.DragStartThreshold", "Pokémon: distancia mínima del deslizamiento sobre las casillas para cambiar de caja");
    }
}
