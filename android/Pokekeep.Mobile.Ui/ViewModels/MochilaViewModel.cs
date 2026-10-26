// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;
using Pokekeep.Mobile.Ui.Modelo;
using Pokekeep.Mobile.Ui.Partida;

namespace Pokekeep.Mobile.Ui.ViewModels;

/// <summary>Cabecera de la lista de la mochila (titulo, bolsillos, acciones): va DENTRO de la lista para desplazarse con ella (con texto grande no cabe fija).</summary>
public sealed record EncabezadoMochila(MochilaViewModel Vm);

/// <summary>Pie de la lista: «Añadir objeto».</summary>
public sealed record PieMochila(MochilaViewModel Vm);

/// <summary>Un bolsillo de la mochila como pestaña (los de verdad del juego, con su <see cref="InventoryType"/>; nada inventado).</summary>
public sealed partial class BolsilloChipVm(BolsilloModelo bolsillo) : ObservableObject
{
    public BolsilloModelo Bolsillo { get; } = bolsillo;
    public string Titulo => Bolsillo.Titulo;
    public Bitmap? ImagenIngredientes
    {
        get
        {
            if (!Bolsillo.EsIngredientesCampamento) return null;
            var ids = Bolsillo.Pouch.GetAllItems();
            var sav = Bolsillo.Sesion.Guardado;
            return ids.Length == 0 ? null : Design.Sprites.ObjetoDeBolsa(ids[0], sav.Context, sav.Version);
        }
    }
    public bool MostrarIngredientes => ImagenIngredientes is not null;
    [ObservableProperty] private bool _elegido;
    public string Accesible => IdiomaInterfaz.F("Mochila.BolsilloAccesible", Titulo);
    public void ActualizarIdioma() { OnPropertyChanged(nameof(Titulo)); OnPropertyChanged(nameof(Accesible)); }
}

/// <summary>Una fila de un bolsillo: objeto, cantidad y las marcas que ese juego guarda. Escribe en el borrador de la mochila, nunca en la partida.</summary>
public sealed partial class FilaObjetoVm : ObservableObject
{
    private readonly MochilaViewModel _vm;
    private readonly BolsilloModelo _bolsillo;
    private bool _releyendo;
    private string? _textoRechazado;

    public FilaObjetoVm(MochilaViewModel vm, BolsilloModelo bolsillo, int fila)
    {
        _vm = vm; _bolsillo = bolsillo; Fila = fila;
        Releer();
    }

    public int Fila { get; }
    private FilaObjeto Datos => _bolsillo.Filas[Fila];
    private ModeloMochila M => _vm.Modelo;

    public int Objeto => Datos.Id;
    public string Nombre => Datos.Id == 0 ? IdiomaInterfaz.T("Mochila.HuecoLibre") : M.Nombre(Datos.Id);
    public Bitmap? Imagen => Design.Sprites.ObjetoDeBolsa(Datos.Id, _vm.Contexto, _vm.VersionJuego);
    /// <summary>Objeto sin arte en ningun paquete (muchos objetos clave y novedades, tampoco en PKHeX): se dibuja un glifo propio en vez de dejar el hueco vacio.</summary>
    public bool MostrarGlifo => Datos.Id > 0 && Imagen is null;
    public int Maximo => Datos.Id == 0 ? _bolsillo.MaxCount : M.MaximoDe(_bolsillo, Datos.Id);
    public string TextoMaximo => IdiomaInterfaz.F("Mochila.Maximo", Maximo);
    public bool PuedeCambiarObjeto => true;
    public bool ObjetoFijo => false;
    public bool PuedeQuitar => true;
    public string Accesible => IdiomaInterfaz.F("Mochila.FilaAccesible", Nombre, Datos.Cantidad, Maximo);

    [ObservableProperty] private string _cantidad = string.Empty;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private bool _favorito, _nuevo, _nuevoTienda, _sostenido, _libre;

