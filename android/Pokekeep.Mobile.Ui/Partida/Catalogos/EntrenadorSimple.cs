// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;
using Pokekeep.Mobile.Ui.Ajustes.Pkhex;
using Pokekeep.Mobile.Ui.Modelo;
using Pokekeep.Mobile.Ui.Partida.Formularios.Gen1a3;

namespace Pokekeep.Mobile.Ui.Partida.Catalogos;

/// <summary>
/// Entrenador generico (<c>SAV_SimpleTrainer</c>): Gen. 1-5, Colosseum y XD (los guardados sin editor de entrenador propio). Cada campo es una linea del formulario de PKHeX con su llamada del Core;
/// los datos que ese juego no tiene (SID en Gen. 1-2, pais en Gen. 1-3, monedas desde Gen. 3...) no aparecen, como en el formulario.
/// </summary>
internal static class EntrenadorSimple
{
    /// <summary>
    /// Medallas de Oro, Plata y Cristal por BIT del guardado (<c>SAV2.Badges</c>), con numero de medalla y bit en vez de un nombre: ni PKHeX ni los textos de <c>fuentes\</c> y <c>extraidos\</c> traen los nombres en castellano de las medallas
    /// y no se escriben de memoria. El formulario pinta las casillas 5 y 6 de Johto intercambiadas (<c>SAV_SimpleTrainer.cs:99</c>: el bit 4 va a la casilla 6 y el 5 a la casilla 5); el numero es el de la casilla del formulario.
    /// </summary>
    private static string NombreMedallaGsc(int bit) => bit < 8
        ? IdiomaInterfaz.F("Form.MedallaJohtoBit", bit == 4 ? 6 : bit == 5 ? 5 : bit + 1, bit)
        : IdiomaInterfaz.F("Form.MedallaKantoBit", bit - 7, bit);

    public static readonly string[] Censo = ["PK-SAV-229", "PK-SAV-231"];

    private static IReadOnlyList<OpcionP> Generos => AyudasGen3.Generos;
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<OpcionP>> EstiloCombatePorIdioma = new[] { "es-ES", "en" }.ToDictionary(id => id, id => P.Lista(ServicioIdioma.Recursos(id)["Form.EstiloCambio"], ServicioIdioma.Recursos(id)["Form.EstiloFijo"]));
    private static IReadOnlyList<OpcionP> EstiloCombate => EstiloCombatePorIdioma[IdiomaInterfaz.Actual.Codigo];
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<OpcionP>> VelocidadGbPorIdioma = new[] { "es-ES", "en" }.ToDictionary(id => id, id => P.Lista(ServicioIdioma.Recursos(id)["Form.VelGb0"], ServicioIdioma.Recursos(id)["Form.VelGb1"], "2", ServicioIdioma.Recursos(id)["Form.VelGb3"], "4", ServicioIdioma.Recursos(id)["Form.VelGb5"], "6", "7"));
    private static IReadOnlyList<OpcionP> VelocidadGb => VelocidadGbPorIdioma[IdiomaInterfaz.Actual.Codigo];
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<OpcionP>> SonidoGbPorIdioma = new[] { "es-ES", "en" }.ToDictionary(id => id, id => P.Lista(ServicioIdioma.Recursos(id)["Form.Mono"], ServicioIdioma.Recursos(id)["Form.Estereo"], ServicioIdioma.Recursos(id)["Form.Izquierda"], ServicioIdioma.Recursos(id)["Form.Derecha"]));
    private static IReadOnlyList<OpcionP> SonidoGb => SonidoGbPorIdioma[IdiomaInterfaz.Actual.Codigo];
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<OpcionP>> SonidoPorIdioma = new[] { "es-ES", "en" }.ToDictionary(id => id, id => P.Lista(ServicioIdioma.Recursos(id)["Form.Mono"], ServicioIdioma.Recursos(id)["Form.Estereo"]));
    private static IReadOnlyList<OpcionP> Sonido => SonidoPorIdioma[IdiomaInterfaz.Actual.Codigo];
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<OpcionP>> VelocidadGbaPorIdioma = new[] { "es-ES", "en" }.ToDictionary(id => id, id => P.Lista(ServicioIdioma.Recursos(id)["Form.VelGba0"], ServicioIdioma.Recursos(id)["Form.VelGba1"], ServicioIdioma.Recursos(id)["Form.VelGba2"], "3", "4", "5", "6", "7"));
    private static IReadOnlyList<OpcionP> VelocidadGba => VelocidadGbaPorIdioma[IdiomaInterfaz.Actual.Codigo];

