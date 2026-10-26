// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Engine.Context;
using AutoLegalidad.Engine.Infra;
using PKHeX.Core;

namespace AutoLegalidad.Engine.Generation;

/// <summary>
/// Hito 3b: el intercambio de Gen1 (<c>EncounterTrade1</c>) tiene un mote fijo POR IDIOMA (tabla <c>Resources/legality/gen1/text_traderby_*.txt</c>) y el Core exige que el Pokemon lo lleve igual
/// (<c>EncounterTrade1.IsNicknameValid</c>, EncounterTrade1.cs:56-68: en una partida japonesa el mote debe ser EXACTAMENTE el de la tabla japonesa). Hay intercambios que solo existen en uno de los dos
/// grupos de partida (Nidoran hembra, indice 01, solo internacional: la linea 1 de <c>text_traderby_ja.txt</c> esta vacia; Nidoran macho, indice 17, solo japones: vacia en el resto). Un mote vacio es
/// invalido SIEMPRE (<c>NicknameVerifier.cs:23-27</c>, <c>NickLengthShort</c>), y una partida japonesa/internacional no admite una entidad del otro grupo (<c>SAV1.BlankPKM = new(Japanese)</c>,
/// SAV1.cs:225; <c>SIZE_STORED</c> segun <c>Japanese</c>, SAV1.cs:219). Es un dominio finito y estatico: el intercambio no existe en el idioma del destino, es una demostracion y no un muestreo.
/// Se construye la entidad con la propia plantilla del Core para el entrenador del destino y se comprueba que el mote que el Core le da es vacio.
/// </summary>
public static class Gen1TradeNicknameProof
{
    public static bool TryDisprove(IEncounterable? enc, Destination dest, out string evidence)
    {
        evidence = string.Empty;
        if (enc is not EncounterTrade1 trade || trade is not IEncounterConvertible conv || dest.Sav is not SAV1 sav) return false;
        PKM? pk;
        lock (EngineGlobalState.Gate)
        {
            dest.ApplyParseSettings();
            if (!GuardedCore.TryCall(() => conv.ConvertToPKM(dest.Trainer, EncounterCriteria.Unrestricted), TimeSpan.FromSeconds(3), out pk) || pk is null) return false;
        }
        if (pk.Nickname.Length != 0) return false;
        evidence = $"El intercambio de Gen1 de la especie {trade.Species} no existe en una partida {(sav.Japanese ? "japonesa" : "internacional")}: la tabla de motes de PKHeX para ese idioma está vacía, "
            + "así que la plantilla sale con el mote vacío, que siempre es inválido, "
            + "y en esa partida el mote tiene que coincidir exactamente con la tabla. No se puede usar una entidad del otro grupo en esa partida. "
            + "Ese intercambio solo existe en las partidas " + (sav.Japanese ? "internacionales" : "japonesas") + ". "
            + $"Referencia técnica: Resources/legality/gen1/text_traderby_{(sav.Japanese ? "ja" : "en")}.txt, NickLengthShort (NicknameVerifier.cs:23-27), EncounterTrade1.cs:56-68, SAV1.cs:219 y 225.";
        return true;
    }
}
