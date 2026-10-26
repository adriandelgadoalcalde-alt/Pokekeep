// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Diagnostics;
using System.Text;
using AutoLegalidad.Catalog.Core;
using AutoLegalidad.Catalog.Coverage;
using AutoLegalidad.Catalog.Db;
using AutoLegalidad.Catalog.Snapshots;
using AutoLegalidad.Catalog.Sources;
using AutoLegalidad.Contracts;

namespace AutoLegalidad.Catalog.Build;

/// <summary>Que construir y desde donde. <see cref="RepoRoot"/> contiene <c>fuentes.lock.json</c>; <see cref="Raiz"/> contiene <c>fuentes\</c>.</summary>
public sealed record BuildPlan(string RepoRoot, string Raiz, string DbPath)
{
    public bool IncludeEvents { get; init; } = true;
    public bool IncludeGo { get; init; } = true;
    /// <summary>Informe JSON con las cifras reales (por defecto <c>docs/datos/hito1-cifras.json</c>).</summary>
    public string? ReportPath { get; init; }
    /// <summary>Manifiesto de EventsGallery (solo hashes y metadatos) para versionar en Git.</summary>
    public string? GalleryManifestPath { get; init; }
    /// <summary>Entradas de la Libreria que el banco del Hito 3b demostro imposibles (por defecto <c>docs/datos/libreria-no-generables.tsv</c>; si no existe, ninguna).</summary>
    public string? LibraryBlocksPath { get; init; }
    /// <summary>Identidad de los binarios con la que se liga la BD (<c>--core-sha</c>...); <c>null</c> = la del proceso (los ficheros junto al ejecutable). Android F1: permite construir en el PC la BD del movil.</summary>
    public IBinaryIdentity? Identity { get; init; }
}

public sealed record BuildReport(IDictionary<string, object?> Stats, GateResult Gate, IReadOnlyDictionary<string, double> Seconds, VerifyResult Verification, string DbPath, string Json)
{
    public string ToText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Cifras reales del catalogo ===");
        sb.AppendLine(Json);
        sb.AppendLine("=== Tiempos (s) ===");
        foreach (var (k, v) in Seconds) sb.AppendLine($"  {k}: {v:F1}");
        sb.AppendLine("=== Verificacion y gate del Hito 1 ===");
        sb.Append(Verification.ToText());
        sb.AppendLine(Gate.Passed ? "GATE DEL HITO 1: CUMPLIDO" : "GATE DEL HITO 1: NO CUMPLIDO");
        sb.AppendLine("BD: " + DbPath);
        return sb.ToString();
    }
}

