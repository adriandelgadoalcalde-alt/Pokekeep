// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Text.Json;
using System.Text.RegularExpressions;
using AutoLegalidad.Catalog.Db;

namespace AutoLegalidad.Catalog.Sources;

/// <summary>Como se interpreta un fichero permitido.</summary>
public enum ParseMode
{
    /// <summary>Tarjeta de regalo: <c>MysteryGift.GetMysteryGift</c> del Core.</summary>
    MysteryGift,
    /// <summary>Pokemon individual: <c>EntityFormat.GetFromBytes</c> del Core.</summary>
    Entity,
    /// <summary>El Core no tiene lector publico para este formato: se registra, no se parsea.</summary>
    None,
}

public sealed class FilenameOverride
{
    public required string Pattern { get; init; }
    public required string EvidenceClass { get; init; }
    public bool? Approve { get; init; }
    public required string Rationale { get; init; }
    private Regex? _re;
    public bool IsMatch(string fileName) => (_re ??= new Regex(Pattern, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)).IsMatch(fileName);
}

/// <summary>Una regla de la allowlist: una carpeta (por prefijo de ruta) y, si procede, las extensiones permitidas.</summary>
public sealed class AllowRule
{
    public required string Id { get; init; }
    public required string Path { get; init; }
    /// <summary><c>import</c> o <c>exclude</c>.</summary>
    public required string Action { get; init; }
    public string[]? Extensions { get; init; }
    public string? ReleaseState { get; init; }
    public string? EvidenceClass { get; init; }
    public bool Approve { get; init; }
    public ParseMode Parse { get; init; } = ParseMode.None;
    public string? EvidenceRef { get; init; }
    public string? ApprovalNote { get; init; }
    public required string Rationale { get; init; }
    public FilenameOverride[]? FilenameOverrides { get; init; }

    public bool Matches(string relativePath, string extension)
    {
        var p = Path.TrimEnd('/');
        var inside = p.Length == 0 || relativePath.StartsWith(p + "/", StringComparison.Ordinal);
        if (!inside) return false;
        return Extensions is null || Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Allowlist documentada de una fuente: SOLO lo que una regla <c>import</c> permite se lee de disco. Resolucion: gana la regla mas especifica
/// (ruta mas larga; a igualdad, las <c>exclude</c> primero); lo que ninguna regla cubre queda fuera con motivo <c>NotInAllowlist</c>.
/// </summary>
public sealed class Allowlist
{
    public required string Source { get; init; }
    public required string Description { get; init; }
    public required IReadOnlyList<AllowRule> Rules { get; init; }
    /// <summary>Sobrescrituras que se prueban contra la RUTA completa de cualquier regla <c>import</c> clasificada como AuthenticDump (solo pueden degradarla).</summary>
    public IReadOnlyList<FilenameOverride> GlobalOverrides { get; init; } = [];
    public required string Sha256 { get; init; }

    public static Allowlist LoadEmbedded(string resourceName)
    {
        var asm = typeof(Allowlist).Assembly;
        using var s = asm.GetManifestResourceStream("Allowlists/" + resourceName)
                      ?? throw new FileNotFoundException($"Allowlist embebida no encontrada: {resourceName}");
        using var r = new StreamReader(s);
        return Parse(r.ReadToEnd());
    }

    public static Allowlist LoadFile(string path) => Parse(File.ReadAllText(path));

    public static Allowlist Parse(string json)
    {
        var norm = json.Replace("\r\n", "\n", StringComparison.Ordinal);
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
        opts.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        var dto = JsonSerializer.Deserialize<Dto>(norm, opts) ?? throw new InvalidDataException("Allowlist vacia.");
        var list = new Allowlist { Source = dto.Source, Description = dto.Description, Rules = dto.Rules, GlobalOverrides = dto.GlobalOverrides ?? [], Sha256 = Hashing.Sha256Hex(norm) };
        list.Validate();
        return list;
    }

    private sealed class Dto
    {
        public required string Source { get; init; }
        public required string Description { get; init; }
        public required AllowRule[] Rules { get; init; }
        public FilenameOverride[]? GlobalOverrides { get; init; }
    }

    /// <summary>Coherencia interna: ids unicos, acciones validas, y reglas de aprobacion que respetan la regla 8.</summary>
    public void Validate()
    {
        foreach (var g in GlobalOverrides)
            if (g.EvidenceClass is not ("DocumentedReconstruction" or "ResearchOnly" or "Unknown"))
                throw new InvalidDataException("Las sobrescrituras globales solo pueden degradar una clase, nunca elevarla a AuthenticDump.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var r in Rules)
        {
            if (!ids.Add(r.Id)) throw new InvalidDataException($"Id de regla duplicado: {r.Id}");
            if (r.Action is not ("import" or "exclude")) throw new InvalidDataException($"{r.Id}: accion invalida '{r.Action}'.");
            if (r.Path.Contains('\\', StringComparison.Ordinal)) throw new InvalidDataException($"{r.Id}: la ruta debe usar '/'.");
            if (r.Action == "exclude") continue;
            if (r.EvidenceClass is not ("AuthenticDump" or "DocumentedReconstruction" or "ResearchOnly" or "Unknown"))
                throw new InvalidDataException($"{r.Id}: evidenceClass invalida.");
            if (r.ReleaseState is not ("Published" or "Unreleased" or "Unknown"))
                throw new InvalidDataException($"{r.Id}: releaseState invalido.");
            CheckApproval(r.Id, r.EvidenceClass, r.ReleaseState, r.Approve, r.EvidenceRef, r.ApprovalNote);
            foreach (var o in r.FilenameOverrides ?? [])
            {
                if (o.EvidenceClass is not ("AuthenticDump" or "DocumentedReconstruction" or "ResearchOnly" or "Unknown"))
                    throw new InvalidDataException($"{r.Id}: override con evidenceClass invalida.");
                CheckApproval(r.Id + "/override", o.EvidenceClass, r.ReleaseState, o.Approve ?? r.Approve, r.EvidenceRef, r.ApprovalNote);
            }
        }
    }

    private static void CheckApproval(string id, string cls, string state, bool approve, string? evidenceRef, string? note)
    {
        if (!approve) return;
        if (state != "Published" || cls is not ("AuthenticDump" or "DocumentedReconstruction"))
            throw new InvalidDataException($"{id}: aprobar para generar exige Published + AuthenticDump|DocumentedReconstruction (regla 8).");
        if (string.IsNullOrWhiteSpace(evidenceRef) || string.IsNullOrWhiteSpace(note))
            throw new InvalidDataException($"{id}: aprobar exige evidenceRef y approvalNote.");
    }

    /// <summary>Resuelve la regla aplicable a una ruta relativa (con '/'), o null si ninguna la cubre.</summary>
    public AllowRule? Resolve(string relativePath)
    {
        var ext = System.IO.Path.GetExtension(relativePath).ToLowerInvariant();
        return Rules.Where(r => r.Matches(relativePath, ext))
            .OrderByDescending(r => r.Path.Length).ThenBy(r => r.Action == "exclude" ? 0 : 1).FirstOrDefault();
    }
}
