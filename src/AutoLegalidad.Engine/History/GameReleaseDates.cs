// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;

namespace AutoLegalidad.Engine.History;

/// <summary>
/// Hito 4c (R-010): fechas REALES de lanzamiento de cada juego principal de Pokemon (fuente y verificacion en <c>docs/datos/lanzamientos-juegos.tsv</c>: Wikipedia, Serebii y Nintendo Life; una sola
/// fuente por fecha). El Core no trae ninguna tabla de lanzamientos (solo ventanas de distribucion de eventos y el calendario valido de cada consola: <c>EncounterDate.IsValidDate*</c>), asi que
/// «posterior al lanzamiento» se decide aqui. Se usa SOLO para Gen4+ (Gen1-3 no guardan fecha de captura).
/// </summary>
public static class GameReleaseDates
{
    // (juego, Japon, Norteamerica, Europa). Orden del TSV.
    private static readonly Dictionary<GameVersion, (DateOnly Jp, DateOnly Na, DateOnly Eu)> Table = Build();

    private static DateOnly D(int y, int m, int d) => new(y, m, d);

    private static Dictionary<GameVersion, (DateOnly, DateOnly, DateOnly)> Build()
    {
        var t = new Dictionary<GameVersion, (DateOnly, DateOnly, DateOnly)>();
        void Add((DateOnly, DateOnly, DateOnly) v, params GameVersion[] vs) { foreach (var g in vs) t[g] = v; }
        Add((D(2006, 9, 28), D(2007, 4, 22), D(2007, 7, 27)), GameVersion.D, GameVersion.P);
        Add((D(2008, 9, 13), D(2009, 3, 22), D(2009, 5, 22)), GameVersion.Pt);
        Add((D(2009, 9, 12), D(2010, 3, 14), D(2010, 3, 26)), GameVersion.HG, GameVersion.SS);
        Add((D(2010, 9, 18), D(2011, 3, 6), D(2011, 3, 4)), GameVersion.B, GameVersion.W);
        Add((D(2012, 6, 23), D(2012, 10, 7), D(2012, 10, 12)), GameVersion.B2, GameVersion.W2);
        Add((D(2013, 10, 12), D(2013, 10, 12), D(2013, 10, 12)), GameVersion.X, GameVersion.Y);
        Add((D(2014, 11, 21), D(2014, 11, 21), D(2014, 11, 28)), GameVersion.OR, GameVersion.AS);
        Add((D(2016, 11, 18), D(2016, 11, 18), D(2016, 11, 23)), GameVersion.SN, GameVersion.MN);
        Add((D(2017, 11, 17), D(2017, 11, 17), D(2017, 11, 17)), GameVersion.US, GameVersion.UM);
        Add((D(2018, 11, 16), D(2018, 11, 16), D(2018, 11, 16)), GameVersion.GP, GameVersion.GE);
        Add((D(2019, 11, 15), D(2019, 11, 15), D(2019, 11, 15)), GameVersion.SW, GameVersion.SH);
        Add((D(2021, 11, 19), D(2021, 11, 19), D(2021, 11, 19)), GameVersion.BD, GameVersion.SP);
        Add((D(2022, 1, 28), D(2022, 1, 28), D(2022, 1, 28)), GameVersion.PLA);
        Add((D(2022, 11, 18), D(2022, 11, 18), D(2022, 11, 18)), GameVersion.SL, GameVersion.VL);
        Add((D(2025, 10, 16), D(2025, 10, 16), D(2025, 10, 16)), GameVersion.ZA);
        return t;
    }

    /// <summary>Primer lanzamiento de la generacion de FORMATO (la primera consola/juego que guarda ese formato): suelo para fechas de transferencia.</summary>
    public static DateOnly FormatFloor(byte format) => format switch
    {
        4 => D(2006, 9, 28),    // Diamante/Perla (Japon)
        5 => D(2010, 9, 18),    // Negro/Blanco (Japon)
        6 => D(2013, 10, 12),   // X/Y
        7 => D(2016, 11, 18),   // Sol/Luna
        8 => D(2019, 11, 15),   // Espada/Escudo
        9 => D(2022, 11, 18),   // Escarlata/Purpura
        _ => D(2000, 1, 1),
    };

    /// <summary>Lanzamiento de Pokemon Bank y Poke Transporter (Japon 2013-12-25, Europa 2014-02-04, EE. UU. 2014-02-05): suelo de las transferencias Gen1-5 -> Gen6/7.</summary>
    public static readonly DateOnly BankFloor = D(2014, 2, 5);

    /// <summary>Lanzamiento general de Pokemon HOME (2020-02-12): suelo de las transferencias a Gen8+ de Pokemon de juegos anteriores.</summary>
    public static readonly DateOnly HomeFloor = D(2020, 2, 12);

    /// <summary>Lanzamiento de Pokemon GO (EE. UU./Australia/Nueva Zelanda 2016-07-06; Europa desde 2016-07-13; Japon 2016-07-22).</summary>
    public static readonly DateOnly GoFloor = D(2016, 7, 6);

    /// <summary>
    /// Primer dia en que el juego existia para el idioma del Pokemon: Japones/Coreano/Chino = Japon; Ingles = el primero de Norteamerica y Europa; el resto de idiomas europeos = Europa. Null si el juego no esta en la tabla (Gen1-3...).
    /// </summary>
    public static DateOnly? For(GameVersion version, int language)
    {
        if (!Table.TryGetValue(version, out var r)) return null;
        return (LanguageID)language switch
        {
            LanguageID.Japanese or LanguageID.Korean or LanguageID.ChineseS or LanguageID.ChineseT => r.Jp,
            LanguageID.English => r.Na < r.Eu ? r.Na : r.Eu,
            _ => r.Eu,
        };
    }

    /// <summary>Lanzamiento mas temprano en cualquier region (para el analizador: una fecha anterior a esto es imposible en cualquier idioma).</summary>
    public static DateOnly? Earliest(GameVersion version) => Table.TryGetValue(version, out var r) ? Min(r.Jp, Min(r.Na, r.Eu)) : null;

    private static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;
}
