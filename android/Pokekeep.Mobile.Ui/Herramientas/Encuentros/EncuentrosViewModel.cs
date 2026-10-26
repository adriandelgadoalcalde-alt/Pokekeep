// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Herramientas.BaseDatos;
using Pokekeep.Mobile.Ui.ViewModels;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Herramientas.Encuentros;

/// <summary>Un tipo de encuentro como casilla (los <c>CheckBox</c> de <c>TypeFilters</c>).</summary>
public sealed partial class TipoEncuentro(EncounterTypeGroup tipo, Action cambiado) : ObservableObject
{
    public EncounterTypeGroup Tipo { get; } = tipo;
    public string Texto { get; } = ModeloEncuentros.NombreTipo(tipo);
    [ObservableProperty] private bool _marcado = true;
    partial void OnMarcadoChanged(bool value) => cambiado();
}

/// <summary>
/// «Más &gt; Herramientas &gt; Generador de encuentros» (<c>SAV_Encounters</c>, PK-SUB-010 a PK-SUB-013, PK-MENU-021): pestañas General, Avanzado y Criterios, «¡Buscar!» en segundo plano y cancelable, resultados en la
/// tabla de la F10 (página = 6 columnas × <c>EncounterDb.ResultsGridRowCount</c> filas, como la cuadrícula de PKHeX) y, al tocar uno, su descripción con «Colocar en la partida» y «Guardar como archivo».
/// Los ajustes <c>EncounterDb</c> se leen en el momento (nunca una copia) y su grupo se abre desde aquí, como la pestaña «Ajustes» del formulario.
/// </summary>
public sealed partial class EncuentrosViewModel : HerramientaViewModel
{
    public const int ColumnasCuadricula = 6;   // GridWidth de SAV_Encounters
    private readonly FilteredGameDataSource _fuentes;
    private readonly IReadOnlyList<PokemonDeReferencia> _referencias;
    private ushort _especie;
    private readonly ushort[] _movimientos = new ushort[4];
    private int _version;
    private int _huevo = -1, _variocolor = -1;
    private int _referencia;
    private int _prefijo, _propiedad;

    /// <summary>
    /// La base de entrenadores: los Pokémon de la carpeta de entrenadores (<c>LocalResources.TrainerPath</c>), cargada en segundo plano al abrir, como PKHeX. Mientras carga (o sin carpeta) está vacía y cada
    /// encuentro usa el entrenador de la partida.
    /// </summary>
    public TrainerDatabase Entrenadores { get; private set; } = new();

    /// <summary>La carga de la base de entrenadores (las pruebas la esperan).</summary>
    public Task CargaEntrenadores { get; }

