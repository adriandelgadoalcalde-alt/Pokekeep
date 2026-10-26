// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Buffers.Binary;
using System.Text.Json;

namespace AutoLegalidad.Engine.SolutionCache;

/// <summary>Formato de la columna <c>payload</c> de <c>soluciones.sqlite</c> (ADR 0097).</summary>
public enum SolutionPayloadFormat
{
    /// <summary>v1 (ADR 0037): JSON de texto <c>{"v":1,"seeds":[..],"ivs":[..],"bin":"base64"}</c>. Columna <c>TEXT</c>.</summary>
    V1Json = 1,

    /// <summary>v2 (ADR 0097): binario con versión, sin base 64, con las casillas universales por columnas. Columna <c>BLOB</c>.</summary>
    V2Binary = 2,
}

/// <summary>Contenido ilegible de una fila de la caché: truncado, corrupto, con versión desconocida o incoherente. Nunca se convierte en un resultado: el lector lo cuenta y lo dice.</summary>
public sealed class SolutionPayloadException(string message) : FormatException(message);

/// <summary>Lo que guarda la columna <c>payload</c>: semillas, vector de IV (familia LCRNG) y contenido binario universal.</summary>
internal sealed record DecodedPayload(ulong[] Seeds, int[]? Ivs, byte[]? Blob)
{
    public bool SameAs(DecodedPayload other)
        => Seeds.AsSpan().SequenceEqual(other.Seeds)
           && (Ivs is null ? other.Ivs is null : other.Ivs is not null && Ivs.AsSpan().SequenceEqual(other.Ivs))
           && (Blob is null ? other.Blob is null : other.Blob is not null && Blob.AsSpan().SequenceEqual(other.Blob));
}

/// <summary>
/// Codec de la columna <c>payload</c>. v1 = JSON (ADR 0037). v2 (ADR 0097), binario, sin pérdida:
/// <c>u8 versión(=2) | u8 banderas | [semillas] | [IV] | [contenido universal]</c>, enteros en varint LEB128 (los IV en zigzag).
/// Banderas: 1 = semillas (<c>varint n | n x varint u64</c>), 2 = IV (<c>varint n | n x zigzag</c>), 4 = universal por columnas, 8 = contenido en bruto (<c>varint largo | bytes</c>); 4 y 8 son excluyentes.
/// Universal por columnas (el <see cref="UniversePayload"/> v1 de <c>n</c> casillas, 1..3 semillas cada una, claves estrictamente crecientes):
/// <c>varint n | n x varint (clave - clave anterior) | n x u8 suma | ceil(n/4) x u8 (2 bits por casilla: semillas-1, de la primera a la última, de menos a más significativo) | todas las semillas u32 LE, casilla a casilla</c>.
/// Un contenido que no cumpla eso EXACTAMENTE se guarda en bruto (bandera 8): la ida y vuelta es siempre byte a byte.
/// </summary>
internal static class SolutionPayloadCodec
{
    public const byte V2Version = 2;
    private const byte FlagSeeds = 1, FlagIvs = 2, FlagColumns = 4, FlagRaw = 8, KnownFlags = 15;

    // ------------------------------------------------------------------------------------------------------------------ v1
    public static string EncodeV1(ulong[] seeds, int[]? ivs, byte[]? blob)
        => JsonSerializer.Serialize(new V1Payload { V = 1, Seeds = seeds.Length == 0 ? null : seeds, Ivs = ivs, Bin = blob is null ? null : Convert.ToBase64String(blob) });

