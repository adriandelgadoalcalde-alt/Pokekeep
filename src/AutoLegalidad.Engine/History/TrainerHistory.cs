// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Engine.Changes;
using AutoLegalidad.Engine.Context;
using AutoLegalidad.Engine.Validation;
using PKHeX.Core;

namespace AutoLegalidad.Engine.History;

/// <summary>
/// Hito 4c (R-010): entrenador anterior (HT) y memorias coherentes con el RECORRIDO del Pokemon (ADR 0028 seccion 5; reglas con su fuente en el informe del Core del hito).
/// El Core da por valido un HT sin ningun motivo (sondas: salvaje de Gen6, 7, 7b, 8, 8a, 8b, 9 y 9a con HT puesto y sin recorrido = valido), asi que la plausibilidad no la da el. Se decide asi:
/// - El HT esta JUSTIFICADO si el OT no es el entrenador de la partida (en un guardado real el Core espera <c>CurrentHandler = 1</c>: HistoryVerifier.cs:101-118) o si el Core lo EXIGE: transferencia (<c>enc.Context != pk.Context</c>, HistoryVerifier.cs:120-121), origen que el OT no puede manejar (<c>neverOT</c>, :294-310: eventos con OT, intercambios de NPC, GO...)
///   o evolucion por intercambio (EvolutionMethod.cs:98). En lugar de duplicar esa logica se usa el propio Core como oraculo: se quita el HT y sus memorias y se valida; si sigue valido, no hacia falta.
/// - ALM pone HT en cuanto la evolucion necesita intercambio, incluso cuando el entrenador ES el OT (mismo nombre con <c>CurrentHandler = 1</c>: el juego solo asigna HT si el manejador es otro, HistoryVerifier.cs:101-118,126-134).
///   Se sustituye por el recorrido real de esa evolucion: otro entrenador (un amigo) fue el ultimo en manejarlo y ahora vuelve a ser del OT (<c>CurrentHandler = 0</c>, HT = el amigo).
/// - Un Pokemon de Gen6 que nunca paso por el Banco no lleva la memoria de HT «estilo Banco» (variable 0): lleva la de un intercambio normal (<c>SetTradeMemoryHT6(false)</c>, PK6.cs:451).
/// Todo cambio se acepta solo si el Core, el destino y el round-trip siguen dando el Pokemon por valido.
/// </summary>
internal static class TrainerHistory
{
    /// <summary>Nombre del «amigo» con el que se intercambio, segun el idioma del Pokemon (cualquier cadena distinta del OT vale para el Core; esta es neutral).</summary>
    internal static string FriendName(int language) => (LanguageID)language switch
    {
        LanguageID.English => "Friend",
        LanguageID.Japanese => "ともだち",
        LanguageID.French => "Ami",
        LanguageID.Italian => "Amico",
        LanguageID.German => "Freund",
        LanguageID.Korean => "친구",
        _ => "Amigo",
    };

    /// <summary>
    /// Recorrido de TRANSFERENCIA: el Pokemon nacio en otro contexto o generacion (Gen1/2 por Poke Transporter, Banco, HOME) o viene de Pokemon GO (Parque GO de Let's Go, HOME): el jugador que pasa SU Pokemon figura como
    /// entrenador original y como quien lo maneja, y el Core exige <c>CurrentHandler = 1</c> (HistoryVerifier.cs:120-121).
    /// </summary>
    internal static bool IsTransfer(PKM pk, IEncounterTemplate enc) => pk.VC || enc.Context != pk.Context || enc.Generation != pk.Format || enc.Version == GameVersion.GO;

    internal static bool HasHandler(PKM pk) => pk.CurrentHandler == 1 || pk.HandlingTrainerName.Length != 0;

