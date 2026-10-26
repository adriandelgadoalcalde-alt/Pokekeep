// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Globalization;
using System.Text.Json;
using AutoLegalidad.Catalog.Core;
using AutoLegalidad.Catalog.Db;
using AutoLegalidad.Catalog.Model;
using Microsoft.Data.Sqlite;
using PKHeX.Core;

namespace AutoLegalidad.Catalog.Sources;

public sealed record GoWindow(string Key, string EncounterKey, int Species, int Form, string? StartDay, string? EndDay, string EncounterType,
    string ShinyState, string? GenderRule, string ConstraintsJson, string SourceRef, string CoreMatch = "NotChecked", string? CoreRoute = null);

/// <summary>Instantanea en memoria de las ventanas de Pokemon GO de PoGoEncTool (solo DATOS: ningun codigo AGPL se copia ni se enlaza).</summary>
public sealed class GoStage
{
    public required SourceClone Clone { get; init; }
    public required string DataSha256 { get; init; }
    public required byte[] Payload { get; init; }
    public required IReadOnlyList<EncounterRow> Encounters { get; init; }
    public required IReadOnlyList<GoWindow> Windows { get; init; }
    public required string ManifestJson { get; init; }
    public string ManifestSha256 => Hashing.Sha256Hex(ManifestJson);
}

/// <summary>
/// Ventanas de GO de <c>PoGoEncTool/Resources/data.json</c> como DATOS con atribucion (Project Pokémon, AGPL-3.0). Se aprueban 0 para generar en el Hito 1:
/// el Core ya trae sus propias tablas GO y estas ventanas son investigacion hasta cruzarlas con el Core fijado (Hito 3).
/// </summary>
public static class PoGoWindowImporter
{
    public const string SourceId = "pogoenctool";
    public const string RelativePath = "Resources/data.json";

