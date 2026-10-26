// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen4;

/// <summary>
/// PK-SAV-027: <c>SAV_Apricorn</c>, la cantidad de cada Bonguri (Oro, Plata y Cristal HeartGold/SoulSilver). El formulario es una rejilla de 7 filas (nombre del objeto y cantidad escrita a mano) con «Dar todos» (99 de cada uno) y
/// «Ninguno» (0), que SOLO tocan la rejilla; al guardar, <c>B_Save_Click</c> lee cada celda como entero (lo que no es un numero cuenta 0) y llama a <c>SetApricornCount(i, Math.Min(255, cantidad))</c>.
/// Aqui es una tabla de 7 filas (0-255) con las dos acciones masivas sobre el borrador; el orden de los nombres es el del formulario (el amarillo y el azul van cambiados respecto al numero de objeto: 485, 487, 486, 488...).
/// </summary>
internal static class Bonguri4
{
    public const string Form = "SAV_Apricorn";
    public static readonly string[] Censo = ["PK-SAV-027"];
    private const int Cantidad = 7, PrimerObjeto = 485;   // 485: Bonguri Rojo
    private static readonly byte[] DesplazamientoDelNombre = [0, 2, 1, 3, 4, 5, 6];

    private static string L(string control) => EtiquetasLang.Requerir(Form, control);

    /// <summary>Predicado barato de <see cref="Crear"/>: <c>B_OpenApricorn</c> solo existe en HeartGold y SoulSilver.</summary>
    public static bool Aplica(SaveFile sav) => sav is SAV4HGSS;

    public static EditorDefP? Crear(SaveFile sav)
    {
        if (sav is not SAV4HGSS) return null;
        var tabla = new TablaDefP
        {
            Id = "cantidades", SinNumero = true, Marcador = IdiomaInterfaz.T("Bonguri4.BuscarBonguri"),
            Ayuda = IdiomaInterfaz.T("Bonguri4.CantidadCadaBonguriBolsa"),
            Filas = c => [.. Enumerable.Range(0, Cantidad).Select(i => new FilaTablaP
            {
                Id = i, Nombre = c.Textos.itemlist[PrimerObjeto + DesplazamientoDelNombre[i]], Min = 0, Max = 255,
                Leer = x => (long)((SAV4HGSS)x.Sav).GetApricornCount(i),
                Escribir = (x, v) => ((SAV4HGSS)x.Sav).SetApricornCount(i, (int)Math.Min(255L, (long)v)),
            })],
            // B_All y B_None cambian solo la rejilla (el borrador); B_Save las escribe todas.
            Acciones =
            [
                new AccionTablaP("todos", L("B_All"), (_, filas) => [.. filas.Select(f => (f, (object)99L))], IdiomaInterfaz.T("Bonguri4.PoneCadaBonguri")),
                new AccionTablaP("ninguno", L("B_None"), (_, filas) => [.. filas.Select(f => (f, (object)0L))], IdiomaInterfaz.T("Bonguri4.PoneCadaBonguri2")),
            ],
        };
        return new EditorDefP("apricorn", TituloGen4.De("apricorn"), [new PestanaP("cantidades", IdiomaInterfaz.T("Bonguri4.Cantidades"), [], Tabla: tabla)], Censo, IdiomaInterfaz.T("Partida.Especifico.apricorn.Descripcion"));
    }
}
