// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Avalonia.Controls;
using Avalonia.Interactivity;
using Pokekeep.Mobile.Ui.Herramientas.EstadisticasBase;

namespace Pokekeep.Mobile.Ui.Views;

public partial class EstadisticasBaseView : UserControl
{
    public EstadisticasBaseView() => InitializeComponent();

    private void AlOrdenar(object? sender, RoutedEventArgs e)
    {
        if (DataContext is EstadisticasBaseViewModel vm) vm.Tabla.SolicitarOrden?.Invoke(vm.Tabla.HojaOrden());
    }
}
