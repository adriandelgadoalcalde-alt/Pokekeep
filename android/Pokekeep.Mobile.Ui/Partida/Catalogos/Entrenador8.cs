// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;
using Pokekeep.Mobile.Ui.Ajustes.Pkhex;
using Pokekeep.Mobile.Ui.Partida.Formularios.Gen1a3;
using static Pokekeep.Mobile.Ui.Partida.TextosPkhex;

namespace Pokekeep.Mobile.Ui.Partida.Catalogos;

/// <summary>
/// Entrenador de Espada y Escudo (<c>SAV_Trainer8</c>): General, Mapa, Valores varios y Equipo, con las mismas llamadas y el mismo orden que <c>Save()</c>.
/// </summary>
internal static class Entrenador8
{
    public static readonly string[] Censo = ["PK-SAV-171", "PK-SAV-172", "PK-SAV-173", "PK-SAV-174", "PK-SAV-175", "PK-SAV-231"];
    private static string T(string c, string? alt = null) => Texto($"SAV_Trainer8.{c}", alt);
    private static SAV8SWSH S(ContextoP c) => (SAV8SWSH)c.Sav;
    private static int IndicePiel(SAV8SWSH s) => (int)PlayerSkinColor8Extensions.GetSkinColorFromSkin(s.MyStatus.Skin);

