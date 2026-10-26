// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;

namespace AutoLegalidad.Engine.Changes;

/// <summary>
/// Hito 4 (propuesta 4 del Hito 2): cuando la relajacion automatica suelta un rasgo del usuario, el resumen lo dice con su razon
/// («se quito el variocolor porque este origen no lo permite»). Solo se explica lo que REALMENTE cambio entre el Pokemon del usuario y el
/// resultado; un rasgo relajado que el resultado conserva no se menciona. Los codigos son estables (uno por rasgo).
/// </summary>
public static class RelaxationExplainer
{
    public const string AbilityRelaxed = "ABILITY_RELAXED";
    public const string MovesRelaxed = "MOVES_RELAXED";
    public const string NatureRelaxed = "NATURE_RELAXED";
    public const string GenderRelaxed = "GENDER_RELAXED";
    public const string ShinyRelaxed = "SHINY_RELAXED";
    public const string FormRelaxed = "FORM_RELAXED";
    public const string DefaultsRelaxed = "DEFAULTS_RELAXED";

    /// <summary>Nivel de relajacion alcanzado por cada escalon de <see cref="Generation.Relaxation"/> (acumulativos).</summary>
    private static int Level(string stepCode) => stepCode switch
    {
        "NoAbility" => 1,
        "NoMoves" => 2,
        "NoNature" => 3,
        "NoGender" => 4,
        "NoShiny" => 5,
        "LevelFree" => 5,
        "BaseForm" => 6,
        "Defaults" => 7,
        _ => 0,
    };

    /// <summary>Anota en <paramref name="log"/> la razon de cada rasgo del usuario que se solto y que de verdad difiere en <paramref name="final"/>.</summary>
    public static void Explain(string stepCode, PKM original, PKM final, ChangeLog log)
    {
        var level = Level(stepCode);
        if (level == 0) return;

        void Mark(string[] fields, string code, string text)
        {
            foreach (var f in fields) log.Reason(f, code, text);
            log.Note(text);
        }

        if (level >= 1 && original.Ability != final.Ability)
            Mark(["Ability"], AbilityRelaxed, "Se cambió la habilidad porque este origen no permite la que tenía.");

        if (level >= 2 && MovesDiffer(original, final))
            Mark(["Move1", "Move2", "Move3", "Move4"], MovesRelaxed, "Se sustituyeron movimientos que este origen no permite.");

        if (level >= 3 && original.Nature != final.Nature)
            Mark(["Nature"], NatureRelaxed, "Se cambió la naturaleza porque este origen no permite la que tenía.");

        if (level >= 4 && original.Gender != final.Gender)
            Mark(["Gender"], GenderRelaxed, "Se cambió el sexo porque este origen no permite el que tenía.");

        if (level >= 5 && original.IsShiny && !final.IsShiny)
            Mark(["IsShiny"], ShinyRelaxed, "Se quitó el variocolor porque este origen no lo permite.");

        if (stepCode == "LevelFree" && original.CurrentLevel != final.CurrentLevel)
            Mark(["Level"], DefaultsRelaxed, "Se subió el nivel porque la forma pedida solo se obtiene a un nivel mayor (evolución).");

        if (level >= 6 && original.Form != final.Form)
            Mark(["Form"], FormRelaxed, "Se usó otra forma porque la que tenía no se puede obtener con este origen.");

        if (level >= 7 && (original.CurrentLevel != final.CurrentLevel || original.HeldItem != final.HeldItem || original.Nickname != final.Nickname))
            Mark(["Level", "HeldItem", "Nickname"], DefaultsRelaxed, "Se ajustaron el nivel, el objeto o el apodo a los valores que permite este origen.");
    }

    /// <summary>True si el escalon todavia exige la naturaleza del usuario (los anteriores a «NoNature»: mismo encuentro, exacto, sin habilidad y sin movimientos).</summary>
    public static bool KeepsNature(string stepCode) => Level(stepCode) < 3;

    /// <summary>
    /// Hito 4b, decision (a) «Respetar mi naturaleza»: explica un cambio de naturaleza que ningun escalon de relajacion explico (p. ej. el mismo encuentro la fija). Dice cual era, cual queda y
    /// por que: el origen la fija, o (si se busco en todos los escalones que la exigian) ningun origen legal hallado la conserva. Nombres de naturaleza en castellano (tabla de PKHeX).
    /// </summary>
    public static void ExplainNature(PKM original, PKM final, Context.Destination dest, bool searchedEveryOrigin, ChangeLog log)
    {
        var before = NatureName(original.Nature);
        var after = NatureName(final.Nature);
        string why;
        LegalityAnalysis? la = null;
        try { la = Optimization.StatOptimizer.Analyze(final, dest); } catch (Exception ex) when (ex is not OutOfMemoryException) { }
        if (la?.EncounterMatch is IFixedNature { IsFixedNature: true })
            why = $"este origen fija la naturaleza {after}";
        else if (searchedEveryOrigin)
            why = "ningún origen legal hallado para este Pokémon conserva la que tenía";
        else if (original.OriginalTrainerName.Length != 0 && original.OriginalTrainerName != dest.Trainer.OT)
            why = "se conserva su origen con el entrenador original (evento o intercambio), y ese origen no admite la que tenía";
        else
            why = "el origen legal hallado no admite la que tenía";
        var text = $"Se cambió la naturaleza ({before} → {after}): {why}.";
        log.Reason("Nature", NatureRelaxed, text);
        log.Note(text);
    }

    private static string NatureName(Nature n)
    {
        try
        {
            var names = GameInfo.GetStrings("es").Natures;
            return (int)n < names.Count ? names[(int)n] : n.ToString();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return n.ToString(); }
    }

    private static bool MovesDiffer(PKM a, PKM b)
    {
        Span<ushort> before = [a.Move1, a.Move2, a.Move3, a.Move4];
        Span<ushort> after = [b.Move1, b.Move2, b.Move3, b.Move4];
        foreach (var m in before)
        {
            if (m != 0 && !after.Contains(m))
                return true;
        }
        return false;
    }
}
