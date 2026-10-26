// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Buffers.Binary;
using System.Globalization;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen8y9;

/// <summary>
/// PK-SAV-188 y PK-SAV-189 · <c>SAV_Fashion9</c>, la moda de Escarlata y Púrpura (8 listas de desbloqueo: gafas, guantes, bolso, calzado, gorros, calcetines, ropa y funda de móvil) y de Leyendas Z-A (10 listas de prendas y 15 de
/// peinado y maquillaje). Cada lista es un bloque de 8 bytes por hueco (<c>FashionItem9</c>, <c>FashionItem9a</c> y <c>HairMakeItem9a</c>): el código del objeto (<c>None</c> = 65535 en los huecos vacíos) y unas marcas (nuevo; en las prendas de
/// Z-A también «nuevo en la tienda», «nuevo en el grupo», «equipado» y «poseído»). El formulario enseña cada lista en una pestaña con una cuadrícula de TODOS los huecos (<c>IFashionBlockEditor</c>: <c>FashionItem9Editor</c>,
/// <c>FashionItem9aEditor</c>, <c>HairMake9aEditor</c>) y «Guardar» reescribe todos los huecos desde la cuadrícula (identidad si no se ha tocado nada: el código y las marcas se leen y se escriben enteros). Aquí cada lista es una
/// colección con un elemento por hueco y un campo por columna. «Marcar todos los que posees» (<c>SetAllOwned</c>, solo Z-A y solo en las prendas; con Mayúsculas en todas las pestañas y con Alt a «no poseídos») son dos acciones
/// inmediatas sobre la categoría elegida o sobre todas, y soltar un archivo de bloque sobre una pestaña (<c>LoadBlock</c>, el tamaño tiene que ser el del bloque) es «Importar bloque» de la categoría elegida.
/// </summary>
internal static class Moda9
{
    public const string Form = "SAV_Fashion9";
    public static readonly string[] Censo = ["PK-SAV-188", "PK-SAV-189"];
    private const int Hueco = 8;
    private const uint Ninguno = ushort.MaxValue;

    private static string L(string control) => EtiquetasLang.Requerir(Form, control);

    /// <summary>Qué clase de lista es: define las columnas y quién la lee (<c>FashionItem9</c> solo tiene «nuevo»; <c>FashionItem9a</c> tiene las cinco marcas; el peinado solo «nuevo»).</summary>
    private enum Clase { SV, Prenda, Peinado }

    private sealed record Categoria(string Id, string Titulo, uint Clave, Clase Clase);

    private static Categoria[] CategoriasSV =>     [
        new("gafas", IdiomaInterfaz.T("Moda9.Gafas"), SaveBlockAccessor9SV.KFashionUnlockedEyewear, Clase.SV),
        new("guantes", IdiomaInterfaz.T("Moda9.Guantes"), SaveBlockAccessor9SV.KFashionUnlockedGloves, Clase.SV),
        new("bolso", IdiomaInterfaz.T("Moda9.Bolso"), SaveBlockAccessor9SV.KFashionUnlockedBag, Clase.SV),
        new("calzado", IdiomaInterfaz.T("Moda9.Calzado"), SaveBlockAccessor9SV.KFashionUnlockedFootwear, Clase.SV),
        new("gorros", IdiomaInterfaz.T("Moda9.Gorros"), SaveBlockAccessor9SV.KFashionUnlockedHeadwear, Clase.SV),
        new("calcetines", IdiomaInterfaz.T("Moda9.Calcetines"), SaveBlockAccessor9SV.KFashionUnlockedLegwear, Clase.SV),
        new("ropa", IdiomaInterfaz.T("Moda9.Ropa"), SaveBlockAccessor9SV.KFashionUnlockedClothing, Clase.SV),
        new("funda", IdiomaInterfaz.T("Moda9.FundaMovil"), SaveBlockAccessor9SV.KFashionUnlockedPhoneCase, Clase.SV),
    ];

