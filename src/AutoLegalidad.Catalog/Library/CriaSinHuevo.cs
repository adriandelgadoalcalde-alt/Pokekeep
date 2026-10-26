// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Globalization;

namespace AutoLegalidad.Catalog.Library;

/// <summary>
/// Hito 5c: Librería honesta con la CRÍA. Las entradas de cría (<c>kind = 'Egg'</c>) llevan un destino «de huevo»: el juego de la Librería, salvo Colosseum y XD (<see cref="GameCubeIds"/>), que comparten el grupo CXD pero no huevos.
/// El recurso incrustado <c>Library/libreria-cria-sin-huevo.tsv</c> lista las entradas para las que el Core de PKHeX NO enumera ningún huevo en ese destino (lo regenera
/// <c>Hito5cCriaDeLaLibreriaTests.RegenerarLaTablaDeCriasSinHuevo</c> y lo comprueba entera <c>LaTablaEmbebidaCoincideConLaEnumeracionDelCore</c>): no hay origen de cría que fijar, así que
/// se atenúan con su motivo y no cuentan como añadibles. Para todas las demás el motor fija el huevo y el resultado es siempre el mismo.
/// </summary>
public static class CriaSinHuevo
{
    private static readonly Lazy<HashSet<long>> Table = new(Load);

    public static long KeyOf(int destination, int species, int form) => destination * 100_000_000L + species * 10_000L + form;

    public static bool Contains(int destination, int species, int form) => Table.Value.Contains(KeyOf(destination, species, form));

    /// <summary>Destino de huevo de una consulta: Colosseum o XD si es GameCube, si no el propio juego.</summary>
    public static int DestinationOf(int gameId, int gcDestination) => GameCubeIds.IsGameCube(gcDestination) ? gcDestination : gameId;

    /// <summary>Condición SQL (alias <c>e</c> de <c>library_entry</c>) que deja fuera las crías sin huevo del destino; null si no hay ninguna.</summary>
    public static string? SqlExclusion(int gameId, int gcDestination)
    {
        var dest = DestinationOf(gameId, gcDestination);
        var codes = Table.Value.Where(k => k / 100_000_000L == dest).Select(k => (k % 100_000_000L).ToString(CultureInfo.InvariantCulture)).ToList();
        return codes.Count == 0 ? null : $"NOT (e.kind = 'Egg' AND (e.species_id * 10000 + e.form_id) IN ({string.Join(',', codes)}))";
    }

    public static string Reason(int destination) => destination switch
    {
        GameCubeIds.Colosseum => "PKHeX no ofrece ninguna cría de esta especie en Pokémon Colosseum: no hay origen de cría que fijar y Pokekeep no lo sustituye por otro origen",
        GameCubeIds.Xd => "PKHeX no ofrece ninguna cría de esta especie en Pokémon XD: no hay origen de cría que fijar y Pokekeep no lo sustituye por otro origen",
        _ => "PKHeX no ofrece ninguna cría de esta especie en este juego: no hay origen de cría que fijar y Pokekeep no lo sustituye por otro origen",
    };

    public static int Count(int destination) => Table.Value.Count(k => k / 100_000_000L == destination);

    private static HashSet<long> Load()
    {
        using var s = typeof(CriaSinHuevo).Assembly.GetManifestResourceStream("Library/libreria-cria-sin-huevo.tsv") ?? throw new InvalidOperationException("Falta el recurso Library/libreria-cria-sin-huevo.tsv");
        using var r = new StreamReader(s);
        var set = new HashSet<long>();
        _ = r.ReadLine();
        while (r.ReadLine() is { } line)
        {
            if (line.Length == 0) continue;
            var c = line.Split('\t');
            set.Add(KeyOf(int.Parse(c[0], CultureInfo.InvariantCulture), int.Parse(c[1], CultureInfo.InvariantCulture), int.Parse(c[2], CultureInfo.InvariantCulture)));
        }
        return set;
    }
}
