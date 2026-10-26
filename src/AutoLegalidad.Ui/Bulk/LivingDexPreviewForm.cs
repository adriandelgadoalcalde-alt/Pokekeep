// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Ui.Controls;
using AutoLegalidad.Ui.Operation;
using PKHeX.Core;

namespace AutoLegalidad.Ui.Bulk;

/// <summary>
/// «Completar el juego»: vista previa ANTES de generar nada (diseño §1.7). Calculada sin llamar al motor ni tocar la partida: cuántos Pokémon se generarían, cuáles no se pueden y por qué,
/// cuántas casillas hacen falta y cuántas hay libres, el tiempo estimado y las opciones (formas, orden, caja de inicio, omitir lo que ya tengo, Workers).
/// </summary>
public sealed class LivingDexPreviewForm : Form
{
    private readonly BulkCoordinator _coordinator;
    private readonly IReadOnlySet<(int Species, int Form)>? _owned;
    private readonly SaveFile _sav;

    private readonly Label _game = new() { Name = "L_PokekeepJuego", AutoSize = true, Dock = DockStyle.Top, Font = new Font(SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(0, 4, 0, 4) };
    private readonly ComboBox _forms = new() { Name = "CB_PokekeepFormas", DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly ComboBox _order = new() { Name = "CB_PokekeepOrden", DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly NumericUpDown _startBox = new() { Name = "NUD_PokekeepCajaInicio", Minimum = 1, Maximum = 1, Width = 70 };
    private readonly CheckBox _skipOwned = new() { Name = "CHK_PokekeepOmitir", Text = LivingDexPreview.SkipOwnedLabel, AutoSize = true, Checked = true };
    private readonly CheckBox _preferEvent = new() { Name = "CHK_PokekeepPreferirEvento", Text = LivingDexPreview.PreferEventLabel, AutoSize = true };
    private readonly NumericUpDown _workers = new() { Name = "NUD_PokekeepWorkers", Minimum = 1, Maximum = 8, Width = 70 };
    private Label _listTitle = null!;
    private readonly CheckBox _home = new() { Name = "CHK_PokekeepActivarHome", AutoSize = true, Visible = false, Margin = new Padding(0, 4, 0, 0) };
    private readonly bool _homeAtStart;
    private readonly Label _summary = new() { Name = "L_PokekeepResumen", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 2, 0, 2) };
    private readonly Label _capacity = new() { Name = "L_PokekeepCapacidad", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 2, 0, 2), Font = new Font(SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont, FontStyle.Bold) };
    private readonly Label _estimate = new() { Name = "L_PokekeepTiempo", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 2, 0, 2) };
    private readonly ListView _impossible = new() { Name = "LV_PokekeepNoSePuede", View = View.Details, FullRowSelect = true, GridLines = true, Dock = DockStyle.Fill, HideSelection = false, MultiSelect = false, ShowItemToolTips = true };
    private readonly TextBox _detail = new() { Name = "TB_PokekeepMotivoCompleto", Multiline = true, ReadOnly = true, WordWrap = true, ScrollBars = ScrollBars.None, Dock = DockStyle.Bottom, Height = 60, TabStop = false };
    private readonly AccessibleButton _go = new() { Name = "B_PokekeepGenerar", AutoSize = true, MinimumSize = new Size(150, 30), DialogResult = DialogResult.OK };
    private readonly AccessibleButton _cancel = new() { Name = "B_PokekeepCompletarCancelar", Text = Textos.Cancelar, AutoSize = true, MinimumSize = new Size(100, 30), DialogResult = DialogResult.Cancel };
    private bool _loading;
    private bool _compact;
    private TableLayoutPanel _info = null!;
    private TableLayoutPanel _options = null!;
    private Panel _gameBar = null!;
    private FlowLayoutPanel _buttons = null!;
    private Panel _body = null!;
    private const int FullHeight = 640;
    private const int FullMinHeight = 560;
    private const int MinWidth0 = 760;

