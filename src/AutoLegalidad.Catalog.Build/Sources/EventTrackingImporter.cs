// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Globalization;
using AutoLegalidad.Catalog.Db;
using AutoLegalidad.Catalog.Model;
using AutoLegalidad.Catalog.Sources.Spreadsheets;
using Microsoft.Data.Sqlite;

namespace AutoLegalidad.Catalog.Sources;

/// <summary>Una fila de las hojas de seguimiento ya interpretada (sin el texto original: solo la ventana, la clave de enlace y de donde sale).</summary>
public sealed record TrackingRow(string Type, int WcId, string SpeciesName, TrackingWindow Window, string File, string Sheet, int SheetRow, string FileSha256);

/// <summary>Ventana derivada para UNA tarjeta del catalogo (solo metadatos: fechas, procedencia y hash del fichero).</summary>
public sealed record EventWindowRow(string EncounterKey, DateOnly Start, DateOnly? End, string SourceRef, string FileSha256);

public sealed class TrackingStage
{
    public required IReadOnlyList<EventWindowRow> Windows { get; init; }
    public required IReadOnlyDictionary<string, object?> Stats { get; init; }
}

/// <summary>
/// Hito 5 (datos, ADR 0029): fechas de distribucion de los eventos de 6.ª y 7.ª generacion a partir de las hojas <c>Extras/Tracking</c> de EventsGallery
/// (<c>wondercard 6 list.xls</c>, hoja «Events»; <c>WC7 Tracker.xlsx</c>, hoja «Events»). Las tarjetas «full» WC6/WC7 no traen fecha propia (el Core pone la de HOY) y hasta ahora
/// se trataban como «sin ventana». Aqui se enlazan por (tipo, id de tarjeta, especie) y SOLO se guardan metadatos derivados (fechas + hash del fichero): ningun byte ni texto de
/// EventsGallery sale hacia Git ni hacia la base (que es artefacto privado).
/// </summary>
public static class EventTrackingImporter
{
    public const string RelativeDir = "Extras/Tracking";
    public const string Wc6File = "wondercard 6 list.xls";
    public const string Wc7File = "WC7 Tracker.xlsx";
    public const string LegalityFile = "wondercard 6  Legality version.xls";

    /// <summary>Lanzamiento de X/Y y de Sol/Luna (primeros dias posibles de una distribucion): una fecha anterior es un error de la hoja y se descarta.</summary>
    public static readonly DateOnly Wc6Floor = new(2013, 10, 12), Wc7Floor = new(2016, 11, 18);

    /// <summary>Duracion maxima creible de una ventana (dias): una mayor se considera error de la hoja y el FIN se trata como desconocido (el inicio se conserva).</summary>
    public const int MaxWindowDays = 800;

    public static TrackingStage Stage(SourceClone clone, SqliteConnection c, DateOnly dateNotAfter, Action<string>? log = null)
    {
        clone.VerifyPinned();
        var dir = Path.Combine(clone.Path, "Extras", "Tracking");
        var stats = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        var rows = new List<TrackingRow>();
        var sha = new Dictionary<string, string>();

        var wc6 = Path.Combine(dir, Wc6File);
        var wc7 = Path.Combine(dir, Wc7File);
        var legality = Path.Combine(dir, LegalityFile);
        if (File.Exists(legality)) stats["sha256_" + LegalityFile] = Hashing.Sha256Hex(File.ReadAllBytes(legality)); // sin fechas: solo su hash
        if (File.Exists(wc6)) rows.AddRange(ReadWc6(File.ReadAllBytes(wc6), sha, stats));
        else log?.Invoke($"Tracking: falta {Wc6File}; sin ventanas de WC6.");
        if (File.Exists(wc7)) rows.AddRange(ReadWc7(File.ReadAllBytes(wc7), sha, stats));
        else log?.Invoke($"Tracking: falta {Wc7File}; sin ventanas de WC7.");
        foreach (var kv in sha) stats["sha256_" + kv.Key] = kv.Value;

        // Nombres en ingles de las especies para enlazar la columna «Species».
        var species = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "SELECT species_id, name FROM species_name WHERE language_code='en'";
            using var r = cmd.ExecuteReader();
            while (r.Read()) species[Normalize(r.GetString(1))] = r.GetInt32(0);
        }

