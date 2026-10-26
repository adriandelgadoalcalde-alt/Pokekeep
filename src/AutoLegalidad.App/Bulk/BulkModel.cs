// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Text;
using AutoLegalidad.Contracts;
using AutoLegalidad.Ui.Operation;

namespace AutoLegalidad.Ui.Bulk;

/// <summary>Estado final de una fila del informe de una operación masiva (diseño §1.11 y §2.3).</summary>
public enum BulkStatus
{
    // ---- «Completar el juego» ----
    Generated,
    /// <summary>R-009: colocado SIN matrícula de HOME (<c>SuccessNeedsHome</c>): nunca «legal».</summary>
    NeedsHome,
    AlreadyHad,
    /// <summary>No se puede generar (imposible demostrado, sin origen ofrecible o sin solución): con su motivo.</summary>
    CannotGenerate,
    /// <summary>Solo existe pasando por HOME y el ajuste «sin matrícula» está desactivado.</summary>
    OnlyWithHome,
    /// <summary>Esa forma solo existe en el equipo o en combate: no cabe en una caja.</summary>
    NotStorable,
    /// <summary>El Core demostró que esa especie y forma no existen en este juego: se excluye de lo planificado con la prueba.</summary>
    NotInGame,
    /// <summary>Hito 4c: existe en el juego pero solo con un DLC que esta partida no tiene.</summary>
    RequiresExpansion,
    TimedOut,
    EngineError,
    /// <summary>No cupo: faltaban casillas libres («hasta donde quepa»).</summary>
    NoRoom,
    SlotTaken,
    FailedFinalCheck,
    Cancelled,

    // ---- «Edición masiva» ----
    Changed,
    /// <summary>Hito 5d (ADR 0046): el Pokémon se sustituyó por su versión de evento (cambia el origen).</summary>
    EventVersion,
    AlreadyOptimal,
    /// <summary>Ninguna operación pedida se pudo aplicar a este Pokémon.</summary>
    NotApplicable,
    NotLegalFirst,
    /// <summary>El usuario cambió la casilla mientras se procesaba.</summary>
    SlotChanged,
    /// <summary>Omitido a propósito (huevo, casilla bloqueada...).</summary>
    Skipped,
}

public static class BulkStatusText
{
    public static string Label(BulkStatus s) => s switch
    {
        BulkStatus.Generated => AppIdioma.T("Generado", "Generated"),
        BulkStatus.NeedsHome => AppIdioma.T("Necesita pasar por HOME", "Needs to pass through HOME"),
        BulkStatus.AlreadyHad => AppIdioma.T("Ya lo tenías", "You already had it"),
        BulkStatus.CannotGenerate => AppIdioma.T("No se puede generar", "Can’t be generated"),
        BulkStatus.OnlyWithHome => AppIdioma.T("Solo con el ajuste de HOME", "Only with the HOME setting"),
        BulkStatus.NotStorable => AppIdioma.T("No cabe en una caja", "Doesn’t fit in a box"),
        BulkStatus.NotInGame => AppIdioma.T("No existe en este juego", "Doesn’t exist in this game"),
        BulkStatus.RequiresExpansion => AppIdioma.T("Requiere un DLC", "Requires DLC"),
        BulkStatus.TimedOut => AppIdioma.T("Tiempo agotado", "Time ran out"),
        BulkStatus.EngineError => AppIdioma.T("Error del motor", "Engine error"),
        BulkStatus.NoRoom => AppIdioma.T("No cabe", "Doesn’t fit"),
        BulkStatus.SlotTaken => AppIdioma.T("Casilla ocupada mientras se generaba", "Slot taken while generating"),
        BulkStatus.FailedFinalCheck => AppIdioma.T("No superó la comprobación final", "Did not pass the final check"),
        BulkStatus.Cancelled => AppIdioma.T("Cancelado", "Canceled"),
        BulkStatus.Changed => AppIdioma.T("Cambiado", "Changed"),
        BulkStatus.EventVersion => AppIdioma.T("Pasa a versión de evento", "Switches to the event version"),
        BulkStatus.AlreadyOptimal => AppIdioma.T("Ya estaba óptimo", "Was already optimal"),
        BulkStatus.NotApplicable => AppIdioma.T("No aplicable", "Not applicable"),
        BulkStatus.NotLegalFirst => AppIdioma.T("Primero hay que legalizarlo", "It has to be legalized first"),
        BulkStatus.SlotChanged => AppIdioma.T("Cambiado por ti mientras se procesaba", "Changed by you while processing"),
        BulkStatus.Skipped => AppIdioma.T("Omitido", "Skipped"),
        _ => s.ToString(),
    };

    /// <summary>Símbolo de una operación en la columna del informe de la edición masiva (✔ cambiado · = ya óptimo · — no aplicable · ✖ fallo).</summary>
    public static string Symbol(OperationStatus s) => s switch
    {
        OperationStatus.Changed => "✔",
        OperationStatus.AlreadyOptimal => "=",
        OperationStatus.NotApplicable => "—",
        _ => "✖",
    };

