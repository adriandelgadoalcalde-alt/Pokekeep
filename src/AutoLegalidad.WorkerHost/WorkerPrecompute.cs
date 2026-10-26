// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
#if !POKEKEEP_PORTABLE
using AutoLegalidad.Engine.Catalog;
using AutoLegalidad.Engine.Optimization;

namespace AutoLegalidad.Worker;

/// <summary>
/// Herramienta de CONSTRUCCION (no del paquete; ADR 0042): precalcula las tablas universales de las plantillas de Teratipo en el fichero de soluciones del registro
/// (<c>POKEKEEP_SOLUCIONES_REGISTRAR</c>), con el mismo binario y la misma identidad que el paquete. Uso:
/// <c>pokekeep-worker --db &lt;bd&gt; --precalcular-teratipo [--contar] [--grupos siete,dist,base,kitakami,arandano] [--estrellas 6,7] [--desde N] [--max N] [--plazo SEGUNDOS_POR_GRUPO]</c>.
/// </summary>
public static class WorkerPrecompute
{
    public static int Run(string[] args, string dbPath)
    {
        string? Arg(string name) { var i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
        var groups = Arg("--grupos")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var stars = Arg("--estrellas")?.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
        var from = int.TryParse(Arg("--desde"), out var d) ? d : 0;
        var max = int.TryParse(Arg("--max"), out var m) ? m : int.MaxValue;
        var limit = TimeSpan.FromSeconds(int.TryParse(Arg("--plazo"), out var p) ? p : 3600);

        var templates = TeraUniversePrecompute.Enumerate(groups, stars);
        Console.Error.WriteLine($"[teratipo] {templates.Count} plantillas (especie y forma) en los grupos {(groups is null ? "todos" : string.Join(',', groups))}.");
        foreach (var g in templates.GroupBy(t => t.Group).OrderBy(g => g.Key))
            Console.Error.WriteLine($"[teratipo]   {g.Key}: {g.Count()} ({string.Join(", ", g.GroupBy(t => t.Stars).OrderBy(s => s.Key).Select(s => $"{s.Key}* x{s.Count()}"))})");
        if (args.Contains("--contar", StringComparer.Ordinal)) return 0;

        var record = Environment.GetEnvironmentVariable(WorkerSolutions.RecordVariable);
        if (string.IsNullOrWhiteSpace(record)) { Console.Error.WriteLine($"[teratipo] falta {WorkerSolutions.RecordVariable} (fichero de soluciones del registro)."); return 2; }
        using var catalog = new CatalogReader(dbPath);
        using var solutions = WorkerSolutions.Open(dbPath, null, catalog, Console.Error, record);
        if (!solutions.Cache.Recording) { Console.Error.WriteLine("[teratipo] la cache no esta en modo registro: " + solutions.Cache.StatusText); return 3; }

        var todo = templates.Skip(from).Take(max).ToList();
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
        var s = TeraUniversePrecompute.Run(solutions.Cache, todo, limit, Console.Out, cts.Token);
        Console.Error.WriteLine($"[teratipo] FIN plantillas={s.Templates} registradas={s.Recorded} ya-estaban={s.AlreadyPresent} no-elegibles={s.NotEligible} incompletas={s.Incomplete} sin-casillas={s.NoBuckets} bytes={s.Bytes} barrido-ms={s.ScanMs} total={s.Elapsed.TotalMinutes:0.0} min");
        return s.Incomplete == 0 ? 0 : 4;
    }
}
#endif
