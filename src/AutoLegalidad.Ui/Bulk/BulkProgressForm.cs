// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Ui.Controls;
namespace AutoLegalidad.Ui.Bulk;

/// <summary>
/// Ventana de progreso de una operación masiva (diseño §1.8 y §3.1): modal respecto a PKHeX mientras dura (la partida no se edita), con barra, porcentaje, «Generando 213 de 866 ·
/// Pikachu · 4 en paralelo · quedan ~3 min», los últimos resultados y Cancelar. Se alimenta de <see cref="BulkCoordinator.ProgressChanged"/>.
/// </summary>
public sealed class BulkProgressForm : Form
{
    private readonly Label _title = new() { Name = "L_PokekeepProgresoTitulo", Dock = DockStyle.Top, Height = 26, Font = new Font(SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont, FontStyle.Bold) };
    private readonly Label _text = new() { Name = "L_PokekeepProgresoTexto", Dock = DockStyle.Top, Height = 38, AutoEllipsis = true };
    private readonly ProgressBar _bar = new() { Name = "PB_PokekeepProgreso", Dock = DockStyle.Top, Height = 22, Minimum = 0, Maximum = 1000, Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 30 };
    private readonly Label _percent = new() { Name = "L_PokekeepProgresoPorcentaje", Dock = DockStyle.Top, Height = 22, TextAlign = ContentAlignment.MiddleRight };
    private readonly ListBox _recent = new() { Name = "LB_PokekeepUltimos", Dock = DockStyle.Fill, IntegralHeight = false, SelectionMode = SelectionMode.None };
    private readonly Label _recentTitle = new() { Text = "Últimos resultados", Dock = DockStyle.Top, Height = 22, TextAlign = ContentAlignment.BottomLeft };
    private readonly AccessibleButton _cancel = new() { Name = "B_PokekeepProgresoCancelar", Text = Textos.Cancelar, AutoSize = true, MinimumSize = new Size(110, 30) };
    private readonly System.Windows.Forms.Timer _clock = new() { Interval = 250 };
    private readonly DateTime _started = DateTime.UtcNow;
    private readonly BulkCoordinator _coordinator;
    private bool _allowClose;
    private string _phase = string.Empty;

    public BulkProgressForm(BulkCoordinator coordinator, string title)
    {
        _coordinator = coordinator;
        Name = "PokekeepBulkProgressForm";
        Text = title;
        _title.Text = title;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ControlBox = false;                 // solo se sale con Cancelar o al terminar (no se deja a medias sin decidir)
        ClientSize = new Size(620, 380);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 44, Padding = new Padding(0, 6, 0, 0) };
        buttons.Controls.Add(_cancel);
        var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14, 10, 14, 6) };
        body.Controls.Add(_recent);
        body.Controls.Add(_recentTitle);
        body.Controls.Add(_percent);
        body.Controls.Add(_bar);
        body.Controls.Add(_text);
        body.Controls.Add(_title);
        Controls.Add(body);
        Controls.Add(buttons);

        _cancel.Click += (_, _) => { _cancel.Enabled = false; _coordinator.Cancel(); };
        _coordinator.ProgressChanged += OnProgress;
        _clock.Tick += (_, _) => RefreshText(null);
        _clock.Start();
        FormClosing += (_, e) => { if (!_allowClose && e.CloseReason == CloseReason.UserClosing) e.Cancel = true; };
        Shown += (_, _) => { if (_allowClose) Close(); };      // la operación terminó antes de que la ventana llegara a mostrarse
    }

    public Button CancelButton_ => _cancel;
    public string StatusText => _text.Text;
    public string PercentText => _percent.Text;
    public ListBox RecentList => _recent;
    public ProgressBar Bar => _bar;

    /// <summary>Cierra la ventana (la operación terminó).</summary>
    public void Finish()
    {
        _allowClose = true;
        _clock.Stop();
        _coordinator.ProgressChanged -= OnProgress;
        if (Visible) Close();
    }

    private BulkProgressInfo? _last;

    private void OnProgress(object? sender, BulkProgressInfo info)
    {
        _last = info;
        _phase = info.Phase;
        var p = info.Progress;
        if (p is { Total: > 0 } && !_phase.StartsWith("Cancelando", StringComparison.Ordinal) && (p.Done > 0 || p.InFlight > 0))
        {
            _bar.Style = ProgressBarStyle.Continuous;
            _bar.Value = (int)Math.Clamp(p.Fraction * 1000, 0, 1000);
            _percent.Text = Textos.Porcentaje(p.Fraction);
        }
        else if (_phase == Textos.Comprobando || _phase == Textos.Aplicando)
        {
            _bar.Style = ProgressBarStyle.Marquee;
            _percent.Text = string.Empty;
        }
        RefreshText(info);
        _cancel.Enabled = !_phase.StartsWith("Cancelando", StringComparison.Ordinal) && _phase != Textos.Comprobando && _phase != Textos.Aplicando;
        _recent.BeginUpdate();
        _recent.Items.Clear();
        foreach (var line in info.Recent.AsEnumerable().Reverse()) _recent.Items.Add(line);
        _recent.EndUpdate();
    }

    private void RefreshText(BulkProgressInfo? info)
    {
        info ??= _last;
        var elapsed = DateTime.UtcNow - _started;
        _text.Text = (info?.Phase ?? _phase) + (elapsed.TotalSeconds > 1 ? $"  ·  {Textos.Segundos(elapsed)} transcurridos" : string.Empty);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _coordinator.ProgressChanged -= OnProgress;
            _clock.Dispose();
        }
        base.Dispose(disposing);
    }
}

/// <summary>La ÚNICA pregunta que hace una operación masiva (diseño §1.8): al cancelar con Pokémon ya generados, ¿se colocan o se descartan? No es un diálogo de alternativas (regla 4): es la confirmación de qué hacer con lo ya hecho.</summary>
public sealed class BulkPlacementDialog : Form
{
    public BulkPlacementDialog(int generated, bool edit)
    {
        Name = "PokekeepBulkPlacementDialog";
        Text = edit ? Textos.EdicionMasiva : Textos.CompletarElJuego;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(520, 112);
        var label = new Label
        {
            Name = "L_PokekeepColocar",
            Dock = DockStyle.Top,
            Height = 52,
            Padding = new Padding(16, 14, 16, 0),
            Text = LivingDexPreview.PlacementQuestion(generated, edit),   // el mismo texto que la pregunta del móvil (F9, 9b)
        };
        var place = new AccessibleButton { Name = "B_PokekeepColocar", Text = edit ? "Aplicar" : "Colocar", AutoSize = true, MinimumSize = new Size(110, 30), DialogResult = DialogResult.Yes };
        var discard = new AccessibleButton { Name = "B_PokekeepDescartar", Text = "Descartar", AutoSize = true, MinimumSize = new Size(110, 30), DialogResult = DialogResult.No };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 48, Padding = new Padding(0, 6, 12, 0) };
        buttons.Controls.Add(discard);
        buttons.Controls.Add(place);
        Controls.Add(label);
        Controls.Add(buttons);
        AcceptButton = place;
        CancelButton = discard;
    }
}
