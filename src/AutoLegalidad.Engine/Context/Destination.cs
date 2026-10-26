// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Contracts;
using AutoLegalidad.Engine.Infra;
using PKHeX.Core;

namespace AutoLegalidad.Engine.Context;

/// <summary>
/// Destino VIRTUAL de una operacion: un <see cref="SaveFile"/> en blanco del juego pedido con los datos del entrenador de la partida.
/// El Worker no recibe bytes ni rutas de la partida real (plan 5): este objeto solo sirve para validar contra el destino y comprobar el round-trip
/// (escribir en una casilla y volver a leer) sin tocar jamas ningun guardado.
/// </summary>
public sealed class Destination
{
    public required SaveFile Sav { get; init; }
    public required GameVersion Version { get; init; }
    public required EntityContext Context { get; init; }
    public required StorageSlotType SlotType { get; init; }
    public required bool IsRealSave { get; init; }
    public required TargetInfo Info { get; init; }

    /// <summary>
    /// R-009: severidad con la que el Core trata un rastreador de HOME ausente en ESTA operacion. <see cref="Severity.Invalid"/> es el valor por defecto del PKHeX oficial
    /// (HOMETransferSettings.cs:9); <see cref="Severity.Fishy"/> solo cuando el usuario activa el ajuste explicito. Se fija en cada <see cref="ApplyParseSettings"/>.
    /// </summary>
    public Severity TrackerSeverity { get; set; } = Severity.Invalid;

    public ITrainerInfo Trainer => Sav;
    public IPersonalTable Personal => Sav.Personal;
    public Type EntityType => Sav.PKMType;
    public byte Generation => Sav.Generation;

    /// <summary>
    /// COLO y XD son identificadores de ORIGEN que el Core NO mapea a un tipo de guardado (GameVersion.SaveFileType solo conoce CXD hacia XD: SaveFileType.cs:71, y
    /// IsValidSavedVersion es falso para ellos: GameUtil.cs:133), pero BlankSaveFile.Get(SaveFileType, ...) es publico y construye SAV3Colosseum y SAV3XD en blanco
    /// (BlankSaveFile.cs:117-118) sin ningun guardado real. Asi Colosseum es un destino virtual medible, con su propio tipo de entidad (CK3).
    /// </summary>
    public static bool IsGameCubeSplit(GameVersion v) => v is GameVersion.COLO or GameVersion.XD;

    /// <summary>Contexto del juego; COLO y XD (sin contexto propio en el Core, GameUtil.cs:133) son Gen3.</summary>
    public static EntityContext ContextOf(GameVersion v) => IsGameCubeSplit(v) ? EntityContext.Gen3 : v.Context;

    /// <summary>Guardado en blanco del juego pedido, incluidos Colosseum y XD por separado (ver <see cref="IsGameCubeSplit"/>).</summary>
    public static SaveFile Blank(GameVersion version, string trainer, LanguageID language) => version switch
    {
        GameVersion.COLO => BlankColosseum(trainer, language),
        GameVersion.XD => BlankSaveFile.Get(SaveFileType.XD, version, trainer, language),
        _ => BlankSaveFile.Get(version, trainer, language),
    };

    /// <summary>
    /// El constructor en blanco publico de Colosseum (<c>new SAV3Colosseum(bool)</c>, SAV3Colosseum.cs:50-57) FALLA siempre: no llama a <c>base(size)</c>, de modo que <c>Data</c> esta vacio y
    /// el setter de <c>OriginalRegion</c> (SAV3Colosseum.cs:246, <c>Data[0x0A]</c>) lanza <see cref="IndexOutOfRangeException"/> (a diferencia de <c>SAV3XD</c>, que si reserva SIZE_G3XD). La via
    /// legitima es el constructor desde datos con una imagen en blanco del tamano exacto (<see cref="ColoCrypto.SAVE_SIZE"/>, sin cifrar): contenido de ceros, ningun guardado real. El contador
    /// de las tres ranuras es 0 y <see cref="ColoCrypto.DetectLatest"/> elige la ranura 0 (SAV3Colosseum.cs:59-72).
    /// </summary>
    private static SaveFile BlankColosseum(string trainer, LanguageID language)
    {
        var sav = new SAV3Colosseum(new byte[ColoCrypto.SAVE_SIZE], decrypt: false);
        sav.CurrentRegion = sav.OriginalRegion = GCRegion.NTSC_U;
        sav.OT = trainer;
        sav.Language = (int)language;
        return sav;
    }

