// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;

namespace AutoLegalidad.Catalog.Core;

/// <summary>De donde sale la presencia de una especie/forma en un juego (columna <c>game.availability_basis</c>).</summary>
public enum AvailabilityBasis
{
    /// <summary><c>GameData.GetPersonal(version).IsPresentInGame</c>: tabla personal del propio juego (o de su grupo en el Core).</summary>
    PersonalTable,
    /// <summary>No hay tabla personal propia: la presencia se deduce de las tablas de encuentros del Core (GO).</summary>
    CoreEncounters,
    /// <summary>El Core fijado no tiene datos de presencia para este juego: se registra <c>Unknown</c>, nunca se inventa.</summary>
    Unknown,
}

/// <summary>Juego CONCRETO (valor guardado en los datos del Pokémon) con sus metadatos del catálogo.</summary>
public sealed record GameDef(GameVersion Version, string NameEs, string? SaveType, bool IsStorage, bool IsTransferOriginOnly, AvailabilityBasis Basis)
{
    public int Id => (int)Version;

    /// <summary>Contexto de entidad del Core (<see cref="EntityContext.None"/> para GO, que no es un guardado).</summary>
    public EntityContext Context => Version == GameVersion.GO ? EntityContext.None : Version.GetContextFromSaved();

    /// <summary>Contexto usado para reglas de forma/evolución. GO se trata como Gen8 (HOME/PK8), el formato que usa el Core para sus ventanas.</summary>
    public EntityContext RuleContext => Version == GameVersion.GO ? EntityContext.Gen8 : Context;

    /// <summary>Generación del formato (<c>PKM.Format</c>); null para juegos sin formato propio.</summary>
    public int? Generation => Context.IsValid ? Context.Generation : null;

    /// <summary>Formato de entidad; null si el Core no da contexto al juego (WI y WA en PKHeX 26.10.18: solo existe el valor del enum).</summary>
    public int? EntityFormat => RuleContext.IsValid ? RuleContext.Generation : null;

    /// <summary>Nombre de la <c>PersonalTable</c> estática del Core que usa este juego, o null si no la tiene.</summary>
    public string? PersonalTableName => Basis == AvailabilityBasis.PersonalTable ? PersonalTableOf(Version) : null;

    public IPersonalTable? PersonalTable => Basis == AvailabilityBasis.PersonalTable ? GameData.GetPersonal(Version) : null;

    private static string PersonalTableOf(GameVersion v) => v switch
    {
        GameVersion.RD or GameVersion.GN or GameVersion.BU => "RB",
        GameVersion.YW => "Y",
        GameVersion.GD or GameVersion.SI => "GS",
        GameVersion.C => "C",
        GameVersion.R or GameVersion.S => "RS",
        GameVersion.E or GameVersion.CXD => "E",
        GameVersion.FR => "FR",
        GameVersion.LG => "LG",
        GameVersion.D or GameVersion.P => "DP",
        GameVersion.Pt => "Pt",
        GameVersion.HG or GameVersion.SS => "HGSS",
        GameVersion.B or GameVersion.W => "BW",
        GameVersion.B2 or GameVersion.W2 => "B2W2",
        GameVersion.X or GameVersion.Y => "XY",
        GameVersion.AS or GameVersion.OR => "AO",
        GameVersion.SN or GameVersion.MN => "SM",
        GameVersion.US or GameVersion.UM => "USUM",
        GameVersion.GP or GameVersion.GE => "GG",
        GameVersion.SW or GameVersion.SH => "SWSH",
        GameVersion.BD or GameVersion.SP => "BDSP",
        GameVersion.PLA => "LA",
        GameVersion.SL or GameVersion.VL => "SV",
        GameVersion.ZA => "ZA",
        _ => throw new ArgumentOutOfRangeException(nameof(v), v, "Sin tabla personal en el Core fijado."),
    };
}

