// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen4;

/// <summary>
/// PK-SAV-019, 026, 036 a 045: <c>SAV_Misc4</c>, el editor de «Datos del Entrenador» de la 4.ª generación (Diamante, Perla, Platino, HeartGold y SoulSilver). En WinForms es una ventana de ocho pestañas, algunas solo para ciertos juegos:
/// Inicio (fichas, PB, mapa del Pokégear y marcas del Subsuelo, destinos de vuelo, Pokétch con su Dot Artist, puntos del Pokéathlon), Frente Batalla (fotos, rachas, Sala y Castillo; Diamante y Perla solo tienen la Torre), Pokéwalker (HG/SS),
/// Sellos, Caja Corazón (accesorios y fondos), Pokochos (D/P/Pt), Pokégear (HG/SS) y Registros. Al guardar, <c>B_Save_Click</c> escribe en este orden: fichas, PB, destinos de vuelo, Pokétch (D/P/Pt) o Pokéwalker, Pokéathlon y mapa (HG/SS), sellos,
/// accesorios, fondos, registros, y después el Pokégear o los Pokochos; el Frente Batalla, el Pokégear y los Pokochos actúan sobre el guardado nada más tocarlos.
/// <para>Cómo se reparten las pestañas en el móvil: una pestaña enseña o secciones, o una tabla, o una lista de elementos, nunca dos a la vez. Por eso «Inicio» son varias pestañas (los destinos de vuelo, las aplicaciones del Pokétch y las
/// rutas del Pokéwalker son tablas con su «marcar todos»), los sellos y los accesorios son tablas con sus botones y los registros son dos tablas (32 y 16 bits). Los botones «Obtener todos/Quitar todos» de las tablas cambian el borrador, como
/// los botones del formulario cambian la rejilla hasta Guardar.</para>
/// <para>Divergencias declaradas en el ADR 0065: <c>SaveMain</c> reescribe TODO al guardar (recorta fichas y PB, normaliza el mapa «inválido» a Johto y Kanto, pone la cuenta de aplicaciones del Pokétch, ajusta cada sello a 99...) y la app solo
/// lo que cambia; los registros se leen sobre una copia para no tocar el cifrado del guardado al abrir.</para>
/// </summary>
internal static partial class VariosGen4
{
    public const string Form = "SAV_Misc4";
    public static readonly string[] Censo = ["PK-SAV-019", "PK-SAV-026", "PK-SAV-036", "PK-SAV-037", "PK-SAV-038", "PK-SAV-039", "PK-SAV-040", "PK-SAV-041", "PK-SAV-042", "PK-SAV-043", "PK-SAV-044", "PK-SAV-045"];

    private static string L(string control) => EtiquetasLang.Requerir(Form, control);
    private static SAV4 S(ContextoP c) => (SAV4)c.Sav;

    /// <summary>Predicado barato de <see cref="Crear"/>: <c>B_OpenMiscEditor</c> en todo guardado de la 4.ª generación de consola portátil (D/P/Pt/HG/SS).</summary>
    public static bool Aplica(SaveFile sav) => sav is SAV4;

    public static EditorDefP? Crear(SaveFile sav)
    {
        if (sav is not SAV4 s4) return null;
        var pestanas = new List<PestanaP> { Inicio(s4), DestinosDeVuelo(s4) };
        if (s4 is SAV4Sinnoh) { pestanas.Add(Poketch()); pestanas.Add(AplicacionesPoketch()); }
        if (s4 is not SAV4DP) pestanas.Add(Fotos(s4));
        pestanas.Add(FrenteBatalla(s4));
        if (s4 is SAV4HGSS) { pestanas.Add(Pokewalker()); pestanas.Add(RutasPokewalker()); }
        pestanas.Add(Sellos());
        pestanas.Add(Accesorios());
        pestanas.Add(Fondos());
        if (s4 is SAV4Sinnoh) { pestanas.Add(Pokochos()); pestanas.Add(TodosLosPokochos()); }
        if (s4 is SAV4HGSS) { pestanas.Add(Pokegear()); pestanas.Add(TodosLosContactos()); }
        pestanas.Add(Registros32());
        pestanas.Add(Registros16());
        return new EditorDefP("misc", TituloGen4.De("misc"), pestanas, Censo, IdiomaInterfaz.T("Partida.Especifico.misc.Descripcion"));
    }

    // ---------------------------------------------------------------- Inicio (TAB_Main)
    private static string[] MapasHgss => [IdiomaInterfaz.T("VariosGen4.MapaJohto"), IdiomaInterfaz.T("VariosGen4.MapaJohto2"), IdiomaInterfaz.T("VariosGen4.MapaJohtoKanto")];

