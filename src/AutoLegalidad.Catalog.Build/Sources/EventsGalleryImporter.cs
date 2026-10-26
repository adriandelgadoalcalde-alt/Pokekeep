// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Text;
using AutoLegalidad.Catalog.Core;
using AutoLegalidad.Catalog.Db;
using AutoLegalidad.Catalog.Model;
using Microsoft.Data.Sqlite;
using PKHeX.Core;

namespace AutoLegalidad.Catalog.Sources;

/// <summary>Resultado de procesar UN fichero permitido de EventsGallery (sin los bytes, que viajan aparte).</summary>
public sealed record GalleryFile(
    string RelativePath, string Sha256, long Length, string Format, string RuleId, string ReleaseState, string EvidenceClass,
    string ParseStatus, string ParsedType, int Species, int Form, string Corroboration, string? CoreKey, bool Approved, string EvidenceRef, string ApprovalNote);

/// <summary>Instantanea en memoria de todo lo que se importaria de EventsGallery: se construye SIN tocar la base de datos.</summary>
public sealed class GalleryStage
{
    public required SourceClone Clone { get; init; }
    public required Allowlist Allowlist { get; init; }
    public required IReadOnlyList<GalleryFile> Files { get; init; }
    public required IReadOnlyDictionary<string, byte[]> Payloads { get; init; }
    public required IReadOnlyList<EncounterRow> NewEncounters { get; init; }
    public required IReadOnlyDictionary<string, int> ExcludedByRule { get; init; }
    public required int TotalFilesSeen { get; init; }
    public required string ManifestJson { get; init; }
    public string ManifestSha256 => Hashing.Sha256Hex(ManifestJson);
}

/// <summary>
/// Importador de EventsGallery con allowlist (PLAN-TECNICO.md 8): staging -> hashes -> formato -> clasificacion -> parseo oficial ->
/// manifest -> transaccion. Los bytes NUNCA salen de aqui hacia Git: solo a la base de datos local del paquete privado (ADR 0005).
/// </summary>
public static class EventsGalleryImporter
{
    public const string SourceId = "eventsgallery";

