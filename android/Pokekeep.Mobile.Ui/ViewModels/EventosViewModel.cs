// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pokekeep.Mobile.Ui.Almacen;
using Pokekeep.Mobile.Ui.Modelo;
using Pokekeep.Mobile.Ui.Partida;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.ViewModels;

/// <summary>Lista virtualizada con la cabecera fija en el primer puesto: al filtrar solo se cambia la cola (dos avisos de rango), asi la cabecera conserva el foco del buscador.</summary>
public sealed class ColeccionConCabecera : ObservableCollection<object>
{
    public void PonerCabecera(object cabecera) { if (Count == 0) Add(cabecera); else this[0] = cabecera; }

    public void ReemplazarCola(IReadOnlyList<object> nuevos)
    {
        var viejos = Items.Skip(1).ToList();
        for (var i = Items.Count - 1; i >= 1; i--) Items.RemoveAt(i);
        if (viejos.Count > 0) OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, viejos, 1));
        foreach (var n in nuevos) Items.Add(n);
        if (nuevos.Count > 0) OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, nuevos.ToList(), 1));
        OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(Count)));
    }
}

public sealed record EncabezadoEventos(EventosViewModel Vm);

/// <summary>Separa «clave_interna (texto legible)» (los nombres de Let's Go) en lo legible y la clave.</summary>
internal static class EtiquetaLegible
{
    public static (string Legible, string? Clave) Partir(string nombre) => Partida.TextoEventosPublico.Partir(nombre);
}

public enum PaginaEventos { Banderas, Constantes, Investigar }

/// <summary>Una pestana de la pantalla (Banderas, Constantes, Investigar).</summary>
public sealed partial class PaginaEventosVm(PaginaEventos pagina, string titulo) : ObservableObject
{
    public PaginaEventos Pagina { get; } = pagina;
    public string Titulo { get; } = titulo;
    [ObservableProperty] private bool _elegida;
    public string Accesible => IdiomaInterfaz.F("Eventos.PestanaEventos", Titulo);
}

/// <summary>Un grupo de banderas o constantes (Historia, Objetos ocultos...) como filtro.</summary>
public sealed partial class GrupoEventosVm(string nombre, int cuantas) : ObservableObject
{
    public string Nombre { get; } = nombre;
    public string Texto => Nombre.Length == 0 ? IdiomaInterfaz.T("ModeloLotes.Todos") : Nombre;
    public int Cuantas { get; } = cuantas;
    [ObservableProperty] private bool _elegido;
    public string Accesible => Nombre.Length == 0 ? IdiomaInterfaz.T("Eventos.TodosGrupos") : IdiomaInterfaz.F("Eventos.GrupoElementos", Nombre, Cuantas);
}

/// <summary>Una bandera con nombre: interruptor Si/No que escribe en el borrador de eventos, nunca en la partida.</summary>
public sealed partial class FilaBanderaVm : ObservableObject
{
    private readonly EventosViewModel _vm;
    private bool _releyendo;

    public FilaBanderaVm(EventosViewModel vm, EtiquetaBandera e)
    {
        _vm = vm; Etiqueta = e;
        _releyendo = true; try { Marcada = vm.Modelo.Bandera(e.Indice); } finally { _releyendo = false; }
    }

    public EtiquetaBandera Etiqueta { get; }
    public string Nombre => EtiquetaLegible.Partir(Etiqueta.Nombre).Legible;
    public string Numero => IdiomaInterfaz.F("Eventos.NumeroEvento", Etiqueta.Indice, Etiqueta.Grupo);
    public string? Clave => EtiquetaLegible.Partir(Etiqueta.Nombre).Clave;
    public bool HayClave => Clave is not null;
    [ObservableProperty] private bool _marcada;
    public bool Modificada => _vm.Modelo.BanderaModificada(Etiqueta.Indice);
    public string TextoMarca => Marcada ? IdiomaInterfaz.T("Comun.Si") : IdiomaInterfaz.T("Comun.No");
    public string Accesible => $"{Nombre}: {TextoMarca}";

