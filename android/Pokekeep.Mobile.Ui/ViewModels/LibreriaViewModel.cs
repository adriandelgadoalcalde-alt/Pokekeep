// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using Pokekeep.Mobile.Ui.Localizacion;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pokekeep.Mobile.Ui.Ajustes;
using Pokekeep.Mobile.Ui.Almacen;
using Pokekeep.Mobile.Ui.Libreria;
using Pokekeep.Mobile.Ui.Modelo;

namespace Pokekeep.Mobile.Ui.ViewModels;

/// <summary>Lo que la Librería necesita de la raíz de la app: hojas, avisos, el motor, los ajustes, el almacenamiento del usuario y la pestaña Pokémon (para elegir casilla). La implementa <see cref="ShellViewModel"/>.</summary>
public interface IAnfitrionLibreria
{
    IAccionesPokekeep? Acciones { get; }
    AjustesPokekeep Ajustes { get; }
    /// <summary>Caja activa de la pestaña Pokémon (donde cae «Añadir a la caja» si no se elige otra casilla).</summary>
    int CajaActiva { get; }
    void AbrirHoja(HojaViewModel hoja);
    void CerrarHoja();
    void Informar(string texto, string clase, string glifo, string? textoAccion = null, ICommand? accion = null);
    IAlmacenDocumentos? Documentos { get; }
    IPortapapeles? Portapapeles { get; }
    /// <summary>Tras añadir (o deshacer lo añadido): refresca las cajas, cuenta el cambio y guarda la copia de recuperación.</summary>
    void TrasCambioDeLibreria();
    /// <summary>Lleva a la pestaña Pokémon en modo «colocar»: se toca una casilla vacía y se llama a <paramref name="colocar"/>; una ocupada se rechaza sin tocar nada.</summary>
    void ElegirCasillaParaColocar(EntradaLibreria entrada, Action<DestinoCasilla> colocar);
}

/// <summary>Una fila de la lista de la Librería: sprite, nombre, origen, estado con su motivo y región. Lo no disponible o no generable sale atenuado y con el motivo exacto.</summary>
public sealed partial class FilaLibreria : ObservableObject
{
    public FilaLibreria(EntradaLibreria e, Pokekeep.Sprites.ModoSprites modo = Pokekeep.Sprites.ModoSprites.Clasico)
    {
        Entrada = e;
        var sprite = SpriteLibreria.Elegir(e.Especie, e.Forma, e.Genero, e.JuegoId, modo);
        Modo = sprite.Modo; Contexto = sprite.Contexto; FormaSprite = sprite.Forma;
        Titulo = e.NombreForma is { Length: > 0 } f ? $"{e.Nombre} ({f})" : e.Nombre;
        Detalle = e.Procedencia.Length > 0 ? $"{e.Origen} · {e.Procedencia}" : e.Origen;
        if (e.Region.Length > 0) Detalle += $" · {e.Region}";
        Etiqueta = e.BloqueoHome is not null ? IdiomaInterfaz.T("Libreria.Estado.Home") : !e.Disponible ? IdiomaInterfaz.T("Libreria.Estado.NoDisponible") : !e.Anadible ? IdiomaInterfaz.T("Libreria.Estado.Pendiente") : e.RequiereHome ? IdiomaInterfaz.T("Libreria.Estado.PasarHome") : IdiomaInterfaz.T("Libreria.Estado.Disponible");
        Clase = e.Ofrecible ? (e.RequiereHome ? "aviso" : "ok") : string.Empty;
        Glifo = e.Ofrecible ? (e.RequiereHome ? "CircleAlert" : "CircleCheck") : "Info";
        // El motivo SIEMPRE visible cuando la fila va atenuada: el texto de «Estado» del escritorio (motivo del Core, del catálogo o del ajuste de HOME).
        Motivo = e.Atenuada ? (e.BloqueoHome ?? (e.Estado.Length > 0 && e.Estado != Etiqueta ? e.Estado : e.MotivoNoDisponible)) : null;
        Accesible = $"{Titulo}, {Detalle}, {Etiqueta.ToLowerInvariant()}{(Motivo is null ? "" : ". " + Motivo)}";
    }

