// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pokekeep.Mobile.Ui.Ajustes;
using Pokekeep.Mobile.Ui.Almacen;
using Pokekeep.Mobile.Ui.Masivas;
using Pokekeep.Mobile.Ui.Modelo;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.ViewModels;

/// <summary>Una opción de una lista corta («Formas», «Orden») que se pinta como chips: la elegida va marcada.</summary>
public sealed partial class OpcionElegibleVm(int indice, string etiqueta, Action<int> elegir) : ObservableObject
{
    public int Indice { get; } = indice;
    public string Etiqueta { get; } = etiqueta;
    [ObservableProperty] private bool _elegida;
    public string Accesible => Elegida ? IdiomaInterfaz.F("CompletarJuego.Elegida", Etiqueta) : Etiqueta;
    partial void OnElegidaChanged(bool value) => OnPropertyChanged(nameof(Accesible));
    [RelayCommand] private void Elegir() => elegir(Indice);
}

/// <summary>Una fila de «Lo que no se genera, con su motivo»; al tocarla se despliega el motivo COMPLETO (el recuadro de detalle del escritorio).</summary>
public sealed partial class FilaNoSeGeneraVm(FilaNoSeGenera fila) : ObservableObject
{
    public FilaNoSeGenera Fila { get; } = fila;
    public string Nombre => Fila.Nombre;
    public string Etiqueta => Fila.Etiqueta;
    public string Motivo => Fila.Motivo;
    public string Detalle => Fila.Detalle;
    public bool EsError => Fila.EsError;
    public bool EsAviso => Fila.EsAviso;
    public bool EsNeutro => !EsError && !EsAviso;
    public string Accesible => Fila.Accesible;
    [ObservableProperty] private bool _abierta;
    [RelayCommand] private void Alternar() => Abierta = !Abierta;
}

/// <summary>
/// «Completar el juego» (R-006, bloque 9b de la F9): opciones (formas, orden, caja de inicio, omitir lo que ya tengo, preferir la versión de evento, procesos en paralelo y, si hace falta, la casilla de HOME) con el resumen
/// recalculado al momento → vista previa con lo que no se genera y su motivo → ejecución con progreso real, Cancelar y la UNA pregunta → informe con UN Deshacer. Es el segundo uso de <see cref="AsistenteMasivoViewModel"/>;
/// los textos son los del núcleo (<c>LivingDexPreview</c>), los mismos que el diálogo del escritorio. Al pulsar «Generar» las opciones se guardan en los ajustes, como hace el escritorio; volver sin generar no cambia nada
/// (tampoco el ajuste de HOME que se marcara aquí).
/// </summary>
public sealed partial class CompletarJuegoViewModel : AsistenteMasivoViewModel, IDisposable
{
    /// <summary>Filas de «lo que no se genera» que se pintan de golpe; el resto con «Mostrar más» (como el informe).</summary>
    public const int FilasPorPagina = 60;
    public const int ProcesosMinimo = 1, ProcesosMaximo = 8;

    private readonly ICompletarJuego _completar;
    private readonly Sesion _sesion;
    private readonly IAjustesPokekeep _ajustes;
    private readonly CancellationTokenSource _analisis = new();
    private IReadOnlySet<(int Especie, int Forma)>? _tienes;
    private int _paginas = 1;
    private bool _cargando = true;
    private string? _motivoInicial;   // por qué no se puede completar esta partida (no cambia con las opciones); un fallo al calcular la vista previa se enseña en Motivo pero se reintenta al cambiar una opción
    private int _procesosPlanificados;
    private readonly Action<string, string, string> _avisarUsuario;

