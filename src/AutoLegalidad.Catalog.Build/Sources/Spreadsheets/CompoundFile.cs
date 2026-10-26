// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Buffers.Binary;
using System.Text;

namespace AutoLegalidad.Catalog.Sources.Spreadsheets;

/// <summary>
/// Lector minimo del contenedor «Compound File Binary» ([MS-CFB]) de los .xls antiguos: solo lo necesario para sacar un flujo grande por nombre
/// (el libro de Excel, siempre mayor que el umbral de 4096 bytes del mini-flujo). Sin dependencias: el catalogo no admite paquetes nuevos para leer 3 hojas.
/// </summary>
public static class CompoundFile
{
    private static readonly byte[] Signature = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];
    private const uint EndOfChain = 0xFFFFFFFE;
    private const uint FreeSector = 0xFFFFFFFF;

    /// <summary>Contenido del flujo <paramref name="name"/> (sin distinguir mayusculas) o <c>null</c> si no existe.</summary>
    public static byte[]? ReadStream(byte[] file, string name)
    {
        if (file.Length < 512 || !file.AsSpan(0, 8).SequenceEqual(Signature))
            throw new InvalidDataException("No es un contenedor Compound File.");
        var sectorShift = BinaryPrimitives.ReadUInt16LittleEndian(file.AsSpan(0x1E));
        var sectorSize = 1 << sectorShift;
        var fatCount = (int)BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(0x2C));
        var firstDir = BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(0x30));
        var miniCutoff = BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(0x38));
        var firstDifat = BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(0x44));
        var difatCount = (int)BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(0x48));

        int SectorOffset(uint id) => checked((int)((id + 1L) << sectorShift));

        // Sectores de la FAT: 109 en la cabecera + cadena de DIFAT.
        var fatSectors = new List<uint>();
        for (var i = 0; i < 109 && fatSectors.Count < fatCount; i++)
        {
            var id = BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(0x4C + 4 * i));
            if (id != FreeSector) fatSectors.Add(id);
        }
        var difat = firstDifat;
        for (var n = 0; n < difatCount && difat != EndOfChain && difat != FreeSector; n++)
        {
            var off = SectorOffset(difat);
            var perSector = sectorSize / 4 - 1;
            for (var i = 0; i < perSector && fatSectors.Count < fatCount; i++)
            {
                var id = BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(off + 4 * i));
                if (id != FreeSector) fatSectors.Add(id);
            }
            difat = BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(off + 4 * perSector));
        }
        var fat = new uint[fatSectors.Count * (sectorSize / 4)];
        for (var s = 0; s < fatSectors.Count; s++)
            for (var i = 0; i < sectorSize / 4; i++)
                fat[s * (sectorSize / 4) + i] = BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(SectorOffset(fatSectors[s]) + 4 * i));

        byte[] Chain(uint start, long size)
        {
            using var ms = new MemoryStream();
            var id = start;
            var guard = 0;
            while (id != EndOfChain && id != FreeSector)
            {
                if (id >= fat.Length || guard++ > fat.Length) throw new InvalidDataException("Cadena de sectores corrupta.");
                ms.Write(file, SectorOffset(id), sectorSize);
                id = fat[id];
            }
            var all = ms.ToArray();
            return size >= 0 && size < all.Length ? all[..(int)size] : all;
        }

        var dir = Chain(firstDir, -1);
        for (var p = 0; p + 128 <= dir.Length; p += 128)
        {
            var nameLen = BinaryPrimitives.ReadUInt16LittleEndian(dir.AsSpan(p + 64));
            if (nameLen < 2 || nameLen > 64) continue;
            var entryName = Encoding.Unicode.GetString(dir, p, nameLen - 2);
            if (!entryName.Equals(name, StringComparison.OrdinalIgnoreCase)) continue;
            var start = BinaryPrimitives.ReadUInt32LittleEndian(dir.AsSpan(p + 116));
            var size = (long)BinaryPrimitives.ReadUInt64LittleEndian(dir.AsSpan(p + 120));
            if (size < miniCutoff) throw new NotSupportedException("Flujo pequeño (mini-flujo) no soportado: el libro de Excel siempre es mayor.");
            return Chain(start, size);
        }
        return null;
    }
}
