// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace AutoLegalidad.Catalog.Db;

/// <summary>Una migracion versionada con su rollback. El SHA-256 es el del texto de <c>up</c> y se guarda en <c>schema_version</c>.</summary>
public sealed record Migration(int Version, string Name, string UpSql, string DownSql)
{
    public string UpSha256 { get; } = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(UpSql)));
}

public sealed class MigrationTamperedException(string message) : Exception(message);

/// <summary>Catalogo de migraciones embebidas en el ensamblado (<c>Migrations/NNNN_nombre.up.sql</c> y <c>.down.sql</c>).</summary>
public static class MigrationCatalog
{
    private static readonly Lazy<IReadOnlyList<Migration>> Cache = new(Load);
    public static IReadOnlyList<Migration> All => Cache.Value;
    public static int Latest => All[^1].Version;

    private static IReadOnlyList<Migration> Load()
    {
        var asm = typeof(MigrationCatalog).Assembly;
        var names = asm.GetManifestResourceNames().Where(n => n.StartsWith("Migrations/", StringComparison.Ordinal)).ToList();
        var result = new List<Migration>();
        foreach (var up in names.Where(n => n.EndsWith(".up.sql", StringComparison.Ordinal)).OrderBy(n => n, StringComparer.Ordinal))
        {
            var stem = up["Migrations/".Length..^".up.sql".Length];
            var parts = stem.Split('_', 2);
            var version = int.Parse(parts[0], CultureInfo.InvariantCulture);
            var down = $"Migrations/{stem}.down.sql";
            if (!names.Contains(down))
                throw new InvalidOperationException($"La migracion {stem} no tiene rollback ({down}).");
            result.Add(new Migration(version, parts[1], Read(asm, up), Read(asm, down)));
        }
        for (var i = 0; i < result.Count; i++)
            if (result[i].Version != i + 1)
                throw new InvalidOperationException("Las migraciones deben ser contiguas desde 1.");
        return result;
    }

    private static string Read(Assembly asm, string name)
    {
        using var s = asm.GetManifestResourceStream(name) ?? throw new InvalidOperationException($"Recurso ausente: {name}");
        using var r = new StreamReader(s, new UTF8Encoding(false));
        return r.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal);
    }
}

/// <summary>
/// Aplica y revierte migraciones. Cada paso va en UNA transaccion (DDL transaccional de SQLite): o se aplica entero o no se toca nada.
/// Al abrir se comprueba que las migraciones ya aplicadas siguen siendo las mismas (SHA-256).
/// </summary>
public sealed class Migrator(SqliteConnection connection)
{
    /// <summary>Version actual del esquema (0 si la base esta vacia).</summary>
    public int CurrentVersion()
    {
        if (CatalogDatabase.Count(connection, "SELECT count(*) FROM sqlite_master WHERE type='table' AND name='schema_version'") == 0)
            return 0;
        return (int)CatalogDatabase.Count(connection, "SELECT COALESCE(MAX(version),0) FROM schema_version");
    }

    /// <summary>Comprueba que cada migracion aplicada coincide con el SHA-256 registrado.</summary>
    public void VerifyIntegrity()
    {
        var current = CurrentVersion();
        if (current == 0) return;
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT version, migration_sha256 FROM schema_version ORDER BY version";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var v = r.GetInt32(0);
            var sha = r.GetString(1);
            var m = MigrationCatalog.All.FirstOrDefault(x => x.Version == v)
                    ?? throw new MigrationTamperedException($"La base tiene la migracion {v}, desconocida para este ejecutable.");
            if (!string.Equals(m.UpSha256, sha, StringComparison.OrdinalIgnoreCase))
                throw new MigrationTamperedException($"La migracion {v} ({m.Name}) ya aplicada difiere de la embebida.");
        }
    }

    public int MigrateToLatest() => MigrateTo(MigrationCatalog.Latest);

    /// <summary>Sube o baja hasta <paramref name="target"/> (0 = esquema vacio).</summary>
    public int MigrateTo(int target)
    {
        if (target < 0 || target > MigrationCatalog.Latest)
            throw new ArgumentOutOfRangeException(nameof(target));
        VerifyIntegrity();
        var current = CurrentVersion();
        while (current < target)
        {
            Apply(MigrationCatalog.All[current], up: true);
            current++;
        }
        while (current > target)
        {
            Apply(MigrationCatalog.All[current - 1], up: false);
            current--;
        }
        return current;
    }

    private void Apply(Migration m, bool up)
    {
        using var tx = connection.BeginTransaction();
        try
        {
            CatalogDatabase.ExecScript(connection, up ? m.UpSql : m.DownSql, tx);
            if (up)
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = "INSERT INTO schema_version(version, applied_utc, migration_sha256) VALUES($v,$t,$h)";
                cmd.Parameters.AddWithValue("$v", m.Version);
                cmd.Parameters.AddWithValue("$t", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
                cmd.Parameters.AddWithValue("$h", m.UpSha256);
                cmd.ExecuteNonQuery();
            }
            else if (CatalogDatabase.Count(connection, "SELECT count(*) FROM sqlite_master WHERE type='table' AND name='schema_version'", tx) > 0)
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = "DELETE FROM schema_version WHERE version=$v";
                cmd.Parameters.AddWithValue("$v", m.Version);
                cmd.ExecuteNonQuery();
            }
            // Integridad referencial tras el paso (un rollback mal hecho no debe dejar huerfanos).
            if (up)
            {
                var fk = CatalogDatabase.Count(connection, "SELECT count(*) FROM pragma_foreign_key_check", tx);
                if (fk != 0) throw new InvalidOperationException($"La migracion {m.Version} dejo {fk} violaciones de clave foranea.");
            }
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }
}
