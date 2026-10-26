// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls.Primitives;
using Pokekeep.Mobile.Ui.Design;
using Pokekeep.Mobile.Ui.ViewModels;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Views;

public partial class CampoPView : UserControl
{
    private int _columna, _fila;
    private readonly TextBlock _posicion = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    public CampoPView()
    {
        InitializeComponent();
        AmpliarRejilla.PropertyChanged += (_,e) => { if(e.Property==ToggleButton.IsCheckedProperty) ActualizarAmpliacion(); };
        AutomationProperties.SetLiveSetting(_posicion,AutomationLiveSetting.Polite);
        MandosRejilla.Children.Add(_posicion);
        var mandos=new WrapPanel(); MandosRejilla.Children.Add(mandos);
        foreach (var (texto,dx,dy) in new[] { (IdiomaInterfaz.T("CampoPViewaxaml.ColumnaAnterior"),-1,0),(IdiomaInterfaz.T("CampoPViewaxaml.ColumnaSiguiente"),1,0),(IdiomaInterfaz.T("CampoPViewaxaml.FilaAnterior"),0,-1),(IdiomaInterfaz.T("CampoPViewaxaml.FilaSiguiente"),0,1),(IdiomaInterfaz.T("CampoPViewaxaml.CambiarTono"),0,0) })
        {
            var b=new Button { Content=texto, MinHeight=48, Margin=new Thickness(0,0,8,8) }; b.Classes.Add("secundario");
            AutomationProperties.SetName(b,texto);
            b.Click += (_,_) =>
            {
                _columna=Math.Clamp(_columna+dx,0,Math.Max(0,RejillaPixeles.AnchoCasillas-1));
                _fila=Math.Clamp(_fila+dy,0,Math.Max(0,RejillaPixeles.AltoCasillas-1));
                if(dx==0 && dy==0) RejillaPixeles.CiclarCasilla(_columna,_fila);
                MostrarPosicion();
            };
            mandos.Children.Add(b);
        }
    }
    private void ActualizarAmpliacion()
    {
        var ampliar=AmpliarRejilla.IsChecked==true;
        MandosRejilla.IsVisible=ampliar;
        VisorRejilla.MaxHeight=ampliar?320:double.PositiveInfinity;
        RejillaPixeles.MaxCasilla=ampliar?48:32;
        RejillaPixeles.Width=ampliar?RejillaPixeles.AnchoCasillas*48:double.NaN;
        VisorRejilla.HorizontalScrollBarVisibility=ampliar?ScrollBarVisibility.Auto:ScrollBarVisibility.Disabled;
        VisorRejilla.VerticalScrollBarVisibility=ampliar?ScrollBarVisibility.Auto:ScrollBarVisibility.Disabled;
        MostrarPosicion();
    }
    private void MostrarPosicion()
    {
        _posicion.Text=IdiomaInterfaz.F("CampoPViewaxaml.FilaColumnaTono", _fila+1, _columna+1, RejillaTonos.Tono(RejillaPixeles.Valor,_columna,_fila));
        if(AmpliarRejilla.IsChecked==true) VisorRejilla.Offset=new Vector(Math.Max(0,_columna*48-VisorRejilla.Viewport.Width/2),Math.Max(0,_fila*48-VisorRejilla.Viewport.Height/2));
    }

    private void AlPerderFoco(object? sender, RoutedEventArgs e)
    {
        if (DataContext is CampoPVm campo) campo.PerderFoco();
    }
}
