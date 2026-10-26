// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using static PKHeX.Core.PokedexResearchTaskType8a;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Catalogos;

/// <summary>
/// Pokédex de Leyendas Pokémon: Arceus (<c>SAV_PokedexLA</c> y el editor de tareas de investigación): por especie de la Pokédex de Hisui, lo visto, obtenido y capturado por forma y género, la vía solitaria,
/// lo mostrado y el progreso de cada tarea de investigación, con las llamadas del Core del formulario.
/// </summary>
internal static class Pokedex8a
{
    public static readonly string[] Censo = ["PK-SAV-162", "PK-SAV-163", "PK-SAV-164", "PK-SAV-165", "PK-SAV-166", "PK-SAV-167", "PK-SAV-149"];
    private static string[] Gen => [IdiomaInterfaz.T("Form.Trainer8.Male"), IdiomaInterfaz.T("Form.Trainer8.Female"), IdiomaInterfaz.T("Pokedex8a.MachoAlfa"), IdiomaInterfaz.T("Pokedex8a.HembraAlfa"), IdiomaInterfaz.T("Pokedex56.MachoVariocolor"), IdiomaInterfaz.T("Pokedex56.HembraVariocolor"), IdiomaInterfaz.T("Pokedex8a.MachoAlfaVariocolor"), IdiomaInterfaz.T("Pokedex8a.HembraAlfaVariocolor")];
    private static string[] Grupos => [IdiomaInterfaz.T("Pokedex8a.VistoSalvaje"), IdiomaInterfaz.T("Pokedex8a.Obtenido"), IdiomaInterfaz.T("Pokedex8a.CapturadoSalvaje")];

    private static PokedexSave8a D(ContextoP c) => ((SAV8LA)c.Sav).Blocks.PokedexSave;
    private static string Nombre(ContextoP c, ushort sp) => sp < c.Textos.Species.Count ? c.Textos.Species[sp] : IdiomaInterfaz.F("DescripcionRegalo.Especie", sp);

    /// <summary>Las especies de la Pokédex de Hisui, por orden de numero de Pokédex.</summary>
    private static IReadOnlyList<(ushort Especie, int Indice)> Especies(ContextoP c)
    {
        var l = new List<(ushort, int)>();
        for (ushort s = 1; s <= c.Sav.MaxSpeciesID; s++)
        {
            var d = PokedexSave8a.GetDexIndex(PokedexType8a.Hisui, s);
            if (d != 0) l.Add((s, d));
        }
        return [.. l.OrderBy(x => x.Item2)];
    }

    private static string[] Formas(ContextoP c, ushort species)
    {
        var f = FormConverter.GetFormList(species, c.Textos.Types, c.Textos.forms, GameInfo.GenderSymbolASCII, EntityContext.Gen8a);
        return f;
    }

    private static IReadOnlyList<(byte Forma, string Nombre)> FormasConDatos(ContextoP c, ushort sp)
    {
        var d = D(c); var l = new List<(byte, string)>();
        var count = c.Sav.Personal[sp].FormCount;
        var nombres = Formas(c, sp);
        var tipo0 = c.Textos.Types[0];
        for (byte f = 0; f < Math.Max((byte)1, (byte)count); f++)
        {
            if (!d.HasFormStorage(sp, f) || d.IsBlacklisted(sp, f)) continue;
            var n = f < nombres.Length ? nombres[f] : string.Empty;
            l.Add((f, n.Length == 0 ? (nombres.Length > 1 ? tipo0 : string.Empty) : n));
        }
        return l;
    }

    /// <summary>Accion masiva que busca bloques que algunas partidas no tienen (una partida en blanco): falla con un mensaje claro y la partida queda como estaba.</summary>
    private static void Seguro(Action accion)
    {
        try { accion(); }
        catch (Exception ex) when (ex is KeyNotFoundException or ArgumentOutOfRangeException or IndexOutOfRangeException)
        { throw new InvalidOperationException(IdiomaInterfaz.T("Partida.SinDatosAccion"), ex); }
    }

    private static string[] Texto(string nombre) => Util.GetStringList(nombre, Pokekeep.Mobile.Ui.Ajustes.Pkhex.ProgramaPkhex.Idioma);

