// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace AutoLegalidad.Ui;

/// <summary>
/// Dónde están el motor (Worker) y el catálogo. En desarrollo: ajuste del usuario, variable de entorno, carpeta <c>Pokekeep</c> junto al ejecutable y la salida de compilación del repo.
/// En el PAQUETE PORTABLE (Hito 6, R-008; compilado con <c>POKEKEEP_PORTABLE</c>) y en cualquier carpeta con <c>Pokekeep\manifiesto.json</c>: SOLO relativo al ejecutable;
/// el binario del paquete ni siquiera contiene los nombres de las variables de entorno de desarrollo.
/// </summary>
public static class PokekeepPaths
{
#if !POKEKEEP_PORTABLE
    public const string WorkerEnv = "POKEKEEP_WORKER";
    public const string CatalogEnv = "POKEKEEP_DB";
#endif
    public const string WorkerFile = "pokekeep-worker.exe";
    public const string CatalogFile = "autolegalidad.sqlite";

    /// <summary>Ruta del Worker. <paramref name="baseDir"/> es la raíz del programa (por defecto <see cref="Portable.PortableMode.Root"/>).</summary>
    public static string? ResolveWorker(PokekeepSettings settings, string? baseDir = null)
    {
        baseDir ??= Portable.PortableMode.Root;
#if POKEKEEP_PORTABLE
        return FirstExisting(Path.Combine(baseDir, WorkerFile));
#else
        // Paquete portable: SOLO relativo al ejecutable; ni ajustes con rutas, ni variables de entorno, ni el árbol de desarrollo.
        if (Portable.PortableMode.IsActiveAt(baseDir)) return FirstExisting(Path.Combine(baseDir, WorkerFile));
        return FirstExisting(settings.WorkerPath, Environment.GetEnvironmentVariable(WorkerEnv),
            Path.Combine(baseDir, "Pokekeep", "Worker", WorkerFile), Path.Combine(baseDir, "Pokekeep", WorkerFile), Path.Combine(baseDir, "Worker", WorkerFile),
            Path.Combine(baseDir, WorkerFile), DevPath(baseDir, Path.Combine("src", "AutoLegalidad.Worker", "bin", "Release", "net10.0", WorkerFile)));
#endif
    }

    public static string? ResolveCatalog(PokekeepSettings settings, string? baseDir = null)
    {
        baseDir ??= Portable.PortableMode.Root;
#if POKEKEEP_PORTABLE
        return FirstExisting(Path.Combine(baseDir, "Pokekeep", "datos", CatalogFile));
#else
        if (Portable.PortableMode.IsActiveAt(baseDir)) return FirstExisting(Path.Combine(baseDir, "Pokekeep", "datos", CatalogFile));
        return FirstExisting(settings.CatalogPath, Environment.GetEnvironmentVariable(CatalogEnv),
            Path.Combine(baseDir, "Pokekeep", "datos", CatalogFile), Path.Combine(baseDir, "Pokekeep", CatalogFile), Path.Combine(baseDir, "datos", CatalogFile),
            Path.Combine(baseDir, CatalogFile), DevPath(baseDir, Path.Combine("datos", CatalogFile)));
#endif
    }

    private static string? FirstExisting(params string?[] candidates)
    {
        foreach (var c in candidates)
        {
            if (!string.IsNullOrWhiteSpace(c) && File.Exists(c))
                return Path.GetFullPath(c);
        }
        return null;
    }

#if !POKEKEEP_PORTABLE
    /// <summary>Desarrollo: sube hasta la carpeta que contiene <c>fuentes.lock.json</c> (el repo) y le añade <paramref name="relative"/>.</summary>
    private static string? DevPath(string baseDir, string relative)
    {
        var d = new DirectoryInfo(baseDir);
        while (d is not null)
        {
            if (File.Exists(Path.Combine(d.FullName, "fuentes.lock.json")))
                return Path.Combine(d.FullName, relative);
            d = d.Parent;
        }
        var repo = Environment.GetEnvironmentVariable("POKEKEEP_REPO");
        return string.IsNullOrWhiteSpace(repo) ? null : Path.Combine(repo, relative);
    }
#endif
}
