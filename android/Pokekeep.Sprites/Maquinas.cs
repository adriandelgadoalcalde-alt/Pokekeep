// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;

namespace Pokekeep.Sprites;

/// <summary>Clase de objeto de enseñanza: MT (máquina técnica), MO (máquina oculta) o DR (disco récord de Espada/Escudo, «TR» en PKHeX).</summary>
public enum ClaseMaquina : byte { MT, MO, DR }

/// <summary>
/// Una MT/MO/DR concreta de un juego: su número, el movimiento que enseña ESA máquina en ESE juego y el tipo de ese movimiento en ese juego (las tablas del Core, no inventadas).
/// </summary>
public readonly record struct Maquina(ClaseMaquina Clase, int Numero, ushort Movimiento, byte Tipo);

/// <summary>
/// Traduce el id de un objeto de la mochila a la máquina que es, con las tablas de PKHeX.Core (<c>PersonalInfo*.MachineMoves*</c>, <c>LearnSource6*.MachineMoves</c>, <c>ItemStorage*.Machine</c>).
/// El id es el NATIVO del formato (el que guarda la mochila): en Gen. 1-3 no es el moderno. Cada juego tiene su numeración (la MT24 de Gen. 3 no enseña lo mismo que la de Gen. 5), de ahí que haga falta el juego y no solo el contexto.
/// Z-A queda fuera a propósito (<see cref="Soportado"/>): PKHeX renombra sus MT respecto al id (<c>RemapTechnicalMachineItemName9a</c>) y el Core no dice qué movimiento enseña cada id; no se adivina.
/// </summary>
public static class Maquinas
{
    /// <summary>Si el Core permite saber el movimiento de cada máquina de ese contexto (todos salvo Z-A y los juegos sin máquinas).</summary>
    public static bool Soportado(EntityContext ctx) => ctx is not (EntityContext.Gen9a or EntityContext.Gen8a or EntityContext.None);

    /// <summary>La máquina que es el objeto <paramref name="idNativo"/> de la mochila de ese juego, o null si no es una máquina (o el contexto no está soportado).</summary>
    public static Maquina? De(GameVersion version, EntityContext ctx, int idNativo)
    {
        if (idNativo <= 0 || !Soportado(ctx)) return null;
        var id = idNativo;
        (ClaseMaquina Clase, int Numero)? q = ctx switch
        {
            EntityContext.Gen1 => Gen1(id),
            EntityContext.Gen2 => Gen2(id),
            EntityContext.Gen3 => Gen3(id),
            EntityContext.Gen8b => Gen8b(id),
            EntityContext.Gen8 => Gen8(id),
            EntityContext.Gen9 => Gen9(id),
            EntityContext.Gen7b => Gen7b(id),
            _ => Clasica(version, ctx, id),
        };
        if (q is not { } r) return null;
        var mov = Movimiento(version, ctx, r.Clase, r.Numero);
        return mov == 0 ? null : new Maquina(r.Clase, r.Numero, mov, MoveInfo.GetType(mov, ctx));
    }

    // ---- id nativo -> (clase, número) ----

    private static (ClaseMaquina, int)? Gen1(int id) => id switch
    {
        >= 196 and <= 200 => (ClaseMaquina.MO, id - 195),
        >= 201 and <= 250 => (ClaseMaquina.MT, id - 200),
        _ => null,
    };

    private static (ClaseMaquina, int)? Gen2(int id)
    {
        var m = ItemStorage2.Machine;
        var i = m.IndexOf((ushort)id);
        if (i < 0) return null;
        return i < 50 ? (ClaseMaquina.MT, i + 1) : (ClaseMaquina.MO, i - 49);
    }

    private static (ClaseMaquina, int)? Gen3(int id) => id switch
    {
        >= 289 and <= 338 => (ClaseMaquina.MT, id - 288),
        >= 339 and <= 346 => (ClaseMaquina.MO, id - 338),
        _ => null,
    };

    /// <summary>Numeración compartida por Gen. 4-7: MT01-92 = 328-419, MT93-95 = 618-620, MT96-100 = 690-694.</summary>
    private static int NumeroMt(int id) => id switch
    {
        >= 328 and <= 419 => id - 327,
        >= 618 and <= 620 => id - 618 + 93,
        >= 690 and <= 694 => id - 690 + 96,
        _ => -1,
    };