    /// <summary>La descripcion de una tarea en castellano; las de movimiento y de tipo usan los nombres de la partida, no los del idioma global del Core.</summary>
    private static string Etiqueta(ContextoP c, PokedexResearchTask8a t, string[] tareas, string[] horas, string[] especies) => t.Task switch
    {
        UseMove => string.Format(tareas[(int)t.Task], t.Move < c.Textos.Move.Count ? c.Textos.Move[t.Move] : IdiomaInterfaz.F("Pokedex8a.Mov", t.Move)),
        DefeatWithMoveType => string.Format(tareas[(int)t.Task], (int)t.Type < c.Textos.Types.Count ? c.Textos.Types[(int)t.Type] : IdiomaInterfaz.F("Pokedex8a.Tipo", (int)t.Type)),
        _ => t.GetTaskLabelString(tareas, horas, especies),
    };

    private static FilaTablaP Marca(int id, string nombre, Func<ContextoP, bool> leer, Action<ContextoP, bool> escribir) => new() { Id = id, Nombre = nombre, EsMarca = true, Leer = x => leer(x), Escribir = (x, v) => escribir(x, (bool)v) };

    private static byte LeerFlags(PokedexSave8a d, int grupo, ushort sp, byte f) => grupo switch { 0 => d.GetPokeSeenInWildFlags(sp, f), 1 => d.GetPokeObtainFlags(sp, f), _ => d.GetPokeCaughtInWildFlags(sp, f) };
    private static void PonerFlags(PokedexSave8a d, int grupo, ushort sp, byte f, byte v) { switch (grupo) { case 0: d.SetPokeSeenInWildFlags(sp, f, v); break; case 1: d.SetPokeObtainFlags(sp, f, v); break; default: d.SetPokeCaughtInWildFlags(sp, f, v); break; } }

