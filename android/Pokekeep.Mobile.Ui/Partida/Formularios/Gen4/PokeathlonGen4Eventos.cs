// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen4;

/// <summary>
/// Las pestañas de <see cref="PokeathlonGen4"/> con listas de elementos: Cursos (cinco, con sus tres participantes), Evento propio y Conexión (diez pruebas cada una, con cinco récords de equipos de tres Pokémon y, en la conexión,
/// cinco entrenadores). Los controles incrustados de WinForms son aquí las secciones de cada elemento: el participante (<c>PokeathlonParticipant4Editor</c>), el selector de especie y forma (<c>PokeathlonSpeciesForm4Editor</c>), el récord
/// (<c>PokeathlonEventRecord4Editor</c>), el entrenador (<c>PokeathlonEventTrainer4Editor</c>), los datos del evento (<c>PokeathlonEventData4Editor</c>) y la conexión (<c>PokeathlonConnection4Editor</c>).
/// </summary>
internal static partial class PokeathlonGen4
{
    // Estática, pero por idioma de la interfaz: antes se fijaba al primero y seguía en él tras cambiarlo (auditoría uso10, A7).
    private static (string Idioma, IReadOnlyList<OpcionP> Lista)? _generos;
    private static IReadOnlyList<OpcionP> Generos
    {
        get
        {
            var idioma = IdiomaInterfaz.Actual.Codigo;
            if (_generos is { } g && g.Idioma == idioma) return g.Lista;
            var lista = P.Lista(IdiomaInterfaz.T("PokeathlonGen4Eventos.Macho"), IdiomaInterfaz.T("PokeathlonGen4Eventos.Hembra"), IdiomaInterfaz.T("GoPark.Genero"));
            _generos = (idioma, lista);
            return lista;
        }
    }

    private static IReadOnlyList<OpcionP> Formas(ContextoP c, long especie)
        => P.Lista(FormConverter.GetFormList((ushort)especie, c.Textos.types, c.Textos.forms, GameInfo.GenderSymbolUnicode, EntityContext.Gen4));

    /// <summary>Especie y forma: dos listas, la de formas depende de la especie y vuelve a la primera forma al cambiarla (<c>LoadForms(0)</c>).</summary>
    private static IEnumerable<CampoP> EspecieYForma(string idEspecie, string idForma, string etiqueta, Func<ContextoP, long> leerEspecie, Action<ContextoP, long> escribirEspecie, Func<ContextoP, long> leerForma, Action<ContextoP, long> escribirForma, int prioridad)
    {
        yield return P.Opcion(idEspecie, $"{etiqueta} · {L("DGV_Species")}", leerEspecie, escribirEspecie, Gen1a3.AyudasGen3.Especies, titulo: $"{etiqueta} · {L("DGV_Species")}", prioridad: prioridad, reinicia: [idForma]);
        yield return P.Opcion(idForma, $"{etiqueta} · {L("L_Form")}", leerForma, escribirForma, c => Formas(c, c.Entero(idEspecie)), titulo: $"{etiqueta} · {L("L_Form")}", prioridad: prioridad + 1);
    }

