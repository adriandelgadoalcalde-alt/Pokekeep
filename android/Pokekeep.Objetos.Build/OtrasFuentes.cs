// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace Pokekeep.Objetos.Build;

/// <summary>Una imagen de arte PROPIO de un juego, sacada de una fuente verificada que numera por id nativo, con su prueba.</summary>
internal sealed record HallazgoJuego(Juego J, ObjetoJuego O, string Archivo, string Url, string Carpeta, string Fuente, string Revision, byte[] Png, string Evidencia);

/// <summary>
/// Fuentes de arte original por juego, fuera de PokeAPI y PokéSprite. Hoy: Escarlata/Púrpura, de <c>pokesprites/all</c> (rips de los iconos HD del propio juego, un PNG de 160x160 por id nativo: <c>items/0001.png</c>).
/// El repositorio no tiene nombres ni licencia, así que la identidad de cada imagen se prueba por el id (alineado en las 96 muestras de PKHeX comparadas a ojo, de los ids 3 a 2344) y cada imagen nueva se revisa
/// a mano con el nombre del Core antes de aceptar el paquete. Se fija por commit y SHA-256 de cada archivo.
/// </summary>
internal static class OtrasFuentes
{
    public const string FuenteSv = "pokesprites/all";
    public const string RevisionSv = "48125ae5edc35663170f46cc325a1575185727cc";
    public static string UrlSv(int idNativo) => $"https://raw.githubusercontent.com/pokesprites/all/{RevisionSv}/items/{idNativo:0000}.png";

    public static async Task<List<HallazgoJuego>> EscarlataPurpura(HttpClient http, string cache, bool sinDescargas, IReadOnlyList<(Juego J, ObjetoJuego O)> pendientes)
    {
        var lista = pendientes.Where(p => p.J.Id == "SV").ToList();
        var bajados = new Dictionary<int, byte[]?>();
        await Parallel.ForEachAsync(lista.Select(p => p.O.IdNativo).Distinct().ToList(), new ParallelOptions { MaxDegreeOfParallelism = 6 }, async (id, _) =>
        {
            var b = await Fuente.Obtener(http, UrlSv(id), Path.Combine(cache, "pokesprites-all", $"{id:0000}.png"), sinDescargas);
            if (b is not null && !Generador.EsPng(b)) throw new InvalidDataException($"pokesprites/all {id:0000}.png no es un PNG.");
            lock (bajados) bajados[id] = b;
        });
        var r = new List<HallazgoJuego>();
        foreach (var (j, o) in lista)
            if (bajados[o.IdNativo] is { } png)
                r.Add(new HallazgoJuego(j, o, $"{o.IdNativo:0000}.png", UrlSv(o.IdNativo), "items", FuenteSv, RevisionSv, png,
                    $"id nativo {o.IdNativo} = items/{o.IdNativo:0000}.png de {FuenteSv}@{RevisionSv[..8]} (icono HD de Escarlata/Púrpura numerado por id; alineación comprobada con PKHeX; nombre del Core «{o.NombreEn}» revisado a ojo)"));
        return r;
    }
}
