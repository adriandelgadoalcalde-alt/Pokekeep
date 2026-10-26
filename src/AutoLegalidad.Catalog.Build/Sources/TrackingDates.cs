// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Globalization;
using System.Text.RegularExpressions;

namespace AutoLegalidad.Catalog.Sources;

/// <summary>Ventana de distribucion de un evento segun una hoja de seguimiento: inicio inclusivo y fin inclusivo o <c>null</c> (desconocido o abierto).</summary>
public sealed record TrackingWindow(DateOnly Start, DateOnly? End);

/// <summary>
/// Interprete CONSERVADOR del texto libre de fechas de las hojas <c>Extras/Tracking</c> de EventsGallery («Oct 12 2013 - Sep 30 2014», «Jan 9 to 15, 2016», «Aug 7 - Sep 30 2014»...).
/// Solo acepta rangos continuos con un inicio inequivoco (mes, dia y un año explicito en el texto); lo que es una lista de dias sueltos («Jan 4, 5, 11, 12», «Jun 6 - 8, 13, 14»), no trae año o
/// no empieza por una fecha devuelve <c>null</c>: nunca se completa lo que el texto no dice.
/// </summary>
public static partial class TrackingDates
{
    private const string Month = "(jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec)[a-z]*\\.?";

    private static readonly Dictionary<string, int> Months = new(StringComparer.Ordinal)
    { ["jan"] = 1, ["feb"] = 2, ["mar"] = 3, ["apr"] = 4, ["may"] = 5, ["jun"] = 6, ["jul"] = 7, ["aug"] = 8, ["sep"] = 9, ["oct"] = 10, ["nov"] = 11, ["dec"] = 12 };

    [GeneratedRegex("\\b(\\d{1,2})(st|nd|rd|th)\\b")] private static partial Regex Ordinal();
    [GeneratedRegex("\\s+")] private static partial Regex Spaces();
    [GeneratedRegex(Month + "\\s*(\\d{1,2})\\s+(20\\d\\d)\\b")] private static partial Regex MonthDayYear();
    [GeneratedRegex("\\b(\\d{1,2})\\s*" + Month + "\\s+(20\\d\\d)\\b")] private static partial Regex DayMonthYear();
    [GeneratedRegex("^\\s*" + Month + "\\s*(\\d{1,2})(?!\\d)(.*)$")] private static partial Regex StartMonthDay();
    [GeneratedRegex("^\\s*(\\d{1,2})\\s*" + Month + "(.*)$")] private static partial Regex StartDayMonth();
    [GeneratedRegex("^\\s*(20\\d\\d)\\b")] private static partial Regex YearRightAfter();
    [GeneratedRegex("\\b(20\\d\\d)\\b")] private static partial Regex AnyYear();
    [GeneratedRegex("(-|\\bto\\b)\\s*(.*)$")] private static partial Regex AfterDash();
    [GeneratedRegex("(?:-|\\bto\\b)\\s*(?:" + Month + ")?\\s*(\\d{1,2})(?!\\d)\\s*(?:" + Month + ")?\\s*(20\\d\\d)?")] private static partial Regex PartialEnd();
    [GeneratedRegex("[;|]\\s*(?:" + Month + "|\\d)")] private static partial Regex SeparatedList();
    [GeneratedRegex("\\|")] private static partial Regex Pipe();
    [GeneratedRegex("\\d{1,2}\\s*,\\s*(?:\\d{1,2}\\b|[a-z]{3})")] private static partial Regex DayList();
    [GeneratedRegex("20\\d\\d\\s*,\\s*[a-z]{3}")] private static partial Regex YearThenMonth();

    /// <summary>Ventana del texto, o <c>null</c> si no es un rango continuo con inicio inequivoco.</summary>
    public static TrackingWindow? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var raw = text.ToLowerInvariant();
        var t = Spaces().Replace(Ordinal().Replace(raw.Replace('|', ' ').Replace(',', ' '), "$1"), " ");
        t = " " + t + " ";

