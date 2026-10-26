// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;

namespace AutoLegalidad.Catalog.Library;

/// <summary>Una fila de la Libreria lista para pintar (la UI del Hito 4 usa estas consultas tal cual).</summary>
public sealed record LibraryRow(string EntryKey, int GameId, string GameName, int SpeciesId, int FormId, int DexNumber, string NameEs, string? FormName, string Kind,
    string Variant, bool Available, string? DimReason, bool Addable, string Status, string? EventTitleEs, string? EventTitle, string? EventLanguage, string? EventStartDate,
    string SourceLabel, string? EncounterKey, bool HomeRequired = false, string? DimSource = null, string? EventTitleEsSource = null,
    string? ShinyRule = null, int? FixedGender = null, bool Gigantamax = false, int OriginGame = 0, string? EventRegion = null, string? EventRegionSource = null);

/// <summary>Filtros de la Libreria: se combinan con AND.</summary>
public sealed record LibraryFilter
{
    /// <summary>Juego de DESTINO (la partida abierta). Lo que no existe en ese juego sale atenuado, con su motivo.</summary>
    public int GameId { get; init; }
    /// <summary>
    /// Colosseum y XD comparten juego de la Librería (CXD) pero no encuentros: <see cref="GameCubeIds.Colosseum"/> o <see cref="GameCubeIds.Xd"/> cuando el destino es uno de los dos; 0 en cualquier otro juego.
    /// Lo que solo existe en el OTRO juego del GameCube sale atenuado, con su motivo (y no cuenta como añadible).
    /// </summary>
    public int GcDestination { get; init; }
    /// <summary>Texto libre: nombre en español (o de otro idioma), forma, titulo de evento, id de carta, juego; o un numero de Pokedex nacional.</summary>
    public string? Text { get; init; }
    public string? Kind { get; init; }
    public int? Generation { get; init; }
    public int? Species { get; init; }
    public int? Form { get; init; }
    public string? EventLanguage { get; init; }
    /// <summary>Hito 5d (ADR 0047): código de región (<c>US</c>, <c>EU</c>, <c>JP</c>, <c>KO</c>, <c>TW</c>, <c>INT</c>); solo casan los eventos cuya región aportan el Core o la documentación de la tarjeta.</summary>
    public string? EventRegion { get; init; }
    public string? EventFrom { get; init; }
    public string? EventTo { get; init; }
    public bool OnlyAvailable { get; init; }
    public bool OnlyAddable { get; init; }
    /// <summary>Excluye las entradas que solo existen pasando por HOME (<c>home_required</c>): lo que la Libreria ofrece SIN el ajuste «sin matricula».</summary>
    public bool ExcludeHomeRequired { get; init; }
    public int Limit { get; init; } = 50;
    public int Offset { get; init; }
}

/// <summary>Consultas de la Libreria (navegacion + buscador). Usan los indices de <c>library_entry</c> y el FTS5 <c>library_search</c>; responden en milisegundos.</summary>
public static class LibraryQueries
{
    private const string Columns = """
        e.entry_key, e.game_id, e.game_name, e.species_id, e.form_id, e.dex_number, e.name_es, e.form_name, e.kind, e.variant_key, e.availability, e.dim_reason, e.addable,
        e.status, e.event_title_es, e.event_title, e.event_language, e.event_start_date, e.source_label, e.encounter_key, e.home_required, e.dim_source, e.event_title_es_source, e.shiny_rule, e.fixed_gender, e.gigantamax,
        COALESCE((SELECT x.origin_game FROM encounter x WHERE x.encounter_key = e.encounter_key), 0), e.event_region, e.event_region_source
        """;

    /// <summary>Convierte lo que teclea el usuario en una consulta FTS5 segura: cada palabra, por prefijo, sin tildes (el tokenizador las ignora).</summary>
    public static string ToFtsQuery(string text)
    {
        var tokens = new List<string>();
        var sb = new StringBuilder();
        foreach (var ch in text)
        {
            if (char.IsLetterOrDigit(ch)) sb.Append(ch);
            else if (sb.Length > 0) { tokens.Add(sb.ToString()); sb.Clear(); }
        }
        if (sb.Length > 0) tokens.Add(sb.ToString());
        return string.Join(' ', tokens.Select(t => "\"" + t + "\"*"));
    }

