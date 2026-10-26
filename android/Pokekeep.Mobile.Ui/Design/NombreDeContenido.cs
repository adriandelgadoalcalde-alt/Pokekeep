// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Pokekeep.Mobile.Ui.Design;

/// <summary>
/// Los contenedores virtualizados de las listas no deben anunciar Content.ToString() (el tipo del modelo).
/// Su nombre procede del texto que la fila muestra. Conserva nombres explícitos y actualiza al reciclar la fila.
/// No modifica selección, foco, gestos ni AccessibilityView.
/// </summary>
public static class NombreDeContenido
{
    public static readonly AttachedProperty<bool> UsarProperty = AvaloniaProperty.RegisterAttached<Control, bool>("Usar", typeof(NombreDeContenido));
    private sealed class Estado { public bool Propio; public string? Ultimo; public bool Armado; public int Intentos; }
    private static readonly ConditionalWeakTable<Control, Estado> Estados = new();

    static NombreDeContenido()
    {
        // Al cambiar de idioma el texto que muestra cada fila cambia sin que cambie su modelo: se recalcula el nombre de las filas ya armadas.
        Localizacion.IdiomaInterfaz.Actual.Cambiado += () =>
        {
            foreach (var (c, estado) in Estados.ToArray()) { c.LayoutUpdated -= Actualizar; estado.Armado = false; Armar(c); }
        };
        UsarProperty.Changed.AddClassHandler<Control>(AlCambiarUsar);
    }

    private static void AlCambiarUsar(Control c, AvaloniaPropertyChangedEventArgs e)
    {
        c.LayoutUpdated -= Actualizar;
        c.DataContextChanged -= AlCambiarElModelo;
        if (e.NewValue is true)
        {
            Estados.Remove(c);
            Estados.Add(c, new Estado { Propio = !c.IsSet(AutomationProperties.NameProperty) });
            c.DataContextChanged += AlCambiarElModelo;
            Armar(c);
        }
        else if (Estados.TryGetValue(c, out var estado))
        {
            if (estado.Propio && AutomationProperties.GetName(c) == estado.Ultimo) c.ClearValue(AutomationProperties.NameProperty);
            Estados.Remove(c);
        }
    }

    public static bool GetUsar(Control c) => c.GetValue(UsarProperty);
    public static void SetUsar(Control c, bool valor) => c.SetValue(UsarProperty, valor);

    // Se calcula UNA vez tras el siguiente diseño de cada cambio de modelo (alta y reciclado de la fila), no en cada
    // pasada de diseño: con la lista en pleno desplazamiento un manejador por fila costaba ~0,8 ms por pasada con 21 filas.
    private static void AlCambiarElModelo(object? origen, EventArgs e) { if (origen is Control c) Armar(c); }

    private static void Armar(Control c)
    {
        if (!Estados.TryGetValue(c, out var estado) || estado.Armado) return;
        estado.Armado = true; estado.Intentos = 0;
        c.LayoutUpdated += Actualizar;
    }

    private static void Actualizar(object? origen, EventArgs e)
    {
        if (origen is not Control c) return;
        c.LayoutUpdated -= Actualizar;
        if (!Estados.TryGetValue(c, out var estado)) return;
        estado.Armado = false;
        if (!estado.Propio) return;
        // Un nombre explícito instalado después del template también tiene prioridad sobre el automático.
        var actual = AutomationProperties.GetName(c);
        if (!string.IsNullOrWhiteSpace(actual) && actual != estado.Ultimo) { estado.Propio = false; return; }
        var texto = string.Join(", ", c.GetVisualDescendants().OfType<TextBlock>()
            .Where(t => t.IsEffectivelyVisible && !string.IsNullOrWhiteSpace(t.Text))
            .Select(t => t.Text!.Trim()).Distinct().Take(3));
        // Si la plantilla todavía no ha materializado el texto, se reintenta en las siguientes pasadas (acotado).
        if (texto.Length == 0 && ++estado.Intentos < 4) { estado.Armado = true; c.LayoutUpdated += Actualizar; return; }
        if (texto == estado.Ultimo) return;
        estado.Ultimo = texto;
        c.SetCurrentValue(AutomationProperties.NameProperty, texto);
    }
}
