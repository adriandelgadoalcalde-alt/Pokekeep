// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using static PKHeX.Core.BattleFrontierFacility3;
using static PKHeX.Core.BattleFrontierStatType3;

namespace AutoLegalidad.Desafios;

/// <summary>
/// Una instalación de la Frontera de Batalla de Esmeralda (<see cref="BattleFrontier3"/>): Torre, Domo, Palacio, Arena, Fábrica, Foso o Pirámide.
/// Los datos están en el bloque pequeño de la partida (SaveBlock2 + 0xCDC) y la racha se guarda por separado para Nv. 50 y Abierto.
///
/// FUENTES de cada número (investigación <c>Pokekeep-trabajo\misiones\desafios-investigacion.md</c> §1, que cita la decompilación pret de pokeemerald):
/// <list type="bullet">
/// <item>Tope «Máximo» 9999: <c>pokeemerald/include/constants/battle_frontier.h:49</c> (<c>MAX_STREAK</c>); <c>IncrementWinStreak</c>, <c>src/frontier_util.c:2111-2160</c>. PKHeX recorta igual: <c>SAV_Misc3.cs:393</c>.</item>
/// <item>Apariciones del jefe: <c>sFrontierBrainStreakAppearances</c>, <c>src/frontier_util.c:86-95</c>; decisión en <c>GetFrontierBrainStatus</c>, <c>frontier_util.c:1656-1693</c> (solo modo Individual; con 0 símbolos sale la plata, con 1 el oro, con 2 se repiten).</item>
/// <item>La racha solo se conserva si su bandera está activa (<c>winStreakActiveFlags</c>): <c>battle_tower.c:916-917</c>, <c>battle_dome.c:2156-2157</c>, <c>battle_palace.c:93</c>, <c>battle_factory.c:208</c>, <c>battle_arena.c:672-677</c>, <c>battle_pike.c:1621</c>, <c>battle_pyramid.c:849-851</c>; bit por modo y nivel: <c>BattleFrontier3.GetContinueBitPosition</c> (<c>BattleFrontier3.cs:150-196</c>).</item>
/// <item>Valores de «Ir al jefe» (investigación §1.4, racha almacenada alineada a la ronda de 7): Torre 28 / 63 (<c>BattleTowerBattleRoom/scripts.inc:22-66</c>), Domo 4 / 9 (<c>battle_dome.c:2478-2498</c>), Palacio 14 / 35 (<c>PalaceBattleRoom/scripts.inc:43-86</c>),
/// Arena 21 / 49 (<c>ArenaBattleRoom/scripts.inc:103-125</c>), Fábrica 14 / 35 (<c>FactoryBattleRoom/scripts.inc:19-63</c>), Foso 14 / 126 (<c>battle_pike.c:1501-1525</c>: Lucy sale cuando racha + sala = 27 o 139, y la racha sube una por sala, <c>:628-656</c>, así que con 14 o 126 al empezar cae dentro de ese reto de 14 salas; leído el 14-nov-2026) y Pirámide 14 / 63 (<c>PyramidTop/scripts.inc:27</c>).</item>
/// <item>Repetición del jefe una vez que se tienen los dos símbolos: columna «repetición» de <c>sFrontierBrainStreakAppearances</c> (Torre 35, Domo 5, Palacio 21, Arena 28, Fábrica 21, Foso 56, Pirámide 35).</item>
/// </list>
/// </summary>
public sealed class Frente3Esmeralda : InstalacionDesafio
{
    /// <summary>Máximo del juego para una racha: <c>MAX_STREAK</c> (battle_frontier.h:49).</summary>
    public const int RachaMaxima = 9999;

    private readonly BattleFrontierFacility3 _f;
    private readonly (int Plata, int Oro, int Repeticion) _jefe;
    private ModoDesafio[]? _modos;

    private Frente3Esmeralda(BattleFrontierFacility3 f, string clave, string claveNombre, (int, int, int) jefe)
        : base(clave, Textos.Doble(claveNombre), Textos.Doble("juego.esmeralda"))
    {
        _f = f;
        _jefe = jefe;
    }

    /// <summary>Las siete instalaciones. Los números de <c>jefe</c> son (plata, oro, repetición) de la racha almacenada (ver el resumen de la clase).</summary>
    public static IReadOnlyList<Frente3Esmeralda> Todas { get; } =
    [
        new(Tower, "esmeralda.torre", "inst.e.torre", (28, 63, 35)),
        new(Dome, "esmeralda.domo", "inst.e.domo", (4, 9, 5)),
        new(Palace, "esmeralda.palacio", "inst.e.palacio", (14, 35, 21)),
        new(Arena, "esmeralda.arena", "inst.e.arena", (21, 49, 28)),
        new(Factory, "esmeralda.fabrica", "inst.e.fabrica", (14, 35, 21)),
        new(Pike, "esmeralda.foso", "inst.e.foso", (14, 126, 56)),
        new(Pyramid, "esmeralda.piramide", "inst.e.piramide", (14, 63, 35)),
    ];

    public BattleFrontierFacility3 Facility => _f;

    public override bool Aplica(SaveFile sav) => sav is SAV3E;

    private static BattleFrontier3 Marco(SaveFile s) => ((SAV3E)s).SmallBlock.BattleFrontier;

    public override IReadOnlyList<ModoDesafio> Modos(SaveFile sav) => _modos ??= Construir();

