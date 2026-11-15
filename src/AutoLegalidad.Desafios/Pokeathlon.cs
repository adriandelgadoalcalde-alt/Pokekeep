// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;

namespace AutoLegalidad.Desafios;

/// <summary>
/// Pokéathlon de HeartGold y SoulSilver (<see cref="Pokeathlon4"/>) al máximo. Con los topes del Core (<c>Pokeathlon4.cs</c>, <c>PokeathlonGlobalCounters4.cs</c>), salvo las puntuaciones de los cursos y de las pruebas, que llevan el tope del juego (1.1.4):
/// <list type="bullet">
/// <item>Insignias («medallas» de PKHeX, cinco bits por especie) de las 493 especies: <see cref="PokeathlonMedalManager4.SetAllMedals"/>.</item>
/// <item>Récords de los cinco cursos (<c>Score0..2</c> y <c>ScoreMax</c>, u16): <see cref="TopesJuego.PokeathlonPuntuacion"/> (999), lo que el juego deja al guardar el curso (<c>pokeheartgold asm/overlay_96.s:3204-3224</c>). El formulario de PKHeX admite 65535.</item>
/// <item>Mejores puntuaciones de las diez pruebas (u16): 999, la misma cota (el juego las recorta a 999 al llevarlas al curso).</item>
/// <item>Puntos de atleta: <see cref="Pokeathlon4.MaxPoints"/> = 99999 (confirmado en el juego: <c>pokeheartgold pokeathlon_save.h</c>, «capped at 99999»).</item>
/// <item>Tarjetas de datos: <see cref="Pokeathlon4.DataCardAllObtained"/> (27 bits). Tienda diaria: <see cref="Pokeathlon4.FlagsShopAllObtained"/> (12 bits, «todo comprado hoy»).</item>
/// <item>Contadores: <see cref="PokeathlonGlobalCounters4.MaxPlay"/> para el tiempo, <see cref="PokeathlonGlobalCounters4.MaxStat"/> para el resto y <see cref="PokeathlonGlobalCounters4.MaxFame"/> para la fama. Los dos totales que el juego calcula
/// (primeros puestos en pruebas) se obtienen de las diez cuentas por prueba, no se escriben.</item>
/// </list>
/// Los participantes de los cursos NO se tocan aquí: los elige el jugador (<see cref="ParticipantePokeathlon"/>).
/// </summary>
public sealed class Pokeathlon4Maximo : InstalacionDesafio
{
    public static Pokeathlon4Maximo Instancia { get; } = new();
    private Pokeathlon4Maximo() : base("pokeathlon4", Textos.Doble("inst.4.pokeathlon"), Textos.Doble("juego.hgss")) { }

    public override bool Aplica(SaveFile sav) => sav is SAV4HGSS;

    private ModoDesafio[]? _modos;

    public override IReadOnlyList<ModoDesafio> Modos(SaveFile sav) => _modos ??= [Construir()];

    private static readonly string[] Cursos = ["velocidad", "fortaleza", "precision", "resistencia", "salto"];
    private static readonly string[] Pruebas = ["vallas", "banderines", "empuje", "bloques", "disco", "lamparas", "relevos", "anillos", "nieve", "rodada"];
    private static string CursoClave(int i) => Cursos[i];
    private static string PruebaClave(int i) => Pruebas[i];

    private static TextoDoble Etiqueta(string claveBase, string claveSufijo, int? n = null)
    {
        var a = Textos.Doble(claveBase);
        var b = Textos.Doble(claveSufijo);
        var sufijo = n is null ? "" : " " + n;
        return new TextoDoble($"{a.Es}: {b.Es}{sufijo}", $"{a.En}: {b.En}{sufijo}");
    }

    private static Pokeathlon4 P(SaveFile s) => ((SAV4HGSS)s).Pokeathlon;