    /// <summary>Crea el destino a partir del contrato. Devuelve false con un mensaje en espanol si el juego/formato no esta soportado.</summary>
    public static bool TryCreate(TargetInfo t, out Destination? dest, out string error)
    {
        dest = null;
        if (!Enum.TryParse<GameVersion>(t.GameVersion, ignoreCase: false, out var version) || !(version.IsValidSavedVersion() || IsGameCubeSplit(version)))
        {
            error = $"El juego de destino '{t.GameVersion}' no es un juego concreto reconocido por PKHeX.";
            return false;
        }
        if (!Enum.TryParse<EntityContext>(t.Context, ignoreCase: false, out var context) || !context.IsValid)
        {
            error = $"El contexto de destino '{t.Context}' no es válido.";
            return false;
        }
        var slot = StorageSlotType.None;
        if (!string.IsNullOrEmpty(t.SlotType) && !Enum.TryParse(t.SlotType, ignoreCase: false, out slot))
        {
            error = $"El tipo de casilla '{t.SlotType}' no es válido.";
            return false;
        }
        if (ContextOf(version) != context)
        {
            error = $"El juego {version} no pertenece al contexto {context} (es {ContextOf(version)}).";
            return false;
        }

        SaveFile sav;
        try
        {
            sav = Blank(version, t.Trainer.Ot, (LanguageID)t.Trainer.Language);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            error = $"No se pudo construir un destino virtual para {version}: {ex.Message}";
            return false;
        }
        // El guardado en blanco de Diamante/Perla devuelve el grupo DP y el de Colosseum/XD solo XD: basta con que el juego pedido y el del guardado se contengan (Hito 3).
        if (sav.Version != version && !sav.Version.Contains(version) && !version.Contains(sav.Version))
        {
            error = $"PKHeX no sabe construir un destino virtual para {version} (devolvió {sav.Version}).";
            return false;
        }
        if (!string.Equals(sav.PKMType.Name, t.SaveRuntimeType, StringComparison.Ordinal))
        {
            error = $"El destino {version} guarda {sav.PKMType.Name}, pero la solicitud dice {t.SaveRuntimeType}.";
            return false;
        }

        sav.TID16 = (ushort)t.Trainer.Tid16;
        sav.SID16 = (ushort)t.Trainer.Sid16;
        sav.Gender = (byte)t.Trainer.Gender;
        sav.Language = t.Trainer.Language;
        sav.OT = t.Trainer.Ot;

        dest = new Destination { Sav = sav, Version = version, Context = context, SlotType = slot, IsRealSave = t.IsRealSave, Info = t };
        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Fija el estado ESTATICO del Core para este destino (era de cartucho/consola virtual y entrenador activo). Debe llamarse con
    /// <see cref="EngineGlobalState.Gate"/> tomado, antes de cada analisis.
    /// </summary>
    public void ApplyParseSettings()
    {
        ParseSettings.InitFromSaveFileData(Sav);
        ParseSettings.Settings.HOMETransfer.HOMETransferTrackerNotPresent = TrackerSeverity;
        var flags = Info.SaveFlags;
        if (flags is null) return;
        if (flags.TryGetValue("CartEraGB", out var gb) && bool.TryParse(gb, out var gbv)) ParseSettings.AllowEraCartGB = gbv;
        if (flags.TryGetValue("CartEraGBA", out var gba) && bool.TryParse(gba, out var gbav)) ParseSettings.AllowEraCartGBA = gbav;
        if (flags.TryGetValue("SwitchEraGBA", out var sw) && bool.TryParse(sw, out var swv)) ParseSettings.AllowEraSwitchGBA = swv;
    }
}
