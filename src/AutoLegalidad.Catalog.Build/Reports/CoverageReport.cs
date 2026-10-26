// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AutoLegalidad.Catalog.Db;

namespace AutoLegalidad.Catalog.Reports;

/// <summary>Estado de una tupla (juego, especie, forma, clase de encuentro, variante) en la matriz de cobertura. Nombres: los estados de la regla 10 del proyecto.</summary>
public enum TupleState
{
    /// <summary>OBSERVED: el motor la generó y el Core oficial la valida (medido).</summary>
    Observed,
    /// <summary>Imposible DEMOSTRADA con el Core (búsqueda completa y motivo exacto): NOT_OBSERVED con prueba. No es un fallo: se atenúa en la Librería con su motivo.</summary>
    ProvenImpossible,
    /// <summary>BLOCKED: una regla del proyecto lo impide (p. ej. regla 6: no se crea una matrícula de HOME).</summary>
    Blocked,
    /// <summary>NOT_APPLICABLE: la medida no tiene sentido para esa tupla.</summary>
    NotApplicable,
    /// <summary>INCONCLUSIVE: se midió y no se pudo decidir (plazo, azar); NUNCA se cuenta como imposible.</summary>
    Inconclusive,
    /// <summary>ERROR: el arnés falló al medirla.</summary>
    Error,
    /// <summary>NOT_OBSERVED: no hay ninguna medida (faltante).</summary>
    NotObserved,
}

/// <summary>Una tupla del alcance: lo que sale de <c>coverage_case</c>.</summary>
public sealed record CoverageTuple(string CaseKey, string Game, int Generation, string Context, string Kind, string Expected);

/// <summary>Una medida: estado y cobertura del TSV del banco (columnas <c>estado</c> y <c>cobertura</c>).</summary>
public sealed record Measurement(string State, string Coverage);

/// <summary>Datos que acompañan al informe (versión del paquete, fecha...). Nada personal.</summary>
public sealed record ReportMeta(string Version, string Commit, string DateText);

/// <summary>Una tabla del informe: se pinta igual en Markdown y en HTML.</summary>
public sealed record ReportTable(string Title, string[] Headers, List<string[]> Rows, string? Note = null);

/// <summary>El informe entero: título, párrafos introductorios y secciones (cada una con su texto y sus tablas).</summary>
public sealed record ReportDocument(string Title, List<string> Intro, List<ReportSection> Sections);

public sealed record ReportSection(string Heading, List<string> Paragraphs, List<ReportTable> Tables);

/// <summary>
/// Informe de cobertura publicable (propuesta del Hito 1, ADR 0050; regla 10: nada de «cobertura completa» sin la matriz). Se genera con <c>pokekeep-catalogo informe-cobertura</c> a partir de la base
/// construida (alcance: <c>coverage_case</c>; Librería) y de las mediciones YA hechas en <c>docs/datos</c> (TSV del banco del Hito 3b, bloqueos de la Librería, matriz por guardado del Hito 5), con los
/// estados OBSERVED / NOT_OBSERVED / INCONCLUSIVE / NOT_APPLICABLE / BLOCKED / ERROR. No mide nada por sí mismo, no abre ningún guardado y no contiene datos personales (solo recuentos y nombres de juegos).
/// </summary>
public static class CoverageReport
{
    public static readonly string[] MeasuredClasses = ["wild", "static", "gift", "trade", "egg", "event", "raid", "go", "transfer", "formnormalization", "absent"];

