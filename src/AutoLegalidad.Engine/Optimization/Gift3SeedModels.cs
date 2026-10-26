// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;

namespace AutoLegalidad.Engine.Optimization;

/// <summary>
/// Modelos de semilla de los regalos de Gen3 (<see cref="EncounterGift3"/>) cuyo PID/IV NO resuelve la busqueda del LCRNG clasico (metodos 1/2/4/BACD): el Jirachi del Canal
/// Pokemon (XDRNG) y los regalos variocolor con semilla restringida (arreglo de bayas y tabla del 5.o aniversario). Hito 3b, cola de clases.
/// </summary>
internal static class Gift3SeedModels
{
    public static ISeedModel? For(EncounterGift3 enc, PK3 original) => enc.Method switch
    {
        PIDType.Channel => new Channel3Model(original),
        // GetSaneSeed (EncounterGift3.cs:355-367) y CommonEvent3Checker (IsBerryFixShiny :149-157, IsRestrictedTableMatch :37-55): la semilla de origen esta acotada.
        PIDType.BACD_RBCD when enc.Shiny == Shiny.Always => new RestrictedGift3Model(enc, RestrictedGift3Model.Domain.BerryFix),
        PIDType.BACD_TS => new RestrictedGift3Model(enc, RestrictedGift3Model.Domain.Table16),
        _ => null,
    };
}

/// <summary>
/// Jirachi del Canal Pokemon (<see cref="PIDType.Channel"/>): TODO sale de una semilla del XDRNG (EncounterGift3.SetValuesFromSeedChannel, EncounterGift3.cs:334-353): SID, PID, objeto, version,
/// genero del EO y los seis IV. El Core solo acepta las semillas con patron de Jirachi posible (<see cref="ChannelJirachi.IsPossible"/>, ChannelJirachi.cs:18). Como el SID, el objeto, la version
/// y el genero del EO pertenecen al ORIGEN del Pokemon (no se tocan al optimizar), solo valen las semillas que reproducen los mismos: el dominio de 2^32 se recorre filtrando por ellos.
/// </summary>
internal sealed class Channel3Model(PK3 original) : ISeedModel
{
    private readonly uint _id32 = original.ID32;
    private readonly ushort _held = (ushort)original.HeldItem;
    private readonly GameVersion _version = original.Version;
    private readonly byte _otGender = original.OriginalTrainerGender;

    public string Name => "SeedSpaceSearch/Channel3";
    public bool IsFullDomain => true;
    public string CacheIdentity => $"{Name}|held={_held}|version={(int)_version}|otGender={_otGender}"; // el SID sale de la semilla: depende del id32 (entra hasheado en la clave)
    public bool DependsOnTrainerId => true;

    /// <summary>True si la semilla (la que genera PID/IV) reproduce el origen del Pokemon; devuelve el estado tras los cinco campos previos a los IV.</summary>
    private bool MatchesOrigin(uint seed, out uint after)
    {
        var s = seed;
        after = 0;
        var sid = XDRNG.Next16(ref s);
        if (sid != (_id32 >> 16)) return false;
        XDRNG.Next16(ref s); // PID alto
        XDRNG.Next16(ref s); // PID bajo
        if (((XDRNG.Next16(ref s) >> 15) + 169u) != _held) return false;
        if (GameVersion.S + (byte)(XDRNG.Next16(ref s) >> 15) != _version) return false;
        if ((byte)(XDRNG.Next16(ref s) >> 15) != _otGender) return false;
        after = s;
        return ChannelJirachi.IsPossible(seed);
    }

    public bool Apply(PKM scratch, ulong seed)
    {
        var s = (uint)seed;
        if (!MatchesOrigin(s, out _)) return false;
        var pk = (PK3)scratch;
        EncounterGift3.SetValuesFromSeedChannel(pk, s);
        pk.RefreshAbility((int)(pk.PID & 1));
        return true;
    }

    public int FastIvSum(ulong seed)
    {
        if (!MatchesOrigin((uint)seed, out var after)) return 0;
        var iv32 = XDRNG.GetSequentialIV32(after);
        var sum = 0;
        for (var i = 0; i < 6; i++) sum += (int)((iv32 >> (5 * i)) & 31);
        return sum;
    }
}

/// <summary>
/// Regalos de Gen3 variocolor con semilla de origen restringida. El generador por criterios del Core no cubre bien estos dominios (no hay un vector de IV -&gt; semilla con esa restriccion),
/// asi que se recorre el dominio finito de la semilla: <see cref="Domain.BerryFix"/> (semilla bruta en [0, 213]: reloj BCD, EncounterGift3.GetSaneSeed) y <see cref="Domain.Table16"/>
/// (semilla bruta = LCRNG.Next2(u16): CommonEvent3Checker.IsRestrictedTableMatch exige <c>LCRNG.Prev2(seed) &lt;= 0xFFFF</c>).
/// </summary>
internal sealed class RestrictedGift3Model(EncounterGift3 enc, RestrictedGift3Model.Domain domain) : ISeedModel
{
    public enum Domain { BerryFix, Table16 }

    public string Name => "SeedSpaceSearch/Gift3" + domain;
    public bool IsFullDomain => true;
    public string CacheIdentity => $"{Name}|enc={SolutionCache.EncounterFingerprint.Of(enc)}";

    private bool TryRaw(ulong seed, out uint raw)
    {
        raw = 0;
        switch (domain)
        {
            case Domain.BerryFix:
                raw = (uint)seed;
                return seed <= 213;
            default:
                if (seed > ushort.MaxValue) return false;
                raw = LCRNG.Next2((uint)seed);
                return true;
        }
    }

    public bool Apply(PKM scratch, ulong seed)
    {
        if (!TryRaw(seed, out var raw)) return false;
        enc.GenerateSeed32(scratch, raw);
        scratch.RefreshAbility((int)(scratch.PID & 1));
        return true;
    }

    // Fuera del dominio ninguna semilla produce nada; dentro, la suma sale de Apply (-1: «no se calcula aparte»).
    public int FastIvSum(ulong seed) => TryRaw(seed, out _) ? -1 : 0;
}
