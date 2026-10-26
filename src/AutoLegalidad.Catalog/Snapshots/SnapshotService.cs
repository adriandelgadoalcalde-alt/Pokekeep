// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Security.Cryptography;
using System.Text;
using AutoLegalidad.Catalog.Core;
using AutoLegalidad.Catalog.Db;
using AutoLegalidad.Catalog.Model;
using Microsoft.Data.Sqlite;

namespace AutoLegalidad.Catalog.Snapshots;

public sealed record SnapshotInfo(string Id, string ManifestSha256, string ContentDigest, bool CreatedNew);

/// <summary>
/// Snapshot inmutable del conjunto de datos (PLAN-TECNICO.md 8): se crea en <c>Staging</c>, se comprueba y pasa a <c>Verified</c>; el anterior queda
/// <c>Retired</c> (se conserva para rollback). Los disparadores de la migracion 0002 impiden modificar lo que un snapshot ya congelo.
/// </summary>
public static class SnapshotService
{
    /// <summary>Digest canonico del contenido ligado a un conjunto de <c>source_version</c>: manifiestos, artefactos, encuentros y ventanas GO.</summary>
    public static string ComputeContentDigest(SqliteConnection c, IReadOnlyList<string> sourceVersionIds)
    {
        var ids = sourceVersionIds.OrderBy(x => x, StringComparer.Ordinal).ToList();
        using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        void Line(string s) => h.AppendData(Encoding.UTF8.GetBytes(s + "\n"));
        var inList = string.Join(',', ids.Select((_, i) => "$v" + i));

        SqliteCommand Cmd(string sql)
        {
            var cmd = c.CreateCommand();
            cmd.CommandText = sql.Replace("{IN}", inList, StringComparison.Ordinal);
            for (var i = 0; i < ids.Count; i++) cmd.Parameters.AddWithValue("$v" + i, ids[i]);
            return cmd;
        }

        using (var cmd = Cmd("SELECT source_version_id, manifest_sha256 FROM source_version WHERE source_version_id IN ({IN}) ORDER BY source_version_id"))
        using (var r = cmd.ExecuteReader())
            while (r.Read()) Line($"SV|{r.GetString(0)}|{r.GetString(1)}");

        using (var cmd = Cmd("""
            SELECT source_version_id, relative_path, artifact_sha256, release_state, evidence_class, approved_for_generation, parse_status, core_corroboration, COALESCE(allowlist_rule_id,'')
              FROM artifact_origin WHERE source_version_id IN ({IN}) ORDER BY source_version_id, relative_path, artifact_sha256
            """))
        using (var r = cmd.ExecuteReader())
            while (r.Read()) Line($"AO|{r.GetString(0)}|{r.GetString(1)}|{r.GetString(2)}|{r.GetString(3)}|{r.GetString(4)}|{r.GetInt32(5)}|{r.GetString(6)}|{r.GetString(7)}|{r.GetString(8)}");

        using (var cmd = Cmd("""
            SELECT e.encounter_key, e.source_version_id, COALESCE(e.artifact_sha256,''), e.species_id, e.form_id, e.origin_game, e.kind, e.descriptor_sha256, e.approved_for_generation,
                   COALESCE((SELECT group_concat(game_id) FROM (SELECT game_id FROM encounter_game g WHERE g.encounter_key = e.encounter_key ORDER BY game_id)), '')
              FROM encounter e WHERE e.source_version_id IN ({IN}) ORDER BY e.encounter_key
            """))
        using (var r = cmd.ExecuteReader())
            while (r.Read()) Line($"EN|{r.GetString(0)}|{r.GetString(1)}|{r.GetString(2)}|{r.GetInt32(3)}|{r.GetInt32(4)}|{r.GetInt32(5)}|{r.GetString(6)}|{r.GetString(7)}|{r.GetInt32(8)}|{r.GetString(9)}");

        using (var cmd = Cmd("""
            SELECT w.window_key, w.encounter_key, COALESCE(w.start_day,''), COALESCE(w.end_day,''), w.encounter_type, w.shiny_state, COALESCE(w.gender_rule,''), w.constraints_json
              FROM pogo_window w JOIN encounter e ON e.encounter_key = w.encounter_key
             WHERE e.source_version_id IN ({IN}) ORDER BY w.window_key
            """))
        using (var r = cmd.ExecuteReader())
            while (r.Read()) Line($"PW|{r.GetString(0)}|{r.GetString(1)}|{r.GetString(2)}|{r.GetString(3)}|{r.GetString(4)}|{r.GetString(5)}|{r.GetString(6)}|{r.GetString(7)}");

        return Convert.ToHexString(h.GetHashAndReset());
    }

    public static string ComputeSnapshotId(IReadOnlyList<string> sourceVersionIds, string manifestSha, string contentDigest)
        => "snap-" + Hashing.Sha256Hex(string.Join('|', sourceVersionIds.OrderBy(x => x, StringComparer.Ordinal)) + "|" + manifestSha + "|" + contentDigest)[..16];

