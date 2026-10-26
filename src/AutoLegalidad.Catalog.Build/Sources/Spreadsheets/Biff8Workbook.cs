// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace AutoLegalidad.Catalog.Sources.Spreadsheets;

/// <summary>
/// Lector de solo lectura de libros BIFF8 (.xls de Excel 97-2003) para extraer los valores de una hoja: contenedor CFB, cifrado RC4 estandar de Office («solo lectura recomendada»:
/// contrasena por defecto <c>VelvetSweatshop</c>, [MS-OFFCRYPTO] 2.3.6 / [MS-XLS] 2.2.10), tabla de cadenas compartidas (SST + CONTINUE) y celdas
/// (LABELSST, NUMBER, RK, MULRK y el resultado en cache de FORMULA). No interpreta formatos ni formulas: lo que Excel dejo calculado.
/// </summary>
public sealed class Biff8Workbook
{
    public const string DefaultPassword = "VelvetSweatshop";

    private const ushort Bof = 0x0809, Eof = 0x000A, FilePass = 0x002F, BoundSheet = 0x0085, Sst = 0x00FC, Continue = 0x003C;
    private const ushort LabelSst = 0x00FD, Number = 0x0203, Rk = 0x027E, MulRk = 0x00BD, Formula = 0x0006, StringRec = 0x0207, Label = 0x0204;
    private static readonly HashSet<ushort> PlainRecords = [0x0809, 0x002F, 0x0194, 0x0195, 0x00E1, 0x0196, 0x0138]; // BOF, FILEPASS, USREXCL, FILELOCK, INTERFACEHDR, RRDINFO, RRDHEAD

    private readonly byte[] _d;
    private readonly List<string> _sst = [];
    private readonly Dictionary<string, int> _sheetOffsets = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> SheetNames => _sheetOffsets.Keys;

    private Biff8Workbook(byte[] stream)
    {
        _d = stream;
        Decrypt();
        ParseGlobals();
    }

    public static Biff8Workbook Open(byte[] xlsFile)
    {
        var s = CompoundFile.ReadStream(xlsFile, "Workbook") ?? CompoundFile.ReadStream(xlsFile, "Book") ?? throw new InvalidDataException("El .xls no tiene flujo «Workbook».");
        return new Biff8Workbook(s);
    }

    // ---------------------------------------------------------------- cifrado

