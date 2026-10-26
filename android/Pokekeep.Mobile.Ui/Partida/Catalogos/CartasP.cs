// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Catalogos;

/// <summary>
/// Cartas (<c>SAV_MailBox</c>, Gen. 2 a 5): una pestana por carta (las del equipo y las del buzon), con autor, identificadores, idioma, version, tipo de carta, Pokémon que aparecen y el mensaje, y la accion de vaciarla.
/// Cada campo se escribe leyendo la carta de la partida, cambiandolo y copiandola de vuelta, como <c>CopyTo</c> del formulario; lo que el formulario rehace al guardar (el duplicado de las cartas de Gen. 2 y el tamano del buzon) se hace una vez al final.
/// No incluye lo que el formulario pregunta en pantalla al borrar una carta (cambiar el objeto que lleva el Pokémon) ni el reordenar cartas: el motivo esta en la ayuda de cada accion.
/// </summary>
internal static class CartasP
{
    public static readonly string[] Censo = ["PK-SAV-227"];
    private static readonly int[] ObjetosGen2 = [0x9E, 0xB5, 0xB6, 0xB7, 0xB8, 0xB9, 0xBA, 0xBB, 0xBC, 0xBD];
    private static readonly int[] ObjetosGen3 = [121, 122, 123, 124, 125, 126, 127, 128, 129, 130, 131, 132];
    private static readonly int[] ObjetosGen45 = [137, 138, 139, 140, 141, 142, 143, 144, 145, 146, 147, 148];

    public static string? NoAplica(SaveFile sav) => sav is SAV2 or SAV2Stadium or SAV3 or SAV4 or SAV5 ? null : IdiomaInterfaz.T("Partida.NoAplica.Cartas");

    private static int[] Objetos(SaveFile sav) => sav.Generation switch { 2 => ObjetosGen2, 3 => ObjetosGen3, _ => ObjetosGen45 };

    private static int NumEquipo(SaveFile sav) => sav switch { SAV2Stadium => SAV2Stadium.MailboxHeldMailCount, SAV4 or SAV5 => sav.PartyData.Count, _ => 6 };

    private static int NumCartas(SaveFile sav) => sav switch
    {
        SAV2Stadium => SAV2Stadium.MailboxHeldMailCount + SAV2Stadium.MailboxMailCount,
        SAV2 => 6 + 10,
        SAV3 => 6 + 10,
        _ => sav.PartyData.Count + 20,
    };

    /// <summary>Lee la carta i de la partida (una copia: cambiarla no toca nada hasta copiarla de vuelta).</summary>
    private static MailDetail Leer(SaveFile sav, int i)
    {
        switch (sav)
        {
            case SAV2 s2: return new Mail2(s2, i);
            case SAV2Stadium st: return new Mail2(st, i);
            case SAV3 s3: return s3.LargeBlock.GetMail(i);
            case SAV4 s4: { var p = s4.PartyData; return i < p.Count ? new Mail4(((PK4)p[i]).HeldMail.ToArray()) : s4.GetMail(i - p.Count); }
            case SAV5 s5: { var p = s5.PartyData; return i < p.Count ? new Mail5(((PK5)p[i]).HeldMail.ToArray()) : s5.GetMail(i - p.Count); }
            default: throw new InvalidOperationException();
        }
    }

    private static void Copiar(SaveFile sav, int i, MailDetail m)
    {
        switch (sav)
        {
            case SAV4 s4 when i < s4.PartyData.Count: { var p = s4.PartyData; m.CopyTo((PK4)p[i]); s4.PartyData = p; break; }
            case SAV5 s5 when i < s5.PartyData.Count: { var p = s5.PartyData; m.CopyTo((PK5)p[i]); s5.PartyData = p; break; }
            case SAV4 s4: m.CopyTo(sav); break;
            default: m.CopyTo(sav); break;
        }
    }

    /// <summary>Texto para pintar: sin el relleno 0xFFFF de una cadena sin inicializar, ni caracteres de control ni de uso privado (el Core los deja en cartas vacias o sin autor y la pantalla enseñaba cuadritos).</summary>
    public static string Visible(string texto)
        => string.Concat(texto.Where(ch => ch >= ' ' && ch != '￿' && ch != '￾' && !char.IsControl(ch) && (ch < '' || ch > '') && !char.IsSurrogate(ch) || char.IsSurrogate(ch)));