    /// <summary>Fases 1-6 (todo en memoria): lee solo lo que la allowlist permite, calcula hashes, clasifica, parsea con el Core y arma el manifest.</summary>
    public static GalleryStage Stage(SourceClone clone, Allowlist allowlist, HashSet<(int, int)> catalogForms, Action<string>? log = null, DateOnly? dateNotAfter = null)
    {
        clone.VerifyPinned();
        var root = clone.Path;
        var coreMgdb = CoreMgdbIndex.BySha;

        var excluded = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var files = new List<GalleryFile>();
        var payloads = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var newEnc = new List<EncounterRow>();
        var seenEnc = new HashSet<string>(StringComparer.Ordinal);
        var total = 0;

        foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var rel = System.IO.Path.GetRelativePath(root, path).Replace('\\', '/');
            if (rel.StartsWith(".git/", StringComparison.Ordinal) || rel is ".gitignore" or ".gitattributes" or "README.md")
                continue;
            total++;
            var rule = allowlist.Resolve(rel);
            if (rule is null) { Bump(excluded, "NotInAllowlist"); continue; }
            if (rule.Action == "exclude") { Bump(excluded, rule.Id); continue; }

            // ---- staging: lectura (solo ficheros permitidos) + hash ----
            var data = File.ReadAllBytes(path);
            var sha = Hashing.Sha256Hex(data);
            var ext = System.IO.Path.GetExtension(rel).ToLowerInvariant();

            // ---- clasificacion (regla + sobrescrituras por nombre de fichero) ----
            var fileName = System.IO.Path.GetFileName(rel);
            var cls = rule.EvidenceClass!;
            var approveWanted = rule.Approve;
            var classNote = string.Empty;
            foreach (var o in rule.FilenameOverrides ?? [])
                if (o.IsMatch(fileName))
                {
                    cls = o.EvidenceClass;
                    approveWanted = o.Approve ?? rule.Approve;
                    classNote = " [" + o.Rationale + "]";
                    break;
                }
            if (cls == "AuthenticDump")
                foreach (var o in allowlist.GlobalOverrides)
                    if (o.IsMatch(rel))
                    {
                        cls = o.EvidenceClass;
                        approveWanted = o.Approve ?? rule.Approve;
                        classNote = " [" + o.Rationale + "]";
                        break;
                    }

            // ---- parseo oficial con el Core ----
            var parse = Parse(rule.Parse, data, ext);
            var corroboration = "NotChecked";
            string? coreKey = null;
            if (parse.Status == "Parsed" && rule.Parse == ParseMode.MysteryGift)
            {
                corroboration = coreMgdb.TryGetValue(parse.GiftSha256!, out var k) ? "ByteIdenticalInCoreMgdb" : "NotInCoreMgdb";
                coreKey = corroboration == "ByteIdenticalInCoreMgdb" ? k : null;
            }
            else if (parse.Status is "Parsed" or "NotAPokemonTemplate" && rule.Parse == ParseMode.Entity)
                corroboration = "NotApplicable";
            else if (rule.Parse == ParseMode.None)
                corroboration = "NotApplicable";

            // ---- aprobacion: regla 8 (el esquema, los disparadores y esta logica coinciden) ----
            var approved = false;
            var why = "no se solicita aprobacion para generar";
            if (approveWanted)
            {
                if (rule.ReleaseState != "Published") why = "no publicado";
                else if (parse.Status != "Parsed") why = $"parseo oficial: {parse.Status}";
                else if (!catalogForms.Contains((parse.Species, parse.Form))) why = "especie/forma fuera del catalogo del Core";
                else if (cls == "DocumentedReconstruction" && corroboration != "ByteIdenticalInCoreMgdb") why = "reconstruccion sin corroboracion byte a byte del Core";
                else { approved = true; why = "cumple Published + clase " + cls + " + parseo oficial" + (corroboration == "ByteIdenticalInCoreMgdb" ? " + identica a una tarjeta del Core" : string.Empty); }
            }
            else if (cls is "ResearchOnly" or "Unknown")
                why = $"clase {cls}: excluida de generar (regla 8)";

            var evidenceRef = (rule.EvidenceRef ?? rule.Rationale) + $" | {SourceId}@{clone.Commit12}:{rel}";
            var note = (approved ? (rule.ApprovalNote ?? string.Empty) + "; " : string.Empty) + why + classNote;
            files.Add(new GalleryFile(rel, sha, data.Length, ext.TrimStart('.'), rule.Id, rule.ReleaseState!, cls, parse.Status, parse.TypeName,
                parse.Species, parse.Form, corroboration, coreKey, approved, evidenceRef, note));
            payloads.TryAdd(sha, data);

            // ---- encuentro nuevo: solo tarjetas aprobadas que el Core NO conoce ya (las identicas ya son filas core-mgdb) ----
            if (approved && rule.Parse == ParseMode.MysteryGift && corroboration == "NotInCoreMgdb" && parse.Gift is { } gift)
            {
                var key = $"{SourceId}:{parse.TypeName}:{parse.GiftSha256![..16]}";
                if (seenEnc.Add(key))
                    newEnc.Add(MakeEncounter(clone, key, sha, gift, parse, rel, dateNotAfter));
            }
        }

