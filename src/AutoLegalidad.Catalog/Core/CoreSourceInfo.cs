// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Reflection;
using System.Text.Json;
using AutoLegalidad.Contracts;
using PKHeX.Core;
using AutoLegalidad.Catalog.Db;

namespace AutoLegalidad.Catalog.Core;

/// <summary>Identidad del Core oficial que alimenta el catalogo (leida de <c>fuentes.lock.json</c> y del ensamblado realmente cargado).</summary>
public sealed record CoreSourceInfo(string Tag, string Commit, string AlmTag, string AlmCommit, string DllSha256, string AssemblyVersion, string InformationalVersion)
{
    public string Commit12 => Commit[..12];

    /// <summary>Fecha de la version estable del Core (tag <c>yy.MM.dd</c>): ninguna fecha de evento puede ser posterior.</summary>
    public DateOnly ReleaseDate => DateOnly.ParseExact(Tag, "yy.MM.dd", System.Globalization.CultureInfo.InvariantCulture);
    public string ReleaseId => $"core-{Tag}-{Commit12}";
    public string SourceVersionId => $"pkhex-core@{Commit12}";

    /// <summary>Lee el commit fijado de <c>fuentes.lock.json</c> y el hash del <c>PKHeX.Core.dll</c> (de <paramref name="identity"/>; por defecto, el fichero junto al ejecutable). Falla si el ensamblado no corresponde al commit fijado.</summary>
    public static CoreSourceInfo Resolve(string fuentesLockPath, IBinaryIdentity? identity = null)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(fuentesLockPath));
        var fuentes = doc.RootElement.GetProperty("fuentes").EnumerateArray().ToList();
        var core = fuentes.First(f => f.GetProperty("carpeta").GetString() == "PKHeX-oficial");
        var alm = fuentes.First(f => f.GetProperty("carpeta").GetString() == "ALM-estable");
        var asm = typeof(PKM).Assembly;
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? string.Empty;
        var commit = core.GetProperty("commit").GetString()!;
        // El Core se compila con -p:SourceRevisionId=<commit12> (ADR 0003); la version informativa lleva "+<commit12>".
        if (!info.Contains(commit[..12], StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"El PKHeX.Core cargado ('{info}') no corresponde al commit fijado {commit[..12]}. Compila con -p:SourceRevisionId={commit[..12]}.");
        return new CoreSourceInfo(
            core.GetProperty("tag").GetString()!, commit,
            alm.GetProperty("tag").GetString()!, alm.GetProperty("commit").GetString()!,
            (identity ?? BinaryIdentity.Current).CoreSha256,
            asm.GetName().Version?.ToString() ?? string.Empty, info);
    }
}

/// <summary>Perfil de reglas del Hito 1: lo que fija la politica de aprobacion para generar. Su SHA-256 se guarda en <c>release.rule_profile_sha256</c>.</summary>
public static class RuleProfile
{
    public const string PolicyVersion = "auto-v1";

    public const string Text =
        "{\"policy\":\"auto-v1\",\"core\":\"official-unmodified\",\"easterEggs\":false," +
        "\"generationApproval\":\"Published+(AuthenticDump|DocumentedReconstruction)+officialParseOk+(reconstruction needs byte-identical Core MGDB)\"," +
        "\"excludedFromGeneration\":[\"Unreleased\",\"Unknown\",\"ResearchOnly\"],\"hito\":1}";

    public static string Sha256 => Hashing.Sha256Hex(Text);
}
