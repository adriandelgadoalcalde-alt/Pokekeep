// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Runtime.CompilerServices;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;
using Pokekeep.Mobile.Ui.Ajustes.Pkhex;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen1a3;

/// <summary>
/// Listas y utilidades comunes de los formularios de la 3.ª generación (bloque 8a-1). Las listas son las de <see cref="FilteredGameDataSource"/> con el guardado abierto (lo que WinForms enseña en sus
/// <c>ComboBox</c>: <c>GameInfo.FilteredSources</c>) y se construyen UNA vez por contexto: una lista de opciones se pide cada vez que se pinta un campo y construir el filtro entero cuesta demasiado.
/// </summary>
internal static class AyudasGen3
{
    private sealed class Listas
    {
        public required FilteredGameDataSource Filtro { get; init; }
        public IReadOnlyList<OpcionP>? Especies, Movimientos, Objetos;
    }

    private static readonly ConditionalWeakTable<ContextoP, Listas> Cache = new();

    private static Listas De(ContextoP c)
    {
        static Listas Crear(ContextoP x) => new() { Filtro = new FilteredGameDataSource(x.Sav, x.Datos) };
        var listas = Cache.GetValue(c, Crear);
        if (ReferenceEquals(listas.Filtro.Source, c.Datos)) return listas;
        // El mismo contexto conserva su borrador y renueva solo las listas del idioma.
        Cache.Remove(c);
        return Cache.GetValue(c, Crear);
    }

    /// <summary>Especies que el juego admite (<c>FilteredSources.Species</c>), con la casilla vacía al principio.</summary>
    public static IReadOnlyList<OpcionP> Especies(ContextoP c) { var l = De(c); return l.Especies ??= P.Lista(l.Filtro.Species); }

    /// <summary>Movimientos del formato (<c>FilteredSources.Moves</c>).</summary>
    public static IReadOnlyList<OpcionP> Movimientos(ContextoP c) { var l = De(c); return l.Movimientos ??= P.Lista(l.Filtro.Moves); }

    /// <summary>Objetos que el guardado acepta (<c>FilteredSources.Items</c>).</summary>
    public static IReadOnlyList<OpcionP> Objetos(ContextoP c)
    {
        var l = De(c);
        if (l.Objetos is not null) return l.Objetos;
        var nombres = NombresObjetos.Lista(c.Textos, c.Sav);
        return l.Objetos = [.. l.Filtro.Items.Select(i => new OpcionP(i.Value, (uint)i.Value < nombres.Length ? nombres[i.Value] : i.Text))];
    }

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<OpcionP>> GenerosPorIdioma = new Dictionary<string, IReadOnlyList<OpcionP>>
    {
        ["es"] = P.Lista(ServicioIdioma.Recursos("es-ES")["Form.Masculino"], ServicioIdioma.Recursos("es-ES")["Form.Femenino"]),
        ["en"] = P.Lista(ServicioIdioma.Recursos("en")["Form.Masculino"], ServicioIdioma.Recursos("en")["Form.Femenino"]),
    };
    public static IReadOnlyList<OpcionP> Generos => GenerosPorIdioma[ProgramaPkhex.Idioma];

    /// <summary>Texto de una etiqueta que PKHeX escribe en dos controles (nombre del campo + sufijo) cuando en el móvil cada campo necesita ir autoexplicado.</summary>
    public static string Con(string prefijo, string etiqueta) => $"{prefijo} · {etiqueta}";
}
