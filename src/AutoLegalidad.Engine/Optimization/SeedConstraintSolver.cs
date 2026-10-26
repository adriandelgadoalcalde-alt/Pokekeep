// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Numerics;
using PKHeX.Core;

namespace AutoLegalidad.Engine.Optimization;

// Hito 3b (ADR 0018): busqueda DIRIGIDA de semillas de 64 bits por restricciones.
//
// Las semillas de Espada/Escudo (incursiones) y de Leyendas Z-A son de 64 bits y el juego las usa con xoroshiro128+ (Core: Xoroshiro128Plus, estado inicial
// (s0 = semilla, s1 = XOROSHIRO_CONST)). Cada salida es s0 + s1 (suma de 64 bits) y el estado avanza con operaciones LINEALES sobre GF(2) (xor, rotacion, desplazamiento).
// Todo lo que el juego deriva de la semilla (IV, habilidad, sexo, naturaleza) sale de los BITS BAJOS de una salida concreta (mascara de NextInt): por tanto,
// «los 5 bits bajos de la salida t valen v» es una ecuacion sobre los 64 bits de la semilla que solo deja de ser lineal por los acarreos de la suma; como
// solo hay 5 a 8 bits, los acarreos se resuelven ramificando (un bit por acarreo no constante) y cada rama es un sistema lineal en GF(2).
// Resultado: dado un vector de IV deseado (y habilidad, sexo y naturaleza), se obtienen semillas de TODO el dominio de 64 bits en milisegundos, no 2^32 intentos.
// El solver SOLO PROPONE semillas: cada una se reproduce con el generador del Core (Apply) y se filtra igual que el recorrido; si el Core no la acepta, no cuenta.

/// <summary>Forma afin sobre GF(2) de los 64 bits de la semilla: <c>(Mask · x) xor C</c>.</summary>
internal readonly record struct Gf2Form(ulong Mask, byte C)
{
    public bool IsConst => Mask == 0;
    public static Gf2Form Const(int v) => new(0, (byte)(v & 1));
    public static Gf2Form operator ^(Gf2Form a, Gf2Form b) => new(a.Mask ^ b.Mask, (byte)(a.C ^ b.C));
}

/// <summary>Sistema de ecuaciones lineales sobre GF(2) en 64 incognitas, en forma escalonada incremental (con clonado barato para el retroceso).</summary>
internal sealed class Gf2System
{
    private readonly ulong[] _mask = new ulong[64];
    private readonly byte[] _c = new byte[64];
    private readonly byte[] _order = new byte[64];
    private ulong _pivots;
    private int _count;

    public int FreeCount => 64 - _count;
    public ulong FreeMask => ~_pivots;

    public Gf2System Clone()
    {
        var s = new Gf2System { _pivots = _pivots, _count = _count };
        Array.Copy(_mask, s._mask, 64);
        Array.Copy(_c, s._c, 64);
        Array.Copy(_order, s._order, 64);
        return s;
    }

    /// <summary>Sustituye las incognitas ya determinadas: el resultado no contiene ningun pivote.</summary>
    public Gf2Form Reduce(Gf2Form f)
    {
        var m = f.Mask;
        var c = f.C;
        var p = m & _pivots;
        while (p != 0)
        {
            var b = BitOperations.TrailingZeroCount(p);
            m ^= _mask[b];
            c ^= _c[b];
            p = m & _pivots;
        }
        return new Gf2Form(m, c);
    }

    /// <summary>Anade <c>f == value</c>; false si contradice lo ya asumido.</summary>
    public bool Assert(Gf2Form f, int value)
    {
        var r = Reduce(f);
        var rhs = (byte)(r.C ^ (value & 1));
        if (r.Mask == 0) return rhs == 0;
        var pivot = BitOperations.TrailingZeroCount(r.Mask);
        _mask[pivot] = r.Mask;
        _c[pivot] = rhs;
        _pivots |= 1UL << pivot;
        _order[_count++] = (byte)pivot;
        return true;
    }

    /// <summary>Una solucion concreta: <paramref name="free"/> da el valor de las incognitas libres (los pivotes se calculan).</summary>
    public ulong Solve(ulong free)
    {
        var x = free & ~_pivots;
        for (var i = _count - 1; i >= 0; i--)
        {
            int b = _order[i];
            var others = _mask[b] & ~(1UL << b);
            var v = _c[b] ^ (BitOperations.PopCount(others & x) & 1);
            if (v == 1) x |= 1UL << b;
        }
        return x;
    }
}

