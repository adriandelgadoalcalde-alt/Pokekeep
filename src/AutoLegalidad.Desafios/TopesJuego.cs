// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Buffers.Binary;
using PKHeX.Core;

namespace AutoLegalidad.Desafios;

/// <summary>
/// Topes de valores de la partida leídos en el código de los juegos (versión 1.1.3, ADR 0108), compartidos por Android y por el PKHeX
/// de escritorio (parche 0106). Cada tope lleva su fuente; los que solo salen del tamaño del dato no están aquí.
/// </summary>
public static class TopesJuego
{
    /// <summary>Mejor puntuación de Salto Pokémon: pokeemerald <c>src/pokemon_jump.c:34</c> <c>MAX_JUMP_SCORE</c>.</summary>
    public const uint SaltoPuntuacion = 99_990;

    /// <summary>Mejor puntuación de A por bayas (Dodrio): pokeemerald <c>src/dodrio_berry_picking.c:30</c> <c>MAX_SCORE</c>, que recorta el récord al guardarlo (<c>:2632-2635</c>).</summary>
    public const uint BayasPuntuacion = 999_990;

    /// <summary>Récords (game stats) de Gen 3: <c>IncrementGameStat</c> los deja en 0xFFFFFF (pokeemerald <c>overworld.c:438-441</c>, pokeruby <c>:281-284</c>, pokefirered <c>:372</c>). El récord 1 (hora del Salón de la Fama) se escribe entero y no sigue este tope.</summary>
    public const uint RegistroGen3 = 0xFFFFFF;

    /// <summary>Récord 1 de Gen 3: la hora de juego al entrar en el Salón de la Fama (horas &lt;&lt; 16 | minutos &lt;&lt; 8 | segundos).</summary>
    public const int RegistroGen3TiempoSalon = 1;

    /// <summary>
    /// Tope de las horas jugadas. Gen 1: el Core escribe 255:00 y para el reloj (<c>SAV1.PlayedHours</c>). Gen 2: 999 (pokecrystal
    /// <c>home/game_time.asm:96-114</c>, «Cap the timer after 1000 hours»). Gen 3 de GBA: 999 (pokeemerald y pokefirered <c>play_time.c</c>).
    /// Gen 4: 999 (pokediamond <c>igt.c:25-26</c>, pokeplatinum <c>play_time.h:7</c>, pokeheartgold <c>igt.c:27-31</c>). Del resto no hay
    /// fuente (Gen 5 solo se parece a las anteriores, sin leerlo en el juego): el tope del dato (65535).
    /// </summary>
    public static int Horas(SaveFile sav) => sav switch
    {
        SAV1 => byte.MaxValue,
        SAV2 => 999,
        SAV3 => 999,   // solo GBA: Colosseum y XD no heredan de SAV3
        SAV4 => 999,
        _ => ushort.MaxValue,
    };

    // ------------------------------------------------------------------------------------------------------------------------------
    // Versión 1.1.4 (15-nov-2026): topes de los informes topes-114-gen1a5.md y topes-114-gen6a9.md (Keep\Pokekeep-trabajo\misiones).
    // ------------------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Puntuación máxima de un curso del Pokéathlon (las tres de cada curso y la mejor). El juego recorta cada una a 999 al guardar el curso:
    /// pokeheartgold <c>asm/overlay_96.s:3204-3224</c> (<c>ov96_021E786C</c>, <c>=0x3E7</c>), y la suma del curso se acota igual (<c>:32053</c>).
    /// También es la cota de la mejor puntuación de cada prueba (inferida: es el mismo valor que luego se recorta, <c>overlay_96.s:3608-3650</c>).
    /// </summary>
    public const int PokeathlonPuntuacion = 999;

    /// <summary>Bonguris de cada color que caben en la bolsa: pokeheartgold <c>src/scrcmd_apricorn.c:32</c> (<c>if ((var1 + apricornCount) &lt;= 99)</c>).</summary>
    public const int BonguriPorColor = 99;

