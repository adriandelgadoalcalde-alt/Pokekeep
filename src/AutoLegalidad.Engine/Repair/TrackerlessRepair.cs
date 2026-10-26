// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Engine.Context;
using PKHeX.Core;

namespace AutoLegalidad.Engine.Repair;

/// <summary>
/// R-009 (ajuste «sin matricula de HOME», Hito 3b, defecto corregido en el Hito 3c): con la severidad <c>Fishy</c> el Core da por «visitado HOME» a un Pokemon sin rastreador y perdona las
/// cintas de generaciones anteriores; el generador (ALM, <c>APILegality.SetAllLegalRibbons</c>) las pone todas, y despues la comprobacion <c>TrackerOnly</c> (con la severidad por defecto el
/// Core solo puede marcar el rastreador) rechazaba el candidato por esas cintas. Aqui se piden al propio Core, con SU rutina publica (<see cref="RibbonApplicator.FixInvalidRibbons"/>), que quite
/// las cintas que la severidad por defecto da por invalidas. No se fuerza nada: lo que quede lo decide el analisis oficial (regla 1); no se toca el rastreador (regla 6).
/// </summary>
public static class TrackerlessRepair
{
    /// <summary>True si se quito alguna cinta. Solo actua sobre Pokemon de Gen8+ sin rastreador con el ajuste activado.</summary>
    public static bool StripRibbonsInvalidByDefault(PKM pk, Destination dest)
    {
        if (dest.TrackerSeverity != Severity.Fishy || pk.IsEgg || pk is not IHomeTrack { Tracker: 0 } || pk is not IRibbonIndex) return false;
        lock (Infra.EngineGlobalState.Gate)
        {
            dest.ApplyParseSettings();
            ParseSettings.Settings.HOMETransfer.HOMETransferTrackerNotPresent = Severity.Invalid;
            try
            {
                var la = new LegalityAnalysis(pk, dest.Personal, dest.SlotType);
                if (!la.Parsed || la.Results.All(r => r.Valid || r.Result is not (LegalityCheckResultCode.RibbonsInvalid_0 or LegalityCheckResultCode.RibbonsMissing_0))) return false;
                var before = pk.Data.ToArray();
                RibbonApplicator.FixInvalidRibbons(new RibbonVerifierArguments(pk, la.EncounterMatch, la.Info.EvoChainsAllGens));
                pk.RefreshChecksum();
                return !pk.Data.SequenceEqual(before);
            }
            finally
            {
                ParseSettings.Settings.HOMETransfer.HOMETransferTrackerNotPresent = dest.TrackerSeverity;
            }
        }
    }
}
