// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Security.Cryptography;

namespace Pokekeep.Gritos.Build;

/// <summary>Las dos fuentes, fijadas por revisión: los .ogg de PokeAPI/cries y las tablas pokemon.csv / pokemon_forms.csv de PokeAPI/pokeapi.</summary>
public static class Fuente
{
    /// <summary>PokeAPI/cries (todo el audio © The Pokémon Company; el repositorio se distribuye bajo CC0 1.0). Confirmación del 12-oct-2026.</summary>
    public const string RevisionGritos = "b3a9044bb839b5e3152aaa536c7b463a0fcf0140";
    /// <summary>PokeAPI/pokeapi: la misma revisión que usa herramientas\completar-sprites-pixel.cjs.</summary>
    public const string RevisionDatos = "c80757193bd0889054e36f4209762360bdaa4b95";

    public static string UrlGrito(string estilo, int id) => $"https://raw.githubusercontent.com/PokeAPI/cries/{RevisionGritos}/cries/pokemon/{estilo}/{id}.ogg";
    public static string UrlCsv(string nombre) => $"https://raw.githubusercontent.com/PokeAPI/pokeapi/{RevisionDatos}/data/v2/csv/{nombre}.csv";

    public static readonly string[] Estilos = ["latest", "legacy"];

    public sealed record FilaPokemon(int Id, string Identificador, int Especie, bool Predeterminada);
    public sealed record FilaForma(string Identificador, string FormaId, int PokemonId);

    public static string Sha(ReadOnlySpan<byte> b) => Convert.ToHexString(SHA256.HashData(b));

    /// <summary>Lee (o descarga a la caché) un fichero. Null si no existe (404) o, con <paramref name="sinDescargas"/>, si no está en la caché.</summary>
    public static async Task<byte[]?> Obtener(HttpClient http, string url, string fichero, bool sinDescargas)
    {
        if (File.Exists(fichero)) return await File.ReadAllBytesAsync(fichero);
        var ausente = fichero + ".ausente";   // marca de «PokeAPI devolvió 404 para esto»: con la caché completa, --sin-descargas reproduce la misma salida
        if (File.Exists(ausente)) return null;
        if (sinDescargas) throw new InvalidDataException($"Falta en la caché ({Path.GetFileName(fichero)}) y --sin-descargas impide pedirlo.");
        for (var intento = 1; ; intento++)
        {
            try
            {
                using var r = await http.GetAsync(url);
                if (r.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(fichero)!);
                    await File.WriteAllBytesAsync(ausente, []);
                    return null;
                }
                r.EnsureSuccessStatusCode();
                var b = await r.Content.ReadAsByteArrayAsync();
                Directory.CreateDirectory(Path.GetDirectoryName(fichero)!);
                var tmp = fichero + ".parte";
                await File.WriteAllBytesAsync(tmp, b);
                File.Move(tmp, fichero, true);
                return b;
            }
            catch (HttpRequestException) when (intento < 4) { await Task.Delay(1000 * intento); }
            catch (TaskCanceledException) when (intento < 4) { await Task.Delay(1000 * intento); }
        }
    }

    public static IReadOnlyList<FilaPokemon> LeerPokemon(byte[] csv)
        => [.. Lineas(csv).Select(v => new FilaPokemon(int.Parse(v[0]), v[1], int.Parse(v[2]), v[7] == "1"))];

    public static IReadOnlyList<FilaForma> LeerFormas(byte[] csv)
        => [.. Lineas(csv).Select(v => new FilaForma(v[1], v[2], int.Parse(v[3])))];

    private static IEnumerable<string[]> Lineas(byte[] csv)
        => System.Text.Encoding.UTF8.GetString(csv).Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(l => l.TrimEnd('\r').Split(','));
}
