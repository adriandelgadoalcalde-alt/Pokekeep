// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Almacen;
using Pokekeep.Mobile.Ui.Masivas;
using Pokekeep.Mobile.Ui.Modelo;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.ViewModels;

// ============================================================================================ Informe masivo (componente común de la F9)
/// <summary>
/// Una operación de una fila con su símbolo. ✔ y ✖ no están en la fuente de la app: dependen de la fuente de reserva del sistema (en el PC y en el móvil cambian de forma y de altura de línea según qué fuentes haya cargado ya), así
/// que se pintan como iconos propios; «=» y «—» son texto de la fuente normal. El texto accesible dice el estado con palabras.
/// </summary>
public sealed record SimboloOperacion(string Nombre, string Simbolo, string? Glifo, string Accesible)
{
    public bool EsGlifo => Glifo is not null;
    public bool EsTexto => Glifo is null;
}

/// <summary>Un filtro del informe: «Todos» (Estado null) o un estado concreto con su recuento.</summary>
public sealed record FiltroEstado(string? Estado, string Etiqueta, int Cuenta, bool Activo)
{
    public string Texto => $"{Etiqueta} ({Cuenta})";
    public string Accesible => Activo ? IdiomaInterfaz.F("MasivasViewModels.FiltroActivo", Texto) : IdiomaInterfaz.F("MasivasViewModels.Filtrar", Etiqueta, Cuenta);
}

/// <summary>Una fila del informe en pantalla; al tocarla se despliega su detalle (antes → después, motivo y resultado de cada operación).</summary>
public sealed partial class FilaInformeVm(FilaInformeMasivo fila) : ObservableObject
{
    public FilaInformeMasivo Fila { get; } = fila;
    public string Nombre => $"{Fila.Numero}. {Fila.Nombre}";
    public string Etiqueta => Fila.Etiqueta;
    public string? Casilla => Fila.Casilla;
    public bool HayCasilla => !string.IsNullOrEmpty(Fila.Casilla);
    public string Accesible => Fila.Accesible;

    /// <summary>Estado que pide atención (fallo, tiempo agotado...): se pinta como error; pasar por HOME, como aviso (los mismos colores que el escritorio: BulkReportForm).</summary>
    public bool EsError => EstadosMasivos.EsError(Fila.Estado);
    public bool EsAviso => EstadosMasivos.EsAviso(Fila.Estado);
    public bool EsOk => EstadosMasivos.EsOk(Fila.Estado);

    [ObservableProperty] private bool _abierta;

    /// <summary>Resumen de una línea de las operaciones de la fila («Stats ✔ Nivel = Amistad —»), con los símbolos del escritorio.</summary>
    public string LineaOperaciones => string.Join("  ", Fila.Operaciones.Select(o => $"{o.Nombre} {o.Simbolo}"));
    public bool HayOperaciones => Fila.Operaciones.Count > 0;
    public IReadOnlyList<SimboloOperacion> Simbolos => [.. Fila.Operaciones.Select(o => new SimboloOperacion(o.Nombre, o.Simbolo, o.Simbolo switch { "✔" => "Check", "✖" => "X", _ => null },
        $"{o.Nombre}: " + (o.Estado == EstadosOperacion.Changed ? IdiomaInterfaz.T("MasivasViewModels.Cambiado") : o.Estado == EstadosOperacion.AlreadyOptimal ? IdiomaInterfaz.T("MasivasViewModels.EstabaOptimo") : o.Estado == EstadosOperacion.NotApplicable ? IdiomaInterfaz.T("MasivasViewModels.NoAplicable") : "fallo")))];

    /// <summary>Todo el detalle de la fila en líneas (para el despliegue y para leerlo con el lector de pantalla).</summary>
    public IReadOnlyList<string> Detalle
    {
        get
        {
            var l = new List<string>();
            if (Fila.Origen is { Length: > 0 } o) l.Add(IdiomaInterfaz.T("MasivasViewModels.Origen") + o);
            if (Fila.Nivel is { } nv) l.Add(IdiomaInterfaz.F("Cajas.Nivel2", nv));
            if (Fila.Ivs is { Length: > 0 } iv) l.Add(IdiomaInterfaz.F("MasivasViewModels.EstadisticasBase", iv));
            if (Fila.Idioma is { Length: > 0 } id) l.Add("Idioma: " + id);
            if (Fila.Motivo is { Length: > 0 } m) l.Add(m);
            l.AddRange(Fila.Detalles);
            foreach (var op in Fila.Operaciones.Where(o2 => o2.Estado != EstadosOperacion.Changed && !string.IsNullOrEmpty(o2.Motivo)))
                l.Add($"{op.Nombre}: {op.Mensaje} {op.Motivo}");
            return l;
        }
    }
    public bool HayDetalle => Detalle.Count > 0;

    [RelayCommand] private void Alternar() => Abierta = !Abierta;
}

/// <summary>
/// Informe final de una operación masiva: recuento por estado como filtros, lista de Pokémon con su estado y, al tocar uno, el detalle; «Copiar» y «Guardar informe…» (TXT o CSV por el selector del sistema,
/// con el mismo texto que el escritorio). Nunca lleva bytes de la partida y nada sale del dispositivo salvo lo que el usuario elija guardar o copiar (reglas 13 y 15).
/// </summary>
public sealed partial class InformeMasivoViewModel : ObservableObject
{
    /// <summary>Filas que se pintan de golpe; el resto con «Mostrar más». Con 800+ filas una lista sin límite haría la pantalla inservible en un móvil.</summary>
    public const int FilasPorPagina = 60;

    private readonly Func<IPortapapeles?> _portapapeles;
    private readonly Func<IAlmacenDocumentos?> _documentos;
    private readonly Action<string, string, string> _informar;
    private string? _estado;
    private int _paginas = 1;

    public InformeMasivoViewModel(InformeMasivo informe, Func<IPortapapeles?> portapapeles, Func<IAlmacenDocumentos?> documentos, Action<string, string, string> informar)
    {
        Informe = informe; _portapapeles = portapapeles; _documentos = documentos; _informar = informar;
        Reconstruir();
    }

    public InformeMasivo Informe { get; }
    public string Titulo => Informe.Titulo;
    public int Total => Informe.Filas.Count;
    public bool HayFilas => Total > 0;
    public bool SinFilas => Total == 0;

    [ObservableProperty] private IReadOnlyList<FiltroEstado> _filtros = [];
    [ObservableProperty] private IReadOnlyList<FilaInformeVm> _visibles = [];
    [ObservableProperty] private string _resumenFiltro = string.Empty;
    [ObservableProperty] private string _textoMas = string.Empty;
    [ObservableProperty] private bool _hayMas;
    [ObservableProperty] private string _aviso = string.Empty;
    [ObservableProperty] private bool _ocupado;
    public bool HayAviso => !string.IsNullOrEmpty(Aviso);
    partial void OnAvisoChanged(string value) => OnPropertyChanged(nameof(HayAviso));