    public EntradaLibreria Entrada { get; private set; }
    public Pokekeep.Sprites.ModoSprites Modo { get; }
    public int Contexto { get; }
    public int FormaSprite { get; }
    public uint ArgumentoForma => Entrada.ArgumentoForma;
    public int VersionJuego => Entrada.JuegoId;
    public string Titulo { get; private set; }
    public string Detalle { get; private set; }
    public string Etiqueta { get; private set; }
    public string Clase { get; private set; }
    public string Glifo { get; private set; }
    public string? Motivo { get; private set; }
    public bool HayMotivo => Motivo is not null;
    public bool Atenuada => Entrada.Atenuada;
    public bool Gigamax => Entrada.Gigamax;
    public string Accesible { get; private set; }
    internal void ActualizarTextos(FilaLibreria traducida)
    {
        Entrada = traducida.Entrada; Titulo = traducida.Titulo; Detalle = traducida.Detalle; Etiqueta = traducida.Etiqueta;
        Clase = traducida.Clase; Glifo = traducida.Glifo; Motivo = traducida.Motivo; Accesible = traducida.Accesible;
        OnPropertyChanged(string.Empty);
    }
    public bool EtiquetaOk => Clase == "ok";
    public bool EtiquetaAviso => Clase == "aviso";
}

/// <summary>
/// Pestaña Librería: el catálogo legal de Pokekeep como LISTA con estado y motivo (decisión del propietario, no rejilla). Buscador (FTS5) con retardo anti-rebote, filtros como chips (generación, origen, idioma de evento,
/// región, especie por rangos de la Pokédex, forma, «solo disponibles»), paginación de 50 en 50, ficha con procedencia y rasgos, «Añadir a la caja» y «Exportar ficha…». La Librería se recarga sola al cambiar la
/// partida y al cambiar el ajuste de HOME.
/// </summary>
public sealed partial class LibreriaViewModel : ObservableObject
{
    /// <summary>Texto de ejemplo del buscador: con el texto muy grande (x1,3 o mas) ni la version corta cabe en una linea de un movil, asi que queda «Buscar…» (el nombre accesible sigue diciendo por que campos se busca).</summary>
    public string TextoDeBusqueda => Design.Movimiento.EscalaTexto >= 1.3 ? IdiomaInterfaz.T("Comun.Buscar") + "…" : IdiomaInterfaz.T("Libreria.Buscar");

    public const int TamanoPagina = 50;
    public static readonly TimeSpan Antirrebote = TimeSpan.FromMilliseconds(250);

    private readonly ICatalogoLibreria _catalogo;
    private readonly IAnfitrionLibreria? _anfitrion;
    private readonly SynchronizationContext? _ui = SynchronizationContext.Current;
    private readonly int _juegoId;
    private readonly int _destinoGc;
    private bool _homeAlCargar;
    private Timer? _retardo;
    private bool _cargando;
    private long _total;

    public LibreriaViewModel(Sesion? sesion, ICatalogoLibreria catalogo, IAnfitrionLibreria? anfitrion = null)
    {
        _catalogo = catalogo;
        _anfitrion = anfitrion;
        Sesion = sesion;
        Juego = sesion?.Juego ?? string.Empty;
        _juegoId = sesion is null ? 0 : catalogo.JuegoDe(sesion.Guardado);
        _destinoGc = sesion is null ? 0 : catalogo.DestinoGc(sesion.Guardado);
        Hay = catalogo.Disponible;
        Cargar();
    }

    public Sesion? Sesion { get; }
    public string Juego { get; }
    public bool Hay { get; }
    public bool NoHay => !Hay;
    public string Subtitulo => Sesion is null ? IdiomaInterfaz.T("Libreria.SubtituloSinPartida") : IdiomaInterfaz.F("Libreria.Subtitulo", Ajustes.Pkhex.ProgramaPkhex.NombreJuegoGuardado(Juego));
    /// <summary>Mensaje en lugar de la lista cuando no hay catálogo o no hay partida con juego conocido.</summary>
    public string? Banner => !Hay ? IdiomaInterfaz.T("Libreria.FaltaCatalogo") : Sesion is null || _juegoId == 0 ? IdiomaInterfaz.T("Libreria.FaltaPartida") : null;
    public bool HayBanner => Banner is not null;
    public bool HayLista => Hay && Banner is null;
    public string TituloBanner => IdiomaInterfaz.T(Hay ? "Libreria.SinPartida" : "Libreria.SinCatalogo");