    public bool HayError => !string.IsNullOrEmpty(Error);
    public bool TieneFavorito => M.TieneFavorito;
    public bool TieneNuevo => M.TieneNuevo;
    public bool TieneNuevoTienda => M.TieneNuevoTienda;
    public bool TieneSostenido => M.TieneSostenido;
    public bool TieneLibre => M.TieneLibre;
    public bool HayMarcas => TieneFavorito || TieneNuevo || TieneNuevoTienda || TieneSostenido || TieneLibre;

    partial void OnErrorChanged(string? value) => OnPropertyChanged(nameof(HayError));

    partial void OnCantidadChanged(string value)
    {
        if (_releyendo) return;
        Error = M.PonerCantidad(_bolsillo, Fila, value);
        _textoRechazado = Error is null ? null : value;
        if (Error is null) _vm.AlCambiar();
        // Un numero bien escrito pero por encima del limite se rechaza de inmediato: el campo vuelve al ultimo valor valido y el motivo sigue a la vista (misma regla en todos los editores de partida).
        else if (int.TryParse(value.Trim(), out var n) && n >= 0)
        {
            _releyendo = true;
            try { Cantidad = Datos.Cantidad.ToString(); } finally { _releyendo = false; }
        }
    }

    partial void OnFavoritoChanged(bool value) { if (!_releyendo) { Datos.Favorito = value; _bolsillo.Tocar(); _vm.AlCambiar(); } }
    partial void OnNuevoChanged(bool value) { if (!_releyendo) { Datos.Nuevo = value; _bolsillo.Tocar(); _vm.AlCambiar(); } }
    partial void OnNuevoTiendaChanged(bool value) { if (!_releyendo) { Datos.NuevoTienda = value; _bolsillo.Tocar(); _vm.AlCambiar(); } }
    partial void OnSostenidoChanged(bool value) { if (!_releyendo) { Datos.Sostenido = value; _bolsillo.Tocar(); _vm.AlCambiar(); } }
    partial void OnLibreChanged(bool value) { if (!_releyendo) { Datos.Libre = value; _bolsillo.Tocar(); _vm.AlCambiar(); } }

    /// <summary>Al salir del campo: si la cantidad escrita se rechazo, vuelve el ultimo valor valido (el motivo sigue a la vista).</summary>
    public void PerderFoco()
    {
        if (Error is null) return;
        _releyendo = true;
        try { Cantidad = Datos.Cantidad.ToString(); } finally { _releyendo = false; }
    }

    public void Releer()
    {
        _releyendo = true;
        try
        {
            var d = Datos;
            Cantidad = d.Cantidad.ToString(); Error = null; _textoRechazado = null;
            Favorito = d.Favorito; Nuevo = d.Nuevo; NuevoTienda = d.NuevoTienda; Sostenido = d.Sostenido; Libre = d.Libre;
        }
        finally { _releyendo = false; }
        ActualizarNombres();
    }

    public void ActualizarNombres()
    {
        if (_textoRechazado is { } texto) Error = M.RechazoCantidad(_bolsillo, Datos.Id, texto);
        OnPropertyChanged(nameof(Nombre)); OnPropertyChanged(nameof(Imagen)); OnPropertyChanged(nameof(MostrarGlifo)); OnPropertyChanged(nameof(Maximo)); OnPropertyChanged(nameof(TextoMaximo)); OnPropertyChanged(nameof(Accesible));
    }

    [RelayCommand] private void Cambiar() => _vm.ElegirObjeto(this);
    [RelayCommand] private void Quitar() => _vm.QuitarFila(this);
}

