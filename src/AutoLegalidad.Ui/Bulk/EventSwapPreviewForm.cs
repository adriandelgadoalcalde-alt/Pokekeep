// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Ui.Controls;

namespace AutoLegalidad.Ui.Bulk;

/// <summary>
/// Vista previa de «Preferir la versión de evento» (Hito 5d, ADR 0046): una fila por Pokémon legendario, sublegendario o singular de la selección, con lo que se propone («Rayquaza (salvaje) → evento «…» · año · idioma»),
/// por qué esa tarjeta y, si no se puede, el motivo. Solo lo MARCADO se aplica; se puede desmarcar uno a uno. Cambia el ORIGEN del Pokémon (entrenador, fecha, ball, cintas...): el aviso lo dice claro.
/// </summary>
public sealed class EventSwapPreviewForm : Form
{
    private readonly EventSwapPlan _plan;
    private readonly Label _summary = new() { Name = "L_PokekeepEventoResumen", Dock = DockStyle.Top, AutoSize = false, Height = 46, Padding = new Padding(0, 2, 0, 4), Font = new Font(SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont, FontStyle.Bold) };
    private readonly Label _warning = new() { Name = "L_PokekeepEventoAviso", Dock = DockStyle.Top, AutoSize = false, Height = 62, Padding = new Padding(0, 0, 0, 4),
        Text = BulkEditTexts.EventSwapWarning };
    private readonly ListView _list = new() { Name = "LV_PokekeepEvento", Dock = DockStyle.Fill, View = View.Details, CheckBoxes = true, FullRowSelect = true, GridLines = true, HideSelection = false, MultiSelect = false, ShowItemToolTips = true };
    private readonly TextBox _detail = new() { Name = "TB_PokekeepEventoDetalle", Dock = DockStyle.Bottom, Height = 84, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, TabStop = false };
    private readonly AccessibleButton _all = new() { Name = "B_PokekeepEventoTodos", Text = BulkEditTexts.MarkAll, AutoSize = true };
    private readonly AccessibleButton _none = new() { Name = "B_PokekeepEventoNinguno", Text = BulkEditTexts.UnmarkAll, AutoSize = true };
    private readonly AccessibleButton _apply = new() { Name = "B_PokekeepEventoAplicar", AutoSize = true, MinimumSize = new Size(180, 30), DialogResult = DialogResult.OK };
    private readonly AccessibleButton _cancel = new() { Name = "B_PokekeepEventoCancelar", Text = Textos.Cancelar, AutoSize = true, MinimumSize = new Size(100, 30), DialogResult = DialogResult.Cancel };
    private bool _loading;

    public EventSwapPreviewForm(EventSwapPlan plan, bool otherOperations)
    {
        _plan = plan;
        OtherOperations = otherOperations;
        Name = "PokekeepEventSwapForm";
        Text = BulkEditTexts.EventSwapTitle;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        MinimumSize = new Size(900, 520);
        Size = new Size(1100, 620);
        AcceptButton = _apply;
        CancelButton = _cancel;

        int[] widths = [130, 140, 100, 280, 50, 190, 380];
        for (var c = 0; c < BulkEditTexts.EventSwapColumns.Count; c++) _list.Columns.Add(BulkEditTexts.EventSwapColumns[c], widths[c]);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 44, Padding = new Padding(0, 6, 0, 0) };
        buttons.Controls.Add(_cancel);
        buttons.Controls.Add(_apply);
        buttons.Controls.Add(_none);
        buttons.Controls.Add(_all);
        var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 8, 12, 4) };
        body.Controls.Add(_list);
        body.Controls.Add(_detail);
        body.Controls.Add(_warning);
        body.Controls.Add(_summary);
        Controls.Add(body);
        Controls.Add(buttons);

        Fill();
        _list.ItemChecked += (_, e) => { if (!_loading) { if (e.Item.Tag is EventSwapItem it) it.Include = e.Item.Checked; UpdateButtons(); } };
        _list.ItemCheck += (_, e) => { if (!_loading && _list.Items[e.Index].Tag is EventSwapItem { Status: not EventSwapStatus.Proposed }) e.NewValue = e.CurrentValue; };   // lo que no se puede cambiar no se marca
        _list.SelectedIndexChanged += (_, _) => ShowDetail();
        _all.Click += (_, _) => SetAll(true);
        _none.Click += (_, _) => SetAll(false);
        Resize += (_, _) => FitColumns();
        Shown += (_, _) => { FitColumns(); if (_list.Items.Count > 0 && _list.SelectedItems.Count == 0) _list.Items[0].Selected = true; };
    }

    /// <summary>Hay otras operaciones marcadas en la edición: «Aplicar» tiene sentido aunque no se cambie ningún origen.</summary>
    public bool OtherOperations { get; }
    public EventSwapPlan Plan => _plan;
    public ListView List => _list;
    public Button ApplyButton => _apply;
    public string SummaryText => _summary.Text;
    public string DetailText => _detail.Text;
    public Button AllButton => _all;
    public Button NoneButton => _none;

    private void Fill()
    {
        _loading = true;
        try
        {
            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (var it in _plan.Items)
            {
                var proposed = it.Status == EventSwapStatus.Proposed;
                var row = new ListViewItem(
                [
                    BulkScope.Describe(it.Slot),
                    it.PokemonName,
                    it.CurrentOrigin,
                    proposed ? it.EventTitle : string.Empty,
                    proposed && it.Year is { } y ? y.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty,
                    proposed ? it.LanguageDisplay : string.Empty,
                    it.PreviewStatus,
                ])
                { Tag = it, Checked = proposed && it.Include, ToolTipText = it.Describe() };
                if (!proposed) row.ForeColor = SystemColors.GrayText;
                else if (it.NeedsHome) row.ForeColor = Color.FromArgb(0x7A, 0x4A, 0x00);
                _list.Items.Add(row);
            }
            _list.EndUpdate();
        }
        finally
        {
            _loading = false;
        }
        _summary.Text = _plan.Summary();
        UpdateButtons();
    }

    private void SetAll(bool on)
    {
        _loading = true;
        foreach (ListViewItem i in _list.Items)
            if (i.Tag is EventSwapItem { Status: EventSwapStatus.Proposed } it) { i.Checked = on; it.Include = on; }
        _loading = false;
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        var n = _plan.Accepted.Count;
        _apply.Text = BulkEditTexts.EventSwapApply(n, OtherOperations);
        _apply.Enabled = BulkEditTexts.EventSwapCanApply(n, OtherOperations);
        _all.Enabled = _plan.Proposed.Count > 0;
        _none.Enabled = _plan.Proposed.Count > 0;
    }

    private void ShowDetail()
    {
        if (_list.SelectedItems.Count == 0 || _list.SelectedItems[0].Tag is not EventSwapItem it) { _detail.Text = string.Empty; return; }
        _detail.Text = string.Join(Environment.NewLine, it.DetailLines());
    }

    private void FitColumns()
    {
        if (_list.Columns.Count < 7) return;
        var used = 0;
        for (var i = 0; i < 6; i++) used += _list.Columns[i].Width;
        _list.Columns[6].Width = Math.Max(240, _list.ClientSize.Width - used - SystemInformation.VerticalScrollBarWidth);
    }
}
