// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Reflection;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pokekeep.Mobile.Ui.Ajustes;
using Pokekeep.Mobile.Ui.Ajustes.Pkhex;
using Pokekeep.Mobile.Ui.Cajas;
using Pokekeep.Mobile.Ui.Mas;
using Pokekeep.Mobile.Ui.Modelo;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.ViewModels;

/// <summary>Una pantalla a pantalla completa colgada de «Más» (Historial, Ajustes, Acerca de): lleva su «Volver a Más».</summary>
public abstract partial class PantallaMasViewModel : ObservableObject
{
    protected PantallaMasViewModel(Action volver) => _volver = volver;
    private readonly Action _volver;
    public abstract string Titulo { get; }
    public virtual void ActualizarIdioma() => OnPropertyChanged(nameof(Titulo));
    [RelayCommand] public void Volver() => _volver();
}

// ============================================================================================ Historial
public sealed record FilaHistorial(EditorCasillas.EntradaHistorial Entrada, bool Rehacible)
{
    public string Titulo => Entrada.Descripcion;
    public string Detalle => IdiomaInterfaz.F("PantallasMasViewModels.OperacionHHMmSs", Entrada.Numero, Entrada.Hora) + (Entrada.Casillas > 1 ? IdiomaInterfaz.F("PantallasMasViewModels.Casillas", Entrada.Casillas) : string.Empty);
    public string Estado => Rehacible ? IdiomaInterfaz.T("PantallasMasViewModels.Deshecho") : IdiomaInterfaz.T("SubeditorHoja.Hecho");
    public string Accesible => $"{Titulo}. {Detalle}. {Estado}";
}

/// <summary>
/// «Historial y Deshacer»: las operaciones de esta sesión (escrituras, lotes, acciones del motor) con su texto y su hora. Es EL historial único del Core: Deshacer y Rehacer recorren la misma pila que usan las cajas, el
/// menú de caja, el editor y las acciones de Pokekeep. Nada de esto sale del dispositivo; al cerrar la partida se pierde (la copia de recuperación guarda el estado, no el historial).
/// </summary>
public sealed partial class HistorialViewModel : PantallaMasViewModel, IDisposable
{
    /// <summary>Con el texto muy grande (x1,3 o mas) Deshacer y Rehacer no caben uno junto al otro: se apilan.</summary>
    public int ColumnasBotones => Design.Movimiento.EscalaTexto >= 1.3 ? 1 : 2;

    private readonly Sesion? _sesion;
    private readonly Action<string, string, string> _informar;

    public HistorialViewModel(Sesion? sesion, Action volver, Action<string, string, string> informar) : base(volver)
    {
        _sesion = sesion; _informar = informar;
        if (sesion is not null) sesion.CasillasCambiadas += AlCambiar;
        Refrescar();
    }

    public void Dispose() { if (_sesion is not null) _sesion.CasillasCambiadas -= AlCambiar; }

    public override string Titulo => IdiomaInterfaz.T("Menu.Mas.Historial.Titulo");
    public bool HaySesion => _sesion is not null;
    [ObservableProperty] private IReadOnlyList<FilaHistorial> _hechas = [];
    [ObservableProperty] private IReadOnlyList<FilaHistorial> _deshechas = [];
    [ObservableProperty] private bool _puedeDeshacer;
    [ObservableProperty] private bool _puedeRehacer;
    [ObservableProperty] private string _resumen = string.Empty;
    public bool HayHechas => Hechas.Count > 0;
    public bool HayDeshechas => Deshechas.Count > 0;
    public bool Vacio => Hechas.Count == 0 && Deshechas.Count == 0;
    public string TextoVacio => _sesion is null ? IdiomaInterfaz.T("PantallasMasViewModels.PartidaAbiertaAbreGuardado") : IdiomaInterfaz.T("PantallasMasViewModels.TodaviaHasHechoNingun");

    partial void OnHechasChanged(IReadOnlyList<FilaHistorial> value) { OnPropertyChanged(nameof(HayHechas)); OnPropertyChanged(nameof(Vacio)); }
    partial void OnDeshechasChanged(IReadOnlyList<FilaHistorial> value) { OnPropertyChanged(nameof(HayDeshechas)); OnPropertyChanged(nameof(Vacio)); }