/// <summary>Los juegos concretos y los grupos de versiones del Core fijado, con su nombre en español de España.</summary>
public static class GameCatalog
{
    private const AvailabilityBasis P = AvailabilityBasis.PersonalTable;

    public static readonly IReadOnlyList<GameDef> Concrete =
    [
        new(GameVersion.RD, "Pokémon Rojo", "SAV1", false, false, P),
        new(GameVersion.GN, "Pokémon Verde (Japón)", "SAV1", false, false, P),
        new(GameVersion.BU, "Pokémon Azul", "SAV1", false, false, P),
        new(GameVersion.YW, "Pokémon Amarillo", "SAV1", false, false, P),
        new(GameVersion.GD, "Pokémon Oro", "SAV2", false, false, P),
        new(GameVersion.SI, "Pokémon Plata", "SAV2", false, false, P),
        new(GameVersion.C, "Pokémon Cristal", "SAV2", false, false, P),
        new(GameVersion.R, "Pokémon Rubí", "SAV3RS", false, false, P),
        new(GameVersion.S, "Pokémon Zafiro", "SAV3RS", false, false, P),
        new(GameVersion.E, "Pokémon Esmeralda", "SAV3E", false, false, P),
        new(GameVersion.FR, "Pokémon Rojo Fuego", "SAV3FRLG", false, false, P),
        new(GameVersion.LG, "Pokémon Verde Hoja", "SAV3FRLG", false, false, P),
        new(GameVersion.CXD, "Pokémon Colosseum / XD", "SAV3Colosseum|SAV3XD", false, false, P),
        new(GameVersion.D, "Pokémon Diamante", "SAV4DP", false, false, P),
        new(GameVersion.P, "Pokémon Perla", "SAV4DP", false, false, P),
        new(GameVersion.Pt, "Pokémon Platino", "SAV4Pt", false, false, P),
        new(GameVersion.HG, "Pokémon Oro HeartGold", "SAV4HGSS", false, false, P),
        new(GameVersion.SS, "Pokémon Plata SoulSilver", "SAV4HGSS", false, false, P),
        new(GameVersion.BATREV, "Pokémon Battle Revolution", "SAV4BR", true, false, AvailabilityBasis.Unknown),
        new(GameVersion.W, "Pokémon Blanco", "SAV5BW", false, false, P),
        new(GameVersion.B, "Pokémon Negro", "SAV5BW", false, false, P),
        new(GameVersion.W2, "Pokémon Blanco 2", "SAV5B2W2", false, false, P),
        new(GameVersion.B2, "Pokémon Negro 2", "SAV5B2W2", false, false, P),
        new(GameVersion.X, "Pokémon X", "SAV6XY", false, false, P),
        new(GameVersion.Y, "Pokémon Y", "SAV6XY", false, false, P),
        new(GameVersion.AS, "Pokémon Zafiro Alfa", "SAV6AO", false, false, P),
        new(GameVersion.OR, "Pokémon Rubí Omega", "SAV6AO", false, false, P),
        new(GameVersion.SN, "Pokémon Sol", "SAV7SM", false, false, P),
        new(GameVersion.MN, "Pokémon Luna", "SAV7SM", false, false, P),
        new(GameVersion.US, "Pokémon Ultrasol", "SAV7USUM", false, false, P),
        new(GameVersion.UM, "Pokémon Ultraluna", "SAV7USUM", false, false, P),
        new(GameVersion.GO, "Pokémon GO", null, false, true, AvailabilityBasis.CoreEncounters),
        new(GameVersion.GP, "Pokémon: Let's Go, Pikachu!", "SAV7b", false, false, P),
        new(GameVersion.GE, "Pokémon: Let's Go, Eevee!", "SAV7b", false, false, P),
        new(GameVersion.SW, "Pokémon Espada", "SAV8SWSH", false, false, P),
        new(GameVersion.SH, "Pokémon Escudo", "SAV8SWSH", false, false, P),
        new(GameVersion.PLA, "Leyendas Pokémon: Arceus", "SAV8LA", false, false, P),
        new(GameVersion.BD, "Pokémon Diamante Brillante", "SAV8BS", false, false, P),
        new(GameVersion.SP, "Pokémon Perla Reluciente", "SAV8BS", false, false, P),
        new(GameVersion.SL, "Pokémon Escarlata", "SAV9SV", false, false, P),
        new(GameVersion.VL, "Pokémon Púrpura", "SAV9SV", false, false, P),
        new(GameVersion.ZA, "Leyendas Pokémon: Z-A", "SAV9ZA", false, false, P),
        new(GameVersion.CP, "Pokémon Champions", null, false, false, AvailabilityBasis.Unknown),
        // PKHeX 26.10.18 añade cuatro valores guardables nuevos. Ninguno tiene tabla personal propia en el Core, así que su presencia es Unknown (nunca se inventa) y no son destino de generación:
        // WI/WA son Pokémon Winds y Waves (el Core solo declara el valor del enum, sin datos) y FRX/LGX son Rojo Fuego/Verde Hoja de Nintendo Switch Online, que solo existen como origen tras pasar a HOME.
        new(GameVersion.WI, "Pokémon Winds (sin datos en el Core)", null, false, false, AvailabilityBasis.Unknown),
        new(GameVersion.WA, "Pokémon Waves (sin datos en el Core)", null, false, false, AvailabilityBasis.Unknown),
        new(GameVersion.FRX, "Pokémon Rojo Fuego (Switch Online, origen tras pasar a HOME)", null, false, true, AvailabilityBasis.Unknown),
        new(GameVersion.LGX, "Pokémon Verde Hoja (Switch Online, origen tras pasar a HOME)", null, false, true, AvailabilityBasis.Unknown),
    ];

