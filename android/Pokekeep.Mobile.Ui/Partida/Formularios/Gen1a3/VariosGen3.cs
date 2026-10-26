// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Buffers.Binary;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;
using Pokekeep.Mobile.Ui.Ajustes.Pkhex;
using Pokekeep.Mobile.Ui.Modelo;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen1a3;

/// <summary>
/// PK-SAV-004, 006 a 015: <c>SAV_Misc3</c>, el editor de varios de la 3.ª generación (Rubí, Zafiro, Esmeralda, Rojo Fuego y Verde Hoja). En WinForms es una ventana de nueve pestañas, algunas solo para ciertos juegos:
/// Inicio (PB y fichas; en Rojo Fuego/Verde Hoja el rival y los iconos de la tarjeta), Minijuegos (Esmeralda y Rojo Fuego/Verde Hoja), Ferry y Frente Batalla (solo Esmeralda), Registros, Pokécubos, Adornos y Pinturas
/// (Rubí, Zafiro y Esmeralda) y Otros (Rubí, Zafiro y Esmeralda). Al guardar, <c>B_Save_Click</c> escribe en este orden: Pokécubos, adornos, pintura abierta, minijuegos, ferry, Frente Batalla, rival e iconos de la tarjeta
/// (Rojo Fuego/Verde Hoja), PB (Esmeralda) y fichas; los registros, los PB conseguidos, las estadísticas del Frente y «Obtener tickets» actúan sobre el guardado nada más tocarlos.
/// <para>Cómo se reparten las pestañas en el móvil: una pestaña del móvil enseña o secciones, o una tabla, o una lista de elementos, nunca dos a la vez. Por eso «Frente Batalla» son dos pestañas (el pase y los símbolos; las
/// estadísticas por instalación), los Pokécubos son dos (la lista de 40 y «Obtener todos»/«Quitar todos») y los adornos y las pinturas son listas (una categoría o una pintura por elemento).</para>
/// <para>Fuera de alcance, declarado: el visor de caracteres especiales del nombre del rival (<c>TrashEditor</c>) y el «Pase Batalla» dibujado con colores (los tres estados del símbolo —sin símbolo, plata y oro— son una lista).</para>
/// </summary>
internal static partial class VariosGen3
{
    public const string Form = "SAV_Misc3";
    public static readonly string[] Censo = ["PK-SAV-004", "PK-SAV-006", "PK-SAV-007", "PK-SAV-008", "PK-SAV-009", "PK-SAV-010", "PK-SAV-011", "PK-SAV-012", "PK-SAV-013", "PK-SAV-014", "PK-SAV-015"];

    private static string L(string control) => EtiquetasLang.Requerir(Form, control);
    private static SAV3 S(ContextoP c) => (SAV3)c.Sav;
    private static ISaveBlock3LargeHoenn Hoenn(ContextoP c) => (ISaveBlock3LargeHoenn)S(c).LargeBlock;
    private static bool EsHoenn(SaveFile sav) => sav is SAV3 { LargeBlock: ISaveBlock3LargeHoenn };

    /// <summary>Predicado barato de <see cref="Crear"/>: el editor existe para todo guardado de la 3.ª generación de consola portátil (<c>B_OpenMiscEditor</c>: <c>sav is SAV3</c>).</summary>
    public static bool Aplica(SaveFile sav) => sav is SAV3;

    public static EditorDefP? Crear(SaveFile sav)
    {
        if (sav is not SAV3 s3) return null;
        var pestanas = new List<PestanaP> { Inicio(s3) };
        if (s3.SmallBlock is ISaveBlock3SmallExpansion) pestanas.Add(Minijuegos());
        if (s3 is SAV3E) { pestanas.Add(Ferry(s3)); pestanas.Add(PaseFrente()); pestanas.Add(EstadisticasFrente()); }
        pestanas.Add(Registros(s3));
        if (EsHoenn(sav)) { pestanas.Add(Pokecubos()); pestanas.Add(TodosLosPokecubos()); pestanas.Add(Adornos()); pestanas.Add(Pinturas()); pestanas.Add(Otros()); }
        return new EditorDefP("misc", IdiomaInterfaz.T("Form.Varios"), pestanas, Censo, IdiomaInterfaz.T("Form.Varios3.Descripcion"));
    }

