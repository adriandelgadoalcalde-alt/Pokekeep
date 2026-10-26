// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Partida.Catalogos;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen4;

/// <summary>
/// PK-SAV-028 a 032: <c>SAV_BattlePass</c>, los 187 pases de combate de Pokémon Battle Revolution (personales, de préstamo, de amigo, descargados y otros). El formulario es una lista de pases y, al elegir uno, cuatro pestañas:
/// «Inicio» (<c>f_MAIN</c>: entrenador, aspecto, foto y diseño del pase y sus indicadores), «Creador» (<c>f_CREATOR</c>: historial de combates y datos de quien lo creó), «Pokémon» (<c>f_PKM</c>: el equipo de seis) y «Expresiones»
/// (<c>f_CATCHPHRASES</c>: las seis frases). Cada pase se escribe entero con <c>SaveCurrent</c> al cambiar de pase y al guardar; subir, bajar, quitar, importar y exportar actúan al momento sobre el pase.
/// <para>En el móvil la lista es una colección de 187 elementos y las cuatro pestañas son las cuatro secciones de cada elemento («Inicio», «Creador», «Pokémon», «Expresiones»), más una sección con las acciones del pase; «Desbloquear todos» de
/// los pases personales y de préstamo están en otra pestaña. Se mantiene el orden de <c>SaveCurrent</c>: nombre, título, TID, SID, aspecto, foto, diseño, indicadores, equipo, expresiones y datos del creador. Al cambiar de personaje el
/// formulario deja el aspecto en el de ese personaje y reinicia las expresiones predeterminadas (<c>UpdatePresetIndexes</c>): aquí es lo mismo, con el personaje como último campo en escribirse.</para>
/// <para>Divergencias declaradas en el ADR 0065: el formulario reescribe entero (y normaliza) cada pase por el que pasa y la app solo los campos que se cambian; «Poner Pokémon» (<c>ClickSet</c>, Mayús + clic con el editor de Pokémon) <b>no se porta</b>:
/// <c>BattlePass.SetPartySlotAtIndex</c> falla en este Core (escribe un Pokémon de 220 bytes en un hueco de 140), así que ni WinForms puede completarlo; el equipo se edita con las cajas, la caja, el espacio y las marcas de cada hueco y «Quitar».</para>
/// </summary>
internal static partial class PasesBr4
{
    public const string Form = "SAV_BattlePass";
    public static readonly string[] Censo = ["PK-SAV-028", "PK-SAV-029", "PK-SAV-030", "PK-SAV-031", "PK-SAV-032"];

    private static string L(string control) => EtiquetasLang.Requerir(Form, control);
    private static SAV4BR S(ContextoP c) => (SAV4BR)c.Sav;
    private static BattlePass Pase(ContextoP c, int i) => S(c).BattlePasses[i];
    private static string Ln(string control) => L(control).TrimEnd(':');

    /// <summary>Predicado barato de <see cref="Crear"/>: <c>B_OpenBattlePass</c> solo existe en Battle Revolution.</summary>
    public static bool Aplica(SaveFile sav) => sav is SAV4BR;

