// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Pokekeep.Mobile.Ui.Design;

namespace Pokekeep.Mobile.Ui.Views;

/// <summary>
/// «Modo arrastre» (uso10, propuesta aprobada por el propietario en la QA de uso9): en cuanto el dedo empieza a llevar un Pokémon,
/// la página se coloca sola para que la caja y el equipo queden a la vista y centrados, y la zona de destino que no estaba en la
/// página entra pegada a su sitio, nunca encima de la caja:
/// <list type="bullet">
/// <item>desde Cajas, el equipo justo DEBAJO de la caja;</item>
/// <item>desde Equipo, la caja actual ENCIMA y el equipo justo debajo (el equipo de la página pasa a su zona de apoyo);</item>
/// <item>desde Otros, la caja y el equipo debajo de la ranura de origen, que se queda a la vista arriba.</item>
/// </list>
/// Con letra grande, si no caben, la caja se encoge solo mientras dura el arrastre (las casillas nunca bajan de
/// <see cref="CeldaMinima"/> dp). Durante el arrastre nada más se mueve; al soltar o cancelar la zona sale, la caja recupera su
/// tamaño y la página vuelve a la posición que tenía antes de levantar el Pokémon. Con «reducir movimiento», todo de golpe.
/// </summary>
public partial class CajasView
{
    /// <summary>Lado mínimo de una casilla de la caja mientras se encoge para caber con el equipo (dp).</summary>
    public const double CeldaMinima = 40;
    private const double Hueco = 12, MargenModo = 12, DuracionModoMs = 200, DuracionSalidaMs = 100, Entrada = 36;

    private readonly ScaleTransform _escalaCajaModo = new(1, 1);
    private readonly TranslateTransform _entradaZona = new();
    private bool _modoActivo;
    private Vector _offsetAntesModo;
    private Border? _zonaModo, _cajaModo;

    /// <summary>Hay un «modo arrastre» en marcha (pruebas).</summary>
    public bool ModoArrastreActivo => _modoActivo;
    /// <summary>Escala de la caja durante el arrastre (1 si cabe a su tamaño).</summary>
    public double EscalaCajaArrastre => _escalaCajaModo.ScaleX;

