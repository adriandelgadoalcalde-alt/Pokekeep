// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Catalogos;

/// <summary>Pokédex de Leyendas Pokémon: Z-A (<c>SAV_Pokedex9a</c>): por especie, géneros, alfa y megaformas vistas, formas capturadas, vistas y variocolor, idiomas y lo mostrado, con las llamadas del Core del formulario y sus acciones de lote.</summary>
internal static class Pokedex9a
{
    public static readonly string[] Censo = ["PK-SAV-192"];
    private static string[] Idiomas => [IdiomaInterfaz.T("ModeloForzarCarga.Japones"), IdiomaInterfaz.T("ModeloForzarCarga.Ingles"), IdiomaInterfaz.T("ModeloForzarCarga.Frances"), IdiomaInterfaz.T("ModeloForzarCarga.Italiano"), IdiomaInterfaz.T("ModeloForzarCarga.Aleman"), IdiomaInterfaz.T("ModeloForzarCarga.Espanol"), IdiomaInterfaz.T("ModeloForzarCarga.Coreano"), "Chino simplificado", "Chino tradicional", IdiomaInterfaz.T("Pokedex9a.EspanolLatinoamerica")];
    private static readonly LanguageID[] IdIdioma = [LanguageID.Japanese, LanguageID.English, LanguageID.French, LanguageID.Italian, LanguageID.German, LanguageID.Spanish, LanguageID.Korean, LanguageID.ChineseS, LanguageID.ChineseT, LanguageID.SpanishL];

    private static SAV9ZA S(ContextoP c) => (SAV9ZA)c.Sav;
    private static Zukan9a Z(ContextoP c) => S(c).Blocks.Zukan;
    private static string Nombre(ContextoP c, ushort sp) => sp < c.Textos.Species.Count ? c.Textos.Species[sp] : IdiomaInterfaz.F("DescripcionRegalo.Especie", sp);

    private static IReadOnlyList<ushort> Especies(ContextoP c)
    {
        ushort Dex(ushort sp)
        {
            var entry = PersonalTable.ZA.GetFormEntry(sp, 0);
            for (byte i = 0; i < entry.FormCount; i++) { entry = PersonalTable.ZA.GetFormEntry(sp, i); if (entry.DexIndex != 0) return entry.DexIndex; }
            return 0;
        }
        return [.. Enumerable.Range(1, c.Sav.MaxSpeciesID).Select(i => (ushort)i).Select(sp => (Sp: sp, D: Dex(sp))).OrderByDescending(x => x.D != 0).ThenBy(x => x.D).Select(x => x.Sp)];
    }

    private static string[] Formas(ContextoP c, ushort species)
    {
        var f = FormConverter.GetFormList(species, c.Textos.Types, c.Textos.forms, GameInfo.GenderSymbolASCII, EntityContext.Gen9a);
        if (f.Length > 0 && f[0].Length == 0) f[0] = c.Textos.Types[0];
        return f;
    }

    /// <summary>Los nombres de las casillas de «mega» de una especie (una, dos o tres), como en el formulario.</summary>
    private static string[] Megas(ContextoP c, ushort sp)
    {
        var n = FormConverter.GetMegaFormNames(c.Textos.forms, GameInfo.GenderSymbolASCII, c.Textos.Types);
        var rev = S(c).SaveRevision;
        if (Zukan9a.IsMegaFormXY(sp, rev)) return [n.X, n.Y];
        if (Zukan9a.IsMegaFormZA(sp, rev)) return [n.Regular, n.Z];
        if (sp == (int)Species.Meowstic) return [n.MeowsticM, n.MeowsticF];
        if (sp == (int)Species.Magearna) return [n.Magearna0, n.Magearna1];
        if (sp == (int)Species.Tatsugiri) return [n.Tatsu0, n.Tatsu1, n.Tatsu2];
        return [n.Regular];
    }

    private static FilaTablaP Marca(int id, string nombre, Func<ContextoP, bool> leer, Action<ContextoP, bool> escribir) => new() { Id = id, Nombre = nombre, EsMarca = true, Leer = x => leer(x), Escribir = (x, v) => escribir(x, (bool)v) };