    partial void OnMarcadaChanged(bool value)
    {
        OnPropertyChanged(nameof(TextoMarca)); OnPropertyChanged(nameof(Accesible));
        if (_releyendo) return;
        _vm.Modelo.PonerBandera(Etiqueta.Indice, value);
        OnPropertyChanged(nameof(Modificada));
        _vm.AlCambiar();
    }

    public void Releer()
    {
        _releyendo = true; try { Marcada = _vm.Modelo.Bandera(Etiqueta.Indice); } finally { _releyendo = false; }
        OnPropertyChanged(nameof(Modificada));
    }
}

/// <summary>Una constante con nombre: numero editable con rechazo motivado y, si el juego tiene valores con nombre, un selector de ellos.</summary>
public sealed partial class FilaConstanteVm : ObservableObject
{
    private readonly EventosViewModel _vm;
    private bool _releyendo;

    public FilaConstanteVm(EventosViewModel vm, EtiquetaConstante e)
    {
        _vm = vm; Etiqueta = e;
        Releer();
    }

    public EtiquetaConstante Etiqueta { get; }
    public string Nombre => EtiquetaLegible.Partir(Etiqueta.Nombre).Legible;
    public string Numero => IdiomaInterfaz.F("Eventos.NumeroEvento", Etiqueta.Indice, Etiqueta.Grupo);
    public string? Clave => EtiquetaLegible.Partir(Etiqueta.Nombre).Clave;
    public bool HayClave => Clave is not null;
    [ObservableProperty] private string _valor = "0";
    [ObservableProperty] private string? _error;
    public bool HayError => !string.IsNullOrEmpty(Error);
    public bool Modificada => _vm.Modelo.ConstanteModificada(Etiqueta.Indice);
    public bool TieneOpciones => Etiqueta.Opciones.Count > 1;
    public string TextoOpcion => Etiqueta.Opciones.FirstOrDefault(o => !o.Personalizado && o.Valor == _vm.Modelo.Constante(Etiqueta.Indice))?.Nombre ?? IdiomaInterfaz.T("ModeloEventos.OtroValor");
    public string Rango => IdiomaInterfaz.F("Eventos.Entre", _vm.Modelo.MinConstante, _vm.Modelo.MaxConstante);
    public string Accesible => $"{Nombre}: {Valor}";

    partial void OnErrorChanged(string? value) => OnPropertyChanged(nameof(HayError));

    partial void OnValorChanged(string value)
    {
        OnPropertyChanged(nameof(Accesible));
        if (_releyendo) return;
        if (!long.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)) { Error = IdiomaInterfaz.F("Eventos.EscribeNumero", Rango); return; }
        var motivo = _vm.Modelo.PonerConstante(Etiqueta.Indice, n);
        if (motivo is not null)
        {
            Error = IdiomaInterfaz.T("Eventos.Cambiado") + motivo[0].ToString().ToLowerInvariant() + motivo[1..];
            _releyendo = true; try { Valor = _vm.Modelo.Constante(Etiqueta.Indice).ToString(CultureInfo.InvariantCulture); } finally { _releyendo = false; }
            return;
        }
        Error = null;
        OnPropertyChanged(nameof(Modificada)); OnPropertyChanged(nameof(TextoOpcion));
        _vm.AlCambiar();
    }

    public void PerderFoco()
    {
        if (Error is null) return;
        _releyendo = true; try { Valor = _vm.Modelo.Constante(Etiqueta.Indice).ToString(CultureInfo.InvariantCulture); } finally { _releyendo = false; }
    }

    public void Releer()
    {
        _releyendo = true;
        try { Valor = _vm.Modelo.Constante(Etiqueta.Indice).ToString(CultureInfo.InvariantCulture); Error = null; } finally { _releyendo = false; }
        OnPropertyChanged(nameof(Modificada)); OnPropertyChanged(nameof(TextoOpcion));
    }

    [RelayCommand] private void ElegirOpcion() => _vm.ElegirOpcion(this);
    public void Poner(OpcionConstante o) { if (!o.Personalizado) Valor = o.Valor.ToString(CultureInfo.InvariantCulture); }
}

/// <summary>Una linea del resultado de comparar dos guardados.</summary>
public sealed record LineaDiferencia(string Titulo, string Detalle)
{
    public string Accesible => $"{Titulo}. {Detalle}";
}

