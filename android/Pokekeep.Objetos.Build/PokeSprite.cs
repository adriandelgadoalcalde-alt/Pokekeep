// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Pokekeep.Objetos.Build;

public static partial class Generador
{
    /// <summary>Generación de PokeAPI (la de <c>item_game_indices</c>) a la que pertenece la numeración de cada juego del censo.</summary>
    private static readonly Dictionary<string, int> GeneracionPokeApi = new(StringComparer.Ordinal)
    {
        ["RBY"] = 1, ["GS"] = 2, ["C"] = 2, ["RS"] = 3, ["E"] = 3, ["FRLG"] = 3, ["COLO"] = 3, ["XD"] = 3, ["DP"] = 4, ["Pt"] = 4, ["HGSS"] = 4, ["BW"] = 5, ["B2W2"] = 5, ["XY"] = 6, ["ORAS"] = 6,
        ["SM"] = 7, ["USUM"] = 7, ["LGPE"] = 7, ["SWSH"] = 8, ["BDSP"] = 8, ["PLA"] = 8, ["SV"] = 9, ["ZA"] = 9,
    };

    /// <summary>(generación de PokeAPI, índice del juego) -> identificadores de PokeAPI que lo llevan (puede haber más de uno: un objeto equipado y su versión de la bolsa).</summary>
    internal static Dictionary<(int Gen, int Indice), List<string>> IndicesPokeApi(IEnumerable<string[]> filas, Dictionary<int, string> identificadores)
    {
        var d = new Dictionary<(int, int), List<string>>();
        foreach (var v in filas)
        {
            if (!identificadores.TryGetValue(int.Parse(v[0]), out var ident)) continue;
            var k = (int.Parse(v[1]), int.Parse(v[2]));
            if (!d.TryGetValue(k, out var l)) d[k] = l = [];
            if (!l.Contains(ident)) l.Add(ident);
        }
        return d;
    }

    internal static int? GeneracionDe(string juego) => GeneracionPokeApi.TryGetValue(juego, out var g) ? g : null;
}

/// <summary>Un objeto del Core al que PokéSprite da imagen, con la prueba de por qué es ese objeto.</summary>
internal sealed record HallazgoPokeSprite(Juego J, ObjetoJuego O, string Ruta, byte[] Png, string Evidencia);

/// <summary>
/// PokéSprite (msikma/pokesprite, revisión fijada): iconos de inventario de la serie principal con un mapa de ids (<c>data/item-map.json</c>: <c>item_NNNN</c> = id modernizado del objeto -> ruta del PNG).
/// Un objeto entra SOLO si coinciden las tres cosas, para ESE juego: (1) el id nativo del Core (modernizado, que en Gen. 4+ es el mismo) está en el mapa; (2) el nombre es el mismo objeto, o bien por el nombre del Core,
/// o bien porque PokeAPI (<c>item_game_indices</c>), de forma independiente, da para ese índice de esa generación el mismo objeto que PokéSprite; y (3) el PNG existe en la revisión fijada.
/// Si PokeAPI conoce otro objeto para ese índice, es un CONFLICTO y no entra (p. ej. el Leaf Letter de Let's Go: PokéSprite pone Eevee donde PokeAPI pone Pikachu).
/// </summary>
internal static class PokeSprite
{
    private static readonly Regex SufijoNumero = new(@"-\d+$", RegexOptions.CultureInvariant);
    private static readonly Regex SufijoPrimeraSegunda = new(@"--(held|bag)$", RegexOptions.CultureInvariant);
    private static readonly Regex CaramelosAV = new(@"^(?<stat>[a-z]+)(-(?<talla>l|xl))?$", RegexOptions.CultureInvariant);