    private static ModoDesafio Construir()
    {
        var campos = new List<CampoDesafio>();
        // Insignias: cuenta de las que hay (493 x 5). Su máximo es 2465 y se escribe con SetAllMedals en EscribirMaximos.
        campos.Add(Campo("insignias", "campo.insignias_total", RolCampo.Otro, PokeathlonMedalManager4.SIZE * 5,
            s => P(s).Medals.GetTotalCount(),
            (s, v) => { var m = P(s).Medals; m.SetAllMedals(v >= PokeathlonMedalManager4.SIZE * 5 ? PokeathlonMedalManager4.MaxMedalBits : (byte)0); }));
        for (var i = 0; i < (int)PokeathlonStat4.Count; i++)
        {
            var curso = (PokeathlonStat4)i;
            campos.Add(Campo($"curso{i + 1}_max", Etiqueta("campo.curso_record", "curso." + CursoClave(i)), RolCampo.Record, TopesJuego.PokeathlonPuntuacion,
                s => P(s).GetCourseRecord(curso).ScoreMax, (s, v) => { var c = P(s).GetCourseRecord(curso); c.ScoreMax = (ushort)Math.Clamp(v, 0, TopesJuego.PokeathlonPuntuacion); }));
            for (var k = 0; k < 3; k++)
            {
                var kk = k;
                campos.Add(Campo($"curso{i + 1}_p{k}", Etiqueta("campo.curso_puntos", "curso." + CursoClave(i), k + 1), RolCampo.Otro, TopesJuego.PokeathlonPuntuacion,
                    s => kk switch { 0 => P(s).GetCourseRecord(curso).Score0, 1 => P(s).GetCourseRecord(curso).Score1, _ => P(s).GetCourseRecord(curso).Score2 },
                    (s, v) =>
                    {
                        var c = P(s).GetCourseRecord(curso);
                        var x = (ushort)Math.Clamp(v, 0, TopesJuego.PokeathlonPuntuacion);
                        if (kk == 0) c.Score0 = x; else if (kk == 1) c.Score1 = x; else c.Score2 = x;
                    }));
            }
        }
        for (var i = 0; i < (int)PokeathlonEvent4.Count; i++)
        {
            var prueba = (PokeathlonEvent4)i;
            campos.Add(Campo($"prueba{i + 1}_mejor", Etiqueta("campo.prueba_mejor", "prueba." + PruebaClave(i)), RolCampo.Record, TopesJuego.PokeathlonPuntuacion,
                s => P(s).GetBestScore(prueba), (s, v) => P(s).SetBestScore(prueba, (ushort)Math.Clamp(v, 0, TopesJuego.PokeathlonPuntuacion))));
        }
        campos.Add(Campo("puntos", "campo.puntos_atleta", RolCampo.Otro, Pokeathlon4.MaxPoints, s => P(s).Points, (s, v) => P(s).Points = (uint)Math.Clamp(v, 0, uint.MaxValue)));
        campos.Add(Campo("tarjetas", "campo.tarjetas_datos", RolCampo.Bandera, Pokeathlon4.DataCardAllObtained, s => P(s).FlagsDataCard, (s, v) => P(s).FlagsDataCard = (uint)Math.Clamp(v, 0, uint.MaxValue)));
        campos.Add(Campo("tienda", "campo.tienda_diaria", RolCampo.Bandera, Pokeathlon4.FlagsShopAllObtained, s => P(s).FlagsDailyShop, (s, v) => P(s).FlagsDailyShop = (ushort)Math.Clamp(v, 0, ushort.MaxValue)));
        AnadirContador(campos, "tiempo", PokeathlonGlobalCounters4.MaxPlay, c => c.TimeSpent, (ref PokeathlonGlobalCounters4 c, uint v) => c.TimeSpent = v);
        AnadirContador(campos, "sesiones", PokeathlonGlobalCounters4.MaxStat, c => c.SessionsJoined, (ref PokeathlonGlobalCounters4 c, uint v) => c.SessionsJoined = v);
        AnadirContador(campos, "primero", PokeathlonGlobalCounters4.MaxStat, c => c.PlacedFirst, (ref PokeathlonGlobalCounters4 c, uint v) => c.PlacedFirst = v);
        AnadirContador(campos, "ultimo", PokeathlonGlobalCounters4.MaxStat, c => c.PlacedLast, (ref PokeathlonGlobalCounters4 c, uint v) => c.PlacedLast = v);
        AnadirContador(campos, "bonus", PokeathlonGlobalCounters4.MaxStat, c => c.BonusesEarned, (ref PokeathlonGlobalCounters4 c, uint v) => c.BonusesEarned = v);
        AnadirContador(campos, "instrucciones", PokeathlonGlobalCounters4.MaxStat, c => c.Instructions, (ref PokeathlonGlobalCounters4 c, uint v) => c.Instructions = v);
        AnadirContador(campos, "fallos", PokeathlonGlobalCounters4.MaxStat, c => c.Failed, (ref PokeathlonGlobalCounters4 c, uint v) => c.Failed = v);
        AnadirContador(campos, "saltos", PokeathlonGlobalCounters4.MaxStat, c => c.Jumped, (ref PokeathlonGlobalCounters4 c, uint v) => c.Jumped = v);
        AnadirContador(campos, "adquiridos", PokeathlonGlobalCounters4.MaxStat, c => c.Acquired, (ref PokeathlonGlobalCounters4 c, uint v) => c.Acquired = v);
        AnadirContador(campos, "placajes", PokeathlonGlobalCounters4.MaxStat, c => c.Tackled, (ref PokeathlonGlobalCounters4 c, uint v) => c.Tackled = v);
        AnadirContador(campos, "caidas", PokeathlonGlobalCounters4.MaxStat, c => c.FellDown, (ref PokeathlonGlobalCounters4 c, uint v) => c.FellDown = v);
        AnadirContador(campos, "carreras", PokeathlonGlobalCounters4.MaxStat, c => c.Dashed, (ref PokeathlonGlobalCounters4 c, uint v) => c.Dashed = v);
        AnadirContador(campos, "cambios", PokeathlonGlobalCounters4.MaxStat, c => c.Switched, (ref PokeathlonGlobalCounters4 c, uint v) => c.Switched = v);
        AnadirContador(campos, "autoobstaculos", PokeathlonGlobalCounters4.MaxStat, c => c.SelfImpeded, (ref PokeathlonGlobalCounters4 c, uint v) => c.SelfImpeded = v);
        AnadirContador(campos, "conexion_participadas", PokeathlonGlobalCounters4.MaxStat, c => c.ConnectionJoined, (ref PokeathlonGlobalCounters4 c, uint v) => c.ConnectionJoined = v);
        AnadirContador(campos, "conexion_primero", PokeathlonGlobalCounters4.MaxStat, c => c.ConnectionFirst, (ref PokeathlonGlobalCounters4 c, uint v) => c.ConnectionFirst = v);
        AnadirContador(campos, "conexion_ultimo", PokeathlonGlobalCounters4.MaxStat, c => c.ConnectionLast, (ref PokeathlonGlobalCounters4 c, uint v) => c.ConnectionLast = v);
        for (var i = 0; i < (int)PokeathlonEvent4.Count; i++)
        {
            var prueba = (PokeathlonEvent4)i;
            campos.Add(Campo($"primero_prueba{i + 1}", Etiqueta("campo.primero_prueba", "prueba." + PruebaClave(i)), RolCampo.Otro, PokeathlonGlobalCounters4.MaxStat,
                s => P(s).GlobalCounters[prueba], (s, v) => { var c = P(s).GlobalCounters; c[prueba] = (uint)Math.Clamp(v, 0, uint.MaxValue); }));
        }
        AnadirContador(campos, "ultimo_pruebas", PokeathlonGlobalCounters4.MaxStat, c => c.TotalEventLast, (ref PokeathlonGlobalCounters4 c, uint v) => c.TotalEventLast = v);
        AnadirContador(campos, "fama", PokeathlonGlobalCounters4.MaxFame, c => c.Fame, (ref PokeathlonGlobalCounters4 c, uint v) => c.Fame = v);
        return new ModoDesafio("todo", Textos.Doble("modo.todo"), NivelDesafio.NoAplica, campos);
    }

