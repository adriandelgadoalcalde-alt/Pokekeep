// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Avalonia.Controls;
using Avalonia.VisualTree;
using Pokekeep.Mobile.Ui.Design;

namespace Pokekeep.Mobile.Ui.Views;

public partial class EditorView : UserControl
{
    public EditorView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            foreach (var tira in this.GetVisualDescendants().OfType<TiraConIndicio>().Where(t => t.GetVisualDescendants().OfType<RadioButton>().Any(b => b.GroupName == "pestana-editor")))
                tira.CentrarSeleccion = true;
        };
    }
}
