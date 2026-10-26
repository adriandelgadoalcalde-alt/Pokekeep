// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Ui.Controls;
using AutoLegalidad.Contracts;
using AutoLegalidad.Ui.Operation;
using PKHeX.Core;

namespace AutoLegalidad.Ui.Bulk;

/// <summary>
/// «Edición masiva» (diseño §2.4): ámbito (todo, cajas concretas, Pokémon marcados, este Pokémon), operaciones combinables con su valor por defecto y una línea de ayuda cada una,
/// recuento en vivo de lo que entra y lo que se omite, Workers y tiempo estimado. PKHeX no tiene selección múltiple de casillas: los Pokémon se marcan aquí.
/// </summary>
public sealed class BulkEditForm : Form
{
    private readonly BulkCoordinator _coordinator;
    private readonly SaveFile _sav;
    private readonly ISlotInfo? _singleSlot;

    private readonly RadioButton _all = new() { Name = "RB_PokekeepTodo", Text = BulkEditTexts.ScopeLabel(BulkScopeKind.All), AutoSize = true, Checked = true };
    private readonly RadioButton _boxes = new() { Name = "RB_PokekeepCajas", Text = BulkEditTexts.ScopeLabel(BulkScopeKind.Boxes), AutoSize = true };
    private readonly RadioButton _marked = new() { Name = "RB_PokekeepMarcados", Text = BulkEditTexts.ScopeLabel(BulkScopeKind.Marked), AutoSize = true };
    private readonly RadioButton _single = new() { Name = "RB_PokekeepEste", Text = BulkEditTexts.ScopeLabel(BulkScopeKind.Single), AutoSize = true };
    private readonly CheckedListBox _boxList = new() { Name = "CLB_PokekeepCajas", CheckOnClick = true, IntegralHeight = false, Height = 170, Width = 220 };
    private readonly AccessibleButton _allBoxes = new() { Name = "B_PokekeepTodasCajas", Text = BulkEditTexts.AllBoxes, AutoSize = true };
    private readonly AccessibleButton _noBoxes = new() { Name = "B_PokekeepNingunaCaja", Text = BulkEditTexts.NoBoxes, AutoSize = true };
    private readonly AccessibleButton _pick = new() { Name = "B_PokekeepElegir", Text = BulkEditTexts.PickPokemon, AutoSize = true };
    private readonly Label _count = new() { Name = "L_PokekeepRecuento", Dock = DockStyle.Fill, AutoSize = false, Font = new Font(SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont, FontStyle.Bold) };
    private readonly Label _zones = new() { Name = "L_PokekeepZonas", Dock = DockStyle.Fill, AutoSize = false };
    private readonly Dictionary<string, CheckBox> _ops = [];
    private readonly CheckBox _preferEvent = new() { Name = "CHK_PokekeepOpPreferEvent", Text = BulkEditTexts.PreferEventLabel, Checked = false, AutoSize = true, Font = new Font(SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont, FontStyle.Bold), Margin = new Padding(0, 2, 0, 0) };
    private readonly NumericUpDown _workers = new() { Name = "NUD_PokekeepEdicionWorkers", Minimum = BulkEditTexts.MinWorkers, Maximum = BulkEditTexts.MaxWorkers, Width = 70 };
    private readonly Label _estimate = new() { Name = "L_PokekeepEdicionTiempo", AutoSize = true };
    private readonly AccessibleButton _apply = new() { Name = "B_PokekeepAplicar", Text = BulkEditTexts.Apply, AutoSize = true, MinimumSize = new Size(130, 30), DialogResult = DialogResult.OK };
    private readonly AccessibleButton _cancel = new() { Name = "B_PokekeepEdicionCancelar", Text = Textos.Cancelar, AutoSize = true, MinimumSize = new Size(100, 30), DialogResult = DialogResult.Cancel };
    private List<ISlotInfo> _markedSlots = [];
    private bool _loading = true;

