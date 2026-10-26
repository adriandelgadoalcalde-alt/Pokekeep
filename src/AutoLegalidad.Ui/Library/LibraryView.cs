// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Text.Json;
using AutoLegalidad.Catalog.Library;
using AutoLegalidad.Ui.Operation;
using PKHeX.Core;
using PKHeX.Drawing.PokeSprite;

namespace AutoLegalidad.Ui.Library;

/// <summary>
/// Pestaña «Librería» (R-003, Hito 4): buscador y navegación sobre el catálogo legal de Pokekeep. Búsqueda por nombre en español, nº de Pokédex, evento, juego e idioma;
/// árbol por origen, generación y especie; filtros de forma, generación, origen e idioma; lo que no existe en la partida abierta sale atenuado con su motivo; ficha con
/// sprite, procedencia y rasgos fijos y variables; «Añadir a la caja» con el mismo motor y la misma validación (primera casilla libre, o arrastrando a una casilla),
/// sin sobrescribir nunca y con deshacer. Referencia de UX: la Librería de Terrakeep, con la estética de PKHeX (controles estándar de WinForms).
/// </summary>
public sealed class LibraryView : UserControl
{
    public const string DragFormat = "Pokekeep.LibraryEntry";
    private const int PageSize = 50;
    /// <summary>Índice de la columna «Estado» en los datos de la fila (se muestra en segundo lugar) y sus anchos mínimo y máximo.</summary>
    public const int StateColumn = 2;
    /// <summary>Índice de la columna «Región» en los datos de la fila (Hito 5d).</summary>
    public const int RegionColumn = 4;
    private const int StateMinWidth = 150;
    private const int StateMaxWidth = 420;

    private readonly PokekeepCoordinator _coordinator;
    private readonly LibraryData _data;
    private readonly bool _ownsData;

