// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.IO.Compression;
using System.Security.Cryptography;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Almacen;

/// <summary>
/// Extraccion y restauracion SEGURAS (Bugs-de-seguridad-reales-encontrados): un archivo de copias o un zip importado no puede escribir fuera de su carpeta destino (zip slip: «..\x», rutas absolutas, enlaces)
/// ni pasar de un tope de tamano, y cada fichero se comprueba por SHA-256 ANTES de tocar nada del usuario.
/// </summary>
public static class ZipSeguro
{
    public const long TopeTotal = 512L * 1024 * 1024;

    /// <summary>Extrae <paramref name="zip"/> en <paramref name="destino"/>; lanza <see cref="InvalidDataException"/> si alguna entrada se sale de la carpeta o el total es absurdo. Devuelve los ficheros extraidos.</summary>
    public static IReadOnlyList<string> Extraer(string zip, string destino)
    {
        var raiz = Path.GetFullPath(destino).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        using var a = ZipFile.OpenRead(zip);
        long total = 0;
        var salida = new List<string>();
        // Primero se VALIDAN todas las entradas; solo si todas son seguras se escribe algo.
        foreach (var e in a.Entries)
        {
            var ruta = Path.GetFullPath(Path.Combine(raiz, e.FullName.Replace('/', Path.DirectorySeparatorChar)));
            if (!ruta.StartsWith(raiz, StringComparison.OrdinalIgnoreCase) || Path.IsPathRooted(e.FullName) || e.FullName.Contains(':'))
                throw new InvalidDataException(IdiomaInterfaz.F("ZipSeguro.EntradaInseguraEnEl", e.FullName));
            total += e.Length;
            if (total > TopeTotal) throw new InvalidDataException(IdiomaInterfaz.T("ZipSeguro.ElArchivoDescomprimidoEs"));
        }
        foreach (var e in a.Entries)
        {
            if (e.FullName.EndsWith('/') || e.Name.Length == 0) continue;
            var ruta = Path.GetFullPath(Path.Combine(raiz, e.FullName.Replace('/', Path.DirectorySeparatorChar)));
            Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
            e.ExtractToFile(ruta, overwrite: false);
            salida.Add(ruta);
        }
        return salida;
    }

    /// <summary>Nombre de fichero simple (sin separadores, sin «..», sin rutas): lo unico que se acepta para elegir una copia de seguridad por nombre.</summary>
    public static bool NombreSimple(string nombre)
        => !string.IsNullOrWhiteSpace(nombre) && nombre == Path.GetFileName(nombre) && !nombre.Contains("..") && nombre.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

    public static string Sha256(byte[] datos) => Convert.ToHexString(SHA256.HashData(datos));
}