/// <summary>Orquesta la construccion reproducible de <c>autolegalidad.sqlite</c> (tarea de build <c>construir-bd</c>, ADR 0005/0007).</summary>
public static class CatalogBuilder
{
    public static BuildReport Run(BuildPlan plan, Action<string>? log = null)
    {
        var secs = new Dictionary<string, double>();
        var sw = Stopwatch.StartNew();
        void Lap(string name) { secs[name] = Math.Round(sw.Elapsed.TotalSeconds, 1); sw.Restart(); }

        var lockPath = Path.Combine(plan.RepoRoot, "fuentes.lock.json");
        var core = CoreSourceInfo.Resolve(lockPath, plan.Identity);
        log?.Invoke($"Core {core.Tag} ({core.Commit12}), dll {core.DllSha256[..12]}...");

        foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
            if (File.Exists(plan.DbPath + suffix)) File.Delete(plan.DbPath + suffix);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(plan.DbPath))!);

        using var c = CatalogDatabase.Open(plan.DbPath);
        var schema = new Migrator(c).MigrateToLatest();
        log?.Invoke($"Esquema en version {schema}.");
        Lap("migraciones");

        var ds = CoreDataSet.Build(core);
        Lap("core_en_memoria");
        CoreCatalogWriter.Write(c, ds, schema);
        log?.Invoke($"Core: {ds.Species.Count} especies, {ds.Forms.Count} formas, {ds.Availability.Count} filas de disponibilidad, {ds.Encounters.Count} encuentros.");
        Lap("core_escritura");

        var versions = new List<string> { core.SourceVersionId };
        var formSet = ds.Forms.Select(f => (f.Species, f.Form)).ToHashSet();
        string? allowlistSha = null;
        IReadOnlyDictionary<string, object?>? trackingStats = null;
        IReadOnlyDictionary<string, object?>? regionStats = null;

        if (plan.IncludeEvents)
        {
            var clone = SourceClone.Resolve(lockPath, plan.Raiz, "EventsGallery");
            var allow = Allowlist.LoadEmbedded("eventsgallery.allowlist.json");
            allowlistSha = allow.Sha256;
            var stage = EventsGalleryImporter.Stage(clone, allow, formSet, log, core.ReleaseDate);
            Lap("eventsgallery_staging");
            EventsGalleryImporter.Persist(c, core.ReleaseId, stage);
            versions.Add($"{EventsGalleryImporter.SourceId}@{clone.Commit12}");
            if (plan.GalleryManifestPath is { } mp)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(mp))!);
                File.WriteAllText(mp, EventsGalleryImporter.ToManifestTsv(stage), new UTF8Encoding(false));
            }
            Lap("eventsgallery_transaccion");

            // Hito 5 (datos, ADR 0029): ventanas de distribucion de las tarjetas WC6/WC7 sin fecha, desde las hojas Extras/Tracking (solo metadatos derivados).
            var tracking = EventTrackingImporter.Stage(clone, c, core.ReleaseDate, log);
            EventTrackingImporter.Persist(c, tracking);
            trackingStats = tracking.Stats;
            Lap("eventsgallery_tracking");

            // Hito 5d (ADR 0047): región de los eventos (Core en Gen 3 y hojas de seguimiento de WC6/WC7): solo metadatos derivados; el resto queda sin región.
            var regions = EventRegionImporter.Stage(clone, c, log);
            EventRegionImporter.Persist(c, regions);
            regionStats = regions.Stats;
            Lap("eventsgallery_regiones");
        }

        if (plan.IncludeGo)
        {
            var clone = SourceClone.Resolve(lockPath, plan.Raiz, "PoGoEncTool");
            var stage = PoGoWindowImporter.Stage(clone, formSet, log);
            PoGoWindowImporter.Persist(c, core.ReleaseId, stage);
            versions.Add($"{PoGoWindowImporter.SourceId}@{clone.Commit12}");
            Lap("pogo");
        }

        var snap = SnapshotService.Create(c, versions, core.ReleaseId, core.DllSha256, allowlistSha);
        log?.Invoke($"Snapshot {snap.Id} Verified ({snap.ContentDigest[..12]}...).");
        Lap("snapshot");

        var cov = CoverageBuilder.Build(c, core.ReleaseId);
        CoverageBuilder.Persist(c, cov.Cases);
        log?.Invoke($"Cobertura: {cov.Cases.Count} tuplas ({cov.GamesInMatrix} juegos destino; sin presencia calculable: {string.Join(", ", cov.SkippedGames)}).");
        Lap("cobertura");

        var blocks = Library.LibraryBlocks.Load(plan.LibraryBlocksPath ?? Path.Combine(plan.RepoRoot, "docs", "datos", Library.LibraryBlocks.FileName));
        var lib = Library.LibraryBuilder.Build(c, core.ReleaseId, blocks);
        CatalogDatabase.Exec(c, "INSERT INTO library_search(library_search) VALUES('integrity-check')");
        log?.Invoke($"Libreria: {lib.Entries} entradas ({lib.Addable} ofrecibles, {lib.Dimmed} atenuadas, {lib.WithEventTitleEs} eventos con titulo en español; {lib.HomeRequired} solo con el ajuste de HOME; {lib.BlockedByRule} atenuadas por regla del catalogo y {lib.BlockedByMeasurement} por medicion del banco).");
        Lap("libreria");

        var verify = CatalogVerifier.Verify(c);
        Lap("verificacion");
        var stats = CatalogStats.Collect(c);
        stats["cobertura_juegos_omitidos_sin_disponibilidad"] = cov.SkippedGames.ToList<object?>();
        stats["cobertura_juegos_destino"] = cov.GamesInMatrix;
        stats["core"] = new Dictionary<string, object?> { ["tag"] = core.Tag, ["commit"] = core.Commit, ["dll_sha256"] = core.DllSha256, ["content_digest"] = ds.ContentDigest };
        if (trackingStats is not null) stats["ventanas_evento_tracking_detalle"] = trackingStats.ToDictionary(k => k.Key, k => k.Value);
        if (regionStats is not null) stats["region_eventos_detalle"] = regionStats.ToDictionary(k => k.Key, k => k.Value);
        stats["core_encuentros_crudos"] = ds.RawEncounterItems;
        stats["core_encuentros_sin_juego_resuelto"] = ds.UnresolvedEncounters;
        stats["core_tipos_sin_clasificar"] = ds.UnclassifiedTypes.ToList<object?>();
        stats["core_mgdb_no_pokemon_omitidos"] = ds.MgdbGiftsSkippedNotPokemon;
        stats["core_mgdb_duplicados"] = ds.MgdbGiftsDuplicate;
        var json = Model.CanonicalJson.SerializeIndented(MergeGate(stats, verify.Gate));
        var reportPath = plan.ReportPath;
        if (reportPath is not null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
            File.WriteAllText(reportPath, json + "\n", new UTF8Encoding(false));
        }
        CatalogDatabase.CheckpointAndClose(c);
        return new BuildReport(stats, verify.Gate, secs, verify, plan.DbPath, json);
    }

    private static IDictionary<string, object?> MergeGate(IDictionary<string, object?> stats, GateResult gate)
    {
        var all = new Dictionary<string, object?>(stats)
        {
            ["gate"] = new Dictionary<string, object?>
            {
                ["passed"] = gate.Passed,
                ["checks"] = gate.Checks.Select(g => (object?)new Dictionary<string, object?> { ["name"] = g.Name, ["ok"] = g.Ok, ["detail"] = g.Detail }).ToList(),
            },
        };
        return all;
    }
}
