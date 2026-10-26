// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Buffers.Binary;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen6y7;

/// <summary>
/// PK-SAV-129 a PK-SAV-133 · <c>SAV_FestivalPlaza</c>, Festi Plaza de Sol/Luna y Ultrasol/Ultraluna. Las cuatro pestañas del original: «Resumen» (<c>Tab_Overview</c>: nombre, Festimonedas, rango, mensajes y hora de inicio),
/// «Desbloqueos» (<c>Tab_Unlock</c>: frases y premios por subir de rango, en dos pestañas de la app porque cada una es una tabla), «Instalaciones» (<c>Tab_Facility</c>: las 7 instalaciones y su visitante) y, solo en
/// Ultrasol/Ultraluna, «Agencia de batalla» (<c>Tab_BattleAgency</c>). El formulario de PKHeX no esta traducido (ni lleva textos en <c>lang_es.txt</c>): las etiquetas salen del diseñador en inglés y aqui se traducen en
/// <c>RespaldoGen6y7</c>; los textos del propio codigo (frases, tipos de instalacion, PNJ) se enseñan como PKHeX los trae, en ingles (ADR 0064).
/// Reglas calcadas: las Festimonedas actuales y las usadas (récord 38) forman un grupo (escribir las actuales recalcula el total con el récord); el nombre de la plaza solo se guarda en Ultrasol/Ultraluna (lo escribe
/// <c>SaveBattleAgency</c>); «Marcar todas», «Todos recibidos» y «Todos listos» cambian los controles y se guardan con «Guardar» (acciones de la tabla); «Borrar datos del visitante» actua de golpe (accion inmediata); las
/// derivaciones de la instalacion (el color se recorta al cambiar el tipo; el resultado de la lotería solo con el tipo 5 y las unidades restantes solo con el 7) son las del formulario.
/// </summary>
internal static class FestiPlaza
{
    public const string Form = "SAV_FestivalPlaza";
    public static readonly string[] Censo = ["PK-SAV-129", "PK-SAV-130", "PK-SAV-131", "PK-SAV-132", "PK-SAV-133"];
    private const int Instalaciones = (int)JoinFesta7.FestaFacilityCount;

    private static string L(string control) => EtiquetasLang.Requerir(Form, control);
    private static JoinFesta7 Festa(SaveFile sav) => ((SAV7)sav).Festa;
    private static FestaFacility Inst(SaveFile sav, int i) => Festa(sav).GetFestaFacility(i);

    /// <summary>Predicado barato de <see cref="Crear"/>: el editor existe para este guardado.</summary>
    public static bool Aplica(SaveFile sav) => sav is SAV7;

    // ---------------- listas del formulario (las mismas que construye su constructor) ----------------
    private static string[] Premios => [IdiomaInterfaz.T("FestiPlaza.RangoMisiones"), IdiomaInterfaz.T("FestiPlaza.RangoInstalacion"), IdiomaInterfaz.T("FestiPlaza.RangoModa"), IdiomaInterfaz.T("FestiPlaza.RangoCambiarNombre"), IdiomaInterfaz.T("FestiPlaza.RangoMenuEspecial"), IdiomaInterfaz.T("FestiPlaza.RangoMusica"), IdiomaInterfaz.T("FestiPlaza.RangoTemaGlitz"), IdiomaInterfaz.T("FestiPlaza.RangoTemaFairy"), IdiomaInterfaz.T("FestiPlaza.RangoTemaTone"), IdiomaInterfaz.T("FestiPlaza.RangoFrase"), IdiomaInterfaz.T("FestiPlaza.RangoActual")];

    private static string[] Mensajes => [IdiomaInterfaz.T("FestiPlaza.MensajeEncuentro"), IdiomaInterfaz.T("FestiPlaza.MensajeDespedida"), IdiomaInterfaz.T("FestiPlaza.Emocion"), IdiomaInterfaz.T("FestiPlaza.Decepcion")];

    private static readonly string[] CategoriasEn = ["Lottery", "Haunted", "Goody", "Food", "Bouncy", "Fortune", "Dye", "Exchange"];