    [ObservableProperty] private string _texto = string.Empty;
    [ObservableProperty] private bool _soloDisponibles;
    [ObservableProperty] private int _generacion;          // 0 = cualquiera
    [ObservableProperty] private string? _origen;
    [ObservableProperty] private string? _idioma;
    [ObservableProperty] private string? _region;
    [ObservableProperty] private int _especie;             // 0 = cualquiera
    [ObservableProperty] private int _forma = -1;          // -1 = cualquiera
    [ObservableProperty] private ObservableCollection<FilaLibreria> _filas = [];
    [ObservableProperty] private string _resumen = string.Empty;
    [ObservableProperty] private bool _sinResultados;
    [ObservableProperty] private int _pagina;
    [ObservableProperty] private string _textoPagina = string.Empty;
    [ObservableProperty] private bool _puedeAnterior;
    [ObservableProperty] private bool _puedeSiguiente;
    /// <summary>Milisegundos de la última consulta (consulta + filas): el criterio del encargo es verlo en menos de 300 ms.</summary>
    [ObservableProperty] private long _ultimaConsultaMs;

    public int TotalPaginas => Math.Max(1, (int)Math.Ceiling(_total / (double)TamanoPagina));
    public long Total => _total;

    // ---- Etiquetas de los chips (el valor elegido, o el nombre del filtro) ----
    public string EtiquetaGeneracion => Generacion == 0 ? IdiomaInterfaz.T("Libreria.Generacion") : IdiomaInterfaz.F("Libreria.NumeroGeneracion", Generacion);
    public string EtiquetaOrigen => Origen is null ? IdiomaInterfaz.T("Libreria.Origen") : _catalogo.Origenes().FirstOrDefault(o => o.Codigo == Origen).Etiqueta ?? Origen;
    public string EtiquetaIdioma => Idioma is null ? IdiomaInterfaz.T("Libreria.Idioma") : _catalogo.IdiomasEvento().FirstOrDefault(o => o.Codigo == Idioma).Etiqueta ?? Idioma;
    public string EtiquetaRegion => Region is null ? IdiomaInterfaz.T("Libreria.Region") : _catalogo.Regiones().FirstOrDefault(o => o.Codigo == Region).Etiqueta ?? Region;
    public string EtiquetaEspecie => Especie == 0 ? IdiomaInterfaz.T("Libreria.Especie") : $"{Especie:000} {_catalogo.NombreEspecie(Especie)}";
    public string EtiquetaForma => Forma < 0 ? IdiomaInterfaz.T("Libreria.Forma") : _formas.FirstOrDefault(f => f.Forma == Forma).Nombre ?? IdiomaInterfaz.F("Libreria.NumeroForma", Forma);
    public bool GeneracionActiva => Generacion != 0;
    public bool OrigenActivo => Origen is not null;
    public bool IdiomaActivo => Idioma is not null;
    public bool RegionActiva => Region is not null;
    public bool EspecieActiva => Especie != 0;
    public bool FormaActiva => Forma >= 0;
    public bool FormaDisponible => Especie != 0 && _formas.Count > 1;
    public bool HayFiltros => GeneracionActiva || OrigenActivo || IdiomaActivo || RegionActiva || EspecieActiva || SoloDisponibles || !string.IsNullOrWhiteSpace(Texto);
    private IReadOnlyList<(int Forma, string Nombre)> _formas = [];

    private void AvisarChips()
    {
        foreach (var n in new[] { nameof(EtiquetaGeneracion), nameof(EtiquetaOrigen), nameof(EtiquetaIdioma), nameof(EtiquetaRegion), nameof(EtiquetaEspecie), nameof(EtiquetaForma), nameof(GeneracionActiva), nameof(OrigenActivo),
                                  nameof(IdiomaActivo), nameof(RegionActiva), nameof(EspecieActiva), nameof(FormaActiva), nameof(FormaDisponible), nameof(HayFiltros) })
            OnPropertyChanged(n);
    }

    partial void OnTextoChanged(string value) { AvisarChips(); Programar(); }
    partial void OnSoloDisponiblesChanged(bool value) => CambioDeFiltro();
    partial void OnGeneracionChanged(int value) => CambioDeFiltro();
    partial void OnOrigenChanged(string? value) => CambioDeFiltro();
    partial void OnIdiomaChanged(string? value) => CambioDeFiltro();
    partial void OnRegionChanged(string? value) => CambioDeFiltro();
    partial void OnFormaChanged(int value) => CambioDeFiltro();
    partial void OnEspecieChanged(int value)
    {
        _formas = value != 0 && _juegoId != 0 ? _catalogo.Formas(_juegoId, value) : [];
        if (!_cargando) { _cargando = true; Forma = -1; _cargando = false; }
        CambioDeFiltro();
    }