    public LivingDexPreviewForm(BulkCoordinator coordinator, IReadOnlySet<(int Species, int Form)>? owned, LivingDexOptions initial, int workers)
    {
        _coordinator = coordinator;
        _owned = owned;
        _sav = coordinator.Host.Env.SAV;
        _homeAtStart = coordinator.Settings.AllowHomeTrackerless;
        _home.Text = LivingDexPreview.HomeLabel;
        new ToolTip { AutoPopDelay = 20000 }.SetToolTip(_home, LivingDexPreview.HomeHelp);
        _home.Checked = _homeAtStart;
        Name = "PokekeepLivingDexForm";
        Text = Textos.CompletarElJuego;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        MinimumSize = new Size(760, 560);
        Size = new Size(820, 640);
        AcceptButton = _go;
        CancelButton = _cancel;

        _forms.Items.AddRange([.. LivingDexPreview.FormLabels]);
        _forms.SelectedIndex = (int)initial.Forms;
        _order.Items.AddRange([.. LivingDexPreview.OrderLabels(_sav)]);
        _order.SelectedIndex = initial.Order == LivingDexOrder.Regional && _order.Items.Count > 1 ? 1 : 0;
        _startBox.Maximum = Math.Max(1, _sav.BoxCount);
        _startBox.Value = Math.Clamp(initial.StartBox + 1, 1, (int)_startBox.Maximum);
        _skipOwned.Checked = initial.SkipOwned;
        _preferEvent.Checked = initial.PreferEvent;
        new ToolTip { AutoPopDelay = 20000 }.SetToolTip(_preferEvent, LivingDexPreview.PreferEventHelp);
        _workers.Value = Math.Clamp(workers, 1, 8);

        _game.Text = LivingDexPreview.GameLine(_sav);
        _impossible.Columns.Add("Pokémon", 150);
        _impossible.Columns.Add("Estado", 190);
        _impossible.Columns.Add("Motivo", 420);

        _options = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 4, AutoSize = true, Padding = new Padding(0, 4, 0, 4) };
        var options = _options;
        options.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        options.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        options.Controls.Add(Lbl(LivingDexPreview.FormsLabel), 0, 0);
        options.Controls.Add(_forms, 1, 0);
        options.Controls.Add(Lbl(LivingDexPreview.OrderLabel), 2, 0);
        options.Controls.Add(_order, 3, 0);
        options.Controls.Add(Lbl(LivingDexPreview.StartBoxLabel), 0, 1);
        var boxFlow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
        boxFlow.Controls.Add(_startBox);
        boxFlow.Controls.Add(_skipOwned);
        options.Controls.Add(boxFlow, 1, 1);
        options.Controls.Add(Lbl(LivingDexPreview.WorkersLabel), 2, 1);
        options.Controls.Add(_workers, 3, 1);
        options.Controls.Add(_preferEvent, 0, 2);
        options.SetColumnSpan(_preferEvent, 4);
        options.Controls.Add(_home, 0, 3);
        options.SetColumnSpan(_home, 4);

