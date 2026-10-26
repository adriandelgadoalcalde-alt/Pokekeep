// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Security.Cryptography;
using System.Text;

namespace Pokekeep.Objetos.Build;

/// <summary>Las fuentes, fijadas por revisión: los PNG de PokeAPI/sprites y las tablas items.csv / item_names.csv de PokeAPI/pokeapi.</summary>
public static class Fuente
{
    /// <summary>PokeAPI/sprites (todo el arte © The Pokémon Company; el repositorio se distribuye bajo CC0 1.0 según su LICENCE.txt). Confirmación del 14-oct-2026.</summary>
    public const string RevisionSprites = "0dc105d5b12416932f29b594078282ab4be045f5";
    /// <summary>PokeAPI/pokeapi: la misma revisión que usan los gritos de serie (X11) y los sprites adicionales.</summary>
    public const string RevisionDatos = "c80757193bd0889054e36f4209762360bdaa4b95";

    /// <summary>PokéSprite (msikma/pokesprite): la revisión fijada (última de la rama master, 22-jul-2022; incluye los iconos de Leyendas: Arceus).</summary>
    public const string RevisionPokeSprite = "c5aaa610ff2acdf7fd8e2dccd181bca8be9fcb3e";
    public const string FuentePokeSprite = "msikma/pokesprite";
    public const string UrlPokeSpriteMapa = "https://raw.githubusercontent.com/msikma/pokesprite/" + RevisionPokeSprite + "/data/item-map.json";
    public static string UrlPokeSprite(string ruta) => $"https://raw.githubusercontent.com/msikma/pokesprite/{RevisionPokeSprite}/items/{ruta}.png";

    /// <summary>Nombre de la fuente de PokeAPI, en la procedencia.</summary>
    public const string FuentePokeApi = "PokeAPI/sprites";

    /// <summary>URL del PNG de un objeto: <paramref name="carpeta"/> vacía = raíz de <c>sprites/items/</c>; si no, <c>gen3</c>, <c>gen5</c>, <c>gen8</c>, <c>gen9</c>.</summary>
    public static string UrlSprite(string carpeta, string identificador)
        => $"https://raw.githubusercontent.com/PokeAPI/sprites/{RevisionSprites}/sprites/items/{(carpeta.Length == 0 ? "" : carpeta + "/")}{identificador}.png";

    public static string UrlCsv(string nombre) => $"https://raw.githubusercontent.com/PokeAPI/pokeapi/{RevisionDatos}/data/v2/csv/{nombre}.csv";

    public static string Sha(ReadOnlySpan<byte> b) => Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();

    /// <summary>Lee (o descarga a la caché) un fichero. Null si no existe (404) o, con <paramref name="sinDescargas"/>, si no está marcado como ausente en la caché.</summary>
    public static async Task<byte[]?> Obtener(HttpClient http, string url, string fichero, bool sinDescargas)
    {
        // Dos peticiones al mismo fichero a la vez (dos claves con el mismo identificador) no se pisan: la segunda espera y lo encuentra en la caché.
        var cerrojo = Cerrojos.GetOrAdd(fichero, _ => new SemaphoreSlim(1, 1));
        await cerrojo.WaitAsync();
        try { return await ObtenerSinCerrojo(http, url, fichero, sinDescargas); }
        finally { cerrojo.Release(); }
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> Cerrojos = new(StringComparer.OrdinalIgnoreCase);

    private static async Task<byte[]?> ObtenerSinCerrojo(HttpClient http, string url, string fichero, bool sinDescargas)
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

    /// <summary>Filas de un CSV de PokeAPI (con comillas RFC 4180), sin la cabecera.</summary>
    public static IEnumerable<string[]> Filas(byte[] csv)
    {
        foreach (var linea in Encoding.UTF8.GetString(csv).Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1))
            yield return Campos(linea.TrimEnd('\r'));
    }

    private static string[] Campos(string linea)
    {
        var campos = new List<string>(); var sb = new StringBuilder(); var comillas = false;
        for (var i = 0; i < linea.Length; i++)
        {
            var c = linea[i];
            if (comillas) { if (c == '"') { if (i + 1 < linea.Length && linea[i + 1] == '"') { sb.Append('"'); i++; } else comillas = false; } else sb.Append(c); }
            else if (c == '"') comillas = true;
            else if (c == ',') { campos.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(c);
        }
        campos.Add(sb.ToString());
        return [.. campos];
    }

    /// <summary>Nombre normalizado para cruzar los de PKHeX con los de PokeAPI: sin acentos, en minúsculas y solo letras y cifras («Poké Ball» = «Poke Ball»; «S.S. Ticket» = «SS Ticket»).</summary>
    public static string Norm(string nombre)
    {
        var sb = new StringBuilder();
        foreach (var c in nombre.Normalize(NormalizationForm.FormD))
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }
}