    // ---------------------------------------------------------------- Inicio (TAB_Main)
    private static PestanaP Inicio(SAV3 s3)
    {
        var general = new List<CampoP>();
        // PB y PB conseguidos solo existen en Esmeralda (en el resto el formulario oculta los dos cuadros). Los PB conseguidos se escriben al cambiarlos; los PB, al guardar, y despues las fichas.
        if (s3 is SAV3E)
        {
            general.Add(P.Entero("pb_conseguidos", L("L_BPEarned"), c => ((SAV3E)c.Sav).SmallBlock.BPEarned, (c, v) => ((SAV3E)c.Sav).SmallBlock.BPEarned = (ushort)v, 0, 65535, prioridad: 20));
            general.Add(P.Entero("pb", L("L_BP"), c => ((SAV3E)c.Sav).SmallBlock.BP, (c, v) => ((SAV3E)c.Sav).SmallBlock.BP = (ushort)v, 0, 9999, prioridad: 30));
        }
        general.Add(P.Entero("fichas", L("L_Coins"), c => S(c).Coin, (c, v) => S(c).Coin = (ushort)v, 0, 9999, prioridad: 31));
        if (s3 is SAV3FRLG)
            general.Add(P.Texto("rival", L("L_TrainerName"), c => ((SAV3FRLG)c.Sav).RivalName, (c, v) => { var f = (SAV3FRLG)c.Sav; if (f.RivalName != v) f.RivalName = v; }, maxLongitud: 7, prioridad: 10));   // si no cambia, se conserva la basura del nombre
        var secciones = new List<SeccionP> { new(string.Empty, general) };
        if (s3 is SAV3FRLG)
        {
            // Iconos Pokemon de la Tarjeta Entrenador: el trabajo 0x43 + i guarda la especie en el orden interno de la 3.ª generacion.
            var iconos = new List<CampoP>();
            for (var i = 0; i < 6; i++)
            {
                var k = i;
                iconos.Add(P.Opcion($"tarjeta{k + 1}", IdiomaInterfaz.F("Form.PokemonNumero", k + 1), c => SpeciesConverter.GetNational3(S(c).GetWork(0x43 + k)), (c, v) => S(c).SetWork(0x43 + k, SpeciesConverter.GetInternal3((ushort)v)), AyudasGen3.Especies, titulo: IdiomaInterfaz.F("Form.PokemonNumero", k + 1), prioridad: 11 + k));
            }
            secciones.Add(new SeccionP(L("GB_TCM"), iconos));
        }
        return new PestanaP("inicio", L("TAB_Main"), secciones);
    }

    // ---------------------------------------------------------------- Minijuegos (TAB_Joyful)
    private static PestanaP Minijuegos()
    {
        static ISaveBlock3SmallExpansion J(ContextoP c) => (ISaveBlock3SmallExpansion)S(c).SmallBlock;
        // Las mascaras del formulario son de 4 cifras (0-9999) y la del polvo de baya de 5. Las puntuaciones se leen con tope 99990 (la mayor posible en el juego).
        return new PestanaP("minijuegos", L("TAB_Joyful"),
        [
            new SeccionP(L("label4"),
            [
                P.Entero("salto_seguidos", L("L_JRow"), c => Math.Min((ushort)9999, J(c).JoyfulJumpInRow), (c, v) => J(c).JoyfulJumpInRow = (ushort)v, 0, 9999, prioridad: 0),
                P.Entero("salto_puntos", L("L_JHigh"), c => Math.Min(99990u, J(c).JoyfulJumpScore), (c, v) => J(c).JoyfulJumpScore = (uint)v, 0, 9999, prioridad: 1),
                P.Entero("salto_5", L("L_J5Score"), c => Math.Min((ushort)9999, J(c).JoyfulJump5InRow), (c, v) => J(c).JoyfulJump5InRow = (ushort)v, 0, 9999, prioridad: 2),
                P.Entero("salto_jugadores", L("L_JMaxPlayers"), c => Math.Min((ushort)9999, J(c).JoyfulJumpGamesMaxPlayers), (c, v) => J(c).JoyfulJumpGamesMaxPlayers = (ushort)v, 0, 9999, prioridad: 3),
            ]),
            new SeccionP(L("label5"),
            [
                P.Entero("bayas_capturadas", L("L_BCaught"), c => Math.Min((ushort)9999, J(c).JoyfulBerriesInRow), (c, v) => J(c).JoyfulBerriesInRow = (ushort)v, 0, 9999, prioridad: 4),
                P.Entero("bayas_puntos", L("L_BHigh"), c => Math.Min(99990u, J(c).JoyfulBerriesScore), (c, v) => J(c).JoyfulBerriesScore = (uint)v, 0, 9999, prioridad: 5),
                P.Entero("bayas_5", L("L_B5Score"), c => Math.Min((ushort)9999, J(c).JoyfulBerries5InRow), (c, v) => J(c).JoyfulBerries5InRow = (ushort)v, 0, 9999, prioridad: 6),
            ]),
            new SeccionP(string.Empty, [P.Entero("polvo", L("L_BerryPowder"), c => Math.Min(99999u, J(c).BerryPowder), (c, v) => J(c).BerryPowder = (uint)v, 0, 99999, prioridad: 7)]),
        ]);
    }