    private static (ClaseMaquina, int)? Clasica(GameVersion v, EntityContext ctx, int id)
    {
        var n = NumeroMt(id);
        if (n > 0)
        {
            var max = ctx switch { EntityContext.Gen4 => 92, EntityContext.Gen5 => 95, _ => 100 };
            return n <= max ? (ClaseMaquina.MT, n) : null;
        }
        switch (ctx)
        {
            case EntityContext.Gen4: return id is >= 420 and <= 427 ? (ClaseMaquina.MO, id - 419) : null;
            case EntityContext.Gen5: return id is >= 420 and <= 425 ? (ClaseMaquina.MO, id - 419) : null;
            case EntityContext.Gen6:
                if (EsRZ(v)) return id switch { >= 420 and <= 425 => (ClaseMaquina.MO, id - 419), 737 => (ClaseMaquina.MO, 7), _ => null };
                return id is >= 420 and <= 424 ? (ClaseMaquina.MO, id - 419) : null;
            default: return null;   // Gen. 7 (SM/USUM) no tiene MO
        }
    }

    private static (ClaseMaquina, int)? Gen7b(int id) => id is >= 328 and <= 387 ? (ClaseMaquina.MT, id - 327) : null;

    private static (ClaseMaquina, int)? Gen8b(int id) => id switch
    {
        >= 328 and <= 419 => (ClaseMaquina.MT, id - 327),
        >= 420 and <= 427 => (ClaseMaquina.MT, id - 327),   // en BD/SP las antiguas MO son la MT93-100
        _ => null,
    };

    private static (ClaseMaquina, int)? Gen8(int id)
    {
        var n = NumeroMt(id);
        if (n is > 0 and <= 99) return (ClaseMaquina.MT, n);
        if (id == 1230) return (ClaseMaquina.MT, 0);
        if (id is >= 1130 and <= 1229) return (ClaseMaquina.DR, id - 1130);
        return null;
    }

    private static (ClaseMaquina, int)? Gen9(int id)
    {
        var n = NumeroMt(id);
        if (n is > 0 and <= 99) return (ClaseMaquina.MT, n);
        if (id == 1230) return (ClaseMaquina.MT, 0);
        if (id is >= 2160 and <= 2289) return (ClaseMaquina.MT, id - 2160 + 100);
        return null;
    }

    // ---- (clase, número) -> movimiento ----

    private static ushort Movimiento(GameVersion v, EntityContext ctx, ClaseMaquina clase, int n)
    {
        static ushort En(ReadOnlySpan<ushort> t, int i) => (uint)i < (uint)t.Length ? t[i] : (ushort)0;
        static ushort EnB(ReadOnlySpan<byte> t, int i) => (uint)i < (uint)t.Length ? t[i] : (ushort)0;
        var mo = clase == ClaseMaquina.MO;
        switch (ctx)
        {
            case EntityContext.Gen1: return EnB(PersonalInfo1.MachineMoves, mo ? 49 + n : n - 1);
            case EntityContext.Gen2: return EnB(PersonalInfo2.MachineMoves, mo ? 49 + n : n - 1);
            case EntityContext.Gen3: return mo ? En(PersonalInfo3.MachineMovesHidden, n - 1) : En(PersonalInfo3.MachineMovesTechnical, n - 1);
            case EntityContext.Gen4:
                if (!mo) return En(PersonalInfo4.MachineMovesTechnical, n - 1);
                return En(EsHGSS(v) ? PersonalInfo4.MachineMovesHiddenHGSS : PersonalInfo4.MachineMovesHiddenDPPt, n - 1);
            case EntityContext.Gen5: return En(PersonalInfo5BW.MachineMoves, mo ? 94 + n : n - 1);
            case EntityContext.Gen6: return En(EsRZ(v) ? LearnSource6AO.MachineMoves : LearnSource6XY.MachineMoves, mo ? 99 + n : n - 1);
            case EntityContext.Gen7: return En(PersonalInfo7.MachineMoves, n - 1);
            case EntityContext.Gen7b: return En(LearnSource7GG.MachineMoves, n - 1);
            case EntityContext.Gen8: return clase == ClaseMaquina.DR ? En(PersonalInfo8SWSH.MachineMovesRecord, n) : En(PersonalInfo8SWSH.MachineMovesTechnical, n);
            case EntityContext.Gen8b: return En(PersonalInfo8BDSP.MachineMoves, n - 1);
            case EntityContext.Gen9: return En(PersonalInfo9SV.MachineMoves, n);
            default: return 0;
        }
    }

    private static bool EsHGSS(GameVersion v) => v is GameVersion.HG or GameVersion.SS or GameVersion.HGSS;
    /// <summary>Rubí Omega / Zafiro Alfa (las MO 06 y 07 son de ellos; X/Y solo tienen cinco).</summary>
    private static bool EsRZ(GameVersion v) => v is GameVersion.OR or GameVersion.AS or GameVersion.ORAS;

    /// <summary>Todas las máquinas de la mochila de un juego (para las pruebas y el informe de cobertura): id nativo y máquina.</summary>
    public static IEnumerable<(int IdNativo, Maquina Maquina)> Todas(GameVersion version, EntityContext ctx, IEnumerable<int> idsNativos)
    {
        foreach (var id in idsNativos)
            if (De(version, ctx, id) is { } m) yield return (id, m);
    }
}