    public static EditorDefP? Crear(SaveFile sav)
    {
        if (sav is not SAV4BR) return null;
        ColeccionDefP? col = null;
        col = new ColeccionDefP
        {
            Id = "pases", Elemento = L("L_BattlePasses").TrimEnd(':'), Cantidad = _ => BattlePassAccessor.PASS_COUNT,
            Titulo = (c, i) => TituloDelPase(c, i), Resumen = (c, i) => ResumenDelPase(c, i),
            Secciones = (c, i) => [.. SeccionesDeInicio(col!, i), .. SeccionesDeCreador(col!, i), .. SeccionesDePokemon(col!, i), .. SeccionesDeExpresiones(col!, i), SeccionDeAcciones(col!, i)],
        };
        return new EditorDefP("pasesbr", TituloGen4.De("pasesbr"),
        [
            new PestanaP("pases", L("L_BattlePasses").TrimEnd(':'), [], Coleccion: col),
            new PestanaP("pases_todos", IdiomaInterfaz.F("Gen4.PestanaTodos", L("L_BattlePasses").TrimEnd(':')),
            [
                new SeccionP(string.Empty,
                [
                    P.Accion("desbloquear_personales", L("B_UnlockCustom"), c => S(c).BattlePasses.UnlockAllCustomPasses(), inmediata: true, textoBoton: IdiomaInterfaz.T("HerramientasGuardado.Desbloquear"), ayuda: IdiomaInterfaz.T("PasesBr4.MarcaDisponiblesTodosPases"),
                        confirmacion: IdiomaInterfaz.T("PasesBr4.MarcanDisponiblesTodosPases")),
                    P.Accion("desbloquear_prestamo", L("B_UnlockRental"), c => S(c).BattlePasses.UnlockAllRentalPasses(), inmediata: true, textoBoton: IdiomaInterfaz.T("HerramientasGuardado.Desbloquear"), ayuda: IdiomaInterfaz.T("PasesBr4.MarcaDisponiblesEntregadosTodos"),
                        confirmacion: IdiomaInterfaz.T("PasesBr4.MarcanDisponiblesEntregadosTodos")),
                ]),
            ]),
        ], Censo, IdiomaInterfaz.T("Partida.Especifico.pasesbr.Descripcion"));
    }

    /// <summary><c>LoadBattlePassList</c>: «NN tipo/nombre», y «(Ninguno)» si el pase no es de préstamo ni está entregado o no tiene nombre.</summary>
    private static string TituloDelPase(ContextoP c, int i)
    {
        var p = Pase(c, i);
        var nombre = p.Name;
        if (p is { Rental: false, Issued: false } || string.IsNullOrWhiteSpace(nombre)) nombre = Estilos[0];
        var tipo = EtiquetasLang.Obtener($"BattlePassType.{S(c).BattlePasses.GetPassType(i)}") ?? S(c).BattlePasses.GetPassType(i).ToString();
        return $"{i + 1:00} {tipo}/{nombre}";
    }

    private static string ResumenDelPase(ContextoP c, int i)
    {
        var p = Pase(c, i);
        var equipo = Enumerable.Range(0, BattlePass.Count).Count(k => p.GetPartySlotPresent(k));
        return $"{(p.Rental ? L("CHK_Rental") : p.Friend ? L("CHK_Friend") : p.Issued ? L("CHK_Issued") : p.Available ? L("CHK_Available") : "—")} · {equipo} Pokémon";
    }

