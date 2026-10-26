// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Contracts;

namespace AutoLegalidad.Ui.Operation;

/// <summary>Cómo terminó una operación, desde el punto de vista de lo que se le dice al usuario.</summary>
public enum OutcomeKind
{
    /// <summary>El resultado se aplicó (y se releyó y revalidó).</summary>
    Applied,
    /// <summary>R-009: aplicado, pero SIN matrícula de HOME (<c>SuccessNeedsHome</c>): PKHeX lo da por inválido hasta que pase por HOME. Nunca se presenta como «Legalizado».</summary>
    AppliedNeedsHome,
    /// <summary>0.6.6 (M4): el Pokémon ya estaba como debía salvo la matrícula de HOME (el motor devolvió los mismos bytes): no se ha escrito nada y no hay nada que deshacer. Sigue necesitando pasar por HOME.</summary>
    UnchangedNeedsHome,
    AlreadyLegal,
    AlreadyOptimal,
    /// <summary>El motor no produjo candidato (sin solución, tiempo agotado, no soportado...): el original queda intacto.</summary>
    NotApplied,
    Cancelled,
    /// <summary>Cambió la casilla, el editor o la partida durante la operación: el resultado se descartó.</summary>
    TargetChanged,
    /// <summary>El resultado se aplicó pero la relectura lo dio por inválido: se deshizo (o ni siquiera se aplicó).</summary>
    ValidationFailed,
    /// <summary>Fallo del motor o de su comunicación (proceso caído, respuesta incoherente...).</summary>
    EngineFailure,
    /// <summary>Ya hay otra operación en curso: esta no se inició.</summary>
    Busy,
}

/// <summary>Resultado de una operación para la interfaz: texto específico, resumen de cambios y, si procede, cómo deshacerlo.</summary>
public sealed record OperationResult
{
    public required PokekeepAction Action { get; init; }
    public required OperationOrigin Origin { get; init; }
    public required OutcomeKind Kind { get; init; }
    /// <summary>Estado del contrato, o el que decide la interfaz (<see cref="ResponseStatus.TargetChanged"/>, <see cref="ResponseStatus.ValidationFailed"/>, <see cref="ResponseStatus.Cancelled"/>).</summary>
    public required ResponseStatus Status { get; init; }
    /// <summary>Titular corto.</summary>
    public required string Headline { get; init; }
    /// <summary>Explicación específica (la razón honesta). Nunca una lista de alternativas.</summary>
    public string? Detail { get; init; }
    /// <summary>Líneas de resumen (incluye los rasgos relajados y «IV optimizados: ...»).</summary>
    public IReadOnlyList<string> Summary { get; init; } = [];
    public IReadOnlyList<ChangeEntry> Changes { get; init; } = [];
    public string? TargetDescription { get; init; }
    public TimeSpan Elapsed { get; init; }
    public LegalizeResponse? Response { get; init; }
    /// <summary>Cómo deshacer lo aplicado (null si no se aplicó nada).</summary>
    public UndoToken? Undo { get; init; }
    /// <summary>Hito 4b: si es el resultado de una operación masiva, su informe por Pokémon (null en las demás).</summary>
    public Bulk.BulkOutcome? Bulk { get; init; }

    public bool Applied => Kind is OutcomeKind.Applied or OutcomeKind.AppliedNeedsHome;
    /// <summary>Aplicado sin la matrícula de HOME (ajuste R-009): hay que avisar de que necesita pasar por HOME.</summary>
    public bool NeedsHome => Kind is OutcomeKind.AppliedNeedsHome or OutcomeKind.UnchangedNeedsHome;

    /// <summary>Un fallo se muestra siempre; un éxito solo si el usuario no apagó el resumen.</summary>
    public bool IsFailure => Kind is OutcomeKind.NotApplied or OutcomeKind.TargetChanged or OutcomeKind.ValidationFailed or OutcomeKind.EngineFailure;
}

/// <summary>Cómo deshacer una operación aplicada. Solo vale mientras el destino siga exactamente como lo dejó Pokekeep.</summary>
public sealed class UndoToken(Func<bool> canUndo, Func<bool> undo)
{
    public bool CanUndo() => canUndo();

    /// <summary>Deshace. Devuelve false (sin tocar nada) si el destino ya no está como lo dejó Pokekeep.</summary>
    public bool Undo() => undo();
}