    // ---------------------------------------------------------------- Ferry (TAB_Ferry)
    internal const ushort ObjetoMapaMarinoAntiguo = 0x178;
    internal static readonly ushort[] ObjetosTicket = [0x109, 0x113, 0x172, 0x173, ObjetoMapaMarinoAntiguo];

    /// <summary>Los tickets que faltan en los objetos clave y si caben, como <c>B_GetTickets_Click</c> antes de preguntar: (faltan, motivo por el que no se puede).</summary>
    private static (ushort[] Faltan, string? Motivo) EstadoTickets(SaveFile sav, bool incluyeMapa)
    {
        var tickets = incluyeMapa ? ObjetosTicket : ObjetosTicket[..^1];
        var bolsa = sav.Inventory.GetPouch(InventoryType.KeyItems);
        var faltan = tickets.Where(t => !bolsa.HasItem(t)).ToArray();
        if (faltan.Length == 0) return (faltan, IdiomaInterfaz.T("Form.Varios3.TicketsYa"));
        var fin = bolsa.FindIndexFirstEmptySlot();
        if (fin == -1 || fin + faltan.Length >= bolsa.Items.Length) return (faltan, IdiomaInterfaz.T("Form.Varios3.TicketsSinSitio"));
        return (faltan, null);
    }

    private static void DarTickets(SaveFile sav, bool incluyeMapa)
    {
        var (faltan, motivo) = EstadoTickets(sav, incluyeMapa);
        if (motivo is not null) return;
        var bolsa = sav.Inventory;
        var objetos = bolsa.GetPouch(InventoryType.KeyItems);
        var fin = objetos.FindIndexFirstEmptySlot();
        for (var i = 0; i < faltan.Length; i++) { var it = objetos.Items[fin + i]; it.Index = faltan[i]; it.Count = 1; }
        bolsa.CopyTo(sav);
    }

    private static bool TieneElMapa(SaveFile sav) => sav.Inventory.GetPouch(InventoryType.KeyItems).HasItem(ObjetoMapaMarinoAntiguo);

    private static string NombresFaltan(ContextoP c, ushort[] faltan)
    {
        var nombres = c.Textos.GetItemStrings(c.Sav.Context, c.Sav.Version);
        return string.Join(", ", faltan.Select(i => nombres[i]));
    }

    private static CampoP Bandera(string id, string etiqueta, int bandera, int prioridad)
        => P.Marca(id, etiqueta, c => S(c).GetEventFlag(bandera), (c, v) => S(c).SetEventFlag(bandera, v), prioridad: prioridad);

