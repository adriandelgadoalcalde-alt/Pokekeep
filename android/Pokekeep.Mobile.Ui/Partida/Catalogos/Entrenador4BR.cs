// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;
using static Pokekeep.Mobile.Ui.Partida.TextosPkhex;

namespace Pokekeep.Mobile.Ui.Partida.Catalogos;

/// <summary>Entrenador de Pokémon Battle Revolution (<c>SAV_Trainer4BR</c>): perfil, tiempo, dinero, récords y coliseos, con las mismas llamadas y el mismo orden que <c>Save()</c>.</summary>
internal static class Entrenador4BR
{
    public static readonly string[] Censo = ["PK-SAV-055"];
    private static string T(string c, string? alt = null) => Texto($"SAV_Trainer4BR.{c}", alt);
    private static SAV4BR S(ContextoP c) => (SAV4BR)c.Sav;

    /// <summary>
    /// Longitud de la presentacion o la frase como la cuenta <c>ValidateCatchphrase</c>: los saltos de linea y los marcadores especiales valen 2, y cada linea suma 2 mas, pero el formulario solo compara con el maximo justo despues de
    /// contar un caracter: lo que sume el final de la ULTIMA linea (o un salto sin caracteres detras) nunca recorta. Devuelve la cuenta tras el ultimo caracter, de modo que <c>Longitud(t) &gt; maximo</c> es exactamente «WinForms recorta».
    /// </summary>
    public static int Longitud(string texto)
    {
        var l = 0; var trasUltimo = 0;
        foreach (var linea in texto.Replace("\r\n", "\n").Split('\n'))
        {
            foreach (var ch in linea) { l += ch is StringConverter4GC.LineBreak or StringConverter4GC.Proportional or StringConverter4GC.PokemonName ? 2 : 1; trasUltimo = l; }
            l += 2;
        }
        return trasUltimo;
    }

    private static int MaxIntro(ContextoP c) => c.Entero("idioma") != (int)LanguageID.Japanese ? 51 : 53;

