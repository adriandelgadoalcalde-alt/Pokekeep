// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen5;

internal static partial class Varios5
{
    // ---------------------------------------------------------------- Metro Batalla
    private static BattleSubway5 Metro(ContextoP c) => S(c).BattleSubway;
    private static BattleSubwayPlay5 Partida(ContextoP c) => S(c).BattleSubwayPlay;

    /// <summary>Un modo del Metro: su «pasado» (partida en curso o ultima), su récord y la marca de «partida activa», que el formulario deriva del pasado.</summary>
    private sealed record Modo(string Id, string Titulo, Func<BattleSubway5, int> Pasado, Action<BattleSubway5, int> PonerPasado, Func<BattleSubway5, int> Record, Action<BattleSubway5, int> PonerRecord,
        Func<BattleSubway5, int> Activa, Action<BattleSubway5, int> PonerActiva, string EtiquetaPasado, string EtiquetaRecord);

    private static IEnumerable<Modo> Modos()
    {
        yield return new("individual", L("GB_Singles"), s => s.SinglePast, (s, v) => s.SinglePast = v, s => s.SingleRecord, (s, v) => s.SingleRecord = v, s => s.SingleSet, (s, v) => s.SingleSet = v, L("L_SinglePast"), L("L_SingleRecord"));
        yield return new("dobles", L("GB_Doubles"), s => s.DoublePast, (s, v) => s.DoublePast = v, s => s.DoubleRecord, (s, v) => s.DoubleRecord = v, s => s.DoubleSet, (s, v) => s.DoubleSet = v, L("L_DoublePast"), L("L_DoubleRecord"));
        yield return new("multi-pnj", $"{L("GB_Multi")} · {L("L_MultiNPC")}", s => s.MultiNPCPast, (s, v) => s.MultiNPCPast = v, s => s.MultiNPCRecord, (s, v) => s.MultiNPCRecord = v, s => s.MultiNPCSet, (s, v) => s.MultiNPCSet = v, L("L_MultiNpcPast"), L("L_MultiNpcRecord"));
        yield return new("multi-amigos", $"{L("GB_Multi")} · {L("L_MultiFriends")}", s => s.MultiFriendsPast, (s, v) => s.MultiFriendsPast = v, s => s.MultiFriendsRecord, (s, v) => s.MultiFriendsRecord = v, s => s.MultiFriendsSet, (s, v) => s.MultiFriendsSet = v, L("L_MultiFriendsPast"), L("L_MultiFriendsRecord"));
        yield return new("super-individual", L("GB_SuperSingles"), s => s.SuperSinglePast, (s, v) => s.SuperSinglePast = v, s => s.SuperSingleRecord, (s, v) => s.SuperSingleRecord = v, s => s.SuperSingleSet, (s, v) => s.SuperSingleSet = v, L("L_SSinglePast"), L("L_SSingleRecord"));
        yield return new("super-dobles", L("GB_SuperDoubles"), s => s.SuperDoublePast, (s, v) => s.SuperDoublePast = v, s => s.SuperDoubleRecord, (s, v) => s.SuperDoubleRecord = v, s => s.SuperDoubleSet, (s, v) => s.SuperDoubleSet = v, L("L_SDoublePast"), L("L_SDoubleRecord"));
        yield return new("super-multi-pnj", $"{L("GB_SuperMulti")} · {L("L_SMultiNPC")}", s => s.SuperMultiNPCPast, (s, v) => s.SuperMultiNPCPast = v, s => s.SuperMultiNPCRecord, (s, v) => s.SuperMultiNPCRecord = v, s => s.SuperMultiNPCSet, (s, v) => s.SuperMultiNPCSet = v, L("L_SMultiNpcPast"), L("L_SMultiNpcRecord"));
        yield return new("super-multi-amigos", $"{L("GB_SuperMulti")} · {L("L_SMultiFriends")}", s => s.SuperMultiFriendsPast, (s, v) => s.SuperMultiFriendsPast = v, s => s.SuperMultiFriendsRecord, (s, v) => s.SuperMultiFriendsRecord = v, s => s.SuperMultiFriendsSet, (s, v) => s.SuperMultiFriendsSet = v, L("L_SMultiFriendsPast"), L("L_SMultiFriendsRecord"));
    }