    /// <summary>Valores del enum posteriores al último juego concreto: grupos que el Core usa en <c>Version</c> de encuentros.</summary>
    public static IReadOnlyList<GameVersion> Groups { get; } = Enum.GetValues<GameVersion>()
        .Where(v => v is not (GameVersion.Any or GameVersion.Invalid) && !v.IsValidSavedVersion())
        .OrderBy(v => (int)v)
        .ToList();

    public static GameDef Get(GameVersion v) => Concrete.First(g => g.Version == v);

    /// <summary>Comprueba que no falta ni sobra ningún juego respecto de los valores guardables del Core (falla en voz alta si el Core cambia).</summary>
    public static void VerifyAgainstCore()
    {
        var core = GameUtil.GameVersions.Where(v => v.IsValidSavedVersion()).ToHashSet();
        core.Add(GameVersion.CP); // el Core lo declara como valor guardable más reciente (HighestGameID)
        var mine = Concrete.Select(g => g.Version).ToHashSet();
        var missing = core.Except(mine).ToList();
        var extra = mine.Except(core).ToList();
        if (missing.Count != 0 || extra.Count != 0)
            throw new InvalidOperationException($"GameCatalog no coincide con el Core: faltan [{string.Join(',', missing)}], sobran [{string.Join(',', extra)}].");
        foreach (var g in Concrete.Where(g => g.SaveType is not null))
            foreach (var t in g.SaveType!.Split('|'))
                if (typeof(SaveFile).Assembly.GetType("PKHeX.Core." + t) is null)
                    throw new InvalidOperationException($"La clase de guardado '{t}' declarada para {g.Version} no existe en el Core fijado.");
    }

    /// <summary>Juegos concretos que abarca una versión de encuentro (concreta, grupo o <c>Any</c>) según <c>GameVersion.Contains</c> del Core.</summary>
    public static IReadOnlyList<GameDef> Resolve(GameVersion version)
    {
        if (version is GameVersion.Invalid) return [];
        return Concrete.Where(g => version.Contains(g.Version)).ToList();
    }
}
