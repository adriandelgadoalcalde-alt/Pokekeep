// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Ui;
using System.Text;
using System.Text.Json;
using AutoLegalidad.Catalog.Db;
using AutoLegalidad.Catalog.Model;
using Microsoft.Data.Sqlite;

namespace AutoLegalidad.Catalog.Library;

/// <summary>Versiones del motor que el catálogo no guarda (las da el Worker): se rellenan si el motor respondió; si no, se dice «no disponible».</summary>
public sealed record SheetEngineVersions(string EngineVersion, string AdapterVersion, string AlmVersion, string AlmCommit, string CoreVersion, string CoreCommit, string CoreDllSha256);

/// <summary>
/// «Exportar ficha…» de la Librería (Hito 5d, ADR 0047): la ficha de UNA entrada del catálogo como JSON canónico (y texto legible), pensada para compartir o adjuntar a un informe.
/// Lleva SOLO datos del catálogo (especie, forma, juego, origen y encuentro, clave, motivo de no generable, región y procedencia con commit y hash de la fuente) y las versiones de Core, ALM, motor y base de datos.
/// NO lleva nada del usuario: ni su partida, ni su entrenador, ni sus ajustes, ni rutas (ni la de la base de datos ni la del fichero que se guarda), ni el nombre del equipo o de la cuenta.
/// </summary>
public static class LibrarySheet
{
    public const string Format = "pokekeep-ficha";
    public const int FormatVersion = 1;

    /// <param name="row">La fila TAL COMO la ve la Librería (ya resuelta: GameCube, crías sin huevo...): el motivo de «no generable» es el mismo que el de la lista.</param>
    /// <param name="engine">Versiones que da el Worker, o null si no respondió.</param>
    public static IDictionary<string, object?> Build(SqliteConnection c, LibraryRow row, SheetEngineVersions? engine)
    {
        var detail = LibraryQueries.Detail(c, row.EntryKey);
        JsonElement? d = detail is null ? null : JsonDocument.Parse(detail).RootElement.Clone();
        JsonElement? Get(string name) => d is { } e && e.TryGetProperty(name, out var v) && v.ValueKind != JsonValueKind.Null ? v.Clone() : null;

        var entry = new Dictionary<string, object?>
        {
            ["entry_key"] = row.EntryKey,
            ["game"] = new Dictionary<string, object?> { ["id"] = row.GameId, ["name"] = row.GameName },
            ["species"] = new Dictionary<string, object?>
            {
                ["id"] = row.SpeciesId, ["name_es"] = row.NameEs, ["pokedex_number"] = row.DexNumber, ["form_id"] = row.FormId, ["form_name"] = row.FormName,
            },
            ["origin"] = new Dictionary<string, object?> { ["kind"] = row.Kind, ["variant"] = row.Variant, ["encounter_key"] = row.EncounterKey },
            ["available_in_game"] = row.Available,
            ["generable"] = row.Addable,
            ["needs_home"] = row.HomeRequired,
            ["not_generable_reason"] = row.Addable ? null : row.DimReason,
            ["not_generable_reason_source"] = row.Addable ? null : row.DimSource,
            ["coverage"] = Get("coverage"),
        };

        Dictionary<string, object?>? ev = null;
        if (row.Kind == "Event")
        {
            ev = new Dictionary<string, object?>
            {
                // 0.6.6 (B8, decision 7 del propietario): ningun titulo de EventsGallery (ni el original ni el castellano que sale de el) sale de la base de datos hacia un fichero; se usa la descripcion
                // generica que ya enseña la interfaz cuando no hay titulo. La tarjeta se identifica por su clave y su fecha.
                ["title"] = AppIdioma.Ingles ? $"{SpeciesEn(row)} · event in {row.GameName}" : $"{row.NameEs} · evento en {row.GameName}",
                ["language"] = row.EventLanguage,
                ["distribution_start"] = row.EventStartDate,
                ["shiny_rule"] = row.ShinyRule,
                ["fixed_gender"] = row.FixedGender,
                ["gigantamax"] = row.Gigantamax,
                ["region"] = string.IsNullOrWhiteSpace(row.EventRegion) ? null : new Dictionary<string, object?>
                {
                    ["codes"] = row.EventRegion.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList<object?>(),
                    ["source"] = row.EventRegionSource,
                },
                ["region_note"] = string.IsNullOrWhiteSpace(row.EventRegion) ? AppIdioma.T("sin dato: ni PKHeX ni la documentación de la tarjeta indican la región", "no data: neither PKHeX nor the card’s documentation gives the region") : null,
            };
        }

        var versions = new Dictionary<string, object?>
        {
            ["sheet_format"] = new Dictionary<string, object?> { ["name"] = Format, ["version"] = FormatVersion },
            ["catalog"] = CatalogVersions(c),
            ["engine"] = engine is null
                ? new Dictionary<string, object?> { ["available"] = false, ["note"] = AppIdioma.T("el motor no respondió: solo constan las versiones guardadas en el catálogo", "the engine did not respond: only the versions stored in the catalog are known") }
                : new Dictionary<string, object?>
                {
                    ["available"] = true, ["engine_version"] = engine.EngineVersion, ["adapter_version"] = engine.AdapterVersion,
                    ["core_version"] = engine.CoreVersion, ["core_commit"] = engine.CoreCommit, ["core_dll_sha256"] = engine.CoreDllSha256,
                    ["alm_version"] = engine.AlmVersion, ["alm_commit"] = engine.AlmCommit,
                },
        };

        return new Dictionary<string, object?>
        {
            ["format"] = Format,
            ["format_version"] = FormatVersion,
            ["entry"] = entry,
            ["event"] = ev,
            ["encounter"] = Get("encounter") is { } enc ? WithoutTitles(enc) : null,   // 0.6.6 (B8): el descriptor del encuentro trae el titulo de la tarjeta (cardTitle): no sale a la ficha
            ["traits"] = Get("traits"),
            ["provenance"] = Get("provenance"),
            ["source_label"] = row.SourceLabel,
            ["versions"] = versions,
        };
    }

