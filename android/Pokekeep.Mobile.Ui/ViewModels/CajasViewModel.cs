// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Cajas;
using Pokekeep.Mobile.Ui.Modelo;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.ViewModels;

public enum ZonaPokemon { Cajas, Equipo, Otros }

/// <summary>Un grupo de casillas de la zona «Otros» (guarderia, Caja de combate, GTS, fusion...) con sus casillas y, en las guarderias, experiencia, huevo y semilla.</summary>
public sealed partial class GrupoViewModel : ObservableObject
{
    public GrupoViewModel(GrupoCasillas grupo, ObservableCollection<CasillaViewModel> casillas, GuarderiaInfo? guarderia = null)
    {
        Grupo = grupo; Casillas = casillas; Guarderia = guarderia;
        if (guarderia is not null)
        {
            _huevo = guarderia.HuevoDisponible;
            _semilla = guarderia.Semilla;
            Experiencias = [.. Enumerable.Range(0, guarderia.Casillas).Select(i => new ExperienciaGuarderia(guarderia, i))];
        }
    }

    public GrupoCasillas Grupo { get; }
    public string Id => Grupo.Id;
    public string Titulo => Grupo.Titulo;
    public string? Nota => Grupo.Nota;
    public bool HayNota => !string.IsNullOrWhiteSpace(Nota);
    public int Columnas => Grupo.Columnas;
    public ObservableCollection<CasillaViewModel> Casillas { get; }
    public GuarderiaInfo? Guarderia { get; }
    public bool EsGuarderia => Guarderia is not null;
    public bool TieneHuevo => Guarderia?.TieneHuevo == true;
    public bool TieneSemilla => Guarderia?.DigitosSemilla > 0;
    public int DigitosSemilla => Guarderia?.DigitosSemilla ?? 0;
    public IReadOnlyList<ExperienciaGuarderia> Experiencias { get; } = [];
    public bool TieneExperiencia => Guarderia?.TieneExperiencia == true;

    [ObservableProperty] private bool _huevo;
    [ObservableProperty] private string _semilla = string.Empty;
    [ObservableProperty] private string _errorSemilla = string.Empty;

    public bool HayErrorSemilla => !string.IsNullOrEmpty(ErrorSemilla);
    partial void OnErrorSemillaChanged(string value) => OnPropertyChanged(nameof(HayErrorSemilla));

    partial void OnHuevoChanged(bool value)
    {
        if (Guarderia is { } g && g.HuevoDisponible != value) { g.HuevoDisponible = value; HaCambiado?.Invoke(); }
    }

    /// <summary>La semilla se escribe al terminar de teclear un valor valido (hexadecimal, los digitos exactos del formato); si no lo es, el guardado no se toca y el campo lo dice.</summary>
    partial void OnSemillaChanged(string value)
    {
        if (Guarderia is not { } g) return;
        if (value == g.Semilla) { ErrorSemilla = string.Empty; return; }
        if (value.Length != g.DigitosSemilla) { ErrorSemilla = IdiomaInterfaz.F("Cajas.FaltanDigitosSonHexadecimales", g.DigitosSemilla); return; }
        if (g.FijarSemilla(value)) { ErrorSemilla = string.Empty; HaCambiado?.Invoke(); }
        else ErrorSemilla = IdiomaInterfaz.T("Cajas.SoloAdmitenDigitosHexadecimales");
    }

    /// <summary>Un cambio hecho directamente sobre datos de la guarderia (experiencia, huevo, semilla): la pestana lo cuenta como cambio pendiente.</summary>
    public event Action? HaCambiado;
    public void Notificar() => HaCambiado?.Invoke();
}

/// <summary>La experiencia ganada de una casilla de guarderia (editable, como el cuadro «XP» de PKHeX).</summary>
public sealed partial class ExperienciaGuarderia : ObservableObject
{
    private readonly GuarderiaInfo _g;
    private readonly int _i;
    public ExperienciaGuarderia(GuarderiaInfo g, int i) { _g = g; _i = i; _texto = g.Experiencia(i).ToString(); }
    public int Numero => _i + 1;
    public string Etiqueta => IdiomaInterfaz.F("Cajas.EXPCasilla", _i + 1);
    [ObservableProperty] private string _texto;
    [ObservableProperty] private bool _valido = true;
    public Action? HaCambiado { get; set; }

