// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Catalogos;

/// <summary>Lo que tienen en comun la Pokédex de Gen. 5 (<c>Zukan5</c>) y la de Gen. 6 (<c>Zukan6</c>): mismas particiones, idiomas y formas, con clases del Core distintas.</summary>
internal interface IZukan56
{
    int MaxEntrada { get; }
    bool TieneIdiomas(ushort sp);
    bool GetCaught(ushort sp); void SetCaught(ushort sp, bool v);
    bool GetSeen(ushort sp, int r); void SetSeen(ushort sp, int r, bool v);
    bool GetDisplayed(ushort sp, int r); void SetDisplayed(ushort sp, int r, bool v);
    bool GetLanguage(ushort sp, int i); void SetLanguage(ushort sp, int i, bool v);
    (int Indice, int Cantidad) Formas(ushort sp);
    bool GetFormFlag(int i, int r); void SetFormFlag(int i, int r, bool v);
    bool NacionalDesbloqueada { get; set; }
    bool NacionalActiva { get; set; }
    uint Spinda { get; set; }
    bool? GetAjena(ushort sp); void SetAjena(ushort sp, bool v);
    void SeenNone(); void SeenAll(bool variocolor);
    void CaughtNone(); void CaughtAll(LanguageID idioma, bool todosIdiomas);
    void FormasNinguna(); void FormasPrimera(bool variocolor); void FormasTodas(bool variocolor);
}

internal sealed class Zukan5Adaptador(Zukan5 z) : IZukan56
{
    public int MaxEntrada => 649;
    public bool TieneIdiomas(ushort sp) => sp <= 493;
    public bool GetCaught(ushort sp) => z.GetCaught(sp); public void SetCaught(ushort sp, bool v) => z.SetCaught(sp, v);
    public bool GetSeen(ushort sp, int r) => z.GetSeen(sp, r); public void SetSeen(ushort sp, int r, bool v) => z.SetSeen(sp, r, v);
    public bool GetDisplayed(ushort sp, int r) => z.GetDisplayed(sp, r); public void SetDisplayed(ushort sp, int r, bool v) => z.SetDisplayed(sp, r, v);
    public bool GetLanguage(ushort sp, int i) => z.GetLanguageFlag(sp, i); public void SetLanguage(ushort sp, int i, bool v) => z.SetLanguageFlag(sp, i, v);
    public (int, int) Formas(ushort sp) { var (i, c) = z.GetFormIndex(sp); return (i, c); }
    public bool GetFormFlag(int i, int r) => z.GetFormFlag(i, r); public void SetFormFlag(int i, int r, bool v) => z.SetFormFlag(i, r, v);
    public bool NacionalDesbloqueada { get => z.IsNationalDexUnlocked; set => z.IsNationalDexUnlocked = value; }
    public bool NacionalActiva { get => z.IsNationalDexMode; set => z.IsNationalDexMode = value; }
    public uint Spinda { get => z.Spinda; set => z.Spinda = value; }
    public bool? GetAjena(ushort sp) => null; public void SetAjena(ushort sp, bool v) { }
    public void SeenNone() => z.SeenNone(); public void SeenAll(bool v) => z.SeenAll(v);
    public void CaughtNone() => z.CaughtNone(); public void CaughtAll(LanguageID l, bool t) => z.CaughtAll(l, t);
    public void FormasNinguna() => z.ClearFormSeen(); public void FormasPrimera(bool v) => z.SetFormsSeen1(v); public void FormasTodas(bool v) => z.SetFormsSeen(v);
}