        var manifest = BuildManifest(clone, allowlist, files, excluded, total, newEnc.Count);
        log?.Invoke($"EventsGallery: {total} ficheros vistos, {files.Count} permitidos por la allowlist, {files.Count(f => f.Approved)} aprobados para generar.");
        return new GalleryStage
        {
            Clone = clone, Allowlist = allowlist, Files = files, Payloads = payloads, NewEncounters = newEnc,
            ExcludedByRule = excluded, TotalFilesSeen = total, ManifestJson = manifest,
        };
    }

    private static void Bump(SortedDictionary<string, int> d, string k) => d[k] = d.GetValueOrDefault(k) + 1;

    private sealed record ParseResult(string Status, string TypeName, int Species, int Form, string? GiftSha256, DataMysteryGift? Gift);

    private static ParseResult Parse(ParseMode mode, byte[] data, string ext)
    {
        try
        {
            switch (mode)
            {
                case ParseMode.MysteryGift:
                {
                    if (!MysteryGift.IsMysteryGift(data.Length)) return new ParseResult("ParseFailed", string.Empty, 0, 0, null, null);
                    var g = MysteryGift.GetMysteryGift((byte[])data.Clone(), ext); // copia: el Core descifra/normaliza IN SITU y no debe tocar los bytes que se hashean y guardan
                    if (g is null) return new ParseResult("ParseFailed", string.Empty, 0, 0, null, null);
                    if (!g.IsEntity || g.Species == 0)
                        return new ParseResult("NotAPokemonTemplate", g.GetType().Name, 0, 0, CoreMgdbIndex.StableSha256(g), null);
                    return new ParseResult("Parsed", g.GetType().Name, g.Species, g.Form, CoreMgdbIndex.StableSha256(g), g);
                }
                case ParseMode.Entity:
                {
                    var pk = EntityFormat.GetFromBytes((byte[])data.Clone()); // copia: ver arriba
                    if (pk is null || pk.Species == 0 || pk.Species > 1025) return new ParseResult("ParseFailed", string.Empty, 0, 0, null, null);
                    return new ParseResult("Parsed", pk.GetType().Name, pk.Species, pk.Form, null, null);
                }
                default:
                    return new ParseResult("NoOfficialParser", string.Empty, 0, 0, null, null);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new ParseResult("ParseFailed", string.Empty, 0, 0, null, null);
        }
    }

    private static EncounterRow MakeEncounter(SourceClone clone, string key, string fileSha, DataMysteryGift gift, ParseResult p, string rel, DateOnly? dateNotAfter)
    {
        var games = CoreDataSet.ResolveGames(gift.Version, gift.Context);
        var d = GiftInfo.Describe(gift, p.TypeName, p.GiftSha256!, dateNotAfter);
        d["file_sha256"] = fileSha;
        d["relative_path"] = rel;
        var desc = CanonicalJson.Serialize(d);
        return new EncounterRow(key, $"{SourceId}@{clone.Commit12}", fileSha, gift.Species, gift.Form, CoreDataSet.OriginGame(gift.Version, gift.Generation),
            gift.Context.ToString(), RuntimeTypes.For(gift.Context, gift.Generation, gift.Version), EncounterKinds.Event,
            gift.CardID > 0 ? gift.CardID.ToString(System.Globalization.CultureInfo.InvariantCulture) : null,
            Hashing.Sha256Hex(desc), desc,
            CanonicalJson.Serialize(new Dictionary<string, object?> { ["levelMin"] = (int)gift.LevelMin, ["levelMax"] = (int)gift.LevelMax, ["isEgg"] = gift.IsEgg, ["generation"] = (int)gift.Generation }),
            $"{SourceId}@{clone.Commit12}:{rel}", true, games.Select(g => g.Id).ToList());
    }

    private static string BuildManifest(SourceClone clone, Allowlist allowlist, List<GalleryFile> files, SortedDictionary<string, int> excluded, int total, int newEncounters)
    {
        var sb = new StringBuilder();
        foreach (var f in files.OrderBy(f => f.RelativePath, StringComparer.Ordinal))
            sb.Append(f.RelativePath).Append('|').Append(f.Sha256).Append('|').Append(f.RuleId).Append('|').Append(f.EvidenceClass).Append('|').Append(f.Approved ? 1 : 0).Append('\n');
        var byRule = files.GroupBy(f => f.RuleId).OrderBy(g => g.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => (object?)new Dictionary<string, object?> { ["files"] = g.Count(), ["approved"] = g.Count(f => f.Approved) });
        object? Group(Func<GalleryFile, string> key) => files.GroupBy(key).OrderBy(g => g.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => (object?)g.Count());
        return CanonicalJson.Serialize(new Dictionary<string, object?>
        {
            ["source"] = SourceId, ["commit"] = clone.Commit, ["license"] = clone.License, ["allowlist_sha256"] = allowlist.Sha256,
            ["files_seen"] = total, ["files_imported"] = files.Count, ["files_approved"] = files.Count(f => f.Approved),
            ["distinct_artifacts"] = files.Select(f => f.Sha256).Distinct().Count(),
            ["new_encounters_not_in_core"] = newEncounters,
            ["excluded_by_rule"] = excluded.ToDictionary(k => k.Key, k => (object?)k.Value),
            ["by_rule"] = byRule, ["by_class"] = Group(f => f.EvidenceClass), ["by_parse_status"] = Group(f => f.ParseStatus),
            ["by_corroboration"] = Group(f => f.Corroboration),
            ["files_digest"] = Hashing.Sha256Hex(sb.ToString()),
        });
    }

    /// <summary>Fase 7: transaccion unica. Si algo falla (clave foranea, CHECK, disparador), no queda NADA de esta fuente en la base.</summary>
    public static void Persist(SqliteConnection c, string releaseId, GalleryStage stage)
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

    public static void PersistInTransaction(SqliteConnection c, SqliteTransaction tx, string releaseId, GalleryStage stage)
    {
        var versionId = $"{SourceId}@{stage.Clone.Commit12}";
        using (var ins = new PreparedInsert(c, tx, "source", "source_id", "name", "url", "category", "license_note", "auto_import"))
            ins.Run(SourceId, "Project Pokémon - EventsGallery", "https://github.com/projectpokemon/EventsGallery", "PublishedEvents",
                "Sin licencia declarada: los bytes NO se versionan en Git; solo van en el paquete privado, con atribucion a Project Pokémon (ADR 0005)", 0);
        using (var ins = new PreparedInsert(c, tx, "source_version", "source_version_id", "source_id", "revision", "retrieved_utc", "manifest_sha256", "manifest_json"))
            ins.Run(versionId, SourceId, stage.Clone.Commit, Hashing.NowUtc(), stage.ManifestSha256, stage.ManifestJson);

        using (var art = new PreparedInsert(c, tx, "artifact", "artifact_sha256", "format", "byte_length", "payload"))
        {
            var done = new HashSet<string>(StringComparer.Ordinal);
            foreach (var f in stage.Files)
                if (done.Add(f.Sha256))
                    art.Run(f.Sha256, f.Format, f.Length, stage.Payloads[f.Sha256]);
        }
        using (var org = new PreparedInsert(c, tx, "artifact_origin", "artifact_sha256", "source_version_id", "relative_path", "release_state", "evidence_class",
                   "approved_for_generation", "evidence_ref", "approval_note", "parse_status", "core_corroboration", "allowlist_rule_id"))
        {
            foreach (var f in stage.Files)
                org.Run(f.Sha256, versionId, f.RelativePath, f.ReleaseState, f.EvidenceClass, f.Approved ? 1 : 0,
                    f.EvidenceRef, f.ApprovalNote, f.ParseStatus, f.Corroboration, f.RuleId);
        }
        EncounterWriter.Write(c, tx, releaseId, stage.NewEncounters);
    }

    /// <summary>Manifiesto versionable en Git: SOLO rutas, hashes y metadatos de procedencia (nunca los bytes). TSV ordenado y reproducible.</summary>
    public static string ToManifestTsv(GalleryStage stage)
    {
        var sb = new StringBuilder();
        sb.Append("# EventsGallery @ ").Append(stage.Clone.Commit).Append(" | allowlist_sha256=").Append(stage.Allowlist.Sha256).Append('\n');
        sb.Append("# Solo hashes y metadatos de procedencia; los ficheros NO se redistribuyen (ADR 0005).\n");
        sb.Append("relative_path\tsha256\tbytes\tformat\trule\tevidence_class\tapproved\tparse\tcore_corroboration\tcore_key\n");
        foreach (var f in stage.Files.OrderBy(f => f.RelativePath, StringComparer.Ordinal))
            sb.Append(f.RelativePath).Append('\t').Append(f.Sha256).Append('\t').Append(f.Length).Append('\t').Append(f.Format).Append('\t').Append(f.RuleId).Append('\t')
              .Append(f.EvidenceClass).Append('\t').Append(f.Approved ? 1 : 0).Append('\t').Append(f.ParseStatus).Append('\t').Append(f.Corroboration).Append('\t')
              .Append(f.CoreKey ?? string.Empty).Append('\n');
        return sb.ToString();
    }
}
