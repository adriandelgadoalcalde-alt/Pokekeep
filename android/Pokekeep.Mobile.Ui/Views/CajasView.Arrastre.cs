// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Pokekeep.Mobile.Ui.Design;
using Pokekeep.Mobile.Ui.ViewModels;

namespace Pokekeep.Mobile.Ui.Views;

/// <summary>
/// «Mantener pulsado y arrastrar» (X10), la parte de la vista: traduce el dedo a las operaciones del modelo (<see cref="CajasViewModel.Levantar"/>, <see cref="CajasViewModel.SoltarEn"/>...) y se ocupa de lo que solo la pantalla sabe:
/// el fantasma que va con el dedo, las animaciones (coger, llevar, soltar, intercambiar, volver), la casilla bajo el dedo y pasar a la caja vecina manteniendo el fantasma junto al borde.
/// <para>Flujo: la pulsacion larga (<see cref="Avalonia.Input.HoldingState.Started"/>, el umbral de siempre) levanta el Pokemon si se puede; la vista captura ese dedo (asi ni el carrusel ni el desplazamiento de la pagina lo roban).
/// Si al soltar el dedo no se ha movido mas de <see cref="GestoArrastre.UmbralMovimiento"/>, el gesto es la pulsacion larga de siempre (empieza la seleccion multiple); si se ha movido, se suelta sobre la casilla que haya debajo.
/// Los caminos accesibles («Mover a…» y la seleccion) no cambian.</para>
/// <para>Con «reducir movimiento» no hay ninguna animacion (el fantasma sigue al dedo, que es manipulacion directa, y todo ocurre de golpe), pero el arrastre funciona igual.</para>
/// </summary>
public partial class CajasView
{
    private readonly AnimadorArrastre _animador = new();
    private readonly ScaleTransform _escalaFantasma = new(1, 1), _escalaDesplazado = new(1, 1);
    private readonly TranslateTransform _posFantasma = new(), _posDesplazado = new();
    private IPointer? _punteroPulsado, _punteroLevantado;
    private Point _posPulsacion, _dedoInicial, _dedo, _centroCelda, _centroFantasma;
    private CasillaViewModel? _casillaLevantada;
    private bool _levantado, _haMovido, _fotogramaArrastrePedido;
    private double _celda = 56, _levante, _agarre = 1;      // levante: 0 = en su casilla, 1 = levantado del todo; agarre: 0 = el fantasma esta sobre su casilla, 1 = ya va con el dedo
    private int _bordeDir, _vuelos;
    private TimeSpan _bordeDesde;
    private Vector _offsetFijo;
    private bool _scrollBloqueado, _inerciaAnterior;

    private void BloquearDesplazamiento()
    {
        _offsetFijo = Desplazador.Offset;
        _inerciaAnterior = Desplazador.IsScrollInertiaEnabled;
        Desplazador.SetCurrentValue(ScrollViewer.IsScrollInertiaEnabledProperty, false);
        _scrollBloqueado = true;
    }

    private void LiberarDesplazamiento()
    {
        if (!_scrollBloqueado) return;
        _scrollBloqueado = false;
        Desplazador.SetCurrentValue(ScrollViewer.IsScrollInertiaEnabledProperty, _inerciaAnterior);
    }

    /// <summary>Hay un Pokemon levantado con el dedo todavia en la pantalla.</summary>
    public bool Levantado => _levantado;
    /// <summary>El dedo se ha movido lo bastante para que sea un arrastre (si no, soltar es la pulsacion larga de siempre).</summary>
    public bool Arrastrando => _levantado && _haMovido;
    /// <summary>Centro del fantasma (coordenadas de esta vista).</summary>
    public Point CentroFantasma => _centroFantasma;
    public double EscalaFantasma => _escalaFantasma.ScaleX;
    public bool FantasmaVisible => Fantasma.IsVisible;
    /// <summary>0..1: cuanto lleva el fantasma junto al borde de la caja hacia pasar a la vecina (1 = pasa).</summary>
    public double ProgresoBorde => _bordeDir == 0 ? 0 : Math.Clamp((_animador.Ahora - _bordeDesde).TotalMilliseconds / GestoArrastre.EsperaBordeMs, 0, 1);
    public int DireccionBorde => _bordeDir;
    /// <summary>Hay una animacion del arrastre en marcha (coger, soltar, volver).</summary>
    public bool AnimandoArrastre => _animador.Activo;

