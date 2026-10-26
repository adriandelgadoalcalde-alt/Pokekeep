// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Text;
using Pokekeep.Sprites;

namespace Pokekeep.Gritos.Build;

/// <summary>Resultado de generar: lo que se escribe y las cifras.</summary>
public sealed record ResultadoGritos(byte[] Paquete, byte[] Manifiesto, string Procedencia, string SinGrito, int Claves, int Archivos, IReadOnlyList<AsignacionForma> Formas, IReadOnlyList<Fuente.FilaPokemon> PokeApiSinForma);

/// <summary>
/// Paquete de gritos de serie. Claves: <c>latest/25</c>, <c>latest/6-1</c> (especie y especie-forma de PKHeX, sin sexo) y lo mismo con <c>legacy/</c> (gritos clásicos de Gen. 1-5).
/// Los .ogg son los ORIGINALES de PokeAPI/cries, byte a byte, sin recodificar. Dos ejecuciones con la misma caché dan bytes idénticos.
/// </summary>
public static class Generador
{
    public const string IdPaquete = "gritos";
    public const string ArchivoPaquete = "gritos.pkpack";

    /// <summary>«ogg», «mp3» o «wav» según los primeros bytes (PokeAPI/cries guarda 21 MP3 y 3 WAV con extensión .ogg; MediaPlayer de Android decodifica los tres por contenido); null si no es ninguno.</summary>
    public static string? Formato(ReadOnlySpan<byte> b)
        => b.Length < 64 ? null
         : b[0] == 'O' && b[1] == 'g' && b[2] == 'g' && b[3] == 'S' ? "ogg"
         : b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' && b[8] == 'W' && b[9] == 'A' && b[10] == 'V' && b[11] == 'E' ? "wav"
         : b[0] == 'I' && b[1] == 'D' && b[2] == '3' ? "mp3"
         : b[0] == 0xFF && (b[1] & 0xE0) == 0xE0 ? "mp3"
         : null;

    public static string Clave(string estilo, int especie, int forma) => forma == 0 ? $"{estilo}/{especie}" : $"{estilo}/{especie}-{forma}";