    public bool PuedeCopiar => _portapapeles() is not null;
    public bool PuedeGuardar => _documentos() is not null;

    /// <summary>Totales por estado en el orden del escritorio, con las etiquetas del núcleo: el cuerpo del resumen («Cambiado: 12»).</summary>
    public IReadOnlyList<string> Totales => [.. Informe.Filas.GroupBy(f => (f.OrdenEstado, f.Etiqueta)).OrderBy(g => g.Key.OrdenEstado).Select(g => $"{g.Key.Etiqueta}: {g.Count()}")];

    private void Reconstruir()
    {
        var porEstado = Informe.Filas.GroupBy(f => f.Estado).OrderBy(g => g.First().OrdenEstado).ToList();
        Filtros = [new FiltroEstado(null, IdiomaInterfaz.T("ModeloLotes.Todos"), Total, _estado is null), .. porEstado.Select(g => new FiltroEstado(g.Key, g.First().Etiqueta, g.Count(), _estado == g.Key))];
        var filtradas = Informe.Filas.Where(f => _estado is null || f.Estado == _estado).ToList();
        var n = Math.Min(filtradas.Count, _paginas * FilasPorPagina);
        Visibles = [.. filtradas.Take(n).Select(f => new FilaInformeVm(f))];
        HayMas = filtradas.Count > n;
        TextoMas = HayMas ? IdiomaInterfaz.F("CompletarJuego.MostrarRestantes", filtradas.Count - n) : string.Empty;
        ResumenFiltro = filtradas.Count == 0 ? IdiomaInterfaz.T("MasivasViewModels.FilasEstado") : _estado is null ? IdiomaInterfaz.F("MasivasViewModels.Filas", filtradas.Count) : IdiomaInterfaz.F("MasivasViewModels.FilasEstado2", filtradas.Count, filtradas[0].Etiqueta);
    }

    [RelayCommand]
    private void Filtrar(FiltroEstado? filtro)
    {
        if (filtro is null) return;
        _estado = filtro.Estado; _paginas = 1;
        Reconstruir();
    }

    [RelayCommand]
    private void MostrarMas() { _paginas++; Reconstruir(); }

    [RelayCommand]
    private async Task Copiar()
    {
        if (_portapapeles() is not { } p) { Aviso = IdiomaInterfaz.T("MasivasViewModels.CopiarDisponibleVentana"); return; }
        Aviso = await p.CopiarAsync(Informe.Texto) ? IdiomaInterfaz.T("MasivasViewModels.InformeCopiado") : IdiomaInterfaz.T("MasivasViewModels.PudoCopiarInforme");
    }

    [RelayCommand] private Task GuardarTexto() => GuardarAsync(".txt", Informe.Texto);
    [RelayCommand] private Task GuardarCsv() => GuardarAsync(".csv", Informe.Csv);

    private async Task GuardarAsync(string extension, string contenido)
    {
        if (_documentos() is not { } docs) { Aviso = IdiomaInterfaz.T("MasivasViewModels.GuardarDisponibleVentana"); return; }
        Ocupado = true;
        try
        {
            var destino = await docs.ElegirDestinoAsync("informe-pokekeep" + extension, CancellationToken.None);
            if (destino is null) return;
            // Con BOM, como el «Guardar informe…» del escritorio: Excel abre el CSV en UTF-8 sin pasar por el asistente.
            await docs.EscribirAsync(destino, new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(contenido)).ToArray(), CancellationToken.None);
            Aviso = IdiomaInterfaz.T("MasivasViewModels.InformeGuardado");
            _informar(Aviso, "ok", "CircleCheck");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Aviso = IdiomaInterfaz.T("MasivasViewModels.PudoGuardarInforme") + ex.Message;
            _informar(Aviso, "error", "TriangleAlert");
        }
        finally { Ocupado = false; }
    }
}

// ============================================================================================ Asistente de pasos (componente común de la F9)
/// <summary>Los pasos del asistente. <see cref="Eventos"/> (bloque 9a) es la vista previa de «Preferir la versión de evento», entre la búsqueda y la ejecución.</summary>
public enum PasoMasivo { Alcance, VistaPrevia, Ejecucion, Informe, Eventos }

/// <summary>
/// Esqueleto de las operaciones masivas largas (Edición masiva y Completar el juego): alcance → vista previa → ejecución (progreso real, Cancelar, UNA pregunta si se cancela con trabajo hecho) → informe con UN Deshacer.
/// Cada operación pone su alcance y su vista previa; el progreso, la cancelación, la pregunta, el informe y el Deshacer son comunes. ADR 0069.
/// </summary>
public abstract partial class AsistenteMasivoViewModel : PantallaMasViewModel
{
    private readonly Func<IPortapapeles?> _portapapeles;
    private readonly Func<IAlmacenDocumentos?> _documentos;
    private readonly Action<string, string, string> _informar;
    private CancellationTokenSource? _cts;
    private TaskCompletionSource<bool>? _pregunta;

    protected AsistenteMasivoViewModel(Action volver, Action<string, string, string> informar, Func<IPortapapeles?> portapapeles, Func<IAlmacenDocumentos?> documentos) : base(volver)
    {
        _informar = informar; _portapapeles = portapapeles; _documentos = documentos;
    }

    /// <summary>Con el texto grande (x1,3 o más) los botones de dos columnas no caben uno junto al otro: se apilan.</summary>
    public int ColumnasBotones => Design.Movimiento.EscalaTexto >= 1.3 ? 1 : 2;

    [ObservableProperty] private PasoMasivo _paso = PasoMasivo.Alcance;
    [ObservableProperty] private bool _ocupado;
    [ObservableProperty] private bool _cancelando;
    [ObservableProperty] private string _textoProgreso = IdiomaInterfaz.T("AccionHoja.PreparandoMotor");
    [ObservableProperty] private double _fraccion;
    [ObservableProperty] private bool _indeterminado = true;
    [ObservableProperty] private string _transcurrido = "0 s";
    /// <summary>«50 % · Tiempo: 5,0 s»: el porcentaje solo cuando hay un avance real (la marquesina no lo tiene).</summary>
    public string LineaTiempo => (Indeterminado ? string.Empty : $"{(int)Math.Round(Fraccion * 100)} % · ") + IdiomaInterfaz.F("AccionHoja.Tiempo", Transcurrido);
    partial void OnTranscurridoChanged(string value) => OnPropertyChanged(nameof(LineaTiempo));
    partial void OnFraccionChanged(double value) => OnPropertyChanged(nameof(LineaTiempo));
    partial void OnIndeterminadoChanged(bool value) => OnPropertyChanged(nameof(LineaTiempo));
    [ObservableProperty] private IReadOnlyList<string> _recientes = [];
    [ObservableProperty] private string? _textoPregunta;
    [ObservableProperty] private string _titular = string.Empty;
    [ObservableProperty] private string? _detalle;
    [ObservableProperty] private string _claseVisual = string.Empty;
    [ObservableProperty] private string _glifoResultado = "Info";
    [ObservableProperty] private InformeMasivoViewModel? _informe;
    [ObservableProperty] private bool _puedeDeshacer;
    [ObservableProperty] private bool _deshecha;
    [ObservableProperty] private string? _avisoDeshacer;
    [ObservableProperty] private string _duracion = string.Empty;

