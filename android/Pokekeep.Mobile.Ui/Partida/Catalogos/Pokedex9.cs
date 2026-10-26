// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Catalogos;

/// <summary>
/// Pokédex de Escarlata y Púrpura (<c>SAV_PokedexSV</c> para la partida sin DLC y <c>SAV_PokedexSVKitakami</c> para la que ya tiene Kitakami o Academia Arándano): estado, géneros y formas vistas, idiomas y
/// lo mostrado, con las mismas llamadas del Core que los formularios y sus acciones de lote.
/// </summary>
internal static class Pokedex9
{
    public static readonly string[] CensoPaldea = ["PK-SAV-193"], CensoKitakami = ["PK-SAV-194"];
    private static string[] Idiomas => [IdiomaInterfaz.T("ModeloForzarCarga.Japones"), IdiomaInterfaz.T("ModeloForzarCarga.Ingles"), IdiomaInterfaz.T("ModeloForzarCarga.Frances"), IdiomaInterfaz.T("ModeloForzarCarga.Italiano"), IdiomaInterfaz.T("ModeloForzarCarga.Aleman"), IdiomaInterfaz.T("ModeloForzarCarga.Espanol"), IdiomaInterfaz.T("ModeloForzarCarga.Coreano"), "Chino simplificado", "Chino tradicional"];
    private static readonly LanguageID[] IdIdioma = [LanguageID.Japanese, LanguageID.English, LanguageID.French, LanguageID.Italian, LanguageID.German, LanguageID.Spanish, LanguageID.Korean, LanguageID.ChineseS, LanguageID.ChineseT];

    private static Zukan9 Z(ContextoP c) => ((SAV9SV)c.Sav).Blocks.Zukan;
    private static string Nombre(ContextoP c, ushort sp) => sp < c.Textos.Species.Count ? c.Textos.Species[sp] : IdiomaInterfaz.F("DescripcionRegalo.Especie", sp);

    private static string[] Formas(ContextoP c, ushort species, bool alcremie)
    {
        var s = c.Textos;
        var f = alcremie && species == (int)Species.Alcremie ? FormConverter.GetAlcremieFormList(s.forms) : FormConverter.GetFormList(species, s.Types, s.forms, GameInfo.GenderSymbolASCII, EntityContext.Gen9);
        if (f.Length > 0 && f[0].Length == 0) f[0] = s.Types[0];
        return f;
    }

    /// <summary>Las especies del formulario, ordenadas como el: primero las que estan en alguna Pokédex (por grupo e indice), luego las demas.</summary>
    private static IReadOnlyList<ushort> Especies(ContextoP c, int max)
    {
        var sav = (SAV9SV)c.Sav;
        (int Grupo, int Indice) Dex(ushort sp)
        {
            var entry = PersonalTable.SV.GetFormEntry(sp, 0);
            for (byte i = 0; i < entry.FormCount; i++)
            {
                entry = PersonalTable.SV.GetFormEntry(sp, i);
                if (entry.DexPaldea != 0) return (1, entry.DexPaldea);
                if (entry.DexKitakami != 0) return (2, entry.DexKitakami);
                if (entry.DexBlueberry != 0) return (3, entry.DexBlueberry);
            }
            return default;
        }
        return [.. Enumerable.Range(1, Math.Min(max, sav.MaxSpeciesID)).Select(i => (ushort)i).Select(sp => (Sp: sp, D: Dex(sp))).OrderByDescending(x => x.D != default).ThenBy(x => x.D).Select(x => x.Sp)];
    }

    private static FilaTablaP Marca(int id, string nombre, Func<ContextoP, bool> leer, Action<ContextoP, bool> escribir) => new() { Id = id, Nombre = nombre, EsMarca = true, Leer = x => leer(x), Escribir = (x, v) => escribir(x, (bool)v) };
    private static FilaTablaP Numero(int id, string nombre, long max, Func<ContextoP, long> leer, Action<ContextoP, long> escribir) => new() { Id = id, Nombre = nombre, Min = 0, Max = max, Leer = x => leer(x), Escribir = (x, v) => escribir(x, (long)v) };

    public static EditorDefP Crear(SAV9SV sav) => sav.SaveRevision == 0 ? Paldea() : Kitakami();

