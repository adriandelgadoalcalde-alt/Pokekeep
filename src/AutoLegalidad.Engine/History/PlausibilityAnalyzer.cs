// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Diagnostics.CodeAnalysis;
using AutoLegalidad.Engine.Catalog;
using AutoLegalidad.Engine.Context;
using PKHeX.Core;

namespace AutoLegalidad.Engine.History;

/// <summary>Una incoherencia de historia hallada en un Pokemon (codigo estable + detalle legible).</summary>
public sealed record PlausibilityIssue(string Code, string Detail, int Count = 1);

/// <summary>Resultado del analisis de plausibilidad de UN Pokemon. Vacio = historia coherente.</summary>
public sealed record PlausibilityReport(IReadOnlyList<PlausibilityIssue> Issues)
{
    public bool Clean => Issues.Count == 0;
    public int Total(string code) => Issues.Where(i => i.Code == code).Sum(i => i.Count);
    public bool Has(string code) => Issues.Any(i => i.Code == code);
}

/// <summary>Codigos de incoherencia (columnas de la metrica del banco, ADR 0028).</summary>
public static class PlausibilityCodes
{
    /// <summary>Cintas o marcas de encuentro de MAS respecto de las que el origen otorga o exige (cuenta unidades).</summary>
    public const string ExtraRibbons = "CINTAS_EXTRA";
    public const string DateBeforeRelease = "FECHA_ANTES_DEL_LANZAMIENTO";
    public const string DateNotBeforeToday = "FECHA_NO_ANTERIOR_A_HOY";
    public const string DateOutsideWindow = "FECHA_FUERA_DE_VENTANA";
    public const string DateBeforeCardStart = "FECHA_ANTES_DEL_INICIO_DE_LA_TARJETA";
    public const string EggAfterHatch = "HUEVO_POSTERIOR_A_LA_ECLOSION";
    public const string LevelBelowMet = "NIVEL_MENOR_QUE_EL_DE_ENCUENTRO";
    public const string MemoryIncoherent = "MEMORIA_INCOHERENTE";
    public const string UnjustifiedHandler = "HT_INJUSTIFICADO";
    public const string AppliedMarkings = "MARCAS_APLICADAS";
    public const string FriendshipUnjustified = "AMISTAD_SIN_MOTIVO";
    public const string Pokerus = "POKERUS_SIN_MOTIVO";
    public const string ContestMax = "CONCURSO_AL_MAXIMO";
    public const string DynamaxLevel = "DYNAMAX_SIN_MOTIVO";
    public const string ScaleInvalid = "TAMANO_INCOHERENTE";
    /// <summary>Recuerdo (OT o HT) vacio donde el juego real habria puesto uno (ADR 0042).</summary>
    public const string MemoryEmpty = "MEMORIA_VACIA";

    public static readonly string[] All =
    [
        ExtraRibbons, DateBeforeRelease, DateNotBeforeToday, DateOutsideWindow, DateBeforeCardStart, EggAfterHatch, LevelBelowMet,
        MemoryIncoherent, UnjustifiedHandler, AppliedMarkings, FriendshipUnjustified, Pokerus, ContestMax, DynamaxLevel, ScaleInvalid, MemoryEmpty,
    ];
}