    private delegate void Escribe(ref PokeathlonGlobalCounters4 c, uint v);

    private static void AnadirContador(List<CampoDesafio> campos, string clave, uint maximo, Func<PokeathlonGlobalCounters4, uint> leer, Escribe escribir)
        => campos.Add(Campo("contador_" + clave, "campo.contador_" + clave, RolCampo.Otro, maximo,
            s => leer(P(s).GlobalCounters),
            (s, v) => { var c = P(s).GlobalCounters; escribir(ref c, (uint)Math.Clamp(v, 0, uint.MaxValue)); }));

    /// <summary>
    /// El campo «insignias» es un total derivado de 493 especies: escribirlo con el tope da las cinco insignias a todas; con cualquier otro valor las quita a todas. Por eso «Máximo» usa <c>SetAllMedals</c> directamente.
    /// </summary>
    protected override void EscribirMaximos(SaveFile sav, ModoDesafio modo)
    {
        var m = P(sav).Medals;
        m.SetAllMedals();
        foreach (var c in modo.Campos)
        {
            if (c.Clave == "insignias")
                continue;
            if (c.Maximo is { } v)
                c.Escribir(sav, v);
        }
    }
}

/// <summary>
/// Un participante de los récords de curso del Pokéathlon (<see cref="PokeathlonParticipant4"/>) copiado de un Pokémon de la partida: especie, forma, sexo, variocolor, clave de cifrado (la «PID» de la interfaz),
/// ID de entrenador (TID16) e ID secreto (SID16). Lo usa el selector «Elegir Pokémon…» del escritorio y de Android.
/// </summary>
public readonly record struct ParticipantePokeathlon(ushort Especie, byte Forma, byte Sexo, bool Variocolor, uint ClaveCifrado, ushort TID16, ushort SID16)
{
    /// <summary>La especie cabe en el campo de 9 bits y existe en la 4.ª generación (hasta Arceus).</summary>
    public bool EspecieValida => Especie is >= 1 and <= (int)Species.Arceus;

    /// <summary>Por qué este Pokémon no puede ser participante (<c>null</c> si puede).</summary>
    public TextoDoble? Motivo => EspecieValida ? null : Textos.Doble("motivo.participante_gen4");

    public static ParticipantePokeathlon Desde(PKM pk) =>
        new(pk.Species, pk.Form, pk.Gender, pk.IsShiny, pk.EncryptionConstant, pk.TID16, pk.SID16);

    /// <summary>Escribe el participante en la partida. Lanza si la especie no existe en la 4.ª generación: el juego no la sabría dibujar.</summary>
    public void Aplicar(PokeathlonParticipant4 destino)
    {
        if (!EspecieValida)
            throw new InvalidOperationException(Textos.Doble("motivo.participante_gen4").Es);
        destino.Species = Especie;
        destino.Form = Forma;
        destino.Gender = Sexo;
        destino.IsShiny = Variocolor;
        destino.EncryptionConstant = ClaveCifrado;
        destino.TID16 = TID16;
        destino.SID16 = SID16;
    }

    public static ParticipantePokeathlon Leer(PokeathlonParticipant4 p) =>
        new(p.Species, p.Form, p.Gender, p.IsShiny, p.EncryptionConstant, p.TID16, p.SID16);
}

