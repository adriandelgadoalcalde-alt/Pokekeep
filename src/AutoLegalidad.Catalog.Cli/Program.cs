// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Catalog.Build;
using AutoLegalidad.Catalog.Db;
using AutoLegalidad.Catalog.Sources;
using AutoLegalidad.Contracts;

namespace AutoLegalidad.Catalog.Cli;

internal static class Program
{
    private const string Uso = """
        pokekeep-catalogo - construye y mantiene la base de datos del catalogo de Pokekeep (Hito 1)

        Comandos:
          construir  [--repo <dir>] [--raiz <dir>] [--db <fichero>] [--informe <fichero.json>] [--sin-eventos] [--sin-go] [--core-sha <hex>] [--engine-sha <hex>] [--adapter-sha <hex>] [--alm-sha <hex>]
                     Crea la BD desde cero: migraciones, Core oficial, EventsGallery (allowlist), ventanas GO, snapshot verificado y matriz de cobertura.
                     --core-sha liga la BD a ESE SHA-256 del PKHeX.Core.dll (64 hex; por defecto, el del fichero junto al ejecutable): asi se construye en el PC la BD de otra
                     plataforma (Android). --engine-sha/--adapter-sha/--alm-sha se aceptan por coherencia con construir-soluciones.ps1, pero la BD solo se liga al Core:
                     los tres ligan la cache de soluciones.
          migrar     --db <fichero> [--a <version>]     Sube (o baja con --a N) las migraciones. --a 0 deja el esquema vacio.
          verificar  --db <fichero>                      Verifica integridad de migraciones, snapshot activo y claves foraneas.
          compactar-movil --db <fichero> [--nivel 1|2]   X42 (ADR 0098): deja la BD ya construida en su forma COMPACTA para el movil (mismas filas y columnas, otro almacenamiento; solo lectura). Nivel 2 por defecto.
          inventario --corpus <dir> --salida <fichero>   Inventario SOLO con hashes de un corpus local (BD aparte; nunca va al catalogo).
          pragmas    --db <fichero>                      Muestra los PRAGMA reales de la conexion.
          consulta   --db <fichero> --sql "<SELECT ...>" Ejecuta una consulta de SOLO LECTURA y la imprime separada por tabuladores.
          libreria-bench --db <fichero> [--repeticiones N]  Mide las consultas de la Libreria (mediana y p95 en milisegundos).
          libreria-reconstruir --db <fichero> [--bloqueos <fichero.tsv>]  Reconstruye SOLO library_entry sobre la BD existente (migra al ultimo esquema antes), con las entradas imposibles del banco.
          diferencias --antes <manifiesto.tsv|bd.sqlite|commit> [--despues <idem; por defecto el manifiesto del arbol de trabajo>] [--repo <dir>] [--git <git.exe>] [--salida <fichero.tsv>] [--max N] [--fallar-si-bajas]
                     Aviso de datos nuevos: compara dos versiones de la base de eventos (manifiesto TSV de EventsGallery, o los eventos de dos BD construidas = MGDB del Core + EventsGallery)
                     y lista altas, bajas y cambios de clasificacion antes de actualizar la pareja de versiones. Un commit o etiqueta lee docs/datos/eventsgallery-manifest.tsv de ese commit. Solo lee.
          informe-cobertura --db <fichero> --salida <carpeta> [--version <v>] [--commit <hash>] [--datos <docs/datos>] [--formato md|html|ambos]
                     Informe de cobertura publicable (matriz de tuplas por juego y generacion con OBSERVED/NOT_OBSERVED/INCONCLUSIVE/NOT_APPLICABLE/BLOCKED/ERROR, casos negativos, Libreria y guardados reales)
                     a partir de la BD construida y de las mediciones ya archivadas en docs/datos. No mide nada ni lleva datos personales. Escribe INFORME-COBERTURA-<version>.md/.html.

        --repo : carpeta del repositorio Pokekeep (por defecto, la que contiene fuentes.lock.json subiendo desde el directorio actual).
        --raiz : carpeta con fuentes\ (por defecto, la propia --repo; ADR 0039).
        """;

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0 || args[0] is "-h" or "--help" or "ayuda")
            {
                Console.WriteLine(Uso);
                return args.Length == 0 ? 2 : 0;
            }
            var opts = Options.Parse(args.Skip(1).ToArray());
            return args[0] switch
            {
                "construir" => Construir(opts),
                "migrar" => Migrar(opts),
                "verificar" => Verificar(opts),
                "pragmas" => Pragmas(opts),
                "compactar-movil" => CompactarMovil(opts),
                "inventario" => Inventario(opts),
                "consulta" => Consulta(opts),
                "libreria-bench" => LibreriaBench(opts),
                "libreria-reconstruir" => LibreriaReconstruir(opts),
                "diferencias" => Diferencias(opts),
                "informe-cobertura" => InformeCobertura(opts),
                _ => Fallo($"Comando desconocido: {args[0]}\n\n{Uso}"),
            };
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Console.Error.WriteLine("ERROR: " + ex.Message);
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static int Fallo(string msg) { Console.Error.WriteLine(msg); return 2; }

    private static int Construir(Options o)
    {
        var repo = o.Repo ?? FindRepo();
        var raiz = o.Raiz ?? repo;
        var db = o.Db ?? Path.Combine(repo, "datos", "autolegalidad.sqlite");
        var plan = new BuildPlan(repo, raiz, db) { IncludeEvents = !o.Flag("sin-eventos"), IncludeGo = !o.Flag("sin-go"), ReportPath = o.Get("informe") ?? Path.Combine(repo, "docs", "datos", "hito1-cifras.json"), GalleryManifestPath = o.Get("manifiesto") ?? Path.Combine(repo, "docs", "datos", "eventsgallery-manifest.tsv") };
        plan = plan with { Identity = BinaryIdentity.WithOverrides(BinaryIdentity.Current, o.Get("core-sha"), o.Get("engine-sha"), o.Get("adapter-sha"), o.Get("alm-sha")) };
        var report = CatalogBuilder.Run(plan, Console.WriteLine);
        Console.WriteLine(report.ToText());
        return report.Gate.Passed ? 0 : 1;
    }

    private static int Migrar(Options o)
    {
        var db = o.Db ?? throw new ArgumentException("Falta --db");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(db))!);
        using var c = CatalogDatabase.Open(db);
        var m = new Migrator(c);
        var target = o.Get("a") is { } a ? int.Parse(a) : MigrationCatalog.Latest;
        Console.WriteLine($"Version actual: {m.CurrentVersion()}; objetivo: {target}");
        Console.WriteLine($"Version resultante: {m.MigrateTo(target)}");
        return 0;
    }

    private static int Verificar(Options o)
    {
        var db = o.Db ?? throw new ArgumentException("Falta --db");
        using var c = CatalogDatabase.Open(db, readOnly: true);
        var r = CatalogVerifier.Verify(c);
        Console.WriteLine(r.ToText());
        return r.Ok ? 0 : 1;
    }

    private static int CompactarMovil(Options o)
    {
        var db = o.Db ?? throw new ArgumentException("Falta --db");
        var nivel = o.Get("nivel") is { } n ? int.Parse(n, System.Globalization.CultureInfo.InvariantCulture) : 2;
        var r = CatalogCompactor.Compact(db, nivel, Console.WriteLine);
        Console.WriteLine($"Compactado (nivel {r.Level}): {r.BytesBefore:N0} B -> {r.BytesAfter:N0} B");
        return 0;
    }

    private static int Pragmas(Options o)
    {
        var db = o.Db ?? throw new ArgumentException("Falta --db");
        using var c = CatalogDatabase.Open(db);
        foreach (var (k, v) in CatalogDatabase.ReadPragmas(c)) Console.WriteLine($"{k} = {v}");
        return 0;
    }

    private static int Consulta(Options o)
    {
        var db = o.Db ?? throw new ArgumentException("Falta --db");
        var sql = o.Get("sql") ?? throw new ArgumentException("Falta --sql");
        using var c = CatalogDatabase.Open(db, readOnly: true);
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        using var r = cmd.ExecuteReader();
        Console.WriteLine(string.Join('\t', Enumerable.Range(0, r.FieldCount).Select(r.GetName)));
        while (r.Read())
            Console.WriteLine(string.Join('\t', Enumerable.Range(0, r.FieldCount).Select(i => r.IsDBNull(i) ? "NULL" : Convert.ToString(r.GetValue(i), System.Globalization.CultureInfo.InvariantCulture))));
        return 0;
    }

    /// <summary>Hito 3c: la Libreria es un indice DERIVADO; se puede reconstruir sin repetir la construccion completa del catalogo (migra al ultimo esquema antes).</summary>
    private static int LibreriaReconstruir(Options o)
    {
        var db = o.Db ?? throw new ArgumentException("Falta --db");
        var repo = o.Repo ?? FindRepo();
        var path = o.Get("bloqueos") ?? Path.Combine(repo, "docs", "datos", Library.LibraryBlocks.FileName);
        using var c = CatalogDatabase.Open(db);
        var schema = new Migrator(c).MigrateToLatest();
        var releaseId = CatalogDatabase.Scalar(c, "SELECT release_id FROM release ORDER BY release_id LIMIT 1");
        CatalogDatabase.Exec(c, "INSERT INTO library_search(library_search) VALUES('delete-all')");
        CatalogDatabase.Exec(c, "DELETE FROM library_entry");
        var lib = Library.LibraryBuilder.Build(c, releaseId, Library.LibraryBlocks.Load(path));
        CatalogDatabase.Exec(c, "INSERT INTO library_search(library_search) VALUES('integrity-check')");
        Console.WriteLine($"Esquema {schema}. Libreria: {lib.Entries} entradas ({lib.Addable} ofrecibles, {lib.Dimmed} atenuadas; {lib.HomeRequired} solo con el ajuste de HOME; {lib.BlockedByRule} atenuadas por regla del catalogo y {lib.BlockedByMeasurement} por medicion del banco).");
        CatalogDatabase.CheckpointAndClose(c);
        return 0;
    }

    /// <summary>ADR 0050: aviso de eventos nuevos (propuesta del Hito 1). Codigo 0 = comparado (haya o no diferencias); 3 = hay bajas de eventos aprobados y se pidio --fallar-si-bajas; 1 = error.</summary>
    private static int Diferencias(Options o)
    {
        var antes = o.Get("antes") ?? throw new ArgumentException("Falta --antes (manifiesto .tsv, base .sqlite o commit/etiqueta de git)");
        var repo = o.Repo ?? FindRepo();
        var despues = o.Get("despues");
        EventDiffReport report;
        var antesEsBd = antes.EndsWith(".sqlite", StringComparison.OrdinalIgnoreCase);
        if (antesEsBd || (despues?.EndsWith(".sqlite", StringComparison.OrdinalIgnoreCase) ?? false))
        {
            if (despues is null || !(antesEsBd && despues.EndsWith(".sqlite", StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("Con bases .sqlite hay que dar --antes y --despues, las dos .sqlite.");
            report = EventDiff.CompareDatabases(antes, despues);
        }
        else
        {
            var textoAntes = ReadManifestSource(antes, repo, o.Get("git"));
            var textoDespues = despues is null ? File.ReadAllText(Path.Combine(repo, EventDiff.ManifestPathInRepo)) : ReadManifestSource(despues, repo, o.Get("git"));
            report = EventDiff.CompareManifests(textoAntes, textoDespues, antes, despues ?? "arbol de trabajo");
        }
        Console.Write(report.ToText(o.Get("max") is { } m ? int.Parse(m, System.Globalization.CultureInfo.InvariantCulture) : 25));
        if (o.Get("salida") is { } salida)
        {
            File.WriteAllText(salida, report.ToTsv(), new System.Text.UTF8Encoding(false));
            Console.WriteLine($"Detalle completo: {salida}");
        }
        return o.Flag("fallar-si-bajas") && report.NoLongerApproved > 0 ? 3 : 0;
    }

    /// <summary>ADR 0050: informe de cobertura publicable (propuesta del Hito 1). Solo lee la BD y los datos ya medidos.</summary>
    private static int InformeCobertura(Options o)
    {
        var db = o.Db ?? throw new ArgumentException("Falta --db");
        var salida = o.Get("salida") ?? throw new ArgumentException("Falta --salida (carpeta)");
        var repo = o.Repo ?? FindRepo();
        var datos = o.Get("datos") ?? Path.Combine(repo, "docs", "datos");
        var version = o.Get("version") ?? "sin-version";
        var formato = (o.Get("formato") ?? "ambos").ToLowerInvariant();
        if (formato is not ("md" or "html" or "ambos")) throw new ArgumentException("--formato debe ser md, html o ambos");
        var meta = new Reports.ReportMeta(version, o.Get("commit") ?? string.Empty, DateTime.UtcNow.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        var doc = Reports.CoverageReport.Build(db, datos, meta);
        Directory.CreateDirectory(salida);
        var baseName = Path.Combine(salida, $"INFORME-COBERTURA-{version}");
        var utf8 = new System.Text.UTF8Encoding(false);
        if (formato is "md" or "ambos") { File.WriteAllText(baseName + ".md", Reports.CoverageReport.ToMarkdown(doc), utf8); Console.WriteLine(baseName + ".md"); }
        if (formato is "html" or "ambos") { File.WriteAllText(baseName + ".html", Reports.CoverageReport.ToHtml(doc), utf8); Console.WriteLine(baseName + ".html"); }
        foreach (var line in doc.Intro) Console.WriteLine(line.Replace("**", string.Empty));
        return 0;
    }

    /// <summary>Un manifiesto desde un fichero, o el de un commit/etiqueta de git (<c>git show &lt;ref&gt;:docs/datos/eventsgallery-manifest.tsv</c>).</summary>
    private static string ReadManifestSource(string source, string repo, string? gitExe)
    {
        if (File.Exists(source)) return File.ReadAllText(source);
        var git = gitExe ?? (File.Exists(@"C:\Program Files\Git\cmd\git.exe") ? @"C:\Program Files\Git\cmd\git.exe" : "git");
        var psi = new System.Diagnostics.ProcessStartInfo(git) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, WorkingDirectory = repo, StandardOutputEncoding = new System.Text.UTF8Encoding(false) };
        psi.ArgumentList.Add("show");
        psi.ArgumentList.Add($"{source}:{EventDiff.ManifestPathInRepo}");
        using var p = System.Diagnostics.Process.Start(psi) ?? throw new InvalidOperationException("No se pudo lanzar git.");
        var stdout = p.StandardOutput.ReadToEndAsync();
        var stderr = p.StandardError.ReadToEndAsync();
        p.WaitForExit();
        if (p.ExitCode != 0) throw new ArgumentException($"No se pudo leer el manifiesto de '{source}' (ni es un fichero ni un commit de {repo}): {stderr.Result.Trim()}");
        return stdout.Result;
    }

    private static int LibreriaBench(Options o)
    {
        var db = o.Db ?? throw new ArgumentException("Falta --db");
        var reps = o.Get("repeticiones") is { } r ? int.Parse(r, System.Globalization.CultureInfo.InvariantCulture) : 200;
        using var c = CatalogDatabase.Open(db, readOnly: true);
        int Game(string v) => (int)long.Parse(CatalogDatabase.Scalar(c, $"SELECT game_id FROM game WHERE version_name='{v}'"), System.Globalization.CultureInfo.InvariantCulture);
        var sl = Game("SL");
        var e = Game("E");
        var cases = new (string Name, Func<object> Run)[]
        {
            ("navegar: Escarlata, clase Salvaje (50)", () => Library.LibraryQueries.Query(c, new Library.LibraryFilter { GameId = sl, Kind = "Wild" })),
            ("navegar: Escarlata, solo ofrecibles, pagina 20 (50)", () => Library.LibraryQueries.Query(c, new Library.LibraryFilter { GameId = sl, OnlyAddable = true, Offset = 1000 })),
            ("navegar: generacion 3 + evento (50)", () => Library.LibraryQueries.Query(c, new Library.LibraryFilter { GameId = e, Generation = 3, Kind = "Event" })),
            ("buscar: 'pikachu' (FTS5)", () => Library.LibraryQueries.Query(c, new Library.LibraryFilter { GameId = sl, Text = "pikachu" })),
            ("buscar: prefijo 'pika' sin tildes", () => Library.LibraryQueries.Query(c, new Library.LibraryFilter { GameId = sl, Text = "pika" })),
            ("buscar: 'Rayquaza' en Esmeralda", () => Library.LibraryQueries.Query(c, new Library.LibraryFilter { GameId = e, Text = "rayquaza" })),
            ("buscar: numero de Pokedex 25", () => Library.LibraryQueries.Query(c, new Library.LibraryFilter { GameId = sl, Text = "25" })),
            ("buscar: titulo de evento 'pikachu' + solo ofrecibles", () => Library.LibraryQueries.Query(c, new Library.LibraryFilter { GameId = sl, Text = "pikachu", Kind = "Event", OnlyAddable = true })),
            ("eventos: idioma Japanese (50)", () => Library.LibraryQueries.Query(c, new Library.LibraryFilter { GameId = Game("X"), Kind = "Event", EventLanguage = "Japanese" })),
            ("eventos: rango de fechas 2013-2014 (50)", () => Library.LibraryQueries.Query(c, new Library.LibraryFilter { GameId = Game("X"), Kind = "Event", EventFrom = "2013-01-01", EventTo = "2014-12-31" })),
            ("contar: Escarlata, ofrecibles", () => Library.LibraryQueries.Count(c, new Library.LibraryFilter { GameId = sl, OnlyAddable = true })),
        };
        Console.WriteLine($"Libreria: {CatalogDatabase.Scalar(c, "SELECT count(*) FROM library_entry")} entradas; {reps} repeticiones por consulta tras calentar.");
        Console.WriteLine("consulta\tfilas\tmediana_ms\tp95_ms\tmax_ms");
        foreach (var (name, run) in cases)
        {
            run();
            var times = new List<double>(reps);
            object last = 0;
            for (var i = 0; i < reps; i++)
            {
                var t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                last = run();
                times.Add(System.Diagnostics.Stopwatch.GetElapsedTime(t0).TotalMilliseconds);
            }
            times.Sort();
            var rows = last is System.Collections.ICollection col ? col.Count : Convert.ToInt32(last, System.Globalization.CultureInfo.InvariantCulture);
            Console.WriteLine($"{name}\t{rows}\t{times[times.Count / 2]:F2}\t{times[(int)(times.Count * 0.95)]:F2}\t{times[^1]:F2}");
        }
        return 0;
    }

    private static int Inventario(Options o)
    {
        var corpus = o.Get("corpus") ?? throw new ArgumentException("Falta --corpus");
        var salida = o.Get("salida") ?? throw new ArgumentException("Falta --salida");
        var excl = (o.Get("excluir") ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var r = Sources.CorpusInventory.Scan(corpus, salida, excl, Console.WriteLine);
        Console.WriteLine(r.ToText());
        return 0;
    }

    private static string FindRepo()
    {
        var d = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (d is not null)
        {
            if (File.Exists(Path.Combine(d.FullName, "fuentes.lock.json"))) return d.FullName;
            d = d.Parent;
        }
        throw new InvalidOperationException("No se encontro fuentes.lock.json: usa --repo.");
    }
}

internal sealed class Options
{
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _flags = new(StringComparer.OrdinalIgnoreCase);
    public string? Repo => Get("repo");
    public string? Raiz => Get("raiz");
    public string? Db => Get("db");
    public string? Get(string k) => _values.GetValueOrDefault(k);
    public bool Flag(string k) => _flags.Contains(k);

    public static Options Parse(string[] args)
    {
        var o = new Options();
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException($"Argumento inesperado: {args[i]}");
            var key = args[i][2..];
            if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                o._values[key] = args[++i];
            else
                o._flags.Add(key);
        }
        return o;
    }
}
