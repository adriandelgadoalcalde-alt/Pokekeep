// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Catalog.Db;
using Microsoft.Data.Sqlite;

namespace AutoLegalidad.Catalog.Build;

/// <summary>
/// X42 (ADR 0098): deriva del catalogo de escritorio una copia COMPACTA para el movil, sin perder ningun dato ni cambiar el resultado de ninguna consulta de la app.
/// Se aplica sobre el fichero ya construido y verificado (nunca durante la construccion): <c>library_entry</c> y <c>coverage_case</c> siguen existiendo con las mismas columnas
/// y las mismas filas; solo cambia como se guardan. Cada paso comprueba su propio resultado fila a fila contra el original antes de borrarlo.
/// <list type="bullet">
/// <item>Nivel 1 (tablas): sin los indices que ninguna consulta de la app usa, <c>coverage_case</c> y <c>availability</c> sin la copia de la clave (WITHOUT ROWID) y el FTS5 sin los indices de prefijo.</item>
/// <item>Nivel 2 (ademas): <c>coverage_case</c> pasa a ser una VISTA sobre <c>library_entry</c> (solo se guardan las dos columnas que no estan ya ahi) y <c>library_entry</c> una vista sobre una tabla con los textos
/// repetidos en diccionarios (los mismos rowid: el FTS5 y las notas los usan).</item>
/// </list>
/// El catalogo compacto es de SOLO LECTURA: la app movil lo abre asi. No sirve para reconstruir la Libreria (<c>libreria-reconstruir</c>) ni para el banco de cobertura de escritorio.
/// </summary>
public static class CatalogCompactor
{
    public sealed record Result(int Level, long BytesBefore, long BytesAfter, IReadOnlyList<string> Steps);

    /// <summary>Indices que ninguna consulta de la app movil usa (medido con EXPLAIN QUERY PLAN sobre las consultas reales; ver docs/android/seguimiento-cierre-codex.md, X42). Solo sirven al construir o a informes de escritorio.</summary>
    public static readonly IReadOnlyList<string> UnusedIndexes =
    [
        "ix_library_name", "ix_library_event_date", "ix_library_anchor", "ix_coverage_status", "ix_coverage_game", "ix_coverage_tuple", "ix_coverage_result", "ix_coverage_probe_state",
        "ix_encounter_source", "ix_encounter_lookup", "ix_encounter_game_game", "ix_pogo_window", "ix_artifact_origin_class", "ix_operation_time", "ix_validation_operation",
    ];

    private const string Fts = "CREATE VIRTUAL TABLE library_search USING fts5(name_es, names_all, form_name, event_title, event_title_es, event_card_id, game_name, "
                               + "content = 'library_entry', content_rowid = 'rowid', tokenize = 'unicode61 remove_diacritics 2')";

    private const string LibraryColumns = "entry_key, release_id, game_id, game_name, species_id, form_id, dex_number, name_es, names_all, form_name, generation, kind, variant_key, availability, dim_reason, addable, "
                                          + "form_storage, status, expected, encounter_key, source_label, event_title, event_title_es, event_card_id, event_language, event_restrict_language, event_start_date, "
                                          + "home_required, dim_source, event_title_es_source, shiny_rule, fixed_gender, gigantamax, event_region, event_region_source";

