// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Pokekeep.Mobile.Ui.Design;

/// <summary>
/// Tira que se desplaza en horizontal (pestanas, chips) y AVISA de que hay mas: un degradado en el borde por el que sigue habiendo contenido (izquierdo si ya se ha desplazado, derecho si queda
/// por ver). Una tira con la barra oculta y sin indicio parece cortada a ras (la pestana «Agencia de batalla» de Festi Plaza quedaba fuera de la vista sin ninguna pista). Los degradados no atrapan
/// toques y solo existen visibles cuando el contenido no cabe; la invariante de MatrizTests exige este indicio en todo ScrollViewer horizontal que desborda.
/// </summary>
public sealed class TiraConIndicio : ContentControl
{
    public const string NombreIndicioIzquierdo = "IndicioDesplazamientoIzquierdo";
    public const string NombreIndicioDerecho = "IndicioDesplazamientoDerecho";
    private const double AnchoIndicio = 32;

    public static readonly StyledProperty<string> ClaveFondoProperty = AvaloniaProperty.Register<TiraConIndicio, string>(nameof(ClaveFondo), "Fondo");

    private ScrollViewer? _visor;
    private Border? _izquierdo;
    private Border? _derecho;
    private (RadioButton Boton, double AnchoVisor, double AnchoBoton, double AnchoContenido)? _seleccionCentrada;

    /// <summary>En carriles de pestañas, centra la seleccionada al cambiarla o cambiar el tamaño; un desplazamiento manual posterior se conserva.</summary>
    private bool _centrarSeleccion;
    public bool CentrarSeleccion
    {
        get => _centrarSeleccion;
        set { _centrarSeleccion = value; if (value) Actualizar(); }
    }

    public TiraConIndicio()
    {
        Template = new FuncControlTemplate<TiraConIndicio>((tira, ambito) =>
        {
            var visor = new ScrollViewer { Name = "PART_Visor", HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
            visor.Content = new ContentPresenter { [!ContentPresenter.ContentProperty] = tira[!ContentControl.ContentProperty], [!ContentPresenter.ContentTemplateProperty] = tira[!ContentControl.ContentTemplateProperty] };
            var izquierdo = Indicio(NombreIndicioIzquierdo, HorizontalAlignment.Left, izquierda: true);
            var derecho = Indicio(NombreIndicioDerecho, HorizontalAlignment.Right, izquierda: false);
            visor.RegisterInNameScope(ambito); izquierdo.RegisterInNameScope(ambito); derecho.RegisterInNameScope(ambito);
            var rejilla = new Grid();
            rejilla.Children.Add(visor); rejilla.Children.Add(izquierdo); rejilla.Children.Add(derecho);
            return rejilla;
        });
    }

    /// <summary>Clave del recurso de color sobre el que se apoya la tira (el degradado se funde con ese fondo): «Fondo» en la pagina, «SuperficieHundida» dentro de la barra de segmentos.</summary>
    public string ClaveFondo { get => GetValue(ClaveFondoProperty); set => SetValue(ClaveFondoProperty, value); }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_visor is not null) { _visor.ScrollChanged -= AlDesplazar; _visor.SizeChanged -= AlDesplazar; _visor.LayoutUpdated -= AlDesplazar; }
        _visor = e.NameScope.Find<ScrollViewer>("PART_Visor");
        _izquierdo = e.NameScope.Find<Border>(NombreIndicioIzquierdo);
        _derecho = e.NameScope.Find<Border>(NombreIndicioDerecho);
        if (_visor is not null) { _visor.ScrollChanged += AlDesplazar; _visor.SizeChanged += AlDesplazar; _visor.LayoutUpdated += AlDesplazar; }
        AplicarFondo();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ClaveFondoProperty) AplicarFondo();
    }

    private void AplicarFondo()
    {
        _izquierdo?.Bind(Border.BackgroundProperty, this.GetResourceObservable(ClaveFondo));
        _derecho?.Bind(Border.BackgroundProperty, this.GetResourceObservable(ClaveFondo));
    }

    private void AlDesplazar(object? sender, EventArgs e) => Actualizar();

    private void Actualizar()
    {
        if (_visor is null || _izquierdo is null || _derecho is null) return;
        var sobra = _visor.Extent.Width - _visor.Viewport.Width;
        if (CentrarSeleccion && _visor.Viewport.Width > 0 && this.GetVisualDescendants().OfType<RadioButton>().FirstOrDefault(b => b.IsChecked == true) is { } seleccionado && seleccionado.Bounds.Width > 0)
        {
            var medida = (seleccionado, _visor.Viewport.Width, seleccionado.Bounds.Width, _visor.Extent.Width);
            if (_seleccionCentrada != medida && seleccionado.TranslatePoint(default, _visor) is { } posicion)
            {
                _seleccionCentrada = medida;
                var margenIzquierdo = _visor.Offset.X > 1 ? AnchoIndicio : 0;
                var margenDerecho = _visor.Offset.X < sobra - 1 ? AnchoIndicio : 0;
                if (posicion.X < margenIzquierdo || posicion.X + seleccionado.Bounds.Width > _visor.Viewport.Width - margenDerecho)
                {
                    var centro = posicion.X + _visor.Offset.X + seleccionado.Bounds.Width / 2;
                    _visor.Offset = new Vector(Math.Clamp(centro - _visor.Viewport.Width / 2, 0, Math.Max(0, sobra)), 0);
                }
            }
        }
        var tira = sobra > 1 && _visor.Viewport.Width > 0;
        var izq = tira && _visor.Offset.X > 1; var der = tira && _visor.Offset.X < sobra - 1;
        if (_izquierdo.IsVisible != izq) _izquierdo.IsVisible = izq;
        if (_derecho.IsVisible != der) _derecho.IsVisible = der;
    }

    private static Border Indicio(string nombre, HorizontalAlignment lado, bool izquierda)
    {
        // El degradado es una MASCARA de opacidad: el color lo pone el fondo del tema (claro u oscuro) y la mascara solo decide cuanto se ve.
        var mascara = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            GradientStops =
            [
                new GradientStop(izquierda ? Colors.Black : Colors.Transparent, 0),
                new GradientStop(izquierda ? Colors.Transparent : Colors.Black, 1),
            ],
        };
        return new Border { Name = nombre, Width = AnchoIndicio, HorizontalAlignment = lado, VerticalAlignment = VerticalAlignment.Stretch, IsHitTestVisible = false, IsVisible = false, OpacityMask = mascara };
    }
}