    public EncuentrosViewModel(ContextoHerramienta contexto, Action volver) : base(contexto, volver)
    {
        var sesion = Sesion ?? throw new InvalidOperationException(DefHerramienta.SinPartida);
        var sav = sesion.Guardado;
        Pokekeep.Mobile.Ui.Ajustes.Pkhex.TextosNucleo.Preparar();
        _fuentes = new FilteredGameDataSource(sav, new GameDataSource(sesion.Textos));
        _referencias = AccionesResultado.Referencias(sesion);
        var abrir = contexto.AbrirHoja;
        var carpetaEntrenadores = contexto.Ajustes.Actual.LocalResources.GetTrainerPath();
        var contextoPartida = sav.Context;
        CargaEntrenadores = Task.Run(() => { Entrenadores = ModeloEncuentros.CargarEntrenadores(carpetaEntrenadores, contextoPartida); });

        // ---- General (PopulateComboBoxes): especies SIN filtrar (Source.SpeciesDataSource), movimientos filtrados, versiones sin la última (la de «todas»).
        Especie = new FilaCriterioOpcion("especie", IdiomaInterfaz.T("Cajas.Especie"), IdiomaInterfaz.T("Encuentros.EspecieBuscas"), AccionesResultado.ConCualquiera(_fuentes.Source.SpeciesDataSource, 0, especies: true), () => _especie, v => { _especie = (ushort)v; Revisar(); }, abrir);
        var movs = AccionesResultado.ConCualquiera(_fuentes.Moves, 0);
        Movimientos = [.. Enumerable.Range(0, 4).Select(i => new FilaCriterioOpcion($"mov{i + 1}", IdiomaInterfaz.F("BaseDatosPkm.Mov2", i + 1), IdiomaInterfaz.T("Encuentros.MovimientoDebePoderConocer"), movs, () => _movimientos[i], v => { _movimientos[i] = (ushort)v; Revisar(); }, abrir))];
        var versiones = _fuentes.Source.VersionDataSource.ToList();
        if (versiones.Count > 0) versiones.RemoveAt(versiones.Count - 1);
        Version = new FilaCriterioOpcion("version", IdiomaInterfaz.T("BaseDatosPkm.VersionEO"), IdiomaInterfaz.T("Encuentros.JuegoOrigenEncuentro"), AccionesResultado.ConCualquiera(versiones, 0), () => _version, v => { _version = v; Revisar(); }, abrir);
        OpcionSelector[] tres = [new(-1, IdiomaInterfaz.T("BaseDatosPkm.Indiferente")), new(1, IdiomaInterfaz.T("Comun.Si")), new(0, IdiomaInterfaz.T("Comun.No"))];
        Huevo = new FilaCriterioOpcion("huevo", IdiomaInterfaz.T("Informe.Columna.IsEgg"), IdiomaInterfaz.T("BaseDatosPkm.SoloHuevosNingunHuevo"), tres, () => _huevo, v => { _huevo = v; Revisar(); }, abrir);
        Variocolor = new FilaCriterioOpcion("variocolor", IdiomaInterfaz.T("Catalogo.Variocolor"), IdiomaInterfaz.T("BaseDatosPkm.SoloVariocolorNingunoIndiferente"), tres, () => _variocolor, v => { _variocolor = v; Revisar(); }, abrir);
        Tipos = [.. ModeloEncuentros.TodosLosTipos.Select(t => new TipoEncuentro(t, Revisar))];

        // ---- Avanzado: instrucciones y su constructor (solo prefijos de filtro).
        Prefijo = new FilaCriterioOpcion("prefijo", IdiomaInterfaz.T("BaseDatosPkm.Condicion"), IdiomaInterfaz.T("BaseDatosPkm.ComparaPropiedad"), [.. InstruccionesFiltro.Prefijos.Select((p, i) => new OpcionSelector(i, $"{p}  {InstruccionesFiltro.Significado(p)}"))], () => _prefijo, v => _prefijo = v, abrir);
        Propiedad = new FilaCriterioOpcion("propiedad", IdiomaInterfaz.T("MenuCaja.Propiedad"), IdiomaInterfaz.T("Encuentros.PropiedadEncuentroCompara"), [.. InstruccionesFiltro.Propiedades.Select((p, i) => new OpcionSelector(i, p))], () => _propiedad, v => _propiedad = v, abrir);

        // ---- Criterios: Pokémon de referencia y el PropertyGrid de EncounterCriteria.
        Referencia = new FilaCriterioOpcion("referencia", IdiomaInterfaz.T("Encuentros.PokemonReferencia"), IdiomaInterfaz.T("Encuentros.HacePestanasEditorPKHeX"), [.. _referencias.Select(r => new OpcionSelector(r.Id, r.Texto))],
            () => _referencia, v => _referencia = v, abrir);
        Criterios = new CriteriosEditables(sesion.Textos, abrir);
        Criterios.Establecer(ModeloEncuentros.CriteriosDe(PokemonReferencia));   // UpdateCriteriaPropertyGrid(BuildCriteriaFromTabs()) al abrir

        Tabla = new TablaResultadosViewModel(
        [
            new ColumnaTabla("especie", IdiomaInterfaz.T("Cajas.Especie"), o => NombreEspecie((IEncounterInfo)o), o => (int)((IEncounterInfo)o).Species),
            new ColumnaTabla("tipo", IdiomaInterfaz.T("MenuCaja.Tipo2"), o => ModeloEncuentros.Tipo((IEncounterInfo)o)),
            new ColumnaTabla("juego", IdiomaInterfaz.T("Form.Juego"), o => NombreVersion(((IEncounterInfo)o).Version)),
            new ColumnaTabla("nivel", IdiomaInterfaz.T("Cajas.Nivel"), o => Nivel((IEncounterInfo)o), o => (int)((IEncounterInfo)o).LevelMin),
            new ColumnaTabla("generacion", IdiomaInterfaz.T("Libreria.Generacion"), o => ((IEncounterInfo)o).Generation.ToString(CultureInfo.InvariantCulture), o => (int)((IEncounterInfo)o).Generation, Visible: false),
            new ColumnaTabla("variocolor", IdiomaInterfaz.T("Catalogo.Variocolor"), o => ((IEncounterInfo)o).IsShiny ? IdiomaInterfaz.T("Comun.Si") : IdiomaInterfaz.T("Comun.No"), Visible: false),
            new ColumnaTabla("huevo", IdiomaInterfaz.T("Informe.Columna.IsEgg"), o => ((IEncounterInfo)o).IsEgg ? IdiomaInterfaz.T("Comun.Si") : IdiomaInterfaz.T("Comun.No"), Visible: false),
        ], o => VerDetalle((IEncounterInfo)o)) { Pagina = () => ColumnasCuadricula * Ajustes.EncounterDb.ResultsGridRowCount };
        Tabla.SolicitarOrden = h => Contexto.AbrirHoja(h);
        FilasAjustes = [.. AjustesPkhexCatalogo.HojasVisibles(GrupoAjustes).Select(n => FabricaFilasPkhex.Crear(n, contexto.Ajustes, abrir))];
        _alCambiarAjustes = AlCambiarAjustes;
        contexto.Ajustes.Cambiado += _alCambiarAjustes;
        Revisar();
    }

