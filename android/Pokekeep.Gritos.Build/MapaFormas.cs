// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Reflection;
using PKHeX.Core;

namespace Pokekeep.Gritos.Build;

/// <summary>Una forma de PKHeX con el pokémon de PokeAPI que le corresponde (null si PokeAPI no le da grito propio) y por qué.</summary>
public sealed record AsignacionForma(int Especie, int Forma, string NombrePkhx, Fuente.FilaPokemon? Pokemon, string Motivo);

/// <summary>
/// Traduce (especie, forma) de PKHeX a un id de PokeAPI. Primero las correspondencias manuales (<c>formas-manuales.tsv</c>, contrastadas con FormConverter del Core fijado y pokemon.csv); si no hay,
/// el nombre de la forma de PKHeX (inglés, <c>FormConverter.GetFormList</c>) contra el sufijo del identificador de PokeAPI, sin signos. Nunca se deduce por igualdad numérica entre los dos proyectos.
/// </summary>
public static class MapaFormas
{
    public const int MaxEspecie = 1025;

    public static string Normalizar(string s) => new([.. s.ToLowerInvariant().Where(char.IsLetterOrDigit)]);

    public static IReadOnlyDictionary<(int, int), string> LeerManuales()
    {
        using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("formas-manuales.tsv") ?? throw new InvalidDataException("Falta formas-manuales.tsv.");
        using var r = new StreamReader(s);
        var d = new Dictionary<(int, int), string>();
        while (r.ReadLine() is { } l)
        {
            if (l.Length == 0 || l[0] == '#' || l.StartsWith("especie\t", StringComparison.Ordinal)) continue;
            var v = l.Split('\t');
            d[(int.Parse(v[0]), int.Parse(v[1]))] = v[2];
        }
        return d;
    }

    /// <summary>Las formas (≥1) que PKHeX conoce de cada especie y su nombre inglés: el primer contexto (de más nuevo a más antiguo) que da nombre a un índice lo fija.</summary>
    public static IReadOnlyDictionary<(int, int), string> FormasPkhex()
    {
        var s = GameInfo.GetStrings("en");
        var contextos = new[] { EntityContext.Gen9a, EntityContext.Gen9, EntityContext.Gen8a, EntityContext.Gen8b, EntityContext.Gen8, EntityContext.Gen7b, EntityContext.Gen7, EntityContext.Gen6, EntityContext.Gen5, EntityContext.Gen4, EntityContext.Gen3 };
        var d = new SortedDictionary<(int, int), string>();
        for (ushort sp = 1; sp <= MaxEspecie; sp++)
            foreach (var c in contextos)
            {
                var lista = FormConverter.GetFormList(sp, s.types, s.forms, GameInfo.GenderSymbolUnicode, c);
                for (var f = 1; f < lista.Length; f++) d.TryAdd((sp, f), lista[f]);
            }
        return d;
    }

    public static (IReadOnlyList<AsignacionForma> Formas, IReadOnlyList<Fuente.FilaPokemon> PokeApiSinForma) Asignar(IReadOnlyList<Fuente.FilaPokemon> pokemon, IReadOnlyList<Fuente.FilaForma> formas)
    {
        var porIdentificador = pokemon.ToDictionary(p => p.Identificador, StringComparer.Ordinal);
        var identEspecie = pokemon.Where(p => p.Predeterminada).ToDictionary(p => p.Especie, p => p.Identificador);
        var formaDe = formas.GroupBy(f => f.PokemonId).ToDictionary(g => g.Key, g => g.Select(x => x.FormaId).Where(x => x.Length > 0).ToList());
        var manuales = LeerManuales();
        var resultado = new List<AsignacionForma>();
        var usados = new HashSet<int>();
        foreach (var ((sp, f), nombre) in FormasPkhex())
        {
            if (manuales.TryGetValue((sp, f), out var ident))
            {
                if (ident == "-") { resultado.Add(new(sp, f, nombre, null, "manual: comparte el grito de la especie")); continue; }
                var p = porIdentificador.GetValueOrDefault(ident) ?? throw new InvalidDataException($"formas-manuales.tsv: «{ident}» no existe en pokemon.csv.");
                if (p.Especie != sp) throw new InvalidDataException($"formas-manuales.tsv: «{ident}» es de la especie {p.Especie}, no de la {sp}.");
                usados.Add(p.Id); resultado.Add(new(sp, f, nombre, p, "manual")); continue;
            }
            var norm = Normalizar(nombre);
            var candidatos = pokemon.Where(p => p.Especie == sp && !p.Predeterminada && !usados.Contains(p.Id)).ToList();
            var coinciden = candidatos.Where(p => Sufijos(p, identEspecie[sp], formaDe).Contains(norm)).ToList();
            if (norm.Length > 0 && coinciden.Count == 1) { usados.Add(coinciden[0].Id); resultado.Add(new(sp, f, nombre, coinciden[0], "nombre")); }
            else resultado.Add(new(sp, f, nombre, null, coinciden.Count > 1 ? "ambigua" : "PokeAPI no distingue esta forma"));
        }
        var sinForma = pokemon.Where(p => !p.Predeterminada && p.Especie <= MaxEspecie && !usados.Contains(p.Id)).ToList();
        return (resultado, sinForma);
    }

    private static HashSet<string> Sufijos(Fuente.FilaPokemon p, string identEspecie, Dictionary<int, List<string>> formaDe)
    {
        var h = new HashSet<string>(StringComparer.Ordinal);
        if (p.Identificador.StartsWith(identEspecie + "-", StringComparison.Ordinal)) h.Add(Normalizar(p.Identificador[(identEspecie.Length + 1)..]));
        if (formaDe.TryGetValue(p.Id, out var l)) foreach (var x in l) h.Add(Normalizar(x));
        return h;
    }
}
