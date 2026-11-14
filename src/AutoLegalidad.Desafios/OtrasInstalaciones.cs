// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;

namespace AutoLegalidad.Desafios;

/// <summary>
/// Copa Mundial Pokémon de Negro 2 y Blanco 2 (<see cref="PWTBlock5"/>): 29 registros u16 desde 0x5C (<c>PWTBlock5.GetRecordOffset</c>), uno por <see cref="PWTRecordID"/>.
/// PKHeX no los expone en ningún formulario. Tope «Máximo» = el del Core (u16, 65535). Qué cuenta exactamente cada registro NO está comprobado en el juego (investigación §4, «PWT»):
/// son torneos con final y no una racha continua, así que «Ir al jefe» no existe aquí.
/// </summary>
public sealed class Pwt5 : InstalacionDesafio
{
    public static Pwt5 Instancia { get; } = new();
    private Pwt5() : base("pwt5", Textos.Doble("inst.5.pwt"), Textos.Doble("juego.b2w2")) { }

    public override bool Aplica(SaveFile sav) => sav is SAV5B2W2;

    private ModoDesafio[]? _modos;

    public override IReadOnlyList<ModoDesafio> Modos(SaveFile sav) => _modos ??= [.. Enum.GetValues<PWTRecordID>().Select(id =>
        new ModoDesafio("pwt." + id.ToString().ToLowerInvariant(), Textos.Doble("pwt." + id.ToString().ToLowerInvariant()), NivelDesafio.NoAplica,
        [
            Campo("registro", "campo.registro", RolCampo.Otro, ushort.MaxValue, s => ((SAV5B2W2)s).PWT.GetPWTRecord(id), (s, v) => ((SAV5B2W2)s).PWT.SetPWTRecord(id, (ushort)Math.Clamp(v, 0, ushort.MaxValue))),
        ]))];
}

/// <summary>
/// Mansión Batalla de Pokémon X, Y, Rubí Omega y Zafiro Alfa (<see cref="MaisonBlock"/>): 20 u16 desde 0x1C0 del bloque, cinco estilos × (actual normal, actual Super, mejor normal, mejor Super).
/// El orden de las casillas es el de los formularios de PKHeX (<c>SAV_Trainer.cs:30-36</c>: índice 0 = actual normal, 1 = actual Super, 2 = mejor normal, 3 = mejor Super); el comentario de <c>MaisonBlock.cs</c> dice otra cosa y no se ha comprobado en partida (investigación §5).
/// Tope «Máximo» 9999: la máscara de cuatro cifras de los formularios (<c>SAV_Trainer.Designer.cs:1410</c>). El tope del juego NO está confirmado.
/// «Ir al jefe»: las Damas Batalla salen en el combate 20 del modo normal y en el 50 del Súper, en los cinco estilos (Bulbapedia, «Battle Maison», «on 20th / 50th consecutive battle»),
/// y el juego deja interrumpir el reto tras cualquier combate sin perder la racha («The player may interrupt any challenge at any time»): racha 19 o 49. Consultado el 14-nov-2026.
/// </summary>
public sealed class Maison6 : InstalacionDesafio
{
    public const int Tope = 9999;
    public static Maison6 Instancia { get; } = new();
    private Maison6() : base("maison6", Textos.Doble("inst.6.maison"), Textos.Doble("juego.gen6")) { }

    public override bool Aplica(SaveFile sav) => sav is ISaveBlock6Main;

    private ModoDesafio[]? _modos;