    partial void OnTextoChanged(string value)
    {
        if (uint.TryParse(value, out var v)) { Valido = true; if (_g.Experiencia(_i) != v) { _g.FijarExperiencia(_i, v); HaCambiado?.Invoke(); } }
        else Valido = value.Length == 0;
    }
}

/// <summary>
/// Pestana Pokemon: las cajas con la cuadricula REAL del formato (6×5 desde la 3.ª generacion, 5×4 en Gen. 1-2) y su selector con nombre y fondo, el equipo, las demas zonas del guardado (guarderia, Caja de combate, GTS,
/// fusion, acompanante...), la ficha compacta, el menu de casilla, la seleccion multiple, el menu de caja, mover y copiar entre cajas, la busqueda y Deshacer/Rehacer. Todo escribe por <see cref="EditorCasillas"/> (historial del Core).
/// </summary>
public sealed partial class CajasViewModel : ObservableObject
{
    private readonly IAnfitrionCajas? _anfitrion;
    private int _generacionAnalisis;
    private readonly Action _alCambiarMovimiento;

    public CajasViewModel(Sesion sesion, IAnfitrionCajas? anfitrion = null, int cajaInicial = 0, bool seleccionarIlegal = false)
    {
        Sesion = sesion;
        _anfitrion = anfitrion;
        if (anfitrion?.CarpetaPreferenciasInterfaz is { } preferencias)
            _centrarEquipoAlArrastrar = new Ajustes.PreferenciasArrastre(preferencias).Leer();
        Columnas = sesion.ColumnasCaja;
        MenuCaja = new MenuCaja(sesion.Casillas);
        var zonas = sesion.Zonas;
        TieneCajas = zonas.TieneCajas;
        TieneEquipo = zonas.TieneEquipo;
        TieneOtros = zonas.TieneOtros;
        CasillasCaja = new ObservableCollection<CasillaViewModel>(Enumerable.Range(0, TieneCajas ? sesion.CasillasPorCaja : 0).Select(i => new CasillaViewModel(sesion, i)));
        CasillasEquipo = new ObservableCollection<CasillaViewModel>(Enumerable.Range(0, TieneEquipo ? zonas.CasillasEquipo : 0).Select(i => new CasillaViewModel(sesion, i)));
        Grupos = new ObservableCollection<GrupoViewModel>(zonas.Grupos.Select(CrearGrupo));
        TotalCajas = TieneCajas ? sesion.Cajas : 0;

        _esCajas = TieneCajas;
        _esEquipo = !TieneCajas && TieneEquipo;
        _esOtros = !TieneCajas && !TieneEquipo && TieneOtros;
        Zona = _esCajas ? ZonaPokemon.Cajas : _esEquipo ? ZonaPokemon.Equipo : ZonaPokemon.Otros;

        if (TieneCajas) CargarCaja(cajaInicial, seleccionar: false); else Refrescar();
        _alCambiarMovimiento = () => OnPropertyChanged(nameof(EtiquetasEnBarra));
        Design.Movimiento.SuscribirDebil(_alCambiarMovimiento);
        sesion.CasillasCambiadas += AlCambiar;
        sesion.Casillas.Cambiado += AlTocar;
        if (seleccionarIlegal && Casillas.FirstOrDefault(c => c.Estado == EstadoLegal.Ilegal) is { } mala) ElegirCasilla(mala);
    }

    public Sesion Sesion { get; }
    public MenuCaja MenuCaja { get; }
    public int Columnas { get; }
    /// <summary>X45: el equipo usa las mismas columnas (y, con la misma separacion, el mismo lado de casilla) que la caja.</summary>
    public int ColumnasEquipo => Columnas;
    public bool TieneCajas { get; }
    public bool TieneEquipo { get; }
    public bool TieneOtros { get; }
    public Avalonia.Controls.GridLength AnchoZonaCajas => TieneCajas ? new(1, Avalonia.Controls.GridUnitType.Star) : new(0);
    public Avalonia.Controls.GridLength AnchoZonaEquipo => TieneEquipo ? new(1, Avalonia.Controls.GridUnitType.Star) : new(0);
    public Avalonia.Controls.GridLength AnchoZonaOtros => TieneOtros ? new(1, Avalonia.Controls.GridUnitType.Star) : new(0);
    public int TotalCajas { get; }
    public ZonaPokemon Zona { get; private set; }

