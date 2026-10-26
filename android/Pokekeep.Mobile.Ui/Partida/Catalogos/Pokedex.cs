// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;
using static PKHeX.Core.Zukan4;

namespace Pokekeep.Mobile.Ui.Partida.Catalogos;

/// <summary>
/// Pokédex (<c>SAV_SimplePokedex</c>, <c>SAV_Pokedex4</c> y, por generaciones, los demas formularios de Pokédex): una tabla de «Vistos» y otra de «Capturados» con la llamada del Core de cada formulario,
/// y las acciones de lote del formulario. Las filas que dependen una de otra (capturado implica visto) se mueven juntas en el borrador.
/// </summary>
internal static class PokedexP
{
    public static readonly string[] Censo = ["PK-SAV-228", "PK-SAV-054"];

    /// <summary>Por que este juego NO tiene Pokédex editable (null si la tiene).</summary>
    public static string? NoAplica(SaveFile sav) => Crear(sav) is not null ? null : sav.HasPokeDex ? IdiomaInterfaz.T("Partida.NoAplica.PokedexPendiente") : IdiomaInterfaz.T("Partida.NoAplica.Pokedex");

    public static EditorDefP? Crear(SaveFile sav) => sav switch
    {
        SAV1 or SAV2 or SAV3 => Sencilla(sav),
        SAV4 s4 => Gen4(s4),
        SAV7 s7 => Pokedex7.Crear(s7),
        SAV7b b7 => Pokedex7.Crear(b7),
        SAV8BS bs => Pokedex8b.Crear(bs),
        SAV8SWSH sw => Pokedex8.Crear(sw),
        SAV8LA la => Pokedex8a.Crear(la),
        SAV9SV sv => Pokedex9.Crear(sv),
        SAV9ZA za => Pokedex9a.Crear(za),
        SAV5 s5 => Pokedex56.Crear(s5, c => new Zukan5Adaptador(((SAV5)c.Sav).Zukan), ["PK-SAV-102"]),
        SAV6XY xy => Pokedex56.Crear(xy, c => new Zukan6Adaptador(((SAV6XY)c.Sav).Blocks.Zukan), ["PK-SAV-114"]),
        SAV6AO ao => Pokedex56.Crear(ao, c => new Zukan6Adaptador(((SAV6AO)c.Sav).Blocks.Zukan), ["PK-SAV-113"]),
        _ => null,
    };

    private static string Nombre(ContextoP c, ushort sp) => sp < c.Textos.Species.Count ? c.Textos.Species[sp] : IdiomaInterfaz.F("DescripcionRegalo.Especie", sp);

    private static IReadOnlyList<FilaTablaP> FilasEspecies(ContextoP c, Func<ContextoP, ushort, bool> leer, Action<ContextoP, ushort, bool> escribir, Func<ushort, bool>? incluye = null)
    {
        var l = new List<FilaTablaP>();
        for (ushort sp = 1; sp <= c.Sav.MaxSpeciesID; sp++)
        {
            if (incluye is not null && !incluye(sp)) continue;
            var s = sp;
            l.Add(new FilaTablaP { Id = s, Nombre = Nombre(c, s), EsMarca = true, Leer = x => leer(x, s), Escribir = (x, v) => escribir(x, s, (bool)v) });
        }
        return l;
    }

    private static string Clave(string tabla, int id) => $"{tabla}:{id}";

    // ---- Gen. 1 a 3: SAV_SimplePokedex ----
    private static EditorDefP Sencilla(SaveFile sav)
    {
        var vistos = new TablaDefP { Id = "vistos", SinNumero = true, Marcador = IdiomaInterfaz.T("Libreria.BuscarEspecie"), Filas = c => FilasEspecies(c, (x, sp) => x.Sav.GetSeen(sp), (x, sp, v) => x.Sav.SetSeen(sp, v)) };
        var capturados = new TablaDefP { Id = "capturados", SinNumero = true, Marcador = IdiomaInterfaz.T("Libreria.BuscarEspecie"), Filas = c => FilasEspecies(c, (x, sp) => x.Sav.GetCaught(sp), (x, sp, v) => x.Sav.SetCaught(sp, v)) };
        return new EditorDefP("pokedex", IdiomaInterfaz.T("Partida.Editor.pokedex.Titulo"),
        [
            new("resumen", IdiomaInterfaz.T("Editor.Resumen"), [new(IdiomaInterfaz.T("CartasP.Estado"), [P.Info("vistoscuenta", IdiomaInterfaz.T("Pokedex.EspeciesVistas"), c => Cuenta(c, x => x.Sav.GetSeen)), P.Info("capturadoscuenta", IdiomaInterfaz.T("Pokedex.EspeciesCapturadas"), c => Cuenta(c, x => x.Sav.GetCaught))]),
                new(IdiomaInterfaz.T("Pokedex.AccionesLote"), Acciones(sav))]),
            new("vistos", IdiomaInterfaz.T("Pokedex.Vistos"), [], Tabla: vistos),
            new("capturados", IdiomaInterfaz.T("Pokedex.Capturados"), [], Tabla: capturados),
        ], Censo, IdiomaInterfaz.T("Pokedex.PokemonVistosCapturados"))
        {
            PrepararAplicar = c => c.Sav is SAV3 ? (Action<ContextoP>)(x => Sanidad3(x.Sav)) : null,
        };
    }