/// <summary>
/// Mochila (<c>SAV_Inventory</c>): bolsillos reales del guardado con el limite real de cada uno, objetos con su sprite y nombre en castellano, cantidades con rechazo motivado, «dar todos», ordenar y limpiar.
/// Trabaja sobre un borrador (<see cref="ModeloMochila"/>); «Aplicar a la partida» lo escribe como UNA entrada del historial (un Deshacer).
/// </summary>
public sealed partial class MochilaViewModel : ObservableObject
{
    private readonly Sesion _sesion;
    internal PKHeX.Core.EntityContext Contexto => _sesion.Guardado.Context;
    internal PKHeX.Core.GameVersion VersionJuego => _sesion.Guardado.Version;
    private readonly Action<HojaViewModel> _abrirHoja;
    private readonly Action<string, string, string, string?, ICommand?> _informar;
    private bool _aplicando;

    private readonly Action<Pokekeep.Mobile.Ui.Cajas.EditorCasillas.TipoCambio> _alCambiarCasillas;   // referencia fuerte de la suscripcion debil a Sesion.CasillasCambiadas
    public MochilaViewModel(Sesion sesion, Action<HojaViewModel> abrirHoja, Action<string, string, string, string?, ICommand?> informar)
    {
        _sesion = sesion; _abrirHoja = abrirHoja; _informar = informar;
        Modelo = new ModeloMochila(sesion);
        _huellaPartida = HuellaDe(sesion);
        _encabezado = new EncabezadoMochila(this); _pie = new PieMochila(this);
        Construir();
        // Deshacer/Rehacer (o cualquier otro cambio del guardado hecho por otra via) deja el borrador desfasado: se vuelve a leer.
        _alCambiarCasillas = _ => AlCambiarLaPartida();
        sesion.SuscribirCasillasDebil(_alCambiarCasillas);
    }

    // Lectura perezosa: mientras la Mochila no esta a la vista, un cambio de otro destino (cada casilla movida en las cajas) solo la marca como desfasada; se vuelve a leer al volver a ella.
    private bool _enPantalla = true, _recargaPendiente;

    /// <summary>La Shell lo pone a false cuando el destino activo no es la Mochila; al volver a true se relee si hubo cambios mientras tanto.</summary>
    public bool EnPantalla
    {
        get => _enPantalla;
        set { _enPantalla = value; if (value && _recargaPendiente) { _recargaPendiente = false; Recargar(); } }
    }

    /// <summary>Cambió el idioma de los datos (bloque 10-1): sin borrador se vuelven a leer los nombres (al momento si está a la vista, o al volver); un borrador sin aplicar no se pierde y toma los nombres nuevos al aplicarlo.</summary>
    public void ReleerNombres()
    {
        Modelo.ActualizarNombres();
        foreach (var bolsillo in Bolsillos) bolsillo.ActualizarIdioma();
        foreach (var fila in Filas) fila.ActualizarNombres();
        ActualizarResumen();
        OnPropertyChanged(nameof(TextoCambios)); OnPropertyChanged(nameof(VacioTexto)); OnPropertyChanged(nameof(MotivoSinMochila));
    }

    /// <summary>Huella de la mochila de la PARTIDA (no del borrador): sirve para saber si otro cambio (Deshacer, Rehacer...) la ha tocado de verdad.</summary>
    private string _huellaPartida = string.Empty;
    private static string HuellaDe(Sesion s) { try { var b = s.Guardado.Inventory; return string.Join('|', b.Pouches.SelectMany(p => p.Items.Select(i => $"{i.Index}:{i.Count}"))); } catch (Exception ex) when (ex is ArgumentOutOfRangeException or IndexOutOfRangeException or InvalidOperationException or NotSupportedException) { return string.Empty; } }

