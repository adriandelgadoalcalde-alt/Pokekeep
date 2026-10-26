// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Android.Provider;
using Pokekeep.Mobile.Ui.Almacen;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Platform;

/// <summary>
/// Bloque 10-8 («Exportar cajas» y «Cargar cajas»): el primer nivel de una carpeta, sus subcarpetas y sustituir un archivo por su nombre, dentro del árbol que el usuario concedió (<c>OPEN_DOCUMENT_TREE</c>).
/// Una carpeta es su URI de árbol (la raíz elegida) o el URI de un documento de carpeta dentro de ese árbol (las subcarpetas): de los dos se saca el árbol y el id del documento.
/// </summary>
public sealed partial class AlmacenDocumentosAndroid : IAlmacenCarpetasCajas
{
    private static readonly string[] ColumnasHijos = [DocumentsContract.Document.ColumnDocumentId, DocumentsContract.Document.ColumnDisplayName, DocumentsContract.Document.ColumnMimeType, DocumentsContract.Document.ColumnSize, DocumentsContract.Document.ColumnLastModified];

    /// <summary>El árbol y el id del documento de la carpeta (raíz del árbol si el URI es el del árbol).</summary>
    private (global::Android.Net.Uri Arbol, string Id) Documento(CarpetaRef carpeta)
    {
        var uri = global::Android.Net.Uri.Parse(carpeta.Uri)!;
        var id = DocumentsContract.IsDocumentUri(contexto, uri) ? DocumentsContract.GetDocumentId(uri)! : DocumentsContract.GetTreeDocumentId(uri)!;
        return (uri, id);
    }

    private IEnumerable<(string Id, string Nombre, bool EsCarpeta, long Tam, long Mod)> Hijos(global::Android.Net.Uri arbol, string id)
    {
        using var c = Resolver.Query(DocumentsContract.BuildChildDocumentsUriUsingTree(arbol, id)!, ColumnasHijos, null, null, null);
        if (c is null) yield break;
        while (c.MoveToNext())
        {
            var hijo = c.GetString(0)!;
            yield return (hijo, c.GetString(1) ?? hijo, c.GetString(2) == DocumentsContract.Document.MimeTypeDir, c.IsNull(3) ? -1 : c.GetLong(3), c.IsNull(4) ? 0 : c.GetLong(4));
        }
    }

    public Task<IReadOnlyList<DocumentoRef>> ArchivosAsync(CarpetaRef carpeta, CancellationToken cancelar) => Task.Run<IReadOnlyList<DocumentoRef>>(() =>
    {
        var (arbol, id) = Documento(carpeta);
        return [.. Hijos(arbol, id).Where(h => !h.EsCarpeta).Select(h => new DocumentoRef(DocumentsContract.BuildDocumentUriUsingTree(arbol, h.Id)!.ToString()!, h.Nombre, h.Mod, h.Tam))];
    }, cancelar);

    public Task<CarpetaRef> SubcarpetaAsync(CarpetaRef carpeta, string nombre, CancellationToken cancelar) => Task.Run(() =>
    {
        var (arbol, id) = Documento(carpeta);
        var existente = Hijos(arbol, id).FirstOrDefault(h => h.EsCarpeta && h.Nombre == nombre);
        var uri = existente.Id is { } e
            ? DocumentsContract.BuildDocumentUriUsingTree(arbol, e)!
            : DocumentsContract.CreateDocument(Resolver, DocumentsContract.BuildDocumentUriUsingTree(arbol, id)!, DocumentsContract.Document.MimeTypeDir, nombre)
              ?? throw new IOException(IdiomaInterfaz.T("AlmacenDocumentosAndroidCajas.ElProveedorNoPermite"));
        return new CarpetaRef(uri.ToString()!, nombre);
    }, cancelar);

    public Task<DocumentoRef> ArchivoParaEscribirAsync(CarpetaRef carpeta, string nombre, CancellationToken cancelar) => Task.Run(() =>
    {
        var (arbol, id) = Documento(carpeta);
        var existente = Hijos(arbol, id).FirstOrDefault(h => !h.EsCarpeta && h.Nombre == nombre);
        if (existente.Id is { } e) return Info(DocumentsContract.BuildDocumentUriUsingTree(arbol, e)!);   // se sustituye con EscribirAsync («rwt»), como File.WriteAllBytes
        var nuevo = DocumentsContract.CreateDocument(Resolver, DocumentsContract.BuildDocumentUriUsingTree(arbol, id)!, "application/octet-stream", nombre)
            ?? throw new IOException(IdiomaInterfaz.T("AlmacenDocumentosAndroidCajas.ElProveedorNoPermite2"));
        return Info(nuevo);
    }, cancelar);
}
