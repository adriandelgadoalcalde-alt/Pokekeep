// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Sprites;

namespace Pokekeep.Mobile.Ui.Cajas;

/// <summary>
/// Gritos de una casilla. Primero la carpeta del usuario con las reglas de CryPlayer de PKHeX (nombre de forma sin sexo y alternativa por número de especie, <c>.wav</c>); si no hay, los que trae la app
/// (<see cref="PaqueteGritos"/>, X11): la forma exacta y, si PokeAPI no le da grito propio, el de la especie. Las partidas de Gen. 1-5 usan antes el grito clásico (los de aquellas consolas, que PokeAPI/cries
/// trae para las 649 primeras especies y sus formas); el resto, el actual. Si no hay clásico, el actual.
/// </summary>
public static class ModeloGritos
{
    public static string Nombre(ISpeciesForm pk, EntityContext contexto)
        => pk is { Species: (ushort)Species.Urshifu, Form: 1 } ? "892-1"
            : SpriteName.GetResourceStringSprite(pk.Species, pk.Form, 0, 0, contexto, false, false).Replace('_', '-')[1..];

    public static string? Ruta(ISpeciesForm pk, EntityContext contexto, string carpeta)
    {
        if (pk.Species == 0) return null;
        var forma = Path.Combine(carpeta, Nombre(pk, contexto) + ".wav");
        if (File.Exists(forma)) return forma;
        var especie = Path.Combine(carpeta, pk.Species + ".wav");
        return File.Exists(especie) ? especie : null;
    }

    /// <summary>Última generación cuyas partidas suenan con los gritos clásicos (Gen. 1-5: los de Game Boy, GBA y DS, con el mismo estilo que los sprites de aquellas partidas).</summary>
    public const int UltimaGeneracionClasica = 5;

    public static bool Clasico(PKM pk) => pk.Format <= UltimaGeneracionClasica;

    public static string Clave(string estilo, int especie, int forma) => forma == 0 ? $"{estilo}/{especie}" : $"{estilo}/{especie}-{forma}";

    /// <summary>
    /// Las claves del paquete que se prueban, en orden. Partidas modernas: actual de la forma y de la especie. Gen. 1-5: clásico de la forma, actual de la forma, clásico de la especie, actual de la especie
    /// (un Deoxys de Gen. 3 suena a Deoxys antiguo y una forma o especie sin grito clásico no se queda muda).
    /// </summary>
    public static IEnumerable<string> Claves(ISpeciesForm pk, EntityContext contexto, bool clasico)
    {
        if (pk.Species == 0) yield break;
        int forma = pk.Form;
        if (pk.Species == (ushort)Species.Pikachu && contexto == EntityContext.Gen6) forma = 0;   // las formas 1-5 de Gen. 6 son los trajes de Cosplay, que no tienen grito propio (en Gen. 7+ son las gorras)
        if (!clasico)
        {
            // Partidas modernas: el grito actual, de la forma y luego de la especie (todas las especies 1-1025 lo tienen). El clásico no se usa nunca aquí.
            if (forma != 0) yield return Clave("latest", pk.Species, forma);
            yield return Clave("latest", pk.Species, 0);
            yield break;
        }
        // Partidas de Gen. 1-5: el clásico de la forma, el actual de la forma, el clásico de la especie y el actual de la especie.
        if (forma != 0) { yield return Clave("legacy", pk.Species, forma); yield return Clave("latest", pk.Species, forma); }
        yield return Clave("legacy", pk.Species, 0);
        yield return Clave("latest", pk.Species, 0);
    }

    /// <summary>La clave del paquete que suena para esta casilla, o null si el paquete no tiene ninguna (p. ej. una especie que aún no existe).</summary>
    public static string? ClaveDePaquete(ISpeciesForm pk, EntityContext contexto, bool clasico, PaqueteGritos paquete)
        => Claves(pk, contexto, clasico).FirstOrDefault(paquete.Contiene);

    /// <summary>El grito que suena, ya como archivo: la carpeta del usuario si trae uno y, si no, el de serie sacado del paquete. Null si no hay ninguno (silencio, como PKHeX).</summary>
    public static string? RutaFinal(PKM pk, string carpetaUsuario, PaqueteGritos? paquete)
    {
        if (Ruta(pk, pk.Context, carpetaUsuario) is { } propia) return propia;
        if (paquete is null) return null;
        return ClaveDePaquete(pk, pk.Context, Clasico(pk), paquete) is { } clave ? paquete.Extraer(clave) : null;
    }
}
