// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.ViewModels;

public enum FaseAccion { Trabajando, Resultado }

/// <summary>Textos de la hoja de acción: los del núcleo (<c>Textos</c> de AutoLegalidad.App) que la interfaz repite en sitios donde no puede llamar al núcleo; una prueba los compara con la fuente.</summary>
public static class TextosAccion
{
    public static string? DetalleParaJugador(string? detalle) => (detalle?.Contains("el vector completo y cada IV por separado", StringComparison.Ordinal) == true || detalle?.Contains("the full vector and each IV separately", StringComparison.Ordinal) == true)
        ? IdiomaInterfaz.T("AccionHoja.PokemonTieneMejoresIV") : detalle;
    public static string Cancelar => IdiomaInterfaz.T("TextosAccion.Cancelar");
    public static string Cancelando => IdiomaInterfaz.T("TextosAccion.Cancelando");
    public static string VerCambios => IdiomaInterfaz.T("TextosAccion.VerCambios");
    public static string VerDetalles => IdiomaInterfaz.T("TextosAccion.VerDetalles");
    public static string Deshacer => IdiomaInterfaz.T("TextosAccion.Deshacer");
    public static string DeshechoCorrectamente => IdiomaInterfaz.T("TextosAccion.DeshechoCorrectamente");
    public static string UndoNoPosible => IdiomaInterfaz.T("TextosAccion.UndoNoPosible");
    public static string Cerrar => IdiomaInterfaz.T("TextosAccion.Cerrar");
    public static string Preparando => IdiomaInterfaz.T("TextosAccion.Preparando");

    /// <summary>Los segundos como los pone el escritorio (<c>Textos.Segundos</c>): una cifra decimal por debajo de 10 s y entera por encima.</summary>
    public static string Segundos(TimeSpan t) => t.TotalSeconds < 10 ? $"{t.TotalSeconds:0.0} s" : $"{t.TotalSeconds:0} s";

    public static string Titulo(AccionPokemon accion, OrigenAccion origen)
        => origen == OrigenAccion.Libreria ? IdiomaInterfaz.T("Libreria.Vista.12") : accion == AccionPokemon.Legalizar ? IdiomaInterfaz.T("Cajas.LegalizarAutomaticamente") : IdiomaInterfaz.T("Editor.OptimizarStats");
}

/// <summary>
/// Hoja de una acción del motor (Legalizar / Optimizar / Añadir a la caja): progreso real por fases con el tiempo transcurrido y Cancelar y, al terminar, el resultado honesto con «Ver cambios», «Deshacer» y «Cerrar».
/// El Deshacer vive AQUÍ y en el Historial (no en un aviso que desaparece) y solo vale mientras el destino siga como lo dejó Pokekeep. Los éxitos se pueden ocultar con el ajuste «Mostrar resumen»; los fallos y
/// las cancelaciones se muestran siempre.
/// </summary>
public sealed partial class AccionHojaViewModel : HojaViewModel
{
    /// <summary>Con el texto muy grande (x1,3 o mas) los dos botones de abajo no caben uno junto al otro: se apilan (Deshacer encima de Cerrar).</summary>
    public int ColumnasBotones => Design.Movimiento.EscalaTexto >= 1.3 ? 1 : 2;

    /// <summary>Reloj de la hoja (el tiempo transcurrido que ve el usuario). Las pruebas de imagen lo congelan: con un reloj real el texto «0,0 s» pasaba a «0,2 s» si la maquina iba cargada y la captura dejaba de coincidir con su referencia.</summary>
    public static Func<DateTime> Ahora { get; set; } = () => DateTime.UtcNow;

