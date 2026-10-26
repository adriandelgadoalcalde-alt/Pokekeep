// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Security.Cryptography;
using AutoLegalidad.Contracts;
using PKHeX.Core;

namespace AutoLegalidad.Ui.Operation;

/// <summary>Bytes canónicos de una entidad (mismo criterio que el motor: descifrados, tamaño de caja o de equipo) y su huella.</summary>
public static class EntityBytes
{
    public static bool UsesPartySize(PKM pk, StorageSlotType slot) => pk.SIZE_PARTY != pk.SIZE_STORED && slot is StorageSlotType.Party or StorageSlotType.BattleBox;

    public static byte[] Export(PKM pk, bool partySize)
    {
        var buffer = new byte[partySize ? pk.SIZE_PARTY : pk.SIZE_STORED];
        if (partySize)
            pk.WriteDecryptedDataParty(buffer);
        else
            pk.WriteDecryptedDataStored(buffer);
        return buffer;
    }

    public static string Sha256(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    /// <summary>Lee una entidad de sus bytes. El Core descifra algunos formatos in situ: siempre se le pasa una COPIA.</summary>
    public static PKM? Parse(ReadOnlySpan<byte> bytes, EntityContext prefer) => EntityFormat.GetFromBytes(bytes.ToArray(), prefer);
}

/// <summary>Construye el destino y la solicitud del contrato a partir de un guardado y una entidad. Solo viajan datos mínimos del entrenador (nunca rutas ni bytes del guardado).</summary>
public static class RequestBuilder
{
    /// <summary>0.6.6 (M6): ¿tiene la partida nombre de entrenador? Sin él no se opera: el Core da por inválido todo Pokémon cuyo EO es el jugador (<c>TrainerNameVerifier</c>: OTShort) y «PKHeX» sale «sospechoso» (OTSuspicious); Pokekeep nunca inventa un nombre.</summary>
    public static bool HasTrainerName(SaveFile sav) => !string.IsNullOrWhiteSpace(sav.OT);

    /// <summary>Indica si el guardado es un destino que Pokekeep puede usar (juego concreto reconocido y no el guardado de relleno de PKHeX).</summary>
    public static bool IsKnownTarget(SaveFile sav) => sav is not FakeSaveFile && ResolveVersion(sav) != 0;

    /// <summary>
    /// Juego concreto del destino. Algunos guardados solo conocen un GRUPO de juegos (Diamante/Perla, Negro/Blanco, X/Y...: <c>sav.Version</c> es un agregado que no es un
    /// juego guardado válido); entonces se usa el primer juego del grupo. El destino virtual del motor solo depende de la tabla personal, el formato y el entrenador, que son los
    /// mismos para todo el grupo. Devuelve 0 si no hay ningún juego concreto.
    /// </summary>
    public static GameVersion ResolveVersion(SaveFile sav)
    {
        var v = sav.Version;
        if (v.IsValidSavedVersion())
            return v;
        foreach (var candidate in GameUtil.GameVersions.OrderBy(static g => (int)g))
        {
            if (v.Contains(candidate) && candidate.IsValidSavedVersion() && candidate.Context == sav.Context)
                return candidate;
        }
        return 0;
    }

    /// <summary>
    /// Juego que viaja en el contrato como destino. Igual que <see cref="ResolveVersion"/> salvo Colosseum: su guardado (<c>SAV3Colosseum</c>, <c>Version = COLO</c>) comparte el grupo CXD con XD,
    /// pero su entidad es CK3 y no XK3; el destino virtual del motor se construye con <c>COLO</c> (<c>Destination.Blank</c>). Con CXD saldría un destino XD y el Worker lo rechazaría
    /// («guarda XK3, pero la solicitud dice CK3»). La Librería y «Completar el juego» siguen usando el grupo CXD (<see cref="ResolveVersion"/>).
    /// </summary>
    public static GameVersion TargetVersion(SaveFile sav) => sav is SAV3Colosseum ? GameVersion.COLO : ResolveVersion(sav);