    /// <summary>Día de ajuste inicial del reloj (RTC) de Rubí, Zafiro y Esmeralda: pokeemerald <c>src/reset_rtc_screen.c:119-125</c> (<c>.maxVal = 9999</c>).</summary>
    public const int RelojDiaInicial = 9_999;

    /// <summary>
    /// Día transcurrido del reloj de Gen 3: el juego lo guarda en un <c>s16</c> (pokeemerald <c>include/global.h:198-204</c>, <c>struct Time.days</c>), así que
    /// por encima de 32767 se lee como negativo. Es el tope del tipo del propio juego (el valor real lo marca el calendario del reloj).
    /// </summary>
    public const int RelojDiaTranscurrido = 32_767;

    /// <summary>
    /// Contadores de combates del Entrenador de Battle Revolution: ocupan 3 bytes en el guardado (PKHeX.Core <c>Saves/SAV4BR.cs:338-380</c>), así
    /// que lo que se escribe por encima de 16.777.215 se trunca. Es el límite del guardado; el que pinta el juego no se ha medido.
    /// </summary>
    public const uint BattleRevolutionContador = 0xFFFFFF;

    /// <summary>Visitantes y fans de la Galería Unión de Blanco 2 y Negro 2: caben 8 y 12 (PKHeX.Core <c>JoinAvenue5.cs:8-9</c>; el comentario del Core dice «always 8» y «always 12»).</summary>
    public const int AvenidaVisitantes = JoinAvenue5.VisitorCount;

    /// <inheritdoc cref="AvenidaVisitantes"/>
    public const int AvenidaFans = JoinAvenue5.FanCount;

    /// <summary>
    /// Días transcurridos de la promoción de la Galería Unión: dura una semana, [0..6], y caduca al séptimo (PKHeX.Core <c>JoinAvenueSettings5.cs:68-71</c>,
    /// <c>PromotionDaysMax = 7</c>). Se admite el 7 del Core: es la cota segura, el 6 solo está inferido.
    /// </summary>
    public const int AvenidaPromocionDias = JoinAvenueSettings5.PromotionDaysMax;

    /// <summary>
    /// Récord 80 («Capturadas» en las bases secretas de ORAS): su tipo de tope es 0, 999.999.999 (PKHeX.Core <c>Saves/Substructures/Records.cs:35</c> y
    /// <c>Gen6/RecordBlock6.cs:89-102</c>). Tabla de PKHeX, no leída en el juego (coincide con los topes de Gen 4 que sí se leyeron).
    /// </summary>
    public const int RecordCapturadasBasesSecretas = 999_999_999;

    /// <summary>
    /// Dinero de Diamante Brillante y Perla Reluciente: 999.999 (PKHeX.Core <c>MyStatus8b.cs:13,46</c>, <c>MAX_MONEY</c>, que recorta al escribir; Bulbapedia,
    /// Poké Dollar: «in Brilliant Diamond and Shining Pearl, a player's wallet can hold up to $999,999»). <c>SaveFile.MaxMoney</c> vale aquí 9.999.999.
    /// </summary>
    public const int DineroBDSP = 999_999;

    /// <summary>Tirada de una incursión de Espada y Escudo: de 1 a 100 (PKHeX.Core <c>Saves/Substructures/Gen8/SWSH/RaidSpawnList8.cs:88</c>).</summary>
    public const int IncursionTirada = 100;

    /// <summary>
    /// Rango máximo de la Clase Maestra de la Torre Batalla de Diamante Brillante y Perla Reluciente: <c>BtlTower.MAX_MASTER_RANK = 10</c>
    /// (volcado il2cpp de DBPR, <c>investigacion/datos-juegos/DBPR/il2cpp/dump.cs:304877</c>; investigación de desafíos §12.1).
    /// </summary>
    public const int TorreBdspRangoMaestro = 10;

