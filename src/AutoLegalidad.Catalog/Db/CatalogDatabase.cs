// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Microsoft.Data.Sqlite;

namespace AutoLegalidad.Catalog.Db;

/// <summary>
/// Apertura de la base de datos del catalogo con los PRAGMA exigidos por el plan (§8): <c>foreign_keys=ON</c>,
/// <c>journal_mode=WAL</c> y <c>busy_timeout</c>. Se aplican en CADA conexion (no persisten salvo WAL) y se comprueban.
/// </summary>
public static class CatalogDatabase
{
    public const int BusyTimeoutMs = 5000;

    /// <summary>Abre (y crea si no existe) un fichero SQLite con los PRAGMA del catalogo ya aplicados y verificados.</summary>
    public static SqliteConnection Open(string path, bool readOnly = false)
    {
        var csb = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = readOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWriteCreate,
            Pooling = false, // Un fichero que se copia/borra no debe quedar retenido por el pool.
            ForeignKeys = true,
        };
        var c = new SqliteConnection(csb.ToString());
        c.Open();
        ApplyPragmas(c, readOnly);
        return c;
    }

    /// <summary>Base en memoria (sin WAL: SQLite no lo admite en memoria). Solo para pruebas de esquema.</summary>
    public static SqliteConnection OpenInMemory()
    {
        var c = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        c.Open();
        ApplyPragmas(c, readOnly: false, memory: true);
        return c;
    }

    private static void ApplyPragmas(SqliteConnection c, bool readOnly, bool memory = false)
    {
        Exec(c, "PRAGMA foreign_keys = ON;");
        Exec(c, $"PRAGMA busy_timeout = {BusyTimeoutMs};");
        if (!readOnly && !memory)
        {
            var mode = Scalar(c, "PRAGMA journal_mode = WAL;");
            if (!string.Equals(mode, "wal", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"No se pudo activar WAL (journal_mode={mode}).");
        }
        if (Scalar(c, "PRAGMA foreign_keys;") != "1")
            throw new InvalidOperationException("foreign_keys no quedo activado.");
        if (Scalar(c, "PRAGMA busy_timeout;") != BusyTimeoutMs.ToString())
            throw new InvalidOperationException("busy_timeout no quedo aplicado.");
    }

    /// <summary>Valores reales de los PRAGMA de una conexion (para informes y pruebas).</summary>
    public static IReadOnlyDictionary<string, string> ReadPragmas(SqliteConnection c) => new Dictionary<string, string>
    {
        ["foreign_keys"] = Scalar(c, "PRAGMA foreign_keys;"),
        ["journal_mode"] = Scalar(c, "PRAGMA journal_mode;"),
        ["busy_timeout"] = Scalar(c, "PRAGMA busy_timeout;"),
        ["sqlite_version"] = Scalar(c, "SELECT sqlite_version();"),
    };

    /// <summary>Cierra el WAL dejando un unico fichero autocontenido (para empaquetar). Cierra tambien la conexion.</summary>
    public static void CheckpointAndClose(SqliteConnection c, bool vacuum = true)
    {
        Exec(c, "PRAGMA wal_checkpoint(TRUNCATE);");
        if (vacuum) Exec(c, "VACUUM;");
        Exec(c, "PRAGMA journal_mode = DELETE;");
        c.Close();
    }

    public static void Exec(SqliteConnection c, string sql, SqliteTransaction? tx = null)
    {
        using var cmd = c.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Ejecuta un script SENTENCIA A SENTENCIA. Motivo (hallado en el Hito 1, ver bitacora): con varias sentencias en un unico comando,
    /// <c>Microsoft.Data.Sqlite</c> 10.0.12 dejo de ejecutar el resto del script SIN lanzar excepcion cuando una sentencia posterior a un
    /// <c>ALTER TABLE ... ADD COLUMN</c> fallaba (esquema con claves foraneas). Una migracion NO puede quedar a medias sin enterarse.
    /// </summary>
    public static void ExecScript(SqliteConnection c, string script, SqliteTransaction? tx = null)
    {
        foreach (var statement in SplitStatements(script))
            Exec(c, statement, tx);
    }

    /// <summary>Divide un script en sentencias completas usando <c>sqlite3_complete</c> (respeta cadenas, comentarios y cuerpos de disparador).</summary>
    public static IReadOnlyList<string> SplitStatements(string script)
    {
        var result = new List<string>();
        var start = 0;
        for (var i = 0; i < script.Length; i++)
        {
            if (script[i] != ';') continue;
            var candidate = script[start..(i + 1)];
            if (SQLitePCL.raw.sqlite3_complete(SQLitePCL.utf8z.FromString(candidate)) == 0) continue;
            if (HasSql(candidate)) result.Add(candidate.Trim());
            start = i + 1;
        }
        var tail = script[start..];
        if (HasSql(tail)) result.Add(tail.Trim());
        return result;
    }

    private static bool HasSql(string text)
        => text.Split('\n').Select(l => l.Trim()).Any(l => l.Length > 0 && !l.StartsWith("--", StringComparison.Ordinal));

    public static string Scalar(SqliteConnection c, string sql, SqliteTransaction? tx = null)
    {
        using var cmd = c.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        return Convert.ToString(cmd.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
    }

    public static long Count(SqliteConnection c, string sql, SqliteTransaction? tx = null)
        => long.Parse(Scalar(c, sql, tx), System.Globalization.CultureInfo.InvariantCulture);
}
