// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pokekeep.Mobile.Ui.Ajustes.Pkhex;
using Pokekeep.Mobile.Ui.Almacen;
using Pokekeep.Mobile.Ui.Modelo;
using Pokekeep.Mobile.Ui.ViewModels;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Herramientas;

/// <summary>
/// Lo que la Shell le da a una herramienta de «Más &gt; Herramientas» (F10, ADR 0072): la partida abierta (si hay), los ajustes de PKHeX VIGENTES (siempre <c>Ajustes.Actual</c>, nunca una copia vieja), las hojas
/// inferiores, los avisos, el portapapeles, los documentos y carpetas del usuario (SAF) y la navegación. Una herramienta NO conoce la Shell: solo este contexto, así que se prueba sin pantalla con <see cref="ContextoDePruebas"/>.
/// </summary>
public sealed record ContextoHerramienta(
    Sesion? Sesion,
    IAjustesPkhex Ajustes,
    Action<HojaViewModel> AbrirHoja,
    Action<string, string, string> Informar,
    Func<IPortapapeles?> Portapapeles,
    Func<IAlmacenDocumentos?> Documentos,
    Func<IAlmacenCarpetas?> Carpetas,
    Action<PantallaMasViewModel?> Abrir)
{
    /// <summary>Bloque 10-4: los servicios de partida de la Shell (abrir un reciente o un guardado ya reconocido, exportar), o null en anfitriones sin almacén.</summary>
    public IServiciosPartida? Servicios { get; init; }

    /// <summary>
    /// Bloque 10-4: abre un documento del usuario por el despachador de la Shell (el camino de «Abrir archivo…» del bloque 10-2, el <c>OpenSAV</c> de PKHeX: comprobación de versión, tarjeta de GameCube,
    /// .zip, avisos y copia automática). Null = sin despachador (se usa <see cref="AbrirSesion"/>).
    /// </summary>
    public Func<DocumentoRef, Task>? AbrirDocumento { get; init; }

    /// <summary>
    /// Bloque 10-4: abre una partida SIN documento (una copia de la app, una carga forzada). La Shell ejecuta <c>abrir</c> con su cerrojo de «ocupado» (como cualquier apertura) y, si da una sesión, la deja
    /// abierta. Devuelve si se abrió. Null = la herramienta no puede abrir partidas.
    /// </summary>
    public Func<Func<Task<Sesion?>>, Task<bool>>? AbrirSesion { get; init; }

    /// <summary>Abre unos bytes como si fueran un archivo (el despachador global de la Shell, bloque 10-2: «Importar hexadecimal»). Null donde no hay Shell (pruebas que no lo usan).</summary>
    public Func<byte[], string, Task>? AbrirBytes { get; init; }

    /// <summary>
    /// Lee los archivos de una carpeta del usuario y de sus subcarpetas (nombre y bytes): el «Carpeta…» del editor por lotes (bloque 10-7), que en PKHeX recorre la carpeta con <c>SearchOption.AllDirectories</c>.
    /// Null donde no se puede listar una carpeta (hoy en Android: el listado de SAF llega con el bloque 10-4, <c>IAlmacenCarpetas.ListarAsync</c>); la herramienta lo dice en vez de enseñar un botón muerto.
    /// </summary>
    public Func<Almacen.CarpetaRef, CancellationToken, Task<IReadOnlyList<(string Nombre, byte[] Datos)>>>? LeerCarpeta { get; init; }
    /// <summary>La caja que se está viendo en la pestaña Pokémon (la «caja actual» de las exportaciones de Showdown, bloque 10-9). Null donde no hay Shell: la primera caja.</summary>
    public Func<int>? CajaActual { get; init; }

    /// <summary>X38: el motor de Pokekeep (para «Legalizar» antes de sacar un Pokémon del banco a la partida). Null donde no hay motor (pruebas que no lo usan).</summary>
    public IAccionesPokekeep? Acciones { get; init; }

    /// <summary>Un contexto para pruebas: ajustes en memoria (sin tocar el Core si <paramref name="aplicar"/> es false), portapapeles en memoria y avisos recogidos en una lista.</summary>
    public static ContextoHerramienta ContextoDePruebas(Sesion? sesion, out List<(string Texto, string Clase)> avisos, out PortapapelesMemoria portapapeles, IAjustesPkhex? ajustes = null, IAlmacenDocumentos? documentos = null, IAlmacenCarpetas? carpetas = null)
    {
        var lista = new List<(string, string)>();
        avisos = lista;
        var clip = new PortapapelesMemoria();
        portapapeles = clip;
        return new ContextoHerramienta(sesion, ajustes ?? new AjustesPkhexEnMemoria(aplicar: false), _ => { }, (t, c, _) => lista.Add((t, c)), () => clip, () => documentos, () => carpetas, _ => { });
    }
}