    private void AlCambiar(EditorCasillas.TipoCambio _) => Refrescar();

    public void Refrescar()
    {
        if (_sesion is null) return;
        var c = _sesion.Casillas;
        Hechas = [.. c.Hechas.Reverse().Select(e => new FilaHistorial(e, false))];
        Deshechas = [.. c.Deshechas.Select(e => new FilaHistorial(e, true))];
        PuedeDeshacer = c.PuedeDeshacer; PuedeRehacer = c.PuedeRehacer;
        Resumen = _sesion.CambiosPendientes == 0 ? IdiomaInterfaz.T("PantallasMasViewModels.CambiosGuardar") : _sesion.CambiosPendientes == 1 ? IdiomaInterfaz.T("PantallasMasViewModels.CambioGuardar") : IdiomaInterfaz.F("PantallasMasViewModels.CambiosGuardar2", _sesion.CambiosPendientes);
    }

    [RelayCommand]
    private void Deshacer()
    {
        if (_sesion is null) return;
        var r = _sesion.Casillas.Deshacer();
        _informar(r.Mensaje, r.Correcto ? "ok" : "aviso", r.Correcto ? "CircleCheck" : "CircleAlert");
        Refrescar();
    }

    [RelayCommand]
    private void Rehacer()
    {
        if (_sesion is null) return;
        var r = _sesion.Casillas.Rehacer();
        _informar(r.Mensaje, r.Correcto ? "ok" : "aviso", r.Correcto ? "CircleCheck" : "CircleAlert");
        Refrescar();
    }
}

// ============================================================================================ Ajustes de Pokekeep
/// <summary>Una fila de la pantalla de Ajustes: nombre, descripción completa (con el riesgo, si lo hay) y valor editable. Cada cambio se guarda al momento.</summary>
public abstract partial class FilaAjuste : ObservableObject
{
    protected FilaAjuste(DefinicionAjuste def, AjustesPokekeep ajustes, Action cambiado)
    {
        Definicion = def; Ajustes = ajustes; Cambiado = cambiado;
        Propiedad = typeof(AjustesPokekeep).GetProperty(def.Propiedad)!;
    }

    public DefinicionAjuste Definicion { get; }
    protected AjustesPokekeep Ajustes { get; }
    protected PropertyInfo Propiedad { get; }
    protected Action Cambiado { get; }
    public string Nombre => Definicion.Nombre;
    public string Descripcion => Definicion.Descripcion;
    public string Accesible => $"{Nombre}. {Descripcion}";
    public string TextoValor => IdiomaInterfaz.F("Ajustes.Valor", ValorTexto);
    /// <summary>El ajuste de HOME lleva un riesgo explícito: su descripción se pinta como aviso.</summary>
    public bool EsRiesgo => Definicion.Propiedad == nameof(AjustesPokekeep.AllowHomeTrackerless);
    public abstract string ValorTexto { get; }
    public abstract void Releer();
    public virtual void ActualizarIdioma()
    {
        OnPropertyChanged(nameof(Nombre)); OnPropertyChanged(nameof(Descripcion));
        OnPropertyChanged(nameof(Accesible)); OnPropertyChanged(nameof(ValorTexto));
        OnPropertyChanged(nameof(TextoValor));
    }
}

public sealed partial class AjusteInterruptor : FilaAjuste
{
    private bool _releyendo;
    public AjusteInterruptor(DefinicionAjuste def, AjustesPokekeep a, Action cambiado) : base(def, a, cambiado) { _releyendo = true; Activo = (bool)Propiedad.GetValue(a)!; _releyendo = false; }
    [ObservableProperty] private bool _activo;
    public override string ValorTexto => IdiomaInterfaz.T(Activo ? "Comun.Si" : "Comun.No");
    partial void OnActivoChanged(bool value) { if (_releyendo) return; Propiedad.SetValue(Ajustes, value); OnPropertyChanged(nameof(ValorTexto)); Cambiado(); }
    public override void Releer() { _releyendo = true; Activo = (bool)Propiedad.GetValue(Ajustes)!; _releyendo = false; OnPropertyChanged(nameof(ValorTexto)); }
}

