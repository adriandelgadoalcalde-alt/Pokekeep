// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Avalonia;
using Avalonia.Controls;
using Pokekeep.Mobile.Ui.ViewModels;

namespace Pokekeep.Mobile.Ui.Views;

/// <summary>Lista de entradas de menu (pantallas Partida y Mas, provisionales en F2: cada fila dice que hara y llega en su fase).</summary>
public partial class MenuView : UserControl
{
    public static readonly StyledProperty<string> TituloProperty = AvaloniaProperty.Register<MenuView, string>(nameof(Titulo), string.Empty);
    public static readonly StyledProperty<string> SubtituloProperty = AvaloniaProperty.Register<MenuView, string>(nameof(Subtitulo), string.Empty);
    public static readonly StyledProperty<IReadOnlyList<EntradaMenu>?> EntradasProperty = AvaloniaProperty.Register<MenuView, IReadOnlyList<EntradaMenu>?>(nameof(Entradas));
    /// <summary>Contenido opcional bajo la lista (en Más: los créditos).</summary>
    public static readonly StyledProperty<object?> PieProperty = AvaloniaProperty.Register<MenuView, object?>(nameof(Pie));

    public MenuView()
    {
        InitializeComponent();
    }

    public string Titulo { get => GetValue(TituloProperty); set => SetValue(TituloProperty, value); }
    public string Subtitulo { get => GetValue(SubtituloProperty); set => SetValue(SubtituloProperty, value); }
    public IReadOnlyList<EntradaMenu>? Entradas { get => GetValue(EntradasProperty); set => SetValue(EntradasProperty, value); }
    public object? Pie { get => GetValue(PieProperty); set => SetValue(PieProperty, value); }
}
