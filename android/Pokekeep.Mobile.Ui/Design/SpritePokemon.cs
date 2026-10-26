// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Rendering;
using PKHeX.Core;
using Pokekeep.Sprites;

namespace Pokekeep.Mobile.Ui.Design;

/// <summary>
/// Sprite de una entidad. Con <see cref="Imagen"/> (la casilla ya compuesta por <c>SpriteComposer.GetSlotSprite</c>) dibuja esa imagen tal cual; sin ella compone a partir de
/// especie, forma y su argumento, sexo, brillo, huevo y objeto con el compositor. Caja, ficha y editor siguen el modo de la partida abierta, como PKHeX.
/// Dibuja la parte visible con la escala comun del contexto (X48, opcion C: lado util / percentil 90 del lado visible del juego de sprites; el que no cabe se reduce lo justo), centrada y sin deformarla de píxeles físicos que quepa; solo reduce cuando no cabe ni a escala 1.
/// El suavizado es opcional únicamente para imágenes de casilla ya preparadas. Recurso ausente = silueta con el número de especie.
/// </summary>
public sealed class SpritePokemon : Control
{
    public static readonly StyledProperty<int> EspecieProperty = AvaloniaProperty.Register<SpritePokemon, int>(nameof(Especie));
    public static readonly StyledProperty<int> FormaProperty = AvaloniaProperty.Register<SpritePokemon, int>(nameof(Forma));
    public static readonly StyledProperty<uint> ArgumentoFormaProperty = AvaloniaProperty.Register<SpritePokemon, uint>(nameof(ArgumentoForma));
    public static readonly StyledProperty<int> GeneroProperty = AvaloniaProperty.Register<SpritePokemon, int>(nameof(Genero));
    public static readonly StyledProperty<bool> BrillanteProperty = AvaloniaProperty.Register<SpritePokemon, bool>(nameof(Brillante));
    public static readonly StyledProperty<bool> HuevoProperty = AvaloniaProperty.Register<SpritePokemon, bool>(nameof(Huevo));
    public static readonly StyledProperty<int> ObjetoProperty = AvaloniaProperty.Register<SpritePokemon, int>(nameof(Objeto));
    public static readonly StyledProperty<int> ContextoProperty = AvaloniaProperty.Register<SpritePokemon, int>(nameof(Contexto));
    public static readonly StyledProperty<int> VersionJuegoProperty = AvaloniaProperty.Register<SpritePokemon, int>(nameof(VersionJuego));
    public static readonly StyledProperty<ModoSprites> ModoProperty = AvaloniaProperty.Register<SpritePokemon, ModoSprites>(nameof(Modo));
    public static readonly StyledProperty<bool> GrandeProperty = AvaloniaProperty.Register<SpritePokemon, bool>(nameof(Grande));
    public static readonly StyledProperty<Bitmap?> ImagenProperty = AvaloniaProperty.Register<SpritePokemon, Bitmap?>(nameof(Imagen));
    public static readonly StyledProperty<string?> JuegoIconoProperty = AvaloniaProperty.Register<SpritePokemon, string?>(nameof(JuegoIcono));
    public static readonly StyledProperty<Bitmap?> FondoProperty = AvaloniaProperty.Register<SpritePokemon, Bitmap?>(nameof(Fondo));
    public static readonly StyledProperty<Rect?> RecuadroProperty = AvaloniaProperty.Register<SpritePokemon, Rect?>(nameof(Recuadro));
    public static readonly StyledProperty<bool> SuaveProperty = AvaloniaProperty.Register<SpritePokemon, bool>(nameof(Suave));
    public static readonly StyledProperty<IReadOnlyList<Sprites.MarcaVista>?> MarcasProperty = AvaloniaProperty.Register<SpritePokemon, IReadOnlyList<Sprites.MarcaVista>?>(nameof(Marcas));
    public static readonly StyledProperty<bool> UnSoloPokemonProperty = AvaloniaProperty.Register<SpritePokemon, bool>(nameof(UnSoloPokemon));
    public static readonly StyledProperty<bool> SiluetaProperty = AvaloniaProperty.Register<SpritePokemon, bool>(nameof(Silueta));
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<SpritePokemon>();

