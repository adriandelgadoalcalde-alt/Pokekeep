// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;
using Pokekeep.Mobile.Ui.Partida.Formularios.Gen1a3;
using static Pokekeep.Mobile.Ui.Partida.TextosPkhex;

namespace Pokekeep.Mobile.Ui.Partida.Catalogos;

/// <summary>
/// Entrenador de la 7.ª generacion (<c>SAV_Trainer7</c>: Sol/Luna y Ultra Sol/Ultra Luna): General, Mapa, Arbol de Combate, Varios y Ultra, mas las listas de destinos de vuelo, mapa desenmascarado,
/// sellos y estilos de lanzamiento, con la misma lista de datos y el mismo orden de escritura que <c>SAV_Trainer7.Save()</c>. Cada campo se escribe solo si cambio.
/// </summary>
internal static class Entrenador7
{
    public static readonly string[] Censo = ["PK-SAV-138", "PK-SAV-139", "PK-SAV-140", "PK-SAV-141", "PK-SAV-142", "PK-SAV-143", "PK-SAV-231"];
    private static string T(string c, string? alt = null) => Texto($"SAV_Trainer7.{c}", alt);
    private static SAV7 S(ContextoP c) => (SAV7)c.Sav;
    private static int SkipFlag(SaveFile s) => s is SAV7USUM ? 4160 : 3200;   // FlagMax - 768

    private static readonly int[] FlyDestNameIndex =
    [
        -1,24,34,8,20,38,12,46,40,30,                    // Melemele
        70,68,78,86,74,104,82,58,90,72,76,92,62,         // Akala
        132,136,138,114,118,144,130,154,140,             // Ula-Ula
        172,184,180,174,176,156,186,                     // Poni
        188,-1,-1,
        198,202,110,204,                                 // Playa
    ];
    private static readonly int[] FlyDestFlagOfs =
    [
        44,43,45,40,41,49,42,47,46,48,
        50,54,39,57,51,55,59,52,58,53,61,60,56,
        62,66,67,64,65,273,270,37,38,
        69,74,72,71,276,73,70,
        75,332,334,
        331,333,335,336,
    ];
    private static readonly int[] MapUnmaskNameIndex =
    [
        6,8,24,-1,18,-1,20,22,12,10,14,
        70,50,68,52,74,54,56,58,60,72,62,64,
        132,192,106,108,122,112,114,126,116,118,120,154,
        172,158,160,162,164,166,168,170,
        188,
        198,202,110,204,
    ];
    private static readonly int[] MapUnmaskFlagOfs =
    [
        5,76,82,91,79,84,80,81,77,78,83,
        19,10,18,11,21,12,13,14,15,20,16,17,
        33,34,30,31,98,92,93,94,95,96,97,141,
        173,144,145,146,147,148,149,172,
        181,
        409,297,32,296,
    ];
    private static string[] DestinosAlternativos => [IdiomaInterfaz.T("Form.Trainer7.Casa"), IdiomaInterfaz.T("Form.Trainer7.ClubHauoli"), IdiomaInterfaz.T("Form.Trainer7.ClubKonikoni")];
    private static string[] MapaAlternativos => [IdiomaInterfaz.T("Form.Trainer7.MarEste"), IdiomaInterfaz.T("Form.Trainer7.MarOeste")];

    /// <summary>Las 24 horas de Alola de <c>GetAlolaTimeList</c>: el desfase 0 es la hora del Sol (24 h), el 12 es la hora de la Luna y el resto +i horas.</summary>
    private static IReadOnlyList<OpcionP> TiempoAlola() => [.. Enumerable.Range(0, 24).Select(i => new OpcionP(i == 0 ? 24 * 3600 : i * 3600, i == 0 ? IdiomaInterfaz.T("Form.Trainer7.HoraSol") : i == 12 ? IdiomaInterfaz.T("Form.Trainer7.HoraLuna") : IdiomaInterfaz.P("Form.Trainer7.HorasOffset", i)))];

    private static IReadOnlyList<OpcionP> Estilos(bool usum) => [.. System.Enum.GetValues<PlayerBattleStyle7>().Where(v => usum || v != PlayerBattleStyle7.Nihilist).Select(v => new OpcionP((int)v, TextosPkhex.Enum(v)))];

