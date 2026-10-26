// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Pokekeep.Mobile.Ui.Design;
using Pokekeep.Mobile.Ui.ViewModels;

namespace Pokekeep.Mobile.Ui.Views;

/// <summary>Fase del carrusel de cajas.</summary>
public enum FaseCarrusel { Reposo, Arrastrando, Animando }

/// <summary>
/// Pestana Pokemon. Dos gestos que XAML no da hechos: la pulsacion larga sobre una casilla (empieza la seleccion multiple) y el CARRUSEL de cajas (X6). Los dos tienen su equivalente accesible: el menu de la casilla
/// («Seleccionar varios») y los botones de caja anterior y siguiente, que usan la misma animacion.
/// <para>Carrusel: la caja que se ve y la VECINA (con su fondo y sus Pokemon, cargada una vez al empezar el gesto: <see cref="CajasViewModel.PrepararVecina"/>) son dos capas dentro de la tarjeta. Mientras el dedo arrastra, la actual sigue
/// al dedo 1:1 y la vecina entra por el lado. Al soltar, <see cref="GestoCaja.Resolver"/> decide: si pasa el umbral (el cuarto del ancho) o fue un lanzamiento, se completa con la curva de salida (~300 ms) y la caja real pasa a
/// ser la que entro; si no, vuelve con un rebote de muelle. Solo se animan las transformaciones de las dos capas (nada de reconstruir la cuadricula por fotograma). Con «reducir movimiento» no hay animacion: el dedo sigue moviendo la
/// caja (es manipulacion directa) y al soltar se cambia, o se vuelve, de golpe.</para>
/// </summary>
public partial class CajasView : UserControl
{
    private readonly TranslateTransform _transActual = new(), _transVecina = new();
    private readonly Stopwatch _reloj = Stopwatch.StartNew();
    private double _acumulado;          // dedo: positivo = hacia la izquierda = caja siguiente
    private double _x;                  // posicion de la caja que se ve (dp, 0 = reposo, negativo = a la izquierda)
    private double _ancho;
    private int _dirVecina;             // +1: la vecina es la siguiente (entra por la derecha), -1: la anterior, 0: ninguna cargada
    private bool _soltado;              // el reconocedor ya entrego la velocidad (arranque de inercia): lo que llegue despues es inercia que se ignora
    private bool _animacionCompleta;    // true = completar la transicion, false = rebote
    private double _x0, _x1, _v0, _duracionMs;
    private int _dirAnimacion;
    private TimeSpan _t0;
    private bool _fotogramaPedido;
    private Window? _ventanaInteraccion;

    /// <summary>Solo pruebas: no pide fotogramas al sistema; quien llama avanza la animacion con <see cref="Avanzar"/>.</summary>
    public bool RelojManual { get; set; }

    public FaseCarrusel Fase { get; private set; }
    /// <summary>Posicion (dp) de la caja que se ve respecto de su sitio de reposo.</summary>
    public double PosicionCaja => _x;
    /// <summary>Posicion (dp) de la caja vecina, o null si no se ve.</summary>
    public double? PosicionVecina => CapaVecina.IsVisible ? _transVecina.X : null;