    public static TargetInfo TargetFor(SaveFile sav, StorageSlotType slotType)
    {
        var flags = new Dictionary<string, string>
        {
            ["CartEraGB"] = (sav switch { SAV1 { IsVirtualConsole: true } => false, SAV2 { IsVirtualConsole: true } => false, { Generation: 1 or 2 } => true, _ => false }).ToString(),
        };
        var isVirtual3 = sav is SAV3 { IsVirtualConsole: true };
        flags["SwitchEraGBA"] = isVirtual3.ToString();
        flags["CartEraGBA"] = (!isVirtual3).ToString();

        return new TargetInfo
        {
            GameVersion = TargetVersion(sav).ToString(),
            SaveRuntimeType = sav.PKMType.Name,
            Context = sav.Context.ToString(),
            SlotType = slotType.ToString(),
            IsRealSave = sav.State.Exportable,
            Trainer = new TrainerInfo
            {
                Ot = sav.OT,   // 0.6.6 (M6): nunca un nombre fijo; sin nombre ni se llega aqui (HasTrainerName)
                Tid16 = sav.TID16,
                Sid16 = sav.SID16,
                Language = LanguageOf(sav),
                Gender = sav.Gender & 1,
            },
            SaveFlags = flags,
        };
    }

    /// <summary>Idioma del entrenador: el del guardado si es válido; en los guardados de Game Boy (sin idioma) japonés o inglés; si no, español.</summary>
    public static int LanguageOf(SaveFile sav)
    {
        var lang = sav.Language;
        if (lang is >= 1 and <= 12)
            return lang;
        if (sav is ILangDeviantSave { Japanese: true })
            return (int)LanguageID.Japanese;
        return sav.Generation <= 2 ? (int)LanguageID.English : (int)LanguageID.Spanish;
    }

    public static LegalizeRequest Create(WorkerManifest manifest, PKM entity, bool partySize, TargetInfo target, LegalizeAction action, DateTimeOffset deadline,
        SelectionInfo? selection, bool optimizeStats, string? originEventKey = null, bool allowHomeTrackerless = false, bool maxLevel = false, bool newPokemon = false, AdjustOptions? adjust = null, int optimizeBudgetSeconds = 0, bool coherentHistory = true, int requestedLanguage = 0)
    {
        var bytes = EntityBytes.Export(entity, partySize);
        return new LegalizeRequest
        {
            ProtocolVersion = Protocol.Version,
            RequestId = Guid.NewGuid().ToString(),
            ReleaseId = manifest.ReleaseId,
            Action = action,
            OriginalEntityBase64 = Convert.ToBase64String(bytes),
            OriginalEntitySha256 = EntityBytes.Sha256(bytes),
            EntityContext = entity.Context.ToString(),
            EntityRuntimeType = entity.GetType().Name,
            Target = target,
            Selection = selection,
            PolicyVersion = manifest.PolicyVersion,
            DatasetSnapshotId = manifest.DatasetSnapshotId ?? string.Empty,
            DeadlineUtc = deadline,
            OriginEventKey = originEventKey,
            Options = new EngineOptions { OptimizeStats = optimizeStats, IncludeReport = true, AllowHomeTrackerless = allowHomeTrackerless, MaxLevel = maxLevel, NewPokemon = newPokemon, Adjust = adjust, OptimizeBudgetSeconds = optimizeBudgetSeconds, CoherentHistory = coherentHistory, RequestedLanguage = requestedLanguage },
        };
    }

    /// <summary>
    /// Plantilla en blanco de una especie/forma para «Añadir a la caja» (la misma que usan las pruebas del motor): el entrenador de la partida es el receptor y
    /// el encuentro lo fija <c>originEventKey</c> del catálogo.
    /// </summary>
    public static PKM BlankFor(SaveFile sav, ushort species, byte form)
    {
        var pk = sav.BlankPKM;
        pk.Species = species;
        pk.Form = form;
        pk.Version = ResolveVersion(sav);
        pk.CurrentLevel = 20;
        pk.Gender = pk.GetSaneGender();
        pk.OriginalTrainerName = sav.OT;
        pk.ID32 = sav.ID32;
        pk.Language = LanguageOf(sav);
        return pk;
    }
}