    static SpritePokemon() => AffectsRender<SpritePokemon>(EspecieProperty, FormaProperty, ArgumentoFormaProperty, GeneroProperty, BrillanteProperty, HuevoProperty, ObjetoProperty, ContextoProperty, VersionJuegoProperty, GrandeProperty, ModoProperty, ImagenProperty, JuegoIconoProperty, FondoProperty, RecuadroProperty, MarcasProperty, SuaveProperty, SiluetaProperty, UnSoloPokemonProperty, ForegroundProperty);

    public SpritePokemon()
    {
        IsHitTestVisible = false;
        Focusable = false;
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
    }

    public int Especie { get => GetValue(EspecieProperty); set => SetValue(EspecieProperty, value); }
    public int Forma { get => GetValue(FormaProperty); set => SetValue(FormaProperty, value); }
    /// <summary>Parámetro especial de forma del Pokémon; también forma parte de la clave del compositor.</summary>
    public uint ArgumentoForma { get => GetValue(ArgumentoFormaProperty); set => SetValue(ArgumentoFormaProperty, value); }
    public int Genero { get => GetValue(GeneroProperty); set => SetValue(GeneroProperty, value); }
    public bool Brillante { get => GetValue(BrillanteProperty); set => SetValue(BrillanteProperty, value); }
    public bool Huevo { get => GetValue(HuevoProperty); set => SetValue(HuevoProperty, value); }
    public int Objeto { get => GetValue(ObjetoProperty); set => SetValue(ObjetoProperty, value); }
    /// <summary><c>EntityContext</c> de la entidad (0 = ninguno): lo usan Pikachu de Gen. VI, Deoxys de Gen. III, Arceus de Gen. IV y Xerneas de Z-A.</summary>
    public int Contexto { get => GetValue(ContextoProperty); set => SetValue(ContextoProperty, value); }
    public int VersionJuego { get => GetValue(VersionJuegoProperty); set => SetValue(VersionJuegoProperty, value); }
    /// <summary>Juego de sprites según la partida; ampliar la vista previa no cambia su estilo.</summary>
    public ModoSprites Modo { get => GetValue(ModoProperty); set => SetValue(ModoProperty, value); }
    public bool Grande { get => GetValue(GrandeProperty); set => SetValue(GrandeProperty, value); }
    /// <summary>Imagen ya compuesta (casilla de caja). Tiene prioridad sobre los datos sueltos.</summary>
    public Bitmap? Imagen { get => GetValue(ImagenProperty); set => SetValue(ImagenProperty, value); }
    /// <summary>
    /// X53: <see cref="Imagen"/> es el icono de caja del PROPIO juego de la partida (id de <see cref="SpritesDeJuego.JuegoDeCaja"/>) y no un sprite de PKHeX: su pixel es el del juego (no el lienzo de 68 x 56), la escala comun sale del conjunto de ESE
    /// juego (<see cref="Sprites.ReferenciaIcono"/>) y las ilustraciones suaves (Diamante Brillante y Perla Reluciente) se reescalan con filtro de calidad en vez de factor entero. Null = sprite de PKHeX.
    /// </summary>
    public string? JuegoIcono { get => GetValue(JuegoIconoProperty); set => SetValue(JuegoIconoProperty, value); }
    public Bitmap? Fondo { get => GetValue(FondoProperty); set => SetValue(FondoProperty, value); }
    /// <summary>Parte visible de <see cref="Imagen"/> (de <c>Sprites.Recuadro</c>): rige la escala comun X48 (ver <see cref="DibujarComun"/>). Null = la imagen entera, ajustada al control. Null = la imagen entera.</summary>
    public Rect? Recuadro { get => GetValue(RecuadroProperty); set => SetValue(RecuadroProperty, value); }
    /// <summary>La <see cref="Imagen"/> ya es la ampliada por el escalador de pixel art (ajuste «Suavizar sprites de la caja»): se dibuja con reescalado suave y factor justo, no con factor entero.</summary>
    public bool Suave { get => GetValue(SuaveProperty); set => SetValue(SuaveProperty, value); }
    /// <summary>Marcas de la casilla (objeto, Gigamax, franja de Teratipo...), ancladas a la celda y dibujadas sobre el Pokemon sin ensanchar su recuadro.</summary>
    public IReadOnlyList<Sprites.MarcaVista>? Marcas { get => GetValue(MarcasProperty); set => SetValue(MarcasProperty, value); }
    /// <summary>
    /// X48: el control enseña UN solo Pokemon (cabecera del editor, fila «Especie», ficha, estadisticas base...): su parte visible llena el hueco con su propia escala (factor entero, o el ajuste justo con el suavizado).
    /// Falso (por defecto) en los contextos que enseñan VARIOS a la vez (caja, equipo, Libreria en lista, selectores, Cobertura, tablas): rige la escala comun de <see cref="DibujarComun"/>.
    /// </summary>
    public bool UnSoloPokemon { get => GetValue(UnSoloPokemonProperty); set => SetValue(UnSoloPokemonProperty, value); }
    /// <summary>Ultimo rectangulo (en dp del control) donde se dibujo el sprite: lo mide la prueba de ocupacion.</summary>
    public Rect UltimoDestino { get; private set; }
    /// <summary>X53: juego del icono de caja que se dibujo la ultima vez (con <see cref="JuegoIcono"/> o por el camino de lista), o null si fue un sprite de PKHeX. Lo miden las pruebas de escala.</summary>
    public string? JuegoIconoDibujado { get; private set; }
    /// <summary>Ultimo recuadro de la imagen (en sus pixeles) que se dibujo en <see cref="UltimoDestino"/>: con la regla comun X48 es la parte visible del sprite (o la imagen entera si no hay recuadro); las pruebas de escala lo miden.</summary>
    public Rect UltimoOrigen { get; private set; }
    /// <summary>Pixeles del bitmap por pixel del sprite en el ultimo dibujo (1 normal, 4 con el suavizado de la caja).</summary>
    public double UltimoPorPixel { get; private set; } = 1;
    /// <summary>La imagen es la silueta de reserva (falta el recurso): se dibuja el numero de especie encima.</summary>
    public bool Silueta { get => GetValue(SiluetaProperty); set => SetValue(SiluetaProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <summary>Hasta cuanto puede sobrar del recuadro (sin usar) para que se prefiera el factor ENTERO exacto (pixeles perfectamente iguales) al ajuste fraccionario nitido.</summary>
    private const double SobraMaximaEntero = 1.12;

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Bitmap, Dictionary<int, Bitmap>> Prescalados = new();

    /// <summary>
    /// Copia de <paramref name="b"/> ampliada <paramref name="m"/> veces con vecino mas proximo (cada pixel pasa a ser un bloque m x m), para el ajuste fraccionario nitido: se reduce desde ella con
    /// filtro de calidad y los bordes quedan limpios (pixeles de 1 o 2 pixeles fisicos suavizados, sin el reparto desigual del vecino mas proximo directo).
    /// </summary>
    private static Bitmap Prescalar(Bitmap b, int m)
    {
        lock (Prescalados)
        {
            var d = Prescalados.GetOrCreateValue(b);
            if (d.TryGetValue(m, out var r)) return r;
            var rtb = new RenderTargetBitmap(new PixelSize(b.PixelSize.Width * m, b.PixelSize.Height * m), new Vector(96, 96));
            using (var dc = rtb.CreateDrawingContext())
            using (dc.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None }))
                dc.DrawImage(b, new Rect(b.PixelSize.ToSize(1)), new Rect(0, 0, rtb.PixelSize.Width, rtb.PixelSize.Height));
            return d[m] = rtb;
        }
    }

