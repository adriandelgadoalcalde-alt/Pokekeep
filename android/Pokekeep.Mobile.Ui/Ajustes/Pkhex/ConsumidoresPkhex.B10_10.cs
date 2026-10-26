// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace Pokekeep.Mobile.Ui.Ajustes.Pkhex;

public static partial class ConsumidoresPkhex
{
    /// <summary>
    /// Bloque 10-10: severidades y reglas del comprobador de legalidad de PKHeX. Son del Core (<c>LegalityAnalysis</c> lee <c>ParseSettings.Settings</c>): <c>AplicadorPkhex</c> los aplica con <c>ReloadSettings</c> y cambian lo que
    /// ENSEÑA la interfaz (insignia de la casilla, informe del editor, «Verificar todos»). NO cambian lo que Legalizar, Optimizar o la Edición masiva aceptan como legal: la segunda opinión de Pokekeep usa siempre los valores
    /// por defecto del Core (ADR 0053, <c>LegalityCheck</c>). <c>Legality.Handler.CurrentHandlerMismatch</c> se declara desde PKHeX 26.10.18, cuando <c>HistoryVerifier</c> empezó a leerlo (fila PK-SET-186).
    /// </summary>
    static partial void DeclararB10_10(Action<string, string> d)
    {
        const string Vista = "Insignia de legalidad de la casilla, informe del editor y «Verificar todos»: el Core lo lee de ParseSettings.Settings";
        d("Legality.FramePattern.RNGFrameNotFound3", Vista + " (cuadro RNG de Gen3 no encontrado)");
        d("Legality.FramePattern.RNGFrameNotFound4", Vista + " (cuadro RNG de Gen4 no encontrado, y pasan los Pokémon de presión)");
        d("Legality.FramePattern.EggRandomAnyType3", Vista + " (PID e IVs de un huevo criado de Gen3: EncounterEgg3.IsCompatible)");
        d("Legality.FramePattern.EggRandomAnyType4", Vista + " (PID e IVs de un huevo criado de Gen4: EncounterEgg4.IsCompatible)");
        d("Legality.Game.Gen3.AllowBattleTowerTeamSwap", Vista + " (cintas de la Torre Batalla: RibbonRules)");
        d("Legality.Game.Gen7.Gen7TransferStarPID", Vista + " (PID variocolor estrella de un Pokémon de Gen1/2: TransferVerifier)");
        d("Legality.Game.Gen8.Gen8MemoryMissingHT", Vista + " (recuerdo de Gen8 que falta al entrenador actual: MemoryVerifier)");
        d("Legality.Handler.CheckActiveHandler", Vista + " (manejador actual contra los datos de la partida abierta: HistoryVerifier)");
        d("Legality.Handler.CurrentHandlerMismatch", Vista + " (severidad de la línea «manejador actual incorrecto» cuando la partida abierta no es el entrenador esperado: HistoryVerifier)");
        d("Legality.Handler.Restrictions.AllowHandleOTGen6", Vista + " (el entrenador original puede ser el manejador actual en Gen6)");
        d("Legality.Handler.Restrictions.AllowHandleOTGen7", Vista + " (el entrenador original puede ser el manejador actual en Gen7)");
        d("Legality.Handler.Restrictions.AllowHandleOTGen7b", Vista + " (el entrenador original puede ser el manejador actual en Gen7b)");
        d("Legality.Handler.Restrictions.AllowHandleOTGen8", Vista + " (el entrenador original puede ser el manejador actual en Gen8)");
        d("Legality.Handler.Restrictions.AllowHandleOTGen8a", Vista + " (el entrenador original puede ser el manejador actual en Gen8a)");
        d("Legality.Handler.Restrictions.AllowHandleOTGen8b", Vista + " (el entrenador original puede ser el manejador actual en Gen8b)");
        d("Legality.Handler.Restrictions.AllowHandleOTGen9", Vista + " (el entrenador original puede ser el manejador actual en Gen9)");
        d("Legality.Handler.Restrictions.AllowHandleOTGen9a", Vista + " (el entrenador original puede ser el manejador actual en Gen9a)");
        d("Legality.HOMETransfer.HOMETransferTrackerNotPresent", Vista + " (falta la matrícula de HOME; solo cambia la severidad que se enseña, nunca se inventa una matrícula)");
        d("Legality.HOMETransfer.ZeroHeightWeight", Vista + " (altura y peso a cero: MiscScaleVerifier)");
    }
}
