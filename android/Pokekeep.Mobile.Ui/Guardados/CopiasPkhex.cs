// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Ajustes.Pkhex;
using Pokekeep.Mobile.Ui.Almacen;
using Pokekeep.Mobile.Ui.Modelo;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Guardados;

/// <summary>Qué pasó con la copia automática al cargar (<c>Main.TryBackupExportCheck</c>): se hizo, por qué no, o el error de PKHeX.</summary>
public enum EstadoCopiaAlCargar { Hecha, Desactivada, SinArchivo, NoExportable, SinCarpeta, YaExistia, Error }

public sealed record CopiaAlCargar(EstadoCopiaAlCargar Estado, string? Ruta, string? Mensaje = null);

/// <summary>Resultado de «Exportar copia (BAK)» (<c>SAVEditor.ExportBackup</c>).</summary>
public sealed record ExportacionBak(bool Hecha, string Mensaje, string? Destino);

/// <summary>
/// COPIAS DE SEGURIDAD DE PKHeX (bloque 10-4: PK-MENU-049, PK-IO-039, PK-BOX-029, PK-IO-034 y el ajuste <c>Backup.BAKEnabled</c>). Las mismas reglas y los mismos nombres que el escritorio:
/// <list type="bullet">
/// <item>Al cargar un guardado (<see cref="AlCargar"/>, el <c>TryBackupExportCheck</c> de <c>Main.OpenSAV</c>): con <c>Backup.BAKEnabled</c>, si el guardado es exportable y la carpeta de copias
/// (<c>LocalResources.GetBackupPath()</c>) existe, se copia el ARCHIVO DE ORIGEN tal cual (no el guardado en memoria) con el nombre <c>Metadata.GetBackupFileName</c> (<c>«nombre» [EO (versión) - tiempo].bak</c>),
/// y solo si esa copia no existe ya. Esas copias no rotan nunca (las de antes de guardar de la F7, sí).</item>
/// <item>«Exportar copia (BAK)» (<see cref="ExportarBakAsync"/>): copia el archivo de origen a donde elija el usuario con el nombre <c>BAKName</c>; si el original ya no está, el mensaje de PKHeX.</item>
/// </list>
/// En Android la carpeta de copias de fábrica es la de la app (<c>bak</c> junto a <c>ajustes-pkhex.json</c>, la misma de las copias de la F7): PKHeX la crea al responder «sí» al aviso del primer arranque
/// (<c>PromptBackup</c>, que en Android no aplica: PK-MENU-050), así que aquí se crea sola. Una ruta escrita a mano que no existe se respeta como en PKHeX: no hay copia.
/// </summary>
public static class CopiasPkhex
{
    /// <summary>El archivo de origen tal y como estaba al abrirlo (la copia privada de la sesión), o null si la sesión no viene de un documento.</summary>
    public static byte[]? Origen(Sesion s)
    {
        if (s.CarpetaPrivada is null) return null;
        var ruta = Path.Combine(s.CarpetaPrivada, "original.bin");
        try { return File.Exists(ruta) ? File.ReadAllBytes(ruta) : null; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }

    /// <summary>El nombre de la copia (<c>PathUtil.CleanFileName(Metadata.BAKName)</c>), con el nombre del archivo recalculado en <c>Metadata</c> (lo hace <c>OpenSAV</c> con <c>SetExtraInfo(path)</c>).</summary>
    public static string NombreBak(Sesion s)
    {
        AsegurarNombre(s);
        return PathUtil.CleanFileName(s.Guardado.Metadata.BAKName);
    }

    private static void AsegurarNombre(Sesion s)
    {
        // Se vuelve a calcular SIEMPRE (como OpenSAV con SetExtraInfo(path) justo antes de la copia): así el nombre ya quita «(1)» y « - Copy» con las «Otras extensiones» vigentes.
        var meta = s.Guardado.Metadata;
        var nombre = meta.FilePath ?? s.NombreArchivo;
        if (s.Guardado.State.Exportable && !string.IsNullOrWhiteSpace(nombre)) meta.SetExtraInfo(Path.GetFileName(nombre));
    }

    /// <summary>La carpeta de copias VIGENTE (<c>LocalResources.GetBackupPath()</c>, leída ahora) y si es la de fábrica de la app.</summary>
    public static (string Ruta, bool DeFabrica) Carpeta(AjustesPkhex a)
        => (a.LocalResources.GetBackupPath(), string.Equals(a.LocalResources.BackupPath, new LocalResourceSettings().BackupPath, StringComparison.Ordinal));

    /// <summary>
    /// <c>Main.TryBackupExportCheck(sav, path)</c> para una sesión recién abierta (la llama <see cref="RecientesStore.Anotar"/>, que es el final de <c>OpenSAV</c>). Nunca toca el documento del usuario: solo lee la
    /// copia privada del original y escribe en la carpeta de copias.
    /// </summary>
    public static CopiaAlCargar AlCargar(Sesion s, AjustesPkhex a)
    {
        if (s.CarpetaPrivada is null) return new(EstadoCopiaAlCargar.SinArchivo, null);   // «not actual save»: partida en blanco, hexadecimal pegado (sin archivo de origen)
        if (!a.Backup.BAKEnabled) return new(EstadoCopiaAlCargar.Desactivada, null);
        if (!s.Guardado.State.Exportable) return new(EstadoCopiaAlCargar.NoExportable, null);
        var (dir, fabrica) = Carpeta(a);
        if (fabrica && !Directory.Exists(dir))
        {
            try { Directory.CreateDirectory(dir); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return new(EstadoCopiaAlCargar.Error, null, Texto(ex)); }
        }
        if (!Directory.Exists(dir)) return new(EstadoCopiaAlCargar.SinCarpeta, null);
        ExtensionesPkhex.Aplicar(a);
        var nombre = NombreBak(s);
        var destino = Path.Combine(dir, nombre);
        if (File.Exists(destino)) return new(EstadoCopiaAlCargar.YaExistia, destino);
        if (Origen(s) is not { } bytes) return new(EstadoCopiaAlCargar.SinArchivo, null);
        try
        {
            File.WriteAllBytes(destino, bytes);   // PKHeX: File.Copy(src, backupName, true)
            return new(EstadoCopiaAlCargar.Hecha, destino);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return new(EstadoCopiaAlCargar.Error, null, Texto(ex)); }
    }

    private static string Texto(Exception ex)
    {
        TextosNucleo.Preparar();
        return $"{MessageStrings.MsgBackupUnable}. {ex.Message}";
    }

    /// <summary>
    /// «Exportar copia (BAK)» (<c>SAVEditor.ExportBackup</c>): el ARCHIVO DE ORIGEN, leído de nuevo del documento del usuario (si se movió o se borró, <c>MsgSaveBackupNotFound</c>), a un documento nuevo que elige el usuario
    /// con el nombre sugerido <c>BAKName</c>. No escribe nada más; null si el usuario cancela el selector.
    /// </summary>
    public static async Task<ExportacionBak?> ExportarBakAsync(Sesion s, IAlmacenDocumentos almacen, CancellationToken cancelar = default)
    {
        TextosNucleo.Preparar();
        if (!s.Guardado.State.Exportable || (s.Documento is null && Origen(s) is null)) return new(false, IdiomaInterfaz.T("CopiasPkhex.PartidaVieneArchivoArchivo"), null);
        // Con documento, el del usuario (leído de nuevo); sin él (copia de la app, carga forzada), la copia privada de lo que se abrió.
        Task<byte[]?> Leer() => s.Documento is { } doc ? LeerOrigenAsync(almacen, doc, cancelar) : Task.FromResult(Origen(s));
        var nombre = s.Documento?.Nombre ?? s.NombreArchivo;
        if (await Leer().ConfigureAwait(false) is null) return new(false, $"{MessageStrings.MsgSaveBackupNotFound} «{nombre}».", null);
        var destino = await almacen.ElegirDestinoAsync(NombreBak(s), cancelar).ConfigureAwait(false);
        if (destino is null) return null;
        var origen = await Leer().ConfigureAwait(false);   // ¿se movió mientras elegía? (PKHeX lo vuelve a mirar)
        if (origen is null) return new(false, $"{MessageStrings.MsgSaveBackupNotFound} «{nombre}».", null);
        await almacen.EscribirAsync(destino, origen, cancelar).ConfigureAwait(false);
        return new(true, $"{MessageStrings.MsgSaveBackup} «{destino.Nombre}».", destino.Nombre);
    }

    private static async Task<byte[]?> LeerOrigenAsync(IAlmacenDocumentos almacen, DocumentoRef doc, CancellationToken cancelar)
    {
        try
        {
            var actual = await almacen.ObtenerAsync(doc.Uri, cancelar).ConfigureAwait(false);
            return actual is null ? null : await almacen.LeerAsync(actual, cancelar).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException) { return null; }
    }
}

/// <summary>
/// «Otras extensiones de partidas» (<c>Backup.OtherSaveFileExtensions</c>): lo mismo que <c>WinFormsUtil.AddSaveFileExtensions</c> al recargar los ajustes, sobre la lista del Core
/// <c>SaveFileMetadata.CustomSaveExtensions</c> (la que usa para quitar «(1)» y « - Copy» del nombre del archivo, y con él el de las copias). Como en PKHeX, solo añade (sin repetir) y no quita; la fila de ajustes (<c>FilaPkhexExtensiones</c>) ya las guarda sin punto.
/// </summary>
public static class ExtensionesPkhex
{
    public static void Aplicar(AjustesPkhex a)
    {
        var destino = SaveFileMetadata.CustomSaveExtensions;
        lock (destino)
        {
            foreach (var e in a.Backup.OtherSaveFileExtensions)
                if (!string.IsNullOrWhiteSpace(e) && !destino.Contains(e)) destino.Add(e);   // tal cual, como PKHeX (la fila de ajustes ya las guarda sin punto)
        }
    }

    /// <summary>Una extensión como la escribe el usuario («.SRM », «srm») a como la guarda PKHeX: sin espacios ni punto delante.</summary>
    public static string Normalizar(string e) => e.Trim().TrimStart('.').Trim();
}