    /// <summary>
    /// Coloca la página y la zona de destino al empezar a mover el dedo (después de <see cref="ViewModels.CajasViewModel.EmpezarMovimiento"/>).
    /// Regla única: la zona que no estaba a la vista entra JUSTO DEBAJO de donde se arrastra (el equipo bajo la caja; la caja bajo
    /// el equipo) y la página centra el conjunto. Si no cabe, se reduce la caja: la de la página con una escala (solo durante el
    /// arrastre) y la de apoyo con un ancho menor, que reparte las casillas más pequeñas sin transparencias; nunca por debajo de
    /// <see cref="CeldaMinima"/> dp por casilla.
    /// </summary>
    private void EntrarModoArrastre()
    {
        if (Vm is not { } vm || _modoActivo) return;
        Control ancla; Border zona; var cajaEsAncla = true;
        if (vm.EsCajas && vm.MostrarApoyoEquipo) { ancla = TarjetaCaja; zona = ApoyoEquipo; }
        else { if (vm.EsEquipo && vm.MostrarApoyoCaja && vm.MostrarApoyoEquipo) EntrarModoEquipo(); else if (vm.EsOtros && (vm.MostrarApoyoCaja || vm.MostrarApoyoEquipo)) EntrarModoOtros(); return; }

        EspacioArrastre.Height = Desplazador.Bounds.Height;   // la página puede subir el ancla hasta arriba aunque el contenido sea corto
        UpdateLayout();
        var alto = AltoVisible();
        var altoAncla = ancla.Bounds.Height;
        var altoZona = Medir(zona);
        var minima = EscalaParaCaber(0, 1, double.MaxValue);
        // Cuánto hay que reducir la caja (la que sea) para que ancla + hueco + zona quepan en la pantalla.
        var altoCaja = cajaEsAncla ? altoAncla : altoZona;
        var otro = cajaEsAncla ? altoZona : altoAncla;
        // La caja de la página se escala entera; en la de apoyo solo se estrecha la cuadrícula (los textos de arriba no cambian).
        var escala = cajaEsAncla ? EscalaParaCaber(alto, altoCaja, otro) : EscalaApoyo(alto - 2 * MargenModo - Hueco - altoAncla, altoZona);
        var altoAnclaFin = cajaEsAncla ? altoAncla * escala : altoAncla;
        var altoZonaFin = cajaEsAncla ? altoZona : AltoApoyoCaja(altoZona, escala);
        var total = altoAnclaFin + Hueco + altoZonaFin;

        _offsetAntesModo = Desplazador.Offset;
        var yAncla0 = ancla.TranslatePoint(default, this)?.Y ?? 0;
        // Si el conjunto ya se ve entero donde está, la página no se mueve (menos movimiento bajo el dedo); si no, se centra.
        var yaCabe = yAncla0 >= MargenModo && yAncla0 + total <= alto - MargenModo;
        var offsetFinal = yaCabe ? _offsetAntesModo.Y : Limitar(_offsetAntesModo.Y + (yAncla0 - Math.Max(MargenModo, (alto - total) / 2)));
        // Si la página no puede desplazarse lo necesario (final del contenido), se reduce un poco más para que la zona quepa debajo.
        var yAnclaFin = yAncla0 - (offsetFinal - _offsetAntesModo.Y);
        var sitio = alto - MargenModo - Hueco - yAnclaFin - otro * (cajaEsAncla ? 1 : 0) - (cajaEsAncla ? 0 : altoAncla);
        if (cajaEsAncla) { if (altoCaja * escala > sitio) escala = Math.Clamp(sitio / altoCaja, minima, escala); }
        else if (AltoApoyoCaja(altoZona, escala) > sitio) escala = Math.Min(escala, EscalaApoyo(sitio, altoZona));
        altoAnclaFin = cajaEsAncla ? altoAncla * escala : altoAncla;

        _modoActivo = true; _zonaModo = zona; _cajaModo = cajaEsAncla ? TarjetaCaja : null;
        PrepararZona(zona);
        if (cajaEsAncla)
        {
            TarjetaCaja.RenderTransformOrigin = new RelativePoint(0.5, 0, RelativeUnit.Relative);
            TarjetaCaja.RenderTransform = _escalaCajaModo;
        }
        else if (escala < 1)
        {
            EstrecharRejillaApoyo(escala);
        }

        // La zona se coloca desde el primer instante en su sitio final (no salta): durante la animación la página lleva la caja hasta
        // ella y la caja se encoge, mientras la zona entra deslizándose. Nunca sale de la pantalla.
        var yZona = Math.Min(yAnclaFin + altoAnclaFin + Hueco, alto - MargenModo - (cajaEsAncla ? altoZona : AltoApoyoCaja(altoZona, escala)));
        zona.Margin = new Thickness(16, yZona, 16, 0);
        PonerVelo(yZona - Hueco / 2, alto);   // justo encima de la zona: es donde empieza lo que no debe asomar
        void Aplicar(double v)
        {
            FijarOffset(_offsetAntesModo.Y + (offsetFinal - _offsetAntesModo.Y) * v);
            if (cajaEsAncla) _escalaCajaModo.ScaleX = _escalaCajaModo.ScaleY = 1 + (escala - 1) * v;
            _entradaZona.Y = Entrada * (1 - v);
            zona.Opacity = v; VeloArrastre.Opacity = v;
        }
        if (Movimiento.Reducido) Aplicar(1);
        else { Aplicar(0); _animador.Iniciar(0, 1, DuracionModoMs, Aplicar, clave: "modo"); PedirFotogramaArrastre(); }
    }

