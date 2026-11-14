// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace AutoLegalidad.Desafios;

/// <summary>
/// Textos de la biblioteca en es-ES y en, desde el recurso incrustado <c>textos-desafios.tsv</c> (clave, español, inglés; separados por tabulador).
/// En español de España los textos que hablan a los jugadores usan «vosotros». Una clave que falta es un error de programación: lanza, nunca devuelve la clave a la interfaz.
/// </summary>
public static class Textos
{
    private static readonly Lazy<IReadOnlyDictionary<string, TextoDoble>> Tabla = new(Cargar);

    public static TextoDoble Doble(string clave) =>
        Tabla.Value.TryGetValue(clave, out var t) ? t : throw new KeyNotFoundException($"Falta el texto «{clave}» en textos-desafios.tsv.");

    /// <summary>Nombre del jefe elegido («Jefe de plata», «Jefe de oro», «Jefe repetido»).</summary>
    public static TextoDoble Jefe(JefeDesafio jefe) => Doble("jefe." + jefe.ToString().ToLowerInvariant());

    public static bool Existe(string clave) => Tabla.Value.ContainsKey(clave);

    /// <summary>Todas las claves del recurso (para las pruebas de cobertura de textos).</summary>
    public static IReadOnlyCollection<string> Claves => [.. Tabla.Value.Keys];

    private static Dictionary<string, TextoDoble> Cargar()
    {
        using var s = typeof(Textos).Assembly.GetManifestResourceStream("textos-desafios.tsv") ?? throw new InvalidOperationException("Falta el recurso textos-desafios.tsv.");
        using var r = new StreamReader(s);
        var d = new Dictionary<string, TextoDoble>(StringComparer.Ordinal);
        string? linea;
        while ((linea = r.ReadLine()) is not null)
        {
            if (linea.Length == 0 || linea[0] == '#')
                continue;
            var p = linea.Split('\t');
            if (p.Length != 3 || p[0].Length == 0 || p[1].Length == 0 || p[2].Length == 0)
                throw new InvalidDataException($"Línea mal formada en textos-desafios.tsv: «{linea}».");
            d.Add(p[0], new TextoDoble(p[1], p[2]));
        }
        return d;
    }
}