    public ResultadoMasivo? Resultado { get; private set; }

    public bool EnAlcance => Paso == PasoMasivo.Alcance;
    public bool EnVistaPrevia => Paso == PasoMasivo.VistaPrevia;
    public bool EnEjecucion => Paso == PasoMasivo.Ejecucion;
    public bool EnInforme => Paso == PasoMasivo.Informe;
    public bool EnEventos => Paso == PasoMasivo.Eventos;
    public bool NoOcupado => !Ocupado;
    public bool NoCancelando => !Cancelando;
    public bool HayPregunta => !string.IsNullOrEmpty(TextoPregunta);
    public bool SinPregunta => !HayPregunta;
    public bool ResultadoOk => ClaseVisual == "ok";
    public bool ResultadoAviso => ClaseVisual == "aviso";
    public bool ResultadoError => ClaseVisual == "error";
    public bool HayAvisoDeshacer => !string.IsNullOrEmpty(AvisoDeshacer);
    public bool HayDetalle => !string.IsNullOrWhiteSpace(Detalle);

    /// <summary>«Paso 2 de 3» (el informe es el resultado, no un paso). Un asistente con un paso más (la versión de evento) lo cambia.</summary>
    public virtual string TextoPaso => Paso switch
    {
        PasoMasivo.Alcance => IdiomaInterfaz.F("MasivasViewModels.PasoDe", NombreAlcance),
        PasoMasivo.VistaPrevia => IdiomaInterfaz.T("MasivasViewModels.PasoVistaPrevia"),
        PasoMasivo.Ejecucion => IdiomaInterfaz.T("MasivasViewModels.PasoEjecucion"),
        _ => IdiomaInterfaz.T("MasivasViewModels.Resultado"),
    };
    protected abstract string NombreAlcance { get; }

    /// <summary>Atrás del sistema sobre esta pantalla: true si lo consume (una operación en marcha no se abandona; un asistente puede volver a un paso suyo). False = cerrar la pantalla.</summary>
    public virtual bool Atras() => Ocupado;

    partial void OnPasoChanged(PasoMasivo value)
    {
        OnPropertyChanged(nameof(EnAlcance)); OnPropertyChanged(nameof(EnVistaPrevia)); OnPropertyChanged(nameof(EnEjecucion)); OnPropertyChanged(nameof(EnInforme)); OnPropertyChanged(nameof(EnEventos)); OnPropertyChanged(nameof(TextoPaso));
        AlCambiarPaso(value);
    }
    partial void OnOcupadoChanged(bool value) => OnPropertyChanged(nameof(NoOcupado));
    partial void OnCancelandoChanged(bool value) => OnPropertyChanged(nameof(NoCancelando));
    partial void OnTextoPreguntaChanged(string? value) { OnPropertyChanged(nameof(HayPregunta)); OnPropertyChanged(nameof(SinPregunta)); }
    partial void OnClaseVisualChanged(string value) { OnPropertyChanged(nameof(ResultadoOk)); OnPropertyChanged(nameof(ResultadoAviso)); OnPropertyChanged(nameof(ResultadoError)); }
    partial void OnAvisoDeshacerChanged(string? value) => OnPropertyChanged(nameof(HayAvisoDeshacer));
    partial void OnDetalleChanged(string? value) => OnPropertyChanged(nameof(HayDetalle));

    protected virtual void AlCambiarPaso(PasoMasivo paso) { }

    /// <summary>Lanza la ejecución del asistente (la operación concreta). Debe respetar el token (Cancelar) y devolver SIEMPRE un resultado honesto.</summary>
    protected abstract Task<ResultadoMasivo> EjecutarAsync(IProgress<ProgresoMasivo> progreso, Func<int, Task<bool>> colocarGenerados, CancellationToken cancelar);

    /// <summary>La pregunta de «Cancelar con trabajo hecho»: una sola, con dos botones (Colocar / Descartar). Texto de la versión del escritorio (<c>BulkPlacementDialog</c>).</summary>
    protected abstract string TextoColocar(int n);

    public static Action<string, Exception>? FalloNoControlado { get; set; }