    /// <summary>Retos realizados en la Torre Batalla de DBPR: <c>BtlTower.MAX_CHALLENGE = 999999</c> (<c>dump.cs:304879</c>).</summary>
    public const uint TorreBdspRetos = 999_999;

    /// <summary>
    /// Estadística de combate de un Pokémon de Gen 1 y 2: el juego no pasa de 999 (<c>MAX_STAT_VALUE</c>: pokered <c>constants/battle_constants.asm:75</c>,
    /// pokecrystal <c>constants/battle_constants.asm:78</c>). De Gen 3 en adelante el juego no recorta a un número y el tope sigue siendo el del dato.
    /// </summary>
    public const int EstadisticaFinalGen1y2 = 999;

    /// <summary>
    /// Racha de la Torre Batalla de Espada y Escudo: cada victoria suma 1 y al pasar de 300 vuelve a 201 (individuales) o 200 (dobles); leído en el
    /// código de Espada (<c>desafios-investigacion.md</c> §12.2, bloques <c>0x6226F5AD</c> y <c>0x5F74FCEE</c>). Parche 0107 del escritorio (el PKHeX dejaba 999).
    /// </summary>
    public const int TorreSwShRacha = 300;

    /// <summary>
    /// Cantidad de cada objeto de un regalo de Pokémon Link (Nexo Pokémon de XY y ORAS). El Core la declara u16 (<c>PL6.Quantity1..6</c>, <c>MysteryGifts/PL6.cs:58</c>),
    /// pero el formulario de PKHeX la escribía con <c>(byte)</c> sobre un cuadro de hasta 65.535 (<c>SAV_Link6.cs:132</c>) y 300 se guardaba como 44, y la app de Android
    /// también la lleva a un byte. 255 es lo que cabe sin dar la vuelta; el tope que da el juego no se ha leído (A12 de <c>topes-114-gen6a9.md</c>). Parche 0107 del escritorio.
    /// </summary>
    public const int NexoCantidadObjeto = 255;

    /// <summary>Estrellas de un donut de Leyendas Z-A: de 0 a 5 (Bulbapedia, Donut: «ranking ranging from zero to five stars»; <c>DonutPocket9a.cs:258-266</c> no da más de 5).</summary>
    public const int DonutEstrellas = 5;

    /// <summary>
    /// Las dos mejores puntuaciones de los minijuegos de Gen 3 son u32 en el juego (pokeemerald <c>include/global.h:226</c>
    /// <c>bestJumpScore</c> y <c>:231</c> <c>bestScore</c>; pokefirered <c>global.h:355-356</c>), pero el Core las lee como u16
    /// (<c>SaveBlock3SmallE.cs:55-56</c>, <c>SaveBlock3SmallFRLG.cs:68-69</c>) y recorta las de bayas a 99990: una puntuación de
    /// 99990 se leía como 34454. Aquí se leen y se escriben con sus 32 bits y el tope de cada minijuego.
    /// </summary>
    public static uint PuntuacionMinijuego(ISaveBlock3SmallExpansion bloque, bool salto)
        => Math.Min(salto ? SaltoPuntuacion : BayasPuntuacion, BinaryPrimitives.ReadUInt32LittleEndian(Puntuaciones(bloque, salto)));

    /// <inheritdoc cref="PuntuacionMinijuego"/>
    public static void PonerPuntuacionMinijuego(ISaveBlock3SmallExpansion bloque, bool salto, uint valor)
        => BinaryPrimitives.WriteUInt32LittleEndian(Puntuaciones(bloque, salto), Math.Min(salto ? SaltoPuntuacion : BayasPuntuacion, valor));

    private static Span<byte> Puntuaciones(ISaveBlock3SmallExpansion bloque, bool salto) => bloque switch
    {
        SaveBlock3SmallE e => e.Data[(salto ? 0x208 : 0x20C)..],
        SaveBlock3SmallFRLG f => f.Data[(salto ? 0xB0C : 0xB10)..],
        _ => throw new NotSupportedException(bloque.GetType().Name),
    };
}
