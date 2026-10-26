// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;
using Pokekeep.Mobile.Ui.Modelo;
using Pokekeep.Mobile.Ui.Partida.Formularios.Gen1a3;
using static Pokekeep.Mobile.Ui.Partida.TextosPkhex;

namespace Pokekeep.Mobile.Ui.Partida.Catalogos;

/// <summary>
/// Entrenadores de la 8.ª y 9.ª generacion de Switch con editor propio en PKHeX: Leyendas Arceus (<c>SAV_Trainer8a</c>), Diamante Brillante / Perla Reluciente (<c>SAV_Trainer8b</c>),
/// Escarlata/Purpura (<c>SAV_Trainer9</c>) y Leyendas Z-A (<c>SAV_Trainer9a</c>), con las mismas llamadas y el mismo orden que cada <c>Save()</c>.
/// </summary>
internal static class EntrenadorSwitch
{
    private static IReadOnlyList<OpcionP> Generos => AyudasGen3.Generos;

    // ------------------------------------------------------------------ Leyendas Arceus
    public static readonly string[] Censo8a = ["PK-SAV-176", "PK-SAV-177", "PK-SAV-178", "PK-SAV-231"];
    private static SAV8LA LA(ContextoP c) => (SAV8LA)c.Sav;
    private static string TA(string k, string? alt = null) => Texto($"SAV_Trainer8a.{k}", alt);

    public static EditorDefP Crear8a(SAV8LA sav) => new("entrenador8a", IdiomaInterfaz.T("Form.Entrenador"),
    [
        new("general", TA("Tab_Overview", IdiomaInterfaz.T("Form.TrainerSwitch8.SAV_Trainer8a.Tab_Overview")),
        [
            new(IdiomaInterfaz.T("Form.Entrenador"),
            [
                P.Opcion("genero", IdiomaInterfaz.T("Form.Genero"), c => LA(c).Gender, (c, v) => LA(c).Gender = (byte)v, _ => Generos),
                P.Texto("ot", IdiomaInterfaz.T("Form.NombreEntrenador"), c => LA(c).OT, (c, v) => { if (LA(c).OT != v) LA(c).OT = v; }, maxFn: c => 12),
                .. IdsP.Campos(sav),
                P.Entero("dinero", IdiomaInterfaz.T("Form.Dinero"), c => LA(c).Money, (c, v) => LA(c).Money = (uint)v, 0, sav.MaxMoney, ayuda: IdiomaInterfaz.F("Form.Maximo", sav.MaxMoney)),
                P.Opcion("idioma", TA("L_Language", IdiomaInterfaz.T("Form.Trainer7b.Idioma")), c => LA(c).Language, (c, v) => LA(c).Language = (int)v, c => P.Lista(c.Datos.LanguageDataSource(8, EntityContext.Gen8a))),
            ]),
            new(IdiomaInterfaz.T("Form.Trainer7.Aventura"),
            [
                .. ComunP.Tiempo(99_999),
                P.FechaHora("inicio", TA("L_Started", IdiomaInterfaz.T("Form.Trainer7b.Inicio")), c => LA(c).AdventureStart.Timestamp, (c, v) => LA(c).AdventureStart.Timestamp = v),
                new CampoP { Id = "guardado", Etiqueta = TA("L_LastSaved", IdiomaInterfaz.T("Form.Trainer7b.Guardado")), Tipo = TipoP.FechaHora, SinSegundosFn = c => !LA(c).LastSaved.HasSeconds, Leer = c => LA(c).LastSaved.Timestamp, Escribir = (c, v) => LA(c).LastSaved.Timestamp = (DateTime)v! },
            ]),
            new(IdiomaInterfaz.T("Form.TrainerSwitch8.Expedicion"),
            [
                P.Entero("meritoact", IdiomaInterfaz.T("Form.TrainerSwitch8.MeritosActuales"), c => Valor(LA(c), SaveBlockAccessor8LA.KMeritCurrent), (c, v) => LA(c).Blocks.SetBlockValue(SaveBlockAccessor8LA.KMeritCurrent, (uint)v), 0, 999_999_999),
                P.Entero("meritotot", IdiomaInterfaz.T("Form.TrainerSwitch8.MeritosTotales"), c => Valor(LA(c), SaveBlockAccessor8LA.KMeritEarnedTotal), (c, v) => LA(c).Blocks.SetBlockValue(SaveBlockAccessor8LA.KMeritEarnedTotal, (uint)v), 0, 999_999_999),
                P.Entero("rango", IdiomaInterfaz.T("Form.TrainerSwitch8.Rango"), c => Valor(LA(c), SaveBlockAccessor8LA.KExpeditionTeamRank), (c, v) => LA(c).Blocks.SetBlockValue(SaveBlockAccessor8LA.KExpeditionTeamRank, (uint)v), 0, 999_999_999),
                P.Entero("morral", IdiomaInterfaz.T("Form.TrainerSwitch8.Morral"), c => Valor(LA(c), SaveBlockAccessor8LA.KSatchelUpgrades), (c, v) => LA(c).Blocks.SetBlockValue(SaveBlockAccessor8LA.KSatchelUpgrades, (uint)v), 0, 999_999_999),
            ]),
        ]),
        new("mapa", TA("Tab_BadgeMap", IdiomaInterfaz.T("Form.Trainer7b.Mapa")),
        [
            new(TA("GB_Map", IdiomaInterfaz.T("Form.Trainer7b.PosicionMapa")),
            [
                P.Texto("mapa", TA("L_CurrentMap", IdiomaInterfaz.T("Form.TrainerSwitch8.SAV_Trainer8a.L_CurrentMap")), c => LA(c).Coordinates.M, (c, v) => LA(c).Coordinates.M = v, 48),
                P.Decimal("x", IdiomaInterfaz.T("Form.Trainer7b.X"), c => LA(c).Coordinates.X, (c, v) => LA(c).Coordinates.X = (float)v, -99_999_999, 99_999_999, decimales: 6, grupo: "mapa"),
                P.Decimal("z", IdiomaInterfaz.T("Form.Trainer7b.Z"), c => LA(c).Coordinates.Z, (c, v) => LA(c).Coordinates.Z = (float)v, -99_999_999, 99_999_999, decimales: 6, grupo: "mapa"),
                P.Decimal("y", IdiomaInterfaz.T("Form.Trainer7b.Y"), c => LA(c).Coordinates.Y, (c, v) => LA(c).Coordinates.Y = (float)v, -99_999_999, 99_999_999, decimales: 6, grupo: "mapa"),
                new CampoP
                {
                    Id = "rotacion", Etiqueta = IdiomaInterfaz.T("Form.Trainer6.Modo3"), Tipo = TipoP.Decimal, Min = -99_999_999, Max = 99_999_999, Decimales = 6, Prioridad = 5, GrupoEscritura = "mapa",
                    Leer = c => Math.Round(Math.Atan2(LA(c).Coordinates.RZ, LA(c).Coordinates.RW) * 360.0 / Math.PI, 6),
                    Escribir = (c, v) =>
                    {
                        var angle = (double)v! * Math.PI / 360.0; var co = LA(c).Coordinates;
                        co.RX = 0; co.RZ = (float)Math.Sin(angle); co.RY = 0; co.RW = (float)Math.Cos(angle);
                    },
                },
            ]),
        ]),
    ], Censo8a, IdiomaInterfaz.F("Form.TrainerSwitch8.DescripcionLA", NombresJuego.Corto(GameVersion.PLA)));

