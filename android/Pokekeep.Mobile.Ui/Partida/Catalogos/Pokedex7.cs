// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Catalogos;

/// <summary>
/// Pokédex de Sol, Luna, Ultra Sol y Ultra Luna (<c>SAV_PokedexSM</c>): una «entrada» por especie y una más por cada forma con bits propios; capturado, visto y mostrado por partición, idiomas y las acciones de lote del formulario
/// (que se reproducen con las mismas llamadas del Core y las mismas reglas de «al menos un visto / un mostrado»).
/// </summary>
internal static class Pokedex7
{
    public static readonly string[] Censo = ["PK-SAV-137"];
    private static string[] Particiones => [IdiomaInterfaz.T("Form.Trainer8.Male"), IdiomaInterfaz.T("Form.Trainer8.Female"), IdiomaInterfaz.T("Pokedex56.MachoVariocolor"), IdiomaInterfaz.T("Pokedex56.HembraVariocolor")];
    private static string[] Idiomas => [IdiomaInterfaz.T("ModeloForzarCarga.Japones"), IdiomaInterfaz.T("ModeloForzarCarga.Ingles"), IdiomaInterfaz.T("ModeloForzarCarga.Frances"), IdiomaInterfaz.T("ModeloForzarCarga.Italiano"), IdiomaInterfaz.T("ModeloForzarCarga.Aleman"), IdiomaInterfaz.T("ModeloForzarCarga.Espanol"), IdiomaInterfaz.T("ModeloForzarCarga.Coreano"), "Chino simplificado", "Chino tradicional"];
    private const string Vis = "vistos", Mos = "mostrados", Cap = "capturados", Idi = "idiomas";
    private static string Clave(string t, int id) => $"{t}:{id}";

    private static Zukan7 Z(ContextoP c) => c.Sav is SAV7b b ? b.Blocks.Zukan : ((SAV7)c.Sav).Zukan;

    /// <summary>Que particiones puede tener una entrada segun el genero de su especie base (hembra sola, macho solo o sin genero).</summary>
    private static (bool Macho, bool Hembra) Generos(Zukan7 z, int index)
    {
        var gt = z.GetBaseSpeciesGenderValue(index);
        return (gt != PersonalInfo.RatioMagicFemale, gt is not (PersonalInfo.RatioMagicMale or PersonalInfo.RatioMagicGenderless));
    }

    private static bool Permitida(Zukan7 z, int index, int r) => (r & 1) == 0 ? Generos(z, index).Macho : Generos(z, index).Hembra;