    /// <summary>Deja el Pokemon como si NUNCA hubiera tenido entrenador anterior: sin HT, sin sus memorias, amistad del OT.</summary>
    internal static void ClearHandler(PKM pk, IEncounterTemplate enc)
    {
        pk.CurrentHandler = 0;
        pk.HandlingTrainerName = string.Empty;
        pk.HandlingTrainerGender = 0;
        if (pk is IHandlerLanguage hl) hl.HandlingTrainerLanguage = 0;
        pk.HandlingTrainerFriendship = 0;
        if (pk is IAffection a) a.HandlingTrainerAffection = 0;
        if (pk is IMemoryHT h) h.ClearMemoriesHT();
        if (!pk.IsEgg) pk.CurrentFriendship = HistoryVerifier.GetSuggestedFriendshipOT(pk, enc);
    }

    /// <summary>True si el HT sobra: el Core sigue dando el Pokemon por valido sin el.</summary>
    internal static bool HandlerIsRemovable(PKM pk, LegalityAnalysis la, Destination dest)
    {
        if (!HasHandler(pk)) return false;
        // En un guardado REAL el Core espera CurrentHandler = 1 cuando el OT NO es el entrenador de la partida (HistoryVerifier.cs:101-118); en el destino virtual esa comprobacion esta desactivada (State.Exportable = false)
        // y no lo veria: un Pokemon de otro entrenador (intercambio, evento con OT) conserva su HT.
        if (!dest.Trainer.IsFromTrainer(pk)) return false;
        var copy = pk.Clone();
        ClearHandler(copy, la.EncounterMatch);
        return HistoryPhase.Analyze(copy, dest).Valid;
    }

    /// <summary>
    /// HT con el mismo nombre que el OT SIN transferencia (<c>enc.Context == pk.Context</c>): no existe en el juego (HistoryVerifier.cs:101-118,126-134). En una transferencia SI es natural: el jugador que pasa SU Pokemon de una
    /// consola a otra figura como entrenador original y como quien lo maneja, y el Core exige entonces <c>CurrentHandler = 1</c> (HistoryVerifier.cs:120-121).
    /// </summary>
    private static bool SameName(PKM pk, IEncounterTemplate enc) => !IsTransfer(pk, enc) && pk.HandlingTrainerName.Length != 0 && string.Equals(pk.HandlingTrainerName, pk.OriginalTrainerName, StringComparison.OrdinalIgnoreCase);

    /// <summary>Memoria de HT «estilo Banco» (variable 0) en un Pokemon de Gen6 cuyo origen es la propia Gen6 (nunca pasó por el Banco).</summary>
    private static bool BankStyleMemoryWithoutBank(PKM pk, IEncounterTemplate enc)
        => pk is PK6 p && p.HandlingTrainerMemory == 4 && p.HandlingTrainerMemoryVariable == 0 && enc.Context == pk.Context;

    public static void Analyze(PKM pk, LegalityAnalysis la, Destination dest, List<PlausibilityIssue> issues)
    {
        if (pk.Format < 6) return;
        var enc = la.EncounterMatch;
        if (pk is IMemoryHT { HandlingTrainerMemory: not 0 } && !HasHandler(pk))
            issues.Add(new PlausibilityIssue(PlausibilityCodes.MemoryIncoherent, "memoria de entrenador anterior sin entrenador anterior"));
        if (BankStyleMemoryWithoutBank(pk, enc))
            issues.Add(new PlausibilityIssue(PlausibilityCodes.MemoryIncoherent, "memoria de HT «estilo Banco» sin haber pasado por el Banco"));
        if (!HasHandler(pk)) return;
        // 0.6.5 (ADR 0052): HT = OT solo es incoherente si sobra (el Core sigue validando sin el) o si figura como quien lo maneja ahora (CurrentHandler 1) sin transferencia; si la evolucion por intercambio lo exige,
        // el entrenador anterior es el jugador (CurrentHandler 0) y es lo que se quiere.
        if (SameName(pk, enc) && (pk.CurrentHandler == 1 || HandlerIsRemovable(pk, la, dest)))
            issues.Add(new PlausibilityIssue(PlausibilityCodes.UnjustifiedHandler, $"el entrenador anterior («{pk.HandlingTrainerName}») es el propio entrenador original"));
        else if (!SameName(pk, enc) && HandlerIsRemovable(pk, la, dest))
            issues.Add(new PlausibilityIssue(PlausibilityCodes.UnjustifiedHandler, $"entrenador anterior («{pk.HandlingTrainerName}») sin intercambio, transferencia ni evolución que lo exija"));
    }

