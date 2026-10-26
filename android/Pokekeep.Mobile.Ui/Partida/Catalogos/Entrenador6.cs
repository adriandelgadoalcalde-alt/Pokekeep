// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Ajustes.Pkhex;
using Pokekeep.Mobile.Ui.Partida.Formularios.Gen1a3;
using Pokekeep.Mobile.Ui.Localizacion;
using static Pokekeep.Mobile.Ui.Partida.TextosPkhex;

namespace Pokekeep.Mobile.Ui.Partida.Catalogos;

/// <summary>
/// Entrenador de la 6.ª generacion (<c>SAV_Trainer</c>: X/Y, Omega Rubi/Zafiro Alfa y la demo): pestanas Inicio, Medallas y mapa, Multijugador, Mansion, Bastion (X/Y) y Estadisticas, con la misma lista de datos y el
/// mismo orden de escritura que <c>SAV_Trainer.Save()</c>. Cada campo se escribe solo si cambio.
/// </summary>
internal static class Entrenador6
{
    public static readonly string[] Censo = ["PK-SAV-121", "PK-SAV-122", "PK-SAV-123", "PK-SAV-124", "PK-SAV-125", "PK-SAV-126", "PK-SAV-127", "PK-SAV-231"];
    private static string T(string c, string? alt = null) => Texto($"SAV_Trainer.{c}", alt);
    private static SAV6 S(ContextoP c) => (SAV6)c.Sav;

    public static IReadOnlyList<OpcionP> Paises() => P.Lista(Util.GetCountryRegionList("countries", ProgramaPkhex.Idioma));
    public static IReadOnlyList<OpcionP> Subregiones(int pais) => pais > 0 ? P.Lista(Util.GetCountryRegionList($"sr_{pais:000}", ProgramaPkhex.Idioma)) : [new OpcionP(0, "—")];