    public CajasView()
    {
        InitializeComponent();
        CapaActual.RenderTransform = _transActual;
        CapaVecina.RenderTransform = _transVecina;
        AddHandler(InputElement.HoldingEvent, AlMantener, RoutingStrategies.Bubble);
        TarjetaCaja.AddHandler(InputElement.ScrollGestureEvent, AlDeslizar, RoutingStrategies.Bubble);
        TarjetaCaja.AddHandler(InputElement.ScrollGestureInertiaStartingEvent, AlSoltarConInercia, RoutingStrategies.Bubble);
        TarjetaCaja.AddHandler(InputElement.ScrollGestureEndedEvent, AlTerminarDeslizamiento, RoutingStrategies.Bubble);
        BotonCajaAnterior.Click += (_, e) => { e.Handled = true; Desplazar(-1); };
        BotonCajaSiguiente.Click += (_, e) => { e.Handled = true; Desplazar(+1); };
        // La barra de búsqueda ocupa al menos lo que la cabecera normal: la rejilla no salta al buscar (RondaF4). Altura mínima, no fija,
        // para que un título de búsqueda más alto que la cabecera (texto al 200 %) crezca en vez de recortarse.
        CabeceraNormal.PropertyChanged += (_, e) =>
        {
            if (e.Property == BoundsProperty && CabeceraNormal.IsVisible && CabeceraNormal.Bounds.Height > 0)
                CabeceraBuscando.MinHeight = Math.Max(CabeceraNormal.Bounds.Height, 48);
        };
        IniciarArrastre();
        DataContextChanged += (_, _) => { AbandonarArrastre(); Reposar(); };
        AttachedToVisualTree += (_, _) =>
        {
            CicloInteraccion.Interrumpida += InterrumpirGesto;
            _ventanaInteraccion = TopLevel.GetTopLevel(this) as Window;
            if (_ventanaInteraccion is { } ventana) ventana.Deactivated += AlDesactivarVentana;
        };
        DetachedFromVisualTree += (_, _) =>
        {
            CicloInteraccion.Interrumpida -= InterrumpirGesto;
            if (_ventanaInteraccion is { } ventana) ventana.Deactivated -= AlDesactivarVentana;
            _ventanaInteraccion = null;
            InterrumpirGesto();
        };
    }

    private void AlDesactivarVentana(object? sender, EventArgs e) => InterrumpirGesto();
    private void InterrumpirGesto() { AbandonarArrastre(); Reposar(); }

    private CajasViewModel? Vm => DataContext as CajasViewModel;

    private static CasillaViewModel? CasillaEn(object? origen)
    {
        for (var v = origen as Visual; v is not null; v = v.GetVisualParent())
            if (v is Control { DataContext: CasillaViewModel c }) return c;
        return null;
    }

    private void AlMantener(object? sender, HoldingRoutedEventArgs e)
    {
        if (e.HoldingState != HoldingState.Started || DataContext is not CajasViewModel vm) return;
        if (CasillaEn(e.Source) is not { } casilla) return;
        AlMantenerArrastre(casilla, e);   // X10: levanta el Pokemon si se puede; si no, lo de siempre (seleccion, menu de la casilla vacia...)
    }

    // ---- Gestos reales: solo traducen los eventos de Avalonia a las operaciones de abajo (que son las que prueban las pruebas) ----
    private void AlDeslizar(object? sender, ScrollGestureEventArgs e)
    {
        if (_levantado) { e.Handled = true; return; }   // con un Pokemon en la mano el dedo es del arrastre: ni el carrusel ni el desplazamiento de la pagina
        if (Vm is not { MostrarCuadriculaCaja: true }) return;
        e.Handled = true;
        if (_soltado) { e.ShouldEndScrollGesture = true; return; }   // inercia del reconocedor tras soltar: la caja ya esta animando por su cuenta
        Arrastrar(e.Delta.X);
    }

    private void AlSoltarConInercia(object? sender, ScrollGestureInertiaStartingEventArgs e)
    {
        if (Fase != FaseCarrusel.Arrastrando) return;
        _soltado = true;
        Soltar(e.Inertia.X);   // dp/s, signo del dedo
    }

    private void AlTerminarDeslizamiento(object? sender, ScrollGestureEndedEventArgs e)
    {
        var yaSoltado = _soltado;
        _soltado = false;
        if (!yaSoltado && Fase == FaseCarrusel.Arrastrando) Soltar(0);   // soltado despacio o parado: sin velocidad de lanzamiento
    }

