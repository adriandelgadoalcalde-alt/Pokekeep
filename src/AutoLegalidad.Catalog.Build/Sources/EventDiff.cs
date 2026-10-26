// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Globalization;
using System.Text;
using AutoLegalidad.Catalog.Db;

namespace AutoLegalidad.Catalog.Sources;

/// <summary>Una fila del manifiesto TSV de EventsGallery (<c>docs/datos/eventsgallery-manifest.tsv</c>): solo ruta, hash y metadatos de procedencia; nunca los bytes del fichero.</summary>
public sealed record ManifestRow(string Path, string Sha256, long Bytes, string Format, string Rule, string EvidenceClass, bool Approved, string Parse, string Corroboration, string CoreKey);

/// <summary>Cambio de una entrada que existe en las dos versiones.</summary>
/// <param name="Kind"><c>Reclasificada</c> (mismo contenido, otra clasificacion), <c>ContenidoCambiado</c> (misma ruta, otro hash) o <c>Movida</c> (mismo hash, otra ruta).</param>
/// <param name="Key">Clave de la entrada (ruta o clave estable).</param>
/// <param name="Description">Texto legible de la entrada.</param>
/// <param name="Details">Lista «campo: antes -> despues».</param>
public sealed record EventChange(string Kind, string Key, string Description, IReadOnlyList<string> Details, bool ApprovedBefore, bool ApprovedAfter);

/// <summary>Resultado de comparar dos versiones de la base de eventos.</summary>
public sealed record EventDiffReport(string Source, string Before, string After, int CountBefore, int CountAfter,
    IReadOnlyList<(string Key, string Description, bool Approved)> Added, IReadOnlyList<(string Key, string Description, bool Approved)> Removed,
    IReadOnlyList<EventChange> Changed, int Unchanged)
{
    public bool HasChanges => Added.Count + Removed.Count + Changed.Count > 0;

    /// <summary>Entradas que pasan a poder ofrecerse (altas aprobadas y cambios de «no aprobada» a «aprobada»).</summary>
    public int NewlyApproved => Added.Count(a => a.Approved) + Changed.Count(c => !c.ApprovedBefore && c.ApprovedAfter);

    /// <summary>Entradas que dejan de poder ofrecerse (bajas aprobadas y cambios de «aprobada» a «no aprobada»): lo que de verdad puede romper algo de un guardado o de la cache.</summary>
    public int NoLongerApproved => Removed.Count(a => a.Approved) + Changed.Count(c => c.ApprovedBefore && !c.ApprovedAfter);

    public string ToText(int maxPerSection = 25)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Diferencias de la base de eventos ({Source})");
        sb.AppendLine($"  antes:   {Before} ({CountBefore} entradas)");
        sb.AppendLine($"  despues: {After} ({CountAfter} entradas)");
        sb.AppendLine($"  altas {Added.Count} | bajas {Removed.Count} | cambios {Changed.Count} | sin cambios {Unchanged}");
        sb.AppendLine($"  efecto sobre lo que se ofrece: +{NewlyApproved} pasan a ofrecerse, -{NoLongerApproved} dejan de ofrecerse");
        if (!HasChanges) { sb.AppendLine("  Sin diferencias."); return sb.ToString(); }
        Section(sb, "Altas", Added.Select(a => $"{(a.Approved ? "[aprobada]    " : "[no aprobada] ")}{a.Description}"), maxPerSection);
        Section(sb, "Bajas", Removed.Select(a => $"{(a.Approved ? "[aprobada]    " : "[no aprobada] ")}{a.Description}"), maxPerSection);
        foreach (var g in Changed.GroupBy(c => c.Kind).OrderBy(g => g.Key, StringComparer.Ordinal))
            Section(sb, $"Cambios: {g.Key}", g.Select(c => $"{c.Description}: {string.Join("; ", c.Details)}"), maxPerSection);
        return sb.ToString();
    }

    private static void Section(StringBuilder sb, string title, IEnumerable<string> lines, int max)
    {
        var all = lines.ToList();
        if (all.Count == 0) return;
        sb.AppendLine().AppendLine($"{title} ({all.Count})");
        foreach (var l in all.Take(max)) sb.AppendLine("  " + l);
        if (all.Count > max) sb.AppendLine($"  ... y {all.Count - max} mas (usa --salida para el detalle completo)");
    }

    /// <summary>Detalle completo en TSV (una fila por diferencia).</summary>
    public string ToTsv()
    {
        var sb = new StringBuilder();
        sb.Append("tipo\tclave\tdescripcion\taprobada_antes\taprobada_despues\tdetalle\n");
        foreach (var a in Added) sb.Append("alta\t").Append(a.Key).Append('\t').Append(a.Description).Append("\t\t").Append(a.Approved ? 1 : 0).Append("\t\n");
        foreach (var a in Removed) sb.Append("baja\t").Append(a.Key).Append('\t').Append(a.Description).Append('\t').Append(a.Approved ? 1 : 0).Append("\t\t\n");
        foreach (var c in Changed)
            sb.Append(c.Kind).Append('\t').Append(c.Key).Append('\t').Append(c.Description).Append('\t').Append(c.ApprovedBefore ? 1 : 0).Append('\t').Append(c.ApprovedAfter ? 1 : 0).Append('\t').Append(string.Join("; ", c.Details)).Append('\n');
        return sb.ToString();
    }
}