    private readonly TextBox _search = new() { Name = "TB_LibreriaBuscar", PlaceholderText = "Buscar: nombre, n.º de Pokédex, evento, juego o idioma…", Dock = DockStyle.Fill };
    private readonly ComboBox _gen = new() { Name = "CB_LibreriaGeneracion", DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly ComboBox _kind = new() { Name = "CB_LibreriaOrigen", DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly ComboBox _lang = new() { Name = "CB_LibreriaIdioma", DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly ComboBox _region = new() { Name = "CB_LibreriaRegion", DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly ComboBox _form = new() { Name = "CB_LibreriaForma", DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, Enabled = false };
    private readonly CheckBox _onlyAvailable = new() { Name = "CHK_LibreriaSoloDisponibles", Text = "Solo disponibles", AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly TreeView _tree = new() { Name = "TV_LibreriaArbol", Dock = DockStyle.Fill, HideSelection = false, ShowLines = false, ShowRootLines = true };
    private readonly ListView _list = new() { Name = "LV_Libreria", Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HideSelection = false, MultiSelect = false, ShowItemToolTips = true, GridLines = false };
    private readonly ImageList _images = new() { ImageSize = new Size(40, 30), ColorDepth = ColorDepth.Depth32Bit };
    private readonly Button _prev = new() { Name = "B_LibreriaAnterior", Text = "‹ Anterior", AutoSize = true };
    private readonly Button _next = new() { Name = "B_LibreriaSiguiente", Text = "Siguiente ›", AutoSize = true };
    private readonly Label _pageInfo = new() { Name = "L_LibreriaPagina", AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
    private readonly PictureBox _sprite = new() { Name = "PB_LibreriaSprite", Size = new Size(76, 62), SizeMode = PictureBoxSizeMode.CenterImage, BorderStyle = BorderStyle.FixedSingle };
    private readonly RichTextBox _sheet = new() { Name = "RTB_LibreriaFicha", ReadOnly = true, BorderStyle = BorderStyle.None, BackColor = SystemColors.Control, Dock = DockStyle.Fill, TabStop = false, DetectUrls = false };
    private readonly Button _add = new() { Name = "B_LibreriaAnadir", Text = "Añadir a la caja", AutoSize = true, Enabled = false };
    private readonly Button _export = new() { Name = "B_LibreriaExportarFicha", Text = "Exportar ficha…", AutoSize = true, Enabled = false };
    private readonly Button _popout = new() { Name = "B_LibreriaVentana", Text = Textos.LibreriaVentana, AutoSize = true };
    private readonly Label _addInfo = new() { Name = "L_LibreriaAviso", AutoSize = false, Dock = DockStyle.Fill, ForeColor = SystemColors.GrayText, AutoEllipsis = true };
    private readonly Label _banner = new() { Name = "L_LibreriaSinPartida", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Visible = false };
    private readonly System.Windows.Forms.Timer _debounce = new() { Interval = 250 };
    private readonly ToolTip _tip = new();

    private IReadOnlyList<LibraryRow> _rows = [];
    private LibraryFilter _filter = new();
    private int _page;
    private long _total;
    private int _gameId;
    private int _gcDestination;
    private int _species;
    private bool _loading;
    private object? _env;
    private bool _homeSettingAtLoad;

    /// <summary>Fila arrastrada ahora mismo desde la lista (la recoge el destino de la suelta en la caja).</summary>
    public static LibraryRow? DraggedRow { get; private set; }

    public LibraryView(PokekeepCoordinator coordinator, LibraryData data, bool ownsData = false)
    {
        _coordinator = coordinator;
        _data = data;
        _ownsData = ownsData;
        Name = "LibraryView";
        Dock = DockStyle.Fill;
        MinimumSize = new Size(360, 300);

        BuildLayout();
        FillStaticFilters();
        _list.SmallImageList = _images;

        _search.TextChanged += (_, _) => { _debounce.Stop(); _debounce.Start(); };
        _debounce.Tick += (_, _) => { _debounce.Stop(); ReloadFromStart(); };
        _gen.SelectedIndexChanged += (_, _) => { if (!_loading) ReloadFromStart(); };
        _kind.SelectedIndexChanged += (_, _) => { if (!_loading) ReloadFromStart(); };
        _lang.SelectedIndexChanged += (_, _) => { if (!_loading) ReloadFromStart(); };
        _region.SelectedIndexChanged += (_, _) => { if (!_loading) ReloadFromStart(); };
        _form.SelectedIndexChanged += (_, _) => { if (!_loading) ReloadFromStart(); };
        _onlyAvailable.CheckedChanged += (_, _) => { if (!_loading) ReloadFromStart(); };
        _tree.AfterSelect += (_, e) => OnTreeSelect(e.Node);
        _list.SelectedIndexChanged += (_, _) => ShowSelection();
        _list.ItemDrag += OnItemDrag;
        _list.DoubleClick += async (_, _) => await AddSelectedAsync().ConfigureAwait(true);
        _prev.Click += (_, _) => { if (_page > 0) { _page--; Reload(); } };
        _next.Click += (_, _) => { if ((_page + 1) * PageSize < _total) { _page++; Reload(); } };
        _add.Click += async (_, _) => await AddSelectedAsync().ConfigureAwait(true);
        _export.Click += async (_, _) => await ExportSelectedAsync().ConfigureAwait(true);
        var menu = new ContextMenuStrip { Name = "CMS_Libreria" };
        var menuAdd = menu.Items.Add("Añadir a la caja");
        var menuExport = menu.Items.Add("Exportar ficha…");
        menuAdd.Name = "CMI_LibreriaAnadir";
        menuExport.Name = "CMI_LibreriaExportarFicha";
        menuAdd.Click += async (_, _) => await AddSelectedAsync().ConfigureAwait(true);
        menuExport.Click += async (_, _) => await ExportSelectedAsync().ConfigureAwait(true);
        menu.Opening += (_, e) => { var pos = _list.PointToClient(Cursor.Position); var hit = _list.GetItemAt(pos.X, pos.Y); if (hit is not null) hit.Selected = true; if (SelectedRow is null) e.Cancel = true; menuAdd.Enabled = _add.Enabled; };
        _list.ContextMenuStrip = menu;
        _popout.Click += (_, _) => OpenWindow();
        _coordinator.Completed += (_, _) => UpdateAddButton();
        _coordinator.ProgressChanged += (_, _) => UpdateAddButton();
    }

    // ---- API para pruebas y para el parche de PKHeX ----
    public ListView List => _list;
    public TextBox SearchBox => _search;
    public ComboBox GenerationBox => _gen;
    public ComboBox KindBox => _kind;
    public ComboBox LanguageBox => _lang;
    public ComboBox RegionBox => _region;
    public ComboBox FormBox => _form;
    public CheckBox OnlyAvailableBox => _onlyAvailable;
    public TreeView Tree => _tree;
    public Button AddButton => _add;
    public Button ExportButton => _export;
    /// <summary>En la ventana propia el botón «Abrir en ventana» no tiene sentido.</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool ShowPopoutButton { get => _popout.Visible; set => _popout.Visible = value; }
    public Button NextButton => _next;
    public Button PreviousButton => _prev;
    public string SheetText => _sheet.Text;
    public string AddInfoText => _addInfo.Text;
    public string PageInfoText => _pageInfo.Text;
    public int GameId => _gameId;
    public long Total => _total;
    public IReadOnlyList<LibraryRow> Rows => _rows;
    public LibraryRow? SelectedRow => _list.SelectedIndices.Count == 1 && _list.SelectedIndices[0] < _rows.Count ? _rows[_list.SelectedIndices[0]] : null;

    /// <summary>Avisa de que se abrió, cambió o cerró una partida: recarga la Librería para el nuevo destino.</summary>
    public void NotifySaveChanged()
    {
        _env = null;
        RefreshTarget();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (!Visible)
            return;
        RefreshTarget();
        // El ajuste «sin matrícula» cambia lo que se ofrece: si se tocó desde la última carga, se vuelve a pintar la lista.
        if (_gameId != 0 && _data.IsAvailable && _coordinator.Settings.AllowHomeTrackerless != _homeSettingAtLoad)
            Reload();
    }

    private void RefreshTarget()
    {
        var env = _coordinator.Host.Env;
        if (ReferenceEquals(env, _env) && _gameId != 0)
            return;
        _env = env;
        var sav = env.SAV;
        _gameId = LibraryData.GameIdOf(sav);
        _gcDestination = LibraryData.GcDestinationOf(sav);
        if (!_data.IsAvailable)
        {
            ShowBanner(Textos.CatalogoNoEncontrado);
            return;
        }
        if (_gameId == 0)
        {
            ShowBanner(Textos.SinPartida + " La Librería muestra lo que existe en la partida abierta.");
            return;
        }
        _banner.Visible = false;
        _form.Items.Clear();
        _form.Items.Add("Cualquier forma");
        _form.SelectedIndex = 0;
        _form.Enabled = false;
        _species = 0;
        ReloadFromStart();
    }

    private void ShowBanner(string text)
    {
        _banner.Text = text;
        _banner.Visible = true;
        _rows = [];
        _list.Items.Clear();
        _total = 0;
        UpdatePager();
        UpdateAddButton();
    }

    // ============================================================================================ Diseño
    private void BuildLayout()
    {
        var filters = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3, RowCount = 3, Padding = new Padding(0, 0, 0, 2) };
        for (var i = 0; i < 3; i++) filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        filters.Controls.Add(_search, 0, 0);
        filters.SetColumnSpan(_search, 2);
        filters.Controls.Add(_popout, 2, 0);
        filters.Controls.Add(_gen, 0, 1);
        filters.Controls.Add(_kind, 1, 1);
        filters.Controls.Add(_lang, 2, 1);
        filters.Controls.Add(_form, 0, 2);
        filters.Controls.Add(_region, 1, 2);
        filters.Controls.Add(_onlyAvailable, 2, 2);
        _popout.Anchor = AnchorStyles.Right;

        _list.Columns.Add("Pokémon", 120);
        _list.Columns.Add("Origen", 100);
        _list.Columns.Add("Estado", StateMinWidth);
        _list.Columns.Add("Procedencia", 160);
        _list.Columns.Add("Región", 150);
        // El estado se ve en segundo lugar (en la pestaña la lista es estrecha y era lo que quedaba cortado); el orden de los datos de cada fila no cambia.
        _list.Columns[StateColumn].DisplayIndex = 1;
        _list.Columns[RegionColumn].DisplayIndex = 3;      // Hito 5d: la región se ve justo después del origen, antes de la procedencia (que es larga)

        var split = new SplitContainer { Size = new Size(440, 200), Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel1, Panel1MinSize = 80, Panel2MinSize = 160, SplitterDistance = 118 };
        split.Panel1.Controls.Add(_tree);
        split.Panel2.Controls.Add(_list);

        var pager = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 28, ColumnCount = 3, RowCount = 1 };
        pager.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pager.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pager.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pager.Controls.Add(_prev, 0, 0);
        pager.Controls.Add(_pageInfo, 1, 0);
        pager.Controls.Add(_next, 2, 0);

        var sheet = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 136, ColumnCount = 3, RowCount = 2, Padding = new Padding(0, 2, 0, 0) };
        sheet.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 84));
        sheet.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        sheet.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        sheet.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        sheet.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        sheet.Controls.Add(_sprite, 0, 0);
        sheet.Controls.Add(_sheet, 1, 0);
        sheet.SetColumnSpan(_sheet, 2);
        sheet.Controls.Add(_addInfo, 0, 1);
        sheet.SetColumnSpan(_addInfo, 2);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Anchor = AnchorStyles.Right };
        buttons.Controls.Add(_export);
        buttons.Controls.Add(_add);
        sheet.Controls.Add(buttons, 2, 1);

        Controls.Add(split);
        Controls.Add(pager);
        Controls.Add(sheet);
        Controls.Add(filters);
        Controls.Add(_banner);
        _banner.BringToFront();
        _tip.SetToolTip(_search, "Escribe un nombre (también sin tildes), un número de Pokédex, el título de un evento, un juego o un idioma.");
        _tip.SetToolTip(_onlyAvailable, "Oculta lo que no existe en la partida abierta. Desmarcado, aparece atenuado con su motivo.");
    }