    public static string Name(string operation) => operation switch
    {
        AdjustOperationNames.Legalize => AppIdioma.T("Legalizar", "Legalize"),
        AdjustOperationNames.MaxLevel => AppIdioma.T("Nivel", "Level"),
        AdjustOperationNames.Optimize => "Stats",
        AdjustOperationNames.MaxFriendship => AppIdioma.T("Amistad", "Friendship"),
        AdjustOperationNames.SpanishLanguage => "Idioma",
        AdjustOperationNames.MaxPP => "PP",
        AdjustOperationNames.HealParty => AppIdioma.T("PS/estado", "HP/status"),
        AdjustOperationNames.CurePokerus => "Pokérus",
        _ => operation,
    };
}

/// <summary>Una fila del informe final: un Pokémon (o especie-forma) con su estado y su motivo.</summary>
public sealed record BulkReportRow
{
    public required int Number { get; init; }
    public required string Pokemon { get; init; }
    public string? Form { get; init; }
    public required BulkStatus Status { get; init; }
    /// <summary>Origen elegido (Completar) o descripción corta (Edición masiva).</summary>
    public string? Origin { get; init; }
    public int? Level { get; init; }
    public string? Ivs { get; init; }
    public string? Language { get; init; }
    /// <summary>Casilla final («Caja 2, casilla 5») o la de origen.</summary>
    public string? Slot { get; init; }
    public string? Reason { get; init; }
    /// <summary>Resultado por operación (Edición masiva).</summary>
    public IReadOnlyList<OperationOutcome> Operations { get; init; } = [];
    /// <summary>Líneas «antes → después» de los cambios (para «Ver detalle»).</summary>
    public IReadOnlyList<string> Details { get; init; } = [];
}

/// <summary>Informe final de una operación masiva. Nunca lleva bytes de la partida (regla 13) y nunca se envía a ningún sitio (regla 15): solo se copia o se guarda donde el usuario elija.</summary>
public sealed class BulkReport(string title, IReadOnlyList<BulkReportRow> rows, bool editReport, string statWord = "IV")
{
    /// <summary>«DV» en Gen 1-2 (0-15), «IV» en el resto (Hito 5b).</summary>
    public string StatWord { get; } = statWord;
    public string Title { get; } = title;
    public IReadOnlyList<BulkReportRow> Rows { get; } = rows;
    /// <summary>True para la edición masiva (columnas por operación); false para «Completar el juego».</summary>
    public bool IsEdit { get; } = editReport;

    public int Count(BulkStatus s) => Rows.Count(r => r.Status == s);

    public IReadOnlyDictionary<BulkStatus, int> Totals => Rows.GroupBy(r => r.Status).ToDictionary(g => g.Key, g => g.Count());

    public string ToText()
    {
        var sb = new StringBuilder();
        sb.AppendLine(Title);
        foreach (var g in Rows.GroupBy(r => r.Status).OrderBy(g => (int)g.Key))
            sb.AppendLine($"  {BulkStatusText.Label(g.Key)}: {g.Count()}");
        sb.AppendLine();
        foreach (var r in Rows)
        {
            sb.Append($"{r.Number,4}. {r.Pokemon}{(r.Form is { Length: > 0 } f ? $" ({f})" : string.Empty)} · {BulkStatusText.Label(r.Status)}");
            if (r.Slot is not null) sb.Append($" · {r.Slot}");
            if (r.Origin is not null) sb.Append($" · {r.Origin}");
            if (r.Level is { } lv) sb.Append($" · nv. {lv}");
            if (r.Ivs is not null) sb.Append($" · IV {r.Ivs}");
            if (r.Language is not null) sb.Append($" · {r.Language}");
            if (r.Operations.Count > 0) sb.Append(" · " + string.Join(" ", r.Operations.Select(o => $"{BulkStatusText.Name(o.Operation)} {BulkStatusText.Symbol(o.Status)}")));
            if (r.Reason is { Length: > 0 }) sb.Append(" · " + r.Reason);
            sb.AppendLine();
            foreach (var d in r.Details) sb.AppendLine("        " + d);
            foreach (var o in r.Operations.Where(o => o.Status != OperationStatus.Changed && o.Reason is { Length: > 0 }))
                sb.AppendLine($"        {BulkStatusText.Name(o.Operation)}: {TextosMotor.Traducir(o.Message)} {TextosMotor.Traducir(o.Reason)}");
        }
        return sb.ToString();
    }

    public string ToCsv()
    {
        static string Q(string? s) => "\"" + (s ?? string.Empty).Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        var sb = new StringBuilder();
        sb.AppendLine("n;pokemon;forma;estado;casilla;origen;nivel;iv;idioma;operaciones;motivo");
        foreach (var r in Rows)
            sb.AppendLine(string.Join(';', r.Number, Q(r.Pokemon), Q(r.Form), Q(BulkStatusText.Label(r.Status)), Q(r.Slot), Q(r.Origin), r.Level?.ToString() ?? string.Empty, Q(r.Ivs), Q(r.Language),
                Q(string.Join(" ", r.Operations.Select(o => $"{BulkStatusText.Name(o.Operation)}:{o.Status}"))), Q(r.Reason)));
        return sb.ToString();
    }
}
