// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;

namespace Pokekeep.Objetos.Build;

/// <summary>Un juego (o pareja/familia de juegos con la misma mochila) del que se comprueba la cobertura. <paramref name="TipoSav"/> es el nombre de clase del Core de sus partidas.</summary>
public sealed record Juego(string Id, string Nombre, GameVersion Version, params string[] TipoSav);

/// <summary>Un objeto que el juego admite en alguna bolsa, con su id nativo, su id modernizado y su nombre inglés (el que cruza con PokeAPI).</summary>
public sealed record ObjetoJuego(Juego Juego, EntityContext Contexto, GameVersion Version, int IdNativo, int IdMostrado, string NombreEn, string Bolsillos);

/// <summary>
/// El universo de objetos de cada juego: todo lo que su mochila admite según el Core (<c>InventoryPouch.GetAllItems</c>, lo que ofrece el editor), sacado de una partida EN BLANCO del Core.
/// Las partidas públicas de <c>saves-prueba</c> solo añaden «en cuántas aparece cada objeto».
/// </summary>
public static class Universo
{
    public static readonly Juego[] Juegos =
    [
        new("RBY", "Rojo/Azul/Amarillo (Gen. 1)", GameVersion.RD, "SAV1"),
        new("GS", "Oro/Plata (Gen. 2)", GameVersion.GD, "SAV2"),
        new("C", "Cristal (Gen. 2)", GameVersion.C, "SAV2"),
        new("RS", "Rubí/Zafiro (Gen. 3)", GameVersion.R, "SAV3RS"),
        new("E", "Esmeralda (Gen. 3)", GameVersion.E, "SAV3E"),
        new("FRLG", "Rojo Fuego/Verde Hoja (Gen. 3)", GameVersion.FR, "SAV3FRLG"),
        new("COLO", "Colosseum (Gen. 3)", GameVersion.COLO, "SAV3Colosseum"),
        new("XD", "XD (Gen. 3)", GameVersion.XD, "SAV3XD"),
        new("DP", "Diamante/Perla (Gen. 4)", GameVersion.D, "SAV4DP"),
        new("Pt", "Platino (Gen. 4)", GameVersion.Pt, "SAV4Pt"),
        new("HGSS", "Oro HeartGold/Plata SoulSilver (Gen. 4)", GameVersion.HG, "SAV4HGSS"),
        new("BW", "Blanco/Negro (Gen. 5)", GameVersion.W, "SAV5BW"),
        new("B2W2", "Blanco 2/Negro 2 (Gen. 5)", GameVersion.W2, "SAV5B2W2"),
        new("XY", "X/Y (Gen. 6)", GameVersion.X, "SAV6XY"),
        new("ORAS", "Rubí Omega/Zafiro Alfa (Gen. 6)", GameVersion.OR, "SAV6AO"),
        new("SM", "Sol/Luna (Gen. 7)", GameVersion.SN, "SAV7SM"),
        new("USUM", "Ultrasol/Ultraluna (Gen. 7)", GameVersion.US, "SAV7USUM"),
        new("LGPE", "Let's Go Pikachu/Eevee (Gen. 7)", GameVersion.GP, "SAV7b"),
        new("SWSH", "Espada/Escudo (Gen. 8)", GameVersion.SW, "SAV8SWSH"),
        new("BDSP", "Diamante Brillante/Perla Reluciente (Gen. 8)", GameVersion.BD, "SAV8BS"),
        new("PLA", "Leyendas Pokémon: Arceus (Gen. 8)", GameVersion.PLA, "SAV8LA"),
        new("SV", "Escarlata/Púrpura (Gen. 9)", GameVersion.SL, "SAV9SV"),
        new("ZA", "Leyendas Pokémon: Z-A (Gen. 9)", GameVersion.ZA, "SAV9ZA"),
    ];

    /// <summary>El juego al que pertenece una partida cargada; null si no está en la lista (p. ej. Battle Revolution).</summary>
    public static Juego? DeSav(SaveFile sav)
    {
        var tipo = sav.GetType().Name;
        var candidatos = Juegos.Where(j => j.TipoSav.Contains(tipo)).ToList();
        if (candidatos.Count <= 1) return candidatos.FirstOrDefault();
        // SAV2: Cristal tiene su propio universo de objetos.
        return sav.Version == GameVersion.C ? candidatos.First(j => j.Id == "C") : candidatos.First(j => j.Id != "C");
    }

    /// <summary>Carpeta de partidas públicas, para los juegos cuya partida en blanco el Core no sabe crear o no tiene mochila legible (Colosseum y Leyendas: Arceus).</summary>
    public static string? CarpetaSaves { get; set; }

    /// <summary>Compilación desde el código público (<c>--sin-partidas</c>): sin las partidas públicas de <c>saves-prueba</c>, los juegos sin partida en blanco con mochila (Colosseum y Leyendas: Arceus) se omiten con un aviso en vez de fallar.</summary>
    public static bool OmitirSinPartida { get; set; }

