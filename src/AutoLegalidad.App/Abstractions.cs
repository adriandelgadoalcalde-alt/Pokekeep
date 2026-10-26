// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Contracts;
using PKHeX.Core;

namespace AutoLegalidad.Ui;

/// <summary>Las dos acciones del producto (R-002), en la capa de interfaz.</summary>
public enum PokekeepAction
{
    /// <summary>«Legalizar automáticamente».</summary>
    Legalize = 0,

    /// <summary>«Optimizar stats» (conserva todo el origen).</summary>
    Optimize = 1,

    /// <summary>Hito 4b, R-006: «Completar el juego» (living dex legal).</summary>
    CompleteGame = 2,

    /// <summary>Hito 4b, R-007: «Edición masiva».</summary>
    BulkEdit = 3,
}

public static class PokekeepActionExtensions
{
    public static LegalizeAction ToContract(this PokekeepAction a) => a switch
    {
        PokekeepAction.Optimize => LegalizeAction.Optimize,
        PokekeepAction.BulkEdit => LegalizeAction.Adjust,
        _ => LegalizeAction.Legalize,
    };

    public static string Title(this PokekeepAction a) => a switch
    {
        PokekeepAction.Optimize => Textos.OptimizarStats,
        PokekeepAction.CompleteGame => Textos.CompletarElJuego,
        PokekeepAction.BulkEdit => Textos.EdicionMasiva,
        _ => Textos.LegalizarAutomaticamente,
    };
}

/// <summary>
/// Historial de casillas del editor (envoltorio de <c>SlotEditor&lt;T&gt;</c> del Core sin su parámetro de tipo gráfico): escribir en una casilla,
/// una entrada de historial por operación, lotes con UN solo Deshacer y Deshacer exacto. Fase 1 de Android (ADR 0054).
/// </summary>
public interface ISlotEditor
{
    /// <summary>Escribe <paramref name="pk"/> en la casilla (una entrada de historial) y avisa a quien muestre la casilla.</summary>
    SlotTouchResult Set(ISlotInfo slot, PKM pk);

    /// <summary>Ejecuta <paramref name="action"/> sobre varias casillas como UNA operación deshacible; si lanza, todo vuelve a como estaba.</summary>
    bool Batch(IEnumerable<ISlotInfo> slots, Action<IReadOnlyList<ISlotInfo>> action);

    /// <summary>Deshace la última entrada del historial.</summary>
    void Undo();
}

/// <summary>
/// Entorno de edición ACTUAL, sin tipos de WinForms: el guardado abierto, el historial de casillas y el editor del Pokémon. Cambia al abrir otro guardado:
/// el coordinador compara la REFERENCIA (por eso un anfitrión debe devolver siempre el mismo objeto mientras el guardado sea el mismo).
/// </summary>
public interface IEditEnvironment
{
    SaveFile SAV { get; }
    ISlotEditor Slots { get; }

    /// <summary>Editor del Pokémon abierto (en escritorio, el de PKHeX; en móvil, el borrador de la ficha).</summary>
    IPKMView Editor { get; }
}

/// <summary>Mensajes y confirmaciones al usuario, sin ventanas concretas (escritorio: <c>MessageBox</c>; móvil: diálogos de la interfaz).</summary>
public interface IDialogs
{
    void Info(string title, string message);
    void Warning(string title, string message);
    bool Confirm(string title, string message);
}

/// <summary>Sprites como PNG por clave de recurso de PKHeX.Drawing.PokeSprite (<c>b_25</c>, <c>b_25s</c>, <c>_ball4</c>...). Escritorio: el recurso incrustado; Android: el atlas.</summary>
public interface ISpriteSource
{
    /// <summary>PNG del sprite, o <c>null</c> si la clave no existe (el llamante muestra su silueta y registra un AVISO).</summary>
    byte[]? TryGetPng(string key);
}

/// <summary>
/// Lo ÚNICO que el coordinador necesita del programa anfitrión (puntos explícitos de integración, sin reflexión sobre controles privados y sin tipos de WinForms).
/// En escritorio lo implementa el adaptador de <c>AutoLegalidad.Ui</c> sobre <c>SAVEditor</c> y <c>Main</c>; en Android, el modelo de las cajas.
/// </summary>
public interface IPokekeepHost
{
    /// <summary>Entorno de edición ACTUAL (guardado + editor + historial de casillas). Cambia al abrir otro guardado: el coordinador compara la referencia.</summary>
    IEditEnvironment Env { get; }

    /// <summary>Caja activa del guardado (para «Añadir a la caja» y los mensajes).</summary>
    int CurrentBox { get; }

    /// <summary>
    /// El editor está completo salvo, como mucho, los EV (suma mayor de 510) o los AV de Let's Go: «Legalizar automáticamente» corrige esos valores, así que no deben impedir la acción. Sin sonido ni diálogo.
    /// </summary>
    bool EditorIsCompleteIgnoringStats { get; }

    /// <summary>Quita el resaltado verde de «casilla modificada» que PKHeX deja en las cajas (al deshacer no debe quedar una casilla verde que parece recién cambiada).</summary>
    void ClearSlotHighlights();

    /// <summary>Refresca los elementos Deshacer/Rehacer del menú tras escribir en una casilla.</summary>
    void NotifyUndoRedoChanged();
}

/// <summary>Acceso al motor (Worker): manifiesto de versiones, una operación por solicitud y las líneas de diagnóstico (progreso) que emite por stderr.</summary>
public interface IEngineGateway
{
    Task<WorkerManifest?> GetManifestAsync(CancellationToken cancellation);
    Task<LegalizeResponse> LegalizeAsync(LegalizeRequest request, CancellationToken cancellation);

    /// <summary>Línea de stderr del motor (<c>POKEKEEP-FASE ...</c> / <c>POKEKEEP-PROGRESO ...</c>). Puede llegar desde cualquier hilo.</summary>
    event Action<string>? DiagnosticLine;

    /// <summary>Texto de por qué el motor no está disponible (null si lo está).</summary>
    string? UnavailableReason { get; }
}