    private void CambioDeFiltro() { AvisarChips(); if (!_cargando) { Pagina = 0; Cargar(); } }

    /// <summary>Retardo anti-rebote del buscador (250 ms): una consulta por pausa al escribir, no por letra. Sin hilo de interfaz (pruebas) se consulta al momento.</summary>
    private void Programar()
    {
        if (_cargando) return;
        if (_ui is null) { Pagina = 0; Cargar(); return; }
        _retardo?.Dispose();
        _retardo = new Timer(_ => _ui.Post(_ => { Pagina = 0; Cargar(); }, null), null, Antirrebote, Timeout.InfiniteTimeSpan);
    }

    /// <summary>Consulta ya, sin esperar al retardo (para cuando se pulsa «Buscar» en el teclado y para las pruebas).</summary>
    [RelayCommand]
    public void RecargarYa() { _retardo?.Dispose(); _retardo = null; Pagina = 0; Cargar(); }

    /// <summary>La Librería se recarga sola cuando cambia el ajuste de HOME (lo que solo existe pasando por HOME se atenúa o se ofrece).</summary>
    public void AlMostrar()
    {
        if (Hay && _anfitrion is not null && _anfitrion.Ajustes.AllowHomeTrackerless != _homeAlCargar) Cargar();
    }

    public void ActualizarIdioma()
    {
        _retardo?.Dispose(); _retardo = null;
        if (Especie != 0 && _juegoId != 0) _formas = _catalogo.Formas(_juegoId, Especie);
        foreach (var nombre in new[] { nameof(TextoDeBusqueda), nameof(Subtitulo), nameof(Banner), nameof(TituloBanner), nameof(HayBanner), nameof(HayLista) }) OnPropertyChanged(nombre);
        AvisarChips(); Cargar(conservarFilas: true);
    }

    private FiltroLibreria Filtro() => new()
    {
        Texto = string.IsNullOrWhiteSpace(Texto) ? null : Texto.Trim(),
        Juego = _juegoId,
        DestinoGc = _destinoGc,
        Origen = Origen,
        Generacion = Generacion == 0 ? null : Generacion,
        Especie = Especie == 0 ? null : Especie,
        Forma = Especie != 0 && Forma >= 0 ? Forma : null,
        Idioma = Idioma,
        Region = Region,
        SoloDisponibles = SoloDisponibles,
        PermitirHome = _anfitrion?.Ajustes.AllowHomeTrackerless == true,
        Limite = TamanoPagina,
        Desplazamiento = Pagina * TamanoPagina,
    };

    private void Cargar(bool conservarFilas = false)
    {
        if (!HayLista) { Filas = []; Resumen = string.Empty; SinResultados = false; TextoPagina = string.Empty; PuedeAnterior = PuedeSiguiente = false; return; }
        var reloj = Stopwatch.StartNew();
        var f = Filtro();
        _homeAlCargar = f.PermitirHome;
        var modo = Sesion?.ModoSprites ?? Pokekeep.Sprites.ModoSprites.Clasico;
        var filas = _catalogo.Consultar(f).Select(e => new FilaLibreria(e, modo)).ToList();
        _total = _catalogo.Contar(f);
        if (conservarFilas && Filas.Count == filas.Count && Filas.Zip(filas).All(p => p.First.Entrada.Clave == p.Second.Entrada.Clave))
            for (var i = 0; i < filas.Count; i++) Filas[i].ActualizarTextos(filas[i]);
        else Filas = new ObservableCollection<FilaLibreria>(filas);
        SinResultados = _total == 0;
        var paginas = TotalPaginas;
        TextoPagina = _total == 0 ? IdiomaInterfaz.T("Libreria.SinResultados") : IdiomaInterfaz.P("Libreria.Pagina", _total, Pagina + 1, paginas);
        Resumen = TextoPagina;
        PuedeAnterior = Pagina > 0;
        PuedeSiguiente = (Pagina + 1) * TamanoPagina < _total;
        UltimaConsultaMs = reloj.ElapsedMilliseconds;
        OnPropertyChanged(nameof(TotalPaginas)); OnPropertyChanged(nameof(Total));
    }