    public ObservableCollection<CasillaViewModel> CasillasCaja { get; }
    public ObservableCollection<CasillaViewModel> CasillasEquipo { get; }
    public ObservableCollection<GrupoViewModel> Grupos { get; }

    /// <summary>La cuadricula activa (cajas o equipo): lo que la ficha, la seleccion y las pruebas recorren.</summary>
    public ObservableCollection<CasillaViewModel> Casillas => EsEquipo ? CasillasEquipo : EsOtros ? (Grupos.FirstOrDefault()?.Casillas ?? CasillasCaja) : CasillasCaja;

    [ObservableProperty] private int _caja;
    [ObservableProperty] private string _nombreCaja = string.Empty;
    [ObservableProperty] private string _ocupacion = string.Empty;
    [ObservableProperty] private int _fondo = -1;
    [ObservableProperty] private Avalonia.Media.Imaging.Bitmap? _imagenFondo;
    [ObservableProperty] private CasillaViewModel? _seleccionada;
    [ObservableProperty] private FichaViewModel? _ficha;
    public bool FichaVisible => Ficha is not null && AjustesPkhexActuales.Hover.HoverSlotShowPreview;
    partial void OnFichaChanged(FichaViewModel? oldValue, FichaViewModel? newValue)
    {
        oldValue?.CancelarDetalles();
        OnPropertyChanged(nameof(FichaVisible));
    }
    [ObservableProperty] private bool _esCajas;
    [ObservableProperty] private bool _esEquipo;
    [ObservableProperty] private bool _esOtros;
    [ObservableProperty] private bool _haySeleccion;
    [ObservableProperty] private bool _hayVacia;
    [ObservableProperty] private string _textoVacia = string.Empty;

    public bool HayFondo => ImagenFondo is not null;
    public string EtiquetaCaja => IdiomaInterfaz.F("Cajas.Caja2", NombreCaja, Caja + 1, TotalCajas);
    public string TextoNumeroCaja => $"{Caja + 1} / {TotalCajas}";
    public string TituloZona => EsEquipo ? IdiomaInterfaz.T("Form.Equipo") : EsOtros ? IdiomaInterfaz.T("Zonas.Otros") : NombreCaja;
    public bool VariasZonas => (TieneCajas ? 1 : 0) + (TieneEquipo ? 1 : 0) + (TieneOtros ? 1 : 0) > 1;
    /// <summary>Con las tres zonas el segmento reduce el texto: «Equipo» no cabe en un tercio del ancho con la letra al doble.</summary>
    public bool TresZonas => TieneCajas && TieneEquipo && TieneOtros;
    public bool MostrarCuadriculaCaja => EsCajas && TieneCajas;
    public bool MostrarEquipo => EsEquipo && TieneEquipo;
    public bool MostrarOtros => EsOtros && TieneOtros;
    public bool MostrarNavegacionCaja => EsCajas && TieneCajas;
    public bool SinAlmacen => !TieneCajas && !TieneEquipo && !TieneOtros;

    partial void OnImagenFondoChanged(Avalonia.Media.Imaging.Bitmap? value) => OnPropertyChanged(nameof(HayFondo));
    partial void OnCajaChanged(int value) { OnPropertyChanged(nameof(EtiquetaCaja)); OnPropertyChanged(nameof(TextoNumeroCaja)); }
    partial void OnNombreCajaChanged(string value) { OnPropertyChanged(nameof(EtiquetaCaja)); OnPropertyChanged(nameof(TituloZona)); }

    partial void OnEsCajasChanged(bool value) { if (value) CambiarZona(ZonaPokemon.Cajas); }
    partial void OnEsEquipoChanged(bool value) { if (value) CambiarZona(ZonaPokemon.Equipo); }
    partial void OnEsOtrosChanged(bool value) { if (value) CambiarZona(ZonaPokemon.Otros); }

