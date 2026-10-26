// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Contracts;

namespace AutoLegalidad.Ui.Operation;

/// <summary>
/// Las notas de «Ver cambios» (resumen, detalle, optimización, validación independiente, origen, versión del núcleo y tiempo del motor), líneas sueltas. Antes vivía en la ventana de WinForms
/// (<c>ChangesForm.BuildNotes</c>); se movió aquí para que la pantalla de Android muestre EXACTAMENTE las mismas líneas (Fase 6, ADR 0060).
/// </summary>
public static class ChangeNotes
{
    public static IReadOnlyList<string> Lines(OperationResult r)
    {
        var lines = new List<string>();
        lines.AddRange(r.Summary);
        if (r.Detail is { Length: > 0 } d) lines.Add(d);
        var resp = r.Response;
        if (resp is not null)
        {
            if (resp.Optimization is { } o)
                lines.Add(AppIdioma.F("Optimización ({0}): suma de IV {1} → {2}", "Optimization ({0}): IV total {1} → {2}", o.Strategy, o.SumBefore, o.SumAfter) + (o.Exhaustive ? AppIdioma.T(" (búsqueda exhaustiva)", " (exhaustive search)") : string.Empty) + (o.LimitReason is { Length: > 0 } l ? $". {TextosMotor.Traducir(l)}" : "."));
            if (resp.Proof is { } p)
            {
                lines.Add(string.Empty);
                lines.Add(AppIdioma.F("Validación independiente: oficial={0}, destino={1}, ida y vuelta={2}, especie={3}.", "Independent validation: official={0}, target={1}, round trip={2}, species={3}.", Yes(p.OfficialValid), Yes(p.DestinationCompatible), Yes(p.RoundTripValid), Yes(p.SpeciesIdentity)));
                if (p.EncounterKey is { Length: > 0 } k) lines.Add(AppIdioma.F("Origen: {0}", "Origin: {0}", k));
                lines.Add(AppIdioma.F("Núcleo {0} ({1}) · perfil de reglas {2}", "Core {0} ({1}) · rules profile {2}", p.ValidatorVersion, p.ValidatorCommit[..Math.Min(12, p.ValidatorCommit.Length)], p.RuleProfileHash[..Math.Min(12, p.RuleProfileHash.Length)]));
            }
            if (resp.ElapsedMilliseconds > 0)
                lines.Add(AppIdioma.F("Tiempo del motor: {0} ms · intentos: {1}", "Engine time: {0} ms · attempts: {1}", resp.ElapsedMilliseconds, resp.Attempts) + (resp.Status != ResponseStatus.Success ? AppIdioma.F(" · estado: {0}", " · status: {0}", resp.Status) : string.Empty));
        }
        return lines;
    }

    public static string Text(OperationResult r) => string.Join(Environment.NewLine, Lines(r));

    private static string Yes(bool b) => b ? AppIdioma.T("sí", "yes") : AppIdioma.T("no", "no");
}