    public static EditorDefP Crear(SaveFile sav)
    {
        var lgpe = sav is SAV7b;
        IReadOnlyList<string> Nombres(ContextoP c) => Z(c).GetEntryNames(c.Textos.Species).ToList();

        var capturados = new TablaDefP
        {
            Id = Cap, SinNumero = true, Marcador = IdiomaInterfaz.T("Libreria.BuscarEspecie"),
            Filas = c => { var n = Nombres(c); return Enumerable.Range(0, c.Sav.MaxSpeciesID).Select(i => new FilaTablaP { Id = i, Nombre = n[i], EsMarca = true, Leer = x => Z(x).GetCaught((ushort)(i + 1)), Escribir = (x, v) => Z(x).SetCaught((ushort)(i + 1), (bool)v) }).ToList(); },
        };
        var vistos = new TablaDefP
        {
            Id = Vis, SinNumero = true, Marcador = IdiomaInterfaz.T("Pokedex56.EspecieForma"), Ayuda = IdiomaInterfaz.T("Pokedex7.FilaEntradaGeneroVariocolor"),
            Filas = c => Filas4(c, Nombres(c), (x, i, r) => Z(x).GetSeen((ushort)(i + 1), r), (x, i, r, v) => Z(x).SetSeen((ushort)(i + 1), r, v)),
            Coherencia = (c, f, v) =>
            {
                var i = f.Id / 8; var r = f.Id % 8;
                var otras = new List<(string, object)>();
                bool Visto(int k) => c.Borrador.TryGetValue(Clave(Vis, i * 8 + k), out var o) && o is true;
                bool Mostrado(int k) => c.Borrador.TryGetValue(Clave(Mos, i * 8 + k), out var o) && o is true;
                if (!Enumerable.Range(0, 4).Any(Visto)) { for (var k = 0; k < 4; k++) if (Mostrado(k)) otras.Add((Clave(Mos, i * 8 + k), false)); }
                else if (!Enumerable.Range(0, 4).Any(Mostrado) && (bool)v) otras.Add((Clave(Mos, i * 8 + r), true));
                return otras;
            },
        };
        var mostrados = new TablaDefP
        {
            Id = Mos, SinNumero = true, Marcador = IdiomaInterfaz.T("Pokedex56.EspecieForma"), Ayuda = IdiomaInterfaz.T("Pokedex7.SoloParticionPuedeEstar"),
            Filas = c => Filas4(c, Nombres(c), (x, i, r) => Z(x).GetDisplayed(i, r), (x, i, r, v) => Z(x).SetDisplayed(i, r, v)),
            Coherencia = (c, f, v) =>
            {
                var i = f.Id / 8; var r = f.Id % 8;
                if (!(bool)v) return [];
                var otras = new List<(string, object)>();
                for (var k = 0; k < 4; k++) if (k != r) otras.Add((Clave(Mos, i * 8 + k), false));
                otras.Add((Clave(Vis, i * 8 + r), true));
                return otras;
            },
        };
        var idiomas = new TablaDefP
        {
            Id = Idi, SinNumero = true, Marcador = IdiomaInterfaz.T("Libreria.BuscarEspecie"), Ayuda = IdiomaInterfaz.T("Pokedex56.IdiomasVistoCadaEspecie"),
            Filas = c =>
            {
                var n = Nombres(c); var l = new List<FilaTablaP>();
                for (var i = 0; i < c.Sav.MaxSpeciesID; i++)
                    for (var k = 0; k < Idiomas.Length; k++) { var s = i; var j = k; l.Add(new FilaTablaP { Id = s * 16 + j, Nombre = $"{n[s]} · {Idiomas[j]}", EsMarca = true, Leer = x => Z(x).GetLanguageFlag(s, j), Escribir = (x, v) => Z(x).SetLanguageFlag(s, j, (bool)v) }); }
                return l;
            },
        };
        return new EditorDefP("pokedex", IdiomaInterfaz.T("Partida.Editor.pokedex.Titulo"),
        [
            new("resumen", IdiomaInterfaz.T("Editor.Resumen"), [new(IdiomaInterfaz.T("Pokedex.AccionesLote"), [P.Opcion("lote", IdiomaInterfaz.T("Pokedex.SobreTodaPokedex"), c => 0, (c, v) => Lote(c, (int)v),
                _ => P.Lista(IdiomaInterfaz.T("Form.Trainer7.SinCambios"), IdiomaInterfaz.T("Pokedex7.CompletarVistosCapturadosE"), IdiomaInterfaz.T("Pokedex.TodosVistos2"), IdiomaInterfaz.T("Pokedex.TodosCapturados2"), IdiomaInterfaz.T("Pokedex56.NingunoVisto"), IdiomaInterfaz.T("Pokedex.NingunoCapturado")), soloAccion: true, ayuda: IdiomaInterfaz.T("Pokedex7.AplicaTodasEspeciesFormas"))])]),
            new("capturados", IdiomaInterfaz.T("Pokedex.Capturados"), [], Tabla: capturados),
            new("vistos", IdiomaInterfaz.T("Pokedex.Vistos"), [], Tabla: vistos),
            new("mostrados", IdiomaInterfaz.T("Pokedex56.Mostrados"), [], Tabla: mostrados),
            new("idiomas", IdiomaInterfaz.T("Pokedex.Idiomas"), [], Tabla: idiomas),
            .. (lgpe ? new[] { new PestanaP("tamanos", IdiomaInterfaz.T("Pokedex7.RecordsTamano"), [], Tabla: Tamanos()) } : []),
        ], lgpe ? ["PK-SAV-136"] : Censo, IdiomaInterfaz.T("Pokedex7.PokemonVistosCapturadosMostrados"));
    }

