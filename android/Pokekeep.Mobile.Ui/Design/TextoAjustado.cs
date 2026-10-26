// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Pokekeep.Mobile.Ui.Design;

/// <summary>
/// Una línea completa: reduce la fuente solo si el ancho disponible lo requiere, sin transformar su caja de diseño. Con
/// <see cref="FraccionMinima"/> (uso10) no baja de esa fracción del tamaño preferido: si aun así no cabe, pasa a dos líneas a ese
/// tamaño (los nombres largos de la mochila a x2 se encogían por debajo de su propio texto secundario).
/// </summary>
public sealed class TextoAjustado : TextBlock
{
    public static readonly StyledProperty<double> TamanoPreferidoProperty =
        AvaloniaProperty.Register<TextoAjustado, double>(nameof(TamanoPreferido), 16, validate: n => n > 0 && double.IsFinite(n));

    public static readonly StyledProperty<double> FraccionMinimaProperty =
        AvaloniaProperty.Register<TextoAjustado, double>(nameof(FraccionMinima), 0, validate: n => n >= 0 && n <= 1);

    static TextoAjustado() => AffectsMeasure<TextoAjustado>(TamanoPreferidoProperty, FraccionMinimaProperty);
    protected override Type StyleKeyOverride => typeof(TextBlock);
    public double TamanoPreferido { get => GetValue(TamanoPreferidoProperty); set => SetValue(TamanoPreferidoProperty, value); }
    /// <summary>Fracción del tamaño preferido por debajo de la cual ya no se encoge y se parte en dos líneas (0: sin límite, una sola línea).</summary>
    public double FraccionMinima { get => GetValue(FraccionMinimaProperty); set => SetValue(FraccionMinimaProperty, value); }

    protected override Size MeasureOverride(Size disponible)
    {
        var natural = new FormattedText(Text ?? string.Empty, CultureInfo.CurrentCulture, FlowDirection,
            new Typeface(FontFamily, FontStyle, FontWeight, FontStretch), TamanoPreferido, Foreground);
        var ancho = Math.Max(0, disponible.Width - Padding.Left - Padding.Right);
        var tamano = ancho > 0 && natural.WidthIncludingTrailingWhitespace > ancho
            ? TamanoPreferido * ancho / natural.WidthIncludingTrailingWhitespace : TamanoPreferido;
        var minimo = TamanoPreferido * FraccionMinima;
        var partir = tamano < minimo;
        if (partir) tamano = minimo;
        if (Math.Abs(FontSize - tamano) > 0.001) SetCurrentValue(FontSizeProperty, tamano);
        if (FraccionMinima > 0)
        {
            SetCurrentValue(TextWrappingProperty, partir ? TextWrapping.Wrap : TextWrapping.NoWrap);
            SetCurrentValue(MaxLinesProperty, partir ? 2 : 0);
            SetCurrentValue(TextTrimmingProperty, partir ? TextTrimming.CharacterEllipsis : TextTrimming.None);
        }
        return base.MeasureOverride(disponible);
    }
}