    public static EditorDefP Crear(SAV9ZA sav)
    {
        string[] nombres = [IdiomaInterfaz.T("Pokedex9.NuevoPokedex"), IdiomaInterfaz.T("Pokedex9.MachoVisto"), IdiomaInterfaz.T("Pokedex9.HembraVista"), IdiomaInterfaz.T("Pokedex9.GeneroVisto"), IdiomaInterfaz.T("Pokedex9a.AlfaVisto"), IdiomaInterfaz.T("Pokedex8.MostrarVariocolor")];
        var marcas = new TablaDefP
        {
            Id = "marcas", SinNumero = true, Marcador = IdiomaInterfaz.T("Libreria.BuscarEspecie"),
            Filas = c => [.. Especies(c).SelectMany(sp => Enumerable.Range(0, nombres.Length).Select(t => Marca(sp * 16 + t, $"{Nombre(c, sp)} · {nombres[t]}",
                x => { var e = Z(x).GetEntry(sp); return t switch { 0 => e.GetDisplayIsNew(), 1 => e.GetIsGenderSeen(0), 2 => e.GetIsGenderSeen(1), 3 => e.GetIsGenderSeen(2), 4 => e.GetIsSeenAlpha(), _ => e.GetDisplayIsShiny() }; },
                (x, v) => { var e = Z(x).GetEntry(sp); switch (t) { case 0: e.SetDisplayIsNew(v); break; case 1: e.SetIsGenderSeen(0, v); break; case 2: e.SetIsGenderSeen(1, v); break; case 3: e.SetIsGenderSeen(2, v); break; case 4: e.SetIsSeenAlpha(v); break; default: e.SetDisplayIsShiny(v); break; } })))],
        };
        var megas = new TablaDefP
        {
            Id = "megas", SinNumero = true, Marcador = IdiomaInterfaz.T("Libreria.BuscarEspecie"), Ayuda = IdiomaInterfaz.T("Pokedex9a.MegaformasVariantesEspecialesVistas"),
            Filas = c => [.. Especies(c).SelectMany(sp => Megas(c, sp).Select((n, i) => Marca(sp * 16 + i, $"{Nombre(c, sp)} · {n}", x => Z(x).GetEntry(sp).GetIsSeenMega((byte)i), (x, v) => Z(x).GetEntry(sp).SetIsSeenMega((byte)i, v))))],
        };
        string[] tipos = [IdiomaInterfaz.T("Pokedex9a.FormaCapturada"), IdiomaInterfaz.T("Pokedex9.FormaVista"), IdiomaInterfaz.T("Pokedex9a.FormaVariocolorVista")];
        var formas = new TablaDefP
        {
            Id = "formas", SinNumero = true, Marcador = IdiomaInterfaz.T("Pokedex56.EspecieForma"),
            Filas = c =>
            {
                var l = new List<FilaTablaP>();
                foreach (var sp in Especies(c))
                {
                    var f = Formas(c, sp);
                    for (var i = 0; i < Math.Min(f.Length, 32); i++)
                        for (var t = 0; t < 3; t++)
                        {
                            var s = sp; var b = (byte)i; var tt = t;
                            l.Add(Marca(s * 128 + t * 32 + i, $"{Nombre(c, s)} · {f[i]} · {tipos[t]}", x => { var e = Z(x).GetEntry(s); return tt switch { 0 => e.GetIsFormCaught(b), 1 => e.GetIsFormSeen(b), _ => e.GetIsShinySeen(b) }; },
                                (x, v) => { var e = Z(x).GetEntry(s); switch (tt) { case 0: e.SetIsFormCaught(b, v); break; case 1: e.SetIsFormSeen(b, v); break; default: e.SetIsShinySeen(b, v); break; } }));
                        }
                }
                return l;
            },
        };
        var idiomas = new TablaDefP
        {
            Id = "idiomas", SinNumero = true, Marcador = IdiomaInterfaz.T("Libreria.BuscarEspecie"),
            Filas = c => [.. Especies(c).SelectMany(sp => Enumerable.Range(0, Idiomas.Length).Select(k => Marca(sp * 16 + k, $"{Nombre(c, sp)} · {Idiomas[k]}", x => Z(x).GetEntry(sp).GetLanguageFlag((int)IdIdioma[k]), (x, v) => Z(x).GetEntry(sp).SetLanguageFlag((int)IdIdioma[k], v))))],
        };
        var numeros = new TablaDefP
        {
            Id = "numeros", SinNumero = true, Marcador = IdiomaInterfaz.T("Libreria.BuscarEspecie"), Ayuda = IdiomaInterfaz.T("Pokedex9a.FormaMostradaNForma"),
            Filas = c => [.. Especies(c).SelectMany(sp => new[]
            {
                new FilaTablaP { Id = sp * 4, Nombre = IdiomaInterfaz.F("Pokedex8.FormaMostrada", Nombre(c, sp)), Min = 0, Max = 255, Leer = x => (long)Z(x).GetEntry(sp).DisplayForm, Escribir = (x, v) => Z(x).GetEntry(sp).DisplayForm = (byte)(long)v },
                new FilaTablaP { Id = sp * 4 + 1, Nombre = IdiomaInterfaz.F("Pokedex8.GeneroMostrado", Nombre(c, sp)), Min = 0, Max = 3, Leer = x => (long)(byte)Z(x).GetEntry(sp).DisplayGender, Escribir = (x, v) => Z(x).GetEntry(sp).DisplayGender = (DisplayGender9a)(byte)(long)v },
            })],
        };
        return new EditorDefP("pokedex", IdiomaInterfaz.T("Partida.Editor.pokedex.Titulo"),
        [
            new("resumen", IdiomaInterfaz.T("Editor.Resumen"), [new(IdiomaInterfaz.T("Pokedex.AccionesLote"), [P.Opcion("lote", IdiomaInterfaz.T("Pokedex.SobreTodaPokedex"), c => 0, (c, v) => Lote(c, (int)v),
                _ => P.Lista(IdiomaInterfaz.T("Form.Trainer7.SinCambios"), IdiomaInterfaz.T("Pokedex8.CompletarTodoCapturado"), IdiomaInterfaz.T("Pokedex8.CompletarVariocolores"), IdiomaInterfaz.T("Pokedex.TodosVistos2"), IdiomaInterfaz.T("Pokedex56.TodosVistosVariocolores"), IdiomaInterfaz.T("Pokedex56.NingunoVisto"), IdiomaInterfaz.T("Pokedex.TodosCapturados2"), IdiomaInterfaz.T("Pokedex8.TodosCapturadosVariocolores"), IdiomaInterfaz.T("Pokedex.NingunoCapturado")), soloAccion: true, ayuda: IdiomaInterfaz.T("Pokedex56.AplicaTodasEspeciesVez"))])]),
            new("marcas", IdiomaInterfaz.T("Pokedex.Vistos"), [], Tabla: marcas), new("megas", "Megas", [], Tabla: megas), new("formas", IdiomaInterfaz.T("Pokedex8b.Formas"), [], Tabla: formas), new("idiomas", IdiomaInterfaz.T("Pokedex.Idiomas"), [], Tabla: idiomas), new("numeros", IdiomaInterfaz.T("Pokedex8a.Mostrado2"), [], Tabla: numeros),
        ], Censo, IdiomaInterfaz.T("Pokedex9a.GenerosMegaformasFormasE"));
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
