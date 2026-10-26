// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Contracts;
using AutoLegalidad.Engine.Changes;
using AutoLegalidad.Engine.Context;
using AutoLegalidad.Engine.Generation;
using AutoLegalidad.Engine.Optimization;
using AutoLegalidad.Engine.Validation;
using PKHeX.Core;

namespace AutoLegalidad.Engine.Adjust;

/// <summary>Resultado interno de una operacion de Adjust. <see cref="Candidate"/>/<see cref="Outcome"/> solo vienen con <see cref="OperationStatus.Changed"/> (entidad nueva ya validada).</summary>
internal sealed record StepResult(OperationStatus Status, string Message, string? Reason = null, PKM? Candidate = null, ValidationOutcome? Outcome = null);

/// <summary>
/// Hito 4b (R-007): operaciones de edicion de la accion <see cref="LegalizeAction.Adjust"/> que NO buscan nada (amistad, idioma, PP, PS/estado, Pokerus). Cada una se aplica sobre una COPIA y se valida
/// con el validador oficial del motor (Core + destino + ida y vuelta); si la copia queda invalida la operacion se deshace (no se devuelve candidato) y se informa con el primer motivo del Core.
/// Ninguna inventa historia: no se busca otro valor si el maximo no vale, no se vacuna, no se fuerza el idioma.
/// </summary>
internal sealed class AdjustOperations(IOfficialValidator validator, PKM original, Destination dest, bool requireCatalogGate, bool partySize)
{
    private StepResult Commit(PKM candidate, string changedMessage, string refusalMessage)
    {
        var outcome = validator.Validate(candidate, original, dest, requireCatalogGate);
        return outcome.Valid
            ? new StepResult(OperationStatus.Changed, changedMessage, null, candidate, outcome)
            : new StepResult(OperationStatus.NotApplicable, refusalMessage, MaxLevelStep.FirstFailure(outcome));
    }

    /// <summary>Amistad 255 con la del entrenador que lo tiene ahora (el Core elige OT o entrenador actual segun <c>CurrentHandler</c>). Si el Core no la valida NO se busca otro valor.</summary>
    public StepResult MaxFriendship(PKM cur, ChangeLog log)
    {
        if (cur.IsEgg) return new(OperationStatus.NotApplicable, "Un huevo no tiene amistad (ese valor es el contador de eclosión).");
        if (cur.Format == 1) return new(OperationStatus.NotApplicable, "La primera generación no guarda la amistad.");
        if (cur.CurrentFriendship == 255) return new(OperationStatus.AlreadyOptimal, "La amistad ya está al máximo.");
        var c = cur.Clone();
        c.CurrentFriendship = 255;
        c.RefreshChecksum();
        var r = Commit(c, "Amistad al máximo (255).", "PKHeX no admite la amistad máxima en este Pokémon; no se ha tocado.");
        if (r.Status == OperationStatus.Changed) log.Reason("Friendship", ReasonCodes.FriendshipMaxed, "Amistad al máximo (255).");
        return r;
    }

    /// <summary>Idioma español si el Core lo valida. Cambia el idioma de ORIGEN: un evento de idioma fijo lo rechaza y no se cambia (R-005 regla 3).</summary>
    public StepResult SpanishLanguage(PKM cur, ChangeLog log)
    {
        if (cur.Format <= 2 || dest.Generation <= 2) return new(OperationStatus.NotApplicable, "Esta generación no guarda el idioma del Pokémon.");
        if (!LanguagePolicy.Supports(dest, LanguagePolicy.Preferred)) return new(OperationStatus.NotApplicable, "Este juego no admite el español.");
        if (cur.Language == (int)LanguagePolicy.Preferred) return new(OperationStatus.AlreadyOptimal, "Ya está en español.");
        var c = cur.Clone();
        c.Language = (int)LanguagePolicy.Preferred;
        if (!cur.IsNicknamed) c.ClearNickname(); // sin mote: el nombre de la especie en español
        c.RefreshChecksum();
        var r = Commit(c, "Idioma cambiado a español.",
            "PKHeX no admite el español para el origen de este Pokémon (por ejemplo, un evento que solo existe en otro idioma); no se ha tocado.");
        if (r.Status == OperationStatus.Changed)
        {
            log.Reason("Language", ReasonCodes.LanguageSpanish, "Idioma cambiado a español.");
            log.Reason("Nickname", ReasonCodes.LanguageSpanish, "Idioma cambiado a español.");
        }
        return r;
    }

