// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Diagnostics;
using AutoLegalidad.Catalog.Db;
using Microsoft.Data.Sqlite;

namespace AutoLegalidad.Engine.SolutionCache;

/// <summary>Resultado de <see cref="SolutionCacheConverter.ConvertToV2"/>; todo medido, nada estimado.</summary>
public sealed record SolutionConversionReport(
    long Rows, long RowsFromV1, long RowsAlreadyV2, long RowsColumnar, long RowsRawBlob,
    long SourceFileBytes, long DestinationFileBytes, long SourcePayloadBytes, long DestinationPayloadBytes,
    long ConvertMs, long VerifyMs, string SourceFormat);

/// <summary>
/// Herramienta de CONSTRUCCION (ADR 0097): convierte una <c>soluciones.sqlite</c> a payload v2 (binario) y comprueba FILA A FILA, leyendo los dos ficheros ya cerrados, que cada fila
/// (clave, familia, suma, exhaustiva, recorridas, coste, cota, semillas, IV y contenido binario byte a byte) es identica a la de origen. Copia <c>meta</c> tal cual: la identidad no cambia
/// (la sella despues <c>sellar-soluciones.py</c>). No toca el origen. El producto no la usa.
/// </summary>
public static class SolutionCacheConverter
{
    private sealed record RawRow(string Key, string Family, long Sum, long Exhaustive, long Scanned, long CostMs, long Bound, bool WasBinary, DecodedPayload Payload, int PayloadBytes);

    public static SolutionConversionReport ConvertToV2(string source, string destination)
    {
        source = Path.GetFullPath(source);
        destination = Path.GetFullPath(destination);
        if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("El destino no puede ser el origen: la conversion no toca el original.");
        if (!File.Exists(source)) throw new FileNotFoundException("No existe el origen.", source);
        if (File.Exists(destination)) throw new IOException("El destino ya existe; borralo antes (la conversion no sobrescribe).");
        var sw = Stopwatch.StartNew();
        long rows = 0, fromV1 = 0, alreadyV2 = 0, columnar = 0, raw = 0, srcPayload = 0, dstPayload = 0;
        string srcFormat;
        var meta = ReadMeta(source);
        using (var src = OpenImmutable(source))
        {
            srcFormat = SqliteSolutionCache.DetectFormat(src) == SolutionPayloadFormat.V2Binary ? "v2" : "v1";
            var dstCsb = new SqliteConnectionStringBuilder { DataSource = destination, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false };
            using var dst = new SqliteConnection(dstCsb.ToString());
            dst.Open();
            CatalogDatabase.Exec(dst, "PRAGMA journal_mode = DELETE;");
            CatalogDatabase.Exec(dst, SqliteSolutionCache.CreateMeta);
            CatalogDatabase.Exec(dst, SqliteSolutionCache.CreateSolution(SolutionPayloadFormat.V2Binary));
            using var tx = dst.BeginTransaction();
            using (var m = dst.CreateCommand())
            {
                m.Transaction = tx;
                m.CommandText = "INSERT INTO meta(k, v) VALUES($k, $v)";
                var pk = m.Parameters.Add("$k", SqliteType.Text);
                var pv = m.Parameters.Add("$v", SqliteType.Text);
                foreach (var (k, v) in meta)
                {
                    pk.Value = k;
                    pv.Value = v;
                    m.ExecuteNonQuery();
                }
            }
            using var ins = dst.CreateCommand();
            ins.Transaction = tx;
            ins.CommandText = "INSERT INTO solution(key, family, payload, sum, exhaustive, scanned, cost_ms, bound) VALUES($key, $family, $payload, $sum, $exhaustive, $scanned, $cost, $bound)";
            var pKey = ins.Parameters.Add("$key", SqliteType.Text);
            var pFam = ins.Parameters.Add("$family", SqliteType.Text);
            var pPay = ins.Parameters.Add("$payload", SqliteType.Blob);
            var pSum = ins.Parameters.Add("$sum", SqliteType.Integer);
            var pExh = ins.Parameters.Add("$exhaustive", SqliteType.Integer);
            var pScn = ins.Parameters.Add("$scanned", SqliteType.Integer);
            var pCst = ins.Parameters.Add("$cost", SqliteType.Integer);
            var pBnd = ins.Parameters.Add("$bound", SqliteType.Integer);
            foreach (var row in Rows(src))
            {
                var bytes = SolutionPayloadCodec.EncodeV2(row.Payload.Seeds, row.Payload.Ivs, row.Payload.Blob);
                pKey.Value = row.Key; pFam.Value = row.Family; pPay.Value = bytes;
                pSum.Value = row.Sum; pExh.Value = row.Exhaustive; pScn.Value = row.Scanned; pCst.Value = row.CostMs; pBnd.Value = row.Bound;
                ins.ExecuteNonQuery();
                rows++;
                if (row.WasBinary) alreadyV2++; else fromV1++;
                if (row.Payload.Blob is not null) { if ((bytes[1] & 4) != 0) columnar++; else raw++; }
                srcPayload += row.PayloadBytes;
                dstPayload += bytes.Length;
            }
            tx.Commit();
        }
        SqliteConnection.ClearAllPools();
        SqliteSolutionCache.Seal(destination);   // checkpoint + VACUUM + journal_mode=DELETE, como el original
        SqliteConnection.ClearAllPools();
        var convertMs = sw.ElapsedMilliseconds;
        sw.Restart();
        VerifyEqual(source, destination);
        var verifyMs = sw.ElapsedMilliseconds;
        foreach (var side in new[] { "-wal", "-shm", "-journal" })
            if (File.Exists(destination + side)) throw new InvalidOperationException($"Queda {destination}{side} tras sellar.");
        return new SolutionConversionReport(rows, fromV1, alreadyV2, columnar, raw, new FileInfo(source).Length, new FileInfo(destination).Length, srcPayload, dstPayload, convertMs, verifyMs, srcFormat);
    }

