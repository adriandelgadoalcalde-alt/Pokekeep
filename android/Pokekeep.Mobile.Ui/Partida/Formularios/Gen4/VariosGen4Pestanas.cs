// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Runtime.CompilerServices;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen4;

/// <summary>Las pestañas de <see cref="VariosGen4"/> que son tablas o listas de huecos: Pokétch, Pokéwalker, Sellos y Caja Corazón (accesorios y fondos).</summary>
internal static partial class VariosGen4
{
    // ---------------------------------------------------------------- Pokétch: aplicación actual y Dot Artist
    private static SAV4Sinnoh Sin(ContextoP c) => (SAV4Sinnoh)c.Sav;

    /// <summary>Los 120 bytes del Dot Artist como 20 líneas de 24 cifras: 0 es el tono más claro y 3 el más oscuro (la tabla <c>ColorTable</c> de PKHeX, de 248 a 8), un píxel por cifra, 2 bits por píxel en el orden del Core.</summary>
    internal static string MatrizATexto(ReadOnlySpan<byte> datos)
    {
        var lineas = new string[PoketchMatriz.Alto];
        for (var y = 0; y < PoketchMatriz.Alto; y++)
        {
            var linea = new char[PoketchMatriz.Ancho];
            for (var x = 0; x < PoketchMatriz.Ancho; x++)
            {
                var i = x + (PoketchMatriz.Ancho * y);
                linea[x] = (char)('0' + ((datos[i >> 2] >> ((i % 4) << 1)) & 3));
            }
            lineas[y] = new string(linea);
        }
        return string.Join('\n', lineas);
    }

    /// <summary>Lo contrario: null si el texto no son 20 líneas de 24 cifras de 0 a 3 (el motivo va en <see cref="ValidarMatriz"/>).</summary>
    internal static byte[]? TextoAMatriz(string texto)
    {
        var lineas = texto.Replace("\r\n", "\n").Split('\n');
        if (lineas.Length != PoketchMatriz.Alto || lineas.Any(l => l.Length != PoketchMatriz.Ancho || l.Any(ch => ch is < '0' or > '3'))) return null;
        var datos = new byte[PoketchMatriz.Ancho * PoketchMatriz.Alto / 4];
        for (var y = 0; y < PoketchMatriz.Alto; y++)
            for (var x = 0; x < PoketchMatriz.Ancho; x++)
            {
                var i = x + (PoketchMatriz.Ancho * y);
                datos[i >> 2] |= (byte)((lineas[y][x] - '0') << ((i % 4) << 1));
            }
        return datos;
    }

    /// <summary>Lo que llega al borrador siempre es una matriz válida: 20 líneas de 24 cifras de 0 a 3 (lo que no lo es se completa con ceros). La pantalla ya rechaza antes lo mal escrito con <see cref="ValidarMatriz"/>.</summary>
    internal static string NormalizarMatriz(string texto)
    {
        var lineas = texto.Replace("\r\n", "\n").Split('\n').Take(PoketchMatriz.Alto).Select(l => new string([.. l.Take(PoketchMatriz.Ancho).Select(ch => ch is >= '0' and <= '3' ? ch : '0')]).PadRight(PoketchMatriz.Ancho, '0')).ToList();
        while (lineas.Count < PoketchMatriz.Alto) lineas.Add(new string('0', PoketchMatriz.Ancho));
        return string.Join('\n', lineas);
    }

    private static string? ValidarMatriz(ContextoP _, string texto)
        => TextoAMatriz(texto) is null ? IdiomaInterfaz.F("VariosGen4Pestanas.EscribeLineasCifrasCada", PoketchMatriz.Alto, PoketchMatriz.Ancho) : null;