/// <summary>
/// Eventos y progreso (<c>SAV_EventFlags</c>, <c>SAV_EventFlags2</c>, <c>SAV_EventWork</c>): banderas y constantes del juego con sus nombres, edicion por numero y «Investigar» (comparar con otro guardado). Trabaja sobre un borrador
/// (<see cref="ModeloEventos"/>); «Aplicar» escribe todo el lote como UNA entrada del historial.
/// </summary>
public sealed partial class EventosViewModel : ObservableObject
{
    private readonly Sesion _sesion;
    private readonly Func<IAlmacenDocumentos?> _documentos;
    private readonly Func<IPortapapeles?> _portapapeles;
    private readonly Action<HojaViewModel> _abrirHoja;
    private readonly Action<string, string, string, string?, ICommand?> _informar;
    private readonly Action _volver;
    private bool _aplicando;
    private DiferenciaEventos? _ultima;

    private readonly Action<Pokekeep.Mobile.Ui.Cajas.EditorCasillas.TipoCambio> _alCambiarCasillas;   // referencia fuerte de la suscripcion debil a Sesion.CasillasCambiadas
    public EventosViewModel(Sesion sesion, Func<IAlmacenDocumentos?> documentos, Func<IPortapapeles?> portapapeles, Action<HojaViewModel> abrirHoja, Action<string, string, string, string?, ICommand?> informar, Action volver)
    {
        _sesion = sesion; _documentos = documentos; _portapapeles = portapapeles; _abrirHoja = abrirHoja; _informar = informar; _volver = volver;
        Modelo = new ModeloEventos(sesion);
        Paginas = [new(PaginaEventos.Banderas, IdiomaInterfaz.T("Catalogo.Marcas")), new(PaginaEventos.Constantes, IdiomaInterfaz.T("Eventos.Constantes")), new(PaginaEventos.Investigar, IdiomaInterfaz.T("EventosViewModel.Investigar"))];
        Elementos.PonerCabecera(new EncabezadoEventos(this));
        Ir(Paginas[0]);
        _alCambiarCasillas = _ => { if (!_aplicando) Recargar(); };
        sesion.SuscribirCasillasDebil(_alCambiarCasillas);
    }

    public ModeloEventos Modelo { get; private set; }
    public IReadOnlyList<PaginaEventosVm> Paginas { get; }
    public ColeccionConCabecera Elementos { get; } = [];
    public ObservableCollection<GrupoEventosVm> Grupos { get; } = [];
    [ObservableProperty] private PaginaEventosVm? _actual;
    [ObservableProperty] private string _filtro = string.Empty;
    [ObservableProperty] private bool _soloCambiadas;
    [ObservableProperty] private bool _hayCambios;
    [ObservableProperty] private string _resumen = string.Empty;
    [ObservableProperty] private string _numeroTexto = string.Empty;
    [ObservableProperty] private string _numeroValor = string.Empty;
    [ObservableProperty] private string? _errorNumero;
    [ObservableProperty] private bool _numeroMarcada;
    [ObservableProperty] private bool _otroEsNuevo = true;
    [ObservableProperty] private string _resumenInvestigar = string.Empty;

    public bool EnBanderas => Actual?.Pagina == PaginaEventos.Banderas;
    public bool EnConstantes => Actual?.Pagina == PaginaEventos.Constantes;
    public bool EnInvestigar => Actual?.Pagina == PaginaEventos.Investigar;
    public bool EnLista => !EnInvestigar;
    public bool HayErrorNumero => !string.IsNullOrEmpty(ErrorNumero);
    public bool SinErrorNumero => !HayErrorNumero;
    public bool PorNumero => Modelo.PorNumero;
    public bool HayGrupos => Grupos.Count > 2;
    public bool NumeroEsBandera => EnBanderas;
    public string TextoCambios => HayCambios ? IdiomaInterfaz.P("EventosViewModel.CambiosSinAplicar", Modelo.NumCambios) : IdiomaInterfaz.T("Mochila.SinCambios");
    public string TituloNumero => EnBanderas ? IdiomaInterfaz.T("Eventos.MarcaNumero") : IdiomaInterfaz.T("Eventos.ConstanteNumero");
    public string AyudaNumero => EnBanderas ? IdiomaInterfaz.F("Eventos.Entre2", Modelo.NumBanderas - 1) : IdiomaInterfaz.F("Eventos.Entre2", Modelo.NumConstantes - 1);
    public bool HayDiferencia => _ultima is { Valida: true };
    public bool HayMotivoDiferencia => _ultima is { Valida: false };
    public string? MotivoDiferencia => _ultima?.Motivo;

