// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;

namespace AutoLegalidad.Engine.Generation;

/// <summary>
/// Rasgos DESEADOS del usuario, separados de la procedencia (plan 7, fase C: «mantener OriginalSnapshot y DesiredTraits separados»).
/// Son criterios BLANDOS: la especie y la forma son obligatorias; el resto se relaja de forma automatica y ordenada (<see cref="Relaxation"/>).
/// </summary>
public sealed record Traits
{
    public required ushort Species { get; init; }
    public required byte Form { get; init; }
    public required EntityContext Context { get; init; }
    public string Nickname { get; init; } = string.Empty;
    public int HeldItem { get; init; }
    public byte Level { get; init; } = 100;
    /// <summary>0/1/2 (macho/hembra/sin genero) o null si no se pide.</summary>
    public byte? Gender { get; init; }
    /// <summary>Id de habilidad pedida o -1.</summary>
    public int Ability { get; init; } = -1;
    /// <summary>Naturaleza pedida o <see cref="Nature.Random"/>.</summary>
    public Nature Nature { get; init; } = Nature.Random;
    public bool Shiny { get; init; }
    public int[] IVs { get; init; } = [31, 31, 31, 31, 31, 31];
    public int[] EVs { get; init; } = [0, 0, 0, 0, 0, 0];
    public ushort[] Moves { get; init; } = [0, 0, 0, 0];
    public Ball Ball { get; init; }
    public byte Friendship { get; init; } = 255;
    /// <summary>Tipo Teratipo pedido (Gen9) o <see cref="MoveType.Any"/>.</summary>
    public MoveType TeraType { get; init; } = MoveType.Any;
    public bool CanGigantamax { get; init; }

    /// <summary>Rasgos del Pokemon actual. Los IV pedidos son los del usuario; <see cref="WithOptimizedIVs"/> los sube a lo maximo.</summary>
    public static Traits From(PKM pk)
    {
        var moves = new ushort[4];
        pk.GetMoves(moves);
        var ivs = PkmFieldsAccess.IVs(pk);
        var (perStat, total) = Repair.EvNormalizer.Limits(pk);
        var evs = Repair.EvNormalizer.Normalize(PkmFieldsAccess.EVs(pk), perStat, total);
        return new Traits
        {
            Species = pk.Species,
            Form = pk.Form,
            Context = pk.Context,
            Nickname = pk.IsNicknamed ? pk.Nickname : string.Empty,
            HeldItem = pk.HeldItem,
            Level = Math.Max((byte)1, Math.Max(pk.CurrentLevel, pk.MetLevel)),
            Gender = pk.Gender <= 2 ? pk.Gender : null,
            Ability = pk.Ability,
            Nature = pk.Nature,
            Shiny = pk.IsShiny,
            IVs = ivs,
            EVs = evs,
            Moves = moves,
            Ball = (Ball)pk.Ball,
            Friendship = pk.CurrentFriendship,
            CanGigantamax = pk is IGigantamax g && g.CanGigantamax,
            TeraType = pk is ITeraType t ? t.TeraTypeOriginal : MoveType.Any,
        };
    }

    public Traits WithOptimizedIVs() => this with { IVs = [31, 31, 31, 31, 31, 31] };

    public TraitsTemplate ToTemplate() => new(this);
}

/// <summary>Plantilla de combate (<see cref="IBattleTemplate"/> del Core) construida desde <see cref="Traits"/>; la consume el adaptador de ALM sin pasar por texto Showdown.</summary>
public sealed class TraitsTemplate(Traits t) : IBattleTemplate
{
    public ushort Species { get; } = t.Species;
    public byte Form { get; } = t.Form;
    public EntityContext Context { get; } = t.Context;
    public string Nickname { get; } = t.Nickname;
    public byte? Gender { get; } = t.Gender;
    public int HeldItem { get; } = t.HeldItem;
    public int Ability { get; } = t.Ability;
    public byte Level { get; } = t.Level;
    public bool Shiny { get; } = t.Shiny;
    public byte Friendship { get; } = t.Friendship;
    public Nature Nature { get; } = t.Nature;
    public string FormName { get; } = string.Empty;
    public sbyte HiddenPowerType { get; } = -1;
    public bool CanGigantamax { get; } = t.CanGigantamax;
    public byte DynamaxLevel { get; } = 10;
    public MoveType TeraType { get; } = t.TeraType;
    public int[] EVs { get; } = [.. t.EVs];
    public int[] IVs { get; } = [.. t.IVs];
    public ushort[] Moves { get; } = [.. t.Moves];
}

/// <summary>Acceso a IV/EV en el orden visual (PS, Ata, Def, AtE, DeE, Vel) que usan las plantillas.</summary>
public static class PkmFieldsAccess
{
    public static int[] IVs(PKM pk) => [pk.IV_HP, pk.IV_ATK, pk.IV_DEF, pk.IV_SPA, pk.IV_SPD, pk.IV_SPE];
    public static int[] EVs(PKM pk) => [pk.EV_HP, pk.EV_ATK, pk.EV_DEF, pk.EV_SPA, pk.EV_SPD, pk.EV_SPE];
}

public static class TraitsEncounterExtensions
{
    /// <summary>
    /// Adapta los rasgos a lo que FIJA un encuentro concreto (variocolor imposible o obligatorio, sexo, naturaleza, habilidad por indice, nivel minimo) para poder
    /// regenerar DENTRO de ese encuentro conservando todo lo demas (identidad del evento, plan 7: «un evento especifico reconocido se intenta preservar primero»).
    /// </summary>
    public static Traits AdaptToEncounter(this Traits t, IEncounterable enc)
    {
        if (enc.Shiny == Shiny.Never) t = t with { Shiny = false };
        else if (enc.Shiny.IsShiny()) t = t with { Shiny = true };
        if (enc is IFixedGender { IsFixedGender: true } fg) t = t with { Gender = fg.Gender };
        if (enc is IFixedNature { IsFixedNature: true } fn) t = t with { Nature = fn.Nature };
        if (enc.Ability is not (AbilityPermission.Any12 or AbilityPermission.Any12H)) t = t with { Ability = -1 };
        if (t.Level < enc.LevelMin) t = t with { Level = enc.LevelMin };
        return t;
    }
}
