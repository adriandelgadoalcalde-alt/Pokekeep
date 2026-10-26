// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Pokekeep.Mobile.Ui.Design;

/// <summary>
/// Quien hace sitio al teclado. Hay DOS maneras correctas y NUNCA se suman (plan 8.4.4, ADR 0055/0056): o Android redimensiona la ventana (<c>adjustResize</c>: la ventana encoge y el layout ya cabe encima del teclado, sin
/// relleno propio) o la ventana mantiene su tamano (Android 15+ con la app a pantalla completa ignora <c>adjustResize</c>: medido en el emulador API 36) y el layout rellena por abajo lo que ocupa el teclado. Sumar las dos deja un
/// hueco del tamano del teclado. En <see cref="EstrategiaTeclado.Auto"/> se decide MIRANDO si la ventana encogio al aparecer el teclado.
/// </summary>
public enum EstrategiaTeclado { Auto, VentanaRedimensionada, RellenoManual }

/// <summary>
/// Contenedor raiz que se hace cargo del teclado: segun <see cref="Estrategia"/> no rellena nada (la ventana ya encogio) o rellena exactamente la altura ocupada; expone <see cref="TecladoVisible"/> para que la interfaz
/// oculte la barra de destinos mientras se escribe, y lleva el campo con foco a la vista cuando el teclado lo taparia. La altura ocupada viene de <c>TopLevel.InputPane</c> o, en pruebas, de <see cref="OcupadoPorTeclado"/>.
/// </summary>
public sealed class KeyboardAwareLayout : ContentControl
{
    public static readonly StyledProperty<EstrategiaTeclado> EstrategiaProperty = AvaloniaProperty.Register<KeyboardAwareLayout, EstrategiaTeclado>(nameof(Estrategia), EstrategiaTeclado.Auto);
    public static readonly StyledProperty<Rect?> OcupadoPorTecladoProperty = AvaloniaProperty.Register<KeyboardAwareLayout, Rect?>(nameof(OcupadoPorTeclado));
    public static readonly DirectProperty<KeyboardAwareLayout, bool> TecladoVisibleProperty = AvaloniaProperty.RegisterDirect<KeyboardAwareLayout, bool>(nameof(TecladoVisible), o => o.TecladoVisible);
    public static readonly DirectProperty<KeyboardAwareLayout, double> RellenoAplicadoProperty = AvaloniaProperty.RegisterDirect<KeyboardAwareLayout, double>(nameof(RellenoAplicado), o => o.RellenoAplicado);

    /// <summary>Solo pruebas de dispositivo: lineas de medicion del teclado (alto ocupado, ventana, barra de acciones, campo con foco) para <c>medir-ime</c>.</summary>
    public static event Action<string>? Medicion;

    /// <summary>En <see cref="EstrategiaTeclado.Auto"/>, lo que se supone si el teclado ya esta abierto cuando se mide por primera vez (la plataforma lo fija: false en Android 15+).</summary>
    public static bool SuponeVentanaRedimensionada { get; set; } = true;

    private double _alturaLibre;
    private EstrategiaTeclado _modo = EstrategiaTeclado.VentanaRedimensionada;
    private bool _visible;
    private double _relleno;
    private TopLevel? _top;

    public EstrategiaTeclado Estrategia { get => GetValue(EstrategiaProperty); set => SetValue(EstrategiaProperty, value); }
    /// <summary>Solo pruebas: rectangulo que el teclado ocupa en coordenadas de la ventana (sustituye al del sistema).</summary>
    public Rect? OcupadoPorTeclado { get => GetValue(OcupadoPorTecladoProperty); set => SetValue(OcupadoPorTecladoProperty, value); }
    /// <summary>Cual de las dos formas esta en uso ahora (nunca <see cref="EstrategiaTeclado.Auto"/>).</summary>
    public EstrategiaTeclado ModoEfectivo => _modo;
    public bool TecladoVisible { get => _visible; private set => SetAndRaise(TecladoVisibleProperty, ref _visible, value); }
    /// <summary>Relleno inferior que este contenedor anade por el teclado: 0 con <see cref="EstrategiaTeclado.VentanaRedimensionada"/>, la altura ocupada con <see cref="EstrategiaTeclado.RellenoManual"/>.</summary>
    public double RellenoAplicado { get => _relleno; private set => SetAndRaise(RellenoAplicadoProperty, ref _relleno, value); }

    static KeyboardAwareLayout()
    {
        EstrategiaProperty.Changed.AddClassHandler<KeyboardAwareLayout>((o, _) => o.Recalcular());
        OcupadoPorTecladoProperty.Changed.AddClassHandler<KeyboardAwareLayout>((o, _) => o.Recalcular());
    }

    public KeyboardAwareLayout()
    {
        AddHandler(GotFocusEvent, AlTomarFoco, RoutingStrategies.Bubble);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _top = TopLevel.GetTopLevel(this);
        if (_top?.InputPane is { } pane) pane.StateChanged += AlCambiarPanel;
        if (_top is not null) _top.PropertyChanged += AlCambiarTamano;
        Recalcular();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_top?.InputPane is { } pane) pane.StateChanged -= AlCambiarPanel;
        if (_top is not null) _top.PropertyChanged -= AlCambiarTamano;
        _top = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void AlCambiarPanel(object? sender, Avalonia.Controls.Platform.InputPaneStateEventArgs e) => Dispatcher.UIThread.Post(Recalcular);

    private void AlCambiarTamano(object? sender, AvaloniaPropertyChangedEventArgs e) { if (e.Property == TopLevel.ClientSizeProperty) Dispatcher.UIThread.Post(Recalcular); }