    // ---- Sin DLC ----
    private static EditorDefP Paldea()
    {
        PokeDexEntry9Paldea E(ContextoP c, ushort sp) => Z(c).DexPaldea.Get(sp);
        IReadOnlyList<ushort> Sp(ContextoP c) => Especies(c, (int)Species.IronLeaves);

        var estados = new TablaDefP
        {
            Id = "estados", SinNumero = true, Marcador = IdiomaInterfaz.T("Libreria.BuscarEspecie"), Ayuda = IdiomaInterfaz.T("Pokedex8b.VerOidoHablarElla"),
            Filas = c => [.. Sp(c).Select(sp => Numero(sp, Nombre(c, sp), 3, x => E(x, sp).GetState(), (x, v) => E(x, sp).SetState((uint)v)))],
        };
        string[] nombres = [IdiomaInterfaz.T("Pokedex9.NuevoPokedex"), IdiomaInterfaz.T("Pokedex9.MachoVisto"), IdiomaInterfaz.T("Pokedex9.HembraVista"), IdiomaInterfaz.T("Pokedex9.GeneroVisto"), IdiomaInterfaz.T("Pokedex9.VariocolorVisto"), IdiomaInterfaz.T("Pokedex8.MostrarVariocolor"), IdiomaInterfaz.T("Pokedex9.GeneroMostradoDistinto")];
        var marcas = new TablaDefP
        {
            Id = "marcas", SinNumero = true, Marcador = IdiomaInterfaz.T("Libreria.BuscarEspecie"),
            Filas = c => [.. Sp(c).SelectMany(sp => Enumerable.Range(0, nombres.Length).Select(t => Marca(sp * 16 + t, $"{Nombre(c, sp)} · {nombres[t]}",
                x => { var e = E(x, sp); return t switch { 0 => e.GetDisplayIsNew(), 1 => e.GetIsGenderSeen(0), 2 => e.GetIsGenderSeen(1), 3 => e.GetIsGenderSeen(2), 4 => e.GetSeenIsShiny(), 5 => e.GetDisplayIsShiny(), _ => e.GetDisplayGenderIsDifferent() }; },
                (x, v) => { var e = E(x, sp); switch (t) { case 0: e.SetDisplayIsNew(v); break; case 1: e.SetIsGenderSeen(0, v); break; case 2: e.SetIsGenderSeen(1, v); break; case 3: e.SetIsGenderSeen(2, v); break; case 4: e.SetSeenIsShiny(v); break; case 5: e.SetDisplayIsShiny(v); break; default: e.SetDisplayGenderIsDifferent(v); break; } })))],
        };
        var formas = new TablaDefP
        {
            Id = "formas", SinNumero = true, Marcador = IdiomaInterfaz.T("Pokedex56.EspecieForma"),
            Filas = c => [.. Sp(c).SelectMany(sp => Formas(c, sp, true).Select((n, i) => (byte)i).Select(i => Marca(sp * 64 + i, $"{Nombre(c, sp)} · {Formas(c, sp, true)[i]}", x => E(x, sp).GetIsFormSeen(i), (x, v) => E(x, sp).SetIsFormSeen(i, v))))],
        };
        var idiomas = new TablaDefP
        {
            Id = "idiomas", SinNumero = true, Marcador = IdiomaInterfaz.T("Libreria.BuscarEspecie"),
            Filas = c => [.. Sp(c).SelectMany(sp => Enumerable.Range(0, Idiomas.Length).Select(k => Marca(sp * 16 + k, $"{Nombre(c, sp)} · {Idiomas[k]}", x => E(x, sp).GetLanguageFlag((int)IdIdioma[k]), (x, v) => E(x, sp).SetLanguageFlag((int)IdIdioma[k], v))))],
        };
        var numeros = new TablaDefP
        {
            Id = "numeros", SinNumero = true, Marcador = IdiomaInterfaz.T("Libreria.BuscarEspecie"), Ayuda = IdiomaInterfaz.T("Pokedex9.GeneroMostradoMachoHembra"),
            Filas = c => [.. Sp(c).SelectMany(sp => new[]
            {
                Numero(sp * 4, IdiomaInterfaz.F("Pokedex8.GeneroMostrado", Nombre(c, sp)), 2, x => E(x, sp).GetDisplayGender(), (x, v) => E(x, sp).SetDisplayGender((int)v)),
                Numero(sp * 4 + 1, IdiomaInterfaz.F("Pokedex8.FormaMostrada", Nombre(c, sp)), 255, x => E(x, sp).GetDisplayForm(), (x, v) => E(x, sp).SetDisplayForm((uint)v)),
            })],
        };
        return new EditorDefP("pokedex", IdiomaInterfaz.T("Partida.Editor.pokedex.Titulo"),
        [
            new("resumen", IdiomaInterfaz.T("Editor.Resumen"), [new(IdiomaInterfaz.T("Pokedex.AccionesLote"), [P.Opcion("lote", IdiomaInterfaz.T("Pokedex.SobreTodaPokedex"), c => 0, (c, v) => Lote(c, (int)v), _ => Acciones, soloAccion: true, ayuda: IdiomaInterfaz.T("Pokedex56.AplicaTodasEspeciesVez"))])]),
            new("estados", IdiomaInterfaz.T("CartasP.Estado"), [], Tabla: estados), new("marcas", IdiomaInterfaz.T("Pokedex.Vistos"), [], Tabla: marcas), new("formas", IdiomaInterfaz.T("Pokedex8b.Formas"), [], Tabla: formas), new("idiomas", IdiomaInterfaz.T("Pokedex.Idiomas"), [], Tabla: idiomas), new("numeros", IdiomaInterfaz.T("Pokedex8a.Mostrado2"), [], Tabla: numeros),
        ], CensoPaldea, IdiomaInterfaz.T("Pokedex9.EstadoCadaEspeciePaldea"));
    }