    public static Result Compact(string dbPath, int level, Action<string>? log = null)
    {
        if (level is not (1 or 2)) throw new ArgumentOutOfRangeException(nameof(level), "El nivel de compactacion es 1 o 2.");
        var before = new FileInfo(dbPath).Length;
        var steps = new List<string>();
        void Step(string text) { steps.Add(text); log?.Invoke(text); }

        using (var c = CatalogDatabase.Open(dbPath))
        {
            CatalogDatabase.Exec(c, "PRAGMA foreign_keys = OFF;");
            CatalogDatabase.Exec(c, "PRAGMA legacy_alter_table = ON;");   // los RENAME no reescriben claves foraneas, vistas ni disparadores de otros objetos
            if (CatalogDatabase.Count(c, "SELECT count(*) FROM sqlite_master WHERE name = 'availability' AND sql LIKE '%WITHOUT ROWID%'") != 0)
                throw new InvalidOperationException("El catalogo ya esta compactado.");

            foreach (var i in UnusedIndexes) CatalogDatabase.Exec(c, $"DROP INDEX IF EXISTS {i}");
            Step($"Indices sin uso quitados ({UnusedIndexes.Count}).");

            if (level == 2) { DictionaryLibrary(c); Step("library_entry: textos repetidos en diccionarios (vista con las mismas columnas y rowid)."); CoverageAsView(c); Step("coverage_case: vista sobre library_entry + solo evidencia y nota."); }
            else { CoverageWithoutRowid(c); Step("coverage_case: WITHOUT ROWID, sin la copia de la clave ni el UNIQUE de construccion."); }

            AvailabilityWithoutRowid(c);
            Step("availability: WITHOUT ROWID con la clave en el orden de las consultas.");

            CatalogDatabase.Exec(c, "DROP TABLE library_search");
            CatalogDatabase.Exec(c, Fts);
            CatalogDatabase.Exec(c, "INSERT INTO library_search(library_search) VALUES('rebuild')");
            CatalogDatabase.Exec(c, "INSERT INTO library_search(library_search) VALUES('integrity-check')");
            Step("library_search (FTS5): reconstruido sin los indices de prefijo.");

            CatalogDatabase.Exec(c, "PRAGMA legacy_alter_table = OFF;");
            CatalogDatabase.Exec(c, "PRAGMA foreign_keys = ON;");
            var fk = CatalogDatabase.Count(c, "SELECT count(*) FROM pragma_foreign_key_check");
            if (fk != 0) throw new InvalidOperationException($"La compactacion deja {fk} violaciones de clave foranea.");
            var integrity = CatalogDatabase.Scalar(c, "PRAGMA integrity_check;");
            if (integrity != "ok") throw new InvalidOperationException("integrity_check tras compactar: " + integrity);
            CatalogDatabase.CheckpointAndClose(c, vacuum: true);
        }
        var after = new FileInfo(dbPath).Length;
        Step($"Tamano: {before:N0} B -> {after:N0} B ({100.0 * (before - after) / before:F1} % menos).");
        return new Result(level, before, after, steps);
    }

    private static long Diff(SqliteConnection c, string a, string b) => CatalogDatabase.Count(c, $"SELECT count(*) FROM ({a} EXCEPT {b})");

    private static void CoverageWithoutRowid(SqliteConnection c)
    {
        CatalogDatabase.Exec(c, """
            CREATE TABLE cc2 (
             case_key TEXT PRIMARY KEY, release_id TEXT NOT NULL REFERENCES release(release_id), game_id INTEGER NOT NULL REFERENCES game(game_id), species_id INTEGER NOT NULL, form_id INTEGER NOT NULL,
             encounter_kind TEXT NOT NULL, variant_key TEXT NOT NULL, expected TEXT NOT NULL CHECK(expected IN ('Legalizable','AlreadyLegal','Rejected','Unknown')), evidence_ref TEXT NOT NULL,
             fixture_sha256 TEXT REFERENCES artifact(artifact_sha256), status TEXT NOT NULL DEFAULT 'Planned' CHECK(status IN ('Planned','Untested')), scope_note TEXT) STRICT, WITHOUT ROWID
            """);
        const string cols = "case_key, release_id, game_id, species_id, form_id, encounter_kind, variant_key, expected, evidence_ref, fixture_sha256, status, scope_note";
        CatalogDatabase.Exec(c, $"INSERT INTO cc2 SELECT {cols} FROM coverage_case");
        Same(c, $"SELECT {cols} FROM coverage_case", $"SELECT {cols} FROM cc2", "coverage_case");
        CatalogDatabase.Exec(c, "DROP TABLE coverage_case");
        CatalogDatabase.Exec(c, "ALTER TABLE cc2 RENAME TO coverage_case");
    }

    private static void Same(SqliteConnection c, string a, string b, string what)
    {
        var x = Diff(c, a, b);
        var y = Diff(c, b, a);
        if (x != 0 || y != 0) throw new InvalidOperationException($"{what}: la copia compacta difiere del original ({x}/{y} filas).");
    }

