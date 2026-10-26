// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen6y7;

/// <summary>
/// PK-SAV-148 · <c>SAV_ZygardeCell</c>, las Células y los Núcleos de Zygarde de Sol/Luna (95 ubicaciones) y Ultrasol/Ultraluna (100): el total almacenado y el coleccionado (0 a 65.535) y el estado de cada ubicación
/// (ninguna, disponible o recibida). «Dar todo» (<c>B_GiveAll</c>) pone todas en «Recibida» y suma las que no lo estaban a «Coleccionado» y, solo en Sol/Luna, a «Almacenado»; en el formulario cambia los controles y se guarda con
/// «Guardar»: aqui es una accion de la tabla sobre el BORRADOR de sus filas (los dos totales son filas de la misma tabla). En Ultrasol/Ultraluna «Guardar» escribe tambien el récord 72 con el coleccionado. Los nombres de las
/// ubicaciones las da PKHeX en inglés en el propio formulario; aquí se compone el lugar OFICIAL en castellano del Core (<c>locations/gen7/text_sm_00000_es.txt</c>) con el detalle común traducido (día/noche, exterior, cima, plantas, puntos cardinales) y los nombres propios que el Core no trae se quedan en inglés (ADR 0064).
/// </summary>
internal static class CelulasZygarde
{
    public const string Form = "SAV_ZygardeCell";
    public static readonly string[] Censo = ["PK-SAV-148"];
    private const int FilaAlmacenado = 1000, FilaColeccionado = 1001, Maximo = ushort.MaxValue;

    private static string L(string control) => EtiquetasLang.Requerir(Form, control);
    private static EventWork7 Eventos(ContextoP c) => ((SAV7)c.Sav).EventWork;
    private static string Clave(int fila) => $"celulas:{fila}";

    /// <summary>Predicado barato de <see cref="Crear"/>: el editor existe para este guardado.</summary>
    public static bool Aplica(SaveFile sav) => sav is SAV7;

    public static EditorDefP? Crear(SaveFile sav)
    {
        if (sav is not SAV7) return null;
        var tabla = new TablaDefP
        {
            Id = "celulas", Marcador = IdiomaInterfaz.T("CelulasZygarde.BuscarUbicacion"), SinNumero = true,
            Ayuda = IdiomaInterfaz.T("CelulasZygarde.EstadoCadaUbicacionNinguna"),
            Filas = c =>
            {
                var l = new List<FilaTablaP>
                {
                    new() { Id = FilaAlmacenado, Nombre = L("L_Cells").TrimEnd(':'), Min = 0, Max = Maximo, Leer = x => (long)Eventos(x).ZygardeCellTotal, Escribir = (x, v) => Eventos(x).ZygardeCellTotal = (ushort)(long)v },
                    new() { Id = FilaColeccionado, Nombre = L("L_Collected").TrimEnd(':'), Min = 0, Max = Maximo, Leer = x => (long)Eventos(x).ZygardeCellCount,
                        Escribir = (x, v) => { Eventos(x).ZygardeCellCount = (ushort)(long)v; if (x.Sav is SAV7USUM u) u.SetRecord(72, (int)(long)v); } },
                };
                var nombres = IdiomaInterfaz.Actual.Codigo == "en" ? (c.Sav is SAV7SM ? CelulasZygardeIngles.SM : CelulasZygardeIngles.USUM) : (c.Sav is SAV7SM ? UbicacionesSM : UbicacionesUSUM);   // en inglés, los nombres de PKHeX tal cual
                for (var i = 0; i < Eventos(c).TotalZygardeCellCount; i++)
                {
                    var n = i;
                    l.Add(new FilaTablaP { Id = n, Nombre = $"{n + 1:000} · {nombres[n]}", Min = 0, Max = Math.Max(2, (int)Eventos(c).GetZygardeCell(n)), Leer = x => (long)Eventos(x).GetZygardeCell(n),
                        Escribir = (x, v) => Eventos(x).SetZygardeCell(n, (ushort)(long)v), Detalle = IdiomaInterfaz.T("CelulasZygarde.NingunaDisponibleRecibida") });
                }
                return l;
            },
            Acciones =
            [
                new AccionTablaP("dar-todo", L("B_GiveAll"), (c, filas) =>
                {
                    var l = new List<(FilaTablaP, object)>();
                    long anadidas = 0;
                    foreach (var f in filas.Where(f => f.Id < FilaAlmacenado)) { if (c.Entero(Clave(f.Id)) != 2) anadidas++; l.Add((f, 2L)); }
                    l.Add((filas.First(f => f.Id == FilaColeccionado), Math.Min(Maximo, c.Entero(Clave(FilaColeccionado)) + anadidas)));
                    if (c.Sav is not SAV7USUM) l.Add((filas.First(f => f.Id == FilaAlmacenado), Math.Min(Maximo, c.Entero(Clave(FilaAlmacenado)) + anadidas)));
                    return l;
                }, IdiomaInterfaz.T("CelulasZygarde.PoneTodasUbicacionesRecibida")),
            ],
        };
        return new EditorDefP("celulas", IdiomaInterfaz.T("Partida.Especifico.celulas.Titulo"), [new PestanaP("celulas", EtiquetasLang.RequerirFormulario(Form), [], Tabla: tabla)], Censo, IdiomaInterfaz.T("Partida.Especifico.celulas.Descripcion"));
    }