    /// <summary>
    /// Un cambio de casillas (mover un Pokemon, Deshacer, Rehacer) no toca la mochila: NO se relee en silencio y el borrador sin aplicar no se pierde. Solo si la mochila de la partida cambio DE VERDAD:
    /// sin borrador se relee (al volver a la vista, si no esta); con borrador se conserva y se avisa para que el usuario elija entre Descartar y Aplicar encima.
    /// </summary>
    private void AlCambiarLaPartida()
    {
        if (_aplicando) return;
        // Sin borrador no hace falta mirar nada (la huella cuesta leer toda la mochila): se relee al momento si esta a la vista, o al volver a ella.
        if (!Modelo.HayCambios) { if (_enPantalla) Recargar(); else _recargaPendiente = true; return; }
        var huella = HuellaDe(_sesion);
        if (huella == _huellaPartida) return;
        _huellaPartida = huella;
        _informar(IdiomaInterfaz.T("Mochila.CambioExterno"), "aviso", "CircleAlert", null, null);
    }

    public ModeloMochila Modelo { get; private set; }
    public ObservableCollection<BolsilloChipVm> Bolsillos { get; } = [];
    public ObservableCollection<FilaObjetoVm> Filas { get; } = [];
    /// <summary>Lo que pinta la lista: la cabecera, las filas del bolsillo y el pie, como una sola lista desplazable y virtualizada.</summary>
    public ObservableCollection<object> Elementos { get; } = [];
    private readonly EncabezadoMochila _encabezado;
    private readonly PieMochila _pie;
    [ObservableProperty] private BolsilloChipVm? _actual;
    [ObservableProperty] private bool _hayCambios;
    [ObservableProperty] private bool _soloConCantidad = true;
    [ObservableProperty] private string _resumen = string.Empty;
    [ObservableProperty] private string _avisoDescartados = string.Empty;

    public bool SinMochila => Modelo.SinMochila;
    public bool ConMochila => !Modelo.SinMochila;
    public string MotivoSinMochila => Modelo.MotivoSinMochila ?? IdiomaInterfaz.T("Mochila.SinMochila");
    public bool ObjetoFijo => Modelo.ObjetoFijo;
    public bool HayAvisoDescartados => !string.IsNullOrEmpty(AvisoDescartados);
    public bool PuedeAnadir => Actual is { } a && a.Bolsillo.PrimeraLibre() >= 0;
    public bool NoPuedeAnadir => !PuedeAnadir;
    public string MotivoNoAnadir => Actual is null ? string.Empty : IdiomaInterfaz.F("Mochila.Lleno", Actual.Bolsillo.Ranuras, Actual.Bolsillo.Ranuras);
    public bool HayFilas => Filas.Count > 0;
    public bool NoHayFilas => !HayFilas;
    public string TextoCambios => IdiomaInterfaz.T(HayCambios ? "Mochila.HayCambios" : "Mochila.SinCambios");
    public bool PuedeDarTodos => Actual is { } a && Modelo.PuedeDarTodos(a.Bolsillo);
    /// <summary>uso10: el interruptor está en todos los juegos (el propietario lo echó en falta en Espada); en los de objeto fijo es donde más filas quita.</summary>
    public bool MostrarFiltro => ConMochila;
    public string VacioTexto => IdiomaInterfaz.T(SoloConCantidad ? "Mochila.VacioFiltro" : "Mochila.Vacio");

    partial void OnSoloConCantidadChanged(bool value) { OnPropertyChanged(nameof(VacioTexto)); Rellenar(); }
    partial void OnHayCambiosChanged(bool value) => OnPropertyChanged(nameof(TextoCambios));

    private void Construir()
    {
        Bolsillos.Clear();
        foreach (var b in Modelo.Bolsillos) Bolsillos.Add(new BolsilloChipVm(b));
        Actual = Bolsillos.FirstOrDefault();
        OnPropertyChanged(nameof(SinMochila)); OnPropertyChanged(nameof(ConMochila)); OnPropertyChanged(nameof(MotivoSinMochila)); OnPropertyChanged(nameof(ObjetoFijo)); OnPropertyChanged(nameof(MostrarFiltro));
        Rellenar();
    }

    partial void OnActualChanged(BolsilloChipVm? value)
    {
        foreach (var b in Bolsillos) b.Elegido = ReferenceEquals(b, value);
        Rellenar();
    }