    public BulkEditForm(BulkCoordinator coordinator, BulkScopeKind initial, ISlotInfo? singleSlot, int? activeBox, int workers)
    {
        _coordinator = coordinator;
        _sav = coordinator.Host.Env.SAV;
        _singleSlot = singleSlot;
        Name = "PokekeepBulkEditForm";
        Text = Textos.EdicionMasiva;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        MinimumSize = new Size(820, 760);
        Size = new Size(900, 880);
        AcceptButton = _apply;
        CancelButton = _cancel;

        for (var b = 0; b < (_sav.HasBox ? _sav.BoxCount : 0); b++)
            _boxList.Items.Add(BulkScope.BoxName(_sav, b), activeBox == b && initial == BulkScopeKind.Boxes);
        _single.Visible = singleSlot is not null;
        // Los radios aun no tienen contenedor: se marca EXACTAMENTE uno (si no, «Todo» y el elegido quedaban marcados a la vez).
        var chosen = initial == BulkScopeKind.Single && singleSlot is not null ? _single : initial == BulkScopeKind.Boxes ? _boxes : initial == BulkScopeKind.Marked ? _marked : _all;
        foreach (var rb in new[] { _all, _boxes, _marked, _single }) rb.Checked = ReferenceEquals(rb, chosen);
        _workers.Value = Math.Clamp(workers, BulkEditTexts.MinWorkers, BulkEditTexts.MaxWorkers);

        // ---- Ámbito ----
        var scope = new GroupBox { Text = BulkEditTexts.ScopeGroup, Dock = DockStyle.Top, Height = 290, Padding = new Padding(10, 6, 10, 6) };
        var scopeLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 5 };
        scopeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        scopeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        scopeLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        scopeLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        scopeLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        scopeLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        scopeLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        scopeLayout.Controls.Add(_all, 0, 0);
        scopeLayout.Controls.Add(_single, 1, 0);
        scopeLayout.Controls.Add(_boxes, 0, 1);
        scopeLayout.Controls.Add(_marked, 1, 1);
        _boxList.Dock = DockStyle.Fill;
        scopeLayout.Controls.Add(_boxList, 0, 2);
        var boxButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        boxButtons.Controls.AddRange([_allBoxes, _noBoxes, _pick]);
        scopeLayout.Controls.Add(boxButtons, 1, 2);
        scopeLayout.Controls.Add(_count, 0, 3);
        scopeLayout.SetColumnSpan(_count, 2);
        _count.Height = 44;
        scopeLayout.Controls.Add(_zones, 0, 4);
        scopeLayout.SetColumnSpan(_zones, 2);
        _zones.Height = 36;
        scope.Controls.Add(scopeLayout);

