// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Catalog.Db;
using AutoLegalidad.Catalog.Model;
using AutoLegalidad.Catalog.Snapshots;
using Microsoft.Data.Sqlite;

namespace AutoLegalidad.Catalog.Build;

/// <summary>Cifras REALES del catalogo, leidas de la base de datos (no de contadores del importador). Sin marcas de tiempo: reproducibles.</summary>
public static class CatalogStats
{
    public static IDictionary<string, object?> Collect(SqliteConnection c)
    {
        long N(string sql) => CatalogDatabase.Count(c, sql);
        Dictionary<string, object?> Group(string sql)
        {
            var d = new Dictionary<string, object?>();
            using var cmd = c.CreateCommand();
            cmd.CommandText = sql;
            using var r = cmd.ExecuteReader();
            while (r.Read()) d[r.GetValue(0)?.ToString() ?? "(null)"] = r.GetInt64(1);
            return d;
        }

        var stats = new Dictionary<string, object?>
        {
            ["schema_version"] = N("SELECT MAX(version) FROM schema_version"),
            ["sqlite_version"] = CatalogDatabase.Scalar(c, "SELECT sqlite_version()"),
            ["snapshot_activo"] = SnapshotService.ActiveSnapshotId(c),
            ["snapshots_por_estado"] = Group("SELECT state, count(*) FROM dataset_snapshot GROUP BY state"),
            ["fuentes_importadas"] = N("SELECT count(*) FROM source_version"),
            ["fuentes"] = Group("SELECT source_id || '@' || substr(revision,1,12), 1 FROM source_version"),
            ["especies"] = N("SELECT count(*) FROM species"),
            ["nombres_de_especie"] = N("SELECT count(*) FROM species_name"),
            ["formas_especie_forma"] = N("SELECT count(*) FROM species_form"),
            ["juegos_concretos"] = N("SELECT count(*) FROM game WHERE is_group=0"),
            ["grupos_de_versiones"] = N("SELECT count(*) FROM game WHERE is_group=1"),
            ["disponibilidad_filas"] = N("SELECT count(*) FROM availability"),
            ["disponibilidad_por_estado"] = Group("SELECT state, count(*) FROM availability GROUP BY state"),
            ["disponibilidad_present_por_almacenabilidad"] = Group("SELECT form_storage, count(*) FROM availability WHERE state='Present' GROUP BY form_storage"),
            ["disponibilidad_por_juego"] = PerGame(c),
            ["artefactos_distintos"] = N("SELECT count(*) FROM artifact"),
            ["artefactos_bytes_totales"] = N("SELECT COALESCE(SUM(byte_length),0) FROM artifact"),
            ["artefacto_origen_filas"] = N("SELECT count(*) FROM artifact_origin"),
            ["artefactos_por_clasificacion"] = Group("SELECT evidence_class, count(*) FROM artifact_origin GROUP BY evidence_class"),
            ["artefactos_por_clasificacion_y_aprobacion"] = Group("SELECT evidence_class || ' / ' || CASE approved_for_generation WHEN 1 THEN 'aprobado' ELSE 'no aprobado' END, count(*) FROM artifact_origin GROUP BY 1"),
            ["artefactos_por_parseo"] = Group("SELECT parse_status, count(*) FROM artifact_origin GROUP BY parse_status"),
            ["artefactos_por_corroboracion"] = Group("SELECT core_corroboration, count(*) FROM artifact_origin GROUP BY core_corroboration"),
            ["artefactos_por_regla"] = Group("SELECT allowlist_rule_id, count(*) FROM artifact_origin GROUP BY allowlist_rule_id"),
            ["encuentros_total"] = N("SELECT count(*) FROM encounter"),
            ["encuentros_por_fuente"] = Group("SELECT v.source_id, count(*) FROM encounter e JOIN source_version v ON v.source_version_id=e.source_version_id GROUP BY v.source_id"),
            ["encuentros_por_clase"] = Group("SELECT kind, count(*) FROM encounter GROUP BY kind"),
            ["encuentros_aprobados_para_generar"] = N("SELECT count(*) FROM v_encounter_generable"),
            ["encuentro_juego_filas"] = N("SELECT count(*) FROM encounter_game"),
            ["ventanas_go"] = N("SELECT count(*) FROM pogo_window"),
            ["ventanas_evento_tracking"] = N("SELECT count(*) FROM event_window"),
            ["ventanas_evento_tracking_por_tipo"] = Group("SELECT json_extract(e.descriptor_json,'$.type'), count(*) FROM event_window w JOIN encounter e ON e.encounter_key=w.encounter_key GROUP BY 1"),
            ["ventanas_go_por_tipo"] = Group("SELECT encounter_type, count(*) FROM pogo_window GROUP BY encounter_type"),
            ["cobertura_tuplas"] = N("SELECT count(*) FROM coverage_case"),
            ["cobertura_por_estado"] = Group("SELECT status, count(*) FROM coverage_case GROUP BY status"),
            ["cobertura_por_clase"] = Group("SELECT encounter_kind, count(*) FROM coverage_case GROUP BY encounter_kind"),
            ["cobertura_por_esperado"] = Group("SELECT expected, count(*) FROM coverage_case GROUP BY expected"),
            ["cobertura_casos_negativos"] = N("SELECT count(*) FROM coverage_case WHERE expected='Rejected'"),
            ["cobertura_tuplas_positivas_distintas_juego_especie_forma"] = N("SELECT count(*) FROM (SELECT DISTINCT game_id, species_id, form_id FROM coverage_case WHERE expected<>'Rejected')"),
            ["cobertura_resultados_medidos"] = N("SELECT count(*) FROM coverage_result"),
            ["cobertura_por_juego"] = CoveragePerGame(c),
            ["libreria_entradas"] = N("SELECT count(*) FROM library_entry"),
            ["libreria_ofrecibles"] = N("SELECT count(*) FROM library_entry WHERE addable=1"),
            ["libreria_atenuadas"] = N("SELECT count(*) FROM library_entry WHERE availability='Dimmed'"),
            ["libreria_ofrecibles_por_clase"] = Group("SELECT kind, count(*) FROM library_entry WHERE addable=1 GROUP BY kind"),
            ["libreria_eventos_con_titulo_es"] = N("SELECT count(*) FROM library_entry WHERE event_title_es IS NOT NULL"),
            ["libreria_eventos_con_fecha"] = N("SELECT count(*) FROM library_entry WHERE event_start_date IS NOT NULL"),
            ["libreria_eventos_con_region"] = N("SELECT count(*) FROM library_entry WHERE kind='Event' AND event_region IS NOT NULL"),
            ["libreria_eventos_sin_region"] = N("SELECT count(*) FROM library_entry WHERE kind='Event' AND event_region IS NULL"),
            ["region_eventos_por_fuente"] = Group("SELECT source, count(*) FROM event_region GROUP BY 1"),
            ["libreria_eventos_con_idioma"] = N("SELECT count(*) FROM library_entry WHERE event_language IS NOT NULL"),
        };
        return stats;
    }

