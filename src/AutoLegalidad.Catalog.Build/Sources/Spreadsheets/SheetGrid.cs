// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Globalization;

namespace AutoLegalidad.Catalog.Sources.Spreadsheets;

/// <summary>Valores (texto o numero ya convertido a texto invariante) de una hoja, por fila y columna de base 0. Solo lo leido: nunca se escribe ni se redistribuye.</summary>
public sealed class SheetGrid
{
    private readonly Dictionary<(int Row, int Col), string> _cells = [];

    public int RowCount { get; private set; }

    public void Set(int row, int col, string value)
    {
        _cells[(row, col)] = value;
        if (row + 1 > RowCount) RowCount = row + 1;
    }

    public string Get(int row, int col) => _cells.TryGetValue((row, col), out var v) ? v : string.Empty;

    /// <summary>Primera celda (fila, columna) cuyo texto recortado coincide con <paramref name="text"/> (sin distinguir mayusculas), o <c>null</c>.</summary>
    public (int Row, int Col)? Find(string text)
    {
        (int, int)? best = null;
        foreach (var (k, v) in _cells)
            if (v.Trim().Equals(text, StringComparison.OrdinalIgnoreCase) && (best is null || k.Row < best.Value.Item1 || (k.Row == best.Value.Item1 && k.Col < best.Value.Item2)))
                best = (k.Row, k.Col);
        return best;
    }

    /// <summary>Numero de la celda si su texto es un numero invariante.</summary>
    public double? Number(int row, int col)
        => double.TryParse(Get(row, col), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;
}