    /// <summary>Recarga las filas del bolsillo actual desde el borrador.</summary>
    public void Rellenar()
    {
        Filas.Clear();
        if (Actual is { } a)
        {
            foreach (var (i, _) in a.Bolsillo.Visibles(SoloConCantidad)) Filas.Add(new FilaObjetoVm(this, a.Bolsillo, i));
        }
        ActualizarResumen();
        ReconstruirElementos();
        OnPropertyChanged(nameof(HayFilas)); OnPropertyChanged(nameof(NoHayFilas)); OnPropertyChanged(nameof(PuedeDarTodos)); OnPropertyChanged(nameof(VacioTexto));
        HayCambios = Modelo.HayCambios;
    }

    private void ActualizarResumen()
    {
        if (Actual is { } a)
        {
            var usadas = a.Bolsillo.Filas.Count(f => f.Id != 0);
            Resumen = a.Bolsillo.ObjetoFijo
                ? IdiomaInterfaz.P("Mochila.ResumenFijo", usadas, a.Bolsillo.MaxCount)
                : IdiomaInterfaz.F("Mochila.Resumen", usadas, a.Bolsillo.Ranuras, a.Bolsillo.MaxCount);
            AvisoDescartados = a.Bolsillo.Descartados.Count == 0 ? string.Empty
                : IdiomaInterfaz.F("Mochila.AvisoDescartados", IdiomaInterfaz.P("Mochila.Descartados", a.Bolsillo.Descartados.Count), string.Join(", ", a.Bolsillo.Descartados.Take(4).Select(Modelo.Nombre)) + (a.Bolsillo.Descartados.Count > 4 ? "…" : ""));
        }
        else { Resumen = string.Empty; AvisoDescartados = string.Empty; }
        OnPropertyChanged(nameof(HayAvisoDescartados)); OnPropertyChanged(nameof(PuedeAnadir)); OnPropertyChanged(nameof(NoPuedeAnadir)); OnPropertyChanged(nameof(MotivoNoAnadir));
    }

    private void ReconstruirElementos()
    {
        if (Elementos.Count == 0) Elementos.Add(_encabezado);
        while (Elementos.Count > 1) Elementos.RemoveAt(Elementos.Count - 1);
        if (!ConMochila) return;
        foreach (var f in Filas) Elementos.Add(f);
        Elementos.Add(_pie);
    }

    /// <summary>Una edicion de la tabla: solo se refresca el resumen y el aviso de cambios.</summary>
    public void AlCambiar()
    {
        HayCambios = Modelo.HayCambios;
        OnPropertyChanged(nameof(PuedeAnadir)); OnPropertyChanged(nameof(NoPuedeAnadir));
    }

    private void Recargar()
    {
        var idx = Actual is null ? 0 : Math.Max(0, Bolsillos.IndexOf(Actual));
        Modelo = new ModeloMochila(_sesion);
        _huellaPartida = HuellaDe(_sesion);
        Construir();
        if (idx < Bolsillos.Count) Actual = Bolsillos[idx];
    }

    [RelayCommand] private void Elegir(BolsilloChipVm? chip) { if (chip is not null) Actual = chip; }

    // ---- Objeto de una fila ----
    internal void ElegirObjeto(FilaObjetoVm fila)
    {
        if (Actual is not { } a) return;
        var opciones = OpcionesDe(a.Bolsillo, fila.Objeto);
        _abrirHoja(new SelectorHojaViewModel(IdiomaInterfaz.F("Mochila.SelectorObjeto", a.Titulo), opciones, fila.Objeto, o =>
        {
            Modelo.PonerObjeto(a.Bolsillo, fila.Fila, o.Id);
            Rellenar();
        }, actualizarOpciones: () => OpcionesDe(a.Bolsillo, fila.Objeto), tituloActual: () => IdiomaInterfaz.F("Mochila.SelectorObjeto", a.Titulo), marcadorActual: () => IdiomaInterfaz.T("Mochila.Buscar")));
    }

