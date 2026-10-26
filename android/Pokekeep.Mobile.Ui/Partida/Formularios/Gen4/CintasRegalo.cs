// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen4;

/// <summary>
/// PK-SAV-214: <c>SAV_GiftRibbons</c>, las cintas de regalo que guarda la partida de Gen. 3 (Rubí, Zafiro, Esmeralda, Rojo Fuego, Verde Hoja, Colosseum y XD: 11 casillas) y de Gen. 4 (14 casillas), nuevo en PKHeX 26.10.18.
/// El botón del SAVEditor se muestra para todo guardado que implemente <see cref="IGiftRibbons"/>. El original es una rejilla de dos columnas (nombre de la cinta e índice de descripción, un byte) con «Todas legales», «Restablecer» y «Guardar»:
/// «Todas legales» vacía la rejilla y pone las cintas que da el juego (Gen. 3: País 32, Nacional 44, Tierra 45, Mundo 32; Gen. 4: Clásica 64, Premier 59); «Restablecer» la deja a cero. Ambos botones solo cambian la rejilla; al
/// guardar, cada celda se lee como byte (lo que no es un número cuenta 0). Aquí es una tabla de 0 a 255 con las dos acciones sobre el borrador. Como en el original, no se recorta a 64 en Gen. 3 (Esmeralda no admite más de 64, Rubí
/// no lo comprueba): la ayuda lo avisa.
/// </summary>
internal static class CintasRegalo
{
    public const string Form = "SAV_GiftRibbons";
    public static readonly string[] Censo = ["PK-SAV-214"];

    private static string L(string control) => EtiquetasLang.Requerir(Form, control);

    /// <summary>Predicado barato de <see cref="Crear"/>: el mismo que el del SAVEditor (<c>sav is IGiftRibbons</c>).</summary>
    public static bool Aplica(SaveFile sav) => sav is IGiftRibbons;

    private static IGiftRibbons B(ContextoP c) => (IGiftRibbons)c.Sav;
    private static int Cuenta(SaveFile sav) => ((IGiftRibbons)sav).GiftRibbons.Length;

    /// <summary>Las cintas con las que «Todas legales» deja la rejilla: (cinta, valor) según la generación, como <c>B_Legal_Click</c>.</summary>
    internal static IReadOnlyList<(RibbonIndex Cinta, byte Valor)> Legales(int generacion) => generacion switch
    {
        3 => [(RibbonIndex.Country, 32), (RibbonIndex.National, 44), (RibbonIndex.Earth, 45), (RibbonIndex.World, 32)],
        4 => [(RibbonIndex.Classic, 64), (RibbonIndex.Premier, 59)],
        _ => [],
    };

    public static EditorDefP? Crear(SaveFile sav)
    {
        if (!Aplica(sav)) return null;
        var tabla = new TablaDefP
        {
            Id = "cintas", SinNumero = true, Marcador = IdiomaInterfaz.T("CintasRegalo.BuscarCinta"),
            Ayuda = IdiomaInterfaz.T("CintasRegalo.AyudaTabla"),
            Filas = c => [.. Enumerable.Range(0, Cuenta(c.Sav)).Select(i => new FilaTablaP
            {
                Id = i, Nombre = c.Textos.Ribbons.GetName(IGiftRibbons.Index[i].PropertyName), Min = 0, Max = byte.MaxValue,
                Leer = x => (long)B(x).GiftRibbons[i],
                Escribir = (x, v) => B(x).GiftRibbons[i] = (byte)Math.Clamp((long)v, 0, byte.MaxValue),
            })],
            Acciones =
            [
                new AccionTablaP("legales", L("B_Legal"), (c, filas) =>
                {
                    // GiftRibbonsClear + GiftRibbonSet de cada cinta legal; GiftRibbonSet ignora una cinta que no cabe en la partida (índice fuera de las casillas) o un valor mayor que el máximo.
                    var max = Cuenta(c.Sav) == IGiftRibbons.SIZE_3 ? IGiftRibbons.MAX_3 : IGiftRibbons.MAX_4;
                    var valores = new long[filas.Count];
                    foreach (var (cinta, valor) in Legales(c.Sav.Generation))
                    {
                        var i = IGiftRibbons.Index.IndexOf(cinta);
                        if (i != -1 && i < valores.Length && valor <= max) valores[i] = valor;
                    }
                    return [.. filas.Select(f => (f, (object)valores[f.Id]))];
                }, IdiomaInterfaz.T("CintasRegalo.AyudaLegales")),
                new AccionTablaP("restablecer", L("B_Reset"), (_, filas) => [.. filas.Select(f => (f, (object)0L))], IdiomaInterfaz.T("CintasRegalo.AyudaRestablecer")),
            ],
        };
        return new EditorDefP("cintasregalo", TituloGen4.De("cintasregalo"), [new PestanaP("cintas", IdiomaInterfaz.T("CintasRegalo.Cintas"), [], Tabla: tabla)], Censo, IdiomaInterfaz.T("Partida.Especifico.cintasregalo.Descripcion"));
    }
}
