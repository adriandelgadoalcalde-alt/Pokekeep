// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;

namespace AutoLegalidad.Engine.Optimization;

/// <summary>
/// Elige, para una semilla de 32 bits, QUE plantillas de un grupo pueden producirla (ADR 0042, solo el precalculo). El barrido de un encuentro suelto llama a <c>CanBeEncountered</c> del Core con las 2^32 semillas
/// (la eleccion de incursion cuesta lo mismo que el resto del barrido); en un grupo cuyas plantillas comparten la tirada (misma dificultad y mapa, o misma distribucion) esa tirada se hace UNA vez por semilla y
/// se consulta una tabla de rangos. La logica replica la del Core (<c>Tera9RNG.IsMatchStarChoice</c>, <c>EncounterDist9.GetIsPossibleSlot</c>) y se COMPRUEBA siempre contra <c>CanBeEncountered</c> del propio Core
/// (<see cref="Verify"/>) antes de usarla: si no coincide para alguna semilla de la muestra, el precalculo se niega a usar el selector.
/// </summary>
internal abstract class UniverseSelector
{
    /// <summary>Tamano maximo de la lista de modelos devuelta por <see cref="Select"/>.</summary>
    public const int MaxOwners = 256;

    /// <summary>Escribe en <paramref name="owners"/> los indices (del grupo) de los modelos que pueden producir la semilla y devuelve cuantos son.</summary>
    public abstract int Select(uint seed, Span<int> owners);

    /// <summary>
    /// Comprueba el selector contra el Core con <paramref name="samples"/> semillas pseudoaleatorias (y los extremos del dominio): para cada una, el conjunto de plantillas elegido debe ser EXACTAMENTE el de las que
    /// <c>CanBeEncountered</c> acepta. Lanza <see cref="InvalidOperationException"/> ante la primera discrepancia.
    /// </summary>
    public void Verify(IReadOnlyList<ITeraRaid9> encounters, int samples, string what)
    {
        Span<int> owners = stackalloc int[MaxOwners];
        var rng = new Random(20261006);
        var expected = new List<int>();
        for (var n = 0; n < samples + 4; n++)
        {
            var seed = n switch { 0 => 0u, 1 => 1u, 2 => uint.MaxValue, 3 => uint.MaxValue - 1, _ => (uint)rng.NextInt64(1L << 32) };
            expected.Clear();
            for (var i = 0; i < encounters.Count; i++)
                if (encounters[i].CanBeEncountered(seed)) expected.Add(i);
            var count = Select(seed, owners);
            var got = owners[..count].ToArray();
            Array.Sort(got);
            if (!got.AsSpan().SequenceEqual(expected.ToArray()))
                throw new InvalidOperationException($"El selector de {what} no coincide con el Core en la semilla {seed}: esperado [{string.Join(',', expected)}], obtenido [{string.Join(',', got)}].");
        }
    }

    /// <summary>Tirada de dificultad del Core (<c>Tera9RNG.IsValidStarRand</c>, privada): el limite inclusivo es el del propio juego.</summary>
    protected static bool IsValidStarRand(uint rand, byte stars) => stars switch
    {
        1 => rand <= 80,
        2 => rand is (<= 70 and > 20) or (> 80),
        3 => true,
        4 => rand > 30,
        5 => rand > 70,
        _ => true,
    };

    /// <summary>
    /// Tabla de la tirada -&gt; encuentros (indices dentro del grupo) para los intervalos <c>[min, min + rate)</c> de cada encuentro. Dos encuentros pueden solaparse (las distribuciones con varios grupos de
    /// estrellas comparten tiradas: <c>CanBeEncountered</c> no mira las estrellas), asi que cada valor apunta a una combinacion; la 0 es «ninguno».
    /// </summary>
    protected sealed class OwnerTable
    {
        public readonly short[] Slot;
        public readonly int[][] Combos;

        public OwnerTable(int total, IReadOnlyList<(int Min, int Rate)> ranges)
        {
            Slot = new short[Math.Max(total, 0)];
            var combos = new List<int[]> { Array.Empty<int>() };
            var index = new Dictionary<string, short>(StringComparer.Ordinal) { [""] = 0 };
            for (var v = 0; v < Slot.Length; v++)
            {
                var members = new List<int>(1);
                for (var e = 0; e < ranges.Count; e++)
                {
                    var (min, rate) = ranges[e];
                    if (min >= 0 && v >= min && v < min + rate) members.Add(e);
                }
                var key = string.Join(',', members);
                if (!index.TryGetValue(key, out var id))
                {
                    id = checked((short)combos.Count);
                    combos.Add([.. members]);
                    index[key] = id;
                }
                Slot[v] = id;
            }
            Combos = [.. combos];
        }

        public int[] At(int value) => Combos[Slot[value]];
    }
}

/// <summary>Incursiones de Teratipo normales (<see cref="EncounterTera9"/>) de la MISMA dificultad y mapa: una tirada de dificultad, una de plantilla por version.</summary>
internal sealed class Tera9GroupSelector : UniverseSelector
{
    private readonly byte _stars;
    private readonly uint _maxScarlet, _maxViolet;
    private readonly OwnerTable _ownerScarlet, _ownerViolet;
    private readonly int[][] _variants;

