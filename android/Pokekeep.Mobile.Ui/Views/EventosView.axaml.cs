// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Avalonia.Controls;
using Avalonia.Interactivity;
using Pokekeep.Mobile.Ui.ViewModels;

namespace Pokekeep.Mobile.Ui.Views;

public partial class EventosView : UserControl
{
    public EventosView() => InitializeComponent();

    private void AlPerderFoco(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox { DataContext: FilaConstanteVm fila }) fila.PerderFoco();
    }
}