    /// <summary>Las formas normalizadas del nombre de un objeto de PokéSprite («berry/silver-razz» = «Silver Razz Berry»; «av-candy/health-l» = «Health Candy L»; «key-item/rotom-bike-2» = «Rotom Bike»).</summary>
    internal static HashSet<string> Variantes(string ruta)
    {
        var partes = ruta.Split('/');
        var grupo = partes[0];
        var nombre = SufijoPrimeraSegunda.Replace(partes[^1], "");
        nombre = SufijoNumero.Replace(nombre, "");
        var set = new HashSet<string>(StringComparer.Ordinal) { Fuente.Norm(nombre) };
        if (grupo == "berry") set.Add(Fuente.Norm(nombre + " berry"));
        if (grupo == "av-candy" && CaramelosAV.Match(nombre) is { Success: true } m) set.Add(Fuente.Norm(m.Groups["stat"].Value + " candy " + m.Groups["talla"].Value));
        return set;
    }

    /// <summary>
    /// El identificador de PokeAPI (<paramref name="ident"/>) es el mismo objeto que las variantes de PokéSprite: igual una vez quitado el sufijo de forma (<c>--bag</c>, <c>--merge</c>...), o la forma de PokeAPI es una variante
    /// de ese nombre base (<c>rotom-bike--water-mode</c> y <c>legendary-clue-1</c> frente a <c>rotom-bike</c> y <c>legendary-clue</c>).
    /// </summary>
    internal static bool MismoObjeto(HashSet<string> variantes, string ident)
    {
        var sinForma = Fuente.Norm(Regex.Replace(ident, "--.*$", ""));
        var completo = Fuente.Norm(ident.Replace("--", " "));
        return variantes.Contains(sinForma) || variantes.Contains(completo) || variantes.Any(v => v.Length >= 8 && sinForma.StartsWith(v, StringComparison.Ordinal));
    }

    /// <summary>El nombre del Core sin el número final entre paréntesis que PKHeX añade a los objetos repetidos («Rotom Bike (2)»).</summary>
    internal static string NombreLimpio(string nombre) => Regex.Replace(nombre, @"\s*\(\d+\)$", "", RegexOptions.CultureInvariant);

    /// <summary>
    /// Los cristales Dinamax de Espada/Escudo (ids 1279-1578, «★And458»...): el Core los reconoce como tales (<c>ItemStorage8SWSH.IsDynamaxCrystal</c>) y todos comparten un icono, el de <c>other-item/dynamax-crystal</c> de PokéSprite
    /// (sin id en su mapa: está en item-unlinked.json). La prueba es la del Core por rango de id, no el nombre (los llama por la estrella).
    /// </summary>
    public static async Task<List<HallazgoPokeSprite>> CristalesDinamax(HttpClient http, string cache, bool sinDescargas, IReadOnlyList<(Juego J, ObjetoJuego O)> pendientes)
    {
        const string Ruta = "other-item/dynamax-crystal";
        var candidatos = pendientes.Where(p => p.J.Id == "SWSH" && PKHeX.Core.ItemStorage8SWSH.IsDynamaxCrystal((ushort)p.O.IdNativo)).ToList();
        if (candidatos.Count == 0) return [];
        var png = await Fuente.Obtener(http, Fuente.UrlPokeSprite(Ruta), Path.Combine(cache, "pokesprite", "items", "other-item", "dynamax-crystal.png"), sinDescargas);
        if (png is null) return [];
        if (!Generador.EsPng(png)) throw new InvalidDataException("PokéSprite other-item/dynamax-crystal.png no es un PNG.");
        return candidatos.Select(p => new HallazgoPokeSprite(p.J, p.O, Ruta, png,
            $"el Core marca el id {p.O.IdNativo} como cristal Dinamax (ItemStorage8SWSH.IsDynamaxCrystal, 1279-1578); todos los cristales comparten el icono «{Ruta}» de PokéSprite (item-unlinked.json); sin coincidencia de nombre")).ToList();
    }

