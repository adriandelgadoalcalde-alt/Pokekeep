// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Text.Json;
using AutoLegalidad.Catalog.Db;
using AutoLegalidad.Catalog.Snapshots;
using Microsoft.Data.Sqlite;

namespace AutoLegalidad.Engine.Catalog;

/// <summary>Identidad de datos con la que el motor generara: snapshot Verified, su release y el hash del Core con el que se construyo.</summary>
public sealed record CatalogIdentity(string SnapshotId, string ReleaseId, string CoreDllSha256, string RuleProfileSha256, int SchemaVersion);

/// <summary>Ventana de distribucion de una tarjeta de evento: inicio inclusivo, fin inclusivo (null = desconocido) y si viene de las hojas de seguimiento (<c>FromTracking</c>) en vez de la propia tarjeta.</summary>
public sealed record EventDateWindow(DateOnly Start, DateOnly? End, bool FromTracking);

/// <summary>
/// Lectura de SOLO LECTURA del catalogo del Hito 1. La generacion (Hito 2+) lee UNICAMENTE las vistas <c>v_encounter_generable</c> y
/// <c>v_artifact_generable</c> (barrera de la regla 8) y la tabla <c>availability</c> (hechos de presencia del Core), nunca <c>encounter</c> a pelo.
/// </summary>
public sealed class CatalogReader : IDisposable
{
    private readonly SqliteConnection _c;
    private HashSet<string>? _generable;
    private readonly string _path;

    public CatalogReader(string dbPath)
    {
        _path = dbPath;
        if (!File.Exists(dbPath))
            throw new FileNotFoundException("No existe la base de datos del catálogo.", dbPath);
        _c = CatalogDatabase.Open(dbPath, readOnly: true);
    }

    public string Path => _path;

    public CatalogIdentity ReadIdentity()
    {
        var snapshot = SnapshotService.ActiveSnapshotId(_c) ?? throw new InvalidOperationException("El catálogo no tiene ninguna instantánea verificada (Verified).");
        using var cmd = _c.CreateCommand();
        cmd.CommandText = "SELECT manifest_json FROM dataset_snapshot WHERE snapshot_id=$id";
        cmd.Parameters.AddWithValue("$id", snapshot);
        var json = (string)(cmd.ExecuteScalar() ?? throw new InvalidOperationException("La instantánea no tiene manifiesto (manifest)."));
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var release = root.GetProperty("release_id").GetString()!;
        var schema = int.Parse(CatalogDatabase.Scalar(_c, "SELECT max(version) FROM schema_version"), System.Globalization.CultureInfo.InvariantCulture);
        return new CatalogIdentity(snapshot, release, root.GetProperty("core_dll_sha256").GetString()!, root.GetProperty("rule_profile_sha256").GetString()!, schema);
    }

    /// <summary>Motivos por los que el snapshot activo ya no vale para este Core/perfil (vacio = vigente). Las fuentes de datos no se recomprueban aqui (no hay clones en tiempo de ejecucion).</summary>
    public IReadOnlyList<string> StaleReasons(string snapshotId, string currentCoreDllSha256)
    {
        var current = ReadIdentity();
        var reasons = new List<string>();
        if (!string.Equals(current.SnapshotId, snapshotId, StringComparison.Ordinal))
            reasons.Add($"La instantánea activa del catálogo es {current.SnapshotId}, no {snapshotId}.");
        if (!string.Equals(current.CoreDllSha256, currentCoreDllSha256, StringComparison.OrdinalIgnoreCase))
            reasons.Add("El PKHeX.Core.dll cargado no es el que se usó al construir el catálogo.");
        return reasons;
    }

    /// <summary>Claves de encuentro aprobadas para generar (vista <c>v_encounter_generable</c>), cargadas una vez.</summary>
    public IReadOnlySet<string> GenerableKeys => GenerableOverrideForTests ?? (_generable ??= LoadGenerable());

    /// <summary>SOLO PRUEBAS: sustituye el conjunto generable para comprobar que la barrera de la regla 8 rechaza lo que no esta aprobado.</summary>
    public IReadOnlySet<string>? GenerableOverrideForTests { get; set; }

