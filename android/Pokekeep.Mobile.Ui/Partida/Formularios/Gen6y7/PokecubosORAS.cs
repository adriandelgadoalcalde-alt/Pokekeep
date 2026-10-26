// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen6y7;

/// <summary>
/// PK-SAV-112 · <c>SAV_PokeBlockORAS</c>, los Pokécubos de Rubí Omega y Zafiro Alfa: la cantidad (0 a 999) de cada uno de los 12 colores, con los nombres del juego (<c>pokeblocks[94 + i]</c>).
/// «Todos máx.» pone las 12 cantidades a 999 (con Ctrl, a 0 en el original: aqui es otra accion) en el BORRADOR, como el formulario cambia sus controles, y se aplica con el resto.
/// «Aleatorizar bayas» (<c>BerryField.ResetAndRandomize</c>) repuebla las 90 parcelas del huerto con bayas al azar: en el formulario actua sobre la copia de trabajo y se guarda con «Guardar»; aqui es una accion
/// pendiente que se ejecuta al aplicar. Es aleatoria por naturaleza: la comparacion con WinForms deja fuera las parcelas (ver el driver).
/// </summary>
internal static class PokecubosORAS
{
    public const string Form = "SAV_PokeBlockORAS";
    public static readonly string[] Censo = ["PK-SAV-112"];

    private static Contest6 Concurso(ContextoP c) => ((SAV6AO)c.Sav).Contest;
    private static string L(string control) => EtiquetasLang.Requerir(Form, control);

    // El orden de los controles del formulario: rojo, azul, rosa, verde, amarillo, arcoiris y sus seis versiones «+».
    private static readonly string[] Etiquetas = ["L_Red", "L_Blue", "L_Pink", "L_Green", "L_Yellow", "L_Rainbow", "L_RedPlus", "L_BluePlus", "L_PinkPlus", "L_GreenPlus", "L_YellowPlus", "L_RainbowPlus"];

    /// <summary>Predicado barato de <see cref="Crear"/>: el editor existe para este guardado.</summary>
    public static bool Aplica(SaveFile sav) => sav is SAV6AO;

    public static EditorDefP? Crear(SaveFile sav)
    {
        if (sav is not SAV6AO) return null;
        // El formulario sustituye el texto de cada etiqueta por el nombre del juego (pokeblocks[94 + i]); las etiquetas de lang_es.txt solo se usan si el juego no lo trae.
        foreach (var e in Etiquetas) EtiquetasLang.Requerir(Form, e);
        var tabla = new TablaDefP
        {
            Id = "cubos", Marcador = IdiomaInterfaz.T("PokecubosORAS.BuscarPokecubo"), SinNumero = true,
            Filas = c => [.. Enumerable.Range(0, Contest6.CountBlock).Select(i => new FilaTablaP
            {
                Id = i, Nombre = NombreDe(c, i), Min = 0, Max = Contest6.MaxBlock,
                Leer = x => (long)Concurso(x).GetBlockCount(i),
                Escribir = (x, v) => Concurso(x).SetBlockCount(i, (uint)(long)v),
            })],
            Acciones =
            [
                new AccionTablaP("maximo", L("B_GiveAllBlocks"), (_, filas) => [.. filas.Select(f => (f, (object)999L))], IdiomaInterfaz.T("PokecubosORAS.PonePokecubos")),
                new AccionTablaP("cero", IdiomaInterfaz.T("PokecubosORAS.Todos"), (_, filas) => [.. filas.Select(f => (f, (object)0L))], IdiomaInterfaz.T("PokecubosORAS.PonePokecubosCtrlOriginal")),
            ],
        };
        var bayas = new SeccionP(string.Empty,
        [
            P.Accion("bayas", L("B_RandomizeBerries"), c => ((SAV6AO)c.Sav).BerryField.ResetAndRandomize(Util.Rand, ItemStorage6XY.Berry), textoBoton: IdiomaInterfaz.T("PokecubosORAS.Aleatorizar"),
                confirmacion: IdiomaInterfaz.T("PokecubosORAS.RepueblanTodasParcelasHuerto"), ayuda: IdiomaInterfaz.T("PokecubosORAS.RepueblaTodasParcelasBayas")),
        ]);
        return new EditorDefP("pokeblocks6", IdiomaInterfaz.T("Partida.Especifico.pokeblocks6.Titulo"),
            [new PestanaP("cubos", EtiquetasLang.RequerirFormulario(Form), [], Tabla: tabla), new PestanaP("bayas", IdiomaInterfaz.T("PokecubosORAS.HuertoBayas"), [bayas])],
            Censo, IdiomaInterfaz.T("Partida.Especifico.pokeblocks6.Descripcion"));
    }

    private static string NombreDe(ContextoP c, int i)
        => 94 + i < c.Textos.pokeblocks.Length && c.Textos.pokeblocks[94 + i] is { Length: > 0 } t ? t : EtiquetasLang.Requerir(Form, Etiquetas[i]);
}
