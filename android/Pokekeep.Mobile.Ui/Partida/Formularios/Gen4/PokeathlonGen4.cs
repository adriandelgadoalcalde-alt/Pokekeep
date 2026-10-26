// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen4;

/// <summary>
/// PK-SAV-020 a 025 y 046 a 053: <c>SAV_Pokeathlon4</c>, el Pokéathlon de HeartGold y SoulSilver. Siete pestañas: General (puntos, tienda diaria y tarjetas de datos), Medallas (493 especies × 5 cursos), Contadores, Récords (el mejor
/// resultado de cada una de las diez pruebas), Cursos (puntuaciones y tres participantes por curso), Evento propio y Conexión (intentos, cinco récords con su equipo de tres Pokémon y, en la conexión, cinco entrenadores). Los seis controles
/// incrustados (<c>PokeathlonConnection4Editor</c>, <c>PokeathlonEventData4Editor</c>, <c>PokeathlonEventRecord4Editor</c>, <c>PokeathlonEventTrainer4Editor</c>, <c>PokeathlonParticipant4Editor</c> y <c>PokeathlonSpeciesForm4Editor</c>)
/// son los campos de las listas de cursos y eventos.
/// <para><c>B_Save_Click</c> escribe: el curso, el evento propio y la conexión que están abiertos, y después General, Medallas, Contadores y Récords ENTEROS. Aquí cada campo escribe lo suyo. Los índices (curso, evento) son
/// los elementos de una lista, así que lo editado en uno no se pierde al abrir otro. Divergencias declaradas en el ADR 0065: el formulario reescribe todo lo que enseña (medallas de las 493 especies con los 5 bits bajos, formas
/// recortadas al número de formas de la especie, géneros a 0-2, nombres de entrenador con su relleno) y la app solo lo que se cambia; el sprite de la especie, que es una ayuda visual, no se enseña.</para>
/// </summary>
internal static partial class PokeathlonGen4
{
    public const string Form = "SAV_Pokeathlon4";
    public static readonly string[] Censo = ["PK-SAV-020", "PK-SAV-021", "PK-SAV-022", "PK-SAV-023", "PK-SAV-024", "PK-SAV-025", "PK-SAV-046", "PK-SAV-047", "PK-SAV-048", "PK-SAV-049", "PK-SAV-050", "PK-SAV-051", "PK-SAV-052", "PK-SAV-053"];

    private static string L(string control) => EtiquetasLang.Requerir(Form, control);
    private static SAV4HGSS S(ContextoP c) => (SAV4HGSS)c.Sav;
    private static Pokeathlon4 At(ContextoP c) => S(c).Pokeathlon;

    /// <summary>Predicado barato de <see cref="Crear"/>: <c>B_OpenPokeathlon</c> solo existe en HeartGold y SoulSilver.</summary>
    public static bool Aplica(SaveFile sav) => sav is SAV4HGSS;

    public static EditorDefP? Crear(SaveFile sav)
    {
        if (sav is not SAV4HGSS) return null;
        return new EditorDefP("pokeathlon", TituloGen4.De("pokeathlon"),
        [
            General(), Medallas(), TodasLasMedallas(), Contadores(), Records(), Cursos(), EventoPropio(), Conexion(),
        ], Censo, IdiomaInterfaz.T("Partida.Especifico.pokeathlon.Descripcion"));
    }

    /// <summary>Traducción de un valor de enumeración de PKHeX (<c>PokeathlonEvent4.HurdleDash</c>, <c>PokeathlonStat4.Speed</c>) con la linea de <c>lang_es.txt</c>.</summary>
    private static string Enum(string tipo, string valor) => EtiquetasLang.Obtener($"{tipo}.{valor}") ?? valor;

    private static readonly string[] Pruebas = ["HurdleDash", "PennantCapture", "CirclePush", "BlockSmash", "DiscCatch", "LampJump", "RelayRun", "RingDrop", "SnowThrow", "GoalRoll"];
    private static readonly string[] Cualidades = ["Speed", "Power", "Skill", "Stamina", "Jump"];

