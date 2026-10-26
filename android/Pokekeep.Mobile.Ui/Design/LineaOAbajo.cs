// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Avalonia;
using Avalonia.Controls;

namespace Pokekeep.Mobile.Ui.Design;

/// <summary>
/// Dos hijos: el primero (el texto) a la izquierda y el segundo (p. ej. «Sin conexion») pegado a la derecha en la MISMA fila si caben los dos con <see cref="Separacion"/> entre ellos; si no, el segundo baja a su propia linea,
/// y el panel crece con ella (la tarjeta que lo contiene siempre contiene su contenido). Es una funcion pura del ancho disponible: se decide al medir, en una sola pasada, sin codigo que mida controles a mano ni bucles
/// (uso real 1, punto 5: la decision de derecha/abajo en codigo ocultaba y mostraba controles en cada cambio de tamano).
/// </summary>
public sealed class LineaOAbajo : Panel
{
    public static readonly StyledProperty<double> SeparacionProperty = AvaloniaProperty.Register<LineaOAbajo, double>(nameof(Separacion), 12);
    public static readonly StyledProperty<double> InterlineaProperty = AvaloniaProperty.Register<LineaOAbajo, double>(nameof(Interlinea), 8);

    static LineaOAbajo()
    {
        AffectsMeasure<LineaOAbajo>(SeparacionProperty, InterlineaProperty);
        AffectsArrange<LineaOAbajo>(SeparacionProperty, InterlineaProperty);
    }

    public double Separacion { get => GetValue(SeparacionProperty); set => SetValue(SeparacionProperty, value); }
    public double Interlinea { get => GetValue(InterlineaProperty); set => SetValue(InterlineaProperty, value); }

    private bool _enFila;

    protected override Size MeasureOverride(Size disponible)
    {
        if (Children.Count < 2) return Children.Count == 1 ? MedirUno(disponible) : default;
        var a = Children[0]; var b = Children[1];
        b.Measure(Size.Infinity);
        a.Measure(new Size(disponible.Width, double.PositiveInfinity));
        var ancho = double.IsInfinity(disponible.Width) ? a.DesiredSize.Width + Separacion + b.DesiredSize.Width : disponible.Width;
        _enFila = double.IsInfinity(disponible.Width) || a.DesiredSize.Width + Separacion + b.DesiredSize.Width <= disponible.Width + 0.01;
        if (!_enFila)
        {
            // En su propia linea: el texto puede usar todo el ancho y el segundo se recorta solo si no cabe ni solo.
            a.Measure(new Size(disponible.Width, double.PositiveInfinity));
            b.Measure(new Size(disponible.Width, double.PositiveInfinity));
            return new Size(ancho, a.DesiredSize.Height + Interlinea + b.DesiredSize.Height);
        }
        // En fila: el texto cede lo que ocupa el segundo.
        a.Measure(new Size(Math.Max(0, disponible.Width - Separacion - b.DesiredSize.Width), double.PositiveInfinity));
        return new Size(ancho, Math.Max(a.DesiredSize.Height, b.DesiredSize.Height));
    }

    private Size MedirUno(Size d) { Children[0].Measure(d); return Children[0].DesiredSize; }

    protected override Size ArrangeOverride(Size final)
    {
        if (Children.Count < 2) { if (Children.Count == 1) Children[0].Arrange(new Rect(final)); return final; }
        var a = Children[0]; var b = Children[1];
        if (_enFila)
        {
            var alto = Math.Max(a.DesiredSize.Height, b.DesiredSize.Height);
            a.Arrange(new Rect(0, (alto - a.DesiredSize.Height) / 2, a.DesiredSize.Width, a.DesiredSize.Height));
            b.Arrange(new Rect(Math.Max(a.DesiredSize.Width + Separacion, final.Width - b.DesiredSize.Width), (alto - b.DesiredSize.Height) / 2, b.DesiredSize.Width, b.DesiredSize.Height));
        }
        else
        {
            a.Arrange(new Rect(0, 0, Math.Min(final.Width, Math.Max(a.DesiredSize.Width, 0)), a.DesiredSize.Height));
            b.Arrange(new Rect(0, a.DesiredSize.Height + Interlinea, Math.Min(final.Width, b.DesiredSize.Width), b.DesiredSize.Height));
        }
        return final;
    }
}
