// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Engine.Context;
using PKHeX.Core;

namespace AutoLegalidad.Engine.Generation;

/// <summary>
/// 0.6.5 (ADR 0052), «origen propio primero». Rango de un encuentro según quién figura como entrenador original (EO) y dónde nació. Menor = mejor.
/// </summary>
public enum OriginRank
{
    /// <summary>Capturado, criado o recibido en el MISMO juego (o su gemelo) y con EO = el entrenador de la partida.</summary>
    SameGame = 0,
    /// <summary>EO = entrenador de la partida, pero nació en otro juego o generación (transferencia necesaria): el jugador figura como entrenador original.</summary>
    OtherGame = 1,
    /// <summary>El encuentro fija el EO (intercambio con un personaje, regalo con entrenador): el EO no puede ser el jugador.</summary>
    FixedTrainer = 2,
    /// <summary>Tarjeta de evento que trae su propio entrenador original.</summary>
    Event = 3,
}

/// <summary>
/// Valoración del origen de un candidato: rango del encuentro, si el EO figurado ES el jugador, si queda un entrenador anterior (HT) que no es el jugador y desde qué juego/generación nació.
/// </summary>
public readonly record struct OriginAssessment(OriginRank Rank, bool ForeignHandler, bool OwnerMatches = true, GameVersion EncounterVersion = GameVersion.Any, byte EncounterGeneration = 0, bool HasHandler = false, bool Evolved = false)
{
    /// <summary>EO = jugador: el encuentro no fija el EO Y el Pokémon lleva de verdad el nombre, TID y SID de la partida.</summary>
    public bool OwnerIsPlayer => Rank <= OriginRank.OtherGame && OwnerMatches;

    /// <summary>Coste ordenable: primero el rango, después el entrenador anterior ajeno.</summary>
    public int Cost => ((int)(OwnerMatches ? Rank : (Rank <= OriginRank.OtherGame ? OriginRank.FixedTrainer : Rank)) << 1) | (ForeignHandler ? 1 : 0);

    /// <summary>Origen propio completo: mismo juego, EO = jugador y sin entrenador anterior ajeno.</summary>
    public bool Clean => Cost == 0;
}

/// <summary>
/// Interruptor de pruebas y mediciones (como <c>OptimizerSwitches</c>): desactivado, el motor se comporta como antes de la 0.6.5 (sin la pasada de «origen propio primero»). Pensado para medir
/// «antes y después» con el mismo binario y para las pruebas de la fase «Historia», que miden lo que hace sobre el resultado de ALM. Los productos nunca lo tocan.
/// </summary>
public static class OriginSwitches
{
    public static bool PreferOwnOrigin { get; set; } = true;
}

/// <summary>Reglas de preferencia de origen del motor. No decide nunca la legalidad: solo ordena candidatos y pistas que el Core ya ha de validar.</summary>
public static class OriginPreference
{
    private static readonly string[][] Twins =
    [
        ["SW", "SH"], ["X", "Y"], ["OR", "AS"], ["SN", "MN"], ["US", "UM"], ["B", "W"], ["B2", "W2"], ["D", "P", "Pt"], ["HG", "SS"],
        ["R", "S", "E"], ["FR", "LG"], ["GP", "GE"], ["BD", "SP"], ["SL", "VL"], ["GD", "SI"], ["RD", "GN", "BU"],
    ];

    /// <summary>¿Es <paramref name="encounter"/> el propio juego de destino, su gemelo o una etiqueta de grupo que lo contiene?</summary>
    public static bool SameGame(GameVersion encounter, GameVersion destination)
    {
        if (encounter == destination) return true;
        if (encounter.Contains(destination) || destination.Contains(encounter)) return true;
        var a = encounter.ToString();
        var b = destination.ToString();
        return Twins.Any(g => g.Contains(a, StringComparer.Ordinal) && g.Contains(b, StringComparer.Ordinal));
    }

