// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace Pokekeep.Mobile.Ui.Ajustes.Pkhex;

public static partial class ConsumidoresPkhex
{
    static partial void DeclararB10_8(Action<string, string> d)
    {
        // Exportar cajas (BoxExporter): el exportador abre con SlotExport.BoxExport (con el alcance forzado desde el menú de Cajas) y BoxExport.Export los lee al escribir; al cerrar, PKHeX guarda lo elegido.
        d("SlotExport.BoxExport.*", "Más > Herramientas > Exportar cajas y Cajas > ⋮ > Exportar caja/cajas (BoxExport.Export: alcance, carpetas, huecos vacíos, prefijo y aviso)");
        d("SlotExport.DefaultBoxExportNamer", "Exportar cajas: nombrador elegido al abrir (EntityFileNamer.AvailableNamers) y guardado al cambiarlo");
    }
}