    public static EditorDefP Crear(SAV6 sav)
    {
        var xy = sav is SAV6XY; var ao = sav is SAV6AO; var demo = sav is SAV6AODemo;
        IReadOnlyList<CampoP> aspectoXY = sav is SAV6XY sxy ? Aspecto(sxy) : [];

        // ---- Inicio ----
        var inicio = new List<SeccionP>
        {
            new(IdiomaInterfaz.T("Form.Entrenador"),
            [
                P.Opcion("juego", T("L_Game", IdiomaInterfaz.T("Form.Juego")), c => (long)S(c).Version, (c, v) => S(c).Version = (GameVersion)v, c => P.Lista(c.Datos.VersionDataSource.Where(v => v.Value is >= 24 and <= 27))),
                P.Opcion("genero", IdiomaInterfaz.T("Form.Genero"), c => S(c).Gender, (c, v) => { S(c).Gender = (byte)v; S(c).Overworld.ResetPlayerModel(); }, _ => AyudasGen3.Generos, afectaA: _ => aspectoXY.Select(a => a.Id)),
                P.Texto("ot", IdiomaInterfaz.T("Form.NombreEntrenador"), c => S(c).OT, (c, v) => { if (S(c).OT != v) S(c).OT = v; }, maxFn: c => c.Sav.MaxStringLengthTrainer),
                .. IdsP.Campos(sav),
                P.Entero("dinero", IdiomaInterfaz.T("Form.Dinero"), c => S(c).Money, (c, v) => S(c).Money = (uint)v, 0, 9_999_999, ayuda: IdiomaInterfaz.T("Form.Trainer6.MaxDinero")),
                P.Entero("bp", T("L_BP"), c => S(c).BP, (c, v) => S(c).BP = (ushort)v, 0, 65535),
                P.Entero("pm", T("L_PM"), c => S(c).GetRecord(63), (c, v) => { S(c).SetRecord(63, (int)v); S(c).SetRecord(64, (int)v); }, 0, int.MaxValue, ayuda: IdiomaInterfaz.T("Form.Trainer6.PokemillasAyuda")),
            ]),
            new(IdiomaInterfaz.T("Form.RegionLengua"),
            [
                P.Opcion("pais", T("L_Country"), c => S(c).Country, (c, v) => S(c).Country = (byte)v, _ => Paises(), reinicia: ["region"]),
                P.Opcion("region", T("L_Region"), c => S(c).Region, (c, v) => S(c).Region = (byte)v, c => Subregiones((int)c.Entero("pais")), prioridad: 1),
                P.Opcion("consola", T("L_3DSReg"), c => S(c).ConsoleRegion, (c, v) => S(c).ConsoleRegion = (byte)v, c => P.Lista(c.Datos.Regions)),
                P.Opcion("idioma", T("L_Language"), c => S(c).Language, (c, v) => S(c).Language = (int)v, c => P.Lista(c.Datos.LanguageDataSource(6, EntityContext.Gen6))),
                P.Opcion("vivillon", T("L_Vivillon", ProgramaPkhex.Textos.specieslist[(int)Species.Vivillon]), c => S(c).Vivillon, (c, v) => S(c).Vivillon = (int)v, c => P.Lista(FormConverter.GetFormList((int)Species.Vivillon, c.Textos.types, c.Textos.forms, GameInfo.GenderSymbolUnicode, EntityContext.Gen6)), titulo: IdiomaInterfaz.F("Form.FormaVivillon", ProgramaPkhex.Textos.specieslist[(int)Species.Vivillon])),
            ]),
            new(T("GB_Sayings"),
            [
                P.Texto("frase1", IdiomaInterfaz.T("Form.Trainer6.Frase1"), c => S(c).Status.Saying1, (c, v) => S(c).Status.Saying1 = v, 16),
                P.Texto("frase2", IdiomaInterfaz.T("Form.Trainer6.Frase2"), c => S(c).Status.Saying2, (c, v) => S(c).Status.Saying2 = v, 16),
                P.Texto("frase3", IdiomaInterfaz.T("Form.Trainer6.Frase3"), c => S(c).Status.Saying3, (c, v) => S(c).Status.Saying3 = v, 16),
                P.Texto("frase4", IdiomaInterfaz.T("Form.Trainer6.Frase4"), c => S(c).Status.Saying4, (c, v) => S(c).Status.Saying4 = v, 16),
                P.Texto("frase5", IdiomaInterfaz.T("Form.Trainer6.Frase5"), c => S(c).Status.Saying5, (c, v) => S(c).Status.Saying5 = v, 16),
            ]),
            new(T("GB_Adventure"), [.. Aventura()]),
            new(IdiomaInterfaz.T("Form.Evolucion"), [.. Megas(ao)]),
        };

        var pestanas = new List<PestanaP>
        {
            new("inicio", T("Tab_Overview"), inicio),
            new("mapa", T("Tab_BadgeMap"), [new(IdiomaInterfaz.T("Form.Medallas"), [.. P.Medallas("med", 8, c => S(c).Badges, (c, v) => S(c).Badges = v)]), new(T("GB_Map"), Mapa())]),
        };
        if (!demo)
        {
            pestanas.Add(new("multi", T("Tab_Multiplayer"), [new(IdiomaInterfaz.T("Form.Multijugador"), [P.Opcion("sprite", T("L_MultiplayerSprite"), c => ((IMultiplayerSprite)c.Sav).MultiplayerSpriteID, (c, v) => ((IMultiplayerSprite)c.Sav).MultiplayerSpriteID = (byte)v, _ => Sprites(sav is SAV6AO), titulo: IdiomaInterfaz.T("Form.IconoMultijugador"))])]));
            pestanas.Add(new("mansion", ProgramaPkhex.Idioma == "en" ? IdiomaInterfaz.T("Form.Trainer6.Mansion") : T("Tab_Maison"), [new(IdiomaInterfaz.T("Form.Trainer6.Mansion"), [.. Mansion()])], Tabla: null));
        }
        if (xy)
        {
            pestanas.Add(new("bastion", ProgramaPkhex.Idioma == "en" ? IdiomaInterfaz.T("Form.Trainer6.Bastion") : T("Tab_BattleChateau"), [new(IdiomaInterfaz.T("Form.Trainer6.Bastion"),
            [
                P.Opcion("rango", T("L_BattleChateauRank"), c => ((SAV6XY)c.Sav).SUBE.ChateauRank, (c, v) => ((SAV6XY)c.Sav).SUBE.ChateauRank = (ushort)v, _ => EnumLista<BattleChateauRank6>(), deriva: c => new Dictionary<string, object?> { ["puntos"] = (long)SubEventLog6XY.GetChateauPointsForRank((ushort)c.Entero("rango")) }),
                P.Entero("puntos", T("L_BattleChateauPoints"), c => ((SAV6XY)c.Sav).SUBE.ChateauPoints, (c, v) => ((SAV6XY)c.Sav).SUBE.ChateauPoints = (ushort)v, 0, SubEventLog6XY.ChateauPointsMax, prioridad: 1),
            ])]));
            pestanas.Add(new("apariencia", T("Tab_Appearance"), [new(IdiomaInterfaz.T("Form.Apariencia"), [
                P.Texto("apodo", T("L_TRNick"), c => ((SAV6XY)c.Sav).Status.Nickname, (c, v) => ((SAV6XY)c.Sav).Status.Nickname = v, 16),
                P.Entero("estilo", T("L_Style"), c => S(c).Situation.Style, (c, v) => S(c).Situation.Style = (byte)v, 0, 255),
            ]), new(IdiomaInterfaz.T("Form.Trainer6.Aspecto"), aspectoXY)]));
        }
        pestanas.Add(new("registros", IdiomaInterfaz.T("Form.Estadisticas"), [], Tabla: TablasP.Registros(RecordLists.RecordList_6)));
        return new EditorDefP("entrenador6", IdiomaInterfaz.T("Form.Entrenador"), pestanas, Censo, IdiomaInterfaz.T("Form.Trainer6.Descripcion"));
    }