/// <summary>
/// Insignias del Pokéathlon por especie («medallas» de PKHeX: <see cref="PokeathlonMedalManager4"/>). Cinco bits por especie, en el orden de <see cref="PokeathlonStat4"/>:
/// Velocidad, Fortaleza (Power), Precisión (Skill), Resistencia (Stamina) y Salto. Solo HeartGold y SoulSilver, especies 1 a 493.
/// </summary>
public static class InsigniasPokeathlon
{
    public const byte Todas = PokeathlonMedalManager4.MaxMedalBits;

    private static PokeathlonMedalManager4 Medallas(SaveFile sav) =>
        sav is SAV4HGSS h ? h.Pokeathlon.Medals : throw new ArgumentException("Las insignias del Pokéathlon solo existen en HeartGold y SoulSilver.", nameof(sav));

    private static void Comprobar(ushort especie)
    {
        if (especie is < 1 or > (int)Species.Arceus)
            throw new ArgumentOutOfRangeException(nameof(especie), especie, "El Pokéathlon solo conoce las especies 1 a 493.");
    }

    /// <summary>Bits de las insignias de la especie (bit 0 = Velocidad … bit 4 = Salto).</summary>
    public static byte Leer(SaveFile sav, ushort especie)
    {
        Comprobar(especie);
        return Medallas(sav).GetMedal(especie);
    }

    public static void Escribir(SaveFile sav, ushort especie, byte bits)
    {
        Comprobar(especie);
        Medallas(sav).SetMedal(especie, (byte)(bits & Todas));
    }

    /// <summary>Da las cinco insignias a la especie.</summary>
    public static void DarTodas(SaveFile sav, ushort especie) => Escribir(sav, especie, Todas);

    public static bool Tiene(SaveFile sav, ushort especie, PokeathlonStat4 curso) => (Leer(sav, especie) & (1 << (int)curso)) != 0;
}
