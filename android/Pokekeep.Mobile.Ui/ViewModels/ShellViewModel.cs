// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pokekeep.Mobile.Ui.Ajustes;
using Pokekeep.Mobile.Ui.Almacen;
using Pokekeep.Mobile.Ui.Mas;
using Pokekeep.Mobile.Ui.Masivas;
using Pokekeep.Mobile.Ui.Modelo;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.ViewModels;

public enum Destino { Pokemon, Mochila, Partida, Libreria, Mas }

/// <summary>Una fila de las pantallas de Partida y Mas: nombre, que hace y su icono. Con <see cref="Comando"/> es accionable; sin el dice «Pronto» (nunca un boton muerto sin explicacion).</summary>
public sealed record EntradaMenu(string TituloBase, string DescripcionBase, string Glifo, string Fase, ICommand? Comando = null) : System.ComponentModel.INotifyPropertyChanged
{
    private bool UsaRecursos { get; init; }
    public string? ClaveTitulo => UsaRecursos ? TituloBase : null;
    public string Titulo => UsaRecursos ? IdiomaInterfaz.T(TituloBase) : TituloBase;
    public string Descripcion => UsaRecursos ? IdiomaInterfaz.T(DescripcionBase) : DescripcionBase;
    public static EntradaMenu Localizada(string titulo, string descripcion, string glifo, string fase, ICommand? comando = null)
        => new(titulo, descripcion, glifo, fase, comando) { UsaRecursos = true };
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    public void ActualizarIdioma()
    {
        PropertyChanged?.Invoke(this, new(nameof(Titulo))); PropertyChanged?.Invoke(this, new(nameof(Descripcion)));
    }
    public bool Accionable => Comando is not null;
    public bool Pronto => Comando is null;
}

/// <summary>Un reciente listo para pintar: nombre, juego y cuando.</summary>
public sealed record FilaReciente(RecienteRef Referencia, string Detalle)
{
    /// <summary>Versión exacta anotada al abrir; los recientes anteriores (Any) se resuelven por el nombre del juego.</summary>
    public GameVersion Juego => Referencia.Version.IsValidSavedVersion() ? Referencia.Version : Design.LogosPartida.VersionDeNombre(Referencia.Juego);
    public GameVersion VersionJuego => Referencia.Version;
    public string? Idioma => Referencia.Idioma;
    public string Nombre => Referencia.Nombre;
    public string Accesible => IdiomaInterfaz.F("Inicio.RecienteAccesible", Referencia.Nombre, Detalle);
}

public sealed record FilaRecuperable(SesionRecuperable Referencia, string Detalle)
{
    public string Nombre => Referencia.Nombre;
    public string Accesible => IdiomaInterfaz.F("Inicio.RecuperableAccesible", Referencia.Nombre, Detalle);
}

/// <summary>Raiz de la app: los cinco destinos, la tarjeta de partida, el editor a pantalla completa, las acciones de abrir/guardar/exportar y los avisos.</summary>
public sealed partial class ShellViewModel : ObservableObject, IAnfitrionCajas, IAnfitrionLibreria
{
    private readonly ICatalogoLibreria _catalogo;
    private readonly IServiciosPartida? _servicios;
    private readonly IRegistros? _registros;
    private readonly IAccionesPokekeep? _acciones;
    private readonly IMasivas? _masivas;
    private Timer? _temporizadorToast;