    private static string Cuenta(ContextoP c, Func<ContextoP, Func<ushort, bool>> f)
    {
        var g = f(c); var n = 0;
        for (ushort sp = 1; sp <= c.Sav.MaxSpeciesID; sp++) if (g(sp)) n++;
        return IdiomaInterfaz.F("Cajas.0De1", n, c.Sav.MaxSpeciesID);
    }

    /// <summary>Lo que hace <c>SanityCheck</c> de SAV_SimplePokedex al guardar: quita las capturas de especies ajenas de FRLG de Consola Virtual y copia las banderas de visto de Gen. 3.</summary>
    private static void Sanidad3(SaveFile sav)
    {
        if (sav is SAV3FRLG { IsVirtualConsole: true })
        {
            for (int i = 150; i < sav.MaxSpeciesID; i++)
            {
                ushort species = (ushort)(i + 1);
                if (!Legal.IsForeignFRLG(species)) continue;
                sav.SetCaught(species, false);
            }
        }
        if (sav is SAV3 s3) s3.MirrorSeenFlags();
    }

    private static IReadOnlyList<CampoP> Acciones(SaveFile sav) =>
    [
        P.Opcion("lotevistos", IdiomaInterfaz.T("Pokedex.TodosVistos"), c => 0, (c, v) => { if (v is 1 or 2) for (ushort sp = 1; sp <= c.Sav.MaxSpeciesID; sp++) c.Sav.SetSeen(sp, v == 1); },
            _ => P.Lista(IdiomaInterfaz.T("Form.Trainer7.SinCambios"), IdiomaInterfaz.T("Pokedex.MarcarTodosVistos"), IdiomaInterfaz.T("Pokedex.QuitarTodosVistos")), soloAccion: true, ayuda: IdiomaInterfaz.T("Pokedex.MarcaQuitaVistoTodas")),
        P.Opcion("lotecapturados", IdiomaInterfaz.T("Pokedex.TodosCapturados"), c => 0, (c, v) => { if (v is 1 or 2) for (ushort sp = 1; sp <= c.Sav.MaxSpeciesID; sp++) c.Sav.SetCaught(sp, v == 1); },
            _ => P.Lista(IdiomaInterfaz.T("Form.Trainer7.SinCambios"), IdiomaInterfaz.T("Pokedex.MarcarTodosCapturados"), IdiomaInterfaz.T("Pokedex.QuitarTodosCapturados")), soloAccion: true, ayuda: IdiomaInterfaz.T("Pokedex.MarcaQuitaCapturadoTodas")),
    ];

    // ---- Gen. 4: SAV_Pokedex4 ----
    private static string[] Idiomas4 => [IdiomaInterfaz.T("ModeloForzarCarga.Japones"), IdiomaInterfaz.T("ModeloForzarCarga.Ingles"), IdiomaInterfaz.T("ModeloForzarCarga.Frances"), IdiomaInterfaz.T("ModeloForzarCarga.Aleman"), IdiomaInterfaz.T("ModeloForzarCarga.Italiano"), IdiomaInterfaz.T("ModeloForzarCarga.Espanol")];

    private static Zukan4 D4(ContextoP c) => ((SAV4)c.Sav).Dex;

