// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;

namespace Pokekeep.Mobile.Ui.Design;

/// <summary>
/// Raíz de la pantalla con una capa modal (la hoja inferior). Mientras <see cref="ModalActivo"/> está encendido,
/// el resto de hijos queda inerte para el teclado (el Tab no entra) y para los servicios de accesibilidad
/// (su peer no los expone), así que el foco y la lectura de TalkBack se quedan en la hoja y no se cuelan por
/// debajo del velo hacia la barra de destinos o la página. No cambia nada visual ni toca el estado de los hijos.
/// </summary>
public class RaizModal : Grid
{
    public static readonly StyledProperty<bool> ModalActivoProperty = AvaloniaProperty.Register<RaizModal, bool>(nameof(ModalActivo));
    public static readonly AttachedProperty<bool> EsModalProperty = AvaloniaProperty.RegisterAttached<RaizModal, Control, bool>("EsModal");

    static RaizModal() => ModalActivoProperty.Changed.AddClassHandler<RaizModal>((r, _) => r.Aplicar());

    public bool ModalActivo { get => GetValue(ModalActivoProperty); set => SetValue(ModalActivoProperty, value); }
    public static bool GetEsModal(Control c) => c.GetValue(EsModalProperty);
    public static void SetEsModal(Control c, bool valor) => c.SetValue(EsModalProperty, valor);

    internal static bool Expone(RaizModal r, Control hijo) => !r.ModalActivo || GetEsModal(hijo);

    private void Aplicar()
    {
        foreach (var hijo in Children.OfType<Control>().Where(h => !GetEsModal(h)))
        {
            if (ModalActivo) KeyboardNavigation.SetTabNavigation(hijo, KeyboardNavigationMode.None);
            else hijo.ClearValue(KeyboardNavigation.TabNavigationProperty);
        }
        (ControlAutomationPeer.FromElement(this) as RaizModalPeer)?.Refrescar();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new RaizModalPeer(this);

    private sealed class RaizModalPeer(RaizModal dueño) : ControlAutomationPeer(dueño)
    {
        public void Refrescar() => InvalidateChildren();

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() =>
            base.GetChildrenCore()?.Where(p => p is not ControlAutomationPeer c || Expone(dueño, c.Owner)).ToList();
    }
}
