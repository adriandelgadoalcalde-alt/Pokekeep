// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Buffers.Binary;
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
/// El orden de las casillas está verificado en el código de X (investigación §15): índice 0 = actual normal, 1 = actual Súper, 2 = récord normal, 3 = récord Súper, como en los
/// formularios de PKHeX (<c>SAV_Trainer.cs:30-36</c>). La racha que lee el juego es <c>bloque + 0x1C0 + 8·estilo + 2·súper</c> (X <c>0x42fa5c</c>; el objeto del juego lleva 4 bytes
/// de cabecera delante del bloque) y el récord, 4 bytes después (<c>0x42fb84</c>).
/// Tope «Máximo» 9999: el del juego. Sumar una victoria (X <c>0x42f90c</c>) no pasa de 9999 y después sube el récord si la racha lo supera.
/// «Ir al jefe»: verificado en el código. Al elegir rival (X <c>0x1d1b70</c>, Zafiro Alfa <c>0x1e5580</c>) el juego lee la racha actual de ese estilo y, si vale 49 en Súper o 19 en
/// normal, saca a la Dama Batalla, en los cinco estilos. La racha se guarda tras cualquier combate, así que el combate siguiente es el 20.º o el 50.º.
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
/// Tope «Máximo» 9999: el del juego. <c>BattleInstSave::GetCountMax</c> devuelve 9999 (Sol <c>0x43afac</c>, Ultraluna <c>0x45072c</c>) y la suma de una victoria
/// (Sol <c>0x43aef4</c>) no pasa de ahí; después sube el récord si la racha lo supera. Los formularios de PKHeX dejan 65535 (u16), un valor al que el juego nunca llega (investigación §14).
/// «Ir al jefe»: verificado en el código (investigación §14). <c>BattleInst::SelectTrainer</c> (Sol <c>0x158070</c>) lee la racha actual guardada de ese tipo y rango
/// (<c>GetWinCount</c>, la de PKHeX con <c>max = false</c>): con 19 en normal sale Red (Individual), Blue (Dobles) o los dos (Multi); con 49 en Súper, igual. Ultraluna compara
/// los mismos 19 y 49 (<c>0x159f1c</c>/<c>0x159f30</c>). La racha se guarda tras cualquier combate, así que el combate siguiente es el 20.º o el 50.º.
/// </summary>
public sealed class Arbol7 : InstalacionDesafio
{
    public const int Tope = 9999;
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
/// Torre Batalla de Espada y Escudo: puntos de torre, racha y victorias de Individual y Dobles (bloques por clave de <see cref="SaveBlockAccessor8SWSH"/>).
/// Los puntos de torre <c>r</c> (byte, 0..33; individuales <c>0x439116A4</c>, dobles <c>0xEE7B2ABD</c>) deciden la bola y el jefe; a su lado, el máximo alcanzado (<c>0xCE4AC6FA</c>/<c>0x29E026C7</c>)
/// y el contador de victorias en la Master Ball (<c>0x0E411743</c>/<c>0xE2950DA8</c>, cuenta de 1 a 10). PKHeX no los expone: se leen por su clave (investigación §12).
/// Topes «Máximo»: puntos y máximo 33, racha 300 y victorias 9.999.999 (los del código del juego, §12). Las victorias también se guardan en el registro del jugador (<c>SAV_Trainer8.cs:141-142</c>): se mantienen en paso.
/// «Ir al jefe»: verificado en el código (§12). <c>IsNextTowerBossBattle</c> es verdadero con <c>r</c> = 5, 14, 26 y 32 y, en la Master Ball (<c>r</c> = 33), con el contador a 10. La racha y las
/// victorias no las lee. Plata = el siguiente jefe de subida de bola (5, 14 o 26), sin bajar nunca los puntos; oro = el paso a la Master Ball (32); repetido = el de cada diez victorias en la Master Ball.
/// Comprobado jugando en Eden con la partida autorizada de Espada (15-nov-2026, informe emulador-torres8): con <c>r</c> = 5 y con <c>r</c> = 33 y el contador a 10 el primer
/// combate es contra Leon («Lionel» en español); con <c>r</c> = 8, un entrenador normal.
/// </summary>
public sealed class Torre8 : InstalacionDesafio
{
    public const int RachaTope = 300;
    public const uint VictoriasTope = 9_999_999;
    /// <summary>Puntos de torre de la Master Ball: el tope del juego.</summary>
    public const int PuntosTope = 33;
    /// <summary>Puntos con los que el combate siguiente es el de un jefe de subida de bola (<c>IsNextTowerBossBattle</c>).</summary>
    public static IReadOnlyList<int> PuntosJefePlata { get; } = [5, 14, 26];
    public const int PuntosJefeOro = 32;
    /// <summary>En la Master Ball, el jefe sale cuando el contador llega a 10.</summary>
    public const int ContadorJefeMaster = 10;