/// <summary>
/// Hito 4c (R-010): metrica de plausibilidad. Cuenta, para un Pokemon ya construido, lo que un jugador notaria como «no parece capturado de verdad» aunque el Core lo de por valido: cintas que el origen no otorga,
/// fechas fuera de ventana o del calendario del juego, huevo recogido despues de eclosionar, memorias sin sentido, entrenador anterior sin recorrido que lo justifique y valores de historial al maximo sin motivo.
/// Es la MISMA definicion que aplica la fase «Historia» (<see cref="HistoryPhase"/>): lo que el banco mide como incoherencia es lo que la fase corrige; asi el objetivo «0 incoherencias en lo generado» es comprobable.
/// </summary>
public static class PlausibilityAnalyzer
{
    [RequiresUnreferencedCode("Cuenta las cintas por reflexion (RibbonInfo.GetRibbonInfo).")]
    public static PlausibilityReport Analyze(PKM pk, Destination dest, DateOnly today, CatalogReader? catalog = null)
    {
        var issues = new List<PlausibilityIssue>();
        void Add(string code, string detail, int count = 1) => issues.Add(new PlausibilityIssue(code, detail, count));
        var la = HistoryPhase.Analyze(pk, dest);
        if (!la.Parsed) return new PlausibilityReport([new PlausibilityIssue("NO_ANALIZABLE", "El analisis oficial no pudo procesar la entidad.")]);
        var enc = la.EncounterMatch;

        // ---- cintas y marcas
        var extra = RibbonHistory.Extra(pk, enc, la.Info.EvoChainsAllGens);
        if (extra > 0) Add(PlausibilityCodes.ExtraRibbons, $"{extra} cintas o marcas que el origen no otorga", extra);

        // ---- fechas (Gen4+)
        if (pk.Format >= 4)
        {
            var none = pk is PB8 ? Locations.Default8bNone : (ushort)0;
            var floor = DatePolicy.Floor(pk, enc);
            var window = EncounterWindows.Of(enc);
            DateOnly? cardStart = null, cardEnd = null;
            if (catalog is not null && EncounterKeys.For(la.EncounterOriginal) is { } key && key.StartsWith("core-mgdb:", StringComparison.Ordinal) && catalog.EventWindow(key) is { } ew)
            {
                cardStart = ew.Start;
                cardEnd = ew.FromTracking ? ew.End : null;   // solo el fin de las hojas es un dato real; el de la tarjeta se estima (+55 dias) y no se exige
            }
            void CheckDate(DateOnly? d, string what, bool isMet)
            {
                if (d is not { } v) return;
                if (v < floor && !(isMet && window is not null && EncounterWindows.Contains(window, v))) Add(PlausibilityCodes.DateBeforeRelease, $"{what} {v:yyyy-MM-dd} anterior a {floor:yyyy-MM-dd}");
                if (v >= today) Add(PlausibilityCodes.DateNotBeforeToday, $"{what} {v:yyyy-MM-dd} no es anterior a hoy ({today:yyyy-MM-dd})");
                if (isMet && window is not null && !EncounterWindows.Contains(window, v)) Add(PlausibilityCodes.DateOutsideWindow, $"{what} {v:yyyy-MM-dd} fuera de la ventana {window.Start:yyyy-MM-dd}..{window.End:yyyy-MM-dd}");
                if (isMet && cardStart is { } cs && v < cs) Add(PlausibilityCodes.DateBeforeCardStart, $"{what} {v:yyyy-MM-dd} anterior al inicio de la tarjeta {cs:yyyy-MM-dd}");
                if (isMet && cardEnd is { } ce && v > ce) Add(PlausibilityCodes.DateOutsideWindow, $"{what} {v:yyyy-MM-dd} posterior al fin de la distribución {ce:yyyy-MM-dd} (hojas de seguimiento)");
            }
            if (!pk.IsEgg && pk.MetLocation != none) CheckDate(pk.MetDate, "captura", true);
            if (pk.EggLocation != none) CheckDate(pk.EggMetDate, "huevo", false);
            if (!pk.IsEgg && pk.MetLocation != none && pk.EggLocation != none && pk.MetDate is { } m && pk.EggMetDate is { } e && e > m)
                Add(PlausibilityCodes.EggAfterHatch, $"huevo {e:yyyy-MM-dd} posterior a la eclosión {m:yyyy-MM-dd}");
        }

        // ---- nivel de encuentro
        if (!pk.IsEgg && pk.MetLevel > 0 && pk.CurrentLevel < pk.MetLevel) Add(PlausibilityCodes.LevelBelowMet, $"nivel {pk.CurrentLevel} < nivel de encuentro {pk.MetLevel}");

        // ---- entrenador anterior y memorias
        TrainerHistory.Analyze(pk, la, dest, issues);
        MemoryHistory.Analyze(pk, la, dest, issues);

        // ---- valores de historial
        var marks = MiscHistory.AppliedMarkings(pk);
        if (marks > 0) Add(PlausibilityCodes.AppliedMarkings, $"{marks} marcas de aspecto aplicadas", marks);
        if (!pk.IsEgg && pk.Format >= 3)
        {
            // Solo se señala lo que se puede corregir: el valor creible debe dar un Pokemon que PKHeX siga dando por valido (el Core fija algunas amistades por su cuenta, p. ej. en Let's Go).
            var fixedFriend = pk.Clone();
            if (MiscHistory.ApplyFriendship(fixedFriend, enc) && HistoryPhase.Analyze(fixedFriend, dest).Valid)
                Add(PlausibilityCodes.FriendshipUnjustified, $"amistad {pk.OriginalTrainerFriendship}/{pk.HandlingTrainerFriendship} (la creíble es {fixedFriend.OriginalTrainerFriendship}/{fixedFriend.HandlingTrainerFriendship}) o afecto de Gen6/7 distinto de 0");
        }
        if (pk.PokerusStrain != 0 || pk.PokerusDays != 0) Add(PlausibilityCodes.Pokerus, $"Pokérus cepa {pk.PokerusStrain} días {pk.PokerusDays}");
        if (pk is IContestStatsReadOnly c && pk.Species != (ushort)Species.Milotic && (c.ContestCool == 255 || c.ContestBeauty == 255 || c.ContestCute == 255 || c.ContestSmart == 255 || c.ContestTough == 255))
            Add(PlausibilityCodes.ContestMax, "estadísticas de concurso al máximo");
        if (MiscHistory.LowerValidDynamax(pk, dest) is { } lower)
            Add(PlausibilityCodes.DynamaxLevel, $"nivel Dynamax {((IDynamaxLevel)pk).DynamaxLevel} cuando PKHeX admite {lower}");
        if (MiscHistory.OutbreakScaleOutOfRange(pk, enc) is { } range)
            Add(PlausibilityCodes.ScaleInvalid, $"escala {((IScaledSize3)pk).Scale} fuera del rango {range.Min}-{range.Max} que fija su brote");
        if (MiscHistory.ZeroSize(pk, enc))
            Add(PlausibilityCodes.ScaleInvalid, "altura y peso a 0 (el juego los genera con la distribución triangular de 0 a 255)");
        return new PlausibilityReport(issues);
    }
}
