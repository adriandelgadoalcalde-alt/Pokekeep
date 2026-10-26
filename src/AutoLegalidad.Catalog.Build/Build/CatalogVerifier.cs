// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Security.Cryptography;
using System.Text;
using AutoLegalidad.Catalog.Core;
using AutoLegalidad.Catalog.Db;
using AutoLegalidad.Catalog.Model;
using AutoLegalidad.Catalog.Snapshots;
using Microsoft.Data.Sqlite;

namespace AutoLegalidad.Catalog.Build;

public sealed record GateCheck(string Name, bool Ok, string Detail);

public sealed record GateResult(IReadOnlyList<GateCheck> Checks)
{
    public bool Passed => Checks.All(c => c.Ok);
}

public sealed record VerifyResult(bool Ok, IReadOnlyList<string> Problems, GateResult Gate)
{
    public string ToText()
    {
        var sb = new StringBuilder();
        sb.AppendLine(Ok ? "VERIFICACION: OK" : "VERIFICACION: FALLO");
        foreach (var p in Problems) sb.AppendLine("  - " + p);
        foreach (var g in Gate.Checks) sb.AppendLine($"  [{(g.Ok ? "OK" : "FALLO")}] {g.Name}: {g.Detail}");
        return sb.ToString();
    }
}

/// <summary>Verificacion de integridad del catalogo y gate del Hito 1 (calculado SIEMPRE sobre la base de datos real, nunca sobre contadores del importador).</summary>
public static class CatalogVerifier
{
    public static VerifyResult Verify(SqliteConnection c)
    {
        var problems = new List<string>();

        try { new Migrator(c).VerifyIntegrity(); }
        catch (MigrationTamperedException ex) { problems.Add(ex.Message); }

        var integrity = CatalogDatabase.Scalar(c, "PRAGMA integrity_check;");
        if (integrity != "ok") problems.Add("integrity_check: " + integrity);
        var fk = CatalogDatabase.Count(c, "SELECT count(*) FROM pragma_foreign_key_check");
        if (fk != 0) problems.Add($"{fk} violaciones de clave foranea");

        var active = SnapshotService.ActiveSnapshotId(c);
        if (active is null) problems.Add("No hay un snapshot Verified activo.");
        else problems.AddRange(VerifySnapshot(c, active));

        return new VerifyResult(problems.Count == 0, problems, Gate(c, active, problems.Count == 0));
    }

    /// <summary>Recalcula todo lo que el snapshot declara y lo compara: manifiestos, contenido, hashes de artefactos y digest del Core.</summary>
    public static IReadOnlyList<string> VerifySnapshot(SqliteConnection c, string snapshotId)
    {
        var problems = new List<string>();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT manifest_sha256, manifest_json, state FROM dataset_snapshot WHERE snapshot_id=$id";
        cmd.Parameters.AddWithValue("$id", snapshotId);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return [$"Snapshot {snapshotId} inexistente."];
        var manifestSha = r.GetString(0);
        var manifestJson = r.GetString(1);
        r.Close();

        if (!string.Equals(Hashing.Sha256Hex(manifestJson), manifestSha, StringComparison.Ordinal))
            problems.Add("El manifest del snapshot no coincide con su SHA-256.");

        var ids = new List<string>();
        using (var q = c.CreateCommand())
        {
            q.CommandText = "SELECT source_version_id FROM snapshot_source WHERE snapshot_id=$id ORDER BY 1";
            q.Parameters.AddWithValue("$id", snapshotId);
            using var rr = q.ExecuteReader();
            while (rr.Read()) ids.Add(rr.GetString(0));
        }
        using var doc = System.Text.Json.JsonDocument.Parse(manifestJson);
        var declared = doc.RootElement.GetProperty("content_digest").GetString();
        var actual = SnapshotService.ComputeContentDigest(c, ids);
        if (declared != actual) problems.Add("El contenido del snapshot cambio respecto a su manifest (content_digest).");
        if (SnapshotService.ComputeSnapshotId(ids, manifestSha, actual) != snapshotId) problems.Add("El id del snapshot no corresponde a su contenido.");

        // manifiesto de cada fuente
        using (var q = c.CreateCommand())
        {
            q.CommandText = "SELECT source_version_id, manifest_sha256, manifest_json FROM source_version";
            using var rr = q.ExecuteReader();
            while (rr.Read())
                if (Hashing.Sha256Hex(rr.GetString(2)) != rr.GetString(1))
                    problems.Add($"El manifest de {rr.GetString(0)} no coincide con su SHA-256.");
        }

        // bytes de cada artefacto = su clave
        using (var q = c.CreateCommand())
        {
            q.CommandText = "SELECT artifact_sha256, payload FROM artifact";
            using var rr = q.ExecuteReader();
            var bad = 0;
            while (rr.Read())
            {
                var data = (byte[])rr.GetValue(1);
                if (!string.Equals(Convert.ToHexString(SHA256.HashData(data)), rr.GetString(0), StringComparison.OrdinalIgnoreCase)) bad++;
            }
            if (bad != 0) problems.Add($"{bad} artefactos cuyo contenido no coincide con su SHA-256.");
        }

        // digest del Core recalculado desde las tablas
        problems.AddRange(VerifyCoreDigest(c));
        return problems;
    }

