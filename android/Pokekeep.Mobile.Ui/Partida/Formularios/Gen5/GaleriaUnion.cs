// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Runtime.CompilerServices;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen5;

/// <summary>
/// PK-SAV-062 a PK-SAV-078 · <c>SAV_JoinAvenue</c>, la Galería Unión de Negro 2/Blanco 2 (bloque <c>JoinAvenue5</c>) con sus controles de usuario: <c>JoinAvenueSettingsEditor</c> (ajustes), <c>JoinAvenueListEditor</c> (la lista con importar/exportar y las pestañas
/// General/Específico), <c>JoinAvenueEntityGeneralEditor</c> y los específicos de visitante, fan y asistente (<c>IJoinAvenueSpecificEditor</c>). Las seis pestañas del original (<c>Tab_Settings</c>, <c>Tab_Visitors</c>, <c>Tab_Fans</c>,
/// <c>Tab_Occupants</c>, <c>Tab_Assistants</c>, <c>Tab_Self</c>) son siete aquí: «Ajustes» (marca de script, cantidades, nombre y título de la galería, experiencia, rango, color del cielo raso, promoción) y «Jugadores» (los 32 ID de jugadores visitantes que
/// recuerda la galería; una colección porque la pestaña de ajustes no puede ser a la vez secciones y lista), y una colección por tipo de entidad (8 visitantes, 12 fans, 8 ocupantes, 4 asistentes) cuyos elementos llevan las secciones «General» y «Específico» y el
/// importar/exportar del <c>JoinAvenueListEditor</c>; «Propio» es la entidad del entrenador (General + Específico). Reglas calcadas: el orden de <c>SaveObject</c> (el nombre y la exclamación se codifican con el idioma que tenía la entidad, el saludo y la despedida con el
/// nuevo: van en grupo con el idioma); las listas separadas por comas del original (conteos de tiendas, récords, curiosidades, actividades) son un campo por valor con el mismo límite (4 bits, 32 bits, 8 bits); la tienda y la tienda deseada se guardan como
/// una tupla (versión, tipo, nivel) con el codificador del Core, que no es el inverso de su decodificador (PKHeX lo hace igual: ADR 0062); un campo de fecha acepta «aaaa-mm-dd», un número o «0x»+hex, y vacío es 0.
/// </summary>
internal static partial class GaleriaUnion
{
    public const string Form = "SAV_JoinAvenue";
    public static readonly string[] Censo = ["PK-SAV-062", "PK-SAV-063", "PK-SAV-064", "PK-SAV-065", "PK-SAV-066", "PK-SAV-067", "PK-SAV-068", "PK-SAV-069", "PK-SAV-070", "PK-SAV-071", "PK-SAV-072", "PK-SAV-073", "PK-SAV-074", "PK-SAV-075", "PK-SAV-076", "PK-SAV-077", "PK-SAV-078"];

    private static string L(string control) => EtiquetasLang.Requerir(Form, control);
    private static JoinAvenue5 Avenida(ContextoP c) => ((SAV5B2W2)c.Sav).JoinAvenue;

    /// <summary>Predicado barato de <see cref="Crear"/>: el editor existe para este guardado.</summary>
    public static bool Aplica(SaveFile sav) => sav is SAV5B2W2;

