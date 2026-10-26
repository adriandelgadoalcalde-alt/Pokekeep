// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Almacen;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Cajas;

/// <summary>Un archivo leído de la carpeta elegida: su nombre (la extensión ayuda al Core a reconocerlo) y sus bytes.</summary>
public sealed record ArchivoLeido(string Nombre, byte[] Datos);

/// <summary>
/// Qué hacer con los Pokémon al ponerlos en la partida: las tres respuestas de la pregunta de PKHeX (<c>SAVEditor.GetImportSettingsOverride</c>): «Sí» = modificarlos (<see cref="EntityImportSettings.All"/>),
/// «No» = dejarlos como están (<see cref="EntityImportSettings.None"/>) y «Cancelar» = usar los ajustes de «Escritura de ranuras» (<c>default</c>, lo que dice <c>SaveFile.SetUpdateSettings</c>).
/// </summary>
public enum AdaptarAlCargar { SegunAjustes, Modificar, NoModificar }

/// <summary>El resultado de «Cargar cajas»: cuántos se cargaron (o -1 como el Core), el mensaje del Core y cuántos archivos se leyeron y se descartaron.</summary>
public sealed record ResultadoCarga(int Cargados, string Mensaje, int Archivos, int NoReconocidos);

/// <summary>
/// «Cargar cajas» de PKHeX (<c>Main.MainMenuBoxLoad</c> → <c>SAVEditor.LoadBoxes</c> → <c>SaveFile.LoadBoxes(path, …)</c>; PK-MENU-014, PK-IO-019, PK-IO-020) sin rutas: <see cref="LeerCarpetaAsync"/> lee los
/// archivos del primer nivel de una carpeta del usuario (SAF) y <see cref="Pokemon"/> los convierte EXACTAMENTE como <c>BoxUtil.GetPossiblePKMsFromPaths</c> (tamaño, <c>FileUtil.GetSupportedFile</c> con la partida
/// como referencia, regalos con Pokémon, encuentros, grupos y listas). <see cref="Cargar"/> llama al MISMO <c>SaveFile.LoadBoxes(pks, …)</c> del Core; la interfaz lo envuelve en un lote del historial
/// (Deshacer exacto de las casillas). Nada se escribe en la carpeta.
/// </summary>
public static class CargaCajas
{
    /// <summary>Tope de lectura de un archivo, también cuando el proveedor no dice su tamaño: <c>FileUtil.IsFileTooBig</c> no deja pasar de 16 MB (salvo guardados y tarjetas de memoria, que aquí no se cargan como Pokémon).</summary>
    public const long TopeArchivo = 0x100_0000;   // 16 MB: lo que el Core acepta de un Pokémon suelto (IsFileTooBig)

    /// <summary>
    /// ¿Esta forma de colocar toca la Pokédex o los contadores de la partida? «Modificar» usa <see cref="EntityImportSettings.All"/> y «Usar los ajustes» lo que diga <c>SaveFile.SetUpdateSettings</c>. Eso NO lo
    /// revierte el historial de casillas (<c>ReversionLote</c> guarda casillas, no la Pokédex): el texto de «Deshacer» tiene que decirlo.
    /// </summary>
    public static bool ActualizaPokedexOContadores(AdaptarAlCargar a) => a switch
    {
        AdaptarAlCargar.Modificar => true,
        AdaptarAlCargar.NoModificar => false,
        _ => SaveFile.SetUpdateSettings.UpdatePokeDex == EntityImportOption.Enable || SaveFile.SetUpdateSettings.UpdateRecord == EntityImportOption.Enable,
    };

    /// <summary>La frase de «Deshacer» que dice la verdad: las casillas vuelven siempre; la Pokédex y los contadores, solo si no se tocaron.</summary>
    public static string AvisoDeshacer(AdaptarAlCargar a) => ActualizaPokedexOContadores(a)
        ? IdiomaInterfaz.T("CargaCajas.PuedesDeshacerCasillasDeshacer")
        : IdiomaInterfaz.T("CargaCajas.PuedesDeshacerlo");

    /// <summary>El pie de las hojas que preguntan cómo colocar los Pokémon (todavía no se sabe la respuesta).</summary>
    public static string PieDeshacer => IdiomaInterfaz.T("CargaCajas.PieDeshacer");

    public static EntityImportSettings Ajustes(AdaptarAlCargar a) => a switch
    {
        AdaptarAlCargar.Modificar => EntityImportSettings.All,
        AdaptarAlCargar.NoModificar => EntityImportSettings.None,
        _ => default,
    };

