// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Pokekeep.Mobile.Ui.Views;

public partial class ShellView : UserControl
{
    private IInsetsManager? _insets;

    public ShellView()
    {
        InitializeComponent();
        ActualThemeVariantChanged += (_, _) => AjustarIconosDeBarras();
        AplicarMovimiento();
        Teclado.PropertyChanged += (_, e) => { if (e.Property == Design.KeyboardAwareLayout.TecladoVisibleProperty) Aplicar(); };
        Design.Movimiento.Cambiado += AplicarMovimiento;
        Raiz.SizeChanged += (_, _) => AjustarAlEspacio();
        // El lema va en una sola línea y solo si cabe: con «Guardar» a la vista (tres botones) en un móvil estrecho se partía en tres.
        ((Control)Lema.Parent!).SizeChanged += (_, _) => AjustarLema();
        Lema.PropertyChanged += (_, e) => { if (e.Property == TextBlock.TextProperty || e.Property == TextBlock.FontSizeProperty) AjustarLema(); };
        DataContextChanged += (_, _) => AjustarAlEspacio();
        LayoutUpdated += (_, _) => {
            if (_ultimaPosicion != PosicionActual()) { _ultimaPosicion = PosicionActual(); Aplicar(); }
            MedirResumenAviso();
        };
    }