    private static IReadOnlyList<OpcionP> Acciones => P.Lista(IdiomaInterfaz.T("Form.Trainer7.SinCambios"), IdiomaInterfaz.T("Pokedex8.CompletarTodoCapturado"), IdiomaInterfaz.T("Pokedex8.CompletarVariocolores"), IdiomaInterfaz.T("Pokedex.TodosVistos2"), IdiomaInterfaz.T("Pokedex56.TodosVistosVariocolores"), IdiomaInterfaz.T("Pokedex56.NingunoVisto"), IdiomaInterfaz.T("Pokedex.TodosCapturados2"), IdiomaInterfaz.T("Pokedex8.TodosCapturadosVariocolores"), IdiomaInterfaz.T("Pokedex.NingunoCapturado"));

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

    // ---- Con Kitakami / Academia Arandano ----
    private static EditorDefP Kitakami()
    {
        PokeDexEntry9Kitakami E(ContextoP c, ushort sp) => Z(c).DexKitakami.Get(sp);
        IReadOnlyList<ushort> Sp(ContextoP c) => Especies(c, int.MaxValue);
        string[] tipos = [IdiomaInterfaz.T("Pokedex9.FormaVista"), IdiomaInterfaz.T("Pokedex9.FormaObtenida"), IdiomaInterfaz.T("Pokedex9.FormaOida"), IdiomaInterfaz.T("Pokedex9.FormaMiradaLibro")];

        var formas = new TablaDefP
        {
            Id = "formas", SinNumero = true, Marcador = IdiomaInterfaz.T("Pokedex56.EspecieForma"), Ayuda = IdiomaInterfaz.T("Pokedex9.FilaFormaVistaObtenida"),
            Filas = c =>
            {
                var l = new List<FilaTablaP>();
                foreach (var sp in Sp(c))
                {
                    var f = Formas(c, sp, false);
                    for (var i = 0; i < Math.Min(f.Length, 32); i++)
                        for (var t = 0; t < 4; t++)
                        {
                            var s = sp; var b = (byte)i; var tt = t;
                            l.Add(Marca(s * 256 + t * 32 + i, $"{Nombre(c, s)} · {f[i]} · {tipos[t]}",
                                x => { var e = E(x, s); return tt switch { 0 => e.GetSeenForm(b), 1 => e.GetObtainedForm(b), 2 => e.GetHeardForm(b), _ => e.GetCheckedForm(b) }; },
                                (x, v) => { var e = E(x, s); switch (tt) { case 0: e.SetSeenForm(b, v); break; case 1: e.SetObtainedForm(b, v); break; case 2: e.SetHeardForm(b, v); break; default: e.SetCheckedForm(b, v); break; } }));
                        }
                }
                return l;
            },
        };
        string[] nombres = [IdiomaInterfaz.T("Pokedex9.MachoVisto"), IdiomaInterfaz.T("Pokedex9.HembraVista"), IdiomaInterfaz.T("Pokedex9.GeneroVisto"), IdiomaInterfaz.T("Pokedex9.VariocolorVisto")];
        var marcas = new TablaDefP
        {
            Id = "marcas", SinNumero = true, Marcador = IdiomaInterfaz.T("Libreria.BuscarEspecie"),
            Filas = c => [.. Sp(c).SelectMany(sp => Enumerable.Range(0, nombres.Length).Select(t => Marca(sp * 16 + t, $"{Nombre(c, sp)} · {nombres[t]}",
                x => t < 3 ? E(x, sp).GetIsGenderSeen((byte)t) : E(x, sp).GetIsModelSeen(true), (x, v) => { if (t < 3) E(x, sp).SetIsGenderSeen((byte)t, v); else E(x, sp).SetIsModelSeen(true, v); })))],
        };
        var idiomas = new TablaDefP
        {
            Id = "idiomas", SinNumero = true, Marcador = IdiomaInterfaz.T("Libreria.BuscarEspecie"),
            Filas = c => [.. Sp(c).SelectMany(sp => Enumerable.Range(0, Idiomas.Length).Select(k => Marca(sp * 16 + k, $"{Nombre(c, sp)} · {Idiomas[k]}", x => E(x, sp).GetLanguageFlag((int)IdIdioma[k]), (x, v) => E(x, sp).SetLanguageFlag((int)IdIdioma[k], v))))],
        };
        string[] dexes = ["Paldea", "Kitakami", IdiomaInterfaz.T("Pokedex9.Arandano")];
        var mostrado = new TablaDefP
        {
            Id = "mostrado", SinNumero = true, Marcador = IdiomaInterfaz.T("Libreria.BuscarEspecie"), Ayuda = IdiomaInterfaz.T("Pokedex9.FormaNGeneroMacho"),
            Filas = c => [.. Sp(c).SelectMany(sp => Enumerable.Range(0, 9).Select(t =>
            {
                var d = t / 3; var q = t % 3;
                return Numero(sp * 16 + t, $"{Nombre(c, sp)} · {dexes[d]} · {(q == 0 ? IdiomaInterfaz.T("Pokedex9.FormaMostrada") : q == 1 ? IdiomaInterfaz.T("Pokedex9.GeneroMostrado") : IdiomaInterfaz.T("Pokedex9.VariocolorMostrado"))}", q == 0 ? 255 : q == 1 ? 2 : 1,
                    x => { var e = E(x, sp); return (long)(d switch { 0 => q switch { 0 => e.DisplayedPaldeaForm, 1 => e.DisplayedPaldeaGender, _ => e.DisplayedPaldeaShiny }, 1 => q switch { 0 => e.DisplayedKitakamiForm, 1 => e.DisplayedKitakamiGender, _ => e.DisplayedKitakamiShiny }, _ => q switch { 0 => e.DisplayedBlueberryForm, 1 => e.DisplayedBlueberryGender, _ => e.DisplayedBlueberryShiny } }); },
                    (x, v) =>
                    {
                        var e = E(x, sp); var b = (byte)v;
                        // Como el formulario: las tres cifras de esa Pokédex se escriben juntas con SetLocal...
                        if (d == 0) e.SetLocalPaldea(q == 0 ? b : e.DisplayedPaldeaForm, q == 1 ? b : e.DisplayedPaldeaGender, q == 2 ? b : e.DisplayedPaldeaShiny);
                        else if (d == 1) e.SetLocalKitakami(q == 0 ? b : e.DisplayedKitakamiForm, q == 1 ? b : e.DisplayedKitakamiGender, q == 2 ? b : e.DisplayedKitakamiShiny);
                        else e.SetLocalBlueberry(q == 0 ? b : e.DisplayedBlueberryForm, q == 1 ? b : e.DisplayedBlueberryGender, q == 2 ? b : e.DisplayedBlueberryShiny);
                    });
            }))],
        };
        return new EditorDefP("pokedex", IdiomaInterfaz.T("Partida.Editor.pokedex.Titulo"),
        [
            new("resumen", IdiomaInterfaz.T("Editor.Resumen"), [new(IdiomaInterfaz.T("Pokedex.AccionesLote"), [P.Opcion("lote", IdiomaInterfaz.T("Pokedex.SobreTodaPokedex"), c => 0, (c, v) => Lote(c, (int)v), _ => Acciones, soloAccion: true, ayuda: IdiomaInterfaz.T("Pokedex56.AplicaTodasEspeciesVez"))])]),
            new("formas", IdiomaInterfaz.T("Pokedex8b.Formas"), [], Tabla: formas), new("marcas", IdiomaInterfaz.T("Pokedex.Vistos"), [], Tabla: marcas), new("idiomas", IdiomaInterfaz.T("Pokedex.Idiomas"), [], Tabla: idiomas), new("mostrado", IdiomaInterfaz.T("Pokedex8a.Mostrado2"), [], Tabla: mostrado),
        ], CensoKitakami, IdiomaInterfaz.T("Pokedex9.FormasGenerosEIdiomas"));
    }
}