    public static EditorDefP Crear(SAV8SWSH sav)
    {
        var pestanas = new List<PestanaP>
        {
            new("general", T("Tab_Overview", IdiomaInterfaz.T("Form.General")),
            [
                new(IdiomaInterfaz.T("Form.Entrenador"),
                [
                    P.Opcion("juego", IdiomaInterfaz.T("Form.Juego"), c => (long)S(c).Version, (c, v) => S(c).Version = (GameVersion)v, c => P.Lista(c.Datos.VersionDataSource.Where(z => (GameVersion)z.Value is GameVersion.SW or GameVersion.SH))),
                    P.Opcion("genero", IdiomaInterfaz.T("Form.Genero"), c => S(c).Gender, (c, v) =>
                    {
                        var s = S(c);
                        if (s.Gender == (byte)v) return;
                        // Cambiar de genero en PKHeX rehace el aspecto: la piel pasa a la de su paridad y se reinicia la ropa (ResetAppearance).
                        s.Gender = s.MyStatus.GenderAppearance = (byte)v;
                        var actual = IndicePiel(s);
                        var idx = ((actual < 0 ? 2 : actual) & ~1) | (int)(v & 1);   // una partida sin piel reconocida (en blanco) parte de la «por defecto»
                        if (idx != actual) s.MyStatus.SetSkinColor((PlayerSkinColor8)idx);
                        s.MyStatus.ResetAppearance((PlayerSkinColor8)idx);
                    }, _ => AyudasGen3.Generos,
                        deriva: c => new Dictionary<string, object?> { ["piel"] = ((c.Entero("piel") < 0 ? 2L : c.Entero("piel")) & ~1L) | (c.Entero("genero") & 1) }),
                    P.Opcion("piel", T("L_SkinColor", IdiomaInterfaz.T("Form.Trainer8.Piel")), c => IndicePiel(S(c)), (c, v) => { if (IndicePiel(S(c)) != (int)v) S(c).MyStatus.SetSkinColor((PlayerSkinColor8)v); }, _ => EnumLista<PlayerSkinColor8>(), prioridad: 3, motivo: c => IndicePiel(S(c)) < 0 ? IdiomaInterfaz.T("Form.Trainer8.PielAusente") : null),
                    P.Texto("ot", IdiomaInterfaz.T("Form.NombreEntrenador"), c => S(c).OT, (c, v) => { if (S(c).OT != v) S(c).OT = v; }, maxFn: c => 12),
                    .. IdsP.Campos(sav),
                    P.Entero("dinero", IdiomaInterfaz.T("Form.Dinero"), c => S(c).Money, (c, v) => S(c).Money = (uint)v, 0, sav.MaxMoney, ayuda: IdiomaInterfaz.F("Form.Maximo", sav.MaxMoney)),
                    P.Entero("vatios", T("L_Watt", IdiomaInterfaz.T("Form.Trainer8.Vatios")), c => S(c).MyStatus.Watt, (c, v) =>
                    {
                        S(c).MyStatus.Watt = (uint)v;
                        if (S(c).GetRecord(Record8.WattTotal) < (uint)v) S(c).SetRecord(Record8.WattTotal, (int)v);
                    }, 0, MyStatus8.MaxWatt, ayuda: IdiomaInterfaz.F("Form.Maximo", MyStatus8.MaxWatt)),
                    P.Entero("pb", T("L_BP", IdiomaInterfaz.T("Form.Trainer8.BP")), c => S(c).Misc.BP, (c, v) => S(c).Misc.BP = (int)v, 0, 9999),
                    P.Opcion("idioma", T("L_Language", IdiomaInterfaz.T("Form.Trainer7b.Idioma")), c => S(c).Language, (c, v) => S(c).Language = (int)v, c => P.Lista(c.Datos.LanguageDataSource(8, EntityContext.Gen8))),
                ]),
                new(IdiomaInterfaz.T("Form.Trainer8.Carne"),
                [
                    P.Texto("carneot", T("L_TrainerCardName", IdiomaInterfaz.T("Form.Trainer8.CarneNombre")), c => S(c).Blocks.TrainerCard.OT, (c, v) => { if (S(c).Blocks.TrainerCard.OT != v) S(c).Blocks.TrainerCard.OT = v; }, 12),
                    P.Texto("carnenumero", T("L_TrainerCardNumber", IdiomaInterfaz.T("Form.Trainer8.CarneNumero")), c => S(c).Blocks.TrainerCard.Number.TrimEnd((char)0), (c, v) => S(c).Blocks.MyStatus.Number = S(c).Blocks.TrainerCard.Number = v, 3, ayuda: IdiomaInterfaz.T("Form.Trainer8.CarneNumeroAyuda")),
                    P.Entero("carneid", T("L_TrainerCardID", IdiomaInterfaz.T("Form.Trainer8.CarneID")), c => S(c).Blocks.TrainerCard.TrainerID, (c, v) => S(c).Blocks.TrainerCard.TrainerID = (int)v, 0, 999_999),
                    P.Entero("rotorally", T("L_RotoRally", IdiomaInterfaz.T("Form.Trainer8.RotoRally")), c => S(c).Blocks.TrainerCard.RotoRallyScore, (c, v) => S(c).Blocks.TrainerCard.RotoRallyScore = (int)v, 0, 99_999),
                ]),
                new(IdiomaInterfaz.T("Form.Trainer7.Aventura"),
                [
                    P.Entero("horas", IdiomaInterfaz.T("Form.HorasJugadas"), c => S(c).PlayedHours, (c, v) => S(c).PlayedHours = (ushort)v, 0, 99_999, grupo: "tiempo"),
                    P.Entero("minutos", IdiomaInterfaz.T("Form.Minutos"), c => S(c).PlayedMinutes, (c, v) => S(c).PlayedMinutes = (ushort)v % 60, 0, 59, grupo: "tiempo"),
                    P.Entero("segundos", IdiomaInterfaz.T("Form.Segundos"), c => S(c).PlayedSeconds, (c, v) => S(c).PlayedSeconds = (ushort)v % 60, 0, 59, grupo: "tiempo"),
                    new CampoP { Id = "inicio", Etiqueta = T("L_Started", IdiomaInterfaz.T("Form.Trainer7b.Inicio")), Tipo = TipoP.FechaHora, SoloFecha = true, Leer = c => Inicio(S(c)), Escribir = (c, v) => { var d = (DateTime)v!; var t = S(c).TrainerCard; t.StartedYear = (ushort)d.Year; t.StartedMonth = (byte)d.Month; t.StartedDay = (byte)d.Day; } },
                    new CampoP
                    {
                        Id = "guardado", Etiqueta = T("L_LastSaved", IdiomaInterfaz.T("Form.Trainer7b.Guardado")), Tipo = TipoP.FechaHora, SinSegundos = true,
                        Leer = c => S(c).Played.LastSavedDate is { } d ? new DateTime(d.Year, d.Month, d.Day, d.Hour, d.Minute, 0) : DateTime.MinValue,
                        Escribir = (c, v) => S(c).Played.LastSavedDate = ((DateTime)v!).Date.AddMinutes(((DateTime)v!).TimeOfDay.TotalMinutes),
                        Visible = c => S(c).Played.LastSavedDate.HasValue,
                    },
                ]),
                new(IdiomaInterfaz.T("Form.Trainer8.ModaAspecto"),
                [
                    P.Opcion("moda", IdiomaInterfaz.T("Form.Trainer7b.Moda"), c => 0, (c, v) => CargarModa(S(c), (int)v), _ => P.Lista(IdiomaInterfaz.T("Form.Trainer7.SinCambios"), IdiomaInterfaz.T("Form.Trainer7.JuegoNuevo"), IdiomaInterfaz.T("Form.Trainer7.TodosLegales"), IdiomaInterfaz.T("Form.Trainer7.Todo")), soloAccion: true, prioridad: 9, ayuda: IdiomaInterfaz.T("Form.Trainer8.ModaAyuda")),
                    P.Opcion("reiniciaraspecto", IdiomaInterfaz.T("Form.Trainer8.ReiniciarAspecto"), c => 0, (c, v) => { if (v == 1) S(c).MyStatus.ResetAppearance((PlayerSkinColor8)Math.Max(0, IndicePiel(S(c)))); }, _ => P.Lista(IdiomaInterfaz.T("Form.Trainer7.SinCambios"), IdiomaInterfaz.T("Form.Trainer8.ReiniciarAspectoOpcion")), soloAccion: true, prioridad: 9, deriva: c => c.Entero("reiniciaraspecto") == 1 ? new Dictionary<string, object?> { ["piel"] = Math.Max(0L, c.Entero("piel")) } : new Dictionary<string, object?>()),
                    P.Opcion("diglett", IdiomaInterfaz.T("Form.Trainer8.Diglett"), c => 0, (c, v) => { if (v == 1) S(c).UnlockAllDiglett(); }, _ => P.Lista(IdiomaInterfaz.T("Form.Trainer7.SinCambios"), IdiomaInterfaz.T("Form.Trainer8.Todos")), soloAccion: true, prioridad: 9, visible: c => S(c).SaveRevision != 0 && Bloque(c, 0x90189DE0)),
                ]),
            ]),
            new("mapa", T("Tab_BadgeMap", IdiomaInterfaz.T("Form.Trainer7b.Mapa")), [new(T("GB_Map", IdiomaInterfaz.T("Form.Trainer7b.PosicionMapa")), [.. Mapa()])]),
            new("varios", T("Tab_MiscValues", IdiomaInterfaz.T("Form.Trainer8.Varios")),
            [
                new(IdiomaInterfaz.T("Form.Trainer8.Torre"),
                [
                    P.Info("torre-ausente", IdiomaInterfaz.T("Form.Trainer8.Datos"), _ => 0L,
                        (_, _) => IdiomaInterfaz.T("Form.Trainer8.SinTorre"),
                        visible: c => !Bloque(c, SaveBlockAccessor8SWSH.KBattleTowerSinglesVictory)
                            && !Bloque(c, SaveBlockAccessor8SWSH.KBattleTowerDoublesVictory)
                            && !Bloque(c, SaveBlockAccessor8SWSH.KBattleTowerSinglesStreak)
                            && !Bloque(c, SaveBlockAccessor8SWSH.KBattleTowerDoublesStreak)),
                    P.Entero("torre1v", IdiomaInterfaz.T("Form.Trainer8.VictoriasIndividuales"), c => S(c).GetValue<uint>(SaveBlockAccessor8SWSH.KBattleTowerSinglesVictory), (c, v) => { S(c).SetValue(SaveBlockAccessor8SWSH.KBattleTowerSinglesVictory, (uint)v); S(c).SetRecord(RecordLists.G8BattleTowerSingleWin, (int)v); }, 0, 9_999_999, visible: c => Bloque(c, SaveBlockAccessor8SWSH.KBattleTowerSinglesVictory)),
                    P.Entero("torre2v", IdiomaInterfaz.T("Form.Trainer8.VictoriasDobles"), c => S(c).GetValue<uint>(SaveBlockAccessor8SWSH.KBattleTowerDoublesVictory), (c, v) => { S(c).SetValue(SaveBlockAccessor8SWSH.KBattleTowerDoublesVictory, (uint)v); S(c).SetRecord(RecordLists.G8BattleTowerDoubleWin, (int)v); }, 0, 9_999_999, visible: c => Bloque(c, SaveBlockAccessor8SWSH.KBattleTowerDoublesVictory)),
                    P.Entero("torre1r", IdiomaInterfaz.T("Form.Trainer8.RachaIndividual"), c => S(c).GetValue<ushort>(SaveBlockAccessor8SWSH.KBattleTowerSinglesStreak), (c, v) => S(c).SetValue(SaveBlockAccessor8SWSH.KBattleTowerSinglesStreak, (ushort)v), 0, 300, visible: c => Bloque(c, SaveBlockAccessor8SWSH.KBattleTowerSinglesStreak)),
                    P.Entero("torre2r", IdiomaInterfaz.T("Form.Trainer8.RachaDoble"), c => S(c).GetValue<ushort>(SaveBlockAccessor8SWSH.KBattleTowerDoublesStreak), (c, v) => S(c).SetValue(SaveBlockAccessor8SWSH.KBattleTowerDoublesStreak, (ushort)v), 0, 300, visible: c => Bloque(c, SaveBlockAccessor8SWSH.KBattleTowerDoublesStreak)),
                ]),
            ]),
        };
        pestanas.Add(Equipo(sav));
        pestanas.Add(new("registros", IdiomaInterfaz.T("Form.Estadisticas"), [], Tabla: TablasP.Registros(RecordLists.RecordList_8)));
        return new EditorDefP("entrenador8", IdiomaInterfaz.T("Form.Entrenador"), pestanas, Censo, IdiomaInterfaz.T("Form.Trainer8.Descripcion"));
    }