        // Listas de dias sueltos o de varias instancias separadas: no son una ventana continua.
        if (Pipe().IsMatch(raw) || SeparatedList().IsMatch(raw) || DayList().Matches(raw).Count >= 1 || YearThenMonth().IsMatch(raw) || raw.Contains("multiple", StringComparison.Ordinal))
            return null;

        var full = new List<DateOnly>();
        foreach (Match m in MonthDayYear().Matches(t))
            if (TryDate(int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture), m.Groups[1].Value, int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture)) is { } d) full.Add(d);
        foreach (Match m in DayMonthYear().Matches(t))
            if (TryDate(int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture), m.Groups[2].Value, int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)) is { } d) full.Add(d);

        int day, month;
        string rest;
        var m1 = StartMonthDay().Match(t);
        if (m1.Success) { month = Months[m1.Groups[1].Value]; day = int.Parse(m1.Groups[2].Value, CultureInfo.InvariantCulture); rest = m1.Groups[3].Value; }
        else
        {
            var m2 = StartDayMonth().Match(t);
            if (!m2.Success) return null;
            day = int.Parse(m2.Groups[1].Value, CultureInfo.InvariantCulture); month = Months[m2.Groups[2].Value]; rest = m2.Groups[3].Value;
        }

        var years = AnyYear().Matches(t).Select(m => int.Parse(m.Value, CultureInfo.InvariantCulture)).ToList();
        if (years.Count == 0) return null;
        var explicitYear = YearRightAfter().Match(rest);
        var year = explicitYear.Success ? int.Parse(explicitYear.Groups[1].Value, CultureInfo.InvariantCulture) : years[0];
        if (TryDate(year, month, day) is not { } start) return null;
        // «Dec 23 - Jan 12 2016»: sin año tras el inicio, el año hallado es el del final y el inicio es del año anterior.
        if (!explicitYear.Success && full.Count > 0 && start > full.Max()) start = TryDate(year - 1, month, day) ?? start;

        DateOnly? end = null;
        var dash = AfterDash().Match(t);
        if (dash.Success && !dash.Groups[2].Value.AsSpan(0, Math.Min(3, dash.Groups[2].Value.Length)).Contains('?'))
        {
            var cand = full.Where(d => d >= start).ToList();
            if (cand.Count > 0) end = cand.Max();
        }
        if (end is null)
        {
            var p = PartialEnd().Match(t);
            if (p.Success)
            {
                var endMonth = p.Groups[1].Success ? Months[p.Groups[1].Value] : p.Groups[3].Success ? Months[p.Groups[3].Value] : start.Month;
                var endYear = p.Groups[4].Success ? int.Parse(p.Groups[4].Value, CultureInfo.InvariantCulture) : start.Year;
                if (TryDate(endYear, endMonth, int.Parse(p.Groups[2].Value, CultureInfo.InvariantCulture)) is { } e && e >= start) end = e;
            }
            else if (!AfterDash().IsMatch(t)) end = start;   // un solo dia
        }
        return new TrackingWindow(start, end);
    }

    /// <summary>Fin de la forma «- Nov 17, 2017» (hoja de WC7); <c>null</c> si es «- ?» o no se entiende.</summary>
    public static DateOnly? ParseEnd(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var m = Regex.Match(text.ToLowerInvariant(), "^\\s*-\\s*" + Month + "\\s+(\\d{1,2})\\s*,?\\s*(20\\d\\d)\\b");
        return m.Success ? TryDate(int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture), m.Groups[1].Value, int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture)) : null;
    }

    private static DateOnly? TryDate(int year, string month3, int day) => Months.TryGetValue(month3[..3], out var m) ? TryDate(year, m, day) : null;

    private static DateOnly? TryDate(int year, int month, int day)
        => year is >= 2000 and <= 2100 && month is >= 1 and <= 12 && day >= 1 && day <= DateTime.DaysInMonth(year, month) ? new DateOnly(year, month, day) : null;
}