    /// <summary><c>GetEventDisplayName</c>: «1 - Carrera de Vallas».</summary>
    private static string NombreDePrueba(int i) => $"{i + 1} - {Enum("PokeathlonEvent4", Pruebas[i])}";
    /// <summary><c>GetCourseDisplayName</c>: «1 - Velocidad».</summary>
    private static string NombreDeCurso(int i) => $"{i + 1} - {Enum("PokeathlonStat4", Cualidades[i])}";

    // ---------------------------------------------------------------- General (Tab_General)
    private static CampoP MarcaDeBits(string id, string etiqueta, Func<ContextoP, bool> leer, Action<ContextoP, bool> escribir, int prioridad, string grupo, IEnumerable<string> todos, Func<ContextoP, string>? etiquetaFn = null)
        => new() { Id = id, Etiqueta = etiqueta, EtiquetaFn = etiquetaFn, Tipo = TipoP.Marca, Leer = c => leer(c), Escribir = (c, v) => escribir(c, v is true), Prioridad = prioridad, GrupoEscritura = grupo, LecturaAsimetrica = true, AfectaA = _ => todos };

    /// <summary>El indicador entero que escribe el formulario: un bit por casilla marcada del borrador (bit k = casilla k).</summary>
    private static uint Bits(ContextoP c, string prefijo, int cantidad)
    {
        uint r = 0;
        for (var k = 0; k < cantidad; k++) if (c.Marca($"{prefijo}{k}")) r |= 1u << k;
        return r;
    }

    private static PestanaP General()
    {
        // LoadGeneral: los puntos se recortan a 99.999 al abrir; las tarjetas se llaman «[i] objeto» con el nombre del objeto 505 + i. SaveGeneral escribe puntos, tienda diaria y tarjetas (cada indicador ENTERO desde las casillas):
        // un campo por casilla y el primero vuelca el indicador entero desde el borrador (grupo de escritura).
        var idsTienda = Enumerable.Range(0, 12).Select(k => $"tienda{k}").ToArray();
        var tienda = Enumerable.Range(0, 12).Select(i => MarcaDeBits(idsTienda[i], $"{L("L_DailyShopFlags").TrimEnd(':')} · {i + 1}", c => ((At(c).FlagsDailyShop >> i) & 1) != 0,
            (c, _) => { if (i == 0) At(c).FlagsDailyShop = (ushort)Bits(c, "tienda", 12); }, 1 + i, "tienda", idsTienda)).ToList();
        var idsTarjeta = Enumerable.Range(0, (int)DataCard4.Count).Select(k => $"tarjeta{k}").ToArray();
        var tarjetas = Enumerable.Range(0, (int)DataCard4.Count).Select(i => MarcaDeBits(idsTarjeta[i], $"[{i}]", c => ((At(c).FlagsDataCard >> i) & 1) != 0,
            (c, _) => { if (i == 0) At(c).FlagsDataCard = Bits(c, "tarjeta", (int)DataCard4.Count); }, 20 + i, "tarjetas", idsTarjeta, c => $"[{i}] {c.Textos.GetItemStrings(c.Sav.Context, c.Sav.Version)[505 + i]}")).ToList();
        return new PestanaP("general", L("Tab_General"),
        [
            new SeccionP(string.Empty, [P.Entero("puntos", L("L_Points").TrimEnd(':'), c => Math.Min(99_999u, At(c).Points), (c, v) => At(c).Points = (uint)v, 0, Pokeathlon4.MaxPoints, prioridad: 0)]),
            new SeccionP(L("L_DailyShopFlags").TrimEnd(':'), tienda),
            new SeccionP(L("L_DataCards").TrimEnd(':'), tarjetas),
        ]);
    }