    private readonly AccionPokemon _accion;
    private readonly OrigenAccion _origen;
    private readonly Func<IProgress<ProgresoAccion>, CancellationToken, Task<ResultadoAccion>> _ejecutar;
    private readonly Action<ResultadoAccion>? _terminada;
    private readonly Action? _deshecho;
    private readonly Func<bool>? _mostrarResumen;
    private readonly Action<HojaViewModel>? _abrirHoja;
    private readonly Func<IPortapapeles?>? _portapapeles;
    private readonly CancellationTokenSource _cts = new();
    private readonly SynchronizationContext? _ui = SynchronizationContext.Current;
    private Timer? _reloj;
    private DateTime _inicio;
    private ResultadoAccion? _resultadoFinal;

    public AccionHojaViewModel(AccionPokemon accion, string objetivo, Func<IProgress<ProgresoAccion>, CancellationToken, Task<ResultadoAccion>> ejecutar, Action<ResultadoAccion>? terminada = null, Action? deshecho = null,
        OrigenAccion origen = OrigenAccion.Casilla, Func<bool>? mostrarResumen = null, Action<HojaViewModel>? abrirHoja = null, Func<IPortapapeles?>? portapapeles = null)
    {
        _accion = accion; Objetivo = objetivo; _ejecutar = ejecutar; _terminada = terminada; _deshecho = deshecho; _origen = origen;
        _mostrarResumen = mostrarResumen; _abrirHoja = abrirHoja; _portapapeles = portapapeles;
    }

    public override string Titulo => TextosAccion.Titulo(_accion, _origen);
    public override bool CierraConFondo => Fase == FaseAccion.Resultado;
    public string Objetivo { get; }
    public ResultadoAccion? Resultado => _resultadoFinal;

    [ObservableProperty] private FaseAccion _fase = FaseAccion.Trabajando;
    [ObservableProperty] private string _textoProgreso = IdiomaInterfaz.T("AccionHoja.PreparandoMotor");
    [ObservableProperty] private double _fraccion;
    [ObservableProperty] private bool _indeterminado = true;
    [ObservableProperty] private string _transcurrido = "0,0 s";
    [ObservableProperty] private bool _cancelando;
    [ObservableProperty] private string _titular = string.Empty;
    [ObservableProperty] private string? _detalle;
    [ObservableProperty] private string _claseVisual = string.Empty;
    [ObservableProperty] private string _glifoResultado = "Info";
    [ObservableProperty] private IReadOnlyList<string> _resumen = [];
    [ObservableProperty] private bool _puedeDeshacer;
    [ObservableProperty] private bool _deshecha;
    [ObservableProperty] private bool _hayVerCambios;
    [ObservableProperty] private string _textoVerCambios = TextosAccion.VerCambios;
    [ObservableProperty] private string _duracion = string.Empty;

    public bool Trabajando => Fase == FaseAccion.Trabajando;
    public bool EnResultado => Fase == FaseAccion.Resultado;
    public bool HayResumen => Resumen.Count > 0;
    public bool HayDetalle => !string.IsNullOrWhiteSpace(Detalle);
    public bool ResultadoOk => ClaseVisual == "ok";
    public bool ResultadoAviso => ClaseVisual == "aviso";
    public bool ResultadoError => ClaseVisual == "error";
    public bool ResultadoNeutro => ClaseVisual == string.Empty;
    public bool NoCancelando => !Cancelando;

    partial void OnFaseChanged(FaseAccion value) { OnPropertyChanged(nameof(Trabajando)); OnPropertyChanged(nameof(EnResultado)); OnPropertyChanged(nameof(CierraConFondo)); }
    partial void OnResumenChanged(IReadOnlyList<string> value) => OnPropertyChanged(nameof(HayResumen));
    partial void OnDetalleChanged(string? value) => OnPropertyChanged(nameof(HayDetalle));
    partial void OnCancelandoChanged(bool value) => OnPropertyChanged(nameof(NoCancelando));
    partial void OnClaseVisualChanged(string value) { OnPropertyChanged(nameof(ResultadoOk)); OnPropertyChanged(nameof(ResultadoAviso)); OnPropertyChanged(nameof(ResultadoError)); OnPropertyChanged(nameof(ResultadoNeutro)); }