    private static void Cambiar(ContextoP c, int i, Action<MailDetail> accion) { var m = Leer(c.Sav, i); accion(m); Copiar(c.Sav, i, m); }

    private static string Titulo(ContextoP c, int i)
    {
        var eq = NumEquipo(c.Sav);
        return i < eq ? IdiomaInterfaz.F("CartasP.Equipo", i + 1) : IdiomaInterfaz.F("CartasP.Buzon", i - eq + 1);
    }

    public static EditorDefP Crear(SaveFile sav)
    {
        var gen = sav.Generation; var objetos = Objetos(sav);
        var total = NumCartas(sav); var eq = NumEquipo(sav);
        var pestanas = new List<PestanaP>();
        if (sav is SAV2 or SAV2Stadium)
            pestanas.Add(new("general", IdiomaInterfaz.T("CartasP.Buzon2"), [new(IdiomaInterfaz.T("CartasP.TamanoBuzon"), [P.Entero("tamano", IdiomaInterfaz.T("CartasP.CartasBuzon"), c => c.Sav.Data[OfsTamano(c.Sav)], (c, v) => c.Sav.Data[OfsTamano(c.Sav)] = (byte)v, 0, sav is SAV2Stadium ? SAV2Stadium.MailboxMailCount : 10, ayuda: IdiomaInterfaz.T("CartasP.CuantasCartasBuzonUsan"))])]));
        for (var i = 0; i < total; i++)
        {
            var k = i;
            string Id(string n) => $"m{k}.{n}";
            List<CampoP> campos = null!;
            campos = new List<CampoP>
            {
                P.Info(Id("estado"), IdiomaInterfaz.T("CartasP.Estado"), c => { var e = Leer(c.Sav, k).IsEmpty; return e == true ? IdiomaInterfaz.T("Form.Fama3.Vacio") : e == false ? IdiomaInterfaz.T("CartasP.Carta") : IdiomaInterfaz.T("CartasP.TipoCartaValido"); }),
                P.Texto(Id("autor"), IdiomaInterfaz.T("CartasP.Autor"), c => Visible(Leer(c.Sav, k).AuthorName), (c, v) => Cambiar(c, k, m => m.AuthorName = v), maxFn: c => c.Sav.MaxStringLengthTrainer, lecturaAsimetrica: gen == 3, ayuda: gen == 3 ? IdiomaInterfaz.T("CartasP.GenCartaVaciaGuarda") : null),
                P.Entero(Id("tid"), IdiomaInterfaz.T("CartasP.TIDAutor"), c => Leer(c.Sav, k).AuthorTID, (c, v) => Cambiar(c, k, m => m.AuthorTID = (ushort)v), 0, 65535),
                P.Opcion(Id("idioma"), IdiomaInterfaz.T("CartasP.IdiomaAutor"), c => Leer(c.Sav, k).AuthorLanguage, (c, v) => Cambiar(c, k, m => { var (l1, l2, ue) = gen == 2 ? (m.GetMessage(false), m.GetMessage(true), m.UserEntered) : (string.Empty, string.Empty, false); m.AuthorLanguage = (byte)v; if (gen == 2) m.SetMessage(l1, l2, ue); }), c => P.Lista(GameInfo.LanguageDataSource(c.Sav.Generation, c.Sav.Context)),
                    motivo: gen == 2 ? c => Leer(c.Sav, k).AuthorLanguage is (byte)LanguageID.Japanese or (byte)LanguageID.Korean ? IdiomaInterfaz.T("CartasP.IdiomaCartaPuedeCambiar") : null : null, afectaA: _ => gen == 2 ? Mensaje(k) : [], lecturaAsimetrica: gen == 2),
                P.Opcion(Id("tipo"), IdiomaInterfaz.T("CartasP.TipoCarta"), c => TipoALista(c.Sav, Leer(c.Sav, k), objetos), (c, v) => Cambiar(c, k, m => CambioTipo(c, m, (int)v, objetos)), c => P.Lista(new[] { c.Textos.GetItemStrings(c.Sav.Context, c.Sav.Version)[0] }.Concat(objetos.Select(o => c.Textos.GetItemStrings(c.Sav.Context, c.Sav.Version)[o]))),
                    afectaA: _ => gen == 2 ? Mensaje(k) : []),
            };
            if (gen != 2) campos.Add(P.Entero(Id("sid"), IdiomaInterfaz.T("CartasP.SIDAutor"), c => Leer(c.Sav, k).AuthorSID, (c, v) => Cambiar(c, k, m => m.AuthorSID = (ushort)v), 0, 65535));
            if (gen is 4 or 5)
            {
                campos.Add(P.Opcion(Id("version"), IdiomaInterfaz.T("CartasP.JuegoAutor"), c => Leer(c.Sav, k).AuthorVersion, (c, v) => Cambiar(c, k, m => m.AuthorVersion = (byte)v), c => P.Lista(c.Datos.VersionDataSource.Where(x => ((GameVersion)x.Value).Context == c.Sav.Context))));
                campos.Add(P.Opcion(Id("genero"), IdiomaInterfaz.T("CartasP.GeneroAutor"), c => Leer(c.Sav, k).AuthorGender & 1, (c, v) => Cambiar(c, k, m => m.AuthorGender = (byte)((m.AuthorGender & 0xFE) | (int)v)), _ => P.Lista(IdiomaInterfaz.T("Form.Trainer8.Male"), IdiomaInterfaz.T("Form.Trainer8.Female"))));
            }
            // Pokémon que aparecen en la carta
            if (gen is 2 or 3)
                // Gen. 3 (PKHeX 26.10.18): la carta guarda el número INTERNO de la especie, y un Unown se guarda como 30000 + letra (A a Z, «!» y «?»; es el UNOWN_OFFSET del juego). El desplegable ofrece los valores tal como
                // se guardan; antes se convertían a nacional y las formas de Unown no se podían elegir ni se leían bien.
                campos.Add(P.Opcion(Id("aparece"), IdiomaInterfaz.T("CartasP.PokemonAparece"), c => Leer(c.Sav, k).AppearPKM,
                    (c, v) => Cambiar(c, k, m => m.AppearPKM = (ushort)v), c => gen == 3 ? ListaEspeciesGen3(c) : ListaEspecies(c)));
            else if (gen == 4)
                for (var j = 0; j < 3; j++)
                {
                    var jj = j;
                    campos.Add(P.Opcion(Id($"aparece{j}"), IdiomaInterfaz.F("CartasP.PokemonAparece2", j + 1), c => { var s = ((Mail4)Leer(c.Sav, k)).GetAppearSpecies(jj) - 7; return s >= 1 && s <= c.Sav.MaxSpeciesID ? s : -1; },
                        (c, v) => Cambiar(c, k, m => ((Mail4)m).SetAppearSpecies(jj, (ushort)(v < 0 ? 0 : v + 7))), c => [new OpcionP(-1, IdiomaInterfaz.T("Form.Ninguno")), .. ListaEspecies(c)]));
                }
            // Mensaje
            if (gen == 2)
            {
                campos.Add(new CampoP { AfectaA = _ => Mensaje(k), Id = Id("linea1"), Etiqueta = IdiomaInterfaz.T("CartasP.MensajeLinea"), Tipo = TipoP.Texto, MaxLongitud = 16, GrupoEscritura = Id("msg"), Leer = c => Leer(c.Sav, k).GetMessage(false), Escribir = (c, v) => Cambiar(c, k, m => m.SetMessage((string)v!, m.GetMessage(true), m.UserEntered)) });
                campos.Add(new CampoP { AfectaA = _ => Mensaje(k), Id = Id("linea2"), Etiqueta = IdiomaInterfaz.T("CartasP.MensajeLinea2"), Tipo = TipoP.Texto, MaxLongitud = 16, GrupoEscritura = Id("msg"), Leer = c => Leer(c.Sav, k).GetMessage(true), Escribir = (c, v) => Cambiar(c, k, m => m.SetMessage(m.GetMessage(false), (string)v!, m.UserEntered)) });
                campos.Add(new CampoP { AfectaA = _ => Mensaje(k), Id = Id("usuario"), Etiqueta = IdiomaInterfaz.T("CartasP.EscritaUsuario"), Tipo = TipoP.Marca, GrupoEscritura = Id("msg"), Leer = c => Leer(c.Sav, k).UserEntered, Escribir = (c, v) => Cambiar(c, k, m => m.SetMessage(m.GetMessage(false), m.GetMessage(true), (bool)v!)) });
            }
            else
            {
                var xc = gen == 3 ? 3 : 4;
                for (var y = 0; y < 3; y++)
                    for (var x = 0; x < xc; x++)
                    {
                        var yy = y; var xx = x;
                        campos.Add(P.Entero(Id($"palabra{y}{x}"), IdiomaInterfaz.F("CartasP.Palabra2", y + 1, x + 1), c => Leer(c.Sav, k).GetMessage(yy, xx), (c, v) => Cambiar(c, k, m => m.SetMessage(yy, xx, (ushort)v)), 0, 65535, ayuda: IdiomaInterfaz.T("CartasP.NumeroPalabraFacilCarta")));
                    }
            }
            if (gen == 5)
            {
                for (var j = 0; j < 3; j++) { var jj = j; campos.Add(P.Entero(Id($"misc{j}"), IdiomaInterfaz.F("CartasP.ValorAdicional", j + 1), c => ((Mail5)Leer(c.Sav, k)).GetMisc(jj), (c, v) => Cambiar(c, k, m => ((Mail5)m).SetMisc(jj, (int)v)), 0, 65535)); }
                campos.Add(P.Entero(Id("final"), IdiomaInterfaz.T("CartasP.FinalMensaje"), c => ((Mail5)Leer(c.Sav, k)).MessageEnding, (c, v) => Cambiar(c, k, m => ((Mail5)m).MessageEnding = (ushort)v), 0, 65535));
            }
            var acciones = new List<CampoP>
            {
                P.Opcion(Id("vaciar"), IdiomaInterfaz.T("CartasP.VaciarCarta"), c => 0, (c, v) => { if (v == 1) { Cambiar(c, k, m => Vaciar(c.Sav, m)); QuitarObjetoCarta(c.Sav, k, objetos); } }, _ => P.Lista(IdiomaInterfaz.T("Form.Trainer7.SinCambios"), IdiomaInterfaz.T("CartasP.VaciarCarta")), soloAccion: true, prioridad: 5,
                    afectaA: c => campos.Where(x => x.Id != Id("vaciar")).Select(x => x.Id),
                    ayuda: IdiomaInterfaz.T("CartasP.DejaCartaVaciaCarta")),
            };
            pestanas.Add(new($"carta{i}", Titulo2(sav, i, eq), [new(IdiomaInterfaz.T("CartasP.Carta2"), campos), new(IdiomaInterfaz.T("Mochila.Acciones"), acciones)]));
        }
        return new EditorDefP("cartas", IdiomaInterfaz.T("CartasP.Cartas"), pestanas, Censo, IdiomaInterfaz.T("CartasP.CartasEquipoBuzonAutor"))
        {
            PrepararAplicar = c => c.Sav switch
            {
                SAV2 s2 => x => Duplicar2(s2),
                _ => null,
            },
        };
    }