public sealed partial class AjusteEntero : FilaAjuste
{
    private bool _releyendo;
    public AjusteEntero(DefinicionAjuste def, AjustesPokekeep a, Action cambiado) : base(def, a, cambiado) { _releyendo = true; Valor = (int)Propiedad.GetValue(a)!; _releyendo = false; }
    [ObservableProperty] private int _valor;
    public int Minimo => Definicion.Minimo;
    public int Maximo => Definicion.Maximo;
    public string Rango => IdiomaInterfaz.F("Ajustes.Rango", Minimo, Maximo);
    public string TextoBajar => IdiomaInterfaz.F("Ajustes.Bajar", Nombre);
    public string TextoSubir => IdiomaInterfaz.F("Ajustes.Subir", Nombre);
    public override void ActualizarIdioma()
    {
        base.ActualizarIdioma(); OnPropertyChanged(nameof(Rango));
        OnPropertyChanged(nameof(TextoBajar)); OnPropertyChanged(nameof(TextoSubir));
    }
    public override string ValorTexto => Valor.ToString(System.Globalization.CultureInfo.CurrentCulture);
    public bool PuedeBajar => Valor > Minimo;
    public bool PuedeSubir => Valor < Maximo;
    public void Poner(int v)
    {
        Propiedad.SetValue(Ajustes, v);              // el ajuste recorta al rango (como en el escritorio)
        var real = (int)Propiedad.GetValue(Ajustes)!;
        if (real != Valor) Valor = real; else Avisar();
        Cambiado();
    }
    partial void OnValorChanged(int value) { if (_releyendo) { Avisar(); return; } if ((int)Propiedad.GetValue(Ajustes)! != value) { Propiedad.SetValue(Ajustes, value); Valor = (int)Propiedad.GetValue(Ajustes)!; } Avisar(); }
    private void Avisar() { OnPropertyChanged(nameof(ValorTexto)); OnPropertyChanged(nameof(TextoValor)); OnPropertyChanged(nameof(PuedeBajar)); OnPropertyChanged(nameof(PuedeSubir)); }
    [RelayCommand] private void Bajar() => Poner(Valor - Definicion.Paso);
    [RelayCommand] private void Subir() => Poner(Valor + Definicion.Paso);
    public override void Releer() { _releyendo = true; Valor = (int)Propiedad.GetValue(Ajustes)!; _releyendo = false; Avisar(); }
}

public sealed partial class AjusteOpcion : FilaAjuste
{
    private readonly Action<HojaViewModel> _abrirHoja;
    public AjusteOpcion(DefinicionAjuste def, AjustesPokekeep a, Action cambiado, Action<HojaViewModel> abrirHoja) : base(def, a, cambiado) { _abrirHoja = abrirHoja; }
    public override string ValorTexto => Definicion.Opciones.First(o => o.Valor == Propiedad.GetValue(Ajustes)!.ToString()).Etiqueta;
    public IReadOnlyList<(string Valor, string Etiqueta)> Opciones => Definicion.Opciones;
    public void Elegir(string valor)
    {
        Propiedad.SetValue(Ajustes, Enum.Parse(Propiedad.PropertyType, valor));
        OnPropertyChanged(nameof(ValorTexto)); Cambiado();
    }
    [RelayCommand]
    private void Abrir()
    {
        var actual = Propiedad.GetValue(Ajustes)!.ToString();
        IReadOnlyList<OpcionSelector> OpcionesActuales() => [.. Opciones.Select((o, i) => new OpcionSelector(i, o.Etiqueta))];
        _abrirHoja(new SelectorHojaViewModel(Nombre, OpcionesActuales(), Opciones.ToList().FindIndex(o => o.Valor == actual),
            o => Elegir(Opciones[o.Id].Valor), actualizarOpciones: OpcionesActuales, tituloActual: () => Nombre));
    }
    public override void Releer() => OnPropertyChanged(nameof(ValorTexto));
}

