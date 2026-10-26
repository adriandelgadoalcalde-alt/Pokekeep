// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Avalonia.Controls;
using Avalonia.Interactivity;
using Pokekeep.Mobile.Ui.ViewModels;

namespace Pokekeep.Mobile.Ui.Views;

public partial class AjustesPkhexGrupoView : UserControl
{
    public AjustesPkhexGrupoView() => InitializeComponent();

    /// <summary>Un texto libre se guarda al salir del campo (no en cada tecla: una ruta a medio escribir no debe aplicarse al Core).</summary>
    private void AlPerderFoco(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: FilaPkhexTexto fila }) fila.Confirmar();
        else if (sender is Control { DataContext: FilaPkhexExtensiones ext }) ext.Confirmar();   // bloque 10-4
        else if (sender is Control { DataContext: FilaPkhexColor color }) color.Confirmar();
    }
}
