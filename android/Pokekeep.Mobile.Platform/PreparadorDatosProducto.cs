// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Security.Cryptography;
using System.Text.Json;

namespace Pokekeep.Mobile.Platform;

/// <summary>Único propietario de FilesDir/datos: solo materializa las bases que SQLite necesita abrir por ruta.</summary>
public sealed class PreparadorDatosProducto(string carpeta, Func<string, Stream> abrirAsset, PreparadorDatosProducto.Textos? textos = null)
{
    // El preparador es independiente de la interfaz; Android proporciona los textos del idioma actual.
    public sealed record Textos(Func<string, string> Preparando, Func<string, string> CopiaNoVerificada, Func<string> Listos);
    private readonly Textos _textos = textos ?? new(
        nombre => $"Preparando los datos de {nombre}…",
        nombre => $"La copia de {nombre} no coincide con el manifiesto.",
        () => "Datos listos");
    // La actividad puede recrearse mientras otro arranque prepara la misma carpeta del proceso.
    private static readonly SemaphoreSlim Preparacion = new(1, 1);
    public sealed record Entrada(string Nombre, long Bytes, string Sha256);

    // Lista cerrada y justificada: los paquetes de imágenes y audio se leen directamente del APK.
    public static IReadOnlyDictionary<string, string> DuplicacionesJustificadas { get; } =
        new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["autolegalidad.sqlite"] = "SQLite necesita un archivo para las consultas aleatorias del catálogo.",
            ["soluciones.sqlite"] = "SQLite necesita un archivo para las consultas aleatorias de soluciones."
        });

    public static IReadOnlyList<Entrada> Analizar(Stream manifiesto)
    {
        using var documento = JsonDocument.Parse(manifiesto);
        var entradas = documento.RootElement.GetProperty("ficheros").EnumerateArray()
            .Select(e => new Entrada(e.GetProperty("nombre").GetString()!, e.GetProperty("bytes").GetInt64(), e.GetProperty("sha256").GetString()!)).ToArray();
        Validar(entradas);
        return entradas;
    }

    private static void Validar(IReadOnlyList<Entrada> entradas)
    {
        var nombres = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entrada in entradas)
            if (entrada.Nombre is null || !DuplicacionesJustificadas.ContainsKey(entrada.Nombre) || !nombres.Add(entrada.Nombre) || entrada.Bytes < 0 ||
                entrada.Sha256 is not { Length: 64 } || !entrada.Sha256.All(char.IsAsciiHexDigit))
                throw new InvalidDataException("El manifiesto contiene un dato sin justificación de copia o una identidad inválida.");
    }

    /// <summary>Reemplaza con verificación antes de publicar y limpia los restos SOLO tras preparar todos los datos vigentes.</summary>
    public async Task<int> PrepararAsync(IReadOnlyList<Entrada> entradas, IProgress<(string Mensaje, double? Fraccion)>? progreso = null, CancellationToken ct = default)
    {
        Validar(entradas);
        await Preparacion.WaitAsync(ct);
        try { return await PrepararConExclusionAsync(entradas, progreso, ct); }
        finally { Preparacion.Release(); }
    }

    private async Task<int> PrepararConExclusionAsync(IReadOnlyList<Entrada> entradas, IProgress<(string Mensaje, double? Fraccion)>? progreso, CancellationToken ct)
    {
        Directory.CreateDirectory(carpeta);
        var copiados = 0;
        var total = entradas.Sum(e => e.Bytes);
        long hecho = 0;
        foreach (var entrada in entradas)
        {
            ct.ThrowIfCancellationRequested();
            var destino = Path.Combine(carpeta, entrada.Nombre);
            var marca = destino + ".sha256";
            if (File.Exists(destino) && new FileInfo(destino).Length == entrada.Bytes && File.Exists(marca) &&
                File.ReadAllText(marca).Trim().Equals(entrada.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                hecho += entrada.Bytes;
                continue;
            }
            var temporal = destino + ".tmp";
            try
            {
                using (var origen = abrirAsset(entrada.Nombre))
                using (var salida = new FileStream(temporal, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20))
                using (var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
                {
                    var buffer = new byte[1 << 20];
                    long copiado = 0;
                    int leidos;
                    while ((leidos = await origen.ReadAsync(buffer, ct)) > 0)
                    {
                        await salida.WriteAsync(buffer.AsMemory(0, leidos), ct);
                        sha.AppendData(buffer, 0, leidos);
                        copiado += leidos;
                        progreso?.Report((_textos.Preparando(entrada.Nombre), total == 0 ? null : (double)(hecho + copiado) / total));
                    }
                    if (copiado != entrada.Bytes || !Convert.ToHexString(sha.GetHashAndReset()).Equals(entrada.Sha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException(_textos.CopiaNoVerificada(entrada.Nombre));
                    await salida.FlushAsync(ct);
                }
                File.Move(temporal, destino, overwrite: true);
                File.WriteAllText(marca, entrada.Sha256);
                hecho += entrada.Bytes;
                copiados++;
            }
            finally { File.Delete(temporal); }
        }
        ct.ThrowIfCancellationRequested();
        // No hay partidas, ajustes ni copias del usuario aquí: residen fuera de esta carpeta exclusiva del producto.
        var vigentes = entradas.SelectMany(e => new[] { e.Nombre, e.Nombre + ".sha256" }).ToHashSet(StringComparer.Ordinal);
        foreach (var archivo in Directory.EnumerateFiles(carpeta))
            if (!vigentes.Contains(Path.GetFileName(archivo))) File.Delete(archivo);
        // Versiones antiguas en subcarpetas: tampoco deben sobrevivir a la actualización.
        foreach (var directorio in Directory.EnumerateDirectories(carpeta))
            Directory.Delete(directorio, recursive: (File.GetAttributes(directorio) & FileAttributes.ReparsePoint) == 0);
        progreso?.Report((_textos.Listos(), 1));
        return copiados;
    }
}
