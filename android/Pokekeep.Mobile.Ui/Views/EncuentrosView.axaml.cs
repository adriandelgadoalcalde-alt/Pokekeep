// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Avalonia.Controls;
using Avalonia.Interactivity;
using Pokekeep.Mobile.Ui.Herramientas.Encuentros;

namespace Pokekeep.Mobile.Ui.Views;

public partial class EncuentrosView : UserControl
{
    public EncuentrosView() => InitializeComponent();

    /// <summary>Un número se confirma al salir del campo (no en cada tecla: «3» camino de «31» no debe aplicarse).</summary>
    private void AlPerderFoco(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: FilaCriterioNumero fila }) fila.Confirmar();
    }
}