    private static void AvailabilityWithoutRowid(SqliteConnection c)
    {
        CatalogDatabase.Exec(c, """
            CREATE TABLE av2 (
             release_id TEXT NOT NULL REFERENCES release(release_id), game_id INTEGER NOT NULL REFERENCES game(game_id), species_id INTEGER NOT NULL, form_id INTEGER NOT NULL,
             state TEXT NOT NULL CHECK(state IN ('Present','Absent','Unknown')), reason_ref TEXT NOT NULL,
             form_storage TEXT NOT NULL DEFAULT 'NotApplicable' CHECK(form_storage IN ('Storable','PartyOnly','BattleOnly','TotemOnly','LordOnly','NotApplicable')),
             PRIMARY KEY(game_id, species_id, form_id, release_id), FOREIGN KEY(species_id, form_id) REFERENCES species_form(species_id, form_id)) STRICT, WITHOUT ROWID
            """);
        const string cols = "release_id, game_id, species_id, form_id, state, reason_ref, form_storage";
        CatalogDatabase.Exec(c, $"INSERT INTO av2 SELECT {cols} FROM availability");
        Same(c, $"SELECT {cols} FROM availability", $"SELECT {cols} FROM av2", "availability");
        CatalogDatabase.Exec(c, "DROP TABLE availability");
        CatalogDatabase.Exec(c, "ALTER TABLE av2 RENAME TO availability");
    }

