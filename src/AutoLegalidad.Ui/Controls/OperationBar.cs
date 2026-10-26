// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.ComponentModel;
using AutoLegalidad.Ui.Bulk;
using AutoLegalidad.Ui.Operation;
using PKHeX.Drawing.PokeSprite;

namespace AutoLegalidad.Ui.Controls;

/// <summary>
/// Barra NO MODAL de Pokekeep (Hito 4): mientras trabaja el motor muestra la fase («Analizando», «Buscando un origen válido», «Comprobando», progreso de
/// «Optimizar») con <b>Cancelar</b>; al terminar muestra el resumen con «Ver cambios» y «Deshacer». No bloquea la ventana: PKHeX sigue siendo utilizable
/// (salvo las acciones de Pokekeep, que esperan a que termine la operación en curso).
/// </summary>
public sealed class OperationBar : UserControl
{
    private readonly PokekeepCoordinator _coordinator;
    private readonly Panel _progressPanel = new() { Dock = DockStyle.Fill, Visible = false };
    private readonly Panel _resultPanel = new() { Dock = DockStyle.Fill, Visible = false };
    private readonly Label _target = new() { AutoSize = false, Dock = DockStyle.Top, Height = 18, AutoEllipsis = true, Font = new Font(SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont, FontStyle.Bold) };
    private readonly Label _phase = new() { AutoSize = false, Dock = DockStyle.Top, Height = 18, AutoEllipsis = true };
    private readonly ProgressBar _progress = new() { Dock = DockStyle.Top, Height = 8, Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 30 };
    private readonly Label _headline = new() { AutoSize = false, Dock = DockStyle.Top, Height = 20, AutoEllipsis = true, Font = new Font(SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont, FontStyle.Bold) };
    private readonly Label _detail = new() { AutoSize = false, Dock = DockStyle.Fill };
    private readonly PictureBox _icon = new() { Dock = DockStyle.Left, Width = 32, SizeMode = PictureBoxSizeMode.CenterImage };
    private readonly Button _cancel = new() { Name = "B_PokekeepCancelar", Text = Textos.Cancelar, AutoSize = true, Margin = new Padding(3, 2, 3, 2) };
    private readonly Button _changes = new() { Name = "B_PokekeepVerCambios", Text = Textos.VerCambios, AutoSize = true, Margin = new Padding(3, 2, 3, 2) };
    private readonly Button _undo = new() { Name = "B_PokekeepDeshacer", Text = Textos.Deshacer, AutoSize = true, Margin = new Padding(3, 2, 3, 2) };
    private readonly Button _close = new() { Name = "B_PokekeepCerrar", Text = "✕ " + Textos.Cerrar, AccessibleName = Textos.CerrarResumen, AutoSize = true, Margin = new Padding(3, 2, 3, 2), ForeColor = SystemColors.ControlText, UseVisualStyleBackColor = true };
    private readonly FlowLayoutPanel _buttons = new() { Dock = DockStyle.Right, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(0, 6, 4, 0) };
    private readonly ToolTip _tip = new();
    private readonly System.Windows.Forms.Timer _clock = new() { Interval = 200 };
    private DateTime _started;
    private string _phaseText = string.Empty;
    private OperationResult? _result;
    private ChangesForm? _changesForm;
    private BulkCoordinator? _bulk;
    private BulkDialogs? _bulkDialogs;