    public override IReadOnlyList<ModoDesafio> Modos(SaveFile sav)
    {
        if (_modos is not null)
            return _modos;
        string[] estilos = ["individual", "dobles", "triples", "rotacion", "multi"];
        var lista = new List<ModoDesafio>();
        for (var e = 0; e < estilos.Length; e++)
        {
            for (var super = 0; super < 2; super++)
            {
                var actual = (e << 2) | super;
                var mejor = actual | 2;
                var tren = Textos.Doble(super == 0 ? "modo.normal" : "modo.super");
                lista.Add(new ModoDesafio($"{estilos[e]}.{(super == 0 ? "normal" : "super")}", Frente3Esmeralda.Combinar(Textos.Doble("modo." + estilos[e]), tren), NivelDesafio.NoAplica,
                [
                    Campo("actual", "campo.racha_actual", RolCampo.RachaActual, Tope, s => ((ISaveBlock6Main)s).Maison.GetMaisonStat(actual), (s, v) => ((ISaveBlock6Main)s).Maison.SetMaisonStat(actual, (ushort)Math.Clamp(v, 0, ushort.MaxValue))),
                    Campo("record", "campo.record_racha", RolCampo.Record, Tope, s => ((ISaveBlock6Main)s).Maison.GetMaisonStat(mejor), (s, v) => ((ISaveBlock6Main)s).Maison.SetMaisonStat(mejor, (ushort)Math.Clamp(v, 0, ushort.MaxValue))),
                ]));
            }
        }
        return _modos = [.. lista];
    }

    internal override PlanJefe Planificar(SaveFile sav, ModoDesafio modo, JefeDesafio jefe) => PlanVeinteCincuenta(sav, modo, jefe);

    /// <summary>Mansión y Árbol: jefe en el combate 20 (normal) y 50 (Súper); se guarda tras cualquier combate, así que la racha queda en 19 o 49.</summary>
    internal static PlanJefe PlanVeinteCincuenta(SaveFile sav, ModoDesafio modo, JefeDesafio jefe)
    {
        var esSuper = modo.Clave.EndsWith(".super", StringComparison.Ordinal);
        if (jefe == JefeDesafio.Repetido)
            return PlanJefe.No("motivo.sin_repeticion");
        if (!esSuper && jefe == JefeDesafio.Oro)
            return PlanJefe.No("motivo.normal_un_jefe_20");
        if (esSuper && jefe == JefeDesafio.Plata)
            return PlanJefe.No("motivo.super_solo_50");
        long racha = esSuper ? 49 : 19;
        var valores = new Dictionary<string, long>
        {
            ["actual"] = racha,
            ["record"] = Math.Max(modo.Campo("record").Leer(sav), racha),
        };
        return PlanJefe.Si(esSuper ? JefeDesafio.Oro : JefeDesafio.Plata, valores, esSuper ? "explic.super_desbloqueado" : null);
    }
}

/// <summary>
/// Árbol Batalla de Sol, Luna, Ultrasol y Ultraluna (<see cref="BattleTree7"/>): u16 en <c>8*tipo + 2*super + 4*récord</c> (<c>BattleTree7.GetStreakOffset</c>), tres tipos (Individual, Dobles, Multi; el cuarto no se usa).
/// Tope «Máximo» 65535: el máximo de los formularios (<c>SAV_Trainer7.Designer.cs</c>, NUD_RCStreak*) y el de PKHeX en el Core (u16); el tope del juego NO está confirmado (investigación §6).
/// «Ir al jefe»: Red (Individual), Blue (Dobles) o los dos (Multi) en el combate 20 del modo normal y en el 50 del Súper (Bulbapedia, «Battle Tree»: «On the 20th battle of normal
/// challenges and the 50th battle of Super challenges, a Battle Legend will be challenged»), y se puede descansar tras cualquier combate sin perder la racha (Serebii): racha 19 o 49.
/// Lo confirma en partida un usuario de Project Pokémon (hilo 43750: racha 29, 39 y 49 y ganar a mano los combates siguientes). Consultado el 14-nov-2026.
/// </summary>
public sealed class Arbol7 : InstalacionDesafio
{
    public const int Tope = ushort.MaxValue;
    public static Arbol7 Instancia { get; } = new();
    private Arbol7() : base("arbol7", Textos.Doble("inst.7.arbol"), Textos.Doble("juego.gen7")) { }