    private static void DictionaryLibrary(SqliteConnection c)
    {
        foreach (var (col, dep) in new[] { ("names_all", "species_id"), ("name_es", "species_id"), ("dex_number", "species_id"), ("game_name", "game_id") })
            if (CatalogDatabase.Count(c, $"SELECT count(*) FROM (SELECT {dep} FROM library_entry GROUP BY {dep} HAVING count(DISTINCT {col}) > 1)") != 0)
                throw new InvalidOperationException($"library_entry.{col} no depende solo de {dep}: no se puede normalizar.");
        if (CatalogDatabase.Count(c, "SELECT count(*) FROM library_entry l JOIN game g ON g.game_id = l.game_id WHERE g.display_name_es <> l.game_name") != 0)
            throw new InvalidOperationException("library_entry.game_name no coincide con game.display_name_es.");

        CatalogDatabase.Exec(c, "CREATE TABLE le_text (id INTEGER PRIMARY KEY, txt TEXT NOT NULL) STRICT");
        CatalogDatabase.Exec(c, """
            INSERT INTO le_text(txt) SELECT txt FROM (SELECT release_id AS txt FROM library_entry UNION SELECT source_label FROM library_entry
              UNION SELECT dim_reason FROM library_entry WHERE dim_reason IS NOT NULL UNION SELECT variant_key FROM library_entry UNION SELECT expected FROM library_entry UNION SELECT status FROM library_entry) ORDER BY txt
            """);
        CatalogDatabase.Exec(c, "CREATE TEMP TABLE t_idx (txt TEXT PRIMARY KEY, id INTEGER) WITHOUT ROWID");
        CatalogDatabase.Exec(c, "INSERT INTO t_idx SELECT txt, id FROM le_text");
        CatalogDatabase.Exec(c, "CREATE TABLE le_species (species_id INTEGER PRIMARY KEY, dex_number INTEGER NOT NULL, name_es TEXT NOT NULL, names_all TEXT NOT NULL) STRICT");
        CatalogDatabase.Exec(c, "INSERT INTO le_species SELECT DISTINCT species_id, dex_number, name_es, names_all FROM library_entry ORDER BY species_id");
        CatalogDatabase.Exec(c, """
            CREATE TABLE le_base (
             entry_key TEXT NOT NULL, release_t INTEGER NOT NULL, game_id INTEGER NOT NULL, species_id INTEGER NOT NULL, form_id INTEGER NOT NULL, form_name TEXT, generation INTEGER,
             kind TEXT NOT NULL, variant_t INTEGER NOT NULL, availability TEXT NOT NULL, dim_reason_t INTEGER, addable INTEGER NOT NULL, form_storage TEXT NOT NULL,
             status_t INTEGER NOT NULL, expected_t INTEGER NOT NULL, encounter_key TEXT, source_label_t INTEGER NOT NULL, event_title TEXT, event_title_es TEXT, event_card_id TEXT,
             event_language TEXT, event_restrict_language TEXT, event_start_date TEXT, home_required INTEGER NOT NULL, dim_source TEXT, event_title_es_source TEXT, shiny_rule TEXT,
             fixed_gender INTEGER, gigantamax INTEGER NOT NULL, event_region TEXT, event_region_source TEXT) STRICT
            """);
        CatalogDatabase.Exec(c, """
            INSERT INTO le_base(rowid, entry_key, release_t, game_id, species_id, form_id, form_name, generation, kind, variant_t, availability, dim_reason_t, addable, form_storage,
              status_t, expected_t, encounter_key, source_label_t, event_title, event_title_es, event_card_id, event_language, event_restrict_language, event_start_date, home_required,
              dim_source, event_title_es_source, shiny_rule, fixed_gender, gigantamax, event_region, event_region_source)
            SELECT l.rowid, l.entry_key, (SELECT id FROM t_idx WHERE txt = l.release_id), l.game_id, l.species_id, l.form_id, l.form_name, l.generation, l.kind,
              (SELECT id FROM t_idx WHERE txt = l.variant_key), l.availability, (SELECT id FROM t_idx WHERE txt = l.dim_reason), l.addable, l.form_storage,
              (SELECT id FROM t_idx WHERE txt = l.status), (SELECT id FROM t_idx WHERE txt = l.expected), l.encounter_key, (SELECT id FROM t_idx WHERE txt = l.source_label), l.event_title,
              l.event_title_es, l.event_card_id, l.event_language, l.event_restrict_language, l.event_start_date, l.home_required, l.dim_source, l.event_title_es_source, l.shiny_rule,
              l.fixed_gender, l.gigantamax, l.event_region, l.event_region_source FROM library_entry l ORDER BY l.rowid
            """);
        CatalogDatabase.Exec(c, "ALTER TABLE library_entry RENAME TO library_entry_orig");   // arrastra sus disparadores e indices
        // Subconsultas escalares y no JOIN: el planificador de SQLite mantiene el FTS5 como tabla conductora (con JOIN lo dejaba el ultimo y la busqueda por texto pasaba de 0,4 a 340 ms).
        string T(string col) => $"(SELECT txt FROM le_text WHERE id = b.{col})";
        CatalogDatabase.Exec(c, $"""
            CREATE VIEW library_entry AS SELECT b.rowid AS rowid, b.entry_key, {T("release_t")} AS release_id, b.game_id, (SELECT display_name_es FROM game WHERE game_id = b.game_id) AS game_name,
              b.species_id, b.form_id, (SELECT dex_number FROM le_species WHERE species_id = b.species_id) AS dex_number, (SELECT name_es FROM le_species WHERE species_id = b.species_id) AS name_es,
              (SELECT names_all FROM le_species WHERE species_id = b.species_id) AS names_all, b.form_name, b.generation, b.kind, {T("variant_t")} AS variant_key, b.availability,
              {T("dim_reason_t")} AS dim_reason, b.addable, b.form_storage, {T("status_t")} AS status, {T("expected_t")} AS expected, b.encounter_key, {T("source_label_t")} AS source_label,
              b.event_title, b.event_title_es, b.event_card_id, b.event_language, b.event_restrict_language, b.event_start_date, b.home_required, b.dim_source, b.event_title_es_source,
              b.shiny_rule, b.fixed_gender, b.gigantamax, b.event_region, b.event_region_source FROM le_base b
            """);
        Same(c, $"SELECT rowid, {LibraryColumns} FROM library_entry_orig", $"SELECT rowid, {LibraryColumns} FROM library_entry", "library_entry");
        CatalogDatabase.Exec(c, "DROP TABLE library_entry_orig");
        // Los indices que la app usa, ahora sobre la tabla base (las mismas columnas y orden; dex_number sale de la especie y ya no se indexa).
        CatalogDatabase.Exec(c, "CREATE UNIQUE INDEX le_clave ON le_base(entry_key)");
        CatalogDatabase.Exec(c, "CREATE INDEX ix_library_game_species ON le_base(game_id, species_id, form_id)");
        CatalogDatabase.Exec(c, "CREATE INDEX ix_library_game_kind ON le_base(game_id, kind, availability, species_id)");
        CatalogDatabase.Exec(c, "CREATE INDEX ix_library_generation ON le_base(generation, kind, species_id)");
        CatalogDatabase.Exec(c, "CREATE INDEX ix_library_event_lang ON le_base(event_language, game_id) WHERE event_language IS NOT NULL");
    }

