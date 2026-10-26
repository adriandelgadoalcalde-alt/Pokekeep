// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Ajustes.Pkhex;
using Pokekeep.Mobile.Ui.Herramientas.Equipo;
using Pokekeep.Mobile.Ui.Modelo;
using Pokekeep.Mobile.Ui.ViewModels;
using Pokekeep.Sprites;

namespace Pokekeep.Mobile.Ui.Herramientas.Banco;

public enum ModoBanco { Pokemon, Papelera, Duplicados, Progreso }

/// <summary>Un Pokémon del banco ya listo para pintar: icono, nombre, detalle, insignias y si está elegido o es favorito.</summary>
public sealed partial class FilaBancoVm : ObservableObject
{
    public FilaBancoVm(EntradaBanco e, GameStrings t, ModoSprites modo, bool elegida = false)
    {
        Entrada = e;
        Titulo = e.Huevo ? TextosBanco.Huevo : e.Titulo(t);
        var forma = e.NombreForma(t);
        Subtitulo = e.Huevo ? TextosBanco.DetalleHuevo(e.Generacion) : TextosBanco.DetalleFila(e.Apodo.Length > 0 ? e.NombreEspecie(t) : string.Empty, forma, e.Nivel, e.Generacion, e.NombreJuego);
        Etiquetas = string.Join("  ", e.EtiquetasOVacio.Select(x => "#" + x));
        Icono = new IconoPokemon(string.Empty, e.Especie, e.Forma, 0, 0, e.Variocolor, e.Huevo, e.Contexto, SpritesBanco.Modo(modo, e.Especie, e.Forma, e.Contexto, e.Variocolor), 0);
        _seleccionada = elegida;
        _favorito = e.Favorito;
        (TextoLegal, ClaseLegal, GlifoLegal) = e.Legalidad switch
        {
            EstadoLegalBanco.Legal => (TextosBanco.Legal, "ok", "CircleCheck"),
            EstadoLegalBanco.Ilegal => (TextosBanco.RevisarLegalidad, "error", "TriangleAlert"),
            _ => (TextosBanco.SinAnalizar, string.Empty, "Info"),
        };
    }

    public EntradaBanco Entrada { get; }
    public string Titulo { get; }
    public string Subtitulo { get; }
    public string Etiquetas { get; }
    public bool HayEtiquetas => Etiquetas.Length > 0;
    public IconoPokemon Icono { get; }
    public string TextoLegal { get; }
    public string ClaseLegal { get; }
    public string GlifoLegal { get; }
    public bool EsLegal => Entrada.Legalidad == EstadoLegalBanco.Legal;
    public bool EsIlegal => Entrada.Legalidad == EstadoLegalBanco.Ilegal;
    public bool EsSinAnalizar => Entrada.Legalidad == EstadoLegalBanco.SinAnalizar;
    public bool Variocolor => Entrada.Variocolor;
    public string GuardadoEl => TextosBanco.GuardadoEl(Entrada.Guardado);
    public string EliminadoEl => Entrada.Eliminado is { } d ? TextosBanco.EliminadoEl(d) : string.Empty;
    public bool HayEliminado => Entrada.Eliminado is not null;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(Accesible))] private bool _seleccionada;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(Accesible), nameof(TextoFavorito), nameof(AccesibleFavorito))] private bool _favorito;

    public string TextoFavorito => Favorito ? TextosBanco.QuitarFavorito : TextosBanco.PonerFavorito;
    public string AccesibleFavorito => TextosBanco.FavoritoAccesible(Titulo, Favorito);
    public string Accesible => TextosBanco.FilaAccesible(Titulo, Subtitulo, TextoLegal, Variocolor, Favorito, Seleccionada, Etiquetas);

    internal Action<FilaBancoVm>? AlCambiarSeleccion { get; set; }
    internal Action<FilaBancoVm>? AlAlternarFavorito { get; set; }
    partial void OnSeleccionadaChanged(bool value) => AlCambiarSeleccion?.Invoke(this);
    [RelayCommand] private void AlternarFavorito() => AlAlternarFavorito?.Invoke(this);
}

