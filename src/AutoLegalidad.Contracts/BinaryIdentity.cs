// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace AutoLegalidad.Contracts;

/// <summary>
/// Identidad (SHA-256, hexadecimal en mayúsculas) de los cuatro binarios que deciden el resultado de una búsqueda y con los que se liga el catálogo y la caché de soluciones:
/// el PKHeX.Core, el motor, el adaptador de ALM y la fuente de ALM. Android F1 (ADR 0054): el código ya no lee la ruta de un ensamblado cargado (en Android los ensamblados viajan
/// dentro del APK); recibe esta identidad. Escritorio: <see cref="FileBinaryIdentity"/> (el hash de los ficheros, como siempre). Android: <see cref="FixedBinaryIdentity"/> con las
/// constantes que genera la compilación. Un valor ausente o distinto es siempre <c>VersionMismatch</c>: nunca se aceptan datos de otra versión.
/// </summary>
public interface IBinaryIdentity
{
    string CoreSha256 { get; }
    string EngineSha256 { get; }
    string AdapterSha256 { get; }
    string AlmSha256 { get; }
}

/// <summary>Identidad con valores explícitos (Android: constantes incrustadas; herramientas de construcción: <c>--core-sha</c>, <c>--engine-sha</c>, <c>--adapter-sha</c>, <c>--alm-sha</c>).</summary>
public sealed record FixedBinaryIdentity(string CoreSha256, string EngineSha256, string AdapterSha256, string AlmSha256) : IBinaryIdentity
{
    /// <summary>Normaliza un SHA-256 dado a mano (64 dígitos hexadecimales, sin distinguir mayúsculas); lanza <see cref="FormatException"/> si no lo es.</summary>
    public static string Normalize(string sha, string argumentName)
    {
        var t = sha.Trim();
        if (t.Length != 64 || !t.All(Uri.IsHexDigit))
            throw new FormatException($"{argumentName}: se esperaban 64 dígitos hexadecimales (SHA-256) y llegó «{sha}».");
        return t.ToUpperInvariant();
    }
}

/// <summary>Identidad calculada del contenido de los ficheros de los binarios (escritorio). Cada hash se calcula una vez por fichero y proceso; un fichero que falta vale <see cref="Absent"/>.</summary>
public sealed class FileBinaryIdentity(string coreDll, string engineDll, string adapterDll, string almDll) : IBinaryIdentity
{
    public const string Absent = "(ausente)";

    public const string CoreFileName = "PKHeX.Core.dll";
    public const string EngineFileName = "AutoLegalidad.Engine.dll";
    public const string AdapterFileName = "AutoLegalidad.ALMAdapter.dll";
    public const string AlmFileName = "AutoLegalidad.AlmSource.dll";

    private static readonly ConcurrentDictionary<string, string> ShaCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Los cuatro binarios con sus nombres de siempre dentro de <paramref name="directory"/> (en escritorio, la carpeta del ejecutable: allí se copian todos los ensamblados).</summary>
    public static FileBinaryIdentity FromDirectory(string directory)
        => new(Path.Combine(directory, CoreFileName), Path.Combine(directory, EngineFileName), Path.Combine(directory, AdapterFileName), Path.Combine(directory, AlmFileName));

    public string CoreSha256 => Sha(coreDll);
    public string EngineSha256 => Sha(engineDll);
    public string AdapterSha256 => Sha(adapterDll);
    public string AlmSha256 => Sha(almDll);

    private static string Sha(string path)
        => ShaCache.GetOrAdd(Path.GetFullPath(path), p => File.Exists(p) ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))) : Absent);
}

/// <summary>La identidad en vigor del proceso. Por defecto, los ficheros junto al ejecutable; Android la fija al arrancar y las herramientas de construcción pueden sobrescribir partes.</summary>
public static class BinaryIdentity
{
    private static IBinaryIdentity? _current;

    public static IBinaryIdentity Current
    {
        get => _current ??= FileBinaryIdentity.FromDirectory(AppContext.BaseDirectory);
        set => _current = value;
    }

    /// <summary>
    /// Copia de <paramref name="baseIdentity"/> con los valores dados cambiados (<c>null</c> = el de <paramref name="baseIdentity"/>). Es lo que permite construir el catálogo y la caché de
    /// soluciones del móvil en el PC con la identidad de los binarios del móvil (<c>--core-sha</c>, <c>--engine-sha</c>, <c>--adapter-sha</c>, <c>--alm-sha</c>).
    /// </summary>
    public static IBinaryIdentity WithOverrides(IBinaryIdentity baseIdentity, string? core = null, string? engine = null, string? adapter = null, string? alm = null)
        => core is null && engine is null && adapter is null && alm is null
            ? baseIdentity
            : new FixedBinaryIdentity(
                core is null ? baseIdentity.CoreSha256 : FixedBinaryIdentity.Normalize(core, "--core-sha"),
                engine is null ? baseIdentity.EngineSha256 : FixedBinaryIdentity.Normalize(engine, "--engine-sha"),
                adapter is null ? baseIdentity.AdapterSha256 : FixedBinaryIdentity.Normalize(adapter, "--adapter-sha"),
                alm is null ? baseIdentity.AlmSha256 : FixedBinaryIdentity.Normalize(alm, "--alm-sha"));

    /// <summary>Lee de una lista de argumentos de línea de comandos (<c>--core-sha &lt;hex&gt;</c> y compañía) las partes sobrescritas. Devuelve la identidad en vigor tal cual si no hay ninguna.</summary>
    public static IBinaryIdentity FromArguments(IReadOnlyList<string> args, IBinaryIdentity? baseIdentity = null)
    {
        string? Value(string name)
        {
            for (var i = 0; i < args.Count - 1; i++)
                if (string.Equals(args[i], name, StringComparison.Ordinal)) return args[i + 1];
            return null;
        }
        return WithOverrides(baseIdentity ?? Current, Value("--core-sha"), Value("--engine-sha"), Value("--adapter-sha"), Value("--alm-sha"));
    }
}