    private static PestanaP Ferry(SAV3 s3)
    {
        var tickets = new List<CampoP>
        {
            // Los cuatro tickets de siempre; el Mapa Marino Antiguo no salio de Japon (en un guardado japones entra con los demas).
            P.Accion("tickets", L("B_GetTickets"), c => DarTickets(c.Sav, S(c).Japanese), motivo: c => EstadoTickets(c.Sav, S(c).Japanese).Motivo, prioridad: -10, textoBoton: IdiomaInterfaz.T("Form.Obtener"),
                detalle: c => EstadoTickets(c.Sav, S(c).Japanese) is { Motivo: null } e ? IdiomaInterfaz.F("Form.Varios3.TicketsDetalle", NombresFaltan(c, e.Faltan)) : null,
                confirmacion: IdiomaInterfaz.T("Form.Varios3.TicketsConfirmar"), ayuda: IdiomaInterfaz.T("Form.Varios3.TicketsAyuda")),
        };
        if (!s3.Japanese)
            tickets.Add(P.Accion("tickets_mapa", L("B_GetTickets"), c => DarTickets(c.Sav, true), motivo: c => EstadoTickets(c.Sav, true).Motivo, prioridad: -9, textoBoton: IdiomaInterfaz.T("Form.Obtener"),
                visible: c => !TieneElMapa(c.Sav),   // con el mapa ya en la bolsa las dos variantes hacen lo mismo: solo se enseña la primera
                etiquetaFn: c => IdiomaInterfaz.F("Form.Varios3.TicketsMapaEtiqueta", L("B_GetTickets"), c.Textos.GetItemStrings(c.Sav.Context, c.Sav.Version)[ObjetoMapaMarinoAntiguo]),
                detalle: c => EstadoTickets(c.Sav, true) is { Motivo: null } e ? IdiomaInterfaz.F("Form.Varios3.TicketsDetalle", NombresFaltan(c, e.Faltan)) : null,
                confirmacion: IdiomaInterfaz.T("Form.Varios3.TicketsMapaConfirmar"), ayuda: IdiomaInterfaz.T("Form.Varios3.TicketsMapaAyuda")));
        return new PestanaP("ferry", L("TAB_Ferry"),
        [
            new SeccionP(string.Empty, [.. tickets, Bandera("ferry_puede_ir", L("CHK_Catchable"), 0x864, 0)]),
            new SeccionP(L("GB_Reachable"),
            [
                Bandera("alcanza_sur", L("CHK_ReachSouthern"), 0x8B3, 1), Bandera("alcanza_origen", L("CHK_ReachBirth"), 0x8D5, 2), Bandera("alcanza_suprema", L("CHK_ReachFaraway"), 0x8D6, 3),
                Bandera("alcanza_ombligo", L("CHK_ReachNavel"), 0x8E0, 4), Bandera("alcanza_frente", L("CHK_ReachBF"), 0x1D0, 5),
            ]),
            new SeccionP(L("GB_InitialEvent"),
            [
                Bandera("inicial_sur", L("CHK_InitialSouthern"), 0x1AE, 6), Bandera("inicial_origen", L("CHK_InitialBirth"), 0x1AF, 7),
                Bandera("inicial_suprema", L("CHK_InitialFaraway"), 0x1B0, 8), Bandera("inicial_ombligo", L("CHK_InitialNavel"), 0x1DB, 9),
            ]),
        ]);
    }