    public CompletarJuegoViewModel(ICompletarJuego completar, Sesion sesion, IAjustesPokekeep ajustes, Action volver, Action<string, string, string> informar,
        Func<IPortapapeles?> portapapeles, Func<IAlmacenDocumentos?> documentos) : base(volver, informar, portapapeles, documentos)
    {
        _completar = completar; _sesion = sesion; _ajustes = ajustes; _avisarUsuario = informar;
        var a = ajustes.Actual;
        LineaJuego = completar.LineaJuego(sesion);
        Formas = [.. completar.EtiquetasFormas.Select((e, i) => new OpcionElegibleVm(i, e, ElegirForma))];
        Ordenes = [.. completar.EtiquetasOrden(sesion).Select((e, i) => new OpcionElegibleVm(i, e, ElegirOrden))];
        _forma = Math.Clamp((int)a.LivingDexForms, 0, Formas.Count - 1);
        _orden = a.LivingDexOrder == OrdenCompletar.Regional && Ordenes.Count > 1 ? 1 : 0;
        CajasTotal = Math.Max(1, sesion.Guardado.BoxCount);
        _cajaInicio = 1;   // por defecto la caja 1, como el escritorio (diseño §1.6)
        _omitirLoQueTengo = a.LivingDexSkipOwned;
        _preferirEvento = a.PreferEventVersion;
        _incluirHome = a.AllowHomeTrackerless;
        _procesos = Math.Clamp(a.BulkWorkers, ProcesosMinimo, ProcesosMaximo);
        MarcarOpciones();
        Motivo = _motivoInicial = completar.MotivoCompletar(sesion);
        Analizando = Motivo is null;
        Carga = Motivo is null ? CargarAsync() : Task.CompletedTask;
        if (Motivo is not null) { _cargando = false; Recalcular(); }
    }

    public override string Titulo => IdiomaInterfaz.T("Menu.Mas.Completar.Titulo");
    protected override string NombreAlcance => IdiomaInterfaz.T("ExportarCajas.Opciones");

    public string LineaJuego { get; }
    public IReadOnlyList<OpcionElegibleVm> Formas { get; }
    public IReadOnlyList<OpcionElegibleVm> Ordenes { get; }
    public bool HayVariosOrdenes => Ordenes.Count > 1;
    public int CajasTotal { get; }

    /// <summary>El análisis previo de lo que ya tienes (las pruebas lo esperan).</summary>
    public Task Carga { get; }

    [ObservableProperty] private int _forma;
    [ObservableProperty] private int _orden;
    [ObservableProperty] private int _cajaInicio;
    [ObservableProperty] private bool _omitirLoQueTengo;
    [ObservableProperty] private bool _reemplazarTodo;
    [ObservableProperty] private bool _confirmoReemplazo;
    public string ConfirmacionReemplazo => IdiomaInterfaz.F("CompletarJuego.ConfirmarReemplazo", Vista?.Sustituidos ?? 0);
    partial void OnReemplazarTodoChanged(bool value) { ConfirmoReemplazo = false; Recalcular(); }
    partial void OnConfirmoReemplazoChanged(bool value) => Avisar();
    [ObservableProperty] private bool _preferirEvento;
    [ObservableProperty] private bool _incluirHome;
    [ObservableProperty] private int _procesos;
    [ObservableProperty] private string? _motivo;
    [ObservableProperty] private bool _analizando;
    [ObservableProperty] private VistaPreviaCompletar? _vista;
    [ObservableProperty] private IReadOnlyList<FilaNoSeGeneraVm> _filasVisibles = [];
    [ObservableProperty] private bool _hayMasFilas;
    [ObservableProperty] private string _textoMasFilas = string.Empty;

    public bool HayMotivo => !string.IsNullOrEmpty(Motivo);
    public bool NoAnalizando => !Analizando;
    public bool PuedeGenerar => PuedeAvanzar && (!ReemplazarTodo || ConfirmoReemplazo);
    public bool PuedeAvanzar => Motivo is null && !Analizando && Vista?.PuedeGenerar == true;
    public bool MostrarHome => !ReemplazarTodo && Vista?.MostrarHome == true;
    public bool HayFilas => Vista?.HayFilas == true;
    public bool HayEstimado => Vista?.HayEstimado == true;
    public string Resumen => Vista?.Resumen ?? string.Empty;
    public string Capacidad => Vista?.Capacidad ?? string.Empty;
    public string Estimado => Vista?.Estimado ?? string.Empty;
    public string TextoGenerar => Vista?.TextoGenerar ?? IdiomaInterfaz.T("CompletarJuego.Generar");
    public string TituloLista => Vista?.TituloLista ?? string.Empty;
    public string TextoCaja => IdiomaInterfaz.F("HojasMenu.Caja", CajaInicio, CajasTotal);
    public bool PuedeMenosCaja => CajaInicio > 1;
    public bool PuedeMasCaja => CajaInicio < CajasTotal;
    public bool PuedeMenosProcesos => Procesos > ProcesosMinimo;
    public bool PuedeMasProcesos => Procesos < ProcesosMaximo;
    public string ValorOmitir => OmitirLoQueTengo ? IdiomaInterfaz.T("Comun.Si") : IdiomaInterfaz.T("Comun.No");
    public string ValorEvento => PreferirEvento ? IdiomaInterfaz.T("Comun.Si") : IdiomaInterfaz.T("Comun.No");
    public string ValorHome => IncluirHome ? IdiomaInterfaz.T("Comun.Si") : IdiomaInterfaz.T("Comun.No");

