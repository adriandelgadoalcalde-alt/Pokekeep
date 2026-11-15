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
    /// Tope de las horas jugadas. Gen 1: el Core escribe 255:00 y para el reloj (<c>SAV1.PlayedHours</c>). Gen 3 de GBA: 999 (pokeemerald y
    /// pokefirered <c>play_time.c</c>). Gen 4: 999 (pokediamond <c>igt.c:25-26</c>, pokeplatinum <c>play_time.h:7</c>, pokeheartgold
    /// <c>igt.c:27-31</c>). Del resto no hay fuente: el tope del dato (65535).
    /// </summary>
    public static int Horas(SaveFile sav) => sav switch
    {
        SAV1 => byte.MaxValue,
        SAV3 => 999,   // solo GBA: Colosseum y XD no heredan de SAV3
        SAV4 => 999,
        _ => ushort.MaxValue,
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