    private static PestanaP Poketch()
    {
        var campos = new List<CampoP>
        {
            // CB_CurrentApp: SelectedIndex = CurrentPoketchApp; SavePoketch lo escribe primero (el Core lo recorta a la ultima aplicacion).
            P.Opcion("app_actual", L("L_CurrentApp"), c => Sin(c).CurrentPoketchApp, (c, v) => Sin(c).CurrentPoketchApp = (sbyte)v,
                c => P.Lista(Enumerable.Range(0, (int)PoketchApp.Alarm_Clock + 1).Select(i => c.Textos.poketchapps[i])), titulo: L("L_CurrentApp"), prioridad: 0),
            new CampoP
            {
                Id = "matriz", Etiqueta = EtiquetaMatriz, Tipo = TipoP.Texto, Multilinea = true, Rejilla = new RejillaP(PoketchMatriz.Ancho, PoketchMatriz.Alto, Design.RejillaTonos.Grises(248, 168, 88, 8)), MaxLongitud = (PoketchMatriz.Ancho + 1) * PoketchMatriz.Alto, Prioridad = 2,
                Leer = c => MatrizATexto(Sin(c).GetPoketchDotArtistData()), Escribir = (c, v) => Sin(c).SetPoketchDotArtistData(TextoAMatriz((string)v!)!),
                Validar = ValidarMatriz, Normaliza = NormalizarMatriz,
                Ayuda = IdiomaInterfaz.T("VariosGen4Pestanas.RejillaCasillasCadaToque"),
                Detalle = _ => IdiomaInterfaz.T("VariosGen4Pestanas.TocaCasillaCambiarTono"),
            },
            // Soltar una imagen sobre la pestana (TAB_Poketch_DragDrop): TryBuild mezcla (OR) el dibujo en el Dot Artist del borrador. Aqui se elige con el selector del sistema y cambia el texto de la matriz del borrador.
            new CampoP
            {
                Id = "imagen", Etiqueta = IdiomaInterfaz.T("VariosGen4Pestanas.ImportarImagen"), Tipo = TipoP.Archivo, Leer = _ => null, Escribir = (_, _) => { }, Prioridad = 3, Extension = "png", TextoBoton = IdiomaInterfaz.T("VariosGen4Pestanas.ImportarImagen2"),
                Ayuda = IdiomaInterfaz.T("VariosGen4Pestanas.ImagenPuntosMuchoTonos"),
                ValidarArchivo = (_, datos) => PoketchMatriz.Brillos(datos) is { } b && PoketchMatriz.Construir(b, new byte[PoketchMatriz.Puntos / 4]) ? null : IdiomaInterfaz.T("VariosGen4Pestanas.ImagenTieneMedirPuntos"),
                Deriva = c =>
                {
                    var destino = TextoAMatriz(c.Texto("matriz")) ?? Sin(c).GetPoketchDotArtistData();
                    if (c.Valor("imagen") is byte[] imagen && PoketchMatriz.Brillos(imagen) is { } brillos) PoketchMatriz.Construir(brillos, destino);
                    else destino = Sin(c).GetPoketchDotArtistData();   // al quitar la imagen, la matriz vuelve a lo que hay en la partida
                    return new Dictionary<string, object?> { ["matriz"] = MatrizATexto(destino) };
                },
                AfectaA = _ => ["matriz"],
            },
        };
        return new PestanaP("poketch", L("GB_Poketch"), [new SeccionP(string.Empty, campos)]);
    }

    /// <summary>«Artista puntos»: el nombre de la aplicación en el texto del juego en castellano (<c>text_poketchapps_es.txt</c>, índice de <see cref="PoketchApp.Dot_Artist"/>), no uno propio.</summary>
    internal static string EtiquetaMatriz => Ajustes.Pkhex.ProgramaPkhex.Textos.poketchapps[(int)PoketchApp.Dot_Artist];

    // ---------------------------------------------------------------- Pokéwalker (TAB_Walker, solo HG/SS)
    private static SAV4HGSS Hg(ContextoP c) => (SAV4HGSS)c.Sav;