    // ---- listas del formulario (las de FilteredSources y de la fuente de idiomas), una vez por contexto ----
    private sealed class Listas { public required FilteredGameDataSource Filtro { get; init; } public IReadOnlyList<OpcionP>? Especies, Juegos; }
    private static readonly ConditionalWeakTable<ContextoP, Listas> Cache = new();
    private static Listas De(ContextoP c)
    {
        // auditoría uso10 (A7): con el cambio de idioma en vivo la lista se rehace si cambian los textos del contexto (como AyudasGen3).
        if (Cache.TryGetValue(c, out var l) && ReferenceEquals(l.Filtro.Source, c.Datos)) return l;
        l = new Listas { Filtro = new FilteredGameDataSource(c.Sav, c.Datos) };
        Cache.AddOrUpdate(c, l);
        return l;
    }
    private static IReadOnlyList<OpcionP> Especies(ContextoP c) { var l = De(c); return l.Especies ??= P.Lista(l.Filtro.Species); }
    private static IReadOnlyList<OpcionP> Juegos(ContextoP c) { var l = De(c); return l.Juegos ??= P.Lista(l.Filtro.Games); }
    private static IReadOnlyList<OpcionP> Idiomas(ContextoP _) => P.Lista(GameInfo.LanguageDataSource(5, EntityContext.Gen5));

    // ---------------------------------------------------------------- Ajustes (JoinAvenueSettingsEditor)
    private static JoinAvenueSettings5 Ajustes(ContextoP c) => Avenida(c).Settings;

    private static IEnumerable<SeccionP> AjustesDeLaGaleria()
    {
        yield return new SeccionP(string.Empty,
        [
            P.Marca("script", L("CHK_ScriptFlag"), c => Avenida(c).ScriptFlag, (c, v) => Avenida(c).ScriptFlag = v),
            P.Entero("visitantes", L("L_VisitorCount").TrimEnd(':'), c => Avenida(c).CountVisitor, (c, v) => Avenida(c).CountVisitor = (uint)v, 0, uint.MaxValue, ayuda: IdiomaInterfaz.T("GaleriaUnion.JuegoSiempreUsa")),
            P.Entero("fans", L("L_FanCount").TrimEnd(':'), c => Avenida(c).CountFan, (c, v) => Avenida(c).CountFan = (uint)v, 0, uint.MaxValue, ayuda: IdiomaInterfaz.T("GaleriaUnion.JuegoSiempreUsa2")),
        ]);
        yield return new SeccionP(L("Tab_Settings"),
        [
            P.Texto("nombre", L("L_Name"), c => Ajustes(c).Name, (c, v) => Ajustes(c).Name = v, 20),
            P.Texto("titulo", L("L_Title"), c => Ajustes(c).PlayerTitle, (c, v) => Ajustes(c).PlayerTitle = v, 20),
            P.Entero("experiencia", L("L_Experience").TrimEnd(':'), c => Ajustes(c).Experience, (c, v) => Ajustes(c).Experience = (uint)v, 0, uint.MaxValue),
            P.Entero("rango", L("L_Rank"), c => Ajustes(c).Rank, (c, v) => Ajustes(c).Rank = (ushort)v, 0, JoinAvenueSettings5.MaxAvenueRank),
            P.Opcion("techo", L("L_CeilingColor"), c => (long)Ajustes(c).CeilingColor, (c, v) => Ajustes(c).CeilingColor = (JoinAvenueCeilingColor5)(int)v, _ => ColoresDelTecho(), titulo: L("L_CeilingColor")),
            P.Entero("marcas", L("L_Flags"), c => Ajustes(c).Flags, (c, v) => Ajustes(c).Flags = (uint)v, 0, uint.MaxValue, hex: true),
            P.Entero("semilla", L("L_Seed"), c => Ajustes(c).Seed, (c, v) => Ajustes(c).Seed = (uint)v, 0, uint.MaxValue),
        ]);
        yield return new SeccionP(L("L_IsPromotionActive").TrimEnd(':'),
        [
            P.Marca("promocion", L("L_IsPromotionActive"), c => Ajustes(c).IsPromotionActive, (c, v) => Ajustes(c).IsPromotionActive = v),
            P.Entero("promocion-dias", L("L_PromotionDaysElapsed"), c => Ajustes(c).PromotionDaysElapsed, (c, v) => Ajustes(c).PromotionDaysElapsed = (ushort)v, 0, ushort.MaxValue),
        ]);
        yield return new SeccionP(L("L_VisitingPlayerDatabase").TrimEnd(':'),
        [
            P.Entero("jugadores-cantidad", L("L_PlayerIDCount"), c => Ajustes(c).VisitingPlayerDatabaseCount, (c, v) => Ajustes(c).VisitingPlayerDatabaseCount = (ushort)v, 0, JoinAvenueSettings5.CountVisitingPlayersRemembered),
            P.Entero("jugadores-insercion", L("L_PlayerIDInsert"), c => Ajustes(c).VistiingPlayerDatabaseInsertIndex, (c, v) => Ajustes(c).VistiingPlayerDatabaseInsertIndex = (ushort)v, 0, JoinAvenueSettings5.CountVisitingPlayersRemembered - 1),
        ]);
    }