    public static Torre8 Instancia { get; } = new();
    private Torre8() : base("torre8", Textos.Doble("inst.8.torre"), Textos.Doble("juego.swsh")) { }

    public override bool Aplica(SaveFile sav) => sav is SAV8SWSH;

    private ModoDesafio[]? _modos;

    public override IReadOnlyList<ModoDesafio> Modos(SaveFile sav) => _modos ??=
    [
        Crear("individual", SaveBlockAccessor8SWSH.KBattleTowerSinglesStreak, SaveBlockAccessor8SWSH.KBattleTowerSinglesVictory, RecordLists.G8BattleTowerSingleWin, 0x439116A4, 0xCE4AC6FA, 0x0E411743),
        Crear("dobles", SaveBlockAccessor8SWSH.KBattleTowerDoublesStreak, SaveBlockAccessor8SWSH.KBattleTowerDoublesVictory, RecordLists.G8BattleTowerDoubleWin, 0xEE7B2ABD, 0x29E026C7, 0xE2950DA8),
    ];

    private static CampoDesafio Byte(string clave, string claveTexto, long? tope, uint bloque) =>
        Campo(clave, claveTexto, RolCampo.Otro, tope, s => ((SAV8SWSH)s).GetValue<byte>(bloque), (s, v) => ((SAV8SWSH)s).SetValue(bloque, (byte)Math.Clamp(v, 0, tope ?? byte.MaxValue)));