/// <summary>
/// Una herramienta de «Más &gt; Herramientas» tal y como la declara su bloque: título y glifo (el nombre de PKHeX en castellano, nunca uno inventado), las filas del censo que cierra (<see cref="Filas"/>), si necesita
/// una partida y cómo se crea. <see cref="Motivo"/> devuelve por qué NO está disponible (null = disponible): una entrada deshabilitada siempre dice por qué, nunca es un botón muerto.
/// </summary>
/// <param name="Estados">Estados de la pantalla que el gate visual debe dibujar además del inicial (p. ej. «con resultado»): el bloque da cómo dejar la pantalla así SIN esperar a un trabajo en segundo plano.</param>
/// <param name="ConCampos">La pantalla tiene campos de texto o número (el gate la prueba también con el teclado abierto).</param>
public sealed record DefHerramienta(string Id, string Titulo, string Descripcion, string Glifo, string Categoria, string Bloque, IReadOnlyList<string> Filas,
    Func<Sesion?, string?> Motivo, Func<ContextoHerramienta, Action, HerramientaViewModel> Crear, IReadOnlyList<EstadoPantalla>? Estados = null, bool ConCampos = false)
{
    public static string SinPartida => IdiomaInterfaz.T("Herramientas.AbrePartidaUsarHerramienta");
    public static string? ExigePartida(Sesion? s) => s is null ? SinPartida : null;
    public static string? SiempreDisponible(Sesion? s) => null;
}

/// <summary>Un estado de una pantalla de herramienta para el gate visual y las capturas: su id (<c>f10-integridad-verificado</c>) y cómo dejarla así, de forma síncrona.</summary>
public sealed record EstadoPantalla(string Id, Action<HerramientaViewModel> Preparar, bool ConTeclado = false);

/// <summary>
/// Catálogo de herramientas. Cada bloque de la F10 añade las suyas en SU fichero parcial (<c>CatalogoHerramientas.B10_N.cs</c>, método <c>AnadirB10_N</c>) sin tocar este: así dos bloques en paralelo no se pisan.
/// La prueba <c>CatalogoHerramientasTests</c> exige que cada herramienta tenga una vista registrada (<see cref="VistasHerramientas"/>), que sus filas existan en el censo y que ninguna se repita.
/// </summary>
public static partial class CatalogoHerramientas
{
    /// <summary>Categorías, en el orden en que salen en el concentrador.</summary>
    public static string[] Categorias => [IdiomaInterfaz.T("CatalogoHerramientas.Partida"), IdiomaInterfaz.T("CatalogoHerramientas.BuscarGenerar"), IdiomaInterfaz.T("Zonas.Cajas"), IdiomaInterfaz.T("CatalogoHerramientas.EdicionLotes"), IdiomaInterfaz.T("Herramientas.ImportarExportar"), IdiomaInterfaz.T("TablaP.Ayuda")];

    private static readonly PorIdioma<IReadOnlyList<DefHerramienta>> _todas = new(() =>
    {
        var l = new List<DefHerramienta>();
        AnadirPiloto(l);
        AnadirB10_1(l); AnadirB10_2(l); AnadirB10_3(l); AnadirB10_4(l); AnadirB10_5(l); AnadirB10_6(l); AnadirB10_7(l); AnadirB10_8(l); AnadirB10_9(l); AnadirB10_10(l); AnadirB10_11(l); AnadirB10_12(l); AnadirB10_13(l);
        var repetidas = l.GroupBy(h => h.Id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (repetidas.Count > 0) throw new InvalidOperationException("Herramientas repetidas: " + string.Join(", ", repetidas));
        return l;
    });

    public static IReadOnlyList<DefHerramienta> Todas => _todas.Value;
    public static DefHerramienta? Buscar(string id) => Todas.FirstOrDefault(h => h.Id == id);

    static partial void AnadirPiloto(List<DefHerramienta> l);
    static partial void AnadirB10_1(List<DefHerramienta> l);
    static partial void AnadirB10_2(List<DefHerramienta> l);
    static partial void AnadirB10_3(List<DefHerramienta> l);
    static partial void AnadirB10_4(List<DefHerramienta> l);
    static partial void AnadirB10_5(List<DefHerramienta> l);
    static partial void AnadirB10_6(List<DefHerramienta> l);
    static partial void AnadirB10_7(List<DefHerramienta> l);
    static partial void AnadirB10_8(List<DefHerramienta> l);
    static partial void AnadirB10_9(List<DefHerramienta> l);
    static partial void AnadirB10_10(List<DefHerramienta> l);
    static partial void AnadirB10_11(List<DefHerramienta> l);
    static partial void AnadirB10_12(List<DefHerramienta> l);
    static partial void AnadirB10_13(List<DefHerramienta> l);
}

/// <summary>Un trabajo en curso: texto, fracción (null = indeterminado) y si se puede cancelar.</summary>
public readonly record struct AvanceTrabajo(string Texto, double? Fraccion);

/// <summary>
/// Base de todas las herramientas: una pantalla a pantalla completa colgada de «Más &gt; Herramientas» con «Volver», un trabajo en segundo plano CANCELABLE con avance (<see cref="EjecutarAsync{T}"/>) y la regla de la
/// F10: <b>una herramienta que solo lee no toca la partida</b>; la que escribe lo hace por <c>Sesion.Casillas</c> (historial único, Deshacer exacto) o por una acción explícita del usuario (regla 13).
/// </summary>
public abstract partial class HerramientaViewModel : PantallaMasViewModel, IDisposable
{
    private CancellationTokenSource? _cts;

