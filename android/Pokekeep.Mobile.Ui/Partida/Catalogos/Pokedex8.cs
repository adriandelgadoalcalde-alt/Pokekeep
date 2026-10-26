// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Catalogos;

/// <summary>
/// Pokédex de Espada y Escudo (<c>SAV_PokedexSWSH</c>): una «entrada» por especie y por Pokédex (Galar, Isla de la Armadura, Nevada Corona); capturado, vistos por forma y género, idiomas, Gigamax, lo mostrado y los encuentros,
/// con las mismas llamadas del Core que el formulario y sus acciones de lote.
/// </summary>
internal static class Pokedex8
{
    public static readonly string[] Censo = ["PK-SAV-168"];
    private static string[] Generos => [IdiomaInterfaz.T("Form.Trainer8.Male"), IdiomaInterfaz.T("Form.Trainer8.Female"), IdiomaInterfaz.T("Pokedex56.MachoVariocolor"), IdiomaInterfaz.T("Pokedex56.HembraVariocolor")];
    private static string[] Idiomas => [IdiomaInterfaz.T("ModeloForzarCarga.Japones"), IdiomaInterfaz.T("ModeloForzarCarga.Ingles"), IdiomaInterfaz.T("ModeloForzarCarga.Frances"), IdiomaInterfaz.T("ModeloForzarCarga.Italiano"), IdiomaInterfaz.T("ModeloForzarCarga.Aleman"), IdiomaInterfaz.T("ModeloForzarCarga.Espanol"), IdiomaInterfaz.T("ModeloForzarCarga.Coreano"), "Chino simplificado", "Chino tradicional"];

    private static Zukan8 Z(ContextoP c) => ((SAV8SWSH)c.Sav).Blocks.Zukan;

    private static IReadOnlyList<Zukan8EntryInfo> Entradas(ContextoP c)
    {
        var z = Z(c);
        var todas = Zukan8.GetRawIndexes(PersonalTable.SWSH, z.GetRevision(), Zukan8Index.TotalCount).Where(e => e.Entry.DexType != Zukan8Type.None).ToList();
        return [.. todas.OrderBy(e => c.Textos.Species[e.Species], StringComparer.CurrentCulture).ThenBy(e => (int)e.Entry.DexType)];
    }

    /// <summary>
    /// Clave estable de una entrada: especie y Pokédex, NUNCA su posición en la lista (que va por nombre y cambia con el idioma).
    /// Auditoría uso10 (A1): con ids posicionales, cambiar el idioma en vivo con el editor abierto ligaba los borradores a otras
    /// especies y «Aplicar» escribía donde no era.
    /// </summary>
    private static int Clave(Zukan8EntryInfo e) => e.Species * 4 + (int)e.Entry.DexType;

    private static string NombreDex(Zukan8Type t) => t switch { Zukan8Type.Galar => "Galar", Zukan8Type.Armor => IdiomaInterfaz.T("Pokedex8.IslaArmadura"), Zukan8Type.Crown => IdiomaInterfaz.T("Pokedex8.NevadaCorona"), _ => "?" };
    private static string Nombre(ContextoP c, Zukan8EntryInfo e) => $"{c.Textos.Species[e.Species]} ({NombreDex(e.Entry.DexType)} n.º {e.Entry.Index})";

    private static string[] Formas(ContextoP c, ushort species)
    {
        var s = c.Textos;
        var f = species == (int)Species.Alcremie ? FormConverter.GetAlcremieFormList(s.forms) : FormConverter.GetFormList(species, s.Types, s.forms, GameInfo.GenderSymbolASCII, EntityContext.Gen8);
        if (f.Length > 0 && f[0].Length == 0) f[0] = s.Types[0];
        return f;
    }