    public static void Apply(ref PKM cur, ref ValidationOutcome curOutcome, HistoryContext ctx)
    {
        if (cur.Format < 6) return;
        var la = HistoryPhase.Analyze(cur, ctx.Dest);
        if (!la.Parsed) return;
        var enc = la.EncounterMatch;

        // 1) HT que sobra: fuera, con sus memorias (solo si el OT es el entrenador de la partida: si no, el Core espera CurrentHandler = 1 en un guardado real).
        if (HasHandler(cur) && ctx.Dest.Trainer.IsFromTrainer(cur))
        {
            var name = cur.HandlingTrainerName;
            HistoryPhase.TryStep(ref cur, ref curOutcome, ctx, pk =>
            {
                ClearHandler(pk, enc);
                return (ReasonCodes.HistoryTrainer, "Entrenador anterior", name, string.Empty,
                    "se quitó el entrenador anterior y sus memorias: este Pokémon no se ha intercambiado ni transferido, ni necesita un intercambio para evolucionar.", 1);
            });
            if (!HasHandler(cur)) return;   // aceptado: nada mas que hacer con el HT
        }

        // 2) HT con el mismo nombre que el OT (ALM, evolucion por intercambio). 0.6.5 (ADR 0052): el entrenador anterior sigue siendo el JUGADOR (CurrentHandler 0): el Core admite HT = OT sin
        //    transferencia cuando la evolucion exige un entrenador anterior (sonda `sonda-ht`). Solo si el Core lo rechaza se vuelve al recorrido anterior (un amigo que lo devolvio).
        if (HasHandler(cur) && SameName(cur, enc) && cur.CurrentHandler == 1)
        {
            var name = cur.HandlingTrainerName;
            HistoryPhase.TryStep(ref cur, ref curOutcome, ctx, pk =>
            {
                pk.CurrentHandler = 0;
                return (ReasonCodes.HistoryTrainer, "Entrenador anterior", name, name,
                    "tu entrenador figura también como entrenador anterior: la evolución por intercambio exige que el Pokémon haya pasado por un entrenador anterior y, como es tuyo, ese entrenador eres tú.", 1);
            });
        }
        if (HasHandler(cur) && SameName(cur, enc) && cur.CurrentHandler == 1)
        {
            var friend = FriendName(cur.Language);
            var name = cur.HandlingTrainerName;
            HistoryPhase.TryStep(ref cur, ref curOutcome, ctx, pk =>
            {
                pk.CurrentHandler = 0;
                pk.HandlingTrainerName = friend;
                pk.HandlingTrainerGender = (byte)(pk.OriginalTrainerGender ^ 1);
                return (ReasonCodes.HistoryTrainer, "Entrenador anterior", name, friend,
                    $"el Core no admite aquí que el entrenador anterior sea el propio entrenador original; ahora es «{friend}», como tras evolucionarlo por intercambio con un amigo y recuperarlo.", 1);
            });
        }

        // 3) Gen6: la memoria de un intercambio normal, no la del Banco.
        if (BankStyleMemoryWithoutBank(cur, enc))
        {
            HistoryPhase.TryStep(ref cur, ref curOutcome, ctx, pk =>
            {
                ((IMemoryHT)pk).SetTradeMemoryHT6(false);
                return (ReasonCodes.HistoryTrainer, "Memoria del entrenador anterior", "Banco", "intercambio", "memoria de intercambio normal (no la del Banco Pokémon, por el que este Pokémon no ha pasado).", 1);
            });
        }
    }
}