    private static string[][] SubtiposEn =>     [
        ["BigDream", "GoldRush", "TreasureHunt"],
        ["GhostsDen", "TrickRoom", "ConfuseRay"],
        ["Ball", IdiomaInterfaz.T("Cajas.General"), "Battle", "SoftDrink", "Pharmacy"],
        ["Rare", "Battle", "FriendshipCafé", "FriendshipParlor"],
        ["Thump", "Clink", "Stomp"],
        ["Kanto", "Johto", "Hoenn", "Sinnoh", "Unova", "Kalos", IdiomaInterfaz.T("Nav.Pokemon")],
        ["Red", "Yellow", "Green", "Blue", "Orange", "NavyBlue", "Purple", "Pink"],
        ["Switcheroo"],
    ];

    /// <summary>Cuantas versiones tiene cada subtipo (3: 1-2-3, 4: 1-3-5, 5: 1 a 5).</summary>
    private static readonly byte[][] NivelesPorTipo =
    [
        [5, 5, 5], [5, 5, 5], [3, 5, 3, 3, 3], [5, 4, 5, 5], [5, 5, 5], [4, 4, 4, 4, 4, 4, 4], [4, 4, 4, 4, 4, 4, 4, 4], [3],
    ];

    /// <summary>Color de cada tipo de instalacion (indices de <see cref="FestivalPlazaFacilityColor"/>); el ultimo valor no cuenta para el maximo.</summary>
    private static readonly byte[][] ColoresPorTipo =
    [
        [0, 1, 2, 3], [4, 0, 5, 3], [1, 0, 5, 3], [6, 7, 0, 3], [4, 5, 8, 3], [0, 1, 2, 3], [0, 7, 8, 4, 5, 1, 9, 10], [11, 1, 5, 3],
    ];

    private static IReadOnlyList<OpcionP> TiposDeInstalacion(bool usum)
    {
        var l = new List<string>();
        for (var k = 0; k < NivelesPorTipo.Length - (usum ? 0 : 1); k++)   // «Exchange» solo en Ultrasol/Ultraluna
        {
            var arr = NivelesPorTipo[k];
            for (var j = 0; j < arr.Length; j++)
            {
                var nombre = $"{CategoriasEn[k]} {SubtiposEn[k][j]}";
                if (arr[j] == 4) { l.Add($"{nombre} 1"); l.Add($"{nombre} 3"); l.Add($"{nombre} 5"); }
                else for (var i = 0; i < arr[j]; i++) l.Add($"{nombre} {i + 1}");
            }
        }
        return P.Lista(l);
    }

    private static int TipoDeIndice(int indice, bool usum)
    {
        var max = usum ? 0x7F : 0x7C;
        if ((uint)indice > max + 1) return -1;
        return indice switch { < 0x0F => 0, < 0x1E => 1, < 0x2F => 2, < 0x41 => 3, < 0x50 => 4, < 0x65 => 5, < 0x7D => 6, _ => 7 };
    }

    private static int CuentaDeColores(int tipo, bool usum) => tipo >= 0 && tipo < ColoresPorTipo.Length - (usum ? 0 : 1) ? ColoresPorTipo[tipo].Length - 1 : 3;

    private static readonly string[] Resultados = BuildResultados();

    private static string[] BuildResultados()
    {
        string[] tipos = ["GTS", IdiomaInterfaz.T("FestiPlaza.IntercambioProdigioso"), "Battle Spot", IdiomaInterfaz.T("Partida.Especifico.festival.Titulo"), IdiomaInterfaz.T("FestiPlaza.Mision"), IdiomaInterfaz.T("FestiPlaza.TiendaLoteria"), IdiomaInterfaz.T("FestiPlaza.CasaEncantada")];
        var l = new List<string> { "ninguno" };
        foreach (var t in tipos) foreach (var nivel in new[] { "+", "++", "+++" }) l.Add($"{nivel} {t}");
        return [.. l];
    }

    private static string[] Pnj =>     [
        IdiomaInterfaz.T("FestiPlaza.EntrenadorGuay"), IdiomaInterfaz.T("FestiPlaza.EntrenadorGuay2"), "Veterano (♀)", "Veterano (♂)", "Oficinista (♂)", "Oficinista (♀)", "Punki (♂)", "Punki (♀)", "Criapokémon (♂)", "Criapokémon (♀)", IdiomaInterfaz.T("FestiPlaza.Joven"), IdiomaInterfaz.T("FestiPlaza.Chica"),
    ];