    public override bool Aplica(SaveFile sav) => sav is SAV7;

    private ModoDesafio[]? _modos;

    public override IReadOnlyList<ModoDesafio> Modos(SaveFile sav)
    {
        if (_modos is not null)
            return _modos;
        string[] tipos = ["individual", "dobles", "multi"];
        var lista = new List<ModoDesafio>();
        for (var t = 0; t < tipos.Length; t++)
        {
            for (var super = 0; super < 2; super++)
            {
                var tt = t;
                var ss = super == 1;
                lista.Add(new ModoDesafio($"{tipos[t]}.{(ss ? "super" : "normal")}", Frente3Esmeralda.Combinar(Textos.Doble("modo." + tipos[t]), Textos.Doble(ss ? "modo.super" : "modo.normal")), NivelDesafio.NoAplica,
                [
                    Campo("actual", "campo.racha_actual", RolCampo.RachaActual, Tope, s => ((SAV7)s).BattleTree.GetTreeStreak(tt, ss, false), (s, v) => ((SAV7)s).BattleTree.SetTreeStreak((int)Math.Clamp(v, 0, ushort.MaxValue), tt, ss, false)),
                    Campo("record", "campo.record_racha", RolCampo.Record, Tope, s => ((SAV7)s).BattleTree.GetTreeStreak(tt, ss, true), (s, v) => ((SAV7)s).BattleTree.SetTreeStreak((int)Math.Clamp(v, 0, ushort.MaxValue), tt, ss, true)),
                ]));
            }
        }
        return _modos = [.. lista];
    }

    internal override PlanJefe Planificar(SaveFile sav, ModoDesafio modo, JefeDesafio jefe) => Maison6.PlanVeinteCincuenta(sav, modo, jefe);
}

/// <summary>
/// Torre Batalla de Espada y Escudo: racha y victorias de Individual y Dobles (bloques por clave de <see cref="SaveBlockAccessor8SWSH"/>).
/// Tope «Máximo»: racha 300 (comentario de <c>SaveBlockAccessor8SWSH.cs:216-217</c> y <c>SAV_Trainer8.cs:138-139</c>: es un tope de PKHeX; que lo imponga el juego NO está confirmado, investigación §7.1) y victorias 9.999.999 (<c>SaveBlockAccessor8SWSH.cs:214-215</c>, <c>SAV_Trainer8.cs:134-135</c>).
/// Las victorias también se guardan en el registro del jugador (<c>SAV_Trainer8.cs:141-142</c>): se mantienen en paso.
/// «Ir al jefe»: Leon sale en el último combate antes de subir de nivel y, en la Master Ball, cada diez victorias (Bulbapedia, «Battle Tower (Galar)»): va por rango, no por la racha. No se ofrece.
/// </summary>
public sealed class Torre8 : InstalacionDesafio
{
    internal override PlanJefe Planificar(SaveFile sav, ModoDesafio modo, JefeDesafio jefe) => PlanJefe.No("motivo.por_rango_galar");

    public const int RachaTope = 300;
    public const uint VictoriasTope = 9_999_999;
    public static Torre8 Instancia { get; } = new();
    private Torre8() : base("torre8", Textos.Doble("inst.8.torre"), Textos.Doble("juego.swsh")) { }

    public override bool Aplica(SaveFile sav) => sav is SAV8SWSH;

    private ModoDesafio[]? _modos;

    public override IReadOnlyList<ModoDesafio> Modos(SaveFile sav) => _modos ??=
    [
        Crear("individual", SaveBlockAccessor8SWSH.KBattleTowerSinglesStreak, SaveBlockAccessor8SWSH.KBattleTowerSinglesVictory, RecordLists.G8BattleTowerSingleWin),
        Crear("dobles", SaveBlockAccessor8SWSH.KBattleTowerDoublesStreak, SaveBlockAccessor8SWSH.KBattleTowerDoublesVictory, RecordLists.G8BattleTowerDoubleWin),
    ];