    public static GoStage Stage(SourceClone clone, HashSet<(int, int)> catalogForms, Action<string>? log = null)
    {
        clone.VerifyPinned();
        var file = System.IO.Path.Combine(clone.Path, "Resources", "data.json");
        var bytes = File.ReadAllBytes(file);
        var sha = Hashing.Sha256Hex(bytes);
        using var doc = JsonDocument.Parse(bytes);
        var data = doc.RootElement.GetProperty("Data");

        var encounters = new List<EncounterRow>();
        var windows = new List<GoWindow>();
        int formsOutside = 0, invalidRange = 0, noStart = 0, speciesRows = 0;
        var versionId = $"{SourceId}@{clone.Commit12}";
        var goGame = (int)GameVersion.GO;

        foreach (var entry in data.EnumerateArray())
        {
            speciesRows++;
            var species = entry.GetProperty("Species").GetInt32();
            var form = entry.TryGetProperty("Form", out var fe) ? fe.GetInt32() : 0;
            var available = entry.TryGetProperty("Available", out var av) && av.GetBoolean();
            if (!catalogForms.Contains((species, form))) { formsOutside++; continue; }
            var key = $"pogo:{species:D4}-{form}";
            var list = entry.TryGetProperty("Data", out var d) ? d.EnumerateArray().ToList() : [];
            var types = new SortedSet<string>(StringComparer.Ordinal);
            var idx = 0;
            var rows = new List<GoWindow>();
            foreach (var w in list)
            {
                var start = Day(w, "Start");
                var end = Day(w, "End");
                if (start is null) noStart++;
                if (start is not null && end is not null && string.CompareOrdinal(start, end) > 0) { invalidRange++; continue; }
                var type = w.TryGetProperty("Type", out var t) ? t.GetString() ?? "None" : "None";
                types.Add(type);
                var shiny = w.TryGetProperty("Shiny", out var sh) ? sh.GetString() : null;
                var gender = w.TryGetProperty("Gender", out var ge) ? ge.GetString() : null;
                var constraints = CanonicalJson.Serialize(new Dictionary<string, object?>
                {
                    ["minIV"] = Num(w, "MinIV"), ["minLevel"] = Num(w, "MinLevel"),
                    ["ballRestriction"] = w.TryGetProperty("BallRestriction", out var b) ? b.GetString() : null,
                    ["localizedStart"] = Bool(w, "LocalizedStart"), ["hasEndTolerance"] = Bool(w, "HasEndTolerance"),
                    ["isGigantamax"] = Bool(w, "IsGigantamax"), ["isFeaturedGOWildArea"] = Bool(w, "IsFeaturedGOWildArea"),
                    ["isAlwaysSpecialTrade"] = Bool(w, "IsAlwaysSpecialTrade"),
                    ["comment"] = w.TryGetProperty("Comment", out var cm) ? cm.GetString() : null,
                });
                rows.Add(new GoWindow($"{key}:{idx++:D3}", key, species, form, start, end, type,
                    shiny switch { "Never" => "Never", "Always" => "Always", _ => "Possible" },
                    gender switch { "MaleOnly" => "MaleOnly", "FemaleOnly" => "FemaleOnly", _ => null }, constraints, $"{versionId}:{RelativePath}#{species}-{form}"));
            }
            if (rows.Count == 0) continue;
            windows.AddRange(rows);
            var desc = CanonicalJson.Serialize(new Dictionary<string, object?>
            {
                ["kind"] = EncounterKinds.GO, ["species"] = species, ["form"] = form, ["available"] = available, ["windows"] = rows.Count,
                ["types"] = types.ToList<object?>(), ["file_sha256"] = sha,
            });
            encounters.Add(new EncounterRow(key, versionId, sha, species, form, goGame, "None", "GO-import", EncounterKinds.GO, null,
                Hashing.Sha256Hex(desc), desc, CanonicalJson.Serialize(new Dictionary<string, object?> { ["windows"] = rows.Count }),
                $"{versionId}:{RelativePath}#{species}-{form}", false, [goGame]));
        }

        // Cruce con las tablas GO del Core fijado (Hito 3): el Core es la autoridad; lo que no reproduce queda PoGoOnly y no se usa para generar.
        var cross = GoCrossCheck.Compare(windows, GoCrossCheck.ReadCore());
        windows = [.. windows.Select(w => w with { CoreMatch = cross.ByWindow[w.Key].Match.ToString(), CoreRoute = cross.ByWindow[w.Key].CoreRoute })];
        var manifest = CanonicalJson.Serialize(new Dictionary<string, object?>
        {
            ["source"] = SourceId, ["commit"] = clone.Commit, ["license"] = clone.License, ["file"] = RelativePath, ["file_sha256"] = sha,
            ["core_cross"] = new Dictionary<string, object?>
            {
                ["windows_identical"] = windows.Count(w => w.CoreMatch == "Identical"), ["windows_dates_off_by_one_day"] = windows.Count(w => w.CoreMatch == "DatesOffByOneDay"),
                ["windows_pogo_only"] = windows.Count(w => w.CoreMatch == "PoGoOnly"), ["core_slots_total"] = cross.CoreSlotsTotal, ["core_slots_home"] = cross.CoreSlotsHome,
                ["core_slots_lgpe"] = cross.CoreSlotsLgpe, ["core_slots_without_window"] = cross.CoreOnly,
                ["core_without_window_by_type"] = cross.CoreOnlyByType.ToDictionary(kv => kv.Key, kv => (object?)kv.Value),
                ["pogo_only_by_type"] = cross.PoGoOnlyByType.ToDictionary(kv => kv.Key, kv => (object?)kv.Value),
            },
            ["attribution"] = "Project Pokémon - PoGoEncTool (AGPL-3.0); solo datos, ningun codigo copiado",
            ["species_rows"] = speciesRows, ["encounters"] = encounters.Count, ["windows"] = windows.Count,
            ["forms_not_in_catalog_skipped"] = formsOutside, ["windows_invalid_range_skipped"] = invalidRange, ["windows_without_start"] = noStart,
            ["approved_for_generation"] = 0,
        });
        log?.Invoke($"PoGoEncTool: {speciesRows} especies en data.json, {encounters.Count} (especie,forma) con ventanas, {windows.Count} ventanas.");
        return new GoStage { Clone = clone, DataSha256 = sha, Payload = bytes, Encounters = encounters, Windows = windows, ManifestJson = manifest };
    }