    /// <summary>Enlace de los créditos de Más: abre la página de PKHeX en el navegador del sistema (si no hay, no pasa nada).</summary>
    private async void AbrirPkhex(object? emisor, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (emisor is not Control { Tag: string url } || TopLevel.GetTopLevel(this)?.Launcher is not { } lanzador) return;
        try { await lanzador.LaunchUriAsync(new Uri(url)); }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or UriFormatException) { }
    }

    private (string Texto, double Ancho, Typeface Fuente, double Tamano, FlowDirection Direccion)? _medidaAviso;

    private void MedirResumenAviso()
    {
        if (DataContext is not ViewModels.ShellViewModel shell || !shell.HayAviso || FilaAviso.Bounds.Width <= 0) return;
        // Se mide SIN descontar «Ver más»: reservarlo antes de saber si hace falta
        // recortaría avisos que caben y crearía una dependencia circular del ancho.
        // Columna 1 = resumen, 2 = acción en línea (solo con poco alto y ancho), 3 = «Ver más»: el texto no cuenta con la acción pero sí con el hueco de «Ver más».
        var ancho = FilaAviso.ColumnDefinitions[1].ActualWidth + FilaAviso.ColumnDefinitions[3].ActualWidth;
        var fuente = new Typeface(ResumenAviso.FontFamily, ResumenAviso.FontStyle, ResumenAviso.FontWeight, ResumenAviso.FontStretch);
        var medida = (Texto: shell.Aviso!, Ancho: ancho, Fuente: fuente, Tamano: ResumenAviso.FontSize, Direccion: ResumenAviso.FlowDirection);
        if (_medidaAviso != medida)
        {
            _medidaAviso = medida;
            var texto = new FormattedText(medida.Texto, System.Globalization.CultureInfo.CurrentCulture,
                medida.Direccion, fuente, medida.Tamano, ResumenAviso.Foreground);
            _anchoTextoAviso = texto.WidthIncludingTrailingWhitespace;
        }
        shell.AvisoRecortado = _anchoTextoAviso > ancho || shell.Aviso!.IndexOfAny(['\r', '\n']) >= 0;
    }
    private double _anchoTextoAviso;

    /// <summary>
    /// Con poco alto (movil apaisado, teclado abierto, texto grande) lo secundario cede el sitio a lo que se esta haciendo: la tarjeta de partida pasa a una linea por debajo de 520 dp ajustados a la escala del texto y la hoja inferior no pasa del 92 %
    /// de la ventana ni lleva titulo por debajo de 300 dp (queda el buscador y la lista). Sin esto la barra de destinos quedaba empujada fuera de la pantalla.
    /// </summary>
    private bool _cabeceraCompleta = true;

    /// <summary>Ancho (dp) desde el que, con poco alto, la acción del aviso comparte fila con su resumen.</summary>
    private const double AnchoMinimoAccionEnLinea = 560;

    /// <summary>Alto (dp) por debajo del cual, con el ancho anterior, la acción del aviso comparte fila (móvil girado con el teclado abierto: unos 200 dp). Por encima, la fila propia aún cabe y el resumen conserva todo el ancho.</summary>
    private const double AltoMaximoAccionEnLinea = 300;

    private void AjustarLema()
    {
        var ancho = ((Control)Lema.Parent!).Bounds.Width;
        var natural = new Avalonia.Media.FormattedText(Lema.Text ?? string.Empty, System.Globalization.CultureInfo.CurrentCulture, Lema.FlowDirection,
            new Avalonia.Media.Typeface(Lema.FontFamily, Lema.FontStyle, Lema.FontWeight, Lema.FontStretch), Lema.FontSize, null).WidthIncludingTrailingWhitespace;
        Lema.IsVisible = _cabeceraCompleta && (ancho <= 0 || natural <= ancho + 0.5);
    }

    private void AjustarAlEspacio()
    {
        var alto = Raiz.Bounds.Height;
        if (alto <= 0) return;
        var escalaTexto = Application.Current?.Resources["TamCuerpo"] is double cuerpo ? Math.Max(1, cuerpo / 15) : 1;
        var completa = alto >= 520 * escalaTexto;
        TarjetaCompleta.IsVisible = completa;
        TarjetaCompacta.IsVisible = !completa;   // nunca se esconde la partida abierta ni se recorta su nombre
        _cabeceraCompleta = completa;
        AjustarLema();
        Raiz.Classes.Set("poco-alto", !completa);
        LogoCabecera.Width = LogoCabecera.Height = completa ? 48 : 28;
        if (DataContext is ViewModels.ShellViewModel shell) shell.AvisoCompacto = !completa;
        // Con sitio, el texto del aviso y su acción ocupan la columna del texto junto al icono, como antes de X22; compacto, bajan a su propia fila al desplegar.
        Grid.SetRow(TextoAvisoScroll, completa ? 0 : 1); Grid.SetColumn(TextoAvisoScroll, completa ? 1 : 0); Grid.SetColumnSpan(TextoAvisoScroll, completa ? 3 : 5);
        // Revisión de B+: con poco alto y ancho de sobra (móvil girado con el teclado abierto) la acción del aviso se queda en la misma fila que el resumen en vez de una fila propia de 48 dp, que empujaba la barra de acciones
        // 9 dp fuera de la ventana. En vertical y estrecho sigue bajando a su fila (el resumen no tendría sitio).
        var accionEnLinea = !completa && alto < AltoMaximoAccionEnLinea && Raiz.Bounds.Width >= AnchoMinimoAccionEnLinea;
        if (accionEnLinea) { Grid.SetRow(AccionAviso, 0); Grid.SetColumn(AccionAviso, 2); Grid.SetColumnSpan(AccionAviso, 1); }
        else { Grid.SetRow(AccionAviso, completa ? 1 : 2); Grid.SetColumn(AccionAviso, completa ? 1 : 0); Grid.SetColumnSpan(AccionAviso, completa ? 3 : 5); }
        MarcaCabecera.FontSize = Application.Current?.Resources[completa ? "TamTitulo" : "TamDato"] is double marca ? marca : 20;
        PanelHoja.MaxHeight = Math.Max(120, alto * 0.92);
        FilaTituloHoja.IsVisible = alto >= 300;
        // Texto completo desplazable sin expulsar página/acciones en horizontal o con letra grande.
        TextoAvisoScroll.MaxHeight = Math.Max(24, Math.Min(120, alto * 0.28 - 80));
    }

    /// <summary>Las animaciones solo existen bajo <c>.movimiento</c>: con «reducir movimiento» del sistema la raiz lleva <c>.sin-movimiento</c> y no hay ninguna.</summary>
    private void AplicarMovimiento()
    {
        Raiz.Classes.Remove("movimiento"); Raiz.Classes.Remove("sin-movimiento");
        Raiz.Classes.Add(Design.Movimiento.ClaseRaiz);
        AjustarAlEspacio();
    }

    /// <summary>Da el foco al primer campo de texto visible con ese nombre accesible (pruebas de dispositivo con el teclado).</summary>
    public bool EnfocarCampo(string nombreAccesible)
    {
        var campo = this.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(t => t.IsEffectivelyVisible && Avalonia.Automation.AutomationProperties.GetName(t) == nombreAccesible);
        campo?.BringIntoView();   // un campo bajo el pliegue del desplazador tiene que verse para que el toque de la prueba (y el dedo) lo alcance
        return campo?.Focus() == true;
    }

    /// <summary>Margenes del sistema (barra de estado, gestos) en dp; en el host de pruebas se fijan a mano.</summary>
    public Thickness MargenSistema
    {
        get => _margen;
        set { _margen = value; Aplicar(); }
    }
    private Thickness _margen;
    private (double, double, double) _ultimaPosicion;
    private (double, double, double) PosicionActual()
        => TopLevel.GetTopLevel(this) is { } top && this.TranslatePoint(new Point(0, 0), top) is { } p ? (Math.Round(p.Y), Math.Round(Bounds.Height), Math.Round(p.X)) : default;

    /// <summary>
    /// Aplica los margenes del sistema SOLO en lo que el anfitrion no haya dejado ya fuera. En Android 15+ (API 36) Avalonia ya desplaza esta vista bajo la barra de estado y sobre la
    /// de gestos Y ademas informa de esos margenes en <c>SafeAreaPadding</c>: aplicarlos otra vez dejaba una franja vacia de 104 dp arriba (172 dp sin teclado) y, con el teclado abierto,
    /// el sprite y el nombre del editor quedaban recortados bajo el borde del desplazador (hallazgo de F2 anotado en el traspaso, medido en pk_pixel_9_pro). Se mide donde esta de verdad
    /// la vista dentro de la ventana y solo se compensa lo que falte (edge-to-edge real: la vista pegada al borde).
    /// </summary>
    private void Aplicar()
    {
        double yaArriba = 0, yaAbajo = 0, yaIzq = 0, yaDer = 0;
        if (TopLevel.GetTopLevel(this) is { } top && Bounds.Width > 0 && Bounds.Height > 0)
        {
            var o = this.TranslatePoint(new Point(0, 0), top);
            if (o is { } p)
            {
                yaArriba = Math.Max(0, p.Y); yaIzq = Math.Max(0, p.X);
                yaAbajo = Math.Max(0, top.ClientSize.Height - (p.Y + Bounds.Height));
                yaDer = Math.Max(0, top.ClientSize.Width - (p.X + Bounds.Width));
            }
        }
        // Si el anfitrion ya dejo algo fuera por un lado, ese lado se da por resuelto (no se suma otro margen en unidades dudosas).
        double Lado(double margen, double ya) => ya >= 1 ? 0 : margen;
        var m = new Thickness(Lado(_margen.Left, yaIzq), Lado(_margen.Top, yaArriba), Lado(_margen.Right, yaDer), Lado(_margen.Bottom, yaAbajo));
        Raiz.Padding = new Thickness(m.Left, m.Top, m.Right, 0);
        Barra.Padding = new Thickness(8, 0, 8, m.Bottom);
        // La hoja inferior llega hasta el borde de la ventana (con la barra de navegacion del sistema por debajo, de gestos o de 3 botones): su contenido se levanta ese margen.
        // Con el teclado abierto el teclado cubre esa zona y la ventana (o el relleno) ya termina sobre el: no se suma otro hueco.
        PanelHoja.Padding = new Thickness(0, 0, 0, Teclado.TecladoVisible ? 0 : m.Bottom);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (TopLevel.GetTopLevel(this) is { } tl) tl.BackRequested += AlPedirAtras;
        ConectarPortapapeles();
        if (TopLevel.GetTopLevel(this)?.InsetsManager is { } im)
        {
            // Un mismo origen para dibujo y accesibilidad también antes de Android 15.
            // Aplicar() conserva las áreas seguras de estado, gestos y cámara.
            if (OperatingSystem.IsAndroid()) im.DisplayEdgeToEdgePreference = true;
            _insets = im;
            AjustarIconosDeBarras();
            im.SafeAreaChanged += AlCambiarMargenes;
            MargenSistema = im.SafeAreaPadding;
        }
    }

    /// <summary>El portapapeles del sistema para exportar sets de Showdown y copiar informes (la Shell no conoce Avalonia: se lo da la vista al unirse al arbol).</summary>
    private void ConectarPortapapeles()
    {
        if (DataContext is ViewModels.ShellViewModel vm && vm.Portapapeles is null && TopLevel.GetTopLevel(this)?.Clipboard is { } cb)
            vm.Portapapeles = new Design.PortapapelesAvalonia(cb);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        ConectarPortapapeles();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Design.Movimiento.Cambiado -= AplicarMovimiento;
        if (TopLevel.GetTopLevel(this) is { } tl) tl.BackRequested -= AlPedirAtras;
        if (_insets is not null) _insets.SafeAreaChanged -= AlCambiarMargenes;
        base.OnDetachedFromVisualTree(e);
    }

    private void AjustarIconosDeBarras()
    {
        if (_insets is null) return;
        try { _insets.SystemBarColor = ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark ? Avalonia.Media.Color.Parse("#151714") : Avalonia.Media.Color.Parse("#F5F3ED"); } catch { }
    }


    /// <summary>
    /// Atrás del sistema (botón, gesto lateral o gesto predictivo): lo primero que se cierra es un desplegable abierto (su lista flota fuera del modelo); después decide la Shell
    /// (<see cref="ViewModels.ShellViewModel.Atras"/>). Solo si no hay nada que cerrar la app sale.
    /// </summary>
    private void AlPedirAtras(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (CerrarDesplegableAbierto()) { e.Handled = true; return; }
        if (DataContext is ViewModels.ShellViewModel vm && vm.Atras()) e.Handled = true;
    }

    /// <summary>Cierra el desplegable (ComboBox o ventana emergente) que esté abierto en esta vista; true si había uno.</summary>
    internal bool CerrarDesplegableAbierto()
    {
        foreach (var d in this.GetVisualDescendants())
        {
            if (d is ComboBox { IsDropDownOpen: true } combo) { combo.IsDropDownOpen = false; return true; }
            if (d is Avalonia.Controls.Primitives.Popup { IsOpen: true } popup && popup.FindAncestorOfType<ComboBox>() is null) { popup.IsOpen = false; return true; }
        }
        return false;
    }

    /// <summary>Lo que hace el sistema al pulsar Atrás (pruebas): igual que el evento, sin necesidad de plataforma.</summary>
    internal bool SimularAtras() { var e = new Avalonia.Interactivity.RoutedEventArgs(); AlPedirAtras(this, e); return e.Handled; }

    private void AlCambiarMargenes(object? sender, SafeAreaChangedArgs e) => MargenSistema = e.SafeAreaPadding;
}
