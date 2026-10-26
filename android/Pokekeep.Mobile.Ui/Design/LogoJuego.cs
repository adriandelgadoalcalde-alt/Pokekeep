// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;
using PKHeX.Core;

namespace Pokekeep.Mobile.Ui.Design;

/// <summary>Logo decorativo del título exacto, con icono de partida como respaldo y sin deformar ni recortar la imagen.</summary>
public sealed class LogoJuego : UserControl
{
    public static readonly StyledProperty<GameVersion> VersionJuegoProperty = AvaloniaProperty.Register<LogoJuego, GameVersion>(nameof(VersionJuego));
    public GameVersion VersionJuego { get => GetValue(VersionJuegoProperty); set => SetValue(VersionJuegoProperty, value); }

    public LogoJuego()
    {
        IsHitTestVisible = false;
        Focusable = false;
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
        Actualizar();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == VersionJuegoProperty) Actualizar();
    }

    private void Actualizar()
    {
        if (LogosDeJuego.Actual?.Obtener(VersionJuego) is { } imagen)
        {
            var logo = new Image { Source = imagen, Stretch = Stretch.Uniform,
                MaxWidth = imagen.PixelSize.Width, MaxHeight = imagen.PixelSize.Height,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            // Decorativo, como el propio control: la fila ya dice el juego en su nombre accesible (uso9, auditoría F11).
            AutomationProperties.SetAccessibilityView(logo, AccessibilityView.Raw);
            Content = logo;
        }
        else
            Content = new Icono { Glifo = "HardDrive", Width = 22, Height = 22,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
    }
}
