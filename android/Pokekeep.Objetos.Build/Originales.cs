// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Text.Json;
using Pokekeep.Sprites;

namespace Pokekeep.Objetos.Build;

/// <summary>Iconos extraídos por herramientas locales (DS público o volcado privado).
/// El índice solo contiene identificadores de juego/objeto y huellas de iconos, nunca nombres de volcados ni claves.
/// Solo es dependencia de CONSTRUCCIÓN: el producto consume el paquete consolidado normal.</summary>
public static class Originales
{
    internal static List<HallazgoJuego> Buscar(string carpetaSprites, IReadOnlyList<(Juego J, ObjetoJuego O)> universo)
    {
        var dir = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(carpetaSprites).TrimEnd(Path.DirectorySeparatorChar))!, "objetos-originales");
        var indice = Path.Combine(dir, "indice.json");
        if (!File.Exists(indice)) return [];
        using var doc = JsonDocument.Parse(File.ReadAllBytes(indice));
        if (doc.RootElement.GetProperty("version").GetInt32() != 1) throw new InvalidDataException("Índice de iconos originales incompatible.");
        var porId = universo.ToDictionary(x => (x.J.Id, x.O.IdNativo));
        var vistos = new HashSet<(string, int)>(); var salida = new List<HallazgoJuego>();
        foreach (var e in doc.RootElement.GetProperty("recursos").EnumerateArray())
        {
            var grupo = e.GetProperty("juego").GetString()!; var id = e.GetProperty("id").GetInt32();
            if (!porId.TryGetValue((grupo, id), out var entrada) || !vistos.Add((grupo, id))) throw new InvalidDataException("Identificador de icono original inválido o repetido.");
            var nombre = $"{grupo}-{id:0000}.png";
            if (e.GetProperty("archivo").GetString() != nombre) throw new InvalidDataException("Nombre de icono no canónico.");
            var png = File.ReadAllBytes(Path.Combine(dir, nombre)); var sha = Fuente.Sha(png);
            if (sha != e.GetProperty("sha256").GetString()) throw new InvalidDataException("Huella de icono original alterada.");
            var img = PngLite.Decodificar(png);
            if (Fuente.Sha(img.Pixels) != e.GetProperty("pixeles_sha256").GetString()
                || img.Width != e.GetProperty("ancho").GetInt32() || img.Height != e.GetProperty("alto").GetInt32()) throw new InvalidDataException("Píxeles del icono original alterados.");
            var fuente = e.GetProperty("fuente").GetString()!;
            if (fuente is not ("pret/pokeplatinum" or "pret/pokeheartgold" or "pret/pokediamond" or "volcado del propietario")) throw new InvalidDataException("Fuente de icono original desconocida.");
            var rev = e.GetProperty("revision").GetString()!;
            var evidencia = e.GetProperty("evidencia").GetString()!;
            var url = e.GetProperty("url").GetString()!;
            if (fuente == "volcado del propietario")
            {
                // Aunque un índice externo estuviera manipulado, jamás propaga rutas/nombres/claves a informes.
                rev = "privado-local"; url = "";
                evidencia = $"volcado del propietario; juego {grupo}; id nativo {id}; SHA-256 del icono {sha}";
            }
            salida.Add(new(entrada.J, entrada.O, nombre, url, "original", fuente, rev, png, evidencia));
        }
        return salida;
    }
}
