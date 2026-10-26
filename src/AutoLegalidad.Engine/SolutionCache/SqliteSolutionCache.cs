// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Globalization;
using AutoLegalidad.Catalog.Db;
using Microsoft.Data.Sqlite;

namespace AutoLegalidad.Engine.SolutionCache;

/// <summary>
/// Cache de soluciones en un fichero SQLite (<c>soluciones.sqlite</c>, ADR 0037). Dos modos:
/// <list type="bullet">
/// <item><b>Lectura</b> (producto): <c>mode=ro&amp;immutable=1</c>; no escribe NADA (ni <c>-wal</c> ni <c>-shm</c>), abre una vez por proceso y consulta por clave. El fichero se entrega «sellado» (<see cref="Seal"/>: sin WAL).</item>
/// <item><b>Registro</b> (solo el precalculo): <c>ReadWriteCreate</c>, WAL, <c>busy_timeout=30000</c>; varios procesos pueden escribir a la vez. Crea <c>meta</c> con la identidad y se niega a continuar si el fichero ya tiene OTRA.</item>
/// </list>
/// Ninguna operacion publica lanza: ante cualquier fallo la cache queda desactivada (<see cref="Enabled"/> = false) con el motivo en <see cref="StatusText"/> y el motor busca como siempre.
/// </summary>
public sealed class SqliteSolutionCache : ISolutionCache, IDisposable
{
    public const int BusyTimeoutMs = 30000;
    public const int PayloadVersion = 1;

    internal const string CreateMeta = "CREATE TABLE IF NOT EXISTS meta(k TEXT PRIMARY KEY, v TEXT NOT NULL) STRICT, WITHOUT ROWID;";

    // ADR 0097: la columna payload es TEXT (v1, JSON) o BLOB (v2, binario). STRICT impide mezclar: un fichero es entero de un formato (el lector, aun asi, distingue por el tipo del valor).
    internal static string CreateSolution(SolutionPayloadFormat format)
        => $"CREATE TABLE IF NOT EXISTS solution(key TEXT PRIMARY KEY, family TEXT NOT NULL, payload {(format == SolutionPayloadFormat.V2Binary ? "BLOB" : "TEXT")} NOT NULL, sum INTEGER NOT NULL, exhaustive INTEGER NOT NULL, scanned INTEGER NOT NULL, cost_ms INTEGER NOT NULL, bound INTEGER NOT NULL) STRICT, WITHOUT ROWID;";

    // Mejor entrada: una exacta supera a una cota; entre exactas gana la mayor suma y, a igual suma, la exhaustiva y, a igual exhaustividad, la de MENOR coste si es exhaustiva
    // (misma solucion, mas barata) o la de MAYOR coste si no lo es (mas busqueda detras: se sirve a presupuestos mayores). Entre cotas gana la exhaustiva, luego la mas fuerte (menor).
    private const string UpsertSql = """
        INSERT INTO solution(key, family, payload, sum, exhaustive, scanned, cost_ms, bound)
        VALUES($key, $family, $payload, $sum, $exhaustive, $scanned, $cost, $bound)
        ON CONFLICT(key) DO UPDATE SET family = excluded.family, payload = excluded.payload, sum = excluded.sum,
            exhaustive = excluded.exhaustive, scanned = excluded.scanned, cost_ms = excluded.cost_ms, bound = excluded.bound
        WHERE CASE
            WHEN solution.bound = 1 AND excluded.bound = 0 THEN 1
            WHEN solution.bound = 0 AND excluded.bound = 1 THEN 0
            WHEN solution.bound = 0 THEN
                excluded.sum > solution.sum
                OR (excluded.sum = solution.sum AND (excluded.exhaustive > solution.exhaustive
                    OR (excluded.exhaustive = solution.exhaustive AND
                        CASE WHEN excluded.exhaustive = 1 THEN excluded.cost_ms < solution.cost_ms ELSE excluded.cost_ms > solution.cost_ms END)))
            ELSE
                excluded.exhaustive > solution.exhaustive
                OR (excluded.exhaustive = solution.exhaustive AND (excluded.sum < solution.sum
                    OR (excluded.sum = solution.sum AND excluded.cost_ms > solution.cost_ms)))
        END;
        """;

    private readonly object _gate = new();
    private SqliteConnection? _connection;
    private SqliteCommand? _get;
    private SqliteCommand? _put;
    private readonly bool _recording;
    private readonly SolutionPayloadFormat _format;
    private int _hits, _misses, _rejected, _puts, _writeErrors, _unreadable;
    private string? _firstUnreadable;

