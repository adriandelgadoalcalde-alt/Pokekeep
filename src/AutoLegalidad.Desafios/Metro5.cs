// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;

namespace AutoLegalidad.Desafios;

/// <summary>
/// Metro Batalla de la 5.ª generación (<see cref="BattleSubway5"/>): ocho modos, cuatro de tren normal y cuatro de tren Super.
/// Cada modo guarda la racha («Past», la actual o la última), el récord y la serie («Set»).
///
/// FUENTES (investigación §4: <c>BattleSubway5.cs</c>, <c>SAV_Misc5.cs:670-745</c> y pruebas en partida de la comunidad, Project Pokémon hilo 53662, mensajes de EvM5_ y Saternoir):
/// <list type="bullet">
/// <item>Regla de continuar: <c>Serie = Past / 7 + 1</c> (<c>SAV_Misc5.cs:692-707</c> y <c>SaveSubway</c>, <c>SAV_Misc5.cs:792-800</c>).</item>
/// <item>Primer jefe (Ingo en Individual y Super Individual, Emmet en Dobles; los dos en Multi): Past = 14 y Serie = 3; el jefe sale en el combate 21 de la serie siguiente. Probado en partida (hilo 53662) en Individual.</item>
/// <item>Segundo jefe, solo en tren Super: Past = 42 (0x2A) y Serie = 7; sale en el combate 49. Probado en partida en Super Individual.</item>
/// <item>Dobles y Multi: verificado en el código (investigación §16). La función que elige rival (Negro <c>ov21 0x21cbd50</c>, Negro 2 <c>ov33 0x217c110</c>) es la misma para los nueve modos:
/// en el 7.º combate del tren saca al jefe si la serie (<c>racha / 7</c>) vale 2 en un tren normal o 6 en uno Super, y elige su ID (0x132-0x139) según el modo. En Multi usa la serie mayor de los dos jugadores.</item>
/// <item>El tren normal termina en el combate 21: no tiene segundo jefe. El tren Super tiene que estar desbloqueado (<c>BattleSubway5.cs:12-15</c>, bits 4, 5 y 6 de <c>Flags</c>).</item>
/// <item>Tope «Máximo» (1.1.5): <see cref="TopesJuego.MetroRacha"/> (65.535). Antes era 9999, el máximo de los formularios de PKHeX (<c>SAV_Misc5.Designer.cs:1735,1955,1976</c>); el motor del juego cuenta la racha en un u16 y solo para en 0xFFFF
/// (B2W2 ov33 <c>0x0217BD4C</c>, BW ov21 <c>0x021CA1FC</c>). La serie derivada con 65.535 vale 9.363.</item>
/// </list>
/// </summary>
public sealed class Metro5 : InstalacionDesafio
{
    public const int RachaMaxima = TopesJuego.MetroRacha;

    private Metro5() : base("metro5", Textos.Doble("inst.5.metro"), Textos.Doble("juego.gen5")) { }

    public static Metro5 Instancia { get; } = new();

    public override bool Aplica(SaveFile sav) => sav is SAV5;

    private sealed record Def(string Clave, bool Super, string Modo, Func<BattleSubway5, int> GetPast, Action<BattleSubway5, int> SetPast,
        Func<BattleSubway5, int> GetRecord, Action<BattleSubway5, int> SetRecord, Func<BattleSubway5, int> GetSet, Action<BattleSubway5, int> SetSet,
        Func<BattleSubway5, bool>? GetSuper, Action<BattleSubway5, bool>? SetSuper);

    private static readonly Def[] Defs =
    [
        new("normal.individual", false, "individual", b => b.SinglePast, (b, v) => b.SinglePast = v, b => b.SingleRecord, (b, v) => b.SingleRecord = v, b => b.SingleSet, (b, v) => b.SingleSet = v, null, null),
        new("normal.dobles", false, "dobles", b => b.DoublePast, (b, v) => b.DoublePast = v, b => b.DoubleRecord, (b, v) => b.DoubleRecord = v, b => b.DoubleSet, (b, v) => b.DoubleSet = v, null, null),
        new("normal.multi_npc", false, "multi_npc", b => b.MultiNPCPast, (b, v) => b.MultiNPCPast = v, b => b.MultiNPCRecord, (b, v) => b.MultiNPCRecord = v, b => b.MultiNPCSet, (b, v) => b.MultiNPCSet = v, null, null),
        new("normal.multi_amigo", false, "multi_amigo", b => b.MultiFriendsPast, (b, v) => b.MultiFriendsPast = v, b => b.MultiFriendsRecord, (b, v) => b.MultiFriendsRecord = v, b => b.MultiFriendsSet, (b, v) => b.MultiFriendsSet = v, null, null),
        new("super.individual", true, "individual", b => b.SuperSinglePast, (b, v) => b.SuperSinglePast = v, b => b.SuperSingleRecord, (b, v) => b.SuperSingleRecord = v, b => b.SuperSingleSet, (b, v) => b.SuperSingleSet = v, b => b.SuperSingle, (b, v) => b.SuperSingle = v),
        new("super.dobles", true, "dobles", b => b.SuperDoublePast, (b, v) => b.SuperDoublePast = v, b => b.SuperDoubleRecord, (b, v) => b.SuperDoubleRecord = v, b => b.SuperDoubleSet, (b, v) => b.SuperDoubleSet = v, b => b.SuperDouble, (b, v) => b.SuperDouble = v),
        new("super.multi_npc", true, "multi_npc", b => b.SuperMultiNPCPast, (b, v) => b.SuperMultiNPCPast = v, b => b.SuperMultiNPCRecord, (b, v) => b.SuperMultiNPCRecord = v, b => b.SuperMultiNPCSet, (b, v) => b.SuperMultiNPCSet = v, b => b.SuperMulti, (b, v) => b.SuperMulti = v),
        new("super.multi_amigo", true, "multi_amigo", b => b.SuperMultiFriendsPast, (b, v) => b.SuperMultiFriendsPast = v, b => b.SuperMultiFriendsRecord, (b, v) => b.SuperMultiFriendsRecord = v, b => b.SuperMultiFriendsSet, (b, v) => b.SuperMultiFriendsSet = v, b => b.SuperMulti, (b, v) => b.SuperMulti = v),
    ];

