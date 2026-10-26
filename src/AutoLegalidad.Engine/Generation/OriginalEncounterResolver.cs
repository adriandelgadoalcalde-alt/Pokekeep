// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Engine.Catalog;
using AutoLegalidad.Engine.Context;
using AutoLegalidad.Engine.Infra;
using PKHeX.Core;

namespace AutoLegalidad.Engine.Generation;

/// <summary>
/// Identifica el encuentro de ORIGEN del Pokemon para intentar conservarlo primero.
/// 1) La IDENTIDAD de un encuentro con entrenador PROPIO (tarjeta de evento, regalo, intercambio del juego, sombra de GameCube...) se comprueba contra los datos del
///    propio encuentro: especie, entrenador original, TID, SID e idioma coinciden (nunca por nombre). Es una prueba mucho mas fuerte que lo que el analisis oficial pueda
///    haber elegido tras un defecto (un variocolor imposible o un nivel incoherente hacen que el analisis salte a OTRO encuentro o a ninguno: Hito 3).
/// 2) Si no hay identidad propia, el encuentro que reconoce el analisis oficial (si es uno de verdad).
/// </summary>
public static class OriginalEncounterResolver
{
    /// <summary>
    /// Encuentros DISTINTOS que se prueban. Una especie con muchas tarjetas (el linaje de Audino tiene mas de 600 en Gen5, la mayoria con el entrenador del receptor) puede dejar la
    /// buena mas alla del indice 600 que se usaba antes: Hito 3b, PGF de Audino con variocolor forzado (13 casos, la identidad se perdia).
    /// </summary>
    private const int MaxProbes = 4000;
    private const int MaxEnumerated = 20000;

    public static IEncounterable? Resolve(PKM original, Destination dest, LegalityAnalysis analysis)
    {
        if (original.OriginalTrainerName.Length != 0)
        {
            var byIdentity = FindByIdentity(original, dest);
            if (byIdentity is not null)
                return byIdentity;
        }
        var enc = analysis.EncounterOriginal;
        if (enc is not EncounterInvalid) return enc;
        return FromWithoutShiny(original, dest);
    }

    /// <summary>
    /// Una tarjeta que reparte el entrenador del receptor no tiene identidad propia que buscar, y un variocolor imposible para esa tarjeta hace que el analisis oficial no encuentre ningun
    /// encuentro. Se pregunta entonces al Core por el encuentro que casaria SIN el variocolor (una copia con el variocolor quitado, solo para identificar el origen: lo que se entrega
    /// sigue pasando por la validacion completa y el variocolor se relaja despues, como en cualquier escalon de la escalera). Hito 3b: huevo de Manaphy de Diamante Brillante.
    /// </summary>
    private static IEncounterable? FromWithoutShiny(PKM original, Destination dest)
    {
        if (!original.IsShiny) return null;
        // Gen1/2: el variocolor sale de los DV y CommonEdits.SetUnshiny entra en un bucle sin fin (PKM.SetPIDGender sobre un PK2, p. ej. Raikou de Cristal); no hay PID que reajustar.
        if (original.Format <= 2) return null;
        var probe = original.Clone();
        // El editor del Core no admite cancelacion: llamada con plazo (GuardedCore), si no termina se da por no identificable.
        if (!GuardedCore.TryCall(() => { probe.SetIsShiny(false); return true; }, TimeSpan.FromSeconds(3), out var done) || !done) return null;
        lock (EngineGlobalState.Gate)
        {
            dest.ApplyParseSettings();
            var la = new LegalityAnalysis(probe, dest.Personal, dest.SlotType);
            var found = la.EncounterOriginal;
            return found is EncounterInvalid ? null : found;
        }
    }

    private static IEncounterable? FindByIdentity(PKM original, Destination dest)
    {
        var blank = dest.Sav.BlankPKM;
        blank.Species = original.Species;
        blank.Form = original.Form;
        blank.Gender = blank.GetSaneGender();
        var probes = 0;
        var enumerated = 0;
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var seenGifts = new HashSet<string>(StringComparer.Ordinal); // las tarjetas se materializan de nuevo en cada enumeracion: se distinguen por su clave de catalogo (que lleva el hash de la tarjeta)
        IEncounterable? best = null; // OT + TID + idioma coinciden; el SID puede diferir (un defecto de variocolor lo puede tocar): vale si no hay una exacta
        var bestRank = int.MaxValue;
        lock (EngineGlobalState.Gate)
        {
            dest.ApplyParseSettings();
            foreach (var cand in CoreVersions.Encounters(blank, dest.Trainer, ReadOnlyMemory<ushort>.Empty))
            {
                if (++enumerated > MaxEnumerated) break;
                if (cand is not IEncounterConvertible conv || cand.IsEgg != original.IsEgg) continue;
                if (!seen.Add(cand) || (cand is MysteryGift && EncounterKeys.For(cand) is { } gk && !seenGifts.Add(gk))) continue;
                if (++probes > MaxProbes) break;
                // Regalos de las maquinas de distribucion de Gen3 (Nueva York y Japon): el entrenador (OT/TID) lo sortea la plantilla en cada llamada, asi que UNA muestra casi nunca coincide con la
                // del usuario. Su identidad se decide con las reglas del propio Core (Gen3Gift.Matches: Distribution3NY.IsValidTrainerID/IsValidTrainerName y Distribution3JPN.GetTrainerID/
                // IsTrainerNameValid), no con una muestra al azar.
                if (cand is EncounterGift3NY or EncounterGift3JPN)
                {
                    if (!Gen3Gift.Matches(cand, original)) continue;
                    var giftRank = cand.Species == original.Species ? 0 : 2; // regla 14: la que solo evoluciona hasta la especie del usuario va despues
                    if (giftRank == 0) return cand;
                    if (giftRank < bestRank) { best = cand; bestRank = giftRank; }
                    continue;
                }
                PKM probe;
                try { probe = conv.ConvertToPKM(dest.Trainer); }
                catch (Exception ex) when (ex is not OutOfMemoryException) { continue; }
                // Solo identifican los encuentros con entrenador PROPIO (los que reparten el del receptor no distinguen un encuentro de otro).
                if (probe.OriginalTrainerName.Length == 0 || (probe.OriginalTrainerName == dest.Trainer.OT && probe.TID16 == dest.Trainer.TID16)) continue;
                if (!string.Equals(probe.OriginalTrainerName, original.OriginalTrainerName, StringComparison.Ordinal)) continue;
                if (probe.TID16 != original.TID16 || probe.Language != original.Language) continue;
                // Varias tarjetas pueden compartir entrenador (la de un Eevee y la de su evolucion; la de Leyendas Arceus y la de Diamante Brillante con el mismo OT/TID).
                // Ranking: (1) la que NO exige un rastreador de HOME en el destino, (2) la que ya es de la especie del usuario (regla 14: la que solo evoluciona hasta ella queda despues),
                // (3) la de SID exacto. Una puntuacion perfecta se devuelve en el acto.
                var rank = (HomeTrackerRule.IsRequired(cand, dest.Context) ? 4 : 0) + (probe.Species == original.Species ? 0 : 2) + (probe.SID16 != original.SID16 && original.Format >= 3 ? 1 : 0);
                if (rank == 0) return cand;
                if (rank < bestRank) { best = cand; bestRank = rank; }
            }
        }
        return best;
    }
}