    private void EnUi(Action a)
    {
        if (_ui is null) a(); else _ui.Post(_ => a(), null);
    }

    /// <summary>Quien registra los fallos que escapan de la hoja (la plataforma lo engancha al registro de fallos de la app). Parametros: contexto y excepcion.</summary>
    public static Action<string, Exception>? FalloNoControlado { get; set; }

    /// <summary>Lanza la acción sin esperar y SIN perder sus excepciones: lo que escape de <see cref="IniciarAsync"/> (p. ej. de la devolucion de llamada de «terminada») se anota. Devuelve la tarea de vigilancia (para las pruebas).</summary>
    public Task Lanzar() => IniciarAsync().ContinueWith(
        t => FalloNoControlado?.Invoke("Fallo en la hoja de una acción de Pokekeep", t.Exception!.GetBaseException()),
        CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);

    /// <summary>
    /// Reloj de 200 ms del tiempo transcurrido. Un <see cref="Timer"/> en marcha es una RAIZ del recolector: con la devolucion de llamada apuntando a la hoja, una hoja «en curso» que nadie cierra (la ventana se destruye, o una
    /// prueba la deja colgada) retenia para siempre a la hoja, a su Shell, a sus vistas y a su ventana con decenas de miles de controles. Medido en el gate visual: 16 hojas asi dejaban vivos 38 ventanas y 30.000 TextBlock, y
    /// como cada <c>Movimiento.Aplicar</c> avisa a TODOS los arboles vivos, cada combinacion de la matriz costaba varias veces mas (la pasada completa pasaba de ~20 min a mas de una hora). Aqui el reloj solo conoce a la hoja
    /// por una referencia debil y se apaga solo cuando la hoja ya no existe.
    /// </summary>
    private sealed class RelojDebil
    {
        private readonly WeakReference<AccionHojaViewModel> _hoja;
        private Timer? _timer;

        private RelojDebil(AccionHojaViewModel hoja) => _hoja = new WeakReference<AccionHojaViewModel>(hoja);

        public static Timer Crear(AccionHojaViewModel hoja)
        {
            var reloj = new RelojDebil(hoja);
            reloj._timer = new Timer(reloj.Latido, null, TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(200));
            return reloj._timer;
        }

        private void Latido(object? _)
        {
            if (!_hoja.TryGetTarget(out var hoja)) { _timer?.Dispose(); return; }
            var ui = hoja._ui;
            // El trabajo pendiente en el hilo de interfaz tampoco retiene a la hoja: solo la nombra por la referencia debil.
            if (ui is null) Actualizar(); else ui.Post(_ => Actualizar(), null);
        }

        private void Actualizar()
        {
            if (_hoja.TryGetTarget(out var hoja) && hoja.Fase == FaseAccion.Trabajando) hoja.Transcurrido = TextosAccion.Segundos(Ahora() - hoja._inicio);
        }
    }

    /// <summary>Lanza la acción y devuelve la tarea (para las pruebas); la hoja se actualiza sola.</summary>
    public async Task IniciarAsync()
    {
        _inicio = Ahora();
        TextoProgreso = TextosAccion.Preparando;
        _reloj = RelojDebil.Crear(this);
        var progreso = new Progress<ProgresoAccion>(p =>
        {
            if (Fase != FaseAccion.Trabajando || Cancelando) return;
            TextoProgreso = p.Texto;
            if (p.Fraccion is { } f) { Indeterminado = false; Fraccion = Math.Clamp(f, 0, 1) * 100; } else Indeterminado = true;
        });
        ResultadoAccion r;
        try { r = await _ejecutar(progreso, _cts.Token); }
        catch (OperationCanceledException) { r = new ResultadoAccion(_accion, ClaseResultado.Cancelado, IdiomaInterfaz.T("AccionHoja.OperacionCanceladaCambiadoNada"), null, [], Ahora() - _inicio, null) { Origen = _origen, Clasificacion = "Cancelled", Estado = "Cancelled" }; }
        catch (Exception ex) { r = new ResultadoAccion(_accion, ClaseResultado.FalloDelMotor, IdiomaInterfaz.T("AccionHoja.ErrorInesperadoInterfazPokekeep"), IdiomaInterfaz.F("AccionHoja.ModificadoNada", ex.GetType().Name, ex.Message), [], Ahora() - _inicio, null) { Origen = _origen, Clasificacion = "EngineFailure", Estado = "InternalError" }; }
        _reloj?.Dispose(); _reloj = null;
        _cts.Dispose();   // la operacion ya acabo: nadie mas cancela (Cancelar tolera el objeto ya liberado)
        Mostrar(r);
    }