public sealed record GrupoAjustes(string Codigo, IReadOnlyList<FilaAjuste> Filas)
{
    public string Nombre => DefinicionAjuste.NombreGrupo(Codigo);
}
public sealed record ResultadoAjuste(string NombreBase, string DescripcionBase, string GrupoBase, ICommand Abrir,
    Func<(string Nombre, string Descripcion, string Grupo)>? TextosActuales = null)
{
    public string Nombre => TextosActuales?.Invoke().Nombre ?? NombreBase;
    public string Descripcion => TextosActuales?.Invoke().Descripcion ?? DescripcionBase;
    public string Grupo => TextosActuales?.Invoke().Grupo ?? GrupoBase;
}

/// <summary>
/// «Ajustes» → Pokekeep: UNA fila por propiedad de <c>PokekeepSettings</c> (nombre, descripción y valor por defecto idénticos; ver <see cref="AjustesPokekeep"/>), agrupadas como el censo (Motor, Historia, Operaciones por lotes,
/// Interfaz). Cada cambio se guarda al momento en <c>ajustes.json</c> y vale desde la siguiente operación.
/// </summary>
public sealed partial class AjustesViewModel : PantallaMasViewModel
{
    private readonly IAjustesPokekeep _almacen;
    private readonly string? _carpetaPreferencias;
    /// <summary>Variante del arrastre al equipo (C16): activada centra la vista en el equipo; desactivada adapta la zona.</summary>
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private bool _centrarEquipoAlArrastrar;
    public bool HayPreferenciaArrastre => _carpetaPreferencias is not null;
    public string TextoCentrarArrastre => Localizacion.IdiomaInterfaz.T(CentrarEquipoAlArrastrar ? "Comun.Si" : "Comun.No");
    partial void OnCentrarEquipoAlArrastrarChanged(bool value)
    {
        OnPropertyChanged(nameof(TextoCentrarArrastre));
        if (_carpetaPreferencias is null) return;
        try { new Ajustes.PreferenciasArrastre(_carpetaPreferencias).Guardar(value); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { _informar(Localizacion.IdiomaInterfaz.T("Cajas.NoGuardarArrastre"), "aviso", "CircleAlert"); }
    }
    private readonly Action<HojaViewModel> _abrirHoja;
    private readonly Action<string, string, string> _informar;

    /// <param name="pkhex">Los ajustes de PKHeX (F10): con ellos la pantalla lista sus grupos debajo de los de Pokekeep; sin ellos (pruebas de la F6) no sale nada nuevo.</param>
    /// <param name="abrirPantalla">Navega a la pantalla de un grupo de PKHeX; su «Volver» regresa aquí.</param>
    /// <param name="carpetas">El selector de carpetas de la Shell, para las filas que eligen carpetas (bloque 10-4).</param>
    public AjustesViewModel(IAjustesPokekeep almacen, Action volver, Action<HojaViewModel> abrirHoja, Action<string, string, string> informar, IAjustesPkhex? pkhex = null, Action<PantallaMasViewModel?>? abrirPantalla = null, Func<Almacen.IAlmacenCarpetas?>? carpetas = null, string? carpetaPreferencias = null) : base(volver)
    {
        _almacen = almacen; _abrirHoja = abrirHoja; _informar = informar;
        _carpetaPreferencias = carpetaPreferencias;
        if (carpetaPreferencias is not null) _centrarEquipoAlArrastrar = new Ajustes.PreferenciasArrastre(carpetaPreferencias).Leer();
        Grupos = [.. DefinicionAjuste.Grupos.Select(g => new GrupoAjustes(g, [.. DefinicionAjuste.Del(g).Select(Crear)]))];
        GruposFiltrados = Grupos;
        Aviso = almacen.Aviso;
        _pkhex = pkhex;
        if (pkhex is not null && abrirPantalla is not null)
        {
            GruposPkhex = [.. AjustesPkhexCatalogo.GruposVisibles().Select(x => new EntradaGrupoPkhex(x.Grupo, x.Cantidad,
                new RelayCommand(() => abrirPantalla(new AjustesPkhexGrupoViewModel(pkhex, x.Grupo, () => abrirPantalla(this), abrirHoja, informar, carpetas)))))];
            AvisoPkhex = pkhex.Aviso;
            _indiceBusqueda = [.. AjustesPkhexCatalogo.GruposVisibles().SelectMany(g =>
                AjustesPkhexCatalogo.HojasVisibles(g.Grupo).Select(n => FabricaFilasPkhex.Crear(n, pkhex, abrirHoja))
                    .Concat(ExtrasAjustesPkhex.Filas(g.Grupo, pkhex, abrirHoja))
                    .Select(f => new ResultadoAjuste(f.Nombre, f.Descripcion, g.Grupo,
                        new RelayCommand(() => abrirPantalla(new AjustesPkhexGrupoViewModel(pkhex, g.Grupo,
                            () => abrirPantalla(this), abrirHoja, informar, carpetas, f.Ruta))),
                        () => (f.Nombre, f.Descripcion, RegistroAjustesPkhex.NombreGrupo(g.Grupo)))))];
        }
    }

    private readonly IAjustesPkhex? _pkhex;
    private readonly IReadOnlyList<ResultadoAjuste> _indiceBusqueda = [];
    [ObservableProperty] private string _filtro = string.Empty;
    [ObservableProperty] private IReadOnlyList<GrupoAjustes> _gruposFiltrados = [];
    [ObservableProperty] private IReadOnlyList<ResultadoAjuste> _resultadosBusqueda = [];
    public bool MostrarGruposPkhex => HayGruposPkhex && string.IsNullOrWhiteSpace(Filtro);
    partial void OnFiltroChanged(string value)
    {
        var q = SelectorHojaViewModel.Normalizar(value.Trim());
        bool Coincide(string n, string d) => SelectorHojaViewModel.Normalizar(n + " " + d).Contains(q);
        GruposFiltrados = [.. Grupos.Select(g => new GrupoAjustes(g.Codigo, [.. g.Filas.Where(f => Coincide(f.Nombre, f.Descripcion))])).Where(g => g.Filas.Count > 0)];
        ResultadosBusqueda = q.Length == 0 ? [] : [.. _indiceBusqueda.Where(f => Coincide(f.Nombre, f.Descripcion))];
        OnPropertyChanged(nameof(MostrarGruposPkhex));
    }

    /// <summary>
    /// «Restablecer todos los ajustes de PKHeX» (<c>SettingsEditor.B_Reset</c>; bloque 10-1). PKHeX borra <c>cfg.json</c> tras confirmar y se reinicia; aquí se confirma igual, se vuelve a los valores de fábrica y se
    /// aplican al Core al momento: no hace falta reiniciar. Las carpetas del usuario elegidas para PKHeX dejan de usarse (no se tocan).
    /// </summary>
    [RelayCommand]
    private void RestablecerPkhex()
    {
        if (_pkhex is not { } p) return;
        _abrirHoja(new ConfirmarHojaViewModel(IdiomaInterfaz.T("PantallasMasViewModels.RestablecerAjustesPKHeX"),
            [IdiomaInterfaz.T("PantallasMasViewModels.TodosAjustesPKHeXVuelven"), IdiomaInterfaz.T("PantallasMasViewModels.AjustesPokekeepCambianCarpetas")],
            IdiomaInterfaz.T("PantallasMasViewModels.RestablecerTodo"), () => { p.Restablecer(); _informar(IdiomaInterfaz.T("PantallasMasViewModels.AjustesPKHeXRestablecidos"), "ok", "CircleCheck"); }, destructiva: true));
    }

    /// <summary>Los grupos de ajustes de PKHeX con al menos un ajuste que hace algo (F10): «Escritura de ranuras», «General»...</summary>
    public IReadOnlyList<EntradaGrupoPkhex> GruposPkhex { get; } = [];
    public bool HayGruposPkhex => GruposPkhex.Count > 0;
    public string? AvisoPkhex { get; }
    public bool HayAvisoPkhex => AvisoPkhex is not null;

    private FilaAjuste Crear(DefinicionAjuste d) => d.Tipo switch
    {
        TipoAjuste.Interruptor => new AjusteInterruptor(d, _almacen.Actual, Guardar),
        TipoAjuste.Entero => new AjusteEntero(d, _almacen.Actual, Guardar),
        _ => new AjusteOpcion(d, _almacen.Actual, Guardar, _abrirHoja),
    };

    public override string Titulo => IdiomaInterfaz.T("Ajustes.Titulo");
    public IReadOnlyList<GrupoAjustes> Grupos { get; }
    public IEnumerable<FilaAjuste> Filas => Grupos.SelectMany(g => g.Filas);
    public string? Aviso { get; }
    public bool HayAviso => Aviso is not null;
    public string Subtitulo => IdiomaInterfaz.T("Ajustes.Subtitulo");
    public string Pronto => IdiomaInterfaz.T("Ajustes.Pronto");
    public override void ActualizarIdioma()
    {
        base.ActualizarIdioma(); OnPropertyChanged(nameof(Subtitulo)); OnPropertyChanged(nameof(Pronto));
        foreach (var fila in Filas) fila.ActualizarIdioma();
        foreach (var grupo in GruposPkhex) grupo.ActualizarIdioma();
        OnFiltroChanged(Filtro); // conserva el texto escrito y vuelve a buscar en los nombres actuales
    }
    public bool HayPronto => AjustesPkhexCatalogo.GruposPendientes().Count > 0;

    private void Guardar()
    {
        if (!_almacen.Guardar()) _informar(IdiomaInterfaz.T("Ajustes.ErrorGuardar"), "aviso", "CircleAlert");
    }

    /// <summary>Vuelve a los valores por defecto de todos los ajustes de Pokekeep.</summary>
    [RelayCommand]
    private void Restablecer()
    {
        var por = new AjustesPokekeep();
        foreach (var p in typeof(AjustesPokekeep).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanWrite))
            p.SetValue(_almacen.Actual, p.GetValue(por));
        foreach (var f in Filas) f.Releer();
        Guardar();
        _informar(IdiomaInterfaz.T("Ajustes.Restablecidos"), "ok", "CircleCheck");
    }
}