    /// <summary>La apariencia actual del entrenador de X/Y (<c>TrainerFashion6</c>): todas las propiedades del objeto, como el PropertyGrid del formulario, que lo lee de la partida, lo cambia y lo escribe de vuelta.</summary>
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "TrainerFashion6 es un tipo de PKHeX que la aplicacion conserva entero.")]
    private static IReadOnlyList<CampoP> Aspecto(SAV6XY xy)
    {
        var tipo = xy.Status.Fashion.GetType();
        var campos = new List<CampoP>();
        foreach (var pi in tipo.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).OrderBy(p => p.Name, StringComparer.Ordinal))
        {
            if (!pi.CanRead || !pi.CanWrite || pi.GetIndexParameters().Length != 0 || !BloquesP.Simple(pi.PropertyType)) continue;
            var c = BloquesP.Campo("aspecto", () => xy.Status.Fashion, o => xy.Status.Fashion = (TrainerFashion6)o, pi, () => campos);
            if (c is not null) campos.Add(c);
        }
        return campos;
    }

    private static IEnumerable<CampoP> Aventura()
    {
        yield return P.Entero("horas", IdiomaInterfaz.T("Form.HorasJugadas"), c => S(c).PlayedHours, (c, v) => S(c).PlayedHours = (ushort)v, 0, 65535);
        yield return P.Entero("minutos", IdiomaInterfaz.T("Form.Minutos"), c => S(c).PlayedMinutes, (c, v) => S(c).PlayedMinutes = (ushort)v % 60, 0, 59);
        yield return P.Entero("segundos", IdiomaInterfaz.T("Form.Segundos"), c => S(c).PlayedSeconds, (c, v) => S(c).PlayedSeconds = (ushort)v % 60, 0, 59);
        yield return P.FechaHora("inicio", T("L_Started"), c => EntrenadorSimple.Fecha(S(c).SecondsToStart), (c, v) => S(c).SecondsToStart = (uint)EntrenadorSimple.Segundos(v));
        yield return P.FechaHora("fama", T("L_Fame"), c => EntrenadorSimple.Fecha(S(c).SecondsToFame), (c, v) => S(c).SecondsToFame = (uint)EntrenadorSimple.Segundos(v));
        yield return new CampoP
        {
            Id = "guardado", Etiqueta = T("L_LastSaved"), Tipo = TipoP.FechaHora,
            Leer = c => S(c).Played.LastSavedDate is { } d ? new DateTime(d.Year, d.Month, d.Day, d.Hour, d.Minute, 0) : DateTime.MinValue, SinSegundos = true,
            Escribir = (c, v) => { var d = (DateTime)v!; S(c).Played.LastSavedDate = new DateTime(d.Year, d.Month, d.Day, d.Hour, d.Minute, 0); },
            Visible = c => S(c).Played.LastSavedDate.HasValue,
        };
    }

    private static IEnumerable<CampoP> Megas(bool ao)
    {
        yield return P.Marca("mega", T("CHK_MegaUnlocked"), c => S(c).Status.IsMegaEvolutionUnlocked, (c, v) => S(c).Status.IsMegaEvolutionUnlocked = v);
        if (ao) yield return P.Marca("megarayquaza", T("CHK_MegaRayquazaUnlocked"), c => S(c).Status.IsMegaRayquazaUnlocked, (c, v) => S(c).Status.IsMegaRayquazaUnlocked = v);
    }

    private static IReadOnlyList<CampoP> Mapa() =>
    [
        P.Entero("mapa", T("L_CurrentMap"), c => S(c).Situation.M, (c, v) => S(c).Situation.M = (int)v, 0, 65535),
        P.Decimal("x", T("L_X"), c => S(c).Situation.X / 18.0, (c, v) => S(c).Situation.X = (float)(v * 18), -100000, 100000, decimales: 6),
        P.Decimal("z", T("L_Z"), c => S(c).Situation.Z / 18.0, (c, v) => S(c).Situation.Z = (float)(v * 18), -100000, 100000, decimales: 6),
        P.Decimal("y", T("L_Y"), c => S(c).Situation.Y / 18.0, (c, v) => S(c).Situation.Y = (float)(v * 18), -100000, 100000, decimales: 6),
        P.Entero("rotacion", T("L_R"), c => S(c).Situation.R, (c, v) => S(c).Situation.R = (int)v, 0, 360),
    ];

    private static IReadOnlyList<OpcionP> Sprites(bool ao)
    {
        var max = ao ? int.MaxValue : (int)TrainerSprite6.Trevor + 1;   // Rubi Omega y Zafiro Alfa añaden los personajes de Hoenn
        return [.. System.Enum.GetValues<TrainerSprite6>().Where(v => (int)v < max).Select(v => new OpcionP((int)v, TextosPkhex.Enum(v)))];
    }

    private static IEnumerable<CampoP> Mansion()
    {
        string[] modos = [(ProgramaPkhex.Idioma == "en" ? T("L_SinglesC") : IdiomaInterfaz.T("Form.Trainer6.Modo0")), (ProgramaPkhex.Idioma == "en" ? T("L_DoublesC") : IdiomaInterfaz.T("Form.Trainer6.Modo1")), (ProgramaPkhex.Idioma == "en" ? T("L_TriplesC") : IdiomaInterfaz.T("Form.Trainer6.Modo2")), (ProgramaPkhex.Idioma == "en" ? T("L_RotationC") : IdiomaInterfaz.T("Form.Trainer6.Modo3")), (ProgramaPkhex.Idioma == "en" ? T("L_MultiC") : IdiomaInterfaz.T("Form.Trainer6.Modo4"))];
        string[] est = [IdiomaInterfaz.T("Form.Trainer6.Racha0"), IdiomaInterfaz.T("Form.Trainer6.Racha1"), IdiomaInterfaz.T("Form.Trainer6.Racha2"), IdiomaInterfaz.T("Form.Trainer6.Racha3")];
        for (var i = 0; i < MaisonBlock.MaisonStatCount; i++)
        {
            var n = i;
            yield return P.Entero($"maison{n}", $"{modos[n / 4]}: {est[n % 4].ToLowerInvariant()}", c => ((ISaveBlock6Main)c.Sav).Maison.GetMaisonStat(n), (c, v) => ((ISaveBlock6Main)c.Sav).Maison.SetMaisonStat(n, (ushort)v), 0, 65535);
        }
    }
}