    public OperationBar(PokekeepCoordinator coordinator)
    {
        _coordinator = coordinator;
        Name = "PokekeepBar";
        AccessibleName = "Barra de Pokekeep";
        Dock = DockStyle.Bottom;
        Height = MinBarHeight;
        Visible = false;
        Padding = new Padding(0, 1, 0, 0);

        _progressPanel.Controls.Add(_progress);
        _progressPanel.Controls.Add(_phase);
        _progressPanel.Controls.Add(_target);
        _resultPanel.Controls.Add(_detail);
        _resultPanel.Controls.Add(_headline);
        _buttons.Controls.AddRange([_cancel, _changes, _undo, _close]);

        var center = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4, 4, 4, 2) };
        center.Controls.Add(_resultPanel);
        center.Controls.Add(_progressPanel);
        Controls.Add(center);
        Controls.Add(_buttons);
        Controls.Add(_icon);

        _cancel.Click += (_, _) => { _cancel.Enabled = false; _coordinator.Cancel(); };
        _changes.Click += (_, _) => ShowChanges();
        _undo.Click += (_, _) => DoUndo();
        _tip.SetToolTip(_close, Textos.CerrarResumenAyuda);
        _close.Click += (_, _) => Hide();
        Resize += (_, _) => { if (_resultPanel.Visible) FitHeight(); };
        _clock.Tick += (_, _) => UpdateElapsed();

        _coordinator.ProgressChanged += OnProgress;
        _coordinator.Completed += OnCompleted;
    }

    /// <summary>Hito 4b: enlaza las operaciones masivas con la barra: su resumen («Completar el juego: 862 colocados…») sale aquí con «Ver informe» y «Deshacer» (el progreso lo muestra su propia ventana modal).</summary>
    public void AttachBulk(BulkCoordinator bulk, BulkDialogs dialogs)
    {
        _bulk = bulk;
        _bulkDialogs = dialogs;
        bulk.Completed += OnCompleted;
    }

    /// <summary>Resultado mostrado ahora (para pruebas).</summary>
    public OperationResult? CurrentResult => _result;
    public bool ShowingProgress => _progressPanel.Visible;
    public string PhaseText => _phaseText;
    public Button CancelButton_ => _cancel;
    public Button UndoButton => _undo;
    public Button ChangesButton => _changes;
    public string HeadlineText => _headline.Text;
    public string DetailText => _detail.Text;
    public Image? IconImage => _icon.Image;
    public Button CloseButton => _close;
    public string CloseToolTip => _tip.GetToolTip(_close) ?? string.Empty;
    public bool DetailEllipsis => _detail.AutoEllipsis;
    /// <summary>Alto que necesita el texto del resumen ahora mismo (lo que la barra ocupa).</summary>
    public int DetailTextHeight => TextRenderer.MeasureText(_detail.Text, _detail.Font, new Size(Math.Max(160, Width - _icon.Width - _buttons.PreferredSize.Width - 24), 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding).Height;
    public int DetailLabelHeight => _detail.Height;

    private void OnProgress(object? sender, OperationProgress p)
    {
        if (!_progressPanel.Visible)
        {
            _started = DateTime.UtcNow;
            _clock.Start();
            _cancel.Enabled = true;
        }
        _result = null;
        _resultPanel.Visible = false;
        _progressPanel.Visible = true;
        _cancel.Visible = true;
        _changes.Visible = _undo.Visible = false;
        _close.Visible = false;
        _icon.Image = null;
        _target.Text = $"{p.Action.Title()} · {p.Target}";
        _phaseText = p.Text;
        if (p.Fraction is { } f)
        {
            _progress.Style = ProgressBarStyle.Continuous;
            _progress.Minimum = 0;
            _progress.Maximum = 1000;
            _progress.Value = (int)Math.Clamp(f * 1000, 0, 1000);
        }
        else if (_progress.Style != ProgressBarStyle.Marquee)
        {
            _progress.Style = ProgressBarStyle.Marquee;
        }
        UpdateElapsed();
        _cancel.Enabled = p.Text != Textos.Cancelando;
        FitHeight();
        Visible = true;
    }

    private void UpdateElapsed()
    {
        var elapsed = DateTime.UtcNow - _started;
        _phase.Text = $"{_phaseText} · {Textos.Segundos(elapsed)}";
    }

    private void OnCompleted(object? sender, OperationResult r) => Present(r);

    /// <summary>Muestra un resultado en la barra (lo llama el coordinador al terminar; también las pruebas).</summary>
    public void Present(OperationResult r)
    {
        _clock.Stop();
        _result = r;
        _progressPanel.Visible = false;
        if (r.Kind == OutcomeKind.Busy)
            return;
        if (!r.IsFailure && r.Kind != OutcomeKind.Cancelled && !_coordinator.Settings.ShowSummary)
        {
            Hide();
            return;
        }
        _resultPanel.Visible = true;
        _headline.Text = r.Applied && r.Origin != OperationOrigin.Library && r.TargetDescription is { } t ? $"{r.Headline} · {t}" : r.Headline;
        var lines = new List<string>(r.Summary);
        if (r.Detail is { Length: > 0 } d) lines.Add(d);
        _detail.Text = string.Join("  ·  ", lines);
        _tip.SetToolTip(_detail, string.Join(Environment.NewLine, lines));
        _tip.SetToolTip(_headline, _headline.Text);
        _icon.Image = IconFor(r);
        _cancel.Visible = false;
        _changes.Visible = r.Bulk is not null || r.Changes.Count > 0 || r.Detail is not null || r.Summary.Count > 0;
        _changes.Text = r.Bulk is not null ? "Ver informe" : r.Applied ? Textos.VerCambios : "Ver detalles";
        _undo.Visible = r.Undo is not null;
        _undo.Enabled = r.Undo?.CanUndo() ?? false;
        _close.Visible = true;
        Visible = true;
        FitHeight();
    }

    private const int MinBarHeight = 58;
    private const int MaxBarHeight = 190;

    /// <summary>
    /// El resumen se lee ENTERO sin abrir otra ventana: la barra crece lo que haga falta (con un máximo) para que el texto largo se parta en varias líneas en vez de cortarse con «…».
    /// Si aun así no cabe, el texto completo está en la ayuda emergente y en «Ver cambios».
    /// </summary>
    private void FitHeight()
    {
        int wanted;
        if (!_resultPanel.Visible || string.IsNullOrEmpty(_detail.Text))
            wanted = MinBarHeight;
        else
        {
            var textWidth = Math.Max(160, Width - _icon.Width - _buttons.PreferredSize.Width - 24);
            var measured = TextRenderer.MeasureText(_detail.Text, _detail.Font, new Size(textWidth, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding).Height;
            wanted = Padding.Top + 4 + _headline.Height + measured + 10;
        }
        wanted = Math.Clamp(wanted, MinBarHeight, MaxBarHeight);
        _detail.AutoEllipsis = wanted >= MaxBarHeight; // solo si ni así cabe
        if (Height != wanted)
            Height = wanted;
    }

    private static Bitmap? WarningIcon()
    {
        try
        {
            using var icon = new Icon(SystemIcons.Warning, 24, 24);
            return icon.ToBitmap();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return null;
        }
    }

    private static Image? IconFor(OperationResult r)
    {
        try
        {
            return r.Kind switch
            {
                OutcomeKind.AppliedNeedsHome or OutcomeKind.UnchangedNeedsHome => WarningIcon(),
                OutcomeKind.Applied or OutcomeKind.AlreadyLegal or OutcomeKind.AlreadyOptimal => SpriteUtil.GetLegalIndicator(true),
                OutcomeKind.Cancelled => null,
                _ => SpriteUtil.GetLegalIndicator(false),
            };
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return null;
        }
    }

    private void DoUndo()
    {
        if (_result?.Bulk is { } bulk)
        {
            // Una operación masiva: «Deshacer» revierte TODO el lote de una vez y solo si todas las casillas siguen como las dejó Pokekeep.
            if (bulk.Undo is { } token && token.Undo())
            {
                _headline.Text = Textos.DeshechoCorrectamente;
                _detail.Text = $"Se han restaurado {token.SlotCount} casillas de una vez.";
                _tip.SetToolTip(_detail, _detail.Text);
                _tip.SetToolTip(_headline, Textos.DeshechoCorrectamente);
                _icon.Image = null;
                _undo.Visible = false;
                _changes.Visible = false;
            }
            else
            {
                _detail.Text = bulk.Undo?.RefusalReason() ?? Textos.UndoNoPosible;
                _undo.Enabled = false;
            }
            FitHeight();
            return;
        }
        if (_coordinator.UndoLast())
        {
            _headline.Text = Textos.DeshechoCorrectamente;
            _detail.Text = string.Empty;
            _tip.SetToolTip(_detail, string.Empty);
            _tip.SetToolTip(_headline, Textos.DeshechoCorrectamente);
            _icon.Image = null;             // el visto verde era del resultado anterior: ya no hay nada legalizado que indicar
            _undo.Visible = false;
            _changes.Visible = false;
            FitHeight();
        }
        else
        {
            _detail.Text = Textos.UndoNoPosible;
            _undo.Enabled = false;
            FitHeight();
        }
    }

    private void ShowChanges()
    {
        if (_result is null) return;
        if (_result.Bulk is { } bulk)
        {
            _bulkDialogs?.ShowReport(bulk, FindForm());
            return;
        }
        if (_changesForm is null || _changesForm.IsDisposed)
            _changesForm = new ChangesForm();
        _changesForm.Present(_result, FindForm());
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _coordinator.ProgressChanged -= OnProgress;
            _coordinator.Completed -= OnCompleted;
            if (_bulk is not null) _bulk.Completed -= OnCompleted;
            _clock.Dispose();
            _tip.Dispose();
            _changesForm?.Dispose();
        }
        base.Dispose(disposing);
    }
}