    public static IReadOnlyList<OpcionP> Paises(int generacion) => P.Lista(Util.GetCountryRegionList(generacion == 4 ? "gen4_countries" : "gen5_countries", ProgramaPkhex.Idioma));
    public static IReadOnlyList<OpcionP> Regiones(int generacion, int pais)
    {
        var l = Util.GetCountryRegionList($"gen{generacion}_sr_{pais:000}", ProgramaPkhex.Idioma);
        if (l.Count == 0) l = Util.GetCountryRegionList($"gen{generacion}_sr_default", ProgramaPkhex.Idioma);
        return P.Lista(l);
    }

    public static EditorDefP? Crear(SaveFile sav)
    {
        var gen = sav.Generation;
        var esColoXd = sav is SAV3Colosseum or SAV3XD;
        var portatil = gen is 1 or 2 or 3 && !esColoXd;       // consolas portatiles con opciones y medallas
        var tieneMapa = sav is SAV4 or SAV5;
        var tieneFechas = gen >= 4;

        var entrenador = new List<CampoP>
        {
            P.Texto("ot", IdiomaInterfaz.T("Form.NombreEntrenador"), c => c.Sav.OT, (c, v) => { if (c.Sav.OT != v) c.Sav.OT = v; }, maxFn: c => c.Sav.MaxStringLengthTrainer),
            P.Opcion("genero", IdiomaInterfaz.T("Form.Genero"), c => c.Sav.Gender, (c, v) => c.Sav.Gender = (byte)v, _ => Generos, visible: c => c.Sav.Generation > 1, motivo: c => c.Sav is SAV2 { Version: not GameVersion.C } ? IdiomaInterfaz.F("Form.EntrenadorSimple.SinGenero", NombresJuego.Corto(GameVersion.GD), NombresJuego.Corto(GameVersion.SI), NombresJuego.Corto(GameVersion.C)) : null),
            P.Entero("tid", "TID", c => c.Sav.TID16, (c, v) => c.Sav.TID16 = (ushort)v, 0, 65535, ayuda: IdiomaInterfaz.T("Form.Tid16Ayuda")),
            P.Entero("sid", "SID", c => c.Sav.SID16, (c, v) => c.Sav.SID16 = (ushort)v, 0, 65535, visible: c => c.Sav.Generation > 2, ayuda: IdiomaInterfaz.T("Form.Sid16Ayuda")),
            P.Entero("dinero", IdiomaInterfaz.T("Form.Dinero"), c => c.Sav.Money, (c, v) => c.Sav.Money = (uint)v, 0, sav.MaxMoney, ayuda: IdiomaInterfaz.F("Form.Maximo", sav.MaxMoney)),
        };
        if (gen < 3)
            entrenador.Add(P.Entero("monedas", IdiomaInterfaz.T("Form.Coins"), c => c.Sav switch { SAV1 s => s.Coin, SAV2 s => s.Coin, _ => 0 },
                (c, v) => { if (c.Sav is SAV1 s1) s1.Coin = (ushort)Math.Min((uint)v, (uint)c.Sav.MaxCoins); else if (c.Sav is SAV2 s2) s2.Coin = (ushort)Math.Min((uint)v, (uint)c.Sav.MaxCoins); }, 0, sav.MaxCoins));
        if (sav is SAV5)
            entrenador.Add(P.Entero("bp", IdiomaInterfaz.T("Form.BattlePoints"), c => ((SAV5)c.Sav).BattleSubway.BP, (c, v) => ((SAV5)c.Sav).BattleSubway.BP = (ushort)Math.Min((uint)v, (uint)c.Sav.MaxCoins), 0, sav.MaxCoins));

        var tiempo = new List<CampoP>
        {
            P.Entero("horas", IdiomaInterfaz.T("Form.HorasJugadas"), c => c.Sav.PlayedHours, (c, v) => c.Sav.PlayedHours = (ushort)v, 0, 65535, grupo: "tiempo"),
            P.Entero("minutos", IdiomaInterfaz.T("Form.Minutos"), c => c.Sav.PlayedMinutes, (c, v) => c.Sav.PlayedMinutes = (ushort)v % 60, 0, 59, grupo: "tiempo"),
            P.Entero("segundos", IdiomaInterfaz.T("Form.Segundos"), c => c.Sav.PlayedSeconds, (c, v) => c.Sav.PlayedSeconds = (ushort)v % 60, 0, 59, grupo: "tiempo"),
        };
        if (tieneFechas)
        {
            tiempo.Add(P.FechaHora("inicio", IdiomaInterfaz.T("Form.InicioAventura"), c => Fecha(c.Sav.SecondsToStart), (c, v) => c.Sav.SecondsToStart = (uint)Segundos(v)));
            tiempo.Add(P.FechaHora("fama", IdiomaInterfaz.T("Form.Fama"), c => Fecha(c.Sav.SecondsToFame), (c, v) => c.Sav.SecondsToFame = (uint)Segundos(v)));
        }

        var secciones = new List<SeccionP> { new(IdiomaInterfaz.T("Form.Entrenador"), entrenador), new(IdiomaInterfaz.T("Form.TiempoJuego"), tiempo) };

        // ---- Medallas ----
        if (!esColoXd)
        {
            IEnumerable<CampoP> medallas = sav switch
            {
                SAV1 => P.Medallas("med", 8, c => ((SAV1)c.Sav).Badges, (c, v) => ((SAV1)c.Sav).Badges = v & 0xFF),
                SAV2 => P.Medallas("med", 16, c => ((SAV2)c.Sav).Badges, (c, v) => ((SAV2)c.Sav).Badges = v & 0xFFFF, NombreMedallaGsc),
                SAV3 => P.Medallas("med", 8, c => ((SAV3)c.Sav).Badges, (c, v) => ((SAV3)c.Sav).Badges = v & 0xFF),
                SAV4HGSS => P.Medallas("med", 16, c => ((SAV4HGSS)c.Sav).Badges | (((SAV4HGSS)c.Sav).Badges16 << 8), (c, v) => { var s = (SAV4HGSS)c.Sav; s.Badges = (byte)v; s.Badges16 = v >> 8; }, i => i < 8 ? IdiomaInterfaz.F("Form.JohtoNumero", i + 1) : IdiomaInterfaz.F("Form.KantoNumero", i - 7)),
                SAV4 => P.Medallas("med", 8, c => ((SAV4)c.Sav).Badges, (c, v) => ((SAV4)c.Sav).Badges = (byte)v),
                SAV5 => P.Medallas("med", 8, c => ((SAV5)c.Sav).Misc.Badges, (c, v) => ((SAV5)c.Sav).Misc.Badges = v & 0xFF),
                _ => [],
            };
            secciones.Add(new SeccionP(IdiomaInterfaz.T("Form.Medallas"), [.. medallas]));
        }

        // ---- Opciones del juego (portatiles) ----
        if (portatil) secciones.Add(new SeccionP(IdiomaInterfaz.T("Form.OpcionesJuego"), Opciones(sav)));

        // ---- Pikachu amarillo ----
        if (sav is SAV1 s1y && s1y.Version.Contains(GameVersion.YW))
            secciones.Add(new SeccionP(ProgramaPkhex.Textos.specieslist[(int)Species.Pikachu], [
                P.Entero("pikaamistad", IdiomaInterfaz.F("Form.PikachuAmistad", ProgramaPkhex.Textos.specieslist[(int)Species.Pikachu]), c => ((SAV1)c.Sav).PikaFriendship, (c, x) => ((SAV1)c.Sav).PikaFriendship = (byte)x, 0, 255),
                P.Entero("pikaplaya", IdiomaInterfaz.F("Form.PikachuPlaya", ProgramaPkhex.Textos.specieslist[(int)Species.Pikachu]), c => ((SAV1)c.Sav).PikaBeachScore, (c, x) => ((SAV1)c.Sav).PikaBeachScore = (ushort)x, 0, 9999),
            ]));

        // ---- Lugar (mapa) y region ----
        if (tieneMapa)
        {
            secciones.Add(new SeccionP(IdiomaInterfaz.T("Form.PosicionMapa"), [
                P.Entero("mapa", IdiomaInterfaz.T("Form.Mapa"), c => c.Sav switch { SAV4 s => s.M, SAV5 s => s.PlayerPosition.M, _ => 0 }, (c, x) => { if (c.Sav is SAV4 s4) s4.M = (int)x; else if (c.Sav is SAV5 s5) s5.PlayerPosition.M = (int)x; }, 0, 65535),
                P.Entero("x", "X", c => c.Sav switch { SAV4 s => s.X, SAV5 s => s.PlayerPosition.X, _ => 0 }, (c, x) => { if (c.Sav is SAV4 s4) s4.X = (int)x; else if (c.Sav is SAV5 s5) s5.PlayerPosition.X = (int)x; }, 0, 65535),
                P.Entero("z", "Z", c => c.Sav switch { SAV4 s => s.Z, SAV5 s => s.PlayerPosition.Z, _ => 0 }, (c, x) => { if (c.Sav is SAV4 s4) s4.Z = (int)x; else if (c.Sav is SAV5 s5) s5.PlayerPosition.Z = (int)x; }, 0, 65535),
                P.Entero("y", "Y", c => c.Sav switch { SAV4 s => s.Y, SAV5 s => s.PlayerPosition.Y, _ => 0 }, (c, x) => { if (c.Sav is SAV4 s4) s4.Y = (int)x; else if (c.Sav is SAV5 s5) s5.PlayerPosition.Y = (int)x; }, 0, 65535),
            ]));
            var g = gen;
            secciones.Add(new SeccionP(IdiomaInterfaz.T("Form.PaisRegion"), [
                P.Opcion("pais", IdiomaInterfaz.T("Form.Pais"), c => c.Sav switch { SAV4 s => s.Country, SAV5 s => s.Country, _ => 0 }, (c, x) => { if (c.Sav is SAV4 s4) s4.Country = (int)x; else if (c.Sav is SAV5 s5) s5.Country = (int)x; }, _ => Paises(g), reinicia: ["region"], titulo: IdiomaInterfaz.T("Form.Pais")),
                P.Opcion("region", IdiomaInterfaz.T("Form.Region"), c => c.Sav switch { SAV4 s => s.Region, SAV5 s => s.Region, _ => 0 }, (c, x) => { if (c.Sav is SAV4 s4) s4.Region = (int)x; else if (c.Sav is SAV5 s5) s5.Region = (int)x; }, c => Regiones(g, (int)c.Entero("pais")), titulo: IdiomaInterfaz.T("Form.Region"), prioridad: 1),
            ]));
        }

        return new EditorDefP("entrenador", IdiomaInterfaz.T("Form.Entrenador"), [new PestanaP("datos", IdiomaInterfaz.T("Form.EntrenadorSimple.Datos"), secciones)], Censo,
            IdiomaInterfaz.T("Form.EntrenadorSimple.Descripcion"));
    }