    private static readonly string[] UbicacionesSM =
    [
        "Cueva Sotobosque · Sala de la Prueba",
        "Ruinas de la Guerra · exterior",
        "Ruta 1 (día)",
        "Ruta 3",
        "Ruta 3 (día)",
        "Bahía Kalae",
        "Cementerio de Hauoli",
        "Ruta 2",
        "Ruta 1 · Trainer School (noche)",
        "Ciudad Hauoli · Zona Comercial",
        "Ruta 1 · Outskirts",
        "Ciudad Hauoli · Zona Comercial (noche)",
        "Ruta 1",
        "Pueblo Lilii (noche)",
        "Ruta 4",
        "Rancho Ohana (noche)",
        "Rancho Ohana (día)",
        "Área Volcánica Wela · cima",
        "Jungla Umbría · cueva",
        "Ruta 7",
        "Afueras de Akala",
        "Avenida Royale (día)",
        "Avenida Royale (noche)",
        "Ciudad Konikoni (noche)",
        "Ciudad Kantai (noche)",
        "Ruta 8",
        "Ruta 8 (día)",
        "Ruta 5",
        "Playa de Hanohano (día)",
        "Ciudad Kantai",
        "Túnel Diglett",
        "Playa de Hanohano",
        "Parque de Malíe",
        "Ciudad Malíe · Community Center (noche)",
        "Ciudad Malíe (día)",
        "Ciudad Malíe · Cabo de las Afueras (día)",
        "Ruta 11 (noche)",
        "Ruta 12 (día)",
        "Ruta 12",
        "Playa de Ula-Ula (noche)",
        "Monte Rubor",
        "Ruta 13",
        "Desierto de Haina",
        "Ruinas de la Cosecha · exterior",
        "Ruta 14",
        "Ruta 14 (noche)",
        "Aldea Tapu",
        "Ruta 15",
        "Aether House (día)",
        "Jardines de Ula-Ula · Boardwalk",
        "Ruta 16 (día)",
        "Jardines de Ula-Ula · hierba",
        "Ruta 17 · Building",
        "Ruta 17 · Ledge",
        "Pueblo Po (noche)",
        "Ruta 10 (día)",
        "Observ. de Hokulani (noche)",
        "Monte Lanakila · Mountainside",
        "Monte Lanakila · High Mountainside",
        "Playa de Ula-Ula (día)",
        "Ruta 13 (noche)",
        "Pueblo Po (día)",
        "Aldea Marina · Blue Food Boat",
        "Aldea Marina · Unbuilt House",
        "Prado de Poni (día)",
        "Prado de Poni (noche)",
        "Prado de Poni",
        "Antiguo Paso de Poni · Near Well (día)",
        "Antiguo Paso de Poni (noche)",
        "Arrecife de Poni (día)",
        "Ruinas del Tránsito",
        "Bosque de Poni · Mountain Corner",
        "Bosque de Poni · Near a Bush",
        "Llanura de Poni (día)",
        "Llanura de Poni (noche)",
        "Llanura de Poni",
        "Jardines de Poni",
        "Costa de Poni (noche)",
        "Costa de Poni",
        "Pendiente de Poni · On Bridge",
        "Pendiente de Poni · Island w/ Trainer",
        "Gruta Desenlace · 1.ª planta (día)",
        "Gruta Desenlace · sótano 1 (noche)",
        "Cañón de Poni · 3.ª planta",
        "Cañón de Poni · 2.ª planta",
        "Cañón de Poni · cima",
        "Cañón de Poni · interior",
        "Antiguo Paso de Poni · Brickwall (día)",
        "Arrecife de Poni (noche)",
        "Gruta Desenlace · sótano 1",
        "Aether Foundation sótano 2 · Right Hallway",
        "Aether Foundation 1.ª planta · exterior · Right Side",
        "Aether Foundation 1.ª planta · exterior (día)",
        "Aether Foundation 1.ª planta · entrada (noche)",
        "Aether Foundation 1.ª planta · Main Building",
    ];

