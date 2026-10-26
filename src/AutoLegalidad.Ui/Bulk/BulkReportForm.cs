// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Ui.Controls;
using System.Text;
using AutoLegalidad.Contracts;

namespace AutoLegalidad.Ui.Bulk;

/// <summary>
/// Informe final de una operación masiva (diseño §1.11 y §2.3): ventana NO modal, una fila por Pokémon con su estado, filtro por estado, «Copiar» y «Guardar informe…» (TXT o CSV donde elija el
/// usuario: nada se sube a ningún sitio, regla 15; y nunca lleva bytes de la partida, regla 13). En la edición masiva, una columna por operación (✔ cambiado · = ya óptimo · — no aplicable · ✖ fallo)
/// con el motivo al pasar el ratón y en «Ver detalle» (los cambios antes/después).
/// </summary>
public sealed class BulkReportForm : Form
{
    private const string AllStates = "Todos los estados";
    private static readonly string[] OperationOrder = [.. AdjustOperationNames.Order];

    private readonly Label _title = new() { Name = "L_PokekeepInformeTitulo", Dock = DockStyle.Top, Height = 28, Font = new Font(SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont, FontStyle.Bold), Padding = new Padding(8, 6, 8, 0) };
    private readonly Label _totals = new() { Name = "L_PokekeepInformeTotales", Dock = DockStyle.Top, Height = 40, Padding = new Padding(8, 0, 8, 0), AutoEllipsis = true };
    private readonly ComboBox _filter = new() { Name = "CB_PokekeepInformeFiltro", DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
    private readonly ListView _list = new() { Name = "LV_PokekeepInforme", Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true, HideSelection = false, MultiSelect = false, ShowItemToolTips = true };
    private readonly TextBox _detail = new() { Name = "TB_PokekeepInformeDetalle", Dock = DockStyle.Bottom, Height = 56, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
    private readonly AccessibleButton _copy = new() { Name = "B_PokekeepInformeCopiar", Text = "Copiar", AutoSize = true, MinimumSize = new Size(90, 28) };
    private readonly AccessibleButton _save = new() { Name = "B_PokekeepInformeGuardar", Text = "Guardar informe…", AutoSize = true, MinimumSize = new Size(130, 28) };
    private readonly AccessibleButton _close = new() { Name = "B_PokekeepInformeCerrar", Text = Textos.Cerrar, AutoSize = true, MinimumSize = new Size(90, 28) };
    private BulkReport _report = new("Informe", [], false);

    public BulkReportForm()
    {
        Name = "PokekeepBulkReportForm";
        Text = "Informe de Pokekeep";
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        MinimumSize = new Size(820, 480);
        Size = new Size(1360, 640);
        KeyPreview = true;
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 38, Padding = new Padding(8, 4, 8, 0) };
        top.Controls.Add(new Label { Text = "Filtrar por estado:", AutoSize = true, Margin = new Padding(0, 6, 6, 0) });
        top.Controls.Add(_filter);
        top.Controls.Add(_copy);
        top.Controls.Add(_save);
        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8, 6, 8, 0) };
        bottom.Controls.Add(_close);
        var detailTitle = new Label { Text = "Ver detalle (selecciona una fila)", Dock = DockStyle.Bottom, Height = 22, Padding = new Padding(8, 4, 0, 0) };
        Controls.Add(_list);
        Controls.Add(detailTitle);
        Controls.Add(_detail);
        Controls.Add(bottom);
        Controls.Add(top);
        Controls.Add(_totals);
        Controls.Add(_title);
        _detail.Dock = DockStyle.Bottom;
        detailTitle.BringToFront();
        _detail.BringToFront();