    /// <summary>Récords de tamaño de Let's Go (<c>SAV_PokedexGG</c>): por especie y forma, altura y peso mínimos y máximos; «sin dato» es altura 254 y peso 127.</summary>
    private static TablaDefP Tamanos()
    {
        string[] tipos = [IdiomaInterfaz.T("Pokedex7.AlturaMinima"), IdiomaInterfaz.T("Pokedex7.AlturaMaxima"), IdiomaInterfaz.T("Pokedex7.PesoMinimo"), IdiomaInterfaz.T("Pokedex7.PesoMaximo")];
        DexSizeType[] grupos = [DexSizeType.MinHeight, DexSizeType.MaxHeight, DexSizeType.MinWeight, DexSizeType.MaxWeight];
        return new TablaDefP
        {
            Id = "tamanos", SinNumero = true, Marcador = IdiomaInterfaz.T("Libreria.BuscarEspecie"), Ayuda = IdiomaInterfaz.T("Pokedex7.DatoAlturaPesoMarcado"),
            Filas = c =>
            {
                var l = new List<FilaTablaP>(); var z = (Zukan7b)Z(c); var vistos = new HashSet<int>();
                for (ushort sp = 1; sp <= c.Sav.MaxSpeciesID; sp++)
                {
                    var forms = c.Sav.Personal[sp].FormCount;
                    var nombres = FormConverter.GetFormList(sp, c.Textos.Types, c.Textos.forms, GameInfo.GenderSymbolASCII, c.Sav.Context);
                    for (byte f = 0; f < Math.Max((byte)1, (byte)forms); f++)
                    {
                        if (!Zukan7b.TryGetSizeEntryIndex(sp, f, out var index) || !vistos.Add(index)) continue;
                        var nombre = f == 0 || f >= nombres.Length || nombres[f].Length == 0 ? c.Textos.Species[sp] : $"{c.Textos.Species[sp]} ({nombres[f]})";
                        for (var t = 0; t < 4; t++)
                        {
                            var g = grupos[t]; var idx = index; var tt = t;
                            l.Add(new FilaTablaP { Id = (idx * 4 + tt) * 4, Nombre = IdiomaInterfaz.F("Pokedex7.Altura", nombre, tipos[tt]), Min = 0, Max = 255, Leer = x => { ((Zukan7b)Z(x)).GetSizeData(g, idx, out var h, out _, out _); return (long)h; }, Escribir = (x, v) => { var zz = (Zukan7b)Z(x); zz.GetSizeData(g, idx, out _, out var w, out var fl); zz.SetSizeData(g, idx, (byte)(long)v, w, fl); } });
                            l.Add(new FilaTablaP { Id = (idx * 4 + tt) * 4 + 1, Nombre = IdiomaInterfaz.F("Pokedex7.Peso", nombre, tipos[tt]), Min = 0, Max = 255, Leer = x => { ((Zukan7b)Z(x)).GetSizeData(g, idx, out _, out var w, out _); return (long)w; }, Escribir = (x, v) => { var zz = (Zukan7b)Z(x); zz.GetSizeData(g, idx, out var h, out _, out var fl); zz.SetSizeData(g, idx, h, (byte)(long)v, fl); } });
                            l.Add(new FilaTablaP { Id = (idx * 4 + tt) * 4 + 2, Nombre = IdiomaInterfaz.F("Pokedex7.Marcado", nombre, tipos[tt]), EsMarca = true, Leer = x => { ((Zukan7b)Z(x)).GetSizeData(g, idx, out _, out _, out var fl); return fl; }, Escribir = (x, v) => { var zz = (Zukan7b)Z(x); zz.GetSizeData(g, idx, out var h, out var w, out _); zz.SetSizeData(g, idx, h, w, (bool)v); } });
                        }
                    }
                }
                return l;
            },
        };
    }