        // ---- Operaciones ----
        var ops = new GroupBox { Text = BulkEditTexts.OperationsGroup, Dock = DockStyle.Fill, Padding = new Padding(10, 6, 10, 6) };
        var opsLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, AutoScroll = true };
        // F9-0 (ADR 0069): los títulos, valores por defecto y ayudas salen del catálogo único que también lee el móvil.
        foreach (var op in BulkOperationCatalog.All) AddOp(opsLayout, op.Key, op.Label, op.DefaultOn, op.Help);
        ops.Controls.Add(opsLayout);

        // Hito 5d (ADR 0046): la opción que CAMBIA EL ORIGEN va aparte y siempre visible (no entre las operaciones con desplazamiento), para que nadie la pase por alto.
        var eventBox = new GroupBox { Name = "GB_PokekeepVersionEvento", Text = BulkEditTexts.PreferEventGroup, Dock = DockStyle.Top, Height = 112, Padding = new Padding(10, 4, 10, 4) };
        var eventFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        _preferEvent.Margin = new Padding(0, 2, 0, 0);
        eventFlow.Controls.Add(_preferEvent);
        eventFlow.Controls.Add(new Label { Text = BulkEditTexts.PreferEventHelp, AutoSize = true, MaximumSize = new Size(800, 0), Margin = new Padding(22, 0, 0, 0) });
        eventBox.Controls.Add(eventFlow);

        var run = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 38, Padding = new Padding(0, 4, 0, 0) };
        run.Controls.Add(new Label { Text = BulkEditTexts.Workers, AutoSize = true, Margin = new Padding(0, 6, 6, 0) });
        run.Controls.Add(_workers);
        run.Controls.Add(_estimate);
        _estimate.Margin = new Padding(14, 6, 0, 0);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 44, Padding = new Padding(0, 6, 0, 0) };
        buttons.Controls.Add(_cancel);
        buttons.Controls.Add(_apply);
        var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 8, 12, 4) };
        body.Controls.Add(ops);
        body.Controls.Add(eventBox);
        body.Controls.Add(run);
        body.Controls.Add(scope);
        Controls.Add(body);
        Controls.Add(buttons);

        _all.CheckedChanged += (_, _) => Refresh_();
        _boxes.CheckedChanged += (_, _) => Refresh_();
        _marked.CheckedChanged += (_, _) => Refresh_();
        _single.CheckedChanged += (_, _) => Refresh_();
        _boxList.ItemCheck += (_, _) => BeginInvoke(Refresh_);
        _allBoxes.Click += (_, _) => { for (var i = 0; i < _boxList.Items.Count; i++) _boxList.SetItemChecked(i, true); };
        _noBoxes.Click += (_, _) => { for (var i = 0; i < _boxList.Items.Count; i++) _boxList.SetItemChecked(i, false); };
        _pick.Click += (_, _) => PickPokemon();
        _workers.ValueChanged += (_, _) => Refresh_();
        foreach (var cb in _ops.Values) cb.CheckedChanged += (_, _) => Refresh_();
        _preferEvent.CheckedChanged += (_, _) => Refresh_();
        _loading = false;
        Shown += (_, _) => Refresh_();
    }

    private void AddOp(TableLayoutPanel layout, string key, string text, bool isDefault, string help)
    {
        var cb = new CheckBox { Name = "CHK_PokekeepOp" + key, Text = text, Checked = isDefault, AutoSize = true, Font = new Font(SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont, FontStyle.Bold), Margin = new Padding(0, 6, 0, 0) };
        var label = new Label { Text = help, AutoSize = true, MaximumSize = new Size(760, 0), Margin = new Padding(22, 0, 0, 2) };
        _ops[key] = cb;
        layout.Controls.Add(cb);
        layout.Controls.Add(label);
    }

    public AdjustOptions Operations => new()
    {
        LegalizeIfIllegal = Checked(AdjustOperationNames.Legalize),
        Optimize = Checked(AdjustOperationNames.Optimize),
        MaxLevel = Checked(AdjustOperationNames.MaxLevel),
        MaxFriendship = Checked(AdjustOperationNames.MaxFriendship),
        SpanishLanguage = Checked(AdjustOperationNames.SpanishLanguage),
        MaxPP = Checked(AdjustOperationNames.MaxPP),
        HealParty = Checked(AdjustOperationNames.HealParty),
        CurePokerus = Checked(AdjustOperationNames.CurePokerus),
    };

    private bool Checked(string key) => _ops.TryGetValue(key, out var cb) && cb.Checked;

    public int Workers => (int)_workers.Value;
    /// <summary>Hito 5d (ADR 0046): «Preferir la versión de evento».</summary>
    public bool PreferEvent => _preferEvent.Checked;
    public CheckBox PreferEventCheck => _preferEvent;
    public BulkSelection Selection { get; private set; } = new() { Kind = BulkScopeKind.All, Included = [], Skipped = [], UntouchedZones = [] };
    public Button ApplyButton => _apply;
    public string CountText => _count.Text;
    public string ZonesText => _zones.Text;
    public RadioButton AllRadio => _all;
    public RadioButton BoxesRadio => _boxes;
    public RadioButton MarkedRadio => _marked;
    public RadioButton SingleRadio => _single;
    public CheckedListBox BoxList => _boxList;
    public IReadOnlyDictionary<string, CheckBox> OperationChecks => _ops;
    public Button PickButton => _pick;

    /// <summary>SOLO PRUEBAS: fija los Pokémon marcados sin abrir el selector.</summary>
    public void SetMarked(IEnumerable<ISlotInfo> slots)
    {
        _markedSlots = [.. slots];
        _marked.Checked = true;
        Refresh_();
    }

    private BulkSelection Resolve()
    {
        if (_single.Checked && _singleSlot is not null) return BulkScope.ForSingle(_sav, _singleSlot);
        if (_marked.Checked) return BulkScope.ForMarked(_sav, _markedSlots);
        if (_boxes.Checked) return BulkScope.ForBoxes(_sav, _boxList.CheckedIndices.Cast<int>());
        return BulkScope.ForAll(_sav);
    }

    private void Refresh_()
    {
        if (_loading) return;
        _boxList.Enabled = _boxes.Checked || _marked.Checked;
        _allBoxes.Enabled = _noBoxes.Enabled = _boxes.Checked || _marked.Checked;
        _pick.Enabled = _marked.Checked;
        var selection = Resolve();
        Selection = selection;
        _count.Text = selection.Included.Count == 0 && selection.Kind != BulkScopeKind.Marked && selection.Kind != BulkScopeKind.Boxes
            ? BulkEditTexts.NothingToEdit
            : selection.Describe();
        var zones = selection.Kind == BulkScopeKind.All ? selection.UntouchedZones.Where(z => z.Pokemon > 0).ToList() : [];
        _zones.Text = zones.Count > 0 ? BulkEditTexts.ZonesPrefix + string.Join("; ", zones.Select(z => $"{z.Name} ({z.Pokemon}): {z.Reason}")) + "." : string.Empty;
        _estimate.Text = selection.Included.Count == 0 ? string.Empty : BulkEditTexts.Estimate(new BulkEditPlan { Selection = selection, Operations = Operations }.Estimate(Workers));
        _apply.Enabled = selection.Included.Count > 0 && (Operations.Any || PreferEvent);
    }

    private void PickPokemon()
    {
        var boxes = _boxList.CheckedIndices.Count > 0 ? _boxList.CheckedIndices.Cast<int>().ToList() : Enumerable.Range(0, _sav.HasBox ? _sav.BoxCount : 0).ToList();
        var candidates = BulkScope.ForBoxes(_sav, boxes, includeParty: false).Included;
        using var picker = new PokemonPickerForm(candidates, _markedSlots);
        if (picker.ShowDialog(this) == DialogResult.OK)
        {
            _markedSlots = [.. picker.Chosen];
            Refresh_();
        }
    }
}