    private void FillStaticFilters()
    {
        _loading = true;
        _gen.Items.Add("Cualquier generación");
        for (var g = 1; g <= 9; g++) _gen.Items.Add(LibraryNames.Generation(g));
        _gen.SelectedIndex = 0;
        _kind.Items.Add("Cualquier origen");
        foreach (var (_, label) in LibraryNames.Kinds) _kind.Items.Add(label);
        _kind.SelectedIndex = 0;
        _lang.Items.Add("Cualquier idioma");
        foreach (var l in _data.EventLanguages()) _lang.Items.Add(LibraryNames.Language(l));
        _lang.SelectedIndex = 0;
        _form.Items.Add("Cualquier forma");
        _form.SelectedIndex = 0;
        _region.Items.Add("Cualquier región");
        foreach (var (_, label) in LibraryNames.Regions) _region.Items.Add(label);
        _region.SelectedIndex = 0;

        var all = _tree.Nodes.Add("Todo");
        all.Tag = "all";
        var origin = _tree.Nodes.Add("Por origen");
        foreach (var (kind, label) in LibraryNames.Kinds) origin.Nodes.Add(new TreeNode(label) { Tag = "kind:" + kind });
        var gens = _tree.Nodes.Add("Por generación");
        for (var g = 1; g <= 9; g++) gens.Nodes.Add(new TreeNode(LibraryNames.Generation(g)) { Tag = "gen:" + g });
        var species = _tree.Nodes.Add("Por especie");
        foreach (var (first, last, label) in LibraryNames.DexRanges)
        {
            var group = new TreeNode(label) { Tag = "range" };
            for (var s = first; s <= last; s++)
                group.Nodes.Add(new TreeNode($"{s:000} {LibraryNames.SpeciesName(s)}") { Tag = "sp:" + s });
            species.Nodes.Add(group);
        }
        all.Expand();
        _loading = false;
    }

