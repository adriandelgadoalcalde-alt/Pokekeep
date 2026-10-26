// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen8y9;

/// <summary>
/// PK-SAV-182 · <c>SAV_Underground8b</c>, los objetos del Subsuelo de Diamante Brillante y Perla Reluciente: por cada objeto del subsuelo (esferas, objetos, estatuas y pedestales; los huecos de tipo «ninguno» no salen)
/// su cantidad (hasta 999, o 99 en estatuas y pedestales), si se enseña la marca «nuevo» (<c>!HideNewFlag</c>) y si es favorito. «Guardar» reescribe las tres cifras de cada fila y llama a <c>WriteItems</c>, que reescribe los 999
/// huecos enteros (las dos marcas siempre como 0 o 1 de cuatro bytes): aquí, con algún cambio, cada cambio se escribe en su objeto y después se repite esa reescritura de los 999. «Todos» (<c>B_All</c>) pone cada cantidad en su
/// máximo; «Ninguno» (<c>B_None</c>) pone la cantidad a 0, «nuevo» marcado y favorito desmarcado. Las dos cambian la cuadrícula (se guardan con «Guardar»): aquí son acciones de la tabla sobre el borrador.
/// El Subsuelo de Diamante/Perla de Sinnoh (Gen. 4) es otro formulario del bloque 8a-2 con la misma entrada de navegación.
/// </summary>
internal static class SubsueloBDSP
{
    public const string Form = "SAV_Underground8b";
    public static readonly string[] Censo = ["PK-SAV-182"];

    private static string L(string control) => EtiquetasLang.Requerir(Form, control);
    private static SAV8BS Sav(ContextoP c) => (SAV8BS)c.Sav;

    /// <summary>Predicado barato de <see cref="Crear"/>: el editor existe para este guardado.</summary>
    public static bool Aplica(SaveFile sav) => sav is SAV8BS;

    private static string Clave(int objeto, int dato) => $"subsuelo:{(objeto * 3) + dato}";

    private static UndergroundItem8b Objeto(ContextoP c, int indice) => new(Sav(c).Underground.Data, indice);

    private static void Escribir(ContextoP c, int indice, Action<UndergroundItem8b> cambio)
    {
        var o = Objeto(c, indice);
        cambio(o);
        o.Write(Sav(c).Underground.Data);
    }

    /// <summary>Lo que hace «Guardar» al llamar a <c>WriteItems</c>: reescribe los 999 huecos (las dos marcas como 0 o 1 en sus cuatro bytes).</summary>
    internal static void Normalizar(ContextoP c) => Sav(c).Underground.WriteItems(Sav(c).Underground.ReadItems());

    private static string Tipo(UgItemType t) => t switch
    {
        UgItemType.Sphere => L("Tipo_Esfera"),
        UgItemType.Item => L("Tipo_Objeto"),
        UgItemType.Statue => L("Tipo_Estatua"),
        UgItemType.Pedestal => L("Tipo_Pedestal"),
        _ => t.ToString(),
    };

    public static EditorDefP? Crear(SaveFile sav)
    {
        if (sav is not SAV8BS) return null;
        var tabla = new TablaDefP
        {
            Id = "subsuelo", Marcador = IdiomaInterfaz.T("Mochila.Buscar"), SinNumero = true,
            Ayuda = IdiomaInterfaz.T("SubsueloBDSP.TodosPoneCadaCantidad"),
            Filas = c =>
            {
                var nombres = Util.GetStringList("ug_item", Pokekeep.Mobile.Ui.Ajustes.Pkhex.ProgramaPkhex.Idioma);
                var l = new List<FilaTablaP>();
                foreach (var item in Sav(c).Underground.ReadItems())
                {
                    if (item.Type == UgItemType.None) continue;
                    var indice = item.Index; var max = item.MaxValue;
                    var nombre = indice < nombres.Length ? nombres[indice] : IdiomaInterfaz.F("SubsueloBDSP.Objeto", indice);
                    var grupo = Tipo(item.Type);
                    l.Add(new FilaTablaP { Id = (indice * 3) + 0, Nombre = $"{nombre} · {L("Col_Count")}", Grupo = grupo, Min = 0, Max = max, Leer = x => (long)Objeto(x, indice).Count, Escribir = (x, v) => Escribir(x, indice, o => o.Count = (int)(long)v) });
                    l.Add(new FilaTablaP { Id = (indice * 3) + 1, Nombre = $"{nombre} · {L("Col_New")}", Grupo = grupo, EsMarca = true, Leer = x => !Objeto(x, indice).HideNewFlag, Escribir = (x, v) => Escribir(x, indice, o => o.HideNewFlag = !(bool)v) });
                    l.Add(new FilaTablaP { Id = (indice * 3) + 2, Nombre = $"{nombre} · {L("Col_Favorite")}", Grupo = grupo, EsMarca = true, Leer = x => Objeto(x, indice).IsFavoriteFlag, Escribir = (x, v) => Escribir(x, indice, o => o.IsFavoriteFlag = (bool)v) });
                }
                return l;
            },
            Acciones =
            [
                new AccionTablaP("todos", L("B_All"), (_, filas) => [.. filas.Where(f => f.Id % 3 == 0).Select(f => (f, (object)f.Max))], IdiomaInterfaz.T("SubsueloBDSP.PoneCadaCantidadMaximo")),
                new AccionTablaP("ninguno", L("B_None"), (_, filas) => [.. filas.Select(f => (f, f.Id % 3 == 0 ? 0L : f.Id % 3 == 1 ? (object)true : false))], IdiomaInterfaz.T("SubsueloBDSP.CantidadNuevoMarcadoFavorito")),
            ],
        };
        return new EditorDefP("subsuelo", IdiomaInterfaz.T("Partida.Especifico.subsuelo.Titulo"), [new PestanaP("objetos", EtiquetasLang.RequerirFormulario(Form), [], Tabla: tabla)], Censo, IdiomaInterfaz.T("SubsueloBDSP.CantidadesObjetosSubsuelo"))
        {
            PrepararAplicar = _ => Normalizar,
        };
    }
}
