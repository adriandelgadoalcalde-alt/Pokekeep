// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Engine.Catalog;
using AutoLegalidad.Engine.SolutionCache;

namespace AutoLegalidad.Worker;

/// <summary>
/// Activacion de la cache persistente de soluciones (R-011, ADR 0037) en el Worker: <c>--soluciones &lt;ruta&gt;</c> o, si no se da, <c>&lt;carpeta de --db&gt;\soluciones.sqlite</c> SI EXISTE (modo lectura).
/// El modo de registro del precalculo se activa solo con la variable de entorno de DESARROLLO <see cref="RecordVariable"/> (no es una opcion de usuario). Cualquier fallo al abrir la cache
/// (corrupta, otra identidad, bloqueada) deja la cache desactivada y se sigue: nunca rompe la solicitud.
/// </summary>
public sealed class WorkerSolutions : IDisposable
{
#if !POKEKEEP_PORTABLE
    /// <summary>Variable de entorno SOLO de desarrollo: ruta del fichero donde el precalculo registra las soluciones halladas. El Worker del paquete portable no la contiene.</summary>
    public const string RecordVariable = "POKEKEEP_SOLUCIONES_REGISTRAR";
#endif

    public const string DefaultFileName = "soluciones.sqlite";

    private readonly ISolutionCache _cache;
    private readonly IDisposable _restore;
    private readonly TextWriter? _log;

    private WorkerSolutions(ISolutionCache cache, TextWriter? log)
    {
        _cache = cache;
        _log = log;
        _restore = SolutionCacheHost.Use(cache);
    }

    public ISolutionCache Cache => _cache;

    /// <summary>Abre la cache que corresponda y la instala como cache ambiente del proceso hasta que se suelte el resultado.</summary>
    public static WorkerSolutions Open(string dbPath, string? explicitPath, CatalogReader catalog, TextWriter? log, string? recordPath = null)
    {
        ISolutionCache cache;
        try
        {
#if !POKEKEEP_PORTABLE
            recordPath ??= Environment.GetEnvironmentVariable(RecordVariable);
#endif
            var record = !string.IsNullOrWhiteSpace(recordPath);
            var path = record ? recordPath! : explicitPath ?? Path.Combine(Path.GetDirectoryName(Path.GetFullPath(dbPath)) ?? ".", DefaultFileName);
            if (!record && explicitPath is null && !File.Exists(path)) return new WorkerSolutions(NullSolutionCache.Instance, log); // sin fichero: nada que decir
            var binding = SolutionCacheBinding.ForRuntime(catalog.ReadIdentity().SnapshotId);
            cache = record ? SqliteSolutionCache.OpenRecording(path, binding, SolutionPayloadFormat.V2Binary) : SqliteSolutionCache.OpenReadOnly(path, binding);
            if (!cache.Enabled) log?.WriteLine("POKEKEEP-CACHE " + cache.StatusText);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            log?.WriteLine($"POKEKEEP-CACHE desactivada: {ex.GetType().Name}: {ex.Message}");
            cache = NullSolutionCache.Instance;
        }
#if !POKEKEEP_PORTABLE
        // ADR 0048: diagnostico de la cache (solo desarrollo): una linea por consulta en <carpeta>\cache-<pid>.tsv.
        if (cache.Enabled && Environment.GetEnvironmentVariable("POKEKEEP_CACHE_DIAG") is { Length: > 0 } diagDir)
        {
            Directory.CreateDirectory(diagDir);
            cache = new DiagnosticSolutionCache(cache, Path.Combine(diagDir, $"cache-{Environment.ProcessId}.tsv"));
        }
#endif
        return new WorkerSolutions(cache, log);
    }

    public void Dispose()
    {
        if (_cache.Enabled) _log?.WriteLine($"POKEKEEP-CACHE aciertos={_cache.Hits} sin-entrada={_cache.Misses} rechazadas={_cache.Rejected}");
        // ADR 0097: las filas que existian pero no se pudieron leer (contenido corrupto o de una version desconocida) se dicen aqui; el motor las trata como ausentes y busca.
        if (_cache is SqliteSolutionCache { Unreadable: > 0 } sqlite) _log?.WriteLine($"POKEKEEP-CACHE ilegibles={sqlite.Unreadable}: {sqlite.StatusText}");
        _restore.Dispose();
        (_cache as IDisposable)?.Dispose();
    }
}
