// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace Pokekeep.Mobile.Ui.Ajustes.Pkhex;

public static partial class ConsumidoresPkhex
{
    static partial void DeclararPiloto(Action<string, string> d)
    {
        // Escritura de ranuras: el Core las lee en SaveFile.SetUpdateDex/SetUpdatePKM/SetUpdateRecords al colocar un Pokémon (StartupUtil.ReloadSettings las fija al aplicar).
        d("SlotWrite.SetUpdateDex", "Core: SaveFile.SetUpdateDex al colocar un Pokémon en una casilla (Pokédex de la partida)");
        d("SlotWrite.SetUpdatePKM", "Core: SaveFile.SetUpdatePKM al colocar un Pokémon (manejador y formato adaptados a la partida)");
        d("SlotWrite.SetUpdateRecords", "Core: SaveFile.SetUpdateRecords al colocar un Pokémon (contadores de huevos y capturas)");
        // Verificar todos los Pokémon (Partida > Integridad): BulkAnalysis recibe Legality.Bulk como en SAVEditor.ClickVerifyStoredEntities.
        d("Legality.Bulk.CheckActiveHandler", "Partida > Integridad > Verificar todos los Pokémon (BulkAnalysisSettings)");
    }
}