    // ---- Operaciones del carrusel ----
    /// <summary>El dedo se ha movido <paramref name="deltaDedo"/> dp (positivo = hacia la izquierda). La caja que se ve lo sigue 1:1 y la vecina entra por el lado.</summary>
    public void Arrastrar(double deltaDedo)
    {
        if (Vm is not { } vm || !vm.MostrarCuadriculaCaja) return;
        if (Fase == FaseCarrusel.Animando) Concluir();
        if (Fase == FaseCarrusel.Reposo) { Fase = FaseCarrusel.Arrastrando; _acumulado = 0; _ancho = TarjetaCaja.Bounds.Width; _dirVecina = 0; }
        _acumulado += deltaDedo;
        var hay = vm.HayCajaVecina;
        var x = GestoCaja.PosicionAlArrastrar(_acumulado, _ancho, hay);
        var dir = x < 0 ? +1 : x > 0 ? -1 : 0;
        if (hay && dir != 0 && dir != _dirVecina && vm.PrepararVecina(dir)) _dirVecina = dir;   // se carga una vez; solo se recarga si el dedo cruza al otro lado
        Colocar(x);
    }

    /// <summary>El dedo se ha soltado con <paramref name="velocidadDedo"/> dp/s (mismo signo que el desplazamiento del dedo): completa la transicion, o vuelve con rebote.</summary>
    public void Soltar(double velocidadDedo)
    {
        if (Fase != FaseCarrusel.Arrastrando || Vm is not { } vm) return;
        var umbralMinimo = Ajustes.Pkhex.AvanzadoPkhex.UmbralDeslizamiento(_ancho, 24);   // nunca menos que Advanced.DragStartThreshold (bloque 10-1)
        var destino = GestoCaja.Resolver(_acumulado, _ancho, velocidadDedo, umbralMinimo, vm.HayCajaVecina && _dirVecina != 0);
        var dir = destino == DestinoCaja.Siguiente ? +1 : destino == DestinoCaja.Anterior ? -1 : 0;
        _acumulado = 0;
        if (dir != 0 && dir == _dirVecina)
        {
            if (Movimiento.Reducido) { Aterrizar(dir); return; }
            Animar(completar: true, dir, _x, -dir * _ancho, -velocidadDedo, GestoCaja.DuracionDeCompletar(Math.Abs(-dir * _ancho - _x), _ancho, velocidadDedo));
        }
        else if (Movimiento.Reducido || Math.Abs(_x) < 0.5) Reposar();
        else Animar(completar: false, 0, _x, 0, -velocidadDedo, GestoCaja.DuracionReboteMs);
    }

    /// <summary>Pasa a la caja anterior (-1) o siguiente (+1) con la misma animacion del deslizamiento (botones). Sin animacion con «reducir movimiento», con una sola caja o si la vista no esta en pantalla.</summary>
    public void Desplazar(int direccion)
    {
        if (Vm is not { } vm) return;
        if (Fase == FaseCarrusel.Animando) Concluir();
        if (Fase == FaseCarrusel.Arrastrando) return;   // el dedo tiene la caja: el boton espera
        var anchoActual = TarjetaCaja.Bounds.Width;
        if (Movimiento.Reducido || !vm.HayCajaVecina || anchoActual <= 0 || !TarjetaCaja.IsEffectivelyVisible || (!RelojManual && TopLevel.GetTopLevel(this) is null))
        {
            if (direccion > 0) vm.CajaSiguienteCommand.Execute(null); else vm.CajaAnteriorCommand.Execute(null);
            return;
        }
        var dir = direccion > 0 ? +1 : -1;
        _ancho = anchoActual;
        if (!vm.PrepararVecina(dir)) { if (dir > 0) vm.CajaSiguienteCommand.Execute(null); else vm.CajaAnteriorCommand.Execute(null); return; }
        _dirVecina = dir;
        Colocar(0);
        Animar(completar: true, dir, 0, -dir * _ancho, 0, GestoCaja.DuracionMs);
    }

