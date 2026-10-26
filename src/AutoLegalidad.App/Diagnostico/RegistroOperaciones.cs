// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Registro;
using AutoLegalidad.Ui.Bulk;
using AutoLegalidad.Ui.Operation;

namespace AutoLegalidad.Ui.Diagnostico;

/// <summary>Textos del registro de fallos (castellano de España). Fichero propio para no pisar <c>Textos.cs</c>.</summary>
public static class TextosRegistro
{
    public static string AbrirCarpeta => AppIdioma.T("Abrir carpeta de registros", "Open logs folder");
    public static string ErrorGuardado => AppIdioma.T("El error queda guardado en la carpeta de registros de Pokekeep (botón «Abrir carpeta de registros»).", "The error is saved in Pokekeep’s logs folder (“Open logs folder” button).");
    public static string SinCarpeta => AppIdioma.T("Pokekeep no ha podido crear ninguna carpeta de registros: ni junto al programa ni en %LOCALAPPDATA%\\Pokekeep\\registros. Los fallos no se están guardando.", "Pokekeep couldn’t create any logs folder: neither next to the program nor in %LOCALAPPDATA%\\Pokekeep\\registros. Failures are not being saved.");
}

/// <summary>
/// Registro de las operaciones de Pokekeep que acaban mal (ADR 0040): Legalizar, Optimizar, Librería, Completar el juego y Edición masiva. Anota operación, origen, resultado, especie y casilla
/// (<c>TargetDescription</c>); NUNCA el Pokémon entero ni datos del entrenador. Un éxito, una cancelación del usuario y un «ya legal» no son fallos y no se anotan.
/// </summary>
public static class RegistroOperaciones
{
    private const int MaxFilasPorOperacion = 40;

    public static void Registrar(OperationResult r)
    {
        try
        {
            if (r.Bulk is { } bulk) { RegistrarMasiva(r, bulk); return; }
            var nivel = r.Kind switch
            {
                OutcomeKind.EngineFailure or OutcomeKind.ValidationFailed => Nivel.Error,
                OutcomeKind.TargetChanged => Nivel.Aviso,
                // «Sin solución» y compañía son un veredicto honesto del motor, pero para revisar después interesa saber cuándo no se pudo y por qué.
                OutcomeKind.NotApplied => Nivel.Aviso,
                _ => (Nivel?)null,
            };
            if (nivel is not { } n) return;
            RegistroFallos.Escribir(n, AppIdioma.F("Operación {0} ({1}) terminó con {2}/{3}: {4}. {5}", "Operation {0} ({1}) ended with {2}/{3}: {4}. {5}", r.Action, r.Origin, r.Kind, r.Status, r.Headline, r.Detail).Trim(),
                contexto: AppIdioma.F("destino: {0} · duración {1:0.0} s", "target: {0} · duration {1:0.0} s", r.TargetDescription ?? "n/d", r.Elapsed.TotalSeconds) + (r.Response is { } resp ? $" · solicitud {resp.RequestId}" : ""));
        }
        catch (Exception) { /* el registro nunca debe romper la interfaz */ }
    }

    private static void RegistrarMasiva(OperationResult r, BulkOutcome b)
    {
        var malas = b.Report.Rows.Where(x => x.Status is BulkStatus.EngineError or BulkStatus.FailedFinalCheck or BulkStatus.TimedOut or BulkStatus.CannotGenerate).ToList();
        if (malas.Count == 0 && b.Result is not (OutcomeKind.EngineFailure or OutcomeKind.ValidationFailed or OutcomeKind.TargetChanged)) return;
        var nivel = b.Result is OutcomeKind.EngineFailure || malas.Any(x => x.Status is BulkStatus.EngineError or BulkStatus.FailedFinalCheck) ? Nivel.Error : Nivel.Aviso;
        var filas = string.Join("\n", malas.Take(MaxFilasPorOperacion).Select(x => $"{x.Number}. {x.Pokemon}{(x.Form is { Length: > 0 } f ? $" ({f})" : "")} · {x.Slot} · {BulkStatusText.Label(x.Status)}" + (x.Reason is { Length: > 0 } rs ? " · " + rs : "")));
        if (malas.Count > MaxFilasPorOperacion) filas += $"\n(y {malas.Count - MaxFilasPorOperacion} más)";
        RegistroFallos.Escribir(nivel, AppIdioma.F("Operación {0} terminó con {1}: {2}. {3}", "Operation {0} ended with {1}: {2}. {3}", r.Action, b.Result, b.Headline, b.Detail).Trim(),
            contexto: AppIdioma.F("{0} Pokémon con fallo · escritos {1} · duración {2:0.0} s", "{0} Pokémon failed · written {1} · duration {2:0.0} s", malas.Count, b.Written, b.Elapsed.TotalSeconds) + (filas.Length > 0 ? "\n" + filas : ""));
    }

    /// <summary>Un error inesperado de la propia interfaz de Pokekeep durante una operación (con la excepción completa).</summary>
    public static void Excepcion(string operacion, Exception ex) => RegistroFallos.Error(AppIdioma.F("Error inesperado en la interfaz de Pokekeep durante {0}.", "Unexpected error in the Pokekeep interface during {0}.", operacion), ex, "RegistroOperaciones.Excepcion");
}
