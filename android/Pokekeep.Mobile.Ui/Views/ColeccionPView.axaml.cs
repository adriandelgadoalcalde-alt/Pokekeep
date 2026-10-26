// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Avalonia.Controls;

namespace Pokekeep.Mobile.Ui.Views;

public partial class ColeccionPView : UserControl
{
    public ColeccionPView()
    {
        InitializeComponent();
        // El selector fijo nunca pasa del 40 % del alto: el resto queda para los campos (ver ColeccionPView.axaml).
        SizeChanged += (_, e) => ZonaSelector.MaxHeight = Math.Max(48, e.NewSize.Height * 0.4);
    }
}
