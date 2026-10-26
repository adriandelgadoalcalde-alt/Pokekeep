// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Pokekeep.Mobile.Ui.Design;

/// <summary>
/// Icono de objeto en su hueco (uso10): nunca se recorta. Los de Switch miden más que el hueco de la fila y con <c>Stretch="None"</c>
/// salían cortados (también las megapiedras de Z-A); se reducen a su hueco y, solo al reducir, con interpolación de calidad. Los
/// iconos de pixel art que caben se dibujan a su tamaño, píxel a píxel, como antes.
/// </summary>
public sealed class IconoObjetoImagen : Image
{
    public IconoObjetoImagen()
    {
        Stretch = Stretch.Uniform;   // con StretchDirection="DownOnly" (mochila) los pequeños quedan a su tamaño; en el selector llenan su hueco
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
    }

    protected override Type StyleKeyOverride => typeof(Image);

    protected override Size ArrangeOverride(Size finalSize)
    {
        var r = base.ArrangeOverride(finalSize);
        var reduce = Source is Bitmap b && (b.Size.Width > r.Width + 0.5 || b.Size.Height > r.Height + 0.5);   // solo al reducir: ampliar pixel art sigue nítido
        var modo = reduce ? BitmapInterpolationMode.HighQuality : BitmapInterpolationMode.None;
        if (RenderOptions.GetBitmapInterpolationMode(this) != modo) RenderOptions.SetBitmapInterpolationMode(this, modo);
        return r;
    }
}
