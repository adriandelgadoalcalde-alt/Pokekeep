// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Android.Content;
using Android.Provider;
using Pokekeep.Mobile.Ui.Ajustes.Pkhex;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Platform;

/// <summary>
/// Lee el contenido de una carpeta del usuario elegida con <c>OPEN_DOCUMENT_TREE</c> (permiso persistente; ver <c>AlmacenDocumentosAndroid.ElegirCarpetaAsync</c>) para las carpetas de trabajo de PKHeX (bloque 10-1):
/// solo los documentos de primer nivel que no son carpetas, por <c>DocumentsContract</c> y el <c>ContentResolver</c>. Nunca escribe.
/// </summary>
public sealed class ExploradorCarpetasAndroid(Context contexto) : IExploradorCarpetas
{
    private ContentResolver Resolver => contexto.ContentResolver!;

    /// <summary>Los hijos de primer nivel que no son carpetas, con UNA consulta al proveedor (id del documento, nombre visible y tamaño).</summary>
    public IReadOnlyList<ArchivoCarpeta> Listar(string uriCarpeta)
    {
        try { return ListarInterno(uriCarpeta); }
        catch (Java.Lang.SecurityException ex) { throw new UnauthorizedAccessException(IdiomaInterfaz.T("AlmacenDocumentosAndroid.SeRetiroElPermiso3"), ex); }
        catch (Java.IO.FileNotFoundException ex) { throw new IOException(IdiomaInterfaz.T("AlmacenDocumentosAndroid.LaCarpetaYaNo2"), ex); }
        catch (Java.Lang.IllegalArgumentException ex) { throw new IOException(IdiomaInterfaz.T("AlmacenDocumentosAndroid.LaCarpetaNoEs"), ex); }
    }

    private IReadOnlyList<ArchivoCarpeta> ListarInterno(string uriCarpeta)
    {
        var arbol = global::Android.Net.Uri.Parse(uriCarpeta)!;
        var hijos = DocumentsContract.BuildChildDocumentsUriUsingTree(arbol, DocumentsContract.GetTreeDocumentId(arbol))!;
        string[] columnas = [DocumentsContract.Document.ColumnDocumentId, DocumentsContract.Document.ColumnDisplayName, DocumentsContract.Document.ColumnSize, DocumentsContract.Document.ColumnMimeType, DocumentsContract.Document.ColumnLastModified];
        using var c = Resolver.Query(hijos, columnas, null, null, null) ?? throw new IOException(IdiomaInterfaz.T("ExploradorCarpetasAndroid.NoSePudoLeer"));
        var l = new List<ArchivoCarpeta>();
        while (c.MoveToNext())
        {
            if (c.GetString(3) == DocumentsContract.Document.MimeTypeDir) continue;
            var id = c.GetString(0); var nombre = c.GetString(1);
            if (id is null || nombre is null) continue;
            l.Add(new ArchivoCarpeta(id, nombre, c.IsNull(2) ? 0 : c.GetLong(2), c.IsNull(4) ? 0 : c.GetLong(4)));
        }
        return l;
    }

    /// <summary>Lee el documento por su id (el que dio <see cref="Listar"/>): sin volver a consultar la carpeta.</summary>
    public byte[] Leer(string uriCarpeta, ArchivoCarpeta archivo)
    {
        try { return LeerInterno(uriCarpeta, archivo); }
        catch (Java.Lang.SecurityException ex) { throw new UnauthorizedAccessException(IdiomaInterfaz.T("AlmacenDocumentosAndroid.SeRetiroElPermiso3"), ex); }
        catch (Java.IO.FileNotFoundException ex) { throw new IOException(IdiomaInterfaz.T("ExploradorCarpetasAndroid.ElArchivoYaNo"), ex); }
    }

    private byte[] LeerInterno(string uriCarpeta, ArchivoCarpeta archivo)
    {
        var doc = DocumentsContract.BuildDocumentUriUsingTree(global::Android.Net.Uri.Parse(uriCarpeta)!, archivo.Id)!;
        using var s = Resolver.OpenInputStream(doc) ?? throw new IOException(IdiomaInterfaz.T("ExploradorCarpetasAndroid.NoSePudoAbrir"));
        using var ms = new MemoryStream();
        s.CopyTo(ms);   // sin tope aquí: lo aplica quien llama (CarpetasTrabajoPkhex) tras leer, también con tamaño desconocido
        return ms.ToArray();
    }

    /// <summary>Devuelve el permiso persistente que se tomó al elegir la carpeta (<c>releasePersistableUriPermission</c>).</summary>
    public void Soltar(string uriCarpeta)
        => Resolver.ReleasePersistableUriPermission(global::Android.Net.Uri.Parse(uriCarpeta)!, ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);
}
