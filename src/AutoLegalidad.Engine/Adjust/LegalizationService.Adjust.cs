// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Contracts;
using AutoLegalidad.Engine.Adjust;
using AutoLegalidad.Engine.Changes;
using AutoLegalidad.Engine.Context;
using AutoLegalidad.Engine.Optimization;
using AutoLegalidad.Engine.Validation;
using PKHeX.Core;

namespace AutoLegalidad.Engine;

public sealed partial class LegalizationService
{
    // ============================================================ Accion C: Adjust (R-007, edicion masiva de UN Pokemon)
    /// <summary>
    /// Hito 4b (R-007): UNA solicitud = UN Pokemon con todas sus operaciones, en el orden fijo de <see cref="AdjustOperationNames.Order"/> (Legalizar -> Nivel -> Optimizar -> Amistad -> Idioma -> PP -> PS/estado -> Pokerus).
    /// Cada operacion se aplica sobre una copia y la valida el validador del motor; si la deja invalida solo se deshace ESA operacion (NotApplicable con el primer motivo del Core) y las demas siguen.
    /// El resultado lleva siempre <see cref="LegalizeResponse.Operations"/>, tambien sin candidato. Los huevos no se editan.
    /// </summary>
    private LegalizeResponse RunAdjust(Operation op, PKM original, PKM working, Destination dest, ValidationOutcome baseline, bool partySize)
    {
        var a = op.Request.Options!.Adjust!; // RequestValidator garantiza que existe y pide algo
        var requested = AdjustOperationNames.Requested(a).ToHashSet(StringComparer.Ordinal);
        var results = new Dictionary<string, OperationOutcome>(StringComparer.Ordinal);
        void Set(string name, OperationStatus status, string message, string? reason = null)
            => results[name] = new OperationOutcome { Operation = name, Status = status, Message = message, Reason = reason };
        LegalizeResponse Ops(LegalizeResponse r) => r with
        {
            Operations = [.. AdjustOperationNames.Order.Where(results.ContainsKey).Select(n => results[n])],
        };
        void AllNotApplicable(string message, string? reason = null)
        {
            foreach (var n in requested) Set(n, OperationStatus.NotApplicable, message, reason);
        }

        op.Phase = Phase.A_CapturaYAnalisis;
        // Los huevos no se editan: su legalidad depende de que sigan siendo huevos y casi todos los campos son contadores de eclosion.
        if (original.IsEgg)
        {
            AllNotApplicable("Los huevos no se editan.");
            return Ops(op.Fail(ResponseStatus.AlreadyOptimal, "No hay nada que cambiar: los huevos no se editan.", null, complete: true));
        }

        var cur = original;
        var curOutcome = baseline;
        var regenerated = false;
        var log = new ChangeLog();
        var legalized = false;

        // ---------- 1) Legalizar (solo si se pidio y es ilegal) ----------
        if (!baseline.Valid || !ReferenceEquals(working, original))
        {
            if (!a.LegalizeIfIllegal)
            {
                AllNotApplicable("Primero hay que legalizarlo.");
                if (!ReferenceEquals(working, original))
                    return Ops(op.Fail(ResponseStatus.UnsupportedData, "La entidad no es del formato del destino: conviértela primero.",
                        $"{original.GetType().Name} no es {dest.EntityType.Name}; la edición masiva no transfiere entre formatos.", complete: true));
                return Ops(op.Fail(ResponseStatus.NotLegalFirst, "Primero hay que legalizarlo: usa «Legalizar automáticamente» o marca «Legalizar los ilegales primero».",
                    $"El Pokémon no es legal tal cual está ({string.Join(", ", baseline.Failures)}); las operaciones de edición no legalizan.", complete: true));
            }
            op.CaptureOnly = true;
            LegalizeResponse? lr = null;
            var timedOut = false;
            try { lr = RunLegalize(op, original, working, dest, baseline, partySize); }
            catch (TimeoutExpiredException) { timedOut = true; }
            finally { op.CaptureOnly = false; }
            if (op.Captured is not { } cap)
            {
                const string skip = "No se ha podido legalizar primero.";
                var failure = timedOut
                    ? op.Fail(ResponseStatus.Timeout, "Se agotó el tiempo antes de terminar la búsqueda.", "El tiempo límite se alcanzó durante la búsqueda; esto NO prueba que no exista solución.", complete: false)
                    : lr!;
                AllNotApplicable(skip);
                Set(AdjustOperationNames.Legalize, OperationStatus.Failed, failure.Message, failure.Reason);
                return Ops(failure);
            }
            cur = cap.Candidate;
            curOutcome = cap.Outcome;
            regenerated = cap.Regenerated;
            legalized = true;
            log.MergeFrom(op.Log);
            log.MergeFrom(cap.Log);
            log.DefaultCode = cap.Log.DefaultCode;
            log.DefaultText = cap.Log.DefaultText;
            Set(AdjustOperationNames.Legalize, OperationStatus.Changed, regenerated ? "Pokémon legalizado: se ha reconstruido un origen válido." : "Pokémon legalizado con una reparación mínima.");
        }
        else if (a.LegalizeIfIllegal)
            Set(AdjustOperationNames.Legalize, OperationStatus.AlreadyOptimal, "Ya era legal: no ha hecho falta legalizarlo.");

        // ---------- 2) Resto de operaciones, sobre copias, en el orden fijo ----------
        var ops = new AdjustOperations(_validator, original, dest, requireCatalogGate: regenerated, partySize);
        OptimizationReport? optimization = null;
        int? finalLevel = null;
        var timeExhausted = false;
        foreach (var name in AdjustOperationNames.Order)
        {
            if (name == AdjustOperationNames.Legalize || !requested.Contains(name)) continue;
            if (timeExhausted)
            {
                Set(name, OperationStatus.Failed, "Se agotó el tiempo antes de llegar a esta operación.", "El tiempo límite se alcanzó; esto no prueba que la operación sea imposible.");
                continue;
            }
            StepResult step;
            try
            {
                op.Tick();
                step = name switch
                {
                    AdjustOperationNames.MaxLevel => StepMaxLevel(op, cur, original, dest, curOutcome, regenerated, log, ref finalLevel),
                    AdjustOperationNames.Optimize => StepOptimize(op, cur, original, dest, legalized, regenerated, log, ref optimization),
                    AdjustOperationNames.MaxFriendship => ops.MaxFriendship(cur, log),
                    AdjustOperationNames.SpanishLanguage => ops.SpanishLanguage(cur, log),
                    AdjustOperationNames.MaxPP => ops.MaxPP(cur, log),
                    AdjustOperationNames.HealParty => ops.HealParty(cur, log),
                    AdjustOperationNames.CurePokerus => ops.CurePokerus(cur, log),
                    _ => new StepResult(OperationStatus.NotApplicable, "Operación desconocida."),
                };
            }
            catch (TimeoutExpiredException)
            {
                timeExhausted = true;
                Set(name, OperationStatus.Failed, "Se agotó el tiempo antes de terminar esta operación.", "El tiempo límite se alcanzó; esto no prueba que la operación sea imposible.");
                continue;
            }
            if (step.Status == OperationStatus.Changed && step.Candidate is not null && step.Outcome is not null)
            {
                cur = step.Candidate;
                curOutcome = step.Outcome;
            }
            Set(name, step.Status, step.Message, step.Reason);
        }

        // ---------- 3) Estado global ----------
        op.Phase = Phase.E_ValidacionIndependiente;
        var changed = results.Values.Any(o => o.Status == OperationStatus.Changed);
        if (!changed)
        {
            if (timeExhausted)
                return Ops(op.Fail(ResponseStatus.Timeout, "Se agotó el tiempo antes de terminar las operaciones.", "El tiempo límite se alcanzó; esto NO prueba que no se pueda aplicar ninguna.", complete: false));
            var optimal = op.Fail(ResponseStatus.AlreadyOptimal, "No había nada que cambiar con las operaciones elegidas.", null, complete: true);
            // 0.6.6 (M4): bajo el ajuste de HOME, que no haya nada que cambiar no quita el aviso de que sigue sin matricula.
            if (curOutcome.TrackerMissing && dest.TrackerSeverity == Severity.Fishy) optimal = optimal with { Summary = [NeedsHomeWarning] };
            return Ops(optimal);
        }
        if (!curOutcome.Valid) // no deberia ocurrir: cada paso acepto un resultado valido; ultima barrera
            return Ops(op.Fail(ResponseStatus.ValidationFailed, "El resultado no superó la revalidación independiente: se descarta.", string.Join(", ", curOutcome.Failures)));

        var done = results.Values.Count(o => o.Status == OperationStatus.Changed);
        var message = done == 1 ? "Se ha aplicado 1 operación." : $"Se han aplicado {done} operaciones.";
        return Ops(Success(op, ResponseStatus.Success, original, cur, dest, curOutcome, partySize, message, optimization, log, finalLevel: finalLevel));
    }

