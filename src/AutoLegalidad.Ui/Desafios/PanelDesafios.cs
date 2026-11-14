// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Desafios;
using PKHeX.Core;
using TextosD = AutoLegalidad.Desafios.Textos;

namespace AutoLegalidad.Ui.Desafios;

/// <summary>
/// Panel de R-013: para cada instalación con racha (Torre Batalla, Metro, Maison…) y cada uno de sus modos, los botones «Ir al jefe» (con la elección de jefe de plata, oro o repetido cuando aplica) y «Máximo»;
/// por instalación, «Todos al jefe» y «Todos al máximo»; y para todo el panel, «Todo al jefe» y «Todo al máximo». La lógica (los números, los topes, qué jefe toca) está en <see cref="InstalacionDesafio"/>: aquí solo se
/// llama y se cuenta lo que pasó, sin repetir ningún número.
/// <para>
/// Dos usos. <b>Compacto</b> (<c>editable = false</c>), dentro de los formularios que ya editan esas rachas (Frente de Esmeralda, Gen 4, Metro): el formulario sigue teniendo sus propios controles, así que el panel solo enseña un
/// resumen de cada modo y avisa al formulario con <paramref name="antes"/> (que vuelque sus controles a la partida) y <paramref name="despues"/> (que los vuelva a leer). <b>Editable</b> (<c>editable = true</c>), en el
/// formulario nuevo de lo que no tiene editor: cada campo tiene su casilla y se escribe en la partida al cambiarla.
/// </para>
/// <para>Si «Ir al jefe» no está disponible en un modo, el botón sale desactivado con el motivo de la biblioteca como ayuda emergente (y como descripción accesible).</para>
/// </summary>
public sealed class PanelDesafios : UserControl
{
    private readonly Func<SaveFile> _sav;
    private readonly IReadOnlyList<InstalacionDesafio> _instalaciones;
    private readonly bool _es;
    private readonly bool _editable;
    private readonly Action? _antes;
    private readonly Action? _despues;
    private readonly ToolTip _ayuda = new() { AutoPopDelay = 12000, InitialDelay = 300, ShowAlways = true };
    private readonly List<FilaModo> _filas = [];
    private readonly List<GroupBox> _bloques = [];
    private readonly Dictionary<InstalacionDesafio, (Button Jefe, Button Maximo)> _botonesInstalacion = [];
    private readonly Button _todoJefe = new() { Name = "B_DesafiosTodoJefe", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
    private readonly Button _todoMaximo = new() { Name = "B_DesafiosTodoMaximo", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
    // Sin AutoSize: una etiqueta con AutoSize y ancho máximo dentro de una fila automática se quedaba en 0 px de alto al agrandar la ventana. El alto se mide a mano (AjustarEstado).
    private readonly Label _estado = new() { Name = "L_DesafiosEstado", AutoSize = false, Dock = DockStyle.Fill, Padding = new Padding(2, 4, 2, 4) };
    private readonly Panel _desplazable = new() { Name = "P_DesafiosLista", Dock = DockStyle.Fill, AutoScroll = true };
    private readonly Panel _lista = new() { Name = "P_DesafiosBloques", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top };
    private bool _cargando;

    /// <summary>Se dispara tras cada acción que ha cambiado algo (para que el formulario que aloja el panel marque cambios, si quiere).</summary>
    public event EventHandler? Cambiado;

    /// <summary>Texto del último mensaje (para pruebas y para tecnología de asistencia).</summary>
    public string TextoEstado => _estado.Text;

    /// <param name="sav">La partida sobre la que se actúa (la copia que edita el formulario, no la del editor principal).</param>
    /// <param name="instalaciones">Las instalaciones que enseña el panel, ya filtradas por <see cref="InstalacionDesafio.Aplica"/>.</param>
    /// <param name="espanol">true: español de España; false: inglés.</param>
    /// <param name="editable">true: casillas por campo; false: solo resumen de cada modo.</param>
    /// <param name="antes">Se llama justo antes de cada acción (el formulario vuelca a la partida lo que lleva en sus controles).</param>
    /// <param name="despues">Se llama justo después de cada acción (el formulario vuelve a leer la partida y enseña los valores nuevos).</param>
    public PanelDesafios(Func<SaveFile> sav, IReadOnlyList<InstalacionDesafio> instalaciones, bool espanol, bool editable, Action? antes = null, Action? despues = null)
    {
        _sav = sav;
        _instalaciones = instalaciones;
        _es = espanol;
        _editable = editable;
        _antes = antes;
        _despues = despues;
        Name = "PanelDesafios";
        Dock = DockStyle.Fill;
        MinimumSize = new Size(420, 200);
        Padding = new Padding(6);

        _todoJefe.Text = TextosDesafiosUi.TodoAlJefe(_es);
        _todoMaximo.Text = TextosDesafiosUi.TodoAlMaximo(_es);
        _todoJefe.Click += (_, _) => AccionTodo(jefe: true);
        _todoMaximo.Click += (_, _) => AccionTodo(jefe: false);
        _ayuda.SetToolTip(_todoJefe, T("Deja la racha de cada modo justo antes del combate con su jefe, donde se sabe cuál es.", "Leaves the streak of every mode right before its boss battle, where it is known."));
        _ayuda.SetToolTip(_todoMaximo, T("Lleva cada campo de todos los modos al tope que admite el juego.", "Takes every field of every mode to the highest value the game accepts."));

        var barra = new FlowLayoutPanel { Name = "FLP_DesafiosBarra", Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, FlowDirection = FlowDirection.LeftToRight };
        barra.Controls.AddRange([_todoJefe, _todoMaximo]);

        _estado.Text = TextosDesafiosUi.EstadoInicial(_es);

        var raiz = new TableLayoutPanel { Name = "TLP_Desafios", Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        raiz.Controls.Add(barra, 0, 0);
        raiz.Controls.Add(_estado, 0, 1);
        _desplazable.Controls.Add(_lista);
        raiz.Controls.Add(_desplazable, 0, 2);
        Controls.Add(raiz);

        if (_instalaciones.Count == 0)
        {
            _todoJefe.Enabled = _todoMaximo.Enabled = false;
            _estado.Text = TextosDesafiosUi.SinInstalaciones(_es);
        }
        // Los bloques se acoplan arriba y a todo el ancho; el último de la colección queda el primero por arriba, así que se añaden al revés.
        var bloques = _instalaciones.Select(CrearBloque).ToList();
        for (var i = bloques.Count - 1; i >= 0; i--)
            _lista.Controls.Add(bloques[i]);

        _desplazable.Resize += (_, _) => Ajustar();
        HandleCreated += (_, _) => { Ajustar(); Refrescar(); };
        Refrescar();
    }

    private string T(string es, string en) => _es ? es : en;

    // ---------------------------------------------------------------- construcción

    private sealed class FilaModo(InstalacionDesafio instalacion, ModoDesafio modo)
    {
        public InstalacionDesafio Instalacion { get; } = instalacion;
        public ModoDesafio Modo { get; } = modo;
        public ComboBox Jefe { get; } = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
        public Label EtiquetaJefe { get; } = new() { AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(8, 7, 0, 0) };
        public Button IrAlJefe { get; } = new() { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        public Button Maximo { get; } = new() { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        public Label Resumen { get; } = new() { AutoSize = true };
        public FlowLayoutPanel Fila { get; } = new() { FlowDirection = FlowDirection.LeftToRight, WrapContents = true, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        public List<(CampoDesafio Campo, Control Control)> Editores { get; } = [];
        public List<JefeDesafio> JefesPosibles { get; } = [];
    }

    private GroupBox CrearBloque(InstalacionDesafio inst)
    {
        var bloque = new GroupBox
        {
            Name = "GB_" + inst.Clave,
            Text = $"{inst.Nombre.Para(_es)} ({inst.Juego.Para(_es)})",
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(8, 6, 8, 8),
            Margin = new Padding(0, 0, 0, 8),
        };
        var interior = new FlowLayoutPanel { Name = "FLP_" + inst.Clave, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top };
        bloque.Controls.Add(interior);

        var modos = inst.Modos(_sav());
        if (modos.Count > 1)
        {
            var jefe = new Button { Name = "B_TodosAlJefe_" + inst.Clave, Text = TextosDesafiosUi.TodosAlJefe(_es), AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
            var max = new Button { Name = "B_TodosAlMaximo_" + inst.Clave, Text = TextosDesafiosUi.TodosAlMaximo(_es), AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
            jefe.Click += (_, _) => AccionInstalacion(inst, esJefe: true);
            max.Click += (_, _) => AccionInstalacion(inst, esJefe: false);
            var cab = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, WrapContents = true, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
            cab.Controls.AddRange([jefe, max]);
            interior.Controls.Add(cab);
            _botonesInstalacion[inst] = (jefe, max);
        }

        foreach (var modo in modos)
        {
            var fila = new FilaModo(inst, modo);
            ConstruirFila(fila, soloUno: modos.Count == 1);
            _filas.Add(fila);
            interior.Controls.Add(fila.Fila);
        }
        _bloques.Add(bloque);
        return bloque;
    }

    private void ConstruirFila(FilaModo f, bool soloUno)
    {
        var id = $"{f.Instalacion.Clave}_{f.Modo.Clave}";
        f.Fila.Name = "FLP_Modo_" + id;
        f.Fila.Margin = new Padding(0, 2, 0, 6);
        var nombre = new Label { Name = "L_Modo_" + id, Text = f.Modo.Nombre.Para(_es), AutoSize = true, Margin = new Padding(3, 7, 8, 0), Font = new Font(Font, FontStyle.Bold) };
        f.EtiquetaJefe.Text = TextosDesafiosUi.Jefe(_es);
        f.EtiquetaJefe.Name = "L_Jefe_" + id;
        f.Jefe.Name = "CB_Jefe_" + id;
        f.IrAlJefe.Name = "B_IrAlJefe_" + id;
        f.Maximo.Name = "B_Maximo_" + id;
        f.IrAlJefe.Text = TextosDesafiosUi.IrAlJefe(_es);
        f.Maximo.Text = TextosDesafiosUi.Maximo(_es);
        f.Resumen.Name = "L_Resumen_" + id;
        f.IrAlJefe.Click += (_, _) => AccionModo(f, esJefe: true);
        f.Maximo.Click += (_, _) => AccionModo(f, esJefe: false);
        f.Jefe.SelectedIndexChanged += (_, _) => { if (!_cargando) ActualizarDisponibilidad(f); };
        _ayuda.SetToolTip(f.Maximo, T("Lleva todos los campos de este modo al tope que admite el juego.", "Takes every field of this mode to the highest value the game accepts."));

        f.Fila.Controls.Add(nombre);
        f.Fila.Controls.Add(f.EtiquetaJefe);
        f.Fila.Controls.Add(f.Jefe);
        f.Fila.Controls.Add(f.IrAlJefe);
        f.Fila.Controls.Add(f.Maximo);
        f.Fila.SetFlowBreak(f.Maximo, true);

        if (_editable)
        {
            var campos = new FlowLayoutPanel { Name = "FLP_Campos_" + id, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = new Padding(12, 0, 0, 0) };
            foreach (var campo in f.Modo.Campos)
            {
                var par = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = new Padding(0, 0, 10, 2) };
                var cid = $"{id}_{campo.Clave}";
                if (campo.Rol == RolCampo.Bandera)
                {
                    var chk = new CheckBox { Name = "CHK_" + cid, Text = campo.Nombre.Para(_es), AutoSize = true, Margin = new Padding(3, 5, 3, 0) };
                    chk.CheckedChanged += (_, _) => { if (!_cargando) EscribirCampo(f, campo, chk.Checked ? 1 : 0); };
                    par.Controls.Add(chk);
                    f.Editores.Add((campo, chk));
                }
                else
                {
                    var etiqueta = new Label { Name = "L_" + cid, Text = campo.Nombre.Para(_es) + ":", AutoSize = true, Margin = new Padding(3, 7, 3, 0) };
                    var nud = new NumericUpDown
                    {
                        Name = "NUD_" + cid,
                        Minimum = 0,
                        Maximum = campo.Maximo is { } m ? Math.Max(m, 0) : uint.MaxValue,
                        ThousandsSeparator = false,
                        Width = 100,
                    };
                    nud.ValueChanged += (_, _) => { if (!_cargando) EscribirCampo(f, campo, (long)nud.Value); };
                    par.Controls.AddRange([etiqueta, nud]);
                    f.Editores.Add((campo, nud));
                }
                campos.Controls.Add(par);
            }
            f.Fila.Controls.Add(campos);
        }
        else
        {
            f.Resumen.Margin = new Padding(12, 0, 0, 0);
            f.Fila.Controls.Add(f.Resumen);
        }
        _ = soloUno;
    }

    private void Ajustar()
    {
        var ancho = Math.Max(300, _desplazable.ClientSize.Width - 4);
        _lista.MaximumSize = new Size(ancho, 0);
        foreach (var f in _filas)
        {
            f.Fila.MaximumSize = new Size(Math.Max(200, ancho - 40), 0);
            f.Resumen.MaximumSize = new Size(Math.Max(150, ancho - 60), 0);
            foreach (var (_, c) in f.Editores)
                if (c.Parent is FlowLayoutPanel p && p.Parent is FlowLayoutPanel campos)
                    campos.MaximumSize = new Size(Math.Max(200, ancho - 60), 0);
        }
        AjustarEstado();
    }

    private void AjustarEstado()
    {
        var ancho = Math.Max(200, ClientSize.Width - Padding.Horizontal - 8);
        var alto = TextRenderer.MeasureText(_estado.Text, _estado.Font, new Size(ancho - _estado.Padding.Horizontal, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.TextBoxControl).Height + _estado.Padding.Vertical + 2;
        if (_estado.Height != alto || _estado.MinimumSize.Height != alto)
        {
            _estado.MinimumSize = new Size(0, alto);
            _estado.Height = alto;
        }
    }

    // ---------------------------------------------------------------- estado y lectura

    /// <summary>Vuelve a leer la partida y actualiza resumen, casillas y botones. Lo llama el panel tras cada acción; el formulario puede llamarlo si cambia la partida por su cuenta.</summary>
    public void Refrescar()
    {
        var sav = _sav();
        _cargando = true;
        try
        {
            foreach (var f in _filas)
            {
                var lectura = f.Instalacion.Leer(sav, f.Modo);
                foreach (var (campo, ctl) in f.Editores)
                {
                    var v = lectura.Valor(campo.Clave) ?? 0;
                    switch (ctl)
                    {
                        case NumericUpDown nud: nud.Value = Math.Min(nud.Maximum, Math.Max(nud.Minimum, v)); break;
                        case CheckBox chk: chk.Checked = v != 0; break;
                    }
                }
                f.Resumen.Text = ResumenLectura(lectura);
                RellenarJefes(f, sav);
            }
        }
        finally { _cargando = false; }
        foreach (var f in _filas)
            ActualizarDisponibilidad(f);
        Ajustar();
    }

    private string ResumenLectura(LecturaDesafio l)
    {
        var partes = new List<string>();
        if (l.RachaActual is { } r)
            partes.Add($"{TextosDesafiosUi.Racha(_es)}: {r}");
        if (l.Record is { } rec)
            partes.Add($"{TextosDesafiosUi.Record(_es)}: {rec}");
        if (partes.Count == 0)
            partes.AddRange(l.Valores.Where(v => v.Rol != RolCampo.Bandera).Take(3).Select(v => $"{v.Nombre.Para(_es)}: {v.Valor}"));
        return string.Join("   ", partes);
    }

    private void RellenarJefes(FilaModo f, SaveFile sav)
    {
        var anterior = f.Jefe.SelectedIndex >= 0 && f.Jefe.SelectedIndex < f.JefesPosibles.Count ? f.JefesPosibles[f.Jefe.SelectedIndex] : (JefeDesafio?)null;
        f.JefesPosibles.Clear();
        foreach (var j in (JefeDesafio[])[JefeDesafio.Plata, JefeDesafio.Oro, JefeDesafio.Repetido])
            if (f.Instalacion.PuedeIrAlJefe(sav, f.Modo, j).Puede)
                f.JefesPosibles.Add(j);
        f.Jefe.Items.Clear();
        foreach (var j in f.JefesPosibles)
            f.Jefe.Items.Add(TextosD.Jefe(j).Para(_es));
        if (f.JefesPosibles.Count > 0)
        {
            var idx = anterior is { } a ? f.JefesPosibles.IndexOf(a) : -1;
            f.Jefe.SelectedIndex = idx >= 0 ? idx : 0;
        }
        // La elección solo aplica cuando hay más de un jefe posible.
        var eleccion = f.JefesPosibles.Count > 1;
        f.Jefe.Visible = f.EtiquetaJefe.Visible = eleccion;
    }

    private JefeDesafio JefeElegido(FilaModo f) =>
        f.Jefe.SelectedIndex >= 0 && f.Jefe.SelectedIndex < f.JefesPosibles.Count ? f.JefesPosibles[f.Jefe.SelectedIndex] : JefeDesafio.Plata;

    private void ActualizarDisponibilidad(FilaModo f)
    {
        var sav = _sav();
        var jefe = JefeElegido(f);
        var d = f.Instalacion.PuedeIrAlJefe(sav, f.Modo, jefe);
        f.IrAlJefe.Enabled = d.Puede;
        var texto = d.Puede
            ? T("Deja la racha de este modo justo antes del combate con el jefe elegido.", "Leaves the streak of this mode right before the chosen boss battle.")
            : d.Motivo?.Para(_es) ?? string.Empty;
        _ayuda.SetToolTip(f.IrAlJefe, texto);
        f.IrAlJefe.AccessibleDescription = texto;
        f.Maximo.AccessibleDescription = T("Lleva todos los campos de este modo al tope que admite el juego.", "Takes every field of this mode to the highest value the game accepts.");
        // Un botón desactivado no recibe el ratón: el padre enseña la ayuda emergente por él.
        f.Fila.MouseMove -= FilaMouseMove;
        f.Fila.MouseMove += FilaMouseMove;
        f.Fila.Tag = f;
        if (_botonesInstalacion.TryGetValue(f.Instalacion, out var b))
            b.Jefe.Enabled = _filas.Where(x => x.Instalacion == f.Instalacion).Any(x => x.IrAlJefe.Enabled);
    }

    private void FilaMouseMove(object? sender, MouseEventArgs e)
    {
        if (sender is not FlowLayoutPanel fila || fila.Tag is not FilaModo f)
            return;
        if (!f.IrAlJefe.Enabled && f.IrAlJefe.Bounds.Contains(e.Location))
            _ayuda.Show(f.IrAlJefe.AccessibleDescription ?? string.Empty, fila, e.X + 12, e.Y + 18, 6000);
    }

    // ---------------------------------------------------------------- acciones

    private void EscribirCampo(FilaModo f, CampoDesafio campo, long valor)
    {
        campo.Escribir(_sav(), valor);
        ActualizarDisponibilidad(f);
        Cambiado?.Invoke(this, EventArgs.Empty);
    }

    private void AccionModo(FilaModo f, bool esJefe)
    {
        var sav = _sav();
        _antes?.Invoke();
        var r = esJefe ? f.Instalacion.IrAlJefe(sav, f.Modo, JefeElegido(f)) : f.Instalacion.Maximo(sav, f.Modo);
        _despues?.Invoke();
        Refrescar();
        _estado.Text = MensajeDe(r, f.Instalacion, f.Modo);
        AjustarEstado();
        if (r.Estado == EstadoResultado.Aplicado)
            Cambiado?.Invoke(this, EventArgs.Empty);
        Ajustar();
    }

    private void AccionInstalacion(InstalacionDesafio inst, bool esJefe)
    {
        var sav = _sav();
        _antes?.Invoke();
        var rs = esJefe ? inst.IrAlJefeTodo(sav) : inst.MaximoTodo(sav);
        _despues?.Invoke();
        Refrescar();
        _estado.Text = $"{inst.Nombre.Para(_es)}: {Contar(rs)}";
        AjustarEstado();
        if (rs.Any(r => r.Estado == EstadoResultado.Aplicado))
            Cambiado?.Invoke(this, EventArgs.Empty);
        Ajustar();
    }

    private void AccionTodo(bool jefe)
    {
        var sav = _sav();
        _antes?.Invoke();
        var rs = new List<ResultadoDesafio>();
        foreach (var inst in _instalaciones)
            rs.AddRange(jefe ? inst.IrAlJefeTodo(sav) : inst.MaximoTodo(sav));
        _despues?.Invoke();
        Refrescar();
        _estado.Text = $"{(jefe ? TextosDesafiosUi.TodoAlJefe(_es) : TextosDesafiosUi.TodoAlMaximo(_es))}: {Contar(rs)}";
        AjustarEstado();
        if (rs.Any(r => r.Estado == EstadoResultado.Aplicado))
            Cambiado?.Invoke(this, EventArgs.Empty);
        Ajustar();
    }

    private string Contar(IReadOnlyList<ResultadoDesafio> rs) =>
        TextosDesafiosUi.Resumen(_es, rs.Count(r => r.Estado == EstadoResultado.Aplicado), rs.Count(r => r.Estado == EstadoResultado.SinCambios), rs.Count(r => r.Estado == EstadoResultado.NoDisponible));

    /// <summary>Mensaje breve de lo que se hizo en un modo: qué jefe toca, qué valores cambiaron y, si la biblioteca lo explica (por ejemplo, que se hace la repetición porque ya tenéis el símbolo), esa explicación.</summary>
    private string MensajeDe(ResultadoDesafio r, InstalacionDesafio inst, ModoDesafio modo)
    {
        var cabecera = $"{inst.Nombre.Para(_es)}, {modo.Nombre.Para(_es)}";
        switch (r.Estado)
        {
            case EstadoResultado.NoDisponible:
                return $"{cabecera}: {r.Motivo?.Para(_es)}";
            case EstadoResultado.SinCambios:
                return $"{cabecera}: {TextosDesafiosUi.YaEstabaAsi(_es)}" + (r.Explicacion is { } e0 ? " " + e0.Para(_es) : string.Empty);
        }
        var cambios = r.Cambios.Take(3).Select(c => $"{c.Nombre.Para(_es)} {c.Antes} → {c.Despues}");
        var resto = r.Cambios.Count > 3 ? (_es ? $" y {r.Cambios.Count - 3} más" : $" and {r.Cambios.Count - 3} more") : string.Empty;
        var jefe = r.JefeAplicado is { } j ? $"{TextosD.Jefe(j).Para(_es)}. " : string.Empty;
        var explicacion = r.Explicacion is { } e ? " " + e.Para(_es) : string.Empty;
        return $"{cabecera}: {jefe}{string.Join("; ", cambios)}{resto}.{explicacion}";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _ayuda.Dispose();
        base.Dispose(disposing);
    }
}