    public static IReadOnlyList<string> VerifyCoreDigest(SqliteConnection c)
    {
        string? declared = null;
        using (var q = c.CreateCommand())
        {
            q.CommandText = "SELECT source_version_id, manifest_json FROM source_version WHERE source_id='pkhex-core'";
            using var rr = q.ExecuteReader();
            if (!rr.Read()) return ["No hay fuente pkhex-core."];
            using var doc = System.Text.Json.JsonDocument.Parse(rr.GetString(1));
            declared = doc.RootElement.GetProperty("content_digest").GetString();
        }
        var recomputed = CoreDataSet.Digest(ReadSpecies(c), ReadForms(c), ReadAvailability(c), ReadCoreEncounters(c));
        return declared == recomputed ? [] : ["El contenido del Core (especies, formas, disponibilidad, encuentros) no coincide con su digest."];
    }

    private static List<SpeciesRow> ReadSpecies(SqliteConnection c)
    {
        var names = new Dictionary<int, List<(string, string)>>();
        using (var q = c.CreateCommand())
        {
            q.CommandText = "SELECT species_id, language_code, name FROM species_name";
            using var r = q.ExecuteReader();
            while (r.Read())
            {
                var id = r.GetInt32(0);
                if (!names.TryGetValue(id, out var l)) names[id] = l = [];
                l.Add((r.GetString(1), r.GetString(2)));
            }
        }
        var res = new List<SpeciesRow>();
        using var q2 = c.CreateCommand();
        q2.CommandText = "SELECT species_id, canonical_name, introduced_generation FROM species";
        using var r2 = q2.ExecuteReader();
        while (r2.Read())
            res.Add(new SpeciesRow(r2.GetInt32(0), r2.GetString(1), r2.IsDBNull(2) ? null : r2.GetInt32(2), names.GetValueOrDefault(r2.GetInt32(0)) ?? []));
        return res;
    }

    private static List<FormRow> ReadForms(SqliteConnection c)
    {
        var res = new List<FormRow>();
        using var q = c.CreateCommand();
        q.CommandText = "SELECT species_id, form_id, canonical_form_name, descriptor_json FROM species_form";
        using var r = q.ExecuteReader();
        while (r.Read()) res.Add(new FormRow(r.GetInt32(0), r.GetInt32(1), r.IsDBNull(2) ? null : r.GetString(2), r.GetString(3)));
        return res;
    }

    private static List<AvailabilityRow> ReadAvailability(SqliteConnection c)
    {
        var res = new List<AvailabilityRow>();
        using var q = c.CreateCommand();
        q.CommandText = "SELECT game_id, species_id, form_id, state, reason_ref, form_storage FROM availability";
        using var r = q.ExecuteReader();
        while (r.Read()) res.Add(new AvailabilityRow(r.GetInt32(0), r.GetInt32(1), r.GetInt32(2), r.GetString(3), r.GetString(4), r.GetString(5)));
        return res;
    }

    private static List<EncounterRow> ReadCoreEncounters(SqliteConnection c)
    {
        var games = new Dictionary<string, List<int>>();
        using (var q = c.CreateCommand())
        {
            q.CommandText = "SELECT g.encounter_key, g.game_id FROM encounter_game g JOIN encounter e ON e.encounter_key=g.encounter_key JOIN source_version v ON v.source_version_id=e.source_version_id WHERE v.source_id='pkhex-core'";
            using var r = q.ExecuteReader();
            while (r.Read())
            {
                var k = r.GetString(0);
                if (!games.TryGetValue(k, out var l)) games[k] = l = [];
                l.Add(r.GetInt32(1));
            }
        }
        var res = new List<EncounterRow>();
        using var q2 = c.CreateCommand();
        q2.CommandText = "SELECT e.encounter_key, e.descriptor_sha256 FROM encounter e JOIN source_version v ON v.source_version_id=e.source_version_id WHERE v.source_id='pkhex-core'";
        using var r2 = q2.ExecuteReader();
        while (r2.Read())
        {
            var k = r2.GetString(0);
            res.Add(new EncounterRow(k, string.Empty, null, 0, 0, 0, string.Empty, string.Empty, string.Empty, null, r2.GetString(1), string.Empty, string.Empty, string.Empty, true,
                games.GetValueOrDefault(k) ?? []));
        }
        return res;
    }