    private static IReadOnlyList<FilaTablaP> Filas4(ContextoP c, IReadOnlyList<string> nombres, Func<ContextoP, int, int, bool> leer, Action<ContextoP, int, int, bool> escribir)
    {
        var l = new List<FilaTablaP>(); var z = Z(c);
        for (var i = 0; i < nombres.Count; i++)
            for (var r = 0; r < 4; r++)
            {
                if (!Permitida(z, i, r)) continue;
                var s = i; var k = r;
                l.Add(new FilaTablaP { Id = s * 8 + k, Nombre = $"{nombres[s]} · {Particiones[k]}", EsMarca = true, Leer = x => leer(x, s, k), Escribir = (x, v) => escribir(x, s, k, (bool)v) });
            }
        return l;
    }

    // ---- Acciones de lote: las reglas de ModifyAll / SetAll / ClearAll / SetSeen / SetCaught del formulario ----
    private sealed class Entrada
    {
        public int Indice; public ushort Especie; public bool EsEspecie;
        public bool Capturado; public bool[] Visto = new bool[4], Mostrado = new bool[4], Idioma = new bool[9];
        public bool Macho, Hembra; public byte Gt;
    }

    private static Entrada Cargar(Zukan7 z, int max, int index)
    {
        var e = new Entrada { Indice = index, Especie = (ushort)(index + 1) };
        e.EsEspecie = e.Especie <= max;
        e.Capturado = z.GetCaught(e.Especie);
        e.Gt = z.GetBaseSpeciesGenderValue(index);
        (e.Macho, e.Hembra) = Generos(z, index);
        for (var i = 0; i < 4; i++) { e.Visto[i] = z.GetSeen(e.Especie, i); e.Mostrado[i] = z.GetDisplayed(index, i); }
        for (var i = 0; i < 9; i++) e.Idioma[i] = e.EsEspecie && z.GetLanguageFlag(index, i);
        return e;
    }

    private static void Guardar(Zukan7 z, Entrada e)
    {
        for (var i = 0; i < 4; i++) z.SetSeen(e.Especie, i, e.Visto[i]);
        for (var i = 0; i < 4; i++) z.SetDisplayed(e.Indice, i, e.Mostrado[i]);
        if (!e.EsEspecie) return;
        z.SetCaught(e.Especie, e.Capturado);
        for (var i = 0; i < 9; i++) z.SetLanguageFlag(e.Indice, i, e.Idioma[i]);
    }

    private static bool PuedeParticion(Entrada e, int i) => (i & 1) == 0 ? e.Macho : e.Hembra;
    private static void MostrarUno(Entrada e, byte gt) { if (gt != PersonalInfo.RatioMagicFemale) e.Mostrado[0] = true; else e.Mostrado[1] = true; }