    /// <summary>Mas PP al maximo en los movimientos que lo admiten y PP restaurados (rutinas del Core de <c>CommonEdits.ApplySetDetails</c>).</summary>
    public StepResult MaxPP(PKM cur, ChangeLog log)
    {
        if (!Legal.IsPPUpAvailable(cur)) return new(OperationStatus.NotApplicable, "Este formato no tiene Más PP.");
        var c = cur.Clone();
        c.SetMaximumPPUps();
        c.RefreshChecksum();
        if (PpState(c) == PpState(cur)) return new(OperationStatus.AlreadyOptimal, "Los PP ya están al máximo.");
        var r = Commit(c, "Más PP al máximo y PP restaurados.", "PKHeX no admite ese cambio de PP en este Pokémon; no se ha tocado.");
        if (r.Status == OperationStatus.Changed)
        {
            log.Reason("PP", ReasonCodes.PpMaxed, "Más PP al máximo y PP restaurados.");
            log.Reason("PPUps", ReasonCodes.PpMaxed, "Más PP al máximo y PP restaurados.");
        }
        return r;
    }

    private static string PpState(PKM p) => $"{p.Move1_PPUps},{p.Move2_PPUps},{p.Move3_PPUps},{p.Move4_PPUps}|{p.Move1_PP},{p.Move2_PP},{p.Move3_PP},{p.Move4_PP}";

    /// <summary>PS al maximo y sin estado alterado, solo donde el formato de la casilla lo guarda (equipo; las generaciones de Game Boy y Let's Go usan el mismo tamano en caja y equipo).</summary>
    public StepResult HealParty(PKM cur, ChangeLog log)
    {
        if (cur.SIZE_PARTY != cur.SIZE_STORED && !partySize)
            return new(OperationStatus.NotApplicable, "Solo aplica a los Pokémon del equipo: en la caja este juego no guarda los PS ni el estado.");
        var c = cur.Clone();
        c.ResetPartyStats();
        c.RefreshChecksum();
        if (PartyState(c) == PartyState(cur)) return new(OperationStatus.AlreadyOptimal, "Ya tiene todos los PS y ningún estado alterado.");
        // Lo que no se guarda en la casilla no es un cambio: se compara lo que realmente se escribiria.
        if (EntityIo.Export(c, partySize).AsSpan().SequenceEqual(EntityIo.Export(cur, partySize)))
            return new(OperationStatus.NotApplicable, "Este formato de casilla no guarda los PS ni el estado alterado.");
        var r = Commit(c, "PS al máximo y sin estado alterado.", "PKHeX no admite ese cambio de PS o de estado en este Pokémon; no se ha tocado.");
        if (r.Status == OperationStatus.Changed)
        {
            log.Reason("HP", ReasonCodes.PartyHealed, "PS al máximo y sin estado alterado.");
            log.Reason("Status", ReasonCodes.PartyHealed, "PS al máximo y sin estado alterado.");
        }
        return r;
    }

    private static string PartyState(PKM p) => $"{p.Stat_HPCurrent},{p.Stat_HPMax},{p.Status_Condition},{p.Stat_ATK},{p.Stat_DEF},{p.Stat_SPA},{p.Stat_SPD},{p.Stat_SPE},{p.Stat_Level}";

    /// <summary>Cura el Pokerus SOLO si esta infectado (dias > 0): dias a 0 conservando la cepa. NUNCA vacuna (PKM.IsPokerusCured/Vaccinate pondrian una cepa a quien nunca la tuvo: inventaria historia).</summary>
    public StepResult CurePokerus(PKM cur, ChangeLog log)
    {
        if (cur.PokerusDays <= 0) return new(OperationStatus.NotApplicable, "No tiene el Pokérus activo: no hay nada que curar (y nunca se vacuna).");
        var c = cur.Clone();
        c.PokerusDays = 0; // la cepa (PokerusStrain) no se toca
        c.RefreshChecksum();
        var r = Commit(c, "Pokérus curado (se conserva la cepa).", "PKHeX no admite curar el Pokérus de este Pokémon; no se ha tocado.");
        if (r.Status == OperationStatus.Changed) log.Reason("Pokerus", ReasonCodes.PokerusCured, "Pokérus curado (se conserva la cepa).");
        return r;
    }
}
