// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Globalization;
using Microsoft.Data.Sqlite;

namespace AutoLegalidad.Catalog.Library;

/// <summary>
/// R-014 «Añadir evento»: los eventos que ofrece un juego, con la misma forma de fila que la Librería. Salen de <c>event_offer</c> (construida con el Core, tarjeta a tarjeta y juego a juego):
/// primero los <see cref="Native"/> (el juego recibió esa distribución) y, aparte, los <see cref="Transfer"/> (se crean en un juego anterior o de otro contexto y se transfieren).
/// Los filtros (texto, idioma, región, fechas, especie, forma y «sin HOME») son los de <see cref="LibraryFilter"/>; <see cref="LibraryRow.Via"/> dice de qué grupo es cada fila.
/// </summary>
public static class EventQueries
{
    public const string Native = "nativo";
    public const string Transfer = "transferencia";

    /// <summary>Eventos del juego <see cref="LibraryFilter.GameId"/> (nativos primero), paginados con <see cref="LibraryFilter.Limit"/> y <see cref="LibraryFilter.Offset"/>. <paramref name="via"/> limita a un solo grupo.</summary>
    public static IReadOnlyList<LibraryRow> EventosParaJuego(SqliteConnection c, LibraryFilter filtro, string? via = null)
    {
        using var cmd = LibraryQueries.Build(c, filtro, count: false, events: true, via);
        var rows = new List<LibraryRow>();
        using var r = cmd.ExecuteReader();
        var gameName = GameName(c, filtro.GameId);
        while (r.Read())
        {
            var row = LibraryQueries.ReadRow(r);
            // La fila es la de la tarjeta en su juego de origen; en el juego de destino se muestra con el nombre del destino. Lo que la Librería ya sabía de la tarjeta (atenuada porque PKHeX no la
            // tiene en su base o porque la medición la rechaza) se conserva tal cual: la oferta pasó el Core en el destino, pero el motor no puede generar esas tarjetas, así que no se enseñan como añadibles.
            rows.Add(row with
            {
                GameId = filtro.GameId,
                GameName = gameName,
                HomeRequired = row.HomeRequired || r.GetInt32(30) == 1,
                Via = r.GetString(29),
            });
        }
        return rows;
    }

    /// <summary>Total de eventos que cumplen el filtro (para paginar).</summary>
    public static long Contar(SqliteConnection c, LibraryFilter filtro, string? via = null)
    {
        using var cmd = LibraryQueries.Build(c, filtro, count: true, events: true, via);
        return Convert.ToInt64(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    /// <summary>Cuántos eventos ofrece cada juego por vía (las cifras de <c>docs/datos/eventos-por-juego.tsv</c>).</summary>
    public static IReadOnlyList<(int GameId, string Via, int Total, int ConHome)> CifrasPorJuego(SqliteConnection c)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT game_id, via, count(*), COALESCE(sum(home_required), 0) FROM event_offer GROUP BY game_id, via ORDER BY game_id, via";
        using var r = cmd.ExecuteReader();
        var list = new List<(int, string, int, int)>();
        while (r.Read()) list.Add((r.GetInt32(0), r.GetString(1), r.GetInt32(2), r.GetInt32(3)));
        return list;
    }

    /// <summary>¿Trae la base la tabla <c>event_offer</c>? (los catálogos anteriores a la migración 0008 no.)</summary>
    public static bool Existe(SqliteConnection c)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT count(*) FROM sqlite_master WHERE name = 'event_offer'";
        return Convert.ToInt64(cmd.ExecuteScalar(), CultureInfo.InvariantCulture) > 0;
    }

    private static string GameName(SqliteConnection c, int gameId)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT display_name_es FROM game WHERE game_id = $g";
        cmd.Parameters.AddWithValue("$g", gameId);
        return cmd.ExecuteScalar() as string ?? string.Empty;
    }
}
