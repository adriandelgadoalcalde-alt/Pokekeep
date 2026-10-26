// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Pokekeep.Mobile.Ui.Partida;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.ViewModels;

/// <summary>Una fila de una tabla larga (record, constante, bandera): nombre, numero y su valor editable (numero o interruptor).</summary>
public sealed partial class FilaTablaPVm : ObservableObject
{
    private readonly ModeloEditorPartida _m;
    private TablaDefP _tabla;
    private readonly Action _alCambiar;
    private bool _releyendo;
    private string? _textoRechazado;

    public FilaTablaPVm(ModeloEditorPartida m, TablaDefP tabla, FilaTablaP def, Action alCambiar)
    {
        _m = m; _tabla = tabla; Def = def; _alCambiar = alCambiar;
        Releer();
    }

    public FilaTablaP Def { get; private set; }
    internal void ActualizarDefinicion(TablaDefP tabla, FilaTablaP def)
    {
        _tabla = tabla; Def = def; _especie = null;
        if (_textoRechazado is { } texto)
            Error = !long.TryParse(texto.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out _) ? IdiomaInterfaz.F("Form.EscribirNumero", Def.Min, Def.Max) : IdiomaInterfaz.F("Form.FueraRango", Def.Min, Def.Max);
        OnPropertyChanged(string.Empty);
    }
    public string Clave => Def.Clave(_tabla.Id);
    public string Nombre => Def.Nombre;
    private int? _especie;
    /// <summary>Especie con la que empieza el nombre de la fila (0 si no nombra ninguna): la fila lleva su sprite.</summary>
    public int Especie => _especie ??= Partida.EspecieEnTexto.De(_m.Sesion.Textos, Def.Nombre);
    public bool HayEspecie => Especie > 0;
    public string? Detalle => Def.Detalle;
    public bool HayDetalle => !string.IsNullOrEmpty(Def.Detalle);
    public string Numero => _tabla.SinNumero ? string.Empty : IdiomaInterfaz.F("Form.Tabla.Numero", Def.Id);
    public bool HayNumero => !_tabla.SinNumero;
    public bool EsMarca => Def.EsMarca;
    public bool EsNumero => !Def.EsMarca && !Def.SoloLectura;
    public bool EsMarcaEditable => Def.EsMarca && !Def.SoloLectura;
    /// <summary>Fila fija: se ve su valor en texto normal (nunca un control desactivado con el texto casi invisible).</summary>
    public bool EsFija => Def.SoloLectura;
    public string TextoFijo => Def.EsMarca ? (Marcado ? IdiomaInterfaz.T("Form.Tabla.FijoSi") : IdiomaInterfaz.T("Form.Tabla.FijoNo")) : Valor;
    public string Rango => IdiomaInterfaz.F("Form.Tabla.Rango", Def.Min, Def.Max);
    public string Accesible => Def.EsMarca ? $"{Def.Nombre}: {(Marcado ? IdiomaInterfaz.T("Comun.Si") : IdiomaInterfaz.T("Comun.No"))}" : $"{Def.Nombre}: {Valor}";

    [ObservableProperty] private string _valor = string.Empty;
    [ObservableProperty] private bool _marcado;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private bool _modificado;
    public bool HayError => !string.IsNullOrEmpty(Error);
    public string TextoMarca => Marcado ? IdiomaInterfaz.T("Comun.Si") : IdiomaInterfaz.T("Comun.No");

    partial void OnErrorChanged(string? value) => OnPropertyChanged(nameof(HayError));

    partial void OnMarcadoChanged(bool value)
    {
        OnPropertyChanged(nameof(TextoMarca)); OnPropertyChanged(nameof(Accesible));
        if (_releyendo) return;
        _m.PonerFila(_tabla.Id, Def, value);
        Modificado = _m.CuentaComoCambio(Clave);
        _alCambiar();
    }

    partial void OnValorChanged(string value)
    {
        OnPropertyChanged(nameof(Accesible));
        if (_releyendo || Def.EsMarca) return;
        _textoRechazado = value;
        if (!long.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)) { Error = IdiomaInterfaz.F("Form.EscribirNumero", Def.Min, Def.Max); return; }
        if (n < Def.Min || n > Def.Max)
        {
            Error = IdiomaInterfaz.F("Form.FueraRango", Def.Min, Def.Max);
            _releyendo = true; try { Valor = Convert.ToString(_m.Borrador(Clave), CultureInfo.InvariantCulture) ?? "0"; } finally { _releyendo = false; }
            return;
        }
        _textoRechazado = null;
        Error = _m.PonerFila(_tabla.Id, Def, n);
        Modificado = _m.CuentaComoCambio(Clave);
        _alCambiar();
    }

    public void PerderFoco()
    {
        if (Error is null) return;
        _releyendo = true;
        try { Valor = Convert.ToString(_m.Borrador(Clave), CultureInfo.InvariantCulture) ?? "0"; } finally { _releyendo = false; }
    }

    public void Releer()
    {
        _releyendo = true;
        try
        {
            var v = _m.Borrador(Clave);
            if (Def.EsMarca) Marcado = v is true; else Valor = Convert.ToString(v, CultureInfo.InvariantCulture) ?? "0";
            Modificado = _m.CuentaComoCambio(Clave); Error = null; _textoRechazado = null;
        }
        finally { _releyendo = false; }
    }
}