    // ---------------------------------------------------------------- Inicio (f_MAIN)
    private static IEnumerable<SeccionP> SeccionesDeInicio(ColeccionDefP col, int i)
    {
        string Id(string campo) => col.IdCampo(i, campo);
        yield return new SeccionP(L("GB_Trainer"),
        [
            P.Texto(Id("nombre"), Ln("L_Name"), c => Pase(c, i).Name, (c, v) => Pase(c, i).Name = v, 11, prioridad: 0),
            P.Opcion(Id("titulo"), Ln("L_TrainerTitle"), c => Pase(c, i).TrainerTitle, (c, v) => Pase(c, i).TrainerTitle = (short)v, TitulosDeEntrenador, titulo: Ln("L_TrainerTitle"), prioridad: 1),
            P.Entero(Id("tid"), Ln("L_TID"), c => Pase(c, i).TID, (c, v) => Pase(c, i).TID = (ushort)v, 0, 65535, prioridad: 2),
            P.Entero(Id("sid"), Ln("L_SID"), c => Pase(c, i).SID, (c, v) => Pase(c, i).SID = (ushort)v, 0, 65535, prioridad: 3),
        ]);
        yield return new SeccionP(L("GB_Appearance"), Aspecto(col, i));
        yield return new SeccionP(string.Empty,
        [
            P.Opcion(Id("foto"), Ln("L_PictureType"), c => Pase(c, i).PictureType, (c, v) => Pase(c, i).PictureType = (int)v, _ => Fotos, titulo: Ln("L_PictureType"), prioridad: 20),
            P.Opcion(Id("diseno"), Ln("L_PassDesign"), c => Pase(c, i).PassDesign, (c, v) => Pase(c, i).PassDesign = (int)v, _ => Disenos, titulo: Ln("L_PassDesign"), prioridad: 21),
            P.Marca(Id("disponible"), L("CHK_Available"), c => Pase(c, i).Available, (c, v) => Pase(c, i).Available = v, prioridad: 22),
            P.Marca(Id("entregado"), L("CHK_Issued"), c => Pase(c, i).Issued, (c, v) => Pase(c, i).Issued = v, prioridad: 23),
            // CHK_Rental.CheckedChanged = UpdatePresetIndexes: al cambiar «Préstamo» el formulario reinicia los seis índices de frases predeterminadas (como al cambiar de personaje); aquí se hace tras escribir el resto, con el personaje ya escrito.
            P.Marca(Id("prestamo"), L("CHK_Rental"), c => Pase(c, i).Rental, (c, v) => { var p = Pase(c, i); p.Rental = v; p.ResetPresetIndexes(); }, prioridad: 401,
                afectaA: _ => [Id("saludo.indice"), Id("salida.indice"), Id("cambio1.indice"), Id("cambio2.indice"), Id("victoria.indice"), Id("derrota.indice")]),
            P.Marca(Id("amigo"), L("CHK_Friend"), c => Pase(c, i).Friend, (c, v) => Pase(c, i).Friend = v, prioridad: 25),
        ]);
    }

