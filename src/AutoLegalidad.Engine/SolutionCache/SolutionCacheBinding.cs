// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Contracts;
using AutoLegalidad.Catalog.Core;
using PKHeX.Core;

namespace AutoLegalidad.Engine.SolutionCache;

/// <summary>
/// Identidad de version con la que se precalculo un fichero de soluciones (R-011, ADR 0037): hash de los binarios que deciden el resultado de una busqueda
/// (Core, motor, adaptador ALM, fuente ALM), las versiones fijadas, el perfil de reglas y la instantanea del catalogo. Se guarda en la tabla <c>meta</c>;
/// al abrir, si NO coincide EXACTAMENTE con la del proceso en ejecucion, la cache queda desactivada entera (nunca se usa parcialmente).
/// </summary>
public sealed class SolutionCacheBinding
{
    public const string Absent = FileBinaryIdentity.Absent;

    private readonly SortedDictionary<string, string> _values;

    public SolutionCacheBinding(IEnumerable<KeyValuePair<string, string>> values)
        => _values = new SortedDictionary<string, string>(values.ToDictionary(k => k.Key, k => k.Value, StringComparer.Ordinal), StringComparer.Ordinal);

    public IReadOnlyDictionary<string, string> Values => _values;

    /// <summary>Copia con un componente cambiado (pruebas de invalidacion).</summary>
    public SolutionCacheBinding With(string key, string value)
    {
        var copy = new Dictionary<string, string>(_values, StringComparer.Ordinal) { [key] = value };
        return new SolutionCacheBinding(copy);
    }

    /// <summary>Primer componente (en orden alfabetico) cuyo valor difiere del guardado o que falta/sobra; <c>null</c> si coinciden TODOS.</summary>
    public string? FirstDifference(IReadOnlyDictionary<string, string> stored)
    {
        foreach (var (k, v) in _values)
            if (!stored.TryGetValue(k, out var s) || !string.Equals(s, v, StringComparison.Ordinal)) return k;
        foreach (var k in stored.Keys.OrderBy(x => x, StringComparer.Ordinal))
            if (!_values.ContainsKey(k)) return k;
        return null;
    }

    /// <summary>
    /// Identidad del proceso en ejecucion. <paramref name="datasetSnapshotId"/> es el id de la instantanea activa del catalogo; <paramref name="identity"/> (por defecto
    /// <see cref="BinaryIdentity.Current"/>) da el SHA-256 de los cuatro binarios: en escritorio los de los ficheros, en Android las constantes incrustadas (ADR 0054).
    /// </summary>
    public static SolutionCacheBinding ForRuntime(string datasetSnapshotId, IBinaryIdentity? identity = null)
    {
        identity ??= BinaryIdentity.Current;
        return new SolutionCacheBinding(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["core_dll_sha256"] = identity.CoreSha256,
            ["engine_dll_sha256"] = identity.EngineSha256,
            ["alm_adapter_dll_sha256"] = identity.AdapterSha256,
            ["alm_source_dll_sha256"] = identity.AlmSha256,
            ["core_tag"] = PinnedVersions.CoreTag,
            ["core_commit"] = PinnedVersions.CoreCommit,
            ["alm_tag"] = PinnedVersions.AlmTag,
            ["alm_commit"] = PinnedVersions.AlmCommit,
            ["engine_version"] = PinnedVersions.EngineVersion,
            ["adapter_version"] = PinnedVersions.AdapterVersion,
            ["rule_profile_sha256"] = RuleProfile.Sha256,
            ["dataset_snapshot_id"] = datasetSnapshotId,
        });
    }
}