/// <summary>
/// Aviso de datos nuevos (propuesta del Hito 1, ADR 0050): compara dos versiones de la base de eventos y lista altas, bajas y cambios de clasificacion ANTES de actualizar la pareja de versiones.
/// Dos fuentes: el manifiesto TSV de EventsGallery (solo hashes y metadatos, ADR 0005) y los eventos de dos bases construidas (<c>encounter</c> con <c>kind = 'Event'</c>, que incluye las tarjetas del MGDB del Core).
/// Solo lee; no cambia nada, no descarga nada y no mira ningun guardado.
/// </summary>
public static class EventDiff
{
    public const string ManifestPathInRepo = "docs/datos/eventsgallery-manifest.tsv";
    private static readonly string[] ManifestHeader = ["relative_path", "sha256", "bytes", "format", "rule", "evidence_class", "approved", "parse", "core_corroboration", "core_key"];

    /// <summary>Lee un manifiesto TSV. Devuelve la linea de procedencia («# EventsGallery @ commit ...») y las filas; falla si la cabecera no es la esperada o una fila esta mal formada.</summary>
    public static (string Provenance, List<ManifestRow> Rows) ParseManifest(string text)
    {
        var provenance = string.Empty;
        var rows = new List<ManifestRow>();
        var headerSeen = false;
        var lineNo = 0;
        foreach (var raw in text.Split('\n'))
        {
            lineNo++;
            var line = raw.TrimEnd('\r');
            if (line.Length == 0) continue;
            if (line.StartsWith('#')) { if (provenance.Length == 0) provenance = line.TrimStart('#', ' '); continue; }
            var f = line.Split('\t');
            if (!headerSeen)
            {
                if (!f.SequenceEqual(ManifestHeader)) throw new FormatException($"Manifiesto: la cabecera (linea {lineNo}) no es la esperada ({string.Join('\t', ManifestHeader)}).");
                headerSeen = true;
                continue;
            }
            if (f.Length != ManifestHeader.Length) throw new FormatException($"Manifiesto: la linea {lineNo} tiene {f.Length} columnas y se esperaban {ManifestHeader.Length}.");
            if (!long.TryParse(f[2], NumberStyles.None, CultureInfo.InvariantCulture, out var bytes)) throw new FormatException($"Manifiesto: bytes no numericos en la linea {lineNo}.");
            rows.Add(new ManifestRow(f[0], f[1], bytes, f[3], f[4], f[5], f[6] == "1", f[7], f[8], f[9]));
        }
        if (!headerSeen) throw new FormatException("Manifiesto: no tiene cabecera.");
        return (provenance, rows);
    }

