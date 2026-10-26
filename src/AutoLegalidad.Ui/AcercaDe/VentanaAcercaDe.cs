// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace AutoLegalidad.Ui.AcercaDe;

/// <summary>
/// Ventana modal «Acerca de Pokekeep». Misma estructura que la de las apps hermanas de la familia Keep: cabecera (nombre, lema, versión), y debajo, con desplazamiento, autoría, créditos y
/// licencias, avisos y novedades por versión. Un RichTextBox de solo lectura: el texto baja de línea siempre (nunca se recorta) y se puede seleccionar y copiar.
/// </summary>
public sealed class VentanaAcercaDe : Form
{
    private readonly RichTextBox _cuerpo = new() { Name = "RTB_PokekeepAcercaDe", Dock = DockStyle.Fill, ReadOnly = true, BorderStyle = BorderStyle.None, BackColor = SystemColors.Window, ScrollBars = RichTextBoxScrollBars.Vertical, DetectUrls = false, TabStop = true };
    private readonly Label _nombre = new() { Name = "L_PokekeepAcercaDeNombre", AutoSize = true };
    private readonly Label _lema = new() { Name = "L_PokekeepAcercaDeLema", AutoSize = true, MaximumSize = new Size(520, 0) };
    private readonly Label _version = new() { Name = "L_PokekeepAcercaDeVersion", AutoSize = true };
    private readonly Button _cerrar = new() { Name = "B_PokekeepAcercaDeCerrar", Text = Textos.Cerrar, DialogResult = DialogResult.OK, AutoSize = true };

    public VentanaAcercaDe(Icon? icono = null, string? carpetaBase = null)
    {
        Name = "PokekeepAcercaDe";
        Text = "Acerca de Pokekeep";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        MinimumSize = new Size(520, 420);
        ClientSize = new Size(640, 640);
        if (icono is not null) Icon = icono;

        var baseFont = SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont;
        _nombre.Text = AcercaDeModelo.Nombre;
        _nombre.Font = new Font(baseFont.FontFamily, baseFont.Size * 2f, FontStyle.Bold);
        _lema.Text = AcercaDeModelo.Lema;
        _version.Text = AcercaDeModelo.VersionCompleta(carpetaBase);
        _version.Font = new Font(baseFont, FontStyle.Bold);
        _version.ForeColor = Color.FromArgb(0x1F, 0x5F, 0xAA);

        var cabecera = new FlowLayoutPanel { Dock = DockStyle.Top, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(18, 14, 18, 8), BackColor = SystemColors.Control };
        cabecera.Controls.AddRange([_nombre, _lema, _version]);

        var pie = new Panel { Dock = DockStyle.Bottom, Height = 44, BackColor = SystemColors.Control };
        _cerrar.Anchor = AnchorStyles.Right | AnchorStyles.Top;
        _cerrar.Location = new Point(pie.Width - _cerrar.PreferredSize.Width - 18, 8);
        pie.Resize += (_, _) => _cerrar.Location = new Point(pie.Width - _cerrar.Width - 18, 8);
        pie.Controls.Add(_cerrar);

        var marco = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18, 6, 8, 6), BackColor = SystemColors.Window };
        marco.Controls.Add(_cuerpo);

        Controls.Add(marco);
        Controls.Add(pie);
        Controls.Add(cabecera);
        AcceptButton = _cerrar;
        CancelButton = _cerrar;
        Rellenar(baseFont, carpetaBase);
    }

    /// <summary>Texto plano de lo que enseña la ventana (para pruebas y para leerlo con tecnología de asistencia).</summary>
    public string TextoCuerpo => _cuerpo.Text;
    public string TextoVersion => _version.Text;
    public RichTextBox Cuerpo => _cuerpo;

    private void Titulo(string t, Font f)
    {
        _cuerpo.SelectionFont = new Font(f.FontFamily, f.Size * 1.2f, FontStyle.Bold);
        _cuerpo.SelectionColor = Color.FromArgb(0x1F, 0x5F, 0xAA);
        _cuerpo.AppendText(t + "\n");
        _cuerpo.SelectionFont = f;
        _cuerpo.SelectionColor = SystemColors.WindowText;
    }

    private void Parrafo(string t, Font f, FontStyle estilo = FontStyle.Regular, int sangria = 0)
    {
        _cuerpo.SelectionIndent = sangria;
        _cuerpo.SelectionHangingIndent = 0;
        _cuerpo.SelectionFont = new Font(f, estilo);
        _cuerpo.AppendText(t + "\n");
        _cuerpo.SelectionIndent = 0;
    }

    private void Rellenar(Font f, string? carpetaBase)
    {
        _cuerpo.Clear();
        Titulo("Autoría", f);
        Parrafo(AcercaDeModelo.Autoria, f);
        Parrafo("", f);
        Titulo("Créditos y licencias", f);
        foreach (var c in AcercaDeModelo.Creditos)
        {
            Parrafo($"{c.Componente} — {c.Licencia}", f, FontStyle.Bold);
            Parrafo(c.Uso, f, sangria: 14);
        }
        Parrafo("", f);
        Parrafo(AcercaDeModelo.Licencia, f);
        Parrafo("", f);
        Parrafo(AcercaDeModelo.AvisoEventos, f);
        Parrafo("", f);
        Parrafo(AcercaDeModelo.AvisoMarcas, f);
        Parrafo("", f);
        Parrafo(AcercaDeModelo.AvisoPrivacidad, f);
        Parrafo("", f);
        Titulo("Novedades", f);
        foreach (var n in AcercaDeModelo.Novedades)
        {
            Parrafo($"Versión {n.Version}   {n.Fecha}", f, FontStyle.Bold);
            Parrafo(n.Resumen, f, FontStyle.Italic);
            foreach (var l in n.Novedades) Parrafo("+ " + l, f, sangria: 14);
            Parrafo("", f);
        }
        _cuerpo.SelectionStart = 0;
        _cuerpo.SelectionLength = 0;
        _cuerpo.ScrollToCaret();
    }

    /// <summary>Abre la ventana modal sobre <paramref name="owner"/> (el icono de la ventana principal se reutiliza).</summary>
    public static void Mostrar(IWin32Window? owner)
    {
        using var v = new VentanaAcercaDe((owner as Form)?.Icon);
        if (owner is null) v.ShowDialog(); else v.ShowDialog(owner);
    }
}
