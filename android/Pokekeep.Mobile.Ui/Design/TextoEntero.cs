// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Pokekeep.Mobile.Ui.Design;

/// <summary>
/// Un <see cref="TextBlock"/> que nunca parte una palabra por dentro: si la palabra más larga no cabe en el ancho que le dan, reduce la letra lo justo para que quepa (como mucho hasta <see cref="FraccionMinima"/> del tamaño que
/// pidió el estilo y nunca por debajo de 10 dp: la misma regla que la barra de destinos). Es el último recurso para una sola palabra larga en una columna que no se puede ensanchar («Superentrenamiento» a x2,0 en 360 dp); lo normal
/// es dar el ancho que falta (<see cref="EtiquetaYValor"/>, <see cref="RejillaSinPartir"/>). El texto sigue pudiendo bajar de línea entre palabras.
/// </summary>
public sealed class TextoEntero : TextBlock
{
    public static readonly StyledProperty<double> FraccionMinimaProperty = AvaloniaProperty.Register<TextoEntero, double>(nameof(FraccionMinima), 0.6, validate: n => n > 0 && n <= 1);

    /// <summary>Fracción del tamaño pedido por debajo de la cual ya no se encoge (y entonces la palabra se parte).</summary>
    public double FraccionMinima { get => GetValue(FraccionMinimaProperty); set => SetValue(FraccionMinimaProperty, value); }

    /// <summary>Letra efectiva mínima legible, en dp.</summary>
    public const double LetraMinima = 10;

    protected override Type StyleKeyOverride => typeof(TextBlock);

    private double _preferido = double.NaN;
    private bool _ajustando;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        // El tamaño que pide el estilo o el XAML es el de referencia; lo que se pone aquí al medir no.
        if (change.Property == FontSizeProperty && !_ajustando && change.NewValue is double d) _preferido = d;
        base.OnPropertyChanged(change);
    }

    protected override Size MeasureOverride(Size disponible)
    {
        if (double.IsNaN(_preferido)) _preferido = FontSize;
        var texto = Text ?? "";
        var ancho = disponible.Width - Padding.Left - Padding.Right;
        var tamano = _preferido;
        if (texto.Length > 0 && !double.IsInfinity(ancho) && ancho > 0)
        {
            var palabra = PalabraMasAncha(texto, _preferido);
            if (palabra > ancho + 0.01)
            {
                var minimo = Math.Min(_preferido, Math.Max(LetraMinima, _preferido * FraccionMinima));
                tamano = Math.Max(minimo, _preferido * ancho / palabra);
            }
        }
        if (Math.Abs(FontSize - tamano) > 0.001)
        {
            _ajustando = true;
            try { SetCurrentValue(FontSizeProperty, tamano); }
            finally { _ajustando = false; }
        }
        return base.MeasureOverride(disponible);
    }

    private double PalabraMasAncha(string texto, double tamano)
    {
        var tipo = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
        var maximo = 0.0;
        foreach (var palabra in texto.Split([' ', '\n', '\r', '\t', '-'], StringSplitOptions.RemoveEmptyEntries))
        {
            var f = new FormattedText(palabra, CultureInfo.CurrentCulture, FlowDirection, tipo, tamano, Foreground);
            maximo = Math.Max(maximo, f.Width);
        }
        return maximo;
    }
}