/// <summary>Selector de Pokémon marcados (rejilla con casillas de verificación) para la edición masiva: PKHeX no tiene selección múltiple de casillas.</summary>
public sealed class PokemonPickerForm : Form
{
    private readonly ListView _list = new() { Name = "LV_PokekeepElegir", Dock = DockStyle.Fill, View = View.Details, CheckBoxes = true, FullRowSelect = true, GridLines = true };
    private readonly AccessibleButton _ok = new() { Name = "B_PokekeepElegirAceptar", Text = BulkEditTexts.Accept, AutoSize = true, MinimumSize = new Size(100, 28), DialogResult = DialogResult.OK };
    private readonly AccessibleButton _cancel = new() { Name = "B_PokekeepElegirCancelar", Text = Textos.Cancelar, AutoSize = true, MinimumSize = new Size(100, 28), DialogResult = DialogResult.Cancel };
    private readonly AccessibleButton _all = new() { Name = "B_PokekeepElegirTodos", Text = BulkEditTexts.MarkAll, AutoSize = true };
    private readonly AccessibleButton _none = new() { Name = "B_PokekeepElegirNinguno", Text = BulkEditTexts.UnmarkAll, AutoSize = true };

    public PokemonPickerForm(IReadOnlyList<BulkSelectedSlot> candidates, IReadOnlyList<ISlotInfo> alreadyMarked)
    {
        Name = "PokekeepPokemonPickerForm";
        Text = BulkEditTexts.PickerTitle;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        MinimizeBox = false;
        MaximizeBox = false;
        Size = new Size(560, 560);
        _list.Columns.Add(BulkEditTexts.PickerSlot, 150);
        _list.Columns.Add(BulkEditTexts.PickerPokemon, 160);
        _list.Columns.Add(BulkEditTexts.PickerLevel, 50);
        var marked = alreadyMarked.Select(s => BulkScope.Describe(s)).ToHashSet();
        _list.BeginUpdate();
        foreach (var c in candidates)
        {
            var label = BulkScope.Describe(c.Slot);
            _list.Items.Add(new ListViewItem([label, PokekeepCoordinator.SpeciesName(c.Pokemon.Species), c.Pokemon.CurrentLevel.ToString()]) { Tag = c.Slot, Checked = marked.Contains(label) });
        }
        _list.EndUpdate();
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8, 6, 8, 0) };
        buttons.Controls.Add(_cancel);
        buttons.Controls.Add(_ok);
        buttons.Controls.Add(_none);
        buttons.Controls.Add(_all);
        Controls.Add(_list);
        Controls.Add(buttons);
        AcceptButton = _ok;
        CancelButton = _cancel;
        _all.Click += (_, _) => { foreach (ListViewItem i in _list.Items) i.Checked = true; };
        _none.Click += (_, _) => { foreach (ListViewItem i in _list.Items) i.Checked = false; };
    }

    public IEnumerable<ISlotInfo> Chosen => _list.CheckedItems.Cast<ListViewItem>().Select(i => (ISlotInfo)i.Tag!);
}