    private static PestanaP Inicio(SAV4 s4)
    {
        // ReadMain: las fichas y los PB se recortan al abrir (el maximo de las fichas es el del juego); SaveMain los escribe primero.
        var general = new List<CampoP>
        {
            P.Entero("fichas", L("L_Coin"), c => Math.Clamp(S(c).Coin, 0, (uint)S(c).MaxCoins), (c, v) => S(c).Coin = (uint)v, 0, s4.MaxCoins, prioridad: 0),
            P.Entero("pb", L("L_BP"), c => Math.Clamp(S(c).BP, 0, 9999), (c, v) => S(c).BP = (int)v, 0, 9999, prioridad: 1),
        };
        if (s4 is SAV4HGSS)
        {
            // El «mapa actual» solo existe en HG/SS; un valor no valido (3) se enseña como Johto y Kanto, que es lo que el formulario guarda.
            general.Add(P.Opcion("mapa", L("L_CurrentMap"), c => { var m = ((SAV4HGSS)c.Sav).MapUnlockState; return (long)(m >= MapUnlockState4.Invalid ? MapUnlockState4.JohtoKanto : m); },
                (c, v) => ((SAV4HGSS)c.Sav).MapUnlockState = (MapUnlockState4)v, _ => P.Lista(MapasHgss), titulo: L("L_CurrentMap"), prioridad: 40));
            general.Add(P.Entero("puntos_atleta", L("L_PokeathlonPoints"), c => Math.Min(((SAV4HGSS)c.Sav).Pokeathlon.Points, 9_999_999u), (c, v) => ((SAV4HGSS)c.Sav).Pokeathlon.Points = (uint)v, 0, Pokeathlon4.MaxPoints, prioridad: 30,
                ayuda: IdiomaInterfaz.T("VariosGen4.CoreGuardaMucho")));
        }
        else
        {
            general.Add(P.Entero("marcas_ug", L("L_UGFlags"), c => Math.Clamp(((SAV4Sinnoh)c.Sav).UG_FlagsCaptured, 0, SAV4Sinnoh.UG_MAX), (c, v) => ((SAV4Sinnoh)c.Sav).UG_FlagsCaptured = (uint)v, 0, SAV4Sinnoh.UG_MAX, prioridad: 20,
                ayuda: IdiomaInterfaz.T("VariosGen4.BanderasCapturadasSubsuelo")));
        }
        return new PestanaP("inicio", L("TAB_Main"), [new SeccionP(string.Empty, general)]);
    }

    // ---------------------------------------------------------------- Destinos de vuelo (GB_FlyDest)
    private const int FlyFlagStart = 2480;
    private static ReadOnlySpan<byte> FlyWorkFlagSinnoh => [000, 001, 002, 003, 004, 005, 006, 007, 008, 009, 010, 011, 012, 013, 014, 015, 016, 017, 067, 068];
    private static ReadOnlySpan<byte> LocationIDsSinnoh => [001, 002, 003, 004, 005, 082, 083, 006, 007, 008, 009, 010, 011, 012, 013, 014, 054, 081, 055, 015];
    private static ReadOnlySpan<byte> FlyWorkFlagHGSS => [000, 001, 002, 003, 004, 005, 006, 007, 008, 009, 010, 011, 012, 013, 014, 015, 016, 017, 018, 019, 020, 021, 022, 027, 030, 033, 035];
    private static ReadOnlySpan<byte> LocationIDsHGSS => [138, 139, 140, 141, 142, 143, 144, 145, 146, 147, 148, 126, 127, 128, 129, 130, 131, 132, 133, 134, 135, 136, 137, 229, 227, 221, 225];

    private static PestanaP DestinosDeVuelo(SAV4 s4)
    {
        var sinnoh = s4 is SAV4Sinnoh;
        var banderas = sinnoh ? FlyWorkFlagSinnoh.ToArray() : FlyWorkFlagHGSS.ToArray();
        var lugares = sinnoh ? LocationIDsSinnoh.ToArray() : LocationIDsHGSS.ToArray();
        var tabla = new TablaDefP
        {
            Id = "vuelo", SinNumero = true, Marcador = IdiomaInterfaz.T("Form.Trainer7.BuscarDestino"),
            Filas = c => [.. Enumerable.Range(0, lugares.Length).Select(i => new FilaTablaP
            {
                Id = i, Nombre = c.Textos.Gen4.Met0[lugares[i]], EsMarca = true,
                Leer = x => S(x).GetEventFlag(FlyFlagStart + banderas[i]), Escribir = (x, v) => S(x).SetEventFlag(FlyFlagStart + banderas[i], (bool)v),
            })],
            Acciones = [new AccionTablaP("todos", L("B_AllFlyDest"), (_, filas) => [.. filas.Select(f => (f, (object)true))], IdiomaInterfaz.T("VariosGen4.MarcaTodosDestinosVuelo"))],
        };
        return new PestanaP("vuelo", L("GB_FlyDest"), [], Tabla: tabla);
    }

