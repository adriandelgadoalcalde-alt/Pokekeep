// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Text.Encodings.Web;
using System.Text.Json;
using PKHeX.Core;

namespace AutoLegalidad.Catalog.Model;

/// <summary>Fila de <c>encounter</c> lista para insertar, con la resolucion a juegos concretos (<c>encounter_game</c>).</summary>
public sealed record EncounterRow(
    string Key, string SourceVersionId, string? ArtifactSha256, int Species, int Form, int OriginGame, string Context, string RuntimeType,
    string Kind, string? InternalId, string DescriptorSha256, string DescriptorJson, string ConstraintsJson, string SourceRef,
    bool Approved, IReadOnlyList<int> Games);

public sealed record SpeciesRow(int Id, string CanonicalName, int? IntroducedGeneration, IReadOnlyList<(string Language, string Name)> Names);

public sealed record FormRow(int Species, int Form, string? CanonicalFormName, string DescriptorJson);

public sealed record AvailabilityRow(int Game, int Species, int Form, string State, string ReasonRef, string FormStorage);

/// <summary>JSON canonico (claves ordenadas, sin escapes innecesarios) para que el mismo contenido de el mismo hash.</summary>
public static class CanonicalJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false,
    };

    private static readonly JsonSerializerOptions IndentedOptions = new(Options) { WriteIndented = true };

    public static string Serialize(object? value) => JsonSerializer.Serialize(Normalize(value), Options);

    public static string SerializeIndented(object? value) => JsonSerializer.Serialize(Normalize(value), IndentedOptions);

    private static object? Normalize(object? v) => v switch
    {
        null => null,
        IDictionary<string, object?> d => new SortedDictionary<string, object?>(
            d.ToDictionary(k => k.Key, k => Normalize(k.Value)), StringComparer.Ordinal),
        System.Collections.IDictionary => throw new NotSupportedException("Usa IDictionary<string, object?>."),
        string s => s,
        System.Collections.IEnumerable e => e.Cast<object?>().Select(Normalize).ToList(),
        _ => v,
    };
}

public static class RuntimeTypes
{
    public static string For(EntityContext context, byte generation, GameVersion version) => version switch
    {
        GameVersion.COLO => "CK3",
        GameVersion.XD => "XK3",
        _ => context switch
        {
            EntityContext.Gen7b => "PB7",
            EntityContext.Gen8a => "PA8",
            EntityContext.Gen8b => "PB8",
            EntityContext.Gen9a => "PA9",
            _ => $"PK{(context.IsValid ? context.Generation : generation)}",
        },
    };
}