/// <summary>
/// «Banco de Pokémon» (X38, decisión del propietario): el banco propio, fuera de cualquier partida. Guarda copias de los Pokémon de las cajas y del equipo, las ordena con favoritos, etiquetas y filtros, y las saca a OTRA
/// partida (convertidas al formato de ese juego, con el veredicto de legalidad del Core y «Legalizar» antes de colocarlas). También la papelera, los duplicados, la comparación de dos Pokémon y el progreso frente a la
/// Pokédex nacional. Se abre desde Más, con o sin partida abierta; lo único que escribe en una partida es «Colocar en la partida» (con confirmación visible y un Deshacer) y «Mover al banco».
/// </summary>
public sealed partial class BancoViewModel : HerramientaViewModel
{
    private readonly UbicacionBanco _ubicacion;
    private FiltroBanco _filtro = new();
    private readonly HashSet<string> _marcadas = new(StringComparer.Ordinal);

    public BancoViewModel(ContextoHerramienta contexto, Action volver, UbicacionBanco ubicacion) : base(contexto, volver)
    {
        _ubicacion = ubicacion;
        TextosNucleo.Preparar();
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(Estado) or nameof(Cargando)) OnPropertyChanged(nameof(MuestraEstado));
            if (e.PropertyName is nameof(Modo) or nameof(Sacando) or nameof(Comparando) or nameof(Cargando))
                foreach (var n in new[] { nameof(VePestanas), nameof(VePokemon), nameof(VePapelera), nameof(VeDuplicados), nameof(VeProgreso), nameof(VeUbicacion), nameof(VeTitulo), nameof(VeSubtitulo) }) OnPropertyChanged(n);
        };
        CargaInicial = CargarAsync();
    }

    // ---------------------------------------------------------------------------------------------------------------- carga (en segundo plano)
    /// <summary>La carga del banco (listar la carpeta, leer el índice y, si hace falta, reconstruirlo): NUNCA en el hilo de la interfaz, porque en una carpeta grande de SAF Android da ANR. Las pruebas esperan a esta tarea.</summary>
    public Task CargaInicial { get; private set; }
    private CancellationTokenSource? _cancelarCarga;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(VePestanas), nameof(VePokemon), nameof(VePapelera), nameof(VeDuplicados), nameof(VeProgreso), nameof(NoCargando), nameof(PuedeReintentar))] private bool _cargando;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(VePestanas), nameof(VePokemon), nameof(VePapelera), nameof(VeDuplicados), nameof(VeProgreso), nameof(PuedeReintentar))] private bool _cargado;
    public bool NoCargando => !Cargando;
    public bool PuedeReintentar => !Cargando && !Cargado;

    [RelayCommand]
    public Task Reintentar() => CargaInicial = CargarAsync();

    [RelayCommand]
    public void CancelarCarga() => _cancelarCarga?.Cancel();

    private async Task CargarAsync()
    {
        _cancelarCarga = new CancellationTokenSource();
        var token = _cancelarCarga.Token;
        Cargando = true; Cargado = false; Estado = TextosBanco.CargandoBanco;
        try
        {
            InformeDeCarga = await Task.Run(() => _ubicacion.Cargar(token), token).ConfigureAwait(true);
            Cargado = InformeDeCarga.ErrorDeposito is null;
            Estado = InformeDeCarga.Limpio ? string.Empty : TextosBanco.ResumenCarga(InformeDeCarga);
        }
        catch (OperationCanceledException) { Estado = TextosBanco.CargaCancelada; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Estado = TextosBanco.FalloAlGuardar(ex.Message); }
        finally { Cargando = false; _cancelarCarga?.Dispose(); _cancelarCarga = null; }
        Refrescar();
    }

    public override string Titulo => TextosBanco.Titulo;
    public string Subtitulo => TextosBanco.Subtitulo;
    public GameStrings Textos => Sesion?.Textos ?? ProgramaPkhex.Textos;
    public BancoPokemon Banco => _ubicacion.Banco;
    public InformeCarga InformeDeCarga { get; private set; }
    private ModoSprites ModoIconos => Sesion?.ModoSprites ?? Design.Sprites.ModoVigente;

    public bool HayPartida => Sesion is not null;
    /// <summary>Por qué no se puede sacar nada a una partida (null si se puede).</summary>
    public string? MotivoSinPartida => Sesion is null ? TextosBanco.SacarSinPartida : null;

    private void Avisar(string texto, bool ok)
    {
        Estado = texto;
        Contexto.Informar(texto, ok ? "ok" : "error", ok ? "CircleCheck" : "TriangleAlert");
    }

    // ---------------------------------------------------------------------------------------------------------------- dónde está
    public string TextoUbicacion => TextosBanco.DondeEsta(_ubicacion.Descripcion, _ubicacion.EnCarpetaDelUsuario);
    public bool AvisoPrivada => !_ubicacion.EnCarpetaDelUsuario;
    public string AvisoPrivadaTexto => _ubicacion.CarpetaPerdida ? TextosBanco.CarpetaPerdida(_ubicacion.NombreCarpetaPerdida ?? string.Empty) : TextosBanco.AvisoCarpetaPrivada;
    public bool PuedeElegirCarpeta => _ubicacion.PuedeElegirCarpeta && !Ocupado;

    [RelayCommand]
    public async Task ElegirCarpetaAsync()
    {
        if (!_ubicacion.PuedeElegirCarpeta) { Avisar(TextosBanco.SinSelector, false); return; }
        Ocupado = true; TextoAvance = TextosBanco.TrasladandoBanco; FraccionIndeterminada = true;
        try
        {
            var r = await _ubicacion.ElegirCarpetaAsync(CancellationToken.None).ConfigureAwait(true);   // el traslado va en segundo plano dentro
            if (!r.Elegida && r.Error is null) { Estado = TextosBanco.SinCambiosCarpeta; return; }   // cancelado en el selector
            if (!r.Correcto) { Avisar(TextosBanco.FalloCarpeta(r.Carpeta ?? string.Empty, r.Error ?? TextosBanco.TrasladoConFallos), false); return; }
            InformeDeCarga = await Task.Run(() => _ubicacion.Banco.Cargar()).ConfigureAwait(true);
            Cargado = true;
            Refrescar();
            Avisar(TextosBanco.CarpetaElegida(r.Carpeta ?? string.Empty, r.YaHabia, r.Trasladados), true);
        }
        finally { Ocupado = false; Notificar(nameof(PuedeElegirCarpeta)); }
    }

    // ---------------------------------------------------------------------------------------------------------------- modos
    [ObservableProperty, NotifyPropertyChangedFor(nameof(EsModoPokemon), nameof(EsModoPapelera), nameof(EsModoDuplicados), nameof(EsModoProgreso), nameof(MuestraAccionesPokemon), nameof(MuestraAccionesPapelera))] private ModoBanco _modo = ModoBanco.Pokemon;
    public bool EsModoPokemon => Modo == ModoBanco.Pokemon;
    public bool EsModoPapelera => Modo == ModoBanco.Papelera;
    public bool EsModoDuplicados => Modo == ModoBanco.Duplicados;
    public bool EsModoProgreso => Modo == ModoBanco.Progreso;
    // Sacar y Comparar ocupan el sitio de las listas: las pestañas y las listas se ocultan mientras tanto.
    public bool VePestanas => Cargado && !Cargando && !Sacando && !Comparando;
    /// <summary>Dónde está el banco se ve siempre salvo mientras se saca o se compara (ahí estorba).</summary>
    /// <summary>El título grande solo en las pestañas; sacar y comparar traen el suyo. La descripción larga, solo en la portada (pestaña Pokémon).</summary>
    public bool VeTitulo => !Sacando && !Comparando;
    public bool VeSubtitulo => EsModoPokemon && VeTitulo;
    public bool VeUbicacion => !Sacando && !Comparando && !Cargando;
    /// <summary>El estado en una línea, salvo mientras carga (ahí ya lo dice la tarjeta de «Cargando»).</summary>
    public bool MuestraEstado => HayEstado && !Cargando;
    public bool VePokemon => EsModoPokemon && VePestanas;
    public bool VePapelera => EsModoPapelera && VePestanas;
    public bool VeDuplicados => EsModoDuplicados && VePestanas;
    public bool VeProgreso => EsModoProgreso && VePestanas;

    partial void OnModoChanged(ModoBanco value)
    {
        _marcadas.Clear();
        SalirDeComparar(); SalirDeSacar();
        Refrescar();
    }

    [RelayCommand] public void VerPokemon() => Modo = ModoBanco.Pokemon;
    [RelayCommand] public void VerPapelera() => Modo = ModoBanco.Papelera;
    [RelayCommand] public void VerDuplicados() => Modo = ModoBanco.Duplicados;
    [RelayCommand] public void VerProgreso() => Modo = ModoBanco.Progreso;

    public string TextoModoPokemon => TextosBanco.PestanaPokemon(Banco.CantidadActivas);
    public string TextoModoPapelera => TextosBanco.PestanaPapelera(Banco.CantidadPapelera);
    public string TextoModoDuplicados => TextosBanco.PestanaDuplicados(Grupos.Count);

    // ---------------------------------------------------------------------------------------------------------------- lista y filtros
    /// <summary>Cuántas filas se pintan de una vez: la lista crece con «Mostrar más» para que un banco grande no cree cientos de filas con su sprite.</summary>
    public const int Pagina = 40;

    public ObservableCollection<FilaBancoVm> Filas { get; } = [];
    public ObservableCollection<FilaBancoVm> FilasPapelera { get; } = [];
    private IReadOnlyList<EntradaBanco> _visibles = [];
    private IReadOnlyList<EntradaBanco> _visiblesPapelera = [];
    private int _limite = Pagina, _limitePapelera = Pagina;
    private FiltroBanco? _filtroAplicado;

    public IReadOnlyList<EntradaBanco> Coincidencias => _visibles;
    public bool HayPokemon => Banco.CantidadActivas > 0;
    public bool BancoVacio => Banco.CantidadActivas == 0;
    public bool HayFilas => Filas.Count > 0;
    public bool SinCoincidencias => HayPokemon && _visibles.Count == 0;
    public bool HayPapelera => Banco.CantidadPapelera > 0;
    public bool PapeleraVacia => Banco.CantidadPapelera == 0;
    public string TextoCantidad => _filtro.HayFiltros ? TextosBanco.CantidadFiltrada(_visibles.Count, Banco.CantidadActivas) : TextosBanco.Cantidad(Banco.CantidadActivas);
    public bool HayFiltros => _filtro.HayFiltros;
    public int Restantes => Math.Max(0, _visibles.Count - Filas.Count);
    public bool HayMas => Restantes > 0;
    public string TextoMostrarMas => TextosBanco.MostrarMas(Math.Min(Pagina, Restantes), Restantes);
    public int RestantesPapelera => Math.Max(0, _visiblesPapelera.Count - FilasPapelera.Count);
    public bool HayMasPapelera => RestantesPapelera > 0;
    public string TextoMostrarMasPapelera => TextosBanco.MostrarMas(Math.Min(Pagina, RestantesPapelera), RestantesPapelera);

    [RelayCommand] public void MostrarMas() { _limite += Pagina; RellenarFilas(); }
    [RelayCommand] public void MostrarMasPapelera() { _limitePapelera += Pagina; RellenarPapelera(); }

    [ObservableProperty] private string _texto = string.Empty;
    [ObservableProperty] private bool _filtrosAbiertos;
    partial void OnTextoChanged(string value) { _filtro = _filtro with { Texto = value ?? string.Empty }; Refiltrar(); }

    public string TextoFiltros => FiltrosAbiertos ? TextosBanco.OcultarFiltros : TextosBanco.MostrarFiltros;
    partial void OnFiltrosAbiertosChanged(bool value) => OnPropertyChanged(nameof(TextoFiltros));

    public string TextoTipo => TextosBanco.FiltroTipo(_filtro.Tipo >= 0 && _filtro.Tipo < Textos.Types.Count ? Textos.Types[_filtro.Tipo] : null);
    public string TextoJuego => TextosBanco.FiltroJuego(_filtro.Juego != 0 && ((GameVersion)_filtro.Juego).IsValidSavedVersion() ? NombresJuego.Oficial((GameVersion)_filtro.Juego) : null);
    public string TextoEspecie => TextosBanco.FiltroEspecie(_filtro.Especie > 0 && _filtro.Especie < Textos.Species.Count ? Textos.Species[_filtro.Especie] : null);
    public string TextoEtiqueta => TextosBanco.FiltroEtiqueta(_filtro.Etiqueta.Length > 0 ? _filtro.Etiqueta : null);
    public string TextoVariocolor => TextosBanco.FiltroVariocolor(_filtro.Variocolor);
    public string TextoLegalidad => TextosBanco.FiltroLegalidad(_filtro.Legal);
    public string TextoOrden => TextosBanco.FiltroOrden(_filtro.Orden);
    public bool SoloFavoritos => _filtro.SoloFavoritos;
    public string TextoSoloFavoritos => TextosBanco.SoloFavoritos;

    [RelayCommand]
    public void AlternarSoloFavoritos() { _filtro = _filtro with { SoloFavoritos = !_filtro.SoloFavoritos }; Refiltrar(); }

    [RelayCommand]
    public void CiclarVariocolor() { _filtro = _filtro with { Variocolor = _filtro.Variocolor switch { Tri3.DaIgual => Tri3.Si, Tri3.Si => Tri3.No, _ => Tri3.DaIgual } }; Refiltrar(); }

    [RelayCommand]
    public void CiclarLegalidad() { _filtro = _filtro with { Legal = _filtro.Legal switch { Tri3.DaIgual => Tri3.Si, Tri3.Si => Tri3.No, _ => Tri3.DaIgual } }; Refiltrar(); }

    [RelayCommand]
    public void QuitarFiltros() { _filtro = new FiltroBanco(Orden: _filtro.Orden); if (Texto.Length > 0) Texto = string.Empty; else Refiltrar(); }

    /// <summary>Elige un filtro de una lista (tipo, juego, especie, etiqueta) con el selector de la app; 0 = sin filtro.</summary>
    [RelayCommand]
    public void ElegirTipo()
    {
        var o = new List<OpcionSelector> { new(0, TextosBanco.CualquierTipo) };
        for (var i = 0; i < Textos.Types.Count; i++) o.Add(new OpcionSelector(i + 1, Textos.Types[i]));
        Contexto.AbrirHoja(new SelectorHojaViewModel(TextosBanco.ElegirTipo, o, _filtro.Tipo + 1, op => { _filtro = _filtro with { Tipo = op.Id - 1 }; Refiltrar(); }, TextosBanco.Buscar));
    }

    [RelayCommand]
    public void ElegirJuego()
    {
        var juegos = Banco.Activas.Select(e => e.Juego).Where(j => j != 0).Distinct().Select(j => (Id: j, Nombre: ((GameVersion)j).IsValidSavedVersion() ? NombresJuego.Oficial((GameVersion)j) : "#" + j)).OrderBy(x => x.Nombre, StringComparer.CurrentCultureIgnoreCase);
        var o = new List<OpcionSelector> { new(0, TextosBanco.CualquierJuego) };
        o.AddRange(juegos.Select(j => new OpcionSelector(j.Id, j.Nombre)));
        Contexto.AbrirHoja(new SelectorHojaViewModel(TextosBanco.ElegirJuego, o, _filtro.Juego, op => { _filtro = _filtro with { Juego = op.Id }; Refiltrar(); }, TextosBanco.Buscar));
    }

    [RelayCommand]
    public void ElegirEspecie()
    {
        var o = new List<OpcionSelector> { new(0, TextosBanco.CualquierEspecie) };
        o.AddRange(Banco.Activas.Select(e => e.Especie).Distinct().OrderBy(s => s).Select(s => new OpcionSelector(s, s < Textos.Species.Count ? Textos.Species[s] : "#" + s, null, s)));
        Contexto.AbrirHoja(new SelectorHojaViewModel(TextosBanco.ElegirEspecie, o, _filtro.Especie, op => { _filtro = _filtro with { Especie = op.Id }; Refiltrar(); }, TextosBanco.Buscar));
    }

    [RelayCommand]
    public void ElegirEtiqueta()
    {
        var etiquetas = Banco.Etiquetas();
        var o = new List<OpcionSelector> { new(0, TextosBanco.CualquierEtiqueta) };
        for (var i = 0; i < etiquetas.Count; i++) o.Add(new OpcionSelector(i + 1, etiquetas[i].Etiqueta, TextosBanco.Cantidad(etiquetas[i].Cantidad)));
        var actual = etiquetas.ToList().FindIndex(e => string.Equals(e.Etiqueta, _filtro.Etiqueta, StringComparison.CurrentCultureIgnoreCase)) + 1;
        Contexto.AbrirHoja(new SelectorHojaViewModel(TextosBanco.ElegirEtiqueta, o, actual, op => { _filtro = _filtro with { Etiqueta = op.Id == 0 ? string.Empty : etiquetas[op.Id - 1].Etiqueta }; Refiltrar(); }, TextosBanco.Buscar));
    }

    [RelayCommand]
    public void ElegirOrden()
    {
        EntradaHoja E(OrdenBanco o, string titulo) => new(titulo, null, "ArrowUpDown", () => { _filtro = _filtro with { Orden = o }; Refiltrar(); }, Elegida: _filtro.Orden == o);
        Contexto.AbrirHoja(new MenuHojaViewModel(TextosBanco.OrdenarPor, [new SeccionHoja(null, [E(OrdenBanco.Reciente, TextosBanco.OrdenReciente), E(OrdenBanco.Pokedex, TextosBanco.OrdenPokedex), E(OrdenBanco.Nombre, TextosBanco.OrdenNombre), E(OrdenBanco.Nivel, TextosBanco.OrdenNivel)])]));
    }

    private FilaBancoVm Fila(EntradaBanco e)
        => new(e, Textos, ModoIconos, _marcadas.Contains(e.Id)) { AlCambiarSeleccion = CambioSeleccion, AlAlternarFavorito = AlternarFavoritoFila };

    /// <summary>Vuelve a leer el banco y reconstruye todo lo que se ve. Lo elegido que ya no está se olvida.</summary>
    public void Refrescar()
    {
        _marcadas.IntersectWith(Banco.Entradas.Select(e => e.Id));
        Refiltrar(); RefrescarPapelera(); RecalcularDuplicados(); Progreso = ModeloProgreso.Calcular(Banco.Entradas);
        RefrescarProgreso();
        foreach (var n in new[] { nameof(TextoUbicacion), nameof(AvisoPrivada), nameof(AvisoPrivadaTexto), nameof(PuedeElegirCarpeta), nameof(TextoModoPokemon), nameof(TextoModoPapelera), nameof(TextoModoDuplicados), nameof(HayPokemon), nameof(BancoVacio), nameof(HayPapelera), nameof(PapeleraVacia) }) OnPropertyChanged(n);
    }

    private void Refiltrar()
    {
        if (_filtroAplicado != _filtro) { _limite = Pagina; _filtroAplicado = _filtro; }   // otro filtro vuelve a la primera página; una acción sobre la misma lista no
        _visibles = _filtro.Aplicar(Banco.Entradas, Textos);
        RellenarFilas();
        foreach (var n in new[] { nameof(SinCoincidencias), nameof(TextoCantidad), nameof(HayFiltros), nameof(TextoTipo), nameof(TextoJuego), nameof(TextoEspecie), nameof(TextoEtiqueta), nameof(TextoVariocolor), nameof(TextoLegalidad), nameof(TextoOrden), nameof(SoloFavoritos), nameof(TextoModoPokemon), nameof(Coincidencias) }) OnPropertyChanged(n);
    }

    private void RellenarFilas()
    {
        Filas.Clear();
        foreach (var e in _visibles.Take(_limite)) Filas.Add(Fila(e));
        foreach (var n in new[] { nameof(HayFilas), nameof(Restantes), nameof(HayMas), nameof(TextoMostrarMas) }) OnPropertyChanged(n);
        ActualizarSeleccion();
    }

    private void RefrescarPapelera()
    {
        _visiblesPapelera = [.. Banco.EnLaPapelera];
        RellenarPapelera();
        OnPropertyChanged(nameof(HayPapelera)); OnPropertyChanged(nameof(PapeleraVacia));
    }

    private void RellenarPapelera()
    {
        FilasPapelera.Clear();
        foreach (var e in _visiblesPapelera.Take(_limitePapelera)) FilasPapelera.Add(Fila(e));
        foreach (var n in new[] { nameof(RestantesPapelera), nameof(HayMasPapelera), nameof(TextoMostrarMasPapelera) }) OnPropertyChanged(n);
        ActualizarSeleccion();
    }

    // ---------------------------------------------------------------------------------------------------------------- selección
    private IReadOnlyList<EntradaBanco> ListaDelModo => Modo == ModoBanco.Papelera ? _visiblesPapelera : _visibles;
    private IEnumerable<FilaBancoVm> FilasDelModo => Modo == ModoBanco.Papelera ? FilasPapelera : Filas;
    /// <summary>Lo elegido en la lista que se ve (incluye lo que aún no se ha pintado por «Mostrar más»).</summary>
    public IReadOnlyList<EntradaBanco> Elegidas => _marcadas.Count == 0 ? [] : [.. ListaDelModo.Where(e => _marcadas.Contains(e.Id))];
    public int NumeroElegidas => _marcadas.Count == 0 ? 0 : ListaDelModo.Count(e => _marcadas.Contains(e.Id));
    public bool HayElegidas => NumeroElegidas > 0;
    public bool MuestraAccionesPokemon => EsModoPokemon;
    public bool MuestraAccionesPapelera => EsModoPapelera;
    public bool PuedeComparar => Modo == ModoBanco.Pokemon && NumeroElegidas == 2;
    public bool PuedeSacar => Modo == ModoBanco.Pokemon && HayElegidas && Sesion is not null;
    public string TextoElegidas => TextosBanco.Elegidas(NumeroElegidas);
    public string AyudaComparar => NumeroElegidas == 2 ? TextosBanco.CompararAyuda : TextosBanco.CompararNecesitaDos;

    private void ActualizarSeleccion()
    {
        foreach (var n in new[] { nameof(NumeroElegidas), nameof(HayElegidas), nameof(PuedeComparar), nameof(PuedeSacar), nameof(TextoElegidas), nameof(AyudaComparar), nameof(TextoFavoritoSeleccion) }) OnPropertyChanged(n);
        SacarCommand.NotifyCanExecuteChanged(); CompararCommand.NotifyCanExecuteChanged();
    }

    private void CambioSeleccion(FilaBancoVm f)
    {
        if (f.Seleccionada) _marcadas.Add(f.Entrada.Id); else _marcadas.Remove(f.Entrada.Id);
        ActualizarSeleccion();
    }

    [RelayCommand]
    public void ElegirTodas()
    {
        foreach (var e in ListaDelModo) _marcadas.Add(e.Id);
        foreach (var f in FilasDelModo) f.Seleccionada = true;
        ActualizarSeleccion();
    }

    [RelayCommand]
    public void QuitarSeleccion()
    {
        _marcadas.Clear();
        foreach (var f in FilasDelModo) f.Seleccionada = false;
        ActualizarSeleccion();
    }

    private void AlternarFavoritoFila(FilaBancoVm f)
    {
        var nuevo = !f.Favorito;
        if (Banco.PonerFavorito([f.Entrada.Id], nuevo) is { } fallo) { Avisar(TextosBanco.FalloAlGuardar(fallo), false); return; }
        Refrescar();
        Avisar(nuevo ? TextosBanco.PuestoFavorito(f.Titulo) : TextosBanco.QuitadoFavorito(f.Titulo), true);
    }
    // ---------------------------------------------------------------------------------------------------------------- favoritos y etiquetas de lo elegido
    /// <summary>Si todos los elegidos son favoritos, el botón los quita; si no, los pone.</summary>
    public string TextoFavoritoSeleccion => Elegidas.Count > 0 && Elegidas.All(e => e.Favorito) ? TextosBanco.QuitarFavoritos : TextosBanco.PonerFavoritos;

    [RelayCommand]
    public void FavoritoDeLaSeleccion()
    {
        var e = Elegidas;
        if (e.Count == 0) { Avisar(TextosBanco.ElegirAntes, false); return; }
        var poner = !e.All(x => x.Favorito);
        if (Banco.PonerFavorito(e.Select(x => x.Id), poner) is { } fallo) { Avisar(TextosBanco.FalloAlGuardar(fallo), false); return; }
        Refrescar();
        Avisar(poner ? TextosBanco.PuestosFavoritos(e.Count) : TextosBanco.QuitadosFavoritos(e.Count), true);
    }

    [ObservableProperty] private string _etiquetaTexto = string.Empty;
    public IReadOnlyList<string> EtiquetasExistentes => [.. Banco.Etiquetas().Select(e => e.Etiqueta)];
    public bool HayEtiquetasExistentes => EtiquetasExistentes.Count > 0;

    [RelayCommand]
    public void UsarEtiqueta(string? etiqueta) { if (!string.IsNullOrWhiteSpace(etiqueta)) EtiquetaTexto = etiqueta; }

    [RelayCommand]
    public void AnadirEtiqueta()
    {
        var e = Elegidas; var t = BancoPokemon.LimpiarEtiqueta(EtiquetaTexto);
        if (e.Count == 0) { Avisar(TextosBanco.ElegirAntes, false); return; }
        if (t.Length == 0) { Avisar(TextosBanco.EscribeEtiqueta, false); return; }
        if (Banco.Etiquetar(e.Select(x => x.Id), [t], []) is { } fallo) { Avisar(TextosBanco.FalloAlGuardar(fallo), false); return; }
        EtiquetaTexto = string.Empty;
        Refrescar(); OnPropertyChanged(nameof(EtiquetasExistentes)); OnPropertyChanged(nameof(HayEtiquetasExistentes));
        Avisar(TextosBanco.EtiquetaAnadida(t, e.Count), true);
    }

    [RelayCommand]
    public void QuitarEtiqueta()
    {
        var e = Elegidas; var t = BancoPokemon.LimpiarEtiqueta(EtiquetaTexto);
        if (e.Count == 0) { Avisar(TextosBanco.ElegirAntes, false); return; }
        if (t.Length == 0) { Avisar(TextosBanco.EscribeEtiqueta, false); return; }
        if (Banco.Etiquetar(e.Select(x => x.Id), [], [t]) is { } fallo) { Avisar(TextosBanco.FalloAlGuardar(fallo), false); return; }
        EtiquetaTexto = string.Empty;
        if (_filtro.Etiqueta.Length > 0 && !Banco.Etiquetas().Any(x => string.Equals(x.Etiqueta, _filtro.Etiqueta, StringComparison.CurrentCultureIgnoreCase))) _filtro = _filtro with { Etiqueta = string.Empty };
        Refrescar(); OnPropertyChanged(nameof(EtiquetasExistentes)); OnPropertyChanged(nameof(HayEtiquetasExistentes));
        Avisar(TextosBanco.EtiquetaQuitada(t, e.Count), true);
    }

    // ---------------------------------------------------------------------------------------------------------------- papelera
    /// <summary>Manda a la papelera lo elegido: no borra nada (los archivos se quedan), se recupera desde la pestaña Papelera.</summary>
    [RelayCommand]
    public void AlaPapelera()
    {
        var e = Elegidas;
        if (e.Count == 0) { Avisar(TextosBanco.ElegirAntes, false); return; }
        if (Banco.AlaPapelera(e.Select(x => x.Id)) is { } fallo) { Avisar(TextosBanco.FalloAlGuardar(fallo), false); return; }
        _marcadas.Clear(); Refrescar();
        Avisar(TextosBanco.EnviadosAPapelera(e.Count), true);
    }

    [RelayCommand]
    public void Restaurar()
    {
        var e = Elegidas;
        if (e.Count == 0) { Avisar(TextosBanco.ElegirAntes, false); return; }
        if (Banco.Restaurar(e.Select(x => x.Id)) is { } fallo) { Avisar(TextosBanco.FalloAlGuardar(fallo), false); return; }
        _marcadas.Clear(); Refrescar();
        Avisar(TextosBanco.Restaurados(e.Count), true);
    }

    [RelayCommand]
    public void BorrarParaSiempre()
    {
        var e = Elegidas;
        if (e.Count == 0) { Avisar(TextosBanco.ElegirAntes, false); return; }
        Contexto.AbrirHoja(new ConfirmarHojaViewModel(TextosBanco.BorrarTitulo, [TextosBanco.BorrarTexto(e.Count), TextosBanco.BorrarSinVuelta], TextosBanco.BorrarConfirmar, () => BorrarConfirmado(e.Select(x => x.Id).ToList()), destructiva: true));
    }

    [RelayCommand]
    public void VaciarPapelera()
    {
        var n = Banco.CantidadPapelera;
        if (n == 0) { Avisar(TextosBanco.PapeleraYaVacia, false); return; }
        Contexto.AbrirHoja(new ConfirmarHojaViewModel(TextosBanco.VaciarTitulo, [TextosBanco.BorrarTexto(n), TextosBanco.BorrarSinVuelta], TextosBanco.VaciarConfirmar, () => BorrarConfirmado(Banco.EnLaPapelera.Select(x => x.Id).ToList()), destructiva: true));
    }

    /// <summary>Borra para siempre sin preguntar (la hoja de confirmación ya preguntó).</summary>
    public void BorrarConfirmado(IReadOnlyList<string> ids)
    {
        var (borrados, fallos, error) = Banco.EliminarDefinitivamente(ids);
        _marcadas.Clear(); Refrescar();
        if (error is not null) { Avisar(TextosBanco.FalloAlGuardar(error), false); return; }
        Avisar(fallos == 0 ? TextosBanco.BorradosParaSiempre(borrados) : TextosBanco.BorradosConFallos(borrados, fallos), fallos == 0);
    }

    // ---------------------------------------------------------------------------------------------------------------- utilidades
    private void Notificar(string propiedad) => OnPropertyChanged(propiedad);
}