    /// <summary>Pasa a la ejecución y la lanza sin esperar, SIN perder sus excepciones (si escapa alguna se anota). Devuelve la tarea de la ejecución (para las pruebas).</summary>
    protected Task Lanzar()
    {
        Paso = PasoMasivo.Ejecucion;
        var tarea = IniciarAsync();
        _ = tarea.ContinueWith(t => FalloNoControlado?.Invoke("Fallo en una operación masiva de Pokekeep", t.Exception!.GetBaseException()), CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        return Ejecucion = tarea;
    }

    /// <summary>La tarea de vigilancia de la última ejecución lanzada (null si no se ha lanzado ninguna): las pruebas la esperan.</summary>
    public Task? Ejecucion { get; protected set; }

    /// <summary>
    /// Un trabajo del motor ANTES de la ejecución (la búsqueda de versiones de evento): el mismo progreso real y el mismo Cancelar que la ejecución, pero sin informe ni pregunta; devuelve su resultado y el asistente
    /// decide a qué paso va.
    /// </summary>
    protected async Task<T> TrabajarAsync<T>(Func<IProgress<ProgresoMasivo>, CancellationToken, Task<T>> trabajo)
    {
        Paso = PasoMasivo.Ejecucion;
        Preparar();
        try { return await trabajo(new Progress<ProgresoMasivo>(AlProgresar), _cts!.Token).ConfigureAwait(true); }
        finally { _cts?.Dispose(); _cts = null; Ocupado = false; Cancelando = false; }
    }

    private void Preparar()
    {
        Ocupado = true; Cancelando = false; Indeterminado = true; Fraccion = 0; Recientes = []; TextoProgreso = IdiomaInterfaz.T("AccionHoja.PreparandoMotor"); Transcurrido = "0 s";
        _cts = new CancellationTokenSource();
    }

    /// <summary>La ejecución completa (las pruebas la esperan).</summary>
    public async Task IniciarAsync()
    {
        Preparar();
        var progreso = new Progress<ProgresoMasivo>(AlProgresar);
        ResultadoMasivo resultado;
        try
        {
            resultado = await EjecutarAsync(progreso, async n =>
            {
                _pregunta = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                TextoPregunta = TextoColocar(n);
                try { return await _pregunta.Task.ConfigureAwait(true); }
                finally { TextoPregunta = null; }
            }, _cts.Token).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            FalloNoControlado?.Invoke(IdiomaInterfaz.T("MasivasViewModels.FalloOperacionMasivaPokekeep"), ex);
            resultado = new ResultadoMasivo(ClaseMasiva.FalloDelMotor, IdiomaInterfaz.T("MasivasViewModels.ErrorInesperadoPokekeep"), IdiomaInterfaz.F("AccionHoja.ModificadoNada", ex.GetType().Name, ex.Message), InformeMasivo.Vacio(IdiomaInterfaz.T("MasivasViewModels.OperacionMasiva")), 0, TimeSpan.Zero, null);
        }
        finally
        {
            _cts?.Dispose(); _cts = null; Ocupado = false; Cancelando = false;
            _pregunta?.TrySetResult(false);   // si la operación terminó sin que se respondiera (Cancelar duro desde la notificación), la pregunta se cierra: no se coloca nada
        }
        Terminar(resultado);
    }

    internal void AlProgresar(ProgresoMasivo p)
    {
        TextoProgreso = p.Texto;
        Indeterminado = p.Total <= 0 || p.Hechos <= 0;   // marquesina hasta el primer avance real
        if (p.Fraccion is { } f) Fraccion = f;
        Transcurrido = TextosAccion.Segundos(p.Transcurrido);
        Recientes = p.Recientes;
    }

    /// <summary>El informe trae ilegales sin legalizar (<c>BulkStatus.NotLegalFirst</c>): el lote no los ha tocado.</summary>
    public static bool TieneIlegalesSinLegalizar(ResultadoMasivo r) => r.Informe.Filas.Any(f => f.Estado == "NotLegalFirst");

    private void Terminar(ResultadoMasivo r)
    {
        Resultado = r;
        Titular = r.Titular; Detalle = r.Detalle; Duracion = TextosAccion.Segundos(r.Duracion);
        (ClaseVisual, GlifoResultado) = r.Clase switch
        {
            // Red de seguridad (investigación de uso9): ilegales que el lote no tocó porque faltaba «Legalizar los ilegales primero» no
            // son una «información» neutra: se avisa, y el titular del núcleo ya dice cuántos.
            _ when TieneIlegalesSinLegalizar(r) => ("aviso", "TriangleAlert"),
            ClaseMasiva.Aplicado => ("ok", "CircleCheck"),
            ClaseMasiva.AplicadoNecesitaHome => ("aviso", "TriangleAlert"),
            ClaseMasiva.Cancelado or ClaseMasiva.SinCambios => ("", "Info"),
            _ => ("error", "CircleAlert"),
        };
        Informe = new InformeMasivoViewModel(r.Informe, _portapapeles, _documentos, _informar);
        PuedeDeshacer = r.Deshacer?.PuedeDeshacer() == true;
        Deshecha = false; AvisoDeshacer = null;
        Paso = PasoMasivo.Informe;
    }

    [RelayCommand]
    private void Cancelar()
    {
        if (!Ocupado || Cancelando) return;
        Cancelando = true;
        TextoProgreso = TextosAccion.Cancelando;
        try { _cts?.Cancel(); } catch (ObjectDisposedException) { }
    }

    [RelayCommand] private void Colocar() => _pregunta?.TrySetResult(true);
    [RelayCommand] private void Descartar() => _pregunta?.TrySetResult(false);

    /// <summary>UN Deshacer del lote entero. Si el usuario tocó alguna casilla después, se niega con el texto del núcleo («Has cambiado N de las casillas después...»).</summary>
    [RelayCommand]
    private void Deshacer()
    {
        if (Resultado?.Deshacer is not { } d) return;
        if (!d.PuedeDeshacer())
        {
            AvisoDeshacer = d.Motivo ?? TextosAccion.UndoNoPosible;
            PuedeDeshacer = false;
            return;
        }
        if (d.Deshacer())
        {
            Deshecha = true; PuedeDeshacer = false; AvisoDeshacer = null;
            _informar(IdiomaInterfaz.T("MasivasViewModels.LoteDeshechoTodasCasillas"), "ok", "CircleCheck");
        }
        else { AvisoDeshacer = d.Motivo ?? TextosAccion.UndoNoPosible; PuedeDeshacer = false; }
    }

    public bool NoDeshecha => !Deshecha;
    partial void OnDeshechaChanged(bool value) => OnPropertyChanged(nameof(NoDeshecha));
}

// ============================================================================================ Edición masiva
/// <summary>Una operación de la edición masiva con su interruptor.</summary>
public sealed partial class OpcionOperacionVm : ObservableObject
{
    private readonly Action _cambiado;
    public OpcionOperacionVm(OperacionMasivaInfo info, Action cambiado) { Info = info; _cambiado = cambiado; _marcada = info.PorDefecto; }
    public OperacionMasivaInfo Info { get; }
    public string Clave => Info.Clave;
    public string Titulo => Info.Titulo;
    public string Ayuda => Info.Ayuda;
    public bool EsAviso => Info.EsAviso;
    public bool NoEsAviso => !Info.EsAviso;
    public string Accesible => Info.Accesible;
    [ObservableProperty] private bool _marcada;
    public string ValorTexto => Marcada ? IdiomaInterfaz.T("Comun.Si") : IdiomaInterfaz.T("Comun.No");
    partial void OnMarcadaChanged(bool value) { OnPropertyChanged(nameof(ValorTexto)); _cambiado(); }
}

/// <summary>Uno de los cuatro ámbitos como chip elegible (los nombres del escritorio).</summary>
public sealed partial class AmbitoOpcionVm(AmbitoMasivo ambito, string texto, Action<AmbitoMasivo> elegir) : ObservableObject
{
    public AmbitoMasivo Ambito { get; } = ambito;
    public string Texto { get; } = texto;
    [ObservableProperty] private bool _activo;
    public string Accesible => Activo ? IdiomaInterfaz.F("MasivasViewModels.AmbitoElegido", Texto) : IdiomaInterfaz.F("MasivasViewModels.ElegirAmbito", Texto);
    partial void OnActivoChanged(bool value) => OnPropertyChanged(nameof(Accesible));
    [RelayCommand] private void Elegir() => elegir(Ambito);
}

/// <summary>Una caja de la lista de «Cajas concretas» con su casilla de verificación.</summary>
public sealed partial class CajaOpcionVm : ObservableObject
{
    private readonly Action _cambiada;
    public CajaOpcionVm(CajaMasiva caja, bool marcada, Action cambiada) { Caja = caja; _marcada = marcada; _cambiada = cambiada; }
    public CajaMasiva Caja { get; }
    public int Indice => Caja.Indice;
    public string Nombre => Caja.Nombre;
    public string Cuenta => Caja.Pokemon == 1 ? "1 Pokémon" : IdiomaInterfaz.F("Cajas.Pokemon2", Caja.Pokemon);
    public string Accesible => $"{Nombre}, {Cuenta}";
    [ObservableProperty] private bool _marcada;
    private bool _callada;
    partial void OnMarcadaChanged(bool value) { if (!_callada) _cambiada(); }
    /// <summary>Marca o desmarca sin avisar (Todas / Ninguna recalculan una sola vez al final).</summary>
    internal void Poner(bool valor) { _callada = true; try { Marcada = valor; } finally { _callada = false; } }
}

/// <summary>Un Pokémon del selector «Elegir Pokémon…» (casilla, Pokémon y nivel) con su marca.</summary>
public sealed partial class CandidatoVm(CandidatoMasivo candidato, bool marcado, string nivel) : ObservableObject
{
    public CandidatoMasivo Candidato { get; } = candidato;
    public string Pokemon => Candidato.Pokemon;
    public string Casilla => Candidato.TextoCasilla;
    public string Nivel { get; } = nivel;
    public string Accesible => Candidato.Accesible;
    [ObservableProperty] private bool _marcado = marcado;
}

/// <summary>Una fila de la vista previa de «Preferir la versión de evento»: solo lo propuesto se puede marcar; al tocarla se despliega su detalle (el del núcleo).</summary>
public sealed partial class FilaEventoVm : ObservableObject
{
    private readonly Action _cambiada;
    public FilaEventoVm(FilaEventoMasivo fila, Action cambiada) { Fila = fila; _cambiada = cambiada; _incluida = fila.Propuesto; }
    public FilaEventoMasivo Fila { get; }
    public bool Marcable => Fila.Propuesto;
    public string Titulo => $"{Fila.Pokemon} · {Fila.Casilla}";
    /// <summary>«legendario · salvaje»: categoría de la especie en la función y origen actual.</summary>
    public string Subtitulo => string.IsNullOrEmpty(Fila.Categoria) ? Fila.OrigenActual : IdiomaInterfaz.F("MasivasViewModels.OrigenActual", Fila.Categoria, Fila.OrigenActual);
    /// <summary>Lo propuesto: «Evento «…» · 2008 · español»; lo demás, vacío.</summary>
    public string Propuesta => !Fila.Propuesto ? string.Empty : string.Join(" · ", new[] { Fila.Evento, Fila.Ano?.ToString(System.Globalization.CultureInfo.InvariantCulture), Fila.Idioma }.Where(x => !string.IsNullOrEmpty(x)));
    public bool HayPropuesta => Propuesta.Length > 0;
    public string Estado => Fila.Columna;
    public bool EsAviso => Fila.Propuesto && Fila.NecesitaHome;
    public bool EsError => EstadosEvento.EsError(Fila.Estado);
    public bool Atenuada => !Fila.Propuesto && !EsError;
    /// <summary>Lo que no se puede cambiar se pinta atenuado (en el escritorio, en gris).</summary>
    public double Opacidad => Atenuada ? 0.75 : 1;
    public IReadOnlyList<string> Detalle => Fila.Detalle;
    public string Accesible => $"{Titulo}. {Fila.Descripcion}. {Fila.Columna}" + (Marcable ? Incluida ? IdiomaInterfaz.T("MasivasViewModels.MarcadoCambiara") : IdiomaInterfaz.T("MasivasViewModels.MarcarQueda") : string.Empty);
    [ObservableProperty] private bool _incluida;
    [ObservableProperty] private bool _abierta;
    private bool _callada;
    partial void OnIncluidaChanged(bool value)
    {
        if (!Fila.Propuesto && value) { Incluida = false; return; }   // lo que no se puede cambiar no se marca (como el escritorio)
        OnPropertyChanged(nameof(Accesible));
        if (!_callada) _cambiada();
    }
    /// <summary>Marca o desmarca sin avisar (Marcar todos / Desmarcar todos actualizan el botón una sola vez al final).</summary>
    internal void Poner(bool valor) { if (!Fila.Propuesto) return; _callada = true; try { Incluida = valor; } finally { _callada = false; } }
    [RelayCommand] private void Alternar() => Abierta = !Abierta;
}

/// <summary>
/// «Edición masiva» (R-007): ámbito (todo, cajas concretas con su lista, Pokémon marcados con «Elegir Pokémon…» o este Pokémon), las operaciones con su valor por defecto, procesos en paralelo y tiempo estimado,
/// «Preferir la versión de evento» con su vista previa, ejecución por el motor con progreso real y Cancelar, informe por Pokémon y un único Deshacer. Piloto de la F9-0, completado en el bloque 9a; el servicio en
/// primer plano llega con el 9c. Todos los textos de la ventana salen del núcleo (<see cref="IMasivas.TextosEdicion"/>).
/// </summary>
public sealed partial class EdicionMasivaViewModel : AsistenteMasivoViewModel
{
    /// <summary>Pokémon que el selector pinta de golpe; el resto con «Mostrar más» (una partida llena son 900+).</summary>
    public const int CandidatosPorPagina = 60;

