// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;
using Pokekeep.Mobile.Ui.Partida.Formularios.Gen1a3;
using static Pokekeep.Mobile.Ui.Partida.TextosPkhex;

namespace Pokekeep.Mobile.Ui.Partida.Catalogos;

/// <summary>
/// Entrenador de Let's Go, Pikachu! / Let's Go, Eevee! (<c>SAV_Trainer7GG</c>): nombre, rival, identificadores, dinero, lengua, mapa, tiempo, titulos y moda; con las mismas llamadas y el mismo orden que <c>Save()</c>.
/// El Complejo GO (<c>Tab_Complex</c>) tiene su propia pantalla (importar y exportar los .gp1).
/// </summary>
internal static class Entrenador7b
{
    public static readonly string[] Censo = ["PK-SAV-144", "PK-SAV-145", "PK-SAV-146", "PK-SAV-231"];
    private static string T(string c, string? alt = null) => Texto($"SAV_Trainer7GG.{c}", alt);
    private static SAV7b S(ContextoP c) => (SAV7b)c.Sav;

    public static EditorDefP Crear(SAV7b sav)
    {
        var pestanas = new List<PestanaP>
        {
            new("general", T("Tab_Overview", IdiomaInterfaz.T("Form.General")),
            [
                new(IdiomaInterfaz.T("Form.Entrenador"),
                [
                    P.Opcion("juego", IdiomaInterfaz.T("Form.Juego"), c => (long)S(c).Version, (c, v) => S(c).Version = (GameVersion)v, c => P.Lista(c.Datos.VersionDataSource.Where(z => (GameVersion)z.Value is GameVersion.GP or GameVersion.GE))),
                    P.Opcion("genero", IdiomaInterfaz.T("Form.Genero"), c => S(c).Gender, (c, v) => S(c).Gender = (byte)v, _ => AyudasGen3.Generos),
                    P.Texto("ot", IdiomaInterfaz.T("Form.NombreEntrenador"), c => S(c).OT, (c, v) => { if (S(c).OT != v) S(c).OT = v; }, maxFn: c => c.Sav.MaxStringLengthTrainer),
                    P.Texto("rival", T("L_RivalName", IdiomaInterfaz.T("Form.Trainer7b.Rival")), c => S(c).Blocks.Misc.RivalName, (c, v) => { if (S(c).Blocks.Misc.RivalName != v) S(c).Blocks.Misc.RivalName = v; }, maxFn: c => c.Sav.MaxStringLengthTrainer),
                    .. IdsP.Campos(sav),
                    P.Entero("dinero", IdiomaInterfaz.T("Form.Dinero"), c => S(c).Blocks.Misc.Money, (c, v) => S(c).Money = (uint)v, 0, 9_999_999, ayuda: IdiomaInterfaz.T("Form.Trainer6.MaxDinero")),
                    P.Opcion("idioma", T("L_Language", IdiomaInterfaz.T("Form.Trainer7b.Idioma")), c => S(c).Language, (c, v) => S(c).Language = (int)v, c => P.Lista(c.Datos.LanguageDataSource(7, EntityContext.Gen7b))),
                ]),
                new(IdiomaInterfaz.T("Form.Trainer7.Aventura"),
                [
                    P.Entero("horas", IdiomaInterfaz.T("Form.HorasJugadas"), c => S(c).PlayedHours, (c, v) => S(c).PlayedHours = (ushort)v, 0, 65535, grupo: "tiempo"),
                    P.Entero("minutos", IdiomaInterfaz.T("Form.Minutos"), c => S(c).PlayedMinutes, (c, v) => S(c).PlayedMinutes = (ushort)v % 60, 0, 59, grupo: "tiempo"),
                    P.Entero("segundos", IdiomaInterfaz.T("Form.Segundos"), c => S(c).PlayedSeconds, (c, v) => S(c).PlayedSeconds = (ushort)v % 60, 0, 59, grupo: "tiempo"),
                    P.FechaHora("inicio", T("L_AdventureBegin", IdiomaInterfaz.T("Form.Trainer7b.Inicio")), c => S(c).PlayerGeoLocation.AdventureBegin.Timestamp, (c, v) => S(c).PlayerGeoLocation.AdventureBegin.Timestamp = v.Date.AddSeconds(v.TimeOfDay.TotalSeconds)),
                    new CampoP
                    {
                        Id = "guardado", Etiqueta = T("L_LastSaved", IdiomaInterfaz.T("Form.Trainer7b.Guardado")), Tipo = TipoP.FechaHora,
                        Leer = c => S(c).Played.LastSavedDate ?? DateTime.MinValue,
                        Escribir = (c, v) => S(c).Played.LastSavedDate = ((DateTime)v!).Date.AddSeconds(((DateTime)v!).TimeOfDay.TotalSeconds),
                        Visible = c => S(c).Played.LastSavedDate.HasValue,
                    },
                ]),
                new(IdiomaInterfaz.T("Form.Trainer7.Desbloqueos"),
                [
                    P.Opcion("titulos", IdiomaInterfaz.T("Form.Trainer7b.Titulos"), c => 0, (c, v) => { if (v == 1) S(c).Blocks.EventWork.UnlockAllTitleFlags(); }, _ => P.Lista(IdiomaInterfaz.T("Form.Trainer7.SinCambios"), IdiomaInterfaz.T("Form.Trainer7b.Desbloquear")), soloAccion: true, ayuda: IdiomaInterfaz.T("Form.Trainer7b.TitulosAyuda")),
                    P.Opcion("moda", IdiomaInterfaz.T("Form.Trainer7b.Moda"), c => 0, (c, v) => { if (v == 1) { S(c).Blocks.FashionPlayer.UnlockAllAccessoriesPlayer(); S(c).Blocks.FashionStarter.UnlockAllAccessoriesStarter(); } }, _ => P.Lista(IdiomaInterfaz.T("Form.Trainer7.SinCambios"), IdiomaInterfaz.T("Form.Trainer7b.Desbloquear")), soloAccion: true, ayuda: IdiomaInterfaz.T("Form.Trainer7b.ModaAyuda")),
                ]),
            ]),
            new("mapa", T("Tab_BadgeMap", IdiomaInterfaz.T("Form.Trainer7b.Mapa")),
            [
                new(T("GB_Map", IdiomaInterfaz.T("Form.Trainer7b.PosicionMapa")),
                [
                    P.Texto("mapa", IdiomaInterfaz.T("Form.Mapa"), c => S(c).Coordinates.M.ToString(System.Globalization.CultureInfo.InvariantCulture), (c, v) => S(c).Coordinates.M = ulong.Parse(v, System.Globalization.CultureInfo.InvariantCulture), 20, ayuda: IdiomaInterfaz.T("Form.Trainer7b.MapaAyuda"), grupo: "mapa", normaliza: Entrenador8.MapaNormaliza),
                    P.Decimal("x", IdiomaInterfaz.T("Form.Trainer7b.X"), c => S(c).Coordinates.X, (c, v) => S(c).Coordinates.X = (float)v, -99_999_999, 99_999_999, decimales: 6, grupo: "mapa"),
                    P.Decimal("z", IdiomaInterfaz.T("Form.Trainer7b.Z"), c => S(c).Coordinates.Z, (c, v) => S(c).Coordinates.Z = (float)v, -99_999_999, 99_999_999, decimales: 6, grupo: "mapa"),
                    P.Decimal("y", IdiomaInterfaz.T("Form.Trainer7b.Y"), c => S(c).Coordinates.Y, (c, v) => S(c).Coordinates.Y = (float)v, -99_999_999, 99_999_999, decimales: 6, grupo: "mapa"),
                    P.Decimal("sx", IdiomaInterfaz.T("Form.Trainer7b.SX"), c => S(c).Coordinates.SX, (c, v) => S(c).Coordinates.SX = (float)v, -99_999_999, 99_999_999, decimales: 6, grupo: "mapa"),
                    P.Decimal("sz", IdiomaInterfaz.T("Form.Trainer7b.SZ"), c => S(c).Coordinates.SZ, (c, v) => S(c).Coordinates.SZ = (float)v, -99_999_999, 99_999_999, decimales: 6, grupo: "mapa"),
                    P.Decimal("sy", IdiomaInterfaz.T("Form.Trainer7b.SY"), c => S(c).Coordinates.SY, (c, v) => S(c).Coordinates.SY = (float)v, -99_999_999, 99_999_999, decimales: 6, grupo: "mapa"),
                    new CampoP
                    {
                        Id = "rotacion", Etiqueta = IdiomaInterfaz.T("Form.Trainer6.Modo3"), Tipo = TipoP.Decimal, Min = -99_999_999, Max = 99_999_999, Decimales = 6, Prioridad = 5, GrupoEscritura = "mapa",
                        Leer = c => Math.Round(Math.Atan2(S(c).Coordinates.RZ, S(c).Coordinates.RW) * 360.0 / Math.PI, 6),
                        Escribir = (c, v) =>
                        {
                            var angle = (double)v! * Math.PI / 360.0;
                            var co = S(c).Coordinates;
                            co.RX = 0; co.RZ = (float)Math.Sin(angle); co.RY = 0; co.RW = (float)Math.Cos(angle);
                        },
                    },
                ]),
            ]),
        };
        return new EditorDefP("entrenador7b", IdiomaInterfaz.T("Form.Entrenador"), pestanas, Censo, IdiomaInterfaz.T("Form.Trainer7b.Descripcion"));
    }
}
