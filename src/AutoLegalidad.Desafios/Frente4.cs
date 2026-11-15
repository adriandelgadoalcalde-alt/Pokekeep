// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using static System.Buffers.Binary.BinaryPrimitives;

namespace AutoLegalidad.Desafios;

/// <summary>
/// Instalaciones de la Zona de Batalla de la 4.ª generación (Torre, Fábrica, Sala, Castillo y Arcade de Platino y HGSS; solo la Torre en DP).
/// El Core no tiene una estructura para ellas: los desplazamientos son la tabla <c>BFF</c> de <c>PKHeX.WinForms\Subforms\Save Editors\Gen4\SAV_Misc4.cs:46-84</c>,
/// llevada aquí con la misma lógica de lectura y escritura (<c>StatAddrControl</c>, <c>SAV_Misc4.cs:487-545</c>).
///
/// FUENTES (investigación §3, decompilación pret de pokeplatinum y pokeheartgold):
/// <list type="bullet">
/// <item>Tope 9999: las estadísticas se recortan a 9999 en el guardado (<c>pokeplatinum battle_frontier_save.c:72-73,172-173</c>; HGSS Arcade <c>frontier_cmd_arcade.c:221</c>); PKHeX igual (<c>SAV_Misc4.cs:508,531</c>).</item>
/// <item>Torre: <c>roomNum = racha / 7</c> (<c>battle_tower.c:372</c>); PKHeX mantiene esa regla (<c>SAV_Misc4.cs:563-579</c>). Platino lee la racha solo si la bandera de racha activa está puesta (<c>battle_tower.c:355-366</c>, <c>battle_factory.c:57-64</c>, <c>battle_hall.c:62-68</c>).</item>
/// <item>Jefes de Platino (investigación §3.3): Torre Palmer 21.º y 49.º combate, N = 14 / 42 (<c>battle_tower_trainers.c:243-252</c>); Fábrica 14 / 42 (<c>battle_factory_helpers.c:268</c>); Sala 40 / 160 (<c>battle_hall_helpers.c:1170</c>);
/// Castillo y Arcade 14 / 42 (<c>battle_castle_helpers.c:52-62</c>, <c>battle_arcade_helpers.c:49-59</c>; la ronda es racha / 7 y la racha solo se carga con la bandera de racha activa: <c>battle_castle.c:79-92</c>, <c>battle_arcade.c:278-284</c>, leído el 14-nov-2026). HGSS Arcade 14 / 42 (<c>pokeheartgold frontier_cmd_arcade.c:405-413</c>).</item>
/// <item>Jefes de HGSS (pokeheartgold 6bf138d, leído el 14-nov-2026): Torre Palmer solo en Individual, en el 7.º combate de la ronda 2 (plata) o 6 (oro) (<c>src/unk_0204B538.c:214-223</c>); la ronda y la racha se cargan del guardado solo con la bandera de continuar (<c>src/unk_0204A3F4.c:280-292</c>), que es el bit <c>modo+8</c> del u16 en +8 y las rondas en +0xA+2·modo (<c>asm/unk_0202D230.s:549-630</c>), es decir, la bandera y el contador de la tabla BFF. Fábrica Thorton (entrenadores 0x135/0x136) y Castillo Darach (0x139/0x13A) cuando <c>7·ronda + combate + 1</c> es 21 o 49 en Individual (<c>asm/overlay_80_02236B78.s:9-27</c>, <c>asm/overlay_80_02237A70.s:9-27</c>), con ronda = racha / 7 (<c>overlay_80_0222FD08.s:60-100,190-196</c>; <c>overlay_80_022324C4.s:168-172,259-264</c>): N = 14 / 42. Sala Argenta (0x133/0x134) cuando <c>10·ronda + combate + 1</c> es 50 o 170 (<c>overlay_80_022372D8.s:77-96</c>), con ronda = racha / 10 (<c>overlay_80_022310C4.s:280-286</c>): N = 40 / 160. Números de entrenador cotejados con <c>pokeplatinum generated/frontier_trainers.txt:306-315</c>.</item>
/// <item>Cada jefe sale una vez en plata y una en oro: no hay repetición (§3.1).</item>
/// <item>Torre de DP, leída en el binario de Diamante (ES, Rev 5; desensamblado el 15-nov-2026, investigación §13): el rival lo elige la función del overlay 6 en <c>0x2244894</c>, que devuelve
/// 305 (Palmer plata) en Individual con ronda 2 y 7.º rival, y 306 (oro) con ronda 6, junto a las mismas tablas de rivales que Platino (<c>ov6 0x2250d78</c> y <c>0x2250d98</c>, 16/16 valores iguales).
/// Al abrir el reto (<c>ov6 0x224373c-0x2243784</c>) la racha solo se carga con el bit <c>8+modo</c> de las banderas del registro, y el contador de rondas se lee siempre. En el registro, las banderas están
/// en <c>+8</c> (<c>arm9 0x202a5c4</c>) y las rondas en <c>+0xC + 2·modo</c> (<c>arm9 0x202a584</c>; en Platino, <c>+0xA</c>): de ahí el contador en la bandera + 3 de la tabla BFF de DP.</item>
/// </list>
/// </summary>
public sealed class Frente4 : InstalacionDesafio
{
    public const int RachaMaxima = 9999;