    private readonly IMasivas _masivas;
    private readonly Sesion _sesion;
    private readonly Func<int> _procesosDelAjuste;
    private readonly Action<int>? _guardarProcesos;
    private readonly ISlotInfo? _este;
    private List<ISlotInfo> _marcadas;
    private List<CandidatoVm> _candidatos = [];
    private int _paginasCandidatos = 1;
    private bool _cargando = true;

    public EdicionMasivaViewModel(IMasivas masivas, Sesion sesion, AlcanceMasivo alcance, Func<int> procesosDelAjuste, Action volver, Action<string, string, string> informar,
        Func<IPortapapeles?> portapapeles, Func<IAlmacenDocumentos?> documentos, Action<int>? guardarProcesos = null) : base(volver, informar, portapapeles, documentos)
    {
        _masivas = masivas; _sesion = sesion; _procesosDelAjuste = procesosDelAjuste; _guardarProcesos = guardarProcesos;
        Textos = masivas.TextosEdicion;
        _este = alcance.Ambito == AmbitoMasivo.Este ? alcance.Casillas?.FirstOrDefault() : null;
        _marcadas = alcance.Ambito == AmbitoMasivo.Marcados ? [.. alcance.Casillas ?? []] : [];
        var marcadasCaja = alcance.Ambito == AmbitoMasivo.Cajas ? (alcance.Cajas ?? []).ToHashSet() : [];
        _incluirEquipoCajas = alcance.IncluirEquipo;
        CajasLista = [.. masivas.Cajas(sesion).Select(c => new CajaOpcionVm(c, marcadasCaja.Contains(c.Indice), Recalcular))];
        // «Este Pokémon» solo existe si se entró desde una casilla (como el radio del escritorio, invisible sin casilla). Los del escritorio van primero y en su orden;
        // «Solo los ilegales» (QA uso6, propio de Pokekeep) va detrás para no romper esa paridad.
        Ambitos = [.. new[] { AmbitoMasivo.Todo, AmbitoMasivo.Cajas, AmbitoMasivo.Marcados, AmbitoMasivo.Este, AmbitoMasivo.Ilegales }.Where(a => a != AmbitoMasivo.Este || _este is not null)
            .Select(a => new AmbitoOpcionVm(a, Textos.NombreAmbito(a), ElegirAmbito))];
        _ambito = alcance.Ambito == AmbitoMasivo.Este && _este is null ? AmbitoMasivo.Todo : alcance.Ambito;
        _procesosElegidos = Math.Clamp(procesosDelAjuste(), Textos.ProcesosMinimo, Textos.ProcesosMaximo);
        Operaciones = [.. masivas.Operaciones().Select(o => new OpcionOperacionVm(o, Recalcular))];
        AjustarLegalizarAlAmbito(_ambito);
        foreach (var a in Ambitos) a.Activo = a.Ambito == _ambito;
        _cargando = false;
        Recalcular();
    }

