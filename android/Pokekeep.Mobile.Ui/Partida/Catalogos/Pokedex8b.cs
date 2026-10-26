// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Catalogos;

/// <summary>Pokédex de Diamante Brillante y Perla Reluciente (<c>SAV_PokedexBDSP</c>): estado, géneros vistos, idiomas y formas por especie, Pokédex Nacional y las acciones de lote del formulario.</summary>
internal static class Pokedex8b
{
    public static readonly string[] Censo = ["PK-SAV-161"];
    private static string[] Generos => [IdiomaInterfaz.T("Form.Trainer8.Male"), IdiomaInterfaz.T("Form.Trainer8.Female"), IdiomaInterfaz.T("Pokedex56.MachoVariocolor"), IdiomaInterfaz.T("Pokedex56.HembraVariocolor")];
    private static (string Nombre, LanguageID Id)[] Idiomas =>
    [
        (IdiomaInterfaz.T("ModeloForzarCarga.Japones"), LanguageID.Japanese), (IdiomaInterfaz.T("ModeloForzarCarga.Ingles"), LanguageID.English), (IdiomaInterfaz.T("ModeloForzarCarga.Frances"), LanguageID.French), (IdiomaInterfaz.T("ModeloForzarCarga.Italiano"), LanguageID.Italian), (IdiomaInterfaz.T("ModeloForzarCarga.Aleman"), LanguageID.German),
        (IdiomaInterfaz.T("ModeloForzarCarga.Espanol"), LanguageID.Spanish), (IdiomaInterfaz.T("ModeloForzarCarga.Coreano"), LanguageID.Korean), ("Chino simplificado", LanguageID.ChineseS), ("Chino tradicional", LanguageID.ChineseT),
    ];

    private static Zukan8b Z(ContextoP c) => ((SAV8BS)c.Sav).Zukan;
    private static string Nombre(ContextoP c, ushort sp) => sp < c.Textos.Species.Count ? c.Textos.Species[sp] : IdiomaInterfaz.F("DescripcionRegalo.Especie", sp);
    private static IEnumerable<ushort> Especies(ContextoP c) => Enumerable.Range(1, 493).Select(i => (ushort)i);