    partial void OnHayCambiosChanged(bool value) => OnPropertyChanged(nameof(TextoCambios));
    partial void OnErrorNumeroChanged(string? value) { OnPropertyChanged(nameof(HayErrorNumero)); OnPropertyChanged(nameof(SinErrorNumero)); }
    partial void OnFiltroChanged(string value) => Rellenar();
    partial void OnSoloCambiadasChanged(bool value) => Rellenar();

    partial void OnActualChanged(PaginaEventosVm? value)
    {
        foreach (var p in Paginas) p.Elegida = ReferenceEquals(p, value);
        foreach (var n in new[] { nameof(EnBanderas), nameof(EnConstantes), nameof(EnInvestigar), nameof(EnLista), nameof(TituloNumero), nameof(AyudaNumero), nameof(NumeroEsBandera) }) OnPropertyChanged(n);
    }

    [RelayCommand]
    private void Ir(PaginaEventosVm? p)
    {
        if (p is null) return;
        Actual = p;
        Filtro = string.Empty;
        ErrorNumero = null; NumeroTexto = string.Empty; NumeroValor = string.Empty;
        ConstruirGrupos();
        Rellenar();
    }

    private void ConstruirGrupos()
    {
        Grupos.Clear();
        if (EnInvestigar) { OnPropertyChanged(nameof(HayGrupos)); return; }
        var nombres = EnBanderas ? Modelo.Banderas.Select(b => b.Grupo) : Modelo.Constantes.Select(c => c.Grupo);
        var agrupados = nombres.GroupBy(x => x).OrderBy(g => g.Key, StringComparer.CurrentCulture).ToList();
        Grupos.Add(new GrupoEventosVm(string.Empty, agrupados.Sum(g => g.Count())) { Elegido = true });
        foreach (var g in agrupados) Grupos.Add(new GrupoEventosVm(g.Key, g.Count()));
        OnPropertyChanged(nameof(HayGrupos));
    }

    [RelayCommand]
    private void ElegirGrupo(GrupoEventosVm? g)
    {
        if (g is null) return;
        foreach (var x in Grupos) x.Elegido = ReferenceEquals(x, g);
        Rellenar();
    }

    private static string Normalizar(string s) => SelectorHojaViewModel.Normalizar(s);