    /// <summary>Código ESTABLE del grupo (el de RegistroAjustesPkhex.OrdenGrupos, nunca traducido): es la clave de HojasVisibles; traducido, en inglés la sección salía vacía (auditoría uso10, A3). Para mostrarlo, RegistroAjustesPkhex.NombreGrupo.</summary>
    public const string GrupoAjustes = "Base de datos de encuentros";
    private readonly Action _alCambiarAjustes;

    public override string Titulo => IdiomaInterfaz.T("Ajustes.Pkhex.Grupo.18");
    public string Subtitulo => IdiomaInterfaz.T("Encuentros.BuscaEncuentrosLegalesEspecie");

    public FilaCriterioOpcion Especie { get; }
    public IReadOnlyList<FilaCriterioOpcion> Movimientos { get; }
    public FilaCriterioOpcion Version { get; }
    public FilaCriterioOpcion Huevo { get; }
    public FilaCriterioOpcion Variocolor { get; }
    public IReadOnlyList<TipoEncuentro> Tipos { get; }
    public FilaCriterioOpcion Prefijo { get; }
    public FilaCriterioOpcion Propiedad { get; }
    public FilaCriterioOpcion Referencia { get; }
    public CriteriosEditables Criterios { get; }
    public TablaResultadosViewModel Tabla { get; }

    [ObservableProperty] private string _instrucciones = string.Empty;
    [ObservableProperty] private string _valorInstruccion = string.Empty;
    [ObservableProperty] private string _pestana = "general";
    [ObservableProperty] private string _motivoBusqueda = string.Empty;
    [ObservableProperty] private string _resultado = string.Empty;
    [ObservableProperty] private bool _hayResultados;
    public bool PuedeBuscar => MotivoBusqueda.Length == 0;
    public bool HayMotivoBusqueda => MotivoBusqueda.Length > 0;
    partial void OnMotivoBusquedaChanged(string value) { OnPropertyChanged(nameof(PuedeBuscar)); OnPropertyChanged(nameof(HayMotivoBusqueda)); }
    partial void OnInstruccionesChanged(string value) => Revisar();
    public bool EnGeneral => Pestana == "general";
    public bool EnAvanzado => Pestana == "avanzado";
    public bool EnCriterios => Pestana == "criterios";
    public bool EnAjustes => Pestana == "ajustes";
    partial void OnPestanaChanged(string value) { OnPropertyChanged(nameof(EnGeneral)); OnPropertyChanged(nameof(EnAvanzado)); OnPropertyChanged(nameof(EnCriterios)); OnPropertyChanged(nameof(EnAjustes)); }
    [RelayCommand] private void VerPestana(string? p) { if (p is "general" or "avanzado" or "criterios" or "ajustes") { Pestana = p; Revisar(); } }

    public PKM PokemonReferencia => _referencias.First(r => r.Id == _referencia).Pokemon;
    public IReadOnlyList<IEncounterInfo> Resultados { get; private set; } = [];

    /// <summary>Los filtros tal y como están en pantalla.</summary>
    public FiltrosEncuentros Filtros => new(_especie, [.. _movimientos.Where(m => m != 0)], (GameVersion)_version, _huevo < 0 ? null : _huevo == 1, _variocolor < 0 ? null : _variocolor == 1, Instrucciones,
        [.. Tipos.Where(t => t.Marcado).Select(t => t.Tipo)]);