    private static void Lote(ContextoP c, int v)
    {
        if (v is < 1 or > 5) return;
        var z = Z(c); var max = c.Sav.MaxSpeciesID;
        var total = z.GetEntryNames(c.Textos.Species).Count;
        int lang = c.Sav.Language; if (lang > 5) lang--; lang--;
        // 1 completar, 2 todos vistos, 3 todos capturados, 4 ninguno visto, 5 ninguno capturado
        var completar = v == 1; var vistosTodos = v == 2; var capturadosTodos = v == 3; var vistoNinguno = v == 4; var capturadoNinguno = v == 5;
        if (vistoNinguno || capturadoNinguno)
        {
            for (var i = 0; i < total; i++)
            {
                var e = Cargar(z, max, i);
                for (var k = 0; k < 9; k++) e.Idioma[k] = false;
                e.Capturado = false;
                if (!capturadoNinguno) { for (var k = 0; k < 4; k++) { e.Visto[k] = false; e.Mostrado[k] = false; } }
                Guardar(z, e);
                if (vistoNinguno && c.Sav is SAV7b && i < max) RegistrosVacios((Zukan7b)z, e.Especie);
            }
            return;
        }
        var legales = c.Sav is SAV7b ? Enumerable.Range(1, 151).Select(i => (ushort)i).Concat(new ushort[] { 808, 809 }).ToList() : Enumerable.Range(1, max).Select(i => (ushort)i).ToList();
        foreach (var sp in legales)
        {
            var idx = sp - 1;
            var b = Cargar(z, max, idx);
            Visto(b, vistoNinguno, false, b.Gt);
            if (!vistosTodos) Capturado(b, b.Gt, lang, false, completar, capturadoNinguno);
            Guardar(z, b);
            if (c.Sav is SAV7b && !vistosTodos) RegistrosRellenar((Zukan7b)z, sp);
            if (c.Sav is SAV7b && sp is (int)Species.Pikachu or (int)Species.Eevee) continue;   // el formulario no toca las formas de los iniciales
            foreach (var f in z.GetAllFormEntries(sp).Where(q => q >= max).Distinct())
            {
                var e = Cargar(z, max, f);
                Visto(e, vistoNinguno, true, b.Gt);
                if (!vistosTodos) Capturado(e, b.Gt, lang, true, completar, capturadoNinguno);
                Guardar(z, e);
            }
        }
    }

    // Let's Go: registros de tamano de la forma 0 (el formulario usa la forma elegida en su lista; aqui siempre la base).
    private static readonly DexSizeType[] Tipos = [DexSizeType.MinHeight, DexSizeType.MaxHeight, DexSizeType.MinWeight, DexSizeType.MaxWeight];

    private static void RegistrosVacios(Zukan7b z, ushort sp)
    {
        if (!Zukan7b.TryGetSizeEntryIndex(sp, 0, out var idx)) return;
        foreach (var t in Tipos) z.SetSizeData(t, idx, Zukan7b.DefaultEntryValueH, Zukan7b.DefaultEntryValueW, false);
    }

    private static void RegistrosRellenar(Zukan7b z, ushort sp)
    {
        if (!Zukan7b.TryGetSizeEntryIndex(sp, 0, out var idx)) return;
        for (var i = 0; i < Tipos.Length; i++)
        {
            if (z.GetSizeData(Tipos[i], idx, out _, out _, out _)) continue;   // ya tiene dato
            var v = (byte)(i % 2 == 0 ? 0 : 255);
            z.SetSizeData(Tipos[i], idx, v, v, false);
        }
    }

    private static void Visto(Entrada e, bool ninguno, bool esForma, byte gt)
    {
        var estabaCapturado = e.Capturado;
        for (var i = 0; i < 4; i++) e.Visto[i] = !ninguno && PuedeParticion(e, i);
        if (!ninguno)
        {
            if (esForma) return;   // como el formulario: una forma no repasa lo mostrado ni los idiomas
            if (!e.Mostrado.Any(x => x)) MostrarUno(e, gt);
        }
        SinCapturado(e, estabaCapturado);
    }

    private static void SinCapturado(Entrada e, bool capturado) { if (!capturado) for (var i = 0; i < 9; i++) e.Idioma[i] = false; }

    private static void Capturado(Entrada e, byte gt, int lang, bool esForma, bool completar, bool ninguno)
    {
        e.Capturado = !ninguno;
        for (var j = 0; j < 9; j++) e.Idioma[j] = e.EsEspecie && (completar || (!ninguno && j == lang));
        if (completar)
            for (var i = 0; i < 4; i++) e.Visto[i] = PuedeParticion(e, i);
        else if (!e.Visto.Any(x => x))
        {
            if (gt != PersonalInfo.RatioMagicFemale) e.Visto[0] = true; else e.Visto[1] = true;
        }
        if (esForma) return;
        if (!e.Mostrado.Any(x => x))
        {
            // El formulario pone (e.EsEspecie) en la casilla elegida: para una especie es cierto.
            if (gt != PersonalInfo.RatioMagicFemale) e.Mostrado[0] = e.EsEspecie; else e.Mostrado[1] = e.EsEspecie;
        }
    }
}