    /// <summary>
    /// Desde Equipo (release, decisión del propietario: «esa animación para todo»): el mismo montaje que desde Cajas, la caja arriba y el equipo
    /// justo debajo, centrados en lo que se ve y entrando con la misma animación. El equipo de la página se sustituye por su zona de apoyo.
    /// Si no cabe, la caja se estrecha (casillas de al menos <see cref="CeldaMinima"/> dp).
    /// </summary>
    private void EntrarModoEquipo()
    {
        if (Vm is null) return;
        EspacioArrastre.Height = Desplazador.Bounds.Height;
        UpdateLayout();
        var alto = AltoVisible();
        var altoCaja = Medir(ApoyoCaja);
        var altoEquipo = Medir(ApoyoEquipo);
        var escala = EscalaApoyo(alto - 2 * MargenModo - Hueco - altoEquipo, altoCaja);
        var altoCajaFin = AltoApoyoCaja(altoCaja, escala);
        var total = altoCajaFin + Hueco + altoEquipo;

        _offsetAntesModo = Desplazador.Offset;
        _modoActivo = true; _cajaModo = null; _zonaModo = ApoyoCaja;
        PrepararZona(ApoyoCaja); PrepararZona(ApoyoEquipo);
        if (escala < 1) EstrecharRejillaApoyo(escala);
        var yCaja = Math.Max(MargenModo, (alto - total) / 2);
        ApoyoCaja.Margin = new Thickness(16, yCaja, 16, 0);
        ApoyoEquipo.Margin = new Thickness(16, yCaja + altoCajaFin + Hueco, 16, 0);
        PonerVelo(yCaja - Hueco / 2, alto);
        void Aplicar(double v)
        {
            _entradaZona.Y = Entrada * (1 - v);
            ApoyoCaja.Opacity = v; ApoyoEquipo.Opacity = v; VeloArrastre.Opacity = v;
        }
        if (Movimiento.Reducido) Aplicar(1);
        else { Aplicar(0); _animador.Iniciar(0, 1, DuracionModoMs, Aplicar, clave: "modo"); PedirFotogramaArrastre(); }
    }

    /// <summary>
    /// Desde Otros, con la misma regla que Cajas y Equipo (QA del propietario: antes flotaban abajo y la página quedaba cortada
    /// detrás): la caja y, debajo, el equipo entran pegados JUSTO DEBAJO de la tarjeta desde la que se arrastra (o de la propia
    /// ranura si la tarjeta es muy alta) y la página centra el conjunto. Si no cabe, la caja se estrecha (casillas de al menos
    /// <see cref="CeldaMinima"/> dp).
    /// </summary>
    private void EntrarModoOtros()
    {
        if (Vm is not { } vm || _casillaLevantada is not { } origenVm || ControlDe(origenVm) is not { } origen) return;
        EspacioArrastre.Height = Desplazador.Bounds.Height;
        UpdateLayout();
        var alto = AltoVisible();
        Control ancla = origen.GetVisualAncestors().OfType<Border>().FirstOrDefault(b => b.Classes.Contains("tarjeta")) is { } tarjeta && tarjeta.Bounds.Height <= alto * 0.45 ? tarjeta : origen;
        var altoAncla = ancla.Bounds.Height;
        var altoCaja = vm.MostrarApoyoCaja ? Medir(ApoyoCaja) : 0;
        var altoEquipo = vm.MostrarApoyoEquipo ? Medir(ApoyoEquipo) : 0;
        var huecos = (vm.MostrarApoyoCaja ? Hueco : 0) + (vm.MostrarApoyoEquipo ? Hueco : 0);
        var escala = vm.MostrarApoyoCaja ? EscalaApoyo(alto - 2 * MargenModo - huecos - altoAncla - altoEquipo, altoCaja) : 1;
        var total = altoAncla + huecos + AltoApoyoCaja(altoCaja, escala) + altoEquipo;

        _offsetAntesModo = Desplazador.Offset;
        var yAncla0 = ancla.TranslatePoint(default, this)?.Y ?? 0;
        var yaCabe = yAncla0 >= MargenModo && yAncla0 + total <= alto - MargenModo;
        var offsetFinal = yaCabe ? _offsetAntesModo.Y : Limitar(_offsetAntesModo.Y + (yAncla0 - Math.Max(MargenModo, (alto - total) / 2)));
        var yAnclaFin = yAncla0 - (offsetFinal - _offsetAntesModo.Y);
        var sitio = alto - MargenModo - huecos - altoEquipo - (yAnclaFin + altoAncla);
        if (vm.MostrarApoyoCaja && AltoApoyoCaja(altoCaja, escala) > sitio) escala = Math.Min(escala, EscalaApoyo(sitio, altoCaja));

        _modoActivo = true; _cajaModo = null;
        _zonaModo = vm.MostrarApoyoCaja ? ApoyoCaja : ApoyoEquipo;
        var y = yAnclaFin + altoAncla + Hueco;
        if (vm.MostrarApoyoCaja)
        {
            PrepararZona(ApoyoCaja);
            if (escala < 1) EstrecharRejillaApoyo(escala);
            var altoFin = AltoApoyoCaja(altoCaja, escala);
            ApoyoCaja.Margin = new Thickness(16, Math.Min(y, alto - MargenModo - altoFin - (vm.MostrarApoyoEquipo ? Hueco + altoEquipo : 0)), 16, 0);
            y = ApoyoCaja.Margin.Top + altoFin + Hueco;
        }
        if (vm.MostrarApoyoEquipo)
        {
            PrepararZona(ApoyoEquipo);
            ApoyoEquipo.Margin = new Thickness(16, Math.Min(y, alto - MargenModo - altoEquipo), 16, 0);
        }
        PonerVelo((vm.MostrarApoyoCaja ? ApoyoCaja.Margin.Top : ApoyoEquipo.Margin.Top) - Hueco / 2, alto);
        void Aplicar(double v)
        {
            FijarOffset(_offsetAntesModo.Y + (offsetFinal - _offsetAntesModo.Y) * v);
            _entradaZona.Y = Entrada * (1 - v);
            if (vm.MostrarApoyoCaja) ApoyoCaja.Opacity = v;
            if (vm.MostrarApoyoEquipo) ApoyoEquipo.Opacity = v;
            VeloArrastre.Opacity = v;
        }
        if (Movimiento.Reducido) Aplicar(1);
        else { Aplicar(0); _animador.Iniciar(0, 1, DuracionModoMs, Aplicar, clave: "modo"); PedirFotogramaArrastre(); }
    }