    // ============================================================================================ Consulta
    private void OnTreeSelect(TreeNode? node)
    {
        if (_loading || node?.Tag is not string tag) return;
        _loading = true;
        _species = 0;
        _gen.SelectedIndex = 0;
        _kind.SelectedIndex = 0;
        _form.Items.Clear();
        _form.Items.Add("Cualquier forma");
        _form.SelectedIndex = 0;
        _form.Enabled = false;
        if (tag.StartsWith("kind:", StringComparison.Ordinal))
        {
            var idx = Array.FindIndex(LibraryNames.Kinds, k => k.Kind == tag[5..]);
            if (idx >= 0) _kind.SelectedIndex = idx + 1;
        }
        else if (tag.StartsWith("gen:", StringComparison.Ordinal) && int.TryParse(tag[4..], out var g))
        {
            _gen.SelectedIndex = g;
        }
        else if (tag.StartsWith("sp:", StringComparison.Ordinal) && int.TryParse(tag[3..], out var sp))
        {
            _species = sp;
            if (_gameId != 0)
            {
                foreach (var (form, name) in _data.FormsOf(_gameId, sp))
                    _form.Items.Add($"{form}: {name}");
                _form.Enabled = _form.Items.Count > 2;
            }
        }
        _loading = false;
        ReloadFromStart();
    }

    private void ReloadFromStart()
    {
        _page = 0;
        Reload();
    }