    /// <summary>Texto del rango de Festimonedas (<c>GetRankText</c>).</summary>
    internal static string TextoDeRango(int rank)
    {
        if (rank < 1) return string.Empty;
        if (rank == 1) return "0 - 5";
        if (rank == 2) return "6 - 15";
        if (rank == 3) return "16 - 30";
        if (rank <= 10) { var i = ((rank - 1) * (rank - 2) * 5) + 1; return $"{i} - {i + ((rank - 1) * 10) - 1}"; }
        if (rank <= 20) { var i = (rank * 100) - 649; return $"{i} - {i + 99}"; }
        if (rank <= 70) { var j = (rank - 1) / 10; var i = (rank * ((j * 30) + 60)) - ((j * j * 150) + (j * 180) + 109); return $"{i} - {i + (j * 30) + 59}"; }
        if (rank <= 100) { var i = (rank * 270) - 8719; return $"{i} - {i + 269}"; }
        if (rank <= 998) { var i = (rank * 300) - 11749; return $"{i} - {i + 299}"; }
        return rank == 999 ? "287951 - " : string.Empty;
    }

    private static string SoloHex(string t, int max)
    {
        var h = Util.GetOnlyHex(t);
        if (string.IsNullOrWhiteSpace(h)) h = "0";
        return (h.Length > max ? h[..max] : h).ToUpperInvariant();
    }

    // ---------------- Resumen ----------------
    private static IEnumerable<SeccionP> Resumen(SaveFile sav)
    {
        var usum = sav is SAV7USUM;
        yield return new SeccionP(string.Empty,
        [
            usum
                ? P.Texto("plaza", L("L_PlazaName"), c => Festa(c.Sav).FestivalPlazaName, (c, v) => Festa(c.Sav).FestivalPlazaName = v, 20, ayuda: IdiomaInterfaz.T("FestiPlaza.SoloGuardaUltrasolUltraluna"))
                : P.Info("plaza", L("L_PlazaName"), c => Festa(c.Sav).FestivalPlazaName),
            P.Entero("rango", L("L_Rank"), c => Festa(c.Sav).FestaRank, (c, v) => Festa(c.Sav).FestaRank = (ushort)v, 0, 999, detalle: c => IdiomaInterfaz.F("FestiPlaza.FestimonedasRango", TextoDeRango((int)c.Entero("rango")))),
        ]);
        yield return new SeccionP(L("GB_FC"),
        [
            // Guardar escribe primero el récord de las usadas y despues las actuales (que recalculan el total con ese récord): un grupo.
            P.Entero("fc-usadas", L("L_FC_Used"), c => Math.Min(((SAV7)c.Sav).GetRecord(38), 9_999_999), (c, v) => ((SAV7)c.Sav).SetRecord(38, (int)v), 0, 9_999_999, prioridad: 0, grupo: "fc"),
            P.Entero("fc-actuales", L("L_FC_Current"), c => Math.Min(Festa(c.Sav).FestaCoins, 9_999_999), (c, v) => Festa(c.Sav).FestaCoins = (int)v, 0, 9_999_999, prioridad: 1, grupo: "fc"),
            P.Info("fc-recogidas", L("L_FC_CollectedL"), c => (c.Entero("fc-actuales") + c.Entero("fc-usadas")).ToString(System.Globalization.CultureInfo.InvariantCulture)),
        ]);
        yield return new SeccionP(L("GB_MyMessage"), [.. Enumerable.Range(0, 4).Select(i => P.Entero($"mensaje{i}", L(i switch { 0 => "L_MyMessageMeet", 1 => "L_MyMessagePart", 2 => "L_MyMessageMoved", _ => "L_MyMessageDisappointed" }),
            c => Festa(c.Sav).GetFestaMessage(i), (c, v) => Festa(c.Sav).SetFestaMessage(i, (ushort)v), 0, 9999))]);
        yield return new SeccionP(L("GB_FestaStartTime"),
        [
            P.FechaHora("inicio", L("GB_FestaStartTime"), c => Festa(c.Sav).FestaDate ?? new DateTime(2000, 1, 1), (c, v) => Festa(c.Sav).FestaDate = new DateTime(v.Year, v.Month, v.Day, v.Hour, v.Minute, v.Second)),
        ]);
    }

