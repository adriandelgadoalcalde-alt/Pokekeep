// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Android.App;
using Android.Content;
using Android.Provider;
using Pokekeep.Mobile.Ui.Almacen;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Platform;

/// <summary>Reparte <c>OnActivityResult</c> de la actividad a quien lanzo un selector del sistema (sin <c>Activity Result API</c>: la actividad es de Avalonia).</summary>
public static class PuenteActividad
{
    private static Activity? _actividad;
    private static readonly Dictionary<int, TaskCompletionSource<(Result Resultado, Intent? Datos)>> Pendientes = [];
    private static int _codigo = 7000;

    public static void Registrar(Activity actividad) => _actividad = actividad;

    /// <summary>La actividad viva (null si no hay): para pedir permisos en tiempo de ejecución (notificaciones del servicio de las operaciones largas, F9 9c).</summary>
    public static Activity? Actual => _actividad;
    /// <summary>La actividad de la app (null antes de crearse): para lo que cambia la ventana, como el brillo del código QR.</summary>
    public static Activity? Actividad => _actividad;

    public static Task<(Result Resultado, Intent? Datos)> LanzarAsync(Intent intencion)
    {
        var actividad = _actividad ?? throw new InvalidOperationException(IdiomaInterfaz.T("AlmacenDocumentosAndroid.NoHayActividadPara"));
        var tcs = new TaskCompletionSource<(Result, Intent?)>(TaskCreationOptions.RunContinuationsAsynchronously);
        int codigo;
        lock (Pendientes) { codigo = ++_codigo; Pendientes[codigo] = tcs; }
        actividad.RunOnUiThread(() => actividad.StartActivityForResult(intencion, codigo));
        return tcs.Task;
    }

    /// <summary>Lo llama <c>MainActivity.OnActivityResult</c>. Devuelve true si el codigo era de un selector nuestro.</summary>
    public static bool Entregar(int codigo, Result resultado, Intent? datos)
    {
        TaskCompletionSource<(Result, Intent?)>? tcs;
        lock (Pendientes) { if (!Pendientes.Remove(codigo, out tcs)) return false; }
        tcs.TrySetResult((resultado, datos));
        return true;
    }
}

/// <summary>
/// Almacenamiento por el Storage Access Framework (plan 5.5): <c>ACTION_OPEN_DOCUMENT</c> con permiso persistente, <c>ACTION_CREATE_DOCUMENT</c> para exportar y <c>ContentResolver</c> para leer y para
/// escribir en una pasada con <c>openFileDescriptor(uri, "rwt")</c> (nunca borrar y crear). Nada de rutas de fichero: el original puede estar en cualquier proveedor.
/// </summary>
public sealed partial class AlmacenDocumentosAndroid(Context contexto) : IAlmacenDocumentos, IAlmacenCarpetas
{
    private ContentResolver Resolver => contexto.ContentResolver!;

    /// <summary>
    /// Bloque 10-4, «Detectar automáticamente la partida más reciente» (<c>Startup.TryDetectRecentSave</c>): quien la pone devuelve la ubicación del guardado más reciente (o null) y el selector se abre ahí
    /// (<c>EXTRA_INITIAL_URI</c>, API 26+), como PKHeX abre su diálogo en esa carpeta. Una ruta de fichero no sirve de sugerencia en Android: se ignora.
    /// </summary>
    public Func<CancellationToken, Task<string?>>? SugerirInicial { get; set; }

