// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Globalization;
using AutoLegalidad.Contracts;
using PKHeX.Core;

namespace AutoLegalidad.Engine.Changes;

/// <summary>Codigos estables de razon de un cambio (plan 6: <c>changes[].reasonCode</c>).</summary>
public static class ReasonCodes
{
    public const string EvNormalized = "EV_NORMALIZED";
    public const string MoveReplaced = "MOVE_REPLACED";
    public const string RelearnFixed = "RELEARN_FIXED";
    public const string BallFixed = "BALL_FIXED";
    public const string LevelFixed = "LEVEL_FIXED";
    public const string FormFixed = "FORM_FIXED";
    public const string GenderFixed = "GENDER_FIXED";
    public const string ShinyRemoved = "SHINY_REMOVED";
    public const string EncounterRegenerated = "ENCOUNTER_REGENERATED";
    public const string FormatConverted = "FORMAT_CONVERTED";
    public const string IvOptimized = "IV_OPTIMIZED";
    public const string HyperTrained = "HYPER_TRAINED";
    public const string StatsRecalculated = "STATS_RECALCULATED";
    public const string Relaxed = "TRAIT_RELAXED";
    public const string EggNormalized = "EGG_NORMALIZED";
    public const string AbilityBitFixed = "ABILITY_BIT_FIXED";
    /// <summary>Hito 5 (Z-A): la habilidad de un PA9 evolucionado se conserva como la de la especie del ENCUENTRO (habilidad de nacimiento), que es la que el Core exige.</summary>
    public const string AbilityBirthKept = "ABILITY_BIRTH_KEPT";
    public const string MetDateCleared = "MET_DATE_CLEARED";
    /// <summary>R-005: el idioma del resultado no es el esperado (ESP al generar, o el del usuario) porque el origen no lo permite.</summary>
    public const string LanguageFixed = "LANGUAGE_FIXED";
    /// <summary>Hito 3b: el Core identifica el Pokemon con otro encuentro indistinguible (Gen1/2 sin metadatos, evento con los mismos datos...), pero se creo desde la plantilla pedida.</summary>
    public const string OriginEquivalent = "ORIGIN_EQUIVALENT";
    /// <summary>Hito 4b (R-006/R-007): el nivel se subio al mayor con el que el Core valida el Pokemon (<c>MaxLevelStep</c>).</summary>
    public const string LevelMaxed = "LEVEL_MAXED";
    /// <summary>Hito 4b (R-007): amistad al maximo (255) porque el Core lo valida.</summary>
    public const string FriendshipMaxed = "FRIENDSHIP_MAXED";
    /// <summary>Hito 4b (R-007): idioma cambiado a espanol a peticion del usuario (distinto de <see cref="LanguageFixed"/>, que lo decide el origen al legalizar).</summary>
    public const string LanguageSpanish = "LANGUAGE_SPANISH";
    /// <summary>Hito 4b (R-007): Mas PP al maximo y PP restaurados.</summary>
    public const string PpMaxed = "PP_MAXED";
    /// <summary>Hito 4b (R-007): PS al maximo y sin estado alterado.</summary>
    public const string PartyHealed = "PARTY_HEALED";
    /// <summary>Hito 4b (R-007): Pokerus curado (dias a 0, la cepa se conserva).</summary>
    public const string PokerusCured = "POKERUS_CURED";

    /// <summary>Hito 4c (R-010, fase «Historia»): cintas y marcas que el origen no otorga, quitadas.</summary>
    public const string HistoryRibbons = "HISTORY_RIBBONS";
    /// <summary>Hito 4c: fechas de captura/eclosion puestas dentro de la ventana real o de un rango plausible.</summary>
    public const string HistoryDates = "HISTORY_DATES";
    /// <summary>Hito 4c: memorias y entrenador anterior (HT) coherentes con el recorrido del Pokemon.</summary>
    public const string HistoryTrainer = "HISTORY_TRAINER";
    /// <summary>Hito 4c: Pokerus, concurso, marcas aplicadas, amistad, afecto y otros valores de historial a valores creibles.</summary>
    public const string HistoryMisc = "HISTORY_MISC";