    private static Categoria[] CategoriasZA =>     [
        new("arriba", IdiomaInterfaz.T("Moda9.ParteArriba"), SaveBlockAccessor9ZA.KFashionTops, Clase.Prenda),
        new("abajo", IdiomaInterfaz.T("Moda9.ParteAbajo"), SaveBlockAccessor9ZA.KFashionBottoms, Clase.Prenda),
        new("conjuntos", IdiomaInterfaz.T("Moda9.Conjuntos"), SaveBlockAccessor9ZA.KFashionAllInOne, Clase.Prenda),
        new("gorros", IdiomaInterfaz.T("Moda9.Gorros"), SaveBlockAccessor9ZA.KFashionHeadwear, Clase.Prenda),
        new("gafas", IdiomaInterfaz.T("Moda9.Gafas"), SaveBlockAccessor9ZA.KFashionEyewear, Clase.Prenda),
        new("guantes", IdiomaInterfaz.T("Moda9.Guantes"), SaveBlockAccessor9ZA.KFashionGloves, Clase.Prenda),
        new("calcetines", IdiomaInterfaz.T("Moda9.Calcetines"), SaveBlockAccessor9ZA.KFashionLegwear, Clase.Prenda),
        new("calzado", IdiomaInterfaz.T("Moda9.Calzado"), SaveBlockAccessor9ZA.KFashionFootwear, Clase.Prenda),
        new("bolsos", IdiomaInterfaz.T("Moda9.Bolsos"), SaveBlockAccessor9ZA.KFashionSatchels, Clase.Prenda),
        new("pendientes", IdiomaInterfaz.T("Moda9.Pendientes"), SaveBlockAccessor9ZA.KFashionEarrings, Clase.Prenda),
        new("peinado", IdiomaInterfaz.T("Moda9.Peinado"), SaveBlockAccessor9ZA.KHairMake00StyleHair, Clase.Peinado),
        new("flequillo", IdiomaInterfaz.T("Moda9.Flequillo"), SaveBlockAccessor9ZA.KHairMake01StyleBangs, Clase.Peinado),
        new("color-pelo-1", IdiomaInterfaz.T("Moda9.ColorPelo"), SaveBlockAccessor9ZA.KHairMake02ColorHair, Clase.Peinado),
        new("color-pelo-2", IdiomaInterfaz.T("Moda9.ColorPelo2"), SaveBlockAccessor9ZA.KHairMake03ColorHair, Clase.Peinado),
        new("color-pelo-3", IdiomaInterfaz.T("Moda9.ColorPelo3"), SaveBlockAccessor9ZA.KHairMake04ColorHair, Clase.Peinado),
        new("cejas", IdiomaInterfaz.T("Moda9.Cejas"), SaveBlockAccessor9ZA.KHairMake05StyleEyebrow, Clase.Peinado),
        new("color-cejas", IdiomaInterfaz.T("Moda9.ColorCejas"), SaveBlockAccessor9ZA.KHairMake06ColorEyebrow, Clase.Peinado),
        new("ojos", IdiomaInterfaz.T("Moda9.Ojos"), SaveBlockAccessor9ZA.KHairMake07StyleEyes, Clase.Peinado),
        new("color-ojos", IdiomaInterfaz.T("Moda9.ColorOjos"), SaveBlockAccessor9ZA.KHairMake08ColorEyes, Clase.Peinado),
        new("pestanas", IdiomaInterfaz.T("Moda9.Pestanas"), SaveBlockAccessor9ZA.KHairMake09StyleEyelash, Clase.Peinado),
        new("color-pestanas", IdiomaInterfaz.T("Moda9.ColorPestanas"), SaveBlockAccessor9ZA.KHairMake10ColorEyelash, Clase.Peinado),
        new("labios", IdiomaInterfaz.T("Moda9.Labios"), SaveBlockAccessor9ZA.KHairMake11Lips, Clase.Peinado),
        new("lunar", IdiomaInterfaz.T("Moda9.Lunar"), SaveBlockAccessor9ZA.KHairMake12BeautyMark, Clase.Peinado),
        new("pecas", IdiomaInterfaz.T("Moda9.Pecas"), SaveBlockAccessor9ZA.KHairMake13Freckles, Clase.Peinado),
        new("ojeras", IdiomaInterfaz.T("Moda9.Ojeras"), SaveBlockAccessor9ZA.KHairMake14DarkCircles, Clase.Peinado),
    ];

    private static Categoria[] Categorias(SaveFile sav) => sav is SAV9ZA ? CategoriasZA : CategoriasSV;

    /// <summary>Predicado barato de <see cref="Crear"/>: el editor existe para este guardado.</summary>
    public static bool Aplica(SaveFile sav) => sav is SAV9SV or SAV9ZA;

    private static SCBlock Bloque(ContextoP c, Categoria cat) => c.Sav is SAV9ZA za ? za.Blocks.GetBlock(cat.Clave) : ((SAV9SV)c.Sav).Blocks.GetBlock(cat.Clave);

    private static int Huecos(ContextoP c, Categoria cat) => Bloque(c, cat).Raw.Length / Hueco;

    private static uint Codigo(ContextoP c, Categoria cat, int i) => BinaryPrimitives.ReadUInt32LittleEndian(Bloque(c, cat).Data[(i * Hueco)..]);
    private static uint Marcas(ContextoP c, Categoria cat, int i) => BinaryPrimitives.ReadUInt32LittleEndian(Bloque(c, cat).Data[((i * Hueco) + 4)..]);

    private static void PonerCodigo(ContextoP c, Categoria cat, int i, uint v) => BinaryPrimitives.WriteUInt32LittleEndian(Bloque(c, cat).Data[(i * Hueco)..], v);

