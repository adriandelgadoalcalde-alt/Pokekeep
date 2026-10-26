// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace Pokekeep.Mobile.Ui.Almacen;

/// <summary>
/// Lo que el bloque 10-8 necesita de una carpeta del usuario (SAF) para hacer lo que PKHeX hace con <c>Directory</c> y <c>File</c> en «Exportar cajas» (<c>BoxExport.Export</c>) y «Cargar cajas»
/// (<c>SaveFile.LoadBoxes(path)</c>). Va aparte de <see cref="IAlmacenCarpetas"/> para no pisar a otros bloques: lo implementa el mismo almacén de Android y, en las pruebas, una carpeta del disco.
/// Una carpeta hija se representa con otro <see cref="CarpetaRef"/> (en Android, el documento de la carpeta dentro del árbol concedido); nunca hay rutas de fichero.
/// </summary>
public interface IAlmacenCarpetasCajas
{
    /// <summary>
    /// Los ARCHIVOS del primer nivel de la carpeta, sin entrar en subcarpetas (<c>SearchOption.TopDirectoryOnly</c>, lo que hace <c>LoadBoxes</c> de PKHeX). Nunca escribe.
    /// </summary>
    Task<IReadOnlyList<DocumentoRef>> ArchivosAsync(CarpetaRef carpeta, CancellationToken cancelar);

    /// <summary>La subcarpeta con ese nombre: la que ya existe o una nueva (<c>Directory.CreateDirectory</c>).</summary>
    Task<CarpetaRef> SubcarpetaAsync(CarpetaRef carpeta, string nombre, CancellationToken cancelar);

    /// <summary>
    /// El archivo con ese nombre EXACTO en la carpeta para escribirlo entero: el que ya existe (se sustituye, como <c>File.WriteAllBytes</c>) o uno nuevo. Se escribe con <see cref="IAlmacenDocumentos.EscribirAsync"/>.
    /// </summary>
    Task<DocumentoRef> ArchivoParaEscribirAsync(CarpetaRef carpeta, string nombre, CancellationToken cancelar);
}
