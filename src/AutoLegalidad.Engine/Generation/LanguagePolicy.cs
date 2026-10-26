// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Catalog.Core;
using AutoLegalidad.Engine.Catalog;
using AutoLegalidad.Engine.Context;
using AutoLegalidad.Engine.Infra;
using PKHeX.Core;

namespace AutoLegalidad.Engine.Generation;

/// <summary>
/// R-005 (ADR 0020): idioma por defecto ESPANOL DE ESPANA para todo lo que el motor GENERA. El Core crea el Pokemon con el idioma del entrenador que recibe
/// (<c>ConvertToPKM(ITrainerInfo)</c>: p. ej. WC7.cs:366, PGF.cs:210), asi que para generar en espanol se le entrega un destino derivado cuyo entrenador tiene
/// <see cref="LanguageID.Spanish"/>; la VALIDACION sigue haciendose con el destino real y el Core decide siempre (nunca se acepta un idioma por el generador).
/// Orden de preferencia de idiomas: (1) el idioma FIJO del encuentro si lo tiene (tarjeta de evento exclusiva de un idioma: regla 3 de R-005); (2) si el Pokemon es del
/// usuario, el suyo (regla 4: se conserva si es legal); (3) espanol; (4) el del entrenador del destino (comportamiento anterior, ultimo recurso).
/// </summary>
public static class LanguagePolicy
{
    public const LanguageID Preferred = LanguageID.Spanish;

    /// <summary>
    /// Valor de <c>OriginEventKey</c> para «Anadir a la caja» de una CRIA de la Libreria (las entradas de cria no llevan clave de encuentro: el origen lo da el Core). Significa
    /// «esto se GENERA, no es un Pokemon del usuario»: el idioma por defecto es ESP. Sin el, una solicitud sin clave es un Pokemon del usuario y conserva su idioma (regla 4).
    /// </summary>
    public const string LibraryEggKey = AutoLegalidad.Contracts.Protocol.LibraryEggKey;

    /// <summary>Codigo de razon estable: el idioma del resultado no es el que se pedia/tenia porque el origen no lo permite.</summary>
    public const string LanguageFixed = Changes.ReasonCodes.LanguageFixed;

    /// <summary>Idiomas de juego que admite el formato del destino segun el Core (Language.cs:40-49; Gen1/2 los reparte via GBPKM.Language, GBPKM.cs:75-104).</summary>
    public static bool Supports(Destination dest, LanguageID language)
        => language != 0 && PKHeX.Core.Language.GetAvailableGameLanguages(dest.Context).Contains((byte)language);

    /// <summary>
    /// Idioma FIJO del encuentro: el de una tarjeta de evento que solo existe en un idioma (<c>Language</c> o <c>RestrictLanguage</c>: WC6/WC7/PGF; el idioma del Pokemon
    /// de la tarjeta en PCD). null = el encuentro no fija idioma (lo reparte el entrenador que recibe).
    /// </summary>
    public static LanguageID? FixedLanguageOf(IEncounterable? enc)
    {
        if (enc is not DataMysteryGift g) return null;
        var (language, restrict) = GiftInfo.Languages(g);
        var id = language != 0 ? language : restrict;
        return id != 0 && Enum.IsDefined((LanguageID)id) ? (LanguageID)id : null;
    }

    /// <summary>Idiomas con los que generar, en orden de preferencia y sin repetir. Nunca vacio.</summary>
    /// <param name="userOwned">True si el Pokemon es del usuario (Legalizar de una entidad suya); false si se genera desde la Libreria (origen fijado).</param>
    public static IReadOnlyList<LanguageID> Candidates(PKM original, bool userOwned, IEncounterable? encounter, Destination dest, LanguageID? requested = null)
    {
        var list = new List<LanguageID>(3);
        void Add(LanguageID l)
        {
            if (Supports(dest, l) && !list.Contains(l)) list.Add(l);
        }
        // Gen1/2: ser japonesa o internacional es propiedad de la PARTIDA (SAV1.cs:18; EncounterGift1.cs:210) y ES/EN no se distinguen en el formato (GBPKM.cs:75-104): no se cambia de idioma.
        if (dest.Generation <= 2) return [(LanguageID)dest.Info.Trainer.Language];
        var fixedLanguage = FixedLanguageOf(encounter);
        if (fixedLanguage is { } f)
        {
            // Evento exclusivo de un idioma (regla 3): conserva su idioma, no se fuerza el espanol.
            Add(f);
            if (list.Count == 0) list.Add(f); // el Core manda: el formato ni lo lista pero la tarjeta lo trae
            return list;
        }
        if (requested is { } req) Add(req);   // ADR 0046: idioma pedido (evento exclusivo de un país con idioma libre en el Core), antes que el español
        if (userOwned) Add((LanguageID)original.Language);
        Add(Preferred);
        Add((LanguageID)dest.Info.Trainer.Language);
        if (list.Count == 0) list.Add((LanguageID)dest.Info.Trainer.Language);
        return list;
    }

    /// <summary>
    /// ADR 0045 (regla 4 de R-005): una ruta ALTERNATIVA a un Pokemon del usuario (<c>BestRoute</c>, mas IV con otro origen) no puede cambiar el idioma que el resultado actual ya conserva
    /// (p. ej. una tarjeta de evento en espanol para un Pokemon ingles) y, al GENERAR (<c>NewPokemon</c>), tampoco el espanol. <paramref name="wanted"/> es el idioma que se pide (el del usuario o ESP).
    /// Si el resultado actual ya habia perdido ese idioma, cualquier alternativa vale. Gen1/2 no guardan el idioma.
    /// </summary>
    public static bool AlternativeKeepsLanguage(PKM current, PKM alternative, int wanted, Destination dest)
        => dest.Generation <= 2 || current.Language != wanted || alternative.Language == wanted;