        // Hito 5c: el bloque de texto se ajusta a su contenido (filas y etiquetas automáticas, con ajuste de línea al ancho real del diálogo): ni sobra hueco ni se corta nada.
        _info = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, RowCount = 3, ColumnCount = 1, Padding = new Padding(0, 4, 0, 4) };
        var info = _info;
        info.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        info.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        info.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        info.Controls.Add(_summary, 0, 0);
        info.Controls.Add(_capacity, 0, 1);
        info.Controls.Add(_estimate, 0, 2);

        _listTitle = new Label { Text = LivingDexPreview.ListTitleWithRows, Dock = DockStyle.Top, Height = 22, Font = new Font(SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont, FontStyle.Bold), TextAlign = ContentAlignment.BottomLeft };
        _buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 44, Padding = new Padding(0, 6, 0, 0) };
        var buttons = _buttons;
        buttons.Controls.Add(_cancel);
        buttons.Controls.Add(_go);

        _body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 8, 12, 8) };
        var body = _body;
        body.Controls.Add(_impossible);
        body.Controls.Add(_detail);
        body.Controls.Add(_listTitle);
        body.Controls.Add(info);
        body.Controls.Add(options);
        var gameBar = new Panel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        _gameBar = gameBar;
        gameBar.Controls.Add(_game);
        body.Controls.Add(gameBar);
        Controls.Add(body);
        Controls.Add(buttons);

        _impossible.SelectedIndexChanged += (_, _) => ShowDetail();
        _forms.SelectedIndexChanged += (_, _) => Recalculate();
        _order.SelectedIndexChanged += (_, _) => Recalculate();
        _startBox.ValueChanged += (_, _) => Recalculate();
        _skipOwned.CheckedChanged += (_, _) => Recalculate();
        _preferEvent.CheckedChanged += (_, _) => Recalculate();
        _workers.ValueChanged += (_, _) => Recalculate();
        _home.CheckedChanged += (_, _) =>
        {
            if (_loading) return;
            _coordinator.Settings.AllowHomeTrackerless = _home.Checked;   // el plan, las opciones y la generación leen este mismo ajuste
            Recalculate();
        };
        FormClosing += (_, _) =>
        {
            if (DialogResult != DialogResult.OK) _coordinator.Settings.AllowHomeTrackerless = _homeAtStart;   // cancelar no deja activado un ajuste de riesgo
        };
        Shown += (_, _) => Recalculate();
        Resize += (_, _) => { UpdateWrapWidths(); FitColumns(); };
    }

    public LivingDexPlan? Plan { get; private set; }
    public int Workers => (int)_workers.Value;
    public LivingDexOptions Options => new()
    {
        Forms = (LivingDexForms)Math.Max(0, _forms.SelectedIndex),
        Order = _order.SelectedIndex == 1 ? LivingDexOrder.Regional : LivingDexOrder.National,
        StartBox = (int)_startBox.Value - 1,
        SkipOwned = _skipOwned.Checked,
        PreferEvent = _preferEvent.Checked,
        IncludeHome = _coordinator.Settings.AllowHomeTrackerless,
    };
    public Button GoButton => _go;
    public ListView ImpossibleList => _impossible;
    public string SummaryText => _summary.Text;
    public string CapacityText => _capacity.Text;
    public string EstimateText => _estimate.Text;
    public ComboBox FormsCombo => _forms;
    public CheckBox PreferEventCheck => _preferEvent;
    public NumericUpDown StartBoxBox => _startBox;

    private static Label Lbl(string text) => new() { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 8, 0) };

    private void Recalculate()
    {
        if (_loading) return;
        _loading = true;
        try
        {
            var plan = _coordinator.PlanLivingDex(Options, _owned);
            Plan = plan;
            // F9 (9b): los textos de la vista previa son los del núcleo (LivingDexPreview): el móvil enseña los MISMOS.
            var preview = LivingDexPreview.Build(plan, _sav, _coordinator.Settings.AllowHomeTrackerless, Workers);
            _home.Visible = preview.ShowHomeOption;
            _summary.Text = preview.Summary;
            _capacity.Text = preview.Capacity;
            _estimate.Text = preview.Estimate;
            _go.Text = preview.GoText;
            _go.Enabled = preview.GoEnabled;
            _impossible.BeginUpdate();
            _impossible.Items.Clear();
            foreach (var row in preview.Rows)
                _impossible.Items.Add(new ListViewItem([row.Name, row.StatusLabel, row.Shown]) { ToolTipText = row.Reason, Tag = row });
            _impossible.EndUpdate();
            // Hito 5b: sin nada que explicar no se enseña una tabla vacía (ni su recuadro de motivo).
            var anySkip = preview.HasRows;
            _impossible.Visible = anySkip;
            _detail.Visible = anySkip;
            _listTitle.Text = preview.ListTitle;
            if (_impossible.Items.Count > 0 && _impossible.SelectedItems.Count == 0) _impossible.Items[0].Selected = true;   // el recuadro enseña ya el motivo completo de la primera fila
            ShowDetail();
            FitColumns();
            UpdateWrapWidths();
            FitHeight(!anySkip);
        }
        finally
        {
            _loading = false;
        }
    }

    /// <summary>Ancho de ajuste de línea de las etiquetas largas: el real del cuerpo del diálogo (así el texto baja de línea en vez de cortarse).</summary>
    private void UpdateWrapWidths()
    {
        if (_body is null) return;
        var w = Math.Max(200, _body.ClientSize.Width - _body.Padding.Horizontal - 6);
        foreach (var c in new Control[] { _summary, _capacity, _estimate, _game })
            c.MaximumSize = new Size(w, 0);
        _home.MaximumSize = new Size(w, 0);
    }

    /// <summary>
    /// Sin nada excluido no hay tabla: el diálogo se ajusta a su contenido (nada de hueco vacío). Con tabla recupera el tamaño cómodo. Solo se redimensiona al cambiar de estado
    /// (o si lo de dentro ya no cabe), nunca contra una medida que el usuario haya elegido.
    /// </summary>
    private void FitHeight(bool compact)
    {
        var frame = Height - ClientSize.Height;
        if (compact)
        {
            var need = _body.Padding.Vertical + _gameBar.PreferredSize.Height + _options.PreferredSize.Height + _info.PreferredSize.Height + _listTitle.Height + _buttons.Height + 8;
            MinimumSize = new Size(MinWidth0, need + frame);
            if (!_compact || ClientSize.Height != need) Height = need + frame;
        }
        else
        {
            MinimumSize = new Size(MinWidth0, FullMinHeight);
            if (_compact) Height = Math.Max(Height, FullHeight);
        }
        if (compact != _compact) { _compact = compact; if (Visible) CenterToParent(); }
    }

    /// <summary>El motivo COMPLETO de la fila elegida, con ajuste de línea (la columna del motivo se corta).</summary>
    private void ShowDetail()
    {
        if (_impossible.SelectedItems.Count == 0) { _detail.Text = LivingDexPreview.Detail(null); return; }
        var it = _impossible.SelectedItems[0];
        it.EnsureVisible();
        _detail.Text = LivingDexPreview.Detail(it.Tag as LivingDexPreviewRow);
    }

    private void FitColumns()
    {
        if (_impossible.Columns.Count < 3) return;
        var rest = _impossible.ClientSize.Width - _impossible.Columns[0].Width - _impossible.Columns[1].Width - SystemInformation.VerticalScrollBarWidth;
        _impossible.Columns[2].Width = Math.Max(240, rest);
    }
}