        // Tarjetas generables WC6/WC7 sin fecha de inicio propia.
        var candidates = new List<(string Key, int Species, string Type, int CardId)>();
        var withOwnDate = 0;
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = @"SELECT encounter_key, species_id, json_extract(descriptor_json,'$.type'), COALESCE(json_extract(descriptor_json,'$.cardId'),0), json_extract(descriptor_json,'$.startDate')
                                FROM v_encounter_generable WHERE json_extract(descriptor_json,'$.type') IN ('WC6','WC7')";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                if (!r.IsDBNull(4)) { withOwnDate++; continue; }
                candidates.Add((r.GetString(0), r.GetInt32(1), r.GetString(2), r.GetInt32(3)));
            }
        }

        // Fecha por (tipo, id, especie): union de todas las filas que coinciden (variantes por region: un dia dado por cualquiera de ellas es real).
        var byKey = new Dictionary<(string, int, int), List<TrackingRow>>();
        var unknownSpecies = 0;
        foreach (var row in rows)
        {
            if (!species.TryGetValue(Normalize(row.SpeciesName), out var sp)) { unknownSpecies++; continue; }
            if (!byKey.TryGetValue((row.Type, row.WcId, sp), out var list)) byKey[(row.Type, row.WcId, sp)] = list = [];
            list.Add(row);
        }

        var windows = new List<EventWindowRow>();
        int noId = 0, noRow = 0, tooEarly = 0, tooLate = 0, endDiscarded = 0;
        foreach (var (key, sp, type, card) in candidates)
        {
            if (card == 0) { noId++; continue; }
            if (!byKey.TryGetValue((type, card, sp), out var list)) { noRow++; continue; }
            var floor = type == "WC6" ? Wc6Floor : Wc7Floor;
            var valid = new List<TrackingRow>();
            foreach (var row in list)
            {
                if (row.Window.Start < floor) { tooEarly++; continue; }
                if (row.Window.Start > dateNotAfter) { tooLate++; continue; }
                valid.Add(row);
            }
            if (valid.Count == 0) continue;
            var start = valid.Min(v => v.Window.Start);
            DateOnly? end = valid.All(v => v.Window.End is not null) ? valid.Max(v => v.Window.End) : null;
            if (end is { } e && (e < start || e.DayNumber - start.DayNumber > MaxWindowDays)) { end = null; endDiscarded++; }
            var first = valid.OrderBy(v => v.Window.Start).ThenBy(v => v.SheetRow).First();
            windows.Add(new EventWindowRow(key, start, end, $"{SourceId(clone)}:{RelativeDir}/{first.File}#{first.Sheet}!R{first.SheetRow}", first.FileSha256));
        }

        stats["tarjetas_generables_wc6_wc7"] = candidates.Count + withOwnDate;
        stats["con_fecha_propia_no_se_tocan"] = withOwnDate;
        stats["candidatas_sin_fecha"] = candidates.Count;
        stats["candidatas_sin_id_de_tarjeta"] = noId;
        stats["candidatas_sin_fila_en_la_hoja"] = noRow;
        stats["filas_hoja_leidas"] = rows.Count;
        stats["filas_con_especie_desconocida"] = unknownSpecies;
        stats["filas_descartadas_por_inicio_anterior_al_lanzamiento"] = tooEarly;
        stats["filas_descartadas_por_inicio_posterior_al_core"] = tooLate;
        stats["ventanas_con_fin_descartado"] = endDiscarded;
        stats["ventanas_derivadas"] = windows.Count;
        stats["ventanas_derivadas_con_fin_conocido"] = windows.Count(w => w.End is not null);
        log?.Invoke($"Tracking: {windows.Count} ventanas derivadas para {candidates.Count} tarjetas WC6/WC7 sin fecha ({rows.Count} filas leidas).");
        return new TrackingStage { Windows = windows, Stats = stats };
    }

    private static string SourceId(SourceClone clone) => $"eventsgallery@{clone.Commit12}";

    /// <summary>Normaliza un nombre de especie para compararlo: sin parentesis de forma («Vulpix (Alolan)»), sin tildes ni tipografia, en minusculas.</summary>
    public static string Normalize(string name)
    {
        var n = name.Trim();
        var p = n.IndexOf('(');
        if (p > 0) n = n[..p];
        return n.Trim().Replace('’', '\'').ToLowerInvariant();
    }

    // ---------------------------------------------------------------- hojas

    public static IEnumerable<TrackingRow> ReadWc6(byte[] file, Dictionary<string, string> sha, IDictionary<string, object?> stats)
    {
        var hash = Hashing.Sha256Hex(file);
        sha[Wc6File] = hash;
        var grid = Biff8Workbook.Open(file).ReadSheet("Events");
        var id = grid.Find("WC6 ID") ?? throw new InvalidDataException("Hoja Events sin columna «WC6 ID».");
        var sp = grid.Find("Species") ?? throw new InvalidDataException("Hoja Events sin columna «Species».");
        var desc = grid.Find("Description") ?? throw new InvalidDataException("Hoja Events sin columna «Description».");
        var result = new List<TrackingRow>();
        int rows = 0, parsed = 0, discrete = 0;
        for (var r = id.Row + 1; r < grid.RowCount; r++)
        {
            if (grid.Number(r, id.Col) is not { } wc || wc <= 0) continue;
            var text = grid.Get(r, desc.Col);
            if (string.IsNullOrWhiteSpace(text)) continue;
            rows++;
            if (TrackingDates.Parse(text) is not { } w) { discrete++; continue; }
            parsed++;
            result.Add(new TrackingRow("WC6", (int)wc, grid.Get(r, sp.Col), w, Wc6File, "Events", r + 1, hash));
        }
        stats["wc6_filas_con_fecha_en_texto"] = rows;
        stats["wc6_filas_interpretadas"] = parsed;
        stats["wc6_filas_no_interpretables_o_listas_de_dias"] = discrete;
        return result;
    }

    public static IEnumerable<TrackingRow> ReadWc7(byte[] file, Dictionary<string, string> sha, IDictionary<string, object?> stats)
    {
        var hash = Hashing.Sha256Hex(file);
        sha[Wc7File] = hash;
        var grid = XlsxWorkbook.ReadSheet(file, "Events");
        var id = grid.Find("WC7 ID") ?? throw new InvalidDataException("Hoja Events sin columna «WC7 ID».");
        var sp = grid.Find("Event and Card Color") ?? throw new InvalidDataException("Hoja Events sin columna «Event and Card Color».");
        var dt = grid.Find("Date") ?? throw new InvalidDataException("Hoja Events sin columna «Date».");
        var result = new List<TrackingRow>();
        int rows = 0, parsed = 0;
        for (var r = id.Row + 1; r < grid.RowCount; r++)
        {
            if (grid.Number(r, id.Col) is not { } wc || wc <= 0) continue;
            if (grid.Number(r, dt.Col) is not { } serial || XlsxWorkbook.FromSerial(serial) is not { } start) continue;
            rows++;
            parsed++;
            result.Add(new TrackingRow("WC7", (int)wc, grid.Get(r, sp.Col), new TrackingWindow(start, TrackingDates.ParseEnd(grid.Get(r, dt.Col + 1))), Wc7File, "Events", r + 1, hash));
        }
        stats["wc7_filas_con_fecha_de_inicio"] = rows;
        stats["wc7_filas_interpretadas"] = parsed;
        return result;
    }

    // ---------------------------------------------------------------- persistencia

    public static void Persist(SqliteConnection c, TrackingStage stage)
    {
        using var tx = c.BeginTransaction();
        try
        {
            using (var ins = new PreparedInsert(c, tx, "event_window", "encounter_key", "start_day", "end_day", "source_ref", "file_sha256"))
                foreach (var w in stage.Windows)
                    ins.Run(w.EncounterKey, Iso(w.Start), w.End is { } e ? Iso(e) : null, w.SourceRef, w.FileSha256);
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    private static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
