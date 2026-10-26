// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Pokekeep.Mobile.Ui.ViewModels;

namespace Pokekeep.Mobile.Ui.Views;

public partial class MochilaView : UserControl
{
    private MochilaViewModel? _vm;

    public MochilaView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            // Baja de la suscripcion al modelo anterior: sin ella el modelo retenia a la vista (y la vista a su arbol) tras cambiar de editor.
            if (_vm is not null) _vm.PropertyChanged -= AlCambiar;
            _vm = DataContext as MochilaViewModel;
            if (_vm is not null) _vm.PropertyChanged += AlCambiar;
        };
    }

    /// <summary>El bolsillo elegido se lleva a la vista (la tira de bolsillos se desplaza en horizontal: con muchos bolsillos el activo puede quedar fuera).</summary>
    private void AlCambiar(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MochilaViewModel.Actual)) return;
        Dispatcher.UIThread.Post(() =>
        {
            foreach (var t in this.GetVisualDescendants().OfType<ToggleButton>())
                if (t.IsChecked == true && t.Classes.Contains("chip") && t.DataContext is BolsilloChipVm) { t.BringIntoView(); break; }
        }, DispatcherPriority.Background);
    }

    private void AlEnfocar(object? sender, RoutedEventArgs e) { if (sender is TextBox t) t.SelectAll(); }

    private void AlPerderFoco(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox { DataContext: FilaObjetoVm fila }) fila.PerderFoco();
    }
}