    private double AlturaOcupada()
    {
        var r = OcupadoPorTeclado ?? _top?.InputPane?.OccludedRect;
        return r is { Height: > 0 } rect ? rect.Height : 0;
    }

    private void Recalcular()
    {
        var ocupado = OcupadoPorTeclado ?? _top?.InputPane?.OccludedRect;
        var alto = ocupado is { Height: > 0 } r ? r.Height : 0;
        var altoVentana = _top?.ClientSize.Height ?? Bounds.Height;
        TecladoVisible = alto > 0;
        if (!TecladoVisible)
        {
            _alturaLibre = altoVentana;               // la ventana entera, sin teclado: la referencia para saber si luego encoge
            _modo = Estrategia == EstrategiaTeclado.RellenoManual ? EstrategiaTeclado.RellenoManual : EstrategiaTeclado.VentanaRedimensionada;
            RellenoAplicado = 0;
        }
        else
        {
            _modo = Estrategia switch
            {
                EstrategiaTeclado.VentanaRedimensionada => EstrategiaTeclado.VentanaRedimensionada,
                EstrategiaTeclado.RellenoManual => EstrategiaTeclado.RellenoManual,
                // La ventana encogio al menos la mitad de lo que ocupa el teclado: Android ya hizo sitio.
                _ => (_alturaLibre > 0 ? altoVentana <= _alturaLibre - 0.5 * alto : SuponeVentanaRedimensionada) ? EstrategiaTeclado.VentanaRedimensionada : EstrategiaTeclado.RellenoManual,
            };
            // En relleno manual el contenido debe terminar donde empieza el teclado: lo que falta entre el borde de la ventana y la parte alta del teclado.
            // (se mide contra el borde de ESTE control, no contra el de la ventana: en Android 15+ Avalonia ya deja fuera de el el hueco de la barra de gestos).
            var bordeInferior = _top is not null && Bounds.Height > 0 ? this.TranslatePoint(new Point(0, Bounds.Height), _top)?.Y ?? altoVentana : altoVentana;
            RellenoAplicado = _modo == EstrategiaTeclado.RellenoManual ? Math.Max(0, bordeInferior - (ocupado?.Y ?? altoVentana - alto)) : 0;
        }
        Padding = new Thickness(0, 0, 0, RellenoAplicado);
        if (TecladoVisible) Dispatcher.UIThread.Post(LlevarFocoALaVista, DispatcherPriority.Loaded);
        if (Medicion is not null) DispatcherTimer.RunOnce(Medir, TimeSpan.FromMilliseconds(600));
    }

    /// <summary>Una linea con lo que Avalonia ve ahora: pantalla, teclado, relleno, y la posicion (dp, coordenadas de la ventana) de la barra de acciones y del campo con foco.</summary>
    public void Medir()
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null || Medicion is null) return;
        string Pos(Control? c) => c is null || !c.IsEffectivelyVisible ? "-" : $"{c.TranslatePoint(new Point(0, 0), top)!.Value.Y:0.#}..{c.TranslatePoint(new Point(0, c.Bounds.Height), top)!.Value.Y:0.#}";
        var barra = this.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Name == "BarraAcciones");
        var foco = top.FocusManager?.GetFocusedElement() as Control;
        var occ = OcupadoPorTeclado ?? top.InputPane?.OccludedRect;
        var cadena = new System.Text.StringBuilder();
        for (Visual? v = this.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault(c => c.Name == "Desplazador"); v is not null && v != top; v = v.GetVisualParent())
            if (v is Control cc) cadena.Append($"[{cc.GetType().Name}{(string.IsNullOrEmpty(cc.Name) ? "" : "#" + cc.Name)} y={cc.TranslatePoint(new Point(0, 0), top)?.Y:0.#} h={cc.Bounds.Height:0.#}{(cc is Border b ? " pad=" + b.Padding : cc is Decorator d ? "" : "")}]");
        Medicion("arbol " + cadena);
        Medicion($"modo={_modo} libre={_alturaLibre:0.#} estrategia={Estrategia} ventana={top.ClientSize.Width:0.#}x{top.ClientSize.Height:0.#}dp escala={top.RenderScaling:0.###} tecladoVisible={TecladoVisible} ocupadoDp={(occ is { } r ? $"{r.Y:0.#}+{r.Height:0.#}" : "-")} relleno={RellenoAplicado:0.#} barraAcciones={Pos(barra)} campoConFoco={Pos(foco)} desplazador={Pos(this.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault(c => c.Name == "Desplazador"))} offsetY={this.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault(c => c.Name == "Desplazador")?.Offset.Y:0.#} sprite={Pos(this.GetVisualDescendants().OfType<SpritePokemon>().FirstOrDefault(c => c.IsEffectivelyVisible))} barraDestinosVisible={this.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Name == "Barra")?.IsEffectivelyVisible}");
    }

    private void AlTomarFoco(object? sender, RoutedEventArgs e)
    {
        if (e.Source is TextBox && TecladoVisible) Dispatcher.UIThread.Post(LlevarFocoALaVista, DispatcherPriority.Loaded);
    }

    /// <summary>Lleva el campo con foco a la vista con un margen: dentro de un ScrollViewer lo desplaza; fuera de el no hace nada.</summary>
    public void LlevarFocoALaVista()
    {
        if (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is Control c && c.GetVisualAncestors().Contains(this))
            c.BringIntoView(new Rect(-8, -16, c.Bounds.Width + 16, c.Bounds.Height + 32));
    }
}