    private void Decrypt()
    {
        // FILEPASS en el bloque global (antes del primer EOF).
        var p = 0;
        var depth = 0;
        var filePassAt = -1;
        while (p + 4 <= _d.Length)
        {
            var id = BinaryPrimitives.ReadUInt16LittleEndian(_d.AsSpan(p));
            var len = BinaryPrimitives.ReadUInt16LittleEndian(_d.AsSpan(p + 2));
            if (id == Bof) depth++;
            if (id == FilePass) { filePassAt = p; break; }
            if (id == Eof && --depth <= 0) break;
            p += 4 + len;
        }
        if (filePassAt < 0) return;

        var q = filePassAt + 4;
        var type = BinaryPrimitives.ReadUInt16LittleEndian(_d.AsSpan(q));
        if (type != 1) throw new NotSupportedException("Cifrado XOR de .xls no soportado.");
        var major = BinaryPrimitives.ReadUInt16LittleEndian(_d.AsSpan(q + 2));
        var minor = BinaryPrimitives.ReadUInt16LittleEndian(_d.AsSpan(q + 4));
        if (major != 1 || minor != 1) throw new NotSupportedException($"RC4 {major}.{minor} (CryptoAPI) no soportado.");
        var salt = _d.AsSpan(q + 6, 16).ToArray();
        var encVerifier = _d.AsSpan(q + 22, 16).ToArray();
        var encVerifierHash = _d.AsSpan(q + 38, 16).ToArray();

        var h0 = MD5.HashData(Encoding.Unicode.GetBytes(DefaultPassword));
        var h1 = MD5.HashData(Repeat([.. h0.AsSpan(0, 5), .. salt], 16));
        byte[] BlockKey(uint block)
        {
            Span<byte> buf = stackalloc byte[9];
            h1.AsSpan(0, 5).CopyTo(buf);
            BinaryPrimitives.WriteUInt32LittleEndian(buf[5..], block);
            return MD5.HashData(buf);
        }

        // Comprobacion de la contrasena: se descifra verificador y hash con el mismo flujo del bloque 0.
        var check = Rc4(BlockKey(0), 0, [.. encVerifier, .. encVerifierHash]);
        if (!MD5.HashData(check.AsSpan(0, 16)).AsSpan().SequenceEqual(check.AsSpan(16, 16)))
            throw new InvalidDataException("La contrasena por defecto de solo lectura no abre este .xls.");

        // Descifrado: bloques de 1024 bytes sobre la posicion ABSOLUTA en el flujo (las cabeceras de registro cuentan, pero no se descifran).
        var keystream = new byte[1024];
        long loadedBlock = -1;
        void Xor(int pos, int len)
        {
            for (var i = 0; i < len; i++)
            {
                var abs = pos + i;
                var block = abs / 1024;
                if (block != loadedBlock)
                {
                    keystream = Rc4(BlockKey((uint)block), 0, new byte[1024]);
                    loadedBlock = block;
                }
                _d[abs] ^= keystream[abs % 1024];
            }
        }

        p = filePassAt + 4 + BinaryPrimitives.ReadUInt16LittleEndian(_d.AsSpan(filePassAt + 2));
        // El propio FILEPASS queda como esta; el resto de registros sensibles se descifra.
        while (p + 4 <= _d.Length)
        {
            var id = BinaryPrimitives.ReadUInt16LittleEndian(_d.AsSpan(p));
            var len = BinaryPrimitives.ReadUInt16LittleEndian(_d.AsSpan(p + 2));
            if (p + 4 + len > _d.Length) break;
            if (!PlainRecords.Contains(id))
            {
                var skip = id == BoundSheet ? 4 : 0; // lbPlyPos no se cifra
                Xor(p + 4 + skip, len - skip);
            }
            p += 4 + len;
        }
    }

    private static byte[] Repeat(byte[] unit, int times)
    {
        var r = new byte[unit.Length * times];
        for (var i = 0; i < times; i++) unit.CopyTo(r, i * unit.Length);
        return r;
    }

    /// <summary>RC4 (descifra/cifra): KSA + PRGA, descartando <paramref name="skip"/> bytes de flujo previos.</summary>
    private static byte[] Rc4(byte[] key, int skip, byte[] data)
    {
        var s = new byte[256];
        for (var i = 0; i < 256; i++) s[i] = (byte)i;
        for (int i = 0, j = 0; i < 256; i++)
        {
            j = (j + s[i] + key[i % key.Length]) & 0xFF;
            (s[i], s[j]) = (s[j], s[i]);
        }
        var o = new byte[data.Length];
        int a = 0, b = 0;
        for (var n = -skip; n < data.Length; n++)
        {
            a = (a + 1) & 0xFF;
            b = (b + s[a]) & 0xFF;
            (s[a], s[b]) = (s[b], s[a]);
            var k = s[(s[a] + s[b]) & 0xFF];
            if (n >= 0) o[n] = (byte)(data[n] ^ k);
        }
        return o;
    }

    // ---------------------------------------------------------------- globales