    private static IEnumerable<SeccionP> Metro()
    {
        yield return new SeccionP(L("GB_SubwayChecks"),
        [
            P.Marca("metro-marca0", L("CHK_Subway0"), c => Metro(c).Flag0, (c, v) => Metro(c).Flag0 = v),
            P.Marca("metro-marca1", L("CHK_Subway1"), c => Metro(c).Flag1, (c, v) => Metro(c).Flag1 = v),
            P.Marca("metro-marca2", L("CHK_Subway2"), c => Metro(c).Flag2, (c, v) => Metro(c).Flag2 = v),
            // El formulario tiene una casilla «Flag3» que no esta conectada a nada: al leer y al guardar, la de «Flag7» hace de las dos (sw.Flag3 = CHK_Subway7.Checked). Se enseña solo para lectura.
            P.Marca("metro-marca3", L("CHK_Subway3"), c => Metro(c).Flag3, null,
                motivo: _ => IdiomaInterfaz.T("Varios5.PKHeXConectaCasillaGuardar")),
            P.Marca("metro-marca7", L("CHK_Subway7"), c => Metro(c).Flag7, (c, v) => { Metro(c).Flag3 = v; Metro(c).Flag7 = v; }, ayuda: IdiomaInterfaz.T("Varios5.CambiaTambienMarcaPKHeX")),
            P.Marca("metro-super-individual", L("CHK_SuperSingle"), c => Metro(c).SuperSingle, (c, v) => Metro(c).SuperSingle = v),
            P.Marca("metro-super-dobles", L("CHK_SuperDouble"), c => Metro(c).SuperDouble, (c, v) => Metro(c).SuperDouble = v),
            P.Marca("metro-super-multi", L("CHK_SuperMulti"), c => Metro(c).SuperMulti, (c, v) => Metro(c).SuperMulti = v),
            P.Marca("metro-pnj-visto", L("CHK_SWNPCMet"), c => Metro(c).NPCMet, (c, v) => Metro(c).NPCMet = v),
        ]);
        yield return new SeccionP(L("GB_CurrentData"),
        [
            P.Entero("metro-tipo", L("L_CurrentType"), c => Partida(c).CurrentType, (c, v) => Partida(c).CurrentType = (int)v, 0, 255),
            P.Entero("metro-combate", L("L_CurrentBattle"), c => Partida(c).CurrentBattle, (c, v) => Partida(c).CurrentBattle = (int)v, 0, 255),
        ]);
        foreach (var m in Modos())
        {
            var idPasado = $"{m.Id}-pasado"; var idRecord = $"{m.Id}-record"; var idActiva = $"{m.Id}-activa"; var grupo = $"metro-{m.Id}";
            yield return new SeccionP(m.Titulo,
            [
                // El pasado, el récord y la marca se guardan juntos: lo que se ve como «Actual» sale de pasado / 7 + 1, y el formulario recalcula esa cifra al guardar con el pasado nuevo.
                P.Entero(idPasado, m.EtiquetaPasado, c => m.Pasado(Metro(c)), (c, v) => m.PonerPasado(Metro(c), (int)v), 0, 9999, grupo: grupo,
                    etiquetaFn: c => c.Marca(idActiva) ? L("L_Current") : m.EtiquetaPasado),
                P.Entero(idRecord, m.EtiquetaRecord, c => m.Record(Metro(c)), (c, v) => m.PonerRecord(Metro(c), (int)v), 0, 9999, grupo: grupo),
                P.Marca(idActiva, $"{L("L_RunActive")} · {m.Titulo}", c => m.Activa(Metro(c)) == (m.Pasado(Metro(c)) / 7) + 1,
                    (c, v) => m.PonerActiva(Metro(c), v ? ((int)c.Entero(idPasado) / 7) + 1 : 0), prioridad: 1, grupo: grupo),
            ]);
        }
    }

    // ---------------------------------------------------------------- Bosque Blanco / Ciudad Negra (solo Negro/Blanco)
    private static IEnumerable<CampoP> CiudadBosque()
    {
        yield return P.Archivo("ciudad-bosque", IdiomaInterfaz.T("Varios5.DatosBosqueBlancoCiudad"),
            (c, b) => BW(c).SetData(BW(c).Forest.ForestCity.Span, b),
            validar: (_, b) => b.Length == WhiteBlack5BW.ForestCitySize ? null : string.Format(AyudasGen5.MensajeCore("MsgFileSizeIncorrect"), b.Length, WhiteBlack5BW.ForestCitySize),
            exporta: c => (AyudasGen5.NombreLimpio($"{BW(c).Version}.fc5"), BW(c).Forest.ForestCity.ToArray()),
            extension: "fc5", textoBoton: L("B_ImportFC"), confirmacion: IdiomaInterfaz.T("Varios5.SustituyenDatosBosqueBlanco"), ayuda: L("L_FC") + IdiomaInterfaz.T("Varios5.ExportarSacaPartidaCambios"));
    }

    // ---------------------------------------------------------------- Musical: complementos
    private static string[] Complementos_ => Util.GetStringList("props", Pokekeep.Mobile.Ui.Ajustes.Pkhex.ProgramaPkhex.Idioma);

    private static TablaDefP Complementos()
        => new()
        {
            Id = "complementos", Marcador = IdiomaInterfaz.T("Varios5.BuscarComplemento"),
            Ayuda = IdiomaInterfaz.T("Varios5.ComplementosMusicalEntrenadorTiene"),
            Filas = _ =>
            {
                var l = new List<FilaTablaP>();
                for (var i = 0; i < Complementos_.Length; i++)
                {
                    var n = i;
                    l.Add(new FilaTablaP { Id = n, Nombre = Complementos_[n], EsMarca = true, Leer = c => S(c).Musical.GetHasProp(n), Escribir = (c, v) => S(c).Musical.SetHasProp(n, (bool)v) });
                }
                return l;
            },
            Acciones =
            [
                // UnlockAllMusicalProps: el Core enciende los 100 bits (la lista del formulario tiene 100 nombres).
                new AccionTablaP("desbloquear-complementos", L("B_UnlockAllProps"), (_, filas) => [.. filas.Select(f => (f, (object)true))], IdiomaInterfaz.T("Varios5.DesbloqueaTodosComplementos")),
            ],
        };
}