    /// <summary>
    /// Dibuja <paramref name="origen"/> (el lienzo de referencia, X48: el mismo para todos los Pokemon, asi todos salen a la misma escala) lo mas grande posible dentro de <paramref name="destino"/>, centrado y sin deformar.
    /// Pixel art: factor ENTERO de pixeles de pantalla (bordes nitidos) mientras no sobre mas de un 12 %; si sobra mas, la escala comun es la que llena el destino y se dibuja con el ajuste fraccionario nitido
    /// (ampliacion previa por vecino mas proximo y reduccion de calidad). Si no cabe ni a escala 1, se reduce. Las imagenes preparadas con el ajuste opcional «Suavizar» usan siempre reescalado suave.
    /// </summary>
    private Rect Dibujar(DrawingContext c, Bitmap b, Rect origen, Rect destino, bool suave, bool unSolo = false)
    {
        var s = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
        var justo = Math.Min(destino.Width * s / origen.Width, destino.Height * s / origen.Height);
        var factor = justo;
        // Un solo Pokemon en pantalla (cabecera del editor, ficha...): llena su hueco con su propia escala, siempre con factor ENTERO (como antes de X48); con la ampliacion suave, el ajuste justo.
        var entero = !suave && justo >= 1 && (unSolo || justo / Math.Floor(justo) <= SobraMaximaEntero);
        if (entero) factor = Math.Floor(justo);
        var wpx = Math.Round(origen.Width * factor); var hpx = Math.Round(origen.Height * factor);
        var xpx = Math.Round((destino.X * s) + (destino.Width * s - wpx) / 2); var ypx = Math.Round((destino.Y * s) + (destino.Height * s - hpx) / 2);
        var r = new Rect(xpx / s, ypx / s, wpx / s, hpx / s);
        if (entero || suave || justo < 1 || unSolo)
        {
            using (c.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = suave ? BitmapInterpolationMode.HighQuality : BitmapInterpolationMode.None }))
                c.DrawImage(b, origen, r);
            return r;
        }
        // Fraccionario nitido: ampliacion previa entera (>= factor) y reduccion de calidad hasta la escala comun.
        var m = (int)Math.Ceiling(factor);
        var grande = Prescalar(b, m);
        using (c.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.HighQuality }))
            c.DrawImage(grande, new Rect(origen.X * m, origen.Y * m, origen.Width * m, origen.Height * m), r);
        return r;
    }

    /// <summary>Fraccion de la casilla (control mas su margen) que ocupa el lado util donde cabe la parte visible de un Pokemon.</summary>
    public const double FraccionUtil = 0.86;

    /// <summary>Lado util (dp) del control: <see cref="FraccionUtil"/> de la casilla (control + margen), sin pasar del propio control.</summary>
    public double LadoUtil
    {
        get
        {
            var m = Margin;
            var casilla = Math.Min(Bounds.Width + m.Left + m.Right, Bounds.Height + m.Top + m.Bottom);
            return Math.Min(FraccionUtil * casilla, Math.Min(Bounds.Width, Bounds.Height));
        }
    }

    /// <summary>
    /// Regla comun de escala (X48, opcion C): UNA escala k = lado util / referencia (percentil 90 del lado mayor visible del juego de sprites, <see cref="Sprites.ReferenciaVisible"/>) para todos los Pokemon del contexto, con
    /// la parte visible centrada en el control. Factor entero de pixeles fisicos si no se pierde mas de un 12 %; si no, escala fraccionaria nitida (ampliacion previa por vecino mas proximo y reduccion de calidad). Solo el
    /// que no cabe con esa k (Gigamax, Eternamax, Wailord...) se reduce lo justo para caber en el lado util. <paramref name="porPixel"/> = pixeles del bitmap por pixel del sprite (1 normal, 4 suavizado).
    /// </summary>
    private Rect DibujarComun(DrawingContext c, Bitmap b, Rect visible, double porPixel, Rect area, bool suave, double? referencia = null, bool liso = false, bool estricto = false)
    {
        var s = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
        double bw = visible.Width / porPixel, bh = visible.Height / porPixel;           // parte visible en pixeles del sprite
        double lado = LadoUtil * s;                                                      // pixeles fisicos
        var k = lado / (referencia ?? Sprites.ReferenciaVisible(Modo));                  // pixeles fisicos por pixel del sprite (X53: con su propia referencia, la del juego, en los iconos de caja)
        // liso = ilustracion suave: escala exacta, sin factor entero. estricto = pixel art de un icono de caja del juego (X53): SIEMPRE factor entero (el mayor que quepa) con vecino mas proximo mientras haya al menos un pixel fisico por pixel.
        var entero = !liso && k >= 1 && (estricto || k / Math.Floor(k) <= SobraMaximaEntero);
        if (entero) k = Math.Floor(k);
        var cabe = lado / Math.Max(bw, bh);
        var ajustado = cabe < k;                                                          // no cabe con la escala comun: se reduce lo justo
        if (ajustado) { if (estricto && cabe >= 1) k = Math.Floor(cabe); else { k = cabe; entero = false; } }
        // Un icono nativo no se reduce solo para reservar el margen del 14 %:
        // si cabe a 1:1 en el control completo, conserva ese píxel físico.
        if (estricto && !suave && k < 1 && bw <= area.Width * s && bh <= area.Height * s) { k = 1; entero = true; }
        double wpx = Math.Max(1, Math.Round(bw * k)), hpx = Math.Max(1, Math.Round(bh * k));
        var xpx = Math.Round(area.X * s + (area.Width * s - wpx) / 2); var ypx = Math.Round(area.Y * s + (area.Height * s - hpx) / 2);
        var r = new Rect(xpx / s, ypx / s, wpx / s, hpx / s);
        if (entero || suave || liso || k < 1)
        {
            using (c.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = suave || liso || (!estricto && k < 1) ? BitmapInterpolationMode.HighQuality : BitmapInterpolationMode.None }))
                c.DrawImage(b, visible, r);
            return r;
        }
        var m = (int)Math.Ceiling(k);
        var grande = Prescalar(b, m);
        using (c.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.HighQuality }))
            c.DrawImage(grande, new Rect(visible.X * m, visible.Y * m, visible.Width * m, visible.Height * m), r);
        return r;
    }

    /// <summary>
    /// Parte de la casilla que una marca (objeto equipado, Gigamax...) puede ocupar a lo sumo por lado. Era 0,40: al pasar la casilla de 360 dp de 46 a 48 dp (ADR 0105), el objeto de Celebi (22 x 22 px) quedaba justo en el
    /// 40 % con 2 px fisicos por pixel (0,4 · 36,67 · 3 / 22 = 2,0004) y saltaba al doble de tamano, pisando al Pokemon. Con 0,38 el factor entero sale como en main en todas las casillas de 35 a 40 dp.
    /// </summary>
    internal const double FraccionMaximaMarca = 0.38;

    /// <summary>Pixeles fisicos por pixel de sprite de una marca: factor entero (pixel art limpio), el del Pokemon como mucho, sin pasar de <see cref="FraccionMaximaMarca"/> de la casilla y nunca por debajo de 1.</summary>
    internal static double PixelesFisicosPorPixelDeMarca(double dpPorPixel, Rect area, int ancho, int alto, double escala)
        => Math.Max(1, Math.Floor(Math.Min(dpPorPixel, FraccionMaximaMarca * Math.Min(area.Width, area.Height) / Math.Max(ancho, alto)) * escala));

    /// <summary>Dibuja las marcas ancladas a la celda con el mismo tamano por pixel que el Pokemon, sin pasar de <see cref="FraccionMaximaMarca"/> de la celda; la franja de Teratipo, en todo el ancho.</summary>
    private static void DibujarMarcas(DrawingContext c, IReadOnlyList<Sprites.MarcaVista> marcas, Rect area, double dpPorPixel, double escala)
    {
        using var _ = c.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None });
        foreach (var m in marcas)
        {
            var origen = new Rect(0, 0, m.Ancho, m.Alto);
            if (m.Esquina == EsquinaMarca.FranjaInferior)
            {
                var alto = Math.Max(2, m.Alto * area.Width / SpriteComposer.Ancho);
                c.DrawImage(m.Bitmap, origen, new Rect(area.X, area.Bottom - alto, area.Width, alto));
                continue;
            }
            // Factor entero de pixeles fisicos tambien en las marcas (pixel art limpio).
            var gpx = PixelesFisicosPorPixelDeMarca(dpPorPixel, area, m.Ancho, m.Alto, escala);
            var g = gpx / escala;
            double w = m.Ancho * g, h = m.Alto * g;
            var x = m.Esquina switch { EsquinaMarca.AbajoDerecha or EsquinaMarca.ArribaDerecha => area.Right - w, EsquinaMarca.ArribaCentro => area.X + (area.Width - w) / 2, _ => area.X };
            var y = m.Esquina == EsquinaMarca.AbajoDerecha ? area.Bottom - h : area.Y;
            x = Math.Round(x * escala) / escala; y = Math.Round(y * escala) / escala;
            c.DrawImage(m.Bitmap, origen, new Rect(x, y, w, h));
        }
    }

    /// <summary>
    /// Baldosa de fondo (X53d) para los iconos recortados por el lienzo en los sitios SIN casilla con marco: el mismo radio, tono y borde que la casilla ocupada de la caja en el tema vigente
    /// (<c>RadioCasilla</c>, <c>Fondo</c> y <c>BordeCasilla</c>), a todo el hueco del control.
    /// </summary>
    private void DibujarBaldosa(DrawingContext c, Rect area)
    {
        var tema = ActualThemeVariant;
        if (!this.TryFindResource("Fondo", tema, out var fondo) || fondo is not IBrush pincel) return;
        var radio = this.TryFindResource("RadioCasilla", tema, out var rc) && rc is CornerRadius cr ? cr.TopLeft : 10;
        IPen? borde = this.TryFindResource("BordeCasilla", tema, out var bc) && bc is IBrush bb ? new Pen(bb, 1.5) : null;
        c.DrawRectangle(pincel, borde, new RoundedRect(area.Deflate(borde is null ? 0 : 0.75), radio));
    }

    private static Bitmap? _estrellaPixeles;
    private static Bitmap SpritesEstrella() => _estrellaPixeles ??= Sprites.Crear(Sprites.EstrellaBrilloPixeles());

    private static readonly Geometry Estrella = Geometry.Parse("M 0,-1 L 0.24,-0.24 L 1,0 L 0.24,0.24 L 0,1 L -0.24,0.24 L -1,0 L -0.24,-0.24 Z");
    private static readonly IBrush RojoBrillo = new SolidColorBrush(Color.FromRgb(0xE5, 0x38, 0x3B));
    private static readonly IPen ContornoBrillo = new Pen(new SolidColorBrush(Color.FromArgb(0xB0, 0xFF, 0xFF, 0xFF)), 0.8);

    /// <summary>
    /// Estrella de brillo propia, en la esquina superior izquierda y de un tamano fijo respecto al control (~28 %): la que PKHeX cose en el sprite ensanchaba la caja visible y el Pokemon quedaba diminuto
    /// en su esquina (uso real 1). Con contorno claro para que se vea sobre cualquier fondo, claro u oscuro.
    /// </summary>
    private static void DibujarEstrella(DrawingContext c, Rect area)
    {
        var ajustes = Ajustes.Pkhex.ProgramaPkhex.Ajustes.Actual;
        var simbolo = ajustes.Display.Unicode ? ajustes.Draw.ShinyUnicode : ajustes.Draw.ShinyDefault;
        if (string.IsNullOrEmpty(simbolo)) return;
        var lado = Math.Max(10, Math.Min(area.Width, area.Height) * 0.28);
        if (simbolo != "☆")
        {
            var texto = new FormattedText(simbolo, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, lado, RojoBrillo);
            c.DrawText(texto, new Point(area.X + 2, area.Y + 2));
            return;
        }
        var radio = lado / 2;
        var centro = new Point(area.X + 2 + radio, area.Y + 2 + radio);
        using (c.PushTransform(Matrix.CreateScale(radio, radio) * Matrix.CreateTranslation(centro.X, centro.Y)))
            c.DrawGeometry(RojoBrillo, new Pen(ContornoBrillo.Brush, 0.8 / radio), Estrella);
    }

    public override void Render(DrawingContext context)
    {
        var area = new Rect(Bounds.Size);
        if (area.Width <= 0 || area.Height <= 0) return;
        if (Fondo is { } fondo) context.DrawImage(fondo, new Rect(fondo.Size), area);
        Bitmap? bmp = Imagen;
        var recuadro = Recuadro;
        var silueta = Silueta;
        if (bmp is null && Sprites.Configurado)
        {
            // X51: la vista previa grande de una partida de Gen. 1 a 5 enseña el sprite pixelado del PROPIO juego; si falta, el de PKHeX de siempre.
            if (Grande && Sprites.VistaDeJuego(Modo, (GameVersion)VersionJuego, Especie, Forma, Genero, Brillante, Huevo) is { } propio)
            {
                var visible = Sprites.Recuadro(propio);
                UltimoDestino = default; UltimoOrigen = visible; UltimoPorPixel = 1;
                // X58: Let's Go trae renders 3D, no pixel art: escala exacta con reescalado de calidad (sin factor entero ni vecino mas proximo) y la estrella de variocolor de la app.
                var render = SpritesDeJuego.JuegoDe((GameVersion)VersionJuego) is { } origenVista && !SpritesDeJuego.EsPixelArt(origenVista, SpritesDeJuego.ClaseVista);
                UltimoDestino = Dibujar(context, Sprites.Bitmap(propio), visible, area, render, unSolo: true);   // pixel art: factor entero (el mayor que quepa), vecino mas proximo, centrado por su parte visible
                if (render) { if (Brillante && !Huevo) DibujarEstrella(context, area); return; }
                if (Brillante && !Huevo)
                {
                    // Estrella de variocolor en pixeles, con el MISMO factor entero que el sprite y en la misma esquina de siempre (sin vectores ni suavizado).
                    var s = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
                    var k = Math.Max(1, Math.Round(UltimoDestino.Width * s / visible.Width));
                    var est = SpritesEstrella(); var lado = est.PixelSize.Width * k / s;
                    var x = Math.Round((area.X + 2) * s) / s; var y = Math.Round((area.Y + 2) * s) / s;
                    using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None }))
                        context.DrawImage(est, new Rect(0, 0, est.PixelSize.Width, est.PixelSize.Height), new Rect(x, y, lado, lado));
                }
                return;
            }
            // X71: Gen. 6 a 9, sin respaldo de PKHeX. Si la combinacion no esta en el paquete del juego, se dibuja el aviso de «recurso ausente» (silueta y numero) y se anota; el sprite de PKHeX no sale.
            if (Grande && Sprites.VistaSinRespaldoDePkhex(Modo, (GameVersion)VersionJuego))
            {
                Sprites.AvisarVistaAusente((GameVersion)VersionJuego, Especie, Forma, Genero, Brillante, Huevo);
                UltimoDestino = default; UltimoOrigen = default; JuegoIconoDibujado = null;
                DibujarAusente(context, area);
                return;
            }
            // X53: el icono de caja del propio juego (Gen. III a V, Espada/Escudo, Diamante Brillante/Perla Reluciente) en listas y filas; en la vista grande sigue el sprite de PKHeX.
            if (!Grande && Sprites.IconoDeCaja(Modo, (GameVersion)VersionJuego, Especie, Forma, Genero, Brillante, Huevo, (EntityContext)Contexto) is { } icono)
            {
                var visible = Sprites.Recuadro(icono.Imagen);
                var liso = !SpritesDeJuego.EsPixelArt(icono.Juego);
                if (Sprites.IconoRecortado(icono.Juego)) DibujarBaldosa(context, area);   // sin casilla con marco, el corte recto del lienzo queda contenido en una baldosa
                UltimoOrigen = visible; UltimoPorPixel = 1; JuegoIconoDibujado = icono.Juego;
                UltimoDestino = UnSoloPokemon ? Dibujar(context, Sprites.Bitmap(icono.Imagen), visible, area, liso, unSolo: true)
                    : DibujarComun(context, Sprites.Bitmap(icono.Imagen), visible, 1, area, false, Sprites.ReferenciaIcono(icono.Juego), liso, estricto: !liso);
                if (Brillante && !Huevo) DibujarEstrella(context, area);
                return;
            }
            var c = Sprites.Compartido(Modo, (GameVersion)VersionJuego);
            var contexto = (EntityContext)Contexto;
            // Misma selección de recurso que en las cajas: cambiar de tamaño no salta a otro juego de imágenes.
            // El compositor conserva sus alternativas verificadas cuando falta la imagen exacta (incluido variocolor).
            var img = c.GetSprite((ushort)Especie, (byte)Forma, (byte)Genero, ArgumentoForma, Grande ? 0 : Objeto, Huevo, Brillante ? Shiny.Always : Shiny.Never, contexto);
            recuadro = Sprites.Recuadro(img);
            bmp = Sprites.Bitmap(img);
            silueta = Especie > 0 && c.NombreRecursoBase((ushort)Especie, (byte)Forma, (byte)Genero, ArgumentoForma, Brillante, (EntityContext)Contexto) is null;
        }
        UltimoDestino = default; UltimoOrigen = default; JuegoIconoDibujado = null;
        if (bmp is not null)
        {
            // Con recuadro (la parte visible, de la casilla o del propio compositor) rige la escala comun; sin recuadro (imagen suelta) se ajusta la imagen entera.
            var origen = recuadro ?? new Rect(bmp.Size);
            UltimoOrigen = origen;
            var icono = JuegoIcono; JuegoIconoDibujado = icono;   // X53: el pixel del icono es el del juego (1 normal, 4 con el suavizado de la caja), no el lienzo de 68 x 56 de PKHeX
            var liso = icono is not null && !SpritesDeJuego.EsPixelArt(icono);
            double porPixel = icono is not null ? (Suave ? Sprites.FactorCasilla : 1) : bmp.PixelSize.Width / (double)SpriteComposer.Ancho;
            UltimoDestino = recuadro is null ? Dibujar(context, bmp, origen, area, Suave) : UnSoloPokemon ? Dibujar(context, bmp, origen, area, Suave || liso, unSolo: true) : DibujarComun(context, bmp, origen, porPixel, area, Suave, icono is null ? null : Sprites.ReferenciaIcono(icono), liso, estricto: icono is not null && !liso && !Suave);
            UltimoPorPixel = recuadro is null ? 1 : porPixel;
            if (Marcas is { Count: > 0 } marcas) DibujarMarcas(context, marcas, area, UltimoDestino.Width / (origen.Width / UltimoPorPixel), TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0);
        }
        if (bmp is not null && Brillante && !silueta && !Huevo) DibujarEstrella(context, area);
        if (bmp is null || silueta) DibujarAusente(context, area, bmp is null);
    }

    /// <summary>Recurso ausente: el numero de la especie sobre la silueta (nunca se oculta el contenido). <paramref name="conCirculo"/> dibuja ademas el circulo discontinuo (no hay imagen alguna).</summary>
    private void DibujarAusente(DrawingContext context, Rect area, bool conCirculo = true)
    {
        var pincel = Foreground ?? Brushes.Gray;
        var lado = Math.Min(area.Width, area.Height) * 0.7;
        if (conCirculo)
        {
            var r = new Rect((area.Width - lado) / 2, (area.Height - lado) / 2, lado, lado);
            context.DrawEllipse(null, new Pen(pincel, 1.5, new DashStyle([3, 3], 0)), r);
        }
        var ft = new FormattedText(Especie.ToString(), System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, Math.Max(9, lado * 0.28), pincel);
        context.DrawText(ft, new Point(area.Width / 2 - ft.Width / 2, area.Height / 2 - ft.Height / 2));
    }
}