    // ---------------------------------------------------------------- Medallas (Tab_Medals)
    private static PestanaP Medallas()
    {
        ColeccionDefP? col = null;
        col = new ColeccionDefP
        {
            Id = "especies", Elemento = L("DGV_Species"), Cantidad = _ => PokeathlonMedalManager4.SIZE,
            Titulo = (c, i) => $"{i + 1:000} · {c.Textos.specieslist[i + 1]}",
            Resumen = (c, i) => $"{System.Numerics.BitOperations.PopCount(At(c).Medals.GetMedal((ushort)(i + 1)) & (uint)PokeathlonMedalManager4.MaxMedalBits)} de 5",
            Secciones = (_, i) =>
            {
                var ids = Cualidades.Select((_, k) => col!.IdCampo(i, $"m{k}")).ToArray();
                // SaveMedals: SetMedal(especie, bits) con los 5 bits de las casillas de la fila (los demas bits del byte se borran).
                var campos = Cualidades.Select((nombre, k) => P.Marca(ids[k], L($"DGV_{nombre}"), c => ((At(c).Medals.GetMedal((ushort)(i + 1)) >> k) & 1) != 0,
                    (c, v) => At(c).Medals.SetMedal((ushort)(i + 1), (byte)Bits(c, string.Empty, ids)), prioridad: k, grupo: $"medalla:{i}", afectaA: _ => ids)).ToList();
                return [new SeccionP(string.Empty, campos)];
            },
        };
        return new PestanaP("medallas", L("Tab_Medals"), [], Coleccion: col);
    }

    private static uint Bits(ContextoP c, string _, string[] ids)
    {
        uint r = 0;
        for (var k = 0; k < ids.Length; k++) if (c.Marca(ids[k])) r |= 1u << k;
        return r;
    }

    private static PestanaP TodasLasMedallas() => new("medallas_todas", IdiomaInterfaz.F("Gen4.PestanaTodas", L("Tab_Medals")),
    [
        new SeccionP(string.Empty,
        [
            // B_MedalsGiveAll / B_MedalsClearAll: SetAllMedals()/Clear() sobre las 493 especies al momento y la rejilla se relee.
            P.Accion("dar_todo", L("B_MedalsGiveAll"), c => At(c).Medals.SetAllMedals(), inmediata: true, textoBoton: IdiomaInterfaz.T("PokeathlonGen4.DarTodo"), confirmacion: IdiomaInterfaz.T("PokeathlonGen4.DanCincoMedallasEspecies"), ayuda: IdiomaInterfaz.T("PokeathlonGen4.DaCincoMedallasTodas")),
            P.Accion("quitar_todo", L("B_MedalsClearAll"), c => At(c).Medals.Clear(), inmediata: true, textoBoton: IdiomaInterfaz.T("PokeathlonGen4.QuitarTodo"), confirmacion: IdiomaInterfaz.T("PokeathlonGen4.QuitanMedallasEspecies"), ayuda: IdiomaInterfaz.T("PokeathlonGen4.QuitaTodasMedallas")),
        ]),
    ]);