    /// <summary>Gate del Hito 1: 0 Unknown/ResearchOnly/Unreleased usados para generar; 1025 IDs sin duplicados; disponibilidad trazable.</summary>
    public static GateResult Gate(SqliteConnection c, string? activeSnapshot, bool integrityOk)
    {
        var checks = new List<GateCheck>();
        long N(string sql) => CatalogDatabase.Count(c, sql);

        var badOrigins = N("SELECT count(*) FROM artifact_origin WHERE approved_for_generation=1 AND (evidence_class IN ('Unknown','ResearchOnly') OR release_state IN ('Unreleased','Unknown'))");
        var badGenerable = N("SELECT count(*) FROM v_artifact_generable WHERE evidence_class IN ('Unknown','ResearchOnly') OR release_state<>'Published'");
        var badEncounters = N("""
            SELECT count(*) FROM v_encounter_generable e JOIN artifact_origin a ON a.artifact_sha256=e.artifact_sha256 AND a.source_version_id=e.source_version_id
             WHERE a.approved_for_generation=0 OR a.evidence_class IN ('Unknown','ResearchOnly') OR a.release_state<>'Published'
            """);
        var approvedEncNoArtifactApproval = N("""
            SELECT count(*) FROM encounter e WHERE e.approved_for_generation=1 AND e.artifact_sha256 IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM v_artifact_generable a WHERE a.artifact_sha256=e.artifact_sha256 AND a.source_version_id=e.source_version_id)
            """);
        checks.Add(new GateCheck("0 Unknown/ResearchOnly/Unreleased usados para generar",
            badOrigins + badGenerable + badEncounters + approvedEncNoArtifactApproval == 0,
            $"artefactos aprobados indebidos={badOrigins}; en vista generable={badGenerable}; encuentros generables con artefacto no aprobado={badEncounters}+{approvedEncNoArtifactApproval}"));

        var species = N("SELECT count(*) FROM species");
        var distinct = N("SELECT count(DISTINCT species_id) FROM species");
        var min = N("SELECT COALESCE(MIN(species_id),0) FROM species");
        var max = N("SELECT COALESCE(MAX(species_id),0) FROM species");
        var dupForms = N("SELECT count(*) FROM (SELECT species_id, form_id FROM species_form GROUP BY 1,2 HAVING count(*)>1)");
        var noForm0 = N("SELECT count(*) FROM species s WHERE NOT EXISTS (SELECT 1 FROM species_form f WHERE f.species_id=s.species_id AND f.form_id=0)");
        checks.Add(new GateCheck("1025 IDs de especie sin duplicados", species == 1025 && distinct == 1025 && min == 1 && max == 1025 && dupForms == 0 && noForm0 == 0,
            $"especies={species}, distintas={distinct}, rango={min}..{max}, formas duplicadas={dupForms}, especies sin forma 0={noForm0}"));

        var concrete = N("SELECT count(*) FROM game WHERE is_group=0");
        var forms = N("SELECT count(*) FROM species_form");
        var rows = N("SELECT count(*) FROM availability");
        var untraceable = N("SELECT count(*) FROM availability WHERE length(trim(reason_ref))=0 OR (state<>'Unknown' AND reason_ref NOT LIKE 'Core:%')");
        var unknownWithoutReason = N("SELECT count(*) FROM availability WHERE state='Unknown' AND reason_ref NOT LIKE 'Core %'");
        var releases = N("SELECT count(*) FROM release");
        var invalidState = N("SELECT count(*) FROM availability WHERE state NOT IN ('Present','Absent','Unknown')");
        checks.Add(new GateCheck("Disponibilidad por juego/forma trazable al Core",
            rows == concrete * forms * releases && untraceable == 0 && unknownWithoutReason == 0 && invalidState == 0,
            $"filas={rows} (juegos concretos {concrete} x formas {forms} x releases {releases}); sin trazabilidad={untraceable}; Unknown sin motivo={unknownWithoutReason}"));

        var libEntries = N("SELECT count(*) FROM library_entry");
        var covCases = N("SELECT count(*) FROM coverage_case");
        var libNoAnchor = N("SELECT count(*) FROM library_entry WHERE addable=1 AND kind<>'Egg' AND (encounter_key IS NULL OR encounter_key NOT IN (SELECT encounter_key FROM v_encounter_generable))");
        var libBadKind = N("SELECT count(*) FROM library_entry WHERE addable=1 AND kind IN ('Absent','FormNormalization','Transfer')");
        var libNoReason = N("SELECT count(*) FROM library_entry WHERE availability='Dimmed' AND (dim_reason IS NULL OR length(trim(dim_reason))=0)");
        checks.Add(new GateCheck("Libreria: solo se ofrece lo generable y lo atenuado explica su motivo",
            libEntries == covCases && libNoAnchor == 0 && libBadKind == 0 && libNoReason == 0,
            $"entradas={libEntries} (tuplas de cobertura={covCases}); ofrecibles sin ancla generable={libNoAnchor}; ofrecibles de clase no generable={libBadKind}; atenuadas sin motivo={libNoReason}"));

        checks.Add(new GateCheck("Snapshot Verified activo e integro", activeSnapshot is not null && integrityOk,
            activeSnapshot is null ? "sin snapshot activo" : $"{activeSnapshot}; integridad={(integrityOk ? "ok" : "FALLO")}"));
        return new GateResult(checks);
    }
}
