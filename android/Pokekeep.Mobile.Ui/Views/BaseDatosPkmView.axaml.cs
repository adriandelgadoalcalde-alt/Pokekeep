// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Pokekeep.Mobile.Ui.Herramientas.Encuentros;
using Pokekeep.Mobile.Ui.Herramientas.BaseDatosPkm;

namespace Pokekeep.Mobile.Ui.Views;

public partial class BaseDatosPkmView : UserControl
{
    public BaseDatosPkmView() => InitializeComponent();

    /// <summary>La carga (lo caro) empieza al mostrarse la pantalla, no al construir el modelo: así el modelo se prueba y se mide sin hilos sueltos.</summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (DataContext is BaseDatosPkmViewModel vm) vm.IniciarCarga();
    }

    /// <summary>Un número se confirma al salir del campo (no en cada tecla: «3» camino de «31» no debe aplicarse).</summary>
    private void AlPerderFoco(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: FilaCriterioNumero fila }) fila.Confirmar();
    }
}