    // ---------------------------------------------------------------- Creador (f_CREATOR)
    private static IEnumerable<SeccionP> SeccionesDeCreador(ColeccionDefP col, int i)
    {
        string Id(string campo) => col.IdCampo(i, campo);
        var idPais = Id("pais"); var idIdioma = Id("idioma");
        IReadOnlyList<CampoP> historial =
        [
            Contador(Id("combates"), "L_Battles", c => Pase(c, i).Battles, (c, v) => Pase(c, i).Battles = (int)v, int.MinValue, int.MaxValue, 0),
            Contador(Id("r_coliseos"), "L_RecordColosseumBattles", c => Pase(c, i).RecordColosseumBattles, (c, v) => Pase(c, i).RecordColosseumBattles = (int)v, int.MinValue, int.MaxValue, 1),
            Contador(Id("r_libres"), "L_RecordFreeBattles", c => Pase(c, i).RecordFreeBattles, (c, v) => Pase(c, i).RecordFreeBattles = (int)v, int.MinValue, int.MaxValue, 2),
            Contador(Id("r_wifi"), "L_RecordWiFiBattles", c => Pase(c, i).RecordWiFiBattles, (c, v) => Pase(c, i).RecordWiFiBattles = (int)v, int.MinValue, int.MaxValue, 3),
            Contador(Id("c_portico"), "L_RecordGatewayColosseumClears", c => Pase(c, i).RecordGatewayColosseumClears, (c, v) => Pase(c, i).RecordGatewayColosseumClears = (int)v, 0, 255, 4),
            Contador(Id("c_calle"), "L_RecordMainStreetColosseumClears", c => Pase(c, i).RecordMainStreetColosseumClears, (c, v) => Pase(c, i).RecordMainStreetColosseumClears = (int)v, 0, 255, 5),
            Contador(Id("c_cascada"), "L_RecordWaterfallColosseumClears", c => Pase(c, i).RecordWaterfallColosseumClears, (c, v) => Pase(c, i).RecordWaterfallColosseumClears = (int)v, 0, 255, 6),
            Contador(Id("c_neon"), "L_RecordNeonColosseumClears", c => Pase(c, i).RecordNeonColosseumClears, (c, v) => Pase(c, i).RecordNeonColosseumClears = (int)v, 0, 255, 7),
            Contador(Id("c_cristal"), "L_RecordCrystalColosseumClears", c => Pase(c, i).RecordCrystalColosseumClears, (c, v) => Pase(c, i).RecordCrystalColosseumClears = (int)v, 0, 255, 8),
            Contador(Id("c_vergel"), "L_RecordSunnyParkColosseumClears", c => Pase(c, i).RecordSunnyParkColosseumClears, (c, v) => Pase(c, i).RecordSunnyParkColosseumClears = (int)v, 0, 255, 9),
            Contador(Id("c_magma"), "L_RecordMagmaColosseumClears", c => Pase(c, i).RecordMagmaColosseumClears, (c, v) => Pase(c, i).RecordMagmaColosseumClears = (int)v, 0, 255, 10),
            Contador(Id("c_bastion"), "L_RecordCourtyardColosseumClears", c => Pase(c, i).RecordCourtyardColosseumClears, (c, v) => Pase(c, i).RecordCourtyardColosseumClears = (int)v, 0, 255, 11),
            Contador(Id("c_ocaso"), "L_RecordSunsetColosseumClears", c => Pase(c, i).RecordSunsetColosseumClears, (c, v) => Pase(c, i).RecordSunsetColosseumClears = (int)v, 0, 255, 12),
            Contador(Id("c_planetario"), "L_RecordStargazerColosseumClears", c => Pase(c, i).RecordStargazerColosseumClears, (c, v) => Pase(c, i).RecordStargazerColosseumClears = (int)v, 0, 255, 13),
        ];
        yield return new SeccionP(L("GB_Records"), historial);
        yield return new SeccionP(L("GB_Creator"),
        [
            P.Texto(Id("creador"), Ln("L_CreatorName"), c => Pase(c, i).CreatorName, (c, v) => Pase(c, i).CreatorName = v, 11, prioridad: 100),
            // TB_BirthMonth/TB_BirthDay.MaxLength = 4, pero el campo del Core son 8 bytes UTF-16 (3 caracteres y el terminador): el cuarto se perderia en silencio. Se limita a 3 (divergencia declarada en el ADR 0065).
            P.Texto(Id("mes"), Ln("L_BirthMonth"), c => Pase(c, i).BirthMonth, (c, v) => Pase(c, i).BirthMonth = v, 3, prioridad: 101),
            P.Texto(Id("dia"), Ln("L_BirthDay"), c => Pase(c, i).BirthDay, (c, v) => Pase(c, i).BirthDay = v, 3, prioridad: 102),
            P.Opcion(idPais, Ln("L_Country"), c => Pase(c, i).Country, (c, v) => Pase(c, i).Country = (int)v, _ => EntrenadorSimple.Paises(4), reinicia: [Id("region")], titulo: Ln("L_Country"), prioridad: 103),
            P.Opcion(Id("region"), Ln("L_Region"), c => Pase(c, i).Region, (c, v) => Pase(c, i).Region = (int)v, c => EntrenadorSimple.Regiones(4, (int)c.Entero(idPais)), titulo: Ln("L_Region"), prioridad: 104),
            P.Opcion(idIdioma, Ln("L_Language"), c => (int)Pase(c, i).Language.ToLanguageID(), (c, v) => Pase(c, i).Language = ((LanguageID)v).ToBattlePassLanguage(), _ => Idiomas, titulo: Ln("L_Language"), prioridad: 105),
            // SaveCurrent: la presentacion se escribe con «￿» delante salvo en japones, y después del idioma que acaba de escribirse.
            Frase(Id("presentacion"), "L_SelfIntroduction", c => Pase(c, i).SelfIntroduction.TrimStart(StringConverter4GC.Proportional), (c, v) => Pase(c, i).SelfIntroduction = (Pase(c, i).Language == BattlePassLanguage.Japanese ? string.Empty : StringConverter4GC.Proportional.ToString()) + v,
                c => c.Entero(idIdioma) != (int)LanguageID.Japanese ? 51 : 53, 106),
            P.Texto(Id("region_codigo"), Ln("L_RegionCode"), c => Pase(c, i).RegionCode, (c, v) => Pase(c, i).RegionCode = v, 4, prioridad: 107),
            P.Texto(Id("id_jugador"), Ln("L_PlayerID"), c => Pase(c, i).PlayerID.ToString("X16"), (c, v) => Pase(c, i).PlayerID = Util.GetHexValue64(v), 16, prioridad: 108, ayuda: IdiomaInterfaz.T("Form.Trainer4BR.IDHexAyuda"),
                normaliza: t => Util.GetHexValue64(t).ToString("X16")),
        ]);
    }