    private readonly bool _incluirEquipoCajas;

    public override string Titulo => IdiomaInterfaz.T("Menu.Mas.Masivas.Titulo");
    protected override string NombreAlcance => IdiomaInterfaz.T("MasivasViewModels.Edita");
    public TextosEdicionMasiva Textos { get; }
    public IReadOnlyList<OpcionOperacionVm> Operaciones { get; }
    public IReadOnlyList<AmbitoOpcionVm> Ambitos { get; }
    public IReadOnlyList<CajaOpcionVm> CajasLista { get; }

    /// <summary>El ámbito como lo resuelve ahora la pantalla (lo que se mandará al motor).</summary>
    public AlcanceMasivo Alcance => Ambito switch
    {
        AmbitoMasivo.Todo => AlcanceMasivo.DeTodo(),
        AmbitoMasivo.Ilegales => AlcanceMasivo.DeIlegales(),
        AmbitoMasivo.Cajas => AlcanceMasivo.DeCajas(CajasMarcadas, _incluirEquipoCajas),
        AmbitoMasivo.Marcados => AlcanceMasivo.DeMarcados(_marcadas),
        _ => AlcanceMasivo.DeEste(_este!),
    };

    [ObservableProperty] private AmbitoMasivo _ambito;
    /// <summary>
    /// «Solo los ilegales» trae marcado «Legalizar los ilegales primero» (QA del propietario en uso9: sin ello el lote no tocaba a
    /// los ilegales, que seguían marcados; causa medida en docs/android/inv-ilegales-masiva.md). Al salir de ese ámbito vuelve a su
    /// valor por defecto si fue la pantalla quien lo marcó. El catálogo del escritorio no cambia.
    /// </summary>
    private void AjustarLegalizarAlAmbito(AmbitoMasivo ambito)
    {
        if (Operaciones?.FirstOrDefault(o => o.Clave == ClaveLegalizar) is not { } legalizar) return;
        if (ambito == AmbitoMasivo.Ilegales && !legalizar.Marcada) { legalizar.Marcada = true; _legalizarPuestoPorAmbito = true; }
        else if (ambito != AmbitoMasivo.Ilegales && _legalizarPuestoPorAmbito) { legalizar.Marcada = legalizar.Info.PorDefecto; _legalizarPuestoPorAmbito = false; }
    }

    private bool _legalizarPuestoPorAmbito;
    /// <summary>Clave de «Legalizar los ilegales primero» en el catálogo del núcleo (<c>AdjustOperationNames.Legalize</c>; la interfaz no depende de Contracts).</summary>
    private const string ClaveLegalizar = "Legalize";

    partial void OnAmbitoChanged(AmbitoMasivo value) { AjustarLegalizarAlAmbito(value); foreach (var a in Ambitos) a.Activo = a.Ambito == value; OnPropertyChanged(nameof(NombreAmbito)); OnPropertyChanged(nameof(VerCajas)); OnPropertyChanged(nameof(VerElegir)); OnPropertyChanged(nameof(TextoCajasAyuda)); Recalcular(); }

    /// <summary>Los mismos nombres de ámbito que el escritorio (<c>BulkEditForm</c>), del núcleo.</summary>
    public string NombreAmbito => Textos.NombreAmbito(Ambito);
    public IReadOnlyList<int> CajasMarcadas => [.. CajasLista.Where(c => c.Marcada).Select(c => c.Indice)];
    /// <summary>La lista de cajas se ve con «Cajas concretas» y con «Pokémon marcados» (allí acota «Elegir Pokémon…»), como en el escritorio.</summary>
    public bool VerCajas => Ambito is AmbitoMasivo.Cajas or AmbitoMasivo.Marcados && CajasLista.Count > 0;
    public bool VerElegir => Ambito == AmbitoMasivo.Marcados;
    public string TextoCajasAyuda => Ambito == AmbitoMasivo.Marcados ? IdiomaInterfaz.T("MasivasViewModels.ComillaAbre") + Textos.ElegirPokemon.TrimEnd('…') + IdiomaInterfaz.T("MasivasViewModels.OfrecePokemonCajasMarcadas") : string.Empty;
    public string TextoMarcados => _marcadas.Count == 1 ? IdiomaInterfaz.T("MasivasViewModels.PokemonMarcado") : IdiomaInterfaz.F("MasivasViewModels.PokemonMarcados", _marcadas.Count);

    private void ElegirAmbito(AmbitoMasivo a) { if (!Ocupado) Ambito = a; }

    [RelayCommand] private void TodasLasCajas() { foreach (var c in CajasLista) c.Poner(true); Recalcular(); }
    [RelayCommand] private void NingunaCaja() { foreach (var c in CajasLista) c.Poner(false); Recalcular(); }

    // ---- Procesos en paralelo y tiempo estimado ----
    [ObservableProperty] private int _procesosElegidos;
    partial void OnProcesosElegidosChanged(int value) { OnPropertyChanged(nameof(Procesos)); OnPropertyChanged(nameof(TextoProcesos)); OnPropertyChanged(nameof(TextoProcesosElegidos)); Recalcular(); }
    public string TextoProcesosElegidos => ProcesosElegidos.ToString(System.Globalization.CultureInfo.InvariantCulture);
    /// <summary>Los procesos que de verdad trabajan: lo elegido, recortado al máximo del dispositivo (1 mientras no haya pool de procesos del motor: F9X-01).</summary>
    public int Procesos => Math.Clamp(ProcesosElegidos, 1, Math.Max(1, _masivas.ProcesosMaximos));
    public string TextoProcesos => Procesos < ProcesosElegidos ? IdiomaInterfaz.F("Procesos.ALaVezMaximo", IdiomaInterfaz.P("Procesos.N", Procesos)) : IdiomaInterfaz.F("Procesos.EnParalelo", IdiomaInterfaz.P("Procesos.N", Procesos));
    [RelayCommand] private void MasProcesos() { if (ProcesosElegidos < Textos.ProcesosMaximo) ProcesosElegidos++; }
    [RelayCommand] private void MenosProcesos() { if (ProcesosElegidos > Textos.ProcesosMinimo) ProcesosElegidos--; }
    public bool PuedeMasProcesos => ProcesosElegidos < Textos.ProcesosMaximo;
    public bool PuedeMenosProcesos => ProcesosElegidos > Textos.ProcesosMinimo;