// ============================================================================================ Acerca de
public sealed partial class AcercaDeViewModel : PantallaMasViewModel
{
    public System.Windows.Input.ICommand? VerGestos { get; set; }
    private readonly IPortapapeles? _portapapeles;

    public AcercaDeViewModel(IAcercaDe proveedor, Action volver, IPortapapeles? portapapeles = null) : base(volver)
    {
        Contenido = proveedor.Obtener();
        _portapapeles = portapapeles;
    }

    public ContenidoAcercaDe Contenido { get; }
    public override string Titulo => IdiomaInterfaz.T("Menu.Mas.AcercaDe.Titulo");
    public bool HayPaquete => Contenido.Paquete.Count > 0;
    [ObservableProperty] private bool _verCambiosPkhex;
    public string TextoCambiosPkhex => VerCambiosPkhex ? Contenido.PkhexCambios : string.Empty;
    public string TextoAlternarPkhex => VerCambiosPkhex ? IdiomaInterfaz.T("PantallasMasViewModels.OcultarRegistroCambiosPKHeX") : IdiomaInterfaz.T("PantallasMasViewModels.VerRegistroCambiosPKHeX");
    public bool HayCambiosPkhex => Contenido.PkhexCambios.Length > 0;
    partial void OnVerCambiosPkhexChanged(bool value) { OnPropertyChanged(nameof(TextoCambiosPkhex)); OnPropertyChanged(nameof(TextoAlternarPkhex)); }
    [RelayCommand] private void AlternarCambiosPkhex() => VerCambiosPkhex = !VerCambiosPkhex;

    [RelayCommand]
    private async Task CopiarDatos()
    {
        if (_portapapeles is null) return;
        await _portapapeles.CopiarAsync(Contenido.LineaVersion + Environment.NewLine + string.Join(Environment.NewLine, Contenido.Paquete.Select(p => $"{p.Etiqueta}: {p.Valor}")));
    }
}
