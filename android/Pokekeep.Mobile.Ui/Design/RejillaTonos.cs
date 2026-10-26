// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Pokekeep.Mobile.Ui.Design;

/// <summary>
/// Rejilla táctil de casillas de un tono cada una, para los controles de PINTAR de PKHeX (un <c>PictureBox</c> con clic): el «Artista puntos» del Poké-reloj (24×20, 4 grises) y, en Gen. 5, el C-Gear y los fondos.
/// Un toque sobre una casilla pasa al tono siguiente (0→1→2→3→0, como <c>SetFlagsFromClickPoint</c>) y arrastrar el dedo pinta las casillas que cruza con el tono de la primera. Con teclado: flechas para moverse y Espacio o Intro para cambiar de tono.
/// <para>El valor es un TEXTO con una línea por fila y un carácter por casilla (<c>0</c>-<c>9</c>, <c>a</c>-<c>z</c>: el índice del tono en <see cref="Paleta"/>): así el borrador, el Deshacer y las pruebas trabajan con el mismo formato que antes de tener rejilla.</para>
/// </summary>
public sealed class RejillaTonos : Control
{
    public static readonly StyledProperty<string> ValorProperty = AvaloniaProperty.Register<RejillaTonos, string>(nameof(Valor), string.Empty, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
    public static readonly StyledProperty<int> AnchoCasillasProperty = AvaloniaProperty.Register<RejillaTonos, int>(nameof(AnchoCasillas), 24);
    public static readonly StyledProperty<int> AltoCasillasProperty = AvaloniaProperty.Register<RejillaTonos, int>(nameof(AltoCasillas), 20);
    public static readonly StyledProperty<IReadOnlyList<uint>> PaletaProperty = AvaloniaProperty.Register<RejillaTonos, IReadOnlyList<uint>>(nameof(Paleta), Grises(248, 168, 88, 8));
    public static readonly StyledProperty<double> MaxCasillaProperty = AvaloniaProperty.Register<RejillaTonos, double>(nameof(MaxCasilla), 32);

    static RejillaTonos()
    {
        AffectsMeasure<RejillaTonos>(AnchoCasillasProperty, AltoCasillasProperty, MaxCasillaProperty);
        AffectsRender<RejillaTonos>(ValorProperty, PaletaProperty, AnchoCasillasProperty, AltoCasillasProperty, IsEnabledProperty);
        FocusableProperty.OverrideDefaultValue<RejillaTonos>(true);
    }

    public RejillaTonos() { GotFocus += (_, _) => InvalidateVisual(); LostFocus += (_, _) => InvalidateVisual(); }

    private int _cx, _cy;
    private int _pincel = -1;
    private (int X, int Y) _ultima = (-1, -1);

    /// <summary>Una línea por fila, un carácter por casilla (índice del tono en <see cref="Paleta"/>).</summary>
    public string Valor { get => GetValue(ValorProperty); set => SetValue(ValorProperty, value); }
    public int AnchoCasillas { get => GetValue(AnchoCasillasProperty); set => SetValue(AnchoCasillasProperty, value); }
    public int AltoCasillas { get => GetValue(AltoCasillasProperty); set => SetValue(AltoCasillasProperty, value); }
    /// <summary>Un color (0xRRGGBB) por tono; el tono 0 es el primero.</summary>
    public IReadOnlyList<uint> Paleta { get => GetValue(PaletaProperty); set => SetValue(PaletaProperty, value); }
    /// <summary>Lado máximo de una casilla (dp): en una tableta la rejilla no crece sin límite.</summary>
    public double MaxCasilla { get => GetValue(MaxCasillaProperty); set => SetValue(MaxCasillaProperty, value); }

    /// <summary>Tonos de gris (0-255) como colores de la paleta.</summary>
    public static IReadOnlyList<uint> Grises(params byte[] niveles) => [.. niveles.Select(g => ((uint)g << 16) | ((uint)g << 8) | g)];

    private static int IndiceDe(char c) => c is >= '0' and <= '9' ? c - '0' : c is >= 'a' and <= 'z' ? c - 'a' + 10 : 0;
    private static char CaracterDe(int i) => (char)(i < 10 ? '0' + i : 'a' + (i - 10));

    private static string[] Filas(string valor) => valor.Replace("\r\n", "\n").Split('\n');

    /// <summary>El tono de la casilla (x, y) del texto; una casilla que el texto no trae vale 0.</summary>
    public static int Tono(string valor, int x, int y)
    {
        var f = Filas(valor);
        return y >= 0 && y < f.Length && x >= 0 && x < f[y].Length ? IndiceDe(f[y][x]) : 0;
    }

    /// <summary>El texto con la casilla (x, y) puesta a <paramref name="tono"/> (las filas y columnas que faltaban se completan con 0).</summary>
    public static string ConTono(string valor, int ancho, int alto, int x, int y, int tono)
    {
        var f = Filas(valor).Take(alto).Select(l => l.Length >= ancho ? l[..ancho] : l.PadRight(ancho, '0')).ToList();
        while (f.Count < alto) f.Add(new string('0', ancho));
        var chars = f[y].ToCharArray(); chars[x] = CaracterDe(tono); f[y] = new string(chars);
        return string.Join('\n', f);
    }

    /// <summary>El texto tras tocar la casilla (x, y): pasa al tono siguiente y, tras el último, vuelve al 0.</summary>
    public static string Ciclar(string valor, int ancho, int alto, int x, int y, int tonos)
        => ConTono(valor, ancho, alto, x, y, (Tono(valor, x, y) + 1) % Math.Max(1, tonos));

    private double Casilla(double ancho) => Math.Max(1, Math.Min(MaxCasilla, Math.Floor(ancho / Math.Max(1, AnchoCasillas))));

    protected override Size MeasureOverride(Size disponible)
    {
        var ancho = double.IsInfinity(disponible.Width) ? AnchoCasillas * MaxCasilla : disponible.Width;
        var c = Casilla(ancho);
        return new Size(c * AnchoCasillas, c * AltoCasillas);
    }

    private (int X, int Y)? Casilla(Point p)
    {
        var c = Casilla(Bounds.Width);
        var x = (int)Math.Floor(p.X / c); var y = (int)Math.Floor(p.Y / c);
        return x >= 0 && y >= 0 && x < AnchoCasillas && y < AltoCasillas ? (x, y) : null;
    }

    private void Poner(int x, int y, int tono)
    {
        if (!IsEnabled) return;
        Valor = ConTono(Valor, AnchoCasillas, AltoCasillas, x, y, tono);
    }

    /// <summary>Mismo consumidor que pintar, desde los mandos accesibles de Ampliar.</summary>
    public void CiclarCasilla(int x, int y)
    {
        if (!IsEnabled || x < 0 || x >= AnchoCasillas || y < 0 || y >= AltoCasillas) return;
        _cx=x; _cy=y;
        SetCurrentValue(ValorProperty, Ciclar(Valor,AnchoCasillas,AltoCasillas,x,y,Paleta.Count));
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!IsEnabled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || Casilla(e.GetPosition(this)) is not { } q) return;
        Focus();
        _cx = q.X; _cy = q.Y;
        _pincel = (Tono(Valor, q.X, q.Y) + 1) % Math.Max(1, Paleta.Count);
        _ultima = q;
        Poner(q.X, q.Y, _pincel);
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_pincel < 0 || !ReferenceEquals(e.Pointer.Captured, this) || Casilla(e.GetPosition(this)) is not { } q || q == _ultima) return;
        _ultima = q; _cx = q.X; _cy = q.Y;
        if (Tono(Valor, q.X, q.Y) != _pincel) Poner(q.X, q.Y, _pincel);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _pincel = -1; _ultima = (-1, -1);
        if (ReferenceEquals(e.Pointer.Captured, this)) e.Pointer.Capture(null);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!IsEnabled) return;
        switch (e.Key)
        {
            case Key.Left: _cx = Math.Max(0, _cx - 1); break;
            case Key.Right: _cx = Math.Min(AnchoCasillas - 1, _cx + 1); break;
            case Key.Up: _cy = Math.Max(0, _cy - 1); break;
            case Key.Down: _cy = Math.Min(AltoCasillas - 1, _cy + 1); break;
            case Key.Space or Key.Enter: Poner(_cx, _cy, (Tono(Valor, _cx, _cy) + 1) % Math.Max(1, Paleta.Count)); break;
            default: return;
        }
        InvalidateVisual();
        e.Handled = true;
    }



    public override void Render(DrawingContext g)
    {
        var c = Casilla(Bounds.Width);
        var filas = Filas(Valor);
        var paleta = Paleta;
        var pinceles = paleta.Select(p => new SolidColorBrush(Color.FromRgb((byte)(p >> 16), (byte)(p >> 8), (byte)p))).ToArray();
        var linea = new Pen(this.TryFindResource("BordeCasilla", ActualThemeVariant, out var r) && r is IBrush b ? b : Brushes.Gray, 1);
        var opacidad = IsEnabled ? 1.0 : 0.5;
        using (g.PushOpacity(opacidad))
        {
            for (var y = 0; y < AltoCasillas; y++)
                for (var x = 0; x < AnchoCasillas; x++)
                {
                    var t = y < filas.Length && x < filas[y].Length ? IndiceDe(filas[y][x]) : 0;
                    g.DrawRectangle(pinceles[Math.Min(t, pinceles.Length - 1)], null, new Rect(x * c, y * c, c, c));
                }
            // Líneas de casilla: finas, para que cada punto se vea como un punto de la pantalla del juego.
            var ancho = c * AnchoCasillas; var alto = c * AltoCasillas;
            for (var x = 0; x <= AnchoCasillas; x++) g.DrawLine(linea, new Point(x * c, 0), new Point(x * c, alto));
            for (var y = 0; y <= AltoCasillas; y++) g.DrawLine(linea, new Point(0, y * c), new Point(ancho, y * c));
        }
        if (IsFocused)
        {
            var foco = this.TryFindResource("Dorado", ActualThemeVariant, out var d) && d is IBrush bd ? bd : Brushes.Orange;
            g.DrawRectangle(null, new Pen(foco, 3), new Rect(_cx * c, _cy * c, c, c));
        }
    }
}