    public LibraryFilter BuildFilter()
    {
        var text = _search.Text.Trim();
        return new LibraryFilter
        {
            GameId = _gameId,
            GcDestination = _gcDestination,
            Text = text.Length == 0 ? null : text,
            Kind = _kind.SelectedIndex > 0 ? LibraryNames.Kinds[_kind.SelectedIndex - 1].Kind : null,
            Generation = _gen.SelectedIndex > 0 ? _gen.SelectedIndex : null,
            Species = _species > 0 ? _species : null,
            Form = _species > 0 && _form.SelectedIndex > 0 && int.TryParse(_form.SelectedItem?.ToString()?.Split(':')[0], out var f) ? f : null,
            EventLanguage = _lang.SelectedIndex > 0 ? _data.EventLanguages()[_lang.SelectedIndex - 1] : null,
            EventRegion = _region.SelectedIndex > 0 ? LibraryNames.Regions[_region.SelectedIndex - 1].Code : null,
            OnlyAvailable = _onlyAvailable.Checked,
            Limit = PageSize,
            Offset = _page * PageSize,
        };
    }

    private void Reload()
    {
        if (_gameId == 0 || !_data.IsAvailable) return;
        _filter = BuildFilter();
        _homeSettingAtLoad = _coordinator.Settings.AllowHomeTrackerless;
        _rows = _data.Query(_filter);
        _total = _data.Count(_filter with { Limit = 1, Offset = 0 });
        _list.BeginUpdate();
        _list.Items.Clear();
        _images.Images.Clear();
        var stateWidth = 0;
        foreach (var r in _rows)
        {
            _images.Images.Add(SpriteFor(r, thumb: true));
            var provenance = LibraryNames.Provenance(r);
            var home = LibraryData.HomeBlockedReason(r, _coordinator.Settings.AllowHomeTrackerless);
            var state = LibraryNames.State(r, home);
            // La ayuda emergente de la fila lleva SIEMPRE el estado completo (la columna puede no tener ancho para un motivo largo), el origen y después el motivo o la procedencia.
            var detail = !r.Available ? null : home ?? (r.HomeRequired ? Textos.NecesitaHomeConAjuste : provenance);
            var origin = LibraryNames.Origin(r);
            var item = new ListViewItem(LibraryNames.SpeciesName(r.SpeciesId) + (string.IsNullOrEmpty(r.FormName) || r.FormId == 0 ? string.Empty : $" ({r.FormName})"), _images.Images.Count - 1)
            {
                Tag = r,
                ToolTipText = $"{state}{Environment.NewLine}Origen: {origin}" + (detail is null || detail == state ? string.Empty : $"{Environment.NewLine}{detail}"),
            };
            item.SubItems.Add(LibraryNames.Origin(r));
            item.SubItems.Add(state);
            item.SubItems.Add(provenance);
            item.SubItems.Add(LibraryNames.Region(r.EventRegion));
            stateWidth = Math.Max(stateWidth, TextRenderer.MeasureText(state, _list.Font).Width);
            if (!r.Available || !r.Addable || home is not null)
            {
                item.ForeColor = SystemColors.GrayText;
                item.Font = new Font(_list.Font, FontStyle.Italic);
            }
            _list.Items.Add(item);
        }
        // Hito 4b: la columna «Estado» (que se ve justo después del nombre) toma el ancho de su texto más largo de la página, con un tope; si un motivo no cabe, la ayuda emergente lo da entero.
        _list.Columns[StateColumn].Width = Math.Clamp(stateWidth + 16, StateMinWidth, StateMaxWidth);
        _list.EndUpdate();
        UpdatePager();
        if (_list.Items.Count > 0)
            _list.Items[0].Selected = true;
        else
            ShowSelection();
    }

    private void UpdatePager()
    {
        var pages = Math.Max(1, (int)Math.Ceiling(_total / (double)PageSize));
        _pageInfo.Text = _total == 0 ? "Sin resultados" : $"Página {_page + 1} de {pages} · {_total:N0} entradas";
        _prev.Enabled = _page > 0;
        _next.Enabled = (_page + 1) * PageSize < _total;
    }

