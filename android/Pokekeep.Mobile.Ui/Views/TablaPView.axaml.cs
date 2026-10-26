// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Avalonia.Controls;
using Avalonia;
using Avalonia.Data;
using Avalonia.Interactivity;
using System.ComponentModel;
using Pokekeep.Mobile.Ui.ViewModels;

namespace Pokekeep.Mobile.Ui.Views;

public partial class TablaPView : UserControl
{
    private TablaPVm? _modelo;
    private bool _cabeceraEnLista;
    private TablaPVm? _enlaceFiltro;
    public TablaPView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (_modelo is not null) _modelo.PropertyChanged -= AlCambiarLista;
            _modelo = DataContext as TablaPVm;
            CabeceraTabla.DataContext = _modelo;
            if (_modelo is not null) _modelo.PropertyChanged += AlCambiarLista;
            ActualizarLista();
        };
        SizeChanged += (_, _) => AjustarCabecera();
        AttachedToVisualTree += (_,_) =>
        {
            if (_modelo is not null) _modelo.PropertyChanged -= AlCambiarLista;
            _modelo=DataContext as TablaPVm;
            CabeceraTabla.DataContext=_modelo;
            if(_modelo is not null) _modelo.PropertyChanged+=AlCambiarLista;
            ActualizarLista();
        };
        DetachedFromVisualTree += (_,_) => { if(_modelo is not null) _modelo.PropertyChanged-=AlCambiarLista; };
    }
    private void AlCambiarLista(object? sender, PropertyChangedEventArgs e)
    { if (e.PropertyName == nameof(TablaPVm.Visibles)) ActualizarLista(); }
    private void ActualizarLista()
    {
        if(DataContext is not TablaPVm modelo) return;
        CabeceraTabla.DataContext=modelo;
        if(!ReferenceEquals(_enlaceFiltro,modelo))
        {
            _enlaceFiltro=modelo;
            FiltroTabla.Bind(TextBox.TextProperty,new Binding(nameof(TablaPVm.Filtro)) { Source=modelo,Mode=BindingMode.TwoWay });
        }
        FilasTabla.ItemsSource=_cabeceraEnLista ? modelo.Visibles.Cast<object>().Prepend(CabeceraTabla).ToArray() : modelo.Visibles;
    }
    private void AjustarCabecera()
    {
        if (Bounds.Height <= 0 || CabeceraTabla.DesiredSize.Height <= 0) return;
        var letraGrande = Application.Current?.Resources["TamCuerpo"] is double cuerpo && cuerpo >= 29.9;
        var pocoAlto = TopLevel.GetTopLevel(this)?.Bounds.Height < 650;
        var mover = letraGrande && pocoAlto || Bounds.Height < CabeceraTabla.DesiredSize.Height + 96;
        if (mover == _cabeceraEnLista) return;
        _cabeceraEnLista = mover;
        if (mover) { RaizTabla.Children.Remove(CabeceraTabla); Grid.SetRow(FilasTabla,0); Grid.SetRowSpan(FilasTabla,2); }
        else { FilasTabla.ItemsSource = null; RaizTabla.Children.Insert(0,CabeceraTabla); Grid.SetRow(FilasTabla,1); Grid.SetRowSpan(FilasTabla,1); }
        ActualizarLista();
    }

    private void AlPerderFoco(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox { DataContext: FilaTablaPVm fila }) fila.PerderFoco();
    }
}
