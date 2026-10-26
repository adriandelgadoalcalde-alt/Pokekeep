// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Collections.Concurrent;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Ajustes.Pkhex;
using Pokekeep.Mobile.Ui.Almacen;
using Pokekeep.Mobile.Ui.Guardados;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Herramientas.BaseDatosPkm;

/// <summary>Las partidas externas que se han podido leer y lo que no (para decirlo en pantalla: un permiso retirado o una carpeta que ya no existe no rompen la carga).</summary>
public sealed record PartidasExternas(IReadOnlyList<SaveFile> Partidas, IReadOnlyList<string> Avisos);

/// <summary>
/// De dónde saca la base de datos de PKM las partidas que no son la abierta (el <c>otherPaths</c> de <c>SAV_Database.LoadDatabase</c> y <c>LoadEntitiesFromFolder</c>), con los tres ajustes <c>EntityDb</c> que
/// mandan en ello, leídos en el momento de cargar:
/// <list type="bullet">
/// <item><c>SearchExtraSaves</c>: cada una de las «Otras rutas de copias» (<c>Backup.OtherBackupPaths</c>) que existe, sin las copias <c>.bak</c> (<c>ignoreBackups</c>). En Android son carpetas que el usuario eligió con
/// el selector del sistema (URI de árbol de SAF, que se leen con <see cref="IAlmacenCarpetas"/>) o, si vienen de un PC, rutas del sistema de archivos que se leen como PKHeX.</item>
/// <item><c>SearchBackups</c>: la carpeta de copias de PKHeX (<c>LocalResources.GetBackupPath()</c>, en la carpeta privada de la app) CON sus <c>.bak</c>.</item>
/// <item><c>SearchExtraSavesDeep</c>: con subcarpetas (<c>SearchOption.AllDirectories</c>) o solo el nivel de arriba. Como PKHeX, vale para las dos clases de carpeta.</item>
/// </list>
/// Lo mismo que <c>SaveUtil.GetSavesFromFolder</c> + <c>TryAddPKMsFromSaveFilePath</c> (guardados y tarjetas de memoria de GameCube); para un documento de SAF la lectura es la de <c>ModeloCarpetas.Reconocer</c> (bloque 10-4).
/// Solo LEE: ninguna partida externa se modifica ni se guarda.
/// </summary>
public sealed class FuentesExternas(IAjustesPkhex ajustes, IAlmacenDocumentos? documentos = null, IAlmacenCarpetas? carpetas = null)
{
    private AjustesPkhex A => ajustes.Actual;