    /// <summary><c>CheckIsSearchAllowed</c>: se revisa con cada cambio de filtro (y al volver de los ajustes, por si cambió «Búsqueda vacía sin resultados»).</summary>
    public void Revisar() { if (Sesion is { } s && Tipos is not null) MotivoBusqueda = ModeloEncuentros.MotivoNoBuscar(s.Guardado, Filtros, Ajustes.EncounterDb) ?? string.Empty; }

    [RelayCommand]
    public async Task Buscar()
    {
        Revisar();
        if (Sesion is null || !PuedeBuscar) return;
        var sav = Sesion.Guardado;
        var filtros = Filtros;
        var ajustes = Ajustes.EncounterDb;
        var r = await EjecutarAsync(IdiomaInterfaz.T("Encuentros.BuscandoEncuentros"), (cancelar, avance) => ModeloEncuentros.Buscar(sav, filtros, ajustes, cancelar, avance));
        if (r is null) return;
        MostrarResultados(r);
    }

    /// <summary>Deja los resultados en pantalla (<c>SetResults</c>); el gate visual lo usa para el estado «con resultados» sin esperar a un trabajo.</summary>
    public void MostrarResultados(IReadOnlyList<IEncounterInfo> r)
    {
        Resultados = r;
        Tabla.Establecer(r);
        HayResultados = r.Count > 0;
        Resultado = r.Count == 0 ? MessageStrings.MsgDBSearchNone : string.Format(CultureInfo.InvariantCulture, IdiomaInterfaz.T("BaseDatosPkm.Cuenta"), r.Count);
        Estado = Resultado;
    }

    /// <summary>Busca en este hilo: solo para el gate visual y las pruebas.</summary>
    public void BuscarSincrono() { if (Sesion is not null) MostrarResultados(ModeloEncuentros.Buscar(Sesion.Guardado, Filtros, Ajustes.EncounterDb)); }

    /// <summary>Elige la especie por número (pruebas y gate visual).</summary>
    public void ElegirEspecie(ushort especie) => Especie.Elegir(especie);

    /// <summary><c>ResetFilters</c>: todo a «Cualquiera», instrucciones vacías, casillas indiferentes y todos los tipos marcados.</summary>
    [RelayCommand]
    private void ReiniciarFiltros()
    {
        Especie.Elegir(0); foreach (var m in Movimientos) m.Elegir(0); Version.Elegir(0); Huevo.Elegir(-1); Variocolor.Elegir(-1);
        Instrucciones = string.Empty;
        foreach (var t in Tipos) t.Marcado = true;
        Revisar();
    }

    [RelayCommand]
    private void AnadirInstruccion()
    {
        var props = InstruccionesFiltro.Propiedades;
        if ((uint)_propiedad >= props.Count) { Contexto.Informar(MessageStrings.MsgBEPropertyInvalid, "aviso", "CircleAlert"); return; }
        Instrucciones = InstruccionesFiltro.Anadir(Instrucciones, InstruccionesFiltro.Linea(InstruccionesFiltro.Prefijos[_prefijo], props[_propiedad], ValorInstruccion));
        ValorInstruccion = string.Empty;
    }

    [RelayCommand] private void CriteriosReiniciar() => Criterios.Establecer(EncounterCriteria.Unrestricted);
    [RelayCommand] private void CriteriosDelEditor() => Criterios.Establecer(ModeloEncuentros.CriteriosDe(PokemonReferencia));

    /// <summary>La pestaña «Ajustes» del formulario (<c>PropertyGrid</c> de <c>Main.Settings.EncounterDb</c>): las MISMAS filas que «Más &gt; Ajustes &gt; Base de datos de encuentros», sobre el mismo almacén.</summary>
    public IReadOnlyList<FilaPkhex> FilasAjustes { get; }

    private void AlCambiarAjustes() { Revisar(); OnPropertyChanged(nameof(TextoPagina)); }
    public string TextoPagina => IdiomaInterfaz.F("BaseDatosPkm.MuestranResultadosPaginaCada", Tabla.TamanoPaginaActual, ColumnasCuadricula);

    public override void Dispose() { Contexto.Ajustes.Cambiado -= _alCambiarAjustes; base.Dispose(); }

    // ---- Resultado elegido ----
    [ObservableProperty] private string _ultimoVisto = string.Empty;

    /// <summary>Genera el Pokémon de un encuentro con los criterios y ajustes vigentes (lo que hace Ctrl+clic en PKHeX).</summary>
    public EncuentroGenerado Generar(IEncounterInfo enc)
    {
        if (Ocupado) return new(null, EsperaBusqueda, Criterios.Valor);
        var sav = Sesion!.Guardado;
        return ModeloEncuentros.Generar(enc, sav, Entrenadores, Ajustes.EncounterDb, PokemonReferencia, Criterios.Valor);
    }