    private static CampoP Contador(string id, string control, Func<ContextoP, long> leer, Action<ContextoP, long> escribir, long min, long max, int prioridad)
        => P.Entero(id, Ln(control), leer, escribir, min, max, prioridad: 50 + prioridad);

    /// <summary>
    /// Una frase (o la presentación): texto de una o varias líneas con el límite de <c>ValidateCatchphrase</c> (los saltos de línea y los marcadores especiales cuentan 2 y cada línea suma 2 más). El Core guarda los saltos con su propio carácter
    /// (<see cref="StringConverter4GC.LineBreak"/>); en el móvil son saltos de línea normales.
    /// </summary>
    private static CampoP Frase(string id, string control, Func<ContextoP, string> leer, Action<ContextoP, string> escribir, Func<ContextoP, int> maximo, int prioridad)
        => new()
        {
            Id = id, Etiqueta = Ln(control), Tipo = TipoP.Texto, Multilinea = true, Prioridad = prioridad,
            Leer = c => leer(c).Replace(StringConverter4GC.LineBreak, '\n'),
            Escribir = (c, v) => escribir(c, ((string)v!).Replace('\n', StringConverter4GC.LineBreak)),
            Validar = (c, t) => Entrenador4BR.Longitud(t) > maximo(c) ? IdiomaInterfaz.F("PasesBr4.CambiadoFraseAdmiteCaracteres", maximo(c)) : null,
            Ayuda = IdiomaInterfaz.T("PasesBr4.CadaSaltoLineaCuenta"),
        };

    // ---------------------------------------------------------------- Pokémon (f_PKM)
    private static IEnumerable<SeccionP> SeccionesDePokemon(ColeccionDefP col, int i)
    {
        for (var k = 0; k < BattlePass.Count; k++)
        {
            var hueco = k;
            string Id(string campo) => col.IdCampo(i, $"pk{hueco}.{campo}");
            yield return new SeccionP(L($"GB_PKM{hueco + 1}"),
            [
                P.Info(Id("pokemon"), IdiomaInterfaz.T("Nav.Pokemon"), c => DescripcionDelPokemon(c, i, hueco)),
                P.Entero(Id("caja"), Ln($"L_PKM{hueco + 1}Box"), c => Pase(c, i).GetPartySlotBoxSlot(hueco).Box, (c, v) => { var p = Pase(c, i); var (_, espacio) = p.GetPartySlotBoxSlot(hueco); p.SetPartySlotBoxSlot(hueco, (byte)v, espacio); }, 0, 255, prioridad: 200 + (hueco * 4)),
                P.Entero(Id("espacio"), Ln($"L_PKM{hueco + 1}Slot"), c => Pase(c, i).GetPartySlotBoxSlot(hueco).Slot, (c, v) => { var p = Pase(c, i); var (caja, _) = p.GetPartySlotBoxSlot(hueco); p.SetPartySlotBoxSlot(hueco, caja, (byte)v); }, 0, 255, prioridad: 201 + (hueco * 4)),
                P.Entero(Id("marcas"), Ln($"L_PKM{hueco + 1}Flags"), c => Pase(c, i).GetPartySlotFlags(hueco), (c, v) => Pase(c, i).SetPartySlotFlags(hueco, (ushort)v), 0, 65535, prioridad: 202 + (hueco * 4)),
                // ClickDelete (Alt + clic): SaveCurrent y DeletePartySlot; los siguientes suben y el último queda vacío. No hace nada si el hueco esta vacio.
                P.Accion(Id("quitar"), IdiomaInterfaz.T("PasesBr4.QuitarPokemon"), c => Pase(c, i).DeletePartySlot(hueco), motivo: c => Pase(c, i).GetPartySlotPresent(hueco) ? null : IdiomaInterfaz.T("PasesBr4.HuecoVacio"), inmediata: true, textoBoton: IdiomaInterfaz.T("PasesBr4.Quitar"),
                    confirmacion: IdiomaInterfaz.T("PasesBr4.QuitaPokemonPaseSiguientes"), ayuda: IdiomaInterfaz.T("PasesBr4.QuitaPokemonHuecoEquipo")),
            ]);
        }
    }