    /// <summary>
    /// Compara dos ficheros de soluciones (cada uno en v1 o v2) fila a fila: mismas claves en el mismo orden, mismos campos y el mismo contenido decodificado, byte a byte. Lanza
    /// <see cref="InvalidOperationException"/> en la primera diferencia (con la clave) y <see cref="SolutionPayloadException"/> si una fila es ilegible. Devuelve el numero de filas comparadas.
    /// </summary>
    public static long VerifyEqual(string a, string b)
    {
        var metaA = ReadMeta(a);
        var metaB = ReadMeta(b);
        if (metaA.Count != metaB.Count || metaA.Any(kv => !metaB.TryGetValue(kv.Key, out var v) || v != kv.Value)) throw new InvalidOperationException("La tabla meta (identidad) difiere entre los dos ficheros.");
        long n = 0;
        using var ca = OpenImmutable(a);
        using var cb = OpenImmutable(b);
        using var ea = Rows(ca).GetEnumerator();
        using var eb = Rows(cb).GetEnumerator();
        while (true)
        {
            var hasA = ea.MoveNext();
            var hasB = eb.MoveNext();
            if (hasA != hasB) throw new InvalidOperationException($"Distinto numero de filas (una acaba tras {n}).");
            if (!hasA) break;
            var x = ea.Current;
            var y = eb.Current;
            if (x.Key != y.Key) throw new InvalidOperationException($"Fila {n}: claves distintas ({x.Key} / {y.Key}).");
            if (x.Family != y.Family || x.Sum != y.Sum || x.Exhaustive != y.Exhaustive || x.Scanned != y.Scanned || x.CostMs != y.CostMs || x.Bound != y.Bound)
                throw new InvalidOperationException($"Fila {x.Key}: difieren los campos escalares.");
            if (!x.Payload.SameAs(y.Payload)) throw new InvalidOperationException($"Fila {x.Key}: difiere el contenido (semillas, IV o binario).");
            n++;
        }
        return n;
    }

    /// <summary>Todas las filas de un fichero (v1 o v2), sin comprobar la identidad: para convertir, comparar y auditar. Lanza <see cref="SolutionPayloadException"/> ante una fila ilegible.</summary>
    internal static List<(string Key, SolutionEntry Entry)> ReadAllRaw(string path)
    {
        using var c = OpenImmutable(path);
        return Rows(c).Select(r => (r.Key, new SolutionEntry(r.Family, r.Payload.Seeds, (int)r.Sum, r.Exhaustive != 0, (ulong)r.Scanned, r.CostMs, r.Payload.Ivs, r.Payload.Blob))).ToList();
    }

    private static SqliteConnection OpenImmutable(string path)
    {
        var csb = new SqliteConnectionStringBuilder { DataSource = new Uri(Path.GetFullPath(path)).AbsoluteUri + "?immutable=1", Mode = SqliteOpenMode.ReadOnly, Pooling = false };
        var c = new SqliteConnection(csb.ToString());
        c.Open();
        return c;
    }

    private static Dictionary<string, string> ReadMeta(string path)
    {
        using var c = OpenImmutable(path);
        var meta = new Dictionary<string, string>(StringComparer.Ordinal);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT k, v FROM meta";
        using var r = cmd.ExecuteReader();
        while (r.Read()) meta[r.GetString(0)] = r.GetString(1);
        if (meta.Count == 0) throw new InvalidOperationException("El fichero no tiene identidad (tabla meta vacia).");
        return meta;
    }

    private static IEnumerable<RawRow> Rows(SqliteConnection c)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT key, family, payload, sum, exhaustive, scanned, cost_ms, bound FROM solution ORDER BY key";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var key = r.GetString(0);
            var stored = SqliteSolutionCache.ReadPayloadValue(r, 2);
            DecodedPayload payload;
            try { payload = SolutionPayloadCodec.Decode(stored); }
            catch (SolutionPayloadException ex) { throw new SolutionPayloadException($"fila {key}: {ex.Message}"); }
            yield return new RawRow(key, r.GetString(1), r.GetInt64(3), r.GetInt64(4), r.GetInt64(5), r.GetInt64(6), r.GetInt64(7), stored is byte[], payload,
                stored is byte[] b ? b.Length : System.Text.Encoding.UTF8.GetByteCount((string)stored!));
        }
    }
}
