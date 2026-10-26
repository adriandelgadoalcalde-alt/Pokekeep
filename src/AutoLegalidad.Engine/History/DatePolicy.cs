// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;

namespace AutoLegalidad.Engine.History;

/// <summary>Fechas elegidas para un Pokemon: captura (o recepcion) y, si procede, la del huevo, con el motivo de la eleccion.</summary>
public sealed record DatePlan(DateOnly Met, DateOnly? Egg, string Source);

/// <summary>
/// Hito 4c (R-010): fecha PLAUSIBLE de captura y de eclosion. El Core solo valida las ventanas de distribucion de los regalos modernos y las de GO; en todo lo demas acepta cualquier fecha del calendario
/// (y los generadores ponen siempre «hoy»: <c>EncounterDate.cs:13-40</c>). Reglas, por orden de preferencia:
/// 1. Ventana REAL del Core (regalos con ventana de distribucion, GO): una fecha dentro de ella.
/// 2. Tarjeta de evento con fecha de inicio conocida (catalogo: PGF/WC6/WC7/WB7 sin el marcador «hoy»): desde ese dia, durante <see cref="TypicalDistributionDays"/> dias (la mediana de las 79 ventanas con fin
///    que publica el Core es de 55 dias).
/// 3. Sin ventana conocida: entre el lanzamiento del juego de origen (<see cref="GameReleaseDates"/>, por idioma) y AYER, elegida de forma determinista a partir de la identidad del Pokemon.
/// Nunca futura ni «hoy» (la historia es de un Pokemon capturado antes), el huevo se recoge antes de eclosionar (1 a 7 dias antes) y las transferencias respetan el suelo de la consola o servicio por el que pasaron.
/// </summary>
public static class DatePolicy
{
    /// <summary>Mediana, medida sobre las 79 ventanas con fin de <c>EncounterServerDate.cs</c> (Core fijado), de la duracion de una distribucion en dias.</summary>
    public const int TypicalDistributionDays = 55;

    /// <summary>Dias maximos entre recoger un huevo y verlo eclosionar que se eligen (1 a 7): un huevo se incuba en dias, no en meses.</summary>
    public const int MaxEggDays = 7;

    public static DateOnly Ceiling(DateOnly today) => today.AddDays(-1);

    /// <summary>Suelo de fechas del Pokemon: lanzamiento del juego en su idioma y, si paso por una transferencia que pone la fecha del dia, el de esa consola o servicio.</summary>
    public static DateOnly Floor(PKM pk, IEncounterTemplate enc)
    {
        var floor = GameReleaseDates.For(pk.Version, pk.Language) ?? GameReleaseDates.FormatFloor(pk.Format);
        var origin = enc.Generation;
        var format = pk.Format;
        void Raise(DateOnly d) { if (d > floor) floor = d; }
        if (origin <= 2 && format >= 7) Raise(GameReleaseDates.BankFloor);                 // Poke Transporter (PK1/PK2 -> PK7: PK1.cs:206, PK2.cs:168 ponen el dia de la transferencia)
        if (origin <= 3 && format >= 5) Raise(GameReleaseDates.FormatFloor(5));            // Pal Park y despues Poke Transfer (PK4 -> PK5 pone la fecha del dia: RetainMetDateTransfer45 = false)
        else if (origin == 3 && format == 4) Raise(GameReleaseDates.FormatFloor(4));       // Pal Park (PK3.cs:255)
        if (origin == 4 && format >= 5) Raise(GameReleaseDates.FormatFloor(5));
        return floor;
    }