    private void ParseGlobals()
    {
        var p = 0;
        var depth = 0;
        while (p + 4 <= _d.Length)
        {
            var id = BinaryPrimitives.ReadUInt16LittleEndian(_d.AsSpan(p));
            var len = BinaryPrimitives.ReadUInt16LittleEndian(_d.AsSpan(p + 2));
            if (p + 4 + len > _d.Length) break;
            if (id == Bof) depth++;
            if (id == BoundSheet)
            {
                var pos = BinaryPrimitives.ReadUInt32LittleEndian(_d.AsSpan(p + 4));
                var cch = _d[p + 4 + 6];
                var flags = _d[p + 4 + 7];
                var name = flags == 0 ? Encoding.Latin1.GetString(_d, p + 4 + 8, cch) : Encoding.Unicode.GetString(_d, p + 4 + 8, cch * 2);
                _sheetOffsets[name] = (int)pos;
            }
            else if (id == Sst)
            {
                var segs = new List<(int Off, int Len)> { (p + 4, len) };
                var n = p + 4 + len;
                while (n + 4 <= _d.Length && BinaryPrimitives.ReadUInt16LittleEndian(_d.AsSpan(n)) == Continue)
                {
                    var l2 = BinaryPrimitives.ReadUInt16LittleEndian(_d.AsSpan(n + 2));
                    segs.Add((n + 4, l2));
                    n += 4 + l2;
                }
                ParseSst(segs);
                p = n;
                continue;
            }
            if (id == Eof && --depth <= 0) break;
            p += 4 + len;
        }
    }

    private void ParseSst(List<(int Off, int Len)> segs)
    {
        var seg = 0;
        var pos = 0;
        bool Next() { if (seg + 1 >= segs.Count) return false; seg++; pos = 0; return true; }
        bool Ensure() { while (pos >= segs[seg].Len) if (!Next()) return false; return true; }
        byte U8() { Ensure(); return _d[segs[seg].Off + pos++]; }
        ushort U16() { Ensure(); var v = BinaryPrimitives.ReadUInt16LittleEndian(_d.AsSpan(segs[seg].Off + pos)); pos += 2; return v; }
        uint U32() { Ensure(); var v = BinaryPrimitives.ReadUInt32LittleEndian(_d.AsSpan(segs[seg].Off + pos)); pos += 4; return v; }
        void Skip(long n)
        {
            while (n > 0)
            {
                if (!Ensure()) return;
                var avail = segs[seg].Len - pos;
                var step = (int)Math.Min(n, avail);
                pos += step; n -= step;
            }
        }

        U32();                     // total de referencias
        var unique = (int)U32();
        for (var i = 0; i < unique; i++)
        {
            if (!Ensure()) break;
            int cch = U16();
            var flags = U8();
            var high = (flags & 1) != 0;
            var runs = (flags & 8) != 0 ? U16() : 0;
            var extLen = (flags & 4) != 0 ? U32() : 0;
            var sb = new StringBuilder(cch);
            var remaining = cch;
            while (remaining > 0)
            {
                if (pos >= segs[seg].Len)
                {
                    if (!Next()) break;
                    high = (U8() & 1) != 0;   // cada CONTINUE que sigue partiendo una cadena repite el indicador de ancho
                    continue;
                }
                var per = high ? 2 : 1;
                var take = Math.Min(remaining, (segs[seg].Len - pos) / per);
                if (take == 0) { pos = segs[seg].Len; continue; }
                var off = segs[seg].Off + pos;
                sb.Append(high ? Encoding.Unicode.GetString(_d, off, take * 2) : Encoding.Latin1.GetString(_d, off, take));
                pos += take * per;
                remaining -= take;
            }
            Skip(runs * 4L + extLen);
            _sst.Add(sb.ToString());
        }
    }

    // ---------------------------------------------------------------- hojas