    /// <summary>Segundos desde el 1-ene-2000 de una fecha y hora (la forma de <c>DateUtil</c>: dia por un lado, hora del dia por otro).</summary>
    public static int Segundos(DateTime v) => DateUtil.GetSecondsFrom2000(v.Date, new DateTime(2000, 1, 1) + v.TimeOfDay);

    public static DateTime Fecha(uint segundos)
    {
        DateUtil.GetDateTime2000(segundos, out var fecha, out var hora);
        return fecha.Date + hora.TimeOfDay;
    }

    private static IReadOnlyList<CampoP> Opciones(SaveFile sav)
    {
        var l = new List<CampoP>();
        switch (sav)
        {
            case SAV1:
                l.Add(P.Marca("efectos", IdiomaInterfaz.T("Form.EfectosCombate"), c => ((SAV1)c.Sav).BattleEffects, (c, v) => ((SAV1)c.Sav).BattleEffects = v));
                l.Add(P.Opcion("estilo", IdiomaInterfaz.T("Form.EstiloCombate"), c => ((SAV1)c.Sav).BattleStyleSwitch ? 0 : 1, (c, v) => ((SAV1)c.Sav).BattleStyleSwitch = v == 0, _ => EstiloCombate));
                if (sav.Version.Contains(GameVersion.YW))
                    l.Add(P.Opcion("sonido", IdiomaInterfaz.T("Form.Sonido"), c => ((SAV1)c.Sav).Sound, (c, v) => ((SAV1)c.Sav).Sound = (int)v, _ => SonidoGb));
                l.Add(P.Opcion("velocidad", IdiomaInterfaz.T("Form.VelocidadTexto"), c => ((SAV1)c.Sav).TextSpeed, (c, v) => ((SAV1)c.Sav).TextSpeed = (int)v, _ => VelocidadGb));
                break;
            case SAV2:
                l.Add(P.Marca("efectos", IdiomaInterfaz.T("Form.EfectosCombate"), c => ((SAV2)c.Sav).BattleEffects, (c, v) => ((SAV2)c.Sav).BattleEffects = v));
                l.Add(P.Opcion("estilo", IdiomaInterfaz.T("Form.EstiloCombate"), c => ((SAV2)c.Sav).BattleStyleSwitch ? 0 : 1, (c, v) => ((SAV2)c.Sav).BattleStyleSwitch = v == 0, _ => EstiloCombate));
                l.Add(P.Opcion("sonido", IdiomaInterfaz.T("Form.Sonido"), c => ((SAV2)c.Sav).Sound > 0 ? 1 : 0, (c, v) => ((SAV2)c.Sav).Sound = v > 0 ? 2 : 0, _ => Sonido));
                l.Add(P.Opcion("velocidad", IdiomaInterfaz.T("Form.VelocidadTexto"), c => ((SAV2)c.Sav).TextSpeed, (c, v) => ((SAV2)c.Sav).TextSpeed = (int)v, _ => VelocidadGb));
                break;
            case SAV3:
                l.Add(P.Marca("efectos", IdiomaInterfaz.T("Form.EfectosCombate"), c => !((SAV3)c.Sav).SmallBlock.OptionBattleScene, (c, v) => ((SAV3)c.Sav).SmallBlock.OptionBattleScene = !v));
                l.Add(P.Opcion("estilo", IdiomaInterfaz.T("Form.EstiloCombate"), c => ((SAV3)c.Sav).SmallBlock.OptionBattleStyle ? 1 : 0, (c, v) => ((SAV3)c.Sav).SmallBlock.OptionBattleStyle = v == 1, _ => EstiloCombate));
                l.Add(P.Opcion("sonido", IdiomaInterfaz.T("Form.Sonido"), c => ((SAV3)c.Sav).SmallBlock.OptionSound ? 1 : 0, (c, v) => ((SAV3)c.Sav).SmallBlock.OptionSound = v == 1, _ => Sonido));
                l.Add(P.Opcion("velocidad", IdiomaInterfaz.T("Form.VelocidadTexto"), c => ((SAV3)c.Sav).SmallBlock.TextSpeed, (c, v) => ((SAV3)c.Sav).SmallBlock.TextSpeed = (int)v, _ => VelocidadGba));
                break;
        }
        return l;
    }
}