    /// <summary>Vuelve a pintar la lista de la pagina con el filtro, el grupo y «solo cambiadas».</summary>
    public void Rellenar()
    {
        var lista = new List<object>();
        var q = Normalizar(Filtro.Trim());
        var grupo = Grupos.FirstOrDefault(g => g.Elegido)?.Nombre ?? string.Empty;
        if (EnBanderas)
        {
            foreach (var b in Modelo.Banderas)
            {
                if (grupo.Length > 0 && b.Grupo != grupo) continue;
                if (SoloCambiadas && !Modelo.BanderaModificada(b.Indice)) continue;
                if (q.Length > 0 && !Normalizar(b.Nombre).Contains(q) && !b.Indice.ToString(CultureInfo.InvariantCulture).Contains(q)) continue;
                lista.Add(new FilaBanderaVm(this, b));
            }
            Resumen = Modelo.Banderas.Count == 0
                ? IdiomaInterfaz.T("Eventos.JuegoTieneNombresMarcas")
                : IdiomaInterfaz.F("Eventos.MarcasNombreTotal", lista.Count, Modelo.Banderas.Count, Modelo.NumBanderas);
        }
        else if (EnConstantes)
        {
            foreach (var c in Modelo.Constantes)
            {
                if (grupo.Length > 0 && c.Grupo != grupo) continue;
                if (SoloCambiadas && !Modelo.ConstanteModificada(c.Indice)) continue;
                if (q.Length > 0 && !Normalizar(c.Nombre).Contains(q) && !c.Indice.ToString(CultureInfo.InvariantCulture).Contains(q)) continue;
                lista.Add(new FilaConstanteVm(this, c));
            }
            Resumen = Modelo.Constantes.Count == 0
                ? IdiomaInterfaz.T("Eventos.JuegoTieneNombresConstantes")
                : IdiomaInterfaz.F("Eventos.ConstantesNombreTotal", lista.Count, Modelo.Constantes.Count, Modelo.NumConstantes);
        }
        else
        {
            if (_ultima is { Valida: true } d)
            {
                lista.AddRange(d.Puestas.Select(i => new LineaDiferencia(IdiomaInterfaz.F("Eventos.MarcaPuesta", i), Modelo.NombreBandera(i) ?? IdiomaInterfaz.T("Partida.Ranuras.SinNombre"))));
                lista.AddRange(d.Quitadas.Select(i => new LineaDiferencia(IdiomaInterfaz.F("Eventos.MarcaQuitada", i), Modelo.NombreBandera(i) ?? IdiomaInterfaz.T("Partida.Ranuras.SinNombre"))));
                lista.AddRange(d.Constantes.Select(t => new LineaDiferencia(IdiomaInterfaz.T("Eventos.ConstanteDistinta"), t)));
            }
            Resumen = _ultima is null ? IdiomaInterfaz.T("Eventos.EligeOtroGuardadoMismo")
                : _ultima.Valida ? IdiomaInterfaz.F("Eventos.PuestasQuitadasConstantesDistintas", _ultima.Puestas.Count, _ultima.Quitadas.Count, _ultima.Constantes.Count) : IdiomaInterfaz.T("Eventos.PodidoComparar");
        }
        Elementos.ReemplazarCola(lista);
        HayCambios = Modelo.HayCambios;
        OnPropertyChanged(nameof(HayDiferencia)); OnPropertyChanged(nameof(HayMotivoDiferencia)); OnPropertyChanged(nameof(MotivoDiferencia));
    }

    public void AlCambiar() { HayCambios = Modelo.HayCambios; OnPropertyChanged(nameof(TextoCambios)); }

    private void Recargar() { Modelo = new ModeloEventos(_sesion); ConstruirGrupos(); Rellenar(); }

    // ---- Por numero ----
    partial void OnNumeroTextoChanged(string value) => LeerNumero();

    private void LeerNumero()
    {
        ErrorNumero = null;
        if (!int.TryParse(NumeroTexto.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)) { NumeroValor = string.Empty; return; }
        var total = EnBanderas ? Modelo.NumBanderas : Modelo.NumConstantes;
        if (i < 0 || i >= total) { ErrorNumero = IdiomaInterfaz.F("Eventos.NumeroDebeEstarEntre", total - 1); return; }
        if (EnBanderas) NumeroMarcada = Modelo.Bandera(i);
        else NumeroValor = Modelo.Constante(i).ToString(CultureInfo.InvariantCulture);
    }

    partial void OnNumeroMarcadaChanged(bool value)
    {
        if (!EnBanderas || !int.TryParse(NumeroTexto.Trim(), out var i) || i < 0 || i >= Modelo.NumBanderas) return;
        if (Modelo.Bandera(i) == value) return;
        Modelo.PonerBandera(i, value);
        Rellenar();
    }

    [RelayCommand]
    private void PonerNumeroValor()
    {
        if (!int.TryParse(NumeroTexto.Trim(), out var i) || i < 0 || i >= Modelo.NumConstantes) { ErrorNumero = IdiomaInterfaz.T("Eventos.EscribeNumeroConstanteValido"); return; }
        if (!long.TryParse(NumeroValor.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)) { ErrorNumero = IdiomaInterfaz.T("Eventos.EscribeValorNumero"); return; }
        var motivo = Modelo.PonerConstante(i, v);
        if (motivo is not null) { ErrorNumero = IdiomaInterfaz.T("Eventos.Cambiado") + motivo; return; }
        ErrorNumero = null;
        Rellenar();
    }