    /// <summary>Destino derivado cuyo entrenador tiene el idioma pedido (mismo juego, OT, TID/SID y genero). Si ya es ese idioma, el propio destino.</summary>
    public static Destination ForLanguage(Destination dest, LanguageID language)
    {
        if (dest.Info.Trainer.Language == (int)language) return dest;
        var info = dest.Info with { Trainer = dest.Info.Trainer with { Language = (int)language } };
        if (!Destination.TryCreate(info, out var derived, out _) || derived is null) return dest;
        derived.TrackerSeverity = dest.TrackerSeverity; // R-009: el ajuste de la solicitud acompana al destino derivado
        return derived;
    }

    /// <summary>Texto en espanol de la razon cuando el idioma del resultado no es el esperado (para la lista de cambios y las notas).</summary>
    public static string ReasonText(PKM final, PKM original, bool userOwned, IEncounterable? encounter, Destination? dest = null)
    {
        var name = Name((LanguageID)final.Language);
        if (final.Language == (int)Preferred)
            return "Se cambió el idioma a español (ESP) porque el idioma que tenía no es legal para este origen.";
        if (FixedLanguageOf(encounter) is not null)
            return $"El idioma es {name} porque este evento solo existe en ese idioma (no se fuerza el español).";
        if (dest is not null && encounter is not null && OriginLanguageFor(encounter, dest) is { } given && given != Preferred)
            return $"El idioma es {name} porque este origen fija el idioma del Pokémon recibido (PKHeX lo reparte así) y no se fuerza el español.";
        if (userOwned && final.Language == original.Language)
            return $"Se conserva el idioma {name} del Pokémon.";
        return $"El idioma es {name} porque el español no es legal para este origen en este juego.";
    }

    /// <summary>
    /// Idioma que el Core da al Pokemon de ese encuentro cuando el entrenador que recibe es ESPANOL (null si no se puede crear). Si no es espanol, el propio origen fija el idioma
    /// (p. ej. los intercambios de Gen4 reparten el idioma ID aleman/ingles/japones del juego: EncounterTrade4PID.cs:124-141) y no hay nada que forzar.
    /// </summary>
    public static LanguageID? OriginLanguageFor(IEncounterable enc, Destination dest)
    {
        if (enc is not IEncounterConvertible conv) return null;
        var spanish = ForLanguage(dest, Preferred);
        PKM? raw = null;
        lock (EngineGlobalState.Gate)
        {
            spanish.ApplyParseSettings();
            GuardedCore.TryCall(() => conv.ConvertToPKM(spanish.Trainer, EncounterCriteria.Unrestricted), TimeSpan.FromSeconds(3), out raw);
        }
        return raw is null || raw.Language == 0 || !Enum.IsDefined((LanguageID)raw.Language) ? null : (LanguageID)raw.Language;
    }

    public static string Name(LanguageID l) => l switch
    {
        LanguageID.Japanese => "japonés",
        LanguageID.English => "inglés",
        LanguageID.French => "francés",
        LanguageID.Italian => "italiano",
        LanguageID.German => "alemán",
        LanguageID.Spanish => "español",
        LanguageID.Korean => "coreano",
        LanguageID.ChineseS => "chino simplificado",
        LanguageID.ChineseT => "chino tradicional",
        LanguageID.SpanishL => "español latinoamericano",
        _ => ((int)l).ToString(System.Globalization.CultureInfo.InvariantCulture),
    };

    /// <summary>
    /// Regla 2 de R-005: si el evento tiene una version ESP (mismo evento, otra tarjeta por idioma), devuelve ESA tarjeta. Â«Mismo eventoÂ» = mismo tipo de tarjeta, misma
    /// especie y forma y mismo <c>CardID</c> del Core (comprobado en el catalogo: los grupos PGF/PCD/WC7 con varias lenguas comparten <c>event_card_id</c>; Meloetta
    /// Gen5: 5 idiomas, un mismo id). Solo se devuelve una tarjeta aprobada para generar (<c>v_encounter_generable</c>, regla 8). null = no hay version ESP distinta de la dada
    /// (la tarjeta ya es ESP, no fija idioma, o el evento no existe en espanol): se queda con la que habia.
    /// </summary>
    public static IEncounterable? SpanishSibling(IEncounterable enc, PKM wish, Destination dest, Engine.Catalog.CatalogReader catalog)
    {
        if (enc is not DataMysteryGift g) return null;
        var own = FixedLanguageOf(g);
        if (own is null || own == Preferred) return null;
        var blank = dest.Sav.BlankPKM;
        blank.Species = wish.Species;
        blank.Form = wish.Form;
        blank.Gender = blank.GetSaneGender();
        DataMysteryGift? best = null;
        lock (EngineGlobalState.Gate)
        {
            dest.ApplyParseSettings();
            foreach (var cand in CoreVersions.Encounters(blank, dest.Trainer, ReadOnlyMemory<ushort>.Empty))
            {
                if (cand is not DataMysteryGift c || c.GetType() != g.GetType() || c.Species != g.Species || c.Form != g.Form || c.CardID != g.CardID) continue;
                if (FixedLanguageOf(c) != Preferred) continue;
                var key = EncounterKeys.For(c);
                if (key is null || !catalog.GenerableKeys.Contains(key)) continue;
                if (best is null || (c.Version == g.Version && best.Version != g.Version)) best = c; // misma version del juego primero
            }
        }
        return best;
    }
}
