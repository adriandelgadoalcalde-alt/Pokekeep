// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Design;

namespace Pokekeep.Mobile.Ui.Cajas;

/// <summary>Un archivo listo para la hoja de compartir: nombre, contenido y tipo.</summary>
public sealed record ArchivoParaCompartir(string Nombre, byte[] Datos, string TipoMime);

/// <summary>
/// Compartir un Pokémon por la hoja del sistema (bloque 10-9, PK-MENU-038 y PK-IO-040): el equivalente móvil de arrastrar el sprite del editor a otra carpeta (<c>Main.Dragout_MouseDown</c> de PKHeX), que crea un
/// archivo temporal con el Pokémon (<c>FileUtil.GetPKMTempFileName</c>; descifrado, o cifrado <c>.ek*</c> con Ctrl) y lo borra a los 20 s. Aquí el contenido y el nombre son EXACTAMENTE los de PKHeX
/// (<see cref="Intercambio.Bytes"/> y <see cref="Intercambio.NombreArchivo"/>); lo único que cambia es el tiempo de vida del archivo: la hoja del sistema espera a que el usuario elija a quién mandarlo, así
/// que vive <see cref="VidaArchivo"/> (y se limpia al arrancar y en cada envío) en vez de 20 s. Sin red: lo manda el sistema solo si el usuario elige una app (reglas 13 y 15).
/// </summary>
public static class ModeloCompartir
{
    /// <summary>Cuánto vive el archivo temporal de «Compartir» (PKHeX: 20 s para soltar el sprite; la hoja del sistema necesita más).</summary>
    public static readonly TimeSpan VidaArchivo = TimeSpan.FromMinutes(10);

    public const string TipoPokemon = "application/octet-stream";

    /// <summary>El Pokémon como lo arrastra PKHeX: <c>.pk*</c> descifrado o, con <paramref name="cifrado"/>, <c>.ek*</c> (en Gen. 1-2 no hay versión cifrada: se manda el descifrado).</summary>
    public static ArchivoParaCompartir Pokemon(PKM pk, bool cifrado)
    {
        var cifra = cifrado && pk.Format >= 3;
        return new ArchivoParaCompartir(Intercambio.NombreArchivo(pk, cifra), Intercambio.Bytes(pk, cifra), TipoPokemon);
    }

    /// <summary>La imagen del QR del Pokémon (la que enseña la pantalla, sin el sprite del centro) como PNG.</summary>
    public static ArchivoParaCompartir ImagenQr(PKM pk, string mensaje)
        => new(PathUtil.CleanFileName($"{pk.FileNameWithoutExtension}.qr.png"), QrPng.Crear(mensaje), "image/png");

    /// <summary>Escribe el archivo en <paramref name="carpeta"/> (se crea si no existe) y devuelve su ruta. Antes limpia los que ya pasaron de <see cref="VidaArchivo"/>.</summary>
    public static string EscribirTemporal(string carpeta, ArchivoParaCompartir archivo, DateTime? ahora = null)
    {
        Directory.CreateDirectory(carpeta);
        LimpiarCaducados(carpeta, ahora);
        var ruta = Path.Combine(carpeta, PathUtil.CleanFileName(archivo.Nombre));
        File.WriteAllBytes(ruta, archivo.Datos);
        File.SetLastWriteTimeUtc(ruta, (ahora ?? DateTime.UtcNow));
        return ruta;
    }

    /// <summary>Borra de la carpeta lo que lleva más de <see cref="VidaArchivo"/>; devuelve cuántos. No toca subcarpetas y nunca falla por un archivo en uso.</summary>
    public static int LimpiarCaducados(string carpeta, DateTime? ahora = null)
    {
        if (!Directory.Exists(carpeta)) return 0;
        var limite = (ahora ?? DateTime.UtcNow) - VidaArchivo;
        var n = 0;
        foreach (var f in Directory.EnumerateFiles(carpeta))
        {
            try { if (File.GetLastWriteTimeUtc(f) <= limite) { File.Delete(f); n++; } }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* en uso: se borrará la próxima vez */ }
        }
        return n;
    }

    /// <summary>Borra el archivo pasado <paramref name="espera"/> (<c>DeleteAsync</c> de PKHeX). Devuelve true si lo borró.</summary>
    public static async Task<bool> BorrarTrasAsync(string ruta, TimeSpan espera, CancellationToken cancelar = default)
    {
        try { await Task.Delay(espera, cancelar).ConfigureAwait(false); }
        catch (OperationCanceledException) { return false; }
        if (!File.Exists(ruta)) return false;
        try { File.Delete(ruta); return true; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }
}
