// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Reflection;
using System.Text.Json;
using PKHeX.Core;

namespace AutoLegalidad.Catalog.Sources;

/// <summary>Una casilla GO del Core (<c>EncountersGO.SlotsGO</c> / <c>SlotsGO_GG</c>) normalizada para compararla con una ventana de PoGoEncTool.</summary>
public sealed record GoCoreSlot(int Species, int Form, int Start, int End, string Shiny, string Gender, string Type, string Route)
{
    public string Key => $"{Species}-{Form}|{Start}|{End}|{Shiny}|{Gender}|{Type}";
}

/// <summary>Resultado del cruce de UNA ventana de PoGoEncTool con las tablas GO del Core fijado.</summary>
public enum GoMatch
{
    /// <summary>Hay una casilla del Core con la misma especie, forma, tipo, variocolor, sexo y fechas (tras aplicar la tolerancia de inicio/fin que PoGoEncTool empaqueta).</summary>
    Identical,
    /// <summary>Casi: solo las fechas difieren en un dia.</summary>
    DatesOffByOneDay,
    /// <summary>La ventana no existe en el Core fijado (los datos de PoGoEncTool son mas nuevos, o la ventana no se empaqueta: Gigantamax...).</summary>
    PoGoOnly,
}

public sealed record GoCrossResult(IReadOnlyDictionary<string, (GoMatch Match, string? CoreRoute)> ByWindow, int CoreSlotsTotal, int CoreSlotsLgpe, int CoreSlotsHome,
    int CoreOnly, IReadOnlyDictionary<string, int> CoreOnlyByType, IReadOnlyDictionary<string, int> PoGoOnlyByType);

/// <summary>
/// Cruce de las ventanas de PoGoEncTool (<c>pogo_window</c>, 11.170 ventanas, solo DATOS) con las tablas GO del Core fijado (Hito 3, pendiente del Hito 1). El Core es la
/// AUTORIDAD (regla 12): una ventana que el Core reproduce exactamente queda corroborada por una fuente aprobada; la que no, se registra como <c>PoGoOnly</c> con su motivo
/// y NO se usa para generar. Las tablas del Core son <c>internal static</c>: se leen por reflexion sobre el ensamblado del commit fijado y falla en voz alta si cambian.
/// </summary>
public static class GoCrossCheck
{
    public static IReadOnlyList<GoCoreSlot> ReadCore()
    {
        var asm = typeof(PKM).Assembly;
        var t = asm.GetType("PKHeX.Core.EncountersGO") ?? throw new InvalidOperationException("El Core fijado no tiene EncountersGO: ¿cambio su estructura interna?");
        const BindingFlags all = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        var list = new List<GoCoreSlot>(20_000);

        if (t.GetField("SlotsGO", all)?.GetValue(null) is not EncounterArea8g[] home)
            throw new InvalidOperationException("EncountersGO.SlotsGO no es EncounterArea8g[].");
        foreach (var area in home)
            foreach (var s in area.Slots)
                list.Add(new GoCoreSlot(s.Species, s.Form, PackDay(s.DayStart), PackDay(s.DayEnd), Shiny(s.Shiny), Gender(s.Gender), s.Type.ToString(), "HOME:" + s.OriginFormat));

        if (t.GetField("SlotsGO_GG", all)?.GetValue(null) is not EncounterArea7g[] gg)
            throw new InvalidOperationException("EncountersGO.SlotsGO_GG no es EncounterArea7g[].");
        foreach (var area in gg)
            foreach (var s in area.Slots)
                list.Add(new GoCoreSlot(s.Species, s.Form, PackDay(s.DayStart), PackDay(s.DayEnd), Shiny(s.Shiny), Gender(s.Gender), s.Type.ToString(), "LGPE"));
        return list;
    }