    // ---- Preferir la versión de evento ----
    [ObservableProperty] private bool _preferirEvento;
    partial void OnPreferirEventoChanged(bool value) { OnPropertyChanged(nameof(TextoPaso)); OnPropertyChanged(nameof(ValorEvento)); Recalcular(); }
    public string ValorEvento => PreferirEvento ? IdiomaInterfaz.T("Comun.Si") : IdiomaInterfaz.T("Comun.No");
    [ObservableProperty] private PlanEventosMasivo? _planEventos;
    [ObservableProperty] private IReadOnlyList<FilaEventoVm> _filasEventos = [];
    [ObservableProperty] private string _textoAplicarEventos = string.Empty;
    [ObservableProperty] private bool _puedeAplicarEventos;
    [ObservableProperty] private string? _avisoEventos;
    [ObservableProperty] private bool _buscandoEventos;
    public bool HayAvisoEventos => !string.IsNullOrEmpty(AvisoEventos);
    partial void OnAvisoEventosChanged(string? value) => OnPropertyChanged(nameof(HayAvisoEventos));
    partial void OnBuscandoEventosChanged(bool value) => OnPropertyChanged(nameof(TextoPaso));
    public string ResumenEventos => PlanEventos?.Resumen ?? string.Empty;
    public bool HayPropuestas => PlanEventos?.Propuestas.Count > 0;
    partial void OnPlanEventosChanged(PlanEventosMasivo? value) { OnPropertyChanged(nameof(ResumenEventos)); OnPropertyChanged(nameof(HayPropuestas)); }
    public IReadOnlyList<int> EventosAceptados => [.. FilasEventos.Where(f => f.Marcable && f.Incluida).Select(f => f.Fila.Indice)];

    /// <summary>Con «Preferir la versión de evento» el asistente tiene un paso más: la vista previa de los eventos.</summary>
    public override string TextoPaso => !PreferirEvento ? base.TextoPaso : Paso switch
    {
        PasoMasivo.Alcance => IdiomaInterfaz.F("MasivasViewModels.PasoDe2", NombreAlcance),
        PasoMasivo.VistaPrevia => IdiomaInterfaz.T("MasivasViewModels.PasoVistaPrevia2"),
        PasoMasivo.Ejecucion when BuscandoEventos => IdiomaInterfaz.T("MasivasViewModels.PasoBuscandoVersionesEvento"),
        PasoMasivo.Eventos => IdiomaInterfaz.T("MasivasViewModels.PasoDe3") + Textos.TituloEventos,
        PasoMasivo.Ejecucion => IdiomaInterfaz.T("MasivasViewModels.PasoEjecucion2"),
        _ => IdiomaInterfaz.T("MasivasViewModels.Resultado"),
    };

    // ---- Recuento ----
    [ObservableProperty] private ResumenAmbito? _resumen;
    [ObservableProperty] private string? _motivo;
    [ObservableProperty] private string _zonasTexto = string.Empty;
    [ObservableProperty] private bool _puedeAvanzar;
    [ObservableProperty] private string _estimado = string.Empty;
    [ObservableProperty] private IReadOnlyList<string> _lineasVistaPrevia = [];

    public bool HayMotivo => !string.IsNullOrEmpty(Motivo);
    public bool HayZonas => !string.IsNullOrEmpty(ZonasTexto);
    public bool HayEstimado => !string.IsNullOrEmpty(Estimado);
    /// <summary>El recuento del núcleo; «No hay ningún Pokémon que editar» solo con Todo y Este (con cajas o marcados vacíos el recuento ya lo dice), igual que el escritorio.</summary>
    public string TextoResumen => Resumen is null ? string.Empty : Resumen.Incluidos == 0 && Resumen.Ambito is AmbitoMasivo.Todo or AmbitoMasivo.Este ? Textos.NadaQueEditar : Resumen.Texto;
    public IReadOnlyList<string> Marcadas => [.. Operaciones.Where(o => o.Marcada).Select(o => o.Clave)];

    partial void OnMotivoChanged(string? value) => OnPropertyChanged(nameof(HayMotivo));
    partial void OnZonasTextoChanged(string value) => OnPropertyChanged(nameof(HayZonas));
    partial void OnEstimadoChanged(string value) => OnPropertyChanged(nameof(HayEstimado));
    partial void OnResumenChanged(ResumenAmbito? value) => OnPropertyChanged(nameof(TextoResumen));

    private void Recalcular()
    {
        if (_cargando) return;
        Motivo = _masivas.MotivoEdicion(_sesion);
        Resumen = _masivas.Resolver(_sesion, Alcance);
        var zonas = Resumen is { Ambito: AmbitoMasivo.Todo } r ? r.Zonas.Where(z => z.Pokemon > 0).ToList() : [];
        ZonasTexto = zonas.Count > 0 ? Textos.PrefijoZonas + string.Join("; ", zonas.Select(z => z.Texto)) + "." : string.Empty;
        var n = Resumen?.Incluidos ?? 0;
        Estimado = n == 0 ? string.Empty : IdiomaInterfaz.F("MasivasViewModels.TiempoEstimado", TextosMasivas.Duracion(_masivas.Estimar(n, Procesos)));
        // Como el escritorio: hace falta algo en el ámbito y alguna operación o la versión de evento.
        PuedeAvanzar = Motivo is null && n > 0 && (Operaciones.Any(o => o.Marcada) || PreferirEvento);
        OnPropertyChanged(nameof(Marcadas)); OnPropertyChanged(nameof(TextoMarcados));
        OnPropertyChanged(nameof(PuedeMasProcesos)); OnPropertyChanged(nameof(PuedeMenosProcesos));
    }

    // ---- «Elegir Pokémon…» ----
    [ObservableProperty] private bool _eligiendo;
    [ObservableProperty] private IReadOnlyList<CandidatoVm> _candidatosVisibles = [];
    [ObservableProperty] private bool _hayMasCandidatos;
    [ObservableProperty] private string _textoMasCandidatos = string.Empty;
    [ObservableProperty] private string _resumenEleccion = string.Empty;
    public bool NoEligiendo => !Eligiendo;
    public bool EnAlcanceSinElegir => EnAlcance && !Eligiendo;
    partial void OnEligiendoChanged(bool value) { OnPropertyChanged(nameof(NoEligiendo)); OnPropertyChanged(nameof(EnAlcanceSinElegir)); }