    public static EditorDefP Crear(SAV8LA sav)
    {
        var tareasTxt = Texto("tasks8a"); var horasTxt = Texto("time_tasks8a"); var especiesTxt = Texto("species_tasks8a");

        var banderas = new TablaDefP
        {
            Id = "banderas", SinNumero = true, Marcador = IdiomaInterfaz.T("Pokedex56.EspecieForma"), Ayuda = IdiomaInterfaz.T("Pokedex8a.VistoLibertadObtenidoCapturado"),
            Filas = c =>
            {
                var l = new List<FilaTablaP>();
                foreach (var (sp, _) in Especies(c))
                    foreach (var (f, nf) in FormasConDatos(c, sp))
                        for (var g = 0; g < 3; g++)
                            for (var b = 0; b < 8; b++)
                            {
                                var s = sp; var form = f; var gg = g; var bit = b;
                                l.Add(Marca(((s * 128 + form) * 3 + gg) * 8 + bit, $"{Nombre(c, s)}{(nf.Length > 0 ? " (" + nf + ")" : "")} · {Grupos[gg]} · {Gen[bit]}",
                                    x => (LeerFlags(D(x), gg, s, form) & (1 << bit)) != 0,
                                    (x, v) => { var d = D(x); var cur = LeerFlags(d, gg, s, form); var nuevo = (byte)(v ? cur | (1 << bit) : cur & ~(1 << bit)); if (v) d.SetPokeHasBeenUpdated(s); PonerFlags(d, gg, s, form, nuevo); }));
                            }
                return l;
            },
        };
        var marcas = new TablaDefP
        {
            Id = "marcas", SinNumero = true, Marcador = IdiomaInterfaz.T("Libreria.BuscarEspecie"), Ayuda = IdiomaInterfaz.T("Pokedex8a.ViaSolitariaCompletadaMostrado"),
            Filas = c => [.. Especies(c).SelectMany(e => new[]
            {
                Marca(e.Especie * 8, IdiomaInterfaz.F("Pokedex8a.ViaSolitariaCompletada", Nombre(c, e.Especie)), x => D(x).GetSolitudeComplete(e.Especie), (x, v) => D(x).SetSolitudeComplete(e.Especie, v)),
                new FilaTablaP
                {
                    Id = e.Especie * 8 + 1, Nombre = IdiomaInterfaz.F("Pokedex8a.Mostrado", Nombre(c, e.Especie)), Min = 0, Max = 2047, Detalle = IdiomaInterfaz.T("Pokedex8a.FormaHembraVariocolorAlfa"),
                    Leer = x => { var d = D(x); return (long)(d.GetSelectedForm(e.Especie) | (d.GetSelectedGender1(e.Especie) ? 256 : 0) | (d.GetSelectedShiny(e.Especie) ? 512 : 0) | (d.GetSelectedAlpha(e.Especie) ? 1024 : 0)); },
                    Escribir = (x, v) => { var n = (int)(long)v; D(x).SetSelectedGenderForm(e.Especie, (byte)(n & 0xFF), (n & 256) != 0, (n & 512) != 0, (n & 1024) != 0); },
                },
            })],
        };
        var investigacion = new TablaDefP
        {
            Id = "investigacion", SinNumero = true, Marcador = IdiomaInterfaz.T("Pokedex8a.EspecieTarea"), Ayuda = IdiomaInterfaz.T("Pokedex8a.ProgresoActualCadaTarea"),
            Filas = c =>
            {
                var l = new List<FilaTablaP>();
                foreach (var (sp, dex) in Especies(c))
                {
                    var tareas = PokedexConstants8a.ResearchTasks[dex - 1];
                    for (var i = 0; i < tareas.Length; i++)
                    {
                        var s = sp; var k = i; var t = tareas[i];
                        var puede = t.Task.CanSetCurrentValue();
                        l.Add(new FilaTablaP
                        {
                            Id = s * 16 + k, Nombre = $"{Nombre(c, s)} · {Etiqueta(c, t, tareasTxt, horasTxt, especiesTxt)}", Min = 0, Max = PokedexConstants8a.MaxPokedexResearchPoints, SoloLectura = !puede,
                            Detalle = IdiomaInterfaz.F("Pokedex8a.PuntosNivelUmbrales", t.PointsSingle + t.PointsBonus, string.Join(", ", t.TaskThresholds)),
                            Leer = x => { try { D(x).GetResearchTaskLevel(s, k, out _, out var valor, out _); return (long)valor; } catch (Exception ex) when (ex is ArgumentOutOfRangeException or InvalidOperationException or KeyNotFoundException or IndexOutOfRangeException) { return 0L; } },   // partida sin los bloques de «Parte de Arceus» (en blanco): 0
                            Escribir = (x, v) => D(x).SetResearchTaskProgressByForce(s, PokedexConstants8a.ResearchTasks[PokedexSave8a.GetDexIndex(PokedexType8a.Hisui, s) - 1][k], (int)(long)v),
                        });
                    }
                }
                return l;
            },
        };
        return new EditorDefP("pokedex", IdiomaInterfaz.T("Partida.Editor.pokedex.Titulo"),
        [
            new("resumen", IdiomaInterfaz.T("Editor.Resumen"), [new(IdiomaInterfaz.T("Pokedex8a.Investigacion"), [P.Opcion("lote", IdiomaInterfaz.T("Pokedex.SobreTodaPokedex"), c => 0, (c, v) => { if (v == 1) Seguro(() => D(c).UpdateAllReportPoke()); }, _ => P.Lista(IdiomaInterfaz.T("Form.Trainer7.SinCambios"), IdiomaInterfaz.T("Pokedex8a.RegistrarInvestigacionTodasEspecies")), soloAccion: true, ayuda: IdiomaInterfaz.T("Pokedex8a.PasaProgresoRegistrarNiveles"))])]),
            new("banderas", IdiomaInterfaz.T("Pokedex8a.VistosCapturados"), [], Tabla: banderas), new("marcas", IdiomaInterfaz.T("Pokedex8a.Mostrado2"), [], Tabla: marcas), new("investigacion", IdiomaInterfaz.T("Pokedex8a.Investigacion"), [], Tabla: investigacion),
        ], Censo, IdiomaInterfaz.T("Pokedex8a.VistoObtenidoCapturadoForma"));
    }
}