    /// <summary>Cambia UNA marca del hueco sin tocar las demas (los setters de <c>FashionItem9*</c> hacen <c>Flags | bit</c> o <c>Flags &amp; ~bit</c>).</summary>
    private static void PonerMarca(ContextoP c, Categoria cat, int i, uint mascara, bool v)
    {
        var marcas = Marcas(c, cat, i);
        BinaryPrimitives.WriteUInt32LittleEndian(Bloque(c, cat).Data[((i * Hueco) + 4)..], v ? marcas | mascara : marcas & ~mascara);
    }

    private static string Resumen(ContextoP c, Categoria cat, int i)
    {
        var v = Codigo(c, cat, i);
        if (v == Ninguno) return null;
        var m = Marcas(c, cat, i);
        return cat.Clase == Clase.Prenda ? $"{((m & 0x10) != 0 ? IdiomaInterfaz.T("Moda9.Poseido") : IdiomaInterfaz.T("Moda9.Poseido2"))}{((m & 0x8) != 0 ? IdiomaInterfaz.T("Moda9.Equipado") : string.Empty)}" : null;
    }

    /// <summary>Lo que hace <c>FashionItem9a.ModifyAll(Block.Data, z => z.IsOwned = estado)</c>: pone «poseído» en todos los huecos con objeto.</summary>
    private static void MarcarPoseidos(ContextoP c, Categoria cat, bool estado)
    {
        var d = Bloque(c, cat).Data;
        for (var i = 0; i < d.Length / Hueco; i++)
        {
            var hueco = d.Slice(i * Hueco, Hueco);
            if (BinaryPrimitives.ReadUInt32LittleEndian(hueco) == Ninguno) continue;
            var marcas = BinaryPrimitives.ReadUInt32LittleEndian(hueco[4..]);
            BinaryPrimitives.WriteUInt32LittleEndian(hueco[4..], estado ? marcas | 0x10u : marcas & ~0x10u);
        }
    }

    private static ColeccionDefP Coleccion(Categoria cat)
    {
        ColeccionDefP? col = null;
        col = new ColeccionDefP
        {
            Id = cat.Id, Elemento = IdiomaInterfaz.T("EventosZA.Hueco"), Cantidad = c => Huecos(c, cat),
            Titulo = (c, i) => $"{i + 1:0000} · {(Codigo(c, cat, i) == Ninguno ? IdiomaInterfaz.T("Form.Fama1.Vacio") : Codigo(c, cat, i).ToString(CultureInfo.InvariantCulture))}",
            Resumen = (c, i) => Resumen(c, cat, i),
            Secciones = (_, i) =>
            {
                var campos = new List<CampoP>
                {
                    P.Entero(col!.IdCampo(i, "codigo"), EtiquetasLang.Requerir(Form, "Col_Value"), c => Codigo(c, cat, i), (c, v) => PonerCodigo(c, cat, i, (uint)v), 0, uint.MaxValue, ayuda: IdiomaInterfaz.T("Moda9.CodigoObjetoHuecoVacio")),
                    P.Marca(col.IdCampo(i, "nuevo"), EtiquetasLang.Requerir(Form, "Col_IsNew"), c => (Marcas(c, cat, i) & 0x1) != 0, (c, v) => PonerMarca(c, cat, i, 0x1, v)),
                };
                if (cat.Clase == Clase.Prenda)
                {
                    campos.Add(P.Marca(col.IdCampo(i, "nuevo-tienda"), EtiquetasLang.Requerir(Form, "Col_IsNewShop"), c => (Marcas(c, cat, i) & 0x2) != 0, (c, v) => PonerMarca(c, cat, i, 0x2, v)));
                    campos.Add(P.Marca(col.IdCampo(i, "nuevo-grupo"), EtiquetasLang.Requerir(Form, "Col_IsNewGroup"), c => (Marcas(c, cat, i) & 0x4) != 0, (c, v) => PonerMarca(c, cat, i, 0x4, v)));
                    campos.Add(P.Marca(col.IdCampo(i, "equipado"), EtiquetasLang.Requerir(Form, "Col_IsEquipped"), c => (Marcas(c, cat, i) & 0x8) != 0, (c, v) => PonerMarca(c, cat, i, 0x8, v)));
                    campos.Add(P.Marca(col.IdCampo(i, "poseido"), EtiquetasLang.Requerir(Form, "Col_IsOwned"), c => (Marcas(c, cat, i) & 0x10) != 0, (c, v) => PonerMarca(c, cat, i, 0x10, v)));
                }
                return [new SeccionP(string.Empty, campos)];
            },
        };
        return col;
    }

