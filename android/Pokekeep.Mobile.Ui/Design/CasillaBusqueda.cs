// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace Pokekeep.Mobile.Ui.Design;

/// <summary>Resultado encontrado: rebote de 250 ms, borde de acento y tick que se desvanecen en 650 ms.</summary>
public sealed class CasillaBusqueda : Border
{
    protected override Type StyleKeyOverride => typeof(Border);
    public static readonly StyledProperty<int> PulsoProperty = AvaloniaProperty.Register<CasillaBusqueda, int>(nameof(Pulso));
    private readonly Stopwatch _reloj = new();
    private readonly ScaleTransform _escala = new();
    private bool _pedido;
    private double _resalte;
    public static readonly StyledProperty<double> IntensidadProperty = AvaloniaProperty.Register<CasillaBusqueda, double>(nameof(Intensidad));
    public double Intensidad { get => GetValue(IntensidadProperty); private set => SetValue(IntensidadProperty, value); }
    public int Pulso { get => GetValue(PulsoProperty); set => SetValue(PulsoProperty, value); }
    internal double EscalaBusqueda => _escala.ScaleX;
    internal double IntensidadBusqueda => _resalte;

    public CasillaBusqueda()
    {
        RenderTransformOrigin = RelativePoint.Center;
        RenderTransform = _escala;
        AttachedToVisualTree += (_, _) => { if (Pulso > 0) { _reloj.Restart(); AvanzarBusqueda(TimeSpan.Zero); Pedir(); } };
        DetachedFromVisualTree += (_, _) => { _reloj.Reset(); AvanzarBusqueda(TimeSpan.FromMilliseconds(650)); };
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != PulsoProperty) return;
        _reloj.Restart();
        AvanzarBusqueda(Pulso > 0 ? TimeSpan.Zero : TimeSpan.FromMilliseconds(650));
        if (Pulso > 0)
        {
            Pedir();
            Dispatcher.UIThread.Post(() => { if (Pulso > 0 && IsEffectivelyVisible) this.BringIntoView(); }, DispatcherPriority.Loaded);
        }
    }

    private void Pedir()
    {
        if (_pedido || Movimiento.Pruebas || _resalte <= 0 || TopLevel.GetTopLevel(this) is not { } raiz) return;
        _pedido = true;
        raiz.RequestAnimationFrame(_ => { _pedido = false; if (Pulso <= 0 || TopLevel.GetTopLevel(this) is null) return; AvanzarBusqueda(_reloj.Elapsed); Pedir(); });
    }

    internal void AvanzarBusqueda(TimeSpan tiempo)
    {
        var ms = Math.Max(0, tiempo.TotalMilliseconds);
        var escala = Movimiento.Reducido || ms >= 250 ? 1 : 1 + 0.15 * Math.Sin(Math.PI * ms / 250);
        _escala.ScaleX = _escala.ScaleY = escala;
        _resalte = Pulso <= 0 ? 0 : Math.Clamp(1 - ms / 650, 0, 1);
        Intensidad = _resalte;
    }

}

/// <summary>Trazo superpuesto sin cambiar el borde ni el contenido de la casilla.</summary>
public sealed class TrazoBusqueda : Control
{
    public static readonly StyledProperty<double> IntensidadProperty = AvaloniaProperty.Register<TrazoBusqueda, double>(nameof(Intensidad));
    public double Intensidad { get => GetValue(IntensidadProperty); set => SetValue(IntensidadProperty, value); }
    static TrazoBusqueda() => AffectsRender<TrazoBusqueda>(IntensidadProperty);
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Intensidad <= 0 || !this.TryFindResource("Dorado", ActualThemeVariant, out var recurso) || recurso is not IBrush acento) return;
        using (context.PushOpacity(Intensidad))
        {
            context.DrawRectangle(null, new Pen(acento, 3), new Rect(Bounds.Size).Deflate(1.5), 12, 12);
            var p = new Pen(acento, 2.5);
            var y = Bounds.Height - 12;
            var fondo = this.TryFindResource("Superficie", ActualThemeVariant, out var f) && f is IBrush b ? b : Brushes.Transparent;
            context.DrawEllipse(fondo, null, new Point(12, y), 10, 10);
            context.DrawLine(p, new Point(6, y), new Point(10, y + 4));
            context.DrawLine(p, new Point(10, y + 4), new Point(17, y - 4));
        }
    }
}