    // ---------------------------------------------------------------- Cursos (Tab_Courses)
    private static PestanaP Cursos()
    {
        ColeccionDefP? col = null;
        col = new ColeccionDefP
        {
            Id = "cursos", Elemento = L("L_CourseIndex").TrimEnd(':'), Cantidad = _ => (int)PokeathlonStat4.Count, Titulo = (_, i) => NombreDeCurso(i),
            Resumen = (c, i) => $"{L("L_CourseScoreMax").TrimEnd(':')}: {At(c).GetCourseRecord((PokeathlonStat4)i).ScoreMax}",
            Secciones = (_, i) =>
            {
                PokeathlonCourseRecord4 Curso(ContextoP c) => At(c).GetCourseRecord((PokeathlonStat4)i);
                var puntos = new List<CampoP>
                {
                    P.Entero(col!.IdCampo(i, "p0"), L("L_CourseScore0").TrimEnd(':'), c => Curso(c).Score0, (c, v) => { var r = Curso(c); r.Score0 = (ushort)v; }, 0, ushort.MaxValue, prioridad: 0),
                    P.Entero(col.IdCampo(i, "p1"), L("L_CourseScore1").TrimEnd(':'), c => Curso(c).Score1, (c, v) => { var r = Curso(c); r.Score1 = (ushort)v; }, 0, ushort.MaxValue, prioridad: 1),
                    P.Entero(col.IdCampo(i, "p2"), L("L_CourseScore2").TrimEnd(':'), c => Curso(c).Score2, (c, v) => { var r = Curso(c); r.Score2 = (ushort)v; }, 0, ushort.MaxValue, prioridad: 2),
                    P.Entero(col.IdCampo(i, "pmax"), L("L_CourseScoreMax").TrimEnd(':'), c => Curso(c).ScoreMax, (c, v) => { var r = Curso(c); r.ScoreMax = (ushort)v; }, 0, ushort.MaxValue, prioridad: 3),
                };
                var secciones = new List<SeccionP> { new(string.Empty, puntos) };
                for (var k = 0; k < PokeathlonCourseRecord4.CountParticipant; k++)
                {
                    var participante = k;
                    secciones.Add(new SeccionP(L($"L_CourseParticipant{k}").TrimEnd(':'), [.. Participante(col, i, participante, c => Curso(c).GetParticipant(participante), 10 + (k * 10))]));
                }
                return secciones;
            },
        };
        return new PestanaP("cursos", L("Tab_Courses"), [], Coleccion: col);
    }

    /// <summary><c>PokeathlonParticipant4Editor</c>: especie y forma, género (0-2), variocolor, PID (hexadecimal de 8 cifras) y TID/SID de 16 bits. <c>SaveObject</c> los escribe todos; cada campo escribe el suyo.</summary>
    private static IEnumerable<CampoP> Participante(ColeccionDefP col, int i, int k, Func<ContextoP, PokeathlonParticipant4> de, int prioridad)
    {
        string Id(string campo) => col.IdCampo(i, $"part{k}.{campo}");
        foreach (var c in EspecieYForma(Id("especie"), Id("forma"), IdiomaInterfaz.T("Nav.Pokemon"),
            c => de(c).Species, (c, v) => { var p = de(c); p.Species = (ushort)v; }, c => de(c).Form, (c, v) => { var p = de(c); p.Form = (byte)v; }, prioridad)) yield return c;
        yield return P.Opcion(Id("genero"), L("L_Gender"), c => Math.Min((byte)2, de(c).Gender), (c, v) => { var p = de(c); p.Gender = (byte)v; }, _ => Generos, titulo: L("L_Gender"), prioridad: prioridad + 2);
        yield return P.Marca(Id("variocolor"), L("CHK_IsShiny"), c => de(c).IsShiny, (c, v) => { var p = de(c); p.IsShiny = v; }, prioridad: prioridad + 3);
        yield return P.Entero(Id("pid"), L("L_PID").TrimEnd(':'), c => de(c).EncryptionConstant, (c, v) => { var p = de(c); p.EncryptionConstant = (uint)v; }, 0, uint.MaxValue, hex: true, prioridad: prioridad + 4, ayuda: IdiomaInterfaz.T("Form.AyudaHex8"));
        yield return P.Entero(Id("tid"), L("L_TID16").TrimEnd(':'), c => de(c).TID16, (c, v) => { var p = de(c); p.TID16 = (ushort)v; }, 0, ushort.MaxValue, prioridad: prioridad + 5);
        yield return P.Entero(Id("sid"), L("L_SID16").TrimEnd(':'), c => de(c).SID16, (c, v) => { var p = de(c); p.SID16 = (ushort)v; }, 0, ushort.MaxValue, prioridad: prioridad + 6);
    }