    [RelayCommand] private void Anterior() { if (Pagina > 0) { Pagina--; Cargar(); } }
    [RelayCommand] private void Siguiente() { if ((Pagina + 1) * TamanoPagina < _total) { Pagina++; Cargar(); } }

    [RelayCommand]
    private void LimpiarFiltros()
    {
        _cargando = true;
        Texto = string.Empty; SoloDisponibles = false; Generacion = 0; Origen = null; Idioma = null; Region = null; Especie = 0; Forma = -1;
        _cargando = false;
        AvisarChips();
        Pagina = 0; Cargar();
    }

    // ---- Selectores de los chips ----
    private void Selector(string titulo, Func<IReadOnlyList<OpcionSelector>> opciones, int actual, Action<OpcionSelector> elegir, string marcador)
        => _anfitrion?.AbrirHoja(new SelectorHojaViewModel(IdiomaInterfaz.T(titulo), opciones(), actual, elegir, IdiomaInterfaz.T(marcador),
            actualizarOpciones: opciones, tituloActual: () => IdiomaInterfaz.T(titulo), marcadorActual: () => IdiomaInterfaz.T(marcador)));

    [RelayCommand]
    private void AbrirGeneracion() => Selector("Libreria.Generacion", () => [new(0, IdiomaInterfaz.T("Libreria.CualquierGeneracion")), .. Enumerable.Range(1, 9).Select(g => new OpcionSelector(g, IdiomaInterfaz.F("Libreria.NumeroGeneracion", g)))], Generacion, o => Generacion = o.Id, "Libreria.BuscarGeneracion");

    [RelayCommand]
    private void AbrirOrigen()
    {
        var origenes = _catalogo.Origenes();
        Selector("Libreria.Origen", () => [new(0, IdiomaInterfaz.T("Libreria.CualquierOrigen")), .. _catalogo.Origenes().Select((o, i) => new OpcionSelector(i + 1, o.Etiqueta))], Origen is null ? 0 : origenes.ToList().FindIndex(o => o.Codigo == Origen) + 1,
            o => Origen = o.Id == 0 ? null : origenes[o.Id - 1].Codigo, "Libreria.BuscarOrigen");
    }

    [RelayCommand]
    private void AbrirIdioma()
    {
        var idiomas = _catalogo.IdiomasEvento();
        Selector("Libreria.IdiomaEvento", () => [new(0, IdiomaInterfaz.T("Libreria.CualquierIdioma")), .. _catalogo.IdiomasEvento().Select((o, i) => new OpcionSelector(i + 1, o.Etiqueta))], Idioma is null ? 0 : idiomas.ToList().FindIndex(o => o.Codigo == Idioma) + 1,
            o => Idioma = o.Id == 0 ? null : idiomas[o.Id - 1].Codigo, "Libreria.BuscarIdioma");
    }

    [RelayCommand]
    private void AbrirRegion()
    {
        var regiones = _catalogo.Regiones();
        Selector("Libreria.RegionEvento", () => [new(0, IdiomaInterfaz.T("Libreria.CualquierRegion")), .. _catalogo.Regiones().Select((o, i) => new OpcionSelector(i + 1, o.Etiqueta))], Region is null ? 0 : regiones.ToList().FindIndex(o => o.Codigo == Region) + 1,
            o => Region = o.Id == 0 ? null : regiones[o.Id - 1].Codigo, "Libreria.BuscarRegion");
    }

    [RelayCommand]
    private void AbrirEspecie()
    {
        var rangos = _catalogo.RangosPokedex();
        Selector("Libreria.Especie", () => [new(0, IdiomaInterfaz.T("Libreria.CualquierEspecie")), .. _catalogo.RangosPokedex().Select((r, i) => new OpcionSelector(i + 1, r.Etiqueta))], Especie == 0 ? 0 : rangos.ToList().FindIndex(r => Especie >= r.Primera && Especie <= r.Ultima) + 1,
            o =>
            {
                if (o.Id == 0) { Especie = 0; return; }
                var indice = o.Id - 1; var (primera, ultima, _) = rangos[indice];
                string Titulo() => _catalogo.RangosPokedex()[indice].Etiqueta;
                IReadOnlyList<OpcionSelector> Opciones() => [.. Enumerable.Range(primera, ultima - primera + 1).Select(s => new OpcionSelector(s, $"{s:000} {_catalogo.NombreEspecie(s)}"))];
                _anfitrion?.AbrirHoja(new SelectorHojaViewModel(Titulo(), Opciones(), Especie, x => Especie = x.Id, IdiomaInterfaz.T("Libreria.BuscarEspecie"),
                    actualizarOpciones: Opciones, tituloActual: Titulo, marcadorActual: () => IdiomaInterfaz.T("Libreria.BuscarEspecie")));
            }, "Libreria.BuscarRango");
    }