    private SqliteSolutionCache(SqliteConnection? connection, bool recording, string status, SolutionPayloadFormat format = SolutionPayloadFormat.V1Json)
    {
        _connection = connection;
        _recording = recording;
        _format = format;
        StatusText = status;
        if (connection is null) return;
        _get = connection.CreateCommand();
        _get.CommandText = "SELECT family, payload, sum, exhaustive, scanned, cost_ms FROM solution WHERE key = $key";
        _get.Parameters.Add("$key", SqliteType.Text);
        if (!recording) return;
        _put = connection.CreateCommand();
        _put.CommandText = UpsertSql;
        foreach (var (name, type) in new[] { ("$key", SqliteType.Text), ("$family", SqliteType.Text), ("$payload", format == SolutionPayloadFormat.V2Binary ? SqliteType.Blob : SqliteType.Text), ("$sum", SqliteType.Integer), ("$exhaustive", SqliteType.Integer), ("$scanned", SqliteType.Integer), ("$cost", SqliteType.Integer), ("$bound", SqliteType.Integer) })
            _put.Parameters.Add(name, type);
    }

    public bool Enabled => _connection is not null;
    public bool Recording => _recording && _connection is not null;
    public string StatusText { get; private set; }
    public int Hits => Volatile.Read(ref _hits);
    public int Misses => Volatile.Read(ref _misses);
    public int Rejected => Volatile.Read(ref _rejected);
    /// <summary>Escrituras ejecutadas (modo registro; incluye las que no mejoraban la entrada existente) y las que fallaron.</summary>
    public int Puts => Volatile.Read(ref _puts);
    public int WriteErrors => Volatile.Read(ref _writeErrors);
    /// <summary>Formato en el que ESCRIBE este objeto (el de la columna del fichero); al leer se acepta cualquiera (v1 o v2, por el tipo del valor).</summary>
    public SolutionPayloadFormat PayloadFormat => _format;
    /// <summary>Filas que existian pero no se pudieron leer (corruptas, truncadas, version desconocida): se tratan como ausentes (el motor busca y revalida siempre) PERO se cuentan y se dicen en <see cref="StatusText"/>; nunca en silencio.</summary>
    public int Unreadable => Volatile.Read(ref _unreadable);

    private void NoteUnreadable(string key, string reason)
    {
        if (Interlocked.Increment(ref _unreadable) == 1) _firstUnreadable = $"{key[..Math.Min(12, key.Length)]}: {reason}";
        StatusText = $"activa con filas ilegibles ({Unreadable}; la primera, {_firstUnreadable})";
    }

    public void NoteHit() => Interlocked.Increment(ref _hits);
    public void NoteMiss() => Interlocked.Increment(ref _misses);
    public void NoteRejected() => Interlocked.Increment(ref _rejected);

    /// <summary>Cache desactivada con un motivo (para registrarlo en la salida de diagnostico).</summary>
    public static SqliteSolutionCache Disabled(string reason) => new(null, false, reason);