    /// <summary>0.6.5 (ADR 0052): origen propio (capturado o criado en este juego con el entrenador de la partida).</summary>
    public const string OriginOwn = "ORIGIN_OWN";
    /// <summary>0.6.5: origen con otro entrenador (evento o intercambio del juego): no hay ruta propia.</summary>
    public const string OriginOtherTrainer = "ORIGIN_OTHER_TRAINER";
    /// <summary>0.6.5: transferido desde otro juego o generación; el entrenador de la partida figura como original.</summary>
    public const string OriginTransferred = "ORIGIN_TRANSFERRED";
    /// <summary>0.6.5: entrenador anterior (HT) necesario por una evolución por intercambio.</summary>
    public const string OriginHandler = "ORIGIN_HANDLER";
    /// <summary>0.6.5: se conserva el entrenador original ajeno que ya tenía el Pokémon del usuario.</summary>
    public const string OriginTrainerKept = "ORIGIN_TRAINER_KEPT";
    /// <summary>0.6.5: el nombre del entrenador no cabe en otra generación (límite de longitud o de caracteres).</summary>
    public const string OriginNameFit = "ORIGIN_NAME_FIT";

    public static bool IsOrigin(string code) => code is OriginOwn or OriginOtherTrainer or OriginTransferred or OriginHandler or OriginTrainerKept or OriginNameFit;

    public static bool IsHistory(string code) => code is HistoryRibbons or HistoryDates or HistoryTrainer or HistoryMisc;

    /// <summary>Codigos cuyo texto es una frase completa: el resumen la muestra tal cual, sin la lista de campos entre parentesis.</summary>
    public static bool IsSelfContained(string code) => code is LevelMaxed or FriendshipMaxed or LanguageSpanish or PpMaxed or PartyHealed or PokerusCured || IsHistory(code) || IsOrigin(code);
}

/// <summary>Razones registradas por cada paso del algoritmo, por campo. El cambio final se calcula comparando original y candidato.</summary>
public sealed class ChangeLog
{
    private readonly Dictionary<string, (string Code, string Text)> _reasons = new(StringComparer.Ordinal);
    private readonly List<string> _notes = [];

    /// <summary>Release: la reparacion borro bytes de basura (detras del final de apodo/entrenador).</summary>
    public bool TrashCleared { get; set; }
    /// <summary>Release: la reparacion cambio algo MAS que la basura (EV, movimientos, bola...).</summary>
    public bool OtherRepairs { get; set; }

    public string DefaultCode { get; set; } = ReasonCodes.EncounterRegenerated;
    public string DefaultText { get; set; } = "El origen del Pokémon se reconstruyó para que sea legal.";

    public void Reason(string field, string code, string text) => _reasons[field] = (code, text);

    /// <summary>True si ya hay una razon propia registrada para el campo.</summary>
    public bool HasReason(string field) => _reasons.ContainsKey(field);

    public void ReasonFor(IEnumerable<string> fields, string code, string text)
    {
        foreach (var f in fields) Reason(f, code, text);
    }

    /// <summary>Copia las razones y notas de otro registro (la accion Adjust une el de la legalizacion con el de cada operacion). No toca <see cref="DefaultCode"/>.</summary>
    public void MergeFrom(ChangeLog other)
    {
        foreach (var (field, reason) in other._reasons) _reasons[field] = reason;
        foreach (var n in other._notes)
            if (!_notes.Contains(n)) _notes.Add(n);
        foreach (var e in other._extra)
            if (!_extra.Contains(e)) _extra.Add(e);
    }

    public void Note(string text) => _notes.Add(text);

    private readonly List<ChangeEntry> _extra = [];

    /// <summary>
    /// Hito 4c: cambios de historia que no salen de comparar campos sueltos (cintas, memorias...): se anaden tal cual a la lista de cambios y su frase («Historia: se quitaron 14 cintas que su origen no otorga.»)
    /// aparece en el resumen.
    /// </summary>
    public void History(string code, string field, string before, string after, string sentence)
        => _extra.Add(new ChangeEntry { Field = field, Before = before, After = after, ReasonCode = code, Reason = "Historia: " + sentence });

    /// <summary>0.6.5 (ADR 0052): frase de «Ver cambios» sobre el ORIGEN (quién figura como entrenador original y por qué); <paramref name="after"/> es la etiqueta corta.</summary>
    public void Origin(string code, string after, string sentence)
        => _extra.Add(new ChangeEntry { Field = "Origen", Before = string.Empty, After = after, ReasonCode = code, Reason = sentence });

    public IReadOnlyList<ChangeEntry> Extra => _extra;
    public IReadOnlyList<string> Notes => _notes;