    [RelayCommand]
    private void AbrirForma()
    {
        if (!FormaDisponible) return;
        Selector("Libreria.Forma", () => [new(-1, IdiomaInterfaz.T("Libreria.CualquierForma")), .. _formas.Select(f => new OpcionSelector(f.Forma, $"{f.Forma}: {f.Nombre}"))], Forma, o => Forma = o.Id, "Libreria.BuscarForma");
    }

    // ---- Ficha, añadir y exportar ----
    /// <summary>El «+» de la fila: añade a la primera casilla libre de la caja activa sin pasar por la ficha (mismos motivos si no se puede).</summary>
    [RelayCommand]
    public void AnadirRapido(FilaLibreria? fila)
    {
        if (fila is null || _anfitrion is null) return;
        new FichaLibreriaHojaViewModel(fila, _catalogo, _anfitrion, Sesion).AnadirCommand.Execute(null);
    }

    [RelayCommand]
    public void AbrirFicha(FilaLibreria? fila)
    {
        if (fila is null || _anfitrion is null) return;
        _anfitrion.AbrirHoja(new FichaLibreriaHojaViewModel(fila, _catalogo, _anfitrion, Sesion));
    }
}

/// <summary>La ficha de una entrada: sprite, disponibilidad, origen, entrenador original, evento, niveles, fuente y rasgos, con «Añadir a la caja» (primera casilla libre de la caja activa o una casilla elegida) y «Exportar ficha…».</summary>
public sealed partial class FichaLibreriaHojaViewModel : HojaViewModel
{
    private readonly FilaLibreria _fila;
    private readonly ICatalogoLibreria _catalogo;
    private readonly IAnfitrionLibreria _anfitrion;
    private readonly Sesion? _sesion;

    public FichaLibreriaHojaViewModel(FilaLibreria fila, ICatalogoLibreria catalogo, IAnfitrionLibreria anfitrion, Sesion? sesion)
    {
        _fila = fila; _catalogo = catalogo; _anfitrion = anfitrion; _sesion = sesion;
        var ficha = catalogo.Ficha(fila.Entrada, anfitrion.Ajustes.AllowHomeTrackerless);
        Lineas = [.. (ficha?.Lineas ?? [new LineaFicha(IdiomaInterfaz.T("Libreria.PudoLeerFicha"))]).Select(l => new LineaFichaVm(l.Texto, l.Negrita))];
        Nombre = fila.Titulo;
        Actualizar();
    }

    public override string Titulo => IdiomaInterfaz.T("Libreria.FichaLibreria");
    public FilaLibreria Fila => _fila;
    public string Nombre { get; }
    public IReadOnlyList<LineaFichaVm> Lineas { get; }
    [ObservableProperty] private string _aviso = string.Empty;
    [ObservableProperty] private bool _puedeAnadir;
    /// <summary>«Elegir casilla…» sigue activo con la caja activa llena (es justo la salida natural: elegir otra caja o casilla); solo se apaga por los demás motivos.</summary>
    [ObservableProperty] private bool _puedeElegirCasilla;
    [ObservableProperty] private bool _exportando;

    /// <summary>Estado del botón «Añadir a la caja» y motivo si no se puede (los textos de la Librería del escritorio).</summary>
    private void Actualizar()
    {
        var e = _fila.Entrada;
        string? motivo = null;
        if (_sesion is null) motivo = IdiomaInterfaz.T("Libreria.PartidaAbiertaJuegoConocido");
        else if (e.BloqueoHome is { } home) motivo = home;
        else if (_anfitrion.Acciones is not { } acciones) motivo = IdiomaInterfaz.T("Libreria.AccionesMotorEstanDisponibles");
        else motivo = acciones.MotivoAnadir(_sesion, e.Clave, _anfitrion.CajaActiva, null);
        PuedeAnadir = motivo is null;
        PuedeElegirCasilla = _sesion is not null && _anfitrion.Acciones is { } ac2 && e.BloqueoHome is null && ac2.MotivoElegirCasilla(_sesion, e.Clave, _anfitrion.CajaActiva) is null;
        Aviso = motivo ?? IdiomaInterfaz.F("Libreria.ColocaraPrimeraCasillaLibre", _anfitrion.CajaActiva + 1) + (e.RequiereHome ? " " + TextosLibreria.NecesitaHomeConAjuste : string.Empty);
    }