    internal void QuitarFila(FilaObjetoVm fila)
    {
        if (Actual is not { } a) return;
        Modelo.Quitar(a.Bolsillo, fila.Fila);
        Rellenar();
    }

    [RelayCommand]
    private void Anadir()
    {
        if (Actual is not { } a) return;
        var libre = a.Bolsillo.PrimeraLibre();
        if (libre < 0) { _informar(MotivoNoAnadir, "aviso", "CircleAlert", null, null); return; }
        var opciones = OpcionesDe(a.Bolsillo);
        _abrirHoja(new SelectorHojaViewModel(IdiomaInterfaz.F("Mochila.SelectorAnadir", a.Titulo), opciones, 0, o =>
        {
            var l = a.Bolsillo.PrimeraLibre();
            if (l < 0) return;
            Modelo.PonerObjeto(a.Bolsillo, l, o.Id);
            Rellenar();
        }, actualizarOpciones: () => OpcionesDe(a.Bolsillo), tituloActual: () => IdiomaInterfaz.F("Mochila.SelectorAnadir", a.Titulo), marcadorActual: () => IdiomaInterfaz.T("Mochila.Buscar")));
    }

    public IReadOnlyList<OpcionSelector> OpcionesDe(BolsilloModelo bolsillo, int conservar = 0) =>
        [.. bolsillo.Admitidos(conservar).Where(id => !bolsillo.ObjetoFijo || bolsillo.Filas.Any(f => f.Id == id && (f.Cantidad == 0 || id == conservar))).Select(id => new OpcionSelector(id, Modelo.Nombre(id), ObtenerIcono:
            () => Design.Sprites.ObjetoDeBolsa(id, Contexto, VersionJuego)))];

    // ---- Acciones del bolsillo (los menus «Dar todos», «Ordenar» y el resto del formulario) ----
    [RelayCommand]
    private void AbrirAcciones()
    {
        if (Actual is not { } a) return;
        var textos = TextosAcciones(a.Bolsillo);
        _abrirHoja(new MenuHojaViewModel(textos.Titulo, textos.Secciones, pie: textos.Pie,
            actualizarTextos: () => TextosAcciones(a.Bolsillo)));
    }

    private TextosMenuHoja TextosAcciones(BolsilloModelo bolsillo)
    {
        var acciones = new List<EntradaHoja>();
        if (Modelo.PuedeDarTodos(bolsillo))
        {
            acciones.Add(new(IdiomaInterfaz.T("Mochila.DarTodos"), IdiomaInterfaz.T("Mochila.DarTodosAyuda"), "Gift", () => PedirCantidadDarTodos(bolsillo)));
            acciones.Add(new(IdiomaInterfaz.T("Mochila.CambiarTodas"), IdiomaInterfaz.T("Mochila.CambiarTodasAyuda"), "ListChecks", () => PedirCantidadTodos(bolsillo)));
        }
        acciones.Add(new(IdiomaInterfaz.T("Mochila.QuitarTodos"), IdiomaInterfaz.T("Mochila.QuitarTodosAyuda"), "Eraser", () => ConfirmarQuitarTodos(bolsillo), Destructiva: true));
        var orden = new List<EntradaHoja>
        {
            new(IdiomaInterfaz.T("Mochila.OrdenNombre"), null, "ArrowUpDown", () => Ordenar(bolsillo, b => Modelo.OrdenarPorNombre(b, false))),
            new(IdiomaInterfaz.T("Mochila.OrdenNombreInverso"), null, "ArrowUpDown", () => Ordenar(bolsillo, b => Modelo.OrdenarPorNombre(b, true))),
            new(IdiomaInterfaz.T("Mochila.OrdenCantidad"), null, "ArrowUpDown", () => Ordenar(bolsillo, b => Modelo.OrdenarPorCantidad(b, false))),
            new(IdiomaInterfaz.T("Mochila.OrdenCantidadInversa"), null, "ArrowUpDown", () => Ordenar(bolsillo, b => Modelo.OrdenarPorCantidad(b, true))),
            new(IdiomaInterfaz.T("Mochila.OrdenId"), null, "ArrowUpDown", () => Ordenar(bolsillo, b => Modelo.OrdenarPorNumero(b, false))),
            new(IdiomaInterfaz.T("Mochila.OrdenIdInverso"), null, "ArrowUpDown", () => Ordenar(bolsillo, b => Modelo.OrdenarPorNumero(b, true))),
        };
        return new TextosMenuHoja(IdiomaInterfaz.F("Mochila.TituloAcciones", bolsillo.Titulo), [new SeccionHoja(null, acciones), new SeccionHoja(IdiomaInterfaz.T("Mochila.Ordenar"), orden)],
            Pie: IdiomaInterfaz.T("Mochila.PieAcciones"));
    }