    /// <summary>Icono y color como en el escritorio: visto (aplicado, ya legal, sin margen), aviso (HOME), ninguno (cancelado) y fallo (el resto).</summary>
    public static (string Clase, string Glifo) Aspecto(ResultadoAccion r) => r.Clase switch
    {
        ClaseResultado.Aplicado or ClaseResultado.YaCorrecto => ("ok", "CircleCheck"),
        ClaseResultado.AplicadoNecesitaHome or ClaseResultado.SinCambios => ("aviso", "CircleAlert"),
        ClaseResultado.Cancelado => (string.Empty, "Info"),
        ClaseResultado.Ocupado => ("aviso", "CircleAlert"),
        _ => ("error", "TriangleAlert"),
    };

    /// <summary>Un fallo se muestra siempre; también la cancelación. Un éxito solo si el usuario no apagó el resumen.</summary>
    public static bool SeMuestraSiempre(ResultadoAccion r) => r.EsFallo || r.Clase is ClaseResultado.Cancelado or ClaseResultado.Ocupado;

    private void Mostrar(ResultadoAccion r)
    {
        _resultadoFinal = r;
        // Titular con la descripción del destino cuando se aplicó (no en la Librería, cuyo titular ya la lleva).
        Titular = r.Aplicado && r.Origen != OrigenAccion.Libreria && r.Objetivo is { Length: > 0 } t ? $"{r.Titular} · {t}" : r.Titular;
        Detalle = TextosAccion.DetalleParaJugador(r.Detalle); Resumen = r.Resumen;
        (ClaseVisual, GlifoResultado) = Aspecto(r);
        PuedeDeshacer = r.Aplicado && r.Deshacer?.PuedeDeshacer() == true;
        Duracion = TextosAccion.Segundos(r.Duracion.TotalSeconds > 0 ? r.Duracion : Ahora() - _inicio);
        HayVerCambios = r.Cambios.Count > 0 || r.Notas.Count > 0 || !string.IsNullOrEmpty(r.Detalle) || r.Resumen.Count > 0;
        TextoVerCambios = r.Aplicado ? TextosAccion.VerCambios : TextosAccion.VerDetalles;
        Fase = FaseAccion.Resultado;
        _terminada?.Invoke(r);
        if (!SeMuestraSiempre(r) && _mostrarResumen is { } m && !m()) Cerrar();   // «Mostrar resumen» apagado: el éxito no abre resumen (lo hecho sigue en el Historial con su Deshacer)
    }

    [RelayCommand]
    private void Cancelar()
    {
        if (Cancelando) return;
        Cancelando = true; TextoProgreso = TextosAccion.Cancelando;
        try { _cts.Cancel(); } catch (ObjectDisposedException) { }
    }

    /// <summary>«Ver cambios» / «Ver detalles»: la pantalla de cambios (antes, después y motivo) con las notas y la validación independiente. Atrás vuelve a este resultado.</summary>
    [RelayCommand]
    private void VerCambios()
    {
        if (_resultadoFinal is not { } r || _abrirHoja is null) return;
        _abrirHoja(new CambiosHojaViewModel(r, Objetivo, () => _abrirHoja(this), _portapapeles));
    }