    public static EditorDefP? Crear(SaveFile sav)
    {
        if (sav is not (SAV9SV or SAV9ZA)) return null;
        var cats = Categorias(sav);
        var esZA = sav is SAV9ZA;
        // 0 = todas las categorias; las demas, una categoria por valor (1, 2...).
        IReadOnlyList<OpcionP> Elegibles(ContextoP _) => [new OpcionP(0, IdiomaInterfaz.T("Moda9.TodasCategorias")), .. cats.Select((x, i) => new OpcionP(i + 1, x.Titulo))];
        IEnumerable<Categoria> Elegidas(ContextoP c, bool soloPrendas) => (c.Entero("categoria") == 0 ? cats : [cats[(int)c.Entero("categoria") - 1]]).Where(x => !soloPrendas || x.Clase == Clase.Prenda);
        string? SinPrendas(ContextoP c) => Elegidas(c, true).Any() ? null : IdiomaInterfaz.T("Moda9.CategoriaTieneCasillaPoseido");
        var campos = new List<CampoP>
        {
            // Parametro de las acciones de abajo: se elige y no se escribe (no cuenta como cambio).
            new CampoP { Id = "categoria", Etiqueta = IdiomaInterfaz.T("Form.Categoria"), Tipo = TipoP.Opcion, Leer = _ => 0L, Escribir = null, SinEscritura = true, Opciones = Elegibles, TituloSelector = IdiomaInterfaz.T("Form.Categoria"), Ayuda = IdiomaInterfaz.T("Moda9.ListaRefierenAccionesAbajo") },
        };
        if (esZA)
        {
            campos.Add(P.Accion("poseer-todos", L("B_SetAllOwned"), c => { foreach (var x in Elegidas(c, true)) MarcarPoseidos(c, x, true); }, SinPrendas, inmediata: true, textoBoton: IdiomaInterfaz.T("BasesSecretas6.Marcar"), ayuda: IdiomaInterfaz.T("Moda9.MarcaPoseidoCadaObjeto")));
            campos.Add(P.Accion("poseer-ninguno", IdiomaInterfaz.F("Moda9.PuntoQuitar", L("B_SetAllOwned")), c => { foreach (var x in Elegidas(c, true)) MarcarPoseidos(c, x, false); }, SinPrendas, inmediata: true, textoBoton: IdiomaInterfaz.T("PasesBr4.Quitar"), confirmacion: IdiomaInterfaz.T("Moda9.QuitaMarcaPoseidoCada"), ayuda: IdiomaInterfaz.T("Moda9.QuitaMarcaPoseidoCada2")));
        }
        campos.Add(ImportarBloque(cats));
        return new EditorDefP("moda", IdiomaInterfaz.T("Moda9.Moda"), [new PestanaP("acciones", EtiquetasLang.RequerirFormulario(Form), [new SeccionP(string.Empty, campos)]), .. cats.Select(x => new PestanaP(x.Id, x.Titulo, [], Coleccion: Coleccion(x)))], Censo, IdiomaInterfaz.T("Partida.Especifico.moda.Descripcion"))
        {
            // Una pestana por clase de columnas (la plantilla es la misma en todas las de la clase) mas las acciones.
            PestanasDeGate = ["acciones", .. cats.GroupBy(x => x.Clase).Select(g => g.First().Id)],
        };
    }

    /// <summary>
    /// Soltar un archivo sobre una pestaña (<c>Main_DragDrop</c> + <c>LoadBlock</c>): el archivo tiene que medir lo mismo que el bloque y sustituye sus datos (<c>SCBlock.ChangeData</c>). Aquí es un campo de archivo que
    /// afecta a la categoría elegida y se escribe PRIMERO (lo que se edite después en los huecos gana).
    /// </summary>
    private static CampoP ImportarBloque(Categoria[] cats) => P.Archivo("importar-bloque", IdiomaInterfaz.T("Moda9.ImportarBloqueCategoria"),
        (c, datos) => Bloque(c, Elegida(cats, c)!).ChangeData(datos),
        (c, datos) => Elegida(cats, c) is not { } cat ? IdiomaInterfaz.T("Moda9.EligeCategoriaConcretaTodas") : datos.Length != Bloque(c, cat).Raw.Length ? IdiomaInterfaz.F("Moda9.ArchivoMideBytesBloque", datos.Length, cat.Titulo, Bloque(c, cat).Raw.Length) : null,
        extension: "bin", textoBoton: IdiomaInterfaz.T("Form.Importar"), prioridad: -1, confirmacion: IdiomaInterfaz.T("Moda9.BloqueCategoriaElegidaSustituye"), ayuda: IdiomaInterfaz.T("Moda9.SustituyeTodosHuecosCategoria"));

    private static Categoria? Elegida(Categoria[] cats, ContextoP c) => c.Entero("categoria") is > 0 and var n && n <= cats.Length ? cats[(int)n - 1] : null;
}