    // ---------------- Desbloqueos ----------------
    private static TablaDefP Frases() => new()
    {
        Id = "frases", Marcador = IdiomaInterfaz.T("FestiPlaza.BuscarFrase"), SinNumero = true,
        Ayuda = IdiomaInterfaz.T("FestiPlaza.FrasesEstanInglesPKHeX"),
        Filas = _ =>
        {
            var t = FestiPlazaFrases.Textos;
            var l = new List<FilaTablaP>();
            // Primero la del rango 100 (indice 106) y despues las comunes, como la lista del formulario.
            l.Add(new FilaTablaP { Id = 106, Nombre = IdiomaInterfaz.T("FestiPlaza.AlcanzarRangoFestiPlaza"), EsMarca = true, Leer = x => Festa(x.Sav).GetFestaPhraseUnlocked(106), Escribir = (x, v) => Festa(x.Sav).SetFestaPhraseUnlocked(106, (bool)v) });
            for (var i = 0; i < t.Length - 1; i++) { var n = i; l.Add(new FilaTablaP { Id = n, Nombre = t[n], EsMarca = true, Leer = x => Festa(x.Sav).GetFestaPhraseUnlocked(n), Escribir = (x, v) => Festa(x.Sav).SetFestaPhraseUnlocked(n, (bool)v) }); }
            return l;
        },
        Acciones = [new AccionTablaP("todas", L("B_AllPhrases"), (_, filas) => [.. filas.Select(f => (f, (object)true))], IdiomaInterfaz.T("FestiPlaza.MarcaTodasFrases"))],
    };

    private static TablaDefP PremiosTabla() => new()
    {
        Id = "premios", Marcador = IdiomaInterfaz.T("FestiPlaza.BuscarPremio"), SinNumero = true,
        Ayuda = IdiomaInterfaz.T("FestiPlaza.RecogerListoRecogerRecibido"),
        Filas = c =>
        {
            var l = new List<FilaTablaP>();
            // El primero es el rango actual (indice 10); despues los diez premios (0 a 9). El formulario guarda 0 como sin marcar, 1 como «listo» y 2 como «recibido».
            for (var k = 0; k < Premios.Length; k++)
            {
                var indice = k == 0 ? 10 : k - 1; var nombre = k == 0 ? Premios[10] : Premios[k - 1];
                l.Add(new FilaTablaP { Id = indice, Nombre = nombre, Min = 0, Max = Math.Max(2, (int)Festa(c.Sav).GetFestPrizeReceived(indice)), Detalle = IdiomaInterfaz.T("FestiPlaza.RecogerListoRecibido"),
                    Leer = x => (long)Festa(x.Sav).GetFestPrizeReceived(indice), Escribir = (x, v) => Festa(x.Sav).SetFestaPrizeReceived(indice, (byte)(long)v) });
            }
            return l;
        },
        Acciones =
        [
            new AccionTablaP("recibidos", L("B_AllReceiveReward"), (_, filas) => [.. filas.Select(f => (f, (object)2L))], IdiomaInterfaz.T("FestiPlaza.TodosPremiosRecibidos")),
            new AccionTablaP("listos", L("B_AllReadyReward"), (_, filas) => [.. filas.Select(f => (f, (object)1L))], IdiomaInterfaz.T("FestiPlaza.TodosPremiosListosRecoger")),
        ],
    };