    private static long Valor(SAV8LA s, uint k) => (long)(uint)s.Blocks.GetBlockValue(k);

    // ------------------------------------------------------------------ Diamante Brillante / Perla Reluciente
    public static readonly string[] Censo8b = ["PK-SAV-179", "PK-SAV-180", "PK-SAV-181", "PK-SAV-231"];
    private static SAV8BS BS(ContextoP c) => (SAV8BS)c.Sav;
    private static string TB(string k, string? alt = null) => Texto($"SAV_Trainer8b.{k}", alt);

    private static DateTime Seg(DateTime d) => d.AddTicks(-(d.Ticks % TimeSpan.TicksPerSecond));
    private static DateTime Revisa(DateTime original, DateTime nuevo) => Seg(nuevo).AddTicks(original.Ticks % TimeSpan.TicksPerSecond);   // conserva los ticks, como ReviseTimestamp

    public static EditorDefP Crear8b(SAV8BS sav)
    {
        var insignias = Enumerable.Range(0, 8).Select(i => P.Marca($"med{i + 1}", IdiomaInterfaz.F("Form.Medalla", i + 1), c => BS(c).FlagWork.GetSystemFlag(124 + i), (c, v) => BS(c).FlagWork.SetSystemFlag(124 + i, v)));
        return new EditorDefP("entrenador8b", IdiomaInterfaz.T("Form.Entrenador"),
        [
            new("general", TB("Tab_Overview", IdiomaInterfaz.T("Form.TrainerSwitch8.SAV_Trainer8a.Tab_Overview")),
            [
                new(IdiomaInterfaz.T("Form.Entrenador"),
                [
                    P.Opcion("juego", IdiomaInterfaz.T("Form.Juego"), c => (long)BS(c).Version, (c, v) => BS(c).Version = (GameVersion)v, c => P.Lista(c.Datos.VersionDataSource.Where(z => (GameVersion)z.Value is GameVersion.BD or GameVersion.SP))),
                    P.Opcion("genero", IdiomaInterfaz.T("Form.Genero"), c => BS(c).Gender, (c, v) => BS(c).Gender = (byte)v, _ => Generos),
                    P.Texto("ot", IdiomaInterfaz.T("Form.NombreEntrenador"), c => BS(c).OT, (c, v) => { if (BS(c).OT != v) BS(c).OT = v; }, maxFn: c => 12),
                    P.Texto("rival", TB("L_Rival", IdiomaInterfaz.T("Form.TrainerSwitch8.SAV_Trainer8b.L_Rival")), c => BS(c).RivalName, (c, v) => { if (BS(c).RivalName != v) BS(c).RivalName = v; }, maxFn: c => 12),
                    .. IdsP.Campos(sav),
                    P.Entero("dinero", IdiomaInterfaz.T("Form.Dinero"), c => BS(c).Money, (c, v) => BS(c).Money = (uint)v, 0, sav.MaxMoney, ayuda: IdiomaInterfaz.F("Form.Maximo", sav.MaxMoney)),
                    P.Entero("pb", TB("L_BP", IdiomaInterfaz.T("Form.Trainer8.BP")), c => BS(c).BattleTower.BP, (c, v) => BS(c).BattleTower.BP = (uint)v, 0, 9999),
                    P.Opcion("idioma", TB("L_Language", IdiomaInterfaz.T("Form.Trainer7b.Idioma")), c => BS(c).Language, (c, v) => BS(c).Language = (int)v, c => P.Lista(c.Datos.LanguageDataSource(8, EntityContext.Gen8b))),
                ]),
                new(IdiomaInterfaz.T("Form.Trainer7.Aventura"),
                [
                    .. ComunP.Tiempo(99_999),
                    P.FechaHora("inicio", TB("L_Started", IdiomaInterfaz.T("Form.Trainer7b.Inicio")), c => Seg(BS(c).System.LocalTimestampStart), (c, v) => BS(c).System.LocalTimestampStart = Revisa(BS(c).System.LocalTimestampStart, v)),
                    P.FechaHora("guardado", TB("L_LastSaved", IdiomaInterfaz.T("Form.Trainer7b.Guardado")), c => Seg(BS(c).System.LocalTimestampLatest), (c, v) => BS(c).System.LocalTimestampLatest = Revisa(BS(c).System.LocalTimestampLatest, v)),
                ]),
            ]),
            new("mapa", TB("Tab_BadgeMap", IdiomaInterfaz.T("Form.TrainerSwitch8.SAV_Trainer8b.Tab_BadgeMap")),
            [
                new(IdiomaInterfaz.T("Form.Medallas"), [.. insignias]),
                new(TB("GB_Map", IdiomaInterfaz.T("Form.Trainer7b.PosicionMapa")),
                [
                    P.Entero("zona", TB("L_CurrentMap", IdiomaInterfaz.T("Form.TrainerSwitch8.SAV_Trainer8b.L_CurrentMap")), c => BS(c).ZoneID, (c, v) => BS(c).ZoneID = (short)v, 0, 1000),
                    P.Entero("x", IdiomaInterfaz.T("Form.Trainer7b.X"), c => BS(c).MyStatus.X, (c, v) => BS(c).MyStatus.X = (int)v, -99_999_999, 99_999_999),
                    P.Decimal("z", IdiomaInterfaz.T("Form.TrainerSwitch8.Altura"), c => BS(c).MyStatus.Height, (c, v) => BS(c).MyStatus.Height = (float)v, -99_999_999, 99_999_999, decimales: 5),
                    P.Entero("y", IdiomaInterfaz.T("Form.Trainer7b.Y"), c => BS(c).MyStatus.Y, (c, v) => BS(c).MyStatus.Y = (int)v, -99_999_999, 99_999_999),
                    P.Decimal("rotacion", IdiomaInterfaz.T("Form.Trainer6.Modo3"), c => BS(c).MyStatus.Rotation, (c, v) => BS(c).MyStatus.Rotation = (float)v, -99_999_999, 99_999_999, decimales: 5),
                ]),
            ]),
            new("registros", IdiomaInterfaz.T("Form.Estadisticas"), [], Tabla: TablasP.Registros(Record8b.RecordList_8b)),
        ], Censo8b, IdiomaInterfaz.F("Form.TrainerSwitch8.DescripcionBDSP", NombresJuego.Corto(GameVersion.BD), NombresJuego.Corto(GameVersion.SP)))
        {
            // Save() de PKHeX: un ID todo a cero se corrige (SID 1) y, si cambia el nombre o el ID, se traslada al bloque de records ampliado.
            PrepararAplicar = c =>
            {
                var s = BS(c);
                var antes = (SAV8BS)s.Clone();
                return _ =>
                {
                    if (s is { TID16: 0, SID16: 0 }) s.SID16 = 1;
                    if (s.HasFirstSaveFileExpansion && (s.OT != antes.OT || s.TID16 != antes.TID16 || s.SID16 != antes.SID16)) s.RecordAdd.ReplaceOT(antes, s);
                };
            },
        };
    }