    /// <summary>
    /// Fin del arrastre: la zona sale, la caja recupera su tamaño y la página vuelve a donde estaba antes de levantar el Pokémon.
    /// <paramref name="despues"/> se ejecuta al terminar (suelta las zonas del modelo); con <paramref name="inmediato"/> (la app se va,
    /// el sistema quita el dedo) o con «reducir movimiento» todo es de golpe.
    /// </summary>
    private void SalirModoArrastre(bool inmediato, Action despues)
    {
        if (!_modoActivo) { despues(); return; }
        var desde = Desplazador.Offset.Y; var hasta = Limitar(_offsetAntesModo.Y); var escalaDesde = _escalaCajaModo.ScaleX;
        var zona = _zonaModo; var caja = _cajaModo;
        void Aplicar(double v)
        {
            Desplazador.SetCurrentValue(ScrollViewer.OffsetProperty, new Vector(Desplazador.Offset.X, desde + (hasta - desde) * v));
            _escalaCajaModo.ScaleX = _escalaCajaModo.ScaleY = escalaDesde + (1 - escalaDesde) * v;
            _entradaZona.Y = Entrada * v;
            foreach (var z in new[] { ApoyoEquipo, ApoyoCaja }) if (z.IsVisible) z.Opacity = 1 - v;
            VeloArrastre.Opacity = 1 - v;
        }
        void Terminar()
        {
            _modoActivo = false; _zonaModo = null; _cajaModo = null;
            EspacioArrastre.Height = 0; VeloArrastre.IsVisible = false; VeloArrastre.Opacity = 1;   // la página ya está donde estaba antes de levantar el Pokémon, dentro del contenido de siempre
            foreach (var b in new[] { TarjetaCaja, ApoyoCaja }) { b.RenderTransform = null; b.RenderTransformOrigin = RelativePoint.Center; }
            _escalaCajaModo.ScaleX = _escalaCajaModo.ScaleY = 1; _entradaZona.Y = 0;
            foreach (var z in new[] { ApoyoEquipo, ApoyoCaja })
            {
                z.Opacity = 1; z.RenderTransform = null; z.Width = double.NaN; z.HorizontalAlignment = HorizontalAlignment.Stretch;
                MarcoApoyoCaja.MaxWidth = double.PositiveInfinity; MarcoApoyoCaja.HorizontalAlignment = HorizontalAlignment.Stretch;
                z.VerticalAlignment = z == ApoyoEquipo ? VerticalAlignment.Bottom : VerticalAlignment.Top;
                z.Margin = z == ApoyoEquipo ? new Thickness(16, 0, 16, 16) : new Thickness(16, 0, 16, 0);
            }
            despues();
        }
        if (inmediato || Movimiento.Reducido) { Aplicar(1); Terminar(); return; }
        _animador.Iniciar(0, 1, DuracionSalidaMs, Aplicar, fin: Terminar, clave: "modo");
        PedirFotogramaArrastre();
    }