    // ------------------------------------------------------------------------------------------------------------------ apertura
    /// <summary>Abre el fichero de soluciones en modo LECTURA. Si no existe, esta corrupto o su identidad no coincide con <paramref name="binding"/>, devuelve una cache desactivada.</summary>
    public static SqliteSolutionCache OpenReadOnly(string path, SolutionCacheBinding binding)
    {
        if (!File.Exists(path)) return Disabled("desactivada: no existe " + Path.GetFileName(path));
        SqliteConnection? c = null;
        try
        {
            var csb = new SqliteConnectionStringBuilder
            {
                DataSource = new Uri(Path.GetFullPath(path)).AbsoluteUri + "?immutable=1",
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false,
            };
            c = new SqliteConnection(csb.ToString());
            c.Open();
            var diff = binding.FirstDifference(ReadMeta(c));
            if (diff is not null)
            {
                c.Dispose();
                return Disabled($"invalidada: cambió {diff}");
            }
            // 0.6.6 (B3): un fichero con la identidad correcta pero sin la tabla de soluciones (o con otro esquema) no se activa: cada consulta lanzaria y tragaria una excepcion y el estado diria «activa».
            if (SchemaProblem(c) is { } problem)
            {
                c.Dispose();
                return Disabled($"corrupta: {problem}");
            }
            return new SqliteSolutionCache(c, recording: false, "activa (solo lectura)", DetectFormat(c));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            c?.Dispose();
            return Disabled($"error al abrir: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>Formato de la columna <c>payload</c> segun su tipo declarado (BLOB = v2; cualquier otro = v1).</summary>
    internal static SolutionPayloadFormat DetectFormat(SqliteConnection c)
    {
        try
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT type FROM pragma_table_info('solution') WHERE name = 'payload'";
            return string.Equals(cmd.ExecuteScalar() as string, "BLOB", StringComparison.OrdinalIgnoreCase) ? SolutionPayloadFormat.V2Binary : SolutionPayloadFormat.V1Json;
        }
        catch (SqliteException) { return SolutionPayloadFormat.V1Json; }   // informativo al leer; al escribir, un Put en BLOB sobre una columna TEXT fallaria con WriteErrors (nunca en silencio)
    }

    /// <summary>Null si la tabla <c>solution</c> existe con las columnas que usa la consulta; si no, el motivo.</summary>
    private static string? SchemaProblem(SqliteConnection c)
    {
        try
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT key, family, payload, sum, exhaustive, scanned, cost_ms FROM solution LIMIT 0";
            using var reader = cmd.ExecuteReader();
            return null;
        }
        catch (SqliteException ex) { return "falta la tabla de soluciones o su esquema no es el esperado (" + ex.Message + ")"; }
    }

    /// <summary>
    /// Abre (o crea) el fichero en modo REGISTRO. Si ya existe con OTRA identidad, devuelve una cache desactivada («invalidada: cambió X»): el precalculo no mezcla versiones.
    /// Seguro con varios procesos a la vez: la identidad se escribe dentro de una transaccion inmediata y gana el primero.
    /// </summary>
    public static SqliteSolutionCache OpenRecording(string path, SolutionCacheBinding binding, SolutionPayloadFormat newFileFormat = SolutionPayloadFormat.V1Json)
    {
        SqliteConnection? c = null;
        try
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var csb = new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false };
            c = new SqliteConnection(csb.ToString());
            c.Open();
            CatalogDatabase.Exec(c, $"PRAGMA busy_timeout = {BusyTimeoutMs};");
            var mode = CatalogDatabase.Scalar(c, "PRAGMA journal_mode = WAL;");
            if (!string.Equals(mode, "wal", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException($"No se pudo activar WAL (journal_mode={mode}).");
            CatalogDatabase.Exec(c, "PRAGMA synchronous = NORMAL;");
            CatalogDatabase.Exec(c, CreateMeta);
            CatalogDatabase.Exec(c, CreateSolution(newFileFormat));   // IF NOT EXISTS: un fichero que ya existe conserva SU formato (se detecta abajo)

            // La identidad se escribe UNA vez, de forma atomica: si dos procesos crean el fichero a la vez, gana el primero y el otro la compara.
            using (var tx = c.BeginTransaction(deferred: false))
            {
                using (var begin = c.CreateCommand())
                {
                    begin.Transaction = tx;
                    begin.CommandText = "INSERT OR IGNORE INTO meta(k, v) VALUES($k, $v)";
                    var pk = begin.Parameters.Add("$k", SqliteType.Text);
                    var pv = begin.Parameters.Add("$v", SqliteType.Text);
                    var empty = Convert.ToInt64(CatalogDatabase.Scalar(c, "SELECT count(*) FROM meta", tx), CultureInfo.InvariantCulture) == 0;
                    if (empty)
                        foreach (var (k, v) in binding.Values)
                        {
                            pk.Value = k;
                            pv.Value = v;
                            begin.ExecuteNonQuery();
                        }
                }
                tx.Commit();
            }
            var diff = binding.FirstDifference(ReadMeta(c));
            if (diff is not null)
            {
                c.Dispose();
                return Disabled($"invalidada: cambió {diff} (el fichero se precalculó con otra versión; no se mezcla)");
            }
            return new SqliteSolutionCache(c, recording: true, "activa (registro)", DetectFormat(c));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            c?.Dispose();
            return Disabled($"error al abrir en registro: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>Deja el fichero listo para entregar: pasa el WAL al fichero principal, compacta y vuelve al modo <c>DELETE</c> (sin <c>-wal</c>/<c>-shm</c>). Solo cuando ningun proceso lo tiene abierto.</summary>
    public static void Seal(string path)
    {
        var c = CatalogDatabase.Open(path);
        CatalogDatabase.CheckpointAndClose(c, vacuum: true);
    }

    private static Dictionary<string, string> ReadMeta(SqliteConnection c)
    {
        var meta = new Dictionary<string, string>(StringComparer.Ordinal);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT k, v FROM meta";
        using var r = cmd.ExecuteReader();
        while (r.Read()) meta[r.GetString(0)] = r.GetString(1);
        if (meta.Count == 0) throw new InvalidOperationException("El fichero no tiene identidad (tabla meta vacia).");
        return meta;
    }

    // ------------------------------------------------------------------------------------------------------------------ lectura y escritura
    public bool TryGet(string key, out SolutionEntry entry)
    {
        entry = null!;
        if (_connection is null) return false;
        try
        {
            lock (_gate)
            {
                if (_get is null) return false;
                _get.Parameters["$key"].Value = key;
                using var r = _get.ExecuteReader();
                if (!r.Read()) return false;
                var family = r.GetString(0);
                DecodedPayload payload;
                try { payload = SolutionPayloadCodec.Decode(ReadPayloadValue(r, 1)); }
                catch (SolutionPayloadException ex)
                {
                    // Fila ilegible (corrupta, truncada, version desconocida): ausente para el motor (busca y revalida siempre), pero CONTADA y dicha en StatusText.
                    NoteUnreadable(key, ex.Message);
                    return false;
                }
                entry = new SolutionEntry(family, payload.Seeds, (int)r.GetInt64(2), r.GetInt64(3) != 0, (ulong)r.GetInt64(4), r.GetInt64(5), payload.Ivs, payload.Blob);
                return true;
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            entry = null!;
            return false; // fallo de SQLite al leer la fila: se trata como ausente (el motor busca)
        }
    }

    /// <summary>El valor de la columna payload tal como lo guarda SQLite: <c>string</c> (v1) o <c>byte[]</c> (v2).</summary>
    internal static object? ReadPayloadValue(SqliteDataReader r, int ordinal)
    {
        if (r.IsDBNull(ordinal)) return null;
        return r.GetFieldType(ordinal) == typeof(byte[]) ? r.GetFieldValue<byte[]>(ordinal) : r.GetString(ordinal);
    }

    public void Put(string key, SolutionEntry entry)
    {
        if (_connection is null || !_recording || _put is null) return;
        try
        {
            object payload = _format == SolutionPayloadFormat.V2Binary
                ? SolutionPayloadCodec.EncodeV2(entry.Seeds, entry.Ivs, entry.Blob)
                : SolutionPayloadCodec.EncodeV1(entry.Seeds, entry.Ivs, entry.Blob);
            lock (_gate)
            {
                _put.Parameters["$key"].Value = key;
                _put.Parameters["$family"].Value = entry.Family;
                _put.Parameters["$payload"].Value = payload;
                _put.Parameters["$sum"].Value = entry.Sum;
                _put.Parameters["$exhaustive"].Value = entry.Exhaustive ? 1 : 0;
                _put.Parameters["$scanned"].Value = (long)entry.Scanned;
                _put.Parameters["$cost"].Value = entry.CostMs;
                _put.Parameters["$bound"].Value = entry.IsBound ? 1 : 0;
                _put.ExecuteNonQuery();
            }
            Interlocked.Increment(ref _puts);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Interlocked.Increment(ref _writeErrors);
            StatusText = $"activa (registro) con errores de escritura: {ex.GetType().Name}: {ex.Message}";
        }
    }

    /// <summary>Numero de soluciones guardadas (diagnostico; recorre la tabla).</summary>
    public long CountEntries()
    {
        if (_connection is null) return 0;
        lock (_gate) return Convert.ToInt64(CatalogDatabase.Scalar(_connection, "SELECT count(*) FROM solution"), CultureInfo.InvariantCulture);
    }

    /// <summary>Todas las filas (clave + entrada), para la auditoria y las pruebas.</summary>
    public List<(string Key, SolutionEntry Entry)> ReadAll()
    {
        var list = new List<(string, SolutionEntry)>();
        if (_connection is null) return list;
        lock (_gate)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT key, family, payload, sum, exhaustive, scanned, cost_ms FROM solution ORDER BY key";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                // La auditoria y las pruebas leen TODO: una fila ilegible lanza SolutionPayloadException con la clave (no se omite).
                DecodedPayload payload;
                try { payload = SolutionPayloadCodec.Decode(ReadPayloadValue(r, 2)); }
                catch (SolutionPayloadException ex) { throw new SolutionPayloadException($"fila {r.GetString(0)}: {ex.Message}"); }
                list.Add((r.GetString(0), new SolutionEntry(r.GetString(1), payload.Seeds, (int)r.GetInt64(3), r.GetInt64(4) != 0, (ulong)r.GetInt64(5), r.GetInt64(6), payload.Ivs, payload.Blob)));
            }
        }
        return list;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _get?.Dispose();
            _put?.Dispose();
            _connection?.Dispose();
            _get = null;
            _put = null;
            _connection = null;
        }
    }
}