    public static async Task<ResultadoGritos> Generar(string cache, bool sinDescargas, HttpClient? http = null)
    {
        http ??= new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var csvPokemon = await Fuente.Obtener(http, Fuente.UrlCsv("pokemon"), Path.Combine(cache, "pokemon.csv"), sinDescargas) ?? throw new InvalidDataException("Falta pokemon.csv fijado.");
        var csvFormas = await Fuente.Obtener(http, Fuente.UrlCsv("pokemon_forms"), Path.Combine(cache, "pokemon_forms.csv"), sinDescargas) ?? throw new InvalidDataException("Falta pokemon_forms.csv fijado.");
        var pokemon = Fuente.LeerPokemon(csvPokemon);
        var (formas, sinForma) = MapaFormas.Asignar(pokemon, Fuente.LeerFormas(csvFormas));

        // (especie, forma) -> pokemon de PokeAPI: la especie con su forma 0 y cada forma asignada.
        var destinos = new SortedDictionary<(int Sp, int F), Fuente.FilaPokemon>();
        foreach (var p in pokemon.Where(p => p.Predeterminada && p.Especie <= MapaFormas.MaxEspecie)) destinos[(p.Especie, 0)] = p;
        foreach (var a in formas.Where(a => a.Pokemon is not null)) destinos[(a.Especie, a.Forma)] = a.Pokemon!;

        var ficheros = new Dictionary<(string Estilo, int Id), byte[]?>();
        var cola = destinos.Values.Select(p => p.Id).Distinct().SelectMany(id => Fuente.Estilos.Select(e => (e, id))).ToList();
        await Parallel.ForEachAsync(cola, new ParallelOptions { MaxDegreeOfParallelism = 6 }, async (t, _) =>
        {
            var b = await Fuente.Obtener(http, Fuente.UrlGrito(t.e, t.id), Path.Combine(cache, t.e, t.id + ".ogg"), sinDescargas);
            if (b is not null && Formato(b) is null) throw new InvalidDataException($"{t.e}/{t.id}.ogg no es Ogg, MP3 ni WAV.");
            lock (ficheros) ficheros[t] = b;
        });

        var entradas = new SortedDictionary<string, (byte[] Bytes, int Sp, int F, string Estilo, Fuente.FilaPokemon P)>(StringComparer.Ordinal);
        foreach (var ((sp, f), p) in destinos)
            foreach (var estilo in Fuente.Estilos)
                if (ficheros.GetValueOrDefault((estilo, p.Id)) is { } b) entradas[Clave(estilo, sp, f)] = (b, sp, f, estilo, p);

        // Paquete: un .ogg por contenido distinto; las claves que comparten contenido apuntan al mismo sitio.
        using var paquete = new MemoryStream();
        var sitios = new Dictionary<string, (long Desp, int Len)>(StringComparer.Ordinal);
        var recursos = new Dictionary<string, EntradaRecurso>(StringComparer.Ordinal);
        var procedencia = new StringBuilder("clave\tespecie\tforma\testilo\tid_pokeapi\tidentificador\tbytes\tsha256\trevision_gritos\trevision_datos\turl\n");
        foreach (var (clave, e) in entradas)
        {
            var sha = Fuente.Sha(e.Bytes);
            if (!sitios.TryGetValue(sha, out var sitio)) { sitio = (paquete.Position, e.Bytes.Length); paquete.Write(e.Bytes); sitios[sha] = sitio; }
            recursos[clave] = new EntradaRecurso(0, sitio.Desp, sitio.Len, 0, 0);
            procedencia.Append(clave).Append('\t').Append(e.Sp).Append('\t').Append(e.F).Append('\t').Append(e.Estilo).Append('\t').Append(e.P.Id).Append('\t').Append(e.P.Identificador)
                .Append('\t').Append(Formato(e.Bytes)).Append('\t').Append(e.Bytes.Length).Append('\t').Append(sha).Append('\t').Append(Fuente.RevisionGritos).Append('\t').Append(Fuente.RevisionDatos).Append('\t').Append(Fuente.UrlGrito(e.Estilo, e.P.Id)).Append('\n');
        }
        var bytesPaquete = paquete.ToArray();
        var manifiesto = new Manifiesto
        {
            Origen = $"PokeAPI/cries@{Fuente.RevisionGritos}; PokeAPI/pokeapi pokemon.csv@{Fuente.RevisionDatos}; audio (c) The Pokemon Company",
            Paquetes = [new PaqueteInfo(IdPaquete, ArchivoPaquete, bytesPaquete.LongLength, Manifiesto.Sha256Hex(bytesPaquete), recursos.Count)],
            Recursos = recursos,
        }.Escribir();

        var sin = new StringBuilder("especie\tforma\tnombre_pkhex\tmotivo\n");
        foreach (var a in formas.Where(a => a.Pokemon is null || !entradas.ContainsKey(Clave("latest", a.Especie, a.Forma)))) sin.Append(a.Especie).Append('\t').Append(a.Forma).Append('\t').Append(a.NombrePkhx).Append('\t').Append(a.Motivo).Append('\n');
        return new ResultadoGritos(bytesPaquete, manifiesto, procedencia.ToString(), sin.ToString(), entradas.Count, sitios.Count, formas, sinForma);
    }

    public static void Escribir(ResultadoGritos r, string salida, string? tsvProcedencia, string? tsvSinGrito)
    {
        Directory.CreateDirectory(salida);
        File.WriteAllBytes(Path.Combine(salida, ArchivoPaquete), r.Paquete);
        File.WriteAllBytes(Path.Combine(salida, "manifest.json"), r.Manifiesto);
        var sinBom = new UTF8Encoding(false);
        if (tsvProcedencia is not null) File.WriteAllText(tsvProcedencia, r.Procedencia, sinBom);
        if (tsvSinGrito is not null) File.WriteAllText(tsvSinGrito, r.SinGrito, sinBom);
    }
}