    /// <summary>Compara dos manifiestos de EventsGallery. La identidad de una entrada es su ruta relativa; una ruta que desaparece y cuyo hash aparece en otra ruta se cuenta como «Movida» (no como baja y alta).</summary>
    public static EventDiffReport CompareManifests(string beforeText, string afterText, string beforeLabel = "antes", string afterLabel = "despues")
    {
        var (provB, before) = ParseManifest(beforeText);
        var (provA, after) = ParseManifest(afterText);
        var byPathB = before.GroupBy(r => r.Path, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var byPathA = after.GroupBy(r => r.Path, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var added = after.Where(r => !byPathB.ContainsKey(r.Path)).ToList();
        var removed = before.Where(r => !byPathA.ContainsKey(r.Path)).ToList();
        var changed = new List<EventChange>();

        // Movidas: el mismo contenido (hash) en otra ruta. Se empareja uno a uno por hash.
        var removedByHash = removed.GroupBy(r => r.Sha256, StringComparer.Ordinal).ToDictionary(g => g.Key, g => new Queue<ManifestRow>(g), StringComparer.Ordinal);
        var stillAdded = new List<ManifestRow>();
        foreach (var a in added)
        {
            if (removedByHash.TryGetValue(a.Sha256, out var q) && q.Count > 0)
            {
                var b = q.Dequeue();
                var details = new List<string> { $"ruta: {b.Path} -> {a.Path}" };
                details.AddRange(Differences(b, a));
                changed.Add(new EventChange("Movida", a.Path, Describe(a), details, b.Approved, a.Approved));
            }
            else stillAdded.Add(a);
        }
        var stillRemoved = removedByHash.Values.SelectMany(q => q).ToList();

        var unchanged = 0;
        foreach (var b in before)
        {
            if (!byPathA.TryGetValue(b.Path, out var a)) continue;
            if (!string.Equals(a.Sha256, b.Sha256, StringComparison.Ordinal))
            {
                var details = new List<string> { $"hash: {Short(b.Sha256)} -> {Short(a.Sha256)}", $"bytes: {b.Bytes} -> {a.Bytes}" };
                details.AddRange(Differences(b, a));
                changed.Add(new EventChange("ContenidoCambiado", a.Path, Describe(a), details, b.Approved, a.Approved));
                continue;
            }
            var diff = Differences(b, a);
            if (diff.Count == 0) { unchanged++; continue; }
            changed.Add(new EventChange("Reclasificada", a.Path, Describe(a), diff, b.Approved, a.Approved));
        }

        static (string, string, bool) Item(ManifestRow r) => (r.Path, Describe(r), r.Approved);
        return new EventDiffReport("manifiesto de EventsGallery", Label(beforeLabel, provB), Label(afterLabel, provA), before.Count, after.Count,
            [.. stillAdded.OrderBy(r => r.Path, StringComparer.Ordinal).Select(Item)], [.. stillRemoved.OrderBy(r => r.Path, StringComparer.Ordinal).Select(Item)],
            [.. changed.OrderBy(c => c.Kind, StringComparer.Ordinal).ThenBy(c => c.Key, StringComparer.Ordinal)], unchanged);
    }

    /// <summary>Compara los eventos (<c>encounter.kind = 'Event'</c>, tarjetas del MGDB del Core y de EventsGallery) de dos bases construidas, por clave estable.</summary>
    public static EventDiffReport CompareDatabases(string beforeDb, string afterDb)
    {
        var before = ReadEvents(beforeDb);
        var after = ReadEvents(afterDb);
        var added = after.Where(kv => !before.ContainsKey(kv.Key)).OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => (kv.Key, kv.Value.Description, kv.Value.Approved)).ToList();
        var removed = before.Where(kv => !after.ContainsKey(kv.Key)).OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => (kv.Key, kv.Value.Description, kv.Value.Approved)).ToList();
        var changed = new List<EventChange>();
        var unchanged = 0;
        foreach (var (key, b) in before.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            if (!after.TryGetValue(key, out var a)) continue;
            var details = new List<string>();
            if (b.Species != a.Species || b.Form != a.Form) details.Add($"especie/forma: {b.Species}/{b.Form} -> {a.Species}/{a.Form}");
            if (b.Game != a.Game) details.Add($"juego de origen: {b.Game} -> {a.Game}");
            if (b.Approved != a.Approved) details.Add($"aprobada: {(b.Approved ? 1 : 0)} -> {(a.Approved ? 1 : 0)}");
            if (!string.Equals(b.DescriptorSha256, a.DescriptorSha256, StringComparison.Ordinal)) details.Add($"descriptor: {Short(b.DescriptorSha256)} -> {Short(a.DescriptorSha256)}");
            if (!string.Equals(b.Corroboration, a.Corroboration, StringComparison.Ordinal)) details.Add($"corroboracion con el Core: {b.Corroboration} -> {a.Corroboration}");
            if (details.Count == 0) { unchanged++; continue; }
            var classification = b.Approved != a.Approved || b.Species != a.Species || b.Form != a.Form || b.Game != a.Game || b.Corroboration != a.Corroboration;
            changed.Add(new EventChange(classification ? "Reclasificada" : "ContenidoCambiado", key, a.Description, details, b.Approved, a.Approved));
        }
        return new EventDiffReport("eventos de la base construida (MGDB del Core y EventsGallery)", Path.GetFileName(beforeDb), Path.GetFileName(afterDb), before.Count, after.Count, added, removed, changed, unchanged);
    }

