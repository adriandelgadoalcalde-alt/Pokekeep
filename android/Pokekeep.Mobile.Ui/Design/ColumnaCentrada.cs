// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Avalonia;
using Avalonia.Controls;

namespace Pokekeep.Mobile.Ui.Design;

/// <summary>
/// Limita el ancho del contenido y lo centra: en un movil ocupa todo el ancho (no hace nada) y en una tableta o un plegable abierto el contenido queda en una columna legible en vez de estirar tarjetas y filas a
/// 1.400 dp. Es un decorador, no un contenedor de dos paneles: el diseno de varios paneles de tableta llega con su fase.
/// </summary>
public sealed class ColumnaCentrada : Decorator
{
    public static readonly StyledProperty<double> AnchoMaximoProperty = AvaloniaProperty.Register<ColumnaCentrada, double>(nameof(AnchoMaximo), 640);

    static ColumnaCentrada()
    {
        AffectsMeasure<ColumnaCentrada>(AnchoMaximoProperty);
        AffectsArrange<ColumnaCentrada>(AnchoMaximoProperty);
    }

    public double AnchoMaximo { get => GetValue(AnchoMaximoProperty); set => SetValue(AnchoMaximoProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Child is null) return default;
        var ancho = Math.Min(double.IsInfinity(availableSize.Width) ? AnchoMaximo : availableSize.Width, AnchoMaximo);
        Child.Measure(new Size(ancho, availableSize.Height));
        return new Size(double.IsInfinity(availableSize.Width) ? Child.DesiredSize.Width : availableSize.Width, Child.DesiredSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Child is null) return finalSize;
        var ancho = Math.Min(finalSize.Width, AnchoMaximo);
        Child.Arrange(new Rect((finalSize.Width - ancho) / 2, 0, ancho, finalSize.Height));
        return finalSize;
    }
}