    [RelayCommand]
    private void Anadir() => Lanzar(null);

    [RelayCommand]
    private void ElegirCasilla()
    {
        if (!PuedeElegirCasilla) return;
        Cerrar();
        _anfitrion.ElegirCasillaParaColocar(_fila.Entrada, destino => Lanzar(destino));
    }

    /// <summary>«Añadir a la caja» por el mismo motor y la misma validación; nunca sobrescribe y siempre se puede deshacer desde la hoja de resultado y desde el Historial.</summary>
    private void Lanzar(DestinoCasilla? destino)
    {
        if (_sesion is null || _anfitrion.Acciones is not { } acciones) return;
        var entrada = _fila.Entrada;
        if (acciones.MotivoAnadir(_sesion, entrada.Clave, _anfitrion.CajaActiva, destino) is { } motivo) { _anfitrion.Informar(motivo, "aviso", "CircleAlert"); return; }
        Cerrar();
        var hoja = new AccionHojaViewModel(AccionPokemon.Legalizar, $"{_fila.Titulo} · {_fila.Entrada.Juego}",
            (p, ct) => acciones.AnadirDeLibreriaAsync(_sesion, entrada.Clave, _anfitrion.CajaActiva, destino, p, ct),
            terminada: r => { if (r.Aplicado) _anfitrion.TrasCambioDeLibreria(); },
            deshecho: _anfitrion.TrasCambioDeLibreria,
            origen: OrigenAccion.Libreria, mostrarResumen: () => _anfitrion.Ajustes.ShowSummary, abrirHoja: _anfitrion.AbrirHoja, portapapeles: () => _anfitrion.Portapapeles);
        _anfitrion.AbrirHoja(hoja);
        _ = hoja.Lanzar();
    }

    [RelayCommand] private Task ExportarJson() => Exportar(FormatoFicha.Json);
    [RelayCommand] private Task ExportarTexto() => Exportar(FormatoFicha.Texto);

    /// <summary>«Exportar ficha…»: JSON o texto legible a un documento elegido con el selector del sistema (SAF). Solo datos del catálogo y versiones del motor; ninguna ruta ni dato del usuario.</summary>
    private async Task Exportar(FormatoFicha formato)
    {
        if (_anfitrion.Documentos is not { } docs) { Aviso = IdiomaInterfaz.T("Libreria.ExportarDisponibleVentana"); return; }
        Exportando = true;
        try
        {
            VersionesMotor? versiones = null;
            if (_anfitrion.Acciones is { } acciones)
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                try { versiones = await acciones.VersionesMotorAsync(cts.Token); } catch (OperationCanceledException) { }
            }
            var fichero = _catalogo.ExportarFicha(_fila.Entrada, formato, versiones);
            if (fichero is null) { Aviso = IdiomaInterfaz.T("Libreria.PudoPrepararFicha"); return; }
            var destino = await docs.ElegirDestinoAsync(fichero.NombreCompleto, CancellationToken.None);
            if (destino is null) return;
            await docs.EscribirAsync(destino, new System.Text.UTF8Encoding(false).GetBytes(fichero.Contenido), CancellationToken.None);
            Aviso = IdiomaInterfaz.T("Libreria.FichaGuardada");
            _anfitrion.Informar(IdiomaInterfaz.T("Libreria.FichaGuardada"), "ok", "CircleCheck");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Aviso = IdiomaInterfaz.T("Libreria.PudoGuardarFicha") + ex.Message;
            _anfitrion.Informar(Aviso, "error", "TriangleAlert");
        }
        finally { Exportando = false; }
    }
}

public sealed record LineaFichaVm(string Texto, bool Negrita)
{
    public Avalonia.Media.FontWeight Peso => Negrita ? Avalonia.Media.FontWeight.Bold : Avalonia.Media.FontWeight.Normal;
}

/// <summary>Textos de la Librería del escritorio que la interfaz repite (una prueba los compara con <c>Textos</c> del núcleo).</summary>
public static class TextosLibreria
{
    public static string NecesitaHomeConAjuste => IdiomaInterfaz.T("Libreria.NecesitaHomeConAjuste");
}