    private GrupoViewModel CrearGrupo(GrupoCasillas g)
    {
        var casillas = new ObservableCollection<CasillaViewModel>(Enumerable.Range(0, g.Casillas.Count).Select(i => new CasillaViewModel(Sesion, i)));
        var guarderia = Sesion.Zonas.Guarderias.FirstOrDefault(d => g.Id == $"guarderia{d.Numero}");
        var vm = new GrupoViewModel(g, casillas, guarderia);
        vm.HaCambiado += () => Sesion.MarcarCambioFueraDelHistorial();
        foreach (var e in vm.Experiencias) e.HaCambiado = () => Sesion.MarcarCambioFueraDelHistorial();
        return vm;
    }

    private void CambiarZona(ZonaPokemon zona)
    {
        CancelarArrastre();
        LiberarArrastre();
        Zona = zona;
        EsCajas = zona == ZonaPokemon.Cajas; EsEquipo = zona == ZonaPokemon.Equipo; EsOtros = zona == ZonaPokemon.Otros;
        OnPropertyChanged(nameof(MostrarEquipoEnPagina));
        foreach (var n in new[] { nameof(Casillas), nameof(MostrarCuadriculaCaja), nameof(MostrarEquipo), nameof(MostrarOtros), nameof(MostrarNavegacionCaja), nameof(TituloZona) }) OnPropertyChanged(n);
        Refrescar();
        ElegirCasilla(null);
    }

    // ---- Carga de las cuadriculas ----
    private IEnumerable<CasillaViewModel> TodasLasVisibles
    {
        get
        {
            IEnumerable<CasillaViewModel> zona = EsCajas ? CasillasCaja : EsEquipo ? CasillasEquipo : Grupos.SelectMany(g => g.Casillas);
            // Durante un arrastre (X10) tambien se ve la zona de destino de la otra pestana (el equipo desde las cajas, la caja desde el equipo).
            if (MostrarApoyoEquipo && !EsEquipo) zona = zona.Concat(CasillasEquipo);
            if (MostrarApoyoCaja && !EsCajas) zona = zona.Concat(CasillasCaja);
            return zona;
        }
    }

    private void CargarCaja(int caja, bool seleccionar = true)
    {
        if (!TieneCajas) return;
        Caja = Math.Clamp(caja, 0, Math.Max(0, TotalCajas - 1));
        var sav = Sesion.Guardado;
        var analisis = !AnalizadorLegalidad.Asincrono;
        var pendientes = new List<(PKM, StorageSlotType, object)>();
        var datos = sav.GetBoxData(Caja);
        var ocupadas = 0;
        for (var i = 0; i < CasillasCaja.Count; i++)
        {
            var pk = i < datos.Length ? datos[i] : null;
            var info = new SlotInfoBox(Caja, i, sav);
            var c = CasillasCaja[i];
            c.Asignar(new CasillaRef(info, Caja), pk, false, analisis);
            if (c.Ocupada) ocupadas++;
            if (c.PendienteDeAnalisis) pendientes.Add((c.Entidad!, c.TipoAlmacen, c));
        }
        NombreCaja = Sesion.NombreCaja(Caja);
        Ocupacion = $"{ocupadas} / {CasillasCaja.Count}";
        Fondo = Sesion.FondoCaja(Caja);
        ImagenFondo = Design.Sprites.FondoDeCaja(sav, Caja);
        LanzarAnalisis(pendientes);
        AplicarMarcas();
        if (seleccionar) ElegirCasilla(null);
    }

    private void CargarEquipo()
    {
        if (!TieneEquipo) return;
        var sav = Sesion.Guardado;
        var analisis = !AnalizadorLegalidad.Asincrono;
        var pendientes = new List<(PKM, StorageSlotType, object)>();
        for (var i = 0; i < CasillasEquipo.Count; i++)
        {
            var pk = i < Math.Min(6, sav.PartyCount) ? sav.GetPartySlotAtIndex(i) : null;
            var c = CasillasEquipo[i];
            c.Asignar(new CasillaRef(new SlotInfoParty(i)), pk, false, analisis);
            if (c.PendienteDeAnalisis) pendientes.Add((c.Entidad!, c.TipoAlmacen, c));
        }
        LanzarAnalisis(pendientes);
        AplicarMarcas();
    }