    [RelayCommand]
    private void ElegirPokemon()
    {
        if (Ambito != AmbitoMasivo.Marcados || Ocupado) return;
        var ya = _marcadas.Select(DescribirCasilla).ToHashSet();
        _candidatos = [.. _masivas.Candidatos(_sesion, CajasMarcadas).Select(c => new CandidatoVm(c, ya.Contains(DescribirCasilla(c.Casilla)), Textos.TextoNivel(c.Nivel)))];
        foreach (var c in _candidatos) c.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(CandidatoVm.Marcado)) ActualizarResumenEleccion(); };
        _paginasCandidatos = 1;
        PintarCandidatos();
        Eligiendo = true;
    }

    private static string DescribirCasilla(ISlotInfo s) => s is SlotInfoBox b ? $"{b.Box}:{b.Slot}" : $"E:{s.Slot}";
    private void ActualizarResumenEleccion() => ResumenEleccion = IdiomaInterfaz.F("MasivasViewModels.Marcados", _candidatos.Count(c => c.Marcado), _candidatos.Count);

    private void PintarCandidatos()
    {
        var n = Math.Min(_candidatos.Count, _paginasCandidatos * CandidatosPorPagina);
        CandidatosVisibles = [.. _candidatos.Take(n)];
        HayMasCandidatos = _candidatos.Count > n;
        TextoMasCandidatos = HayMasCandidatos ? IdiomaInterfaz.F("CompletarJuego.MostrarRestantes", _candidatos.Count - n) : string.Empty;
        ActualizarResumenEleccion();
    }

    [RelayCommand] private void MostrarMasCandidatos() { _paginasCandidatos++; PintarCandidatos(); }
    [RelayCommand] private void MarcarTodosCandidatos() { foreach (var c in _candidatos) c.Marcado = true; }
    [RelayCommand] private void DesmarcarTodosCandidatos() { foreach (var c in _candidatos) c.Marcado = false; }

    [RelayCommand]
    private void AceptarEleccion()
    {
        if (!Eligiendo) return;
        _marcadas = [.. _candidatos.Where(c => c.Marcado).Select(c => c.Candidato.Casilla)];
        Eligiendo = false;
        Recalcular();
    }

    [RelayCommand] private void CancelarEleccion() => Eligiendo = false;

    protected override void AlCambiarPaso(PasoMasivo paso)
    {
        OnPropertyChanged(nameof(EnAlcanceSinElegir));
        if (paso == PasoMasivo.VistaPrevia) ConstruirVistaPrevia();
    }

    private void ConstruirVistaPrevia()
    {
        var l = new List<string>();
        var r = Resumen!;
        l.Add(r.Texto);
        foreach (var o in Operaciones.Where(o => o.Marcada)) l.Add($"{o.Titulo}: {o.Ayuda}");
        if (PreferirEvento) l.Add($"{Textos.PreferirEvento}: {Textos.AyudaEvento}");
        l.Add(IdiomaInterfaz.T("MasivasViewModels.CadaOperacionCompruebaPKHeX"));
        l.Add(IdiomaInterfaz.T("MasivasViewModels.EscribeNadaPartidaHasta"));
        l.Add(TextoProcesos);
        LineasVistaPrevia = l;
    }

    public override bool Atras()
    {
        if (Ocupado) return true;
        if (Eligiendo) { Eligiendo = false; return true; }
        if (Paso == PasoMasivo.Eventos) { VolverDeEventos(); return true; }
        if (Paso == PasoMasivo.VistaPrevia) { AnteriorCommand.Execute(null); return true; }
        return false;
    }

    [RelayCommand]
    private void Siguiente()
    {
        if (Paso == PasoMasivo.Alcance && PuedeAvanzar && !Eligiendo) { AvisoEventos = null; Paso = PasoMasivo.VistaPrevia; }
    }

    [RelayCommand]
    private void Anterior()
    {
        if (Paso == PasoMasivo.VistaPrevia) { Recalcular(); Paso = PasoMasivo.Alcance; }
    }

    [RelayCommand]
    private void Aplicar()
    {
        if (Paso != PasoMasivo.VistaPrevia || Ocupado) return;
        _guardarProcesos?.Invoke(ProcesosElegidos);   // como el escritorio: el valor elegido queda como ajuste
        if (PreferirEvento) { Ejecucion = BuscarEventosAsync(); return; }
        PlanEventos = null; FilasEventos = [];
        _ = Lanzar();
    }

    /// <summary>Busca las versiones de evento con el motor (progreso real y Cancelar) y enseña su vista previa; cancelada o con un problema, vuelve a la vista previa diciendo por qué. No toca la partida.</summary>
    private async Task BuscarEventosAsync()
    {
        BuscandoEventos = true;
        PlanEventosMasivo plan;
        try { plan = await TrabajarAsync((p, c) => _masivas.BuscarVersionesDeEventoAsync(_sesion, Alcance, Marcadas, Procesos, p, c)).ConfigureAwait(true); }
        catch (OperationCanceledException) { plan = new PlanEventosMasivo([], string.Empty, Cancelado: true); }   // un puerto que cancela lanzando: es Cancelar, no un fallo
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            FalloNoControlado?.Invoke(IdiomaInterfaz.T("MasivasViewModels.FalloBuscarVersionesEvento"), ex);
            plan = new PlanEventosMasivo([], string.Empty, Problema: IdiomaInterfaz.F("AccionHoja.ModificadoNada", ex.GetType().Name, ex.Message));
        }
        finally { BuscandoEventos = false; }
        if (plan.Cancelado || plan.Problema is not null)
        {
            AvisoEventos = plan.Cancelado ? IdiomaInterfaz.T("MasivasViewModels.BusquedaCanceladaCambiadoNada") : plan.Problema;
            Paso = PasoMasivo.VistaPrevia;
            return;
        }
        PlanEventos = plan;
        FilasEventos = [.. plan.Filas.Select(f => new FilaEventoVm(f, ActualizarAplicarEventos))];
        ActualizarAplicarEventos();
        Paso = PasoMasivo.Eventos;
    }

    private void ActualizarAplicarEventos()
    {
        (TextoAplicarEventos, PuedeAplicarEventos) = _masivas.AplicarEventos(EventosAceptados.Count, Marcadas.Count > 0);
        OnPropertyChanged(nameof(EventosAceptados));
    }

    [RelayCommand] private void MarcarTodosEventos() { foreach (var f in FilasEventos) f.Poner(true); ActualizarAplicarEventos(); }
    [RelayCommand] private void DesmarcarTodosEventos() { foreach (var f in FilasEventos) f.Poner(false); ActualizarAplicarEventos(); }

    /// <summary>Atrás desde la vista previa de eventos: se descarta lo buscado (como cerrar la ventana en el escritorio) y se vuelve a la vista previa.</summary>
    [RelayCommand]
    private void VolverDeEventos()
    {
        if (Paso != PasoMasivo.Eventos) return;
        PlanEventos = null; FilasEventos = [];
        Paso = PasoMasivo.VistaPrevia;
    }

    [RelayCommand]
    private void AplicarEventos()
    {
        if (Paso != PasoMasivo.Eventos || Ocupado || !PuedeAplicarEventos) return;
        _ = Lanzar();
    }

    protected override string TextoColocar(int n) => IdiomaInterfaz.F("MasivasViewModels.PreparadoCambiosQuieresAplicarlos", n);

    protected override Task<ResultadoMasivo> EjecutarAsync(IProgress<ProgresoMasivo> progreso, Func<int, Task<bool>> colocarGenerados, CancellationToken cancelar)
        => PlanEventos is { } plan
            ? _masivas.EjecutarEdicionAsync(_sesion, Alcance, Marcadas, Procesos, plan, EventosAceptados, progreso, colocarGenerados, cancelar)
            : _masivas.EjecutarEdicionAsync(_sesion, Alcance, Marcadas, Procesos, progreso, colocarGenerados, cancelar);
}
