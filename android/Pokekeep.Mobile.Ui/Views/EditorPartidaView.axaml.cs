// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Pokekeep.Mobile.Ui.ViewModels;

namespace Pokekeep.Mobile.Ui.Views;

public partial class EditorPartidaView : UserControl
{
    private EditorPartidaViewModel? _vm;

    public EditorPartidaView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            // Baja de la suscripcion al modelo anterior: sin ella el modelo retenia a la vista (y la vista a su arbol) tras cambiar de editor.
            if (_vm is not null) _vm.PropertyChanged -= AlCambiar;
            _vm = DataContext as EditorPartidaViewModel;
            if (_vm is not null) _vm.PropertyChanged += AlCambiar;
        };
    }

    /// <summary>La pestana elegida se lleva a la vista (la tira de pestanas se desplaza en horizontal y la activa puede quedar fuera).</summary>
    private void AlCambiar(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(EditorPartidaViewModel.Actual)) return;
        Dispatcher.UIThread.Post(() =>
        {
            foreach (var t in this.GetVisualDescendants().OfType<ToggleButton>())
                if (t.IsChecked == true && t.Classes.Contains("chip") && t.DataContext is PestanaPVm) { t.BringIntoView(); break; }
        }, DispatcherPriority.Background);
    }
}
