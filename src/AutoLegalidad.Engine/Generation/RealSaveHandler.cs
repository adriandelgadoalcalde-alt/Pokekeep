// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Engine.Context;
using PKHeX.Core;

namespace AutoLegalidad.Engine.Generation;

/// <summary>
/// Hito 5: en un guardado REAL el Core espera <c>CurrentHandler = 1</c> (y el entrenador actual = el jugador) cuando el entrenador original NO es el jugador (HistoryVerifier.cs:101-118,
/// <c>TransferCurrentHandlerInvalid</c>); en el destino virtual esa comprobación está desactivada (<c>State.Exportable = false</c>). Sin esto, un Pokémon de otro entrenador legalizado en una partida
/// de BD/SP (cuyo <c>SetPKM</c> no adapta nada) o con una casilla de equipo salía válido en el motor e inválido al escribirlo. Es lo que haría el juego al recibirlo: el jugador pasa a ser su
/// entrenador actual (rutinas públicas del Core, igual que <see cref="HandlerUpdate"/>). No toca el rastreador de HOME (regla 6) ni ningún dato de origen.
/// </summary>
public static class RealSaveHandler
{
    /// <summary>Devuelve el candidato ajustado (copia) o el mismo si no hay nada que hacer.</summary>
    public static PKM Apply(PKM pk, Destination dest)
    {
        if (!dest.IsRealSave || pk.Format < 6 || pk.IsEgg || pk.Species == 0) return pk;
        if (dest.Trainer.IsFromTrainer(pk) || pk.CurrentHandler == 1) return pk;
        var copy = pk.Clone();
        copy.CurrentHandler = 1;
        HandlerUpdate.Apply(copy, dest);
        // Si la rutina del formato no fijó el nombre del entrenador actual (formatos sin IHandlerUpdate), no se toca nada más: el resultado se revalida fuera.
        return copy.CurrentHandler == 1 ? copy : pk;
    }
}