    private sealed record EventRow(string Description, int Species, int Form, int Game, bool Approved, string DescriptorSha256, string Corroboration);

    private static Dictionary<string, EventRow> ReadEvents(string db)
    {
        if (!File.Exists(db)) throw new FileNotFoundException("No existe la base: " + db);
        using var c = CatalogDatabase.Open(db, readOnly: true);
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
            SELECT e.encounter_key, e.species_id, e.form_id, e.origin_game, e.approved_for_generation, e.descriptor_sha256,
                   COALESCE((SELECT ao.core_corroboration FROM artifact_origin ao WHERE ao.artifact_sha256 = e.artifact_sha256 AND ao.source_version_id = e.source_version_id LIMIT 1), '')
              FROM encounter e WHERE e.kind = 'Event'
            """;
        var map = new Dictionary<string, EventRow>(StringComparer.Ordinal);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var key = r.GetString(0);
            var desc = $"{key} (especie {r.GetInt32(1)}{(r.GetInt32(2) != 0 ? "/" + r.GetInt32(2).ToString(CultureInfo.InvariantCulture) : "")}, juego {r.GetInt32(3)})";
            map[key] = new EventRow(desc, r.GetInt32(1), r.GetInt32(2), r.GetInt32(3), r.GetInt32(4) != 0, r.GetString(5), r.GetString(6));
        }
        return map;
    }

    private static List<string> Differences(ManifestRow b, ManifestRow a)
    {
        var d = new List<string>();
        if (b.EvidenceClass != a.EvidenceClass) d.Add($"clase de evidencia: {b.EvidenceClass} -> {a.EvidenceClass}");
        if (b.Approved != a.Approved) d.Add($"aprobada: {(b.Approved ? 1 : 0)} -> {(a.Approved ? 1 : 0)}");
        if (b.Parse != a.Parse) d.Add($"lectura: {b.Parse} -> {a.Parse}");
        if (b.Corroboration != a.Corroboration) d.Add($"corroboracion con el Core: {b.Corroboration} -> {a.Corroboration}");
        if (b.Rule != a.Rule) d.Add($"regla de la lista permitida: {b.Rule} -> {a.Rule}");
        if (b.Format != a.Format) d.Add($"formato: {b.Format} -> {a.Format}");
        if (b.CoreKey != a.CoreKey) d.Add($"clave del Core: {(b.CoreKey.Length == 0 ? "(ninguna)" : b.CoreKey)} -> {(a.CoreKey.Length == 0 ? "(ninguna)" : a.CoreKey)}");
        return d;
    }

    private static string Describe(ManifestRow r) => $"{r.Path} [{r.Format}, {r.EvidenceClass}]";
    private static string Short(string sha) => sha.Length > 12 ? sha[..12] : sha;
    private static string Label(string label, string provenance) => provenance.Length == 0 ? label : $"{label} - {provenance.Split('|')[0].Trim()}";
}