    private static ModoDesafio Crear(string clave, uint claveRacha, uint claveVictorias, int registro, uint clavePuntos, uint claveMaximo, uint claveContador) =>
        new(clave, Textos.Doble("modo." + clave), NivelDesafio.NoAplica,
        [
            Byte("puntos", "campo.puntos_torre", PuntosTope, clavePuntos),
            Byte("puntos_max", "campo.puntos_torre_max", PuntosTope, claveMaximo),
            Byte("contador_master", "campo.contador_master", null, claveContador),
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

    internal override PlanJefe Planificar(SaveFile sav, ModoDesafio modo, JefeDesafio jefe)
    {
        var r = (int)modo.Campo("puntos").Leer(sav);
        int destino, contador = 0;
        switch (jefe)
        {
            case JefeDesafio.Plata:
                var siguiente = PuntosJefePlata.FirstOrDefault(p => p >= r);
                if (siguiente == 0)
                    return PlanJefe.No("motivo.galar_plata_pasada");
                destino = siguiente;
                break;
            case JefeDesafio.Oro:
                if (r > PuntosJefeOro)
                    return PlanJefe.No("motivo.galar_ya_en_master");
                destino = PuntosJefeOro;
                break;
            default:
                destino = PuntosTope;
                contador = ContadorJefeMaster;
                break;
        }
        var valores = new Dictionary<string, long>
        {
            ["puntos"] = destino,
            ["puntos_max"] = Math.Max(modo.Campo("puntos_max").Leer(sav), destino),
            ["contador_master"] = contador,
            // El jefe no las lee; se dejan coherentes con los puntos (cada victoria suma 1 a todo, §12).
            ["actual"] = Math.Max(modo.Campo("actual").Leer(sav), destino),
            ["victorias"] = Math.Max(modo.Campo("victorias").Leer(sav), destino),
        };
        return PlanJefe.Si(jefe, valores, "explic.galar_por_puntos");
    }
}

/// <summary>
/// Torre Batalla de Diamante Brillante y Perla Reluciente (<see cref="BattleTowerWork8b"/>): cuatro clases con racha actual (<c>RenshouCount</c>), racha anterior (<c>RenshouCountOld</c>), victorias totales,
/// rango, ronda (<c>tower_round</c>, +0x18, no la expone PKHeX) y partida suspendida (<c>suspension_flag</c>, +0x01), más dos datos generales (<c>MasterRankMax</c> y <c>ChallengeCount</c>).
/// «Máximo»: racha, racha anterior y victorias a 9999, el <c>MAX_WIN_COUNT</c> del código del juego (investigación §12). Rango, ronda, suspensión, <c>MasterRankMax</c> y <c>ChallengeCount</c> no los toca.
/// «Ir al jefe»: el script <c>ev_tower_rooms_if_leader_battle</c> saca a Palmer en el modo 0 (Individual normal) cuando <c>renshou_cnt</c> vale 20 o 48 (VERIFICADO, §12), y la tabla de rivales tiene su fila
/// en el rango 3 y el 7, ronda 7. Pokekeep deja la clase como al volver al vestíbulo tras ganar dos series (rango 3, racha 14) o seis (rango 7, racha 42), ronda 0 y sin suspender: así el séptimo
/// combate de la serie siguiente es el de Palmer sin tocar el estado de una partida suspendida. Comprobado jugando en Eden con la partida autorizada (15-nov-2026, informe emulador-dbpr): con este estado, seis rivales normales y Palmer
/// en el 7.º combate de Individual normal; con la racha en 19 el siguiente rival es normal y el de después, Palmer.
/// </summary>
public sealed class Torre8b : InstalacionDesafio
{
    public const int Tope = 9999;
    /// <summary>Bandera de evento <c>FE_BTLTW_N_SINGLE_SUSPENSION</c> (Individual normal suspendida), la que mira el vestíbulo para ofrecer «continuar».</summary>
    public const int BanderaSuspensionIndividual = 1421;
    private const int InicioClases = 20;

    public static Torre8b Instancia { get; } = new();
    private Torre8b() : base("torre8b", Textos.Doble("inst.8b.torre"), Textos.Doble("juego.bdsp")) { }

    public override bool Aplica(SaveFile sav) => sav is SAV8BS;

    private ModoDesafio[]? _modos;

    private static Span<byte> Bruto(SaveFile s, int idx) => ((SAV8BS)s).BattleTower.Data.Slice(InicioClases + (idx * BattleTowerClassData8b.SIZE), BattleTowerClassData8b.SIZE);

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
                Campo("ronda", "campo.ronda", RolCampo.Otro, null, s => BinaryPrimitives.ReadUInt32LittleEndian(Bruto(s, c)[0x18..]),
                    (s, v) => BinaryPrimitives.WriteUInt32LittleEndian(Bruto(s, c)[0x18..], (uint)Math.Clamp(v, 0, 10))),
                Campo("suspendida", "campo.suspendida", RolCampo.Bandera, null, s => Bruto(s, c)[0x01],
                    (s, v) =>
                    {
                        Bruto(s, c)[0x01] = v != 0 ? (byte)1 : (byte)0;
                        if (c == 0)
                            ((SAV8BS)s).FlagWork.SetFlag(BanderaSuspensionIndividual, v != 0);
                    }),
            ]));
        }
        lista.Add(new ModoDesafio("general", Textos.Doble("modo.general"), NivelDesafio.NoAplica,
        [
            Campo("rango_maestro", "campo.rango_maestro", RolCampo.Otro, null, s => ((SAV8BS)s).BattleTower.MasterRankMax, (s, v) => ((SAV8BS)s).BattleTower.MasterRankMax = (int)Math.Clamp(v, 0, int.MaxValue)),
            Campo("retos", "campo.retos", RolCampo.Otro, null, s => ((SAV8BS)s).BattleTower.ChallengeCount, (s, v) => ((SAV8BS)s).BattleTower.ChallengeCount = (uint)Math.Clamp(v, 0, uint.MaxValue)),
        ]));
        return _modos = [.. lista];
    }

    internal override PlanJefe Planificar(SaveFile sav, ModoDesafio modo, JefeDesafio jefe)
    {
        if (modo.Clave != "clase1")
            return PlanJefe.No(modo.Clave == "general" ? "motivo.dbpr_general_sin_jefe" : "motivo.dbpr_solo_individual_normal");
        if (jefe == JefeDesafio.Repetido)
            return PlanJefe.No("motivo.dbpr_maestra_sin_comprobar");
        var (rango, racha) = jefe == JefeDesafio.Oro ? (7, 42) : (3, 14);
        if (modo.Campo("rango").Leer(sav) > rango)
            return PlanJefe.No(jefe == JefeDesafio.Oro ? "motivo.dbpr_oro_pasado" : "motivo.dbpr_plata_pasada");
        var valores = new Dictionary<string, long>
        {
            ["rango"] = rango,
            ["actual"] = racha,
            ["ronda"] = 0,
            ["suspendida"] = 0,
            ["victorias"] = Math.Max(modo.Campo("victorias").Leer(sav), racha),
        };
        return PlanJefe.Si(jefe, valores, "explic.dbpr_palmer");
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
/// Royale Infinita de Z-A: lo que tiene nombre en el Core. <see cref="InfiniteRoyale9a"/> (<c>Wins</c> y <c>PrizeMedals</c>; <c>Value0/1</c> son el contador de identificadores de carta, <c>Value4</c> una bandera y <c>Value5</c> relleno; investigación §12.3, y no se tocan) y los puntos de entrada de las dos Royales
/// (<c>SAV9ZA.TicketPointsRoyale</c> y <c>TicketPointsRoyaleInfinite</c>).
/// Topes de «Máximo»: puntos 310.000 (Royale) y 50.000 (infinita), los de los botones de PKHeX (<c>SAV_Trainer9a.cs:28-29</c>). <c>Wins</c> 99 y <c>PrizeMedals</c> 9999, los del código de Z-A
/// (investigación §12.3): <c>Wins</c> son las victorias de la sesión de Royale, que el juego acota a 99 (setter <c>0x48ace0</c>), y las medallas, a <c>medal_stock_max</c> (<c>AddMedals</c> <c>0xb3b42c</c>).
/// «Ir al jefe»: no aplica. El combate de Taunie/Urbain depende de la fase de la historia (<c>SYS_WORK_PHASE</c>) y de la cuenta de combates de recompensa, no de una racha (§12.3).
/// </summary>
public sealed class Royale9a : InstalacionDesafio
{
    public const uint PuntosRoyaleTope = 310_000;
    public const uint PuntosInfinitaTope = 50_000;
    public const uint VictoriasTope = 99;
    public const uint MedallasTope = 9999;
    public static Royale9a Instancia { get; } = new();
    private Royale9a() : base("royale9a", Textos.Doble("inst.9a.royale"), Textos.Doble("juego.za")) { }

    public override bool Aplica(SaveFile sav) => sav is SAV9ZA;

    internal override PlanJefe Planificar(SaveFile sav, ModoDesafio modo, JefeDesafio jefe) => PlanJefe.No("motivo.royale_por_historia");

    private ModoDesafio[]? _modos;

    public override IReadOnlyList<ModoDesafio> Modos(SaveFile sav) => _modos ??=
    [
        new ModoDesafio("infinita", Textos.Doble("modo.royale_infinita"), NivelDesafio.NoAplica,
        [
            Campo("victorias", "campo.victorias", RolCampo.RachaActual, VictoriasTope, s => ((SAV9ZA)s).InfiniteRoyale.Wins, (s, v) => ((SAV9ZA)s).InfiniteRoyale.Wins = (uint)Math.Clamp(v, 0, VictoriasTope)),
            Campo("medallas_premio", "campo.medallas_premio", RolCampo.Otro, MedallasTope, s => ((SAV9ZA)s).InfiniteRoyale.PrizeMedals, (s, v) => ((SAV9ZA)s).InfiniteRoyale.PrizeMedals = (uint)Math.Clamp(v, 0, MedallasTope)),
            Campo("puntos", "campo.puntos_entrada", RolCampo.Otro, PuntosInfinitaTope, s => ((SAV9ZA)s).TicketPointsRoyaleInfinite, (s, v) => ((SAV9ZA)s).TicketPointsRoyaleInfinite = (uint)Math.Clamp(v, 0, uint.MaxValue)),
        ]),
        new ModoDesafio("normal", Textos.Doble("modo.royale"), NivelDesafio.NoAplica,
        [
            Campo("puntos", "campo.puntos_entrada", RolCampo.Otro, PuntosRoyaleTope, s => ((SAV9ZA)s).TicketPointsRoyale, (s, v) => ((SAV9ZA)s).TicketPointsRoyale = (uint)Math.Clamp(v, 0, uint.MaxValue)),
        ]),
    ];
}