    public async Task<DocumentoRef?> ElegirParaAbrirAsync(CancellationToken cancelar)
    {
        var intencion = new Intent(Intent.ActionOpenDocument).AddCategory(Intent.CategoryOpenable).SetType("*/*")
            .AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission | ActivityFlags.GrantPersistableUriPermission);
        if (SugerirInicial is not null && (int)global::Android.OS.Build.VERSION.SdkInt >= 26)
        {
            string? sugerida = null;
            try { sugerida = await SugerirInicial(cancelar).ConfigureAwait(false); }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException) { }
            if (sugerida is not null && sugerida.StartsWith("content://", StringComparison.Ordinal)) intencion.PutExtra(DocumentsContract.ExtraInitialUri, global::Android.Net.Uri.Parse(sugerida));
        }
        var (resultado, datos) = await PuenteActividad.LanzarAsync(intencion).ConfigureAwait(false);
        if (resultado != Result.Ok || datos?.Data is not { } uri) return null;
        var escritura = datos.Flags.HasFlag(ActivityFlags.GrantWriteUriPermission);
        try { Resolver.TakePersistableUriPermission(uri, ActivityFlags.GrantReadUriPermission | (escritura ? ActivityFlags.GrantWriteUriPermission : 0)); }
        catch (Exception ex) when (ex is Java.Lang.SecurityException or Java.Lang.IllegalArgumentException) { /* el proveedor no ofrece permiso persistente: se abre igual, sin «recientes» fiables */ }
        return Info(uri) with { SoloLectura = !escritura };
    }

    public async Task<DocumentoRef?> ElegirDestinoAsync(string nombreSugerido, CancellationToken cancelar)
    {
        var intencion = new Intent(Intent.ActionCreateDocument).AddCategory(Intent.CategoryOpenable).SetType("application/octet-stream").PutExtra(Intent.ExtraTitle, nombreSugerido)
            .AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);
        var (resultado, datos) = await PuenteActividad.LanzarAsync(intencion).ConfigureAwait(false);
        return resultado == Result.Ok && datos?.Data is { } uri ? Info(uri) : null;
    }

    public async Task<DocumentoRef?> ElegirOriginalAsync(DocumentoRef original, CancellationToken cancelar)
    {
        var intencion = new Intent(Intent.ActionOpenDocument).AddCategory(Intent.CategoryOpenable).SetType("*/*")
            .AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission | ActivityFlags.GrantPersistableUriPermission);
        if ((int)global::Android.OS.Build.VERSION.SdkInt >= 26 && original.Uri.StartsWith("content://", StringComparison.Ordinal))
            intencion.PutExtra(DocumentsContract.ExtraInitialUri, global::Android.Net.Uri.Parse(original.Uri));
        var (resultado, datos) = await PuenteActividad.LanzarAsync(intencion).ConfigureAwait(false);
        if (resultado != Result.Ok || datos?.Data is not { } uri) return null;
        // El proveedor concede el permiso temporal. El gestor comprobará nombre, tamaño y hash ANTES de persistirlo.
        try { return Info(uri) with { SoloLectura = !datos.Flags.HasFlag(ActivityFlags.GrantWriteUriPermission) }; }
        catch (Java.Lang.SecurityException ex) { throw new UnauthorizedAccessException(IdiomaInterfaz.T("AlmacenDocumentosAndroid.ElProveedorRetiroEl"), ex); }
        catch (Java.IO.FileNotFoundException ex) { throw new IOException(IdiomaInterfaz.T("AlmacenDocumentosAndroid.ElDocumentoElegidoYa"), ex); }
        catch (Java.Lang.IllegalArgumentException ex) { throw new IOException(IdiomaInterfaz.T("AlmacenDocumentosAndroid.ElProveedorNoPudo"), ex); }
        catch (InvalidOperationException ex) { throw new IOException(IdiomaInterfaz.T("AlmacenDocumentosAndroid.ElProveedorNoPudo"), ex); }
    }

    public Task<bool> PersistirEscrituraAsync(DocumentoRef elegido, CancellationToken cancelar)
    {
        cancelar.ThrowIfCancellationRequested();
        try
        {
            var uri = global::Android.Net.Uri.Parse(elegido.Uri)!;
            Resolver.TakePersistableUriPermission(uri, ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);
            return Task.FromResult(Resolver.PersistedUriPermissions.Any(p => p.Uri is { } u && u.Equals(uri) && p.IsReadPermission && p.IsWritePermission));
        }
        catch (Exception ex) when (ex is Java.Lang.SecurityException or Java.Lang.IllegalArgumentException) { return Task.FromResult(false); }
    }

    public Task<DocumentoRef?> ObtenerAsync(string uri, CancellationToken cancelar)
    {
        try { return Task.FromResult<DocumentoRef?>(Info(global::Android.Net.Uri.Parse(uri)!)); }
        catch (Exception ex) when (ex is Java.IO.FileNotFoundException or Java.Lang.SecurityException or Java.Lang.IllegalArgumentException or InvalidOperationException) { return Task.FromResult<DocumentoRef?>(null); }
    }

    public Task<bool> PuedeLeerAsync(DocumentoRef documento, CancellationToken cancelar) => Task.Run(() =>
    {
        cancelar.ThrowIfCancellationRequested();
        try { using var s = Resolver.OpenInputStream(global::Android.Net.Uri.Parse(documento.Uri)!); if (s is null) return false; s.ReadByte(); return true; }
        catch (Exception ex) when (ex is Java.IO.IOException or Java.Lang.SecurityException or Java.Lang.IllegalArgumentException) { return false; }
    }, cancelar);

    private DocumentoRef Info(global::Android.Net.Uri uri)
    {
        // Un proveedor de documentos (SAF) da DOCUMENT_LAST_MODIFIED; MediaStore da DATE_MODIFIED (segundos). Se prueba la proyeccion completa y, si el proveedor no la entiende, la minima.
        foreach (var columnas in new[]
        {
            new[] { IOpenableColumns.DisplayName, IOpenableColumns.Size, DocumentsContract.Document.ColumnLastModified },
            new[] { IOpenableColumns.DisplayName, IOpenableColumns.Size, MediaStore.IMediaColumns.DateModified },
            new[] { IOpenableColumns.DisplayName, IOpenableColumns.Size },
        })
        {
            global::Android.Database.ICursor? c = null;
            try { c = Resolver.Query(uri, columnas, null, null, null); }
            catch (Java.Lang.IllegalArgumentException) { continue; }
            if (c is null) continue;
            using (c)
            {
                if (!c.MoveToFirst()) throw new InvalidOperationException(IdiomaInterfaz.T("AlmacenDocumentosAndroid.ElDocumentoNoExiste"));
                string? nombre = null; long tam = -1, mod = 0;
                for (var i = 0; i < c.ColumnCount; i++)
                {
                    var n = c.GetColumnName(i);
                    if (n == IOpenableColumns.DisplayName) nombre = c.GetString(i);
                    else if (n == IOpenableColumns.Size && !c.IsNull(i)) tam = c.GetLong(i);
                    else if (n == DocumentsContract.Document.ColumnLastModified && !c.IsNull(i)) mod = c.GetLong(i);
                    else if (n == MediaStore.IMediaColumns.DateModified && !c.IsNull(i)) mod = c.GetLong(i) * 1000;
                }
                return new DocumentoRef(uri.ToString()!, nombre ?? uri.LastPathSegment ?? "guardado", mod, tam);
            }
        }
        throw new InvalidOperationException(IdiomaInterfaz.T("AlmacenDocumentosAndroid.ElProveedorNoEntiende"));
    }

    /// <summary>
    /// El documento que trae un Intent de «Abrir con» (ACTION_VIEW) o «Compartir» (ACTION_SEND) de otra app (F10-ABRIR-001/002). El permiso es el que dio esa app: si no concedió escritura, el documento es
    /// de solo lectura y «Guardar» no lo sustituye (F10-ABRIR-003). Si concedió permiso persistente, se toma (así funciona también desde «Recientes»). Nunca lanza: un proveedor que no responde a la consulta
    /// de metadatos da un documento con tamaño desconocido (se lee con tope).
    /// </summary>
    public DocumentoRef DeIntent(global::Android.Net.Uri uri, ActivityFlags flags)
    {
        var escritura = contexto.CheckCallingOrSelfUriPermission(uri, ActivityFlags.GrantWriteUriPermission) == global::Android.Content.PM.Permission.Granted;
        if (flags.HasFlag(ActivityFlags.GrantPersistableUriPermission))
        {
            try { Resolver.TakePersistableUriPermission(uri, ActivityFlags.GrantReadUriPermission | (escritura ? ActivityFlags.GrantWriteUriPermission : 0)); }
            catch (Exception ex) when (ex is Java.Lang.SecurityException or Java.Lang.IllegalArgumentException) { /* sin permiso persistente: vale mientras dure esta apertura */ }
        }
        try { return Info(uri) with { SoloLectura = !escritura }; }
        catch (Exception ex) when (ex is Java.Lang.SecurityException or Java.Lang.IllegalArgumentException or InvalidOperationException or Java.IO.FileNotFoundException)
        {
            return new DocumentoRef(uri.ToString()!, uri.LastPathSegment ?? "archivo", 0, -1, !escritura);
        }
    }

    /// <summary>Lee como mucho <paramref name="tope"/> bytes, por trozos: un archivo mayor (o un flujo sin fin) se corta y devuelve null sin cargarlo entero en memoria (F10-ABRIR-007).</summary>
    public Task<byte[]?> LeerHastaAsync(DocumentoRef documento, long tope, CancellationToken cancelar) => Task.Run(() =>
    {
        try
        {
            using var s = Resolver.OpenInputStream(global::Android.Net.Uri.Parse(documento.Uri)!) ?? throw new IOException(IdiomaInterfaz.T("AlmacenDocumentosAndroid.ElProveedorNoAbrio"));
            using var ms = new MemoryStream();
            var trozo = new byte[81920];
            int n;
            while ((n = s.Read(trozo, 0, trozo.Length)) > 0)
            {
                cancelar.ThrowIfCancellationRequested();
                if (ms.Length + n > tope) return null;
                ms.Write(trozo, 0, n);
            }
            return ms.ToArray();
        }
        catch (Java.Lang.SecurityException ex) { throw new UnauthorizedAccessException(IdiomaInterfaz.T("AlmacenDocumentosAndroid.SeRetiroElPermiso"), ex); }
        catch (Java.IO.IOException ex) { throw new IOException(IdiomaInterfaz.T("AlmacenDocumentosAndroid.NoSePudoLeer"), ex); }
        catch (Java.Lang.IllegalArgumentException ex) { throw new IOException(IdiomaInterfaz.T("AlmacenDocumentosAndroid.ElDocumentoNoEs"), ex); }
    }, cancelar);

    public Task<byte[]> LeerAsync(DocumentoRef documento, CancellationToken cancelar) => Task.Run(() =>
    {
        try
        {
            using var s = Resolver.OpenInputStream(global::Android.Net.Uri.Parse(documento.Uri)!) ?? throw new IOException(IdiomaInterfaz.T("AlmacenDocumentosAndroid.ElProveedorNoAbrio"));
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            return ms.ToArray();
        }
        // Las excepciones de Java no derivan de System.IO: el código común (listas de carpetas, carga al iniciar) solo maneja las de .NET. Pendiente de compilar y de dispositivo.
        catch (Java.Lang.SecurityException ex) { throw new UnauthorizedAccessException(IdiomaInterfaz.T("AlmacenDocumentosAndroid.SeRetiroElPermiso"), ex); }
        catch (Java.IO.FileNotFoundException ex) { throw new IOException(IdiomaInterfaz.T("AlmacenDocumentosAndroid.ElDocumentoYaNo"), ex); }
        catch (Java.Lang.IllegalArgumentException ex) { throw new IOException(IdiomaInterfaz.T("AlmacenDocumentosAndroid.ElDocumentoNoEs"), ex); }
    }, cancelar);

    public Task EscribirAsync(DocumentoRef documento, ReadOnlyMemory<byte> datos, CancellationToken cancelar) => Task.Run(() =>
    {
        using var pfd = Resolver.OpenFileDescriptor(global::Android.Net.Uri.Parse(documento.Uri)!, "rwt") ?? throw new IOException(IdiomaInterfaz.T("AlmacenDocumentosAndroid.ElProveedorNoPermite"));
        using var fos = new Java.IO.FileOutputStream(pfd.FileDescriptor);
        fos.Write(datos.ToArray());
        fos.Flush();
        fos.FD?.Sync();
    }, cancelar);

    // ---- Carpeta de copias (arbol de SAF con permiso persistente) ----
    public async Task<CarpetaRef?> ElegirCarpetaAsync(CancellationToken cancelar)
    {
        var intencion = new Intent(Intent.ActionOpenDocumentTree)
            .AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission | ActivityFlags.GrantPersistableUriPermission | ActivityFlags.GrantPrefixUriPermission);
        var (resultado, datos) = await PuenteActividad.LanzarAsync(intencion).ConfigureAwait(false);
        if (resultado != Result.Ok || datos?.Data is not { } arbol) return null;
        Resolver.TakePersistableUriPermission(arbol, ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);
        return NombreCarpeta(arbol);
    }

    public Task<CarpetaRef?> ObtenerCarpetaAsync(string uri, CancellationToken cancelar)
    {
        try
        {
            var arbol = global::Android.Net.Uri.Parse(uri)!;
            var concedido = Resolver.PersistedUriPermissions.Any(p => p.Uri is { } u && u.Equals(arbol) && p.IsWritePermission);
            return Task.FromResult(concedido ? NombreCarpeta(arbol) : null);
        }
        catch (Exception ex) when (ex is Java.IO.FileNotFoundException or Java.Lang.SecurityException or Java.Lang.IllegalArgumentException or InvalidOperationException) { return Task.FromResult<CarpetaRef?>(null); }
    }

    private CarpetaRef NombreCarpeta(global::Android.Net.Uri arbol)
    {
        var raiz = DocumentsContract.BuildDocumentUriUsingTree(arbol, DocumentsContract.GetTreeDocumentId(arbol))!;
        using var c = Resolver.Query(raiz, [DocumentsContract.Document.ColumnDisplayName], null, null, null)
            ?? throw new InvalidOperationException(IdiomaInterfaz.T("AlmacenDocumentosAndroid.LaCarpetaYaNo"));
        if (!c.MoveToFirst()) throw new InvalidOperationException(IdiomaInterfaz.T("AlmacenDocumentosAndroid.LaCarpetaYaNo2"));
        return new CarpetaRef(arbol.ToString()!, c.GetString(0) ?? "carpeta");
    }

    public Task<DocumentoRef> CrearEnCarpetaAsync(CarpetaRef carpeta, string nombre, CancellationToken cancelar) => Task.Run(() =>
    {
        var arbol = global::Android.Net.Uri.Parse(carpeta.Uri)!;
        var raiz = DocumentsContract.BuildDocumentUriUsingTree(arbol, DocumentsContract.GetTreeDocumentId(arbol))!;
        var nuevo = DocumentsContract.CreateDocument(Resolver, raiz, "application/octet-stream", nombre)
            ?? throw new IOException(IdiomaInterfaz.T("AlmacenDocumentosAndroidCajas.ElProveedorNoPermite2"));
        return Info(nuevo);
    }, cancelar);

    /// <summary>X38: borra un documento del árbol de SAF (solo los archivos del banco propio de Pokémon, a orden del usuario).</summary>
    public Task BorrarAsync(DocumentoRef documento, CancellationToken cancelar) => Task.Run(() =>
    {
        cancelar.ThrowIfCancellationRequested();
        try
        {
            if (!DocumentsContract.DeleteDocument(Resolver, global::Android.Net.Uri.Parse(documento.Uri)!)) throw new IOException("El proveedor no ha borrado el archivo.");
        }
        catch (Java.Lang.SecurityException ex) { throw new UnauthorizedAccessException("Se retiró el permiso de la carpeta.", ex); }
        catch (Java.IO.FileNotFoundException) { /* ya no existe: el resultado buscado */ }
        catch (Java.Lang.IllegalArgumentException ex) { throw new IOException("El documento no es válido.", ex); }
    }, cancelar);

    /// <summary>Bloque 10-4: los documentos del árbol y de sus subcarpetas (<c>SearchOption.AllDirectories</c> de PKHeX), con un tope de profundidad para no recorrer una tarjeta entera.</summary>
    public Task<IReadOnlyList<DocumentoRef>> ListarAsync(CarpetaRef carpeta, CancellationToken cancelar) => ListarAsync(carpeta, true, cancelar);

    /// <summary>Bloque 10-5: con <paramref name="profundo"/> falso solo el primer nivel (<c>EntityDb.SearchExtraSavesDeep</c> apagado). Pendiente de compilar y de dispositivo.</summary>
    public Task<IReadOnlyList<DocumentoRef>> ListarAsync(CarpetaRef carpeta, bool profundo, CancellationToken cancelar) => Task.Run<IReadOnlyList<DocumentoRef>>(() =>
    {
        try { return ListarInterno(carpeta, profundo, cancelar); }
        // Ver LeerAsync: las excepciones de Java se convierten en las de .NET que maneja el código común. Pendiente de compilar y de dispositivo.
        catch (Java.Lang.SecurityException ex) { throw new UnauthorizedAccessException(IdiomaInterfaz.T("AlmacenDocumentosAndroid.SeRetiroElPermiso3"), ex); }
        catch (Java.IO.FileNotFoundException ex) { throw new IOException(IdiomaInterfaz.T("AlmacenDocumentosAndroid.LaCarpetaYaNo2"), ex); }
        catch (Java.Lang.IllegalArgumentException ex) { throw new IOException(IdiomaInterfaz.T("AlmacenDocumentosAndroid.LaCarpetaNoEs"), ex); }
    }, cancelar);

    private List<DocumentoRef> ListarInterno(CarpetaRef carpeta, bool profundo, CancellationToken cancelar)
    {
        var arbol = global::Android.Net.Uri.Parse(carpeta.Uri)!;
        var l = new List<DocumentoRef>();
        var pendientes = new Stack<(string Id, int Nivel)>();
        pendientes.Push((DocumentsContract.GetTreeDocumentId(arbol)!, 0));
        string[] columnas = [DocumentsContract.Document.ColumnDocumentId, DocumentsContract.Document.ColumnDisplayName, DocumentsContract.Document.ColumnMimeType, DocumentsContract.Document.ColumnSize, DocumentsContract.Document.ColumnLastModified];
        while (pendientes.Count > 0)
        {
            cancelar.ThrowIfCancellationRequested();
            var (id, nivel) = pendientes.Pop();
            var hijos = DocumentsContract.BuildChildDocumentsUriUsingTree(arbol, id)!;
            using var c = Resolver.Query(hijos, columnas, null, null, null);
            if (c is null) continue;
            while (c.MoveToNext())
            {
                var hijo = c.GetString(0)!;
                if (c.GetString(2) == DocumentsContract.Document.MimeTypeDir) { if (profundo && nivel < 8) pendientes.Push((hijo, nivel + 1)); continue; }
                var uri = DocumentsContract.BuildDocumentUriUsingTree(arbol, hijo)!;
                l.Add(new DocumentoRef(uri.ToString()!, c.GetString(1) ?? hijo, c.IsNull(4) ? 0 : c.GetLong(4), c.IsNull(3) ? -1 : c.GetLong(3)));
            }
        }
        return l;
    }
}
