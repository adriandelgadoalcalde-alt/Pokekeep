// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Pokekeep.Mobile.Ui.Design;

/// <summary>
/// El fondo de una caja (los <c>box_wp*</c> de PKHeX), dibujado como el juego lo pinta y no como un adorno apagado (X6). Dos decisiones, las dos medidas sobre los fondos reales (docs/android/seguimiento-cierre-codex.md, X6):
/// <list type="bullet">
/// <item>Muestreo: los fondos de pixel (Gen. 3-5 de 156-162 px, Gen. 1-2 y 6-7 de 220 px) se amplian 6-9 veces: con interpolacion suave salen borrosos, asi que se dibujan SIN interpolar (<c>None</c>, pixel nitido); los modernos (417-576 px) conservan <c>HighQuality</c>.</item>
/// <item>Encaje: los fondos de Gen. 3-5 llevan MARCO propio pensado para llenar el panel (PKHeX los estira: <c>BackgroundImageLayout = Stretch</c>, hasta un 17 %). Si el panel y la imagen difieren como mucho un 16 % se llena el panel (no se pierde el marco y la deformacion es inapreciable);
/// si difieren mas (Espada/Escudo y Escarlata/Purpura, 1,45 : 1) se recorta por el centro al llenar, sin deformar nada.</item>
/// </list>
/// </summary>
public sealed class FondoCajaImagen : Control
{
    public static readonly StyledProperty<IImage?> SourceProperty = AvaloniaProperty.Register<FondoCajaImagen, IImage?>(nameof(Source));

    /// <summary>Hasta este ancho (px) un fondo se considera pixel art y se dibuja sin interpolar.</summary>
    public const double AnchoMaximoDePixelArt = 256;

    /// <summary>Diferencia maxima de proporcion (fraccion) entre la imagen y el panel para llenar el panel estirando en lugar de recortar.</summary>
    public const double TolerarProporcion = 0.16;

    static FondoCajaImagen()
    {
        AffectsRender<FondoCajaImagen>(SourceProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<FondoCajaImagen>(false);
    }

    public IImage? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }

    /// <summary>Ultimo rectangulo de origen y de destino que se dibujo (para las pruebas).</summary>
    public (Rect Origen, Rect Destino) UltimoEncaje { get; private set; }

    /// <summary>Modo de muestreo con el que se dibuja el fondo actual.</summary>
    public BitmapInterpolationMode Muestreo => RenderOptions.GetBitmapInterpolationMode(this);

    public static bool EsPixelArt(double anchoPx) => anchoPx <= AnchoMaximoDePixelArt;

    public static BitmapInterpolationMode MuestreoDe(double anchoPx) => EsPixelArt(anchoPx) ? BitmapInterpolationMode.None : BitmapInterpolationMode.HighQuality;

    /// <summary>Que parte de la imagen se dibuja y donde: toda la imagen sobre todo el panel si las proporciones casi coinciden; si no, el recorte central que llena el panel con la proporcion del panel.</summary>
    public static (Rect Origen, Rect Destino) Encajar(Size imagen, Size panel)
    {
        var destino = new Rect(panel);
        var completo = new Rect(imagen);
        if (imagen.Width <= 0 || imagen.Height <= 0 || panel.Width <= 0 || panel.Height <= 0) return (completo, destino);
        var proporcionImagen = imagen.Width / imagen.Height;
        var proporcionPanel = panel.Width / panel.Height;
        if (Math.Abs(proporcionImagen / proporcionPanel - 1) <= TolerarProporcion) return (completo, destino);
        if (proporcionImagen > proporcionPanel)
        {
            var ancho = imagen.Height * proporcionPanel;
            return (new Rect((imagen.Width - ancho) / 2, 0, ancho, imagen.Height), destino);
        }
        var alto = imagen.Width / proporcionPanel;
        return (new Rect(0, (imagen.Height - alto) / 2, imagen.Width, alto), destino);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourceProperty && change.NewValue is IImage img) RenderOptions.SetBitmapInterpolationMode(this, MuestreoDe(img.Size.Width));
    }

    public override void Render(DrawingContext context)
    {
        if (Source is not { } img || Bounds.Width <= 0 || Bounds.Height <= 0) return;
        var (origen, destino) = Encajar(img.Size, Bounds.Size);
        UltimoEncaje = (origen, destino);
        context.DrawImage(img, origen, destino);
    }
}
