// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using CommunityToolkit.Mvvm.Input;

namespace Pokekeep.Mobile.Ui.ViewModels;

// X37: el botón «Cobertura y plantillas» de la pestaña Equipo. Aparte para no tocar el resto de CajasViewModel.
public sealed partial class CajasViewModel
{
    [RelayCommand]
    private void AbrirCoberturaYPlantillas() => _anfitrion?.AbrirCoberturaYPlantillas();
}