    // ---------------------------------------------------------------- Récords de un evento (PokeathlonEventRecord4Editor)
    /// <summary>El récord de una prueba (u16) y el equipo de tres Pokémon (<c>SpeciesForm10</c>: especie de 10 bits y forma de 6) que lo logró.</summary>
    private static IEnumerable<CampoP> Registro(ColeccionDefP col, int i, string prefijo, string titulo, Func<ContextoP, PokeathlonEventRecord4> de, int prioridad)
    {
        string Id(string campo) => col.IdCampo(i, $"{prefijo}.{campo}");
        yield return P.Entero(Id("record"), $"{titulo} · {L("L_Record").TrimEnd(':')}", c => de(c).Record, (c, v) => { var r = de(c); r.Record = (ushort)v; }, 0, ushort.MaxValue, prioridad: prioridad);
        for (var e = 0; e < 3; e++)
        {
            var entrada = e;
            SpeciesForm10 Leer(ContextoP c) { var r = de(c); return entrada switch { 0 => r.Entry0, 1 => r.Entry1, _ => r.Entry2 }; }
            void Poner(ContextoP c, Func<SpeciesForm10, SpeciesForm10> cambia) { var r = de(c); switch (entrada) { case 0: r.Entry0 = cambia(r.Entry0); break; case 1: r.Entry1 = cambia(r.Entry1); break; default: r.Entry2 = cambia(r.Entry2); break; } }
            foreach (var c in EspecieYForma(Id($"e{e}.especie"), Id($"e{e}.forma"), $"{titulo} · {e + 1}", c => Leer(c).Species, (c, v) => Poner(c, x => { x.Species = (ushort)v; return x; }),
                c => Leer(c).Form, (c, v) => Poner(c, x => { x.Form = (byte)v; return x; }), prioridad + 1 + (e * 2))) yield return c;
        }
    }

    // ---------------------------------------------------------------- Evento propio (Tab_SelfEvent)
    private static PestanaP EventoPropio()
    {
        ColeccionDefP? col = null;
        col = new ColeccionDefP
        {
            Id = "eventos", Elemento = L("L_SelfEventIndex").TrimEnd(':'), Cantidad = _ => (int)PokeathlonEvent4.Count, Titulo = (_, i) => NombreDePrueba(i),
            Resumen = (c, i) => $"{L("L_Attempts").TrimEnd(':')} {At(c).GetEventSelf((PokeathlonEvent4)i).Attempts}",
            Secciones = (_, i) =>
            {
                PokeathlonEventData4 Datos(ContextoP c) => At(c).GetEventSelf((PokeathlonEvent4)i);
                var secciones = new List<SeccionP>
                {
                    new(string.Empty, [P.Entero(col!.IdCampo(i, "intentos"), L("L_Attempts").TrimEnd(':'), c => Math.Min(PokeathlonEventData4.MaxAttempts, Datos(c).Attempts), (c, v) => { var d = Datos(c); d.Attempts = (uint)v; }, 0, PokeathlonEventData4.MaxAttempts, prioridad: 0)]),
                };
                for (var r = 0; r < (int)PokeathlonEventData4.MaxRecord; r++)
                {
                    var registro = r;
                    secciones.Add(new SeccionP($"{L("L_Record").TrimEnd(':')} {r + 1}", [.. Registro(col, i, $"r{r}", $"{L("L_Record").TrimEnd(':')} {r + 1}", c => Datos(c).GetRecord(registro), 10 + (r * 10))]));
                }
                return secciones;
            },
        };
        return new PestanaP("evento_propio", L("Tab_SelfEvent"), [], Coleccion: col);
    }

    // ---------------------------------------------------------------- Conexión (Tab_Connection)
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ContextoP, Tuple<GameStrings, IReadOnlyList<OpcionP>>> Idiomas = new();

    private static IReadOnlyList<OpcionP> ListaDeIdiomas(ContextoP c)
    {
        // auditoría uso10 (A7): con el cambio de idioma en vivo la lista se rehace si cambian los textos del contexto.
        if (Idiomas.TryGetValue(c, out var e) && ReferenceEquals(e.Item1, c.Textos)) return e.Item2;
        IReadOnlyList<OpcionP> lista = [new OpcionP(0, c.Textos.specieslist[0]), .. P.Lista(GameInfo.LanguageDataSource(4, EntityContext.Gen4))];
        Idiomas.AddOrUpdate(c, Tuple.Create(c.Textos, lista));
        return lista;
    }