    /// <summary>Fecha de captura y de huevo para el Pokemon, o null si no hay un rango valido (se deja la que puso el generador).</summary>
    public static DatePlan? Plan(PKM pk, IEncounterTemplate enc, DateOnly today, DateOnly? cardStart, int variant = 0, DateOnly? cardEnd = null, bool endFromTracking = false)
    {
        if (pk.Format <= 3) return null;   // Gen1-3 no guardan fecha (MiscDateVerifier.cs:16-17)
        var ceiling = Ceiling(today);
        var lo = Floor(pk, enc);
        var hi = ceiling;
        string source;
        if (EncounterWindows.Of(enc) is { } w)
        {
            lo = w.Start;   // la ventana del Core es el dato real y manda sobre el lanzamiento (hay distribuciones que abren un dia antes por la zona horaria: WC9 de Escarlata/Purpura, WA8 de Arceus)
            if (w.End is { } e && e < hi) hi = e;
            source = w.Source;
        }
        else if (cardStart is { } cs)
        {
            // La fecha de la tarjeta puede ser anterior al lanzamiento del juego (fecha de creacion o de un pase previo): nadie pudo recibirla antes de que el juego existiera, asi que la ventana empieza ese dia.
            var start = cs > lo ? cs : lo;
            // Fin conocido (hojas Extras/Tracking, ADR 0029): la distribucion real; si no, la mediana de las ventanas con fin del Core.
            var end = cardEnd ?? start.AddDays(TypicalDistributionDays);
            lo = start;
            if (end < hi) hi = end;
            source = cardEnd is not null && endFromTracking ? "ventana de distribución de las hojas de seguimiento de EventsGallery (Extras/Tracking)"
                : endFromTracking ? $"inicio de la distribución según las hojas de seguimiento de EventsGallery + {TypicalDistributionDays} días (fin desconocido)"
                : $"fecha de inicio de la tarjeta (o del juego) + {TypicalDistributionDays} días (mediana de las distribuciones del Core)";
        }
        else source = "entre el lanzamiento del juego y ayer";

        var hasEgg = enc.IsEgg || pk.IsEgg || HasEggDate(pk);
        var eggDays = hasEgg ? 1 + (int)(Hash(pk, variant, 7) % MaxEggDays) : 0;
        if (pk.IsEgg)
        {
            // Un huevo sin eclosionar solo lleva la fecha en que se recogio.
            if (hi < lo) return null;
            return new DatePlan(default, Pick(lo, hi, pk, variant), source);
        }
        var metLo = hasEgg ? lo.AddDays(eggDays) : lo;
        if (hi < metLo) return null;
        var met = Pick(metLo, hi, pk, variant);
        return new DatePlan(met, hasEgg ? met.AddDays(-eggDays) : null, source);
    }

    private static bool HasEggDate(PKM pk) => pk.EggMetDate is not null && pk.EggLocation != (pk is PB8 ? Locations.Default8bNone : 0);

    private static DateOnly Pick(DateOnly lo, DateOnly hi, PKM pk, int variant)
    {
        var span = hi.DayNumber - lo.DayNumber;
        return span <= 0 ? lo : lo.AddDays((int)(Hash(pk, variant, 1) % (ulong)(span + 1)));
    }

    /// <summary>Hash determinista (misma identidad = misma fecha: dos ejecuciones producen el mismo resultado) y sin estado global.</summary>
    private static ulong Hash(PKM pk, int variant, ulong salt)
    {
        var x = ((ulong)pk.EncryptionConstant << 32 | pk.PID) ^ (salt * 0x9E3779B97F4A7C15UL) ^ ((ulong)(variant + 1) * 0xBF58476D1CE4E5B9UL) ^ pk.Species;
        x ^= x >> 30; x *= 0xBF58476D1CE4E5B9UL;
        x ^= x >> 27; x *= 0x94D049BB133111EBUL;
        x ^= x >> 31;
        return x;
    }

    /// <summary>Escribe las fechas en el Pokemon, respetando donde el Core exige fecha (lugar distinto de «ninguno»: <c>MiscDateVerifier.cs:18-37</c>).</summary>
    public static void Apply(PKM pk, DatePlan plan)
    {
        var none = pk is PB8 ? Locations.Default8bNone : (ushort)0;
        if (!pk.IsEgg && pk.MetLocation != none) pk.MetDate = plan.Met;
        if (plan.Egg is { } egg && pk.EggLocation != none) pk.EggMetDate = egg;
        if (pk is PB7 pb7 && !pk.IsEgg) pb7.ReceivedDate = plan.Met;   // MiscVerifierPB7.cs:46: sin intercambiar, la recepcion ES la captura (y si luego se quita el HT, tambien); con HT puede coincidir
    }
}