    public static EditorDefP Crear(SAV7 sav)
    {
        var usum = sav is SAV7USUM;
        var estilos = Estilos(usum);
        var pestanas = new List<PestanaP>();

        // ---- General ----
        pestanas.Add(new("general", T("Tab_Overview"),
        [
            new(IdiomaInterfaz.T("Form.Entrenador"),
            [
                P.Opcion("juego", IdiomaInterfaz.T("Form.Juego"), c => (long)S(c).Version, (c, v) => S(c).Version = (GameVersion)v, c => P.Lista(c.Datos.VersionDataSource.Where(x => x.Value is >= 30 and <= 33))),
                P.Opcion("genero", IdiomaInterfaz.T("Form.Genero"), c => S(c).Gender, (c, v) => S(c).Gender = (byte)v, _ => AyudasGen3.Generos,
                    deriva: c => new Dictionary<string, object?> { ["piel"] = (c.Entero("piel") & ~1L) | (c.Entero("genero") & 1) }),
                P.Texto("ot", IdiomaInterfaz.T("Form.NombreEntrenador"), c => S(c).OT, (c, v) => { if (S(c).OT != v) S(c).OT = v; }, maxFn: c => c.Sav.MaxStringLengthTrainer),
                .. IdsP.Campos(sav),
                P.Entero("dinero", IdiomaInterfaz.T("Form.Dinero"), c => S(c).Money, (c, v) => S(c).Money = (uint)v, 0, 9_999_999, ayuda: IdiomaInterfaz.T("Form.Trainer7.MaxDinero")),
                P.Entero("pb", T("L_BP"), c => S(c).Misc.BP, (c, v) => S(c).Misc.BP = (uint)v, 0, 9999),
                P.Entero("festimonedas", T("L_FC"), c => S(c).Festa.FestaCoins, (c, v) => S(c).Festa.FestaCoins = (int)v, 0, 9_999_999),
            ]),
            new(IdiomaInterfaz.T("Form.RegionLengua"),
            [
                P.Opcion("pais", T("L_Country"), c => S(c).Country, (c, v) => S(c).Country = (byte)v, _ => Entrenador6.Paises(), reinicia: ["region"]),
                P.Opcion("region", T("L_Region"), c => S(c).Region, (c, v) => S(c).Region = (byte)v, c => Entrenador6.Subregiones((int)c.Entero("pais")), prioridad: 1),
                P.Opcion("consola", T("L_3DSReg"), c => S(c).ConsoleRegion, (c, v) => S(c).ConsoleRegion = (byte)v, c => P.Lista(c.Datos.Regions)),
                P.Opcion("idioma", T("L_Language"), c => S(c).Language, (c, v) => S(c).Language = (int)v, c => P.Lista(c.Datos.LanguageDataSource(7, EntityContext.Gen7))),
                new CampoP
                {
                    Id = "horaalola", Etiqueta = T("L_AlolaTime"), Tipo = TipoP.Opcion, TituloSelector = T("L_AlolaTime"),
                    Leer = c => { var t = S(c).GameTime.AlolaTime; return (long)(t == 0 ? 24 * 3600 : t); },
                    Escribir = (c, v) => S(c).GameTime.AlolaTime = (ulong)(long)v!,
                    Opciones = _ => TiempoAlola(),
                    Motivo = c => S(c).GameTime.AlolaTime == 9_999_999 ? IdiomaInterfaz.T("Form.Trainer7.AlolaAusente") : null,
                },
            ]),
            new(IdiomaInterfaz.T("Form.Trainer7.Aventura"), [.. Aventura()]),
        ]));

        // ---- Mapa y medallas ----
        pestanas.Add(new("mapa", T("Tab_BadgeMap"),
        [
            new(T("GB_Map"),
            [
                P.Entero("mapa", T("L_CurrentMap"), c => S(c).Situation.M, (c, v) => S(c).Situation.M = (int)v, 0, 1000, grupo: "mapa"),
                P.Decimal("x", T("L_X"), c => S(c).Situation.X / 60.0, (c, v) => S(c).Situation.X = (float)(v * 60), -99_999_999, 99_999_999, decimales: 6, grupo: "mapa"),
                P.Decimal("z", T("L_Z"), c => S(c).Situation.Z / 60.0, (c, v) => S(c).Situation.Z = (float)(v * 60), -99_999_999, 99_999_999, decimales: 6, grupo: "mapa"),
                P.Decimal("y", T("L_Y"), c => S(c).Situation.Y / 60.0, (c, v) => S(c).Situation.Y = (float)(v * 60), -99_999_999, 99_999_999, decimales: 6, grupo: "mapa"),
                new CampoP
                {
                    Id = "rotacion", Etiqueta = T("L_R"), Tipo = TipoP.Decimal, Min = -99_999_999, Max = 99_999_999, Decimales = 6, Prioridad = 5, GrupoEscritura = "mapa",
                    Leer = c => Math.Round(Math.Atan2(S(c).Situation.RZ, S(c).Situation.RW) * 360.0 / Math.PI, 6),
                    Escribir = (c, v) =>
                    {
                        var angle = (double)v! * Math.PI / 360.0;
                        var sit = S(c).Situation;
                        sit.RX = 0; sit.RZ = (float)Math.Sin(angle); sit.RY = 0; sit.RW = (float)Math.Cos(angle);
                        sit.UpdateOverworldCoordinates();
                    },
                },
            ]),
        ]));

        // ---- Arbol de Combate ----
        var arbol = new List<CampoP>();
        string[] modos = [IdiomaInterfaz.T("Form.Trainer7.Modo0"), IdiomaInterfaz.T("Form.Trainer7.Modo1"), IdiomaInterfaz.T("Form.Trainer7.Modo2")];
        foreach (var (super, nombre) in new[] { (false, IdiomaInterfaz.T("Form.Trainer7.Regular")), (true, IdiomaInterfaz.T("Form.Trainer7.Super")) })
            foreach (var (max, rotulo) in new[] { (false, IdiomaInterfaz.T("Form.Trainer7.Actual")), (true, IdiomaInterfaz.T("Form.Trainer7.Maxima")) })
                for (var i = 0; i < 3; i++)
                {
                    var tipo = i; var sup = super; var mx = max;
                    arbol.Add(P.Entero($"arbol_{(super ? "s" : "r")}{(max ? "m" : "c")}{i}", IdiomaInterfaz.F("Form.Trainer7.Racha", nombre, rotulo, modos[i].ToLowerInvariant()), c => S(c).BattleTree.GetTreeStreak(tipo, sup, mx), (c, v) => S(c).BattleTree.SetTreeStreak((int)v, tipo, sup, mx), 0, 65535));
                }
        pestanas.Add(new("arbol", T("Tab_BattleTree"), [new(IdiomaInterfaz.T("Form.Trainer7.Rachas"), arbol)]));

        // ---- Varios ----
        var varios = new List<SeccionP>
        {
            new(IdiomaInterfaz.T("Form.Trainer7.Pokevisor"),
            [
                P.Entero("snap", T("L_SnapCount"), c => S(c).PokeFinder.SnapCount, (c, v) => S(c).PokeFinder.SnapCount = (uint)v, 0, 9_999_999),
                P.Entero("pulgarestotal", T("L_ThumbsTotal"), c => S(c).PokeFinder.ThumbsTotalValue, (c, v) => S(c).PokeFinder.ThumbsTotalValue = (uint)v, 0, 9_999_999),
                new CampoP { Id = "pulgaresrecord", Etiqueta = T("L_ThumbsRecord"), Tipo = TipoP.Entero, Min = 0, Max = 9_999_999, Leer = c => (long)S(c).PokeFinder.ThumbsHighValue, Escribir = (c, v) => S(c).PokeFinder.ThumbsHighValue = (uint)(long)v!, Prioridad = 1, Deriva = c => new Dictionary<string, object?> { ["pulgarestotal"] = Math.Max(c.Entero("pulgarestotal"), c.Entero("pulgaresrecord")) }, Ayuda = IdiomaInterfaz.T("Form.Trainer7.PulgaresAyuda") },
                P.Opcion("camara", T("L_CameraVersion"), c => Math.Min(4, (int)S(c).PokeFinder.CameraVersion), (c, v) => S(c).PokeFinder.CameraVersion = (ushort)v, _ => P.Lista("1", "2", "3", "4", "5")),
                P.Marca("giro", T("CHK_Gyro"), c => S(c).PokeFinder.GyroFlag, (c, v) => S(c).PokeFinder.GyroFlag = v),
            ]),
            new(IdiomaInterfaz.T("Form.Trainer7.AspectoLanzamiento"),
            [
                P.Opcion("piel", T("L_SkinColor"), c => S(c).MyStatus.DressUpSkinColor, (c, v) => S(c).MyStatus.DressUpSkinColor = (int)v, _ => EnumLista<PlayerSkinColor7>(),
                    ayuda: IdiomaInterfaz.T("Form.Trainer7.PielAyuda"),
                    prioridad: 3),
                P.Info("avisopiel", IdiomaInterfaz.T("Form.Trainer7.Aviso"), _ => 0L, (_, _) => IdiomaInterfaz.T("Form.Trainer7.PielAviso"), visible: c => (c.Entero("piel") & 1) != (c.Entero("genero") & 1)),
                P.Opcion("lanzamiento", T("L_BallThrowType"), c => (sbyte)S(c).MyStatus.BallThrowType, (c, v) => S(c).MyStatus.BallThrowType = (byte)v, _ => estilos),
            ]),
            new(IdiomaInterfaz.T("Form.Trainer7.Desbloqueos"),
            [
                P.Marca("superindiv", T("CHK_UnlockSuperSingles"), c => S(c).EventWork.GetEventFlag(333), (c, v) => S(c).EventWork.SetEventFlag(333, v)),
                P.Marca("superdobles", T("CHK_UnlockSuperDoubles"), c => S(c).EventWork.GetEventFlag(334), (c, v) => S(c).EventWork.SetEventFlag(334, v)),
                P.Marca("supermulti", T("CHK_UnlockSuperMulti"), c => S(c).EventWork.GetEventFlag(335), (c, v) => S(c).EventWork.SetEventFlag(335, v)),
                P.Marca("mega", T("CHK_UnlockMega"), c => S(c).MyStatus.MegaUnlocked, (c, v) => S(c).MyStatus.MegaUnlocked = v),
                P.Marca("zmov", T("CHK_UnlockZMove"), c => S(c).MyStatus.ZMoveUnlocked, (c, v) => S(c).MyStatus.ZMoveUnlocked = v),
            ]),
            new(IdiomaInterfaz.T("Form.Trainer7.Moda"),
            [
                P.Opcion("moda", T("B_Fashion", IdiomaInterfaz.T("Form.Trainer7.Moda")), c => 0, (c, v) => CargarModa(S(c), (int)v), _ => P.Lista(IdiomaInterfaz.T("Form.Trainer7.SinCambios"), IdiomaInterfaz.T("Form.Trainer7.JuegoNuevo"), IdiomaInterfaz.T("Form.Trainer7.TodosLegales"), IdiomaInterfaz.T("Form.Trainer7.Todo")), prioridad: 9, soloAccion: true,
                    ayuda: IdiomaInterfaz.T("Form.Trainer7.ModaAyuda")),
            ]),
            new(IdiomaInterfaz.T("Form.Trainer7.PlazaOtros"),
            [
                P.Texto("plaza", T("L_PlazaName"), c => S(c).Festa.FestivalPlazaName, (c, v) => S(c).Festa.FestivalPlazaName = v, 20),
                P.Opcion("vivillon", T("L_Vivillon", "Vivillon"), c => S(c).Misc.Vivillon, (c, v) => S(c).Misc.Vivillon = (int)v, c => P.Lista(FormConverter.GetFormList((int)Species.Vivillon, c.Textos.types, c.Textos.forms, GameInfo.GenderSymbolUnicode, EntityContext.Gen7)), titulo: IdiomaInterfaz.F("Form.FormaVivillon", Ajustes.Pkhex.ProgramaPkhex.Textos.specieslist[(int)Species.Vivillon])),
                P.Entero("dias", T("L_DaysFromRefreshed"), c => S(c).Misc.DaysFromRefreshed, (c, v) => S(c).Misc.DaysFromRefreshed = (byte)v, 0, 255),
            ]),
        };
        pestanas.Add(new("varios", T("Tab_Misc"), varios));

        // ---- Ultra ----
        if (usum)
            pestanas.Add(new("ultra", T("Tab_Ultra"),
            [
                new(T("GB_Surf"), [.. Enumerable.Range(0, 4).Select(i => P.Entero($"surf{i}", T($"L_Surf{i}"), c => S(c).Misc.GetSurfScore(i), (c, v) => S(c).Misc.SetSurfScore(i, (int)v), 0, 999_999_999))]),
                new(T("GB_Rotom"),
                [
                    P.Texto("rotomot", T("L_RotomOT"), c => S(c).FieldMenu.RotomOT, (c, v) => S(c).FieldMenu.RotomOT = v, 12),
                    P.Entero("rotomafecto", T("L_RotomAffection"), c => S(c).FieldMenu.RotomAffection, (c, v) => S(c).FieldMenu.RotomAffection = (ushort)v, 0, 9999),
                    P.Marca("loto1", T("CHK_RotoLoto1"), c => S(c).FieldMenu.RotomLoto1, (c, v) => S(c).FieldMenu.RotomLoto1 = v),
                    P.Marca("loto2", T("CHK_RotoLoto2"), c => S(c).FieldMenu.RotomLoto2, (c, v) => S(c).FieldMenu.RotomLoto2 = v),
                ]),
            ]));

        // ---- Listas ----
        pestanas.Add(new("sellos", T("L_Stamps", IdiomaInterfaz.T("Form.Trainer7.Sellos")), [], Tabla: new TablaDefP
        {
            Id = "sellos", Marcador = IdiomaInterfaz.T("Form.Trainer7.BuscarSello"),
            Filas = c => [.. System.Enum.GetValues<Stamp7>().Select((v, i) => { var bit = i; return new FilaTablaP { Id = bit, Nombre = TextosPkhex.Enum(v), EsMarca = true, Leer = x => (S(x).Misc.Stamps & (1u << bit)) != 0, Escribir = (x, val) => { var m = S(x).Misc; m.Stamps = (bool)val ? m.Stamps | (1u << bit) : m.Stamps & ~(1u << bit); } }; })],
        }));
        // WinForms swaps these destinations for Moon/Ultra Moon; never mutate the shared index array.
        var destinos = FlyDestNameIndex.ToArray();
        if (sav.Version is GameVersion.MN or GameVersion.UM) { destinos[28] = 142; destinos[36] = 178; }
        pestanas.Add(new("vuelo", T("GB_FlyDest"), [], Tabla: BanderasLugares("vuelo", IdiomaInterfaz.T("Form.Trainer7.BuscarDestino"), destinos, FlyDestFlagOfs, DestinosAlternativos, usum ? 0 : 6)));
        pestanas.Add(new("mapamasc", T("GB_MapUnmask"), [], Tabla: BanderasLugares("mapamasc", IdiomaInterfaz.T("Form.Trainer7.BuscarZona"), MapUnmaskNameIndex, MapUnmaskFlagOfs, MapaAlternativos, usum ? 0 : 4)));
        if (sav is SAV7SM)
        {
            pestanas.Add(new("lanzdesb", IdiomaInterfaz.T("Form.Trainer7.LanzamientosDesbloqueados"), [], Tabla: BanderasLanzamiento("lanzdesb", 292, 2, estilos, soloPrimeras: 2)));
            pestanas.Add(new("lanzapr", IdiomaInterfaz.T("Form.Trainer7.LanzamientosAprendidos"), [], Tabla: BanderasLanzamiento("lanzapr", 3479, 1, estilos, soloPrimeras: 1)));
        }
        pestanas.Add(new("registros", IdiomaInterfaz.T("Form.Estadisticas"), [], Tabla: TablasP.Registros(RecordLists.RecordList_7)));
        return new EditorDefP("entrenador7", IdiomaInterfaz.T("Form.Entrenador"), pestanas, Censo, IdiomaInterfaz.T("Form.Trainer7.Descripcion"));
    }