    /// <summary>
    /// Los archivos de la carpeta, en orden de nombre sin distinguir mayúsculas (el orden en que Windows enumera una carpeta NTFS, que es el que ve PKHeX; el de un proveedor de SAF no está definido).
    /// Los que el proveedor dice que son demasiado grandes o pequeños no se leen (<c>IsFileTooBig</c>/<c>IsFileTooSmall</c> sobre <c>FileInfo.Length</c>, como PKHeX).
    /// </summary>
    public static async Task<IReadOnlyList<ArchivoLeido>> LeerCarpetaAsync(CarpetaRef carpeta, IAlmacenCarpetasCajas carpetas, IAlmacenDocumentos documentos, CancellationToken cancelar = default, IProgress<int>? leidos = null)
    {
        var archivos = await carpetas.ArchivosAsync(carpeta, cancelar).ConfigureAwait(false);
        return await LeerArchivosAsync(archivos, documentos, cancelar, leidos).ConfigureAwait(false);
    }

    /// <summary>Lee un listado del proveedor con los mismos límites y cancelación; el editor por lotes proporciona el árbol completo.</summary>
    public static async Task<IReadOnlyList<ArchivoLeido>> LeerArchivosAsync(IReadOnlyList<DocumentoRef> archivos, IAlmacenDocumentos documentos, CancellationToken cancelar = default, IProgress<int>? leidos = null)
    {
        var lista = archivos.OrderBy(d => d.Nombre, StringComparer.OrdinalIgnoreCase).ToList();
        var salida = new List<ArchivoLeido>(lista.Count);
        foreach (var d in lista)
        {
            cancelar.ThrowIfCancellationRequested();
            if (d.Bytes >= 0 && (FileUtil.IsFileTooBig(d.Bytes) || FileUtil.IsFileTooSmall(d.Bytes))) continue;
            byte[]? datos;
            try { datos = await documentos.LeerHastaAsync(d, TopeArchivo, cancelar).ConfigureAwait(false); }
            catch (OperationCanceledException) { throw; }
            // Como PKHeX («fail safely»): un archivo que no se puede leer se salta, sea cual sea la excepción. En Android el proveedor de SAF lanza excepciones de Java (FileNotFoundException,
            // SecurityException, IllegalArgumentException...) que no derivan de las de System.IO.
            catch (Exception) when (!cancelar.IsCancellationRequested) { continue; }
            if (datos is null) continue;
            salida.Add(new ArchivoLeido(d.Nombre, datos));
            leidos?.Report(salida.Count);
        }
        return salida;
    }

    /// <summary>Los Pokémon que el Core saca de esos archivos, en orden (<c>BoxUtil.GetPossiblePKMsFromPaths</c>). <paramref name="noReconocidos"/>: archivos que no aportan ninguno.</summary>
    public static List<PKM> Pokemon(IEnumerable<ArchivoLeido> archivos, SaveFile sav, out int noReconocidos)
    {
        var l = new List<PKM>();
        noReconocidos = 0;
        foreach (var a in archivos)
        {
            var antes = l.Count;
            if (!FileUtil.IsFileTooBig(a.Datos.Length) && !FileUtil.IsFileTooSmall(a.Datos.Length))
            {
                object? obj;
                try { obj = FileUtil.GetSupportedFile(a.Datos.ToArray(), Path.GetExtension(a.Nombre.AsSpan()), sav); }   // copia: el Core descifra PCD/PGT en el sitio
                catch { obj = null; }
                switch (obj)
                {
                    case PKM pk: l.Add(pk); break;
                    case MysteryGift { IsEntity: true } g: l.Add(g.ConvertToPKM(sav)); break;
                    case IEncounterInfo e when e.Species != 0: l.Add(e.ConvertToPKM(sav)); break;
                    case IPokeGroup g: l.AddRange(g.Contents); break;
                    case IEnumerable<PKM> contenido: l.AddRange(contenido); break;
                }
            }
            if (l.Count == antes) noReconocidos++;
        }
        return l;
    }

    /// <summary>
    /// Pone los Pokémon en las cajas desde <paramref name="cajaInicio"/> (<c>SaveFile.LoadBoxes(pks, out result, Box.CurrentBox, clearAll, overwrite, settings)</c>). <paramref name="vaciar"/> = «¿Vaciar las cajas
    /// posteriores…?» (<c>ClearBoxes(cajaInicio)</c>); PKHeX nunca pide <c>overwrite</c> (<c>GetBulkImportSettings</c> lo deja en false): solo se rellenan huecos vacíos.
    /// </summary>
    public static int Cargar(SaveFile sav, IEnumerable<PKM> pokemon, int cajaInicio, bool vaciar, AdaptarAlCargar adaptar, out string mensaje)
        => sav.LoadBoxes(pokemon, out mensaje, cajaInicio, vaciar, overwrite: false, Ajustes(adaptar));

    /// <summary>Las casillas que «Cargar cajas» puede tocar: todas las de caja desde <paramref name="cajaInicio"/> (para el lote del historial).</summary>
    public static IReadOnlyList<ISlotInfo> CasillasDesde(SaveFile sav, int cajaInicio)
    {
        var l = new List<ISlotInfo>();
        for (var b = cajaInicio; b < sav.BoxCount; b++) for (var s = 0; s < sav.BoxSlotCount; s++) l.Add(new SlotInfoBox(b, s, sav));
        return l;
    }
}