    public ShellViewModel(Sesion? sesion, ICatalogoLibreria? catalogo = null, int cajaInicial = 0, bool seleccionarIlegal = false, IServiciosPartida? servicios = null, IRegistros? registros = null, IAccionesPokekeep? acciones = null, IMasivas? masivas = null)
    {
        _catalogo = catalogo ?? new CatalogoNulo();
        _servicios = servicios;
        _registros = registros;
        _acciones = acciones;
        _masivas = masivas;
        Sesion = sesion;
        _acciones?.SesionActiva(sesion);   // la partida activa: si cambia mientras trabaja el motor, el resultado se descarta (regla 7)
        _masivas?.SesionActiva(sesion);
        Libreria = new LibreriaViewModel(sesion, _catalogo, this);
        if (sesion is not null)
        {
            Cajas = new CajasViewModel(sesion, this, cajaInicial, seleccionarIlegal);
            Cajas.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(CajasViewModel.BarraContextual)) { OnPropertyChanged(nameof(HayBarraContextual)); OnPropertyChanged(nameof(NoHayBarraContextual)); } };
            MochilaVm = new MochilaViewModel(sesion, AbrirHoja, Informar) { EnPantalla = false };
            Cambios = sesion.CambiosPendientes;
            // Cualquier cambio de casillas (cajas, editor, menu de caja, Deshacer) cuenta y deja la copia de recuperacion; nada se exporta (regla 13).
            sesion.CasillasCambiadas += _ => { Cambios = sesion.CambiosPendientes; _servicios?.Recuperacion(sesion); };
        }
        MenuPartida =
        [
            EntradaMenu.Localizada("Menu.Archivo.Abrir.Titulo", "Menu.Archivo.Abrir.Descripcion", "FolderOpen", "F10", servicios?.Abridor is null ? null : AbrirArchivoCommand),
            EntradaMenu.Localizada("Menu.Archivo.Guardar.Titulo", "Menu.Archivo.Guardar.Descripcion", "Save", "F2", HaySesion ? GuardarCommand : null),
            EntradaMenu.Localizada("Menu.Archivo.Exportar.Titulo", "Menu.Archivo.Exportar.Descripcion", "FileDown", "F2", HaySesion ? ExportarCopiaCommand : null),
            EntradaMenu.Localizada("Menu.Archivo.CopiaCarpeta.Titulo", "Menu.Archivo.CopiaCarpeta.Descripcion", "FolderOpen", "F2", HaySesion && servicios is not null ? GuardarEnCarpetaCommand : null),
            EntradaMenu.Localizada("Menu.Archivo.Carpeta.Titulo", "Menu.Archivo.Carpeta.Descripcion", "HardDrive", "F2", servicios is null ? null : ElegirCarpetaCommand),
        ];
        PartidaVm = new PartidaViewModel(sesion, MenuPartida, HaySesion ? GuardarCommand : null, HaySesion ? ExportarCopiaCommand : null, AbrirHoja, Informar, () => Documentos, () => Portapapeles, BorradoresSinAplicar);
        PartidaVm.AbrirHerramienta = id => { Destino = Destino.Mas; NavegarF10("herramienta=" + id, out _); };
        PartidaVm.CambiarRanura = CambiarRanuraBr;
        if (MochilaVm is not null) MochilaVm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(MochilaViewModel.HayCambios)) PartidaVm.ActualizarBorradores(); };
        PartidaVm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(PartidaViewModel.EnEditor)) { OnPropertyChanged(nameof(MostrarCabecera)); OnPropertyChanged(nameof(PartidaEditando)); } };
        MenuMas = ConEntradaBanco(ConEntradasHerramientas(
        [
            EntradaMenu.Localizada("Menu.Mas.Completar.Titulo", "Menu.Mas.Completar.Descripcion", "Sparkles", "F9", masivas is ICompletarJuego ? AbrirCompletarJuegoCommand : null),
            EntradaMenu.Localizada("Menu.Mas.Masivas.Titulo", "Menu.Mas.Masivas.Descripcion", "Layers", "F9", masivas is not null ? AbrirEdicionMasivaTodoCommand : null),
            EntradaMenu.Localizada("Menu.Mas.Historial.Titulo", "Menu.Mas.Historial.Descripcion", "History", "F6", AbrirHistorialCommand),
            EntradaMenu.Localizada("Menu.Mas.Archivo.Titulo", "Menu.Mas.Archivo.Descripcion", "FolderOpen", "F10", AbrirMenuArchivoCommand),
            EntradaMenu.Localizada("Menu.Mas.Datos.Titulo", "Menu.Mas.Datos.Descripcion", "FileDown", "F10", AbrirMenuDatosCommand),
            EntradaMenu.Localizada("Menu.Mas.Guardados.Titulo", "Menu.Mas.Guardados.Descripcion", "BookOpen", "F10", AbrirGuiaGuardadosCommand),
            EntradaMenu.Localizada("Menu.Mas.Ajustes.Titulo", "Menu.Mas.Ajustes.Descripcion", "Settings", "F6", AbrirAjustesCommand),
            EntradaMenu.Localizada("Menu.Mas.Registros.Titulo", "Menu.Mas.Registros.Descripcion", "ScrollText", "F2", registros is null ? null : AbrirRegistrosCommand),
            EntradaMenu.Localizada("Menu.Mas.AcercaDe.Titulo", "Menu.Mas.AcercaDe.Descripcion", "Info", "F6", AbrirAcercaDeCommand),
        ]));
        EsPokemon = true;
        RefrescarRecientes();
        AvisoDeApertura(sesion);   // F10-2: abierta sin permiso de escritura o dentro de un contenedor (ShellViewModel.Abrir.cs)
    }

    public Sesion? Sesion { get; }
    /// <summary>Ajustes de Pokekeep (ajustes.json). Los pone quien crea la Shell; sin ellos valen los de fábrica en memoria.</summary>
    public IAjustesPokekeep AlmacenDeAjustes
    {
        get => _almacenDeAjustes;
        set { _almacenDeAjustes = value; value.Cambiado += AplicarAjustesDeSprites; AplicarAjustesDeSprites(); }
    }
    private IAjustesPokekeep _almacenDeAjustes = new AjustesEnMemoria();
    private void AplicarAjustesDeSprites()
    {
        var nuevo = _almacenDeAjustes.Actual.SmoothBoxSprites;
        if (Design.Sprites.SuavizarCaja == nuevo) return;
        Design.Sprites.SuavizarCaja = nuevo;
        Cajas?.Refrescar();   // la caja visible se vuelve a dibujar con el nuevo escalado
    }
    /// <summary>«Acerca de Pokekeep»: lo da quien crea la Shell (núcleo compartido + datos del paquete).</summary>
    public IAcercaDe? ProveedorAcercaDe { get; set; }
    /// <summary>Pantalla a pantalla completa colgada de «Más» (Historial, Ajustes, Acerca de), o null.</summary>
    [ObservableProperty] private PantallaMasViewModel? _pantalla;
    public bool ViendoPantalla => Pantalla is not null;
    public bool HaySesion => Sesion is not null;
    public bool NoHaySesion => Sesion is null;
    public CajasViewModel? Cajas { get; }
    /// <summary>La pestana Pokemon tiene una barra de acciones contextual (seleccion o colocar) que sustituye a la barra de destinos.</summary>
    public bool HayBarraContextual => Cajas?.BarraContextual == true;
    public bool NoHayBarraContextual => !HayBarraContextual;
    public LibreriaViewModel Libreria { get; }
    public MochilaViewModel? MochilaVm { get; }
    public PartidaViewModel PartidaVm { get; }

    /// <summary>Se ha abierto (o recuperado) otra partida: quien hospeda la Shell la reemplaza por una nueva con esa sesion.</summary>
    public event Action<Sesion>? SesionNueva;

    public string TituloPartida => Sesion is null ? IdiomaInterfaz.T("Cabecera.SinPartida") : TituloDePartida(Sesion);   // Privacy.HideSAVDetails (ShellViewModel.CambiosSinGuardar.cs)
    /// <summary>El juego de una partida reciente con su nombre oficial («Pokémon Espada»); las entradas antiguas guardan el texto corto del Core.</summary>
    private static string JuegoDeReciente(string guardado) => NombresJuego.Oficial(Ajustes.Pkhex.ProgramaPkhex.NombreJuegoGuardado(guardado));

    public string TituloCompactoPartida => Sesion is null ? IdiomaInterfaz.T("Cabecera.SinPartida") : TituloCompactoDePartida(Sesion);
    // GameCube: el guardado dice si es Colosseum, XD o Box (Version da CXD, que no tiene logo propio).
    public GameVersion JuegoPartida => Sesion is { } s ? Design.LogosDeJuego.VersionDe(s.Guardado) : GameVersion.Invalid;
    /// <summary>El rótulo de la cabecera sigue el idioma de la partida, no el de la interfaz.</summary>
    public string? IdiomaPartida => Sesion is { } s ? Design.LogosDeJuego.IdiomaDe(s.Guardado) : null;
    /// <summary>Nombre accesible de la tarjeta compacta (TalkBack): partida completa, plataforma, conexion y cambios.</summary>
    public string ResumenPartida => Sesion is null ? IdiomaInterfaz.T("Cabecera.SinPartida") : IdiomaInterfaz.F("Cabecera.Resumen", TituloPartida + (OcultarDetallesPartida ? string.Empty : ". " + Sesion.JuegoOficial), Sesion.Plataforma, HayCambios ? TextoCambios : IdiomaInterfaz.T("Cabecera.SinCambios"));
    public string TextoCopiaLocal => IdiomaInterfaz.F("Cabecera.CopiaLocal", Sesion?.Plataforma);
    public string SubtituloPartida => Sesion is null ? string.Empty : SubtituloDePartida(Sesion);

    [ObservableProperty] private Destino _destino = Destino.Pokemon;
    [ObservableProperty] private EditorViewModel? _editor;
    [ObservableProperty] private RegistrosViewModel? _registrosVm;
    [ObservableProperty] private bool _esPokemon;
    [ObservableProperty] private bool _esMochila;
    [ObservableProperty] private bool _esPartida;
    [ObservableProperty] private bool _esLibreria;
    [ObservableProperty] private bool _esMas;
    [ObservableProperty] private bool _editando;
    [ObservableProperty] private bool _viendoRegistros;
    [ObservableProperty] private int _cambios;
    [ObservableProperty] private string? _aviso;
    [ObservableProperty] private bool _avisoExpandido;
    /// <summary>Con poco alto o letra grande (la misma regla que la tarjeta compacta) el aviso se queda en una línea; solo ofrece «Ver más» si esa línea no cabe. Con sitio se lee entero.</summary>
    [ObservableProperty] private bool _avisoCompacto;
    [ObservableProperty] private bool _avisoRecortado;
    public bool PuedeExpandirAviso => AvisoCompacto && AvisoRecortado;
    public string TextoDetalleAviso => IdiomaInterfaz.T(AvisoExpandido ? "Comun.VerMenos" : "Comun.VerMas");
    public bool MostrarResumenAviso => AvisoCompacto && !AvisoExpandido;
    public bool MostrarDetalleAviso => !AvisoCompacto || AvisoExpandido;
    partial void OnAvisoExpandidoChanged(bool value) { OnPropertyChanged(nameof(TextoDetalleAviso)); OnPropertyChanged(nameof(MostrarResumenAviso)); OnPropertyChanged(nameof(MostrarDetalleAviso)); }
    partial void OnAvisoCompactoChanged(bool value) { OnPropertyChanged(nameof(MostrarResumenAviso)); OnPropertyChanged(nameof(MostrarDetalleAviso)); OnPropertyChanged(nameof(PuedeExpandirAviso)); }
    partial void OnAvisoRecortadoChanged(bool value) { OnPropertyChanged(nameof(PuedeExpandirAviso)); if (!value) AvisoExpandido = false; }
    [RelayCommand] private void AlternarDetalleAviso() => AvisoExpandido = !AvisoExpandido;
    [ObservableProperty] private string _claseAviso = string.Empty;
    [ObservableProperty] private string _glifoAviso = "Info";
    [ObservableProperty] private string? _textoAccionAviso;
    [ObservableProperty] private ICommand? _comandoAccionAviso;
    [ObservableProperty] private HojaViewModel? _hoja;
    [ObservableProperty] private IReadOnlyList<FilaReciente> _recientes = [];
    [ObservableProperty] private IReadOnlyList<FilaRecuperable> _recuperables = [];
    [ObservableProperty] private bool _ocupado;

    public bool HayAviso => !string.IsNullOrEmpty(Aviso);
    public bool HayAccionAviso => !string.IsNullOrEmpty(TextoAccionAviso) && ComandoAccionAviso is not null;
    public bool HayHoja => Hoja is not null;
    public bool HayRecientes => Recientes.Count > 0;
    public bool HayRecuperables => Recuperables.Count > 0;
    public string TextoCambios => IdiomaInterfaz.P("Cabecera.Cambios", Cambios);
    public bool HayCambios => Cambios > 0;
    public bool NoEditando => !Editando && !ViendoRegistros && !ViendoPantalla;
    /// <summary>La cabecera de la app (marca y tarjeta de partida) se oculta mientras hay un editor de Partida abierto: con texto grande o en horizontal el editor necesita todo el alto.</summary>
    public bool MostrarCabecera => NoEditando && !PartidaEditando;
    public bool PartidaEditando => EsPartida && PartidaVm?.EnEditor == true;
    public bool MostrarPokemon => EsPokemon && NoEditando && HaySesion;
    public bool MostrarSinPartida => EsPokemon && NoHaySesion && NoEditando;
    public bool MostrarMochila => EsMochila && NoEditando;
    public bool MostrarPartida => EsPartida && NoEditando;
    public bool MostrarLibreria => EsLibreria && NoEditando;
    public bool MostrarMas => EsMas && NoEditando;

    public IReadOnlyList<EntradaMenu> MenuPartida { get; }
    public IReadOnlyList<EntradaMenu> MenuMas { get; }

    partial void OnDestinoChanged(Destino value)
    {
        Pantalla = null;
        ViendoRegistros = false;
        if (MochilaVm is not null) MochilaVm.EnPantalla = value == Destino.Mochila;
        EsPokemon = value == Destino.Pokemon; EsMochila = value == Destino.Mochila; EsPartida = value == Destino.Partida; EsLibreria = value == Destino.Libreria; EsMas = value == Destino.Mas;
    }

    [RelayCommand]
    private void NavegarDestino(Destino destino)
    {
        Pantalla = null; ViendoRegistros = false;
        Destino = destino;
        Refrescar();
    }

    partial void OnEsPokemonChanged(bool value) { if (value) Destino = Destino.Pokemon; Refrescar(); }
    partial void OnEsMochilaChanged(bool value) { if (value) Destino = Destino.Mochila; Refrescar(); }
    partial void OnEsPartidaChanged(bool value) { if (value) Destino = Destino.Partida; OnPropertyChanged(nameof(PartidaEditando)); OnPropertyChanged(nameof(MostrarCabecera)); Refrescar(); }
    partial void OnEsLibreriaChanged(bool value) { if (value) { Destino = Destino.Libreria; Libreria.AlMostrar(); } Refrescar(); }
    partial void OnEsMasChanged(bool value) { if (value) Destino = Destino.Mas; Refrescar(); }
    partial void OnEditandoChanged(bool value) { OnPropertyChanged(nameof(NoEditando)); OnPropertyChanged(nameof(MostrarCabecera)); Refrescar(); }
    partial void OnViendoRegistrosChanged(bool value) { OnPropertyChanged(nameof(NoEditando)); OnPropertyChanged(nameof(MostrarCabecera)); Refrescar(); }
    partial void OnPantallaChanged(PantallaMasViewModel? value) { value?.ActualizarIdioma(); OnPropertyChanged(nameof(ViendoPantalla)); OnPropertyChanged(nameof(NoEditando)); OnPropertyChanged(nameof(MostrarCabecera)); Refrescar(); }
    partial void OnCambiosChanged(int value) { OnPropertyChanged(nameof(ResumenPartida)); OnPropertyChanged(nameof(TextoCambios)); OnPropertyChanged(nameof(HayCambios)); }
    partial void OnAvisoChanged(string? value) => OnPropertyChanged(nameof(HayAviso));
    partial void OnTextoAccionAvisoChanged(string? value) => OnPropertyChanged(nameof(HayAccionAviso));
    partial void OnComandoAccionAvisoChanged(ICommand? value) => OnPropertyChanged(nameof(HayAccionAviso));
    partial void OnHojaChanged(HojaViewModel? oldValue, HojaViewModel? newValue)
    {
        OnPropertyChanged(nameof(HayHoja));
        Brillo?.Maximo(newValue?.PideBrilloMaximo == true);
        if (oldValue is ConfirmarHojaViewModel c) c.Retirada();   // una confirmación retirada sin «Sí» deshace lo que dejó pendiente (ShellViewModel.CambiosSinGuardar.cs)
    }
    partial void OnClaseAvisoChanged(string value) { OnPropertyChanged(nameof(AvisoOk)); OnPropertyChanged(nameof(AvisoAviso)); OnPropertyChanged(nameof(AvisoError)); }
    public bool AvisoOk => ClaseAviso == "ok";
    public bool AvisoAviso => ClaseAviso == "aviso";
    public bool AvisoError => ClaseAviso == "error";
    partial void OnRecientesChanged(IReadOnlyList<FilaReciente> value) => OnPropertyChanged(nameof(HayRecientes));
    partial void OnRecuperablesChanged(IReadOnlyList<FilaRecuperable> value) => OnPropertyChanged(nameof(HayRecuperables));

    private void Refrescar()
    {
        OnPropertyChanged(nameof(MostrarPokemon)); OnPropertyChanged(nameof(MostrarSinPartida)); OnPropertyChanged(nameof(MostrarMochila));
        OnPropertyChanged(nameof(MostrarPartida)); OnPropertyChanged(nameof(MostrarLibreria)); OnPropertyChanged(nameof(MostrarMas));
    }

    public void AbrirEditor(Cajas.CasillaRef casilla, PKM pk)
    {
        if (Sesion is null || Cajas is null) return;
        Editor = new EditorViewModel(Sesion, pk, casilla, CerrarEditor, r =>
        {
            // El Core ya ha escrito la casilla y avisado (Sesion.CasillasCambiadas): aqui solo el aviso con «Deshacer».
            Informar(r.Mensaje, "ok", "CircleCheck", Cajas.PuedeDeshacer ? IdiomaInterfaz.T("SubeditorHoja.Deshacer") : null, Cajas.PuedeDeshacer ? Cajas.DeshacerCommand : null);
        }, _acciones, AbrirHoja, () => Portapapeles, () => Documentos) { MostrarResumenDeAcciones = () => AlmacenDeAjustes.Actual.ShowSummary, AjustesDePkhex = () => AjustesPkhex.Actual, Compartir = () => Compartidor };
        Editando = true;
    }

    // ---- IAnfitrionCajas: lo que la pestana Pokemon pide a la raiz ----
    string? IAnfitrionCajas.MotivoAccion(AccionPokemon accion, Cajas.CasillaRef casilla)
        => _acciones is null || Sesion is null ? MotivoSinAcciones : _acciones.MotivoCasilla(accion, Sesion, casilla.EsEquipo, casilla.Caja, casilla.Indice);
    public string? MotivoSinAcciones => _acciones is null ? IdiomaInterfaz.T("Libreria.AccionesMotorEstanDisponibles") : _acciones.NoDisponible;
    public IPortapapeles? Portapapeles { get; set; }
    /// <summary>La hoja de compartir del sistema (bloque 10-9): la pone quien crea la Shell en Android; sin ella «Compartir» dice que no está disponible.</summary>
    public ICompartirSistema? Compartidor { get; set; }
    /// <summary>El brillo de la pantalla (bloque 10-9): al máximo mientras hay una hoja con un código QR. Sin él no se toca el brillo.</summary>
    public IBrilloPantalla? Brillo { get; set; }
    ICompartirSistema? IAnfitrionCajas.Compartir => Compartidor;
    Ajustes.Pkhex.AjustesPkhex? IAnfitrionCajas.AjustesDePkhex => AjustesPkhex.Actual;
    public IAlmacenDocumentos? Documentos => _servicios?.Documentos;
    public IAlmacenCarpetas? Carpetas => _servicios?.Carpetas;
    string? IAnfitrionCajas.CarpetaPreferenciasInterfaz => _servicios?.CarpetaPrivada;
    public void IrALibreria() => Destino = Destino.Libreria;
    void IAnfitrionCajas.AbrirHoja(HojaViewModel hoja) => AbrirHoja(hoja);
    void IAnfitrionCajas.CerrarHoja() => CerrarHoja();
    void IAnfitrionCajas.Informar(string texto, string clase, string glifo, string? textoAccion, ICommand? accion) => Informar(texto, clase, glifo, textoAccion, accion);
    void IAnfitrionCajas.LanzarAccion(AccionPokemon accion, Cajas.CasillaRef casilla) => LanzarAccionCasilla(accion, casilla);

    [RelayCommand]
    public void CerrarEditor() { Editando = false; Editor = null; }

    // ---- Abrir, guardar, exportar, recuperar ----
    /// <summary>
    /// Toast: un aviso en la parte alta con icono, color y texto (nunca solo color). Sin accion, los correctos se retiran solos a los 6 s y los demas esperan a que el usuario los cierre;
    /// CON accion (p. ej. «Deshacer») permanece hasta que se use o se cierre: una accion que desaparece sola no se puede usar con calma.
    /// </summary>
    public void Informar(string texto, string clase, string glifo, string? textoAccion = null, ICommand? accion = null)
    {
        _temporizadorToast?.Dispose(); _temporizadorToast = null;
        AvisoExpandido = false;
        Aviso = texto; ClaseAviso = clase; GlifoAviso = glifo; TextoAccionAviso = textoAccion; ComandoAccionAviso = accion;
        if (clase == "ok" && accion is null && !Design.Movimiento.Pruebas)
        {
            _temporizadorToast = new Timer(_ => Avalonia.Threading.Dispatcher.UIThread.Post(() => { if (Aviso == texto) Aviso = null; }), null, TimeSpan.FromSeconds(6), Timeout.InfiniteTimeSpan);
        }
    }
    [RelayCommand] private void CerrarAviso() { _temporizadorToast?.Dispose(); _temporizadorToast = null; Aviso = null; TextoAccionAviso = null; ComandoAccionAviso = null; }

    // ---- Hoja inferior ----
    public void AbrirHoja(HojaViewModel hoja)
    {
        CerrarHoja();
        hoja.CierreSolicitado += () => { if (ReferenceEquals(Hoja, hoja)) Hoja = null; };
        Hoja = hoja;
    }

    [RelayCommand] public void CerrarHoja() => Hoja = null;

    /// <summary>Tocar el velo: cierra la hoja salvo que su contenido pida una decision explicita (una operacion en curso se cancela con «Cancelar»).</summary>
    [RelayCommand] public void TocarVelo() { if (Hoja is { CierraConFondo: true }) Hoja = null; }

    /// <summary>
    /// Lleva la Shell a una pantalla por nombre (pruebas de dispositivo y capturas del emulador, que no pueden depender de coordenadas de toque): cajas, ficha, editor, stats, libreria, mochila, partida, mas, registros.
    /// </summary>
    public bool Navegar(string pantalla)
    {
        CerrarHoja(); if (Editando) CerrarEditor();
        if (NavegarX38(pantalla, out var resultadoX38)) return resultadoX38;   // «banco» (ShellViewModel.X38.cs)
        if (NavegarF10(pantalla, out var resultadoF10)) return resultadoF10;   // «herramientas», «herramienta=<id>[:<estado>]», «ajustes-pkhex=<grupo>» (ShellViewModel.Herramientas.cs)
        switch (pantalla)
        {
            case "cajas": case "ficha": Destino = Destino.Pokemon; return true;
            case "editor": case "stats": case "editor-teclado":
                Destino = Destino.Pokemon;
                if (Cajas is null) return false;
                if (Cajas.Seleccionada is null) return false;
                Cajas.EditarCommand.Execute(null);
                if (pantalla == "stats" && Editor is not null) Editor.EnStats = true;
                return Editando;
            case var s when s.StartsWith("editor:", StringComparison.Ordinal):
                // «editor:<pestana>[:sub=<subeditor>][:menu][:foco=<campo>]»: pruebas de dispositivo del editor de entidad (la parte :foco la atiende quien arranca, que conoce la vista).
                {
                    Destino = Destino.Pokemon;
                    if (Cajas?.Seleccionada is null) return false;
                    Cajas.EditarCommand.Execute(null);
                    if (Editor is null) return false;
                    var partes = s.Split(':', StringSplitOptions.RemoveEmptyEntries);
                    if (partes.Length > 1) Editor.SeleccionarPorId(partes[1]);
                    foreach (var p in partes.Skip(2))
                    {
                        if (p.StartsWith("sub=", StringComparison.Ordinal)) Editor.AbrirSubeditor(p[4..], "mote");
                        else if (p == "menu") Editor.MenuCommand.Execute(null);
                        else if (p.StartsWith("campo=", StringComparison.Ordinal)) Editor.ElegirCampo(p[6..]);
                    }
                    return Editando;
                }
            case var s when s.StartsWith("cajas:sel=", StringComparison.Ordinal):
                // «cajas:sel=<n>»: abre la pestana Pokemon con la casilla n (1-based) de la caja actual elegida (pruebas de dispositivo: los rectangulos de accesibilidad dentro del desplazador no sirven para tocar la rejilla).
                Destino = Destino.Pokemon;
                if (Cajas is null || !int.TryParse(s["cajas:sel=".Length..], out var n) || n < 1 || n > Cajas.Casillas.Count) return false;
                Cajas.Seleccionar(Cajas.Casillas[n - 1]);
                return Cajas.Ficha is not null;
            case var s when s.StartsWith("libreria:caja=", StringComparison.Ordinal):
                // «libreria:caja=<n>»: abre la Libreria con la caja n (1-based) como caja activa (pruebas de dispositivo de «Anadir a la caja» y de colocar en una casilla).
                if (Cajas is null || !int.TryParse(s["libreria:caja=".Length..], out var nc) || nc < 1 || nc > Cajas.TotalCajas) return false;
                Cajas.IrACaja(nc - 1); Destino = Destino.Libreria; return true;
            case "libreria": Destino = Destino.Libreria; return true;
            case "mochila": Destino = Destino.Mochila; return true;
            case "partida":
                // Igual que «Volver» del editor: con cambios sin aplicar pregunta antes en vez de descartarlos en silencio.
                Destino = Destino.Partida;
                if (PartidaVm.Editor is { HayCambios: true } e) { e.Volver(); return true; }
                PartidaVm.Editor = null; return true;
            case var p when p.StartsWith("partida:", StringComparison.Ordinal):
                // «partida:<editor>[:pestana]»: abre un editor de Partida (pruebas de dispositivo y capturas).
                {
                    Destino = Destino.Partida;
                    var partes = p.Split(':', StringSplitOptions.RemoveEmptyEntries);
                    // «partida:esp=<entrada>[:pestana]» abre un formulario de «Específicos del juego» (F8).
                    if (!PartidaVm.AbrirEditor(partes[1].StartsWith("esp=", StringComparison.Ordinal) ? "esp:" + partes[1][4..] : partes[1])) return false;
                    if (partes.Length > 2) PartidaVm.Editor!.IrAPestana(partes[2]);
                    return true;
                }
            case "mas": Destino = Destino.Mas; return true;
            case "registros": Destino = Destino.Mas; AbrirRegistros(); return ViendoRegistros;
            case "historial": Destino = Destino.Mas; AbrirHistorial(); return ViendoPantalla;
            case "ajustes": Destino = Destino.Mas; AbrirAjustes(); return ViendoPantalla;
            case "masiva": Destino = Destino.Mas; AbrirEdicionMasiva(AlcanceMasivo.DeTodo()); return ViendoPantalla;
            case "acerca": Destino = Destino.Mas; AbrirAcercaDe(); return ViendoPantalla;
            default: return false;
        }
    }

    /// <summary>
    /// Cómo sale de la app cuando el usuario lo confirma (la plataforma lo da: en Android, <c>Activity.Finish()</c>). Sin él, la confirmación de salida con cambios
    /// sin guardar no tiene a dónde ir y Atrás no hace nada en la pantalla principal con cambios.
    /// </summary>
    public Action? SalirDeLaApp { get; set; }

    /// <summary>
    /// Atrás del sistema (botón, gesto lateral o gesto predictivo): devuelve true si lo ha gestionado y false si la app debe salir. Siempre hace UNA sola cosa, la primera que corresponda, en este orden:
    /// 1) cerrar la hoja, el menú o la confirmación abiertos (los desplegables los cierra la vista antes de llegar aquí);
    /// 2) la operación en curso no se abandona sin más: pregunta si se cancela (y «Seguir esperando» la deja como estaba);
    /// 3) salir de la búsqueda, de la selección, del modo colocar o del arrastre de las cajas;
    /// 4) volver del editor, de Registros o de la pantalla, y de la pestaña a la principal;
    /// 5) solo en la pantalla principal y sin nada abierto, salir de la app, pidiendo confirmación si hay cambios sin guardar.
    /// </summary>
    public bool Atras()
    {
        if (Hoja is AccionHojaViewModel { Trabajando: true } accion) { PedirCancelarOperacion(accion.Cancelando ? null : () => accion.CancelarCommand.Execute(null), () => AbrirHoja(accion)); return true; }
        if (Hoja is not null) { if (Hoja.CierraConFondo) Hoja = null; return true; }
        // Una operación masiva en marcha no se abandona con Atrás sin preguntar: se pregunta si se cancela (si no, seguiría escribiendo en una pantalla que ya no se ve).
        if (Pantalla is AsistenteMasivoViewModel { Ocupado: true } ocupado) { PedirCancelarOperacion(ocupado.Cancelando ? null : () => ocupado.CancelarCommand.Execute(null), null); return true; }
        if (Cajas is { } cajas && Destino == Destino.Pokemon && !Editando && !ViendoRegistros && Pantalla is null && SalirDeModoDeCajas(cajas)) return true;
        if (Editando) { CerrarEditor(); return true; }
        if (ViendoRegistros) { RegistrosVm = null; ViendoRegistros = false; return true; }
        // El asistente puede consumir Atrás para volver a un paso suyo (cerrar «Elegir Pokémon…», volver de la vista previa de eventos).
        if (Pantalla is AsistenteMasivoViewModel asistente && asistente.Atras()) return true;
        if (Pantalla is not null) { CerrarPantalla(); return true; }
        if (Destino == Destino.Partida && PartidaVm.Atras()) return true;
        if (Destino != Destino.Pokemon) { Destino = Destino.Pokemon; return true; }
        return PedirSalirDeLaApp();
    }

    /// <summary>Lo que tenga el modo de las cajas (arrastre, colocar, selección, búsqueda): lo cierra de uno en uno y devuelve true si había algo.</summary>
    private static bool SalirDeModoDeCajas(CajasViewModel cajas)
    {
        if (cajas.Arrastrando) { cajas.CancelarArrastre(); return true; }
        if (cajas.Colocando) { cajas.CancelarColocacion(); return true; }
        if (cajas.Seleccionando) { cajas.CancelarSeleccion(); return true; }
        if (cajas.Buscando) { cajas.QuitarBusqueda(); return true; }
        return false;
    }

    /// <summary>«¿Cancelar la operación?» con dos salidas: cancelar de verdad o seguir esperando (la hoja de progreso vuelve a verse).</summary>
    private void PedirCancelarOperacion(Action? cancelar, Action? volver)
    {
        if (cancelar is null) { volver?.Invoke(); return; }   // ya se está cancelando: Atrás solo devuelve a la hoja
        AbrirHoja(new ConfirmarHojaViewModel(IdiomaInterfaz.T("Atras.CancelarOperacion.Titulo"), [IdiomaInterfaz.T("Atras.CancelarOperacion.Texto")],
            IdiomaInterfaz.T("Atras.CancelarOperacion.Si"), () => { cancelar(); volver?.Invoke(); }, destructiva: true,
            textoCancelar: IdiomaInterfaz.T("Atras.CancelarOperacion.No"), alDescartar: volver));
    }

    /// <summary>
    /// Atrás en la pantalla principal sin nada abierto: sale (false, y la plataforma cierra la actividad), salvo que haya cambios sin guardar; entonces pregunta y, si el usuario insiste,
    /// sale por <see cref="SalirDeLaApp"/>. Devuelve true cuando se queda preguntando.
    /// </summary>
    private bool PedirSalirDeLaApp()
    {
        if (CambiosQueSePerderian() is not { } perdidos || SalirDeLaApp is null) return false;
        AbrirHoja(new ConfirmarHojaViewModel(IdiomaInterfaz.T("Atras.Salir.Titulo"), [IdiomaInterfaz.F("Atras.Salir.Texto", perdidos)],
            IdiomaInterfaz.T("Atras.Salir.Si"), () => SalirDeLaApp(), destructiva: true, textoCancelar: IdiomaInterfaz.T("Atras.Salir.No")));
        return true;
    }

    // ---- Acciones del motor sobre la casilla elegida ----
    private void LanzarAccionCasilla(AccionPokemon accion, Cajas.CasillaRef casilla)
    {
        if (_acciones is null || Sesion is null) return;
        var pk = Sesion.Casillas.Leer(casilla.Info);
        if (pk.Species == 0) return;
        var equipo = casilla.EsEquipo; var caja = casilla.Caja; var indice = casilla.Indice;
        // El mismo motivo que el escritorio da como ayuda del elemento desactivado (sin partida, sin nombre de entrenador, casilla vacía o bloqueada, operación en curso, motor).
        if (!Sesion.Casillas.EsEscribible(casilla.Info)) { Informar(IdiomaInterfaz.T("Shell.CasillaSoloLecturaCopia"), "aviso", "CircleAlert"); return; }
        if (_acciones.MotivoCasilla(accion, Sesion, equipo, caja, indice) is { } motivo) { Informar(motivo, "aviso", "CircleAlert"); return; }
        var nombre = Sesion.Textos.Species[pk.Species];
        var objetivo = $"{nombre} · {casilla.Descripcion}";
        void Tras() { Cambios = Sesion.CambiosPendientes; Cajas?.Refrescar(); _servicios?.Recuperacion(Sesion); }
        var hoja = new AccionHojaViewModel(accion, objetivo,
            (p, ct) => _acciones.EjecutarEnCasillaAsync(accion, Sesion, equipo, caja, indice, p, ct),
            terminada: r => { if (r.Aplicado) Tras(); },   // la acción ya anotó su escritura en el historial único (Sesion.Casillas)
            deshecho: Tras,                                  // un Deshacer exacto del historial; aquí solo se refresca la vista
            mostrarResumen: () => AlmacenDeAjustes.Actual.ShowSummary, abrirHoja: AbrirHoja, portapapeles: () => Portapapeles);
        AbrirHoja(hoja);
        _ = hoja.Lanzar();
    }

    private async Task Proteger(Func<Task> accion)
    {
        if (Ocupado) return;
        Ocupado = true;
        try { await accion(); }
        catch (AlmacenException ex) { Informar(ex.Message, ex.Resultado == ResultadoGuardado.OriginalCambiado ? "aviso" : "error", ex.Resultado == ResultadoGuardado.OriginalCambiado ? "CircleAlert" : "TriangleAlert"); }
        catch (OperationCanceledException) { }
        finally { Ocupado = false; }
    }

    [RelayCommand]
    private Task Abrir() => Proteger(async () =>
    {
        if (_servicios is null) { Informar(IdiomaInterfaz.T("Shell.AbrirGuardadosDisponibleVentana"), "aviso", "CircleAlert"); return; }
        // F10-2: con el despachador global, «Abrir» abre cualquier archivo que lea PKHeX (ShellViewModel.Abrir.cs); sin él, solo guardados.
        if (_servicios.Abridor is not null && Documentos is { } docs) { if (await docs.ElegirParaAbrirAsync(CancellationToken.None) is { } doc) await AbrirDocumentoSinProteger(doc, Pokekeep.Mobile.Ui.Abrir.OrigenArchivo.Selector); return; }
        var antes = _servicios.Recientes();
        var s = await _servicios.AbrirAsync(CancellationToken.None);
        if (s is not null) CambiarDeSesion(s, antes);   // SlotWrite.ModifyUnset: avisa si se perderían cambios (ShellViewModel.CambiosSinGuardar.cs)
    });

    [RelayCommand]
    private Task AbrirReciente(FilaReciente? fila) => Proteger(async () =>
    {
        if (fila is null || _servicios is null) return;
        // F10-2: un reciente se abre por el mismo despachador que «Abrir archivo…» (una tarjeta de GameCube con varias partidas vuelve a preguntar cuál; un .zip sigue siendo un .zip).
        if (_servicios.Abridor is not null && await _servicios.DocumentoRecienteAsync(fila.Referencia, CancellationToken.None) is { } doc) { await AbrirDocumentoSinProteger(doc, Pokekeep.Mobile.Ui.Abrir.OrigenArchivo.Selector); return; }
        var antes = _servicios.Recientes();
        var s = await _servicios.AbrirRecienteAsync(fila.Referencia, CancellationToken.None);
        if (s is not null) CambiarDeSesion(s, antes);
    });

    /// <summary>Nombres de lo que tiene un borrador SIN APLICAR a la partida (la Mochila y el editor abierto): Guardar y Exportar solo escriben lo ya aplicado y no deben ignorarlo en silencio.</summary>
    public string? BorradoresSinAplicar()
    {
        List<string> l = [];
        // «Comprobar si el Pokémon del editor tiene cambios sin guardar al exportar» (Advanced.SaveExportCheckUnsavedEntity, bloque 10-4): el MsgProgramSaveUnsaved de PKHeX.
        if (AjustesPkhex.Actual.Advanced.SaveExportCheckUnsavedEntity && Editor is { HayCambios: true }) l.Add(IdiomaInterfaz.T("Shell.PokemonEditor"));
        if (MochilaVm is { HayCambios: true }) l.Add(IdiomaInterfaz.T("Shell.Mochila"));
        if (PartidaVm.Editor is { HayCambios: true } e) l.Add($"«{e.Titulo}»");
        if (PartidaVm.Eventos is { HayCambios: true }) l.Add(IdiomaInterfaz.T("Partida.Editor.eventos.Titulo"));
        if (PartidaVm.Regalos is { HayCambios: true }) l.Add(IdiomaInterfaz.T("Partida.Editor.regalos.Titulo"));
        if (PartidaVm.GoPark is { HayCambios: true }) l.Add(IdiomaInterfaz.T("Shell.ComplejoGO"));
        return l.Count == 0 ? null : string.Join(", ", l);
    }

    /// <summary>Si hay borradores sin aplicar, avisa antes de guardar o exportar (que no los incluyen); si el usuario sigue, ejecuta la accion.</summary>
    private Task ConAvisoDeBorradores(string verbo, Func<Task> accion)
    {
        if (BorradoresSinAplicar() is not { } b) return Proteger(accion);
        AbrirHoja(new ConfirmarHojaViewModel(IdiomaInterfaz.T("Form.Salir.Titulo"),
            [IdiomaInterfaz.F("Shell.CambiosAplicar2", b), IdiomaInterfaz.F("Shell.SoloIncluyeAplicadoPartida", verbo), IdiomaInterfaz.T("Shell.AplicalosAntesBotonAplicar")], IdiomaInterfaz.F("Shell.Ellos", verbo), () => _ = Proteger(accion)));
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task Guardar() => ConAvisoDeBorradores(IdiomaInterfaz.T("Shell.Guardar"), async () =>
    {
        if (Sesion is null || _servicios is null) return;
        if (Sesion.Documento is { SoloLectura: true })
        {
            AbrirHoja(new MenuHojaViewModel(IdiomaInterfaz.T("Shell.GuardarPartida"), [new SeccionHoja(null,
            [new(IdiomaInterfaz.T("Shell.GuardarEnOriginal"), IdiomaInterfaz.T("Shell.EligeMismoArchivoPermiso"), "Save", () => GuardarEnOriginalCommand.Execute(null)),
             new(IdiomaInterfaz.T("Shell.ExportarCopia"), IdiomaInterfaz.T("Shell.GuardaCambiosArchivoNuevo"), "Download", () => ExportarCopiaCommand.Execute(null))])]));
            return;
        }
        // «Forzar "Guardar como"» (Advanced.SaveExportForceSaveAs, bloque 10-4): PKHeX no ofrece sobrescribir el archivo y abre siempre el selector; aquí, «Guardar» exporta una copia donde elija el usuario.
        // Lo mismo con una partida sin documento (copia de la app, carga forzada): PKHeX no sobrescribe una copia (Metadata.IsBackup) ni lo que no tiene ruta.
        if (AjustesPkhex.Actual.Advanced.SaveExportForceSaveAs || Sesion.Documento is null)
        {
            var copia = await _servicios.ExportarCopiaAsync(Sesion, CancellationToken.None);
            if (copia is null) return;
            Informar(copia.Mensaje, copia.Resultado == ResultadoGuardado.Verificado ? "ok" : copia.Resultado == ResultadoGuardado.SinComprobar ? "aviso" : "error", copia.Resultado == ResultadoGuardado.Verificado ? "CircleCheck" : "CircleAlert");
            return;
        }
        await GuardarOriginalSinProteger();
    });

    private async Task GuardarOriginalSinProteger()
    {
        if (Sesion is null || _servicios is null) return;
        var inf = await _servicios.GuardarAsync(Sesion, CancellationToken.None);
        Cambios = Sesion.CambiosPendientes;
        var (clase, glifo) = inf.Resultado switch
        {
            ResultadoGuardado.Verificado => ("ok", "CircleCheck"),
            ResultadoGuardado.SinComprobar => ("aviso", "CircleAlert"),
            ResultadoGuardado.OriginalCambiado => ("aviso", "CircleAlert"),
            _ => ("error", "TriangleAlert"),
        };
        Informar(inf.Mensaje, clase, glifo);
    }

    [RelayCommand]
    private Task GuardarEnOriginal() => ConAvisoDeBorradores(IdiomaInterfaz.T("Shell.Guardar"), async () =>
    {
        if (Sesion is null || _servicios is null) return;
        try
        {
            if (await _servicios.AutorizarOriginalAsync(Sesion, CancellationToken.None)) await GuardarOriginalSinProteger();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            Informar(IdiomaInterfaz.T("Shell.PudoComprobarAutorizarOriginal"), "aviso", "CircleAlert", IdiomaInterfaz.T("Shell.ExportarCopia"), ExportarCopiaCommand);
        }
    });

    [RelayCommand]
    private Task ExportarCopia() => ConAvisoDeBorradores(IdiomaInterfaz.T("Editor.Exportar"), async () =>
    {
        if (Sesion is null || _servicios is null) return;
        var inf = await _servicios.ExportarCopiaAsync(Sesion, CancellationToken.None);
        if (inf is null) return;
        Informar(inf.Mensaje, inf.Resultado == ResultadoGuardado.Verificado ? "ok" : inf.Resultado == ResultadoGuardado.SinComprobar ? "aviso" : "error", inf.Resultado == ResultadoGuardado.Verificado ? "CircleCheck" : "CircleAlert");
    });

    [RelayCommand]
    private Task ElegirCarpeta() => Proteger(async () =>
    {
        if (_servicios is null) return;
        var c = await _servicios.ElegirCarpetaCopiasAsync(CancellationToken.None);
        if (c is not null) Informar(IdiomaInterfaz.F("Shell.CarpetaCopiasRecordaraReiniciar", c.Nombre), "ok", "CircleCheck");
    });

    [RelayCommand]
    private Task GuardarEnCarpeta() => ConAvisoDeBorradores(IdiomaInterfaz.T("Shell.GuardarCopia"), async () =>
    {
        if (Sesion is null || _servicios is null) return;
        if (await _servicios.CarpetaCopiasAsync(CancellationToken.None) is null)
        {
            var c = await _servicios.ElegirCarpetaCopiasAsync(CancellationToken.None);
            if (c is null) return;
        }
        var inf = await _servicios.GuardarEnCarpetaAsync(Sesion, CancellationToken.None);
        if (inf is null) return;
        Informar(inf.Mensaje, inf.Resultado == ResultadoGuardado.Verificado ? "ok" : inf.Resultado == ResultadoGuardado.SinComprobar ? "aviso" : "error", inf.Resultado == ResultadoGuardado.Verificado ? "CircleCheck" : "CircleAlert");
    });

    [RelayCommand]
    private Task Recuperar(FilaRecuperable? fila) => Proteger(async () =>
    {
        if (fila is null || _servicios is null) return;
        var s = await _servicios.RecuperarAsync(fila.Referencia, CancellationToken.None);
        CambiarDeSesion(s);
    });

    [RelayCommand]
    private void DescartarRecuperacion(FilaRecuperable? fila)
    {
        if (fila is null || _servicios is null) return;
        _servicios.Descartar(fila.Referencia);
        RefrescarRecientes();
        Informar(IdiomaInterfaz.T("Shell.SesionDescartadaArchivoOriginal"), "ok", "CircleCheck");
    }

    private void RefrescarRecientes()
    {
        if (_servicios is null) return;
        Recientes = [.. _servicios.Recientes().Select(r => new FilaReciente(r, $"{JuegoDeReciente(r.Juego)} · {Hace(r.Fecha)}"))];
        Recuperables = [.. _servicios.Recuperables().Select(r => new FilaRecuperable(r, $"{JuegoDeReciente(r.Juego)} · {IdiomaInterfaz.F("Inicio.CambiosSinGuardar", Hace(r.Fecha))}"))];
        if (_servicios.Documentos is not null) { Recientes = []; _ = ValidarRecientesAsync(); }
    }

    private static string Hace(DateTimeOffset f)
    {
        var t = DateTimeOffset.UtcNow - f;
        return t.TotalMinutes < 1 ? IdiomaInterfaz.T("Inicio.Ahora") : t.TotalHours < 1 ? IdiomaInterfaz.F("Inicio.Minutos", (int)t.TotalMinutes) : t.TotalDays < 1 ? IdiomaInterfaz.F("Inicio.Horas", (int)t.TotalHours) : IdiomaInterfaz.F("Inicio.Dias", (int)t.TotalDays);
    }

    public Task ValidacionRecientes { get; private set; } = Task.CompletedTask;
    public Task ValidarRecientesAsync()
    {
        if (!ValidacionRecientes.IsCompleted) return ValidacionRecientes;
        Recientes = []; // nunca ofrecer una URI aún sin comprobar
        return ValidacionRecientes = ComprobarRecientes();
    }
    private async Task ComprobarRecientes()
    {
        if (_servicios is null) return;
        var vigentes = await _servicios.RevisarRecientesAsync(CancellationToken.None);
        Recientes = [.. vigentes.Select(r => new FilaReciente(r, JuegoDeReciente(r.Juego)))];
    }
    [RelayCommand] private Task ActualizarRecientes() => ValidarRecientesAsync();

    /// <summary>«Completar el juego» (bloque 9b): la pantalla de opciones y vista previa. Sin partida o sin puerto no hace nada.</summary>
    [RelayCommand]
    public void AbrirCompletarJuego()
    {
        if (_masivas is not ICompletarJuego completar) return;
        if (Sesion is null) { Informar(IdiomaInterfaz.T("Shell.AbrePartidaCompletarJuego"), "aviso", "CircleAlert"); return; }
        Pantalla = new CompletarJuegoViewModel(completar, Sesion, AlmacenDeAjustes, CerrarPantalla, (t, c, g) => Informar(t, c, g), () => Portapapeles, () => Documentos);
    }

    string? IAnfitrionCajas.MotivoCompletarJuego => _masivas is not ICompletarJuego completar
        ? IdiomaInterfaz.T("MasivasSupervisadas.CompletarElJuegoNo")
        : Sesion is null ? IdiomaInterfaz.T("Shell.AbrePartidaCompletarJuego") : completar.MotivoCompletar(Sesion);

    // ---- Historial, Ajustes y Acerca de ----
    [RelayCommand]
    private void AbrirHistorial() => Pantalla = new HistorialViewModel(Sesion, CerrarPantalla, (t, c, g) => Informar(t, c, g));

    [RelayCommand]
    private void AbrirAjustes() => Pantalla = new AjustesViewModel(AlmacenDeAjustes, CerrarPantalla, AbrirHoja, (t, c, g) => Informar(t, c, g), AjustesPkhex, p => Pantalla = p, () => Carpetas, _servicios?.CarpetaPrivada);

    [RelayCommand]
    private void AbrirAcercaDe()
    {
        if (ProveedorAcercaDe is null) { Informar(IdiomaInterfaz.T("Shell.AcercaDisponibleVentana"), "aviso", "CircleAlert"); return; }
        Pantalla = new AcercaDeViewModel(ProveedorAcercaDe, CerrarPantalla, Portapapeles) { VerGestos = AbrirGuiaGestosCommand };
    }

    // ---- Operaciones masivas (F9) ----
    [RelayCommand]
    private void AbrirEdicionMasivaTodo() => AbrirEdicionMasiva(AlcanceMasivo.DeTodo());

    /// <summary>Abre la Edición masiva sobre un ámbito (Más → Todo, o la selección de la pestaña Pokémon). Sin partida o sin puerto de masivas no hace nada.</summary>
    public void AbrirEdicionMasiva(AlcanceMasivo alcance)
    {
        if (_masivas is null) return;
        if (Sesion is null) { Informar(IdiomaInterfaz.T("Shell.AbrePartidaUsarEdicion"), "aviso", "CircleAlert"); return; }
        Pantalla = new EdicionMasivaViewModel(_masivas, Sesion, alcance, () => AlmacenDeAjustes.Actual.BulkWorkers, CerrarPantalla, (t, c, g) => Informar(t, c, g), () => Portapapeles, () => Documentos,
            procesos => { AlmacenDeAjustes.Actual.BulkWorkers = procesos; AlmacenDeAjustes.Guardar(); });
    }

    /// <summary>Menú de la caja (parche 0006 del escritorio): «Edición masiva de esta caja…» abre «Cajas concretas» con la caja marcada; «de todas las cajas…», el ámbito Todo.</summary>
    Masivas.TextosEdicionMasiva? IAnfitrionCajas.TextosMasivas => _masivas?.TextosEdicion;
    void IAnfitrionCajas.AbrirEdicionMasivaDeCaja(int? caja) => AbrirEdicionMasiva(caja is { } c ? AlcanceMasivo.DeCajas([c]) : AlcanceMasivo.DeTodo());
    void IAnfitrionCajas.AbrirEdicionMasivaIlegales() => AbrirEdicionMasiva(AlcanceMasivo.DeIlegales());

    string? IAnfitrionCajas.MotivoEdicionMasiva => _masivas is null ? IdiomaInterfaz.T("Shell.EdicionMasivaDisponibleVentana") : Sesion is null ? IdiomaInterfaz.T("Shell.AbrePartidaUsarEdicion") : _masivas.MotivoEdicion(Sesion);
    void IAnfitrionCajas.AbrirEdicionMasiva(IReadOnlyList<Cajas.CasillaRef> casillas)
    {
        if (casillas.Count == 0) return;
        AbrirEdicionMasiva(casillas.Count == 1 ? AlcanceMasivo.DeEste(casillas[0].Info) : AlcanceMasivo.DeMarcados(casillas.Select(c => c.Info)));
    }

    public void CerrarPantalla() => Pantalla = null;

    /// <summary>La pantalla de «Más» que sale se suelta SIEMPRE (cerrar, abrir otra encima o <c>Navegar</c>): el Historial está suscrito a <c>Sesion.CasillasCambiadas</c> y,
    /// si solo se soltaba al cerrar, el sustituido quedaba retenido por la sesión (revisión de la F6, <c>FugasPantallasMasTests</c>).</summary>
    partial void OnPantallaChanging(PantallaMasViewModel? oldValue, PantallaMasViewModel? newValue)
    {
        if (!ReferenceEquals(oldValue, newValue) && oldValue is IDisposable saliente) saliente.Dispose();
    }

    // ---- IAnfitrionLibreria ----
    IAccionesPokekeep? IAnfitrionLibreria.Acciones => _acciones;
    AjustesPokekeep IAnfitrionLibreria.Ajustes => AlmacenDeAjustes.Actual;
    int IAnfitrionLibreria.CajaActiva => Cajas?.Caja ?? 0;
    IAlmacenDocumentos? IAnfitrionLibreria.Documentos => Documentos;
    IPortapapeles? IAnfitrionLibreria.Portapapeles => Portapapeles;
    void IAnfitrionLibreria.TrasCambioDeLibreria() { if (Sesion is null) return; Cambios = Sesion.CambiosPendientes; Cajas?.Refrescar(); _servicios?.Recuperacion(Sesion); }
    void IAnfitrionLibreria.ElegirCasillaParaColocar(EntradaLibreria entrada, Action<DestinoCasilla> colocar)
    {
        if (Cajas is null) return;
        Destino = Destino.Pokemon;
        Cajas.IniciarColocacionLibreria(entrada, colocar);
    }

    // ---- Registros ----
    [RelayCommand]
    private void AbrirRegistros()
    {
        if (_registros is null) return;
        RegistrosVm = new RegistrosViewModel(_registros, () => { ViendoRegistros = false; RegistrosVm = null; });
        ViendoRegistros = true;
    }
}
