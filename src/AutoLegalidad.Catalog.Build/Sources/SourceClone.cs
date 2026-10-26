// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Text.Json;

namespace AutoLegalidad.Catalog.Sources;

/// <summary>Un clon de una fuente de terceros fijado por commit en <c>fuentes.lock.json</c>.</summary>
public sealed record SourceClone(string Folder, string Path, string Commit, string License)
{
    public string Commit12 => Commit[..12];

    public static SourceClone Resolve(string fuentesLockPath, string raiz, string folder)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(fuentesLockPath));
        var e = doc.RootElement.GetProperty("fuentes").EnumerateArray().First(f => f.GetProperty("carpeta").GetString() == folder);
        return new SourceClone(folder, System.IO.Path.Combine(raiz, "fuentes", folder), e.GetProperty("commit").GetString()!, e.GetProperty("licencia").GetString()!);
    }

    /// <summary>Comprueba que el HEAD del clon es exactamente el commit fijado (lee .git directamente: no depende de ejecutar git).</summary>
    public void VerifyPinned()
    {
        if (!Directory.Exists(Path))
            throw new DirectoryNotFoundException($"No existe el clon de {Folder}: {Path}");
        var head = GitHead.Read(Path);
        if (!string.Equals(head, Commit, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"{Folder} esta en {head[..Math.Min(12, head.Length)]} y el commit fijado es {Commit12}.");
    }
}

public static class GitHead
{
    public static string Read(string repoDir)
    {
        var gitDir = System.IO.Path.Combine(repoDir, ".git");
        if (File.Exists(gitDir)) // worktree/submodulo: ".git" es un fichero "gitdir: ..."
        {
            var line = File.ReadAllText(gitDir).Trim();
            gitDir = line.StartsWith("gitdir:", StringComparison.Ordinal) ? System.IO.Path.GetFullPath(System.IO.Path.Combine(repoDir, line[7..].Trim())) : gitDir;
        }
        var head = File.ReadAllText(System.IO.Path.Combine(gitDir, "HEAD")).Trim();
        if (!head.StartsWith("ref:", StringComparison.Ordinal))
            return head;
        var refName = head[4..].Trim();
        var refFile = System.IO.Path.Combine(gitDir, refName.Replace('/', System.IO.Path.DirectorySeparatorChar));
        if (File.Exists(refFile))
            return File.ReadAllText(refFile).Trim();
        var packed = System.IO.Path.Combine(gitDir, "packed-refs");
        if (File.Exists(packed))
            foreach (var l in File.ReadLines(packed))
                if (!l.StartsWith('#') && !l.StartsWith('^') && l.EndsWith(" " + refName, StringComparison.Ordinal))
                    return l.Split(' ')[0];
        throw new InvalidOperationException($"No se pudo resolver {refName} en {repoDir}.");
    }
}