    /// <summary>
    /// La caja de apoyo que no cabe entera: la tarjeta sigue a todo el ancho (no asoma nada por detrás) y solo la cuadrícula se estrecha,
    /// centrada, para que sus casillas (cuadradas) sean más pequeñas.
    /// </summary>
    private void EstrecharRejillaApoyo(double escala)
    {
        MarcoApoyoCaja.MaxWidth = Math.Max(0, Bounds.Width - 32 - 24) * escala + 24;   // la cuadrícula se estrecha; el marco de 12 dp se queda
        MarcoApoyoCaja.HorizontalAlignment = HorizontalAlignment.Center;
    }

    /// <summary>Escala de la cuadrícula de la caja de apoyo para que toda la tarjeta quepa en <paramref name="sitio"/> (casillas de al menos <see cref="CeldaMinima"/> dp).</summary>
    private double EscalaApoyo(double sitio, double altoTarjeta)
    {
        var rejilla = MarcoApoyoCaja.DesiredSize.Height - 24;   // sin el marco de la tarjeta
        if (rejilla <= 0) return 1;
        return Math.Clamp((sitio - (altoTarjeta - rejilla)) / rejilla, EscalaParaCaber(0, 1, double.MaxValue), 1);
    }

    /// <summary>Alto de la caja de apoyo con la cuadrícula estrechada: los textos de arriba no cambian, la cuadrícula baja en proporción.</summary>
    private double AltoApoyoCaja(double alto, double escala) => escala >= 1 ? alto : alto - Math.Max(0, MarcoApoyoCaja.DesiredSize.Height - 24) * (1 - escala);

    /// <summary>Tapa con el color del fondo lo que hay en la página entre <paramref name="desde"/> y <paramref name="hasta"/> (detrás de las zonas).</summary>
    private void PonerVelo(double desde, double hasta)
    {
        VeloArrastre.Margin = new Thickness(0, Math.Max(0, desde), 0, 0);
        VeloArrastre.Height = Math.Max(0, hasta - desde);
        VeloArrastre.Opacity = 0;
        VeloArrastre.IsVisible = true;
    }

    private void PrepararZona(Border zona)
    {
        zona.VerticalAlignment = VerticalAlignment.Top;
        zona.RenderTransform = zona == _cajaModo ? null : _entradaZona;
        zona.Opacity = 0;
    }

    /// <summary>Escala de la caja para que caja + hueco + <paramref name="otro"/> quepan en <paramref name="alto"/>; nunca amplía ni deja casillas por debajo de <see cref="CeldaMinima"/>.</summary>
    private double EscalaParaCaber(double alto, double altoCaja, double otro)
    {
        if (altoCaja <= 0) return 1;
        var cabe = (alto - 2 * MargenModo - Hueco - otro) / altoCaja;
        var minima = _celda > 0 ? Math.Min(1, CeldaMinima / _celda) : 1;
        return Math.Clamp(cabe, minima, 1);
    }

    private double Medir(Control c)
    {
        // Las zonas llevan sus propios márgenes laterales (16 dp): se miden con el ancho de la vista (restarlos aquí las daba más
        // estrechas y, con casillas cuadradas, más bajas de lo que se pintan; medido el 20-oct: 293 frente a ~320 dp).
        c.Measure(new Size(Bounds.Width, double.PositiveInfinity));
        return c.DesiredSize.Height;
    }

    /// <summary>
    /// Alto de la página que de verdad se ve: hasta el borde de arriba de la barra de navegación (o de la contextual) si la tapa por
    /// abajo. Medido el 20-oct: la vista mide 664 dp y la barra ocupa los últimos ~65 (la caja quedaba debajo de la barra).
    /// </summary>
    private double AltoVisible()
    {
        var alto = Desplazador.Bounds.Height;
        if (TopLevel.GetTopLevel(this) is not { } top) return alto;
        foreach (var barra in top.GetVisualDescendants().OfType<Border>().Where(b => b.Name is "Barra" or "BarraContextual" && b.IsEffectivelyVisible))
            if (barra.TranslatePoint(default, this) is { } p && p.Y > 0 && p.Y < alto) alto = p.Y;
        return alto;
    }

    private double Limitar(double offset) => Math.Clamp(offset, 0, Math.Max(0, Desplazador.Extent.Height - Desplazador.Viewport.Height));

    /// <summary>Mueve la página durante el arrastre sin que el bloqueo del desplazamiento la devuelva a donde estaba.</summary>
    private void FijarOffset(double y)
    {
        var o = new Vector(Desplazador.Offset.X, y);
        _offsetFijo = o;
        Desplazador.SetCurrentValue(ScrollViewer.OffsetProperty, o);
    }
}