    internal void ElegirOpcion(FilaConstanteVm fila)
    {
        var opciones = fila.Etiqueta.Opciones.Select((o, k) => new OpcionSelector(k, o.Nombre, o.Personalizado ? null : IdiomaInterfaz.F("Eventos.Valor", o.Valor))).ToList();
        _abrirHoja(new SelectorHojaViewModel(IdiomaInterfaz.F("Eventos.Valor2", fila.Nombre), opciones, -1, o =>
        {
            var op = fila.Etiqueta.Opciones[o.Id];
            if (op.Personalizado) return;
            fila.Poner(op);
        }, IdiomaInterfaz.T("Eventos.BuscarValor")));
    }

    // ---- Investigar ----
    [RelayCommand]
    private void ElegirOtroGuardado() => _ = CompararAsync();

    private async Task CompararAsync()
    {
        var docs = _documentos();
        if (docs is null) { _informar(IdiomaInterfaz.T("Form.SinSelector"), "aviso", "CircleAlert", null, null); return; }
        try
        {
            var doc = await docs.ElegirParaAbrirAsync(CancellationToken.None);
            if (doc is null) return;
            var datos = await docs.LeerAsync(doc, CancellationToken.None);
            Comparar(datos);
        }
        catch (Exception ex) { _informar(IdiomaInterfaz.T("Editor.PodidoLeerArchivo") + ex.Message, "error", "TriangleAlert", null, null); }
    }

    /// <summary>Compara la partida abierta con los bytes de otro guardado (separado del selector para poder probarlo).</summary>
    public void Comparar(byte[] datos)
    {
        _ultima = Modelo.Comparar(datos, OtroEsNuevo);
        if (!_ultima.Valida) _informar(_ultima.Motivo ?? IdiomaInterfaz.T("Eventos.PodidoComparar"), "error", "TriangleAlert", null, null);
        Rellenar();
    }

    [RelayCommand]
    private async Task CopiarResumen()
    {
        var p = _portapapeles();
        if (p is null || _ultima is not { Valida: true } d) return;
        var l = new List<string> { IdiomaInterfaz.T("Eventos.MarcasPuestas") + (d.Puestas.Count == 0 ? "ninguna" : string.Join(", ", d.Puestas.Select(i => i.ToString("0000")))), IdiomaInterfaz.T("Eventos.MarcasQuitadas") + (d.Quitadas.Count == 0 ? "ninguna" : string.Join(", ", d.Quitadas.Select(i => i.ToString("0000")))) };
        l.AddRange(d.Constantes);
        var ok = await p.CopiarAsync(string.Join(Environment.NewLine, l));
        _informar(ok ? IdiomaInterfaz.T("Eventos.ResumenCopiadoPortapapeles") : IdiomaInterfaz.T("Eventos.PodidoCopiarPortapapeles"), ok ? "ok" : "error", ok ? "CircleCheck" : "TriangleAlert", null, null);
    }

    // ---- Aplicar ----
    [RelayCommand]
    private void Aplicar()
    {
        _aplicando = true;
        try
        {
            var inf = Modelo.Aplicar();
            if (inf.Aplicado) _informar(IdiomaInterfaz.T("Eventos.EventosAplicadosPartidaPuedes"), "ok", "CircleCheck", null, null);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IndexOutOfRangeException or FormatException or OverflowException or KeyNotFoundException or NotSupportedException)
        {
            _informar(IdiomaInterfaz.F("Eventos.JuegoAdmitioCambioPartida", ex.Message), "error", "TriangleAlert", null, null);
        }
        finally { _aplicando = false; }
        Rellenar();
    }

    [RelayCommand]
    private void Descartar()
    {
        Modelo.Recargar();
        Rellenar();
        _informar(IdiomaInterfaz.T("Form.Descartados"), "ok", "CircleCheck", null, null);
    }

    [RelayCommand]
    public void Volver()
    {
        if (!Modelo.HayCambios) { _volver(); return; }
        _abrirHoja(new ConfirmarHojaViewModel(IdiomaInterfaz.T("Form.Salir.Titulo"), [IdiomaInterfaz.P("EventosViewModel.HasCambiado", Modelo.NumCambios), IdiomaInterfaz.T("Form.Salir.Explicacion")], IdiomaInterfaz.T("Form.Salir.Confirmar"), _volver, destructiva: true, textoCancelar: IdiomaInterfaz.T("Form.Salir.Cancelar")));
    }

    public bool Atras() { Volver(); return true; }
}
