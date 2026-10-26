// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Avalonia.Controls;
using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Pokekeep.Mobile.Ui.ViewModels;

namespace Pokekeep.Mobile.Ui.Views;

public partial class SinPartidaView : UserControl
{
    public SinPartidaView()
    {
        InitializeComponent();
        Point? inicio = null;
        AddHandler(PointerPressedEvent, (_, e) =>
        {
            inicio = this.GetVisualDescendants().OfType<ScrollViewer>().First().Offset.Y <= 0 ? e.GetPosition(this) : null;
        }, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, (_, e) =>
        {
            if (inicio is { } p && e.GetPosition(this).Y - p.Y >= 72 && DataContext is ShellViewModel shell)
                shell.ActualizarRecientesCommand.Execute(null);
            inicio = null;
        }, RoutingStrategies.Tunnel, handledEventsToo: true);
    }
}