    /// <summary>
    /// La búsqueda tiene el cerrojo de legalidad (<c>LegalityGate</c>) durante TODO su recorrido (la lista de tipos del Core es global) y <c>Lineas</c>/<c>Generar</c> lo piden también. Desde el hilo de interfaz
    /// eso sería esperar la búsqueda entera (un ANR): mientras trabaja, ver, colocar y guardar un resultado están desactivados y avisan.
    /// </summary>
    private static string EsperaBusqueda => IdiomaInterfaz.T("Encuentros.EsperaTermineBusquedaCancelala");

    public void VerDetalle(IEncounterInfo enc)
    {
        if (Ocupado) { Contexto.Informar(EsperaBusqueda, "aviso", "CircleAlert"); return; }
        var lineas = ModeloEncuentros.Lineas(enc);
        Contexto.AbrirHoja(new MenuHojaViewModel(NombreEspecie(enc),
        [
            new SeccionHoja(null,
            [
                new EntradaHoja(IdiomaInterfaz.T("BaseDatosPkm.ColocarPartida"), IdiomaInterfaz.T("Encuentros.GeneraCriteriosPonePrimera"), "Plus", () => ColocarEncuentro(enc)),
                new EntradaHoja(IdiomaInterfaz.T("BaseDatosPkm.GuardarArchivo"), IdiomaInterfaz.T("Encuentros.GeneraGuardaArchivoPokemon"), "Download", () => _ = GuardarEncuentroAsync(enc)),
            ]),
        ], string.Join("\n", lineas)));
    }

    public InformeOperacionResultado ColocarEncuentro(IEncounterInfo enc)
    {
        if (Ocupado) { Contexto.Informar(EsperaBusqueda, "aviso", "CircleAlert"); return new(false, EsperaBusqueda); }
        var g = Generar(enc);
        if (g.Pokemon is null) { Contexto.Informar(g.Error ?? IdiomaInterfaz.T("Encuentros.PudoGenerar"), "error", "CircleAlert"); return new(false, g.Error ?? string.Empty); }
        var inf = AccionesResultado.Colocar(Sesion!, g.Pokemon);
        UltimoVisto = NombreEspecie(enc);
        Contexto.Informar(inf.Mensaje, inf.Correcto ? "ok" : "error", inf.Correcto ? "CircleCheck" : "CircleAlert");
        return new(inf.Correcto, inf.Mensaje);
    }

    private async Task GuardarEncuentroAsync(IEncounterInfo enc)
    {
        if (Ocupado) { Contexto.Informar(EsperaBusqueda, "aviso", "CircleAlert"); return; }
        var g = Generar(enc);
        if (g.Pokemon is null) { Contexto.Informar(g.Error ?? IdiomaInterfaz.T("Encuentros.PudoGenerar"), "error", "CircleAlert"); return; }
        var texto = await AccionesResultado.GuardarArchivoAsync(Contexto.Documentos(), g.Pokemon);
        if (texto.Length > 0) Contexto.Informar(texto, "ok", "CircleCheck");
    }

    // ---- Textos ----
    private string NombreEspecie(IEncounterInfo e)
    {
        var t = Sesion!.Textos;
        var n = (uint)e.Species < t.Species.Count ? t.Species[e.Species] : e.Species.ToString(CultureInfo.InvariantCulture);
        if (e.Form == 0) return n;
        var formas = FormConverter.GetFormList(e.Species, t.Types, t.forms, GameInfo.GenderSymbolUnicode, e.Context);
        return e.Form < formas.Length && !string.IsNullOrEmpty(formas[e.Form]) ? $"{n} ({formas[e.Form]})" : n;
    }

    private string NombreVersion(GameVersion v)
    {
        foreach (var c in _fuentes.Source.VersionDataSource) if (c.Value == (int)v) return c.Text;
        return v.ToString();
    }

    private static string Nivel(IEncounterInfo e) => e.LevelMin == e.LevelMax ? e.LevelMin.ToString(CultureInfo.InvariantCulture) : $"{e.LevelMin}-{e.LevelMax}";
}

/// <summary>Lo que devolvió una acción sobre un resultado (para las pruebas).</summary>
public sealed record InformeOperacionResultado(bool Correcto, string Mensaje);