    private void CargarGrupos()
    {
        var sav = Sesion.Guardado;
        var analisis = !AnalizadorLegalidad.Asincrono;
        var pendientes = new List<(PKM, StorageSlotType, object)>();
        foreach (var g in Grupos)
        {
            for (var i = 0; i < g.Casillas.Count; i++)
            {
                var info = g.Grupo.Casillas[i];
                var pk = info.Read(sav);
                var c = g.Casillas[i];
                c.Asignar(new CasillaRef(info), pk, g.Grupo.OcultaLegalidad, analisis);
                if (c.PendienteDeAnalisis) pendientes.Add((c.Entidad!, c.TipoAlmacen, c));
            }
        }
        LanzarAnalisis(pendientes);
        AplicarMarcas();
    }

    private void LanzarAnalisis(List<(PKM, StorageSlotType, object)> pendientes)
    {
        if (pendientes.Count == 0) return;
        var generacion = ++_generacionAnalisis;
        AnalizadorLegalidad.EvaluarEnSegundoPlano(pendientes, () => generacion == _generacionAnalisis, (destino, estado) =>
        {
            var c = (CasillaViewModel)destino;
            c.PonerEstado(estado);
            if (ReferenceEquals(c, Seleccionada)) Ficha = c.Ocupada ? CrearFicha(c) : null;
        });
    }

    /// <summary>Vuelve a leer del guardado la zona que se ve (tras un cambio) y conserva la casilla elegida.</summary>
    public void Refrescar()
    {
        RefrescarTextosBusqueda();
        var elegida = Seleccionada?.Ref is { } r ? (Zona, r.Info.Slot, (r.Info as SlotInfoBox)?.Box ?? -1, r.Info.Type) : default((ZonaPokemon, int, int, StorageSlotType)?);
        if (EsCajas) CargarCaja(Caja, seleccionar: false);
        else if (EsEquipo) CargarEquipo();
        else CargarGrupos();
        OnPropertyChanged(nameof(Casillas));
        if (elegida is { } e && TodasLasVisibles.FirstOrDefault(c => c.Ref is { } cr && cr.Info.Slot == e.Item2 && ((cr.Info as SlotInfoBox)?.Box ?? -1) == e.Item3 && cr.Info.Type == e.Item4) is { } mismo) ElegirCasilla(mismo);
        else if (Seleccionada is not null) { Seleccionada = null; Ficha = null; HaySeleccion = false; HayVacia = false; }
        OnPropertyChanged(nameof(PuedeDeshacer)); OnPropertyChanged(nameof(PuedeRehacer));
    }

    // ---- Navegacion de cajas ----
    [RelayCommand] public void CajaAnterior() { if (EsCajas && TotalCajas > 0) IrACaja((Caja + TotalCajas - 1) % TotalCajas); }
    [RelayCommand] public void CajaSiguiente() { if (EsCajas && TotalCajas > 0) IrACaja((Caja + 1) % TotalCajas); }

    /// <summary>Cambia de caja (la seleccion multiple y el modo de colocar se conservan entre cajas).</summary>
    public void IrACaja(int caja)
    {
        if (!TieneCajas) return;
        if (!EsCajas) { EsCajas = true; }
        CargarCaja(caja);
    }

    // ---- Ficha y seleccion de casilla ----
    [RelayCommand]
    public void Seleccionar(CasillaViewModel? casilla)
    {
        if (DateTime.UtcNow < _ignorarToqueHasta) return;   // el toque que sigue a una pulsacion larga
        if (Seleccionando) { AlternarMarca(casilla); return; }
        if (Colocando) { if (_colocarLibreria is not null) ColocarLibreriaEn(casilla); else ColocarEn(casilla); return; }
        ElegirCasilla(casilla);
    }

    /// <summary>Actualiza la ficha al cargar o refrescar sin simular un toque: nunca marca ni coloca automáticamente. En esos modos la vecina y la caja real muestran solo sus marcas.</summary>
    private void ElegirCasilla(CasillaViewModel? casilla)
    {
        if (Seleccionando || Colocando) casilla = null;
        if (Seleccionada is not null) Seleccionada.Seleccionada = false;
        Seleccionada = casilla;
        if (casilla is null) { Ficha = null; HaySeleccion = false; HayVacia = false; return; }
        casilla.Seleccionada = true;
        HaySeleccion = true;
        Ficha = casilla.Ocupada ? CrearFicha(casilla) : null;
        if (casilla.Entidad is { } pk && AjustesPkhexActuales.Hover.HoverSlotPlayCry) _anfitrion?.ReproducirGrito(pk);
        else _anfitrion?.PararGrito();
        HayVacia = !casilla.Ocupada;
        TextoVacia = casilla.Ref is { } r ? IdiomaInterfaz.F("Cajas.Vacia", r.Descripcion) : IdiomaInterfaz.T("Cajas.CasillaVacia");
    }

