// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace AutoLegalidad.Engine.SolutionCache;

/// <summary>
/// Contenido binario de una entrada UNIVERSAL de encuentro (ADR 0041, <see cref="SolutionFamily.EncounterUniverse"/>): para cada casilla de rasgos (naturaleza, sexo, habilidad, Teratipo...),
/// la suma de IV maxima del encuentro con esos rasgos y las mejores semillas (hasta 3, de mejor a peor en el orden del motor: suma, relevancia de los stats, semilla). Formato, little-endian:
/// <c>u8 version(=1) | u16 numero de casillas | por casilla: u32 casilla | u8 suma | u8 n (1..3) | n x u32 semilla</c>, casillas ordenadas por valor. Las semillas del dominio de estos modelos caben en 32 bits.
/// </summary>
internal static class UniversePayload
{
    public const byte Version = 1;
    public const int MaxSeedsPerBucket = 3;

    internal sealed record Bucket(uint Key, int Sum, uint[] Seeds);

    public static byte[] Encode(IReadOnlyCollection<Bucket> buckets)
    {
        var ordered = buckets.OrderBy(b => b.Key).ToList();
        using var ms = new MemoryStream(3 + ordered.Count * 12);
        using var w = new BinaryWriter(ms);
        w.Write(Version);
        w.Write((ushort)ordered.Count);
        foreach (var b in ordered)
        {
            w.Write(b.Key);
            w.Write((byte)b.Sum);
            w.Write((byte)b.Seeds.Length);
            foreach (var s in b.Seeds) w.Write(s);
        }
        w.Flush();
        return ms.ToArray();
    }

    /// <summary>Decodifica; <c>null</c> si el contenido esta truncado, tiene otra version o es incoherente (se trata como ausente: el motor busca).</summary>
    public static Dictionary<uint, Bucket>? Decode(byte[] blob)
    {
        try
        {
            using var ms = new MemoryStream(blob, writable: false);
            using var r = new BinaryReader(ms);
            if (r.ReadByte() != Version) return null;
            var count = r.ReadUInt16();
            var map = new Dictionary<uint, Bucket>(count);
            for (var i = 0; i < count; i++)
            {
                var key = r.ReadUInt32();
                var sum = r.ReadByte();
                var n = r.ReadByte();
                if (n is 0 or > MaxSeedsPerBucket || sum > 186) return null;
                var seeds = new uint[n];
                for (var j = 0; j < n; j++) seeds[j] = r.ReadUInt32();
                if (!map.TryAdd(key, new Bucket(key, sum, seeds))) return null;
            }
            return ms.Position == ms.Length ? map : null;
        }
        catch (Exception ex) when (ex is EndOfStreamException or IOException) { return null; }
    }
}