    private ModoDesafio[]? _modos;

    public override IReadOnlyList<ModoDesafio> Modos(SaveFile sav) => _modos ??= [.. Defs.Select(Construir)];

    private static ModoDesafio Construir(Def d)
    {
        static BattleSubway5 B(SaveFile s) => ((SAV5)s).BattleSubway;
        var campos = new List<CampoDesafio>
        {
            Campo("actual", "campo.racha_actual", RolCampo.RachaActual, RachaMaxima, s => d.GetPast(B(s)), (s, v) => d.SetPast(B(s), (int)Math.Clamp(v, 0, ushort.MaxValue))),
            Campo("record", "campo.record_racha", RolCampo.Record, RachaMaxima, s => d.GetRecord(B(s)), (s, v) => d.SetRecord(B(s), (int)Math.Clamp(v, 0, ushort.MaxValue))),
            // La serie no tiene «Máximo» propio: se deriva de la racha (Serie = Past / 7 + 1). Su tope es el del juego, 65.534 (TopesJuego.MetroSerie).
            Campo("serie", "campo.serie", RolCampo.Contador, null, s => d.GetSet(B(s)), (s, v) => d.SetSet(B(s), (int)Math.Clamp(v, 0, TopesJuego.MetroSerie)), TopesJuego.MetroSerie),
        };
        if (d.GetSuper is { } gs && d.SetSuper is { } ss)
            campos.Add(Campo("super_desbloqueado", "campo.super_desbloqueado", RolCampo.Bandera, 1, s => gs(B(s)) ? 1 : 0, (s, v) => ss(B(s), v != 0)));
        var tren = Textos.Doble(d.Super ? "modo.tren_super" : "modo.tren_normal");
        return new ModoDesafio(d.Clave, Frente3Esmeralda.Combinar(tren, Textos.Doble("modo." + d.Modo)), NivelDesafio.NoAplica, campos);
    }

    protected override void Coherencia(SaveFile sav, ModoDesafio modo)
    {
        modo.Campo("serie").Escribir(sav, modo.Campo("actual").Leer(sav) / 7 + 1);
    }

    internal override PlanJefe Planificar(SaveFile sav, ModoDesafio modo, JefeDesafio jefe)
    {
        var esSuper = modo.Clave.StartsWith("super.", StringComparison.Ordinal);
        // Individual (Ingo), Dobles (Emmet) y Multi (los dos) tienen el jefe en el combate 21 del tren normal y en el 49 del Super (Bulbapedia, «Battle Subway»,
        // apartado de los jefes, consultado el 14-nov-2026; Individual, además, comprobado en partida, investigación §4). El juego obliga a guardar entre trenes
        // de 7 combates, así que el punto de guardado anterior al jefe es el final del tren 2 (14) o del 6 (42).
        if (jefe == JefeDesafio.Repetido)
            return PlanJefe.No("motivo.sin_repeticion");
        if (!esSuper && jefe == JefeDesafio.Oro)
            return PlanJefe.No("motivo.metro_normal_un_jefe");
        if (esSuper && jefe == JefeDesafio.Plata)
            return PlanJefe.No("motivo.metro_super_solo_oro");

        long past = esSuper ? 42 : 14;
        var valores = new Dictionary<string, long>
        {
            ["actual"] = past,
            ["record"] = Math.Max(modo.Campo("record").Leer(sav), past),
            ["serie"] = past / 7 + 1, // 3 y 7
        };
        if (esSuper)
            valores["super_desbloqueado"] = 1; // el tren Super tiene que estar desbloqueado (BattleSubway5.cs:12-15; Multi NPC y Multi amigo comparten el bit 6)
        return PlanJefe.Si(esSuper ? JefeDesafio.Oro : JefeDesafio.Plata, valores);
    }
}

/// <summary>
/// Puntos de Batalla del Metro Batalla de la 5.ª generación (<see cref="BattleSubway5.BP"/>, u16 al inicio del bloque).
/// Tope 9999: el de los PB en PKHeX (<c>SAV_Misc4.cs:123</c>, <c>Math.Clamp(SAV.BP, 0, 9999)</c>, la 4.ª generación; en la 5.ª no hay formulario para este campo). Tope del juego en la 5.ª: NO CONFIRMADO.
/// </summary>
public sealed class PuntosMetro5 : InstalacionDesafio
{
    public const int Maximo9999 = 9999;
    public static PuntosMetro5 Instancia { get; } = new();
    private PuntosMetro5() : base("metro5.pb", Textos.Doble("inst.5.metro_pb"), Textos.Doble("juego.gen5")) { }

    public override bool Aplica(SaveFile sav) => sav is SAV5;

    private ModoDesafio[]? _modos;

    public override IReadOnlyList<ModoDesafio> Modos(SaveFile sav) => _modos ??=
    [
        new ModoDesafio("pb", Textos.Doble("modo.puntos"), NivelDesafio.NoAplica,
        [
            Campo("pb", "campo.pb", RolCampo.Otro, Maximo9999, s => ((SAV5)s).BattleSubway.BP, (s, v) => ((SAV5)s).BattleSubway.BP = (int)Math.Clamp(v, 0, ushort.MaxValue)),
        ]),
    ];
}