    /// <summary>Crea (y deja <c>Verified</c>) el snapshot de las fuentes dadas. Idempotente: el mismo contenido da el mismo id.</summary>
    public static SnapshotInfo Create(SqliteConnection c, IReadOnlyList<string> sourceVersionIds, string releaseId, string coreDllSha256, string? allowlistSha256)
    {
        using var tx = c.BeginTransaction();
        try
        {
            var digest = ComputeContentDigest(c, sourceVersionIds);
            var manifest = BuildManifest(c, sourceVersionIds, releaseId, coreDllSha256, allowlistSha256, digest);
            var manifestSha = Hashing.Sha256Hex(manifest);
            var id = ComputeSnapshotId(sourceVersionIds, manifestSha, digest);

            var exists = CatalogDatabase.Count(c, $"SELECT count(*) FROM dataset_snapshot WHERE snapshot_id = '{id}'", tx) > 0;
            if (exists)
            {
                tx.Rollback();
                return new SnapshotInfo(id, manifestSha, digest, false);
            }

            using (var ins = new PreparedInsert(c, tx, "dataset_snapshot", "snapshot_id", "created_utc", "manifest_sha256", "manifest_json", "state"))
                ins.Run(id, Hashing.NowUtc(), manifestSha, manifest, "Staging");
            using (var ins = new PreparedInsert(c, tx, "snapshot_source", "snapshot_id", "source_version_id"))
                foreach (var sv in sourceVersionIds) ins.Run(id, sv);

            // Comprobacion final antes de congelar: el contenido recalculado tiene que coincidir con el manifest.
            if (!string.Equals(ComputeContentDigest(c, sourceVersionIds), digest, StringComparison.Ordinal))
                throw new InvalidOperationException("El contenido cambio mientras se creaba el snapshot.");

            CatalogDatabase.Exec(c, "UPDATE dataset_snapshot SET state='Retired' WHERE state='Verified'", tx);
            CatalogDatabase.Exec(c, $"UPDATE dataset_snapshot SET state='Verified' WHERE snapshot_id='{id}'", tx);
            tx.Commit();
            return new SnapshotInfo(id, manifestSha, digest, true);
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    private static string BuildManifest(SqliteConnection c, IReadOnlyList<string> ids, string releaseId, string coreDllSha, string? allowlistSha, string digest)
    {
        var sources = new List<object?>();
        foreach (var id in ids.OrderBy(x => x, StringComparer.Ordinal))
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT source_id, revision, manifest_sha256 FROM source_version WHERE source_version_id=$id";
            cmd.Parameters.AddWithValue("$id", id);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) throw new InvalidOperationException($"source_version desconocido: {id}");
            sources.Add(new Dictionary<string, object?> { ["id"] = id, ["source"] = r.GetString(0), ["revision"] = r.GetString(1), ["manifest_sha256"] = r.GetString(2) });
        }
        return CanonicalJson.Serialize(new Dictionary<string, object?>
        {
            ["release_id"] = releaseId, ["core_dll_sha256"] = coreDllSha, ["rule_profile_sha256"] = RuleProfile.Sha256, ["policy"] = RuleProfile.PolicyVersion,
            ["allowlist_sha256"] = allowlistSha, ["sources"] = sources, ["content_digest"] = digest,
        });
    }

    /// <summary>Vuelve a poner <c>Verified</c> el snapshot anterior (rollback de datos): el activo pasa a <c>Retired</c>. Atomico.</summary>
    public static string Rollback(SqliteConnection c)
    {
        using var tx = c.BeginTransaction();
        try
        {
            var active = CatalogDatabase.Scalar(c, "SELECT snapshot_id FROM dataset_snapshot WHERE state='Verified'", tx);
            var previous = CatalogDatabase.Scalar(c, "SELECT snapshot_id FROM dataset_snapshot WHERE state='Retired' ORDER BY created_utc DESC, rowid DESC LIMIT 1", tx);
            if (active.Length == 0 || previous.Length == 0)
                throw new InvalidOperationException("No hay un snapshot anterior al que volver.");
            CatalogDatabase.Exec(c, $"UPDATE dataset_snapshot SET state='Retired' WHERE snapshot_id='{active}'", tx);
            CatalogDatabase.Exec(c, $"UPDATE dataset_snapshot SET state='Verified' WHERE snapshot_id='{previous}'", tx);
            tx.Commit();
            return previous;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public static string? ActiveSnapshotId(SqliteConnection c)
        => CatalogDatabase.Scalar(c, "SELECT snapshot_id FROM dataset_snapshot WHERE state='Verified'") is { Length: > 0 } s ? s : null;

    /// <summary>
    /// ¿Sigue valiendo el snapshot para las entradas ACTUALES? (invalidacion): un cambio de Core, de allowlist, de commit de una fuente o de perfil de reglas
    /// lo invalida; el motor (Hito 2) no debe generar con un snapshot invalidado.
    /// </summary>
    public static IReadOnlyList<string> StaleReasons(SqliteConnection c, string snapshotId, string currentCoreDllSha256, string? currentAllowlistSha256,
        IReadOnlyDictionary<string, string> currentSourceRevisions)
    {
        var reasons = new List<string>();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT manifest_json FROM dataset_snapshot WHERE snapshot_id=$id";
        cmd.Parameters.AddWithValue("$id", snapshotId);
        var json = cmd.ExecuteScalar() as string ?? throw new InvalidOperationException("Snapshot inexistente.");
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.GetProperty("core_dll_sha256").GetString() != currentCoreDllSha256) reasons.Add("Core: el PKHeX.Core.dll cambio");
        if (root.GetProperty("rule_profile_sha256").GetString() != RuleProfile.Sha256) reasons.Add("Perfil de reglas: cambio");
        if (root.TryGetProperty("allowlist_sha256", out var al) && al.ValueKind == System.Text.Json.JsonValueKind.String && al.GetString() != currentAllowlistSha256)
            reasons.Add("Allowlist: cambio");
        foreach (var s in root.GetProperty("sources").EnumerateArray())
        {
            var source = s.GetProperty("source").GetString()!;
            if (currentSourceRevisions.TryGetValue(source, out var rev) && rev != s.GetProperty("revision").GetString())
                reasons.Add($"Fuente {source}: el commit cambio");
        }
        return reasons;
    }
}