    public async Task<PartidasExternas> CargarAsync(CancellationToken cancelar = default)
    {
        TextosNucleo.Preparar();
        var a = A;
        var partidas = new List<SaveFile>();
        var avisos = new List<string>();
        var profundo = a.EntityDb.SearchExtraSavesDeep;

        if (a.EntityDb.SearchExtraSaves)
        {
            foreach (var ruta in a.Backup.OtherBackupPaths.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.Ordinal))
            {
                cancelar.ThrowIfCancellationRequested();
                if (ModeloCarpetas.EsLocal(ruta))
                {
                    if (Directory.Exists(ruta)) partidas.AddRange(await Task.Run(() => DeCarpetaLocal(ruta, profundo, ignorarCopias: true, cancelar), cancelar).ConfigureAwait(false));
                }
                else
                {
                    var (lista, aviso) = await DeSafAsync(ruta, profundo, cancelar).ConfigureAwait(false);
                    partidas.AddRange(lista);
                    if (aviso is not null) avisos.Add(aviso);
                }
            }
        }
        if (a.EntityDb.SearchBackups)
        {
            var copias = CopiasPkhex.Carpeta(a).Ruta;
            if (Directory.Exists(copias)) partidas.AddRange(await Task.Run(() => DeCarpetaLocal(copias, profundo, ignorarCopias: false, cancelar), cancelar).ConfigureAwait(false));
        }
        return new(partidas, avisos);
    }

    // ---------------------------------------------------------------------------------------------------------------- carpetas del sistema de archivos (lo que hace PKHeX)
    /// <summary><c>SaveUtil.GetSavesFromFolder</c> y, por cada archivo, <c>TryAddPKMsFromSaveFilePath</c> (Parallel.ForEach como el formulario).</summary>
    public static IReadOnlyList<SaveFile> DeCarpetaLocal(string carpeta, bool profundo, bool ignorarCopias, CancellationToken cancelar)
    {
        var bolsa = new ConcurrentBag<SaveFile>();
        if (!SaveUtil.GetSavesFromFolder(carpeta, profundo, cancelar, out var rutas, ignorarCopias)) return [];
        Parallel.ForEach(rutas, new ParallelOptions { CancellationToken = cancelar }, archivo => AnadirDeArchivo(bolsa, archivo));
        return [.. bolsa];
    }

    private static void AnadirDeArchivo(ConcurrentBag<SaveFile> bolsa, string archivo)
    {
        if (SaveUtil.TryGetSaveFile(archivo, out var sav))
        {
            bolsa.Add(sav);
            return;
        }
        if (FileUtil.TryGetMemoryCard(archivo, out var tarjeta))
            AnadirDeTarjeta(bolsa, tarjeta, archivo);
        else
            System.Diagnostics.Debug.WriteLine($"Unable to load SaveFile: {archivo}");
    }

    /// <summary><c>TryAddPKMsFromMemoryCard</c>: una tarjeta de memoria de GameCube puede traer Colosseum, XD y la Caja de Rubí y Zafiro; cada una es una partida.</summary>
    private static void AnadirDeTarjeta(ConcurrentBag<SaveFile> bolsa, SAV3GCMemoryCard mc, string archivo)
    {
        var estado = mc.GetMemoryCardState();
        if (estado == MemoryCardSaveStatus.Invalid)
            return;

        if (mc.HasCOLO) Probar(bolsa, mc, archivo, SaveFileType.Colosseum);
        if (mc.HasXD) Probar(bolsa, mc, archivo, SaveFileType.XD);
        if (mc.HasRSBOX) Probar(bolsa, mc, archivo, SaveFileType.RSBox);

        static void Probar(ConcurrentBag<SaveFile> bolsa, SAV3GCMemoryCard mc, string ruta, SaveFileType juego)
        {
            mc.SelectSaveGame(juego);
            if (!SaveUtil.TryGetSaveFile(mc, out var sav))
                return;
            sav.Metadata.SetExtraInfo(ruta);
            bolsa.Add(sav);
        }
    }

    // ---------------------------------------------------------------------------------------------------------------- carpetas de SAF
    private async Task<(IReadOnlyList<SaveFile> Partidas, string? Aviso)> DeSafAsync(string uri, bool profundo, CancellationToken cancelar)
    {
        if (carpetas is null || documentos is null) return ([], null);
        CarpetaRef? carpeta;
        IReadOnlyList<DocumentoRef> docs;
        try
        {
            carpeta = await carpetas.ObtenerCarpetaAsync(uri, cancelar).ConfigureAwait(false);
            // Como Directory.Exists en PKHeX: una carpeta que ya no existe (o sin permiso) se salta, pero aquí se avisa.
            if (carpeta is null) return ([], IdiomaInterfaz.F("FuentesExternas.PodidoLeerMovioBorro", ModeloCarpetas.NombreDeUri(uri)));
            docs = await carpetas.ListarAsync(carpeta, profundo, cancelar).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException) { return ([], IdiomaInterfaz.F("FuentesExternas.PodidoLeer", ModeloCarpetas.NombreDeUri(uri))); }

        var l = new List<SaveFile>();
        foreach (var d in docs)
        {
            cancelar.ThrowIfCancellationRequested();
            if (SaveUtil.IsBackupFilePath(d.Nombre)) continue;   // ignoreBackups: true
            if (!SaveUtil.IsSizeValid(d.Bytes)) continue;
            byte[] datos;
            try { datos = await documentos.LeerAsync(d, cancelar).ConfigureAwait(false); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException) { continue; }
            if (ModeloCarpetas.Reconocer(datos, d.Nombre) is { } sav) l.Add(sav);
            else if (FileUtil.TryGetMemoryCard(datos.ToArray(), out var tarjeta))
            {
                var bolsa = new ConcurrentBag<SaveFile>();
                AnadirDeTarjeta(bolsa, tarjeta, d.Nombre);
                l.AddRange(bolsa);
            }
        }
        return (l, null);
    }
}
