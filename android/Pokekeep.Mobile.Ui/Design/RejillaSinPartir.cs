// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Avalonia;
using Avalonia.Controls;

namespace Pokekeep.Mobile.Ui.Design;

/// <summary>
/// Como un <see cref="Avalonia.Controls.Primitives.UniformGrid"/> de <see cref="Columns"/> columnas (celdas del mismo ancho y todas con el alto del más alto), pero si con ese número de columnas alguna celda tendría que
/// partir una palabra por dentro («Optimiza|r») usa una columna menos, y así hasta una sola columna a todo el ancho. Con letra normal se ve igual que un <c>UniformGrid</c>; con letra grande (x1,3 a x2,0) los botones se apilan en
/// vez de partir su texto. La decisión se toma al medir, con el texto ya maquetado (<see cref="PalabrasEnteras"/>); la última fila reparte su ancho entre los que le tocan.
/// </summary>
public sealed class RejillaSinPartir : Panel
{
    public static readonly StyledProperty<int> ColumnsProperty = AvaloniaProperty.Register<RejillaSinPartir, int>(nameof(Columns), 2, validate: n => n >= 1);
    public static readonly StyledProperty<double> RowSpacingProperty = AvaloniaProperty.Register<RejillaSinPartir, double>(nameof(RowSpacing), 0);
    public static readonly StyledProperty<double> ColumnSpacingProperty = AvaloniaProperty.Register<RejillaSinPartir, double>(nameof(ColumnSpacing), 0);

    static RejillaSinPartir()
    {
        AffectsMeasure<RejillaSinPartir>(ColumnsProperty, RowSpacingProperty, ColumnSpacingProperty);
        AffectsArrange<RejillaSinPartir>(ColumnsProperty, RowSpacingProperty, ColumnSpacingProperty);
    }

    public int Columns { get => GetValue(ColumnsProperty); set => SetValue(ColumnsProperty, value); }
    public double RowSpacing { get => GetValue(RowSpacingProperty); set => SetValue(RowSpacingProperty, value); }
    public double ColumnSpacing { get => GetValue(ColumnSpacingProperty); set => SetValue(ColumnSpacingProperty, value); }

    private int _columnas = 1;
    private double _altoCelda;

    private List<Control> Visibles() => Children.Where(c => c.IsVisible).ToList();

    /// <summary>Cuántos hijos caen en cada fila con <paramref name="columnas"/> columnas (las primeras filas llenas y la última con el resto).</summary>
    private static List<int> Filas(int n, int columnas)
    {
        var r = new List<int>();
        for (var i = 0; i < n; i += columnas) r.Add(Math.Min(columnas, n - i));
        return r;
    }

    /// <summary>Ancho de una celda: con las columnas pedidas, 1/Columns del ancho como el UniformGrid (con menos hijos en la última fila quedan celdas vacías); con menos columnas que las pedidas, la fila reparte su ancho entre los que tiene.</summary>
    private double AnchoCelda(double ancho, int columnas, int enFila)
    {
        var reparto = columnas == Columns ? columnas : enFila;
        return Math.Max(0, (ancho - ColumnSpacing * (reparto - 1)) / reparto);
    }

    protected override Size MeasureOverride(Size disponible)
    {
        var hijos = Visibles();
        if (hijos.Count == 0) { foreach (var c in Children) c.Measure(default); return default; }
        var maximo = Columns;   // como el UniformGrid: con menos hijos que columnas las celdas siguen siendo de 1/Columns del ancho
        if (double.IsInfinity(disponible.Width))
        {
            // Sin límite de ancho (dentro de un desplazamiento horizontal): como un UniformGrid, celdas del ancho del más ancho.
            foreach (var c in hijos) c.Measure(Size.Infinity);
            _columnas = maximo;
            var w = hijos.Max(c => c.DesiredSize.Width); _altoCelda = hijos.Max(c => c.DesiredSize.Height);
            var filasInf = Filas(hijos.Count, _columnas).Count;

            return new Size(w * _columnas + ColumnSpacing * (_columnas - 1), _altoCelda * filasInf + RowSpacing * (filasInf - 1));
        }
        for (var columnas = maximo; columnas >= 1; columnas--)
        {
            var filas = Filas(hijos.Count, columnas);
            var i = 0;
            foreach (var enFila in filas)
            {
                var ancho = AnchoCelda(disponible.Width, columnas, enFila);
                for (var k = 0; k < enFila; k++) hijos[i++].Measure(new Size(ancho, double.PositiveInfinity));
            }
            _columnas = columnas;
            if (columnas == 1 || !hijos.Any(PalabrasEnteras.HayPalabraPartida)) break;
        }
        _altoCelda = hijos.Max(c => c.DesiredSize.Height);
        var nFilas = Filas(hijos.Count, _columnas).Count;
        return new Size(disponible.Width, _altoCelda * nFilas + RowSpacing * (nFilas - 1));
    }

    protected override Size ArrangeOverride(Size final)
    {
        var hijos = Visibles();
        foreach (var c in Children) if (!c.IsVisible) c.Arrange(default);
        if (hijos.Count == 0) return final;
        var i = 0; var y = 0.0;
        foreach (var enFila in Filas(hijos.Count, _columnas))
        {
            var ancho = AnchoCelda(final.Width, _columnas, enFila);
            for (var k = 0; k < enFila; k++) hijos[i++].Arrange(new Rect(k * (ancho + ColumnSpacing), y, ancho, _altoCelda));
            y += _altoCelda + RowSpacing;
        }
        return final;
    }
}
