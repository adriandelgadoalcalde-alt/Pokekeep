// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Engine.Catalog;
using AutoLegalidad.Engine.Context;
using AutoLegalidad.Engine.Generation;
using PKHeX.Core;

namespace AutoLegalidad.Engine.Changes;

/// <summary>
/// 0.6.5 (ADR 0052): frases de «Ver cambios» sobre el ORIGEN de un Pokémon que Pokekeep construye: quién figura como entrenador original (EO) y por qué. Todo sale del análisis oficial del
/// candidato final (<see cref="OriginAssessment"/>), nunca de una suposición.
/// </summary>
public static class OriginExplainer
{
    public static void Explain(PKM original, PKM final, OriginAssessment origin, Destination dest, CatalogReader? catalog, ChangeLog log, IEnumerable<string>? unfitNotes = null)
    {
        var player = dest.Trainer.OT;
        var keptForeign = original.OriginalTrainerName.Length != 0 && !string.Equals(original.OriginalTrainerName, player, StringComparison.Ordinal)
                          && string.Equals(final.OriginalTrainerName, original.OriginalTrainerName, StringComparison.Ordinal);
        if (keptForeign)
        {
            log.Origin(ReasonCodes.OriginTrainerKept, "conservado",
                $"Entrenador original conservado: «{original.OriginalTrainerName}». El Pokémon ya era de otro entrenador y su origen lo exige; no se ha pasado a tu entrenador.");
            return;
        }
        if (origin.OwnerIsPlayer && origin.Rank == OriginRank.SameGame)
            log.Origin(ReasonCodes.OriginOwn, "propio", "Origen propio: capturado o criado en este juego con tu entrenador.");
        else if (origin.OwnerIsPlayer)
        {
            var from = origin.EncounterVersion == GameVersion.GO ? "Pokémon GO" : GameName(origin.EncounterVersion, origin.EncounterGeneration, catalog);
            log.Origin(ReasonCodes.OriginTransferred, "transferido", $"Transferido desde {from}: tu entrenador figura como original.");
        }
        else
        {
            var why = origin.Rank switch { OriginRank.Event => "evento", OriginRank.FixedTrainer => "intercambio del juego", _ => "otro entrenador" };
            log.Origin(ReasonCodes.OriginOtherTrainer, why, $"Origen con otro entrenador: {why}. No hay ruta propia en este juego.");
        }
        // El entrenador anterior solo se explica cuando no es una transferencia (en una transferencia el jugador figura ya como EO y como entrenador actual).
        if (origin.HasHandler && origin.Rank != OriginRank.OtherGame)
        {
            if (!origin.OwnerMatches)
                log.Origin(ReasonCodes.OriginHandler, "tú", "Entrenador actual: eres tú; el Pokémon es de otro entrenador y el juego te registra como quien lo maneja ahora.");
            else if (origin.Evolved)
                log.Origin(ReasonCodes.OriginHandler, origin.ForeignHandler ? "otro entrenador" : "tú",
                    origin.ForeignHandler ? "Entrenador anterior: necesario por evolución por intercambio." : "Entrenador anterior: eres tú; la evolución por intercambio lo exige.");
            else
                log.Origin(ReasonCodes.OriginHandler, origin.ForeignHandler ? "otro entrenador" : "tú", "Entrenador anterior: lo exige el Core para este origen.");
        }
        if (unfitNotes is not null && !(origin.OwnerIsPlayer && origin.Rank == OriginRank.SameGame))
            foreach (var n in unfitNotes.Distinct()) log.Origin(ReasonCodes.OriginNameFit, "no cabe", n);
    }

    private static string GameName(GameVersion v, byte generation, CatalogReader? catalog)
    {
        if (catalog?.GameNameEs(v.ToString()) is { Length: > 0 } es) return es;
        return generation > 0 ? $"un juego de la generación {generation}" : "otro juego";
    }
}