    private enum Tipo { Torre, Fabrica, Sala, Castillo, Arcade }

    private sealed record Def(string Clave, string ClaveNombre, Tipo Tipo, int Direccion, int Zancada, int Bandera, int Modos, bool Jefe, int Plata, int Oro, int RondasPlata, int RondasOro);

    private readonly Def _d;
    private readonly Type _juego;
    private ModoDesafio[]? _modos;

    private Frente4(Def d, Type juego, string claveJuego) : base(d.Clave, Textos.Doble(d.ClaveNombre), Textos.Doble(claveJuego))
    {
        _d = d;
        _juego = juego;
    }

    // Tabla BFF de SAV_Misc4.cs:52-77: [direccion, zancada, byte de banderas]. Modos de la Torre: BFT[1] (5); el resto: BFT[0] (3). El Arcade solo tiene Individual (stride 4 en PKHeX).
    private static readonly Def[] DefsDP =
    [
        new("dp.torre", "inst.e.torre", Tipo.Torre, 0x5FCA, 0x04, 0x6601, 5, true, 14, 42, 2, 6), // Palmer en los combates 21 y 49 de Individual: código de Diamante (ov6 0x2244894, rondas 2 y 6), como en Platino; ver la cabecera
    ];

    private static readonly Def[] DefsPt =
    [
        new("pt.torre", "inst.e.torre", Tipo.Torre, 0x68E0, 0x04, 0x723D, 5, true, 14, 42, 2, 6),
        new("pt.fabrica", "inst.e.fabrica", Tipo.Fabrica, 0x68F4, 0x10, 0x7EF8, 3, true, 14, 42, 0, 0),
        new("pt.sala", "inst.4.sala", Tipo.Sala, 0x6924, 0x18, 0x7EFC, 3, true, 40, 160, 0, 0),
        new("pt.castillo", "inst.4.castillo", Tipo.Castillo, 0x696C, 0x10, 0x7F00, 3, true, 14, 42, 0, 0),
        new("pt.arcade", "inst.4.arcade", Tipo.Arcade, 0x699C, 0x04, 0x7F04, 1, true, 14, 42, 0, 0),
    ];

    private static readonly Def[] DefsHgss =
    [
        new("hgss.torre", "inst.e.torre", Tipo.Torre, 0x5264, 0x04, 0x5BC1, 5, true, 14, 42, 2, 6),
        new("hgss.fabrica", "inst.e.fabrica", Tipo.Fabrica, 0x5278, 0x10, 0x687C, 3, true, 14, 42, 0, 0),
        new("hgss.sala", "inst.4.sala", Tipo.Sala, 0x52A8, 0x18, 0x6880, 3, true, 40, 160, 0, 0),
        new("hgss.castillo", "inst.4.castillo", Tipo.Castillo, 0x52F0, 0x10, 0x6884, 3, true, 14, 42, 0, 0),
        new("hgss.arcade", "inst.4.arcade", Tipo.Arcade, 0x5320, 0x04, 0x6888, 1, true, 14, 42, 0, 0),
    ];

    public static IReadOnlyList<Frente4> DePlatino { get; } = [.. DefsPt.Select(d => new Frente4(d, typeof(SAV4Pt), "juego.platino"))];
    public static IReadOnlyList<Frente4> DeHgss { get; } = [.. DefsHgss.Select(d => new Frente4(d, typeof(SAV4HGSS), "juego.hgss"))];
    public static IReadOnlyList<Frente4> DeDp { get; } = [.. DefsDP.Select(d => new Frente4(d, typeof(SAV4DP), "juego.dp"))];

    public override bool Aplica(SaveFile sav) => _juego.IsInstanceOfType(sav);

    public override IReadOnlyList<ModoDesafio> Modos(SaveFile sav) => _modos ??= Construir();