    public static DecodedPayload DecodeV1(string json)
    {
        V1Payload? p;
        try { p = JsonSerializer.Deserialize<V1Payload>(json); }
        catch (JsonException ex) { throw new SolutionPayloadException("contenido v1 (JSON) ilegible: " + ex.Message); }
        if (p is null) throw new SolutionPayloadException("contenido v1 (JSON) vacío");
        if (p.V != 1) throw new SolutionPayloadException($"versión desconocida del contenido JSON: {p.V}");
        byte[]? blob;
        try { blob = p.Bin is null ? null : Convert.FromBase64String(p.Bin); }
        catch (FormatException) { throw new SolutionPayloadException("contenido v1: el campo bin no es base 64 válido"); }
        return new DecodedPayload(p.Seeds ?? [], p.Ivs, blob);
    }

    // ------------------------------------------------------------------------------------------------------------------ v2
    public static byte[] EncodeV2(ulong[] seeds, int[]? ivs, byte[]? blob)
    {
        var ms = new MemoryStream(32 + (blob?.Length ?? 0));
        var flags = (byte)((seeds.Length > 0 ? FlagSeeds : 0) | (ivs is not null ? FlagIvs : 0));
        var columns = blob is not null && TryColumns(blob);
        if (blob is not null) flags |= columns ? FlagColumns : FlagRaw;
        ms.WriteByte(V2Version);
        ms.WriteByte(flags);
        if ((flags & FlagSeeds) != 0)
        {
            WriteVarint(ms, (ulong)seeds.Length);
            foreach (var s in seeds) WriteVarint(ms, s);
        }
        if (ivs is not null)
        {
            WriteVarint(ms, (ulong)ivs.Length);
            foreach (var v in ivs) WriteVarint(ms, ZigZag(v));
        }
        if (blob is not null)
        {
            if (columns) WriteColumns(ms, blob);
            else
            {
                WriteVarint(ms, (ulong)blob.Length);
                ms.Write(blob);
            }
        }
        return ms.ToArray();
    }

    public static DecodedPayload DecodeV2(ReadOnlySpan<byte> data)
    {
        if (data.Length < 2) throw new SolutionPayloadException($"contenido v2 truncado ({data.Length} bytes)");
        if (data[0] != V2Version) throw new SolutionPayloadException($"versión desconocida del contenido binario: {data[0]} (este motor lee la 2)");
        var flags = data[1];
        if ((flags & ~KnownFlags) != 0) throw new SolutionPayloadException($"contenido v2 con banderas desconocidas (0x{flags:X2})");
        if ((flags & FlagColumns) != 0 && (flags & FlagRaw) != 0) throw new SolutionPayloadException("contenido v2 incoherente: universal por columnas y en bruto a la vez");
        var pos = 2;
        ulong[] seeds = [];
        int[]? ivs = null;
        byte[]? blob = null;
        if ((flags & FlagSeeds) != 0)
        {
            var n = ReadCount(data, ref pos, 1);
            if (n == 0) throw new SolutionPayloadException("contenido v2 incoherente: bandera de semillas con cero semillas");
            seeds = new ulong[n];
            for (var i = 0; i < n; i++) seeds[i] = ReadVarint(data, ref pos);
        }
        if ((flags & FlagIvs) != 0)
        {
            var n = ReadCount(data, ref pos, 1);
            ivs = new int[n];
            for (var i = 0; i < n; i++)
            {
                var z = ReadVarint(data, ref pos);
                if (z > uint.MaxValue) throw new SolutionPayloadException("contenido v2: IV fuera de rango");
                ivs[i] = UnZigZag((uint)z);
            }
        }
        if ((flags & FlagColumns) != 0) blob = ReadColumns(data, ref pos);
        else if ((flags & FlagRaw) != 0)
        {
            var len = ReadCount(data, ref pos, 1);
            if (data.Length - pos < len) throw new SolutionPayloadException("contenido v2 truncado (contenido en bruto)");
            blob = data.Slice(pos, len).ToArray();
            pos += len;
        }
        if (pos != data.Length) throw new SolutionPayloadException($"contenido v2 con {data.Length - pos} bytes sobrantes");
        return new DecodedPayload(seeds, ivs, blob);
    }

