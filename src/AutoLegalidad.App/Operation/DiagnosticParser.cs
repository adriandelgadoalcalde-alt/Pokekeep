// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Text.Json;
using AutoLegalidad.Contracts;

namespace AutoLegalidad.Ui.Operation;

/// <summary>Progreso que el motor cuenta por stderr (ADR 0011/0016): <c>POKEKEEP-FASE &lt;Phase&gt;</c> y <c>POKEKEEP-PROGRESO {json}</c>.</summary>
public abstract record EngineSignal
{
    public sealed record PhaseChanged(Phase Phase) : EngineSignal;
    public sealed record OptimizeProgress(string Strategy, double Fraction, int BestSum, int TargetSum, double Seconds) : EngineSignal;
}

public static class DiagnosticParser
{
    public const string PhasePrefix = "POKEKEEP-FASE ";
    public const string ProgressPrefix = "POKEKEEP-PROGRESO ";

    /// <summary>Interpreta una línea de stderr. Devuelve null si no es una señal de progreso (cualquier otra línea es solo diagnóstico).</summary>
    public static EngineSignal? Parse(string? line)
    {
        if (string.IsNullOrEmpty(line))
            return null;
        if (line.StartsWith(PhasePrefix, StringComparison.Ordinal))
            return Enum.TryParse<Phase>(line[PhasePrefix.Length..].Trim(), out var phase) ? new EngineSignal.PhaseChanged(phase) : null;
        if (line.StartsWith(ProgressPrefix, StringComparison.Ordinal))
        {
            try
            {
                using var doc = JsonDocument.Parse(line[ProgressPrefix.Length..]);
                var root = doc.RootElement;
                return new EngineSignal.OptimizeProgress(
                    root.TryGetProperty("Strategy", out var s) ? s.GetString() ?? string.Empty : string.Empty,
                    root.TryGetProperty("Fraction", out var f) && f.TryGetDouble(out var fv) ? fv : 0,
                    root.TryGetProperty("BestSum", out var b) && b.TryGetInt32(out var bv) ? bv : 0,
                    root.TryGetProperty("TargetSum", out var t) && t.TryGetInt32(out var tv) ? tv : 0,
                    root.TryGetProperty("segundos", out var sec) && sec.TryGetDouble(out var sv) ? sv : 0);
            }
            catch (JsonException)
            {
                return null;
            }
        }
        return null;
    }

    /// <summary>Texto de la barra para cada fase del motor.</summary>
    public static string PhaseText(Phase phase) => phase switch
    {
        Phase.A_CapturaYAnalisis => Textos.Analizando,
        Phase.B_ReparacionMinima or Phase.C_Regeneracion or Phase.D_RelajacionAutomatica => Textos.BuscandoOrigen,
        Phase.E_ValidacionIndependiente => Textos.Comprobando,
        _ => Textos.Analizando,
    };
}