    public SheetGrid ReadSheet(string name)
    {
        if (!_sheetOffsets.TryGetValue(name, out var start)) throw new KeyNotFoundException($"Hoja «{name}» no encontrada.");
        var grid = new SheetGrid();
        var p = start;
        var depth = 0;
        (int Row, int Col)? pendingFormula = null;
        while (p + 4 <= _d.Length)
        {
            var id = BinaryPrimitives.ReadUInt16LittleEndian(_d.AsSpan(p));
            var len = BinaryPrimitives.ReadUInt16LittleEndian(_d.AsSpan(p + 2));
            if (p + 4 + len > _d.Length) break;
            var r = _d.AsSpan(p + 4, len);
            switch (id)
            {
                case Bof: depth++; break;
                case Eof: depth--; break;
                case LabelSst:
                {
                    var idx = (int)BinaryPrimitives.ReadUInt32LittleEndian(r[6..]);
                    if (idx < _sst.Count) grid.Set(BinaryPrimitives.ReadUInt16LittleEndian(r), BinaryPrimitives.ReadUInt16LittleEndian(r[2..]), _sst[idx]);
                    break;
                }
                case Number:
                    grid.Set(BinaryPrimitives.ReadUInt16LittleEndian(r), BinaryPrimitives.ReadUInt16LittleEndian(r[2..]), BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(r[6..])).ToString("R", CultureInfo.InvariantCulture));
                    break;
                case Rk:
                    grid.Set(BinaryPrimitives.ReadUInt16LittleEndian(r), BinaryPrimitives.ReadUInt16LittleEndian(r[2..]), DecodeRk(BinaryPrimitives.ReadUInt32LittleEndian(r[6..])).ToString("R", CultureInfo.InvariantCulture));
                    break;
                case MulRk:
                {
                    var row = BinaryPrimitives.ReadUInt16LittleEndian(r);
                    var first = BinaryPrimitives.ReadUInt16LittleEndian(r[2..]);
                    var n = (len - 6) / 6;
                    for (var i = 0; i < n; i++)
                        grid.Set(row, first + i, DecodeRk(BinaryPrimitives.ReadUInt32LittleEndian(r[(4 + 6 * i + 2)..])).ToString("R", CultureInfo.InvariantCulture));
                    break;
                }
                case Label: // BIFF8 antiguo: cadena inline
                {
                    int cch = BinaryPrimitives.ReadUInt16LittleEndian(r[6..]);
                    var flags = r[8];
                    grid.Set(BinaryPrimitives.ReadUInt16LittleEndian(r), BinaryPrimitives.ReadUInt16LittleEndian(r[2..]), (flags & 1) == 0 ? Encoding.Latin1.GetString(r.Slice(9, cch)) : Encoding.Unicode.GetString(r.Slice(9, cch * 2)));
                    break;
                }
                case Formula:
                {
                    var row = BinaryPrimitives.ReadUInt16LittleEndian(r);
                    var col = BinaryPrimitives.ReadUInt16LittleEndian(r[2..]);
                    if (r[12] == 0xFF && r[13] == 0xFF)
                    {
                        if (r[6] == 0) pendingFormula = (row, col);                       // texto: llega en el STRING que sigue
                        else if (r[6] == 1) grid.Set(row, col, r[8] != 0 ? "TRUE" : "FALSE");
                    }
                    else grid.Set(row, col, BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(r[6..])).ToString("R", CultureInfo.InvariantCulture));
                    break;
                }
                case StringRec when pendingFormula is { } cell:
                {
                    int cch = BinaryPrimitives.ReadUInt16LittleEndian(r);
                    var flags = r[2];
                    grid.Set(cell.Row, cell.Col, (flags & 1) == 0 ? Encoding.Latin1.GetString(r.Slice(3, Math.Min(cch, len - 3))) : Encoding.Unicode.GetString(r.Slice(3, Math.Min(cch * 2, len - 3))));
                    pendingFormula = null;
                    break;
                }
            }
            p += 4 + len;
            if (depth <= 0) break;
        }
        return grid;
    }

    private static double DecodeRk(uint rk)
    {
        double v;
        if ((rk & 2) != 0) v = (int)rk >> 2;
        else v = BitConverter.Int64BitsToDouble((long)(rk & 0xFFFFFFFC) << 32);
        return (rk & 1) != 0 ? v / 100.0 : v;
    }
}