    public static EditorDefP Crear(SAV8BS sav)
    {
        var estados = new TablaDefP
        {
            Id = "estados", SinNumero = true, Marcador = IdiomaInterfaz.T("Libreria.BuscarEspecie"), Ayuda = IdiomaInterfaz.T("Pokedex8b.VerOidoHablarElla"),
            Filas = c => [.. Especies(c).Select(sp => new FilaTablaP { Id = sp, Nombre = Nombre(c, sp), Min = 0, Max = 3, Leer = x => (long)(int)Z(x).GetState(sp), Escribir = (x, v) => Z(x).SetState(sp, (ZukanState8b)(int)(long)v) })],
        };
        var generos = new TablaDefP
        {
            Id = "generos", SinNumero = true, Marcador = IdiomaInterfaz.T("Libreria.BuscarEspecie"), Ayuda = IdiomaInterfaz.T("Pokedex8b.GenerosVariocoloresVistosCada"),
            Filas = c => [.. Especies(c).SelectMany(sp => Enumerable.Range(0, 4).Select(k => new FilaTablaP
            {
                Id = sp * 8 + k, Nombre = $"{Nombre(c, sp)} · {Generos[k]}", EsMarca = true,
                Leer = x => { Z(x).GetGenderFlags(sp, out var m, out var f, out var ms, out var fs); return new[] { m, f, ms, fs }[k]; },
                Escribir = (x, v) => { Z(x).GetGenderFlags(sp, out var m, out var f, out var ms, out var fs); var a = new[] { m, f, ms, fs }; a[k] = (bool)v; Z(x).SetGenderFlags(sp, a[0], a[1], a[2], a[3]); },
            }))],
        };
        var idiomas = new TablaDefP
        {
            Id = "idiomas", SinNumero = true, Marcador = IdiomaInterfaz.T("Libreria.BuscarEspecie"), Ayuda = IdiomaInterfaz.T("Pokedex56.IdiomasVistoCadaEspecie"),
            Filas = c => [.. Especies(c).SelectMany(sp => Enumerable.Range(0, Idiomas.Length).Select(k => new FilaTablaP
            {
                Id = sp * 16 + k, Nombre = $"{Nombre(c, sp)} · {Idiomas[k].Nombre}", EsMarca = true,
                Leer = x => Z(x).GetLanguageFlag(sp, (int)Idiomas[k].Id), Escribir = (x, v) => Z(x).SetLanguageFlag(sp, (int)Idiomas[k].Id, (bool)v),
            }))],
        };
        var formas = new TablaDefP
        {
            Id = "formas", SinNumero = true, Marcador = IdiomaInterfaz.T("Pokedex56.EspecieForma"), Ayuda = IdiomaInterfaz.T("Pokedex8b.FormasVistasFilaVariocolor"),
            Filas = c =>
            {
                var l = new List<FilaTablaP>();
                foreach (var sp in Especies(c))
                {
                    var fc = Zukan8b.GetFormCount(sp);
                    if (fc <= 0) continue;
                    var nombres = FormConverter.GetFormList(sp, c.Textos.Types, c.Textos.forms, GameInfo.GenderSymbolUnicode, c.Sav.Context).Take(fc).ToArray();
                    for (var i = 0; i < nombres.Length; i++)
                    {
                        var s = sp; var k = (byte)i; var n = nombres[i].Length == 0 ? IdiomaInterfaz.T("Pokedex56.FormaBase") : nombres[i];
                        l.Add(new FilaTablaP { Id = s * 256 + k, Nombre = $"{Nombre(c, s)} · {n}", EsMarca = true, Leer = x => Z(x).GetHasFormFlag(s, k, false), Escribir = (x, v) => Z(x).SetHasFormFlag(s, k, false, (bool)v) });
                        l.Add(new FilaTablaP { Id = s * 256 + 128 + k, Nombre = $"{Nombre(c, s)} · * {n}", EsMarca = true, Leer = x => Z(x).GetHasFormFlag(s, k, true), Escribir = (x, v) => Z(x).SetHasFormFlag(s, k, true, (bool)v) });
                    }
                }
                return l;
            },
        };
        return new EditorDefP("pokedex", IdiomaInterfaz.T("Partida.Editor.pokedex.Titulo"),
        [
            new("resumen", IdiomaInterfaz.T("Editor.Resumen"), [new(IdiomaInterfaz.T("Partida.Editor.pokedex.Titulo"), [P.Marca("nacional", IdiomaInterfaz.T("Pokedex.PokedexNacional"), c => Z(c).HasNationalDex, (c, v) => Z(c).HasNationalDex = v, ayuda: IdiomaInterfaz.T("Pokedex8b.DesbloqueaPokedexNacional"))]),
                new(IdiomaInterfaz.T("Pokedex.AccionesLote"), [P.Opcion("lote", IdiomaInterfaz.T("Pokedex.SobreTodaPokedex"), c => 0, (c, v) => Lote(c, (int)v),
                    _ => P.Lista(IdiomaInterfaz.T("Form.Trainer7.SinCambios"), IdiomaInterfaz.T("Pokedex8.CompletarTodoCapturado"), IdiomaInterfaz.T("Pokedex8.CompletarVariocolores"), IdiomaInterfaz.T("Pokedex.TodosVistos2"), IdiomaInterfaz.T("Pokedex56.TodosVistosVariocolores"), IdiomaInterfaz.T("Pokedex56.NingunoVisto"), IdiomaInterfaz.T("Pokedex.TodosCapturados2"), IdiomaInterfaz.T("Pokedex.NingunoCapturado")), soloAccion: true, ayuda: IdiomaInterfaz.T("Pokedex56.AplicaTodasEspeciesVez"))])]),
            new("estados", IdiomaInterfaz.T("CartasP.Estado"), [], Tabla: estados),
            new("generos", IdiomaInterfaz.T("Pokedex8b.Generos"), [], Tabla: generos),
            new("idiomas", IdiomaInterfaz.T("Pokedex.Idiomas"), [], Tabla: idiomas),
            new("formas", IdiomaInterfaz.T("Pokedex8b.Formas"), [], Tabla: formas),
        ], Censo, IdiomaInterfaz.T("Pokedex8b.EstadoCadaEspecieGeneros"));
    }

    private static void Lote(ContextoP c, int v)
    {
        var z = Z(c);
        switch (v)
        {
            case 1: z.CompleteDex(false); break;
            case 2: z.CompleteDex(true); break;
            case 3: z.SetAllSeen(); break;
            case 4: z.SetAllSeen(shinyToo: true); break;
            case 5: z.SetAllSeen(false); break;
            case 6: z.CaughtAll(); break;
            case 7: z.CaughtNone(); break;
        }
    }
}