    private static IReadOnlyList<OpcionP> ColoresDelTecho() => TextosPkhex.EnumLista<JoinAvenueCeilingColor5>();

    /// <summary>Los 32 ID de jugador que recuerda la galería (<c>DGV_VisitingPlayerDatabase</c>): TID (16 bits bajos) y SID (altos), que se guardan juntos.</summary>
    private static ColeccionDefP Jugadores()
    {
        ColeccionDefP? col = null;
        col = new ColeccionDefP
        {
            Id = "jugadores", Elemento = IdiomaInterfaz.T("GaleriaUnion.IDJugador"), Cantidad = _ => JoinAvenueSettings5.CountVisitingPlayersRemembered,
            Titulo = (_, i) => $"{L("DGV_Column_Index")} {i + 1}",
            Resumen = (c, i) => Ajustes(c).GetVisitingPlayerTrainerID(i) is var v && v == JoinAvenueSettings5.VisitingPlayerDefaultNone ? null : $"{L("DGV_Column_TID")} {(ushort)v:00000} · {L("DGV_Column_SID")} {(ushort)(v >> 16):00000}",
            Secciones = (_, i) =>
            {
                string Id(string x) => col!.IdCampo(i, x);
                void Guardar(ContextoP c) => Ajustes(c).SetVisitingPlayerTrainerID(i, (uint)c.Entero(Id("tid")) | ((uint)c.Entero(Id("sid")) << 16));
                return
                [
                    new SeccionP(string.Empty,
                    [
                        P.Entero(Id("tid"), L("DGV_Column_TID"), c => (ushort)Ajustes(c).GetVisitingPlayerTrainerID(i), (c, _) => Guardar(c), 0, ushort.MaxValue, grupo: $"jugador{i}"),
                        P.Entero(Id("sid"), L("DGV_Column_SID"), c => (ushort)(Ajustes(c).GetVisitingPlayerTrainerID(i) >> 16), (c, _) => Guardar(c), 0, ushort.MaxValue, grupo: $"jugador{i}"),
                    ]),
                ];
            },
        };
        return col;
    }

    public static EditorDefP? Crear(SaveFile sav)
    {
        if (!Aplica(sav)) return null;
        var pestanas = new List<PestanaP>
        {
            new("ajustes", L("Tab_Settings"), [.. AjustesDeLaGaleria()]),
            new("jugadores", L("L_VisitingPlayerDatabase").TrimEnd(':'), [], Coleccion: Jugadores()),
            new("visitantes", L("Tab_Visitors"), [], Coleccion: Visitantes()),
            new("fans", L("Tab_Fans"), [], Coleccion: Fans()),
            new("ocupantes", L("Tab_Occupants"), [], Coleccion: Ocupantes()),
            new("asistentes", L("Tab_Assistants"), [], Coleccion: Asistentes()),
            new("propio", L("Tab_Self"), [.. Propio()]),
        };
        return new EditorDefP("avenida", IdiomaInterfaz.T("Partida.Especifico.avenida.Titulo"), pestanas, Censo, IdiomaInterfaz.T("Partida.Especifico.avenida.Descripcion"));
    }
}
