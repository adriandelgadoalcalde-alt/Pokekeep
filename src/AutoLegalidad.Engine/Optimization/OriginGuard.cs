// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Engine.Validation;
using PKHeX.Core;

namespace AutoLegalidad.Engine.Optimization;

/// <summary>
/// Guarda de ORIGEN de la accion B (R-002): compara por reflexion TODAS las propiedades publicas legibles del Pokemon antes y despues y solo deja pasar
/// las que forman parte de los stats (IV/DV, PID y constante de cifrado ligados a la semilla, Entrenamiento extremo, stats derivados). Cualquier otro
/// cambio (entrenador, juego de origen, lugar/fecha de captura, Ball, memorias, cintas, apodo, movimientos...) descarta la mejora.
/// </summary>
public static class OriginGuard
{
    /// <summary>Propiedades que pueden cambiar al optimizar stats. Todo lo demas se considera origen y debe quedar identico.</summary>
    public static IReadOnlySet<string> AllowedToChange { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        // IV / DV y lo que se deriva de ellos
        "IV_HP", "IV_ATK", "IV_DEF", "IV_SPA", "IV_SPD", "IV_SPE", "IV_SPC", "IV32", "DV16", "HPType", "HPPower", "Characteristic",
        "FlawlessIVCount", "IVTotal", "MaximumIV", "PotentialRating", "FileName", "FileNameWithoutExtension",
        "AbilityNumber", "PIDAbility",
        // Correlacion PID/IV (Gen3/4/GameCube): la semilla fija ambos. Naturaleza, sexo, variocolor y habilidad NO se permiten (se conservan).
        "PID", "EncryptionConstant", "PSV", "ShinyXor",
        // Entrenamiento extremo (Gen7+)
        "HT_HP", "HT_ATK", "HT_DEF", "HT_SPA", "HT_SPD", "HT_SPE", "HyperTrainFlags",
        // Stats calculados y derivados
        "Stat_HPMax", "Stat_HPCurrent", "Stat_ATK", "Stat_DEF", "Stat_SPE", "Stat_SPA", "Stat_SPD", "Stat_Level", "StatHPCurrent",
        "CP", "CalcCP",
        // Let's Go: la PC guardada y la PC base se calculan con los IV y el Entrenamiento extremo (PB7.cs:574-598, 631); sin recalcularlas el Core invalida el Pokemon (StatOptimizer.RefreshDerivedFromIvs).
        "Stat_CP", "BaseCP",
    };

    /// <summary>Ademas, con semillas modernas (Gen8+) la semilla fija el tamano del Pokemon: cambia con ella.</summary>
    public static IReadOnlySet<string> SeedDerivedExtra { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "HeightScalar", "WeightScalar", "Scale", "Height", "Weight", "WeightAbsolute", "HeightAbsolute",
    };

    public static IReadOnlyList<string> UnexpectedChanges(PKM before, PKM after, IReadOnlySet<string>? extraAllowed = null)
    {
        if (ReferenceEquals(before, after)) return [];
        var diff = PkmFields.ReflectDiff(before, after);
        // Gen1 no tiene sexo: el valor que expone el Core se deriva de los DV y no es un rasgo real del Pokemon.
        var gen1 = before.Format == 1;
        return [.. diff.Keys.Where(k => !AllowedToChange.Contains(k) && !(gen1 && k == "Gender") && (extraAllowed is null || !extraAllowed.Contains(k)))];
    }
}
