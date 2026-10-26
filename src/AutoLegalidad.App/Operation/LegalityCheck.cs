// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Runtime.ExceptionServices;
using PKHeX.Core;

namespace AutoLegalidad.Ui.Operation;

/// <summary>
/// Análisis oficial de legalidad en el proceso de la interfaz (segunda opinión sobre lo que devuelve el motor y comprobación de lo ya escrito en el guardado).
/// 0.6.6 (ADR 0053, A3 y decisión 2): la segunda opinión usa SIEMPRE la severidad POR DEFECTO del Core, sin importar los ajustes de legalidad que el usuario tenga en PKHeX, y
/// ya no pone nunca «Fishy» en el estado estático del Core: la falta de la matrícula de HOME se tolera mirando el resultado del análisis ESTRICTO (<see cref="OnlyTrackerMissing"/>),
/// igual que hace el motor (<c>OfficialValidator</c>). Como <c>ParseSettings</c> es estático y lo usa también el hilo de la interfaz de PKHeX (iconos de legalidad, editor), cualquier
/// análisis que lo cambie (aquí se sustituye por los ajustes por defecto mientras dura el análisis) se ejecuta en ESE hilo cuando hay uno enlazado
/// (<see cref="BindUiThread"/>) y, en cualquier caso, bajo un cerrojo propio: nunca dos análisis a la vez ni uno concurrente con el repintado de PKHeX.
/// </summary>
public static class LegalityCheck
{
    private static readonly object Gate = new();
    private static SynchronizationContext? _ui;
    private static int _uiThreadId;

    /// <summary>
    /// Enlaza el hilo de la interfaz de PKHeX: desde ahora los análisis pedidos desde otros hilos (el <c>Task.Run</c> de las operaciones masivas) se ejecutan en él, uno a uno, de modo que
    /// el estado estático del Core no se toca nunca a la vez que PKHeX lo lee. Se llama desde ese hilo (<see cref="PokekeepApp"/>).
    /// </summary>
    public static void BindUiThread(SynchronizationContext? context, int uiThreadId)
    {
        lock (Gate)
        {
            _ui = context;
            _uiThreadId = uiThreadId;
        }
    }

    /// <summary>Ejecuta <paramref name="work"/> con los ajustes de legalidad POR DEFECTO del Core y los restaura siempre; en el hilo de la interfaz si hay uno enlazado.</summary>
    private static T WithDefaultSettings<T>(Func<T> work)
    {
        SynchronizationContext? ui;
        int uiThread;
        lock (Gate) { ui = _ui; uiThread = _uiThreadId; }
        if (ui is null || Environment.CurrentManagedThreadId == uiThread)
            return RunUnderGate(work);
        T result = default!;
        ExceptionDispatchInfo? error = null;
        ui.Send(_ =>
        {
            try { result = RunUnderGate(work); }
            catch (Exception ex) { error = ExceptionDispatchInfo.Capture(ex); }
        }, null);
        error?.Throw();
        return result;
    }

    private static T RunUnderGate<T>(Func<T> work)
    {
        // Android (Fase 6, ADR 0060): la interfaz móvil analiza la legalidad de las casillas en segundo plano bajo SU cerrojo (<c>LegalityGate</c>). Orden de toma ÚNICO en todo el producto:
        // primero el cerrojo del anfitrión (<see cref="ExternalGate"/>) y después el propio, el mismo orden que ya siguen los análisis de fondo (solo toman el primero): así no hay bloqueo mutuo
        // (ABBA) en ningún camino. En el escritorio ExternalGate es null y no cambia nada.
        var external = ExternalGate;
        if (external is null) { lock (Gate) return Swap(work); }
        lock (external) lock (Gate) return Swap(work);
    }