    private ModoDesafio[] Construir()
    {
        var lista = new List<ModoDesafio>();
        string[] tiposTorre = ["individual", "dobles", "multi_entrenador", "multi_amigo", "wifi"];
        string[] tiposResto = ["individual", "dobles", "multi"];
        var abierto = _d.Tipo == Tipo.Fabrica; // solo la Fabrica distingue Nv. 50 y Abierto (SAV_Misc4.cs:436-437)
        for (var t = 0; t < _d.Modos; t++)
        {
            for (var l = 0; l < (abierto ? 2 : 1); l++)
            {
                var tt = t;
                var ll = l;
                var baseVal = _d.Direccion + (_d.Zancada * tt) + (ll << 3);
                var mascara = (byte)(1 << (tt + (ll << 2)));
                var campos = new List<CampoDesafio>();
                CampoDesafio U16(string clave, string texto, RolCampo rol, int desplazamiento) => Campo(clave, texto, rol, RachaMaxima,
                    s => ReadUInt16LittleEndian(((SAV4)s).General[(baseVal + desplazamiento)..]),
                    (s, v) => WriteUInt16LittleEndian(((SAV4)s).General[(baseVal + desplazamiento)..], (ushort)Math.Clamp(v, 0, RachaMaxima)));
                // BFV de SAV_Misc4.cs:330-334: Torre/Sala/Arcade [Max, Actual]; Fabrica [Max, Actual, MaxCanje, ActualCanje]; Castillo [Max, Actual, ActualCP, (usado), MaxCP].
                campos.Add(U16("actual", "campo.racha_actual", RolCampo.RachaActual, 2));
                campos.Add(U16("record", "campo.record_racha", RolCampo.Record, 0));
                if (_d.Tipo == Tipo.Fabrica)
                {
                    campos.Add(U16("canjes_actual", "campo.canjes_actual", RolCampo.Otro, 6));
                    campos.Add(U16("canjes_record", "campo.canjes_record", RolCampo.Otro, 4));
                }
                else if (_d.Tipo == Tipo.Castillo)
                {
                    campos.Add(U16("pc_actual", "campo.pc_actual", RolCampo.Otro, 4));
                    campos.Add(U16("pc_record", "campo.pc_record", RolCampo.Otro, 8));
                }
                campos.Add(Campo("continuar", "campo.continuar", RolCampo.Bandera, 1,
                    s => (((SAV4)s).General[_d.Bandera] & mascara) != 0 ? 1 : 0,
                    (s, v) =>
                    {
                        var g = ((SAV4)s).General;
                        if (v != 0)
                        {
                            g[_d.Bandera] |= mascara;
                            if (_d.Tipo == Tipo.Castillo)
                                g[_d.Bandera + 1] |= 0x01; // SAV_Misc4.cs:537: el significado de esta bandera no se conoce; PKHeX la pone igual
                        }
                        else
                        {
                            g[_d.Bandera] &= (byte)~mascara;
                        }
                    }));
                if (_d.Tipo == Tipo.Torre)
                {
                    // Contador de rondas (ofs +1 en Platino y HGSS, +3 en DP): racha / 7, como hace PKHeX (SAV_Misc4.cs:493,516,561-579).
                    var ofsContador = _d.Bandera + (_juego == typeof(SAV4DP) ? 3 : 1) + (tt << 1);
                    campos.Add(Campo("rondas", "campo.rondas", RolCampo.Contador, null,
                        s => ReadUInt16LittleEndian(((SAV4)s).General[ofsContador..]),
                        (s, v) => WriteUInt16LittleEndian(((SAV4)s).General[ofsContador..], (ushort)Math.Clamp(v, 0, ushort.MaxValue))));
                }
                var nombresModo = _d.Tipo == Tipo.Torre ? tiposTorre : tiposResto;
                var nombre = Textos.Doble("modo." + nombresModo[tt]);
                if (abierto)
                    nombre = Frente3Esmeralda.Combinar(nombre, Textos.Doble(ll == 0 ? "nivel.nv50" : "nivel.abierto"));
                lista.Add(new ModoDesafio($"{nombresModo[tt]}{(abierto ? (ll == 0 ? ".nv50" : ".abierto") : "")}", nombre,
                    abierto ? (ll == 0 ? NivelDesafio.Nv50 : NivelDesafio.Abierto) : NivelDesafio.NoAplica, campos));
            }
        }
        return [.. lista];
    }

    protected override void Coherencia(SaveFile sav, ModoDesafio modo)
    {
        // Torre: el contador de rondas sigue a la racha (racha / 7, como hace PKHeX al editar una de las dos).
        if (modo.Rol(RolCampo.Contador) is { } rondas && modo.Rol(RolCampo.RachaActual) is { } actual)
            rondas.Escribir(sav, actual.Leer(sav) / 7);
    }

    internal override PlanJefe Planificar(SaveFile sav, ModoDesafio modo, JefeDesafio jefe)
    {
        if (!_d.Jefe)
            return PlanJefe.No("motivo.sin_comprobar");
        if (!modo.Clave.StartsWith("individual", StringComparison.Ordinal))
            return PlanJefe.No("motivo.solo_individual");
        if (jefe == JefeDesafio.Repetido)
            return PlanJefe.No("motivo.sin_repeticion");
        var oro = jefe == JefeDesafio.Oro;
        long n = oro ? _d.Oro : _d.Plata;
        var valores = new Dictionary<string, long>
        {
            ["actual"] = n,
            ["record"] = Math.Max(modo.Campo("record").Leer(sav), n),
            ["continuar"] = 1,
        };
        if (_d.Tipo == Tipo.Torre)
            valores["rondas"] = oro ? _d.RondasOro : _d.RondasPlata; // = racha / 7 (2 y 6)
        return PlanJefe.Si(jefe, valores);
    }
}