    private void Ordenar(BolsilloModelo b, Action<BolsilloModelo> hacer) { hacer(b); Rellenar(); }

    private TextosNumeroHoja TextosDarTodos(BolsilloModelo b) => new(
        IdiomaInterfaz.T("Mochila.DarTodos"), Modelo.Apretado(b)
            ? IdiomaInterfaz.F("Mochila.DarApretado", b.Titulo, b.Ranuras)
            : IdiomaInterfaz.F("Mochila.DarDescripcion", b.Titulo),
        IdiomaInterfaz.T("Mochila.CantidadCada"), IdiomaInterfaz.T("Mochila.Dar"));

    private void PedirCantidadDarTodos(BolsilloModelo b)
    {
        var t = TextosDarTodos(b);
        _abrirHoja(new NumeroHojaViewModel(t.Titulo, t.Descripcion, t.Etiqueta, Modelo.CantidadSugerida(b), 1, b.MaxCount, t.Boton, n =>
        {
            Modelo.DarTodos(b, n, barajar: false); Rellenar();
            _informar(IdiomaInterfaz.T("Mochila.Rellenado"), "ok", "CircleCheck", null, null);
        }, () => TextosDarTodos(b)));
    }

    private TextosNumeroHoja TextosCantidadTodos(BolsilloModelo b) => new(
        IdiomaInterfaz.T("Mochila.CambiarTodas"), IdiomaInterfaz.F("Mochila.CambiarDescripcion", b.Titulo),
        IdiomaInterfaz.T("Mochila.Cantidad"), IdiomaInterfaz.T("Mochila.CambiarBoton"));

    private void PedirCantidadTodos(BolsilloModelo b)
    {
        var t = TextosCantidadTodos(b);
        _abrirHoja(new NumeroHojaViewModel(t.Titulo, t.Descripcion, t.Etiqueta, Modelo.CantidadSugerida(b), 1, b.MaxCount, t.Boton, n =>
        {
            Modelo.CambiarTodasLasCantidades(b, n); Rellenar();
            _informar(IdiomaInterfaz.T("Mochila.Cambiadas"), "ok", "CircleCheck", null, null);
        }, () => TextosCantidadTodos(b)));
    }

    private static TextosConfirmarHoja TextosQuitar(BolsilloModelo b) => new(
        IdiomaInterfaz.F("Mochila.QuitarTitulo", b.Titulo),
        [IdiomaInterfaz.T("Mochila.QuitarParrafo"), IdiomaInterfaz.T("Mochila.AplicarParrafo")], IdiomaInterfaz.T("Mochila.QuitarTodos"));

    private void ConfirmarQuitarTodos(BolsilloModelo b)
    {
        var t = TextosQuitar(b);
        _abrirHoja(new ConfirmarHojaViewModel(t.Titulo, t.Parrafos, t.Confirmar, () =>
        {
            Modelo.QuitarTodos(b); Rellenar();
        }, destructiva: true, actualizarTextos: () => TextosQuitar(b)));
    }

