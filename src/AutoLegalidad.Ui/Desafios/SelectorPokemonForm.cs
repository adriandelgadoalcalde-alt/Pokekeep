// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Desafios;
using PKHeX.Core;
using PKHeX.Drawing.PokeSprite;

namespace AutoLegalidad.Ui.Desafios;

/// <summary>
/// «Elegir Pokémon…» del editor de participantes del Pokéathlon: lista el equipo y todas las cajas de la partida con su sprite, nombre, caja y casilla, con búsqueda por nombre (de la especie o del mote). Al elegir
/// devuelve el Pokémon; quien llama rellena los campos con <see cref="ParticipantePokeathlon.Desde"/>. Los Pokémon que no pueden ser participantes (especie fuera de la 1 a la 493) salen atenuados y no se pueden elegir.
/// </summary>
public sealed class SelectorPokemonForm : Form
{
    private readonly record struct Entrada(PKM Pokemon, string Nombre, string Ubicacion, string Casilla, int Orden, bool Valido);

    private readonly bool _es;
    private readonly List<Entrada> _todas = [];
    private readonly TextBox _busqueda = new() { Name = "TB_SelectorBuscar", Dock = DockStyle.Fill };
    private readonly ListView _lista = new() { Name = "LV_SelectorPokemon", Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false, HideSelection = false, GridLines = false };
    private readonly ImageList _imagenes = new() { ImageSize = new Size(68, 56), ColorDepth = ColorDepth.Depth32Bit };
    private readonly Label _cuenta = new() { Name = "L_SelectorCuenta", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 12, 0) };
    private readonly Label _motivo = new() { Name = "L_SelectorMotivo", AutoSize = true, Margin = new Padding(3, 8, 3, 0), ForeColor = SystemColors.GrayText };
    private readonly Button _elegir = new() { Name = "B_SelectorElegir", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, MinimumSize = new Size(90, 0), Enabled = false };
    private readonly Button _cancelar = new() { Name = "B_SelectorCancelar", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, MinimumSize = new Size(90, 0), DialogResult = DialogResult.Cancel };

    /// <summary>El Pokémon elegido (válido solo si el diálogo se cerró con OK).</summary>
    public PKM? Elegido { get; private set; }

    public SelectorPokemonForm(SaveFile sav, bool espanol)
    {
        _es = espanol;
        Name = "FormPokekeepSelectorPokemon";
        Text = TextosDesafiosUi.SelectorTitulo(_es);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        MinimumSize = new Size(560, 420);
        ClientSize = new Size(640, 560);

        var etiqueta = new Label { Name = "L_SelectorBuscar", Text = TextosDesafiosUi.Buscar(_es), AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(6, 8, 6, 0) };
        var cabecera = new TableLayoutPanel { Name = "TLP_SelectorCabecera", Dock = DockStyle.Top, ColumnCount = 2, RowCount = 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(2, 4, 6, 4) };
        cabecera.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        cabecera.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        cabecera.Controls.Add(etiqueta, 0, 0);
        cabecera.Controls.Add(_busqueda, 1, 0);

        _lista.SmallImageList = _imagenes;
        _lista.Columns.Add(TextosDesafiosUi.ColPokemon(_es), 250);
        _lista.Columns.Add(TextosDesafiosUi.ColUbicacion(_es), 110);
        _lista.Columns.Add(TextosDesafiosUi.ColCasilla(_es), 70);
        _lista.Columns.Add(TextosDesafiosUi.ColNivel(_es), 50);
        _lista.Resize += (_, _) => AjustarColumnas();

        _elegir.Text = TextosDesafiosUi.Elegir(_es);
        _cancelar.Text = TextosDesafiosUi.Cancelar(_es);
        _elegir.Click += (_, _) => Confirmar();
        _lista.DoubleClick += (_, _) => Confirmar();
        _lista.SelectedIndexChanged += (_, _) => ActualizarSeleccion();
        _busqueda.TextChanged += (_, _) => Filtrar();
        AcceptButton = _elegir;
        CancelButton = _cancelar;

        var pie = new FlowLayoutPanel { Name = "FLP_SelectorPie", Dock = DockStyle.Bottom, FlowDirection = FlowDirection.LeftToRight, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(6), WrapContents = true };
        pie.Controls.AddRange([_cuenta, _motivo]);
        var botones = new FlowLayoutPanel { Name = "FLP_SelectorBotones", Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(6) };
        botones.Controls.AddRange([_cancelar, _elegir]);

        Controls.Add(_lista);
        Controls.Add(cabecera);
        Controls.Add(botones);
        Controls.Add(pie);

        Cargar(sav);
        Filtrar();
        Shown += (_, _) => AjustarColumnas();
    }

    private void Cargar(SaveFile sav)
    {
        var nombres = GameInfo.Strings.specieslist;
        var orden = 0;
        void Agregar(PKM pk, string ubicacion, string casilla)
        {
            if (pk.Species == 0)
                return;
            var especie = pk.Species < nombres.Length ? nombres[pk.Species] : pk.Species.ToString();
            var texto = pk.IsNicknamed && !string.IsNullOrWhiteSpace(pk.Nickname) ? $"{pk.Nickname} ({especie})" : especie;
            var valido = ParticipantePokeathlon.Desde(pk).EspecieValida;
            _imagenes.Images.Add(pk.Sprite()); // una sola vez: el índice de imagen es el orden de la entrada
            _todas.Add(new Entrada(pk, texto, ubicacion, casilla, orden++, valido));
        }

        if (sav.HasParty)
        {
            for (var i = 0; i < sav.PartyCount; i++)
                Agregar(sav.GetPartySlotAtIndex(i), TextosDesafiosUi.Equipo(_es), (i + 1).ToString());
        }
        var porCaja = Math.Max(1, sav.BoxSlotCount);
        var datos = sav.BoxData;
        for (var i = 0; i < datos.Count; i++)
            Agregar(datos[i], NombreCaja(sav, i / porCaja), (i % porCaja + 1).ToString());
    }

    private string NombreCaja(SaveFile sav, int caja) => sav is IBoxDetailName n ? n.GetBoxName(caja) : $"{(_es ? "Caja" : "Box")} {caja + 1}";

    private void Filtrar()
    {
        var q = _busqueda.Text.Trim();
        _lista.BeginUpdate();
        _lista.Items.Clear();
        var items = new List<ListViewItem>();
        foreach (var e in _todas)
        {
            if (q.Length > 0 && e.Nombre.IndexOf(q, StringComparison.CurrentCultureIgnoreCase) < 0)
                continue;
            var it = new ListViewItem(e.Nombre, e.Orden) { Tag = e, ForeColor = e.Valido ? SystemColors.WindowText : SystemColors.GrayText };
            it.SubItems.Add(e.Ubicacion);
            it.SubItems.Add(e.Casilla);
            it.SubItems.Add(e.Pokemon.CurrentLevel.ToString());
            items.Add(it);
        }
        _lista.Items.AddRange([.. items]);
        _lista.EndUpdate();
        _cuenta.Text = _lista.Items.Count == 0 ? TextosDesafiosUi.NingunPokemon(_es) : TextosDesafiosUi.Cuenta(_es, _lista.Items.Count, _todas.Count);
        ActualizarSeleccion();
    }

    private void AjustarColumnas()
    {
        var resto = _lista.ClientSize.Width - _lista.Columns[1].Width - _lista.Columns[2].Width - _lista.Columns[3].Width;
        _lista.Columns[0].Width = Math.Max(150, resto);
    }

    private Entrada? Seleccionada => _lista.SelectedItems.Count == 1 && _lista.SelectedItems[0].Tag is Entrada e ? e : null;

    private void ActualizarSeleccion()
    {
        var s = Seleccionada;
        _elegir.Enabled = s is { Valido: true };
        _motivo.Text = s is { Valido: false } ? ParticipantePokeathlon.Desde(s.Value.Pokemon).Motivo?.Para(_es) ?? string.Empty : string.Empty;
    }

    private void Confirmar()
    {
        if (Seleccionada is not { Valido: true } e)
            return;
        Elegido = e.Pokemon;
        DialogResult = DialogResult.OK;
        Close();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _imagenes.Dispose();
        base.Dispose(disposing);
    }
}
