// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace Pokekeep.Mobile.Ui.Ajustes.Pkhex;

/// <summary>Quién usa un ajuste: lo declara el bloque que cablea el consumidor, con una frase que dice DÓNDE se nota (la prueba del bloque demuestra que se nota).</summary>
public sealed record ConsumidorPkhex(string Ruta, string Quien, string Bloque)
{
    /// <summary>Una ruta terminada en <c>.*</c> cubre todo el subárbol (<c>SlotWrite.*</c>); sin ella, solo esa ruta.</summary>
    public bool Cubre(string ruta) => Ruta.EndsWith(".*", StringComparison.Ordinal)
        ? ruta.StartsWith(Ruta[..^1], StringComparison.Ordinal) || ruta == Ruta[..^2]
        : ruta == Ruta;
}

/// <summary>
/// REGLA DE LA F10: <b>ningún ajuste que no haga nada</b>. Un ajuste de PKHeX solo sale en «Más &gt; Ajustes» si un bloque ha declarado aquí quién lo consume y una prueba demuestra que cambiarlo cambia algo.
/// Un bloque declara los suyos en su propio fichero parcial (<c>ConsumidoresPkhex.B10_N.cs</c>, método <c>DeclararB10_N</c>), sin tocar este: se declaran juntos con el cambio que los cablea.
/// <c>AjustesPkhexTests</c> comprueba contra el censo que toda fila <c>hecha</c> tiene consumidor y que todo consumidor declarado corresponde a una ruta del registro.
/// </summary>
public static partial class ConsumidoresPkhex
{
    private static readonly Lazy<IReadOnlyList<ConsumidorPkhex>> _todos = new(Reunir);

    public static IReadOnlyList<ConsumidorPkhex> Todos => _todos.Value;

    private static IReadOnlyList<ConsumidorPkhex> Reunir()
    {
        var lista = new List<ConsumidorPkhex>();
        Action<string, string> Declarar(string bloque) => (ruta, quien) =>
        {
            if (RegistroAjustesPkhex.Buscar(ruta.EndsWith(".*", StringComparison.Ordinal) ? ruta[..^2] : ruta) is null) throw new InvalidOperationException($"El consumidor declara «{ruta}», que no es un ajuste de PKHeX.");
            lista.Add(new ConsumidorPkhex(ruta, quien, bloque));
        };
        DeclararPiloto(Declarar("10-0"));
        DeclararB10_1(Declarar("10-1")); DeclararB10_2(Declarar("10-2")); DeclararB10_3(Declarar("10-3")); DeclararB10_4(Declarar("10-4")); DeclararB10_5(Declarar("10-5"));
        DeclararB10_6(Declarar("10-6")); DeclararB10_7(Declarar("10-7")); DeclararB10_8(Declarar("10-8")); DeclararB10_9(Declarar("10-9")); DeclararB10_10(Declarar("10-10"));
        DeclararB10_11(Declarar("10-11")); DeclararB10_12(Declarar("10-12")); DeclararB10_13(Declarar("10-13"));
        return lista;
    }

    /// <summary>Los consumidores de una ruta (vacío = nadie la usa todavía: no se enseña).</summary>
    public static IEnumerable<ConsumidorPkhex> De(string ruta) => Todos.Where(c => c.Cubre(ruta));

    /// <summary>Una hoja sale en la pantalla si alguien la consume; un contenedor, si alguna de sus hojas sale.</summary>
    public static bool EsVisible(NodoAjustePkhex n)
    {
        if (n.Oculto || n.Grupo == RegistroAjustesPkhex.GrupoInterno) return false;
        if (n.EsHoja) return De(n.Ruta).Any();
        return RegistroAjustesPkhex.Hojas.Any(h => h.Ruta.StartsWith(n.Ruta + ".", StringComparison.Ordinal) && EsVisible(h));
    }

    // Piloto (F10-0) y un método parcial por bloque: sin implementación desaparecen al compilar.
    static partial void DeclararPiloto(Action<string, string> d);
    static partial void DeclararB10_1(Action<string, string> d);
    static partial void DeclararB10_2(Action<string, string> d);
    static partial void DeclararB10_3(Action<string, string> d);
    static partial void DeclararB10_4(Action<string, string> d);
    static partial void DeclararB10_5(Action<string, string> d);
    static partial void DeclararB10_6(Action<string, string> d);
    static partial void DeclararB10_7(Action<string, string> d);
    static partial void DeclararB10_8(Action<string, string> d);
    static partial void DeclararB10_9(Action<string, string> d);
    static partial void DeclararB10_10(Action<string, string> d);
    static partial void DeclararB10_11(Action<string, string> d);
    static partial void DeclararB10_12(Action<string, string> d);
    static partial void DeclararB10_13(Action<string, string> d);
}