    private ModoDesafio[] Construir()
    {
        var lista = new List<ModoDesafio>();
        var n = BattleFrontier3.GetModeCount(_f);
        string[] nombres = ["individual", "dobles", "multi", "multi_enlace"];
        for (var m = 0; m < n; m++)
        {
            for (var l = 0; l < 2; l++)
            {
                var modo = (BattleFrontierBattleMode3)m;
                var nivel = (BattleFrontierRecordType3)l;
                var campos = new List<CampoDesafio>();
                foreach (var stat in BattleFrontier3.GetValidStats(_f))
                {
                    var s = stat;
                    var (clave, texto, rol) = s switch
                    {
                        CurrentStreak => ("actual", "campo.racha_actual", RolCampo.RachaActual),
                        RecordStreak => ("record", "campo.record_racha", RolCampo.Record),
                        Championships => ("campeonatos", "campo.campeonatos", RolCampo.Otro),
                        RecordCleared => ("despejado", "campo.despejado", RolCampo.Otro),
                        CurrentSwapped => ("canjes_actual", "campo.canjes_actual", RolCampo.Otro),
                        RecordSwapped => ("canjes_record", "campo.canjes_record", RolCampo.Otro),
                        _ => throw new InvalidOperationException(s.ToString()),
                    };
                    // El formulario de PKHeX recorta todas las estadisticas del Frente a 9999 (SAV_Misc3.cs:393).
                    campos.Add(Campo(clave, texto, rol, RachaMaxima,
                        sav => Marco(sav).GetStat(_f, modo, nivel, s),
                        (sav, v) => { var bf = Marco(sav); bf.SetStat(_f, modo, nivel, s, (ushort)Math.Clamp(v, 0, ushort.MaxValue)); }));
                }
                // Bandera de «continuar la racha» (winStreakActiveFlags): sin ella el juego pone la racha a 0 al empezar un reto.
                campos.Add(Campo("continuar", "campo.continuar", RolCampo.Bandera, 1,
                    sav => Marco(sav).GetContinueFlag(_f, modo, nivel) ? 1 : 0,
                    (sav, v) => { var bf = Marco(sav); bf.SetContinueFlag(_f, modo, nivel, v != 0); }));
                lista.Add(new ModoDesafio($"{nombres[m]}.{(l == 0 ? "nv50" : "abierto")}",
                    Combinar(Textos.Doble("modo." + nombres[m]), Textos.Doble(l == 0 ? "nivel.nv50" : "nivel.abierto")),
                    l == 0 ? NivelDesafio.Nv50 : NivelDesafio.Abierto, campos));
            }
        }
        return [.. lista];
    }

    internal static TextoDoble Combinar(TextoDoble a, TextoDoble b) => new($"{a.Es}, {b.Es}", $"{a.En}, {b.En}");

    /// <summary>Símbolos del jugador en la instalación: 0, 1 o 2 (<c>GetPlayerSymbolCountForFacility</c>, frontier_util.c:1847: suma las banderas de plata y oro).</summary>
    public int Simbolos(SaveFile sav)
    {
        var s = (SAV3E)sav;
        return (s.GetEventFlag(BattleFrontier3.GetSymbolSilverFlagIndex(_f)) ? 1 : 0)
             + (s.GetEventFlag(BattleFrontier3.GetSymbolGoldFlagIndex(_f)) ? 1 : 0);
    }

    internal override PlanJefe Planificar(SaveFile sav, ModoDesafio modo, JefeDesafio jefe)
    {
        // El jefe solo sale en el modo Individual (GetFrontierBrainStatus, frontier_util.c:1665).
        if (!modo.Clave.StartsWith("individual.", StringComparison.Ordinal))
            return PlanJefe.No("motivo.solo_individual");
        var simbolos = Simbolos(sav);
        JefeDesafio efectivo;
        long n;
        string? explicacion = null;
        switch (jefe)
        {
            case JefeDesafio.Plata:
                // 0 símbolos: sale la plata. 2 símbolos: la plata se repite. Con 1 solo, el juego solo mira el oro (frontier_util.c:1675-1679).
                if (simbolos == 1)
                {
                    efectivo = JefeDesafio.Oro;
                    n = _jefe.Oro;
                    explicacion = "expl.plata_ya_tenida_va_oro";
                }
                else
                {
                    efectivo = JefeDesafio.Plata;
                    n = _jefe.Plata;
                    if (simbolos == 2)
                        explicacion = "expl.plata_repetida";
                }
                break;
            case JefeDesafio.Oro:
                if (simbolos == 0)
                    return PlanJefe.No("motivo.falta_simbolo_plata");
                efectivo = JefeDesafio.Oro;
                n = _jefe.Oro;
                if (simbolos == 2)
                    explicacion = "expl.oro_repetido";
                break;
            default:
                // Primera repetición periódica después del oro: oro + repetición (frontier_util.c:1685-1688).
                if (simbolos < 2)
                    return PlanJefe.No("motivo.repetido_requiere_dos_simbolos");
                efectivo = JefeDesafio.Repetido;
                n = _jefe.Oro + _jefe.Repeticion;
                break;
        }

        var record = modo.Campo("record").Leer(sav);
        var valores = new Dictionary<string, long>
        {
            ["actual"] = n,
            ["record"] = Math.Max(record, n),
            ["continuar"] = 1,
        };
        return PlanJefe.Si(efectivo, valores, explicacion);
    }
}