    // ------------------------------------------------------------------------------------------------------------------ clasificación (pura, probada)
    /// <summary>Estado de una tupla a partir de su medida (o de la ausencia de ella) y de lo que la interfaz real demostró después (Hito 5c).</summary>
    public static TupleState Classify(Measurement? measured, bool provenImpossibleLater)
    {
        if (measured is null) return TupleState.NotObserved;
        var cov = measured.Coverage.ToUpperInvariant();
        TupleState state;
        switch (measured.State.ToUpperInvariant())
        {
            case "OBSERVED": state = TupleState.Observed; break;
            case "BLOCKED": state = TupleState.Blocked; break;
            case "NOT_APPLICABLE": state = TupleState.NotApplicable; break;
            case "INCONCLUSIVE": state = TupleState.Inconclusive; break;
            case "ERROR": state = TupleState.Error; break;
            // NOT_OBSERVED solo es «imposible demostrada» si el banco lo marco asi con su prueba; sin prueba es un faltante, nunca una imposibilidad (regla 5).
            case "NOT_OBSERVED": state = cov == "IMPOSIBLE_DEMOSTRADA" ? TupleState.ProvenImpossible : TupleState.NotObserved; break;
            default: state = TupleState.Error; break;
        }
        // Una tupla que el banco dio por resuelta y que la interfaz real (5c) demostro que NO se genera pasa a imposible demostrada; nunca al reves.
        return provenImpossibleLater && state == TupleState.Observed ? TupleState.ProvenImpossible : state;
    }

    /// <summary>True si la tupla cuenta como COMPROBADA: hay una medida con veredicto (generada, imposible demostrada o bloqueada por una regla) y no es un faltante ni un fallo del arnés.</summary>
    public static bool IsChecked(TupleState s) => s is TupleState.Observed or TupleState.ProvenImpossible or TupleState.Blocked or TupleState.NotApplicable;

    public static string Label(TupleState s) => s switch
    {
        TupleState.Observed => "OBSERVED",
        TupleState.ProvenImpossible => "Imposible demostrada",
        TupleState.Blocked => "BLOCKED",
        TupleState.NotApplicable => "NOT_APPLICABLE",
        TupleState.Inconclusive => "INCONCLUSIVE",
        TupleState.Error => "ERROR",
        _ => "NOT_OBSERVED",
    };

    // ------------------------------------------------------------------------------------------------------------------ lectura
    /// <summary>
    /// Lee los TSV del banco del Hito 3b (<c>hito3b-&lt;clase&gt;.tsv</c>) y, encima, las REMEDICIONES posteriores de tuplas concretas (<c>hito3b-&lt;clase&gt;-remedicion*.tsv</c>: misma sonda, mismo formato; la
    /// ultima medida de una clave manda): clave de caso -> medida. Una clase sin fichero no aporta medidas (sus tuplas quedan NOT_OBSERVED).
    /// </summary>
    public static Dictionary<string, Measurement> ReadMeasurements(string dataDir, List<string>? usedFiles = null)
    {
        var map = new Dictionary<string, Measurement>(StringComparer.Ordinal);
        foreach (var cls in MeasuredClasses)
        {
            var files = new List<string>();
            var baseFile = Path.Combine(dataDir, $"hito3b-{cls}.tsv");
            if (File.Exists(baseFile)) files.Add(baseFile);
            if (Directory.Exists(dataDir)) files.AddRange(Directory.EnumerateFiles(dataDir, $"hito3b-{cls}-remedicion*.tsv").OrderBy(f => f, StringComparer.Ordinal));
            foreach (var path in files)
            {
                usedFiles?.Add(path);
                string[]? header = null;
                foreach (var raw in File.ReadLines(path, Encoding.UTF8))
                {
                    var line = raw.TrimStart('﻿');
                    if (line.Length == 0) continue;
                    var f = line.Split('	');
                    if (header is null) { header = f; continue; }
                    int Col(string name) => Array.IndexOf(header, name);
                    int iKey = Col("caseKey"), iState = Col("estado"), iCov = Col("cobertura");
                    if (iKey < 0 || iState < 0 || iCov < 0 || f.Length <= Math.Max(iKey, Math.Max(iState, iCov))) continue;
                    map[f[iKey]] = new Measurement(f[iState], f[iCov]);
                }
            }
        }
        return map;
    }