    // ---- Aplicar / descartar ----
    [RelayCommand]
    private void Aplicar()
    {
        if (!Modelo.HayCambios) return;
        _aplicando = true;
        try
        {
            var r = Modelo.Aplicar();
            _huellaPartida = HuellaDe(_sesion);
            Rellenar();
            if (r.SinCambios) _informar(IdiomaInterfaz.T("Mochila.YaEraAsi"), "aviso", "Info", null, null);
            else _informar(IdiomaInterfaz.T("Mochila.Aplicada"), "ok", "CircleCheck", null, null);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IndexOutOfRangeException or FormatException or OverflowException or KeyNotFoundException or NotSupportedException)
        {
            // El historial ya ha devuelto la partida a como estaba; el borrador se conserva para que el usuario lo corrija o lo descarte.
            _informar(IdiomaInterfaz.F("Mochila.NoAdmitido", ex.Message), "error", "TriangleAlert", null, null);
        }
        finally { _aplicando = false; }
    }

    [RelayCommand]
    private void Descartar()
    {
        Recargar();
        _informar(IdiomaInterfaz.T("Mochila.Descartada"), "ok", "CircleCheck", null, null);
    }
}

/// <summary>Hoja con un numero (cantidad) y un boton: «Dar todos» y «Cambiar la cantidad de todos».</summary>
public sealed record TextosNumeroHoja(string Titulo, string Descripcion, string Etiqueta, string Boton);

public sealed partial class NumeroHojaViewModel : HojaViewModel
{
    private TextosNumeroHoja _textos;
    private readonly Func<TextosNumeroHoja>? _actualizarTextos;
    private readonly Action<int> _aplicar;

    public NumeroHojaViewModel(string titulo, string descripcion, string etiqueta, int valor, int min, int max, string textoBoton, Action<int> aplicar, Func<TextosNumeroHoja>? actualizarTextos = null)
    {
        _textos = new(titulo, descripcion, etiqueta, textoBoton); _actualizarTextos = actualizarTextos;
        Min = min; Max = max; _aplicar = aplicar;
        Valor = valor.ToString();
    }

    public override string Titulo => _textos.Titulo;
    public string Descripcion => _textos.Descripcion;
    public string Etiqueta => _textos.Etiqueta;
    public int Min { get; }
    public int Max { get; }
    public string TextoBoton => _textos.Boton;
    public override void ActualizarIdioma()
    {
        if (_actualizarTextos is { } actualizar) _textos = actualizar();
        OnPropertyChanged(nameof(Titulo)); OnPropertyChanged(nameof(Descripcion));
        OnPropertyChanged(nameof(Etiqueta)); OnPropertyChanged(nameof(TextoBoton)); OnPropertyChanged(nameof(Rango));
        // Validación de presentación: no confirma ni cambia el texto que se está escribiendo.
        OnValorChanged(Valor);
    }
    public string Rango => IdiomaInterfaz.F("Ajustes.Rango", Min, Max);
    [ObservableProperty] private string _valor = string.Empty;
    [ObservableProperty] private string? _error;
    public bool HayError => !string.IsNullOrEmpty(Error);
    partial void OnErrorChanged(string? value) => OnPropertyChanged(nameof(HayError));

    partial void OnValorChanged(string value)
        => Error = int.TryParse(value.Trim(), out var n) && n >= Min && n <= Max ? null : IdiomaInterfaz.F("Numero.Error", Min, Max);

    [RelayCommand]
    private void Confirmar()
    {
        if (!int.TryParse(Valor.Trim(), out var n) || n < Min || n > Max) { Error = IdiomaInterfaz.F("Numero.Error", Min, Max); return; }
        Cerrar(); _aplicar(n);
    }
}
