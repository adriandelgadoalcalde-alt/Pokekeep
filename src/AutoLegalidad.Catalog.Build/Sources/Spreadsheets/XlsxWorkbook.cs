// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.IO.Compression;
using System.Xml.Linq;

namespace AutoLegalidad.Catalog.Sources.Spreadsheets;

/// <summary>Lector de solo lectura de libros .xlsx (zip + XML): cadenas compartidas y el valor en cache de cada celda. Las fechas llegan como numero de serie.</summary>
public static class XlsxWorkbook
{
    private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRel = "http://schemas.openxmlformats.org/package/2006/relationships";

    public static IReadOnlyList<string> SheetNames(byte[] file)
    {
        using var zip = new ZipArchive(new MemoryStream(file), ZipArchiveMode.Read);
        return [.. Load(zip, "xl/workbook.xml").Descendants(Main + "sheet").Select(s => (string)s.Attribute("name")!)];
    }

    public static SheetGrid ReadSheet(byte[] file, string sheetName)
    {
        using var zip = new ZipArchive(new MemoryStream(file), ZipArchiveMode.Read);
        var sheet = Load(zip, "xl/workbook.xml").Descendants(Main + "sheet").FirstOrDefault(s => string.Equals((string?)s.Attribute("name"), sheetName, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException($"Hoja «{sheetName}» no encontrada.");
        var rid = (string)sheet.Attribute(Rel + "id")!;
        var target = Load(zip, "xl/_rels/workbook.xml.rels").Descendants(PackageRel + "Relationship").First(r => (string?)r.Attribute("Id") == rid).Attribute("Target")!.Value;
        var path = target.StartsWith('/') ? target[1..] : "xl/" + target;

        var shared = new List<string>();
        if (zip.GetEntry("xl/sharedStrings.xml") is not null)
            foreach (var si in Load(zip, "xl/sharedStrings.xml").Descendants(Main + "si"))
                shared.Add(string.Concat(si.Descendants(Main + "t").Select(t => t.Value)));

        var grid = new SheetGrid();
        foreach (var c in Load(zip, path).Descendants(Main + "c"))
        {
            var r = (string?)c.Attribute("r");
            if (r is null) continue;
            var (row, col) = ParseRef(r);
            var type = (string?)c.Attribute("t");
            string? value;
            if (type == "inlineStr") value = string.Concat(c.Descendants(Main + "t").Select(t => t.Value));
            else
            {
                value = c.Element(Main + "v")?.Value;
                if (value is not null && type == "s") value = int.TryParse(value, out var i) && i < shared.Count ? shared[i] : null;
            }
            if (value is not null) grid.Set(row, col, value);
        }
        return grid;
    }

    private static XDocument Load(ZipArchive zip, string path)
    {
        var e = zip.GetEntry(path) ?? throw new FileNotFoundException($"Falta {path} en el .xlsx.");
        using var s = e.Open();
        return XDocument.Load(s);
    }

    private static (int Row, int Col) ParseRef(string r)
    {
        var col = 0;
        var i = 0;
        for (; i < r.Length && char.IsLetter(r[i]); i++) col = col * 26 + (char.ToUpperInvariant(r[i]) - 'A' + 1);
        return (int.Parse(r[i..], System.Globalization.CultureInfo.InvariantCulture) - 1, col - 1);
    }

    /// <summary>Fecha de un numero de serie de Excel (sistema 1900).</summary>
    public static DateOnly? FromSerial(double serial)
        => serial is > 1 and < 80000 ? DateOnly.FromDateTime(new DateTime(1899, 12, 30).AddDays(Math.Floor(serial))) : null;
}