    private static PestanaP Pokewalker()
        => new("walker", L("TAB_Walker"),
        [
            new SeccionP(string.Empty,
            [
                // ReadWalker: NUD_Watts y NUD_Steps llegan a 9.999.999. SaveWalker escribe primero las rutas y despues vatios y pasos.
                P.Entero("vatios", L("L_Watts"), c => Math.Min(Hg(c).PokewalkerWatts, 9_999_999u), (c, v) => Hg(c).PokewalkerWatts = (uint)v, 0, 9_999_999, prioridad: 1),
                P.Entero("pasos", L("L_Steps"), c => Math.Min(Hg(c).PokewalkerSteps, 9_999_999u), (c, v) => Hg(c).PokewalkerSteps = (uint)v, 0, 9_999_999, prioridad: 2),
            ]),
        ]);

    private static PestanaP RutasPokewalker()
    {
        var tabla = new TablaDefP
        {
            Id = "rutas", SinNumero = true, Marcador = IdiomaInterfaz.T("VariosGen4Pestanas.BuscarRuta"),
            // Una ruta es un bit del indicador de 32: cada fila escribe los 32 con el suyo cambiado (SetPokewalkerCoursesUnlocked), como SaveWalker con la lista entera.
            Filas = c => [.. Enumerable.Range(0, c.Textos.walkercourses.Length).Select(i => new FilaTablaP
            {
                Id = i, Nombre = c.Textos.walkercourses[i], EsMarca = true,
                Leer = x => { Span<bool> r = stackalloc bool[SAV4HGSS.PokewalkerCourseFlagCount]; Hg(x).GetPokewalkerCoursesUnlocked(r); return r[i]; },
                Escribir = (x, v) => { Span<bool> r = stackalloc bool[SAV4HGSS.PokewalkerCourseFlagCount]; Hg(x).GetPokewalkerCoursesUnlocked(r); r[i] = (bool)v; Hg(x).SetPokewalkerCoursesUnlocked(r); },
            })],
            Acciones = [new AccionTablaP("todas", L("B_AllWalkerCourses"), (_, filas) => [.. filas.Select(f => (f, (object)true))], IdiomaInterfaz.T("VariosGen4Pestanas.MarcaTodasRutasPokewalker"))],
        };
        return new PestanaP("rutas", L("GB_WalkerCourses"), [], Tabla: tabla);
    }

    // ---------------------------------------------------------------- Sellos (Tab_Seals)
    private static PestanaP Sellos()
    {
        var tabla = new TablaDefP
        {
            Id = "sellos", SinNumero = true, Marcador = IdiomaInterfaz.T("Form.Trainer7.BuscarSello"), Ayuda = IdiomaInterfaz.T("VariosGen4Pestanas.CantidadCadaSelloCapsula"),
            Filas = c => [.. Enumerable.Range(0, (int)Seal4.MAX).Select(i => new FilaTablaP
            {
                Id = i, Nombre = c.Textos.seals[i], Min = 0, Max = SAV4.SealMaxCount,
                Leer = x => (long)S(x).GetSealCount((Seal4)i), Escribir = (x, v) => S(x).SetSealCount((Seal4)i, (byte)Math.Clamp((long)v, 0, 255)),
            })],
            // SetAllSeals(false) llega a MAXLEGAL (sin incluirlo) y SetAllSeals(true) a MAX: solo ponen 99, no quitan nada; ClearSeals pone 0 a todos.
            Acciones =
            [
                new AccionTablaP("quitar", L("B_ClearSeals"), (_, filas) => [.. filas.Select(f => (f, (object)0L))], IdiomaInterfaz.T("VariosGen4Pestanas.PoneTodosSellos")),
                new AccionTablaP("legales", L("B_AllSealsLegal"), (_, filas) => [.. filas.Where(f => f.Id < (int)Seal4.MAXLEGAL).Select(f => (f, (object)(long)SAV4.SealMaxCount))], IdiomaInterfaz.T("VariosGen4Pestanas.CadaSelloExisteJuego")),
                new AccionTablaP("ilegales", L("B_AllSealsIllegal"), (_, filas) => [.. filas.Select(f => (f, (object)(long)SAV4.SealMaxCount))], IdiomaInterfaz.T("VariosGen4Pestanas.CadaSelloTambienSalieron")),
            ],
        };
        return new PestanaP("sellos", L("Tab_Seals"), [], Tabla: tabla);
    }

