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
    /// Gen 4: 999 (pokediamond <c>igt.c:25-26</c>, pokeplatinum <c>play_time.h:7</c>, pokeheartgold <c>igt.c:27-31</c>). Diamante Brillante y
    /// Perla Reluciente: 999 (1.1.5; <c>Dpr.PlayTimeManager._limitSec</c> = 3.599.999 s = 999:59:59, fijado en su constructor estático, RVA
    /// <c>0x1BCB930</c> de <c>investigacion/datos-juegos/DBPR/main.img.bin</c>: <c>mov w10,#0xee7f; movk w10,#0x36,lsl #16; str w10,[x9,#0x10]</c>;
    /// campos en <c>il2cpp/dump.cs:349192-349198</c>).
    /// 1.1.5 (matriz de topes, <c>topes-115-ds.md</c>, <c>topes-115-3ds.md</c> y <c>topes-115-switch.md</c>): también 999 en Gen 5 (B2W2, ARM9 <c>0x02008C76</c>:
    /// <c>ldr r0,=0x3E7 ; cmp r6,r0</c>; BW, ARM9 <c>0x020085D6</c>), X, Y, Rubí Omega y Zafiro Alfa (X <c>0x120afc</c>), Sol, Luna, Ultrasol y Ultraluna
    /// (<c>0x443eb8</c>, <c>0x459e68</c>), Let's Go (Pikachu <c>001ca450</c>), Espada y Escudo (<c>01429710</c>) y Leyendas Arceus (<c>00cb65c0</c>, <c>cmp #0x3e7</c>).
    /// Todas guardan las horas en un <c>ushort</c> (<c>PlayTime5/6/7/8</c> del Core), así que 999 cabe. Escarlata y Púrpura: 999, VERIFICADO en Eden el 16-nov-2026
    /// (sembrado 999:59:58, tras jugar y guardar el juego dejó 999:59:59; <c>topes-115-switch.md</c>, sección 7). Leyendas Z-A: SIN TOPE en el dato (sembrado 999:59:58,
    /// el juego guardó 1000:04:37; con 5000 h guardó 5000:00:54; solo el diálogo «Guardar» pinta «999:59»), así que se queda en el tope del dato (65535).
    /// </summary>
    public static int Horas(SaveFile sav) => sav switch
    {
        SAV1 => byte.MaxValue,
        SAV2 => 999,
        SAV3 => 999,   // solo GBA: Colosseum y XD no heredan de SAV3
        SAV4 => 999,
        SAV5 => 999,   // 1.1.5: BW y B2W2
        SAV6 => 999,   // 1.1.5: XY y ORAS
        SAV7 => 999,   // 1.1.5: SM y USUM
        SAV7b => 999,  // 1.1.5: Let's Go
        SAV8SWSH => 999, // 1.1.5
        SAV8LA => 999,   // 1.1.5
        SAV8BS => 999,
        SAV9SV => 999,   // 1.1.5: medido en Eden
        _ => ushort.MaxValue, // Z-A: el juego no para el reloj (medido en Eden)
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

    /// <summary>
    /// Puntos atleta de una prueba del Pokéathlon (versión 1.1.5): la conversión de la puntuación en bruto recorta a 200 (pokeheartgold
    /// <c>asm/overlay_96.s:107100-107113</c>, <c>ov96_0221A61C</c>: si el valor es mayor que 200.0 (<c>0x40690000</c>) deja <c>mov r4,#0xc8</c>). Es el tope
    /// de las tres puntuaciones de cada curso (<c>Score0..2</c>, los puntos de sus tres pruebas); el total (<c>ScoreMax</c>, con los puntos extra) se
    /// queda en <see cref="PokeathlonPuntuacion"/>.
    /// </summary>
    public const int PokeathlonPuntosPrueba = 200;

    /// <summary>
    /// Tope de la mejor puntuación de cada prueba del Pokéathlon, guardada ya en Puntos atleta (PKHeX.Core <c>Pokeathlon4.cs:44-47</c>). Versión 1.1.5:
    /// <see cref="PokeathlonPuntosPrueba"/> (200) en nueve pruebas; Disc Catch no llega a 150, porque su conversión es 150 − 120 / (1 + k·bruto)
    /// (pokeheartgold <c>overlay_96.s:107068-107098</c>, <c>ov96_0221A5D4</c>: constantes 1.0 <c>0x3FF00000</c>, 120.0 <c>0x405E0000</c> y 150.0 <c>0x4062C000</c>)
    /// y se trunca a entero: 149. El 198 de Circle Push que da Bulbapedia es la mejor partida posible, no el tope del código (3 · bruto, recortado a 200).
    /// </summary>
    public static int PokeathlonMejorPrueba(PokeathlonEvent4 prueba) => prueba == PokeathlonEvent4.DiscCatch ? 149 : PokeathlonPuntosPrueba;

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
    /// Cada uno de los cinco sabores de un Pokocho de Diamante Brillante y Perla Reluciente (1.1.5): al cocinar, <c>PofinCookModel.CalcParam</c> (RVA <c>0x205B380</c>) pasa cada sabor por
    /// <c>Mathf.Clamp(int, 0, 100)</c> (<c>0205b86c: mov w21,#0x64</c> ... <c>0205b8d8: bl 0x26b8d90</c>) y lo guarda en un <c>byte</c> (<c>topes-115-switch.md</c> S21, VERIFICADO con Ghidra). La suavidad no lleva
    /// <c>Clamp</c>: se queda en el tope del dato. Un valor ya guardado por encima (el PKHeX deja 255) no se recorta al abrir.
    /// </summary>
    public const int PokochoSabor = 100;

    /// <summary>
    /// Nivel de un Pokocho de DBPR (1.1.5): sale del valor de la entrada elegida de la lista de sabores (<c>PofinCookModel.GetPofinData</c>, <c>0205bc54: ldrb w22,[x8,#0x24]</c>, RVA
    /// <c>0x205BA80</c>), es decir, de un sabor ya recortado a <see cref="PokochoSabor"/>: no pasa de 100 (<c>topes-115-switch.md</c> S21).
    /// </summary>
    public const int PokochoNivel = 100;

    /// <summary>
    /// Mayor dato de tienda de la Galería Unión (1.1.5): 4 versiones x 8 tipos x 10 rangos = 320 combinaciones, guardadas como <c>((versión · 8 + tipo) · 10 + rango) + 1</c> (0 = sin tienda;
    /// <c>JoinAvenueVisitor5.ShopVersionCount</c>, <c>ShopTypeCount</c> y <c>ShopMaxRank</c>). Las partidas reales traen además 65.535 en los visitantes que no tienen tienda.
    /// </summary>
    public const int AvenidaTiendaDatoMaximo = JoinAvenueVisitor5.ShopVersionCount * JoinAvenueVisitor5.ShopTypeCount * JoinAvenueVisitor5.ShopMaxRank;

    /// <summary>
    /// Dato de 16 bits de una tienda (o tienda deseada) de la Galería Unión (1.1.5): <c>((versión · 8 + tipo) · 10 + rango) + 1</c>. Es la inversa exacta de <see cref="AvenidaDescifrarTienda"/> (la cuenta de
    /// <c>JoinAvenueVisitor5.DecodeShop</c>). El <c>EncodeShop</c> del Core no lo es: multiplica por 4 y por 8 donde el descifrado divide por 8 y por 10 (<c>JoinAvenueVisitor5.cs:313-326</c> frente a
    /// <c>:328-342</c>) y un rango de 10 valores no cabe en un radix de 8, así que guardar con su tupla cambiaba el rango y la versión (el rango 9 volvía como 8 y la versión 3, como 1). El Core va sellado
    /// (ADR 0068): el dato se escribe aquí, con <c>ShopType</c> y <c>DesiredShopType</c>.
    /// </summary>
    public static ushort AvenidaCodificarTienda(byte version, JoinAvenueShopType5 tipo, byte rango)
        => (ushort)((((version % JoinAvenueVisitor5.ShopVersionCount) * JoinAvenueVisitor5.ShopTypeCount + ((int)tipo % JoinAvenueVisitor5.ShopTypeCount)) * JoinAvenueVisitor5.ShopMaxRank) + (rango % JoinAvenueVisitor5.ShopMaxRank) + 1);

    /// <summary>
    /// Tienda que guarda un dato de la Galería Unión, o <c>null</c> si no hay (1.1.5): 0, y todo lo que pasa de <see cref="AvenidaTiendaDatoMaximo"/>, como el 65.535 que traen las partidas reales en
    /// los visitantes sin tienda (descifrarlo daba una tienda inventada: versión 3, Salón, rango 4).
    /// </summary>
    public static (byte Version, JoinAvenueShopType5 Tipo, byte Rango)? AvenidaDescifrarTienda(ushort bruto)
    {
        if (bruto == 0 || bruto > AvenidaTiendaDatoMaximo) return null;
        var resto = bruto - 1;
        var rango = resto % JoinAvenueVisitor5.ShopMaxRank; resto /= JoinAvenueVisitor5.ShopMaxRank;
        var tipo = resto % JoinAvenueVisitor5.ShopTypeCount; resto /= JoinAvenueVisitor5.ShopTypeCount;
        return ((byte)(resto % JoinAvenueVisitor5.ShopVersionCount), (JoinAvenueShopType5)tipo, (byte)rango);
    }

    /// <summary>
    /// Mueble elegido en el Enlace Global de Blanco, Negro, Blanco 2 y Negro 2 (1.1.5): el dato guarda 7 bits y el octavo es otra marca (PKHeX.Core <c>GlobalLink5.cs:67</c>,
    /// <c>Furniture &amp; 0x7F</c>), así que lo que cabe es 127 (que es «ninguno»). El formulario de PKHeX ofrecía 255 y un valor mayor se guardaba sin el bit alto (200 volvía como 72).
    /// </summary>
    public const int EnlaceGlobalMuebleElegido = 0x7F;

    /// <summary>
    /// Contador de rondas de la Torre Batalla de Diamante, Perla, Platino, HeartGold y SoulSilver (1.1.5): el juego lo sube hasta 65.534 (<c>roomNums</c>,
    /// pokeplatinum <c>battle_tower_save.c:300</c>; HG/SS, ARM9 <c>0x202a584</c>; <c>desafios-investigacion.md</c> §3.4). El dato es un u16 (PKHeX <c>SAV_Misc4.cs:561-579</c>
    /// lo sincroniza con la racha, 7 a 1). «Máximo» no lo escribe: lo deja en racha / 7.
    /// </summary>
    public const int FrenteTorreRondas = ushort.MaxValue - 1;

    /// <summary>
    /// Serie del Metro Batalla de Gen 5 (1.1.5): <c>IncSets</c> sube la cuenta hasta 65.534 y para (B2W2 ARM9 <c>0x0200E3F8</c>, BW <c>0x0200DB60</c>:
    /// <c>ldr r0,=0xFFFE ; cmp r1,r0 ; bhs fin ; adds r0,r1,#1 ; strh</c>; <c>topes-115-ds.md</c> S1, VERIFICADO). El dato es un u16 (PKHeX.Core <c>BattleSubway5.cs:21-33</c>).
    /// «Máximo» no lo escribe: lo deja en racha / 7 + 1.
    /// </summary>
    public const int MetroSerie = ushort.MaxValue - 1;

    /// <summary>
    /// Contador de la Master Ball de la Torre Batalla de Espada y Escudo (1.1.5): el bloque es un <c>byte</c> (<c>GetValue&lt;byte&gt;</c> en <c>Torre8.cs</c>), así que 255 es lo
    /// que cabe. No hay fuente del juego para un tope menor: solo se sabe que el jefe sale cuando vale 10 (<c>Torre8.ContadorJefeMaster</c>).
    /// </summary>
    public const int TorreSwShContadorMaster = byte.MaxValue;

    /// <summary>
    /// Ronda de una clase de la Torre Batalla de Diamante Brillante y Perla Reluciente (<c>tower_round</c>, +0x18) en 1.1.5: el rival de la ronda sale de <c>trainer_random[7]</c>
    /// y <c>BtlTower.TRAINER_CNT = 7</c> (<c>investigacion/datos-juegos/DBPR/il2cpp/dump.cs:304881</c>); la ronda 7 es la del jefe, jugada en Eden con la partida autorizada
    /// (<c>emulador-dbpr-informe.md</c>, puntos 1 y 2). Antes el escritor admitía hasta 10, que no tiene fuente y se sale de la tabla de rivales.
    /// </summary>
    public const int TorreBdspRonda = 7;

    /// <summary>Partida suspendida de una clase de la Torre de DBPR (<c>suspension_flag</c>, +0x01): es un <c>bool</c> (dump.cs:301951), 0 o 1.</summary>
    public const int TorreBdspSuspendida = 1;

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
    /// pero el formulario de PKHeX la escribía con <c>(byte)</c> sobre un cuadro de hasta 65.535 (<c>SAV_Link6.cs:132</c>) y 300 se guardaba como 44. La 1.1.4 la dejó en 255.
    /// 1.1.5: el juego la recorta a 999 al recibir el regalo (X, <c>DllEshop.cro</c> <c>0x10cc</c>: <c>cmp r0,#0x3e8 ; ldrhs r0,=0x3e7 ; strhhs r0,[r7,#0x8a]</c>; misma
    /// función en X 1.2; <c>topes-115-3ds.md</c> S8), así que el tope es 999 y se escribe con sus 16 bits en el escritorio y en Android.
    /// </summary>
    public const int NexoCantidadObjeto = 999;

    // ------------------------------------------------------------------------------------------------------------------------------
    // Versión 1.1.5 (15-nov-2026): matriz de topes (Keep\Pokekeep-trabajo\misiones\matriz-topes-115.md), fuentes en topes-115-ds.md, topes-115-3ds.md
    // y topes-115-switch.md. Android y escritorio usan estas mismas constantes.
    // ------------------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Racha actual y récord de cada modo del Metro Batalla de Gen 5 (1.1.5): el motor del juego los cuenta en un <c>u16</c> y solo para en 0xFFFF (B2W2 ov33
    /// <c>0x0217BD4C</c>: <c>ldrh r2,[r0,#0xE] ; ldr r1,=0xFFFF ; cmp r2,r1 ; bhs fin ; +1</c>; BW ov21 <c>0x021CA1FC</c>, idéntico). Es el tope del dato
    /// (PKHeX.Core <c>BattleSubway5.cs:21-33</c>, <c>ushort</c>); la serie derivada (racha / 7 + 1) llega a 9.363 y cabe.
    /// </summary>
    public const int MetroRacha = ushort.MaxValue;

    /// <summary>
    /// Cada uno de los 29 registros del PWT de Blanco 2 y Negro 2 (1.1.5): la única función del juego que los escribe suma 1 hasta 9.999 (B2W2 ARM9
    /// <c>0x0200FEB4</c>: <c>ldrh r2,[r0,r3] ; ldr r1,=0x270F ; cmp r2,r1 ; bhs fin ; adds r1,r2,#1 ; strh</c>). El dato es <c>u16</c> (PKHeX.Core
    /// <c>PWTBlock5.cs:11-12</c>), pero el «Máximo» escribía 65.535.
    /// </summary>
    public const int PwtRegistro = 9_999;

    /// <summary>
    /// Puntos de Batalla de X, Y, Rubí Omega y Zafiro Alfa (1.1.5): <c>SetBP</c> recorta a 9.999 (X e Y <c>0x433d9c</c>: <c>ldr r1,=0x270f ; cmp r0,r1 ; movhi r0,r1 ;
    /// str r0,[r2,#0x44]</c>; Zafiro Alfa <c>0x462a1c</c>, Rubí Omega <c>0x462a24</c>). El dato es <c>u16</c> (PKHeX.Core <c>Misc6XY.cs:20-24</c>). Los de Gen 7 ya
    /// usaban 9.999 (<c>Misc::SetBP</c>).
    /// </summary>
    public const int PuntosBatalla3DS = 9_999;

    /// <summary>
    /// Participantes del Festival de Blanco 2 y Negro 2 (1.1.5): el juego solo guarda un valor de 100 o menos (B2W2 ARM9 <c>0x02010E80</c>: <c>cmp r1,#0x64 ; bhi fin ;
    /// ldrb r2,[r0,#0xFA] ; cmp r2,r1 ; bhs fin ; strb r1,[r0,#0xFA]</c>). El dato es <c>u8</c> sin recorte (PKHeX.Core <c>FestaBlock5.cs:49-52</c>).
    /// </summary>
    public const int FestivalParticipantes = 100;

    /// <summary>
    /// Intercambios que quedan en Festi Plaza (1.1.5): 3 (Ultraluna <c>JoinFestaDataSave::FacilitiesChange</c> <c>0x454434</c>, que guarda en <c>[r4,#0x358]</c> el resultado
    /// de <c>0x48b760</c>). Sol y Luna no tienen esa instalación, así que el 3 solo se contrasta con Ultrasol y Ultraluna. El dato es <c>u8</c> (PKHeX.Core
    /// <c>FestaFacility.cs:29</c>).
    /// </summary>
    public const int FestiPlazaIntercambios = 3;

    /// <summary>
    /// Puntos del Poder O de X e Y (1.1.5): arrancan en 10 y es lo máximo (X <c>0x42f8e0</c>: <c>mov r0,#0xa ; strb r0,[r4,#0x45]</c>, y la recarga en <c>0x11fc9c</c>).
    /// El dato es <c>u8</c> (PKHeX.Core <c>OPower6.cs:19,32</c>).
    /// </summary>
    public const int PoderOPuntos = 10;

    /// <summary>
    /// Veces combatido de cada Pokémon en la Pokédex de Espada y Escudo (1.1.5): el incremento no pasa de 999 (Espada <c>01434a24</c>: <c>cmp w9,#0x3e6</c>). El dato es
    /// <c>u32</c> (PKHeX.Core <c>Zukan8.cs:369-370</c>).
    /// </summary>
    public const int PokedexCombates8 = 999;

    /// <summary>
    /// Victorias seguidas del Salón de la Fama de X, Y, Rubí Omega y Zafiro Alfa (1.1.5): 9.999 (X <c>0x43416c</c>–<c>0x434190</c>, <c>savedata::Dendou</c>; Zafiro Alfa
    /// <c>0x462dec</c>). El dato ocupa 14 bits (16.383, PKHeX.Core <c>HallFame6Entity.cs:83</c>) y 9.999 cabe; antes el campo se quedaba en 255.
    /// </summary>
    public const int SalonFamaVictorias6 = 9_999;

    /// <summary>
    /// Fama de Pokéstar de un Pokémon de Gen 5 (1.1.5): el dato es un <c>u8</c> entero (PKHeX.Core <c>PK5.cs:272-273</c>, <c>Data[0x87]</c>); el juego no recorta a 100. El
    /// umbral de «estrella» (más de 250) es de PKHeX y no sale del juego. Antes el campo de Android se quedaba en 100.
    /// </summary>
    public const int FamaPokestar = byte.MaxValue;

    /// <summary>
    /// Récord en bruto de cada prueba del Pokéathlon (1.1.5), leído en el código de pokeheartgold (<c>asm/overlay_96.s</c>; informe <c>topes-115-ds.md</c> §2.4): Pennant Capture y
    /// Lamp Jump, 999 (<c>:32053-32056</c> y <c>:63710</c>); Disc Catch, 1.023 (máscara <c>0x3FF</c>, <c>:57438</c>); Goal Roll, 4.095 (12 bits, <c>:97342-97371</c>); Snow Throw, 99
    /// (<c>ov96_022130EC</c>, <c>:91953-91961</c>, <c>cmp r1,#0x63</c>); Relay Run y Ring Drop, 200 (<c>:74726-74742</c> y <c>ov96_0220FD28</c>, <c>:85320-85337</c>, <c>cmp r1,#0xc8</c>);
    /// Block Smash, 62.463 (parte entera menor de 1.024 más un byte de hasta 60 por 1.024, <c>:49342-49361</c>; el byte empieza en 0: la memoria de la prueba,
    /// 0x648 bytes, se pone a ceros al reservarla, <c>MI_CpuFill8</c> en <c>:46209-46214</c>, y solo lo cambian la suma hasta 60 y la resta hasta 0 de <c>:49359-49413</c>). Hurdle Dash (fotogramas a 30 por segundo) y Circle Push (los puntos de la prueba),
    /// formato medido en melonDS el 16-nov-2026 (<c>topes-115-ds.md</c> §3.1), no se ha encontrado tope en el código y ninguna partida llegó a uno: solo manda el dato, <c>u16</c> (PKHeX.Core <c>PokeathlonEventRecord4.cs:24</c>), sin el 0xFFFF reservado para «sin récord».
    /// </summary>
    public static int PokeathlonRecordBruto(PokeathlonEvent4 prueba) => prueba switch
    {
        PokeathlonEvent4.PennantCapture or PokeathlonEvent4.LampJump => 999,
        PokeathlonEvent4.DiscCatch => 1_023,
        PokeathlonEvent4.GoalRoll => 4_095,
        PokeathlonEvent4.SnowThrow => 99,
        PokeathlonEvent4.RelayRun or PokeathlonEvent4.RingDrop => 200,
        PokeathlonEvent4.BlockSmash => 62_463,
        _ => ushort.MaxValue - 1,
    };

    /// <summary>
    /// Rango de la Galería Unión de Blanco 2 y Negro 2 (1.1.5): el último registro de la tabla de rangos (miembro 7 del NARC <c>a/2/4/4</c>) es el 100 y la función de subida
    /// (ov137 <c>0x021F30EC</c>: <c>cmp r1,r0 / blo</c>) no suma más. PKHeX deja 9.999 (<c>JoinAvenueSettings5.cs:46</c>, <c>MaxAvenueRank</c>); el dato es <c>u16</c> (<c>:44</c>).
    /// </summary>
    public const int AvenidaRango = 100;

    /// <summary>
    /// Experiencia del rango de la Galería Unión (1.1.5): siempre por debajo del umbral de su rango; el mayor es 2.000 (rangos 41 a 99), así que 1.999 (ov137 <c>0x021F30EC</c>,
    /// <c>0x021F3142-0x021F3150</c>, columna 5 de la tabla del miembro 7 de <c>a/2/4/4</c>). Es el tope del campo sin distinguir rango. El dato es <c>u32</c> (<c>JoinAvenueSettings5.cs:43</c>).
    /// </summary>
    public const int AvenidaExperiencia = 1_999;

    /// <summary>Rango de la tienda de un visitante de la Galería Unión (1.1.5): a partir de 9 no sube más (ov137 <c>0x021F31F8</c>, <c>0x021F3256: cmp r0,#9 / blo</c>). PKHeX deja 10 (<c>JoinAvenueVisitor5.cs:16</c>, <c>ShopMaxRank</c>).</summary>
    public const int AvenidaTiendaRango = 9;

    /// <summary>
    /// Experiencia de la tienda de un visitante de la Galería Unión (1.1.5): por debajo del umbral del registro de su tipo, que llega a 1.000 en el rango 8 (miembro 1 de <c>a/2/4/4</c>,
    /// ov137 <c>0x021F3266-0x021F326A</c>): 999. El dato es <c>u16</c> (<c>JoinAvenueVisitor5.cs:288</c>).
    /// </summary>
    public const int AvenidaTiendaExperiencia = 999;

    /// <summary>Estrellas de un donut de Leyendas Z-A: de 0 a 5 (Bulbapedia, Donut: «ranking ranging from zero to five stars»; <c>DonutPocket9a.cs:258-266</c> no da más de 5).</summary>
    public const int DonutEstrellas = 5;

    /// <summary>
    /// Cantidad por objeto que cabe en la mochila de Gen 7 (SM, USUM), Let's Go y Gen 8 de Switch (Espada y Escudo) en modo HaX (versión 1.1.5): 10 bits
    /// (<c>InventoryItem7.cs:35</c> <c>(Count &amp; 0x3FF) &lt;&lt; 10</c>, <c>InventoryItem7b.cs:35</c> y <c>InventoryItem8.cs:36</c>, ambos <c>(Count &amp; 0x3FF) &lt;&lt; 15</c>).
    /// El Core deja 65.535 en <c>PlayerBag.MaxQuantityHaX</c> (<c>PlayerBag.cs:17</c>) y 5.000 se guardaba como 904.
    /// </summary>
    public const int MochilaCantidad10Bits = 0x3FF;

    /// <summary>
    /// Cantidad por objeto que cabe en la mochila de Leyendas Arceus en modo HaX (1.1.5): <c>Int16</c> con signo (<c>InventoryItem8a.cs:21</c>,
    /// <c>ReadInt16LittleEndian</c>); por encima de 32.767 se leía como negativo.
    /// </summary>
    public const int MochilaCantidadInt16 = short.MaxValue;

    /// <summary>
    /// Usos que le quedan al saco de Superentrenamiento de un Pokémon de Gen 6 (PK6 <c>+0x17</c>, <see cref="PK6.TrainingBagHits"/>), según el saco que lleva (1.1.5, S25, VERIFICADO).
    /// El juego fija el valor con una tabla de 26 filas al asignar el saco (<c>DllField.cro</c> de X 1.0, tabla en <c>0xF8BD8</c>, el valor en el desplazamiento 8: 2, 5 o 10 en los
    /// sacos de estadística, 2 en los especiales, 20 en el de reinicio y 50 en el calmante) y solo lo baja hasta 0 (<c>code.bin</c> <c>0x3E47D0</c>); nunca lo sube por encima. Es la
    /// misma tabla que <see cref="SuperTrainBagExtensions"/> de PKHeX (<c>topes-115-3ds.md</c> S25). Sin saco, 0.
    /// </summary>
    public static int UsosSacoSuperentrenamiento(int saco) => saco is < 0 or > byte.MaxValue ? 0 : ((SuperTrainBag)saco).GetMax();

    /// <summary>
    /// Tope de la cantidad por objeto de una mochila en modo HaX (1.1.5): el de <see cref="PlayerBag.MaxQuantityHaX"/> (65.535, o 255 en Gen 1 y 2) salvo en
    /// las mochilas cuyo dato es más estrecho: <see cref="MochilaCantidad10Bits"/> en Gen 7, Let's Go y Espada y Escudo, y <see cref="MochilaCantidadInt16"/> en
    /// Leyendas Arceus. El Core va sellado (ADR 0068): el tope vive aquí, no en <c>PlayerBag</c>. Gen 3, 4, 5, 6, 8b, 9 y 9a caben (<c>ushort</c> o <c>uint</c>).
    /// </summary>
    public static int MochilaCantidadHaX(PlayerBag bolsa) => bolsa switch
    {
        PlayerBag7SM or PlayerBag7USUM or PlayerBag7b or PlayerBag8 => Math.Min(bolsa.MaxQuantityHaX, MochilaCantidad10Bits),
        PlayerBag8a => Math.Min(bolsa.MaxQuantityHaX, MochilaCantidadInt16),
        _ => bolsa.MaxQuantityHaX,
    };

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