    /// <summary>La partida de la que sacar la mochila del juego: una EN BLANCO del Core y, si esa no tiene mochila (Colosseum, Leyendas: Arceus), la primera partida PÚBLICA de ese juego.</summary>
    public static SaveFile EnBlanco(Juego j)
    {
        try
        {
            SaveFile sav = j.Id switch
            {
                "COLO" => new SAV3Colosseum(),   // la fábrica de partidas en blanco del Core no sabe crearla por versión (y su constructor falla)
                "XD" => BlankSaveFile.Get(GameVersion.CXD),
                _ => BlankSaveFile.Get(j.Version),
            };
            _ = sav.Inventory.Pouches.Count;
            return sav;
        }
        catch (Exception e) when (e is IndexOutOfRangeException or ArgumentOutOfRangeException or InvalidOperationException) { }
        if (CarpetaSaves is not null && Directory.Exists(CarpetaSaves))
            foreach (var f in Directory.EnumerateFiles(CarpetaSaves, "*", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                SaveFile? s;
                try { s = SaveUtil.GetSaveFile(File.ReadAllBytes(f), Path.GetFileName(f)); } catch (Exception e) when (e is IOException or ArgumentException or InvalidOperationException) { continue; }
                if (s is not null && DeSav(s) == j) return s;
            }
        throw new InvalidOperationException($"No hay partida de {j.Id} (ni en blanco con mochila ni pública) de la que sacar la mochila.");
    }

    /// <summary>Todos los objetos que admite la mochila del juego (sin repetir: un objeto en varias bolsas lleva sus bolsillos juntos).</summary>
    public static IReadOnlyList<ObjetoJuego> Objetos(Juego j, GameStrings ingles)
    {
        SaveFile sav;
        try { sav = EnBlanco(j); }
        catch (InvalidOperationException e) when (OmitirSinPartida)
        {
            Console.Error.WriteLine("aviso: " + e.Message + " Sus objetos no entran en el paquete (--sin-partidas).");
            return [];
        }
        var ctx = sav.Context; var version = sav.Version;
        var nombres = ingles.GetItemStrings(ctx, version);
        var porId = new SortedDictionary<int, List<string>>();
        IReadOnlyList<InventoryPouch> bolsillos;
        try { bolsillos = sav.Inventory.Pouches; } catch (Exception e) when (e is ArgumentOutOfRangeException or IndexOutOfRangeException or InvalidOperationException) { bolsillos = []; }
        foreach (var p in bolsillos)
            foreach (var id in p.GetAllItems())
            {
                if (id == 0) continue;
                if (!porId.TryGetValue(id, out var l)) porId[id] = l = [];
                if (!l.Contains(p.Type.ToString())) l.Add(p.Type.ToString());
            }
        var lista = new List<ObjetoJuego>();
        foreach (var (id, bols) in porId)
        {
            var nombre = id < nombres.Length ? nombres[id] : "";
            lista.Add(new ObjetoJuego(j, ctx, version, id, ItemConverter.GetItemDisplay(id, ctx), nombre, string.Join("/", bols)));
        }
        return lista;
    }

    /// <summary>Cuántas partidas públicas de cada juego guardan cada objeto (id nativo) y cuántas partidas hay de cada juego.</summary>
    public static (Dictionary<string, int> Partidas, Dictionary<(string Juego, int Id), int> PorObjeto) ContarEnPartidas(string carpetaSaves)
    {
        var partidas = new Dictionary<string, int>(); var porObjeto = new Dictionary<(string, int), int>();
        if (!Directory.Exists(carpetaSaves)) return (partidas, porObjeto);
        foreach (var coleccion in Directory.EnumerateDirectories(carpetaSaves).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
        {
            var datos = Path.Combine(coleccion, "Save Data");
            if (!Directory.Exists(datos)) continue;
            foreach (var f in Directory.EnumerateFiles(datos, "*", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                SaveFile? sav;
                try { sav = SaveUtil.GetSaveFile(File.ReadAllBytes(f), Path.GetFileName(f)); } catch (Exception e) when (e is IOException or ArgumentException or InvalidOperationException) { continue; }
                if (sav is null || DeSav(sav) is not { } juego) continue;
                partidas[juego.Id] = partidas.GetValueOrDefault(juego.Id) + 1;
                IReadOnlyList<InventoryPouch> bolsillos;
                try { bolsillos = sav.Inventory.Pouches; } catch (Exception e) when (e is ArgumentOutOfRangeException or IndexOutOfRangeException or InvalidOperationException) { continue; }
                var vistos = new HashSet<int>();
                foreach (var p in bolsillos) foreach (var it in p.Items) if (it.Index > 0 && it.Count > 0) vistos.Add(it.Index);
                foreach (var id in vistos) porObjeto[(juego.Id, id)] = porObjeto.GetValueOrDefault((juego.Id, id)) + 1;
            }
        }
        return (partidas, porObjeto);
    }
}