    private static IEnumerable<string> Mensaje(int k) => [$"m{k}.linea1", $"m{k}.linea2", $"m{k}.usuario"];

    private static string Titulo2(SaveFile sav, int i, int eq) => i < eq ? IdiomaInterfaz.F("CartasP.Equipo", i + 1) : IdiomaInterfaz.F("CartasP.Buzon", i - eq + 1);

    private static IReadOnlyList<OpcionP> ListaEspecies(ContextoP c) => P.Lista(c.Datos.SpeciesDataSource.Where(x => x.Value >= 1 && x.Value <= c.Sav.MaxSpeciesID));

    /// <summary>Valor de una carta de Gen. 3 para la letra <paramref name="i"/> de Unown: 30000 + índice (A-Z = 0-25, «!» = 26, «?» = 27).</summary>
    internal const int DesplazamientoUnown = 30000;

    /// <summary>
    /// Lista de especies de las cartas de Gen. 3 como la de <c>SAV_MailBox</c> en PKHeX 26.10.18: el valor de cada especie es su número interno de Gen. 3 y Unown se despliega en sus 28 formas (el nombre lleva la letra).
    /// </summary>
    internal static IReadOnlyList<OpcionP> ListaEspeciesGen3(ContextoP c)
    {
        var lista = new List<OpcionP>();
        foreach (var item in c.Datos.SpeciesDataSource.Where(x => x.Value >= 1 && x.Value <= c.Sav.MaxSpeciesID))
        {
            if ((Species)item.Value != Species.Unown) { lista.Add(new OpcionP(SpeciesConverter.GetInternal3((ushort)item.Value), item.Text)); continue; }
            for (var i = 0; i < 26; i++) lista.Add(new OpcionP(DesplazamientoUnown + i, $"{item.Text} ({(char)('A' + i)})"));
            lista.Add(new OpcionP(DesplazamientoUnown + 26, $"{item.Text} (!)"));
            lista.Add(new OpcionP(DesplazamientoUnown + 27, $"{item.Text} (?)"));
        }
        return lista;
    }

