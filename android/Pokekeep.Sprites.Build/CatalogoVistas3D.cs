// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Text;
using System.Text.Json;
using PKHeX.Core;
using Pokekeep.Sprites;

/// <summary>
/// X71: el catalogo ESPERADO de las vistas previas de Gen. 6 a 9, calculado del Core de PKHeX (tablas personales de cada juego): las especies y formas que se pueden guardar en una caja de ese juego (sin
/// las que solo existen en combate ni las de Totem), mas la lista de las que el propio PKHeX distingue por sexo. Sirve para medir la cobertura de los paquetes (cuentas y huecos con su motivo) y para las pruebas.
/// </summary>
internal static class CatalogoVistas3D
{
    /// <summary>Juego de sprites -> (tabla personal del Core, formato de la generacion).</summary>
    internal static (IPersonalTable Tabla, byte Formato) Tabla(string juego) => juego switch
    {
        SpritesDeJuego.JuegoY => (PersonalTable.XY, 6),
        SpritesDeJuego.JuegoOr => (PersonalTable.AO, 6),
        SpritesDeJuego.JuegoUs => (PersonalTable.USUM, 7),
        SpritesDeJuego.JuegoSwsh => (PersonalTable.SWSH, 8),
        SpritesDeJuego.JuegoBdsp => (PersonalTable.BDSP, 8),
        SpritesDeJuego.JuegoPla => (PersonalTable.LA, 8),
        SpritesDeJuego.JuegoSv => (PersonalTable.SV, 9),
        SpritesDeJuego.JuegoZa => (PersonalTable.ZA, 9),
        _ => throw new ArgumentException("Juego sin vista 3D: " + juego),
    };

    /// <summary>Pares (especie, forma) que se pueden guardar en una caja de ese juego.</summary>
    internal static List<(int Especie, int Forma)> Esperado(string juego)
    {
        var (tabla, formato) = Tabla(juego);
        var lista = new List<(int, int)>();
        for (ushort sp = 1; sp <= tabla.MaxSpeciesID; sp++)
        {
            if (!tabla.IsSpeciesInGame(sp)) continue;
            if (!tabla.IsPresentInGame(sp, 0)) continue;
            for (byte f = 0; f < 40; f++)
            {
                if (!tabla.IsPresentInGame(sp, f)) continue;
                if (FormInfo.IsBattleOnlyForm(sp, f, formato) || (f != 0 && FormInfo.IsTotemForm(sp, f))) continue;
                lista.Add((sp, f));
            }
        }
        return lista;
    }

    public static int Ejecutar(string salida)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true }))
        {
            w.WriteStartObject();
            foreach (var juego in SpritesDeJuego.JuegosVista3D)
            {
                var esperado = Esperado(juego);
                w.WriteStartObject(juego);
                w.WriteNumber("total", esperado.Count);
                w.WriteStartArray("pares");
                foreach (var (sp, f) in esperado) { w.WriteStartArray(); w.WriteNumberValue(sp); w.WriteNumberValue(f); w.WriteEndArray(); }
                w.WriteEndArray();
                w.WriteEndObject();
                Console.WriteLine($"{juego,-5} {esperado.Count,5} especies/formas guardables");
            }
            w.WriteEndObject();
        }
        File.WriteAllBytes(salida, ms.ToArray());
        return 0;
    }
}