    // ---------------------------------------------------------------- Caja Corazón: accesorios (Tab_FashionCase)
    private static PestanaP Accesorios()
    {
        var tabla = new TablaDefP
        {
            Id = "accesorios", SinNumero = true, Marcador = IdiomaInterfaz.T("AccesoriosBr4.BuscarAccesorio"), Ayuda = IdiomaInterfaz.T("VariosGen4Pestanas.AccesoriosColeccionCuentanResto"),
            Filas = c => [.. Enumerable.Range(0, AccessoryInfo.Count).Select(i =>
            {
                var multiple = ((Accessory4)i).IsMultiple();
                return new FilaTablaP
                {
                    Id = i, Nombre = c.Textos.accessories[i], EsMarca = !multiple, Min = 0, Max = AccessoryInfo.AccessoryMaxCount,
                    Leer = x => multiple ? (long)S(x).GetAccessoryOwnedCount((Accessory4)i) : S(x).GetAccessoryOwnedCount((Accessory4)i) != 0,
                    Escribir = (x, v) => S(x).SetAccessoryOwnedCount((Accessory4)i, v is bool b ? (byte)(b ? 1 : 0) : (byte)Math.Clamp((long)v, 0, 255)),
                };
            })],
            // SetAllAccessories: los de colección a 9 y los demás a 1 (hasta MaxLegal, o todos); no quita nada. ClearAccessories pone todos a 0.
            Acciones =
            [
                new AccionTablaP("quitar", L("B_ClearAccessories"), (_, filas) => [.. filas.Select(f => (f, f.EsMarca ? (object)false : 0L))], IdiomaInterfaz.T("VariosGen4Pestanas.QuitaTodosAccesorios")),
                new AccionTablaP("legales", L("B_AllAccessoriesLegal"), (_, filas) => Todos(filas, AccessoryInfo.MaxLegal + 1), IdiomaInterfaz.T("VariosGen4Pestanas.TodosAccesoriosExistenJuego")),
                new AccionTablaP("ilegales", L("B_AllAccessoriesIllegal"), (_, filas) => Todos(filas, AccessoryInfo.Count), IdiomaInterfaz.T("VariosGen4Pestanas.TodosAccesoriosTambienSalieron")),
            ],
        };
        return new PestanaP("accesorios", IdiomaInterfaz.F("Gen4.TabAccesorios", L("Tab_FashionCase")), [], Tabla: tabla);

        static IReadOnlyList<(FilaTablaP, object)> Todos(IReadOnlyList<FilaTablaP> filas, int hasta)
            => [.. filas.Where(f => f.Id <= AccessoryInfo.MaxMulti || f.Id < hasta).Select(f => (f, f.EsMarca ? (object)true : (long)AccessoryInfo.AccessoryMaxCount))];
    }

    // ---------------------------------------------------------------- Caja Corazón: fondos (DGV_Backdrops)
    private static readonly ConditionalWeakTable<ContextoP, Tuple<GameStrings, IReadOnlyList<OpcionP>>> OpcionesFondos = new();

    /// <summary>La lista del formulario: los nombres ordenados (<c>backdrops.Order()</c>), cada uno con el identificador de su primera aparición (<c>IndexOf</c>); «(Ninguno)» es el hueco vacío.</summary>
    private static IReadOnlyList<OpcionP> ListaFondos(ContextoP c)
    {
        // auditoría uso10 (A7): con el cambio de idioma en vivo la lista se rehace si cambian los textos del contexto.
        if (OpcionesFondos.TryGetValue(c, out var e) && ReferenceEquals(e.Item1, c.Textos)) return e.Item2;
        IReadOnlyList<OpcionP> lista = [.. c.Textos.backdrops.Order().Select(n => new OpcionP(Array.IndexOf(c.Textos.backdrops, n), n))];
        OpcionesFondos.AddOrUpdate(c, Tuple.Create(c.Textos, lista));
        return lista;
    }