    private static ModoDesafio Crear(string clave, uint claveRacha, uint claveVictorias, int registro) =>
        new(clave, Textos.Doble("modo." + clave), NivelDesafio.NoAplica,
        [
            Campo("actual", "campo.racha_actual", RolCampo.RachaActual, RachaTope, s => ((SAV8SWSH)s).GetValue<ushort>(claveRacha), (s, v) => ((SAV8SWSH)s).SetValue(claveRacha, (ushort)Math.Clamp(v, 0, ushort.MaxValue))),
            Campo("victorias", "campo.victorias", RolCampo.Otro, VictoriasTope, s => ((SAV8SWSH)s).GetValue<uint>(claveVictorias),
                (s, v) =>
                {
                    var sav = (SAV8SWSH)s;
                    var valor = (uint)Math.Clamp(v, 0, VictoriasTope);
                    sav.SetValue(claveVictorias, valor);
                    sav.SetRecord(registro, (int)valor);
                }),
        ]);
}

/// <summary>
/// Torre Batalla de Diamante Brillante y Perla Reluciente (<see cref="BattleTowerWork8b"/>): cuatro clases con racha actual (<c>RenshouCount</c>), racha anterior (<c>RenshouCountOld</c>, hace de «récord»), victorias totales y rango,
/// más dos datos generales (<c>MasterRankMax</c> y <c>ChallengeCount</c>).
/// «Máximo»: racha, racha anterior y victorias totales a 9999 por ANALOGÍA con la Torre de HGSS/Platino (el original de DBPR guarda 9999 como tope, investigación §3.4); el Core las guarda como u32 sin tope y el del juego NO está confirmado (investigación §7.2).
/// El rango, <c>MasterRankMax</c> y <c>ChallengeCount</c> se pueden leer y escribir pero «Máximo» NO los toca: no se conoce su escala (investigación §7.2, «interacción con Rank»).
/// «Ir al jefe»: en DBPR el jefe del 7.º combate de la Clase Maestra lo decide el rango (rango 10: Palmer en Individual, Barry y Palmer en Dobles; Bulbapedia, «Battle Tower (Sinnoh)»),
/// no la racha. El Core no dice qué clase es cada una de las cuatro, así que no se ofrece: el rango se puede editar a mano en el modo.
/// </summary>
public sealed class Torre8b : InstalacionDesafio
{
    internal override PlanJefe Planificar(SaveFile sav, ModoDesafio modo, JefeDesafio jefe) => PlanJefe.No("motivo.por_rango_dbpr");

    public const int Tope = 9999;
    public static Torre8b Instancia { get; } = new();
    private Torre8b() : base("torre8b", Textos.Doble("inst.8b.torre"), Textos.Doble("juego.bdsp")) { }

    public override bool Aplica(SaveFile sav) => sav is SAV8BS;

    private ModoDesafio[]? _modos;

