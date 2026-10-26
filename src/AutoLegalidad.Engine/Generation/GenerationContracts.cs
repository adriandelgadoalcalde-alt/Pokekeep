// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Engine.Context;
using PKHeX.Core;

namespace AutoLegalidad.Engine.Generation;

/// <summary>Que se pide a una estrategia en un escalon de relajacion.</summary>
public sealed class GenerationContext
{
    /// <summary>Copia de la entidad ORIGINAL (las estrategias no la mutan; si necesitan mutar, clonan).</summary>
    public required PKM Original { get; init; }
    public required Destination Dest { get; init; }
    public required Traits Traits { get; init; }
    public required RelaxationStep Step { get; init; }
    /// <summary>Encuentro que el analisis oficial asocio a la entidad original (para priorizarlo: «buscar primero el encuentro original verificable»).</summary>
    public IEncounterable? OriginalEncounter { get; init; }
    /// <summary>Limite duro de esta llamada (el motor ya acota por escalon).</summary>
    public required DateTime DeadlineUtc { get; init; }
    public required CancellationToken Cancellation { get; init; }
    /// <summary>Intentos maximos por escalon (cota de busqueda).</summary>
    public int MaxAttempts { get; init; } = 3;
    /// <summary>La estrategia anota aqui lo que hizo (para el informe tecnico).</summary>
    public StrategyReport Report { get; } = new();

    public bool Expired => DateTime.UtcNow >= DeadlineUtc || Cancellation.IsCancellationRequested;
}

public sealed class StrategyReport
{
    /// <summary>La estrategia agoto un dominio FINITO y bien definido (no un muestreo aleatorio).</summary>
    public bool DomainExhausted { get; set; }
    /// <summary>La estrategia se detuvo por tiempo (no prueba imposibilidad).</summary>
    public bool TimedOut { get; set; }
    public int Produced { get; set; }
    public List<string> Notes { get; } = [];
}

/// <summary>
/// Una forma de producir CANDIDATOS. Nunca decide el exito: el motor valida cada candidato con el validador oficial independiente
/// (nunca basta el bool de ALM ni la ultima comprobacion del generador).
/// </summary>
public interface IGenerationStrategy
{
    string Name { get; }

    /// <summary>Produce candidatos (copias nuevas) de forma perezosa; el motor se detiene en cuanto uno valida con su puntuacion maxima.</summary>
    IEnumerable<PKM> Generate(GenerationContext context);
}
