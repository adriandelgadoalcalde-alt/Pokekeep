// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using AutoLegalidad.Catalog.Db;
using Microsoft.Data.Sqlite;
using PKHeX.Core;

namespace AutoLegalidad.Catalog.Sources;

public sealed record CorpusReport(long FilesFound, long DistinctHashes, long DuplicateFiles, long TotalBytes, long Unreadable,
    IReadOnlyDictionary<string, (long Files, long Distinct)> ByExtension, string OutputDb)
{
    public string ToText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Inventario del corpus local (SOLO hashes; ningun contenido ni dato de entrenador se lee ni se guarda)");
        sb.AppendLine($"  ficheros de entidades/tarjetas encontrados: {FilesFound}");
        sb.AppendLine($"  hashes distintos (contenido unico): {DistinctHashes}; ficheros duplicados: {DuplicateFiles}");
        sb.AppendLine($"  bytes totales: {TotalBytes}; ilegibles: {Unreadable}");
        foreach (var (ext, v) in ByExtension.OrderByDescending(k => k.Value.Files).ThenBy(k => k.Key, StringComparer.Ordinal))
            sb.AppendLine($"    .{ext}: {v.Files} ficheros, {v.Distinct} distintos");
        return sb.ToString();
    }
}

/// <summary>
/// Inventario del corpus local del usuario (PLAN-TECNICO.md 11: «no considerar los 73.046 archivos como 73.046 casos buenos»). Como MAXIMO un inventario con hashes:
/// no se parsean ni se leen sus campos (OT/TID/SID/nombres), no se importan como verdad y NO entran en la base del catalogo ni en el paquete:
/// van a una base local aparte (<c>datos/corpus-local.sqlite</c>, ignorada por Git). Mejora sobre el plan (ADR 0007): mantiene el catalogo reproducible.
/// </summary>
public static class CorpusInventory
{
    public static readonly IReadOnlySet<string> Extensions = BuildExtensions();

    private static HashSet<string> BuildExtensions()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in EntityFileExtension.GetExtensions()) set.Add(e);
        foreach (var e in new[] { "pkm", "xdpkm", "pk3m", "wc3", "me3", "wc4", "pcd", "pgt", "pgf", "wc5full", "wc6", "wc6full", "wc7", "wc7full", "wb7", "wb7full", "wr7", "wc8", "wb8", "wa8", "wc9", "wa9" })
            set.Add(e);
        return set;
    }

    public static CorpusReport Scan(string corpusDir, string outputDb, IReadOnlyList<string> excludeDirNames, Action<string>? log = null)
    {
        var root = Path.GetFullPath(corpusDir);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
        var exclude = new HashSet<string>(excludeDirNames, StringComparer.OrdinalIgnoreCase) { ".git" };
        var found = new List<string>();
        Walk(root, exclude, found);
        log?.Invoke($"Inventario: {found.Count} ficheros candidatos; calculando hashes...");

        var results = new ConcurrentBag<(string Rel, string Sha, long Len, string Ext)>();
        long unreadable = 0;
        Parallel.ForEach(found, new ParallelOptions { MaxDegreeOfParallelism = 4 }, f =>
        {
            try
            {
                using var fs = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16, FileOptions.SequentialScan);
                var sha = Convert.ToHexString(SHA256.HashData(fs));
                results.Add((Path.GetRelativePath(root, f), sha, new FileInfo(f).Length, Path.GetExtension(f).TrimStart('.').ToLowerInvariant()));
            }
            catch (IOException) { Interlocked.Increment(ref unreadable); }
            catch (UnauthorizedAccessException) { Interlocked.Increment(ref unreadable); }
        });

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputDb))!);
        foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
            if (File.Exists(outputDb + suffix)) File.Delete(outputDb + suffix);
        using var c = CatalogDatabase.Open(outputDb);
        CatalogDatabase.ExecScript(c, """
            CREATE TABLE corpus_file (rel_path TEXT PRIMARY KEY, sha256 TEXT NOT NULL CHECK(length(sha256)=64), byte_length INTEGER NOT NULL, extension TEXT NOT NULL) STRICT;
            CREATE INDEX ix_corpus_sha ON corpus_file(sha256);
            CREATE TABLE corpus_scan (scanned_utc TEXT NOT NULL, files INTEGER NOT NULL, distinct_hashes INTEGER NOT NULL, unreadable INTEGER NOT NULL) STRICT;
            """);
        var list = results.OrderBy(r => r.Rel, StringComparer.Ordinal).ToList();
        using (var tx = c.BeginTransaction())
        {
            using var ins = new PreparedInsert(c, tx, "corpus_file", "rel_path", "sha256", "byte_length", "extension");
            foreach (var r in list) ins.Run(r.Rel, r.Sha, r.Len, r.Ext);
            using var s = new PreparedInsert(c, tx, "corpus_scan", "scanned_utc", "files", "distinct_hashes", "unreadable");
            s.Run(Hashing.NowUtc(), list.Count, list.Select(r => r.Sha).Distinct().Count(), unreadable);
            tx.Commit();
        }
        var byExt = list.GroupBy(r => r.Ext).ToDictionary(g => g.Key, g => ((long)g.Count(), (long)g.Select(x => x.Sha).Distinct().Count()));
        var distinct = list.Select(r => r.Sha).Distinct().Count();
        CatalogDatabase.CheckpointAndClose(c, vacuum: false);
        return new CorpusReport(list.Count, distinct, list.Count - distinct, list.Sum(r => r.Len), unreadable, byExt, outputDb);
    }

    private static void Walk(string dir, HashSet<string> exclude, List<string> output)
    {
        IEnumerable<string> entries;
        try { entries = Directory.EnumerateFileSystemEntries(dir); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return; }
        foreach (var e in entries)
        {
            var name = Path.GetFileName(e);
            var attrs = File.GetAttributes(e);
            if (attrs.HasFlag(FileAttributes.Directory))
            {
                if (exclude.Contains(name) || attrs.HasFlag(FileAttributes.ReparsePoint)) continue; // enlaces: no se siguen (evita ciclos y salir del corpus)
                Walk(e, exclude, output);
            }
            else if (Extensions.Contains(Path.GetExtension(name).TrimStart('.')))
                output.Add(e);
        }
    }
}