    /// <summary>
    /// 0.6.6 (B6): ¿trae la tarjeta su propio entrenador original PARA ESTE IDIOMA? Las tarjetas modernas (WB7, WC8, WB8, WA8, WC9, WA9) guardan un nombre por idioma y el Core solo fija el EO si existe el del idioma
    /// del jugador (<c>HistoryVerifier</c>: <c>GetHasOT(pk.Language)</c>); <c>OriginalTrainerName</c> devuelve el del idioma por defecto de la tarjeta, que puede no ser el del jugador.
    /// </summary>
    private static bool GiftFixesTrainer(MysteryGift gift, int language) => gift switch
    {
        WB7 g => g.GetHasOT(language),
        WC8 g => g.GetHasOT(language),
        WB8 g => g.GetHasOT(language),
        WA8 g => g.GetHasOT(language),
        WC9 g => g.GetHasOT(language),
        WA9 g => g.GetHasOT(language),
        _ => gift.OriginalTrainerName.Length > 0,
    };

    /// <summary>Rango de un encuentro en el destino (ver <see cref="OriginRank"/>).</summary>
    public static OriginRank Rank(IEncounterTemplate enc, Destination dest)
    {
        if (enc is MysteryGift gift && GiftFixesTrainer(gift, dest.Trainer.Language)) return OriginRank.Event;
        if (enc is IFixedTrainer { IsFixedTrainer: true } or EncounterGift2 { Trainer: not EncounterGift2.TrainerType.Recipient } or EncounterGift1 { Trainer: not EncounterGift1.TrainerType.Recipient }) return OriginRank.FixedTrainer;
        if (enc.Version == GameVersion.GO) return OriginRank.OtherGame;
        return SameGame(enc.Version, dest.Version) && enc.Context == dest.Context ? OriginRank.SameGame : OriginRank.OtherGame;
    }

    /// <summary>
    /// ¿Figura el jugador como entrenador original? Nombre, TID y SID (sin mirar la version: en una transferencia el EO sigue siendo el jugador). 0.6.6 (M2, decision 3 del propietario): un Pokemon de
    /// CONSOLA VIRTUAL (Gen 1-2 llevada a Gen 7) solo guarda el TID: su SID es 0 por necesidad y el genero del EO tambien (el Core lo dice asi en <c>TrainerIDVerifier</c>: «Only TID is used for Gen 1/2 VC»);
    /// con el nombre y el TID del jugador su EO ES el jugador, es imposible que sea de otra forma.
    /// </summary>
    public static bool OwnedByPlayer(ITrainerInfo trainer, PKM pk)
    {
        if (pk.VC && pk.Format >= 7)
            return pk.TID16 == trainer.TID16 && string.Equals(pk.OriginalTrainerName, trainer.OT, StringComparison.Ordinal);
        return TrainerInfoExtensions.IsFromTrainerNoVersion(trainer, pk) || (pk.IsEgg && trainer.IsFromTrainer(pk));
    }

    /// <summary>Valora un candidato ya analizado: <paramref name="enc"/> es el encuentro que el Core le asigna.</summary>
    public static OriginAssessment Assess(PKM pk, IEncounterTemplate enc, Destination dest)
    {
        var rank = Rank(enc, dest);
        var hasHandler = pk.Format >= 6 && (pk.CurrentHandler == 1 || pk.HandlingTrainerName.Length != 0);
        var foreign = hasHandler && !string.Equals(pk.HandlingTrainerName, dest.Trainer.OT, StringComparison.OrdinalIgnoreCase);
        // El EO es el jugador cuando el nombre, el TID y el SID coinciden (sin mirar la version: en una transferencia el EO sigue siendo el jugador).
        var owner = OwnedByPlayer(dest.Trainer, pk);
        return new OriginAssessment(rank, foreign, owner, enc.Version, enc.Generation, hasHandler, enc.Species != pk.Species);
    }

    /// <summary>Nombre corto del motivo cuando el origen no es propio (para «Ver cambios»).</summary>
    public static string Reason(OriginRank rank) => rank switch
    {
        OriginRank.Event => "evento",
        OriginRank.FixedTrainer => "intercambio del juego",
        OriginRank.OtherGame => "transferencia",
        _ => string.Empty,
    };
}
