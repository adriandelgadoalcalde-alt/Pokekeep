// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace AutoLegalidad.Catalog.Db;

/// <summary>INSERT preparado y reutilizable dentro de una transaccion (los parametros son posicionales).</summary>
public sealed class PreparedInsert : IDisposable
{
    private readonly SqliteCommand _cmd;
    private readonly SqliteParameter[] _params;

    public PreparedInsert(SqliteConnection c, SqliteTransaction tx, string table, params string[] columns)
    {
        _cmd = c.CreateCommand();
        _cmd.Transaction = tx;
        _cmd.CommandText = $"INSERT INTO {table}({string.Join(',', columns)}) VALUES({string.Join(',', columns.Select((_, i) => "$p" + i.ToString(CultureInfo.InvariantCulture)))})";
        _params = columns.Select((_, i) =>
        {
            var p = _cmd.CreateParameter();
            p.ParameterName = "$p" + i.ToString(CultureInfo.InvariantCulture);
            _cmd.Parameters.Add(p);
            return p;
        }).ToArray();
        _cmd.Prepare();
    }

    public int Run(params object?[] values)
    {
        if (values.Length != _params.Length)
            throw new ArgumentException($"Se esperaban {_params.Length} valores y llegaron {values.Length}.");
        for (var i = 0; i < values.Length; i++)
            _params[i].Value = values[i] ?? DBNull.Value;
        return _cmd.ExecuteNonQuery();
    }

    public void Dispose() => _cmd.Dispose();
}

public static class Hashing
{
    public static string Sha256Hex(ReadOnlySpan<byte> data) => Convert.ToHexString(SHA256.HashData(data));

    public static string Sha256Hex(string text) => Sha256Hex(Encoding.UTF8.GetBytes(text));

    public static string NowUtc() => DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
}
