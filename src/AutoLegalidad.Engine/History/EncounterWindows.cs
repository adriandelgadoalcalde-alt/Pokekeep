// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;

namespace AutoLegalidad.Engine.History;

/// <summary>Ventana de fechas en la que un encuentro pudo conseguirse de verdad (inicio inclusivo; fin inclusivo o abierto) y de donde sale.</summary>
public sealed record DateWindow(DateOnly Start, DateOnly? End, string Source);

/// <summary>
/// Hito 4c (R-010): de donde sale la ventana REAL de fechas de un encuentro. Solo lo que el Core publica: las ventanas de distribucion de los regalos (<c>EncounterServerDate.cs</c>: WB7, WC8, WA8,
/// WB8, WC9, WA9) y las de GO (<c>IPogoDateRange</c>, empaquetadas por PoGoEncTool: <c>EncounterSlot7GO/8GO</c>). Sin ventana conocida el llamador elige una fecha plausible (<see cref="DatePolicy"/>).
/// </summary>
public static class EncounterWindows
{
    /// <summary>Ventana del Core para el encuentro, o null si no tiene (salvaje, estatico, huevo, regalo sin restriccion...).</summary>
    public static DateWindow? Of(IEncounterTemplate enc)
    {
        DistributionWindow w;
        switch (enc)
        {
            case WB7 c when c.IsDateRestricted && c.GetDistributionWindow(out w): return From(w, "ventana de distribución del Core (EncounterServerDate.cs, WB7)");
            case WC8 c when c.IsDateRestricted && c.GetDistributionWindow(out w): return From(w, "ventana de distribución del Core (EncounterServerDate.cs, WC8)");
            case WA8 c when c.GetDistributionWindow(out w): return From(w, "ventana de distribución del Core (EncounterServerDate.cs, WA8)");
            case WB8 c when c.IsDateRestricted && c.GetDistributionWindow(out w): return From(w, "ventana de distribución del Core (EncounterServerDate.cs, WB8)");
            case WC9 c when c.GetDistributionWindow(out w): return From(w, "ventana de distribución del Core (EncounterServerDate.cs, WC9)");
            case WA9 c when c.GetDistributionWindow(out w): return From(w, "ventana de distribución del Core (EncounterServerDate.cs, WA9)");
            case IPogoDateRange g when enc is EncounterSlot7GO or EncounterSlot8GO:
                return FromGo(g);
            default: return null;
        }
    }

    private static DateWindow From(DistributionWindow w, string source) => new(w.GetGenerateDate(), w.End, source);

    /// <summary>
    /// Dia 0 de las fechas de GO del Core: <c>IPogoDateRange.FirstDay</c> (interno, 736150 - 1 = lanzamiento de GO, 2016-07-06, menos un dia por los husos horarios).
    /// Desde PKHeX 26.10.18 el Core guarda <c>DayStart</c>/<c>DayEnd</c> como dias desde ese origen (0 = sin limite) en lugar de la fecha empaquetada AAAAMMDD.
    /// </summary>
    public static readonly int GoFirstDayNumber = new DateOnly(2016, 7, 6).DayNumber - 1;

    /// <summary>
    /// Ventana de GO de una casilla del Core. Con inicio/fin «locales» (<c>IsLocalDayStart</c>/<c>IsLocalDayEnd</c>: la fecha depende del huso del jugador) se toma el intervalo
    /// seguro, un dia por dentro de cada extremo marcado, igual que <c>IPogoDateRange.GetRandomValidDate</c> y que el nombre largo de la casilla en el Core. Si el intervalo seguro
    /// queda vacio (ventana de un dia) se usa el bruto, que es el que valida el Core (<c>IsWithinStartEnd</c>).
    /// </summary>
    public static DateWindow FromGo(IPogoDateRange g)
    {
        const string source = "ventana de GO del Core (IPogoDateRange, datos de PoGoEncTool)";
        var startDay = g.DayStart == 0 ? (int?)null : g.DayStart;
        var endDay = g.DayEnd == 0 ? (int?)null : g.DayEnd;
        if (startDay is { } s0 && endDay is { } e0)
        {
            var s = s0 + (g.IsLocalDayStart ? 1 : 0);
            var e = e0 - (g.IsLocalDayEnd ? 1 : 0);
            if (s > e) { s = s0; e = e0; }
            return new DateWindow(DateOnly.FromDayNumber(GoFirstDayNumber + s), DateOnly.FromDayNumber(GoFirstDayNumber + e), source);
        }
        var start = startDay is { } sd ? DateOnly.FromDayNumber(GoFirstDayNumber + sd + (g.IsLocalDayStart ? 1 : 0)) : GameReleaseDates.GoFloor;
        DateOnly? end = endDay is { } ed ? DateOnly.FromDayNumber(GoFirstDayNumber + ed - (g.IsLocalDayEnd ? 1 : 0)) : null;
        return new DateWindow(start, end, source);
    }

    /// <summary>True si la fecha cae en la ventana (el Core la ha comprobado ya; esto es para la metrica y las pruebas).</summary>
    public static bool Contains(DateWindow w, DateOnly d) => d >= w.Start && (w.End is not { } e || d <= e);
}
