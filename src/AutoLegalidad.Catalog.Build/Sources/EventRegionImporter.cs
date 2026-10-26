// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Text.Json;
using AutoLegalidad.Catalog.Db;
using AutoLegalidad.Catalog.Model;
using AutoLegalidad.Catalog.Sources.Spreadsheets;
using Microsoft.Data.Sqlite;

namespace AutoLegalidad.Catalog.Sources;

/// <summary>Región de un evento para UNA tarjeta del catálogo: códigos entre comas (<c>,US,EU,JP,</c>), de dónde sale y, si viene de una hoja, el hash del fichero. Solo metadatos derivados (ADR 0047).</summary>
public sealed record EventRegionRow(string EncounterKey, string Regions, string Source, string SourceRef, string? FileSha256);

public sealed class RegionStage
{
    public required IReadOnlyList<EventRegionRow> Rows { get; init; }
    public required IReadOnlyDictionary<string, object?> Stats { get; init; }
}

/// <summary>
/// Hito 5d (ADR 0047): región de distribución de los eventos, SOLO cuando una fuente la aporta de verdad. Dos fuentes y nada más:
/// (1) el CORE: las tarjetas de regalo de 3.ª generación vienen de tablas con nombre de región (<c>EncountersWC3.Japan</c>, <c>International</c>, <c>PCJP</c> = Pokémon Center de Japón, <c>PCNY</c> = Pokémon Center de Nueva York);
/// (2) la documentación de las tarjetas de EventsGallery: las columnas «US / EU / JP / TW / KO» de las hojas de seguimiento de WC6 y WC7 (<c>Extras/Tracking</c>), enlazadas por (tipo, id de tarjeta, especie).
/// Para el resto (Gen 4, Gen 5, WB7, WC8, WC9...) ni el Core ni la documentación dan región: queda vacía, nunca se deduce del idioma ni se inventa. Solo se guardan los códigos, la procedencia y el hash del fichero
/// (ningún byte ni texto de EventsGallery: ADR 0005; la base es un artefacto privado).
/// </summary>
public static class EventRegionImporter
{
    public const string Wc6File = EventTrackingImporter.Wc6File;
    public const string Wc7File = EventTrackingImporter.Wc7File;
    public static readonly string[] Codes = ["US", "EU", "JP", "KO", "TW", "INT"];

    private sealed record Card(string Key, string Type, int CardId, int Species, string DescriptorJson);

    public static RegionStage Stage(SourceClone clone, SqliteConnection c, Action<string>? log = null)
    {
        clone.VerifyPinned();
        var stats = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        var rows = new List<EventRegionRow>();

        var cards = new List<Card>();
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "SELECT encounter_key, json_extract(descriptor_json,'$.type'), COALESCE(json_extract(descriptor_json,'$.cardId'),0), species_id, descriptor_json FROM encounter WHERE kind='Event'";
            using var r = cmd.ExecuteReader();
            while (r.Read()) cards.Add(new Card(r.GetString(0), r.IsDBNull(1) ? string.Empty : r.GetString(1), r.GetInt32(2), r.GetInt32(3), r.GetString(4)));
        }

        // ---- (1) Core: tablas de 3.ª generación ----
        var gen3 = 0;
        foreach (var card in cards.Where(x => x.Type.StartsWith("EncounterGift3", StringComparison.Ordinal)))
            if (Gen3Region(card.Type, card.DescriptorJson) is { } g)
            {
                rows.Add(new EventRegionRow(card.Key, g.Regions, "core", g.Ref, null));
                gen3++;
            }
        stats["gen3_con_region_del_core"] = gen3;
        stats["gen3_sin_region_mezcla_de_tablas"] = cards.Count(x => x.Type.StartsWith("EncounterGift3", StringComparison.Ordinal)) - gen3;

        // ---- (2) EventsGallery: hojas de seguimiento ----
        var dir = Path.Combine(clone.Path, "Extras", "Tracking");
        var sha = new Dictionary<string, string>();
        var sheet = new List<(string Type, int WcId, string Species, string Regions, string File, string Sheet, int Row, string Sha)>();
        var wc6 = Path.Combine(dir, Wc6File);
        var wc7 = Path.Combine(dir, Wc7File);
        if (File.Exists(wc6)) sheet.AddRange(ReadWc6(File.ReadAllBytes(wc6), sha, stats));
        else log?.Invoke($"Regiones: falta {Wc6File}; sin regiones de WC6.");
        if (File.Exists(wc7)) sheet.AddRange(ReadWc7(File.ReadAllBytes(wc7), sha, stats));
        else log?.Invoke($"Regiones: falta {Wc7File}; sin regiones de WC7.");