    /// <summary>Los procesos que trabajarán de verdad: el valor elegido, sin pasar del máximo de este dispositivo (1 mientras no haya pool de procesos del motor).</summary>
    public int ProcesosEfectivos => Math.Clamp(Procesos, 1, Math.Max(1, _completar.ProcesosMaximos));
    public string TextoProcesos => ProcesosEfectivos < Procesos
        ? IdiomaInterfaz.F("CompletarJuego.ProcesosLimitados", Procesos, IdiomaInterfaz.P("Procesos.N", ProcesosEfectivos))
        : IdiomaInterfaz.F("Procesos.EnParalelo", IdiomaInterfaz.P("Procesos.N", ProcesosEfectivos));

    public OpcionesCompletar Opciones => new((FormasCompletar)Forma, Orden == 1 ? OrdenCompletar.Regional : OrdenCompletar.National, CajaInicio - 1, OmitirLoQueTengo, PreferirEvento, IncluirHome, ReemplazarTodo);

    partial void OnMotivoChanged(string? value) { OnPropertyChanged(nameof(HayMotivo)); Avisar(); }
    partial void OnAnalizandoChanged(bool value) { OnPropertyChanged(nameof(NoAnalizando)); Avisar(); }
    partial void OnFormaChanged(int value) { MarcarOpciones(); Recalcular(); }
    partial void OnOrdenChanged(int value) { MarcarOpciones(); Recalcular(); }
    partial void OnCajaInicioChanged(int value) { OnPropertyChanged(nameof(TextoCaja)); OnPropertyChanged(nameof(PuedeMenosCaja)); OnPropertyChanged(nameof(PuedeMasCaja)); Recalcular(); }
    partial void OnOmitirLoQueTengoChanged(bool value) { OnPropertyChanged(nameof(ValorOmitir)); Recalcular(); }
    partial void OnPreferirEventoChanged(bool value) { OnPropertyChanged(nameof(ValorEvento)); Recalcular(); }
    partial void OnIncluirHomeChanged(bool value) { OnPropertyChanged(nameof(ValorHome)); Recalcular(); }
    partial void OnProcesosChanged(int value)
    {
        OnPropertyChanged(nameof(ProcesosEfectivos)); OnPropertyChanged(nameof(TextoProcesos)); OnPropertyChanged(nameof(PuedeMenosProcesos)); OnPropertyChanged(nameof(PuedeMasProcesos));
        // Solo cambia el tiempo estimado, y solo si los procesos que trabajan de verdad cambian (el máximo del dispositivo recorta el valor): replanificar a ciegas es trabajo de más en el hilo de la interfaz.
        if (ProcesosEfectivos != _procesosPlanificados) Recalcular();
    }
    partial void OnVistaChanged(VistaPreviaCompletar? value)
    {
        ConfirmoReemplazo = false;
        OnPropertyChanged(nameof(ConfirmacionReemplazo));
        foreach (var p in new[] { nameof(MostrarHome), nameof(HayFilas), nameof(HayEstimado), nameof(Resumen), nameof(Capacidad), nameof(Estimado), nameof(TextoGenerar), nameof(TituloLista) }) OnPropertyChanged(p);
        _paginas = 1;
        ReconstruirFilas();
        Avisar();
    }

    private void Avisar() { OnPropertyChanged(nameof(PuedeGenerar)); OnPropertyChanged(nameof(PuedeAvanzar)); }

    private void MarcarOpciones()
    {
        foreach (var f in Formas) f.Elegida = f.Indice == Forma;
        foreach (var o in Ordenes) o.Elegida = o.Indice == Orden;
    }

    private void ElegirForma(int i) { if (!Ocupado && Paso == PasoMasivo.Alcance) Forma = i; }
    private void ElegirOrden(int i) { if (!Ocupado && Paso == PasoMasivo.Alcance) Orden = i; }

    /// <summary>Análisis de lo que ya tienes (fuera del hilo de la interfaz; el «cursor de espera» del escritorio es aquí un aviso con marquesina) y primera vista previa.</summary>
    private async Task CargarAsync()
    {
        try { _tienes = await _completar.AnalizarLoQueTienesAsync(_sesion, _analisis.Token).ConfigureAwait(true); }
        catch (OperationCanceledException) { return; }
        finally { _cargando = false; Analizando = false; }
        Recalcular();
    }