    private static readonly string[] UbicacionesUSUM =
    [
        "Ciudad Hauoli · Zona Comercial · salón (exterior)",
        "Ciudad Hauoli · Zona Comercial · Malasada Shop (exterior)",
        "Ciudad Hauoli · Zona Comercial · Ilima's House (2.ª planta)",
        "Ciudad Malíe · biblioteca (1.ª planta)",
        "Ciudad Hauoli · Puerto · muelle",
        "Ruta 2 · Southeast House",
        "Ciudad Hauoli · Zona Comercial · Ilima's House (exterior)",
        "Ciudad Hauoli · Zona Comercial · ayuntamiento",
        "Ciudad Kantai · hotel (3.ª planta)",
        "Ruta 2 · Berry Fields House",
        "Ruta 2 · Berry Fields House (exterior)",
        "Avenida Royale · noreste",
        "Ciudad Hauoli · Zona Comercial · Centro Pokémon (exterior)",
        "Avenida Royale · sur",
        "Observ. de Hokulani · sala",
        "Observ. de Hokulani · recepción",
        "Ciudad Hauoli · Zona Comercial · ayuntamiento (exterior)",
        "Ciudad Konikoni · Olivia's Jewelry Shop (2.ª planta)",
        "Ciudad Kantai · Surfboard (exterior)",
        "Pueblo Po · suroeste",
        "Resort Hanohano vestíbulo · Southwest Water",
        "Ciudad Hauoli · Zona Comercial · Northwest of Police Station",
        "Ciudad Hauoli · Puerto · Ferry Terminal (exterior)",
        "Ruta 2 · Southeast House (exterior)",
        "Ruta 2 · Centro Pokémon (exterior)",
        "Ciudad Kantai · oeste",
        "Ciudad Kantai · hotel oeste (exterior)",
        "Ciudad Kantai · hotel este (exterior)",
        "Ciudad Kantai · Research Lab East (exterior)",
        "Ciudad Kantai · Research Lab South (exterior)",
        "Ciudad Kantai · Game Freak",
        "Observ. de Hokulani · callejón sin salida",
        "Ciudad Kantai · Game Freak Building (3.ª planta)",
        "Ciudad Kantai · Research Lab",
        "Ciudad Kantai · hotel (1.ª planta)",
        "Estadio Royale · 2.ª planta",
        "Pueblo Ohana · oeste",
        "Pueblo Ohana · Kiawe's House (1.ª planta)",
        "Pueblo Ohana · Kiawe's House (2.ª planta)",
        "Rancho Ohana · noroeste",
        "Rancho Ohana · sureste",
        "Playa de Hanohano",
        "Resort Hanohano · sur",
        "Resort Hanohano · norte",
        "Konikoni City Lighthouse (Through Diglett's Tunnel)",
        "Estadio Royale · 1.ª planta",
        "Ruta 8 · Aether Base (exterior)",
        "Ruta 8 · Fossil Restoration Center (exterior)",
        "Ciudad Konikoni · oeste",
        "Ciudad Konikoni · restaurante (1.ª planta)",
        "Pueblo Lilii · suroeste",
        "Ciudad Hauoli · Zona Comercial · Ilima's House Pool",
        "Área Volcánica Wela · Rocks Behind Sign",
        "Ruta 5 · South of Pokemon Center",
        "Playa de Hanohano · Below Sandygast",
        "Ciudad Malíe · Cabo de las Afueras · Recycling Plant (exterior)",
        "Ciudad Malíe · Ferry Terminal (exterior)",
        "Ciudad Malíe · Apparel Shop (exterior)",
        "Ciudad Malíe · salón (exterior)",
        "Ruta 16 · Aether Base (exterior)",
        "Monte Rubor · Power Plant (exterior)",
        "Ciudad Malíe · biblioteca (2.ª planta)",
        "Parque de Malíe · noreste",
        "Ciudad Malíe · CommunityCenter",
        "Observ. de Hokulani · exterior",
        "Pico Hokulani",
        "Monte Rubor · Power Plant",
        "Ruta 13",
        "Ruta 14 · Front of Abandoned Megamart",
        "Ruta 14 · norte",
        "Ruta 15 · Islet Surfboard (exterior)",
        "Ruta 17 · comisaría (exterior)",
        "Ruta 17 · comisaría",
        "Pueblo Po · Centro Pokémon (exterior)",
        "Isla Exeggutor · Under Rock",
        "Pueblo Po · Mansión Misteriosa este (exterior)",
        "Pueblo Po · Centro Pokémon",
        "Pueblo Po · Mansión Misteriosa (1.ª planta)",
        "Ruta 13 · motel (exterior)",
        "Pueblo Po · Mansión Misteriosa 2.ª planta (exterior)",
        "Ruta 17 · South of Po Town",
        "Jardines de Ula-Ula",
        "Pueblo Po · Shady House West Rocks (Outside) 1",
        "Pueblo Po · Shady House West Rocks (Outside) 2",
        "Pueblo Po · Shady House West Rocks (Outside) 3",
        "Aldea Marina · Southeast Whiscash (Mina's Ship) (exterior)",
        "Aldea Marina · Southwest Huntail",
        "Aldea Marina · Southwest Huntail (exterior)",
        "Aldea Marina · Southeast Whiscash (Mina's Ship)",
        "Aldea Marina · West Wailord (restaurante)",
        "Aldea Marina · East Steelix",
        "Prado de Poni · sureste",
        "Antiguo Paso de Poni · Hapu's House (Kitchen)",
        "Aldea Marina · noreste",
        "Antiguo Paso de Poni · Hapu's House (Bedroom)",
        "Antiguo Paso de Poni · suroeste",
        "Antiguo Paso de Poni · Hapu's House (Courtyard)",
        "Antiguo Paso de Poni · Hapu's House (Outside Behind Well)",
        "Antiguo Paso de Poni · noreste",
        "Árbol de Combate · entrada",
    ];
}