    // ---------------------------------------------------------------- Registros (Tab_Records)
    // Record3.GetItems da los nombres en ingles (enum RecID3*): PKHeX no los traduce, asi que van aqui traducidos de forma descriptiva.
    private static readonly Dictionary<string, string> NombresRegistros = new(StringComparer.Ordinal)
    {
        ["SAVED_GAME"] = ServicioIdioma.Recursos("es-ES")["Form.Varios3.Registro.SAVED_GAME"], ["FIRST_HOF_PLAY_TIME"] = ServicioIdioma.Recursos("es-ES")["Form.Varios3.Registro.FIRST_HOF_PLAY_TIME"], ["STARTED_TRENDS"] = ServicioIdioma.Recursos("es-ES")["Form.Varios3.Registro.STARTED_TRENDS"],
        ["PLANTED_BERRIES"] = ServicioIdioma.Recursos("es-ES")["Form.Varios3.Registro.PLANTED_BERRIES"], ["TRADED_BIKES"] = ServicioIdioma.Recursos("es-ES")["Form.Varios3.Registro.TRADED_BIKES"], ["STEPS"] = ServicioIdioma.Recursos("es-ES")["Form.Varios3.Registro.STEPS"], ["GOT_INTERVIEWED"] = ServicioIdioma.Recursos("es-ES")["Form.Varios3.Registro.GOT_INTERVIEWED"], ["TOTAL_BATTLES"] = ServicioIdioma.Recursos("es-ES")["Form.Varios3.Registro.TOTAL_BATTLES"],
        ["WILD_BATTLES"] = ServicioIdioma.Recursos("es-ES")["Form.Varios3.Registro.WILD_BATTLES"], ["TRAINER_BATTLES"] = ServicioIdioma.Recursos("es-ES")["Form.Varios3.Registro.TRAINER_BATTLES"], ["ENTERED_HOF"] = ServicioIdioma.Recursos("es-ES")["Form.Varios3.Registro.ENTERED_HOF"], ["POKEMON_CAPTURES"] = ServicioIdioma.Recursos("es-ES")["Form.Varios3.Registro.POKEMON_CAPTURES"],
        ["FISHING_CAPTURES"] = ServicioIdioma.Recursos("es-ES")["Form.Varios3.Registro.FISHING_CAPTURES"], ["HATCHED_EGGS"] = ServicioIdioma.Recursos("es-ES")["Form.Varios3.Registro.HATCHED_EGGS"], ["EVOLVED_POKEMON"] = ServicioIdioma.Recursos("es-ES")["Form.Varios3.Registro.EVOLVED_POKEMON"], ["USED_POKECENTER"] = ServicioIdioma.Recursos("es-ES")["Form.Varios3.Registro.USED_POKECENTER"],
        ["RESTED_AT_HOME"] = ServicioIdioma.Recursos("es-ES")["Form.Varios3.Registro.RESTED_AT_HOME"], ["ENTERED_SAFARI_ZONE"] = ServicioIdioma.Recursos("es-ES")["Form.Varios3.Registro.ENTERED_SAFARI_ZONE"], ["USED_CUT"] = ServicioIdioma.Recursos("es-ES")["Form.Varios3.Registro.USED_CUT"], ["USED_ROCK_SMASH"] = ServicioIdioma.Recursos("es-ES")["Form.Varios3.Registro.USED_ROCK_SMASH"],
        ["MOVED_SECRET_BASE"] = ServicioIdioma.Recursos("es-ES")["Form.Varios3.Registro.MOVED_SECRET_BASE"], ["POKEMON_TRADES"] = ServicioIdioma.Recursos("es-ES")["Form.Varios3.Registro.POKEMON_TRADES"], ["UNKNOWN_22"] = ServicioIdioma.Recursos("es-ES")["Form.Varios3.Registro.UNKNOWN_22"], ["LINK_BATTLE_WINS"] = ServicioIdioma.Recursos("es-ES")["Form.Varios3.Registro.LINK_BATTLE_WINS"],
        ["LINK_BATTLE_LOSSES"] = ServicioIdioma.Recursos("es-ES")["Form.Varios3.Registro.LINK_BATTLE_LOSSES"], ["LINK_BATTLE_DRAWS"] = ServicioIdioma.Recursos("es-ES")["Form.Varios3.Registro.LINK_BATTLE_DRAWS"], ["USED_SPLASH"] = ServicioIdioma.Recursos("es-ES")["Form.Varios3.Registro.USED_SPLASH"], ["USED_STRUGGLE"] = ServicioIdioma.Recursos("es-ES")["Form.Varios3.Registro.USED_STRUGGLE"],
        ["SLOT_JACKPOTS"] = ServicioIdioma.Recursos("es-ES")["Form.Varios3.Registro.SLOT_JACKPOTS"], ["CONSECUTIVE_ROULETTE_WINS"] = ServicioIdioma.Recursos("es-ES")["Form.Varios3.Registro.CONSECUTIVE_ROULETTE_WINS"], ["ENTERED_BATTLE_TOWER"] = ServicioIdioma.Recursos("es-ES")["Form.Varios3.Registro.ENTERED_BATTLE_TOWER"],
        ["UNKNOWN_31"] = ServicioIdioma.Recursos("es-ES")["Form.Varios3.Registro.UNKNOWN_31"], ["BATTLE_TOWER_BEST_STREAK"] = ServicioIdioma.Recursos("es-ES")["Form.Varios3.Registro.BATTLE_TOWER_BEST_STREAK"], ["POKEBLOCKS"] = ServicioIdioma.Recursos("es-ES")["Form.Varios3.Registro.POKEBLOCKS"], ["POKEBLOCKS_WITH_FRIENDS"] = ServicioIdioma.Recursos("es-ES")["Form.Varios3.Registro.POKEBLOCKS_WITH_FRIENDS"],
        ["WON_LINK_CONTEST"] = ServicioIdioma.Recursos("es-ES")["Form.Varios3.Registro.WON_LINK_CONTEST"], ["ENTERED_CONTEST"] = ServicioIdioma.Recursos("es-ES")["Form.Varios3.Registro.ENTERED_CONTEST"], ["WON_CONTEST"] = ServicioIdioma.Recursos("es-ES")["Form.Varios3.Registro.WON_CONTEST"], ["SHOPPED"] = ServicioIdioma.Recursos("es-ES")["Form.Varios3.Registro.SHOPPED"],
        ["USED_ITEMFINDER"] = ServicioIdioma.Recursos("es-ES")["Form.Varios3.Registro.USED_ITEMFINDER"], ["GOT_RAINED_ON"] = ServicioIdioma.Recursos("es-ES")["Form.Varios3.Registro.GOT_RAINED_ON"], ["CHECKED_POKEDEX"] = ServicioIdioma.Recursos("es-ES")["Form.Varios3.Registro.CHECKED_POKEDEX"], ["RECEIVED_RIBBONS"] = ServicioIdioma.Recursos("es-ES")["Form.Varios3.Registro.RECEIVED_RIBBONS"],
        ["JUMPED_DOWN_LEDGES"] = ServicioIdioma.Recursos("es-ES")["Form.Varios3.Registro.JUMPED_DOWN_LEDGES"], ["WATCHED_TV"] = ServicioIdioma.Recursos("es-ES")["Form.Varios3.Registro.WATCHED_TV"], ["CHECKED_CLOCK"] = ServicioIdioma.Recursos("es-ES")["Form.Varios3.Registro.CHECKED_CLOCK"], ["WON_POKEMON_LOTTERY"] = ServicioIdioma.Recursos("es-ES")["Form.Varios3.Registro.WON_POKEMON_LOTTERY"],
        ["USED_DAYCARE"] = ServicioIdioma.Recursos("es-ES")["Form.Varios3.Registro.USED_DAYCARE"], ["RODE_CABLE_CAR"] = ServicioIdioma.Recursos("es-ES")["Form.Varios3.Registro.RODE_CABLE_CAR"], ["ENTERED_HOT_SPRINGS"] = ServicioIdioma.Recursos("es-ES")["Form.Varios3.Registro.ENTERED_HOT_SPRINGS"], ["UNION_WITH_FRIENDS"] = ServicioIdioma.Recursos("es-ES")["Form.Varios3.Registro.UNION_WITH_FRIENDS"],
        ["BERRY_CRUSH_WITH_FRIENDS"] = ServicioIdioma.Recursos("es-ES")["Form.Varios3.Registro.BERRY_CRUSH_WITH_FRIENDS"],
    };