    /// <summary>Cruza las ventanas con las casillas HOME y, por separado, con las de Let's Go (una ventana puede estar en las dos rutas). El estado de la ventana es el de la ruta HOME.</summary>
    public static GoCrossResult Compare(IReadOnlyList<GoWindow> windows, IReadOnlyList<GoCoreSlot> core)
    {
        var home = core.Where(c => c.Route != "LGPE").ToList();
        var lgpe = core.Where(c => c.Route == "LGPE").ToList();
        var homeMatch = Match(windows, home, out var homeCoreOnly);
        var lgpeMatch = Match(windows, lgpe, out var lgpeCoreOnly);
        var result = new Dictionary<string, (GoMatch, string?)>(windows.Count, StringComparer.Ordinal);
        var poGoOnlyByType = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var w in windows)
        {
            var h = homeMatch[w.Key];
            var l = lgpeMatch[w.Key];
            var route = h.Match != GoMatch.PoGoOnly && l.Match != GoMatch.PoGoOnly ? "HOME+LGPE" : h.Match != GoMatch.PoGoOnly ? "HOME" : l.Match != GoMatch.PoGoOnly ? "LGPE" : null;
            // Estado: la ventana esta corroborada si el Core la reproduce en ALGUNA de sus dos rutas.
            var best = h.Match == GoMatch.Identical || l.Match == GoMatch.Identical ? GoMatch.Identical
                : h.Match == GoMatch.DatesOffByOneDay || l.Match == GoMatch.DatesOffByOneDay ? GoMatch.DatesOffByOneDay : GoMatch.PoGoOnly;
            result[w.Key] = (best, route);
            if (best == GoMatch.PoGoOnly) poGoOnlyByType[w.EncounterType] = poGoOnlyByType.GetValueOrDefault(w.EncounterType) + 1;
        }
        var coreOnly = homeCoreOnly.Concat(lgpeCoreOnly).ToList();
        return new GoCrossResult(result, core.Count, lgpe.Count, home.Count, coreOnly.Count,
            coreOnly.GroupBy(c => c.Route == "LGPE" ? "LGPE:" + c.Type : c.Type).OrderBy(g => g.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count()), poGoOnlyByType);
    }

    private static Dictionary<string, (GoMatch Match, GoCoreSlot? Slot)> Match(IReadOnlyList<GoWindow> windows, IReadOnlyList<GoCoreSlot> core, out List<GoCoreSlot> coreOnly)
    {
        // Multiconjunto de casillas del Core por clave exacta y por clave sin fechas (para la coincidencia «a un dia»).
        var byKey = core.GroupBy(c => c.Key).ToDictionary(g => g.Key, g => new Queue<GoCoreSlot>(g));
        var byLoose = core.GroupBy(c => $"{c.Species}-{c.Form}|{c.Shiny}|{c.Gender}|{c.Type}").ToDictionary(g => g.Key, g => g.ToList());
        var used = new HashSet<GoCoreSlot>(ReferenceEqualityComparer.Instance);
        var result = new Dictionary<string, (GoMatch, GoCoreSlot?)>(windows.Count, StringComparer.Ordinal);
        foreach (var w in windows)
        {
            var (localized, tolerance, gigantamax) = Flags(w.ConstraintsJson);
            var start = Pack(w.StartDay, localized ? -1 : 0);
            var end = Pack(w.EndDay, tolerance ? 1 : 0);
            var shiny = w.ShinyState switch { "Never" => "Never", "Always" => "Always", _ => "Random" };
            var gender = w.GenderRule switch { "MaleOnly" => "Male", "FemaleOnly" => "Female", _ => "Random" };
            var key = $"{w.Species}-{w.Form}|{start}|{end}|{shiny}|{gender}|{w.EncounterType}";

            GoCoreSlot? hit = null;
            if (byKey.TryGetValue(key, out var q))
                while (q.Count > 0) { var c = q.Dequeue(); if (used.Add(c)) { hit = c; break; } }
            if (hit is not null) { result[w.Key] = (GoMatch.Identical, hit); continue; }

            if (!gigantamax && byLoose.TryGetValue($"{w.Species}-{w.Form}|{shiny}|{gender}|{w.EncounterType}", out var loose))
            {
                var near = loose.FirstOrDefault(c => !used.Contains(c) && Close(c.Start, start) && Close(c.End, end));
                if (near is not null) { used.Add(near); result[w.Key] = (GoMatch.DatesOffByOneDay, near); continue; }
            }
            result[w.Key] = (GoMatch.PoGoOnly, null);
        }
        coreOnly = core.Where(c => !used.Contains(c)).ToList();
        return result.ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    private static bool Close(int a, int b)
    {
        if (a == b) return true;
        if (a == 0 || b == 0) return false;
        var da = Date(a); var db = Date(b);
        return da is not null && db is not null && Math.Abs(da.Value.DayNumber - db.Value.DayNumber) <= 1;
    }

    /// <summary>El Core empaqueta un dia como <c>(anio &lt;&lt; 16) | (mes &lt;&lt; 8) | dia</c>; 0 = sin fecha. <paramref name="offsetDays"/>: -1 si el inicio es «local», +1 si el fin tiene tolerancia.</summary>
    public static int Pack(string? isoDay, int offsetDays)
    {
        if (isoDay is null) return 0;
        if (!DateOnly.TryParseExact(isoDay, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d)) return 0;
        d = d.AddDays(offsetDays);
        return (d.Year << 16) | (d.Month << 8) | d.Day;
    }

    /// <summary>
    /// Desde PKHeX 26.10.18 el Core guarda los dias de GO como <c>ushort</c> desde <c>IPogoDateRange.FirstDay</c> (interno: 2016-07-06 menos un dia; 0 = sin limite). Se convierten al
    /// empaquetado de siempre (<c>aaaammdd</c>) para que la comparacion con las ventanas de PoGoEncTool no cambie. El dia guardado es el bruto: las banderas de dia local se aplican
    /// aparte (el inicio local ya viene un dia antes, el fin local un dia despues), igual que en <c>Pack(isoDay, offset)</c>.
    /// </summary>
    public static int PackDay(ushort day)
    {
        if (day == 0) return 0;
        var d = DateOnly.FromDayNumber(new DateOnly(2016, 7, 6).DayNumber - 1 + day);
        return (d.Year << 16) | (d.Month << 8) | d.Day;
    }

    private static DateOnly? Date(int packed)
    {
        try { return new DateOnly(packed >> 16, (packed >> 8) & 0xFF, packed & 0xFF); }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    private static (bool Localized, bool Tolerance, bool Gigantamax) Flags(string constraintsJson)
    {
        using var doc = JsonDocument.Parse(constraintsJson);
        bool B(string n) => doc.RootElement.TryGetProperty(n, out var e) && e.ValueKind == JsonValueKind.True;
        return (B("localizedStart"), B("hasEndTolerance"), B("isGigantamax"));
    }

    private static string Shiny(Shiny s) => s switch { PKHeX.Core.Shiny.Never => "Never", PKHeX.Core.Shiny.Always => "Always", _ => "Random" };
    private static string Gender(Gender g) => g switch { PKHeX.Core.Gender.Male => "Male", PKHeX.Core.Gender.Female => "Female", _ => "Random" };
}