    private static void CoverageAsView(SqliteConnection c)
    {
        const string cols = "case_key, release_id, game_id, species_id, form_id, encounter_kind, variant_key, expected, evidence_ref, fixture_sha256, status, scope_note";
        var bad = CatalogDatabase.Count(c, """
            SELECT count(*) FROM coverage_case k LEFT JOIN library_entry l ON l.entry_key = k.case_key
             WHERE l.rowid IS NULL OR l.release_id <> k.release_id OR l.game_id <> k.game_id OR l.species_id <> k.species_id OR l.form_id <> k.form_id OR l.kind <> k.encounter_kind
                OR l.variant_key <> k.variant_key OR l.expected <> k.expected OR l.status <> k.status OR k.fixture_sha256 IS NOT NULL
            """);
        if (bad != 0 || CatalogDatabase.Count(c, "SELECT count(*) FROM coverage_case") != CatalogDatabase.Count(c, "SELECT count(*) FROM library_entry"))
            throw new InvalidOperationException($"coverage_case no se deriva de library_entry ({bad} filas distintas).");
        CatalogDatabase.Exec(c, "CREATE TABLE coverage_scope (scope_id INTEGER PRIMARY KEY, scope_note TEXT NOT NULL) STRICT");
        CatalogDatabase.Exec(c, "INSERT INTO coverage_scope(scope_note) SELECT DISTINCT scope_note FROM coverage_case WHERE scope_note IS NOT NULL ORDER BY 1");
        CatalogDatabase.Exec(c, "CREATE TABLE coverage_note (entry_rowid INTEGER PRIMARY KEY, evidence_ref TEXT NOT NULL, scope_id INTEGER) STRICT");
        CatalogDatabase.Exec(c, """
            INSERT INTO coverage_note SELECT l.rowid, k.evidence_ref, s.scope_id FROM coverage_case k JOIN library_entry l ON l.entry_key = k.case_key
              LEFT JOIN coverage_scope s ON s.scope_note = k.scope_note ORDER BY l.rowid
            """);
        // Las tablas de resultados de cobertura (vacias en el catalogo del producto) apuntaban a coverage_case con una clave foranea: con una VISTA como padre, pragma_foreign_key_check falla.
        // Se vuelven a crear sin esa referencia; solo se hace si estan vacias (no se pierde ningun dato).
        var referring = new List<(string Name, string Sql)>();
        using (var q = c.CreateCommand())
        {
            q.CommandText = "SELECT name, sql FROM sqlite_master WHERE type = 'table' AND sql LIKE '%REFERENCES coverage_case%'";
            using var r = q.ExecuteReader();
            while (r.Read()) referring.Add((r.GetString(0), r.GetString(1)));
        }
        foreach (var (name, sql) in referring)
        {
            if (CatalogDatabase.Count(c, $"SELECT count(*) FROM \"{name}\"") != 0)
                throw new InvalidOperationException($"{name} tiene filas y referencia a coverage_case: no se puede convertir coverage_case en vista.");
            CatalogDatabase.Exec(c, $"DROP TABLE \"{name}\"");
            CatalogDatabase.Exec(c, System.Text.RegularExpressions.Regex.Replace(sql, @"\s*REFERENCES\s+coverage_case\s*\(case_key\)", string.Empty));
        }
        CatalogDatabase.Exec(c, "ALTER TABLE coverage_case RENAME TO coverage_case_orig");
        CatalogDatabase.Exec(c, """
            CREATE VIEW coverage_case AS SELECT l.entry_key AS case_key, l.release_id, l.game_id, l.species_id, l.form_id, l.kind AS encounter_kind, l.variant_key, l.expected, n.evidence_ref,
              NULL AS fixture_sha256, l.status, (SELECT scope_note FROM coverage_scope WHERE scope_id = n.scope_id) AS scope_note
              FROM library_entry l JOIN coverage_note n ON n.entry_rowid = l.rowid
            """);
        Same(c, $"SELECT {cols} FROM coverage_case_orig", $"SELECT {cols} FROM coverage_case", "coverage_case");
        CatalogDatabase.Exec(c, "DROP TABLE coverage_case_orig");
    }
}
