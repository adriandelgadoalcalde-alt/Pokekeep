// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Engine.Context;
using PKHeX.Core;

namespace AutoLegalidad.Engine.Repair;

/// <summary>
/// Hito 3b: aplica a un candidato recien generado lo MISMO que haria el guardado del destino al meterlo en una casilla, para que el candidato que se valida (y se entrega)
/// ya sea el estable tras guardar. Hoy: Let's Go (PB7), tambien Espiritu y Humor en reposo. <c>SAV7b.SetPKM</c> llama a <c>UpdateHandler</c> (si el entrenador manejador coincide con el del guardado el Pokemon vuelve
/// a ser «del propio entrenador»: la amistad actual pasa a ser la del OT) y la puntuacion de combate (PC) se calcula con esa amistad; sin recalcularla, el candidato era valido
/// pero la entidad leida de la casilla fallaba con «Los PC calculados no coinciden» (fuente: Saves/SAV7b.cs:99-108, PKM/PB7.cs:574-631 y Legality/Verifiers/Misc/MiscVerifierPB7.cs:22).
/// No es una excepcion de la legalidad: el Core sigue siendo quien decide.
/// </summary>
public static class ImportNormalizer
{
    public static PKM Apply(PKM candidate, Destination dest)
    {
        if (candidate is PB7 pb && !pb.IsEgg)
        {
            lock (Infra.EngineGlobalState.Gate)
            {
                pb.UpdateHandler(dest.Trainer);
                // Meltan/Melmetal (WB7 con tamano FIJO, MiscVerifierPB7.cs:69-72) conservan el tamano del encuentro; el resto lo calcula el juego (PKM/PB7.cs:631-637).
                if (pb.Species is (ushort)Species.Meltan or (ushort)Species.Melmetal) pb.ResetCP(); else pb.ResetCalculatedValues();
                // Hito 4b: en reposo (casilla de caja) Espiritu y Humor valen 100 (MiscVerifierPB7.VerifyMoodSpirit: «At rest, these should be the default values»); solo cambian en el EQUIPO.
                // Sin esto, ningun Pokemon generado para una casilla de caja de Let's Go era valido («Spirit should be 100 for Pokemon not in the player's party»).
                if (dest.SlotType == StorageSlotType.Box && !pb.IsStarter) pb.ResetSpiritMood();
                pb.RefreshChecksum();
            }
        }
        return candidate;
    }
}