    // ---------------- Instalaciones ----------------
    private static IEnumerable<CampoP> CamposDeLaInstalacion(ColeccionDefP col, int i, bool usum)
    {
        string Id(string campo) => col.IdCampo(i, campo);
        var idTipo = Id("tipo"); var idColor = Id("color");
        int Tipo(ContextoP c) => TipoDeIndice((int)c.Entero(idTipo), usum);
        yield return P.Opcion(idTipo, L("L_FacilityType"), c => Inst(c.Sav, i).Type, (c, v) => Inst(c.Sav, i).Type = (int)v, _ => TiposDeInstalacion(usum), titulo: L("L_FacilityType"),
            deriva: c =>
            {
                // Cambiar el tipo recorta el color al maximo del tipo nuevo (CB_FacilityType_SelectedIndexChanged).
                var max = CuentaDeColores(Tipo(c), usum);
                return c.Entero(idColor) > max ? new Dictionary<string, object?> { [idColor] = (long)max } : new Dictionary<string, object?>();
            });
        yield return P.Entero(idColor, L("L_FacilityColor"), c => Math.Min(Inst(c.Sav, i).Color, CuentaDeColores(TipoDeIndice(Inst(c.Sav, i).Type, usum), usum)), (c, v) => Inst(c.Sav, i).Color = (int)v, 0, 7,
            maxFn: c => CuentaDeColores(Tipo(c), usum),
            detalle: c => Tipo(c) is var t && t >= 0 && t < ColoresPorTipo.Length - (usum ? 0 : 1) && c.Entero(idColor) is var k && k >= 0 && k < ColoresPorTipo[t].Length ? TextosPkhex.Enum((FestivalPlazaFacilityColor)ColoresPorTipo[t][k]) : null);
        yield return P.Opcion(Id("pnj"), L("L_FacilityNPC"), c => Inst(c.Sav, i).NPC < Pnj.Length ? Inst(c.Sav, i).NPC : 0, (c, v) => Inst(c.Sav, i).NPC = (int)v, _ => P.Lista(Pnj), titulo: L("L_FacilityNPC"));
        yield return P.Marca(Id("presentada"), L("CHK_FacilityIntroduced"), c => Inst(c.Sav, i).IsIntroduced, (c, v) => Inst(c.Sav, i).IsIntroduced = v);
        yield return P.Texto(Id("visitante"), L("L_VisitorName"), c => Inst(c.Sav, i).OriginalTrainerName, (c, v) => Inst(c.Sav, i).OriginalTrainerName = v, 12);
        yield return P.Opcion(Id("genero"), IdiomaInterfaz.F("Form.PuntoGenero", L("L_VisitorName")), c => Inst(c.Sav, i).Gender & 1, (c, v) => Inst(c.Sav, i).Gender = (byte)v, _ => P.Lista(IdiomaInterfaz.T("Varios5.Macho"), IdiomaInterfaz.T("Varios5.Hembra")), titulo: IdiomaInterfaz.T("FestiPlaza.GeneroVisitante"));
        for (var m = 0; m < 4; m++)
        {
            var n = m;
            yield return P.Entero(Id($"mensaje{n}"), $"{L("GB_FacilityMessage")} · {Mensajes[n]}", c => Inst(c.Sav, i).GetMessage(n), (c, v) => Inst(c.Sav, i).SetMessage(n, (ushort)v), 0, 9999);
        }
        yield return P.Opcion(Id("loteria"), L("L_LuckyResult"), c =>
            {
                var f = Inst(c.Sav, i); var lucky = (f.UsedLuckyPlace * 3) + f.UsedLuckyRank - 3;
                return (uint)lucky >= Resultados.Length ? 0 : lucky;
            },
            (c, v) => { var f = Inst(c.Sav, i); var lucky = (int)v - 1; f.UsedLuckyRank = lucky < 0 ? 0 : (lucky % 3) + 1; f.UsedLuckyPlace = lucky < 0 ? 0 : (lucky / 3) + 1; },
            _ => P.Lista(Resultados), visible: c => Tipo(c) == 5, titulo: L("L_LuckyResult"));
        yield return P.Entero(Id("intercambios"), L("L_Exchangable"), c => Inst(c.Sav, i).ExchangeLeftCount, (c, v) => Inst(c.Sav, i).ExchangeLeftCount = (int)v, 0, byte.MaxValue, visible: c => Tipo(c) == 7);
        yield return P.Entero(Id("banderas"), L("L_UsedFlags"), c => Inst(c.Sav, i).UsedFlags, (c, v) => Inst(c.Sav, i).UsedFlags = (uint)v, 0, uint.MaxValue, hex: true);
        yield return P.Entero(Id("estadisticas"), L("L_UsedStats"), c => Inst(c.Sav, i).UsedRandStat, (c, v) => Inst(c.Sav, i).UsedRandStat = (uint)v, 0, uint.MaxValue, hex: true);
        yield return P.Texto(Id("id"), L("L_FestaID"), c => Convert.ToHexString(Inst(c.Sav, i).TrainerFesID),
            (c, v) => { var dest = Inst(c.Sav, i).TrainerFesID; dest.Clear(); Util.GetBytesFromHexString(v, dest); }, 24, normaliza: t => SoloHex(t, 24), ayuda: IdiomaInterfaz.T("FestiPlaza.CifrasHexadecimalesBytes"));
    }