    private static string DescripcionDelPokemon(ContextoP c, int i, int hueco)
    {
        var p = Pase(c, i);
        if (!p.GetPartySlotPresent(hueco)) return "—";
        // El Core llama a Decrypt4BE con los 140 bytes del hueco (136 del Pokémon y 4 de caja/espacio/marcas) y su Debug.Assert exige 136: se le pasan los 136.
        var datos = p.Data.Slice(0x1FC + (BattlePass.PokeSize * hueco), BattlePass.PokeSize - 4).ToArray();
        PokeCrypto.Decrypt4BE(datos);
        var pk = new BK4(datos);
        return $"{c.Textos.specieslist[pk.Species]} · Nv. {pk.CurrentLevel}";
    }

    // ---------------------------------------------------------------- Expresiones (f_CATCHPHRASES)
    private static IEnumerable<SeccionP> SeccionesDeExpresiones(ColeccionDefP col, int i)
    {
        string Id(string campo) => col.IdCampo(i, campo);
        (string Clave, string Nombre, int Max, Func<BattlePass, string> Leer, Action<BattlePass, string> Escribir, Func<BattlePass, bool> LeerPred, Action<BattlePass, bool> EscribirPred, Func<BattlePass, ushort> LeerIdx, Action<BattlePass, ushort> EscribirIdx, string ChkControl, string LControl)[] frases =
        [
            ("saludo", "Greeting", 25, p => p.Greeting, (p, v) => p.Greeting = v, p => p.PresetGreeting, (p, v) => p.PresetGreeting = v, p => p.PresetGreetingIndex, (p, v) => p.PresetGreetingIndex = v, "CHK_PresetGreeting", "L_Greeting"),
            ("salida", "SentOut", 27, p => p.SentOut, (p, v) => p.SentOut = v, p => p.PresetSentOut, (p, v) => p.PresetSentOut = v, p => p.PresetSentOutIndex, (p, v) => p.PresetSentOutIndex = v, "CHK_PresetSentOut", "L_SentOut"),
            ("cambio1", "Shift1", 25, p => p.Shift1, (p, v) => p.Shift1 = v, p => p.PresetShift1, (p, v) => p.PresetShift1 = v, p => p.PresetShift1Index, (p, v) => p.PresetShift1Index = v, "CHK_PresetShift1", "L_Shift1"),
            ("cambio2", "Shift2", 25, p => p.Shift2, (p, v) => p.Shift2 = v, p => p.PresetShift2, (p, v) => p.PresetShift2 = v, p => p.PresetShift2Index, (p, v) => p.PresetShift2Index = v, "CHK_PresetShift2", "L_Shift2"),
            ("victoria", "Win", 51, p => p.Win, (p, v) => p.Win = v, p => p.PresetWin, (p, v) => p.PresetWin = v, p => p.PresetWinIndex, (p, v) => p.PresetWinIndex = v, "CHK_PresetWin", "L_Win"),
            ("derrota", "Lose", 51, p => p.Lose, (p, v) => p.Lose = v, p => p.PresetLose, (p, v) => p.PresetLose = v, p => p.PresetLoseIndex, (p, v) => p.PresetLoseIndex = v, "CHK_PresetLose", "L_Lose"),
        ];
        for (var n = 0; n < frases.Length; n++)
        {
            var f = frases[n]; var orden = n;
            yield return new SeccionP(Ln(f.LControl),
            [
                P.Marca(Id($"{f.Clave}.predeterminada"), L(f.ChkControl), c => f.LeerPred(Pase(c, i)), (c, v) => f.EscribirPred(Pase(c, i), v), prioridad: 300 + (orden * 3)),
                P.Entero(Id($"{f.Clave}.indice"), IdiomaInterfaz.T("PasesBr4.IndiceFrasePredeterminada"), c => f.LeerIdx(Pase(c, i)), (c, v) => f.EscribirIdx(Pase(c, i), (ushort)v), 0, 65535, prioridad: 301 + (orden * 3)),
                Frase(Id($"{f.Clave}.texto"), f.LControl, c => f.Leer(Pase(c, i)), (c, v) => f.Escribir(Pase(c, i), v), _ => f.Max, 302 + (orden * 3)),
            ]);
        }
    }

