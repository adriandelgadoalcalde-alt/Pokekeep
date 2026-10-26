// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen6y7;

/// <summary>
/// PK-SAV-135 · <c>SAV_Pokebean</c>, las Pokéhabas de Sol/Luna y Ultrasol/Ultraluna: la cantidad (0 a 255) de cada una de las 15 (7 colores lisos, 7 con dibujos y la arcoíris). «Todos» (<c>FillBeans</c>, 255 de cada) y
/// «Ninguno» (<c>ClearBeans</c>) cambian las cantidades del formulario y se guardan con «Guardar»: aqui son acciones de la tabla sobre el borrador. Los nombres los da el Core en inglés
/// (<c>GetBeanIndexNames</c>: «Red Bean»); PKHeX no los traduce, asi que se traducen aqui con el mismo orden (divergencia de idioma declarada en el ADR 0064).
/// </summary>
internal static class Pokehabas
{
    public const string Form = "SAV_Pokebean";
    public static readonly string[] Censo = ["PK-SAV-135"];

    private static string L(string control) => EtiquetasLang.Requerir(Form, control);
    private static Span<byte> Habas(ContextoP c) => ((SAV7)c.Sav).ResortSave.GetBeans();

    private static string[] Colores => IdiomaInterfaz.Actual.Codigo == "en" ? ["Red", "Blue", "Light blue", "Green", "Yellow", "Purple", "Orange"] : ["roja", "azul", "celeste", "verde", "amarilla", "morada", "naranja"];

    /// <summary>Nombre en castellano de la haba <paramref name="i"/>, en el orden del Core (lisas, con dibujos, arcoíris).</summary>
    internal static string Nombre(int i)
        => i < Colores.Length ? IdiomaInterfaz.F("Pokehabas.Pokehaba", Colores[i]) : i < Colores.Length * 2 ? IdiomaInterfaz.F("Pokehabas.PokehabaConDibujos", Colores[i - Colores.Length]) : IdiomaInterfaz.T("Pokehabas.PokehabaArcoiris");

    /// <summary>Predicado barato de <see cref="Crear"/>: el editor existe para este guardado.</summary>
    public static bool Aplica(SaveFile sav) => sav is SAV7;

    public static EditorDefP? Crear(SaveFile sav)
    {
        if (sav is not SAV7) return null;
        var tabla = new TablaDefP
        {
            Id = "habas", Marcador = IdiomaInterfaz.T("Pokehabas.BuscarPokehaba"), SinNumero = true,
            Filas = _ => [.. Enumerable.Range(0, ResortSave7.BEANS_MAX).Select(i => new FilaTablaP
            {
                Id = i, Nombre = Nombre(i), Min = 0, Max = byte.MaxValue,
                Leer = x => (long)Habas(x)[i],
                Escribir = (x, v) => Habas(x)[i] = (byte)Math.Min(byte.MaxValue, (long)v),
            })],
            Acciones =
            [
                new AccionTablaP("todas", L("B_All"), (_, filas) => [.. filas.Select(f => (f, (object)255L))], IdiomaInterfaz.T("Pokehabas.PonePokehabas")),
                new AccionTablaP("ninguna", L("B_None"), (_, filas) => [.. filas.Select(f => (f, (object)0L))], IdiomaInterfaz.T("Pokehabas.PonePokehabas2")),
            ],
        };
        return new EditorDefP("habichuelas", IdiomaInterfaz.T("Partida.Especifico.habichuelas.Titulo"), [new PestanaP("habas", EtiquetasLang.RequerirFormulario(Form), [], Tabla: tabla)], Censo, IdiomaInterfaz.T("Partida.Especifico.habichuelas.Descripcion"));
    }
}