    public (string Code, string Text) For(string field) => _reasons.TryGetValue(field, out var r) ? r : (DefaultCode, DefaultText);

    public static readonly string[] IvFields = ["IV_HP", "IV_ATK", "IV_DEF", "IV_SPA", "IV_SPD", "IV_SPE"];
    public static readonly string[] EvFields = ["EV_HP", "EV_ATK", "EV_DEF", "EV_SPA", "EV_SPD", "EV_SPE"];
}

/// <summary>Compara la entidad original con el candidato y produce la lista de cambios (con razon) y el resumen en espanol.</summary>
public static class ChangeBuilder
{
    /// <summary>Gen 1-2 no tienen IV de 0-31 sino DV de 0-15: el texto de cara al usuario dice «DV» (Hito 5b).</summary>
    public static string StatWord(PKM pk) => pk.Format <= 2 ? "DV" : "IV";

    private static readonly string[] StatNames = ["PS", "Ataque", "Defensa", "At. Esp.", "Def. Esp.", "Velocidad"];

    public static IReadOnlyList<ChangeEntry> Build(PKM before, PKM after, ChangeLog log)
    {
        var list = new List<ChangeEntry>();
        void Add(string field, string label, object? b, object? a)
        {
            var sb = Convert.ToString(b, CultureInfo.InvariantCulture);
            var sa = Convert.ToString(a, CultureInfo.InvariantCulture);
            if (sb == sa) return;
            var (code, text) = log.For(field);
            list.Add(new ChangeEntry { Field = label, Before = sb, After = sa, ReasonCode = code, Reason = text });
        }

        Add("Species", "Especie", before.Species, after.Species);
        Add("Form", "Forma", before.Form, after.Form);
        Add("Nickname", "Apodo", before.Nickname, after.Nickname);
        Add("Nature", "Naturaleza", before.Nature, after.Nature);
        Add("Ability", "Habilidad", before.Ability, after.Ability);
        Add("Gender", "Sexo", before.Gender, after.Gender);
        Add("IsShiny", "Variocolor", before.IsShiny, after.IsShiny);
        Add("PID", "PID", before.PID, after.PID);
        Add("EncryptionConstant", "Constante de cifrado", before.EncryptionConstant, after.EncryptionConstant);
        // Orden VISUAL (PS, Ata, Def, AtE, DeE, Vel): PKM.GetIV/GetEV usan el orden interno del juego, no sirven para las etiquetas.
        var ivB = Engine.Generation.PkmFieldsAccess.IVs(before);
        var ivA = Generation.PkmFieldsAccess.IVs(after);
        var evB = Generation.PkmFieldsAccess.EVs(before);
        var evA = Generation.PkmFieldsAccess.EVs(after);
        for (var i = 0; i < 6; i++)
        {
            Add(ChangeLog.IvFields[i], StatWord(before) + " " + StatNames[i], ivB[i], ivA[i]);
            Add(ChangeLog.EvFields[i], "EV " + StatNames[i], evB[i], evA[i]);
        }
        Add("Move1", "Movimiento 1", before.Move1, after.Move1);
        Add("Move2", "Movimiento 2", before.Move2, after.Move2);
        Add("Move3", "Movimiento 3", before.Move3, after.Move3);
        Add("Move4", "Movimiento 4", before.Move4, after.Move4);
        Add("Relearn", "Movimientos recordables", string.Join(',', before.RelearnMoves.ToArray()), string.Join(',', after.RelearnMoves.ToArray()));
        Add("Ball", "Poke Ball", before.Ball, after.Ball);
        Add("Level", "Nivel", before.CurrentLevel, after.CurrentLevel);
        Add("OT", "Entrenador original", before.OriginalTrainerName, after.OriginalTrainerName);
        Add("TID16", "TID", before.TID16, after.TID16);
        Add("SID16", "SID", before.SID16, after.SID16);
        Add("OTGender", "Género del entrenador original", before.OriginalTrainerGender, after.OriginalTrainerGender);
        Add("Language", "Idioma", before.Language, after.Language);
        Add("Version", "Juego de origen", before.Version, after.Version);
        Add("MetLocation", "Lugar de captura", before.MetLocation, after.MetLocation);
        Add("MetLevel", "Nivel de captura", before.MetLevel, after.MetLevel);
        Add("MetDate", "Fecha de captura", before.MetDate, after.MetDate);
        Add("EggMetDate", "Fecha de eclosión del huevo", before.EggMetDate, after.EggMetDate);
        Add("EggLocation", "Lugar del huevo", before.EggLocation, after.EggLocation);
        Add("IsEgg", "Es huevo", before.IsEgg, after.IsEgg);
        Add("HeldItem", "Objeto", before.HeldItem, after.HeldItem);
        Add("Format", "Formato", before.GetType().Name, after.GetType().Name);
        // Hito 4b (R-007): estos campos solo se informan cuando una operacion de Adjust registro su razon (al legalizar o al optimizar no son un cambio que informar).
        void AddReasoned(string field, string label, object? b, object? a)
        {
            if (log.HasReason(field)) Add(field, label, b, a);
        }
        AddReasoned("Friendship", "Amistad", before.CurrentFriendship, after.CurrentFriendship);
        AddReasoned("PP", "PP actuales", string.Join(',', before.Move1_PP, before.Move2_PP, before.Move3_PP, before.Move4_PP), string.Join(',', after.Move1_PP, after.Move2_PP, after.Move3_PP, after.Move4_PP));
        AddReasoned("PPUps", "Más PP", string.Join(',', before.Move1_PPUps, before.Move2_PPUps, before.Move3_PPUps, before.Move4_PPUps), string.Join(',', after.Move1_PPUps, after.Move2_PPUps, after.Move3_PPUps, after.Move4_PPUps));
        AddReasoned("HP", "PS actuales", before.Stat_HPCurrent, after.Stat_HPCurrent);
        AddReasoned("Status", "Estado alterado", before.Status_Condition, after.Status_Condition);
        AddReasoned("Pokerus", "Días de Pokérus", before.PokerusDays, after.PokerusDays);
        if (before is IScaledSize sb && after is IScaledSize sa)
        {
            Add("HeightScalar", "Altura (escala)", sb.HeightScalar, sa.HeightScalar);
            Add("WeightScalar", "Peso (escala)", sb.WeightScalar, sa.WeightScalar);
        }
        if (before is IScaledSize3 s3b && after is IScaledSize3 s3a)
            Add("Scale", "Tamaño (escala)", s3b.Scale, s3a.Scale);
        list.AddRange(log.Extra);
        return list;
    }

