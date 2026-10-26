// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pokekeep.Mobile.Ui.Localizacion;
using Pokekeep.Mobile.Ui.Ajustes.Pkhex;

namespace Pokekeep.Mobile.Ui.ViewModels;

public sealed partial class FilaPkhexColor : FilaPkhex
{
    public FilaPkhexColor(NodoAjustePkhex nodo, IAjustesPkhex almacen) : base(nodo, almacen) => Releer();
    [ObservableProperty] private string _texto = string.Empty;
    [ObservableProperty] private string? _aviso;
    public bool HayAviso => !string.IsNullOrEmpty(Aviso);
    public IBrush Muestra => PresentacionPkhex.Pincel(Texto, "#000000");
    public override string ValorTexto => Texto;
    partial void OnTextoChanged(string value) { OnPropertyChanged(nameof(Muestra)); OnPropertyChanged(nameof(ValorTexto)); }
    partial void OnAvisoChanged(string? value) => OnPropertyChanged(nameof(HayAviso));
    public override void ActualizarIdioma()
    {
        base.ActualizarIdioma();
        if (Aviso is not null) Aviso = IdiomaInterfaz.T("Ajustes.Color.Invalido");
    }
    public override void Releer() { Texto = Leer() as string ?? "#000000"; Aviso = null; }
    [RelayCommand]
    public void Confirmar()
    {
        if (!Color.TryParse(Texto, out var color)) { Aviso = IdiomaInterfaz.T("Ajustes.Color.Invalido"); return; }
        Escribir(color.ToString());
        Releer();
    }
}

public static partial class FabricaFilasPkhex
{
    static partial void PersonalizadaB10_12(NodoAjustePkhex n, IAjustesPkhex a, Action<HojaViewModel> h, ref FilaPkhex? fila)
    {
        if (RegistroAjustesPkhex.EsColor(n)) fila = new FilaPkhexColor(n, a);
    }
}