    private static PestanaP Fondos()
    {
        var ids = Enumerable.Range(0, BackdropInfo.Count).Select(r => $"fondo{r}").ToArray();
        const string g = "fondos";
        var campos = new List<CampoP>();
        for (var r = 0; r < ids.Length; r++)
        {
            var hueco = r;
            // LoadBackdropPositions: el hueco r enseña el fondo cuya posicion es r (el ultimo si hubiera dos); sin ninguno, «(Ninguno)».
            long Leer(ContextoP c)
            {
                long fondo = (long)Backdrop4.Unset;
                for (var i = 0; i < BackdropInfo.Count; i++) if (S(c).GetBackdropPosition((Backdrop4)i) == hueco) fondo = i;
                return fondo;
            }
            campos.Add(P.Opcion(ids[r], IdiomaInterfaz.F("VariosGen4Pestanas.Hueco", r + 1), Leer, (c, v) => { if (hueco == 0) VolcarFondos(c, ids); }, ListaFondos, titulo: IdiomaInterfaz.F("VariosGen4Pestanas.Hueco", r + 1), prioridad: r, grupo: g, lecturaAsimetrica: true, afectaA: _ => ids));
        }
        // Los botones cambian los huecos del borrador (la rejilla), igual que en el formulario; al aplicar se vuelcan como SaveBackdrops.
        IReadOnlyDictionary<string, object?> Poner(Func<int, long> fondo) => ids.Select((id, r) => (id, r)).ToDictionary(t => t.id, t => (object?)fondo(t.r));
        campos.Add(AccionConBorrador("fondos_quitar", L("B_ClearBackdrops"), IdiomaInterfaz.T("VariosGen4Pestanas.QuitaTodosFondos"), _ => Poner(_ => (long)Backdrop4.Unset)));
        campos.Add(AccionConBorrador("fondos_legales", L("B_AllBackdropsLegal"), IdiomaInterfaz.T("VariosGen4Pestanas.TodosFondosExistenJuego"), c => Poner(r => r <= (int)BackdropInfo.MaxLegal ? r : c.Entero(ids[r]))));
        campos.Add(AccionConBorrador("fondos_ilegales", L("B_AllBackdropsIllegal"), IdiomaInterfaz.T("VariosGen4Pestanas.TodosFondosTambienSalieron"), _ => Poner(r => r)));
        return new PestanaP("fondos", IdiomaInterfaz.F("Gen4.TabFondos", L("Tab_FashionCase")), [new SeccionP(IdiomaInterfaz.T("VariosGen4Pestanas.Fondos"), campos.Take(BackdropInfo.Count).ToList()), new SeccionP(string.Empty, campos.Skip(BackdropInfo.Count).ToList())]);
    }

    /// <summary><c>SaveBackdrops</c>: quita todos los fondos y pone seguidos, en el orden de los huecos, los que no son «(Ninguno)».</summary>
    private static void VolcarFondos(ContextoP c, string[] ids)
    {
        var s = S(c);
        for (var i = 0; i < BackdropInfo.Count; i++) s.RemoveBackdrop((Backdrop4)i);
        byte n = 0;
        for (var r = 0; r < ids.Length; r++)
        {
            var fondo = (Backdrop4)(int)c.Entero(ids[r]);
            if (fondo.IsUnset()) continue;
            s.SetBackdropPosition(fondo, n);
            n++;
        }
    }

    /// <summary>
    /// Un botón del formulario que solo cambia lo que enseña la rejilla (hasta Guardar): en el móvil es una acción pendiente que, al marcarla, pone en el BORRADOR los valores de los campos y no escribe nada por sí misma.
    /// </summary>
    internal static CampoP AccionConBorrador(string id, string etiqueta, string ayuda, Func<ContextoP, IReadOnlyDictionary<string, object?>> deriva)
        => new() { Id = id, Etiqueta = etiqueta, Tipo = TipoP.Accion, Leer = _ => false, Escribir = (_, _) => { }, Prioridad = 1000, TextoBoton = IdiomaInterfaz.T("Eventos.Poner"), Ayuda = ayuda, Deriva = deriva };
}
