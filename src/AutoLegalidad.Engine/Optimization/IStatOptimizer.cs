// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Contracts;
using AutoLegalidad.Engine.Changes;
using AutoLegalidad.Engine.Context;
using AutoLegalidad.Engine.Validation;
using PKHeX.Core;

namespace AutoLegalidad.Engine.Optimization;

/// <param name="Candidate">Entidad VALIDA a mejorar (no se muta: se trabaja sobre copias).</param>
/// <param name="Original">Entidad original de la solicitud (para la guarda de origen y la identidad de especie).</param>
/// <param name="PreserveOrigin">Accion B: ningun campo de origen puede cambiar (guarda de seguridad con reflexion).</param>
/// <param name="RequireCatalogGate">Si el candidato salio de una regeneracion, sus variantes tambien deben estar en el catalogo.</param>
public sealed record OptimizeRequest(PKM Candidate, PKM Original, Destination Dest, IOfficialValidator Validator, DateTime DeadlineUtc,
    CancellationToken Cancellation, ChangeLog Log, bool PreserveOrigin, bool RequireCatalogGate, Action<OptimizeProgress>? Progress = null);

/// <summary>Progreso de una busqueda larga de «Optimizar stats» (la interfaz lo muestra y permite cancelar). <c>Fraction</c> es el avance del dominio de semillas [0, 1].</summary>
public sealed record OptimizeProgress(string Strategy, double Fraction, int BestSum, int TargetSum, TimeSpan Elapsed);

public enum OptimizeStatus
{
    /// <summary>Se ejecuto la optimizacion (con o sin mejora).</summary>
    Done,
    /// <summary>La mejor opcion cambiaba un campo de origen: descartada.</summary>
    OriginGuard,
    /// <summary>Este tipo de encuentro aun no se sabe optimizar de forma segura.</summary>
    Unsupported,
    /// <summary>Sin mejora hallada y SIN prueba de que no exista (presupuesto agotado): no se declara «sin margen».</summary>
    NoImprovementFound,
}

public sealed record OptimizeOutcome(PKM Candidate, OptimizationReport Report, bool Improved, OptimizeStatus Status = OptimizeStatus.Done, int Attempts = 0);

public interface IStatOptimizer
{
    OptimizeOutcome Optimize(OptimizeRequest request);
}