    /// <summary>Los registros del juego de este guardado como (número, nombre en castellano), SIN el último (<c>NUM_GAME_STATS</c>) como hace <see cref="Record3.GetItems"/>.</summary>
    internal static IReadOnlyList<(int Id, string Nombre)> ListaRegistros(SAV3 sav)
    {
        if (ProgramaPkhex.Idioma == "en") return [.. Record3.GetItems(sav).Select(r => (r.Value, HumanizarRegistro(r.Text)))];
        var nombres = Record3.GetEnumNames(sav.Version); var valores = Record3.GetEnumValues(sav.Version);
        return [.. Enumerable.Range(0, valores.Length - 1).Select(i => (valores[i], NombresRegistros.TryGetValue(nombres[i], out var t) ? t : nombres[i].Replace('_', ' ')))];
    }

    // The Core's enum labels remain untouched; these are display corrections authorized in X36r.
    internal static string HumanizarRegistro(string nombre) => nombre
        .Replace("Pokecenter", "Pokémon Center", StringComparison.Ordinal)
        .Replace("Pokemon", IdiomaInterfaz.T("Nav.Pokemon"), StringComparison.Ordinal)
        .Replace("Pokeblock", "Pokéblock", StringComparison.Ordinal)
        .Replace("Pokedex", IdiomaInterfaz.T("Partida.Editor.pokedex.Titulo"), StringComparison.Ordinal)
        .Replace("Hof", "Hall of Fame", StringComparison.Ordinal)
        .Replace("Tv", "TV", StringComparison.Ordinal)
        .Replace("Daycare", "Day Care", StringComparison.Ordinal)
        .Replace("Union With", "Union Room With", StringComparison.Ordinal);