/// <summary>Una accion masiva de una tabla en pantalla («Obtener todos»).</summary>
public sealed class AccionTablaPVm(AccionTablaP def, Action ejecutar) : ObservableObject
{
    private AccionTablaP _def = def;
    internal string Id => _def.Id;
    internal void ActualizarDefinicion(AccionTablaP nueva) { _def = nueva; OnPropertyChanged(nameof(Texto)); OnPropertyChanged(nameof(Ayuda)); }
    public string Texto => _def.Texto;
    public string Ayuda => _def.Ayuda ?? _def.Texto;
    public System.Windows.Input.ICommand Comando { get; } = new CommunityToolkit.Mvvm.Input.RelayCommand(ejecutar);
}

/// <summary>Tabla larga con buscador, virtualizada: records de entrenador, constantes y banderas de evento. Solo se pintan las filas que se ven.</summary>
public sealed partial class TablaPVm : ObservableObject
{
    private readonly ModeloEditorPartida _m;
    private readonly List<FilaTablaPVm> _todas;
    private readonly Action _alCambiar;

    public TablaPVm(ModeloEditorPartida m, TablaDefP def, Action alCambiar)
    {
        _m = m; Def = def; _alCambiar = alCambiar;
        _todas = [.. m.Filas(def.Id).Select(f => new FilaTablaPVm(m, def, f, Cambio))];
        Acciones = [.. def.Acciones.Select(a => new AccionTablaPVm(a, () => EjecutarAccion(Def.Acciones.Single(n => n.Id == a.Id))))];
        Filtrar();
    }

    public IReadOnlyList<AccionTablaPVm> Acciones { get; }
    public bool HayAcciones => Acciones.Count > 0;
    /// <summary>Lo ultimo que dijo una accion masiva (rechazos) o null.</summary>
    [ObservableProperty] private string? _avisoAccion;
    public bool HayAviso => !string.IsNullOrEmpty(AvisoAccion);
    partial void OnAvisoAccionChanged(string? value) => OnPropertyChanged(nameof(HayAviso));

    private void EjecutarAccion(AccionTablaP a)
    {
        AvisoAccion = _m.AplicarAccionTabla(Def.Id, a);
        foreach (var f in _todas) f.Releer();
        Filtrar();
        _alCambiar();
    }

    public TablaDefP Def { get; private set; }
    public string Marcador => Def.Marcador;
    public bool HayAyuda => !string.IsNullOrEmpty(Def.Ayuda);
    public string? Ayuda => Def.Ayuda;
    [ObservableProperty] private string _filtro = string.Empty;
    [ObservableProperty] private bool _ayudaVisible;
    public bool MostrarAyuda => HayAyuda && AyudaVisible;
    partial void OnAyudaVisibleChanged(bool value) => OnPropertyChanged(nameof(MostrarAyuda));
    [ObservableProperty] private bool _soloCambiadas;
    [ObservableProperty] private IReadOnlyList<FilaTablaPVm> _visibles = [];
    [ObservableProperty] private string _resumen = string.Empty;
    public bool SinResultados => Visibles.Count == 0;

    partial void OnFiltroChanged(string value) => Filtrar();
    partial void OnSoloCambiadasChanged(bool value) => Filtrar();
    partial void OnVisiblesChanged(IReadOnlyList<FilaTablaPVm> value) => OnPropertyChanged(nameof(SinResultados));

    internal void ActualizarIdioma(TablaDefP def)
    {
        Def = def;
        var filas = _m.Filas(def.Id).ToDictionary(f => f.Id);
        foreach (var fila in _todas) fila.ActualizarDefinicion(def, filas[fila.Def.Id]);
        foreach (var accion in Acciones) accion.ActualizarDefinicion(def.Acciones.Single(a => a.Id == accion.Id));
        OnPropertyChanged(nameof(Marcador)); OnPropertyChanged(nameof(Ayuda)); OnPropertyChanged(nameof(HayAyuda)); OnPropertyChanged(nameof(MostrarAyuda));
        Filtrar();
    }

    private void Cambio()
    {
        // Una fila puede arrastrar a otras (capturado -> visto): se vuelven a leer todas.
        if (Def.Coherencia is not null) foreach (var f in _todas) f.Releer();
        _alCambiar();
    }

    public void Filtrar()
    {
        var f = SelectorHojaViewModel.Normalizar(Filtro.Trim());
        IEnumerable<FilaTablaPVm> q = _todas;
        if (SoloCambiadas) q = q.Where(x => x.Modificado);
        if (f.Length > 0) q = q.Where(x => SelectorHojaViewModel.Normalizar(x.Nombre).Contains(f) || (x.Detalle is not null && SelectorHojaViewModel.Normalizar(x.Detalle).Contains(f)) || x.Def.Id.ToString(CultureInfo.InvariantCulture) == f);
        var l = q.ToList();
        Visibles = l;
        var cambiadas = _todas.Count(x => x.Modificado);
        Resumen = IdiomaInterfaz.F("Form.Tabla.Resumen", l.Count, _todas.Count) + (cambiadas > 0 ? IdiomaInterfaz.F("Form.Tabla.Cambiadas", cambiadas) : string.Empty);
    }
}