/// <summary>Estado simbolico de xoroshiro128+ (s0 = semilla simbolica, s1 = constante) para las primeras salidas; es el mismo para toda semilla.</summary>
internal static class XoroSymbolic
{
    public const int Steps = 48;
    public static readonly Gf2Form[][] S0 = new Gf2Form[Steps][];
    public static readonly Gf2Form[][] S1 = new Gf2Form[Steps][];

    static XoroSymbolic()
    {
        var a = new Gf2Form[64];
        var b = new Gf2Form[64];
        for (var i = 0; i < 64; i++)
        {
            a[i] = new Gf2Form(1UL << i, 0);
            b[i] = Gf2Form.Const((int)((Xoroshiro128Plus.XOROSHIRO_CONST >> i) & 1));
        }
        for (var t = 0; t < Steps; t++)
        {
            S0[t] = a;
            S1[t] = b;
            var b1 = new Gf2Form[64];
            for (var i = 0; i < 64; i++) b1[i] = b[i] ^ a[i];
            var na = new Gf2Form[64];
            var nb = new Gf2Form[64];
            for (var i = 0; i < 64; i++)
            {
                na[i] = a[(i - 24) & 63] ^ b1[i] ^ (i >= 16 ? b1[i - 16] : Gf2Form.Const(0)); // rotl(s0,24) ^ s1 ^ (s1 << 16)
                nb[i] = b1[(i - 37) & 63];                                                      // rotl(s1,37)
            }
            a = na;
            b = nb;
        }
    }
}

/// <summary>Una salida de xoroshiro: sus <see cref="Bits"/> bajos deben valer alguno de <see cref="Values"/> (la primera tirada se acepta: el juego no la repite). <c>Bits = 0</c>: salida sin restriccion.</summary>
internal readonly record struct SeedDraw(int Bits, int[] Values)
{
    public static SeedDraw Free => new(0, []);
}

/// <summary>Un modelo de semilla de 64 bits que sabe expresar lo que quiere de la semilla como restricciones sobre las salidas de xoroshiro.</summary>
internal interface ISeedConstraintModel : ISeedModel
{
    /// <summary>Problemas alternativos (cada uno, una secuencia de tiradas) cuyas semillas dan <paramref name="target"/> como IV y conservan habilidad, sexo y naturaleza de <paramref name="template"/>.</summary>
    IEnumerable<List<SeedDraw>> Problems(PKM template, int[] target);
}

internal static class SeedConstraintSolver
{
    /// <summary>
    /// Semillas de 64 bits que cumplen todas las restricciones de algun problema Y la prueba <paramref name="accept"/> (la generacion real del Core). Determinista:
    /// mismo orden de ramas y mismas muestras de las incognitas libres. Se detiene en <paramref name="want"/> semillas aceptadas o al pasar <paramref name="deadline"/>.
    /// </summary>
    public static List<ulong> Find(IEnumerable<List<SeedDraw>> problems, Func<ulong, bool> accept, int want, int samplesPerSystem, DateTime deadline, CancellationToken ct)
    {
        var found = new List<ulong>();
        foreach (var draws in problems)
        {
            if (ct.IsCancellationRequested || DateTime.UtcNow >= deadline) break;
            var sys = new Gf2System();
            Descend(draws, 0, sys, s => Leaf(s, accept, found, want, samplesPerSystem), ct, deadline);
            if (found.Count >= want) break;
        }
        return found;
    }

    private static bool Leaf(Gf2System sys, Func<ulong, bool> accept, List<ulong> found, int want, int samples)
    {
        var free = sys.FreeMask;
        var dims = sys.FreeCount;
        var total = dims >= 40 ? ulong.MaxValue : 1UL << dims;
        var n = (int)Math.Min((ulong)samples, total);
        var state = 0x9E3779B97F4A7C15UL ^ (ulong)dims;
        for (var i = 0; i < n; i++)
        {
            ulong assign;
            if (total <= (ulong)samples) assign = Deposit((ulong)i, free);          // enumera TODAS las soluciones del sistema
            else if (i == 0) assign = 0;                                            // primera muestra: incognitas libres a 0
            else { state = SplitMix(state); assign = state & free; }
            var seed = sys.Solve(assign);
            if (found.Contains(seed) || !accept(seed)) continue;
            found.Add(seed);
            if (found.Count >= want) return true;
        }
        return false;
    }

    private static ulong Deposit(ulong value, ulong mask)
    {
        ulong r = 0;
        for (var m = mask; m != 0 && value != 0; m &= m - 1)
        {
            if ((value & 1) != 0) r |= m & (ulong)-(long)m;
            value >>= 1;
        }
        return r;
    }

    private static ulong SplitMix(ulong x)
    {
        x += 0x9E3779B97F4A7C15UL;
        x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
        x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
        return x ^ (x >> 31);
    }