    private static string? Day(JsonElement w, string name)
    {
        if (!w.TryGetProperty(name, out var e)) return null;
        var s = e.GetString();
        return s is not null && DateTime.TryParseExact(s, "yyyy.MM.dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)
            ? dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : null;
    }

    private static object? Num(JsonElement w, string n) => w.TryGetProperty(n, out var e) && e.ValueKind == JsonValueKind.Number ? e.GetInt32() : null;
    private static bool Bool(JsonElement w, string n) => w.TryGetProperty(n, out var e) && e.ValueKind == JsonValueKind.True;

    public static void Persist(SqliteConnection c, string releaseId, GoStage stage)
    {
        using var tx = c.BeginTransaction();
        try
        {
            PersistInTransaction(c, tx, releaseId, stage);
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public static void PersistInTransaction(SqliteConnection c, SqliteTransaction tx, string releaseId, GoStage stage)
    {
        var versionId = $"{SourceId}@{stage.Clone.Commit12}";
        using (var ins = new PreparedInsert(c, tx, "source", "source_id", "name", "url", "category", "license_note", "auto_import"))
            ins.Run(SourceId, "Project Pokémon - PoGoEncTool (datos)", "https://github.com/projectpokemon/PoGoEncTool", "GOResearch",
                "AGPL-3.0: solo se usan sus DATOS (data.json), con atribucion; no se copia codigo. Los bytes no se versionan en Git (ADR 0005)", 0);
        using (var ins = new PreparedInsert(c, tx, "source_version", "source_version_id", "source_id", "revision", "retrieved_utc", "manifest_sha256", "manifest_json"))
            ins.Run(versionId, SourceId, stage.Clone.Commit, Hashing.NowUtc(), stage.ManifestSha256, stage.ManifestJson);
        using (var art = new PreparedInsert(c, tx, "artifact", "artifact_sha256", "format", "byte_length", "payload"))
            art.Run(stage.DataSha256, "json", stage.Payload.Length, stage.Payload);
        using (var org = new PreparedInsert(c, tx, "artifact_origin", "artifact_sha256", "source_version_id", "relative_path", "release_state", "evidence_class",
                   "approved_for_generation", "evidence_ref", "approval_note", "parse_status", "core_corroboration", "allowlist_rule_id"))
            org.Run(stage.DataSha256, versionId, RelativePath, "Published", "ResearchOnly", 0,
                $"{versionId}:{RelativePath}", "Datos comunitarios de GO: investigacion hasta cruzarlos con las tablas GO del Core fijado (Hito 3); no aprobados para generar", "Parsed", "NotApplicable", "GO-DATA");

        EncounterWriter.Write(c, tx, releaseId, stage.Encounters);
        using var w = new PreparedInsert(c, tx, "pogo_window", "window_key", "encounter_key", "species_id", "form_id", "start_day", "end_day", "encounter_type",
            "shiny_state", "gender_rule", "import_route", "constraints_json", "source_ref", "core_match", "core_route");
        foreach (var x in stage.Windows)
            w.Run(x.Key, x.EncounterKey, x.Species, x.Form, x.StartDay, x.EndDay, x.EncounterType, x.ShinyState, x.GenderRule,
                x.CoreRoute is null ? "no reproducida por el Core fijado (PoGoOnly): no se usa para generar" : "Core:" + x.CoreRoute, x.ConstraintsJson, x.SourceRef, x.CoreMatch, x.CoreRoute);
    }
}