internal sealed class Zukan6Adaptador(Zukan6 z) : IZukan56
{
    public int MaxEntrada => 721;
    public bool TieneIdiomas(ushort sp) => true;
    public bool GetCaught(ushort sp) => z.GetCaught(sp); public void SetCaught(ushort sp, bool v) => z.SetCaught(sp, v);
    public bool GetSeen(ushort sp, int r) => z.GetSeen(sp, r); public void SetSeen(ushort sp, int r, bool v) => z.SetSeen(sp, r, v);
    public bool GetDisplayed(ushort sp, int r) => z.GetDisplayed(sp, r); public void SetDisplayed(ushort sp, int r, bool v) => z.SetDisplayed(sp, r, v);
    public bool GetLanguage(ushort sp, int i) => z.GetLanguageFlag(sp, i); public void SetLanguage(ushort sp, int i, bool v) => z.SetLanguageFlag(sp, i, v);
    public (int, int) Formas(ushort sp) { var (i, c) = z.GetFormIndex(sp); return (i, c); }
    public bool GetFormFlag(int i, int r) => z.GetFormFlag(i, r); public void SetFormFlag(int i, int r, bool v) => z.SetFormFlag(i, r, v);
    public bool NacionalDesbloqueada { get => z.IsNationalDexUnlocked; set => z.IsNationalDexUnlocked = value; }
    public bool NacionalActiva { get => z.IsNationalDexMode; set => z.IsNationalDexMode = value; }
    public uint Spinda { get => z.Spinda; set => z.Spinda = value; }
    public bool? GetAjena(ushort sp) => z is Zukan6XY xy && sp <= (int)Species.Genesect ? xy.GetForeignFlag(sp) : null;
    public void SetAjena(ushort sp, bool v) { if (z is Zukan6XY xy && sp <= (int)Species.Genesect) xy.SetForeignFlag(sp, v); }
    public void SeenNone() => z.SeenNone(); public void SeenAll(bool v) => z.SeenAll(v);
    public void CaughtNone() => z.CaughtNone(); public void CaughtAll(LanguageID l, bool t) => z.CaughtAll(l, t);
    public void FormasNinguna() => z.ClearFormSeen(); public void FormasPrimera(bool v) => z.SetFormsSeen1(v); public void FormasTodas(bool v) => z.SetFormsSeen(v);
}

/// <summary>Pokédex de Gen. 5 y 6 (<c>SAV_Pokedex5</c>, <c>SAV_PokedexXY</c>, <c>SAV_PokedexORAS</c>): capturado, visto y mostrado por partición, idiomas, formas y las acciones de lote del formulario.</summary>
internal static class Pokedex56
{
    private static string[] Particiones => [IdiomaInterfaz.T("Form.Trainer8.Male"), IdiomaInterfaz.T("Form.Trainer8.Female"), IdiomaInterfaz.T("Pokedex56.MachoVariocolor"), IdiomaInterfaz.T("Pokedex56.HembraVariocolor")];
    private static string[] Idiomas => [IdiomaInterfaz.T("ModeloForzarCarga.Japones"), IdiomaInterfaz.T("ModeloForzarCarga.Ingles"), IdiomaInterfaz.T("ModeloForzarCarga.Frances"), IdiomaInterfaz.T("ModeloForzarCarga.Italiano"), IdiomaInterfaz.T("ModeloForzarCarga.Aleman"), IdiomaInterfaz.T("ModeloForzarCarga.Espanol"), IdiomaInterfaz.T("ModeloForzarCarga.Coreano")];
    private const string Vis = "vistos", Mos = "mostrados", Cap = "capturados", Idi = "idiomas", FVis = "formasvistas", FMos = "formasmostradas", Aje = "ajenos";

    private static string Clave(string t, int id) => $"{t}:{id}";
    private static string Nombre(ContextoP c, ushort sp) => sp < c.Textos.Species.Count ? c.Textos.Species[sp] : IdiomaInterfaz.F("DescripcionRegalo.Especie", sp);

