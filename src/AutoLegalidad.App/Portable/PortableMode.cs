// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AutoLegalidad.Ui.Portable;

/// <summary>Un fichero del paquete portable con su huella.</summary>
public sealed record PortableFile(
    [property: JsonPropertyName("ruta")] string Path,
    [property: JsonPropertyName("bytes")] long Bytes,
    [property: JsonPropertyName("sha256")] string Sha256);

/// <summary>Identidad del PKHeX.Core que el paquete lleva (UNO solo, compartido por la interfaz y el Worker).</summary>
public sealed record PortableCore(
    [property: JsonPropertyName("archivo")] string File,
    [property: JsonPropertyName("sha256")] string Sha256);

/// <summary>Pareja de versiones del paquete (informativa; la comprobación fuerte es el hash del Core).</summary>
public sealed record PortablePair(
    [property: JsonPropertyName("pkhex")] string PkhexTag,
    [property: JsonPropertyName("pkhexCommit")] string PkhexCommit,
    [property: JsonPropertyName("alm")] string AlmTag,
    [property: JsonPropertyName("almCommit")] string AlmCommit,
    [property: JsonPropertyName("motor")] string Engine);

/// <summary>Contenido de <c>Pokekeep\manifiesto.json</c>: lo escribe <c>herramientas\construir-portable.ps1</c> y NO cambia después de empaquetar.</summary>
public sealed record PortableManifest(
    [property: JsonPropertyName("producto")] string Product,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("generado")] string GeneratedUtc,
    [property: JsonPropertyName("pareja")] PortablePair Pair,
    [property: JsonPropertyName("core")] PortableCore Core,
    [property: JsonPropertyName("archivos")] IReadOnlyList<PortableFile> Files);

/// <summary>
/// Modo PORTABLE (R-008, Hito 6, ADR 0036). Se activa solo si junto al ejecutable existe <c>Pokekeep\manifiesto.json</c> (lo trae el paquete; un entorno de desarrollo no lo tiene).
/// En modo portable: (1) el Worker, la base de datos y la caché de soluciones se buscan SOLO relativos a la carpeta del ejecutable (ni ajustes con rutas, ni variables de entorno,
/// ni subir por el árbol de desarrollo); (2) al arrancar se comprueba que el único <c>PKHeX.Core.dll</c> del paquete es el del manifiesto, y el Worker debe usar exactamente ese mismo binario;
/// (3) no se hace ninguna conexión de red por iniciativa propia (la comprobación de versiones de PKHeX queda desactivada, ver el parche de WinForms).
/// </summary>
public static class PortableMode
{
    public const string ManifestRelativePath = "Pokekeep/manifiesto.json";
    public const string ManifestFileName = "manifiesto.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };
    private static readonly Lock Gate = new();
    private static string? _checkedDir;
    private static string? _problem;

    /// <summary>
    /// Carpeta del programa sobre la que se decide todo lo demás (Android F1: ya no se lee <c>AppContext.BaseDirectory</c> por todas partes; quien hospeda la fija una vez). Por defecto, la carpeta del ejecutable.
    /// </summary>
    public static string Root { get; set; } = AppContext.BaseDirectory;

    /// <summary>True si la carpeta del ejecutable (<see cref="Root"/>) es un paquete portable de Pokekeep.</summary>
    public static bool IsActive => IsActiveAt(Root);

    public static bool IsActiveAt(string baseDir) => File.Exists(Path.Combine(baseDir, "Pokekeep", ManifestFileName));