    private HashSet<string> LoadGenerable()
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        using var cmd = _c.CreateCommand();
        cmd.CommandText = "SELECT encounter_key FROM v_encounter_generable";
        using var r = cmd.ExecuteReader();
        while (r.Read()) set.Add(r.GetString(0));
        return set;
    }

    /// <summary>Estado de presencia de una especie/forma en un juego concreto segun el catalogo (<c>Present|Absent|Unknown</c>) y su almacenabilidad.</summary>
    public (string State, string FormStorage)? Availability(int gameId, int species, int form)
    {
        using var cmd = _c.CreateCommand();
        cmd.CommandText = "SELECT state, form_storage FROM availability WHERE game_id=$g AND species_id=$s AND form_id=$f";
        cmd.Parameters.AddWithValue("$g", gameId);
        cmd.Parameters.AddWithValue("$s", species);
        cmd.Parameters.AddWithValue("$f", form);
        using var r = cmd.ExecuteReader();
        return r.Read() ? (r.GetString(0), r.GetString(1)) : null;
    }

    /// <summary>
    /// Hito 4c: fecha de inicio de la distribucion de una tarjeta de evento (<c>descriptor_json.startDate</c>, solo si la tarjeta la trae y no es el marcador «hoy» del Core), leida de la vista
    /// generable. Null = desconocida (nunca se inventa).
    /// </summary>
    public DateOnly? EventStartDate(string encounterKey)
    {
        using var cmd = _c.CreateCommand();
        cmd.CommandText = "SELECT json_extract(descriptor_json, '$.startDate') FROM v_encounter_generable WHERE encounter_key=$k LIMIT 1";
        cmd.Parameters.AddWithValue("$k", encounterKey);
        return cmd.ExecuteScalar() is string s && DateOnly.TryParseExact(s, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d) ? d : null;
    }

    /// <summary>
    /// Hito 5 (datos, ADR 0029): ventana de distribucion de una tarjeta de evento. Prefiere la fecha de inicio PROPIA de la tarjeta (<c>descriptor_json.startDate</c>; sin fin) y, si la
    /// tarjeta no la trae (las «full» WC6/WC7 llevan el marcador «hoy» del Core), la que se derivo de las hojas <c>Extras/Tracking</c> de EventsGallery (<c>event_window</c>: inicio y, si
    /// la hoja lo dice, fin). Null = desconocida (nunca se inventa). Una base de datos anterior a la migracion 0006 no tiene la tabla: solo cuenta la fecha propia.
    /// </summary>
    public EventDateWindow? EventWindow(string encounterKey)
    {
        if (EventStartDate(encounterKey) is { } own) return new EventDateWindow(own, null, false);
        try
        {
            using var cmd = _c.CreateCommand();
            cmd.CommandText = "SELECT w.start_day, w.end_day FROM event_window w WHERE w.encounter_key=$k AND EXISTS (SELECT 1 FROM v_encounter_generable g WHERE g.encounter_key=w.encounter_key)";
            cmd.Parameters.AddWithValue("$k", encounterKey);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;
            var start = DateOnly.ParseExact(r.GetString(0), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            DateOnly? end = r.IsDBNull(1) ? null : DateOnly.ParseExact(r.GetString(1), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            return new EventDateWindow(start, end, true);
        }
        catch (SqliteException ex) when (ex.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
    }

    /// <summary>0.6.5: nombre en español del juego (<c>game.display_name_es</c>) por el nombre de versión del Core (p. ej. «LG»); null si el catálogo no lo conoce.</summary>
    public string? GameNameEs(string versionName)
    {
        using var cmd = _c.CreateCommand();
        cmd.CommandText = "SELECT display_name_es FROM game WHERE version_name=$v AND is_group=0 LIMIT 1";
        cmd.Parameters.AddWithValue("$v", versionName);
        return cmd.ExecuteScalar() as string;
    }

    public void Dispose() => _c.Dispose();
}
