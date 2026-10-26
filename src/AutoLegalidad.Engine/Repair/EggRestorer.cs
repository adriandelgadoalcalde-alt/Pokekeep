// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Engine.Context;
using AutoLegalidad.Engine.Infra;
using PKHeX.Core;

namespace AutoLegalidad.Engine.Repair;

/// <summary>
/// Un huevo debe seguir siendo un huevo (invariante D18 del ADR 0010), pero los generadores (el del Core y ALM) devuelven el huevo YA ECLOSIONADO
/// (<c>ForceHatch</c>). Aqui se deshace la eclosion: se prueban las combinaciones de los ajustes que distinguen un huevo de un Pokemon eclosionado en cada formato
/// (apodo, lugar y nivel de captura, fecha, amistad, nivel, recuerdos del OT y obediencia) y SE DEJA QUE EL CORE DECIDA cual es legal: nada se asume por generacion.
/// Si ninguna variante es legal, no hay huevo (el motor no lo presenta como exito).
/// </summary>
public static class EggRestorer
{
    /// <summary>Primera variante de huevo que el analisis oficial da por valida con la tabla personal del destino; <c>null</c> si ninguna lo es.</summary>
    public static PKM? TryRestore(PKM hatched, Destination dest)
    {
        foreach (var variant in Variants(hatched))
        {
            LegalityAnalysis la;
            lock (EngineGlobalState.Gate)
            {
                dest.ApplyParseSettings();
                la = new LegalityAnalysis(variant, dest.Personal, dest.SlotType);
            }
            if (la.Valid && la.Parsed && variant.GetType() == dest.EntityType)
                return variant;
        }
        return null;
    }

    public static IEnumerable<PKM> Variants(PKM hatched)
    {
        var lang = hatched is GBPKM gb ? gb.GuessedLanguage() : hatched.Language;
        foreach (var nick in new[] { false, true })
        foreach (var metLevel in new[] { 0, 1 })
        foreach (var clearMeta in new[] { true, false })
        foreach (var friend in new[] { 0, 1 })
        foreach (var level in new byte[] { 1, 5 })
        foreach (var clearDate in new[] { true, false })
        foreach (var clearMemory in new[] { true, false })
        foreach (var obey in new[] { 0, 1 })
        {
            var p = hatched.Clone();
            p.IsEgg = true;
            p.IsNicknamed = nick;
            p.Nickname = SpeciesName.GetEggName(lang, hatched.Generation);
            if (clearMeta && p.Format >= 4) p.MetLocation = 0;
            if (p.Format >= 4) p.MetLevel = (byte)metLevel;
            if (clearDate && p.Format >= 4) p.MetDate = null;
            p.OriginalTrainerFriendship = friend == 0 ? p.PersonalInfo.HatchCycles : (byte)1;
            p.CurrentLevel = level;
            if (clearMemory && p is IMemoryOT mo) { mo.OriginalTrainerMemory = 0; mo.OriginalTrainerMemoryIntensity = 0; mo.OriginalTrainerMemoryFeeling = 0; mo.OriginalTrainerMemoryVariable = 0; }
            if (p is IObedienceLevel ob) ob.ObedienceLevel = (byte)(obey == 0 ? 0 : 1);
            p.RefreshChecksum();
            yield return p;
        }
    }
}
