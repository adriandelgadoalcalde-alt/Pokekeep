// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Avalonia.Controls;
using Avalonia.Interactivity;
using Pokekeep.Mobile.Ui.Herramientas;

namespace Pokekeep.Mobile.Ui.Views;

/// <summary>La rejilla de resultados (tarjetas con filtro, orden y páginas) de las herramientas de la F10. El orden se elige en una hoja con las columnas; la hoja la abre quien aloja la tabla (<c>SolicitarOrden</c>).</summary>
public partial class TablaResultadosView : UserControl
{
    public TablaResultadosView() => InitializeComponent();

    private void AlOrdenar(object? sender, RoutedEventArgs e)
    {
        if (DataContext is TablaResultadosViewModel vm) vm.SolicitarOrden?.Invoke(vm.HojaOrden());
    }
}