    // ============================================================================================ Ficha
    private void ShowSelection()
    {
        var row = SelectedRow;
        _sheet.Clear();
        _sprite.Image = null;
        if (row is null)
        {
            UpdateAddButton();
            return;
        }
        _sprite.Image = SpriteFor(row, thumb: false);
        Bold($"{LibraryNames.SpeciesName(row.SpeciesId)}" + (row.FormId != 0 && !string.IsNullOrEmpty(row.FormName) ? $" ({row.FormName})" : string.Empty) + $" · n.º {row.DexNumber}");
        Line($"{row.GameName}: {(row.Available ? (LibraryData.HomeBlockedReason(row, _coordinator.Settings.AllowHomeTrackerless) is not null ? "solo con el ajuste de HOME (ahora desactivado)" : "disponible") : row.DimReason ?? "no disponible")}");
        Line("Origen: " + LibraryNames.Origin(row));
        if (row.HomeRequired) Line(LibraryData.HomeBlockedReason(row, _coordinator.Settings.AllowHomeTrackerless) ?? Textos.NecesitaHomeConAjuste);
        try
        {
            if (_data.Detail(row.EntryKey) is { } json)
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                // 0.6.5 (ADR 0052): quien figura como entrenador original (tambien en la cria, que no tiene encuentro propio en el catalogo).
                if (row.Available && row.Kind is not ("Absent" or "FormNormalization"))
                {
                    var hasEnc = root.TryGetProperty("encounter", out var enc0) && enc0.ValueKind == JsonValueKind.Object;
                    var type = hasEnc && enc0.TryGetProperty("type", out var ty) ? ty.GetString() : null;
                    var traitsJson = hasEnc && enc0.TryGetProperty("fixedTraits", out var ft) ? ft.GetRawText() : null;
                    var rank = row.HomeRequired ? 5 : OwnerRanks.OfKind(row.Kind, OwnerRanks.IsOwnerFixed(row.Kind, type, traitsJson, row.EncounterKey));
                    Line(LibraryNames.OwnerLine(rank));
                }
                if (root.TryGetProperty("encounter", out var enc) && enc.ValueKind == JsonValueKind.Object)
                {
                    if (row.Kind == "Event")
                    {
                        Line("Evento: " + LibraryNames.EventTitle(row) + (LibraryNames.EventTitleSourceText(row) is { } src ? $" ({src})" : " (sin título en español de fuente fiable: se muestran el Pokémon y el juego)"));
                        if (LibraryNames.OriginalEventTitle(row) is { } original)
                            Line("Título original" + (string.IsNullOrWhiteSpace(row.EventLanguage) ? string.Empty : " (" + LibraryNames.Language(row.EventLanguage!) + ")") + ": " + original);
                        var parts = new List<string>();
                        if (!string.IsNullOrWhiteSpace(row.EventLanguage)) parts.Add("idioma " + LibraryNames.Language(row.EventLanguage!));
                        if (!string.IsNullOrWhiteSpace(row.EventStartDate)) parts.Add("desde " + row.EventStartDate);
                        if (enc.TryGetProperty("restrictLanguage", out var rl) && !string.IsNullOrEmpty(rl.GetString())) parts.Add("solo para " + LibraryNames.Language(rl.GetString()!));
                        if (parts.Count > 0) Line(string.Join(" · ", parts));
                        // Hito 5d (ADR 0047): la región solo se enseña si el Core o la documentación de la tarjeta la aportan; si no, se dice que no hay dato (nunca se deduce del idioma).
                        Line(string.IsNullOrWhiteSpace(row.EventRegion)
                            ? "Región: sin dato (ni PKHeX ni la documentación de la tarjeta la indican)"
                            : $"Región: {LibraryNames.Region(row.EventRegion)} (fuente: {LibraryNames.RegionSource(row.EventRegionSource)})");
                    }
                    if (enc.TryGetProperty("levelMin", out var lmin) && enc.TryGetProperty("levelMax", out var lmax))
                        Line(lmin.GetInt32() == lmax.GetInt32() ? $"Nivel {lmin.GetInt32()}" : $"Niveles {lmin.GetInt32()}–{lmax.GetInt32()}");
                }
                if (root.TryGetProperty("provenance", out var prov) && prov.ValueKind == JsonValueKind.Object)
                {
                    var rev = prov.TryGetProperty("revision", out var rv) ? rv.GetString() : null;
                    var sha = prov.TryGetProperty("artifact_sha256", out var sh) ? sh.GetString() : null;
                    Line($"Fuente: {LibraryNames.Source(row.SourceLabel)}" + (rev is { Length: >= 7 } ? $" (commit {rev[..7]})" : string.Empty) + (sha is { Length: >= 12 } ? $" · SHA-256 {sha[..12]}…" : string.Empty));
                }
                else
                    Line("Fuente: " + LibraryNames.Source(row.SourceLabel));
                if (root.TryGetProperty("traits", out var tr))
                {
                    if (tr.TryGetProperty("fixed", out var fx)) Line("Rasgos fijos: " + LibraryData.TraitList(fx));
                    if (tr.TryGetProperty("variable", out var vr)) Line("Rasgos variables: " + LibraryData.TraitList(vr));
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or Microsoft.Data.Sqlite.SqliteException)
        {
            Line("(No se pudo leer la ficha: " + ex.Message + ")");
        }
        _sheet.SelectionStart = 0;
        _sheet.ScrollToCaret();
        UpdateAddButton();
    }

    private void Bold(string text)
    {
        _sheet.SelectionFont = new Font(_sheet.Font, FontStyle.Bold);
        _sheet.AppendText(text + Environment.NewLine);
        _sheet.SelectionFont = _sheet.Font;
    }

    private void Line(string text) => _sheet.AppendText(text + Environment.NewLine);

    /// <summary>
    /// Sprite de una entrada: el de PKHeX para la especie, la forma y el contexto del juego, con lo que cambia el aspecto de lo que SE VA A GENERAR: variocolor fijo de la tarjeta (estrella o cuadrado),
    /// sexo fijo cuando el sprite cambia con el sexo (Hippopotas, Hippowdon, Unfezant, Frillish, Jellicent, Pyroar) y el distintivo Gigamax de las tarjetas que lo reparten.
    /// </summary>
    public static Bitmap SpriteFor(LibraryRow r, bool thumb)
    {
        try
        {
            var context = ((GameVersion)r.GameId).Context;
            var shiny = r.ShinyRule switch { "Always" => Shiny.Always, "AlwaysStar" => Shiny.AlwaysStar, "AlwaysSquare" => Shiny.AlwaysSquare, _ => Shiny.Never };
            var gender = (byte)(r.FixedGender == 1 ? 1 : 0);
            // El sprite de PKHeX del modo activo, o el de otro modo si ese no lo trae (la Librería lista también lo que no existe en el juego abierto); el de reserva de PKHeX solo si no hay otro.
            var img = LibrarySprites.Build((ushort)r.SpeciesId, (byte)r.FormId, gender, context, shiny)
                      ?? SpriteUtil.GetSprite((ushort)r.SpeciesId, (byte)r.FormId, gender, 0, 0, false, shiny, context);
            return r.Gigantamax ? WithGigantamax(img) : img;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new Bitmap(thumb ? 40 : 68, thumb ? 30 : 56);
        }
    }

    private static Bitmap WithGigantamax(Bitmap sprite)
    {
        var result = new Bitmap(sprite);
        using var g = Graphics.FromImage(result);
        var dyna = PKHeX.Drawing.PokeSprite.Properties.Resources.dyna;
        g.DrawImage(dyna, (result.Width - dyna.Width) / 2, 0);
        return result;
    }

    // ============================================================================================ Añadir
    /// <summary>Estado del botón «Añadir a la caja» y motivo si no se puede.</summary>
    public (bool Enabled, string Message) AddState()
    {
        var row = SelectedRow;
        if (row is null) return (false, "Elige una entrada de la lista.");
        if (LibraryData.HomeBlockedReason(row, _coordinator.Settings.AllowHomeTrackerless) is { } home) return (false, home);
        var av = _coordinator.GetLibraryAvailability(row);
        if (!av.Enabled) return (false, av.Reason ?? string.Empty);
        var sav = _coordinator.Host.Env.SAV;
        var free = PokekeepCoordinator.FindFreeSlot(sav, _coordinator.Host.CurrentBox);
        return free is null ? (false, Textos.NoHayHueco) : (true, $"Se colocará en la primera casilla libre de la caja {_coordinator.Host.CurrentBox + 1}, o suéltalo en una casilla vacía." + (row.HomeRequired ? " " + Textos.NecesitaHomeConAjuste : string.Empty));
    }

    private void UpdateAddButton()
    {
        _export.Enabled = SelectedRow is not null && _data.IsAvailable;
        var (enabled, message) = AddState();
        _add.Enabled = enabled;
        _addInfo.Text = message;
        _tip.SetToolTip(_addInfo, message);
    }

    /// <summary>
    /// «Exportar ficha…» (Hito 5d, ADR 0047): pregunta dónde guardar (JSON o texto legible) y escribe la ficha de la entrada elegida. Solo datos del catálogo y versiones; nunca nada del usuario ni rutas.
    /// </summary>
    private async Task ExportSelectedAsync()
    {
        var row = SelectedRow;
        if (row is null) return;
        var species = LibraryNames.SpeciesName(row.SpeciesId);
        using var dialog = new SaveFileDialog
        {
            Title = "Exportar ficha de la Librería",
            Filter = "JSON (*.json)|*.json|Texto legible (*.txt)|*.txt",
            FileName = SuggestedSheetName(row, species),
            OverwritePrompt = true,
        };
        if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;
        try
        {
            await ExportSheetAsync(row, dialog.FileName).ConfigureAwait(true);
            _addInfo.Text = "Ficha guardada.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Registro.RegistroFallos.Error("No se pudo guardar la ficha de la Librería.", ex, "LibraryView.ExportSelected");
            MessageBox.Show(FindForm(), "No se pudo guardar la ficha: " + ex.Message, "Exportar ficha", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    /// <summary>Nombre de fichero sugerido: especie, juego y un trozo de la clave (sin tildes ni caracteres que Windows no admite).</summary>
    public static string SuggestedSheetName(LibraryRow row, string speciesName)
    {
        static string Clean(string s)
        {
            var normalized = s.Normalize(System.Text.NormalizationForm.FormD);
            var sb = new System.Text.StringBuilder();
            foreach (var ch in normalized)
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark && (char.IsLetterOrDigit(ch) || ch is '-' or '_')) sb.Append(ch);
                else if (ch == ' ') sb.Append('-');
            return sb.ToString();
        }
        var key = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(row.EntryKey)))[..8].ToLowerInvariant();
        return $"ficha-{Clean(speciesName)}-{Clean(row.GameName)}-{key}";
    }

    /// <summary>
    /// Escribe la ficha de <paramref name="row"/> en <paramref name="path"/>: texto legible si la extensión es <c>.txt</c>, JSON en cualquier otro caso. Pide al motor sus versiones (sin tocar la partida); si no responde, la ficha lo dice.
    /// La ruta del fichero no se escribe dentro de la ficha.
    /// </summary>
    public async Task ExportSheetAsync(LibraryRow row, string path)
    {
        var manifest = await _coordinator.TryGetManifestAsync().ConfigureAwait(true);
        var engine = manifest is null ? null : new Catalog.Library.SheetEngineVersions(manifest.EngineVersion, manifest.AdapterVersion, manifest.AlmVersion, manifest.AlmCommit, manifest.CoreVersion, manifest.CoreCommit, manifest.CoreDllSha256);
        var sheet = _data.Sheet(row, engine) ?? throw new InvalidOperationException(Textos.CatalogoNoEncontrado);
        var text = path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ? LibrarySheet.ToText(sheet) : LibrarySheet.ToJson(sheet) + Environment.NewLine;
        await File.WriteAllTextAsync(path, text, new System.Text.UTF8Encoding(false)).ConfigureAwait(true);
    }

    private async Task AddSelectedAsync()
    {
        var row = SelectedRow;
        if (row is null) return;
        var (enabled, _) = AddState();
        if (!enabled) return;
        await _coordinator.AddLibraryEntryAsync(row).ConfigureAwait(true);
        UpdateAddButton();
    }

    private void OnItemDrag(object? sender, ItemDragEventArgs e)
    {
        if (e.Item is not ListViewItem { Tag: LibraryRow row }) return;
        // Arrastrable: disponible y añadible en el destino (no bloqueado por HOME) y sin otra operación en curso. La casilla de destino se comprueba al soltar.
        if (!row.Available || !row.Addable || _coordinator.IsBusy || LibraryData.HomeBlockedReason(row, _coordinator.Settings.AllowHomeTrackerless) is not null) return;
        DraggedRow = row;
        try
        {
            _list.DoDragDrop(new DataObject(DragFormat, row.EntryKey), DragDropEffects.Copy);
        }
        finally
        {
            DraggedRow = null;
        }
    }

    private void OpenWindow()
    {
        var window = new LibraryWindow(_coordinator, _data);
        window.Show(FindForm());
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _debounce.Dispose();
            _tip.Dispose();
            _images.Dispose();
            if (_ownsData) _data.Dispose();
        }
        base.Dispose(disposing);
    }
}

/// <summary>La Librería en su propia ventana (misma vista, mismo coordinador): útil para tenerla junto a las cajas y arrastrar entradas a una casilla.</summary>
public sealed class LibraryWindow : Form
{
    private readonly LibraryView _view;

    public LibraryWindow(PokekeepCoordinator coordinator, LibraryData data)
    {
        Name = "PokekeepLibraryWindow";
        Text = "Librería de Pokekeep";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(760, 640);
        MinimumSize = new Size(520, 420);
        ShowInTaskbar = true;
        _view = new LibraryView(coordinator, data) { Dock = DockStyle.Fill, Padding = new Padding(6), ShowPopoutButton = false };
        Controls.Add(_view);
        Shown += (_, _) => _view.NotifySaveChanged();
    }

    public LibraryView View => _view;
}