    public override IReadOnlyList<ModoDesafio> Modos(SaveFile sav)
    {
        if (_modos is not null)
            return _modos;
        var lista = new List<ModoDesafio>();
        for (var i = 0; i < 4; i++)
        {
            var c = i;
            static BattleTowerClassData8b Cl(SaveFile s, int idx) => ((SAV8BS)s).BattleTower.Records[idx];
            lista.Add(new ModoDesafio($"clase{c + 1}", Textos.Doble("modo.clase" + (c + 1)), NivelDesafio.NoAplica,
            [
                Campo("actual", "campo.racha_actual", RolCampo.RachaActual, Tope, s => Cl(s, c).RenshouCount, (s, v) => Cl(s, c).RenshouCount = (uint)Math.Clamp(v, 0, uint.MaxValue)),
                Campo("record", "campo.racha_anterior", RolCampo.Record, Tope, s => Cl(s, c).RenshouCountOld, (s, v) => Cl(s, c).RenshouCountOld = (uint)Math.Clamp(v, 0, uint.MaxValue)),
                Campo("victorias", "campo.victorias", RolCampo.Otro, Tope, s => Cl(s, c).TotalWins, (s, v) => Cl(s, c).TotalWins = (uint)Math.Clamp(v, 0, uint.MaxValue)),
                Campo("rango", "campo.rango", RolCampo.Otro, null, s => Cl(s, c).Rank, (s, v) => Cl(s, c).Rank = (uint)Math.Clamp(v, 0, uint.MaxValue)),
            ]));
        }
        lista.Add(new ModoDesafio("general", Textos.Doble("modo.general"), NivelDesafio.NoAplica,
        [
            Campo("rango_maestro", "campo.rango_maestro", RolCampo.Otro, null, s => ((SAV8BS)s).BattleTower.MasterRankMax, (s, v) => ((SAV8BS)s).BattleTower.MasterRankMax = (int)Math.Clamp(v, 0, int.MaxValue)),
            Campo("retos", "campo.retos", RolCampo.Otro, null, s => ((SAV8BS)s).BattleTower.ChallengeCount, (s, v) => ((SAV8BS)s).BattleTower.ChallengeCount = (uint)Math.Clamp(v, 0, uint.MaxValue)),
        ]));
        return _modos = [.. lista];
    }
}

/// <summary>
/// Colina Entrenador de Esmeralda: el mejor tiempo de cada modo (Normal, Variedad, Único y Experto), en fotogramas de 1/60 s (<c>SaveBlock3LargeE.GetTrainerHillRecord</c>, u32 desde 0x3718).
/// OJO: en el juego un modo sin jugar vale 215999 (<c>HILL_MAX_TIME</c>, <c>pokeemerald src/trainer_hill.c:35,288</c>), no 0 como decía la investigación, y un tiempo MEJOR es uno MENOR: el récord solo se sustituye si el nuevo es más bajo (<c>trainer_hill.c:456</c>).
/// «Máximo» = el mejor tiempo plausible: <see cref="MejorTiempoPlausible"/>. No lo fija el juego (el temporizador admite hasta el 0, que nadie puede hacer: hay que recorrer cuatro plantas y combatir en cada una) sino esta biblioteca: 3 min 00 s, por debajo del
/// escalón del premio mayor (menos de 12 min, <c>trainer_hill.c:1075-1085</c>) y de lo que se puede hacer jugando de verdad. Es una decisión de producto revisable.
/// No hay racha ni jefe: «Ir al jefe» no existe aquí.
/// </summary>
public sealed class ColinaEntrenador3 : InstalacionDesafio
{
    /// <summary><c>HILL_MAX_TIME</c> (60 * 60 * 60 - 1, trainer_hill.c:35): el valor de «sin récord» y el tiempo más lento que guarda el juego.</summary>
    public const uint SinRecord = 215999;
    /// <summary>Mejor tiempo plausible (decisión de la biblioteca, ver el resumen de la clase): 3 min 00 s a 60 fotogramas por segundo.</summary>
    public const uint MejorTiempoPlausible = 3 * 60 * 60;

    public static ColinaEntrenador3 Instancia { get; } = new();
    private ColinaEntrenador3() : base("colina3", Textos.Doble("inst.e.colina"), Textos.Doble("juego.esmeralda")) { }

    public override bool Aplica(SaveFile sav) => sav is SAV3E;

    protected override void EscribirMaximos(SaveFile sav, ModoDesafio modo)
    {
        // Un tiempo MEJOR es uno MENOR: si el jugador ya tiene uno por debajo del plausible, no se empeora.
        var campo = modo.Campo("tiempo");
        if (campo.Leer(sav) > MejorTiempoPlausible)
            campo.Escribir(sav, MejorTiempoPlausible);
    }

    private ModoDesafio[]? _modos;