    // ------------------------------------------------------------------ Escarlata / Purpura
    public static readonly string[] Censo9 = ["PK-SAV-197", "PK-SAV-198", "PK-SAV-199", "PK-SAV-200", "PK-SAV-201", "PK-SAV-231"];
    private static SAV9SV SV(ContextoP c) => (SAV9SV)c.Sav;
    private static string T9(string k, string? alt = null) => Texto($"SAV_Trainer9.{k}", alt);

    public static EditorDefP Crear9(SAV9SV sav)
    {
        var pestanas = new List<PestanaP>
        {
            new("general", T9("Tab_Overview", IdiomaInterfaz.T("Cajas.General")),
            [
                new(IdiomaInterfaz.T("EntrenadorSwitch.Entrenador"),
                [
                    P.Opcion("juego", IdiomaInterfaz.T("Form.Juego"), c => (long)SV(c).Version, (c, v) => SV(c).Version = (GameVersion)v, c => P.Lista(c.Datos.VersionDataSource.Where(z => (GameVersion)z.Value is GameVersion.SL or GameVersion.VL))),
                    P.Opcion("genero", IdiomaInterfaz.T("Form.Genero"), c => SV(c).Gender, (c, v) => SV(c).Gender = (byte)v, _ => Generos),
                    P.Texto("ot", IdiomaInterfaz.T("Form.NombreEntrenador"), c => SV(c).OT, (c, v) => { if (SV(c).OT != v) SV(c).OT = v; }, maxFn: c => 12),
                    .. IdsP.Campos(sav),
                    P.Entero("dinero", IdiomaInterfaz.T("Form.Dinero"), c => SV(c).Money, (c, v) => SV(c).Money = (uint)v, 0, sav.MaxMoney, ayuda: IdiomaInterfaz.F("Form.Maximo", sav.MaxMoney)),
                    P.Entero("pl", T9("L_LP", IdiomaInterfaz.T("EntrenadorSwitch.PuntosLigaPL")), c => SV(c).LeaguePoints, (c, v) => SV(c).LeaguePoints = (uint)v, 0, 9_999_999),
                    P.Opcion("idioma", T9("L_Language", IdiomaInterfaz.T("Catalogo.Idioma")), c => SV(c).Language, (c, v) => SV(c).Language = (int)v, _ => P.Lista(GameInfo.LanguageDataSource(9, EntityContext.Gen9))),
                ]),
                new(IdiomaInterfaz.T("Form.Trainer7.Aventura"),
                [
                    .. ComunP.Tiempo(99_999),
                    new CampoP { Id = "inicio", Etiqueta = T9("L_Started", IdiomaInterfaz.T("EntrenadorSwitch.InicioAventura")), Tipo = TipoP.FechaHora, SoloFecha = true, Leer = c => SV(c).EnrollmentDate.Timestamp.Date, Escribir = (c, v) => SV(c).EnrollmentDate.Timestamp = (DateTime)v! },
                    P.FechaHora("guardado", T9("L_LastSaved", IdiomaInterfaz.T("Form.Trainer7b.Guardado")), c => SV(c).LastSaved.Timestamp, (c, v) => SV(c).LastSaved.Timestamp = v.Date.AddSeconds(v.TimeOfDay.TotalSeconds)),
                ]),
            ]),
            new("mapa", T9("Tab_MiscValues", IdiomaInterfaz.T("EntrenadorSwitch.Mapa")),
            [
                new(T9("GB_Map", IdiomaInterfaz.T("EntrenadorSwitch.PosicionMapa")),
                [
                    P.Decimal("x", IdiomaInterfaz.T("Form.Trainer7b.X"), c => SV(c).X, (c, v) => { }, -99_999_999, 99_999_999, decimales: 6, grupo: "mapa"),
                    P.Decimal("y", IdiomaInterfaz.T("Form.Trainer7b.Y"), c => SV(c).Y, (c, v) => { }, -99_999_999, 99_999_999, decimales: 6, grupo: "mapa"),
                    P.Decimal("z", IdiomaInterfaz.T("Form.Trainer7b.Z"), c => SV(c).Z, (c, v) => { }, -99_999_999, 99_999_999, decimales: 6, grupo: "mapa"),
                    new CampoP
                    {
                        Id = "rotacion", Etiqueta = IdiomaInterfaz.T("Form.Trainer6.Modo3"), Tipo = TipoP.Decimal, Min = -99_999_999, Max = 99_999_999, Decimales = 6, Prioridad = 5, GrupoEscritura = "mapa",
                        Ayuda = IdiomaInterfaz.T("EntrenadorSwitch.PKHeXLeeRotacionOtro"),
                        // SaveMap() de PKHeX: una sola llamada con las tres coordenadas y otra con la rotacion (el Core ignora diferencias menores de 0,0001).
                        Leer = c => Math.Round(Math.Atan2(SV(c).RY, SV(c).RW) * 360.0 / Math.PI, 6),
                        Escribir = (c, v) =>
                        {
                            SV(c).SetCoordinates((float)c.Decimal("x"), (float)c.Decimal("y"), (float)c.Decimal("z"));
                            var a = (double)v! * Math.PI / 360.0;
                            SV(c).SetPlayerRotation(0, (float)Math.Sin(a), 0, (float)Math.Cos(a));
                        },
                    },
                ]),
                new(IdiomaInterfaz.T("Form.Trainer7.Desbloqueos"), [
                    P.Opcion("vuelos", IdiomaInterfaz.T("EntrenadorSwitch.DestinosVuelo"), c => 0, (c, v) => { if (v == 1) Seguro(() => DesbloquearVuelos(SV(c))); }, _ => P.Lista(IdiomaInterfaz.T("Form.Trainer7.SinCambios"), IdiomaInterfaz.T("Form.Trainer7b.Desbloquear")), soloAccion: true, ayuda: IdiomaInterfaz.T("EntrenadorSwitch.MarcaTodosDestinosVuelo")),
                    P.Opcion("estacas", IdiomaInterfaz.T("EntrenadorSwitch.Estacas"), c => 0, (c, v) => { if (v == 1) Seguro(() => SV(c).CollectAllStakes()); }, _ => P.Lista(IdiomaInterfaz.T("Form.Trainer7.SinCambios"), IdiomaInterfaz.T("EntrenadorSwitch.ConseguirTodas")), soloAccion: true),
                    P.Opcion("recetasmt", IdiomaInterfaz.T("EntrenadorSwitch.RecetasDeMt"), c => 0, (c, v) => { if (v == 1) Seguro(() => SV(c).UnlockAllTMRecipes()); }, _ => P.Lista(IdiomaInterfaz.T("Form.Trainer7.SinCambios"), IdiomaInterfaz.T("EntrenadorSwitch.DesbloquearTodas")), soloAccion: true),
                    P.Opcion("snacksworth", IdiomaInterfaz.T("EntrenadorSwitch.LegendariosSnacksworth"), c => 0, (c, v) => { if (v == 1) Seguro(() => SV(c).ActivateSnacksworthLegendaries()); }, _ => P.Lista(IdiomaInterfaz.T("Form.Trainer7.SinCambios"), IdiomaInterfaz.T("Form.Activar")), soloAccion: true),
                    P.Opcion("entrenadores", IdiomaInterfaz.T("EntrenadorSwitch.EntrenadoresClub"), c => 0, (c, v) => { if (v == 1) Seguro(() => SV(c).UnlockAllCoaches()); }, _ => P.Lista(IdiomaInterfaz.T("Form.Trainer7.SinCambios"), IdiomaInterfaz.T("Form.Trainer7b.Desbloquear")), soloAccion: true),
                    P.Opcion("moto", IdiomaInterfaz.T("EntrenadorSwitch.MejorasMoto"), c => 0, (c, v) => { if (v == 1) Seguro(() => DesbloquearMoto(SV(c))); }, _ => P.Lista(IdiomaInterfaz.T("Form.Trainer7.SinCambios"), IdiomaInterfaz.T("EntrenadorSwitch.DesbloquearTodas")), soloAccion: true),
                    P.Opcion("ropa", IdiomaInterfaz.T("EntrenadorSwitch.RopaBasica"), c => 0, (c, v) => { if (v == 1) Seguro(() => PlayerFashionUnlock9.UnlockBase(SV(c).Accessor, SV(c).Gender)); }, _ => P.Lista(IdiomaInterfaz.T("Form.Trainer7.SinCambios"), IdiomaInterfaz.T("HerramientasGuardado.Desbloquear")), soloAccion: true),
                ]),
            ]),
        };
        if (sav.SaveRevision >= 2)
            pestanas.Add(new("arandano", T9("Tab_Blueberry", IdiomaInterfaz.T("EntrenadorSwitch.AcademiaArandano")),
            [
                new(IdiomaInterfaz.T("EntrenadorSwitch.AcademiaArandano"),
                [
                    P.Entero("pa", T9("L_BP", IdiomaInterfaz.T("EntrenadorSwitch.PuntosArandanoPA")), c => SV(c).BlueberryPoints, (c, v) => SV(c).BlueberryPoints = (uint)v, 0, 9_999_999),
                    P.Entero("misionessolo", IdiomaInterfaz.T("EntrenadorSwitch.MisionesSolitario"), c => SV(c).BlueberryQuestRecord.QuestsDoneSolo, (c, v) => SV(c).BlueberryQuestRecord.QuestsDoneSolo = (uint)v, 0, uint.MaxValue),
                    P.Entero("misionesgrupo", IdiomaInterfaz.T("EntrenadorSwitch.MisionesGrupo"), c => SV(c).BlueberryQuestRecord.QuestsDoneGroup, (c, v) => SV(c).BlueberryQuestRecord.QuestsDoneGroup = (uint)v, 0, uint.MaxValue),
                    P.Opcion("lanzamientos", IdiomaInterfaz.T("EntrenadorSwitch.EstilosLanzamiento"), c => 0, (c, v) => { if (v == 1) Seguro(SV(c).UnlockAllThrowStyles); }, _ => P.Lista(IdiomaInterfaz.T("Form.Trainer7.SinCambios"), IdiomaInterfaz.T("Form.Trainer7b.Desbloquear")), soloAccion: true),
                    P.Opcion("lanzamiento", IdiomaInterfaz.T("EntrenadorSwitch.EstiloLanzamiento"), c => (int)SV(c).ThrowStyle - 1, (c, v) => SV(c).ThrowStyle = (ThrowStyle9)((int)v + 1), c => P.Lista(Util.GetStringList("throw_styles", Pokekeep.Mobile.Ui.Ajustes.Pkhex.ProgramaPkhex.Idioma)), titulo: IdiomaInterfaz.T("EntrenadorSwitch.EstiloLanzamiento")),
                ]),
            ]));
        return new EditorDefP("entrenador9", IdiomaInterfaz.T("EntrenadorSwitch.Entrenador"), pestanas, Censo9, IdiomaInterfaz.T("EntrenadorSwitch.EntrenadorMapaDesbloqueosAcademia"));
    }