    /// <summary>Avanza la animacion en curso hasta <paramref name="transcurrido"/> desde su comienzo. Lo llama el reloj de fotogramas; las pruebas lo llaman con tiempos a mano.</summary>
    public void Avanzar(TimeSpan transcurrido)
    {
        if (Fase != FaseCarrusel.Animando) return;
        var ms = Math.Max(0, transcurrido.TotalMilliseconds);
        if (_animacionCompleta)
        {
            if (ms >= _duracionMs) { Aterrizar(_dirAnimacion); return; }
            Colocar(_x0 + (_x1 - _x0) * GestoCaja.CurvaDeSalida(ms / _duracionMs));
        }
        else
        {
            var t = ms / 1000;
            if (GestoCaja.MuelleAsentado(t, _x0, _v0)) { Reposar(); return; }
            Colocar(GestoCaja.PosicionDelMuelle(t, _x0, _v0));
        }
    }

    /// <summary>Termina ya la animacion en curso (una pulsacion o un gesto nuevo no espera a que acabe).</summary>
    public void Concluir()
    {
        if (Fase != FaseCarrusel.Animando) return;
        if (_animacionCompleta) Aterrizar(_dirAnimacion); else Reposar();
    }

    private void Animar(bool completar, int dir, double desde, double hasta, double velocidadInicial, double duracionMs)
    {
        Fase = FaseCarrusel.Animando;
        _animacionCompleta = completar; _dirAnimacion = dir; _x0 = desde; _x1 = hasta; _v0 = velocidadInicial; _duracionMs = Math.Max(1, duracionMs);
        _t0 = _reloj.Elapsed;
        Rejilla.IsHitTestVisible = false;   // un toque a mitad de la transicion caeria en una cuadricula que va a cambiar
        PedirFotograma();
    }

    private void PedirFotograma()
    {
        if (RelojManual || _fotogramaPedido) return;
        if (TopLevel.GetTopLevel(this) is not { } top) { Concluir(); return; }
        _fotogramaPedido = true;
        top.RequestAnimationFrame(_ =>
        {
            _fotogramaPedido = false;
            if (Fase != FaseCarrusel.Animando) return;
            Avanzar(_reloj.Elapsed - _t0);
            if (Fase == FaseCarrusel.Animando) PedirFotograma();
        });
    }

    /// <summary>Pone las dos capas en su sitio para una posicion de la caja que se ve (solo transformaciones: ningun diseno ni reconstruccion).</summary>
    private void Colocar(double x)
    {
        _x = x;
        _transActual.X = x;
        // La vecina es visible desde que esta cargada (aunque aun este fuera de la tarjeta, recortada): asi su diseno se hace al empezar, no en el primer fotograma de la animacion.
        var ver = _dirVecina != 0;
        if (ver) _transVecina.X = x + (_dirVecina > 0 ? _ancho : -_ancho);
        if (CapaVecina.IsVisible != ver) CapaVecina.IsVisible = ver;
    }

    /// <summary>La caja vecina ha entrado del todo: la caja real pasa a mostrar la misma y las dos capas vuelven a su sitio, en un solo paso (ningun fotograma intermedio).</summary>
    private void Aterrizar(int dir)
    {
        var vm = Vm;
        var destino = vm is not null && vm.CajaVecina >= 0 ? vm.CajaVecina : -1;
        Reposar();
        if (vm is not null && destino >= 0) vm.IrACaja(destino);
    }

    /// <summary>Todo en reposo: la caja en su sitio, la vecina oculta y descargada.</summary>
    private void Reposar()
    {
        Fase = FaseCarrusel.Reposo;
        _acumulado = 0; _dirVecina = 0; _x = 0;
        _transActual.X = 0; _transVecina.X = 0;
        if (CapaVecina.IsVisible) CapaVecina.IsVisible = false;
        Rejilla.IsHitTestVisible = true;
        Vm?.DescartarVecina();
    }
}