    /// <summary>Copia del JSON sin ninguna propiedad cuyo nombre contenga «title» (<c>cardTitle</c>, <c>cardTitleEs</c>...): los titulos de EventsGallery no salen de la base de datos hacia un fichero.</summary>
    private static object? WithoutTitles(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Object => e.EnumerateObject().Where(p => !p.Name.Contains("title", StringComparison.OrdinalIgnoreCase)).ToDictionary(p => p.Name, p => WithoutTitles(p.Value)),
        JsonValueKind.Array => e.EnumerateArray().Select(WithoutTitles).ToList(),
        _ => e.Clone(),
    };

    private static Dictionary<string, object?> CatalogVersions(SqliteConnection c)
    {
        var result = new Dictionary<string, object?>();
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "SELECT release_id, core_version, core_commit, core_dll_sha256, alm_version, alm_commit, policy_version, protocol_version, schema_version, rule_profile_sha256 FROM release LIMIT 1";
            using var r = cmd.ExecuteReader();
            if (r.Read())
            {
                result["release_id"] = r.GetString(0);
                result["core_version"] = r.GetString(1);
                result["core_commit"] = r.GetString(2);
                result["core_dll_sha256"] = r.GetString(3);
                result["alm_version"] = r.GetString(4);
                result["alm_commit"] = r.GetString(5);
                result["policy_version"] = r.GetString(6);
                result["protocol_version"] = r.GetInt32(7);
                result["schema_version_of_release"] = r.GetInt32(8);
                result["rule_profile_sha256"] = r.GetString(9);
            }
        }
        result["database_schema_version"] = CatalogDatabase.Count(c, "SELECT COALESCE(MAX(version),0) FROM schema_version");
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "SELECT snapshot_id FROM dataset_snapshot WHERE state='Verified' ORDER BY rowid DESC LIMIT 1";
            result["snapshot_id"] = cmd.ExecuteScalar() as string;
        }
        var sources = new List<object?>();
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "SELECT source_id, revision FROM source_version ORDER BY source_id";
            using var r = cmd.ExecuteReader();
            while (r.Read()) sources.Add(new Dictionary<string, object?> { ["source"] = r.GetString(0), ["revision"] = r.GetString(1) });
        }
        result["sources"] = sources;
        return result;
    }

    private static object? V(IDictionary<string, object?> d, string key) => d.TryGetValue(key, out var v) ? v : null;

    public static string ToJson(IDictionary<string, object?> sheet) => CanonicalJson.SerializeIndented(sheet);

    /// <summary>Versión legible (castellano) de la misma ficha: lo mismo que el JSON, en líneas.</summary>
    private static string? SpeciesEn(int species)
    {
        try { var n = PKHeX.Core.GameInfo.GetStrings("en").Species; return species >= 0 && species < n.Count ? n[species] : null; }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return null; }
    }

    private static string SpeciesEn(LibraryRow row) => SpeciesEn(row.SpeciesId) ?? row.NameEs;

    public static string ToText(IDictionary<string, object?> sheet)
    {
        var sb = new StringBuilder();
        var entry = (IDictionary<string, object?>)sheet["entry"]!;
        var species = (IDictionary<string, object?>)entry["species"]!;
        var game = (IDictionary<string, object?>)entry["game"]!;
        var origin = (IDictionary<string, object?>)entry["origin"]!;
        sb.AppendLine(AppIdioma.T("Ficha de la Librería de Pokekeep", "Pokekeep Library summary"));
        sb.AppendLine(AppIdioma.F("Pokémon: {0}{1} · n.º {2}", "Pokémon: {0}{1} · No. {2}", AppIdioma.Ingles ? SpeciesEn((int)species["id"]!) ?? species["name_es"] : species["name_es"], species["form_name"] is string f && f.Length > 0 && (int)species["form_id"]! != 0 ? $" ({f})" : string.Empty, species["pokedex_number"]));
        sb.AppendLine(AppIdioma.F("Juego: {0}", "Game: {0}", game["name"]));
        sb.AppendLine(AppIdioma.F("Origen: {0}{1}", "Origin: {0}{1}", origin["kind"], origin["variant"] is string v && v != "direct" ? $" ({v})" : string.Empty));
        sb.AppendLine(AppIdioma.F("Clave de la entrada: {0}", "Entry key: {0}", entry["entry_key"]));
        if (origin["encounter_key"] is string ek) sb.AppendLine(AppIdioma.F("Clave del encuentro: {0}", "Encounter key: {0}", ek));
        sb.AppendLine(AppIdioma.F("Existe en el juego: {0}", "Exists in the game: {0}", (bool)entry["available_in_game"]! ? AppIdioma.T("sí", "yes") : AppIdioma.T("no", "no")));
        sb.AppendLine(AppIdioma.F("Se puede generar: {0}{1}", "Can be generated: {0}{1}", (bool)entry["generable"]! ? AppIdioma.T("sí", "yes") : AppIdioma.T("no", "no"), (bool)entry["needs_home"]! ? AppIdioma.T(" (solo pasando por HOME)", " (only by passing through HOME)") : string.Empty));
        if (entry["not_generable_reason"] is string why) sb.AppendLine(AppIdioma.F("Motivo de que no se genere: {0}", "Reason it isn’t generated: {0}", why));
        if (sheet["event"] is IDictionary<string, object?> ev)
        {
            sb.AppendLine();
            sb.AppendLine(AppIdioma.T("Evento", "Event"));
            if (ev["title"] is string t) sb.AppendLine(AppIdioma.F("  Descripción: {0}", "  Description: {0}", t));
            if (ev["language"] is string l) sb.AppendLine(AppIdioma.F("  Idioma: {0}", "  Language: {0}", l));
            if (ev["distribution_start"] is string s) sb.AppendLine(AppIdioma.F("  Inicio de la distribución: {0}", "  Distribution start: {0}", s));
            if (ev["shiny_rule"] is string sh) sb.AppendLine(AppIdioma.F("  Variocolor: {0}", "  Shiny: {0}", sh));
            if (ev["region"] is IDictionary<string, object?> reg) sb.AppendLine(AppIdioma.F("  Región: {0} (fuente: {1})", "  Region: {0} (source: {1})", string.Join(", ", ((IEnumerable<object?>)reg["codes"]!).Select(x => x?.ToString())), reg["source"]));
            else sb.AppendLine(AppIdioma.F("  Región: {0}", "  Region: {0}", ev["region_note"]));
        }
        if (sheet["provenance"] is JsonElement prov && prov.ValueKind == JsonValueKind.Object)
        {
            sb.AppendLine();
            sb.AppendLine(AppIdioma.T("Procedencia", "Provenance"));
            foreach (var p in prov.EnumerateObject()) sb.AppendLine($"  {p.Name}: {p.Value}");
        }
        if (sheet["traits"] is JsonElement tr && tr.ValueKind == JsonValueKind.Object)
        {
            sb.AppendLine();
            foreach (var p in tr.EnumerateObject()) sb.AppendLine(AppIdioma.F("Rasgos {0}: {1}", "Traits {0}: {1}", p.Name == "fixed" ? AppIdioma.T("fijos", "fixed") : AppIdioma.T("variables", "variable"), string.Join(", ", p.Value.EnumerateArray().Select(x => x.ToString()))));
        }
        sb.AppendLine();
        sb.AppendLine(AppIdioma.T("Versiones", "Versions"));
        var versions = (IDictionary<string, object?>)sheet["versions"]!;
        var cat = (IDictionary<string, object?>)versions["catalog"]!;
        sb.AppendLine(AppIdioma.F("  Base de datos: esquema {0}, instantánea {1}", "  Database: schema {0}, snapshot {1}", V(cat, "database_schema_version"), V(cat, "snapshot_id")));
        sb.AppendLine(AppIdioma.F("  Core (catálogo): {0} ({1})", "  Core (catalog): {0} ({1})", V(cat, "core_version"), V(cat, "core_commit")));
        sb.AppendLine(AppIdioma.F("  ALM (catálogo): {0} ({1})", "  ALM (catalog): {0} ({1})", V(cat, "alm_version"), V(cat, "alm_commit")));
        if (versions["engine"] is IDictionary<string, object?> eng && (bool)eng["available"]!)
        {
            sb.AppendLine(AppIdioma.F("  Motor: {0} · adaptador {1}", "  Engine: {0} · adapter {1}", eng["engine_version"], eng["adapter_version"]));
            sb.AppendLine(AppIdioma.F("  Core (motor): {0} ({1})", "  Core (engine): {0} ({1})", eng["core_version"], eng["core_commit"]));
            sb.AppendLine(AppIdioma.F("  ALM (motor): {0} ({1})", "  ALM (engine): {0} ({1})", eng["alm_version"], eng["alm_commit"]));
        }
        else sb.AppendLine(AppIdioma.T("  Motor: no disponible (no respondió al pedir sus versiones)", "  Engine: not available (it did not respond when asked for its versions)"));
        return sb.ToString();
    }
}