    private FichaViewModel CrearFicha(CasillaViewModel c) => new(c, Sesion, () => Editar(), _anfitrion is not null ? a => LanzarAccion(a, c) : null, () => AbrirMenuCasilla(c), c.Ref?.Descripcion,
        () => MostrarLegalidad(c), AjustesPkhexActuales.Display.SlotLegalityAlwaysVisible, AjustesPkhexActuales, () => _anfitrion?.Portapapeles, Aviso);

    [RelayCommand]
    public void Editar()
    {
        if (Seleccionada is { Entidad: { } pk, Ref: { } r }) _anfitrion?.AbrirEditor(r, pk);
    }

    private void LanzarAccion(AccionPokemon accion, CasillaViewModel c)
    {
        if (c.Ref is { } r) _anfitrion?.LanzarAccion(accion, r);
    }

    // ---- Deshacer y Rehacer ----
    public bool PuedeDeshacer => Sesion.Casillas.PuedeDeshacer;
    public bool PuedeRehacer => Sesion.Casillas.PuedeRehacer;

    [RelayCommand]
    public void Deshacer() => Resultado(Sesion.Casillas.Deshacer(), conDeshacer: false);

    [RelayCommand]
    public void Rehacer() => Resultado(Sesion.Casillas.Rehacer(), conDeshacer: true);

    private void AlCambiar(EditorCasillas.TipoCambio tipo)
    {
        Refrescar();
        // La zona de destino de un arrastre (otra pestana, a la vista mientras dura) tambien se pone al dia.
        if (MostrarApoyoEquipo && !EsEquipo) CargarEquipo();
        if (MostrarApoyoCaja && !EsCajas) CargarCaja(Caja, seleccionar: false);
    }

    private void AlTocar(EditorCasillas.TipoCambio tipo, IReadOnlyList<ISlotInfo> tocadas)
    {
        // El ultimo cambio se pinta sobre la casilla (colores de SlotTouchType de PKHeX): lo escrito en verde, lo borrado o deshecho en rojo.
        Dispatch(() =>
        {
            foreach (var c in TodasLasVisibles)
            {
                if (c.Ref is not { } r || !tocadas.Any(t => MismaCasilla(t, r.Info))) continue;
                c.Toque = tipo == EditorCasillas.TipoCambio.Deshacer || c.Vacia ? Toque.Borrar : Toque.Colocar;
            }
        });
    }

    private static void Dispatch(Action a) => a();

    private static bool MismaCasilla(ISlotInfo a, ISlotInfo b) => (a, b) switch
    {
        (SlotInfoBox x, SlotInfoBox y) => x.Box == y.Box && x.Slot == y.Slot,
        (SlotInfoParty x, SlotInfoParty y) => x.Slot == y.Slot,
        _ => a.Type == b.Type && a.Slot == b.Slot,
    };

    /// <summary>Avisa del resultado de una operacion: lo bueno con «Deshacer» a mano (permanece hasta que se use), lo malo con su motivo y sin alternativas.</summary>
    internal void Resultado(InformeOperacion r, bool conDeshacer = true)
    {
        OnPropertyChanged(nameof(PuedeDeshacer)); OnPropertyChanged(nameof(PuedeRehacer));
        if (_anfitrion is null) return;
        if (r.Correcto) _anfitrion.Informar(r.Mensaje, "ok", "CircleCheck", conDeshacer && Sesion.Casillas.PuedeDeshacer ? IdiomaInterfaz.T("SubeditorHoja.Deshacer") : null, conDeshacer && Sesion.Casillas.PuedeDeshacer ? DeshacerCommand : null);
        else _anfitrion.Informar(r.Mensaje, r.Tipo == TipoResultado.SinCambios ? "aviso" : "error", r.Tipo == TipoResultado.SinCambios ? "Info" : "TriangleAlert");
    }

    protected void Mostrar(HojaViewModel hoja) => _anfitrion?.AbrirHoja(hoja);
    protected void Ocultar() => _anfitrion?.CerrarHoja();
}