    /// <summary>Resumen legible en espanol de los cambios (una linea por grupo).</summary>
    public static IReadOnlyList<string> Summarize(IReadOnlyList<ChangeEntry> changes, int[] ivBefore, int[] ivAfter, string stat = "IV")
    {
        var lines = new List<string>();
        var ivChanged = !ivBefore.AsSpan().SequenceEqual(ivAfter);
        var origin = changes.Where(c => c.ReasonCode == ReasonCodes.EncounterRegenerated && !c.Field.StartsWith("IV ", StringComparison.Ordinal) && !c.Field.StartsWith("DV ", StringComparison.Ordinal)).ToList();
        if (origin.Count > 0)
            lines.Add("Origen reconstruido: " + string.Join(", ", origin.Select(c => c.Field).Distinct()) + ".");
        foreach (var g in changes.Where(c => c.ReasonCode is not ReasonCodes.EncounterRegenerated and not ReasonCodes.IvOptimized).GroupBy(c => c.ReasonCode))
            lines.Add(ReasonCodes.IsSelfContained(g.Key) ? g.First().Reason : $"{g.First().Reason} ({string.Join(", ", g.Select(c => c.Field))}).");
        if (ivChanged)
            lines.Add($"{stat} optimizados: {string.Join('/', ivBefore)} -> {string.Join('/', ivAfter)}.");
        else
        {
            // Los IV quedan como los pedia el usuario (p. ej. 6x31) pero con un PID/semilla LEGAL: cambia el PID y la constante de cifrado aunque los IV no. No puede quedar un cambio sin decir.
            var seedFields = changes.Where(c => c.ReasonCode == ReasonCodes.IvOptimized && !c.Field.StartsWith("IV ", StringComparison.Ordinal) && !c.Field.StartsWith("DV ", StringComparison.Ordinal)).Select(c => c.Field).ToList();
            if (seedFields.Count > 0)
                lines.Add($"{stat} conservados ({string.Join('/', ivAfter)}) con una semilla legal del encuentro: cambian {string.Join(", ", seedFields)}.");
        }
        return lines;
    }
}