    private static EditorDefP Gen4(SAV4 sav)
    {
        const string vis = "vistos", cap = "capturados", idi = "idiomas";
        var vistos = new TablaDefP
        {
            Id = vis, SinNumero = true, Marcador = IdiomaInterfaz.T("Libreria.BuscarEspecie"), Ayuda = IdiomaInterfaz.T("Pokedex.QuitarVistoEspecieQuita"),
            Filas = c => FilasEspecies(c, (x, sp) => D4(x).GetSeen(sp), (x, sp, v) => { if (v) D4(x).ModifyAll(sp, SetDexArgs.SeenAll | SetDexArgs.SetAllForms); else D4(x).ClearSeen(sp); }),
            Coherencia = (c, f, v) => (bool)v ? [] : [(Clave(cap, f.Id), false), .. Enumerable.Range(0, Idiomas4.Length).Select(i => (Clave(idi, f.Id * 8 + i), (object)false))],
        };
        var capturados = new TablaDefP
        {
            Id = cap, SinNumero = true, Marcador = IdiomaInterfaz.T("Libreria.BuscarEspecie"), Ayuda = IdiomaInterfaz.T("Pokedex.MarcarEspecieCapturadaMarca"),
            Filas = c => FilasEspecies(c, (x, sp) => D4(x).GetCaught(sp), (x, sp, v) => D4(x).SetCaught(sp, v)),
            Coherencia = (c, f, v) => (bool)v ? [(Clave(vis, f.Id), true)] : [],
        };
        var idiomas = new TablaDefP
        {
            Id = idi, SinNumero = true, Marcador = IdiomaInterfaz.T("Libreria.BuscarEspecie"), Ayuda = IdiomaInterfaz.T("Pokedex.IdiomasVistoCadaEspecie"),
            Filas = c =>
            {
                var l = new List<FilaTablaP>();
                for (ushort sp = 1; sp <= c.Sav.MaxSpeciesID; sp++)
                {
                    if (!D4(c).HasLanguage(sp)) continue;
                    for (var i = 0; i < Idiomas4.Length; i++)
                    {
                        var s = sp; var k = i;
                        l.Add(new FilaTablaP { Id = s * 8 + k, Nombre = $"{Nombre(c, s)} · {Idiomas4[k]}", EsMarca = true, Leer = x => D4(x).GetLanguageBitIndex(s, k), Escribir = (x, v) => D4(x).SetLanguageBitIndex(s, k, (bool)v) });
                    }
                }
                return l;
            },
        };
        string[] modos = [IdiomaInterfaz.T("Pokedex.Dar"), IdiomaInterfaz.T("Pokedex.ModoSimple"), IdiomaInterfaz.T("Pokedex.DetectarFormas"), IdiomaInterfaz.T("Pokedex.PokedexNacional"), IdiomaInterfaz.T("Pokedex.OtrosIdiomas")];
        if (sav is SAV4HGSS) modos = [.. modos.Where((_, i) => i != 2)];
        return new EditorDefP("pokedex", IdiomaInterfaz.T("Partida.Editor.pokedex.Titulo"),
        [
            new("resumen", IdiomaInterfaz.T("Editor.Resumen"), [new(IdiomaInterfaz.T("CartasP.Estado"), [P.Info("vistoscuenta", IdiomaInterfaz.T("Pokedex.EspeciesVistas"), c => Cuenta(c, x => x.Sav.GetSeen)), P.Info("capturadoscuenta", IdiomaInterfaz.T("Pokedex.EspeciesCapturadas"), c => Cuenta(c, x => x.Sav.GetCaught)),
                    P.Opcion("modo", IdiomaInterfaz.T("Pokedex.ModoPokedex"), c => ((SAV4)c.Sav).DexUpgraded, (c, v) => ((SAV4)c.Sav).DexUpgraded = (int)v,
                        c => { var l = modos.Select((t, i) => new OpcionP(i, t)).ToList(); var cur = ((SAV4)c.Sav).DexUpgraded; if (cur >= l.Count) l.Add(new OpcionP(cur, IdiomaInterfaz.F("Eventos.Valor", cur))); return l; },
                        ayuda: IdiomaInterfaz.T("Pokedex.HastaDondeMejoradoJuego"))]),
                new(IdiomaInterfaz.T("Pokedex.AccionesLote"), [P.Opcion("lote", IdiomaInterfaz.T("Pokedex.SobreTodaPokedex"), c => 0, (c, v) => Lote4(c, (int)v),
                    _ => P.Lista(IdiomaInterfaz.T("Form.Trainer7.SinCambios"), IdiomaInterfaz.T("Pokedex.CompletarVistoCapturadoIdiomas"), IdiomaInterfaz.T("Pokedex.TodosVistos2"), IdiomaInterfaz.T("Pokedex.TodosCapturados2"), IdiomaInterfaz.T("Pokedex.NingunoCapturado"), IdiomaInterfaz.T("Pokedex.VaciarPokedex")), soloAccion: true, ayuda: IdiomaInterfaz.T("Pokedex.AplicaEspeciesVez"))])]),
            new("vistos", IdiomaInterfaz.T("Pokedex.Vistos"), [], Tabla: vistos),
            new("capturados", IdiomaInterfaz.T("Pokedex.Capturados"), [], Tabla: capturados),
            new("idiomas", IdiomaInterfaz.T("Pokedex.Idiomas"), [], Tabla: idiomas),
        ], Censo, IdiomaInterfaz.T("Pokedex.PokemonVistosCapturadosIdiomas"));
    }

    private static void Lote4(ContextoP c, int v)
    {
        var dex = D4(c);
        var lang = GetGen4LanguageBitIndex(c.Sav.Language);
        SetDexArgs args = v switch { 1 => SetDexArgs.Complete, 2 => SetDexArgs.SeenAll, 3 => SetDexArgs.CaughtAll, 4 => SetDexArgs.CaughtNone, 5 => SetDexArgs.None, _ => (SetDexArgs)(-1) };
        if ((int)args == -1) return;
        for (ushort i = 1; i <= 493; i++) dex.ModifyAll(i, args, lang);
    }
}