    private static int OfsTamano(SaveFile sav) => sav is SAV2Stadium ? Mail2.GetMailboxOffsetStadium2(sav.Language) : Mail2.GetMailboxOffset(sav.Language);

    /// <summary>Cambia el tipo; en Gen. 2 el formulario vuelve a escribir el mensaje despues (TempSave), con lo que tenia cargado.</summary>
    private static void CambioTipo(ContextoP c, MailDetail m, int cb, int[] objetos)
    {
        var gen2 = c.Sav.Generation == 2;
        var (l1, l2, ue) = gen2 ? (m.GetMessage(false), m.GetMessage(true), m.UserEntered) : (string.Empty, string.Empty, false);
        m.MailType = ListaATipo(c.Sav, cb, objetos);
        if (gen2) m.SetMessage(l1, l2, ue);
    }

    private static long TipoALista(SaveFile sav, MailDetail m, int[] objetos) => sav.Generation <= 3 ? 1 + Array.IndexOf(objetos, m.MailType) : (m.IsEmpty == false ? 1 + m.MailType : 0);
    private static int ListaATipo(SaveFile sav, int cb, int[] objetos) => sav.Generation <= 3 ? (cb > 0 ? objetos[cb - 1] : 0) : (cb > 0 ? cb - 1 : 0xFF);

