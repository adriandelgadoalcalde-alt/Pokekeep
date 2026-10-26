// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Android.App;
using Android.Content;
using Android.Database;
using Android.OS;
using Android.Provider;
using Pokekeep.Mobile.Platform;

namespace Pokekeep.Mobile;

/// <summary>
/// Sirve a la hoja de compartir del sistema los archivos temporales de <c>cache/compartido/</c> (un <c>.pk*</c>, un <c>.ek*</c> o la imagen de un QR) con un <c>content://</c> de SOLO LECTURA y permiso por envío
/// (<c>GrantUriPermissions</c>, sin exportar): la app que recibe el archivo lee ese archivo y nada más. Solo atiende nombres de archivo sueltos de esa carpeta (nunca rutas) y no admite escribir ni borrar.
/// PENDIENTE DE DISPOSITIVO: el proveedor solo se ejercita al compartir de verdad.
/// </summary>
[ContentProvider(new[] { CompartirAndroid.Autoridad }, Name = "com.incredibad.pokekeep.CompartirProvider", Exported = false, GrantUriPermissions = true)]
public sealed class CompartirProvider : ContentProvider
{
    public override bool OnCreate() => true;

    private Java.IO.File? Archivo(Android.Net.Uri uri)
    {
        var nombre = uri.LastPathSegment;
        if (string.IsNullOrEmpty(nombre) || nombre != System.IO.Path.GetFileName(nombre)) return null;   // un nombre suelto, nunca una ruta
        var carpeta = CompartirAndroid.RutaCarpeta(Context!);
        var ruta = System.IO.Path.Combine(carpeta, nombre);
        return File.Exists(ruta) ? new Java.IO.File(ruta) : null;
    }

    public override ParcelFileDescriptor? OpenFile(Android.Net.Uri uri, string mode)
    {
        if (mode != "r") throw new Java.IO.FileNotFoundException("Solo lectura.");
        var f = Archivo(uri) ?? throw new Java.IO.FileNotFoundException("El archivo ya no está disponible.");
        return ParcelFileDescriptor.Open(f, ParcelFileMode.ReadOnly);
    }

    public override ICursor? Query(Android.Net.Uri uri, string[]? projection, string? selection, string[]? selectionArgs, string? sortOrder)
    {
        var f = Archivo(uri);
        if (f is null) return null;
        var columnas = projection is { Length: > 0 } ? projection : [OpenableColumns.DisplayName, OpenableColumns.Size];
        var cursor = new MatrixCursor(columnas, 1);
        var fila = cursor.NewRow()!;
        foreach (var c in columnas)
            fila.Add(c, c == OpenableColumns.Size ? Java.Lang.Long.ValueOf(f.Length()) : new Java.Lang.String(f.Name));
        return cursor;
    }

    public override string? GetType(Android.Net.Uri uri) => "application/octet-stream";
    public override Android.Net.Uri? Insert(Android.Net.Uri uri, ContentValues? values) => null;
    public override int Delete(Android.Net.Uri uri, string? selection, string[]? selectionArgs) => 0;
    public override int Update(Android.Net.Uri uri, ContentValues? values, string? selection, string[]? selectionArgs) => 0;
}