    public static EditorDefP Crear(SAV4BR sav)
    {
        string[] clausuras = ["Gateway", "MainStreet", "Waterfall", "Neon", "Crystal", "SunnyPark", "Magma", "Courtyard", "Sunset", "Stargazer"];
        var coliseos = new List<CampoP>();
        var pistas = new List<CampoP>();
        (string Id, string Nombre, Func<SAV4BR, byte> Leer, Action<SAV4BR, byte> Escribir, Func<SAV4BR, bool> LeerDesb, Action<SAV4BR, bool> EscribirDesb)[] datos =
        [
            ("gateway", T("L_RecordGatewayColosseumClears", IdiomaInterfaz.T("Form.Trainer4BR.L_RecordGatewayColosseumClears")), s => s.RecordGatewayColosseumClears, (s, v) => s.RecordGatewayColosseumClears = v, s => s.UnlockedGatewayColosseum, (s, v) => s.UnlockedGatewayColosseum = v),
            ("callemayor", T("L_RecordMainStreetColosseumClears", IdiomaInterfaz.T("Form.Trainer4BR.L_RecordMainStreetColosseumClears")), s => s.RecordMainStreetColosseumClears, (s, v) => s.RecordMainStreetColosseumClears = v, s => s.UnlockedMainStreetColosseum, (s, v) => s.UnlockedMainStreetColosseum = v),
            ("cascada", T("L_RecordWaterfallColosseumClears", IdiomaInterfaz.T("Form.Trainer4BR.L_RecordWaterfallColosseumClears")), s => s.RecordWaterfallColosseumClears, (s, v) => s.RecordWaterfallColosseumClears = v, s => s.UnlockedWaterfallColosseum, (s, v) => s.UnlockedWaterfallColosseum = v),
            ("neon", T("L_RecordNeonColosseumClears", IdiomaInterfaz.T("Form.Trainer4BR.L_RecordNeonColosseumClears")), s => s.RecordNeonColosseumClears, (s, v) => s.RecordNeonColosseumClears = v, s => s.UnlockedNeonColosseum, (s, v) => s.UnlockedNeonColosseum = v),
            ("cristal", T("L_RecordCrystalColosseumClears", IdiomaInterfaz.T("Form.Trainer4BR.L_RecordCrystalColosseumClears")), s => s.RecordCrystalColosseumClears, (s, v) => s.RecordCrystalColosseumClears = v, s => s.UnlockedCrystalColosseum, (s, v) => s.UnlockedCrystalColosseum = v),
            ("vergel", T("L_RecordSunnyParkColosseumClears", IdiomaInterfaz.T("Form.Trainer4BR.L_RecordSunnyParkColosseumClears")), s => s.RecordSunnyParkColosseumClears, (s, v) => s.RecordSunnyParkColosseumClears = v, s => s.UnlockedSunnyParkColosseum, (s, v) => s.UnlockedSunnyParkColosseum = v),
            ("magma", T("L_RecordMagmaColosseumClears", IdiomaInterfaz.T("Form.Trainer4BR.L_RecordMagmaColosseumClears")), s => s.RecordMagmaColosseumClears, (s, v) => s.RecordMagmaColosseumClears = v, s => s.UnlockedMagmaColosseum, (s, v) => s.UnlockedMagmaColosseum = v),
            ("bastion", T("L_RecordCourtyardColosseumClears", IdiomaInterfaz.T("Form.Trainer4BR.L_RecordCourtyardColosseumClears")), s => s.RecordCourtyardColosseumClears, (s, v) => s.RecordCourtyardColosseumClears = v, s => s.UnlockedCourtyardColosseum, (s, v) => s.UnlockedCourtyardColosseum = v),
            ("ocaso", T("L_RecordSunsetColosseumClears", IdiomaInterfaz.T("Form.Trainer4BR.L_RecordSunsetColosseumClears")), s => s.RecordSunsetColosseumClears, (s, v) => s.RecordSunsetColosseumClears = v, s => s.UnlockedSunsetColosseum, (s, v) => s.UnlockedSunsetColosseum = v),
            ("planetario", T("L_RecordStargazerColosseumClears", IdiomaInterfaz.T("Form.Trainer4BR.L_RecordStargazerColosseumClears")), s => s.RecordStargazerColosseumClears, (s, v) => s.RecordStargazerColosseumClears = v, s => s.UnlockedStargazerColosseum, (s, v) => s.UnlockedStargazerColosseum = v),
        ];
        foreach (var d in datos)
        {
            var dd = d;
            pistas.Add(P.Entero("clear_" + dd.Id, dd.Nombre, c => dd.Leer(S(c)), (c, v) => dd.Escribir(S(c), (byte)v), 0, 255, ayuda: IdiomaInterfaz.T("Form.Trainer4BR.ClearsAyuda")));
            coliseos.Add(P.Marca("desb_" + dd.Id, dd.Nombre, c => dd.LeerDesb(S(c)), (c, v) => dd.EscribirDesb(S(c), v)));
        }
        coliseos.Add(P.Marca("postgame", T("CHK_PostGame", IdiomaInterfaz.T("Form.Trainer4BR.CHK_PostGame")), c => S(c).UnlockedPostGame, (c, v) => S(c).UnlockedPostGame = v));

        return new EditorDefP("entrenador4br", IdiomaInterfaz.T("Form.Entrenador"),
        [
            new("perfil", T("GB_Profile", IdiomaInterfaz.T("Form.Trainer4BR.GB_Profile")),
            [
                new(IdiomaInterfaz.T("Form.Trainer4BR.GB_Profile"),
                [
                    P.Texto("ot", IdiomaInterfaz.T("Form.NombreEntrenador"), c => S(c).CurrentOT, (c, v) => { if (S(c).CurrentOT != v) S(c).CurrentOT = v; }, maxFn: c => c.Sav.MaxStringLengthTrainer),
                    P.Texto("mes", T("L_BirthMonth", IdiomaInterfaz.T("Form.Trainer4BR.L_BirthMonth")), c => S(c).BirthMonth, (c, v) => S(c).BirthMonth = v, 3),
                    P.Texto("dia", T("L_BirthDay", IdiomaInterfaz.T("Form.Trainer4BR.L_BirthDay")), c => S(c).BirthDay, (c, v) => S(c).BirthDay = v, 3),
                    P.Opcion("pais", T("L_Country", IdiomaInterfaz.T("Form.Pais")), c => S(c).Country, (c, v) => S(c).Country = (int)v, _ => EntrenadorSimple.Paises(4), reinicia: ["region"], lecturaAsimetrica: true, detalle: _ => IdiomaInterfaz.T("Form.Trainer4BR.PaisAsimetrico")),
                    P.Opcion("region", T("L_Region", IdiomaInterfaz.T("Form.Trainer4BR.L_Region")), c => S(c).Region, (c, v) => S(c).Region = (int)v, c => EntrenadorSimple.Regiones(4, (int)c.Entero("pais")), prioridad: 1, lecturaAsimetrica: true),
                    new CampoP
                    {
                        Id = "presentacion", Etiqueta = T("L_SelfIntroduction", IdiomaInterfaz.T("Form.Trainer4BR.L_SelfIntroduction")), Tipo = TipoP.Texto, Multilinea = true,
                        Ayuda = IdiomaInterfaz.T("Form.Trainer4BR.IntroAyuda"),
                        Leer = c => string.Join("\n", S(c).SelfIntroduction.TrimStart(StringConverter4GC.Proportional).Split(StringConverter4GC.LineBreak)),
                        Escribir = (c, v) => S(c).SelfIntroduction = (S(c).Japanese ? string.Empty : StringConverter4GC.Proportional.ToString()) + string.Join(StringConverter4GC.LineBreak, ((string)v!).Split('\n')),
                        Validar = (c, t) => Longitud(t) > MaxIntro(c) ? IdiomaInterfaz.F("Form.Trainer4BR.IntroLarga", MaxIntro(c)) : null,
                    },
                    P.Texto("playerid", T("L_PlayerID", IdiomaInterfaz.T("Form.Trainer4BR.L_PlayerID")), c => S(c).PlayerID.ToString("X16"), (c, v) => S(c).PlayerID = Util.GetHexValue64(v), 16, ayuda: IdiomaInterfaz.T("Form.Trainer4BR.IDHexAyuda"), normaliza: t => Util.GetHexValue64(t).ToString("X16")),
                    P.Opcion("idioma", T("L_Language", IdiomaInterfaz.T("Form.Trainer7b.Idioma")), c => S(c).Language, (c, v) => S(c).Language = (int)v, c => P.Lista(c.Datos.LanguageDataSource(3, EntityContext.Gen4))),
                ]),
                new(IdiomaInterfaz.T("Form.Trainer7.Aventura"),
                [
                    .. ComunP.Tiempo(),
                    P.Entero("dinero", IdiomaInterfaz.T("Form.Dinero"), c => S(c).Money, (c, v) => S(c).Money = (uint)v, 0, sav.MaxMoney, ayuda: IdiomaInterfaz.F("Form.Maximo", sav.MaxMoney)),
                    P.Entero("tid", T("L_TID", IdiomaInterfaz.T("Form.Trainer4BR.L_TID")), c => S(c).TID16, (c, v) => S(c).TID16 = (ushort)v, 0, 65535),
                    P.Entero("sid", T("L_SID", IdiomaInterfaz.T("Form.Trainer4BR.L_SID")), c => S(c).SID16, (c, v) => S(c).SID16 = (ushort)v, 0, 65535),
                ]),
            ]),
            new("records", T("GB_Records", IdiomaInterfaz.T("Form.Trainer4BR.GB_Records")),
            [
                new(IdiomaInterfaz.T("Form.Trainer4BR.Combates"),
                [
                    P.Entero("totalcombates", T("L_RecordTotalBattles", IdiomaInterfaz.T("Form.Trainer4BR.L_RecordTotalBattles")), c => S(c).RecordTotalBattles, (c, v) => S(c).RecordTotalBattles = (uint)v, 0, uint.MaxValue),
                    P.Entero("combatescoliseo", T("L_RecordColosseumBattles", IdiomaInterfaz.T("Form.Trainer4BR.L_RecordColosseumBattles")), c => S(c).RecordColosseumBattles, (c, v) => S(c).RecordColosseumBattles = (uint)v, 0, uint.MaxValue),
                    P.Entero("combateslibres", T("L_RecordFreeBattles", IdiomaInterfaz.T("Form.Trainer4BR.L_RecordFreeBattles")), c => S(c).RecordFreeBattles, (c, v) => S(c).RecordFreeBattles = (uint)v, 0, uint.MaxValue),
                    P.Entero("combateswifi", T("L_RecordWiFiBattles", IdiomaInterfaz.T("Form.Trainer4BR.L_RecordWiFiBattles")), c => S(c).RecordWiFiBattles, (c, v) => S(c).RecordWiFiBattles = (uint)v, 0, uint.MaxValue),
                ]),
                new(IdiomaInterfaz.T("Form.Trainer4BR.ColiseosSuperados"), pistas),
            ]),
            new("coliseos", T("GB_Colosseums", IdiomaInterfaz.T("Form.Trainer4BR.GB_Colosseums")), [new(IdiomaInterfaz.T("Form.Trainer4BR.ColiseosDesbloqueados"), coliseos)]),
        ], Censo, IdiomaInterfaz.T("Form.Trainer4BR.Descripcion"));
    }
}
