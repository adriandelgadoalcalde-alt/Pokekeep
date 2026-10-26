// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.IO.Compression;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Almacen;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Abrir;

public enum TipoEnvoltorio { Directo, Zip, TarjetaGC }

/// <summary>
/// En qué contenedor venía la partida, para escribirla igual (PK-IO-007, PK-IO-028): directa (los bytes del Core), dentro de un .zip (<c>ZipReader</c> del Core al leer y <c>ZipReader.Update</c> al exportar a
/// un .zip, como <c>WinFormsUtil.ExportSAVInternal</c>) o dentro de una tarjeta de memoria de GameCube con la partida elegida (el Core ya devuelve la tarjeta entera en <c>Write()</c>).
/// Se guarda con la sesión (<see cref="Clave"/>) para que «Recuperar» la reabra igual.
/// </summary>
public sealed record EnvoltorioPartida(TipoEnvoltorio Tipo, SaveFileType JuegoTarjeta = SaveFileType.None)
{
    public static EnvoltorioPartida Directo { get; } = new(TipoEnvoltorio.Directo);
    public static EnvoltorioPartida Zip { get; } = new(TipoEnvoltorio.Zip);
    public static EnvoltorioPartida TarjetaGC(SaveFileType juego) => new(TipoEnvoltorio.TarjetaGC, juego);

    /// <summary>Texto para la tarjeta de partida y los avisos («dentro de un .zip», «en una tarjeta de memoria de GameCube»), o null si es directa.</summary>
    public string? Descripcion => Tipo switch
    {
        TipoEnvoltorio.Zip => IdiomaInterfaz.T("EnvoltorioPartida.DentroZip"),
        TipoEnvoltorio.TarjetaGC => IdiomaInterfaz.F("EnvoltorioPartida.TarjetaMemoriaGameCube", ReconocedorArchivos.NombreJuegoTarjeta(JuegoTarjeta)),
        _ => null,
    };

    /// <summary>Clave estable para <c>sesion.json</c> («directo», «zip», «gc:Colosseum»).</summary>
    public string Clave => Tipo switch { TipoEnvoltorio.Zip => "zip", TipoEnvoltorio.TarjetaGC => "gc:" + JuegoTarjeta, _ => "directo" };

    public static EnvoltorioPartida DeClave(string? clave) => clave switch
    {
        "zip" => Zip,
        { } c when c.StartsWith("gc:", StringComparison.Ordinal) && Enum.TryParse<SaveFileType>(c[3..], out var j) => TarjetaGC(j),
        _ => Directo,
    };

    /// <summary>Lee la partida del contenedor (lo mismo que hizo el reconocedor), o null si ya no es una partida. Siempre sobre una COPIA de los bytes.</summary>
    /// <summary>
    /// Deja el nombre del archivo en <c>Metadata</c> como <see cref="AperturaGuardado.Abrir"/> (y como <c>Main.OpenSAV</c> con la ruta del archivo abierto, sea el .zip o la tarjeta): el Core distingue
    /// por él los guardados de consola virtual (<c>IsVirtualConsole</c>). Solo el nombre, nunca una ruta.
    /// </summary>
    public static SaveFile ConNombre(SaveFile sav, string nombre)
    {
        if (sav.State.Exportable && !string.IsNullOrWhiteSpace(nombre)) sav.Metadata.SetExtraInfo(Path.GetFileName(nombre));
        return sav;
    }

    public SaveFile? Desempaquetar(byte[] datos, string nombre)
    {
        try
        {
            switch (Tipo)
            {
                case TipoEnvoltorio.TarjetaGC:
                    if (!FileUtil.TryGetMemoryCard(datos.ToArray(), out var tarjeta)) return null;
                    tarjeta.GetMemoryCardState();
                    tarjeta.SelectSaveGame(JuegoTarjeta);
                    if (tarjeta.SelectedGameVersion != JuegoTarjeta) return null;
                    return SaveUtil.TryGetSaveFile(tarjeta, out var deTarjeta) ? ConNombre(deTarjeta, nombre) : null;
                case TipoEnvoltorio.Zip:
                    return ZipReader.TryRead(datos.ToArray(), out var deZip) ? ConNombre(deZip, nombre) : null;
                default:
                    return AperturaGuardado.Abrir(datos.ToArray(), nombre);
            }
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or ArgumentException or InvalidOperationException or IndexOutOfRangeException) { return null; }
    }

    /// <summary>
    /// Los bytes que se escriben en un documento llamado <paramref name="nombreDestino"/>: si la partida venía en un .zip y se escribe en el MISMO documento («Guardar», <paramref name="mismoContenedor"/>) o en
    /// otro que se llama .zip («Exportar copia…»), el .zip <paramref name="contenedorOriginal"/> con su entrada de partida actualizada (<c>ZipReader.Update</c>, el resto de entradas intacto); en cualquier otro
    /// caso, lo que exporta el Core (<c>Write()</c>; para la tarjeta de GameCube, la tarjeta entera). «Guardar» sigue al CONTENIDO, como al abrir (el .zip se reconoce por su cabecera): un .zip que no se llama
    /// .zip vuelve a ser un .zip. PKHeX (<c>ExportSAVInternal</c>) mira la extensión del origen y del destino; aquí el documento original no se puede renombrar (divergencia del ADR 0073).
    /// </summary>
    public byte[] Empaquetar(SaveFile sav, ReadOnlySpan<byte> contenedorOriginal, string nombreDestino, bool mismoContenedor = false)
        => Empaquetar(sav.Write().ToArray(), contenedorOriginal, nombreDestino, mismoContenedor);

    /// <summary>
    /// Lo mismo con los bytes de la partida ya exportados (bloque 10-4: «Exportar copia…» los saca con las banderas de <c>GetSuggestedFlags</c>, <see cref="GestorSesiones.Exportar"/>, y después los envuelve,
    /// como <c>WinFormsUtil.ExportSAV</c> → <c>ExportSAVInternal</c>).
    /// </summary>
    public byte[] Empaquetar(byte[] exportado, ReadOnlySpan<byte> contenedorOriginal, string nombreDestino, bool mismoContenedor = false)
    {
        if (Tipo != TipoEnvoltorio.Zip || contenedorOriginal.IsEmpty || !(mismoContenedor || Path.GetExtension(nombreDestino).Equals(".zip", StringComparison.OrdinalIgnoreCase))) return exportado;
        using var ms = new MemoryStream();
        ms.Write(contenedorOriginal);
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Update, leaveOpen: true))
        {
            if (!ZipReader.Update(zip, exportado)) throw new InvalidDataException(IdiomaInterfaz.T("EnvoltorioPartida.ElZipOriginalYa"));
        }
        return ms.ToArray();
    }
}