    public static PortableManifest? Load(string baseDir)
    {
        try
        {
            var path = Path.Combine(baseDir, "Pokekeep", ManifestFileName);
            return File.Exists(path) ? JsonSerializer.Deserialize<PortableManifest>(File.ReadAllBytes(path), JsonOptions) : null;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { return null; }
    }

    public static string Serialize(PortableManifest manifest) => JsonSerializer.Serialize(manifest, JsonOptions);

    /// <summary>
    /// Problema de integridad del paquete (texto en castellano) o <c>null</c> si todo está bien o no es portable. Se calcula una vez por proceso: hashea el único PKHeX.Core.dll y comprueba que
    /// el que está cargado ES ese fichero y coincide con el manifiesto, y que el Worker y la base de datos están (con el tamaño del manifiesto).
    /// </summary>
    public static string? IntegrityProblem => IntegrityProblemAt(Root, loadedCorePath: LoadedCorePath());

    /// <summary>
    /// Fichero del que está cargado el PKHeX.Core de este proceso (el módulo del ensamblado; vacío si no viene de un fichero, p. ej. en Android, donde el modo portable no existe y no se usa).
    /// </summary>
    private static string? LoadedCorePath()
    {
        var path = typeof(PKHeX.Core.PKM).Module.FullyQualifiedName;
        return Path.IsPathRooted(path) ? path : null;
    }

    public static string? IntegrityProblemAt(string baseDir, string? loadedCorePath)
    {
        lock (Gate)
        {
            if (string.Equals(_checkedDir, baseDir, StringComparison.OrdinalIgnoreCase)) return _problem;
            _problem = Compute(baseDir, loadedCorePath);
            _checkedDir = baseDir;
            return _problem;
        }
    }

    /// <summary>Solo para pruebas: olvida el resultado en caché.</summary>
    internal static void ResetCache() { lock (Gate) { _checkedDir = null; _problem = null; } }

    private static string? Compute(string baseDir, string? loadedCorePath)
    {
        if (!IsActiveAt(baseDir)) return null;
        var manifest = Load(baseDir);
        if (manifest is null) return "El manifiesto del paquete (Pokekeep\\manifiesto.json) está dañado o no se puede leer. Vuelve a descomprimir el paquete original.";

        var corePath = Path.Combine(baseDir, manifest.Core.File);
        if (!File.Exists(corePath)) return $"Falta {manifest.Core.File}. Vuelve a descomprimir el paquete original.";
        if (loadedCorePath is { Length: > 0 } && !string.Equals(Path.GetFullPath(loadedCorePath), Path.GetFullPath(corePath), StringComparison.OrdinalIgnoreCase))
            return $"La interfaz ha cargado un PKHeX.Core distinto del del paquete ({Path.GetFileName(loadedCorePath)} en otra carpeta). Debe haber un solo PKHeX.Core.dll.";
        var coreSha = Sha256Of(corePath);
        if (!string.Equals(coreSha, manifest.Core.Sha256, StringComparison.OrdinalIgnoreCase))
            return "El PKHeX.Core.dll del paquete no coincide con el del manifiesto: el paquete está alterado o dañado. Vuelve a descomprimir el paquete original.";

        // El motor y el catálogo deben estar y con el tamaño del manifiesto (hash completo: «Verificar-paquete.ps1» o PortableMode.VerifyAll).
        foreach (var f in manifest.Files.Where(f => IsCritical(f.Path)))
        {
            var p = Path.Combine(baseDir, f.Path.Replace('/', Path.DirectorySeparatorChar));
            var info = new FileInfo(p);
            if (!info.Exists) return $"Falta {f.Path}. Vuelve a descomprimir el paquete original.";
            if (info.Length != f.Bytes) return $"{f.Path} no tiene el tamaño del manifiesto: el paquete está alterado o dañado.";
        }
        // Los binarios del motor (pocos y pequeños) sí se hashean: el motor que genera es el que se empaquetó.
        foreach (var f in manifest.Files.Where(f => IsEngineBinary(f.Path)))
        {
            var p = Path.Combine(baseDir, f.Path.Replace('/', Path.DirectorySeparatorChar));
            if (!string.Equals(Sha256Of(p), f.Sha256, StringComparison.OrdinalIgnoreCase))
                return $"{f.Path} no coincide con el manifiesto: el paquete está alterado o dañado.";
        }
        return null;
    }

    private static bool IsCritical(string rel) =>
        rel.Equals("pokekeep-worker.exe", StringComparison.OrdinalIgnoreCase)
        || rel.EndsWith(".sqlite", StringComparison.OrdinalIgnoreCase);

    private static bool IsEngineBinary(string rel) =>
        rel.Equals("pokekeep-worker.exe", StringComparison.OrdinalIgnoreCase)
        || (rel.StartsWith("AutoLegalidad.", StringComparison.OrdinalIgnoreCase) && rel.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        || rel.Equals("pokekeep-worker.dll", StringComparison.OrdinalIgnoreCase);

    /// <summary>Comprobación COMPLETA de todos los ficheros del manifiesto. Devuelve la lista de problemas (vacía = íntegro).</summary>
    public static IReadOnlyList<string> VerifyAll(string baseDir)
    {
        var manifest = Load(baseDir);
        if (manifest is null) return ["No se puede leer Pokekeep\\manifiesto.json."];
        var problems = new List<string>();
        foreach (var f in manifest.Files)
        {
            var p = Path.Combine(baseDir, f.Path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(p)) { problems.Add($"Falta {f.Path}"); continue; }
            if (new FileInfo(p).Length != f.Bytes) { problems.Add($"Tamaño distinto: {f.Path}"); continue; }
            if (!string.Equals(Sha256Of(p), f.Sha256, StringComparison.OrdinalIgnoreCase)) problems.Add($"SHA-256 distinto: {f.Path}");
        }
        return problems;
    }

    public static string Sha256Of(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