    private void Recalcular()
    {
        if (_cargando) return;
        if (_motivoInicial is not null) { Vista = null; return; }
        try
        {
            _procesosPlanificados = ProcesosEfectivos;
            Vista = _completar.Planificar(_sesion, Opciones, _tienes, _procesosPlanificados);
            Motivo = null;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Planificar corre en el hilo de la interfaz (BD del catálogo, análisis): una excepción aquí saldría por PropertyChanged y tumbaría la app. Se enseña y se reintenta al cambiar una opción.
            _avisarUsuario(IdiomaInterfaz.F("CompletarJuego.PudoCalcularVistaPrevia", ex.GetType().Name), "error", "TriangleAlert");
            Vista = null;
            Motivo = IdiomaInterfaz.T("CompletarJuego.PudoCalcularVistaPrevia2");
        }
    }

    private void ReconstruirFilas()
    {
        var filas = Vista?.Filas ?? [];
        var n = Math.Min(filas.Count, _paginas * FilasPorPagina);
        FilasVisibles = [.. filas.Take(n).Select(f => new FilaNoSeGeneraVm(f))];
        HayMasFilas = filas.Count > n;
        TextoMasFilas = HayMasFilas ? IdiomaInterfaz.F("CompletarJuego.MostrarRestantes", filas.Count - n) : string.Empty;
    }

    [RelayCommand] private void MostrarMasFilas() { _paginas++; ReconstruirFilas(); }
    [RelayCommand] private void MenosCaja() { if (PuedeMenosCaja) CajaInicio--; }
    [RelayCommand] private void MasCaja() { if (PuedeMasCaja) CajaInicio++; }
    [RelayCommand] private void MenosProcesos() { if (PuedeMenosProcesos) Procesos--; }
    [RelayCommand] private void MasProcesos() { if (PuedeMasProcesos) Procesos++; }

    [RelayCommand]
    private void Siguiente()
    {
        if (Paso == PasoMasivo.Alcance && PuedeAvanzar) Paso = PasoMasivo.VistaPrevia;
    }

    [RelayCommand]
    private void Anterior()
    {
        if (Paso == PasoMasivo.VistaPrevia) { Recalcular(); Paso = PasoMasivo.Alcance; }
    }

    /// <summary>Atrás del sistema, como en la Edición masiva (9a): mientras trabaja no abandona; desde la vista previa vuelve a las opciones; en el resto cierra la pantalla.</summary>
    public override bool Atras()
    {
        if (Ocupado) return true;
        if (Paso == PasoMasivo.VistaPrevia) { AnteriorCommand.Execute(null); return true; }
        return false;
    }

    /// <summary>«Generar» / «Generar hasta donde quepa»: guarda las opciones en los ajustes (como el escritorio tras aceptar el diálogo, también el de HOME si se marcó aquí) y lanza la ejecución.</summary>
    [RelayCommand]
    private void Generar()
    {
        if (Paso != PasoMasivo.VistaPrevia || Ocupado || !PuedeGenerar) return;
        var a = _ajustes.Actual;
        a.LivingDexForms = (FormasCompletar)Forma;
        a.LivingDexOrder = Orden == 1 ? OrdenCompletar.Regional : OrdenCompletar.National;
        a.LivingDexSkipOwned = OmitirLoQueTengo;
        a.PreferEventVersion = PreferirEvento;
        a.BulkWorkers = Procesos;
        a.AllowHomeTrackerless = IncluirHome;
        // Guardar los ajustes no debe impedir completar el juego (disco lleno, carpeta sin permiso): se avisa en el registro y se sigue.
        try { _ajustes.Guardar(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { _avisarUsuario(IdiomaInterfaz.T("CompletarJuego.PudieronGuardarAjustesCompleta"), "aviso", "CircleAlert"); }
        _ = Lanzar();
    }

    protected override string TextoColocar(int n) => _completar.TextoColocar(n);

    protected override Task<ResultadoMasivo> EjecutarAsync(IProgress<ProgresoMasivo> progreso, Func<int, Task<bool>> colocarGenerados, CancellationToken cancelar)
        => _completar.EjecutarCompletarAsync(_sesion, Vista!, ProcesosEfectivos, progreso, colocarGenerados, cancelar);

    public void Dispose()
    {
        try { _analisis.Cancel(); } catch (ObjectDisposedException) { }
        _analisis.Dispose();
    }
}
