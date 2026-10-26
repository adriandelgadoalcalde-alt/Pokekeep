// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Contracts;
using AutoLegalidad.Ui.Operation;

namespace AutoLegalidad.Ui.Controls;

/// <summary>Ventana NO modal «Ver cambios»: qué se tocó (antes/después/motivo, incluidos los rasgos relajados) y el informe de la validación independiente.</summary>
public sealed class ChangesForm : Form
{
    private readonly Label _title = new() { Dock = DockStyle.Top, Height = 22, Font = new Font(SystemFonts.MessageBoxFont ?? SystemFonts.DefaultFont, FontStyle.Bold), Padding = new Padding(6, 4, 6, 0) };
    private readonly Label _sub = new() { Dock = DockStyle.Top, Height = 20, Padding = new Padding(6, 0, 6, 0) };
    private readonly ListView _list = new() { Name = "LV_PokekeepCambios", Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true, HideSelection = false, MultiSelect = false };
    private readonly TextBox _notes = new() { Name = "TB_PokekeepNotas", Dock = DockStyle.Bottom, Height = 150, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
    private readonly Button _close = new() { Name = "B_PokekeepCambiosCerrar", Text = Textos.Cerrar, Anchor = AnchorStyles.Right | AnchorStyles.Bottom };

    public ChangesForm()
    {
        Name = "PokekeepChangesForm";
        Text = Textos.CambiosTitulo;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        MinimumSize = new Size(560, 360);
        Size = new Size(720, 460);
        _list.ShowItemToolTips = true;
        _list.Columns.Add("Campo", 150);
        _list.Columns.Add("Antes", 140);
        _list.Columns.Add("Después", 140);
        _list.Columns.Add("Motivo", 260);
        Resize += (_, _) => FitColumns();
        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 38 };
        _close.Location = new Point(bottom.Width - _close.Width - 12, 6);
        bottom.Controls.Add(_close);
        bottom.Resize += (_, _) => _close.Location = new Point(bottom.Width - _close.Width - 12, 6);
        Controls.Add(_list);
        Controls.Add(_notes);
        Controls.Add(bottom);
        Controls.Add(_sub);
        Controls.Add(_title);
        _close.Click += (_, _) => Close();     // ventana no modal: se cierra a mano (un DialogResult solo la ocultaría sin cerrarla)
        KeyPreview = true;
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };
    }

    public ListView List => _list;
    public string NotesText => _notes.Text;

    public void Present(OperationResult r, Form? owner)
    {
        _title.Text = r.Headline;
        _sub.Text = r.TargetDescription is { } t ? $"{r.Action.Title()} · {t} · {Textos.Segundos(r.Elapsed)}" : r.Action.Title();
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var c in r.Changes)
        {
            var item = new ListViewItem([c.Field, ChangeFormatter.Describe(c.Field, c.Before), ChangeFormatter.Describe(c.Field, c.After), c.Reason]) { ToolTipText = c.Reason };
            if (c.ReasonCode.EndsWith("_RELAXED", StringComparison.Ordinal))
                item.Font = new Font(_list.Font, FontStyle.Italic);
            _list.Items.Add(item);
        }
        if (r.Changes.Count == 0)
            _list.Items.Add(new ListViewItem(["—", "—", "—", r.Origin == OperationOrigin.Library ? "Sin cambios de campos: es un Pokémon nuevo de la Librería (su origen está en el resumen)." : "Sin cambios de campos."]));
        _list.EndUpdate();
        FitColumns(growWindow: true);
        _notes.Text = BuildNotes(r);
        if (owner is not null && !Visible)
            Show(owner);
        else if (!Visible)
            Show();
        else
            Activate();
    }

    /// <summary>
    /// «Motivo» recibe TODO el ancho que sobra (era una columna fija y estrecha que cortaba los motivos largos): las otras tres se ajustan a su contenido y, al presentar un resultado, la ventana
    /// crece (sin pasar del 92 % de la pantalla) hasta que el motivo más largo cabe entero; lo que aun así no cabe se lee en la ayuda emergente de la fila.
    /// </summary>
    private void FitColumns(bool growWindow = false)
    {
        if (_list.Columns.Count < 4)
            return;
        const int padding = 18;
        int Content(int col, int minimum, int maximum)
        {
            var w = TextRenderer.MeasureText(_list.Columns[col].Text, _list.Font).Width + padding;
            foreach (ListViewItem item in _list.Items)
                w = Math.Max(w, TextRenderer.MeasureText(item.SubItems[col].Text, _list.Font).Width + padding);
            return Math.Clamp(w, minimum, maximum);
        }
        var fixedWidth = 0;
        for (var c = 0; c < 3; c++)
        {
            _list.Columns[c].Width = Content(c, c == 0 ? 120 : 90, c == 0 ? 200 : 240);
            fixedWidth += _list.Columns[c].Width;
        }
        var longestReason = Content(3, 260, 2000);
        if (growWindow)
        {
            var screen = Screen.FromControl(this).WorkingArea;
            var wanted = fixedWidth + longestReason + SystemInformation.VerticalScrollBarWidth + 40;
            var target = Math.Min(Math.Max(Width, wanted + (Width - _list.ClientSize.Width)), (int)(screen.Width * 0.92));
            if (target > Width)
            {
                var oldWidth = Width;
                Width = target;
                if (Visible)   // ya presentada: se reparte el crecimiento a los dos lados; si aún no se mostró, CenterParent la centra con su nuevo ancho
                    Left = Math.Min(Math.Max(screen.Left, Left - (target - oldWidth) / 2), screen.Right - Width);
            }
        }
        var available = _list.ClientSize.Width - fixedWidth - SystemInformation.VerticalScrollBarWidth;
        _list.Columns[3].Width = Math.Max(260, available);
    }

    public static string BuildNotes(OperationResult r) => ChangeNotes.Text(r);

}