    // ---------------------------------------------------------------- Pokétch (GB_Poketch, solo D/P/Pt)
    private static PestanaP AplicacionesPoketch()
    {
        var tabla = new TablaDefP
        {
            Id = "apps", SinNumero = true, Marcador = IdiomaInterfaz.T("VariosGen4.BuscarAplicacion"),
            Filas = c => [.. Enumerable.Range(0, (int)PoketchApp.Alarm_Clock + 1).Select(i => new FilaTablaP
            {
                Id = i, Nombre = $"{i:00} - {c.Textos.poketchapps[i]}", EsMarca = true,
                Leer = x => ((SAV4Sinnoh)x.Sav).GetPoketchAppUnlocked((PoketchApp)i),
                Escribir = (x, v) => { var s = (SAV4Sinnoh)x.Sav; s.SetPoketchAppUnlocked((PoketchApp)i, (bool)v); s.PoketchUnlockedCount = (byte)Enumerable.Range(0, (int)PoketchApp.Alarm_Clock + 1).Count(k => s.GetPoketchAppUnlocked((PoketchApp)k)); },
            })],
            Acciones = [new AccionTablaP("todas", L("B_AllPoketch"), (_, filas) => [.. filas.Select(f => (f, (object)true))], IdiomaInterfaz.F("VariosGen4.ObtieneTodasAplicaciones", NombreReloj.Oficial))],
        };
        return new PestanaP("apps", IdiomaInterfaz.F("Gen4.AppsDel", L("GB_Poketch")), [], Tabla: tabla);
    }

    // ---------------------------------------------------------------- Registros (Tab_Records)
    /// <summary>Los registros se LEEN sobre una copia: leer un <c>Record4</c> descifra su bloque en el sitio y solo <c>EndAccess</c> lo vuelve a cifrar, así que abrir el formulario no debe tocar el guardado.</summary>
    private static Record4 Lector(SAV4 s) => new(s, s.Records.Data.ToArray());

    private static void EscribirRegistro(SAV4 s, Action<Record4> escribe) { var r = s.Records; escribe(r); r.EndAccess(); }

    private static PestanaP Registros32()
    {
        var tabla = new TablaDefP
        {
            Id = "registros", SinNumero = true, Marcador = IdiomaInterfaz.T("VariosGen4.BuscarRecord"), Ayuda = IdiomaInterfaz.T("VariosGen4.ValoresRecortanMaximoCada"),
            Filas = c =>
            {
                var r = Lector(S(c));
                return [.. Enumerable.Range(0, r.Record32).Select(i => new FilaTablaP
                {
                    Id = i, Nombre = $"{L("L_Record32")} {i}", Min = 0, Max = r.GetMax32(i), Detalle = IdiomaInterfaz.F("VariosGen4.MaximoN0", r.GetMax32(i)),
                    Leer = x => (long)Lector(S(x)).GetRecord32(i), Escribir = (x, v) => EscribirRegistro(S(x), rec => rec.SetRecord32(i, (uint)(long)v)),
                })];
            },
        };
        return new PestanaP("registros", L("Tab_Records"), [], Tabla: tabla);
    }

    private static PestanaP Registros16()
    {
        var tabla = new TablaDefP
        {
            Id = "registros16", SinNumero = true, Marcador = IdiomaInterfaz.T("VariosGen4.BuscarRecord"), Ayuda = IdiomaInterfaz.T("VariosGen4.ValoresRecortanMaximoCada"),
            Filas = c =>
            {
                var r = Lector(S(c));
                return [.. Enumerable.Range(0, Record4.Record16).Select(i => new FilaTablaP
                {
                    Id = i, Nombre = $"{L("L_Record16")} {i}", Min = 0, Max = r.GetMax16(i), Detalle = IdiomaInterfaz.F("VariosGen4.MaximoN0", r.GetMax16(i)),
                    Leer = x => (long)Lector(S(x)).GetRecord16(i), Escribir = (x, v) => EscribirRegistro(S(x), rec => rec.SetRecord16(i, (ushort)(long)v)),
                })];
            },
        };
        return new PestanaP("registros16", $"{L("Tab_Records")} (16 bits)", [], Tabla: tabla);
    }
}