        var species = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "SELECT species_id, name FROM species_name WHERE language_code='en'";
            using var r = cmd.ExecuteReader();
            while (r.Read()) species[EventTrackingImporter.Normalize(r.GetString(1))] = r.GetInt32(0);
        }
        var byKey = new Dictionary<(string, int, int), List<(string Regions, string File, string Sheet, int Row, string Sha)>>();
        var unknown = 0;
        foreach (var s in sheet)
        {
            if (!species.TryGetValue(EventTrackingImporter.Normalize(s.Species), out var sp)) { unknown++; continue; }
            if (!byKey.TryGetValue((s.Type, s.WcId, sp), out var list)) byKey[(s.Type, s.WcId, sp)] = list = [];
            list.Add((s.Regions, s.File, s.Sheet, s.Row, s.Sha));
        }
        int noId = 0, noRow = 0, linked = 0;
        foreach (var card in cards.Where(x => x.Type is "WC6" or "WC7"))
        {
            if (card.CardId == 0) { noId++; continue; }
            if (!byKey.TryGetValue((card.Type, card.CardId, card.Species), out var list)) { noRow++; continue; }
            // Unión de las filas que coinciden (variantes por región de la misma tarjeta): una región dada por cualquiera de ellas es real.
            var union = Codes.Where(code => list.Any(x => x.Regions.Contains("," + code + ",", StringComparison.Ordinal))).ToList();
            if (union.Count == 0) continue;
            var first = list.OrderBy(x => x.Row).First();
            rows.Add(new EventRegionRow(card.Key, "," + string.Join(',', union) + ",", "eventsgallery", $"eventsgallery@{clone.Commit12}:Extras/Tracking/{first.File}#{first.Sheet}!R{first.Row}", first.Sha));
            linked++;
        }
        stats["wc6_wc7_con_region_de_la_hoja"] = linked;
        stats["wc6_wc7_sin_id_de_tarjeta"] = noId;
        stats["wc6_wc7_sin_fila_en_la_hoja"] = noRow;
        stats["filas_hoja_con_especie_desconocida"] = unknown;
        foreach (var kv in sha) stats["sha256_" + kv.Key] = kv.Value;
        log?.Invoke($"Regiones: {rows.Count} tarjetas con región ({gen3} del Core, {linked} de las hojas de seguimiento).");
        stats["tarjetas_con_region"] = rows.Count;
        return new RegionStage { Rows = rows, Stats = stats };
    }

    /// <summary>
    /// Región de una tarjeta de 3.ª generación a partir de las tablas del Core de las que sale (el descriptor guarda los nombres de tabla). <c>null</c> si mezcla tablas de regiones distintas
    /// (o ninguna lleva región): entonces no se afirma ninguna.
    /// </summary>
    public static (string Regions, string Ref)? Gen3Region(string type, string descriptorJson)
    {
        switch (type)
        {
            case "EncounterGift3NY": return (",US,", "core:EncountersWC3.PCNY (Pokémon Center de Nueva York)");
            case "EncounterGift3JPN": return (",JP,", "core:EncountersWC3.PCJP (Pokémon Center de Japón)");
        }
        try
        {
            using var doc = JsonDocument.Parse(descriptorJson);
            if (!doc.RootElement.TryGetProperty("tables", out var tables) || tables.ValueKind != JsonValueKind.Object) return null;
            var names = tables.EnumerateObject().Select(p => p.Name).ToList();
            var japan = names.Contains("EncountersWC3.Japan");
            var intl = names.Contains("EncountersWC3.International");
            if (japan && !intl) return (",JP,", "core:EncountersWC3.Japan");
            if (intl && !japan) return (",INT,", "core:EncountersWC3.International");
        }
        catch (JsonException) { /* sin descriptor legible: sin región */ }
        return null;
    }

    private static IEnumerable<(string Type, int WcId, string Species, string Regions, string File, string Sheet, int Row, string Sha)> ReadWc6(byte[] file, Dictionary<string, string> sha, IDictionary<string, object?> stats)
    {
        var hash = Hashing.Sha256Hex(file);
        sha[Wc6File] = hash;
        var grid = Biff8Workbook.Open(file).ReadSheet("Events");
        var id = grid.Find("WC6 ID") ?? throw new InvalidDataException("Hoja Events sin columna «WC6 ID».");
        var sp = grid.Find("Species") ?? throw new InvalidDataException("Hoja Events sin columna «Species».");
        // Cabecera de regiones: US, EU, JP, TW, KO (columnas consecutivas; se ancla en «EU» y se comprueba el resto).
        var cols = RegionColumns(grid, id.Row, ["US", "EU", "JP", "TW", "KO"], anchor: "EU", anchorIndex: 1);
        var result = new List<(string, int, string, string, string, string, int, string)>();
        for (var r = id.Row + 1; r < grid.RowCount; r++)
        {
            if (grid.Number(r, id.Col) is not { } wc || wc <= 0) continue;
            var regions = FlagsToRegions(grid, r, cols);
            if (regions.Length == 0) continue;
            result.Add(("WC6", (int)wc, grid.Get(r, sp.Col), regions, Wc6File, "Events", r + 1, hash));
        }
        stats["wc6_filas_con_region"] = result.Count;
        return result;
    }

    private static IEnumerable<(string Type, int WcId, string Species, string Regions, string File, string Sheet, int Row, string Sha)> ReadWc7(byte[] file, Dictionary<string, string> sha, IDictionary<string, object?> stats)
    {
        var hash = Hashing.Sha256Hex(file);
        sha[Wc7File] = hash;
        var grid = XlsxWorkbook.ReadSheet(file, "Events");
        var id = grid.Find("WC7 ID") ?? throw new InvalidDataException("Hoja Events sin columna «WC7 ID».");
        var sp = grid.Find("Event and Card Color") ?? throw new InvalidDataException("Hoja Events sin columna «Event and Card Color».");
        // En esta hoja «US» aparece dos veces (Ultrasol y la región): se ancla en «EU» (única) y se comprueban sus vecinas.
        var cols = RegionColumns(grid, id.Row, ["US", "EU", "JP", "KO"], anchor: "EU", anchorIndex: 1);
        var result = new List<(string, int, string, string, string, string, int, string)>();
        for (var r = id.Row + 1; r < grid.RowCount; r++)
        {
            if (grid.Number(r, id.Col) is not { } wc || wc <= 0) continue;
            var regions = FlagsToRegions(grid, r, cols);
            if (regions.Length == 0) continue;
            result.Add(("WC7", (int)wc, grid.Get(r, sp.Col), regions, Wc7File, "Events", r + 1, hash));
        }
        stats["wc7_filas_con_region"] = result.Count;
        return result;
    }

    private static (string Code, int Col)[] RegionColumns(SheetGrid grid, int headerRow, string[] expected, string anchor, int anchorIndex)
    {
        int? anchorCol = null;
        for (var c = 0; c < 80 && anchorCol is null; c++)
            if (grid.Get(headerRow, c).Trim().Equals(anchor, StringComparison.OrdinalIgnoreCase)) anchorCol = c;
        if (anchorCol is not { } a) throw new InvalidDataException($"Hoja Events sin columna de región «{anchor}».");
        var first = a - anchorIndex;
        var cols = new (string, int)[expected.Length];
        for (var i = 0; i < expected.Length; i++)
        {
            var text = grid.Get(headerRow, first + i).Trim();
            if (!text.Equals(expected[i], StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException($"La cabecera de regiones cambió: se esperaba «{expected[i]}» en la columna {first + i} y hay «{text}».");
            cols[i] = (expected[i], first + i);
        }
        return cols;
    }

    private static string FlagsToRegions(SheetGrid grid, int row, (string Code, int Col)[] cols)
    {
        var on = cols.Where(x => grid.Number(row, x.Col) is > 0).Select(x => x.Code).ToList();
        return on.Count == 0 ? string.Empty : "," + string.Join(',', on) + ",";
    }

    public static void Persist(SqliteConnection c, RegionStage stage)
    {
        using var tx = c.BeginTransaction();
        try
        {
            using (var ins = new PreparedInsert(c, tx, "event_region", "encounter_key", "regions", "source", "source_ref", "file_sha256"))
                foreach (var r in stage.Rows)
                    ins.Run(r.EncounterKey, r.Regions, r.Source, r.SourceRef, r.FileSha256);
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }
}