    // ---------------- Agencia de batalla (solo Ultrasol y Ultraluna) ----------------
    private const int OfsPrimero = 0x6C200, OfsSegundo = 0x6C2E8, OfsTercero = 0x6C420, OfsElegido = 0x6C55E, OfsDerrotados = 0x6C558, OfsGrado = 0x6C55C, OfsInvitado1 = 0x6C3EE, OfsInvitado2 = 0x6C526, OfsEntrenadores = 0x6C56C;
    private const ushort Invitado = 0x7DFF;
    private static ushort U16(SaveFile s, int ofs) => BinaryPrimitives.ReadUInt16LittleEndian(s.Data[ofs..]);
    private static bool EstaInvitado(SaveFile s) => (U16(s, OfsInvitado1) & Invitado) == Invitado && (U16(s, OfsInvitado2) & Invitado) == Invitado;
    private static int MaxDerrotados(long grado) => ((int)Math.Min(49, grado) / 10 * 3) + 2;
    private static PKM Participante(SaveFile s, int i) => i == 0 ? s.GetStoredSlot(s.Data[OfsPrimero..]) : s.GetPartySlot(s.Data[(i == 1 ? OfsSegundo : OfsTercero)..]);

    private static IEnumerable<CampoP> CamposDeLaAgencia()
    {
        for (var i = 0; i < 3; i++)
        {
            var n = i;
            yield return P.Info($"pk{n}", IdiomaInterfaz.F("Form.PokemonNumero", n + 1), c => c.Textos.specieslist[Math.Min(Participante(c.Sav, n).Species, c.Textos.specieslist.Length - 1)]);
        }
        yield return P.Accion("importar-equipo", L("B_ImportParty"), c =>
            {
                var party = c.Sav.PartyData;
                for (var i = 0; i < Math.Min(3, party.Count); i++)
                {
                    if (i == 0) party[i].WriteEncryptedDataStored(c.Sav.Data[OfsPrimero..]);
                    else party[i].WriteEncryptedDataParty(c.Sav.Data[(i == 1 ? OfsSegundo : OfsTercero)..]);
                }
            }, motivo: c => c.Sav.HasParty ? null : IdiomaInterfaz.T("FestiPlaza.GuardadoTieneEquipo"), textoBoton: IdiomaInterfaz.T("Form.Fama3.Importar"), confirmacion: IdiomaInterfaz.T("FestiPlaza.SustituyenTresPokemonAgencia"),
            ayuda: IdiomaInterfaz.T("FestiPlaza.CopiaTresPrimerosPokemon"));
        yield return P.Marca("elegido", L("CHK_Choosed"), c => c.Sav.GetFlag(OfsElegido, 1), (c, v) => c.Sav.SetFlag(OfsElegido, 1, v));
        yield return P.Marca("invitado", L("CHK_TrainerInvited"), c => EstaInvitado(c.Sav), (c, v) =>
        {
            if (EstaInvitado(c.Sav) == v) return;
            BinaryPrimitives.WriteUInt16LittleEndian(c.Sav.Data[OfsInvitado1..], (ushort)(v ? U16(c.Sav, OfsInvitado1) | Invitado : 0));
            BinaryPrimitives.WriteUInt16LittleEndian(c.Sav.Data[OfsInvitado2..], (ushort)(v ? U16(c.Sav, OfsInvitado2) | Invitado : 0));
        });
        // Grado y derrotados se guardan juntos en un mismo valor de 16 bits (con los 6 bits bajos que ya tenia): un grupo.
        static void GuardarGrado(ContextoP c)
        {
            var packed = (((int)c.Entero("derrotados") & 0xF) << 12) | (((int)c.Entero("grado") & 0x3F) << 6) | (c.Sav.Data[OfsGrado] & 0x3F);
            BinaryPrimitives.WriteUInt16LittleEndian(c.Sav.Data[OfsGrado..], (ushort)packed);
        }
        // Al subir o bajar el grado, los derrotados se recortan al maximo del grado (NUD_Grade_ValueChanged).
        yield return new CampoP
        {
            Id = "grado", Etiqueta = L("L_Grade"), Tipo = TipoP.Entero, Min = 0, Max = 50, GrupoEscritura = "grado",
            Leer = c => (long)((U16(c.Sav, OfsGrado) >> 6) & 0x3F), Escribir = (c, _) => GuardarGrado(c),
            Deriva = c => c.Entero("derrotados") > MaxDerrotados(c.Entero("grado")) ? new Dictionary<string, object?> { ["derrotados"] = (long)MaxDerrotados(c.Entero("grado")) } : new Dictionary<string, object?>(),
        };
        yield return P.Entero("derrotados", L("L_Defeated"), c => Math.Min(U16(c.Sav, OfsGrado) >> 12, MaxDerrotados((U16(c.Sav, OfsGrado) >> 6) & 0x3F)), (c, _) => GuardarGrado(c), 0, 15,
            maxFn: c => MaxDerrotados(c.Entero("grado")), grupo: "grado");
        yield return P.Entero("derrotados-hoy", L("L_DefeatMon"), c => U16(c.Sav, OfsDerrotados), (c, v) => BinaryPrimitives.WriteUInt16LittleEndian(c.Sav.Data[OfsDerrotados..], (ushort)v), 0, ushort.MaxValue);
        for (var i = 0; i < 3; i++)
        {
            var n = i;
            yield return P.Entero($"entrenador{n}", $"{L("L_Note")} · {n + 1}", c => Math.Min((int)U16(c.Sav, OfsEntrenadores + (0x14 * n)), 210),
                (c, v) => BinaryPrimitives.WriteUInt16LittleEndian(c.Sav.Data[(OfsEntrenadores + (0x14 * n))..], (ushort)v), 0, 210, ayuda: IdiomaInterfaz.T("FestiPlaza.Asignar"));
        }
        yield return P.Accion("gafas", L("B_AgentGlass"), c => ((SAV7)c.Sav).Fashion.GiveAgentSunglasses(), motivo: c => (((SAV7)c.Sav).Fashion.Data[0xD0] & 1) == 0 ? null : IdiomaInterfaz.T("FestiPlaza.TienesGafasAgente"),
            textoBoton: IdiomaInterfaz.T("Comun.Dar"), confirmacion: IdiomaInterfaz.T("FestiPlaza.GafasAgenteSonRecompensa"), ayuda: IdiomaInterfaz.T("FestiPlaza.DaGafasAgente"));
    }