    protected HerramientaViewModel(ContextoHerramienta contexto, Action volver) : base(volver) => Contexto = contexto;

    protected ContextoHerramienta Contexto { get; }
    protected Sesion? Sesion => Contexto.Sesion;
    protected AjustesPkhex Ajustes => Contexto.Ajustes.Actual;

    /// <summary>Texto corto de estado bajo el título («Comprobando 312 Pokémon…», «Listo»).</summary>
    [ObservableProperty] private string _estado = string.Empty;
    [ObservableProperty] private bool _ocupado;
    [ObservableProperty] private double _fraccion;
    [ObservableProperty] private bool _fraccionIndeterminada = true;
    [ObservableProperty] private string _textoAvance = string.Empty;
    public bool NoOcupado => !Ocupado;
    partial void OnOcupadoChanged(bool value) => OnPropertyChanged(nameof(NoOcupado));
    public bool HayEstado => !string.IsNullOrEmpty(Estado);
    partial void OnEstadoChanged(string value) => OnPropertyChanged(nameof(HayEstado));

    /// <summary>Se dispara al terminar (bien, cancelado o con error) cada trabajo lanzado con <see cref="EjecutarAsync{T}"/>: las pruebas esperan a este suceso, nunca duermen.</summary>
    public event Action? TrabajoTerminado;

    /// <summary>
    /// Ejecuta <paramref name="trabajo"/> en un hilo aparte con avance y cancelación. Devuelve su resultado, o <c>default</c> si se canceló o falló (el fallo se avisa con texto claro y queda en el registro;
    /// nunca una pantalla en blanco ni un cierre). Un segundo trabajo mientras hay uno en curso se rechaza: la pantalla ya muestra «Cancelar».
    /// </summary>
    protected async Task<T?> EjecutarAsync<T>(string texto, Func<CancellationToken, IProgress<AvanceTrabajo>, T> trabajo)
    {
        if (Ocupado) return default;
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        Ocupado = true; TextoAvance = texto; FraccionIndeterminada = true; Fraccion = 0; Estado = texto;
        var avance = new Progress<AvanceTrabajo>(a => { TextoAvance = a.Texto; FraccionIndeterminada = a.Fraccion is null; Fraccion = a.Fraccion ?? 0; });
        try { return await Task.Run(() => trabajo(token, avance), token).ConfigureAwait(true); }
        catch (OperationCanceledException) { Estado = TextoCancelado(); return default; }
        catch (Exception ex)
        {
            Estado = TextoFallo(ex);
            Contexto.Informar(Estado, "error", "CircleAlert");
            return default;
        }
        finally
        {
            Ocupado = false; _cts?.Dispose(); _cts = null;
            TrabajoTerminado?.Invoke();
        }
    }

    /// <summary>El texto al cancelar un trabajo. Una herramienta que ya ha escrito algo fuera de la partida (archivos en una carpeta) lo sustituye para no decir «No se ha cambiado nada» (M-5 de la auditoría de Fable).</summary>
    protected virtual string TextoCancelado() => IdiomaInterfaz.T("Herramientas.CanceladoCambiadoNada");

    /// <summary>El texto cuando el trabajo falla. Igual que <see cref="TextoCancelado"/>, una herramienta que ya escribió algo lo puede sustituir.</summary>
    protected virtual string TextoFallo(Exception ex) => IdiomaInterfaz.T("Herramientas.PudoTerminar") + ex.GetType().Name + IdiomaInterfaz.T("Cajas.CambiadoNada");

    [RelayCommand]
    public void Cancelar() => _cts?.Cancel();

    public virtual void Dispose() { _cts?.Cancel(); _cts?.Dispose(); _cts = null; GC.SuppressFinalize(this); }
}