    private StepResult StepMaxLevel(Operation op, PKM cur, PKM original, Destination dest, ValidationOutcome curOutcome, bool regenerated, ChangeLog log, ref int? finalLevel)
    {
        var r = MaxLevelStep.Apply(cur, original, dest, _validator, curOutcome, requireCatalogGate: regenerated, op.Deadline, op.Ct);
        if (r.Status != MaxLevelStatus.NotApplicable) finalLevel = r.FinalLevel;
        switch (r.Status)
        {
            case MaxLevelStatus.Changed:
                log.Reason("Level", ReasonCodes.LevelMaxed, r.Message);
                return new StepResult(OperationStatus.Changed, r.Message, r.Limit, r.Candidate, r.Outcome);
            case MaxLevelStatus.AlreadyMax:
                return new StepResult(OperationStatus.AlreadyOptimal, r.Message, r.Limit);
            default:
                return new StepResult(OperationStatus.NotApplicable, r.Message);
        }
    }

    /// <summary>
    /// Optimizar stats dentro de Adjust. Con un Pokemon que NO se ha legalizado en esta solicitud es la accion B (R-002): conserva todo el origen (guarda de origen). Si se acaba de legalizar usa los mismos parametros que
    /// <see cref="FinishLegalize"/>. En ambos casos con el presupuesto propio de la optimizacion de «Legalizar» (<see cref="ServiceSettings.LegalizeOptimizeBudget"/>), para que no se coma el plazo de la solicitud.
    /// </summary>
    private StepResult StepOptimize(Operation op, PKM cur, PKM original, Destination dest, bool legalized, bool regenerated, ChangeLog log, ref OptimizationReport? optimization)
    {
        var deadline = Min(op.Deadline, DateTime.UtcNow + OptimizeBudget(op.Request));
        var result = _optimizer.Optimize(new OptimizeRequest(cur, original, dest, _validator, deadline, op.Ct, log, PreserveOrigin: !legalized, RequireCatalogGate: regenerated, Progress: _settings.Progress));
        var report = result.Report;
        optimization = report;
        op.Attempts += result.Attempts;
        switch (result.Status)
        {
            case OptimizeStatus.OriginGuard:
                return new StepResult(OperationStatus.NotApplicable, "La única mejora posible exigía cambiar el origen del Pokémon: no se ha aplicado.", report.LimitReason);
            case OptimizeStatus.Unsupported:
                return new StepResult(OperationStatus.NotApplicable, "Este tipo de encuentro aún no se puede optimizar.", report.LimitReason);
            case OptimizeStatus.NoImprovementFound:
                return new StepResult(OperationStatus.Failed, "No se ha encontrado una mejora de stats en el tiempo disponible (la búsqueda no es exhaustiva).", report.LimitReason);
        }
        if (!result.Improved)
            return new StepResult(OperationStatus.AlreadyOptimal, "Sin margen de mejora: los stats de este Pokémon ya son los mejores posibles para su encuentro.", report.LimitReason);

        // Revalidacion INDEPENDIENTE del candidato optimizado (misma comprobacion que Optimizar y que Legalizar).
        var reval = _validator.Validate(result.Candidate, original, dest, requireCatalogGate: regenerated);
        if (!reval.Valid)
            return new StepResult(OperationStatus.NotApplicable, "La optimización dejaba el Pokémon inválido: se ha descartado.", MaxLevelStep.FirstFailure(reval));
        return new StepResult(OperationStatus.Changed, $"{ChangeBuilder.StatWord(result.Candidate)} optimizados: {string.Join('/', report.IvsBefore)} -> {string.Join('/', report.IvsAfter)}.", report.LimitReason, result.Candidate, reval);
    }
}