    /// <param name="encounters">Una por plantilla DISTINTA del grupo (todas con las mismas estrellas y mapa).</param>
    /// <param name="variants">Para cada plantilla, los indices (del grupo de modelos) de sus variantes de forma.</param>
    public Tera9GroupSelector(IReadOnlyList<EncounterTera9> encounters, int[][] variants)
    {
        _stars = encounters[0].Stars;
        var map = encounters[0].Map;
        if (encounters.Any(e => e.Stars != _stars || e.Map != map)) throw new ArgumentException("El grupo mezcla dificultades o mapas.");
        _maxScarlet = (uint)Math.Max((short)0, EncounterTera9.GetRateTotalSL(_stars, map));
        _maxViolet = (uint)Math.Max((short)0, EncounterTera9.GetRateTotalVL(_stars, map));
        _ownerScarlet = new OwnerTable((int)_maxScarlet, [.. encounters.Select(e => ((int)e.RandRateMinScarlet, (int)e.RandRate))]);
        _ownerViolet = new OwnerTable((int)_maxViolet, [.. encounters.Select(e => ((int)e.RandRateMinViolet, (int)e.RandRate))]);
        _variants = variants;
    }

    public override int Select(uint seed, Span<int> owners)
    {
        var rand = new Xoroshiro128Plus(seed);
        if (_stars < 6 && !IsValidStarRand((uint)rand.NextInt(100), _stars)) return 0;
        var n = 0;
        var first = Array.Empty<int>();
        if (_maxScarlet != 0)
        {
            var x = rand;
            first = _ownerScarlet.At((int)x.NextInt(_maxScarlet));
            foreach (var e in first) foreach (var v in _variants[e]) owners[n++] = v;
        }
        if (_maxViolet != 0)
        {
            var x = rand;
            foreach (var e in _ownerViolet.At((int)x.NextInt(_maxViolet)))
            {
                if (Array.IndexOf(first, e) >= 0) continue; // ya elegida por la tirada de Escarlata
                foreach (var v in _variants[e]) owners[n++] = v;
            }
        }
        return n;
    }
}

/// <summary>Plantillas de una MISMA distribucion (<see cref="EncounterDist9"/> con los mismos totales por fase y version): una tirada por total distinto, comun a todas.</summary>
internal sealed class Dist9GroupSelector : UniverseSelector
{
    private const int Stages = 4;
    private readonly uint[] _totals = new uint[2 * Stages];          // [version * 4 + fase]; 0 = esa fase no existe en esa version
    private readonly OwnerTable?[] _owners = new OwnerTable?[2 * Stages];
    private readonly int[][] _variants;

    /// <summary>Clave de agrupacion: dos plantillas con la misma clave comparten TODAS las tiradas.</summary>
    public static string SignatureOf(EncounterDist9 e)
        => string.Join(',', Enumerable.Range(0, Stages).Select(s => e.GetRandRateTotalScarlet(s)).Concat(Enumerable.Range(0, Stages).Select(s => e.GetRandRateTotalViolet(s))));

    public Dist9GroupSelector(IReadOnlyList<EncounterDist9> encounters, int[][] variants)
    {
        if (encounters.Select(SignatureOf).Distinct().Count() != 1) throw new ArgumentException("El grupo mezcla tiradas distintas.");
        for (var stage = 0; stage < Stages; stage++)
        {
            _totals[stage] = encounters[0].GetRandRateTotalScarlet(stage);
            _totals[Stages + stage] = encounters[0].GetRandRateTotalViolet(stage);
            _owners[stage] = _totals[stage] == 0 ? null : new OwnerTable((int)_totals[stage], [.. encounters.Select(e => ((int)e.GetRandRateMinScarlet(stage), (int)e.RandRate))]);
            _owners[Stages + stage] = _totals[Stages + stage] == 0 ? null : new OwnerTable((int)_totals[Stages + stage], [.. encounters.Select(e => ((int)e.GetRandRateMinViolet(stage), (int)e.RandRate))]);
        }
        _variants = variants;
    }

    public override int Select(uint seed, Span<int> owners)
    {
        var rand = new Xoroshiro128Plus(seed);
        _ = rand.NextInt(100);
        Span<int> seen = stackalloc int[MaxOwners];
        var seenCount = 0;
        for (var slot = 0; slot < 2 * Stages; slot++)
        {
            var total = _totals[slot];
            if (total == 0) continue;
            var x = rand;
            foreach (var owner in _owners[slot]!.At((int)x.NextInt(total)))
            {
                var dup = false;
                for (var i = 0; i < seenCount; i++) if (seen[i] == owner) { dup = true; break; }
                if (!dup) seen[seenCount++] = owner;
            }
        }
        var n = 0;
        for (var i = 0; i < seenCount; i++) foreach (var v in _variants[seen[i]]) owners[n++] = v;
        return n;
    }
}
