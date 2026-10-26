// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace Pokekeep.Mobile.Ui.Ajustes.Pkhex;

public static partial class RespaldoAjustesPkhex
{
    // BoxExportSettings es una clase del Core sin [LocalizedDescription]: lang_es.txt trae la etiqueta de cada propiedad (PropertyGrid.Scope, .FolderCreation...) pero ninguna descripción. Se escriben a partir
    // de los comentarios del código del Core (BoxExportSettings.cs) y del comportamiento de BoxExport.Export. Clave por RUTA: «Scope», «Notify»... son nombres genéricos que otros grupos podrían usar.
    static partial void AnadirDescripcionesB10_8(Action<string, string> a)
    {
        a("SlotExport.BoxExport.Scope", "Exportar todas las cajas o solo la que se está viendo.");
        a("SlotExport.BoxExport.FolderCreation", "Dejar todos los archivos en la carpeta elegida o crear dentro una carpeta por caja.");
        a("SlotExport.BoxExport.FolderPrefix", "Nombre de la carpeta de cada caja (solo con «Carpeta por caja»).");
        a("SlotExport.BoxExport.EmptySlots", "Qué hacer con los huecos vacíos o con datos no válidos: omitirlos o exportarlos también como archivo.");
        a("SlotExport.BoxExport.FileIndexPrefix", "Número que se pone delante del nombre de cada archivo para conservar el orden de las cajas.");
        a("SlotExport.BoxExport.Notify", "Avisar al terminar de cuántos archivos se han exportado, o terminar en silencio.");
    }
}
