// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Pokekeep.Mobile.Ui.Views;

public partial class CampoView : UserControl
{
    public CampoView()
    {
        InitializeComponent();
        Focusable = true;
        // Solo recibe el foco por código (al pulsar Intro, para soltar el TextBox): como parada del Tab sería un destino sin nombre entre campo y campo.
        IsTabStop = false;
        // El campo que se esta escribiendo conserva su texto aunque otra escritura haga releer los demas; al salir se aplica lo pendiente y se muestra el valor normalizado.
        AddHandler(GotFocusEvent, (_, e) => { if (e.Source is TextBox && DataContext is ViewModels.CampoVm c) c.EnFoco = true; }, RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(LostFocusEvent, (_, e) => { if (e.Source is TextBox && DataContext is ViewModels.CampoVm c) c.PerderFoco(); }, RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(KeyDownEvent, (_, e) => { if (e.Key == Key.Enter && e.Source is TextBox) { if (DataContext is ViewModels.CampoVm c) c.PerderFoco(); Focus(); } }, RoutingStrategies.Bubble, handledEventsToo: false);
    }
}