    /// <summary>Lo que guarda la base según el tipo de valor de SQLite: texto = v1 (JSON); BLOB = binario, por su primer byte.</summary>
    public static DecodedPayload Decode(object? stored) => stored switch
    {
        string json => DecodeV1(json),
        byte[] bytes => DecodeV2(bytes),
        null or DBNull => throw new SolutionPayloadException("contenido vacío (NULL)"),
        _ => throw new SolutionPayloadException("tipo de contenido no soportado: " + stored.GetType().Name),
    };

    // ------------------------------------------------------------------------------------------------------------------ universal por columnas
    private static bool TryColumns(byte[] blob)
    {
        // Debe ser EXACTAMENTE un UniversePayload v1 con claves estrictamente crecientes; si no, va en bruto (se comprueba con el recorrido inverso en WriteColumns/ReadColumns).
        try
        {
            if (blob.Length < 3 || blob[0] != UniversePayload.Version) return false;
            var n = BinaryPrimitives.ReadUInt16LittleEndian(blob.AsSpan(1));
            var pos = 3;
            long prev = -1;
            for (var i = 0; i < n; i++)
            {
                if (blob.Length - pos < 6) return false;
                var key = BinaryPrimitives.ReadUInt32LittleEndian(blob.AsSpan(pos));
                var count = blob[pos + 5];
                if (count is 0 or > UniversePayload.MaxSeedsPerBucket || key <= prev) return false;
                prev = key;
                pos += 6 + 4 * count;
                if (pos > blob.Length) return false;
            }
            return pos == blob.Length;
        }
        catch (ArgumentOutOfRangeException) { return false; }
    }

    private static void WriteColumns(MemoryStream ms, byte[] blob)
    {
        var n = BinaryPrimitives.ReadUInt16LittleEndian(blob.AsSpan(1));
        WriteVarint(ms, n);
        var offsets = new int[n];
        var pos = 3;
        uint prev = 0;
        for (var i = 0; i < n; i++)
        {
            offsets[i] = pos;
            var key = BinaryPrimitives.ReadUInt32LittleEndian(blob.AsSpan(pos));
            WriteVarint(ms, key - prev);   // la primera es la clave misma; las demás, estrictamente > 0
            prev = key;
            pos += 6 + 4 * blob[pos + 5];
        }
        for (var i = 0; i < n; i++) ms.WriteByte(blob[offsets[i] + 4]);
        var packed = new byte[(n + 3) / 4];
        for (var i = 0; i < n; i++) packed[i / 4] |= (byte)((blob[offsets[i] + 5] - 1) << (2 * (i % 4)));
        ms.Write(packed);
        for (var i = 0; i < n; i++) ms.Write(blob.AsSpan(offsets[i] + 6, 4 * blob[offsets[i] + 5]));
    }