    private static T Swap<T>(Func<T> work)
    {
        var before = ParseSettings.Settings;
        var temporales = new LegalitySettings();
        ParseSettings.Settings = temporales;
        try { return work(); }
        finally
        {
            // Solo se devuelve lo del usuario si el valor vigente sigue siendo el que puse yo. Si otro camino que no toma estos cerrojos (o los toma en otro orden) entregó mientras tanto ajustes nuevos
            // al Core (StartupUtil.ReloadSettings / ParseSettings.Initialize), esos son ya los del usuario y restaurar «before» los pisaría en silencio (actualización perdida).
            if (ReferenceEquals(ParseSettings.Settings, temporales)) ParseSettings.Settings = before;
        }
    }

    /// <summary>Cerrojo adicional que ya usa el anfitrión para sus propios análisis del Core (Android: <c>Pokekeep.Sprites.LegalityGate.Cerrojo</c>). Orden de toma: primero este, después el propio (<c>Gate</c>); null en el escritorio.</summary>
    public static object? ExternalGate { get; set; }

    /// <summary>Mismo análisis que el menú «Legalidad» de PKHeX: tabla personal del guardado y tipo de casilla, con la severidad por defecto del Core. Fija el estado estático del Core con los datos del guardado, como hace PKHeX al abrirlo.</summary>
    public static LegalityAnalysis Analyze(PKM pk, SaveFile sav, StorageSlotType slotType)
        => WithDefaultSettings(() =>
        {
            ParseSettings.InitFromSaveFileData(sav);
            return new LegalityAnalysis(pk, sav.Personal, slotType);
        });

    /// <summary>
    /// Copia del candidato tal como quedaría al escribirlo en este guardado con el import por defecto de PKHeX (<c>AdaptToSaveFile</c>: manejador actual, datos del entrenador que lo trae...).
    /// Hito 5: validar el candidato SIN adaptar rechazaba Pokémon que el propio guardado deja legales al escribirlos (p. ej. un Pokémon de otro entrenador en el equipo de Gen 7).
    /// </summary>
    public static PKM AdaptedClone(PKM pk, SaveFile sav, StorageSlotType slotType)
    {
        var copy = pk.Clone();
        sav.AdaptToSaveFile(copy, isParty: slotType is StorageSlotType.Party or StorageSlotType.BattleBox);
        return copy;
    }

    /// <summary>
    /// Hito 5: el candidato que debe escribirse en este guardado. Si ya es válido tal cual, ese mismo (un Pokémon del jugador que solo se ha ajustado: adaptarlo de nuevo podría cambiarle el entrenador
    /// actual y sus memorias); si no, su versión adaptada al guardado (<see cref="AdaptedClone"/>) cuando ésa sí es válida (un Pokémon de otro entrenador recién construido). Null si ninguno lo es.
    /// 0.6.6 (A3): la comprobación de «necesita HOME» se repite sobre la copia adaptada, que es lo que se escribe.
    /// </summary>
    public static PKM? ResolveForSave(PKM candidate, SaveFile sav, StorageSlotType slotType, bool tolerateMissingHomeTracker)
    {
        if (IsValid(candidate, sav, slotType, tolerateMissingHomeTracker)) return candidate;
        var adapted = AdaptedClone(candidate, sav, slotType);
        return IsValid(adapted, sav, slotType, tolerateMissingHomeTracker) ? adapted : null;
    }

    /// <summary>
    /// Igual que <see cref="Analyze"/>, y devuelve si el Pokémon es legal con la severidad por defecto del Core. Con <paramref name="tolerateMissingHomeTracker"/> (resultado <c>SuccessNeedsHome</c>,
    /// ajuste R-009) se acepta además el Pokémon cuyo ÚNICO defecto es la falta de la matrícula de HOME (resultados, movimientos y recordables válidos todos); cualquier otra cosa lo rechaza.
    /// La matrícula no se toca (regla 6) y la severidad global de PKHeX no se cambia a «sospechoso» en ningún momento.
    /// </summary>
    public static bool IsValid(PKM pk, SaveFile sav, StorageSlotType slotType, bool tolerateMissingHomeTracker)
        => WithDefaultSettings(() =>
        {
            ParseSettings.InitFromSaveFileData(sav);
            var la = new LegalityAnalysis(pk, sav.Personal, slotType);
            if (la.Valid) return true;
            return tolerateMissingHomeTracker && OnlyTrackerMissing(la);
        });