    private static PestanaP Conexion()
    {
        ColeccionDefP? col = null;
        col = new ColeccionDefP
        {
            Id = "conexiones", Elemento = L("L_ConnectionIndex").TrimEnd(':'), Cantidad = _ => (int)PokeathlonEvent4.Count, Titulo = (_, i) => NombreDePrueba(i),
            Resumen = (c, i) => $"{L("L_Attempts").TrimEnd(':')} {At(c).GetEventConnection((PokeathlonEvent4)i).Inner.Attempts}",
            Secciones = (_, i) =>
            {
                PokeathlonConnection4 Conexion(ContextoP c) => At(c).GetEventConnection((PokeathlonEvent4)i);
                var secciones = new List<SeccionP>
                {
                    new(string.Empty, [P.Entero(col!.IdCampo(i, "intentos"), L("L_Attempts").TrimEnd(':'), c => Math.Min(PokeathlonEventData4.MaxAttempts, Conexion(c).Inner.Attempts), (c, v) => { var d = Conexion(c).Inner; d.Attempts = (uint)v; }, 0, PokeathlonEventData4.MaxAttempts, prioridad: 0)]),
                };
                for (var r = 0; r < (int)PokeathlonConnection4.MaxTrainer; r++)
                {
                    var registro = r;
                    secciones.Add(new SeccionP($"{L("L_Record").TrimEnd(':')} {r + 1} · {L($"L_Trainer{r}").TrimEnd(':')}",
                    [
                        .. Registro(col, i, $"r{r}", $"{L("L_Record").TrimEnd(':')} {r + 1}", c => Conexion(c).Inner.GetRecord(registro), 10 + (r * 30)),
                        .. Entrenador(col, i, r, c => Conexion(c).GetTrainer(registro), 20 + (r * 30)),
                    ]));
                }
                return secciones;
            },
        };
        return new PestanaP("conexion", L("Tab_Connection"), [], Coleccion: col);
    }

    /// <summary><c>PokeathlonEventTrainer4Editor</c>: nombre (7 caracteres), TID, SID e idioma. <c>SaveObject</c> escribe el nombre primero (con el idioma que ya tenga el entrenador) y el idioma el ÚLTIMO: ese es el orden.</summary>
    private static IEnumerable<CampoP> Entrenador(ColeccionDefP col, int i, int r, Func<ContextoP, PokeathlonEventTrainer4> de, int prioridad)
    {
        string Id(string campo) => col.IdCampo(i, $"r{r}.ent.{campo}");
        var titulo = L($"L_Trainer{r}").TrimEnd(':');
        yield return P.Texto(Id("ot"), $"{titulo} · {L("L_OT").TrimEnd(':')}", c => de(c).OriginalTrainerName, (c, v) => { var t = de(c); t.OriginalTrainerName = v; }, maxLongitud: 7, prioridad: prioridad);
        yield return P.Entero(Id("tid"), $"{titulo} · {L("L_TID16").TrimEnd(':')}", c => de(c).TID16, (c, v) => { var t = de(c); t.TID16 = (ushort)v; }, 0, ushort.MaxValue, prioridad: prioridad + 1);
        yield return P.Entero(Id("sid"), $"{titulo} · {L("L_SID16").TrimEnd(':')}", c => de(c).SID16, (c, v) => { var t = de(c); t.SID16 = (ushort)v; }, 0, ushort.MaxValue, prioridad: prioridad + 2);
        yield return P.Opcion(Id("idioma"), $"{titulo} · {L("L_Language").TrimEnd(':')}", c => de(c).Language, (c, v) => { var t = de(c); t.Language = (byte)v; }, ListaDeIdiomas, titulo: $"{titulo} · {L("L_Language").TrimEnd(':')}", prioridad: prioridad + 3);
    }
}