    public static async Task<(List<HallazgoPokeSprite> Aceptados, Dictionary<(string Juego, int Nativo), string> Rechazos)> Buscar(
        HttpClient http, string cache, bool sinDescargas, IReadOnlyList<(Juego J, ObjetoJuego O, int Efectivo)> pendientes, Dictionary<(int Gen, int Indice), List<string>> indicesPokeApi)
    {
        var json = await Fuente.Obtener(http, Fuente.UrlPokeSpriteMapa, Path.Combine(cache, "pokesprite", "item-map.json"), sinDescargas) ?? throw new InvalidDataException("Falta item-map.json de PokéSprite.");
        var mapa = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? throw new InvalidDataException("item-map.json de PokéSprite vacío.");
        var aceptados = new List<HallazgoPokeSprite>();
        var rechazos = new Dictionary<(string, int), string>();
        var aComprobar = new List<(Juego J, ObjetoJuego O, string Ruta, string Evidencia)>();
        foreach (var (j, o, efectivo) in pendientes)
        {
            if (efectivo <= 0) continue;
            if (!mapa.TryGetValue($"item_{efectivo:0000}", out var ruta)) continue;
            var variantes = Variantes(ruta);
            var porNombre = variantes.Contains(Fuente.Norm(NombreLimpio(o.NombreEn)));
            var gen = Generador.GeneracionDe(j.Id);
            var deIndice = gen is { } g && indicesPokeApi.TryGetValue((g, o.IdNativo), out var l) ? l : null;
            var porIndice = deIndice is not null && deIndice.Any(i => MismoObjeto(variantes, i));
            if (deIndice is not null && !porIndice) { rechazos[(j.Id, o.IdNativo)] = $"conflicto con PokéSprite: item_{efectivo:0000} es «{ruta}» pero PokeAPI da «{string.Join("/", deIndice)}» para ese índice de la generación {gen}"; continue; }
            // Gen. 1-3: el id moderno sale de nuestra tabla POR NOMBRE, así que el nombre solo no demuestra nada; hace falta la confirmación independiente por índice de PokeAPI.
            if (j.Id is "RBY" or "GS" or "C" or "RS" or "E" or "FRLG" or "COLO" or "XD" && !porIndice) { rechazos[(j.Id, o.IdNativo)] = $"Gen. 1-3: PokéSprite tiene item_{efectivo:0000} («{ruta}») pero solo coincide el nombre; PokeAPI no confirma ese índice de la generación {gen}"; continue; }
            if (!porNombre && !porIndice) { rechazos[(j.Id, o.IdNativo)] = $"PokéSprite tiene item_{efectivo:0000} («{ruta}») pero ni el nombre del Core ni PokeAPI confirman que sea el mismo objeto"; continue; }
            var pruebas = new List<string> { $"id nativo {o.IdNativo} = item_{efectivo:0000} del mapa de PokéSprite («{ruta}»)" };
            if (porNombre) pruebas.Add($"nombre del Core «{NombreLimpio(o.NombreEn)}» = nombre de PokéSprite");
            if (porIndice) pruebas.Add($"PokeAPI item_game_indices (gen {gen}, índice {o.IdNativo}) = «{string.Join("/", deIndice!)}»");
            aComprobar.Add((j, o, ruta, string.Join("; ", pruebas)));
        }
        var bajados = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        await Parallel.ForEachAsync(aComprobar.Select(a => a.Ruta).Distinct().ToList(), new ParallelOptions { MaxDegreeOfParallelism = 6 }, async (ruta, _) =>
        {
            var b = await Fuente.Obtener(http, Fuente.UrlPokeSprite(ruta), Path.Combine(cache, "pokesprite", "items", ruta.Replace('/', Path.DirectorySeparatorChar) + ".png"), sinDescargas);
            if (b is not null && !Generador.EsPng(b)) throw new InvalidDataException($"PokéSprite {ruta}.png no es un PNG.");
            lock (bajados) bajados[ruta] = b;
        });
        foreach (var (j, o, ruta, evidencia) in aComprobar)
        {
            if (bajados[ruta] is { } png) aceptados.Add(new HallazgoPokeSprite(j, o, ruta, png, evidencia));
            else rechazos[(j.Id, o.IdNativo)] = $"PokéSprite lista «{ruta}» en su mapa pero el PNG no existe en la revisión fijada";
        }
        return (aceptados, rechazos);
    }
}
