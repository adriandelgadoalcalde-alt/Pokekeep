// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Avalonia;
using Avalonia.Controls;

namespace Pokekeep.Mobile.Ui.Design;

/// <summary>Un modelo que adapta su presentacion al ancho real que le da la pantalla (p. ej. la tabla de estadisticas, que se apila si no cabe).</summary>
public interface IConAnchoDisponible
{
    double AnchoDisponible { get; set; }
}

/// <summary>
/// <c>d:AnchoDisponible.Medir="True"</c> en un control: cada vez que cambia su tamano, pasa su ancho a su DataContext si este es
/// <see cref="IConAnchoDisponible"/>. El ancho no depende de lo que el modelo decida con el (solo cambia el alto), asi que no hay bucle de medida.
/// </summary>
public static class AnchoDisponible
{
    public static readonly AttachedProperty<bool> MedirProperty = AvaloniaProperty.RegisterAttached<Control, bool>("Medir", typeof(AnchoDisponible));

    static AnchoDisponible() => MedirProperty.Changed.AddClassHandler<Control>((c, e) =>
    {
        c.SizeChanged -= Cambio;
        if (e.NewValue is true) c.SizeChanged += Cambio;
    });

    public static bool GetMedir(Control c) => c.GetValue(MedirProperty);
    public static void SetMedir(Control c, bool valor) => c.SetValue(MedirProperty, valor);

    private static void Cambio(object? origen, SizeChangedEventArgs e)
    {
        if (origen is Control { DataContext: IConAnchoDisponible m } && e.WidthChanged) m.AnchoDisponible = e.NewSize.Width;
    }
}