    private static bool Descend(List<SeedDraw> draws, int idx, Gf2System sys, Func<Gf2System, bool> leaf, CancellationToken ct, DateTime deadline)
    {
        if (idx >= draws.Count) return leaf(sys);
        if (ct.IsCancellationRequested || DateTime.UtcNow >= deadline) return false;
        var d = draws[idx];
        if (d.Bits == 0) return Descend(draws, idx + 1, sys, leaf, ct, deadline);
        foreach (var v in d.Values)
        {
            var s2 = sys.Clone();
            if (AddLow(s2, idx, d.Bits, v, 0, Gf2Form.Const(0), s => Descend(draws, idx + 1, s, leaf, ct, deadline))) return true;
        }
        return false;
    }

    /// <summary>
    /// Impone que los <paramref name="k"/> bits bajos de la salida <paramref name="t"/> (s0 + s1) valgan <paramref name="value"/>. Suma bit a bit: la ecuacion del bit es lineal
    /// (a xor b xor acarreo) y el acarreo siguiente es constante (si a != b es el acarreo anterior) o la forma <c>a</c> (si a == b); un acarreo no constante se ramifica (0 o 1).
    /// </summary>
    private static bool AddLow(Gf2System sys, int t, int k, int value, int bit, Gf2Form carry, Func<Gf2System, bool> next)
    {
        if (bit == k) return next(sys);
        var c = sys.Reduce(carry);
        if (!c.IsConst)
        {
            for (var g = 0; g < 2; g++)
            {
                var s2 = sys.Clone();
                if (!s2.Assert(c, g)) continue;
                if (AddLow(s2, t, k, value, bit, Gf2Form.Const(g), next)) return true;
            }
            return false;
        }
        int gamma = c.C;
        var vb = (value >> bit) & 1;
        var a = XoroSymbolic.S0[t][bit];
        var b = XoroSymbolic.S1[t][bit];
        var p = vb ^ gamma; // valor de a xor b
        if (!sys.Assert(a ^ b, p)) return false;
        var newCarry = p == 1 ? Gf2Form.Const(gamma) : a;
        return AddLow(sys, t, k, value, bit + 1, newCarry, next);
    }

    // ------------------------------------------------------------------------------------------------------------ construccion de problemas
    /// <summary>Secuencias ordenadas (sin repetir) de <paramref name="count"/> indices de <paramref name="pool"/>; acotadas a 720 (6P5).</summary>
    public static IEnumerable<int[]> OrderedPicks(IReadOnlyList<int> pool, int count)
    {
        if (count == 0) { yield return []; yield break; }
        var used = new bool[pool.Count];
        var cur = new int[count];
        var emitted = 0;
        IEnumerable<int[]> Rec(int depth)
        {
            if (depth == count) { yield return (int[])cur.Clone(); yield break; }
            for (var i = 0; i < pool.Count; i++)
            {
                if (used[i]) continue;
                used[i] = true;
                cur[depth] = pool[i];
                foreach (var r in Rec(depth + 1)) yield return r;
                used[i] = false;
            }
        }
        foreach (var r in Rec(0))
        {
            yield return r;
            if (++emitted >= 720) yield break;
        }
    }

    /// <summary>Valores de una tirada de <c>NextInt(max)</c> (primer intento, sin rechazo) para los que <paramref name="ok"/> es cierto, repartidos para no ramificar de mas.</summary>
    public static int[] Allowed(int max, Func<int, bool> ok, int cap = 16)
    {
        var all = new List<int>();
        for (var v = 0; v < max; v++) if (ok(v)) all.Add(v);
        if (all.Count <= cap) return [.. all];
        var pick = new int[cap];
        for (var i = 0; i < cap; i++) pick[i] = all[(int)((long)i * all.Count / cap)];
        return pick;
    }

    /// <summary>Tiradas comunes tras los IV: habilidad (0/1/2), sexo y naturaleza; <c>false</c> si la plantilla pide algo que la semilla no puede dar.</summary>
    public static bool TryAppendTail(List<SeedDraw> draws, AbilityPermission ability, int abilityIndex, Func<List<SeedDraw>, bool> gender, bool natureFixed, int nature)
    {
        switch (ability)
        {
            case AbilityPermission.Any12H:
                if (abilityIndex is < 0 or > 2) return false;
                draws.Add(new SeedDraw(2, [abilityIndex]));
                break;
            case AbilityPermission.Any12:
                if (abilityIndex is < 0 or > 1) return false;
                draws.Add(new SeedDraw(1, [abilityIndex]));
                break;
        }
        if (!gender(draws)) return false;
        if (!natureFixed) draws.Add(new SeedDraw(5, [nature]));
        return true;
    }
}
