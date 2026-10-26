// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Catalog.Db;
using AutoLegalidad.Catalog.Model;
using Microsoft.Data.Sqlite;
using PKHeX.Core;

namespace AutoLegalidad.Catalog.Core;

/// <summary>Escribe un <see cref="CoreDataSet"/> en la base de datos, todo en UNA transaccion (o entra entero o no entra nada).</summary>
public static class CoreCatalogWriter
{
    public const string SourceId = "pkhex-core";

    public static void Write(SqliteConnection c, CoreDataSet ds, int schemaVersion)
    {
        using var tx = c.BeginTransaction();
        try
        {
            WriteInTransaction(c, tx, ds, schemaVersion);
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public static void WriteInTransaction(SqliteConnection c, SqliteTransaction tx, CoreDataSet ds, int schemaVersion)
    {
        var src = ds.Source;
        var now = Hashing.NowUtc();

        // 1) Fuente y version de fuente (manifest inmutable con los recuentos y el digest del contenido).
        using (var ins = new PreparedInsert(c, tx, "source", "source_id", "name", "url", "category", "license_note", "auto_import"))
            ins.Run(SourceId, "PKHeX.Core oficial (kwsch/PKHeX)", "https://github.com/kwsch/PKHeX", "OfficialCore",
                "GPL-3.0-or-later; compilado desde el commit fijado, sin cambios de legalidad", 0);
        var manifest = CanonicalJson.Serialize(new Dictionary<string, object?>
        {
            ["source"] = SourceId, ["tag"] = src.Tag, ["commit"] = src.Commit, ["core_dll_sha256"] = src.DllSha256,
            ["assembly_version"] = src.AssemblyVersion, ["informational_version"] = src.InformationalVersion,
            ["species"] = ds.Species.Count, ["forms"] = ds.Forms.Count, ["availability_rows"] = ds.Availability.Count,
            ["encounter_rows"] = ds.Encounters.Count, ["raw_encounter_items"] = ds.RawEncounterItems,
            ["mgdb_not_pokemon_skipped"] = ds.MgdbGiftsSkippedNotPokemon, ["mgdb_duplicates"] = ds.MgdbGiftsDuplicate,
            ["unresolved_encounters"] = ds.UnresolvedEncounters, ["unclassified_types"] = ds.UnclassifiedTypes.ToList<object?>(),
            ["content_digest"] = ds.ContentDigest,
        });
        using (var ins = new PreparedInsert(c, tx, "source_version", "source_version_id", "source_id", "revision", "retrieved_utc", "manifest_sha256", "manifest_json"))
            ins.Run(src.SourceVersionId, SourceId, src.Commit, now, Hashing.Sha256Hex(manifest), manifest);

        // 2) Release (pareja de versiones + perfil de reglas).
        var releaseManifest = CanonicalJson.Serialize(new Dictionary<string, object?>
        {
            ["core"] = new Dictionary<string, object?> { ["tag"] = src.Tag, ["commit"] = src.Commit, ["dll_sha256"] = src.DllSha256 },
            ["alm"] = new Dictionary<string, object?> { ["tag"] = src.AlmTag, ["commit"] = src.AlmCommit, ["integrated"] = false },
            ["policy"] = RuleProfile.PolicyVersion, ["rule_profile"] = RuleProfile.Text, ["hito"] = 1,
        });
        using (var ins = new PreparedInsert(c, tx, "release", "release_id", "core_version", "core_commit", "core_dll_sha256", "alm_version", "alm_commit",
                   "adapter_version", "policy_version", "protocol_version", "schema_version", "rule_profile_sha256", "manifest_json"))
            ins.Run(src.ReleaseId, src.Tag, src.Commit, src.DllSha256, src.AlmTag, src.AlmCommit, "none (Hito 1: sin adaptador)", RuleProfile.PolicyVersion, 1,
                schemaVersion, RuleProfile.Sha256, releaseManifest);

        // 3) Juegos concretos y grupos de versiones.
        using (var ins = new PreparedInsert(c, tx, "game", "game_id", "version_name", "display_name_es", "context", "save_runtime_type", "is_storage",
                   "is_transfer_origin_only", "is_group", "generation", "entity_format", "personal_table", "availability_basis"))
        {
            foreach (var g in GameCatalog.Concrete)
                ins.Run(g.Id, g.Version.ToString(), g.NameEs, g.Context.ToString(), g.SaveType, g.IsStorage ? 1 : 0, g.IsTransferOriginOnly ? 1 : 0, 0,
                    g.Generation, g.EntityFormat, g.PersonalTableName, g.Basis.ToString());
            foreach (var v in GameCatalog.Groups)
                ins.Run((int)v, v.ToString(), "Grupo de versiones " + v, string.Empty, null, 0, 0, 1, null, null, null, "Unknown");
        }

        // 4) Especies, nombres y formas.
        using (var sp = new PreparedInsert(c, tx, "species", "species_id", "canonical_name", "introduced_generation", "core_resource_sha256"))
        using (var sn = new PreparedInsert(c, tx, "species_name", "species_id", "language_code", "name"))
        {
            foreach (var s in ds.Species)
            {
                sp.Run(s.Id, s.CanonicalName, s.IntroducedGeneration, src.DllSha256);
                foreach (var (lang, name) in s.Names) sn.Run(s.Id, lang, name);
            }
        }
        using (var sf = new PreparedInsert(c, tx, "species_form", "species_id", "form_id", "canonical_form_name", "descriptor_json"))
            foreach (var f in ds.Forms) sf.Run(f.Species, f.Form, f.CanonicalFormName, f.DescriptorJson);

        // 5) Disponibilidad.
        using (var av = new PreparedInsert(c, tx, "availability", "release_id", "game_id", "species_id", "form_id", "state", "reason_ref", "form_storage"))
            foreach (var a in ds.Availability) av.Run(src.ReleaseId, a.Game, a.Species, a.Form, a.State, a.ReasonRef, a.FormStorage);

        // 6) Encuentros del Core + MGDB (las filas de encuentro de las demas fuentes las escribe cada importador con el mismo escritor).
        EncounterWriter.Write(c, tx, src.ReleaseId, ds.Encounters);
    }
}

/// <summary>Inserta filas de <c>encounter</c> y <c>encounter_game</c>; comun a todas las fuentes.</summary>
public static class EncounterWriter
{
    public static void Write(SqliteConnection c, SqliteTransaction tx, string releaseId, IEnumerable<EncounterRow> rows)
    {
        using var e = new PreparedInsert(c, tx, "encounter", "encounter_key", "release_id", "source_version_id", "artifact_sha256", "species_id", "form_id",
            "origin_game", "context", "runtime_type", "kind", "internal_id", "descriptor_sha256", "descriptor_json", "constraints_json", "source_ref", "approved_for_generation");
        using var g = new PreparedInsert(c, tx, "encounter_game", "encounter_key", "game_id");
        foreach (var r in rows)
        {
            e.Run(r.Key, releaseId, r.SourceVersionId, r.ArtifactSha256, r.Species, r.Form, r.OriginGame, r.Context, r.RuntimeType, r.Kind, r.InternalId,
                r.DescriptorSha256, r.DescriptorJson, r.ConstraintsJson, r.SourceRef, r.Approved ? 1 : 0);
            foreach (var game in r.Games.Distinct()) g.Run(r.Key, game);
        }
    }
}
