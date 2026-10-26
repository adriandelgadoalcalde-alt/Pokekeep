// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Avalonia;
using Avalonia.Controls;

namespace Pokekeep.Mobile.Ui.Design;

/// <summary>
/// Dos hijos: la etiqueta (el primero) a la izquierda y el valor o el campo (el segundo, a su ancho natural) a la derecha, en la misma fila y centrados en vertical, como un <c>Grid ColumnDefinitions="*,Auto"</c>. Si con el
/// ancho que queda la etiqueta tendría que partir una palabra por dentro («Superentrena|miento»), el valor baja a su propia línea debajo de la etiqueta y cada uno usa todo el ancho. La decisión se toma al medir con el texto
/// ya maquetado (<see cref="PalabrasEnteras"/>), así que con letra normal es igual que el <c>Grid</c> de antes y solo cambia con letra grande en pantallas estrechas.
/// </summary>
public sealed class EtiquetaYValor : Panel
{
    public static readonly StyledProperty<double> SeparacionProperty = AvaloniaProperty.Register<EtiquetaYValor, double>(nameof(Separacion), 12);
    public static readonly StyledProperty<double> InterlineaProperty = AvaloniaProperty.Register<EtiquetaYValor, double>(nameof(Interlinea), 4);
    public static readonly StyledProperty<bool> InvertidaProperty = AvaloniaProperty.Register<EtiquetaYValor, bool>(nameof(Invertida));

    static EtiquetaYValor()
    {
        AffectsMeasure<EtiquetaYValor>(SeparacionProperty, InterlineaProperty, InvertidaProperty);
        AffectsArrange<EtiquetaYValor>(SeparacionProperty, InterlineaProperty, InvertidaProperty);
    }

    /// <summary>Hueco entre la etiqueta y el valor cuando van en la misma fila.</summary>
    public double Separacion { get => GetValue(SeparacionProperty); set => SetValue(SeparacionProperty, value); }
    /// <summary>Hueco entre la etiqueta y el valor cuando el valor baja a su línea.</summary>
    public double Interlinea { get => GetValue(InterlineaProperty); set => SetValue(InterlineaProperty, value); }

    /// <summary>
    /// El primer hijo sigue siendo el que se puede ajustar (el texto), pero se coloca a la DERECHA del segundo (una insignia o un número a la izquierda) y, si no caben, debajo de él. Para «Total 309» y su texto de habilidades.
    /// </summary>
    public bool Invertida { get => GetValue(InvertidaProperty); set => SetValue(InvertidaProperty, value); }

    private bool _enFila = true;

    protected override Size MeasureOverride(Size disponible)
    {
        if (Children.Count == 0) return default;
        var a = Children[0];
        if (Children.Count == 1) { a.Measure(disponible); return a.DesiredSize; }
        var b = Children[1];
        b.Measure(new Size(double.PositiveInfinity, disponible.Height));
        var hueco = b.DesiredSize.Width > 0 ? Separacion : 0;   // un valor oculto no deja hueco
        if (double.IsInfinity(disponible.Width))
        {
            a.Measure(Size.Infinity); _enFila = true;
            return new Size(a.DesiredSize.Width + hueco + b.DesiredSize.Width, Math.Max(a.DesiredSize.Height, b.DesiredSize.Height));
        }
        a.Measure(new Size(Math.Max(0, disponible.Width - hueco - b.DesiredSize.Width), double.PositiveInfinity));
        _enFila = !PalabrasEnteras.HayPalabraPartida(a);
        if (_enFila) return new Size(disponible.Width, Math.Max(a.DesiredSize.Height, b.DesiredSize.Height));
        a.Measure(new Size(disponible.Width, double.PositiveInfinity));
        b.Measure(new Size(disponible.Width, double.PositiveInfinity));
        return new Size(disponible.Width, a.DesiredSize.Height + (b.DesiredSize.Height > 0 ? Interlinea : 0) + b.DesiredSize.Height);
    }

    protected override Size ArrangeOverride(Size final)
    {
        if (Children.Count == 0) return final;
        var a = Children[0];
        if (Children.Count == 1) { a.Arrange(new Rect(final)); return final; }
        var b = Children[1];
        if (_enFila)
        {
            var anchoB = Math.Min(b.DesiredSize.Width, final.Width);
            var anchoA = Math.Max(0, final.Width - (anchoB > 0 ? Separacion : 0) - anchoB);
            if (Invertida)
            {
                b.Arrange(new Rect(0, 0, anchoB, final.Height));
                a.Arrange(new Rect(final.Width - anchoA, 0, anchoA, final.Height));
            }
            else
            {
                a.Arrange(new Rect(0, 0, anchoA, final.Height));
                b.Arrange(new Rect(final.Width - anchoB, 0, anchoB, final.Height));
            }
        }
        else
        {
            if (Invertida)
            {
                b.Arrange(new Rect(0, 0, final.Width, b.DesiredSize.Height));
                a.Arrange(new Rect(0, b.DesiredSize.Height + (b.DesiredSize.Height > 0 ? Interlinea : 0), final.Width, a.DesiredSize.Height));
            }
            else
            {
                a.Arrange(new Rect(0, 0, final.Width, a.DesiredSize.Height));
                b.Arrange(new Rect(0, a.DesiredSize.Height + Interlinea, final.Width, b.DesiredSize.Height));
            }
        }
        return final;
    }
}
