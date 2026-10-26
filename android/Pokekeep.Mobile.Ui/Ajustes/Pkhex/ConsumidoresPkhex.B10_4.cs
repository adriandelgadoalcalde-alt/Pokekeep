// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace Pokekeep.Mobile.Ui.Ajustes.Pkhex;

public static partial class ConsumidoresPkhex
{
    static partial void DeclararB10_4(Action<string, string> d)
    {
        // Arranque y recientes (StartupSettings).
        d("Startup.TryDetectRecentSave", "Partida > Abrir: el selector del sistema se abre en la carpeta del guardado más reciente de las otras rutas de copias (ModeloCarpetas.SugerenciaAlAbrirAsync)");
        d("Startup.AutoLoadSaveOnStartup", "Arranque de la app: carga la copia más reciente o la última partida abierta (ModeloCarpetas.AlArrancarAsync)");
        d("Startup.RecentlyLoaded", "Partida > Recientes y Lista de carpetas > Reciente: la lista y su orden (RecientesStore, RecientesPkhex)");
        d("Startup.RecentlyLoadedMaxCount", "Partida > Recientes: cuántas partidas recientes se recuerdan (RecientesPkhex.Anotar)");
        // Copias de seguridad (BackupSettings).
        d("LocalResources.BackupPath", "Al abrir una partida: dónde se escribe la copia automática (.bak) y qué lista «Copias» (CopiasPkhex.AlCargar, GetBackupPath)");
        d("Backup.BAKEnabled", "Al abrir una partida: copia automática del archivo a la carpeta de copias (CopiasPkhex.AlCargar)");
        d("Backup.OtherBackupPaths", "Lista de carpetas (Reciente y Carpetas), Cargar el guardado más reciente y la carga al iniciar (ModeloCarpetas)");
        d("Backup.OtherSaveFileExtensions", "Core: SaveFileMetadata.CustomSaveExtensions (quita «(1)» y « - Copy» del nombre de las copias BAK; ExtensionesPkhex)");
        // Exportar (AdvancedSettings).
        d("Advanced.SaveExportForceSaveAs", "Partida > Guardar: exporta siempre una copia con el selector en vez de sobrescribir el archivo");
        d("Advanced.SaveExportCheckUnsavedEntity", "Partida > Guardar y Exportar: avisa si el Pokémon del editor tiene cambios sin aplicar");
    }
}