    public static EditorDefP Crear(SAV8SWSH sav)
    {
        IReadOnlyList<FilaTablaP> Marcas(ContextoP c, Func<Zukan8Index, ushort, bool>? incluye, string[] nombres, Func<ContextoP, Zukan8Index, int, bool> leer, Action<ContextoP, Zukan8Index, int, bool> escribir)
        {
            var l = new List<FilaTablaP>(); var es = Entradas(c);
            for (var k = 0; k < es.Count; k++)
            {
                if (incluye is not null && !incluye(es[k].Entry, es[k].Species)) continue;
                for (var t = 0; t < nombres.Length; t++)
                {
                    var e = es[k].Entry; var tt = t; var n = nombres[t];
                    l.Add(new FilaTablaP { Id = Clave(es[k]) * 16 + t, Nombre = $"{Nombre(c, es[k])} · {n}", EsMarca = true, Leer = x => leer(x, e, tt), Escribir = (x, v) => escribir(x, e, tt, (bool)v) });
                }
            }
            return l;
        }

        var capturados = new TablaDefP
        {
            Id = "capturados", SinNumero = true, Marcador = IdiomaInterfaz.T("Libreria.BuscarEspecie"), Ayuda = IdiomaInterfaz.T("Pokedex8.CapturadoGigamaxCadaEntrada"),
            Filas = c => Marcas(c, null, [IdiomaInterfaz.T("Pokedex8.Capturado"), IdiomaInterfaz.T("Pokedex8.Gigamaxizo"), IdiomaInterfaz.T("Pokedex8.MostrarGigamax"), IdiomaInterfaz.T("Pokedex8.MostrarVariocolor")], (x, e, t) => t switch { 0 => Z(x).GetCaught(e), 1 => Z(x).GetCaughtGigantamaxed(e), 2 => Z(x).GetDisplayDynamaxInstead(e), _ => Z(x).GetDisplayShiny(e) },
                (x, e, t, v) => { switch (t) { case 0: Z(x).SetCaught(e, v); break; case 1: Z(x).SetCaughtGigantamax(e, v); break; case 2: Z(x).SetDisplayDynamaxInstead(e, v); break; default: Z(x).SetDisplayShiny(e, v); break; } }),
        };
        var urshifu = new TablaDefP
        {
            Id = "urshifu", SinNumero = true, Marcador = IdiomaInterfaz.T("Comun.Buscar"), Ayuda = IdiomaInterfaz.T("Pokedex8.SegundoGigamaxUrshifu"),
            Filas = c => Marcas(c, (e, sp) => sp == (int)Species.Urshifu, [IdiomaInterfaz.T("Pokedex8.Gigamaxizo2")], (x, e, t) => Z(x).GetCaughtGigantamax1(e), (x, e, t, v) => Z(x).SetCaughtGigantamax1(e, v)),
        };
        var idiomas = new TablaDefP
        {
            Id = "idiomas", SinNumero = true, Marcador = IdiomaInterfaz.T("Libreria.BuscarEspecie"), Ayuda = IdiomaInterfaz.T("Pokedex56.IdiomasVistoCadaEspecie"),
            Filas = c => Marcas(c, null, Idiomas, (x, e, t) => Z(x).GetIsLanguageIndexObtained(e, t), (x, e, t, v) => Z(x).SetIsLanguageIndexObtained(e, t, v)),
        };
        var vistos = new TablaDefP
        {
            Id = "vistos", SinNumero = true, Marcador = IdiomaInterfaz.T("Pokedex56.EspecieForma"), Ayuda = IdiomaInterfaz.T("Pokedex8.FormasVistasGeneroGigamax"),
            Filas = c =>
            {
                var l = new List<FilaTablaP>(); var es = Entradas(c);
                for (var k = 0; k < es.Count; k++)
                {
                    var e = es[k].Entry; var sp = es[k].Species; var formas = Formas(c, sp);
                    var js = Enumerable.Range(0, Math.Min(formas.Length, 63)).Select(j => (J: j, N: formas[j])).ToList();
                    js.Add((63, sp == (int)Species.Urshifu ? IdiomaInterfaz.F("Pokedex8.Gigamax", formas[0]) : IdiomaInterfaz.T("Libreria.Vista.11")));
                    if (sp == (int)Species.Urshifu && formas.Length > 1) js.Add((62, IdiomaInterfaz.F("Pokedex8.Gigamax", formas[1])));
                    foreach (var (j0, n) in js)
                        for (var r = 0; r < 4; r++)
                        {
                            var j = (byte)j0; var rr = r;
                            l.Add(new FilaTablaP { Id = (Clave(es[k]) * 64 + j0) * 8 + r, Nombre = $"{Nombre(c, es[k])} · {n} · {Generos[r]}", EsMarca = true, Leer = x => Z(x).GetSeenRegion(e, j, rr), Escribir = (x, v) => Z(x).SetSeenRegion(e, j, rr, (bool)v) });
                        }
                }
                return l;
            },
        };
        var numeros = new TablaDefP
        {
            Id = "numeros", SinNumero = true, Marcador = IdiomaInterfaz.T("Libreria.BuscarEspecie"), Ayuda = IdiomaInterfaz.T("Pokedex8.EncuentrosFormaMostradaN"),
            Filas = c =>
            {
                var l = new List<FilaTablaP>(); var es = Entradas(c);
                for (var k = 0; k < es.Count; k++)
                {
                    var e = es[k].Entry;
                    l.Add(new FilaTablaP { Id = Clave(es[k]) * 4, Nombre = IdiomaInterfaz.F("Pokedex8.Encuentros", Nombre(c, es[k])), Min = 0, Max = int.MaxValue, Leer = x => (long)Z(x).GetBattledCount(e), Escribir = (x, v) => Z(x).SetBattledCount(e, (uint)(long)v) });
                    l.Add(new FilaTablaP { Id = Clave(es[k]) * 4 + 1, Nombre = IdiomaInterfaz.F("Pokedex8.FormaMostrada", Nombre(c, es[k])), Min = 0, Max = 0x1FFF, Leer = x => (long)Z(x).GetFormDisplayed(e), Escribir = (x, v) => Z(x).SetFormDisplayed(e, (uint)(long)v) });
                    l.Add(new FilaTablaP { Id = Clave(es[k]) * 4 + 2, Nombre = IdiomaInterfaz.F("Pokedex8.GeneroMostrado", Nombre(c, es[k])), Min = 0, Max = 2, Leer = x => (long)Z(x).GetGenderDisplayed(e), Escribir = (x, v) => Z(x).SetGenderDisplayed(e, (uint)(long)v) });
                }
                return l;
            },
        };
        return new EditorDefP("pokedex", IdiomaInterfaz.T("Partida.Editor.pokedex.Titulo"),
        [
            new("resumen", IdiomaInterfaz.T("Editor.Resumen"), [new(IdiomaInterfaz.T("Pokedex.AccionesLote"), [P.Opcion("lote", IdiomaInterfaz.T("Pokedex.SobreTodaPokedex"), c => 0, (c, v) => Lote(c, (int)v),
                _ => P.Lista(IdiomaInterfaz.T("Form.Trainer7.SinCambios"), IdiomaInterfaz.T("Pokedex8.CompletarTodoCapturado"), IdiomaInterfaz.T("Pokedex8.CompletarVariocolores"), IdiomaInterfaz.T("Pokedex.TodosVistos2"), IdiomaInterfaz.T("Pokedex56.TodosVistosVariocolores"), IdiomaInterfaz.T("Pokedex56.NingunoVisto"), IdiomaInterfaz.T("Pokedex.TodosCapturados2"), IdiomaInterfaz.T("Pokedex8.TodosCapturadosVariocolores"), IdiomaInterfaz.T("Pokedex.NingunoCapturado")), soloAccion: true, ayuda: IdiomaInterfaz.T("Pokedex56.AplicaTodasEspeciesVez"))])]),
            new("capturados", IdiomaInterfaz.T("Pokedex.Capturados"), [], Tabla: capturados),
            new("vistos", IdiomaInterfaz.T("Pokedex.Vistos"), [], Tabla: vistos),
            new("idiomas", IdiomaInterfaz.T("Pokedex.Idiomas"), [], Tabla: idiomas),
            new("numeros", IdiomaInterfaz.T("Pokedex8.Numeros"), [], Tabla: numeros),
            new("urshifu", "Urshifu", [], Tabla: urshifu),
        ], Censo, IdiomaInterfaz.T("Pokedex8.PokemonVistosCapturadosGalar"));
    }

    private static void Lote(ContextoP c, int v)
    {
        var z = Z(c);
        switch (v)
        {
            case 1: z.CompleteDex(false); break;
            case 2: z.CompleteDex(true); break;
            case 3: z.SeenAll(false); break;
            case 4: z.SeenAll(true); break;
            case 5: z.SeenNone(); break;
            case 6: z.CaughtAll(false); break;
            case 7: z.CaughtAll(true); break;
            case 8: z.CaughtNone(); break;
        }
    }
}