    // ---------------------------------------------------------------- Acciones del pase
    private static SeccionP SeccionDeAcciones(ColeccionDefP col, int i)
    {
        string Id(string campo) => col.IdCampo(i, campo);
        return new SeccionP(IdiomaInterfaz.T("PasesBr4.Pase"),
        [
            // B_Import / B_Export: importar copia los 0x6EC bytes en el pase (y vuelve a leerlo); exportar escribe los bytes del pase con su nombre.
            new CampoP
            {
                Id = Id("archivo"), Etiqueta = IdiomaInterfaz.T("PasesBr4.DatosPaseBin"), Tipo = TipoP.Archivo, Leer = _ => null, Extension = "bin", TextoBoton = L("B_Import"), Prioridad = -100,
                Escribir = (c, v) => { if (v is byte[] datos) datos.CopyTo(Pase(c, i).Data); },
                ValidarArchivo = (_, datos) => datos.Length == BattlePass.Size ? null : IdiomaInterfaz.F("PasesBr4.PaseCombateMideBytes", BattlePass.Size, datos.Length),
                Exporta = c => ($"{(string.IsNullOrWhiteSpace(Pase(c, i).Name) ? "Trainer" : PathUtil.CleanFileName(Pase(c, i).Name))}.bin", Pase(c, i).Data.ToArray()),
                Ayuda = IdiomaInterfaz.T("PasesBr4.ImportaExportaDatosPase"),
                Detalle = _ => IdiomaInterfaz.T("PasesBr4.ExportarGuardaPasePartida"),
            },
            P.Accion(Id("subir"), IdiomaInterfaz.T("PasesBr4.SubirPuesto"), c => S(c).BattlePasses.Swap(i, i - 1), motivo: _ => i == 0 ? IdiomaInterfaz.T("PasesBr4.PrimerPase") : null, inmediata: true, textoBoton: IdiomaInterfaz.T("PasesBr4.Subir"), ayuda: IdiomaInterfaz.T("PasesBr4.IntercambiaPaseAnterior")),
            P.Accion(Id("bajar"), IdiomaInterfaz.T("PasesBr4.BajarPuesto"), c => S(c).BattlePasses.Swap(i, i + 1), motivo: _ => i == BattlePassAccessor.PASS_COUNT - 1 ? IdiomaInterfaz.T("PasesBr4.UltimoPase") : null, inmediata: true, textoBoton: IdiomaInterfaz.T("PasesBr4.Bajar"), ayuda: IdiomaInterfaz.T("PasesBr4.IntercambiaPaseSiguiente")),
            P.Accion(Id("quitar"), IdiomaInterfaz.T("PasesBr4.QuitarPase"), c => S(c).BattlePasses.Delete(i), motivo: c => S(c).BattlePasses.GetPassType(i) == BattlePassType.Rental ? IdiomaInterfaz.T("PasesBr4.PasesPrestamoPuedenQuitar") : null, inmediata: true, textoBoton: IdiomaInterfaz.T("PasesBr4.Quitar"),
                confirmacion: IdiomaInterfaz.T("PasesBr4.VaciaPasePersonalesConservan"), ayuda: IdiomaInterfaz.T("PasesBr4.VaciaPase")),
        ]);
    }
}
