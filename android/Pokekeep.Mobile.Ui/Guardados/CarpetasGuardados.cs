// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Pokekeep.Mobile.Ui.Modelo;
using System.Globalization;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Ajustes.Pkhex;
using Pokekeep.Mobile.Ui.Almacen;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Guardados;

/// <summary><c>StartupSettings.LoadSaveFile</c> de PKHeX (el espejo <see cref="InicioPkhex"/> no lleva métodos): el último abierto va delante, sin repetir, con el tope <c>RecentlyLoadedMaxCount</c>.</summary>
public static class RecientesPkhex
{
    public static void Anotar(InicioPkhex startup, string ubicacion)
    {
        var recent = startup.RecentlyLoaded;
        // Igual que el original: si ya estaba se quita (y no se recorta); si no, al llegar al tope se quita el último.
        if (!recent.Remove(ubicacion) && recent.Count >= startup.RecentlyLoadedMaxCount)
            recent.RemoveAt(recent.Count - 1);
        recent.Insert(0, ubicacion);
        // El tope pudo bajar en Ajustes desde la última vez: PKHeX lo recorta al ir entrando partidas (una por vez); aquí igual, nunca de golpe.
    }
}

/// <summary>
/// Un guardado de la «Lista de carpetas» (<c>SavePreview</c> de PKHeX): las mismas columnas (Juego, G, tiempo jugado, fecha, carpeta, nombre, EO, ID e IDS) y dónde está. <see cref="EnLaApp"/> = un archivo de la
/// carpeta privada (las copias de seguridad): se abre como partida SIN documento original (en PKHeX una copia <c>.bak</c> siempre se exporta con «Guardar como»). Si no, <see cref="Documento"/> es el del usuario (SAF).
/// </summary>
public sealed record GuardadoListado(SaveFile Partida, string Nombre, string Carpeta, string Ubicacion, bool EnLaApp, DateTime FechaUtc, DocumentoRef? Documento = null)
{
    public string OT => Partida.OT;
    public int G => Partida.Generation;
    /// <summary>Nombre corto en español para enseñar («Platino»); <see cref="Juego"/> es el del Core, que va a recientes.</summary>
    public string JuegoCorto => NombresJuego.Corto(Partida.Version);
    public string Juego => GameInfo.GetVersionName(Partida.Version) is { Length: > 0 } n ? n : Partida.Version.ToString();
    /// <summary>Como PKHeX: <c>PlayTimeString</c> rellenado a 9 caracteres con ceros (así ordena bien como texto).</summary>
    public string Jugado => Partida.PlayTimeString.PadLeft(9, '0');
    public string Fecha => FechaUtc == DateTime.MinValue ? "—" : FechaUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("es-ES"));
    public string TID => Partida.GetDisplayTID().ToString(Partida.TrainerIDDisplayFormat.GetTrainerIDFormatStringTID());
    public string SID => Partida.GetDisplaySID().ToString(Partida.TrainerIDDisplayFormat.GetTrainerIDFormatStringSID());
    public bool EsCopia => SaveUtil.IsBackupFilePath(Nombre);
}

/// <summary>Una carpeta de la pestaña «Carpetas»: las copias de PKHeX y cada una de las «Otras rutas de copias». <see cref="Existe"/> = el botón de PKHeX habilitado (<c>Directory.Exists</c>).</summary>
public sealed record CarpetaListada(string Nombre, string Ubicacion, bool EnLaApp, bool Existe, string Detalle);