    [RelayCommand]
    private void Deshacer()
    {
        if (_resultadoFinal?.Deshacer is not { } d) return;
        if (d.Deshacer())
        {
            Deshecha = true; PuedeDeshacer = false; HayVerCambios = false; Resumen = [];
            Titular = TextosAccion.DeshechoCorrectamente; Detalle = null; ClaseVisual = "ok"; GlifoResultado = "CircleCheck";
            _deshecho?.Invoke();
        }
        else
        {
            PuedeDeshacer = false; Detalle = TextosAccion.UndoNoPosible; ClaseVisual = "aviso"; GlifoResultado = "CircleAlert";
        }
    }
}

/// <summary>Una fila de «Ver cambios» ya lista para pintar: campo, antes, después y motivo (los rasgos relajados, en cursiva).</summary>
public sealed record FilaCambio(CambioCampo Cambio)
{
    public string Campo => Cambio.Campo;
    public string Antes => Cambio.Antes;
    public string Despues => Cambio.Despues;
    public string Motivo => Cambio.Motivo;
    public bool Relajado => Cambio.Relajado;
    public bool HayMotivo => Motivo.Length > 0;
    public string Accesible => Cambio.Accesible;
}

/// <summary>«Cambios de Pokekeep»: qué se tocó (antes, después y motivo, incluidos los rasgos relajados) y el informe de la validación independiente. Es la pantalla del <c>ChangesForm</c> del escritorio.</summary>
public sealed partial class CambiosHojaViewModel : HojaViewModel
{
    private readonly Action _volver;
    private readonly Func<IPortapapeles?>? _portapapeles;

    public CambiosHojaViewModel(ResultadoAccion r, string objetivo, Action volver, Func<IPortapapeles?>? portapapeles = null)
    {
        _volver = volver; _portapapeles = portapapeles;
        Subtitulo = r.Objetivo is { } t ? $"{TextosAccion.Titulo(r.Accion, r.Origen)} · {t} · {TextosAccion.Segundos(r.Duracion)}" : TextosAccion.Titulo(r.Accion, r.Origen);
        Cabecera = r.Aplicado && r.Origen != OrigenAccion.Libreria && r.Objetivo is { Length: > 0 } o ? $"{r.Titular} · {o}" : r.Titular;
        Filas = [.. r.Cambios.Select(c => new FilaCambio(c))];
        // Sin cambios de campos el escritorio pone una fila de guiones con esta frase; en el móvil basta la frase (sin tarjeta vacía): el mismo texto, sin la tabla.
        TextoSinCambios = r.Cambios.Count > 0 ? null : r.Origen == OrigenAccion.Libreria ? IdiomaInterfaz.T("AccionHoja.CambiosCamposPokemonNuevo") : IdiomaInterfaz.T("AccionHoja.CambiosCampos");
        Notas = string.Join(Environment.NewLine, r.Notas);
        HayNotas = Notas.Length > 0;
    }

    public override string Titulo => TextosAccion.VerCambios;
    public string Cabecera { get; }
    public string Subtitulo { get; }
    public IReadOnlyList<FilaCambio> Filas { get; }
    public string? TextoSinCambios { get; }
    public bool HayFilas => Filas.Count > 0;
    public bool HayTextoSinCambios => TextoSinCambios is not null;
    public string Notas { get; }
    public bool HayNotas { get; }
    public bool PuedeCopiar => _portapapeles?.Invoke() is not null;

    [RelayCommand] private void Volver() => _volver();

    [RelayCommand]
    private async Task Copiar()
    {
        if (_portapapeles?.Invoke() is not { } p) return;
        var texto = string.Join(Environment.NewLine, new[] { Cabecera, Subtitulo, string.Empty }.Concat(Filas.Select(f => $"{f.Campo}: {f.Antes} → {f.Despues}. {f.Motivo}")).Concat(TextoSinCambios is null ? [] : [TextoSinCambios]).Concat(["", Notas]));
        await p.CopiarAsync(texto);
    }
}