    public static EditorDefP Crear(SaveFile sav, Func<ContextoP, IZukan56> z, string[] censo)
    {
        // Las particiones que el juego no puede dar (una especie solo hembra no tiene «macho»).
        static bool Permitida(ContextoP c, ushort sp, int r)
        {
            var pi = c.Sav.Personal[sp];
            return (r & 1) == 0 ? !pi.OnlyFemale : !(pi.OnlyMale || pi.Genderless);
        }

        var capturados = new TablaDefP
        {
            Id = Cap, SinNumero = true, Marcador = IdiomaInterfaz.T("Libreria.BuscarEspecie"),
            Filas = c => Enumerable.Range(1, c.Sav.MaxSpeciesID).Select(i => (ushort)i).Select(sp => new FilaTablaP { Id = sp, Nombre = Nombre(c, sp), EsMarca = true, Leer = x => z(x).GetCaught(sp), Escribir = (x, v) => z(x).SetCaught(sp, (bool)v) }).ToList(),
        };
        var vistos = new TablaDefP
        {
            Id = Vis, SinNumero = true, Marcador = IdiomaInterfaz.T("Libreria.BuscarEspecie"), Ayuda = IdiomaInterfaz.T("Pokedex56.FilaEspecieGeneroVariocolor"),
            Filas = c => Filas4(c, z, (x, sp, r) => z(x).GetSeen(sp, r), (x, sp, r, v) => z(x).SetSeen(sp, r, v), Permitida),
            Coherencia = (c, f, v) =>
            {
                var sp = f.Id / 8; var r = f.Id % 8;
                var otras = new List<(string, object)>();
                bool Visto(int k) => c.Borrador.TryGetValue(Clave(Vis, sp * 8 + k), out var o) && o is true;
                bool Mostrado(int k) => c.Borrador.TryGetValue(Clave(Mos, sp * 8 + k), out var o) && o is true;
                if (!Enumerable.Range(0, 4).Any(Visto)) { for (var k = 0; k < 4; k++) if (Mostrado(k)) otras.Add((Clave(Mos, sp * 8 + k), false)); }
                else if (!Enumerable.Range(0, 4).Any(Mostrado) && (bool)v) otras.Add((Clave(Mos, sp * 8 + r), true));
                return otras;
            },
        };
        var mostrados = new TablaDefP
        {
            Id = Mos, SinNumero = true, Marcador = IdiomaInterfaz.T("Libreria.BuscarEspecie"), Ayuda = IdiomaInterfaz.T("Pokedex56.SoloParticionPuedeEstar"),
            Filas = c => Filas4(c, z, (x, sp, r) => z(x).GetDisplayed(sp, r), (x, sp, r, v) => z(x).SetDisplayed(sp, r, v), Permitida),
            Coherencia = (c, f, v) =>
            {
                var sp = f.Id / 8; var r = f.Id % 8;
                if (!(bool)v) return [];
                var otras = new List<(string, object)>();
                for (var k = 0; k < 4; k++) if (k != r) otras.Add((Clave(Mos, sp * 8 + k), false));
                otras.Add((Clave(Vis, sp * 8 + r), true));
                return otras;
            },
        };
        var idiomas = new TablaDefP
        {
            Id = Idi, SinNumero = true, Marcador = IdiomaInterfaz.T("Libreria.BuscarEspecie"), Ayuda = IdiomaInterfaz.T("Pokedex56.IdiomasVistoCadaEspecie"),
            Filas = c =>
            {
                var l = new List<FilaTablaP>();
                for (ushort sp = 1; sp <= c.Sav.MaxSpeciesID; sp++)
                {
                    if (!z(c).TieneIdiomas(sp)) continue;
                    for (var i = 0; i < Idiomas.Length; i++) { var s = sp; var k = i; l.Add(new FilaTablaP { Id = s * 8 + k, Nombre = $"{Nombre(c, s)} · {Idiomas[k]}", EsMarca = true, Leer = x => z(x).GetLanguage(s, k), Escribir = (x, v) => z(x).SetLanguage(s, k, (bool)v) }); }
                }
                return l;
            },
        };
        var fvistas = new TablaDefP
        {
            Id = FVis, SinNumero = true, Marcador = IdiomaInterfaz.T("Pokedex56.EspecieForma"), Ayuda = IdiomaInterfaz.T("Pokedex56.FormasVistasFilaVariocolor"),
            Filas = c => Formas(c, z, 0, 1),
            Coherencia = (c, f, v) => [],
        };
        var fmostradas = new TablaDefP
        {
            Id = FMos, SinNumero = true, Marcador = IdiomaInterfaz.T("Pokedex56.EspecieForma"), Ayuda = IdiomaInterfaz.T("Pokedex56.SoloFormaPuedeEstar"),
            Filas = c => Formas(c, z, 2, 3),
            Coherencia = (c, f, v) =>
            {
                if (!(bool)v) return [];
                var (sp, k) = (f.Id / 256, f.Id % 256);
                var otras = new List<(string, object)>();
                var cuantas = FormConverter.GetFormList((ushort)sp, c.Textos.Types, c.Textos.forms, GameInfo.GenderSymbolUnicode, c.Sav.Context).Length;
                for (var j = 0; j < cuantas * 2; j++) if (j != k) otras.Add((Clave(FMos, sp * 256 + j), false));
                otras.Add((Clave(FVis, sp * 256 + k), true));
                return otras;
            },
        };
        var ajenos = new TablaDefP
        {
            Id = Aje, SinNumero = true, Marcador = IdiomaInterfaz.T("Libreria.BuscarEspecie"), Ayuda = IdiomaInterfaz.T("Pokedex56.EspeciesKalosObtenidasOtro"),
            Filas = c => Enumerable.Range(1, (int)Species.Genesect).Select(i => (ushort)i).Where(sp => z(c).GetAjena(sp) is not null).Select(sp => new FilaTablaP { Id = sp, Nombre = Nombre(c, sp), EsMarca = true, Leer = x => z(x).GetAjena(sp) ?? false, Escribir = (x, v) => z(x).SetAjena(sp, (bool)v) }).ToList(),
        };

        var pestanas = new List<PestanaP>
        {
            new("resumen", IdiomaInterfaz.T("Editor.Resumen"), [new(IdiomaInterfaz.T("Pokedex.PokedexNacional"), [Nacional(z, sav), P.Entero("spinda", IdiomaInterfaz.T("Pokedex56.PidDeSpinda"), c => z(c).Spinda, (c, v) => z(c).Spinda = (uint)v, 0, uint.MaxValue, hex: true, ayuda: IdiomaInterfaz.T("Pokedex56.HastaCifrasHexadecimalesDecide"))]),
                new(IdiomaInterfaz.T("Pokedex.AccionesLote"), [P.Opcion("lote", IdiomaInterfaz.T("Pokedex.SobreTodaPokedex"), c => 0, (c, v) => Lote(c, z, (int)v),
                    _ => P.Lista(IdiomaInterfaz.T("Form.Trainer7.SinCambios"), IdiomaInterfaz.T("Pokedex56.CompletarVistosCapturados"), IdiomaInterfaz.T("Pokedex.TodosVistos2"), IdiomaInterfaz.T("Pokedex56.TodosVistosVariocolores"), IdiomaInterfaz.T("Pokedex56.NingunoVisto"), IdiomaInterfaz.T("Pokedex.TodosCapturados2"), IdiomaInterfaz.T("Pokedex56.TodosCapturadosTodosIdiomas"), IdiomaInterfaz.T("Pokedex.NingunoCapturado")), soloAccion: true, ayuda: IdiomaInterfaz.T("Pokedex56.AplicaTodasEspeciesVez")),
                    P.Opcion("loteformas", IdiomaInterfaz.T("Pokedex56.FormasTodaPokedex"), c => 0, (c, v) => LoteFormas(c, z, (int)v),
                        _ => P.Lista(IdiomaInterfaz.T("Form.Trainer7.SinCambios"), IdiomaInterfaz.T("Pokedex56.QuitarTodasFormasVistas"), IdiomaInterfaz.T("Pokedex56.PrimeraFormaCadaEspecie"), IdiomaInterfaz.T("Pokedex56.TodasFormas"), IdiomaInterfaz.T("Pokedex56.TodasFormasVariocolores")), soloAccion: true)])] ),
            new("capturados", IdiomaInterfaz.T("Pokedex.Capturados"), [], Tabla: capturados),
            new("vistos", IdiomaInterfaz.T("Pokedex.Vistos"), [], Tabla: vistos),
            new("mostrados", IdiomaInterfaz.T("Pokedex56.Mostrados"), [], Tabla: mostrados),
            new("idiomas", IdiomaInterfaz.T("Pokedex.Idiomas"), [], Tabla: idiomas),
            new("formasvistas", IdiomaInterfaz.T("Pokedex56.FormasVistas"), [], Tabla: fvistas),
            new("formasmostradas", IdiomaInterfaz.T("Pokedex56.FormasMostradas"), [], Tabla: fmostradas),
        };
        if (sav is SAV6XY) pestanas.Add(new("ajenos", IdiomaInterfaz.T("Pokedex56.OtroJuego"), [], Tabla: ajenos));
        return new EditorDefP("pokedex", IdiomaInterfaz.T("Partida.Editor.pokedex.Titulo"), pestanas, censo, IdiomaInterfaz.T("Pokedex56.PokemonVistosCapturadosMostrados"));
    }