/// <summary>
/// LA «LISTA DE CARPETAS» DE PKHeX SIN PANTALLA (<c>SAV_FolderList</c>, bloque 10-4; PK-SUB-014 a 017, PK-MENU-023, PK-MENU-042, PK-IO-002, PK-IO-061 y los ajustes de arranque). Mismas llamadas al Core que el
/// escritorio (<c>SaveUtil.IsSizeValid</c>, <c>IsBackupFilePath</c>, <c>TryGetSaveFile</c>, <c>SaveLanguage.TryRevise</c>, orden por fecha de escritura) sobre dos clases de carpeta:
/// <list type="bullet">
/// <item>la carpeta de copias de PKHeX (<c>LocalResources.GetBackupPath()</c>, en la carpeta privada de la app): se lee con <c>SaveFinder.GetSaveFiles</c> tal cual;</item>
/// <item>las «Otras rutas de copias» (<c>Backup.OtherBackupPaths</c>): en Android, carpetas que el usuario elige con el selector del sistema (URI de árbol de SAF, permiso persistente), que se recorren con
/// <see cref="IAlmacenCarpetas.ListarAsync"/>. Una ruta absoluta que exista (ajustes traídos del PC, pruebas) se lee como en PKHeX.</item>
/// </list>
/// Lo que PKHeX busca por unidades (tarjetas SD de 3DS y Switch: <c>Get3DSBackupPaths</c>, <c>GetSwitchBackupPaths</c>) no aplica: Android no da unidades ni rutas, y una tarjeta SD se elige como carpeta.
/// SOLO LEE: abrir pasa por la sesión de siempre (copia privada) y borrar solo afecta a las copias de la carpeta de copias de la app.
/// </summary>
public sealed class ModeloCarpetas(IAjustesPkhex ajustes, IAlmacenDocumentos? documentos = null, IAlmacenCarpetas? carpetas = null, Func<IReadOnlyList<RecienteRef>>? recientes = null)
{
    /// <summary>Lo que PKHeX llama «PKHeX Backups» en la lista de carpetas.</summary>
    public static string NombreCopias => IdiomaInterfaz.T("CarpetasGuardados.NombreCopias");
    /// <summary>Tope de PKHeX para la detección de la partida más reciente (<c>SuggestInitialFileName</c>, <c>ClickSaveFileName</c>, <c>DetectSaveFiles</c>): 5 segundos.</summary>
    public static readonly TimeSpan TopeDeteccion = TimeSpan.FromSeconds(5);

    private AjustesPkhex A => ajustes.Actual;