    private static SqliteCommand Build(SqliteConnection c, LibraryFilter f, bool count)
    {
        var where = new List<string> { "e.game_id = $game" };
        var cmd = c.CreateCommand();
        cmd.Parameters.AddWithValue("$game", f.GameId);
        var join = string.Empty;
        var order = "e.species_id, e.form_id, e.kind, e.variant_key";

        if (!string.IsNullOrWhiteSpace(f.Text))
        {
            var text = f.Text.Trim();
            if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var dex))
            {
                where.Add("e.dex_number = $dex");   // numero de Pokedex nacional
                cmd.Parameters.AddWithValue("$dex", dex);
            }
            else
            {
                var fts = ToFtsQuery(text);
                if (fts.Length == 0)
                    where.Add("0");
                else
                {
                    join = "JOIN library_search s ON s.rowid = e.rowid";
                    where.Add("library_search MATCH $q");
                    cmd.Parameters.AddWithValue("$q", fts);
                    order = "s.rank, " + order;
                }
            }
        }
        if (f.Kind is not null) { where.Add("e.kind = $kind"); cmd.Parameters.AddWithValue("$kind", f.Kind); }
        if (f.Generation is not null) { where.Add("e.generation = $gen"); cmd.Parameters.AddWithValue("$gen", f.Generation); }
        if (f.Species is not null) { where.Add("e.species_id = $sp"); cmd.Parameters.AddWithValue("$sp", f.Species); }
        if (f.Form is not null) { where.Add("e.form_id = $form"); cmd.Parameters.AddWithValue("$form", f.Form); }
        if (f.EventLanguage is not null) { where.Add("e.event_language = $lang"); cmd.Parameters.AddWithValue("$lang", f.EventLanguage); }
        if (f.EventRegion is not null) { where.Add("e.event_region LIKE $region"); cmd.Parameters.AddWithValue("$region", "%," + f.EventRegion + ",%"); }
        if (f.EventFrom is not null) { where.Add("e.event_start_date >= $from"); cmd.Parameters.AddWithValue("$from", f.EventFrom); }
        if (f.EventTo is not null) { where.Add("e.event_start_date <= $to"); cmd.Parameters.AddWithValue("$to", f.EventTo); }
        if (f.OnlyAvailable) where.Add("e.availability = 'Available'");
        if (f.OnlyAddable) where.Add("e.addable = 1");
        if (GameCubeIds.IsGameCube(f.GcDestination) && f.OnlyAddable)
        {
            where.Add($"(COALESCE((SELECT x.origin_game FROM encounter x WHERE x.encounter_key = e.encounter_key), 0) NOT IN ({GameCubeIds.Colosseum}, {GameCubeIds.Xd}) OR (SELECT x.origin_game FROM encounter x WHERE x.encounter_key = e.encounter_key) = $gc)");
            cmd.Parameters.AddWithValue("$gc", f.GcDestination);
        }
        if (f.OnlyAddable && CriaSinHuevo.SqlExclusion(f.GameId, f.GcDestination) is { } noEggs) where.Add(noEggs);   // Hito 5c: la cría sin huevo en el Core no se ofrece (en ningún juego)
        if (f.ExcludeHomeRequired) where.Add("e.home_required = 0");

        if (count)
        {
            cmd.CommandText = $"SELECT count(*) FROM library_entry e {join} WHERE {string.Join(" AND ", where)}";
            return cmd;
        }
        cmd.CommandText = $"SELECT {Columns} FROM library_entry e {join} WHERE {string.Join(" AND ", where)} ORDER BY {order} LIMIT $limit OFFSET $offset";
        cmd.Parameters.AddWithValue("$limit", f.Limit);
        cmd.Parameters.AddWithValue("$offset", f.Offset);
        return cmd;
    }

    public static IReadOnlyList<LibraryRow> Query(SqliteConnection c, LibraryFilter f)
    {
        using var cmd = Build(c, f, count: false);
        var rows = new List<LibraryRow>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            rows.Add(WrongGameCube(new LibraryRow(r.GetString(0), r.GetInt32(1), r.GetString(2), r.GetInt32(3), r.GetInt32(4), r.GetInt32(5), r.GetString(6), Str(r, 7), r.GetString(8), r.GetString(9),
                r.GetString(10) == "Available", Str(r, 11), r.GetInt32(12) == 1, r.GetString(13), Str(r, 14), Str(r, 15), Str(r, 16), Str(r, 17), r.GetString(18), Str(r, 19), r.GetInt32(20) == 1, Str(r, 21), Str(r, 22), Str(r, 23), r.IsDBNull(24) ? null : r.GetInt32(24), r.GetInt32(25) == 1, r.GetInt32(26), Str(r, 27), Str(r, 28)), f.GcDestination));
        return rows;
    }

    /// <summary>Texto del motivo cuando el origen de la entrada es del OTRO juego del GameCube (Colosseum y XD no comparten encuentros, solo la entrada de la Librería).</summary>
    public const string OnlyXdReason = "Este origen solo existe en Pokémon XD: el destino es Pokémon Colosseum";
    public const string OnlyColosseumReason = "Este origen solo existe en Pokémon Colosseum: el destino es Pokémon XD";

    private static LibraryRow WrongGameCube(LibraryRow row, int gc)
    {
        if (!row.Addable) return row;
        // Hito 5c: una cría solo se ofrece si el Core enumera un huevo para la especie en ese destino (tabla medida con el Core, ver CriaSinHuevo); si no, atenuada con su motivo y sin clave.
        if (row.Kind == "Egg" && CriaSinHuevo.Contains(CriaSinHuevo.DestinationOf(row.GameId, gc), row.SpeciesId, row.FormId))
            return row with { Addable = false, DimReason = CriaSinHuevo.Reason(CriaSinHuevo.DestinationOf(row.GameId, gc)), DimSource = "egg-absent" };
        if (!GameCubeIds.IsGameCube(gc) || !GameCubeIds.IsGameCube(row.OriginGame) || row.OriginGame == gc) return row;
        return row with { Addable = false, DimReason = gc == GameCubeIds.Colosseum ? OnlyXdReason : OnlyColosseumReason, DimSource = "gamecube-origin" };
    }

    /// <summary>Total de entradas que cumplen el filtro (para paginar sin cargar todo).</summary>
    public static long Count(SqliteConnection c, LibraryFilter f)
    {
        using var cmd = Build(c, f, count: true);
        return Convert.ToInt64(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Ficha de una entrada: disponibilidad (con su referencia al Core), cobertura, procedencia (fuente con commit y hash del artefacto), descriptor del encuentro
    /// o evento (nombre, fechas, idioma, OT fijo...) y rasgos FIJOS y VARIABLES. Se compone con JOIN sobre las tablas de verdad (sin copias): no hay JSON duplicado.
    /// </summary>
    public static string? Detail(SqliteConnection c, string entryKey)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
            SELECT l.kind, l.variant_key, l.status, l.expected, l.dim_reason, l.addable, l.form_storage, l.source_label,
                   a.state, a.reason_ref, k.evidence_ref, k.scope_note,
                   e.encounter_key, e.descriptor_json, e.source_ref, e.artifact_sha256, e.source_version_id, v.revision
              FROM library_entry l
              JOIN availability a ON a.release_id = l.release_id AND a.game_id = l.game_id AND a.species_id = l.species_id AND a.form_id = l.form_id
              JOIN coverage_case k ON k.case_key = l.entry_key
              LEFT JOIN encounter e ON e.encounter_key = l.encounter_key
              LEFT JOIN source_version v ON v.source_version_id = e.source_version_id
             WHERE l.entry_key = $k
            """;
        cmd.Parameters.AddWithValue("$k", entryKey);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        var kind = r.GetString(0);
        System.Text.Json.JsonElement? descriptor = r.IsDBNull(13) ? null : System.Text.Json.JsonDocument.Parse(r.GetString(13)).RootElement.Clone();
        object? fixedTraits, variableTraits;
        if (descriptor is { } d && d.TryGetProperty("fixedTraits", out var ft) && d.TryGetProperty("variableTraits", out var vt))
        {
            fixedTraits = ft.Clone();
            variableTraits = vt.Clone();
        }
        else
        {
            fixedTraits = new List<object?> { "species", "form" };
            variableTraits = new List<object?> { "level-within-range", "ivs-by-method", "nature-or-pid-by-generation-rules" };
        }
        var detail = new Dictionary<string, object?>
        {
            ["entry_key"] = entryKey, ["kind"] = kind, ["variant"] = r.GetString(1), ["addable"] = r.GetInt32(5) == 1, ["dim_reason"] = Str(r, 4),
            ["source_label"] = r.GetString(7),
            ["availability"] = new Dictionary<string, object?> { ["state"] = r.GetString(8), ["form_storage"] = r.GetString(6), ["reason_ref"] = r.GetString(9) },
            ["coverage"] = new Dictionary<string, object?> { ["status"] = r.GetString(2), ["expected"] = r.GetString(3), ["evidence_ref"] = r.GetString(10), ["note"] = Str(r, 11) },
            ["traits"] = new Dictionary<string, object?> { ["fixed"] = fixedTraits, ["variable"] = variableTraits },
            ["encounter"] = descriptor,
            ["provenance"] = r.IsDBNull(12) ? null : new Dictionary<string, object?>
            {
                ["encounter_key"] = r.GetString(12), ["source_ref"] = r.GetString(14), ["artifact_sha256"] = Str(r, 15),
                ["source_version_id"] = r.GetString(16), ["revision"] = r.GetString(17),
            },
        };
        return Model.CanonicalJson.SerializeIndented(detail);
    }

    private static string? Str(SqliteDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);
}