    private static CampoP Nacional(Func<ContextoP, IZukan56> z, SaveFile sav)
    {
        // El formulario: «desbloqueada» arrastra a «activa» al cambiarla; despues se guardan los dos.
        return new CampoP
        {
            Id = "nacional", Etiqueta = IdiomaInterfaz.T("Pokedex.PokedexNacional"), Tipo = TipoP.Opcion, TituloSelector = IdiomaInterfaz.T("Pokedex.PokedexNacional"),
            Opciones = _ => P.Lista(IdiomaInterfaz.T("Pokedex56.Bloqueada"), IdiomaInterfaz.T("Pokedex56.DesbloqueadaActivar"), IdiomaInterfaz.T("Pokedex56.DesbloqueadaActiva")),
            Leer = c => (long)(z(c).NacionalDesbloqueada ? (z(c).NacionalActiva ? 2 : 1) : 0),
            Escribir = (c, v) => { var n = (long)v!; z(c).NacionalDesbloqueada = n >= 1; z(c).NacionalActiva = n == 2; },
            Ayuda = IdiomaInterfaz.T("Pokedex56.EstadoPokedexNacionalPartida"),
        };
    }

    private static IReadOnlyList<FilaTablaP> Filas4(ContextoP c, Func<ContextoP, IZukan56> z, Func<ContextoP, ushort, int, bool> leer, Action<ContextoP, ushort, int, bool> escribir, Func<ContextoP, ushort, int, bool> permitida)
    {
        var l = new List<FilaTablaP>();
        for (ushort sp = 1; sp <= c.Sav.MaxSpeciesID && sp <= z(c).MaxEntrada; sp++)
            for (var r = 0; r < 4; r++)
            {
                if (!permitida(c, sp, r)) continue;
                var s = sp; var k = r;
                l.Add(new FilaTablaP { Id = s * 8 + k, Nombre = $"{Nombre(c, s)} · {Particiones[k]}", EsMarca = true, Leer = x => leer(x, s, k), Escribir = (x, v) => escribir(x, s, k, (bool)v) });
            }
        return l;
    }