    /// <summary>Lo que hace <c>B_Fashion_Click</c>: vaciar el bloque y escribir la carga (juego nuevo, todos los legales o todo) segun el juego y el genero.</summary>
    private static void CargarModa(SAV7 sav, int modo)
    {
        if (modo is < 1 or > 3) return;
        sav.Fashion.Clear();
        if (modo == 1) { sav.Fashion.Reset(); return; }
        var uu = sav is SAV7USUM ? "uu" : "sm";
        sav.Fashion.ImportPayload(TextosPkhex.Datos($"fashion_{(sav.Gender == 0 ? "m" : "f")}_{uu}{(modo == 3 ? "_illegal" : string.Empty)}"));
    }

    private static IEnumerable<CampoP> Aventura()
    {
        yield return P.Entero("horas", IdiomaInterfaz.T("Form.Trainer7.Horas"), c => S(c).PlayedHours, (c, v) => S(c).PlayedHours = (ushort)v, 0, 65535, grupo: "tiempo");
        yield return P.Entero("minutos", IdiomaInterfaz.T("Form.Minutos"), c => S(c).PlayedMinutes, (c, v) => S(c).PlayedMinutes = (ushort)v % 60, 0, 59, grupo: "tiempo");
        yield return P.Entero("segundos", IdiomaInterfaz.T("Form.Segundos"), c => S(c).PlayedSeconds, (c, v) => S(c).PlayedSeconds = (ushort)v % 60, 0, 59, grupo: "tiempo");
        yield return P.FechaHora("inicio", T("L_Started"), c => EntrenadorSimple.Fecha(S(c).SecondsToStart), (c, v) => S(c).SecondsToStart = (uint)EntrenadorSimple.Segundos(v));
        yield return P.FechaHora("fama", T("L_Fame"), c => EntrenadorSimple.Fecha(S(c).SecondsToFame), (c, v) => S(c).SecondsToFame = (uint)EntrenadorSimple.Segundos(v));
        yield return new CampoP
        {
            Id = "guardado", Etiqueta = T("L_LastSaved"), Tipo = TipoP.FechaHora, SinSegundos = true,
            Leer = c => S(c).Played.LastSavedDate is { } d ? new DateTime(d.Year, d.Month, d.Day, d.Hour, d.Minute, 0) : DateTime.MinValue,
            Escribir = (c, v) => { var d = (DateTime)v!; S(c).Played.LastSavedDate = new DateTime(d.Year, d.Month, d.Day, d.Hour, d.Minute, 0); },
            Visible = c => S(c).Played.LastSavedDate.HasValue,
        };
    }