    // ------------------------------------------------------------------------------------------------ carpetas
    /// <summary>Las carpetas de la pestaña «Carpetas» (<c>GetPathList</c>): primero las que existen, sin repetir.</summary>
    public async Task<IReadOnlyList<CarpetaListada>> CarpetasAsync(CancellationToken cancelar = default)
    {
        var (dir, _) = CopiasPkhex.Carpeta(A);
        var l = new List<CarpetaListada> { new(NombreCopias, dir, true, Directory.Exists(dir), IdiomaInterfaz.T("CarpetasGuardados.CarpetaCopiasSeguridadApp")) };
        foreach (var ruta in A.Backup.OtherBackupPaths.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.Ordinal))
        {
            cancelar.ThrowIfCancellationRequested();
            if (EsLocal(ruta)) { l.Add(new(NombreLocal(ruta), ruta, false, Directory.Exists(ruta), IdiomaInterfaz.T("CarpetasGuardados.OtraRutaCopias"))); continue; }
            CarpetaRef? viva = null;
            if (carpetas is not null)
            {
                try { viva = await carpetas.ObtenerCarpetaAsync(ruta, cancelar).ConfigureAwait(false); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException) { }
            }
            l.Add(new(viva?.Nombre ?? NombreDeUri(ruta), ruta, false, viva is not null, viva is null ? IdiomaInterfaz.T("CarpetasGuardados.DisponibleMovioBorroRetiro") : IdiomaInterfaz.T("CarpetasGuardados.OtraRutaCopias")));
        }
        return [.. l.DistinctBy(c => c.Ubicacion).OrderByDescending(c => c.Existe)];
    }

    /// <summary>Una ruta del sistema de archivos (las que PKHeX guarda), frente a una URI de SAF (<c>content://…</c>).</summary>
    public static bool EsLocal(string ruta) => !ruta.Contains("://", StringComparison.Ordinal) && Path.IsPathRooted(ruta);

    /// <summary><c>CustomFolderPath.ResolveFolderName</c>: el nombre de la carpeta de encima, salvo que sea la raíz.</summary>
    private static string NombreLocal(string ruta)
    {
        var di = new DirectoryInfo(ruta);
        var display = di.Parent?.Name ?? di.Name;
        return di.Root.Name == display ? di.Name : display;
    }

    /// <summary>El nombre que se ve de una URI de SAF sin consultarla: lo que hay tras el último «/» o «:» del identificador (<c>primary:Pokemon/Partidas</c> → «Partidas»).</summary>
    public static string NombreDeUri(string uri)
    {
        var u = Uri.UnescapeDataString(uri).TrimEnd('/');
        var corte = u.LastIndexOfAny(['/', ':']);
        return corte >= 0 && corte < u.Length - 1 ? u[(corte + 1)..] : u;
    }

    /// <summary>La carpeta de un documento de SAF por su URI (<c>…/document/primary%3APokemon%2Fx.sav</c> → «Pokemon»), o «???» como PKHeX si no se sabe.</summary>
    public static string CarpetaDeUri(string uri)
    {
        var u = Uri.UnescapeDataString(uri);
        var i = u.LastIndexOf("/document/", StringComparison.Ordinal);
        var id = i >= 0 ? u[(i + "/document/".Length)..] : u;
        var dos = id.IndexOf(':');
        if (dos >= 0) id = id[(dos + 1)..];
        var barra = id.LastIndexOf('/');
        if (barra <= 0) return dos >= 0 ? IdiomaInterfaz.T("Ajustes.Pkhex.Grupo.3") : "???";
        var carpeta = id[..barra];
        var otra = carpeta.LastIndexOf('/');
        return otra >= 0 ? carpeta[(otra + 1)..] : carpeta;
    }

    // ------------------------------------------------------------------------------------------------ guardados
    /// <summary>Pestaña «Copia de seg.»: <c>SaveFinder.GetSaveFiles(drives, false, [backups], false)</c> (TODOS los guardados de la carpeta de copias, también los <c>.bak</c>), más recientes primero.</summary>
    public Task<IReadOnlyList<GuardadoListado>> CopiasAsync(CancellationToken cancelar = default) => Task.Run<IReadOnlyList<GuardadoListado>>(() =>
    {
        var (dir, _) = CopiasPkhex.Carpeta(A);
        return [.. Locales(dir, NombreCopias, ignorarCopias: false, cancelar)];
    }, cancelar);

    /// <summary>
    /// Pestaña «Reciente»: los guardados de las «Otras rutas de copias» (sin las copias <c>.bak</c>; <c>GetSaveFiles(drives, false, extra, true)</c>) y, DELANTE, los abiertos hace poco
    /// (<c>Startup.RecentlyLoaded</c>) que no estén ya, como hace PKHeX (<c>loaded.Concat(recent)</c>).
    /// </summary>
    public async Task<IReadOnlyList<GuardadoListado>> RecientesAsync(CancellationToken cancelar = default)
    {
        var deCarpetas = await OtrasRutasAsync(ignorarCopias: true, cancelar).ConfigureAwait(false);
        var cargados = new List<GuardadoListado>();
        var lista = recientes?.Invoke() ?? [.. A.Startup.RecentlyLoaded.Select(u => new RecienteRef(u, NombreDeUri(u), string.Empty, DateTimeOffset.MinValue))];
        foreach (var r in lista)
        {
            cancelar.ThrowIfCancellationRequested();
            if (deCarpetas.Any(x => x.Ubicacion == r.Uri)) continue;
            if (await DesdeUriAsync(r.Uri, CarpetaDeUri(r.Uri), cancelar).ConfigureAwait(false) is { } g) cargados.Add(g);
        }
        return [.. cargados.Concat(deCarpetas)];
    }

    /// <summary>Los guardados de UNA carpeta de la pestaña «Carpetas» (lo que en PKHeX se ve al abrirla en el Explorador), más recientes primero.</summary>
    public async Task<IReadOnlyList<GuardadoListado>> DeCarpetaAsync(CarpetaListada c, CancellationToken cancelar = default)
    {
        if (c.EnLaApp || EsLocal(c.Ubicacion)) return await Task.Run(() => Locales(c.Ubicacion, c.Nombre, ignorarCopias: false, cancelar).ToList(), cancelar).ConfigureAwait(false);
        return await DeSafAsync(c.Ubicacion, c.Nombre, ignorarCopias: false, cancelar).ConfigureAwait(false);
    }

    private async Task<List<GuardadoListado>> OtrasRutasAsync(bool ignorarCopias, CancellationToken cancelar)
    {
        var l = new List<GuardadoListado>();
        await foreach (var g in OtrasRutasPorFechaAsync(ignorarCopias, cancelar).ConfigureAwait(false)) l.Add(g);
        return l;
    }

    /// <summary>
    /// Los guardados de las «Otras rutas de copias», del más reciente al más antiguo y LEYENDO SOLO LO QUE SE PIDE: los documentos de SAF se ordenan por fecha de modificación (la que da la carpeta) ANTES de leerlos y
    /// reconocerlos, y quien consume puede dejar de pedir en cuanto encuentra el que busca (PKHeX, <c>SaveFinder.FindMostRecentSaveFile</c>, ordena por fecha y reconoce hasta el primero válido). Los de una ruta local
    /// ya vienen reconocidos (<c>SaveFinder.GetSaveFiles</c>). Los de fecha desconocida van al final.
    /// </summary>
    private async IAsyncEnumerable<GuardadoListado> OtrasRutasPorFechaAsync(bool ignorarCopias, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancelar)
    {
        var (copias, _) = CopiasPkhex.Carpeta(A);
        var candidatos = new List<(long Ms, GuardadoListado? Ya, DocumentoRef? Doc, string Carpeta)>();
        foreach (var ruta in A.Backup.OtherBackupPaths.Where(p => !string.IsNullOrWhiteSpace(p) && p != copias).Distinct(StringComparer.Ordinal))
        {
            cancelar.ThrowIfCancellationRequested();
            if (EsLocal(ruta))
                foreach (var g in Locales(ruta, NombreLocal(ruta), ignorarCopias, cancelar)) candidatos.Add((new DateTimeOffset(g.FechaUtc, TimeSpan.Zero).ToUnixTimeMilliseconds(), g, null, g.Carpeta));
            else
            {
                var (nombre, docs) = await DocumentosDeSafAsync(ruta, null, cancelar).ConfigureAwait(false);
                foreach (var d in docs.Where(d => !(ignorarCopias && SaveUtil.IsBackupFilePath(d.Nombre)) && TamanoAdmitido(d.Bytes))) candidatos.Add((d.UltimaModificacionMs, null, d, nombre));
            }
        }
        foreach (var c in candidatos.OrderByDescending(x => x.Ms))
        {
            cancelar.ThrowIfCancellationRequested();
            if (c.Ya is not null) { yield return c.Ya; continue; }
            if (await LeerAsync(c.Doc!, c.Carpeta, cancelar).ConfigureAwait(false) is { } g) yield return g;
        }
    }

    /// <summary>Una carpeta del sistema de archivos, con la MISMA llamada que PKHeX (<c>SaveFinder.GetSaveFiles</c> sin detección de unidades).</summary>
    private static IEnumerable<GuardadoListado> Locales(string dir, string nombreCarpeta, bool ignorarCopias, CancellationToken cancelar)
    {
        if (!Directory.Exists(dir)) yield break;
        foreach (var sav in SaveFinder.GetSaveFiles([], false, [dir], ignorarCopias, cancelar))
        {
            var ruta = sav.Metadata.FilePath!;
            yield return new GuardadoListado(sav, Path.GetFileName(ruta), nombreCarpeta, ruta, true, File.GetLastWriteTimeUtc(ruta));
        }
    }

    /// <summary>El nombre de una carpeta de SAF y sus documentos (sin leerlos). Vacío si no hay permiso, no existe o el proveedor falla: como una carpeta que no existe.</summary>
    private async Task<(string Carpeta, IReadOnlyList<DocumentoRef> Documentos)> DocumentosDeSafAsync(string uri, string? nombreCarpeta, CancellationToken cancelar)
    {
        if (carpetas is null || documentos is null) return (nombreCarpeta ?? string.Empty, []);
        try
        {
            var viva = await carpetas.ObtenerCarpetaAsync(uri, cancelar).ConfigureAwait(false);
            if (viva is null) return (nombreCarpeta ?? string.Empty, []);
            return (nombreCarpeta ?? viva.Nombre, await carpetas.ListarAsync(viva, cancelar).ConfigureAwait(false));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException) { return (nombreCarpeta ?? string.Empty, []); }
    }

    /// <summary>Una carpeta de SAF: los mismos filtros que <c>SaveUtil.GetSavesFromFolder</c> (copias fuera si se pide, tamaño de guardado) y la misma lectura que <c>SaveUtil.TryGetSaveFile(path)</c>.</summary>
    private async Task<List<GuardadoListado>> DeSafAsync(string uri, string? nombreCarpeta, bool ignorarCopias, CancellationToken cancelar)
    {
        var l = new List<GuardadoListado>();
        var (carpeta, docs) = await DocumentosDeSafAsync(uri, nombreCarpeta, cancelar).ConfigureAwait(false);
        foreach (var d in docs)
        {
            cancelar.ThrowIfCancellationRequested();
            if (ignorarCopias && SaveUtil.IsBackupFilePath(d.Nombre)) continue;
            if (!TamanoAdmitido(d.Bytes)) continue;
            if (await LeerAsync(d, carpeta, cancelar).ConfigureAwait(false) is { } g) l.Add(g);
        }
        return [.. l.OrderByDescending(g => g.FechaUtc)];
    }

    private async Task<GuardadoListado?> DesdeUriAsync(string uri, string carpeta, CancellationToken cancelar)
    {
        // Primero como documento (lo normal en Android: una URI de SAF con permiso); si el almacén no lo conoce y es una ruta (ajustes traídos del PC), como PKHeX: Where(File.Exists).Select(GetSaveFile).
        if (documentos is not null)
        {
            DocumentoRef? d = null;
            try { d = await documentos.ObtenerAsync(uri, cancelar).ConfigureAwait(false); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException) { }
            if (d is not null) return await LeerAsync(d, carpeta, cancelar).ConfigureAwait(false);
        }
        if (!EsLocal(uri) || !File.Exists(uri) || !SaveUtil.TryGetSaveFile(uri, out var sav)) return null;
        return new GuardadoListado(sav, Path.GetFileName(uri), NombreLocal(Path.GetDirectoryName(uri) ?? uri), uri, true, File.GetLastWriteTimeUtc(uri));
    }

    /// <summary>
    /// <c>SaveUtil.IsSizeValid(tamaño)</c> de <c>GetSavesFromFolder</c>. Un proveedor de SAF que no informa del tamaño (algunos de nube no devuelven <c>COLUMN_SIZE</c>) da un tamaño DESCONOCIDO (negativo), que no es un tamaño
    /// inválido: el documento se lee y el tamaño real se comprueba entonces (<see cref="LeerAsync"/>), en vez de volver invisible toda la carpeta (auditoría de Fable I5).
    /// </summary>
    public static bool TamanoAdmitido(long bytes) => bytes < 0 || SaveUtil.IsSizeValid(bytes);

    private async Task<GuardadoListado?> LeerAsync(DocumentoRef d, string carpeta, CancellationToken cancelar)
    {
        byte[] datos;
        try { datos = await documentos!.LeerAsync(d, cancelar).ConfigureAwait(false); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException) { return null; }
        if (!SaveUtil.IsSizeValid(datos.Length)) return null;   // el tamaño real, ahora que se conoce (el que declaró el proveedor pudo no existir)
        var sav = Reconocer(datos, d.Nombre);
        return sav is null ? null : new GuardadoListado(sav, d.Nombre, carpeta, d.Uri, false, d.UltimaModificacionMs <= 0 ? DateTime.MinValue : DateTimeOffset.FromUnixTimeMilliseconds(d.UltimaModificacionMs).UtcDateTime, d);
    }

    /// <summary><c>SaveUtil.TryGetSaveFile(path)</c> con los bytes ya leídos: detección, nombre del archivo en <c>Metadata</c> e idioma de Gen. 1-3 (<c>SaveLanguage.TryRevise</c>). Sobre una COPIA: el Core puede tocar el buffer.</summary>
    public static SaveFile? Reconocer(byte[] datos, string nombre)
    {
        try
        {
            if (!SaveUtil.TryGetSaveFile(datos.ToArray(), out var sav, nombre)) return null;
            sav.Metadata.SetExtraInfo(Path.GetFileName(nombre));
            if (sav.Generation <= 3) SaveLanguage.TryRevise(sav);
            return sav;
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { return null; }
    }

    // ------------------------------------------------------------------------------------------------ detección
    /// <summary>
    /// <c>SaveFinder.FindMostRecentSaveFile</c>: entre las «Otras rutas de copias» (sin <c>.bak</c>), el guardado con la fecha de escritura más reciente CUYAS SUMAS SON VÁLIDAS. Con el tope de 5 s de PKHeX; null si no hay
    /// ninguno o se agota el tiempo.
    /// </summary>
    public async Task<GuardadoListado?> MasRecienteAsync(CancellationToken cancelar = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancelar);
        cts.CancelAfter(TopeDeteccion);
        try
        {
            await foreach (var g in OtrasRutasPorFechaAsync(ignorarCopias: true, cts.Token).ConfigureAwait(false))
                if (g.Partida.ChecksumsValid) return g;   // se para en el primero válido: no se lee el resto
            return null;
        }
        catch (OperationCanceledException) when (!cancelar.IsCancellationRequested) { return null; }
    }

    /// <summary>
    /// «Detectar automáticamente la partida más reciente» (<c>Startup.TryDetectRecentSave</c>, <c>WinFormsUtil.SuggestInitialFileName</c>): con el ajuste activo, la ubicación de la partida más reciente para que el
    /// selector del sistema se abra ahí (Android: <c>EXTRA_INITIAL_URI</c>); con el ajuste apagado, o sin ninguna, null y el selector se abre donde quiera el sistema.
    /// </summary>
    public async Task<string?> SugerenciaAlAbrirAsync(CancellationToken cancelar = default)
        => A.Startup.TryDetectRecentSave ? (await MasRecienteAsync(cancelar).ConfigureAwait(false))?.Ubicacion : null;

    /// <summary>
    /// «Cargar partida automáticamente al iniciar» (<c>Startup.AutoLoadSaveOnStartup</c>, <c>StartupArguments.ReadSettingsAnyPKM</c>): <c>Disabled</c> = ninguna; <c>RecentBackup</c> = la más reciente de las
    /// «Otras rutas de copias» (<c>DetectSaveFiles(...).FirstOrDefault()</c>, sin mirar sumas, con el tope de 5 s); <c>LastLoaded</c> = la primera de los recientes que todavía existe y se reconoce.
    /// </summary>
    public async Task<GuardadoListado?> AlArrancarAsync(CancellationToken cancelar = default)
    {
        switch (A.Startup.AutoLoadSaveOnStartup)
        {
            case SaveFileLoadSetting.RecentBackup:
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancelar))
                {
                    cts.CancelAfter(TopeDeteccion);
                    try
                    {
                        await foreach (var g in OtrasRutasPorFechaAsync(ignorarCopias: true, cts.Token).ConfigureAwait(false)) return g;   // el primero que se reconoce, sin leer el resto
                        return null;
                    }
                    catch (OperationCanceledException) when (!cancelar.IsCancellationRequested) { return null; }
                }
            case SaveFileLoadSetting.LastLoaded:
                foreach (var u in A.Startup.RecentlyLoaded.ToList())
                    if (await DesdeUriAsync(u, CarpetaDeUri(u), cancelar).ConfigureAwait(false) is { } g) return g;
                return null;
            default:
                return null;
        }
    }

    // ------------------------------------------------------------------------------------------------ borrar
    /// <summary>
    /// «Borrar» (<c>ClickDeleteFile</c>): SOLO las copias de la carpeta de copias de la app (las que hace Pokekeep). Los archivos de las carpetas del usuario no se borran desde aquí (divergencia: el gestor de archivos
    /// del sistema es quien borra lo del usuario; ADR del bloque). Devuelve el mensaje de error de PKHeX o null si se borró.
    /// </summary>
    public string? Borrar(GuardadoListado g)
    {
        TextosNucleo.Preparar();
        if (!PuedeBorrar(g)) return IdiomaInterfaz.T("CarpetasGuardados.SoloPuedenBorrarDesde");
        try { File.Delete(g.Ubicacion); return null; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return $"{MessageStrings.MsgFileDeleteFail} {ex.Message}"; }
    }

    public bool PuedeBorrar(GuardadoListado g)
    {
        if (!g.EnLaApp) return false;
        var (dir, _) = CopiasPkhex.Carpeta(A);
        var raiz = Path.GetFullPath(dir) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(g.Ubicacion).StartsWith(raiz, StringComparison.Ordinal) && File.Exists(g.Ubicacion);
    }

    // ------------------------------------------------------------------------------------------------ abrir
    /// <summary>
    /// Abre un guardado listado como lo abre <c>OpenSAV</c>: un documento del usuario por la vía de siempre (<c>AbrirRecienteAsync</c>: copia privada, recientes y copia automática); un archivo del sistema de archivos
    /// (la carpeta de copias de la app, una ruta traída del PC) como sesión SIN documento, que solo se exporta como copia (PKHeX no sobrescribe una copia: <c>Metadata.IsBackup</c>). Null si ya no se puede leer o
    /// no se reconoce. Lo usan la «Lista de carpetas» y la carga al iniciar.
    /// </summary>
    public static async Task<Modelo.Sesion?> AbrirAsync(GuardadoListado g, IServiciosPartida? servicios, CancellationToken cancelar = default)
    {
        if (g.Documento is { } d)
            return servicios is null ? null : await servicios.AbrirRecienteAsync(new RecienteRef(d.Uri, d.Nombre, g.Juego, DateTimeOffset.UtcNow), cancelar).ConfigureAwait(false);
        if (!EsLocal(g.Ubicacion) || !File.Exists(g.Ubicacion)) return null;
        var datos = await File.ReadAllBytesAsync(g.Ubicacion, cancelar).ConfigureAwait(false);
        if (Reconocer(datos, g.Nombre) is not { } sav) return null;
        return servicios is null ? new Modelo.Sesion(sav, g.Nombre) : await servicios.AbrirGuardadoAsync(sav, datos, g.Nombre, cancelar).ConfigureAwait(false);
    }
}