    /// <summary>Una accion masiva del Core que busca bloques que algunas partidas no tienen (una partida en blanco, una version sin DLC): falla con un mensaje claro y la partida queda como estaba.</summary>
    private static void Seguro(Action accion)
    {
        try { accion(); }
        catch (Exception ex) when (ex is KeyNotFoundException or ArgumentOutOfRangeException or IndexOutOfRangeException)
        { throw new InvalidOperationException(IdiomaInterfaz.T("Partida.SinDatosAccion"), ex); }
    }

    private static void DesbloquearMoto(SAV9SV sav)
    {
        string[] bloques = ["FSYS_RIDE_DASH_ENABLE", "FSYS_RIDE_SWIM_ENABLE", "FSYS_RIDE_HIJUMP_ENABLE", "FSYS_RIDE_GLIDE_ENABLE", "FSYS_RIDE_CLIMB_ENABLE"];
        var acc = sav.Accessor;
        foreach (var b in bloques) acc.GetBlock(b).ChangeBooleanType(SCTypeCode.Bool2);
        if (acc.TryGetBlock("FSYS_RIDE_FLIGHT_ENABLE", out var vuelo)) vuelo.ChangeBooleanType(SCTypeCode.Bool2);   // las partidas base y del DLC 1 no lo tienen
    }