    private static TablaDefP BanderasLugares(string id, string marcador, int[] nombres, int[] banderas, string[] alternativos, int omitirFinal) => new()
    {
        Id = id, Marcador = marcador,
        Filas = c =>
        {
            var lugares = c.Datos.Met.GetLocationList(GameVersion.US, EntityContext.Gen7, false);
            var l = new List<FilaTablaP>();
            for (int i = 0, u = 0, m = nombres.Length - omitirFinal; i < m; i++)
            {
                var dest = nombres[i]; var n = i;
                var nombre = dest < 0 ? alternativos[u++] : lugares.First(v => v.Value == dest).Text;
                l.Add(new FilaTablaP { Id = n, Nombre = nombre, EsMarca = true, Leer = x => S(x).EventWork.GetEventFlag(SkipFlag(x.Sav) + banderas[n]), Escribir = (x, v) => S(x).EventWork.SetEventFlag(SkipFlag(x.Sav) + banderas[n], (bool)v) });
            }
            return l;
        },
    };

    private static TablaDefP BanderasLanzamiento(string id, int inicio, int desde, IReadOnlyList<OpcionP> estilos, int soloPrimeras) => new()
    {
        Id = id, Marcador = IdiomaInterfaz.T("Form.Trainer7.BuscarEstilo"),
        Ayuda = soloPrimeras == 2 ? IdiomaInterfaz.T("Form.Trainer7.DosEstilos") : IdiomaInterfaz.T("Form.Trainer7.PrimerEstilo"),
        Filas = c => [.. estilos.Select((o, i) => { var n = i; var fijo = n < desde; return new FilaTablaP { Id = n, Nombre = o.Texto, EsMarca = true, SoloLectura = fijo, Leer = x => fijo || S(x).EventWork.GetEventFlag(inicio + n), Escribir = (x, v) => S(x).EventWork.SetEventFlag(inicio + n, (bool)v) }; })],
    };
}