    /// <summary>Claves que la interfaz real demostro imposibles (Hito 5c, <c>libreria-no-generables-5c.tsv</c>, tipo <c>bloqueo</c>).</summary>
    public static HashSet<string> ReadLaterBlocks(string dataDir, List<string>? usedFiles = null)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        var path = Path.Combine(dataDir, "libreria-no-generables-5c.tsv");
        if (!File.Exists(path)) return set;
        usedFiles?.Add(path);
        foreach (var line in File.ReadLines(path, Encoding.UTF8).Skip(1))
        {
            var f = line.Split('\t');
            if (f.Length >= 3 && f[0].Length > 0 && (f.Length < 4 || f[3] != "home")) set.Add(f[0]);
        }
        return set;
    }

    private sealed record SaveOp(string SaveId, string Context, string Mode, string Op, string Status, int Rank, string File);

    /// <summary>
    /// Pasos de la matriz por guardado REAL (Hito 5, 5b y 5c): un estado por (guardado, modo, paso), el de la medida mas reciente (5c > 5b > 5). Solo cuentan los guardados de la coleccion publica de pruebas
    /// (los del usuario, marcados <c>User</c>, se ignoran); del guardado solo se usa su generacion, nunca su nombre, ruta ni hash.
    /// </summary>
    public static (int Saves, Dictionary<string, (int Saves, Dictionary<string, int> Steps)> ByContext) ReadSaves(string dataDir, List<string>? usedFiles = null)
    {
        var latest = new Dictionary<(string, string, string), SaveOp>();
        var ranks = new[] { ("hito5", 0), ("hito5b", 1), ("hito5c", 2) };
        foreach (var (folder, rank) in ranks)
        {
            var dir = Path.Combine(dataDir, folder);
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
            {
                JsonDocument doc;
                try { doc = JsonDocument.Parse(File.ReadAllText(file, Encoding.UTF8)); }
                catch (JsonException) { continue; }
                using (doc)
                {
                    var root = doc.RootElement;
                    if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("Ops", out var ops) || !root.TryGetProperty("Id", out var idEl)) continue;
                    if (root.TryGetProperty("User", out var user) && user.ValueKind == JsonValueKind.True) continue;
                    var meta = root.TryGetProperty("Meta", out var m) && m.ValueKind == JsonValueKind.Object ? m : default;
                    string Meta(string k) => meta.ValueKind == JsonValueKind.Object && meta.TryGetProperty(k, out var v) ? v.GetString() ?? string.Empty : string.Empty;
                    var id = idEl.GetString() ?? string.Empty;
                    var context = Meta("contexto");
                    var mode = Meta("modoUi");
                    usedFiles?.Add(file);
                    foreach (var op in ops.EnumerateArray())
                    {
                        var name = op.TryGetProperty("Op", out var n) ? n.GetString() ?? string.Empty : string.Empty;
                        var status = op.TryGetProperty("Status", out var s) ? s.GetString() ?? string.Empty : string.Empty;
                        latest[(id, mode, name)] = new SaveOp(id, context.Length > 0 ? context : "?", mode, name, status, rank, file);
                    }
                }
            }
        }
        // Las medidas de 5b/5c no repiten el contexto: se toma el del primer fichero que lo trae para ese guardado.
        var contextById = latest.Values.Where(o => o.Context != "?").GroupBy(o => o.SaveId, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First().Context, StringComparer.Ordinal);
        var byContext = new Dictionary<string, (int, Dictionary<string, int>)>(StringComparer.Ordinal);
        foreach (var grp in latest.Values.GroupBy(o => contextById.GetValueOrDefault(o.SaveId) ?? o.Context, StringComparer.Ordinal))
        {
            var steps = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var o in grp) steps[o.Status] = steps.GetValueOrDefault(o.Status) + 1;
            byContext[grp.Key] = (grp.Select(o => o.SaveId).Distinct(StringComparer.Ordinal).Count(), steps);
        }
        return (latest.Values.Select(o => o.SaveId).Distinct(StringComparer.Ordinal).Count(), byContext);
    }

    // ------------------------------------------------------------------------------------------------------------------ construccion del informe
    public static ReportDocument Build(string dbPath, string dataDir, ReportMeta meta)
    {
        var used = new List<string>();
        var measured = ReadMeasurements(dataDir, used);
        var later = ReadLaterBlocks(dataDir, used);
        var (saveCount, saveContexts) = ReadSaves(dataDir, used);

        using var c = CatalogDatabase.Open(dbPath, readOnly: true);
        var core = ReadRow(c, "SELECT core_version, core_commit FROM release ORDER BY release_id LIMIT 1");
        var snapshot = CatalogDatabase.Scalar(c, "SELECT snapshot_id FROM dataset_snapshot WHERE state = 'Verified' ORDER BY created_utc DESC LIMIT 1");

        var games = new List<(int Id, string Name, string Display, int Generation, string Context, string Basis)>();
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "SELECT game_id, version_name, display_name_es, COALESCE(generation, 0), context, availability_basis FROM game WHERE is_group = 0 ORDER BY COALESCE(generation, 0), game_id";
            using var r = cmd.ExecuteReader();
            while (r.Read()) games.Add((r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetInt32(3), r.GetString(4), r.GetString(5)));
        }
        var gameById = games.ToDictionary(g => g.Id);

        var tuples = new List<CoverageTuple>();
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "SELECT case_key, game_id, encounter_kind, expected FROM coverage_case";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var g = gameById.GetValueOrDefault(r.GetInt32(1));
                tuples.Add(new CoverageTuple(r.GetString(0), g.Name ?? r.GetInt32(1).ToString(CultureInfo.InvariantCulture), g.Generation, g.Context ?? string.Empty, r.GetString(2), r.GetString(3)));
            }
        }

        // Estado de cada tupla.
        var states = new TupleState[tuples.Count];
        for (var i = 0; i < tuples.Count; i++)
            states[i] = Classify(measured.GetValueOrDefault(tuples[i].CaseKey), later.Contains(tuples[i].CaseKey));
        var positives = Enumerable.Range(0, tuples.Count).Where(i => tuples[i].Expected != "Rejected").ToList();
        var negatives = Enumerable.Range(0, tuples.Count).Where(i => tuples[i].Expected == "Rejected").ToList();

        var doc = new ReportDocument($"Informe de cobertura de Pokekeep {meta.Version}", [], []);
        var total = positives.Count;
        var checkedCount = positives.Count(i => IsChecked(states[i]));
        var missing = positives.Count(i => !IsChecked(states[i]));
        doc.Intro.Add($"Versión {meta.Version}{(meta.Commit.Length > 0 ? $" (fuente {meta.Commit})" : string.Empty)}; generado el {meta.DateText}. Core oficial {core[0]} (commit {Short(core[1])}); instantánea del catálogo {snapshot}.");
        doc.Intro.Add("**Esto NO es una declaración de «cobertura completa».** Es la matriz de tuplas con sus estados (regla 10 del proyecto). NOT_OBSERVED significa «sin medida», nunca «imposible»; " +
                      "«Imposible demostrada» solo se usa cuando el banco agotó la búsqueda y el Core dio el motivo exacto. Un tiempo agotado es INCONCLUSIVE, nunca imposible (regla 5).");
        doc.Intro.Add($"Alcance: {Num(total)} tuplas posibles (juego, especie, forma, clase de encuentro y variante) en {games.Count(g => tuples.Any(t => t.Game == g.Name))} juegos y {Num(negatives.Count)} casos negativos. " +
                      $"Con veredicto archivado (generadas, imposibles demostradas, bloqueadas por una regla o no aplicables): {Num(checkedCount)} ({Pct(checkedCount, total)}); faltantes (sin medida o sin veredicto): {Num(missing)} ({Pct(missing, total)}).");
        doc.Intro.Add("El veredicto de cada tupla es el de su última medida ARCHIVADA en el repositorio (banco del Hito 3b, remediciones y lo que la interfaz real demostró en el Hito 5c); este informe no repite las medidas con el motor de esta versión.");

        // 1. Leyenda y resumen.
        var s1 = new ReportSection("1. Estados y resumen", [], []);
        s1.Paragraphs.Add("Cada tupla tiene exactamente un estado: **OBSERVED** (el motor la generó y el Core la valida), **Imposible demostrada** (búsqueda completa y motivo exacto del Core; NOT_OBSERVED con prueba), " +
                          "**BLOCKED** (una regla del proyecto lo impide, p. ej. la regla 6: no se crea una matrícula de HOME), **NOT_APPLICABLE** (la medida no tiene sentido), **INCONCLUSIVE** (se midió y no se pudo decidir), " +
                          "**ERROR** (falló el arnés) y **NOT_OBSERVED** (sin ninguna medida: faltante).");
        var order = new[] { TupleState.Observed, TupleState.ProvenImpossible, TupleState.Blocked, TupleState.NotApplicable, TupleState.Inconclusive, TupleState.Error, TupleState.NotObserved };
        s1.Tables.Add(new ReportTable("Tuplas posibles por estado", ["Estado", "Tuplas", "%"],
            [.. order.Select(st => new[] { Label(st), Num(positives.Count(i => states[i] == st)), Pct(positives.Count(i => states[i] == st), total) }), ["Total", Num(total), "100 %"]]));
        s1.Tables.Add(new ReportTable("Casos negativos (lo que el motor debe rechazar o lo que no existe en ese juego)", ["Estado", "Casos", "%"],
            [.. order.Where(st => negatives.Any(i => states[i] == st)).Select(st => new[] { Label(st), Num(negatives.Count(i => states[i] == st)), Pct(negatives.Count(i => states[i] == st), negatives.Count) }), ["Total", Num(negatives.Count), "100 %"]],
            "Un caso negativo OBSERVED es un rechazo comprobado (el motor no inventa lo que no existe); NOT_APPLICABLE es una especie que ese juego no tiene."));
        doc.Sections.Add(s1);

        // 2. Matriz por generacion y por juego.
        var s2 = new ReportSection("2. Matriz por generación y por juego", [], []);
        s2.Paragraphs.Add("Posibles = tuplas elegibles de ese juego. Generadas = OBSERVED. Comprobadas = con veredicto (generadas + imposibles demostradas + BLOCKED + NOT_APPLICABLE). Faltantes = posibles - comprobadas (INCONCLUSIVE, ERROR y NOT_OBSERVED).");
        string[] headers = ["Juego", "Posibles", "Generadas (OBSERVED)", "Imposibles demostradas", "BLOCKED", "NOT_APPLICABLE", "INCONCLUSIVE", "ERROR", "NOT_OBSERVED", "Comprobadas", "Faltantes"];
        string[] Row(string label, IEnumerable<int> idx)
        {
            var l = idx.ToList();
            int N(TupleState st) => l.Count(i => states[i] == st);
            var chk = l.Count(i => IsChecked(states[i]));
            return [label, Num(l.Count), Num(N(TupleState.Observed)), Num(N(TupleState.ProvenImpossible)), Num(N(TupleState.Blocked)), Num(N(TupleState.NotApplicable)), Num(N(TupleState.Inconclusive)), Num(N(TupleState.Error)), Num(N(TupleState.NotObserved)), $"{Num(chk)} ({Pct(chk, l.Count)})", Num(l.Count - chk)];
        }
        var byGen = positives.GroupBy(i => tuples[i].Generation).OrderBy(g => g.Key).ToList();
        s2.Tables.Add(new ReportTable("Por generación (suma de los juegos de la generación)", headers, [.. byGen.Select(g => Row(g.Key == 0 ? "sin generación" : $"Generación {g.Key}", g)), Row("Total", positives)]));
        foreach (var g in byGen)
        {
            var rows = new List<string[]>();
            foreach (var game in games.Where(x => x.Generation == g.Key).OrderBy(x => x.Id))
            {
                var idx = g.Where(i => tuples[i].Game == game.Name).ToList();
                if (idx.Count == 0)
                {
                    rows.Add([$"{game.Display} ({game.Name})", "0", "-", "-", "-", "-", "-", "-", "-", "-", "-"]);
                    continue;
                }
                rows.Add(Row($"{game.Display} ({game.Name})", idx));
            }
            var note = games.Where(x => x.Generation == g.Key && g.All(i => tuples[i].Game != x.Name) && x.Basis is "Unknown" or "NotApplicable").Select(x => x.Name).ToList();
            s2.Tables.Add(new ReportTable($"Generación {(g.Key == 0 ? "?" : g.Key.ToString(CultureInfo.InvariantCulture))}", headers, rows,
                note.Count > 0 ? $"Sin tuplas (NOT_APPLICABLE: el Core fijado no tiene datos de presencia para {string.Join(", ", note)}; no se mide)." : null));
        }
        doc.Sections.Add(s2);

        // 3. Por clase de encuentro.
        var s3 = new ReportSection("3. Por clase de encuentro", [], []);
        var kindRows = new List<string[]>();
        foreach (var k in positives.GroupBy(i => tuples[i].Kind).OrderBy(g => g.Key, StringComparer.Ordinal))
            kindRows.Add(Row(k.Key, k));
        kindRows.Add(Row("Total", positives));
        s3.Tables.Add(new ReportTable("Tuplas posibles por clase", ["Clase", .. headers.Skip(1)], kindRows,
            "GO: casi todo BLOCKED por la regla 6 con los ajustes por defecto (necesita pasar por Pokémon HOME, que asigna su matrícula); con el ajuste «sin matrícula» se ofrece lo que el motor genera (ver sección 4). " +
            "Transfer: veredicto «Unknown» de origen (la ruta depende de la transferencia, no de un encuentro)."));
        doc.Sections.Add(s3);

        // 4. Libreria.
        var s4 = new ReportSection("4. Lo que la Librería ofrece y lo que atenúa", [], []);
        var libRows = new List<string[]>();
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = """
                SELECT g.display_name_es, g.version_name, COALESCE(g.generation, 0), count(*), SUM(l.addable), SUM(l.home_required),
                       SUM(CASE WHEN l.addable = 0 AND l.dim_source = 'catalog' THEN 1 ELSE 0 END), SUM(CASE WHEN l.addable = 0 AND l.dim_source = 'measured' THEN 1 ELSE 0 END),
                       SUM(CASE WHEN l.addable = 0 AND l.dim_source IS NULL THEN 1 ELSE 0 END)
                  FROM library_entry l JOIN game g ON g.game_id = l.game_id
                 GROUP BY g.game_id ORDER BY COALESCE(g.generation, 0), g.game_id
                """;
            using var r = cmd.ExecuteReader();
            long tot = 0, add = 0, home = 0, cat = 0, mea = 0, nin = 0;
            while (r.Read())
            {
                long n = r.GetInt64(3), a = r.GetInt64(4), h = r.GetInt64(5), ca = r.GetInt64(6), me = r.GetInt64(7), ni = r.GetInt64(8);
                libRows.Add([$"{r.GetString(0)} ({r.GetString(1)})", Num(n), Num(a), Num(h), Num(ca), Num(me), Num(ni)]);
                tot += n; add += a; home += h; cat += ca; mea += me; nin += ni;
            }
            libRows.Add(["Total", Num(tot), Num(add), Num(home), Num(cat), Num(mea), Num(nin)]);
        }
        s4.Tables.Add(new ReportTable("Entradas de la Librería por juego", ["Juego", "Entradas", "Ofrecibles", "Solo con el ajuste de HOME", "Atenuadas por regla del catálogo", "Atenuadas por medición del banco", "No está en el juego"], libRows,
            "«Ofrecibles» incluye las que solo se ofrecen con el ajuste de HOME (columna siguiente). Atenuada por medición = el banco demostró que no se genera (con el motivo exacto del Core)."));
        doc.Sections.Add(s4);

        // 5. Guardados reales.
        var s5 = new ReportSection("5. Reimportación sobre guardados reales (Hito 5, 5b y 5c)", [], []);
        s5.Paragraphs.Add($"Pasos de la matriz por guardado (abrir, legalizar, editar, exportar y reabrir), un estado por paso y guardado: el de la medida más reciente. {Num(saveCount)} guardados de la colección pública de pruebas (copias; " +
                          "los guardados personales no entran en este informe ni en ningún dato publicado).");
        var saveRows = new List<string[]>();
        var stateNames = new[] { "OBSERVED", "BLOCKED", "NOT_APPLICABLE", "INCONCLUSIVE", "NOT_OBSERVED", "ERROR" };
        foreach (var (ctx, v) in saveContexts.OrderBy(k => k.Key, StringComparer.Ordinal))
            saveRows.Add([ctx, Num(v.Saves), .. stateNames.Select(sn => Num(v.Steps.GetValueOrDefault(sn))), Num(v.Steps.Values.Sum())]);
        if (saveContexts.Count > 0)
            saveRows.Add(["Total", Num(saveCount), .. stateNames.Select(sn => Num(saveContexts.Values.Sum(v => v.Steps.GetValueOrDefault(sn)))), Num(saveContexts.Values.Sum(v => v.Steps.Values.Sum()))]);
        s5.Tables.Add(new ReportTable("Pasos por contexto de guardado", ["Contexto", "Guardados", .. stateNames, "Pasos"], saveRows));
        doc.Sections.Add(s5);

        // 6. Procedencia.
        var s6 = new ReportSection("6. Procedencia de los datos y límites", [], []);
        s6.Paragraphs.Add("Todo lo de arriba sale de mediciones YA hechas y archivadas en el repositorio (este informe no mide nada). Las tuplas posibles y la Librería salen de la base del catálogo construida con el Core oficial indicado; " +
                          "los veredictos, del banco del Hito 3b (`hito3b-<clase>.tsv`), corregidos con lo que la interfaz real demostró después (Hito 5c) y los guardados del Hito 5. Si el motor o el Core cambian, las medidas anteriores " +
                          "pueden dejar de valer: volver a medir antes de afirmar nada sobre una versión nueva.");
        s6.Paragraphs.Add("No cubre (NOT_OBSERVED a propósito en este informe): Windows 10, máquinas sin .NET y usuarios sin privilegios; el efecto sobre guardados personales; ni una repetición de todo el banco con el motor de esta versión.");
        var provRows = new List<string[]>();
        foreach (var f in used.Distinct(StringComparer.Ordinal).Where(f => f.EndsWith(".tsv", StringComparison.OrdinalIgnoreCase)).OrderBy(f => f, StringComparer.Ordinal))
            provRows.Add([Path.GetFileName(f), Num(new FileInfo(f).Length) + " bytes", Sha(f)]);
        provRows.Add([$"{used.Count(f => f.EndsWith(".json", StringComparison.OrdinalIgnoreCase))} ficheros JSON de la matriz por guardado (Hito 5, 5b, 5c)", "-", "-"]);
        s6.Tables.Add(new ReportTable("Ficheros de medida usados", ["Fichero", "Tamaño", "SHA-256 (12 primeros)"], provRows));
        doc.Sections.Add(s6);
        return doc;
    }

    private static string[] ReadRow(Microsoft.Data.Sqlite.SqliteConnection c, string sql)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return ["?", "?"];
        return [.. Enumerable.Range(0, r.FieldCount).Select(i => Convert.ToString(r.GetValue(i), CultureInfo.InvariantCulture) ?? "?")];
    }

    private static string Short(string s) => s.Length > 12 ? s[..12] : s;
    private static string Sha(string path) { using var fs = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(fs))[..12]; }
    private static string Num(long n) => n.ToString("N0", CultureInfo.InvariantCulture).Replace(',', '.');
    private static string Pct(long part, long whole) => whole == 0 ? "-" : (part * 100.0 / whole).ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',') + " %";

    // ------------------------------------------------------------------------------------------------------------------ pintado
    public static string ToMarkdown(ReportDocument d)
    {
        var sb = new StringBuilder();
        sb.Append("# ").AppendLine(d.Title).AppendLine();
        foreach (var p in d.Intro) sb.AppendLine(p).AppendLine();
        foreach (var s in d.Sections)
        {
            sb.Append("## ").AppendLine(s.Heading).AppendLine();
            foreach (var p in s.Paragraphs) sb.AppendLine(p).AppendLine();
            foreach (var t in s.Tables)
            {
                sb.Append("### ").AppendLine(t.Title).AppendLine();
                sb.Append("| ").Append(string.Join(" | ", t.Headers.Select(Esc))).AppendLine(" |");
                sb.Append('|').Append(string.Concat(t.Headers.Select((_, i) => i == 0 ? " --- |" : " ---: |"))).AppendLine();
                foreach (var r in t.Rows) sb.Append("| ").Append(string.Join(" | ", r.Select(Esc))).AppendLine(" |");
                sb.AppendLine();
                if (t.Note is not null) sb.AppendLine("_" + t.Note + "_").AppendLine();
            }
        }
        return sb.ToString();
    }

    private static string Esc(string s) => s.Replace("|", "\\|");

    public static string ToHtml(ReportDocument d)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!doctype html>").AppendLine("<html lang=\"es\">").AppendLine("<head>").AppendLine("<meta charset=\"utf-8\">").AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        sb.Append("<title>").Append(WebUtility.HtmlEncode(d.Title)).AppendLine("</title>");
        sb.AppendLine("""
            <style>
            :root { --fondo: #ffffff; --texto: #1d2330; --suave: #5b6578; --linea: #d7dce5; --cab: #eef1f6; --acento: #1f5fbf; }
            @media (prefers-color-scheme: dark) { :root { --fondo: #14171d; --texto: #e6e9ef; --suave: #9aa4b5; --linea: #2c333f; --cab: #1d222b; --acento: #7aa7ee; } }
            body { background: var(--fondo); color: var(--texto); font: 15px/1.5 system-ui, "Segoe UI", sans-serif; margin: 0 auto; padding: 24px 16px 48px; max-width: 1200px; }
            h1 { font-size: 1.6rem; margin: 0 0 12px; } h2 { font-size: 1.25rem; margin: 32px 0 8px; border-bottom: 1px solid var(--linea); padding-bottom: 4px; } h3 { font-size: 1rem; margin: 20px 0 6px; color: var(--acento); }
            p { margin: 8px 0; } .nota { color: var(--suave); font-size: 0.9rem; }
            .tabla { overflow-x: auto; } table { border-collapse: collapse; width: 100%; font-size: 0.88rem; font-variant-numeric: tabular-nums; }
            th, td { border: 1px solid var(--linea); padding: 4px 8px; text-align: right; white-space: nowrap; } th:first-child, td:first-child { text-align: left; min-width: 13rem; white-space: normal; }
            thead th { background: var(--cab); } tbody tr:last-child td { font-weight: 600; }
            </style>
            """);
        sb.AppendLine("</head>").AppendLine("<body>");
        sb.Append("<h1>").Append(WebUtility.HtmlEncode(d.Title)).AppendLine("</h1>");
        foreach (var p in d.Intro) sb.Append("<p>").Append(Inline(p)).AppendLine("</p>");
        foreach (var s in d.Sections)
        {
            sb.Append("<h2>").Append(WebUtility.HtmlEncode(s.Heading)).AppendLine("</h2>");
            foreach (var p in s.Paragraphs) sb.Append("<p>").Append(Inline(p)).AppendLine("</p>");
            foreach (var t in s.Tables)
            {
                sb.Append("<h3>").Append(WebUtility.HtmlEncode(t.Title)).AppendLine("</h3>").AppendLine("<div class=\"tabla\"><table>");
                sb.Append("<thead><tr>").Append(string.Concat(t.Headers.Select(h => "<th>" + WebUtility.HtmlEncode(h) + "</th>"))).AppendLine("</tr></thead><tbody>");
                foreach (var r in t.Rows) sb.Append("<tr>").Append(string.Concat(r.Select(x => "<td>" + WebUtility.HtmlEncode(x) + "</td>"))).AppendLine("</tr>");
                sb.AppendLine("</tbody></table></div>");
                if (t.Note is not null) sb.Append("<p class=\"nota\">").Append(WebUtility.HtmlEncode(t.Note)).AppendLine("</p>");
            }
        }
        sb.AppendLine("</body>").AppendLine("</html>");
        return sb.ToString();
    }

    /// <summary>Negrita (<c>**x**</c>) y codigo (<c>`x`</c>) de los parrafos, codificado para HTML.</summary>
    private static string Inline(string text)
    {
        var enc = WebUtility.HtmlEncode(text);
        var sb = new StringBuilder();
        var bold = false;
        for (var i = 0; i < enc.Length; i++)
        {
            if (i + 1 < enc.Length && enc[i] == '*' && enc[i + 1] == '*') { sb.Append(bold ? "</strong>" : "<strong>"); bold = !bold; i++; }
            else sb.Append(enc[i]);
        }
        var s = sb.ToString();
        var code = false;
        sb.Clear();
        foreach (var ch in s)
        {
            if (ch == '`') { sb.Append(code ? "</code>" : "<code>"); code = !code; }
            else sb.Append(ch);
        }
        return sb.ToString();
    }
}