    /// <summary>El equipo que se ve en el carné de entrenador y en la pantalla de título (<c>TrainerCard.ViewPoke</c> y <c>TitleScreen.ViewPoke</c>, el PropertyGrid de «Equipo»), con los botones de copiarlo del equipo de la partida.</summary>
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "TrainerCard8Poke y TitleScreen8Poke son tipos de PKHeX que la aplicacion conserva enteros.")]
    /// <summary>Rotulo en castellano de las propiedades del Pokemon del carne y de la pantalla de titulo; lo que no tiene traduccion clara (bytes sin nombre) dice «Dato sin nombre» con su desplazamiento.</summary>
    private static string NombreVista(string propiedad) => ProgramaPkhex.Idioma == "en" ? propiedad : propiedad switch
    {
        "EncryptionConstant" => IdiomaInterfaz.T("Form.Trainer8.EncryptionConstant"),
        "Form" => IdiomaInterfaz.T("Form.Trainer8.Form"),
        "FormArgument" => IdiomaInterfaz.T("Form.Trainer8.FormArgument"),
        "Gender" => IdiomaInterfaz.T("Form.Trainer8.Gender"),
        "IsShiny" => IdiomaInterfaz.T("Form.Trainer8.IsShiny"),
        "Nickname" => IdiomaInterfaz.T("Form.Trainer8.Nickname"),
        "Level" => IdiomaInterfaz.T("Form.Respaldo.SAV_Roamer3.L_Level"),
        _ when propiedad.StartsWith("Unknown", StringComparison.Ordinal) => IdiomaInterfaz.F("Form.Trainer8.DatoSinNombre", propiedad[7..]),
        _ => propiedad,
    };

    private static PestanaP Equipo(SAV8SWSH sav)
    {
        var todos = new List<CampoP>();
        var secciones = new List<SeccionP>();
        void Vista(string prefijo, string titulo, Func<int, object> ver)
        {
            for (var i = 0; i < 6; i++)
            {
                var k = i; var campos = new List<CampoP>();
                var species = P.Opcion($"{prefijo}{k}.Species", IdiomaInterfaz.T("Form.Trainer8.Species"), c => Convert.ToInt64(ver(k).GetType().GetProperty("Species")!.GetValue(ver(k))),
                    (c, v) => ver(k).GetType().GetProperty("Species")!.SetValue(ver(k), (ushort)v), c => P.Lista(c.Datos.SpeciesDataSource.Where(x => x.Value <= c.Sav.MaxSpeciesID)),
                    afectaA: _ => todos.Where(x => x.Id.StartsWith($"{prefijo}{k}.")).Select(x => x.Id), especie: true);
                campos.Add(species); todos.Add(species);
                foreach (var pi in ver(k).GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    if (pi.Name == "Species" || !pi.CanRead || !pi.CanWrite || pi.GetIndexParameters().Length != 0 || !BloquesP.Simple(pi.PropertyType)) continue;
                    var campo = pi.Name == "Gender" && Type.GetTypeCode(pi.PropertyType) is not (TypeCode.Boolean or TypeCode.String)
                        ? P.Opcion($"{prefijo}{k}.Gender", IdiomaInterfaz.T("Form.Trainer8.Gender"), c => Convert.ToInt64(pi.GetValue(ver(k))), (c, v) => pi.SetValue(ver(k), Convert.ChangeType(v, pi.PropertyType)), _ => P.Lista(IdiomaInterfaz.T("Form.Trainer8.Male"), IdiomaInterfaz.T("Form.Trainer8.Female"), IdiomaInterfaz.T("Form.Trainer8.Genderless")))
                        : BloquesP.Campo($"{prefijo}{k}", () => ver(k), null, pi, () => todos, NombreVista);
                    if (campo is not null) { campos.Add(campo); todos.Add(campo); }
                }
                secciones.Add(new(IdiomaInterfaz.F("Form.Trainer8.PokemonNumero", titulo, k + 1), campos));
            }
        }
        Vista("carne", IdiomaInterfaz.T("Form.Trainer8.CarneCorto"), k => sav.Blocks.TrainerCard.ViewPoke(k));
        Vista("titulo", IdiomaInterfaz.T("Form.Trainer8.Titulo"), k => sav.Blocks.TitleScreen.ViewPoke(k));
        secciones.Insert(0, new(IdiomaInterfaz.T("Form.Trainer8.Copiar"), [
            P.Opcion("copiarcarne", IdiomaInterfaz.T("Form.Trainer8.AlCarne"), c => 0, (c, v) => { if (v == 1) S(c).Blocks.TrainerCard.SetPartyData(); }, _ => P.Lista(IdiomaInterfaz.T("Form.Trainer7.SinCambios"), IdiomaInterfaz.T("Form.Trainer8.CopiarOpcion")), soloAccion: true, prioridad: 5,
                afectaA: _ => todos.Where(x => x.Id.StartsWith("carne")).Select(x => x.Id), ayuda: IdiomaInterfaz.T("Form.Trainer8.CopiarCarneAyuda")),
            P.Opcion("copiartitulo", IdiomaInterfaz.T("Form.Trainer8.AlTitulo"), c => 0, (c, v) => { if (v == 1) S(c).Blocks.TitleScreen.SetPartyData(); }, _ => P.Lista(IdiomaInterfaz.T("Form.Trainer7.SinCambios"), IdiomaInterfaz.T("Form.Trainer8.CopiarOpcion")), soloAccion: true, prioridad: 5,
                afectaA: _ => todos.Where(x => x.Id.StartsWith("titulo")).Select(x => x.Id), ayuda: IdiomaInterfaz.T("Form.Trainer8.CopiarTituloAyuda")),
        ]));
        return new("equipo", T("Tab_Team", IdiomaInterfaz.T("Form.Equipo")), secciones);
    }

    /// <summary>El bloque existe de verdad (en una partida en blanco el Core crea bloques falsos que ignoran lo que se les escribe).</summary>
    private static bool Bloque(ContextoP c, uint clave) => S(c).Accessor.TryGetBlock(clave, out var b) && b.Type != SCTypeCode.None;

    private static DateTime Inicio(SAV8SWSH s)
    {
        try { return new DateTime(s.TrainerCard.StartedYear, s.TrainerCard.StartedMonth, s.TrainerCard.StartedDay); }
        catch (ArgumentOutOfRangeException) { return DateTime.MinValue; }
    }

    private static void CargarModa(SAV8SWSH sav, int modo)
    {
        if (modo is < 1 or > 3) return;
        sav.Fashion.Clear();
        if (modo == 1) sav.Fashion.Reset();
        else if (modo == 2) sav.Fashion.UnlockAllLegal();
        else sav.Fashion.UnlockAll();
    }

    /// <summary>Solo las cifras del texto (0 si no hay ninguna) y, si pasa de 2^64-1, el maximo: el formulario no admite mas en su casilla numerica.</summary>
    internal static string MapaNormaliza(string t)
    {
        var cifras = new string((t ?? string.Empty).Where(char.IsAsciiDigit).ToArray()).TrimStart('0');
        return cifras.Length == 0 ? "0" : ulong.TryParse(cifras, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var n) ? n.ToString(System.Globalization.CultureInfo.InvariantCulture) : ulong.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static IEnumerable<CampoP> Mapa()
    {
        // M es un ulong (un hash de 64 bits: en una partida real pasa de 2^63): un entero con signo lo enseñaria negativo. Se edita como texto de cifras decimales, igual que el NumericUpDown del formulario (0..2^64-1).
        yield return P.Texto("mapa", IdiomaInterfaz.T("Form.Mapa"), c => S(c).Coordinates.M.ToString(System.Globalization.CultureInfo.InvariantCulture), (c, v) => S(c).Coordinates.M = ulong.Parse(v, System.Globalization.CultureInfo.InvariantCulture), 20, ayuda: IdiomaInterfaz.T("Form.Trainer7b.MapaAyuda"), grupo: "mapa", normaliza: MapaNormaliza);
        yield return P.Decimal("x", IdiomaInterfaz.T("Form.Trainer7b.X"), c => S(c).Coordinates.X, (c, v) => S(c).Coordinates.X = (float)v, -99_999_999, 99_999_999, decimales: 6, grupo: "mapa");
        yield return P.Decimal("z", IdiomaInterfaz.T("Form.Trainer7b.Z"), c => S(c).Coordinates.Z, (c, v) => S(c).Coordinates.Z = (float)v, -99_999_999, 99_999_999, decimales: 6, grupo: "mapa");
        yield return P.Decimal("y", IdiomaInterfaz.T("Form.Trainer7b.Y"), c => S(c).Coordinates.Y, (c, v) => S(c).Coordinates.Y = (float)v, -99_999_999, 99_999_999, decimales: 6, grupo: "mapa");
        yield return P.Decimal("sx", IdiomaInterfaz.T("Form.Trainer7b.SX"), c => S(c).Coordinates.SX, (c, v) => S(c).Coordinates.SX = (float)v, -99_999_999, 99_999_999, decimales: 6, grupo: "mapa");
        yield return P.Decimal("sz", IdiomaInterfaz.T("Form.Trainer7b.SZ"), c => S(c).Coordinates.SZ, (c, v) => S(c).Coordinates.SZ = (float)v, -99_999_999, 99_999_999, decimales: 6, grupo: "mapa");
        yield return P.Decimal("sy", IdiomaInterfaz.T("Form.Trainer7b.SY"), c => S(c).Coordinates.SY, (c, v) => S(c).Coordinates.SY = (float)v, -99_999_999, 99_999_999, decimales: 6, grupo: "mapa");
        yield return new CampoP
        {
            Id = "rotacion", Etiqueta = IdiomaInterfaz.T("Form.Trainer6.Modo3"), Tipo = TipoP.Decimal, Min = -99_999_999, Max = 99_999_999, Decimales = 6, Prioridad = 5, GrupoEscritura = "mapa",
            Leer = c => Math.Round(Math.Atan2(S(c).Coordinates.RZ, S(c).Coordinates.RW) * 360.0 / Math.PI, 6),
            Escribir = (c, v) =>
            {
                var angle = (double)v! * Math.PI / 360.0;
                var co = S(c).Coordinates;
                co.RX = 0; co.RZ = (float)Math.Sin(angle); co.RY = 0; co.RW = (float)Math.Cos(angle);
            },
        };
    }
}