    private const int RegistroTiempoHall = 1;

    private static uint TiempoHall(ContextoP c) => (uint)((Math.Min(9999, (uint)c.Entero("hall_horas")) << 16) | (Math.Min(59, (uint)c.Entero("hall_minutos")) << 8) | Math.Min(59, (uint)c.Entero("hall_segundos")));

    private static PestanaP Registros(SAV3 s3)
    {
        var registros = new List<CampoP>();
        foreach (var (id, nombre) in ListaRegistros(s3))
        {
            if (id == RegistroTiempoHall) continue;
            var k = id;
            registros.Add(P.Entero($"registro:{k}", nombre, c => S(c).GetRecord(k), (c, v) => S(c).SetRecord(k, (uint)v), 0, uint.MaxValue, prioridad: 100 + k));
        }
        // El registro 1 es el tiempo de juego al entrar en el Hall de la Fama: horas << 16 | minutos << 8 | segundos (NUD_FameH/M/S del formulario). Se escribe entero cuando cambia una de las tres partes.
        var hall = new List<CampoP>
        {
            P.Entero("hall_horas", IdiomaInterfaz.T("Form.Horas"), c => Math.Min(9999u, S(c).GetRecord(RegistroTiempoHall) >> 16), (c, v) => S(c).SetRecord(RegistroTiempoHall, TiempoHall(c)), 0, 9999, grupo: "hall", prioridad: 100),
            P.Entero("hall_minutos", IdiomaInterfaz.T("Form.Minutos"), c => Math.Min(59u, (byte)(S(c).GetRecord(RegistroTiempoHall) >> 8)), (c, v) => S(c).SetRecord(RegistroTiempoHall, TiempoHall(c)), 0, 59, grupo: "hall", prioridad: 100),
            P.Entero("hall_segundos", IdiomaInterfaz.T("Form.Segundos"), c => Math.Min(59u, (byte)S(c).GetRecord(RegistroTiempoHall)), (c, v) => S(c).SetRecord(RegistroTiempoHall, TiempoHall(c)), 0, 59, grupo: "hall", prioridad: 100),
        };
        return new PestanaP("registros", L("Tab_Records"),
        [
            new SeccionP(IdiomaInterfaz.T("Form.Varios3.Registro.FIRST_HOF_PLAY_TIME"), hall),
            new SeccionP(L("Tab_Records"), registros),
        ]);
    }

    // ---------------------------------------------------------------- Otros (Tab_Other)
    private static PestanaP Otros()
    {
        static ushort PidBajo(ContextoP c) => BinaryPrimitives.ReadUInt16LittleEndian(S(c).LargeBlock.PartyBuffer);
        return new PestanaP("otros", L("Tab_Other"),
        [
            new SeccionP(string.Empty,
            [
                // B_ForceMirageIsland_Click: la Isla Espejismo aparece cuando el valor 0x24 coincide con la mitad baja del PID del primer Pokemon del equipo.
                P.Accion("isla_espejismo", L("B_ForceMirageIsland"), c => S(c).SetWork(0x24, PidBajo(c)), textoBoton: IdiomaInterfaz.T("Form.Varios3.HacerAparecer"),
                    motivo: c => S(c).SmallBlock is not ISaveBlock3SmallHoenn ? IdiomaInterfaz.F("Form.Varios3.SoloHoenn", NombresJuego.Corto(GameVersion.R), NombresJuego.Corto(GameVersion.S), NombresJuego.Corto(GameVersion.E)) : S(c).GetWork(0x24) == PidBajo(c) ? IdiomaInterfaz.T("Form.Varios3.EspejismoYa") : null,
                    ayuda: IdiomaInterfaz.T("Form.Varios3.EspejismoAyuda")),
            ]),
        ]);
    }
}