    /// <summary>
    /// uso10 (decisión del propietario, 20-oct-2026): barrido de los bytes de basura de la edición masiva. Si lo ÚNICO que marca el análisis (severidad por defecto del Core) son bytes de basura que
    /// deberían estar borrados (<c>TrashBytesShouldBeEmpty</c>) en el apodo, el entrenador original o el último entrenador (cadenas UTF-16: formato 8 o superior y Let's Go), devuelve una copia con
    /// esos bytes a cero detrás del final de cada texto (lo mismo que «Borrar basura» del editor de texto de PKHeX) SOLO si la copia queda legal del todo; si no, null y no se toca nada.
    /// Con <paramref name="tolerateMissingHomeTracker"/> se acepta además la falta de la matrícula de HOME, con el mismo criterio que <see cref="IsValid"/>.
    /// </summary>
    public static PKM? CleanedOfTrash(PKM pk, SaveFile sav, StorageSlotType slotType, bool tolerateMissingHomeTracker)
        => WithDefaultSettings(() =>
        {
            if (pk.Species == 0 || !(pk.Format >= 8 || pk.Context == EntityContext.Gen7b)) return null;
            ParseSettings.InitFromSaveFileData(sav);
            var la = new LegalityAnalysis(pk, sav.Personal, slotType);
            if (la.Valid || !la.Parsed || !la.Info.Moves.All(m => m.Valid) || !la.Info.Relearn.All(m => m.Valid)) return null;
            var invalid = la.Results.Where(r => !r.Valid).ToList();
            var trash = invalid.Where(r => r.Result == LegalityCheckResultCode.TrashBytesShouldBeEmpty).ToList();
            if (trash.Count == 0 || invalid.Any(r => r.Result is not (LegalityCheckResultCode.TrashBytesShouldBeEmpty or LegalityCheckResultCode.TransferTrackerMissing))) return null;
            var copy = pk.Clone();
            foreach (var r in trash)
            {
                switch (r.Identifier)
                {
                    case CheckIdentifier.Trainer: ClearAfterTerminator(copy.OriginalTrainerTrash); break;
                    case CheckIdentifier.Nickname: ClearAfterTerminator(copy.NicknameTrash); break;
                    case CheckIdentifier.Handler: ClearAfterTerminator(copy.HandlingTrainerTrash); break;
                    default: return null;
                }
            }
            copy.RefreshChecksum();
            var after = new LegalityAnalysis(copy, sav.Personal, slotType);
            return after.Valid || (tolerateMissingHomeTracker && OnlyTrackerMissing(after)) ? copy : null;
        });

    /// <summary>Pone a cero lo que queda detrás del terminador de una cadena UTF-16 (el texto y su terminador no cambian). Sin terminador no toca nada.</summary>
    private static void ClearAfterTerminator(Span<byte> text)
    {
        for (var i = 0; i + 1 < text.Length; i += 2)
        {
            if (text[i] != 0 || text[i + 1] != 0) continue;
            text[(i + 2)..].Clear();
            return;
        }
    }

    /// <summary>Lo único que marca el análisis (a la severidad por defecto) es la falta de la matrícula de transferencia de HOME: ni otros resultados, ni movimientos, ni recordables inválidos. Mismo criterio que <c>OfficialValidator</c>.</summary>
    public static bool OnlyTrackerMissing(LegalityAnalysis la)
    {
        if (!la.Parsed) return false;
        var invalid = la.Results.Where(r => !r.Valid).ToList();
        return invalid.Count > 0
            && invalid.All(r => r.Result == LegalityCheckResultCode.TransferTrackerMissing)
            && la.Info.Moves.All(m => m.Valid)
            && la.Info.Relearn.All(m => m.Valid);
    }
}