    private static void DesbloquearVuelos(SAV9SV sav)
    {
        var acc = sav.Accessor;
        foreach (var hash in Vuelos)
            if (acc.TryGetBlock(hash, out var b)) b.ChangeBooleanType(SCTypeCode.Bool2);
    }

    /// <summary>Las mismas claves, en el mismo orden, que <c>FlyHashes</c> de SAV_Trainer9 (destinos de vuelo, Centros Pokémon, Tesoros de la Perdición y mapas del DLC).</summary>
    internal static readonly uint[] Vuelos =
    [
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_01,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_02,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_03,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_04,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_05,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_06,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_07,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_08,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_09,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_10,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_11,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_12,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_13,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_14,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_15,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_16,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_17,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_18,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_19,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_20,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_21,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_22,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_23,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_24,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_25,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_26,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_27,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_28,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_29,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_30,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_31,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_32,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_33,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_34,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_35,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_MAGATAMA,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_MOKKAN,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_TSURUGI,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_UTSUWA,
        SaveBlockAccessor9SV.FSYS_YMAP_POKECEN_02,
        SaveBlockAccessor9SV.FSYS_YMAP_POKECEN_03,
        SaveBlockAccessor9SV.FSYS_YMAP_POKECEN_04,
        SaveBlockAccessor9SV.FSYS_YMAP_POKECEN_05,
        SaveBlockAccessor9SV.FSYS_YMAP_POKECEN_06,
        SaveBlockAccessor9SV.FSYS_YMAP_POKECEN_07,
        SaveBlockAccessor9SV.FSYS_YMAP_POKECEN_08,
        SaveBlockAccessor9SV.FSYS_YMAP_POKECEN_09,
        SaveBlockAccessor9SV.FSYS_YMAP_POKECEN_10,
        SaveBlockAccessor9SV.FSYS_YMAP_POKECEN_11,
        SaveBlockAccessor9SV.FSYS_YMAP_POKECEN_12,
        SaveBlockAccessor9SV.FSYS_YMAP_POKECEN_13,
        SaveBlockAccessor9SV.FSYS_YMAP_POKECEN_14,
        SaveBlockAccessor9SV.FSYS_YMAP_POKECEN_15,
        SaveBlockAccessor9SV.FSYS_YMAP_POKECEN_16,
        SaveBlockAccessor9SV.FSYS_YMAP_POKECEN_17,
        SaveBlockAccessor9SV.FSYS_YMAP_POKECEN_18,
        SaveBlockAccessor9SV.FSYS_YMAP_POKECEN_19,
        SaveBlockAccessor9SV.FSYS_YMAP_POKECEN_20,
        SaveBlockAccessor9SV.FSYS_YMAP_POKECEN_21,
        SaveBlockAccessor9SV.FSYS_YMAP_POKECEN_22,
        SaveBlockAccessor9SV.FSYS_YMAP_POKECEN_23,
        SaveBlockAccessor9SV.FSYS_YMAP_POKECEN_24,
        SaveBlockAccessor9SV.FSYS_YMAP_POKECEN_25,
        SaveBlockAccessor9SV.FSYS_YMAP_POKECEN_26,
        SaveBlockAccessor9SV.FSYS_YMAP_POKECEN_27,
        SaveBlockAccessor9SV.FSYS_YMAP_POKECEN_28,
        SaveBlockAccessor9SV.FSYS_YMAP_POKECEN_29,
        SaveBlockAccessor9SV.FSYS_YMAP_POKECEN_30,
        SaveBlockAccessor9SV.FSYS_YMAP_POKECEN_31,
        SaveBlockAccessor9SV.FSYS_YMAP_POKECEN_32,
        SaveBlockAccessor9SV.FSYS_YMAP_POKECEN_33,
        SaveBlockAccessor9SV.FSYS_YMAP_POKECEN_34,
        SaveBlockAccessor9SV.FSYS_YMAP_POKECEN_35,
        SaveBlockAccessor9SV.FSYS_YMAP_MAGATAMA,
        SaveBlockAccessor9SV.FSYS_YMAP_MOKKAN,
        SaveBlockAccessor9SV.FSYS_YMAP_TSURUGI,
        SaveBlockAccessor9SV.FSYS_YMAP_UTSUWA,
        SaveBlockAccessor9SV.FSYS_YMAP_SU1MAP_CHANGE,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_SU1_AREA10,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_SU1_BUSSTOP,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_SU1_CENTER01,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_SU1_PLAZA,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_SU1_SPOT01,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_SU1_SPOT02,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_SU1_SPOT03,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_SU1_SPOT04,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_SU1_SPOT05,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_SU1_SPOT06,
        SaveBlockAccessor9SV.FSYS_YMAP_S2_MAPCHANGE_ENABLE,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_SU2_DRAGON,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_SU2_ENTRANCE,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_SU2_FAIRY,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_SU2_HAGANE,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_SU2_HONOO,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_SU2_SPOT01,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_SU2_SPOT02,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_SU2_SPOT03,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_SU2_SPOT04,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_SU2_SPOT05,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_SU2_SPOT06,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_SU2_SPOT07,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_SU2_SPOT08,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_SU2_SPOT09,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_SU2_SPOT10,
        SaveBlockAccessor9SV.FSYS_YMAP_FLY_SU2_SPOT11,
        SaveBlockAccessor9SV.FSYS_YMAP_POKECEN_SU02,
    ];

