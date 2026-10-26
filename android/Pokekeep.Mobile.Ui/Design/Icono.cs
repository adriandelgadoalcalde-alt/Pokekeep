// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace Pokekeep.Mobile.Ui.Design;

/// <summary>
/// Icono de trazo (Lucide, ISC) dibujado como geometria vectorial en una rejilla de 24. Toma el color de <see cref="Foreground"/> (heredado del texto del contenedor)
/// y es decorativo para los lectores de pantalla: el control que lo contiene lleva el nombre accesible.
/// </summary>
public sealed class Icono : Control
{
    private static readonly Dictionary<string, Geometry> Cache = new();

    public static readonly StyledProperty<string?> GlifoProperty = AvaloniaProperty.Register<Icono, string?>(nameof(Glifo));
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<Icono>();
    public static readonly StyledProperty<double> GrosorProperty = AvaloniaProperty.Register<Icono, double>(nameof(Grosor), 2.0);

    static Icono()
    {
        AffectsRender<Icono>(GlifoProperty, ForegroundProperty, GrosorProperty);
        WidthProperty.OverrideDefaultValue<Icono>(24);
        HeightProperty.OverrideDefaultValue<Icono>(24);
    }

    public Icono()
    {
        Focusable = false;
        IsHitTestVisible = false;
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
    }

    /// <summary>Nombre del icono (por ejemplo «FolderOpen»), de <see cref="Iconos"/>.</summary>
    public string? Glifo { get => GetValue(GlifoProperty); set => SetValue(GlifoProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public double Grosor { get => GetValue(GrosorProperty); set => SetValue(GrosorProperty, value); }

    private static Geometry? Obtener(string? glifo)
    {
        if (string.IsNullOrEmpty(glifo) || !Iconos.Todos.TryGetValue(glifo, out var datos)) return null;
        lock (Cache)
        {
            if (!Cache.TryGetValue(glifo, out var g)) Cache[glifo] = g = Geometry.Parse(datos);
            return g;
        }
    }

    public override void Render(DrawingContext context)
    {
        var g = Obtener(Glifo);
        if (g is null) return;
        var lado = Math.Min(Bounds.Width, Bounds.Height);
        if (lado <= 0) return;
        var escala = lado / 24.0;
        var pincel = Foreground ?? Brushes.Gray;
        var pen = new Pen(pincel, Grosor, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        var dx = (Bounds.Width - lado) / 2; var dy = (Bounds.Height - lado) / 2;
        using (context.PushTransform(Matrix.CreateScale(escala, escala) * Matrix.CreateTranslation(dx, dy)))
            context.DrawGeometry(null, pen, g);
    }
}