    public static EditorDefP? Crear(SaveFile sav)
    {
        if (sav is not SAV7) return null;
        var usum = sav is SAV7USUM;
        ColeccionDefP? col = null;
        col = new ColeccionDefP
        {
            Id = "instalaciones", Elemento = IdiomaInterfaz.T("VariosGen4Frente.Instalacion"), Cantidad = _ => Instalaciones, MostrarPosicion = false,
            Titulo = (_, i) => IdiomaInterfaz.F("FestiPlaza.Instalacion", i + 1),
            Resumen = (c, i) => Inst(c.Sav, i).Type is var t && t < TiposDeInstalacion(usum).Count ? TiposDeInstalacion(usum)[t].Texto : null,
            Secciones = (_, i) =>
            [
                new SeccionP(string.Empty, [.. CamposDeLaInstalacion(col!, i, usum)]),
                new SeccionP(string.Empty, [P.Accion(col!.IdCampo(i, "borrar"), L("B_DelVisitor"), c =>
                    {
                        var f = Inst(c.Sav, i);
                        if (f.IsIntroduced) f.ClearTrainerFesID();   // hay un valor desconocido cuando no la presento: sin reproducibilidad, igual que el original
                        f.IsIntroduced = false; f.OriginalTrainerName = string.Empty; f.Gender = 0;
                        for (var k = 0; k < 4; k++) f.SetMessage(k, 0);
                    }, inmediata: true, textoBoton: IdiomaInterfaz.T("Seleccion.Borrar"), confirmacion: IdiomaInterfaz.T("FestiPlaza.BorranDatosVisitanteInstalacion"), ayuda: IdiomaInterfaz.T("FestiPlaza.QuitaVisitanteNombreGenero"))]),
            ],
        };
        var pestanas = new List<PestanaP>
        {
            new("resumen", L("Tab_Overview"), [.. Resumen(sav)]),
            new("frases", L("GB_Phrase"), [], Tabla: Frases()),
            new("premios", L("GB_Reward"), [], Tabla: PremiosTabla()),
            new("instalaciones", L("Tab_Facility"), [], Coleccion: col),
        };
        if (usum) pestanas.Add(new("agencia", L("Tab_BattleAgency"), [new SeccionP(string.Empty, [.. CamposDeLaAgencia()])]));
        return new EditorDefP("festival", IdiomaInterfaz.T("Partida.Especifico.festival.Titulo"), pestanas, Censo, IdiomaInterfaz.T("Partida.Especifico.festival.Descripcion"));
    }
}
