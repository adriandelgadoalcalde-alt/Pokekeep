// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using CommunityToolkit.Mvvm.ComponentModel;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.ViewModels;

/// <summary>Pantalla de arranque: marca y progreso real de la preparacion de datos (copia de la Libreria a la carpeta privada, con verificacion SHA-256).</summary>
public sealed partial class ArranqueViewModel : ObservableObject
{
    [ObservableProperty] private string _mensaje = IdiomaInterfaz.T("Arranque.PreparandoPokekeep");
    [ObservableProperty] private double _fraccion;
    [ObservableProperty] private bool _indeterminado = true;
    [ObservableProperty] private string? _error;
    public bool HayError => Error is not null;
    partial void OnErrorChanged(string? value) => OnPropertyChanged(nameof(HayError));

    public void Informar(string mensaje, double? fraccion)
    {
        Mensaje = mensaje;
        Indeterminado = fraccion is null;
        if (fraccion is { } f) Fraccion = Math.Clamp(f, 0, 1) * 100;
    }
}
