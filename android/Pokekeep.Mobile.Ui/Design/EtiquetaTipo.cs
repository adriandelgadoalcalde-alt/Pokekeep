// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Pokekeep.Mobile.Ui.Design;

/// <summary>
/// La etiqueta de un tipo (Fuego, Agua...) con el color del tipo, la MISMA en toda la app (cabecera de la ficha, cajas, estadísticas base, cobertura, selector de movimientos...):
/// rectángulo de radio 8, fondo oscuro con el 28 % del color del tipo, borde de 2 dp del color puro y texto blanco Medium; igual en claro y en oscuro (<see cref="PaletaTipos"/>).
/// <see cref="Tipo"/> es el índice del Core (el de <c>GameStrings.Types</c>) y <see cref="Texto"/> el nombre ya traducido al idioma de la interfaz.
/// </summary>
public sealed class EtiquetaTipo : Border
{
    public static readonly StyledProperty<int> TipoProperty = AvaloniaProperty.Register<EtiquetaTipo, int>(nameof(Tipo), -1);
    public static readonly StyledProperty<string?> TextoProperty = AvaloniaProperty.Register<EtiquetaTipo, string?>(nameof(Texto));

    private readonly TextBlock _texto = new()
    {
        Foreground = Brushes.White,
        FontWeight = FontWeight.Medium,
        TextWrapping = TextWrapping.NoWrap,
        VerticalAlignment = VerticalAlignment.Center,
    };

    static EtiquetaTipo()
    {
        TipoProperty.Changed.AddClassHandler<EtiquetaTipo>((e, _) => e.Aplicar());
        TextoProperty.Changed.AddClassHandler<EtiquetaTipo>((e, _) => e.Aplicar());
    }

    public EtiquetaTipo()
    {
        CornerRadius = new CornerRadius(8);
        BorderThickness = new Thickness(2);
        Padding = new Thickness(10, 3);
        VerticalAlignment = VerticalAlignment.Center;
        HorizontalAlignment = HorizontalAlignment.Left;
        _texto.Bind(TextBlock.FontSizeProperty, this.GetResourceObservable("TamEtiqueta"));
        Child = _texto;
        Aplicar();
    }

    public int Tipo { get => GetValue(TipoProperty); set => SetValue(TipoProperty, value); }
    public string? Texto { get => GetValue(TextoProperty); set => SetValue(TextoProperty, value); }

    private void Aplicar()
    {
        Background = new SolidColorBrush(PaletaTipos.Fondo(Tipo));
        BorderBrush = new SolidColorBrush(PaletaTipos.Borde(Tipo));
        _texto.Text = Texto;
        IsVisible = !string.IsNullOrEmpty(Texto);
        AutomationProperties.SetName(this, Texto ?? string.Empty);
    }
}