    public override IReadOnlyList<ModoDesafio> Modos(SaveFile sav) => _modos ??= [.. Enum.GetValues<TrainerHillMode3E>().Select(m =>
        new ModoDesafio("colina." + m.ToString().ToLowerInvariant(), Textos.Doble("colina." + m.ToString().ToLowerInvariant()), NivelDesafio.NoAplica,
        [
            Campo("tiempo", "campo.mejor_tiempo", RolCampo.Record, MejorTiempoPlausible, s => ((SAV3E)s).LargeBlock.GetTrainerHillRecord(m), (s, v) => ((SAV3E)s).LargeBlock.SetTrainerHillRecord(m, (uint)Math.Clamp(v, 0, uint.MaxValue))),
        ]))];
}

/// <summary>
/// Royale Infinita de Z-A: lo que tiene nombre en el Core. <see cref="InfiniteRoyale9a"/> (<c>Wins</c> y <c>PrizeMedals</c>; <c>Value0/1/4/5</c> no tienen nombre y no se tocan) y los puntos de entrada de las dos Royales
/// (<c>SAV9ZA.TicketPointsRoyale</c> y <c>TicketPointsRoyaleInfinite</c>).
/// Topes de «Máximo»: puntos 310.000 (Royale) y 50.000 (infinita), los de los botones de PKHeX (<c>SAV_Trainer9a.cs:28-29</c>). <c>Wins</c> y <c>PrizeMedals</c> son u32 sin tope en el Core ni en ninguna otra fuente (investigación §8, NO CONFIRMADO):
/// se pueden leer y escribir, pero «Máximo» NO los toca. «Ir al jefe» (Taunie/Urbain tras la 15.ª partida de recompensa): no se sabe si <c>Wins</c> es esa cuenta; no se ofrece.
/// </summary>
public sealed class Royale9a : InstalacionDesafio
{
    public const uint PuntosRoyaleTope = 310_000;
    public const uint PuntosInfinitaTope = 50_000;
    public static Royale9a Instancia { get; } = new();
    private Royale9a() : base("royale9a", Textos.Doble("inst.9a.royale"), Textos.Doble("juego.za")) { }

    public override bool Aplica(SaveFile sav) => sav is SAV9ZA;

    private ModoDesafio[]? _modos;

    public override IReadOnlyList<ModoDesafio> Modos(SaveFile sav) => _modos ??=
    [
        new ModoDesafio("infinita", Textos.Doble("modo.royale_infinita"), NivelDesafio.NoAplica,
        [
            Campo("victorias", "campo.victorias", RolCampo.RachaActual, null, s => ((SAV9ZA)s).InfiniteRoyale.Wins, (s, v) => ((SAV9ZA)s).InfiniteRoyale.Wins = (uint)Math.Clamp(v, 0, uint.MaxValue)),
            Campo("medallas_premio", "campo.medallas_premio", RolCampo.Otro, null, s => ((SAV9ZA)s).InfiniteRoyale.PrizeMedals, (s, v) => ((SAV9ZA)s).InfiniteRoyale.PrizeMedals = (uint)Math.Clamp(v, 0, uint.MaxValue)),
            Campo("puntos", "campo.puntos_entrada", RolCampo.Otro, PuntosInfinitaTope, s => ((SAV9ZA)s).TicketPointsRoyaleInfinite, (s, v) => ((SAV9ZA)s).TicketPointsRoyaleInfinite = (uint)Math.Clamp(v, 0, uint.MaxValue)),
        ]),
        new ModoDesafio("normal", Textos.Doble("modo.royale"), NivelDesafio.NoAplica,
        [
            Campo("puntos", "campo.puntos_entrada", RolCampo.Otro, PuntosRoyaleTope, s => ((SAV9ZA)s).TicketPointsRoyale, (s, v) => ((SAV9ZA)s).TicketPointsRoyale = (uint)Math.Clamp(v, 0, uint.MaxValue)),
        ]),
    ];
}