    // ------------------------------------------------------------------ Leyendas Z-A
    public static readonly string[] Censo9a = ["PK-SAV-202", "PK-SAV-203", "PK-SAV-204", "PK-SAV-205", "PK-SAV-206", "PK-SAV-231"];
    private static SAV9ZA ZA(ContextoP c) => (SAV9ZA)c.Sav;
    private static string T9a(string k, string? alt = null) => Texto($"SAV_Trainer9a.{k}", alt);

    public static EditorDefP Crear9a(SAV9ZA sav)
    {
        var pestanas = new List<PestanaP>
        {
            new("general", T9a("Tab_Overview", IdiomaInterfaz.T("Cajas.General")),
            [
                new(IdiomaInterfaz.T("EntrenadorSwitch.Entrenador"),
                [
                    new CampoP
                    {
                        Id = "genero", Etiqueta = IdiomaInterfaz.T("Form.Genero"), Tipo = TipoP.Opcion, TituloSelector = IdiomaInterfaz.T("Form.Genero"),
                        Leer = c => (long)ZA(c).Gender,
                        Escribir = (c, v) => { if (ZA(c).Gender != (byte)(long)v!) { ZA(c).Gender = (byte)(long)v!; ZA(c).PlayerFashion.Reset(); } },
                        Opciones = _ => Generos,
                        Ayuda = IdiomaInterfaz.T("EntrenadorSwitch.CambiarGeneroReiniciaRopa"),
                    },
                    P.Texto("ot", IdiomaInterfaz.T("Form.NombreEntrenador"), c => ZA(c).OT, (c, v) => { if (ZA(c).OT != v) ZA(c).OT = v; }, maxFn: c => 12),
                    .. IdsP.Campos(sav),
                    P.Entero("dinero", IdiomaInterfaz.T("Form.Dinero"), c => ZA(c).Money, (c, v) => ZA(c).Money = (uint)v, 0, sav.MaxMoney, ayuda: IdiomaInterfaz.F("Form.Maximo", sav.MaxMoney)),
                    P.Opcion("idioma", T9a("L_Language", IdiomaInterfaz.T("Catalogo.Idioma")), c => ZA(c).Language, (c, v) => ZA(c).Language = (int)v, _ => P.Lista(GameInfo.LanguageDataSource(9, EntityContext.Gen9a))),
                ]),
                new(IdiomaInterfaz.T("Form.Trainer7.Aventura"),
                [
                    .. ComunP.Tiempo(99_999),
                    P.FechaHora("guardado", T9a("L_LastSaved", IdiomaInterfaz.T("Form.Trainer7b.Guardado")), c => ZA(c).LastSaved.Timestamp, (c, v) => ZA(c).LastSaved.Timestamp = v.Date.AddSeconds(v.TimeOfDay.TotalSeconds)),
                ]),
            ]),
            new("varios", T9a("Tab_MiscValues", IdiomaInterfaz.T("Form.Trainer8.Varios")),
            [
                new(IdiomaInterfaz.T("EntrenadorSwitch.TicketsPuntos"),
                [
                    P.Entero("royale", IdiomaInterfaz.T("EntrenadorSwitch.PuntosEntradasRoyaleNormal"), c => ZA(c).TicketPointsRoyale, (c, v) => ZA(c).TicketPointsRoyale = (uint)v, 0, 9_999_999, ayuda: IdiomaInterfaz.T("EntrenadorSwitch.MaximoJuego")),
                    P.Entero("royaleinf", IdiomaInterfaz.T("EntrenadorSwitch.PuntosEntradasRoyaleInfinito"), c => ZA(c).TicketPointsRoyaleInfinite, (c, v) => ZA(c).TicketPointsRoyaleInfinite = (uint)v, 0, 9_999_999, ayuda: IdiomaInterfaz.T("EntrenadorSwitch.MaximoJuego2")),
                ]),
                new(T9a("GB_Map", IdiomaInterfaz.T("EntrenadorSwitch.PosicionMapa")),
                [
                    P.Texto("mapa", T9a("L_Map", IdiomaInterfaz.T("EntrenadorSwitch.Mapa")), c => ZA(c).Coordinates.Map, (c, v) => { }, 32, grupo: "mapa"),
                    P.Decimal("x", IdiomaInterfaz.T("Form.Trainer7b.X"), c => ZA(c).Coordinates.X, (c, v) => { }, -99_999_999, 99_999_999, decimales: 6, grupo: "mapa"),
                    P.Decimal("y", IdiomaInterfaz.T("Form.Trainer7b.Y"), c => ZA(c).Coordinates.Y, (c, v) => { }, -99_999_999, 99_999_999, decimales: 6, grupo: "mapa"),
                    P.Decimal("z", IdiomaInterfaz.T("Form.Trainer7b.Z"), c => ZA(c).Coordinates.Z, (c, v) => { }, -99_999_999, 99_999_999, decimales: 6, grupo: "mapa"),
                    new CampoP
                    {
                        Id = "rotacion", Etiqueta = IdiomaInterfaz.T("Form.Trainer6.Modo3"), Tipo = TipoP.Decimal, Min = -99_999_999, Max = 99_999_999, Decimales = 6, Prioridad = 5, GrupoEscritura = "mapa",
                        Leer = c => Math.Round(ZA(c).Coordinates.Rotation, 6),
                        // SaveMap() de PKHeX: coordenadas, rotacion y nombre del mapa juntos.
                        Escribir = (c, v) =>
                        {
                            var co = ZA(c).Coordinates;
                            co.SetCoordinates((float)c.Decimal("x"), (float)c.Decimal("y"), (float)c.Decimal("z"));
                            var ang = (double)v! * Math.PI / 360.0;
                            // PKHeX escribe la rotacion en un eje (RY) distinto del que lee (RZ): aqui se escribe donde se lee.
                            co.SetPlayerRotation(0f, 0f, (float)Math.Sin(ang), (float)Math.Cos(ang));
                            co.Map = c.Texto("mapa");
                        },
                    },
                ]),
                new(IdiomaInterfaz.T("EntrenadorSwitch.Recoleccion"), [
                    P.Opcion("mts", IdiomaInterfaz.T("EntrenadorSwitch.MaquinasTecnicas"), c => 0, (c, v) => { if (v == 1) Seguro(() => TechnicalMachine9a.SetAllTechnicalMachines(ZA(c), true)); }, _ => P.Lista(IdiomaInterfaz.T("Form.Trainer7.SinCambios"), IdiomaInterfaz.T("EntrenadorSwitch.ConseguirTodas")), soloAccion: true),
                    P.Opcion("tornillos", IdiomaInterfaz.T("EntrenadorSwitch.TornillosDeColores"), c => 0, (c, v) => { if (v == 1) Seguro(() => ColorfulScrew9a.CollectScrews(ZA(c))); }, _ => P.Lista(IdiomaInterfaz.T("Form.Trainer7.SinCambios"), IdiomaInterfaz.T("EntrenadorSwitch.RecogerTodos")), soloAccion: true),
                ]),
            ]),
        };
        if (sav.SaveRevision != 0)
            pestanas.Add(new("dlc", T9a("Tab_DLC", "DLC"),
            [
                new(IdiomaInterfaz.T("EntrenadorSwitch.DimensionHiperespacio"),
                [
                    P.Entero("hiperespacio", IdiomaInterfaz.T("EntrenadorSwitch.PuntosEstudioHiperespacio"), c => ZA(c).GetValue<uint>(SaveBlockAccessor9ZA.KHyperspaceSurveyPoints), (c, v) => ZA(c).SetValue(SaveBlockAccessor9ZA.KHyperspaceSurveyPoints, (uint)v), 0, 9_999_999, ayuda: IdiomaInterfaz.T("EntrenadorSwitch.MaximoJuego3")),
                    P.Texto("calle", T9a("L_StreetName", IdiomaInterfaz.T("EntrenadorSwitch.NombreCalle")), c => ZA(c).GetString(ZA(c).Blocks.GetBlock(SaveBlockAccessor9ZA.KStreetName).Data), (c, v) => ZA(c).SetString(ZA(c).Blocks.GetBlock(SaveBlockAccessor9ZA.KStreetName).Data, v, 18, StringConverterOption.ClearZero), 18),
                ]),
            ]));
        return new EditorDefP("entrenador9a", IdiomaInterfaz.T("EntrenadorSwitch.Entrenador"), pestanas, Censo9a, IdiomaInterfaz.T("EntrenadorSwitch.EntrenadorTicketsMapaDLC"));
    }
}
