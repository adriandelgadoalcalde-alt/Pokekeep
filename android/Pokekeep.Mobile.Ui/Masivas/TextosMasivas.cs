// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace Pokekeep.Mobile.Ui.Masivas;

/// <summary>
/// Los nombres de estado del informe masivo (los <c>BulkStatus</c> del núcleo, por NOMBRE: la interfaz no referencia el núcleo) y cómo se pintan. <c>EstadosMasivosTests</c> compara esta lista con el enum real y con
/// las etiquetas de <c>BulkStatusText</c>: si el núcleo añade un estado y esta clase no lo conoce, la prueba falla.
/// </summary>
public static class EstadosMasivos
{
    public const string Generated = "Generated", NeedsHome = "NeedsHome", AlreadyHad = "AlreadyHad", CannotGenerate = "CannotGenerate", OnlyWithHome = "OnlyWithHome", NotStorable = "NotStorable",
        NotInGame = "NotInGame", RequiresExpansion = "RequiresExpansion", TimedOut = "TimedOut", EngineError = "EngineError", NoRoom = "NoRoom", SlotTaken = "SlotTaken", FailedFinalCheck = "FailedFinalCheck",
        Cancelled = "Cancelled", Changed = "Changed", EventVersion = "EventVersion", AlreadyOptimal = "AlreadyOptimal", NotApplicable = "NotApplicable", NotLegalFirst = "NotLegalFirst",
        SlotChanged = "SlotChanged", Skipped = "Skipped";

    /// <summary>Todos, en el orden del enum del núcleo (el del informe).</summary>
    public static readonly IReadOnlyList<string> Todos =
    [
        Generated, NeedsHome, AlreadyHad, CannotGenerate, OnlyWithHome, NotStorable, NotInGame, RequiresExpansion, TimedOut, EngineError, NoRoom, SlotTaken, FailedFinalCheck, Cancelled,
        Changed, EventVersion, AlreadyOptimal, NotApplicable, NotLegalFirst, SlotChanged, Skipped,
    ];

    /// <summary>Estados que piden atención porque algo no se pudo hacer (los mismos que el escritorio pinta en rojo).</summary>
    public static bool EsError(string estado) => estado is CannotGenerate or TimedOut or EngineError or FailedFinalCheck or NoRoom or SlotTaken or SlotChanged;

    /// <summary>Estados que piden cuidado sin ser un fallo (los que el escritorio pinta en ámbar: pasar por HOME).</summary>
    public static bool EsAviso(string estado) => estado is NeedsHome or OnlyWithHome;

    /// <summary>Lo que salió como se pedía.</summary>
    public static bool EsOk(string estado) => estado is Generated or Changed;
}

/// <summary>
/// Los estados de una fila de «Preferir la versión de evento» (los <c>EventSwapStatus</c> del núcleo, por NOMBRE). <c>EstadosEventoTests</c> compara esta lista con el enum real: si el núcleo añade uno, la prueba falla.
/// </summary>
public static class EstadosEvento
{
    public const string Proposed = "Proposed", AlreadyEvent = "AlreadyEvent", NoEvent = "NoEvent", NotLegalFirst = "NotLegalFirst", NotGenerable = "NotGenerable", TimedOut = "TimedOut",
        EngineError = "EngineError", Cancelled = "Cancelled";

    /// <summary>Todos, en el orden del enum del núcleo.</summary>
    public static readonly IReadOnlyList<string> Todos = [Proposed, AlreadyEvent, NoEvent, NotLegalFirst, NotGenerable, TimedOut, EngineError, Cancelled];

    /// <summary>Lo que no se pudo comprobar (tiempo agotado, error del motor): se pinta como error, igual que en el informe masivo.</summary>
    public static bool EsError(string estado) => estado is TimedOut or EngineError;
}

/// <summary>Estados de UNA operación del informe (los <c>OperationStatus</c> del contrato, por nombre).</summary>
public static class EstadosOperacion
{
    public const string Changed = "Changed", AlreadyOptimal = "AlreadyOptimal", NotApplicable = "NotApplicable";
}

public static class TextosMasivas
{
    /// <summary>La duración como la escribe el núcleo (<c>Textos.Duracion</c>): segundos por debajo de 90 y minutos por encima. Una prueba la compara con la fuente.</summary>
    public static string Duracion(TimeSpan t) => t.TotalSeconds < 90 ? $"{Math.Max(1, (int)Math.Round(t.TotalSeconds))} s" : $"{Math.Max(1, (int)Math.Round(t.TotalMinutes))} min";
}