    /// <summary>Lo de <c>ModifyHeldItem</c> con la respuesta «Sí» (la de por defecto): los Pokémon del equipo que llevan ESTA carta como objeto lo pierden (en Gen. 3, tambien su numero de carta).</summary>
    private static void QuitarObjetoCarta(SaveFile sav, int entrada, int[] objetos)
    {
        if (sav is SAV2Stadium || sav is not (SAV2 or SAV3 or SAV4 or SAV5)) return;
        var p = sav.PartyData;
        if (entrada >= p.Count) return;
        var cambiado = false;
        for (var i = 0; i < p.Count; i++)
        {
            var carta = sav is SAV3 ? ((PK3)p[i]).HeldMailID : i;
            if (carta != entrada || !objetos.Contains(p[i].HeldItem)) continue;
            p[i].HeldItem = 0;
            if (sav is SAV3) ((PK3)p[i]).HeldMailID = -1;
            cambiado = true;
        }
        if (cambiado) sav.PartyData = p;
    }

    /// <summary>Lo de <c>B_Delete_Click</c>: Gen. 4 y 5 vacian con el idioma y la version de la ultima carta del buzon.</summary>
    private static void Vaciar(SaveFile sav, MailDetail m)
    {
        switch (m)
        {
            case Mail4 m4: { var ult = (Mail4)Leer(sav, NumCartas(sav) - 1); m4.SetBlank(ult.AuthorLanguage, ult.AuthorVersion); break; }
            case Mail5 m5: { var ult = (Mail5)Leer(sav, NumCartas(sav) - 1); m5.SetBlank(ult.AuthorLanguage, ult.AuthorVersion); break; }
            default: m.SetBlank(); break;
        }
    }

    /// <summary>Lo que <c>Save()</c> hace en Gen. 2 despues de copiar las cartas: duplica el bloque de las del equipo y el del buzon (con su tamano).</summary>
    private static void Duplicar2(SAV2 sav)
    {
        int ofs = 0x600;
        int len = Mail2.GetMailSize(sav.Language) * 6;
        sav.Data.Slice(ofs, len).CopyTo(sav.Data.Slice(ofs + len, len));
        ofs += len << 1;
        len = (Mail2.GetMailSize(sav.Language) * 10) + 1;
        sav.Data.Slice(ofs, len).CopyTo(sav.Data.Slice(ofs + len, len));
    }
}