    private static byte[] ReadColumns(ReadOnlySpan<byte> data, ref int pos)
    {
        var n = (int)ReadVarint(data, ref pos);
        if (n < 0 || n > ushort.MaxValue) throw new SolutionPayloadException($"contenido v2: número de casillas fuera de rango ({n})");
        // Cada casilla ocupa al menos 1 (clave) + 1 (suma) + 4 (semilla) bytes: un n imposible se rechaza ANTES de reservar nada.
        if ((long)n * 6 > data.Length - pos) throw new SolutionPayloadException("contenido v2 truncado (casillas)");
        var keys = new uint[n];
        ulong prev = 0;
        for (var i = 0; i < n; i++)
        {
            var d = ReadVarint(data, ref pos);
            if (i > 0 && d == 0) throw new SolutionPayloadException("contenido v2: claves de casilla no crecientes");
            prev += d;
            if (prev > uint.MaxValue) throw new SolutionPayloadException("contenido v2: clave de casilla fuera de rango");
            keys[i] = (uint)prev;
        }
        if (data.Length - pos < n) throw new SolutionPayloadException("contenido v2 truncado (sumas)");
        var sums = data.Slice(pos, n);
        pos += n;
        var packedLen = (n + 3) / 4;
        if (data.Length - pos < packedLen) throw new SolutionPayloadException("contenido v2 truncado (cuentas de semillas)");
        var packed = data.Slice(pos, packedLen);
        pos += packedLen;
        var counts = new int[n];
        long totalSeeds = 0;
        for (var i = 0; i < n; i++)
        {
            var c = ((packed[i / 4] >> (2 * (i % 4))) & 3) + 1;
            if (c > UniversePayload.MaxSeedsPerBucket) throw new SolutionPayloadException("contenido v2: cuenta de semillas inválida");
            counts[i] = c;
            totalSeeds += c;
        }
        if ((n % 4) != 0 && (packed[^1] >> (2 * (n % 4))) != 0) throw new SolutionPayloadException("contenido v2: relleno de cuentas distinto de cero");
        if (data.Length - pos < totalSeeds * 4) throw new SolutionPayloadException("contenido v2 truncado (semillas de las casillas)");
        var blob = new byte[3 + 6 * n + 4 * (int)totalSeeds];
        blob[0] = UniversePayload.Version;
        BinaryPrimitives.WriteUInt16LittleEndian(blob.AsSpan(1), (ushort)n);
        var o = 3;
        for (var i = 0; i < n; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(blob.AsSpan(o), keys[i]);
            blob[o + 4] = sums[i];
            blob[o + 5] = (byte)counts[i];
            data.Slice(pos, 4 * counts[i]).CopyTo(blob.AsSpan(o + 6));
            pos += 4 * counts[i];
            o += 6 + 4 * counts[i];
        }
        return blob;
    }

    // ------------------------------------------------------------------------------------------------------------------ varint
    private static void WriteVarint(Stream s, ulong v)
    {
        while (v >= 0x80)
        {
            s.WriteByte((byte)(v | 0x80));
            v >>= 7;
        }
        s.WriteByte((byte)v);
    }

    private static ulong ReadVarint(ReadOnlySpan<byte> data, ref int pos)
    {
        ulong result = 0;
        for (var shift = 0; shift < 70; shift += 7)
        {
            if (pos >= data.Length) throw new SolutionPayloadException("contenido v2 truncado (entero)");
            var b = data[pos++];
            if (shift == 63 && b > 1) throw new SolutionPayloadException("contenido v2: entero fuera de rango");
            result |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0)
            {
                if (b == 0 && shift > 0) throw new SolutionPayloadException("contenido v2: entero no canónico (ceros sobrantes)");
                return result;
            }
        }
        throw new SolutionPayloadException("contenido v2: entero demasiado largo");
    }

    /// <summary>Un contador cuyos elementos ocupan al menos <paramref name="minBytesPerItem"/> bytes: si no caben en lo que queda, está corrupto (y no se reserva memoria).</summary>
    private static int ReadCount(ReadOnlySpan<byte> data, ref int pos, int minBytesPerItem)
    {
        var n = ReadVarint(data, ref pos);
        if (n > (ulong)(data.Length - pos) / (ulong)minBytesPerItem) throw new SolutionPayloadException($"contenido v2 truncado o con un contador imposible ({n})");
        return (int)n;
    }

    private static ulong ZigZag(int v) => (uint)((v << 1) ^ (v >> 31));
    private static int UnZigZag(uint z) => (int)(z >> 1) ^ -(int)(z & 1);

    private sealed class V1Payload
    {
        [System.Text.Json.Serialization.JsonPropertyName("v")] public int V { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("seeds")] public ulong[]? Seeds { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("ivs")] public int[]? Ivs { get; set; }
        /// <summary>Contenido binario (base64) de una entrada universal (ADR 0041).</summary>
        [System.Text.Json.Serialization.JsonPropertyName("bin")] public string? Bin { get; set; }
    }
}
