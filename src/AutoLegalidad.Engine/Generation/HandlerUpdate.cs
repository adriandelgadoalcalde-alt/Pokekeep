// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Engine.Context;
using PKHeX.Core;

namespace AutoLegalidad.Engine.Generation;

/// <summary>
/// Al entrar en la partida de OTRO entrenador el juego fija el «entrenador actual» (HT) con las rutinas publicas del Core (<c>IHandlerUpdate.UpdateHandler</c>; BD/SP necesita
/// <c>PKH.UpdateHandler</c> porque su rutina no lo fija si el OT ya es el del destino). Un Pokemon creado desde una plantilla de HOME/GO y convertido al formato del destino sin esto
/// sale con <c>TransferHandlerMismatchName</c>. No toca el rastreador de HOME (regla 6).
/// </summary>
public static class HandlerUpdate
{
    public static void Apply(PKM pk, Destination dest)
    {
        if (pk.IsEgg) return;
        if (pk is IHandlerUpdate hu) hu.UpdateHandler(dest.Trainer);
        // Si el OT ya es el del destino la rutina del formato no fija el HT, pero el «entrenador actual» sigue siendo HOME (CurrentHandler 1): se fija igual que hace HOME (PKH.UpdateHandler).
        switch (pk)
        {
            case PK8 { CurrentHandler: 1 } a: PKH.UpdateHandler(a, dest.Trainer); break;
            case PB8 { CurrentHandler: 1 } b: PKH.UpdateHandler(b, dest.Trainer); break;
            case PA8 { CurrentHandler: 1 } c: PKH.UpdateHandler(c, dest.Trainer); break;
            case PK9 { CurrentHandler: 1 } d: PKH.UpdateHandler(d, dest.Trainer); break;
            case PA9 { CurrentHandler: 1 } e: PKH.UpdateHandler(e, dest.Trainer); break;
        }
    }
}