    private void IniciarArrastre()
    {
        Desplazador.PropertyChanged += (_, e) =>
        {
            if (_scrollBloqueado && e.Property == ScrollViewer.OffsetProperty && Desplazador.Offset != _offsetFijo)
                Desplazador.SetCurrentValue(ScrollViewer.OffsetProperty, _offsetFijo);
        };
        AddHandler(InputElement.ScrollGestureEvent, (_, e) => { if (_scrollBloqueado) e.Handled = true; }, RoutingStrategies.Tunnel);
        AddHandler(InputElement.ScrollGestureInertiaStartingEvent, (_, e) => { if (_scrollBloqueado) e.Handled = true; }, RoutingStrategies.Tunnel);
        Fantasma.RenderTransform = new TransformGroup { Children = { _escalaFantasma, _posFantasma } };
        FantasmaDesplazado.RenderTransform = new TransformGroup { Children = { _escalaDesplazado, _posDesplazado } };
        // Tunel y «aunque este manejado»: el dedo pulsado se anota siempre; despues de levantar, el movimiento y el soltar son de esta vista.
        AddHandler(InputElement.PointerPressedEvent, AlPulsarDedo, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(InputElement.PointerMovedEvent, AlMoverDedo, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(InputElement.PointerReleasedEvent, AlSoltarDedo, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(InputElement.PointerCaptureLostEvent, AlPerderLaCaptura, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private void AlPulsarDedo(object? sender, PointerPressedEventArgs e)
    {
        if (_levantado) return;   // un segundo dedo no hace nada mientras hay un Pokemon en la mano
        _punteroPulsado = e.Pointer;
        _posPulsacion = e.GetPosition(this);
    }

    private void AlMoverDedo(object? sender, PointerEventArgs e)
    {
        if (!_levantado || !ReferenceEquals(e.Pointer, _punteroLevantado)) return;
        e.Handled = true;
        MoverDedo(e.GetPosition(this));
    }

    private void AlSoltarDedo(object? sender, PointerReleasedEventArgs e)
    {
        if (!_levantado || !ReferenceEquals(e.Pointer, _punteroLevantado)) return;
        e.Handled = true;
        SoltarDedo(e.GetPosition(this));
    }

    private void AlPerderLaCaptura(object? sender, PointerCaptureLostEventArgs e)
    {
        // Si el sistema le quita el dedo a la vista (otra aplicacion, una llamada...) el Pokemon vuelve a su casilla en vez de quedarse en el aire.
        if (_levantado && ReferenceEquals(e.Pointer, _punteroLevantado)) AbandonarArrastre();
    }

    /// <summary>La vista se va o cambia de partida con un Pokemon en la mano: se deja todo como estaba, sin animar ni escribir.</summary>
    private void AbandonarArrastre()
    {
        var vm = Vm;
        LiberarDesplazamiento();
        _levantado = false; _haMovido = false; _casillaLevantada = null;
        SoltarPuntero();
        _bordeDir = 0;
        vm?.CancelarArrastre();
        TerminarArrastre(inmediato: true);
    }

    /// <summary>Pulsacion larga: levanta el Pokemon si se puede (ver <see cref="CajasViewModel.PuedeLevantar"/>); si no, hace lo de siempre (selecciona, abre el menu de una casilla vacia, avisa si es de solo lectura).</summary>
    private void AlMantenerArrastre(CasillaViewModel casilla, HoldingRoutedEventArgs e)
    {
        var vm = Vm!;
        if (_punteroPulsado is { } p && vm.PuedeLevantar(casilla, out _) && Levantar(casilla, _posPulsacion))
        {
            _punteroLevantado = p;
            p.Capture(this);
            e.Handled = true;
            return;
        }
        vm.PuedeLevantar(casilla, out var motivo);
        if (motivo is not null) vm.AvisarSoloLectura(motivo);
        vm.PulsacionLarga(casilla);
        e.Handled = true;
    }

    /// <summary>Levanta el Pokemon de <paramref name="casilla"/> con el dedo en <paramref name="dedo"/> (coordenadas de esta vista): escala, sombra y vibracion. Las pruebas la llaman directamente; el gesto real, desde la pulsacion larga.</summary>
    public bool Levantar(CasillaViewModel casilla, Point dedo)
    {
        if (Vm is not { } vm || _levantado) return false;
        Concluir();
        _animador.Concluir();
        if (!vm.Levantar(casilla)) return false;
        _levantado = true; _haMovido = false; _casillaLevantada = casilla;
        BloquearDesplazamiento();
        _dedoInicial = _dedo = dedo;
        var celda = ControlDe(casilla);
        if (celda?.GetVisualDescendants().OfType<SpritePokemon>().FirstOrDefault() is { } origenSprite &&
            Fantasma.GetVisualDescendants().OfType<SpritePokemon>().FirstOrDefault() is { } sprite)
        {
            // La casilla no seleccionada tiene otro grosor de borde. Copiar el área real evita
            // que el redondeo entero del pixel art salte de escala al levantarla.
            sprite.Width = origenSprite.Bounds.Width; sprite.Height = origenSprite.Bounds.Height;
            sprite.HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center;
            sprite.VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center;
        }
        _celda = celda is { Bounds.Width: > 0, Bounds.Height: > 0 } ? Math.Min(celda.Bounds.Width, celda.Bounds.Height) : 56;
        _centroCelda = celda is null ? dedo : CentroDe(celda);
        _centroFantasma = _centroCelda;
        _agarre = 0; _levante = 0;
        PrepararFantasma(Fantasma, FantasmaSombra, vm.Fantasma, _centroCelda);
        FantasmaCantidad.IsVisible = vm.ArrastreCantidad > 1;
        FantasmaTexto.Text = vm.ArrastreCantidad.ToString();
        FantasmaDesplazado.IsVisible = false;
        CapaArrastre.IsVisible = true;
        Haptica.PulsoCorto();
        if (Movimiento.Reducido) { _levante = 1; AplicarFantasma(); }
        else { AplicarFantasma(); _animador.Iniciar(0, 1, GestoArrastre.DuracionAgarreMs, v => { _levante = v; if (_levantado) AplicarFantasma(); }, clave: "levante"); PedirFotogramaArrastre(); }
        return true;
    }

    private void PrepararFantasma(Panel fantasma, Border? sombra, CasillaViewModel? datos, Point centro)
    {
        fantasma.DataContext = datos;
        fantasma.Width = fantasma.Height = _celda;
        Canvas.SetLeft(fantasma, 0); Canvas.SetTop(fantasma, 0);
        fantasma.IsVisible = true;
        if (sombra is not null) sombra.Opacity = 0;
        PonerFantasma(fantasma == Fantasma ? _escalaFantasma : _escalaDesplazado, fantasma == Fantasma ? _posFantasma : _posDesplazado, centro, 1);
    }

    private void PonerFantasma(ScaleTransform escala, TranslateTransform pos, Point centro, double factor)
    {
        escala.ScaleX = escala.ScaleY = factor;
        pos.X = centro.X - _celda / 2; pos.Y = centro.Y - _celda / 2;
    }

    /// <summary>Coloca el fantasma segun el estado del gesto: sobre su casilla al principio, y con el dedo (algo por encima) cuando ya se mueve, mezclando los dos mientras dura el «agarre».</summary>
    private void AplicarFantasma()
    {
        var objetivo = new Point(_dedo.X, _dedo.Y - GestoArrastre.AlturaFantasmaPara(_celda));
        var f = _haMovido ? _agarre : 0;
        _centroFantasma = new Point(_centroCelda.X + (objetivo.X - _centroCelda.X) * f, _centroCelda.Y + (objetivo.Y - _centroCelda.Y) * f);
        PonerFantasma(_escalaFantasma, _posFantasma, _centroFantasma, 1 + (GestoArrastre.EscalaLevantado - 1) * _levante);
        FantasmaSombra.Opacity = _levante;
    }

    /// <summary>El dedo se ha movido a <paramref name="dedo"/> (coordenadas de esta vista). Pasado el umbral, empieza el arrastre: aparece la zona de destino extra y el fantasma pasa a seguir al dedo.</summary>
    public void MoverDedo(Point dedo)
    {
        if (!_levantado) return;
        _dedo = dedo;
        if (!_haMovido)
        {
            var dx = dedo.X - _dedoInicial.X; var dy = dedo.Y - _dedoInicial.Y;
            if (Math.Sqrt(dx * dx + dy * dy) < GestoArrastre.UmbralMovimiento) return;
            _haMovido = true;
            Vm?.EmpezarMovimiento();
            EntrarModoArrastre();   // uso10: caja y equipo a la vista y centrados, la zona pegada a su sitio (CajasView.ModoArrastre.cs)
            if (Movimiento.Reducido) _agarre = 1;
            else { _agarre = 0; _animador.Iniciar(0, 1, GestoArrastre.DuracionAgarreMs, v => { _agarre = v; if (_levantado) AplicarFantasma(); }, clave: "agarre"); PedirFotogramaArrastre(); }
        }
        if (!RelojManual) _animador.Avanzar(_reloj.Elapsed);
        AlFotogramaArrastre();
        PedirFotogramaArrastre(); // también con reducir movimiento: el paso de caja por el borde necesita reloj.
    }

    /// <summary>Un fotograma del arrastre: coloca el fantasma, resalta la casilla de debajo y cuenta el tiempo junto al borde.</summary>
    private void AlFotogramaArrastre()
    {
        if (!_levantado) return;
        AplicarFantasma();
        if (_haMovido) { ActualizarSobrevuelo(); EvaluarBorde(); }
    }

    private void ActualizarSobrevuelo()
    {
        if (Vm is not { } vm) return;
        vm.Sobrevolar(Fase == FaseCarrusel.Reposo ? CasillaBajo(_dedo) : null);
        ResalteDestino.IsVisible = false;
        if (vm.SobreArrastre is { } c && (c.ObjetivoSoltar || c.ObjetivoRechazado) && ControlDe(c) is { } celda)
        {
            var p = celda.TranslatePoint(default, this) ?? default;
            ResalteDestino.Width = celda.Bounds.Width + 6; ResalteDestino.Height = celda.Bounds.Height + 6;
            Canvas.SetLeft(ResalteDestino, p.X - 3); Canvas.SetTop(ResalteDestino, p.Y - 3);
            if (c.ObjetivoRechazado) ResalteDestino.Classes.Add("rechazo"); else ResalteDestino.Classes.Remove("rechazo");
            ResalteDestino.IsVisible = true;
        }
    }

    private Border CajaDeDestino => Vm is { EsCajas: true } ? TarjetaCaja : MarcoApoyoCaja;   // el borde de la caja, no su etiqueta

    /// <summary>Mantener el dedo en la franja del borde de la caja el tiempo fijado pasa a la caja vecina con la animacion del carrusel; sigue el arrastre y, si se mantiene, otra vez lo mismo para la siguiente.</summary>
    private void EvaluarBorde()
    {
        var dir = 0;
        var tarjeta = CajaDeDestino;
        if (Vm is { TotalCajas: > 1 } vm && (vm.EsCajas || vm.MostrarApoyoCaja) && Fase == FaseCarrusel.Reposo && tarjeta.IsEffectivelyVisible && tarjeta.Bounds.Width > 0)
        {
            var origen = tarjeta.TranslatePoint(default, this) ?? default;
            var fin = tarjeta.TranslatePoint(new Point(tarjeta.Bounds.Width, tarjeta.Bounds.Height), this) ?? origen;   // con la escala del modo arrastre
            var izq = origen.X; var der = fin.X;
            var enVertical = _dedo.Y >= origen.Y - GestoArrastre.MargenVerticalBorde && _dedo.Y <= fin.Y + GestoArrastre.MargenVerticalBorde;
            if (enVertical) dir = _dedo.X < izq + GestoArrastre.ZonaBorde ? -1 : _dedo.X > der - GestoArrastre.ZonaBorde ? +1 : 0;
        }
        if (dir != _bordeDir) { _bordeDir = dir; _bordeDesde = _animador.Ahora; }
        var p = ProgresoBorde;
        PonerAvisosDeBorde(dir, p);
        if (dir != 0 && p >= 1)
        {
            _bordeDir = 0; PonerAvisosDeBorde(0, 0);
            if (Vm is { EsCajas: false } apoyo) apoyo.CambiarCajaDeApoyo(dir);
            else Desplazar(dir);
            // Con «reducir movimiento» el cambio es inmediato y la fase no pasa por Animando: que la espera vuelva a empezar de cero.
            _bordeDir = dir; _bordeDesde = _animador.Ahora;
        }
    }

    private void PonerAvisosDeBorde(int dir, double progreso)
    {
        var tarjeta = CajaDeDestino;
        var o = tarjeta.IsEffectivelyVisible ? tarjeta.TranslatePoint(default, this) ?? default : default;
        var f = tarjeta.IsEffectivelyVisible ? tarjeta.TranslatePoint(new Point(tarjeta.Bounds.Width, tarjeta.Bounds.Height), this) ?? o : o;
        var alto = Math.Max(0, f.Y - o.Y);
        foreach (var (aviso, lado) in new[] { (AvisoBordeIzq, -1), (AvisoBordeDer, +1) })
        {
            aviso.Height = alto;
            Canvas.SetTop(aviso, o.Y);
            Canvas.SetLeft(aviso, lado < 0 ? o.X : f.X - aviso.Width);
            aviso.Opacity = dir == lado ? 0.25 + 0.75 * progreso : 0;
        }
    }

    /// <summary>Suelta el Pokemon con el dedo en <paramref name="dedo"/>. Sin haber movido el dedo: la pulsacion larga de siempre. Sobre una casilla que admite: se mueve o se intercambia. En cualquier otro sitio: vuelve volando a su casilla.</summary>
    public void SoltarDedo(Point dedo)
    {
        if (!_levantado || Vm is not { } vm) return;
        _dedo = dedo;
        var origenVm = _casillaLevantada;
        var movido = _haMovido;
        LiberarDesplazamiento();
        _levantado = false; _haMovido = false; _casillaLevantada = null;
        ResalteDestino.IsVisible = false;
        SoltarPuntero();
        PonerAvisosDeBorde(0, 0); _bordeDir = 0;

        if (!movido)
        {
            // Pulsacion larga de siempre: el fantasma se apaga y empieza la seleccion (o se alterna la marca).
            OcultarFantasmas();
            vm.CancelarArrastre();
            vm.LiberarArrastre();
            if (origenVm is not null) vm.PulsacionLarga(origenVm);
            return;
        }

        Concluir();   // una transicion de caja a medias no espera: la caja que se ve es la que cuenta
        var objetivo = CasillaBajo(dedo);
        var desde = _centroFantasma;
        var escalaDesde = _escalaFantasma.ScaleX; var sombraDesde = FantasmaSombra.Opacity;
        var origenAunVisible = origenVm is not null && vm.OrigenArrastre is { } oa && origenVm.Ref is { } orv && SigueEnSuSitio(oa, orv) ? origenVm : null;
        var centroOrigen = origenAunVisible is null ? (Point?)null : ControlDe(origenAunVisible) is { } co ? CentroDe(co) : null;
        var centroDestino = objetivo is null ? (Point?)null : ControlDe(objetivo) is { } cd ? CentroDe(cd) : null;

        var r = vm.SoltarEn(objetivo);
        if (r.Aplicado && centroDestino is { } destino)
        {
            Volar(_escalaFantasma, _posFantasma, Fantasma, FantasmaSombra, desde, destino, escalaDesde, 1, sombraDesde, 0, GestoArrastre.DuracionSoltarMs, rebote: true);
            if (r.Desplazado is { } desplazado && centroOrigen is { } hacia)
            {
                PrepararFantasma(FantasmaDesplazado, null, desplazado, destino);
                Volar(_escalaDesplazado, _posDesplazado, FantasmaDesplazado, null, destino, hacia, 1, 1, 0, 0, GestoArrastre.DuracionIntercambioMs);
            }
        }
        else
        {
            // No se ha movido nada (fuera de todo destino, casilla que no admite, el propio origen): vuelve a su casilla.
            if (centroOrigen is { } vuelta) Volar(_escalaFantasma, _posFantasma, Fantasma, FantasmaSombra, desde, vuelta, escalaDesde, 1, sombraDesde, 0, GestoArrastre.DuracionVueltaMs);
            else Volar(_escalaFantasma, _posFantasma, Fantasma, FantasmaSombra, desde, desde, escalaDesde, escalaDesde * 0.8, sombraDesde, 0, GestoArrastre.DuracionVueltaMs, desvanecer: true);
        }
        if (_vuelos == 0) TerminarArrastre();
    }

    private static bool SigueEnSuSitio(Cajas.CasillaRef a, Cajas.CasillaRef b) => (a.Info, b.Info) switch
    {
        (PKHeX.Core.SlotInfoBox x, PKHeX.Core.SlotInfoBox y) => x.Box == y.Box && x.Slot == y.Slot,
        (PKHeX.Core.SlotInfoParty x, PKHeX.Core.SlotInfoParty y) => x.Slot == y.Slot,
        (PKHeX.Core.SlotInfoMisc x, PKHeX.Core.SlotInfoMisc y) => x.Type == y.Type && x.Slot == y.Slot && x.Data.Equals(y.Data),
        _ => false,
    };

    /// <summary>Cancela el gesto (el sistema quita el dedo, la vista se va): el Pokemon vuelve a su casilla sin escribir nada.</summary>
    public void CancelarLevantado() => SoltarComoCancelado();

    private void SoltarComoCancelado()
    {
        LiberarDesplazamiento();
        ResalteDestino.IsVisible = false;
        if (!_levantado || Vm is not { } vm) return;
        var origenVm = _casillaLevantada;
        var movido = _haMovido;
        _levantado = false; _haMovido = false; _casillaLevantada = null;
        SoltarPuntero();
        PonerAvisosDeBorde(0, 0); _bordeDir = 0;
        var desde = _centroFantasma; var escalaDesde = _escalaFantasma.ScaleX; var sombraDesde = FantasmaSombra.Opacity;
        var centroOrigen = origenVm is not null && ControlDe(origenVm) is { } co ? CentroDe(co) : (Point?)null;
        vm.CancelarArrastre();
        if (!movido) { OcultarFantasmas(); vm.LiberarArrastre(); return; }
        if (centroOrigen is { } vuelta) Volar(_escalaFantasma, _posFantasma, Fantasma, FantasmaSombra, desde, vuelta, escalaDesde, 1, sombraDesde, 0, GestoArrastre.DuracionVueltaMs);
        else Volar(_escalaFantasma, _posFantasma, Fantasma, FantasmaSombra, desde, desde, escalaDesde, escalaDesde * 0.8, sombraDesde, 0, GestoArrastre.DuracionVueltaMs, desvanecer: true);
        if (_vuelos == 0) TerminarArrastre();
    }

    private void SoltarPuntero()
    {
        var p = _punteroLevantado; _punteroLevantado = null;
        if (p is not null && ReferenceEquals(p.Captured, this)) p.Capture(null);
    }

    /// <summary>Anima un fantasma de un punto a otro (con la curva de salida); sin animacion con «reducir movimiento».</summary>
    private void Volar(ScaleTransform escala, TranslateTransform pos, Panel fantasma, Border? sombra, Point desde, Point hasta, double escalaDesde, double escalaHasta, double sombraDesde, double sombraHasta, double ms, bool desvanecer = false, bool rebote = false)
    {
        if (Movimiento.Reducido) { fantasma.IsVisible = false; return; }
        _vuelos++;
        _animador.Iniciar(0, 1, ms, v =>
        {
            PonerFantasma(escala, pos, new Point(desde.X + (hasta.X - desde.X) * v, desde.Y + (hasta.Y - desde.Y) * v), escalaDesde + (escalaHasta - escalaDesde) * v + (rebote ? escalaHasta * GestoArrastre.Rebote(v) : 0));   // rebote corto al aterrizar (uso10)
            if (sombra is not null) sombra.Opacity = sombraDesde + (sombraHasta - sombraDesde) * v;
            fantasma.Opacity = desvanecer ? 1 - v : 1;
        }, fin: () =>
        {
            fantasma.IsVisible = false; fantasma.Opacity = 1;
            if (--_vuelos <= 0) { _vuelos = 0; if (!_levantado) TerminarArrastre(); }
        });
        PedirFotogramaArrastre();
    }

    private void OcultarFantasmas()
    {
        _animador.Descartar(); _vuelos = 0;
        Fantasma.IsVisible = false; FantasmaDesplazado.IsVisible = false; Fantasma.Opacity = 1; FantasmaDesplazado.Opacity = 1;
        CapaArrastre.IsVisible = false;
        ResalteDestino.IsVisible = false;
    }

    /// <summary>Todo ha acabado: se esconde la capa, la página vuelve a su sitio (modo arrastre) y el modelo suelta el fantasma y las zonas de destino extra.</summary>
    private void TerminarArrastre(bool inmediato = false)
    {
        OcultarFantasmas();
        var vm = Vm;
        SalirModoArrastre(inmediato, () => vm?.LiberarArrastre());
    }

    /// <summary>Avanza <paramref name="delta"/> las animaciones y el tiempo junto al borde (solo pruebas, con <see cref="RelojManual"/>); en la aplicacion lo hace el reloj de fotogramas.</summary>
    public void AvanzarArrastre(TimeSpan delta)
    {
        _animador.Avanzar(_animador.Ahora + delta);
        AlFotogramaArrastre();
        // Una caja que cambia sola (el carrusel acaba de aterrizar) cambia lo que hay bajo el dedo.
    }

    /// <summary>Termina ya todas las animaciones del arrastre (solo pruebas).</summary>
    public void ConcluirArrastre() { _animador.Concluir(); }

    private void PedirFotogramaArrastre()
    {
        if (RelojManual || _fotogramaArrastrePedido) return;
        if (TopLevel.GetTopLevel(this) is not { } top) { _animador.Concluir(); return; }
        _fotogramaArrastrePedido = true;
        top.RequestAnimationFrame(_ =>
        {
            _fotogramaArrastrePedido = false;
            _animador.Avanzar(_reloj.Elapsed);
            AlFotogramaArrastre();
            if (_levantado || _animador.Activo) PedirFotogramaArrastre();
        });
    }

    // ---- Localizar casillas en la vista ----
    private Button? ControlDe(CasillaViewModel c) => this.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => ReferenceEquals(b.DataContext, c) && b.IsEffectivelyVisible && b.Bounds.Width > 0);

    private Point CentroDe(Control c) => c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), this) ?? default;

    /// <summary>La casilla (de la caja que se ve, del equipo o de la zona de destino extra) que hay bajo el punto; null si no hay ninguna.</summary>
    public CasillaViewModel? CasillaBajo(Point punto)
    {
        if (Vm is not { } vm) return null;
        for (var v = this.InputHitTest(punto) as Visual; v is not null; v = v.GetVisualParent())
            if (v is Control { DataContext: CasillaViewModel c } && c.Ref is not null && !ReferenceEquals(c, vm.Fantasma) && (vm.CasillasCaja.Contains(c) || vm.CasillasEquipo.Contains(c) || vm.Grupos.Any(g => g.Casillas.Contains(c)))) return c;
        return null;
    }

    /// <summary>Centro (coordenadas de esta vista) de una casilla visible, o null si no esta a la vista.</summary>
    public Point? CentroDeCasilla(CasillaViewModel c) => ControlDe(c) is { } b ? CentroDe(b) : null;
}
