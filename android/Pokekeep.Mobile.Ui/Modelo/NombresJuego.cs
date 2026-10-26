// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Ajustes.Pkhex;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Modelo;

/// <summary>
/// Nombre OFICIAL en castellano del juego de una partida (QA uso 6: «en la cabecera estaría bien que te dijera qué juego de Pokémon es, porque en generaciones más antiguas […] casi siempre tiene el nombre puesto en el
/// guardado, pero en los modernos los nombres son main, savedata, etc.»). El texto sale de las listas del Core (<c>gamelist</c>: «Espada», «Escarlata», «Diamante Brillante»...) con el título completo delante
/// («Pokémon Espada»). Si el guardado no distingue la pareja (el Core lo deja como «SWSH», «DP», «RS»...), el nombre es el conjunto con las dos ediciones («Pokémon Espada/Escudo»).
/// </summary>
public static class NombresJuego
{
    // Parejas que el Core usa como versión genérica cuando el guardado no dice cuál de las dos es (GameVersion.SWSH, DP, RS...): los ids de las ediciones sueltas, en el orden en que se nombran.
    private static readonly Dictionary<GameVersion, GameVersion[]> Conjuntos = new()
    {
        [GameVersion.RB] = [GameVersion.RD, GameVersion.BU],
        [GameVersion.RBY] = [GameVersion.RD, GameVersion.BU, GameVersion.YW],
        [GameVersion.GS] = [GameVersion.GD, GameVersion.SI],
        [GameVersion.GSC] = [GameVersion.GD, GameVersion.SI, GameVersion.C],
        [GameVersion.RS] = [GameVersion.R, GameVersion.S],
        [GameVersion.RSE] = [GameVersion.R, GameVersion.S, GameVersion.E],
        [GameVersion.FRLG] = [GameVersion.FR, GameVersion.LG],
        [GameVersion.DP] = [GameVersion.D, GameVersion.P],
        [GameVersion.DPPt] = [GameVersion.D, GameVersion.P, GameVersion.Pt],
        [GameVersion.HGSS] = [GameVersion.HG, GameVersion.SS],
        [GameVersion.BW] = [GameVersion.B, GameVersion.W],
        [GameVersion.B2W2] = [GameVersion.B2, GameVersion.W2],
        [GameVersion.XY] = [GameVersion.X, GameVersion.Y],
        [GameVersion.ORAS] = [GameVersion.OR, GameVersion.AS],
        [GameVersion.SM] = [GameVersion.SN, GameVersion.MN],
        [GameVersion.USUM] = [GameVersion.US, GameVersion.UM],
        [GameVersion.GG] = [GameVersion.GP, GameVersion.GE],
        [GameVersion.SWSH] = [GameVersion.SW, GameVersion.SH],
        [GameVersion.BDSP] = [GameVersion.BD, GameVersion.SP],
        [GameVersion.SV] = [GameVersion.SL, GameVersion.VL],
    };

    // Los que el Core no trae en la lista de ediciones (ni suelta ni en pareja) y su nombre oficial.
    private static Dictionary<GameVersion, string> Propios => new()
    {
        [GameVersion.COLO] = "Pokémon Colosseum",
        [GameVersion.XD] = "Pokémon XD: Tempestad Oscura",
        [GameVersion.CXD] = "Pokémon Colosseum / XD",
        [GameVersion.RSBOX] = IdiomaInterfaz.T("NombresJuego.PokemonBoxRubiZafiro"),
        [GameVersion.BATREV] = "Pokémon Battle Revolution",
        [GameVersion.Stadium] = "Pokémon Stadium",
        [GameVersion.StadiumJ] = IdiomaInterfaz.T("NombresJuego.PokemonStadiumJapon"),
        [GameVersion.Stadium2] = "Pokémon Stadium 2",
        [GameVersion.PLA] = IdiomaInterfaz.T("NombresJuego.LeyendasPokemonArceus"),
        [GameVersion.ZA] = IdiomaInterfaz.T("NombresJuego.LeyendasPokemonZ"),
        [GameVersion.GP] = "Pokémon: Let's Go, Pikachu!",
        [GameVersion.GE] = "Pokémon: Let's Go, Eevee!",
        [GameVersion.GO] = IdiomaInterfaz.T("Libreria.Origen.GO"),
    };

    private static bool EnCastellano => ProgramaPkhex.Idioma == "es";

    /// <summary>Nombre oficial («Pokémon Espada») de la versión del guardado. Con los datos en otro idioma, el nombre que da el Core en ese idioma.</summary>
    public static string Oficial(GameVersion version)
    {
        if (EnCastellano && Propios.TryGetValue(version, out var propio)) return propio;
        if (Conjuntos.TryGetValue(version, out var partes))
            return Prefijo + string.Join("/", partes.Select(p => SinEtiqueta(Suelto(p))));   // «Pokémon Diamante/Perla»; en un conjunto sobra la etiqueta de región del Core («Azul [JP]» -> «Azul»)
        return Prefijo + Suelto(version);
    }

    /// <summary>
    /// Nombre corto en español para etiquetas pequeñas (subtítulo de la ficha): el oficial de <see cref="Oficial(GameVersion)"/> sin el «Pokémon » de delante («Platino», «Rojo/Azul», «Oro HeartGold»). Misma fuente que la cabecera de X44,
    /// ninguna tabla propia; «Leyendas Pokémon: Arceus» y «Pokémon: Let's Go...» se quedan como están.
    /// </summary>
    public static string Corto(GameVersion version)
    {
        var o = Oficial(version);
        const string p = "Pokémon ";
        return EnCastellano && o.StartsWith(p, StringComparison.Ordinal) && o.Length > p.Length ? o[p.Length..] : o;
    }

    private static string Prefijo => EnCastellano ? "Pokémon " : string.Empty;

    /// <summary>Nombre oficial a partir del texto corto que el Core da a una edición («Espada», o «Sword» en una lista de recientes antigua), o el propio texto si no se reconoce.</summary>
    public static string Oficial(string texto)
    {
        if (string.IsNullOrEmpty(texto)) return texto;
        if (EnCastellano && (texto.StartsWith("Pokémon", StringComparison.Ordinal) || texto.StartsWith("Leyendas Pokémon", StringComparison.Ordinal))) return texto;
        foreach (var lista in new[] { ProgramaPkhex.Textos.gamelist, GameInfo.GetStrings("en").gamelist })
            for (var i = 1; i < lista.Length; i++)
                if (lista[i].Length != 0 && lista[i] == texto && Enum.IsDefined((GameVersion)i)) return Oficial((GameVersion)i);
        // Sin texto de edición en la lista (el Core deja el nombre de la enumeración: «SWSH», «COLO»...): se resuelve por la versión.
        return Enum.TryParse<GameVersion>(texto, out var v) && Enum.IsDefined(v) && !char.IsDigit(texto[0]) ? Oficial(v) : texto;
    }

    private static string SinEtiqueta(string t) => t.IndexOf(" [", StringComparison.Ordinal) is var i and > 0 ? t[..i] : t;

    private static string Suelto(GameVersion v)
    {
        var lista = ProgramaPkhex.Textos.gamelist;
        var i = (int)v;
        return i > 0 && i < lista.Length && lista[i].Length != 0 ? lista[i] : v.ToString();
    }
}