    private static Dictionary<string, object?> PerGame(SqliteConnection c)
    {
        var d = new Dictionary<string, object?>();
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
            SELECT g.version_name, sum(a.state='Present'), sum(a.state='Absent'), sum(a.state='Unknown'),
                   sum(a.state='Present' AND a.form_storage<>'Storable')
              FROM availability a JOIN game g ON g.game_id=a.game_id GROUP BY g.game_id ORDER BY g.game_id
            """;
        using var r = cmd.ExecuteReader();
        while (r.Read())
            d[r.GetString(0)] = new Dictionary<string, object?> { ["present"] = r.GetInt64(1), ["absent"] = r.GetInt64(2), ["unknown"] = r.GetInt64(3), ["present_no_almacenable"] = r.GetInt64(4) };
        return d;
    }

    private static Dictionary<string, object?> CoveragePerGame(SqliteConnection c)
    {
        var d = new Dictionary<string, object?>();
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
            SELECT g.version_name, count(*), sum(k.expected='Rejected'), sum(k.status='Planned')
              FROM coverage_case k JOIN game g ON g.game_id=k.game_id GROUP BY g.game_id ORDER BY g.game_id
            """;
        using var r = cmd.ExecuteReader();
        while (r.Read())
            d[r.GetString(0)] = new Dictionary<string, object?> { ["tuplas"] = r.GetInt64(1), ["negativas"] = r.GetInt64(2), ["planned"] = r.GetInt64(3) };
        return d;
    }

    public static string ToJson(IDictionary<string, object?> stats, GateResult gate)
    {
        var all = new Dictionary<string, object?>(stats)
        {
            ["gate"] = new Dictionary<string, object?>
            {
                ["passed"] = gate.Passed,
                ["checks"] = gate.Checks.Select(g => (object?)new Dictionary<string, object?> { ["name"] = g.Name, ["ok"] = g.Ok, ["detail"] = g.Detail }).ToList(),
            },
        };
        return CanonicalJson.SerializeIndented(all);
    }
}