    /// <summary>Una fila por forma (y su variocolor): el indice de fila es especie*256 + posicion en la lista del formulario (formas, luego variocolores).</summary>
    private static IReadOnlyList<FilaTablaP> Formas(ContextoP c, Func<ContextoP, IZukan56> z, int regionNormal, int regionVariocolor)
    {
        var l = new List<FilaTablaP>();
        for (ushort sp = 1; sp <= c.Sav.MaxSpeciesID && sp <= z(c).MaxEntrada; sp++)
        {
            var (indice, cuenta) = z(c).Formas(sp);
            if (cuenta == 0) continue;
            var nombres = FormConverter.GetFormList(sp, c.Textos.Types, c.Textos.forms, GameInfo.GenderSymbolUnicode, c.Sav.Context);
            if (nombres.Length < 1) continue;
            for (var i = 0; i < nombres.Length; i++)
            {
                var s = sp; var k = i; var ind = indice;
                var nombre = nombres[i].Length == 0 ? IdiomaInterfaz.T("Pokedex56.FormaBase") : nombres[i];
                l.Add(new FilaTablaP { Id = s * 256 + k, Nombre = $"{Nombre(c, s)} · {nombre}", EsMarca = true, Leer = x => z(x).GetFormFlag(ind + k, regionNormal), Escribir = (x, v) => z(x).SetFormFlag(ind + k, regionNormal, (bool)v) });
            }
            for (var i = 0; i < nombres.Length; i++)
            {
                var s = sp; var k = i; var ind = indice;
                var nombre = nombres[i].Length == 0 ? IdiomaInterfaz.T("Pokedex56.FormaBase") : nombres[i];
                l.Add(new FilaTablaP { Id = s * 256 + nombres.Length + k, Nombre = $"{Nombre(c, s)} · * {nombre}", EsMarca = true, Leer = x => z(x).GetFormFlag(ind + k, regionVariocolor), Escribir = (x, v) => z(x).SetFormFlag(ind + k, regionVariocolor, (bool)v) });
            }
        }
        return l;
    }

    private static void Lote(ContextoP c, Func<ContextoP, IZukan56> z, int v)
    {
        var d = z(c); var lang = (LanguageID)c.Sav.Language;
        switch (v)
        {
            case 1: d.SeenAll(false); d.CaughtAll(lang, false); break;
            case 2: d.SeenAll(false); break;
            case 3: d.SeenAll(true); break;
            case 4: d.SeenNone(); break;
            case 5: d.CaughtAll(lang, false); break;
            case 6: d.CaughtAll(lang, true); break;
            case 7: d.CaughtNone(); break;
        }
    }

    private static void LoteFormas(ContextoP c, Func<ContextoP, IZukan56> z, int v)
    {
        var d = z(c);
        switch (v)
        {
            case 1: d.FormasNinguna(); break;
            case 2: d.FormasPrimera(false); break;
            case 3: d.FormasTodas(false); break;
            case 4: d.FormasTodas(true); break;
        }
    }
}
