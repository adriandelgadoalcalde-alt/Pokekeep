// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Engine.Context;
using PKHeX.Core;

namespace AutoLegalidad.Engine.Generation;

/// <summary>
/// 0.6.5 (ADR 0052): ¿cabe el nombre del entrenador de la partida como «entrenador original» (EO) de un Pokémon de una generación anterior? Un origen en otra generación (transferencia) lleva el nombre
/// con los límites de ESA generación: Gen 1-2 guardan 7 caracteres (5 en japonés y coreano) con un juego de caracteres reducido; Gen 3-5, 7; Gen 6 en adelante, 12. En vez de duplicar tablas, se
/// pregunta al Core: se escribe el nombre en un Pokémon en blanco del formato y se lee de vuelta; si no vuelve igual, no cabe o no se puede codificar. Nunca se inventa otro nombre.
/// </summary>
public static class TrainerNameFit
{
    /// <summary>Juego representativo de cada generación (solo para crear un guardado y un Pokémon en blanco del formato).</summary>
    private static GameVersion Representative(int generation) => generation switch
    {
        1 => GameVersion.YW,
        2 => GameVersion.C,
        3 => GameVersion.FR,
        4 => GameVersion.Pt,
        5 => GameVersion.B2,
        6 => GameVersion.X,
        7 => GameVersion.US,
        8 => GameVersion.SW,
        _ => GameVersion.SL,
    };

    /// <summary>Gen 1-2 solo distinguen japonés e internacional: el resto de idiomas se escribe con el juego de caracteres internacional.</summary>
    private static LanguageID Lang(int generation, LanguageID language) => generation <= 2 ? (language == LanguageID.Japanese ? LanguageID.Japanese : LanguageID.English) : language;

    /// <summary>True si <paramref name="name"/> se guarda sin cambios como entrenador original en un Pokémon de la generación dada y el idioma dado.</summary>
    public static bool Fits(string name, int generation, LanguageID language)
        => name.Length != 0 && Cache.GetOrAdd((name, generation, Lang(generation, language)), static k => Compute(k.Name, k.Generation, k.Language));

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(string Name, int Generation, LanguageID Language), bool> Cache = new();

    private static bool Compute(string name, int generation, LanguageID language)
    {
        try
        {
            var sav = BlankSaveFile.Get(Representative(Math.Clamp(generation, 1, 9)), TrainerName.ProgramINT, Lang(generation, language));
            var pk = sav.BlankPKM;
            if (generation <= 2) pk.Language = (int)Lang(generation, language);
            pk.OriginalTrainerName = name;
            // El formato guarda el nombre en un buzon de 11 caracteres aunque el juego solo admita 7 (5 en japones): el Core exige ademas no pasar de su maximo (TrainerNameVerifier).
            return name.Length <= pk.MaxStringLengthTrainer && string.Equals(pk.OriginalTrainerName, name, StringComparison.Ordinal);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return false; }
    }

    /// <summary>Longitud máxima del nombre del EO en la generación (la del formato en blanco), solo para explicar el motivo.</summary>
    public static int MaxLength(int generation, LanguageID language)
    {
        try
        {
            var sav = BlankSaveFile.Get(Representative(Math.Clamp(generation, 1, 9)), TrainerName.ProgramINT, Lang(generation, language));
            var pk = sav.BlankPKM;
            if (generation <= 2) pk.Language = (int)Lang(generation, language);
            return pk.MaxStringLengthTrainer;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return 0; }
    }

    /// <summary>Frase para «Ver cambios» cuando el nombre no cabe en esa generación.</summary>
    public static string Explain(string name, int generation, LanguageID language)
    {
        var max = MaxLength(generation, language);
        return max > 0 && name.Length > max
            ? $"El nombre de tu entrenador («{name}») no cabe en un Pokémon de la generación {generation} (máximo {max} caracteres): no se ha usado ese origen."
            : $"El nombre de tu entrenador («{name}») no se puede escribir con los caracteres de un Pokémon de la generación {generation}: no se ha usado ese origen.";
    }

    /// <summary>
    /// 0.6.6 (M1, decision 5 del propietario): el nombre del entrenador NUNCA se recorta, en ninguna pasada. True si <paramref name="candidate"/> lleva el TID (y el SID, salvo en Gen 1-3 y Consola
    /// Virtual, que no los guardan completos) del jugador pero un nombre de entrenador original DISTINTO del suyo (el Core recorta en silencio al escribir un nombre largo en Gen 1-5): el origen es
    /// el del jugador, no el de un encuentro que fije otro EO (<paramref name="ownerOrigin"/>), y el Pokemon del usuario no traia ya ese nombre (<paramref name="original"/>: identidad que se conserva).
    /// </summary>
    public static bool AltersPlayerName(PKM candidate, PKM original, Destination dest, bool ownerOrigin)
    {
        if (!ownerOrigin) return false;
        var player = dest.Trainer.OT;
        var name = candidate.OriginalTrainerName;
        // Solo un RECORTE real: el nombre del candidato es un prefijo estricto del nombre del jugador (el Core corta a la longitud del formato). Un nombre distinto que fija el propio encuentro (p. ej. el «????» del Bosque Entree) no es un recorte.
        if (player.Length == 0 || name.Length == 0 || name.Length >= player.Length || !player.StartsWith(name, StringComparison.Ordinal)) return false;
        if (string.Equals(name, original.OriginalTrainerName, StringComparison.Ordinal) && original.OriginalTrainerName.Length != 0) return false;   // identidad del usuario conservada
        if (candidate.TID16 != dest.Trainer.TID16) return false;
        var storesSid = candidate.Format > 3 && !(candidate.VC && candidate.Format >= 7);
        return !storesSid || candidate.SID16 == dest.Trainer.SID16;
    }

    /// <summary>Atajo con el destino: usa el nombre y el idioma de la partida.</summary>
    public static bool Fits(Destination dest, int generation) => Fits(dest.Trainer.OT, generation, (LanguageID)dest.Trainer.Language);
}