    // ---------------------------------------------------------------- Contadores (Tab_Counters)
    private static PestanaP Contadores()
    {
        const uint Estadistica = PokeathlonGlobalCounters4.MaxStat;
        // (id, clave de la etiqueta en lang_es, tope del cuadro, leer, escribir). InitializeCounters: el contador «Total 1.º» no tiene setter (cuadro deshabilitado). SaveCounters los escribe todos con (uint)valor.
        (string Id, string Control, uint Max, Func<PokeathlonGlobalCounters4, uint> Leer, Action<PokeathlonGlobalCounters4, uint>? Escribir)[] filas =
        [
            ("tiempo", "L_TimeSpent", PokeathlonGlobalCounters4.MaxPlay, g => g.TimeSpent, (g, v) => g.TimeSpent = v),
            ("sesiones", "L_SessionsJoined", Estadistica, g => g.SessionsJoined, (g, v) => g.SessionsJoined = v),
            ("primero", "L_PlacedFirst", Estadistica, g => g.PlacedFirst, (g, v) => g.PlacedFirst = v),
            ("ultimo", "L_PlacedLast", Estadistica, g => g.PlacedLast, (g, v) => g.PlacedLast = v),
            ("bonos", "L_BonusesEarned", Estadistica, g => g.BonusesEarned, (g, v) => g.BonusesEarned = v),
            ("instrucciones", "L_Instructions", Estadistica, g => g.Instructions, (g, v) => g.Instructions = v),
            ("fallos", "L_Failed", Estadistica, g => g.Failed, (g, v) => g.Failed = v),
            ("saltos", "L_Jumped", Estadistica, g => g.Jumped, (g, v) => g.Jumped = v),
            ("obtenido", "L_Acquired", Estadistica, g => g.Acquired, (g, v) => g.Acquired = v),
            ("placajes", "L_Tackled", Estadistica, g => g.Tackled, (g, v) => g.Tackled = v),
            ("caidas", "L_FellDown", Estadistica, g => g.FellDown, (g, v) => g.FellDown = v),
            ("carreras", "L_Dashed", Estadistica, g => g.Dashed, (g, v) => g.Dashed = v),
            ("cambios", "L_Switched", Estadistica, g => g.Switched, (g, v) => g.Switched = v),
            ("estorbos", "L_SelfImpeded", Estadistica, g => g.SelfImpeded, (g, v) => g.SelfImpeded = v),
            ("conexiones", "L_ConnectionJoined", Estadistica, g => g.ConnectionJoined, (g, v) => g.ConnectionJoined = v),
            ("conexion_primero", "L_ConnectionFirst", Estadistica, g => g.ConnectionFirst, (g, v) => g.ConnectionFirst = v),
            ("conexion_ultimo", "L_ConnectionLast", Estadistica, g => g.ConnectionLast, (g, v) => g.ConnectionLast = v),
        ];
        var campos = new List<CampoP>();
        for (var i = 0; i < filas.Length; i++)
        {
            var (id, control, max, leer, escribir) = filas[i];
            campos.Add(P.Entero(id, L(control).TrimEnd(':'), c => Math.Min(max, leer(At(c).GlobalCounters)), (c, v) => { var g = At(c).GlobalCounters; escribir!(g, (uint)v); }, 0, max, prioridad: i));
        }
        for (var e = 0; e < Pruebas.Length; e++)
        {
            var evento = (PokeathlonEvent4)e;
            campos.Add(P.Entero($"prueba{e}", L($"L_{Pruebas[e]}First").TrimEnd(':'), c => Math.Min(Estadistica, At(c).GlobalCounters[evento]), (c, v) => { var g = At(c).GlobalCounters; g[evento] = (uint)v; }, 0, Estadistica, prioridad: 20 + e, afectaA: _ => ["total_primero"]));
        }
        campos.Add(P.Entero("total_primero", L("L_TotalEventFirst").TrimEnd(':'), c => Math.Min(Estadistica * (long)PokeathlonEvent4.Count, At(c).GlobalCounters.TotalEventFirst), null, 0, Estadistica * (long)PokeathlonEvent4.Count,
            motivo: _ => IdiomaInterfaz.T("PokeathlonGen4.SumaDiezPruebasArriba"), prioridad: 40));
        campos.Add(P.Entero("total_ultimo", L("L_TotalEventLast").TrimEnd(':'), c => Math.Min(Estadistica, At(c).GlobalCounters.TotalEventLast), (c, v) => { var g = At(c).GlobalCounters; g.TotalEventLast = (uint)v; }, 0, Estadistica, prioridad: 41));
        campos.Add(P.Entero("fama", L("L_Fame").TrimEnd(':'), c => Math.Min(PokeathlonGlobalCounters4.MaxFame, At(c).GlobalCounters.Fame), (c, v) => { var g = At(c).GlobalCounters; g.Fame = (uint)v; }, 0, PokeathlonGlobalCounters4.MaxFame, prioridad: 42));
        return new PestanaP("contadores", L("Tab_Counters"), [new SeccionP(string.Empty, campos)]);
    }

    // ---------------------------------------------------------------- Récords (Tab_Best)
    private static PestanaP Records()
    {
        var campos = Enumerable.Range(0, Pruebas.Length).Select(i => (CampoP)P.Entero($"mejor{i}", NombreDePrueba(i), c => At(c).GetBestScore((PokeathlonEvent4)i), (c, v) => At(c).SetBestScore((PokeathlonEvent4)i, (ushort)v), 0, ushort.MaxValue, prioridad: i)).ToList();
        return new PestanaP("mejores", L("Tab_Best"), [new SeccionP(string.Empty, campos)]);
    }
}