        _filter.SelectedIndexChanged += (_, _) => Fill();
        _list.SelectedIndexChanged += (_, _) => ShowDetail();
        _copy.Click += (_, _) => CopyReport();
        _save.Click += (_, _) => SaveReport();
        _close.Click += (_, _) => Close();
        Resize += (_, _) => FitColumns();
    }

    public ListView List => _list;
    public ComboBox Filter => _filter;
    public string DetailText => _detail.Text;
    public string TotalsText => _totals.Text;
    public Button CopyButton => _copy;
    public Button SaveButton => _save;
    public BulkReport Report => _report;

    public void Present(BulkReport report, Form? owner)
    {
        LoadReport(report);
        if (!Visible)
        {
            if (owner is not null) Show(owner); else Show();
        }
        else
            Activate();
    }

    /// <summary>Rellena el informe sin mostrar la ventana (las capturas sin pantalla y las pruebas la usan).</summary>
    public void LoadReport(BulkReport report)
    {
        _report = report;
        _title.Text = report.Title;
        _totals.Text = string.Join("  ·  ", report.Totals.OrderBy(kv => (int)kv.Key).Select(kv => $"{BulkStatusText.Label(kv.Key)}: {kv.Value}"));
        BuildColumns();
        _filter.Items.Clear();
        _filter.Items.Add(AllStates);
        foreach (var s in report.Totals.Keys.OrderBy(k => (int)k)) _filter.Items.Add($"{BulkStatusText.Label(s)} ({report.Totals[s]})");
        _filter.SelectedIndex = 0;
        Fill();
    }

    private void BuildColumns()
    {
        _list.Columns.Clear();
        _list.Columns.Add("N.º", 48);
        _list.Columns.Add("Pokémon", 130);
        _list.Columns.Add("Forma", 130);
        _list.Columns.Add("Estado", 200);
        _list.Columns.Add(_report.IsEdit ? "Caja y casilla" : "Casilla", 120);
        if (!_report.IsEdit) _list.Columns.Add("Origen", 160);
        _list.Columns.Add("Nv.", 44);
        _list.Columns.Add(_report.StatWord, 150);
        _list.Columns.Add("Idioma", 110);
        if (_report.IsEdit)
            foreach (var op in OperationOrder.Where(o => o != AdjustOperationNames.Legalize || _report.Rows.Any(r => r.Operations.Any(x => x.Operation == o))))
                _list.Columns.Add(BulkStatusText.Name(op), 66);
        _list.Columns.Add("Motivo", 420);
    }

    private IEnumerable<BulkReportRow> Filtered()
    {
        if (_filter.SelectedIndex <= 0) return _report.Rows;
        var label = (string)_filter.SelectedItem!;
        var status = _report.Totals.Keys.FirstOrDefault(s => label.StartsWith(BulkStatusText.Label(s) + " (", StringComparison.Ordinal));
        return _report.Rows.Where(r => r.Status == status);
    }

    private void Fill()
    {
        _list.BeginUpdate();
        _list.Items.Clear();
        var opColumns = _report.IsEdit ? _list.Columns.Cast<ColumnHeader>().Select(c => c.Text).ToList() : [];
        foreach (var r in Filtered())
        {
            var cells = new List<string> { r.Number.ToString(), r.Pokemon, r.Form ?? string.Empty, BulkStatusText.Label(r.Status), r.Slot ?? string.Empty };
            if (!_report.IsEdit) cells.Add(r.Origin ?? string.Empty);
            cells.Add(r.Level?.ToString() ?? string.Empty);
            cells.Add(r.Ivs ?? string.Empty);
            cells.Add(r.Language ?? string.Empty);
            if (_report.IsEdit)
                foreach (var op in OperationOrder.Where(o => opColumns.Contains(BulkStatusText.Name(o))))
                    cells.Add(r.Operations.FirstOrDefault(x => x.Operation == op) is { } o ? BulkStatusText.Symbol(o.Status) : string.Empty);
            cells.Add(r.Reason ?? string.Empty);
            var item = new ListViewItem([.. cells]) { Tag = r, ToolTipText = ToolTip(r) };
            if (r.Status is BulkStatus.CannotGenerate or BulkStatus.TimedOut or BulkStatus.EngineError or BulkStatus.FailedFinalCheck or BulkStatus.NoRoom or BulkStatus.SlotTaken or BulkStatus.SlotChanged)
                item.ForeColor = Color.FromArgb(0x8B, 0x1A, 0x1A);
            else if (r.Status is BulkStatus.NeedsHome or BulkStatus.OnlyWithHome)
                item.ForeColor = Color.FromArgb(0x7A, 0x4A, 0x00);
            _list.Items.Add(item);
        }
        _list.EndUpdate();
        FitColumns();
        _detail.Text = string.Empty;
    }

    private static string ToolTip(BulkReportRow r)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{r.Pokemon}: {BulkStatusText.Label(r.Status)}");
        if (r.Reason is { Length: > 0 }) sb.AppendLine(r.Reason);
        foreach (var o in r.Operations)
            sb.AppendLine($"{BulkStatusText.Name(o.Operation)} {BulkStatusText.Symbol(o.Status)} {o.Message}{(o.Reason is { Length: > 0 } why ? $" ({why})" : string.Empty)}");
        return sb.ToString().TrimEnd();
    }

    private void ShowDetail()
    {
        if (_list.SelectedItems.Count == 0 || _list.SelectedItems[0].Tag is not BulkReportRow r) { _detail.Text = string.Empty; return; }
        var sb = new StringBuilder();
        sb.AppendLine(ToolTip(r));
        if (r.Details.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Cambios (antes → después):");
            foreach (var d in r.Details) sb.AppendLine("  " + d);
        }
        _detail.Text = sb.ToString().TrimEnd().Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", Environment.NewLine, StringComparison.Ordinal);
    }

    private void FitColumns()
    {
        if (_list.Columns.Count == 0) return;
        var last = _list.Columns.Count - 1;
        var used = 0;
        for (var i = 0; i < last; i++) used += _list.Columns[i].Width;
        _list.Columns[last].Width = Math.Max(260, _list.ClientSize.Width - used - SystemInformation.VerticalScrollBarWidth);
    }

    private void CopyReport()
    {
        try { Clipboard.SetText(_report.ToText()); }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.ExternalException or System.Threading.ThreadStateException) { /* el portapapeles puede estar ocupado: no es grave */ }
    }

    private void SaveReport()
    {
        using var dialog = new SaveFileDialog { Title = "Guardar informe", Filter = "Texto (*.txt)|*.txt|Hoja de cálculo (*.csv)|*.csv", FileName = "informe-pokekeep", OverwritePrompt = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var csv = dialog.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);
        File.WriteAllText(dialog.FileName, csv ? _report.ToCsv() : _report.ToText(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }
}
