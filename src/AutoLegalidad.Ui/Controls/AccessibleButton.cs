// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace AutoLegalidad.Ui.Controls;

/// <summary>Colores del texto atenuado de los diálogos de Pokekeep y cálculo de la relación de contraste WCAG 2.x.</summary>
public static class AccessibleColors
{
    /// <summary>Relación mínima de contraste WCAG (nivel AA) para texto normal.</summary>
    public const double MinimumContrast = 4.5;

    /// <summary>Texto de un botón desactivado: gris medio-oscuro (en cursiva), claramente más claro que el texto activo (negro) pero legible sobre el fondo del botón.</summary>
    public static readonly Color DisabledText = Color.FromArgb(0x59, 0x59, 0x59);
    /// <summary>Fondo de un botón desactivado (plano y más claro que el de uno activo).</summary>
    public static readonly Color DisabledFace = Color.FromArgb(0xF0, 0xF0, 0xF0);
    public static readonly Color DisabledBorder = Color.FromArgb(0xA0, 0xA0, 0xA0);

    /// <summary>Luminancia relativa de WCAG 2.x (sRGB).</summary>
    public static double Luminance(Color c)
    {
        static double Lin(byte v)
        {
            var s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Lin(c.R) + 0.7152 * Lin(c.G) + 0.0722 * Lin(c.B);
    }

    /// <summary>Relación de contraste entre dos colores opacos, de 1 a 21.</summary>
    public static double ContrastRatio(Color a, Color b)
    {
        var la = Luminance(a);
        var lb = Luminance(b);
        var (hi, lo) = la >= lb ? (la, lb) : (lb, la);
        return (hi + 0.05) / (lo + 0.05);
    }
}

/// <summary>
/// Botón cuyo estado desactivado se dibuja con contraste suficiente: el de WinForms pinta el texto en <c>A0A0A0</c> sobre <c>F0F0F0</c> (2,3:1, ilegible para mucha gente).
/// Activado es un <see cref="Button"/> normal; en el modo de alto contraste de Windows se respeta el dibujo del sistema.
/// </summary>
public class AccessibleButton : Button
{
    private Font? _italic;

    protected override void OnPaint(PaintEventArgs pevent)
    {
        if (Enabled || SystemInformation.HighContrast)
        {
            base.OnPaint(pevent);
            return;
        }
        var g = pevent.Graphics;
        var r = ClientRectangle;
        using (var face = new SolidBrush(AccessibleColors.DisabledFace)) g.FillRectangle(face, r);
        using (var pen = new Pen(AccessibleColors.DisabledBorder)) g.DrawRectangle(pen, r.X, r.Y, r.Width - 1, r.Height - 1);
        if (_italic is null || _italic.Name != Font.Name || _italic.Size != Font.Size)
        {
            _italic?.Dispose();
            _italic = new Font(Font, Font.Style | FontStyle.Italic);
        }
        TextRenderer.DrawText(g, Text, _italic, r, AccessibleColors.DisabledText, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _italic?.Dispose();
        base.Dispose(disposing);
    }
}
