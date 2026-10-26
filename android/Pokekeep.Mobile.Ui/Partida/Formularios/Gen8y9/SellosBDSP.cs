// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen8y9;

/// <summary>
/// PK-SAV-170 · <c>SAV_SealStickers8b</c>, la lista de Sellos de Diamante Brillante y Perla Reluciente: por cada sello con nombre (los 96 de la lista <c>stickers</c>, de los 200 huecos del guardado) cuántos hay
/// (<c>Count</c>), cuántos se han conseguido en total (<c>TotalCount</c>) y si se ha obtenido alguna vez (<c>IsGet</c>). «Guardar» reescribe las tres cifras de cada fila y llama a <c>WriteItems</c>, que reescribe los
/// 200 huecos enteros (la marca «obtenido» siempre como 0 o 1 de cuatro bytes): aquí, con algún cambio, cada cambio se escribe en su sello y después se repite esa reescritura de los 200. «Todos» (<c>B_All</c>) suma a cada
/// cantidad lo que falta para llegar al máximo (99) en el total, pone el total a 99 y marca «obtenido»; «Ninguno» (<c>B_None</c>) pone todo a 0 y desmarca. Las dos cambian la cuadrícula (se guardan con «Guardar»):
/// aquí son acciones de la tabla sobre el borrador, que se pueden seguir editando antes de aplicar.
/// </summary>
internal static class SellosBDSP
{
    public const string Form = "SAV_SealStickers8b";
    public static readonly string[] Censo = ["PK-SAV-170"];
    private const int Maximo = SealSticker8b.MaxValue;
    private const long Tope = Maximo;

    private static string L(string control) => EtiquetasLang.Requerir(Form, control);
    private static SAV8BS Sav(ContextoP c) => (SAV8BS)c.Sav;

    /// <summary>Predicado barato de <see cref="Crear"/>: el editor existe para este guardado.</summary>
    public static bool Aplica(SaveFile sav) => sav is SAV8BS;

    private static string Clave(int sello, int dato) => $"sellos:{(sello * 3) + dato}";

    private static SealSticker8b Sello(ContextoP c, int indice) => new(Sav(c).SealList.Data, indice);

    private static void Escribir(ContextoP c, int indice, Action<SealSticker8b> cambio)
    {
        var s = Sello(c, indice);
        cambio(s);
        s.Write(Sav(c).SealList.Data);
    }

    /// <summary>Lo que hace «Guardar» al llamar a <c>WriteItems</c>: reescribe los 200 huecos (la marca «obtenido» como 0 o 1 en sus cuatro bytes).</summary>
    internal static void Normalizar(ContextoP c) => Sav(c).SealList.WriteItems(Sav(c).SealList.ReadItems());

    public static EditorDefP? Crear(SaveFile sav)
    {
        if (sav is not SAV8BS) return null;
        var tabla = new TablaDefP
        {
            Id = "sellos", Marcador = IdiomaInterfaz.T("Form.Trainer7.BuscarSello"), SinNumero = true,
            Ayuda = IdiomaInterfaz.T("SellosBDSP.TodosNingunoCambianTres"),
            Filas = _ =>
            {
                var nombres = Util.GetStringList("stickers", Pokekeep.Mobile.Ui.Ajustes.Pkhex.ProgramaPkhex.Idioma);
                var l = new List<FilaTablaP>();
                for (var i = 0; i < SealList8b.SealSaveSize; i++)
                {
                    if ((uint)i >= nombres.Length || nombres[i].Length == 0) continue;
                    var indice = i;
                    l.Add(new FilaTablaP { Id = (indice * 3) + 0, Nombre = $"{nombres[indice]} · {L("Col_Count")}", Min = 0, Max = Tope, Leer = x => (long)Sello(x, indice).Count, Escribir = (x, v) => Escribir(x, indice, s => s.Count = (int)(long)v) });
                    l.Add(new FilaTablaP { Id = (indice * 3) + 1, Nombre = $"{nombres[indice]} · {L("Col_Total")}", Min = 0, Max = Tope, Leer = x => (long)Sello(x, indice).TotalCount, Escribir = (x, v) => Escribir(x, indice, s => s.TotalCount = (int)(long)v) });
                    l.Add(new FilaTablaP { Id = (indice * 3) + 2, Nombre = $"{nombres[indice]} · {L("Col_Obtained")}", EsMarca = true, Leer = x => Sello(x, indice).IsGet, Escribir = (x, v) => Escribir(x, indice, s => s.IsGet = (bool)v) });
                }
                return l;
            },
            Acciones =
            [
                new AccionTablaP("todos", L("B_All"), (c, filas) =>
                {
                    var l = new List<(FilaTablaP, object)>();
                    foreach (var f in filas.Where(f => f.Id % 3 == 0))
                    {
                        var s = f.Id / 3;
                        var cantidad = c.Entero(Clave(s, 0)) + (Maximo - c.Entero(Clave(s, 1)));
                        l.Add((f, Math.Clamp(cantidad, 0, Tope)));
                        l.Add((filas.First(x => x.Id == (s * 3) + 1), Tope));
                        l.Add((filas.First(x => x.Id == (s * 3) + 2), true));
                    }
                    return l;
                }, IdiomaInterfaz.T("SellosBDSP.CadaCantidadSumaFalta")),
                new AccionTablaP("ninguno", L("B_None"), (_, filas) => [.. filas.Select(f => (f, f.EsMarca ? (object)false : 0L))], IdiomaInterfaz.T("SellosBDSP.CantidadTotalNingunoObtenido")),
            ],
        };
        return new EditorDefP("pegatinas8b", IdiomaInterfaz.T("Partida.Especifico.pegatinas8b.Titulo"), [new PestanaP("sellos", EtiquetasLang.RequerirFormulario(Form), [], Tabla: tabla)], Censo, IdiomaInterfaz.T("Partida.Especifico.pegatinas8b.Descripcion"))
        {
            PrepararAplicar = _ => Normalizar,
        };
    }
}
