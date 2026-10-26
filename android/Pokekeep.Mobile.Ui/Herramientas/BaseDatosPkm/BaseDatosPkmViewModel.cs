// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;
using PKHeX.Core.Searching;
using Pokekeep.Mobile.Ui.Herramientas.BaseDatos;
using Pokekeep.Mobile.Ui.Herramientas.Encuentros;
using Pokekeep.Mobile.Ui.ViewModels;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Herramientas.BaseDatosPkm;

/// <summary>
/// «Más &gt; Herramientas &gt; Base de datos de PKM» (<c>SAV_Database</c>, PK-SUB-007 a PK-SUB-009, PK-MENU-018, PK-IO-041, ajustes <c>EntityDb</c>): pestañas General (los filtros de <c>EntitySearchControl</c>), Avanzado
/// (instrucciones de filtro), Opciones (el menú «Ajustes de búsqueda» de PKHeX: dónde buscar, legales / ilegales, solo clones) y Ajustes (el <c>PropertyGrid</c> de <c>EntityDb</c>); «Buscar» en segundo plano y cancelable;
/// resultados en la tabla de la F10 (página = 6 columnas × <c>EntityDb.ResultsGridRowCount</c> filas, como la cuadrícula de PKHeX) y, al tocar uno, su hoja con «Colocar en la partida» (<c>ClickView</c>), «Guardar como
/// archivo», «Eliminar» (<c>ClickDelete</c>) y «Añadir a la base de datos» (<c>ClickSet</c>). La sección «Resultados» es el menú «Herramientas» del formulario: exportar los resultados a una carpeta (<c>Menu_Export</c>),
/// importarlos a las cajas (<c>Menu_Import</c>) y eliminar los clones (<c>Menu_DeleteClones</c>). Los ajustes <c>EntityDb</c> se leen en el momento (nunca una copia).
/// <para>La carga (<c>LoadDatabase</c>) es lo caro (archivos de la carpeta, copias de seguridad y otras carpetas de SAF) y va en segundo plano con avance y cancelación, como PKHeX («Cargando…»); la disparan la vista al
/// mostrarse o las pruebas llamando a <see cref="Cargar"/>. Mientras trabaja, ver, colocar, eliminar, importar y guardar un resultado avisan y vuelven al instante: la búsqueda toma el cerrojo de legalidad por Pokémon y el
/// hilo de interfaz no debe esperarlo.</para>
/// <para>Diferencias con el escritorio, sin efecto en lo que se obtiene: «Abrir la carpeta de la base de datos» no existe (Android no tiene explorador que abrir; la carpeta es privada de la app y se llena con «Añadir»); la
/// pregunta «¿Verificar la legalidad del filtro de palabras?» es un interruptor en Opciones (regla 4: nada de diálogos de alternativas); el Pokémon que se añade se elige de la partida (PKHeX toma el de las pestañas del
/// editor); «Crear informe de datos» (<c>ReportGrid</c>) es del bloque 10-7.</para>
/// </summary>
public sealed partial class BaseDatosPkmViewModel : HerramientaViewModel
{
    public const int ColumnasCuadricula = 6;   // GridWidth de SAV_Database
    /// <summary>Código ESTABLE del grupo (el de RegistroAjustesPkhex.OrdenGrupos, nunca traducido): es la clave de HojasVisibles; traducido, en inglés la sección salía vacía (auditoría uso10, A3). Para mostrarlo, RegistroAjustesPkhex.NombreGrupo.</summary>
    public const string GrupoAjustes = "Base de datos PKM";
    private static string EsperaTrabajo => IdiomaInterfaz.T("BaseDatosPkm.EsperaTermineTrabajoCurso");

    private readonly FilteredGameDataSource _fuentes;
    private readonly Action _alCambiarAjustes;
    private readonly SaveFile _sav;
    private ushort _especie;
    private int _objeto = -1, _habilidad = -1, _naturaleza = (int)Nature.Random, _tipoPoderOculto = -1;
    private readonly ushort[] _movimientos = new ushort[4];
    private int _variocolor = -1, _huevo = -1;
    private int _nivel, _esv;
    private SearchComparison _comparadorNivel;
    private int _potencialIv, _entrenamientoEv;
    private int _version, _generacion;
    private SearchComparison _comparadorFormato;
    private int _formato;
    private int _prefijo, _propiedad;
    private int _cajaInicio;
    private int _referenciaAnadir;
    private readonly IReadOnlyList<PokemonDeReferencia> _referencias;

    public BaseDatosPkmViewModel(ContextoHerramienta contexto, Action volver) : base(contexto, volver)
    {
        var sesion = Sesion ?? throw new InvalidOperationException(DefHerramienta.SinPartida);
        _sav = sesion.Guardado;
        Pokekeep.Mobile.Ui.Ajustes.Pkhex.TextosNucleo.Preparar();
        _fuentes = new FilteredGameDataSource(_sav, new GameDataSource(sesion.Textos));
        var abrir = contexto.AbrirHoja;
        var inicial = FiltrosPkm.Iniciales(_sav);
        _comparadorFormato = inicial.ComparadorFormato;
        _formato = (int)inicial.Formato;
        _referencias = [.. AccionesResultado.Referencias(sesion).Skip(1)];   // sin el «Ninguno (en blanco)»: añadir un Pokémon vacío no tiene sentido
        _referenciaAnadir = _referencias.Count > 0 ? _referencias[0].Id : 0;

        // ---- General (PopulateComboBoxes). «Cualquiera» donde PKHeX pone MsgAny.
        Especie = new FilaCriterioOpcion("especie", IdiomaInterfaz.T("Cajas.Especie"), IdiomaInterfaz.T("BaseDatosPkm.EspeciePokemon"), AccionesResultado.ConCualquiera(_fuentes.Source.SpeciesDataSource, 0, especies: true), () => _especie, v => _especie = (ushort)v, abrir);
        Objeto = new FilaCriterioOpcion("objeto", IdiomaInterfaz.T("Cajas.ObjetoEquipado"), IdiomaInterfaz.T("BaseDatosPkm.ObjetoLleva"), AccionesResultado.ConCualquiera(_fuentes.Items, -1), () => _objeto, v => _objeto = v, abrir);
        Habilidad = new FilaCriterioOpcion("habilidad", IdiomaInterfaz.T("Informe.Columna.Ability"), IdiomaInterfaz.T("BaseDatosPkm.HabilidadPokemon"), AccionesResultado.ConCualquiera(_fuentes.Source.AbilityDataSource, -1), () => _habilidad, v => _habilidad = v, abrir);
        Naturaleza = new FilaCriterioOpcion("naturaleza", IdiomaInterfaz.T("Informe.Columna.Nature"), IdiomaInterfaz.T("BaseDatosPkm.NaturalezaPokemon"), AccionesResultado.ConCualquiera(_fuentes.Source.NatureDataSource, (int)Nature.Random), () => _naturaleza, v => _naturaleza = v, abrir);
        TipoPoderOculto = new FilaCriterioOpcion("poder-oculto", IdiomaInterfaz.T("Informe.Columna.HP_Type"), IdiomaInterfaz.T("BaseDatosPkm.TipoPoderOculto"), AccionesResultado.ConCualquiera(Util.GetCBList(_fuentes.Source.Strings.HiddenPowerTypes), -1), () => _tipoPoderOculto, v => _tipoPoderOculto = v, abrir);
        var movs = AccionesResultado.ConCualquiera(_fuentes.Moves, 0);   // moves.RemoveAt(0): fuera «(Ninguno)»
        Movimientos = [.. Enumerable.Range(0, 4).Select(i => new FilaCriterioOpcion($"mov{i + 1}", IdiomaInterfaz.F("BaseDatosPkm.Mov2", i + 1), IdiomaInterfaz.T("BaseDatosPkm.MovimientoDebeConocer"), movs, () => _movimientos[i], v => _movimientos[i] = (ushort)v, abrir))];
        Apodo = string.Empty;
        OpcionSelector[] niveles = [new((int)SearchComparison.None, AccionesResultado.Cualquiera), new((int)SearchComparison.Equals, IdiomaInterfaz.T("BaseDatosPkm.Exactamente")), new((int)SearchComparison.GreaterThanEquals, IdiomaInterfaz.T("BaseDatosPkm.Minimo")), new((int)SearchComparison.LessThanEquals, IdiomaInterfaz.T("BaseDatosPkm.Maximo"))];
        ComparadorNivel = new FilaCriterioOpcion("comparador-nivel", IdiomaInterfaz.T("Cajas.Nivel"), IdiomaInterfaz.T("BaseDatosPkm.ComparaNivelNumeroDebajo"), niveles, () => (int)_comparadorNivel, v => CambiarComparadorNivel((SearchComparison)v), abrir);
        Nivel = new FilaCriterioNumero("nivel", IdiomaInterfaz.T("BaseDatosPkm.NivelComparar"), IdiomaInterfaz.T("BaseDatosPkm.FiltrarNivel"), 0, 100, () => _nivel, v => _nivel = v, IdiomaInterfaz.T("BaseDatosPkm.Filtrar"));
        PotencialIv = new FilaCriterioOpcion("iv", IdiomaInterfaz.T("Cajas.PotencialIV"), IdiomaInterfaz.T("BaseDatosPkm.SumaSeisIV"), [new(0, AccionesResultado.Cualquiera), new(1, "<= 90"), new(2, "91-120"), new(3, "121-150"), new(4, "151-179"), new(5, "180+"), new(6, "== 186")], () => _potencialIv, v => _potencialIv = v, abrir);
        EntrenamientoEv = new FilaCriterioOpcion("ev", IdiomaInterfaz.T("BaseDatosPkm.EntrenamientoEV"), IdiomaInterfaz.T("BaseDatosPkm.SumaEV"), [new(0, AccionesResultado.Cualquiera), new(1, IdiomaInterfaz.T("Cajas.Ninguno")), new(2, IdiomaInterfaz.T("Cajas.Algunos")), new(3, IdiomaInterfaz.T("Cajas.Mitad")), new(4, IdiomaInterfaz.T("BaseDatosPkm.Todos"))], () => _entrenamientoEv, v => _entrenamientoEv = v, abrir);
        OpcionSelector[] tres = [new(-1, IdiomaInterfaz.T("BaseDatosPkm.Indiferente")), new(1, IdiomaInterfaz.T("Comun.Si")), new(0, IdiomaInterfaz.T("Comun.No"))];
        Variocolor = new FilaCriterioOpcion("variocolor", IdiomaInterfaz.T("Catalogo.Variocolor"), IdiomaInterfaz.T("BaseDatosPkm.SoloVariocolorNingunoIndiferente"), tres, () => _variocolor, v => _variocolor = v, abrir);
        Huevo = new FilaCriterioOpcion("huevo", IdiomaInterfaz.T("Informe.Columna.IsEgg"), IdiomaInterfaz.T("BaseDatosPkm.SoloHuevosNingunHuevo"), tres, () => _huevo, v => { _huevo = v; OnPropertyChanged(nameof(EsvVisible)); }, abrir);
        Esv = new FilaCriterioNumero("esv", IdiomaInterfaz.T("BaseDatosPkm.ESVHuevo"), IdiomaInterfaz.T("BaseDatosPkm.ValorVariocolorHuevo"), 0, 4095, () => _esv, v => _esv = v);
        var versiones = _fuentes.Source.VersionDataSource.ToList();
        if (versiones.Count > 0) versiones.RemoveAt(versiones.Count - 1);
        Version = new FilaCriterioOpcion("version", IdiomaInterfaz.T("BaseDatosPkm.VersionEO"), IdiomaInterfaz.T("BaseDatosPkm.JuegoOrigenPokemon"), AccionesResultado.ConCualquiera(versiones, 0), () => _version, v => { _version = v; if (v != 0) _generacion = 0; Generacion?.Avisar(); }, abrir);
        Generacion = new FilaCriterioOpcion("generacion", IdiomaInterfaz.T("Libreria.Generacion"), IdiomaInterfaz.T("BaseDatosPkm.GeneracionOrigino"), [new(0, AccionesResultado.Cualquiera), .. Enumerable.Range(1, 9).Select(g => new OpcionSelector(g, IdiomaInterfaz.F("Libreria.NumeroGeneracion", g)))],
            () => _generacion, v => { _generacion = v; if (v != 0) _version = 0; Version?.Avisar(); }, abrir);
        OpcionSelector[] comparadores = [new((int)SearchComparison.None, AccionesResultado.Cualquiera), new((int)SearchComparison.Equals, IdiomaInterfaz.T("BaseDatosPkm.SoloFormato")), new((int)SearchComparison.GreaterThanEquals, IdiomaInterfaz.T("BaseDatosPkm.FormatoAdelante")), new((int)SearchComparison.LessThanEquals, IdiomaInterfaz.T("BaseDatosPkm.HastaFormato"))];
        ComparadorFormato = new FilaCriterioOpcion("comparador-formato", IdiomaInterfaz.T("BaseDatosPkm.FormatoArchivo"), IdiomaInterfaz.T("BaseDatosPkm.ComparaFormatoPokemonPK3"), comparadores, () => (int)_comparadorFormato, v => CambiarComparadorFormato((SearchComparison)v), abrir);
        Formato = new FilaCriterioOpcion("formato", IdiomaInterfaz.T("BaseDatosPkm.Formato"), IdiomaInterfaz.T("BaseDatosPkm.FormatoCompara"), [new((int)EntityContext.None, AccionesResultado.Cualquiera), .. FormatosDisponibles()], () => _formato, v => _formato = v, abrir);

        // ---- Avanzado
        Prefijo = new FilaCriterioOpcion("prefijo", IdiomaInterfaz.T("BaseDatosPkm.Condicion"), IdiomaInterfaz.T("BaseDatosPkm.ComparaPropiedad"), [.. InstruccionesFiltro.Prefijos.Select((p, i) => new OpcionSelector(i, $"{p}  {InstruccionesFiltro.Significado(p)}"))], () => _prefijo, v => _prefijo = v, abrir);
        Propiedad = new FilaCriterioOpcion("propiedad", IdiomaInterfaz.T("MenuCaja.Propiedad"), IdiomaInterfaz.T("BaseDatosPkm.PropiedadPokemonCompara"), [.. InstruccionesFiltro.Propiedades.Select((p, i) => new OpcionSelector(i, p))], () => _propiedad, v => _propiedad = v, abrir);

        // ---- Resultados: dónde empieza «Importar» y qué Pokémon de la partida se añade a la base de datos.
        CajaInicio = new FilaCriterioOpcion("caja", IdiomaInterfaz.T("BaseDatosPkm.ImportarDesdeCaja"), IdiomaInterfaz.T("BaseDatosPkm.ResultadosColocanDesdeCaja"), [.. Enumerable.Range(0, _sav.HasBox ? _sav.BoxCount : 0).Select(b => new OpcionSelector(b, sesion.NombreCaja(b)))],
            () => _cajaInicio, v => _cajaInicio = v, abrir);
        ReferenciaAnadir = new FilaCriterioOpcion("anadir", IdiomaInterfaz.T("BaseDatosPkm.PokemonPartida"), IdiomaInterfaz.T("BaseDatosPkm.PokemonGuardaArchivoBase"), [.. _referencias.Select(r => new OpcionSelector(r.Id, r.Texto))], () => _referenciaAnadir, v => _referenciaAnadir = v, abrir);

        Tabla = new TablaResultadosViewModel(
        [
            new ColumnaTabla("especie", IdiomaInterfaz.T("Cajas.Especie"), o => NombreEspecie(((SlotCache)o).Entity), o => (int)((SlotCache)o).Entity.Species),
            new ColumnaTabla("nivel", IdiomaInterfaz.T("Cajas.Nivel"), o => ((SlotCache)o).Entity.CurrentLevel.ToString(CultureInfo.InvariantCulture), o => (int)((SlotCache)o).Entity.CurrentLevel),
            new ColumnaTabla("origen", IdiomaInterfaz.T("Libreria.Origen"), o => ModeloBaseDatosPkm.Origen((SlotCache)o, _sav, Carpeta, sesion.NombreCaja)),
            new ColumnaTabla("apodo", IdiomaInterfaz.T("BaseDatosPkm.Apodo"), o => ((SlotCache)o).Entity.Nickname, Visible: false),
            new ColumnaTabla("generacion", IdiomaInterfaz.T("Libreria.Generacion"), o => ((SlotCache)o).Entity.Generation.ToString(CultureInfo.InvariantCulture), o => (int)((SlotCache)o).Entity.Generation, Visible: false),
            new ColumnaTabla("variocolor", IdiomaInterfaz.T("Catalogo.Variocolor"), o => ((SlotCache)o).Entity.IsShiny ? IdiomaInterfaz.T("Comun.Si") : IdiomaInterfaz.T("Comun.No"), Visible: false),
            new ColumnaTabla("huevo", IdiomaInterfaz.T("Informe.Columna.IsEgg"), o => ((SlotCache)o).Entity.IsEgg ? IdiomaInterfaz.T("Comun.Si") : IdiomaInterfaz.T("Comun.No"), Visible: false),
        ], o => VerDetalle((SlotCache)o)) { Pagina = () => ColumnasCuadricula * Ajustes.EntityDb.ResultsGridRowCount };
        Tabla.SolicitarOrden = h => Contexto.AbrirHoja(h);
        FilasAjustes = [.. AjustesPkhexCatalogo.HojasVisibles(GrupoAjustes).Select(n => FabricaFilasPkhex.Crear(n, contexto.Ajustes, abrir))];
        _alCambiarAjustes = () => OnPropertyChanged(nameof(TextoPagina));
        contexto.Ajustes.Cambiado += _alCambiarAjustes;
        Nivel.Sincronizar(); Esv.Sincronizar();
        PropertyChanged += (_, e) => { if (e.PropertyName == nameof(Ocupado)) OnPropertyChanged(nameof(PuedeBuscar)); };
        Estado = IdiomaInterfaz.T("BaseDatosPkm.CargandoBaseDatos");
    }

    public override string Titulo => IdiomaInterfaz.T("BaseDatosPkm.BaseDatosPKM");
    public string Subtitulo => IdiomaInterfaz.T("BaseDatosPkm.BuscaEntrePokemonCajas");

    /// <summary>La carpeta de la base de datos VIGENTE (se lee de los ajustes cada vez).</summary>
    public string Carpeta => ModeloBaseDatosPkm.Carpeta(Ajustes);

    /// <summary>Todo lo cargado (<c>RawDB</c>); vacío hasta que termina <see cref="Cargar"/>.</summary>
    public List<SlotCache> Todos { get; private set; } = [];
    public IReadOnlyList<SlotCache> Resultados { get; private set; } = [];
    [ObservableProperty] private bool _cargada;
    [ObservableProperty] private string _avisoCarga = string.Empty;
    public bool HayAvisoCarga => AvisoCarga.Length > 0;
    partial void OnAvisoCargaChanged(string value) => OnPropertyChanged(nameof(HayAvisoCarga));

    public FilaCriterioOpcion Especie { get; }
    public FilaCriterioOpcion Objeto { get; }
    public FilaCriterioOpcion Habilidad { get; }
    public FilaCriterioOpcion Naturaleza { get; }
    public FilaCriterioOpcion TipoPoderOculto { get; }
    public IReadOnlyList<FilaCriterioOpcion> Movimientos { get; }
    public FilaCriterioOpcion ComparadorNivel { get; }
    public FilaCriterioNumero Nivel { get; }
    public FilaCriterioOpcion PotencialIv { get; }
    public FilaCriterioOpcion EntrenamientoEv { get; }
    public FilaCriterioOpcion Variocolor { get; }
    public FilaCriterioOpcion Huevo { get; }
    public FilaCriterioNumero Esv { get; }
    public FilaCriterioOpcion Version { get; }
    public FilaCriterioOpcion Generacion { get; }
    public FilaCriterioOpcion ComparadorFormato { get; }
    public FilaCriterioOpcion Formato { get; }
    public FilaCriterioOpcion Prefijo { get; }
    public FilaCriterioOpcion Propiedad { get; }
    public FilaCriterioOpcion CajaInicio { get; }
    public FilaCriterioOpcion ReferenciaAnadir { get; }
    public TablaResultadosViewModel Tabla { get; }
    public IReadOnlyList<FilaPkhex> FilasAjustes { get; }

    [ObservableProperty] private string _apodo = string.Empty;
    [ObservableProperty] private string _instrucciones = string.Empty;
    [ObservableProperty] private string _valorInstruccion = string.Empty;
    [ObservableProperty] private string _pestana = "general";
    [ObservableProperty] private string _resultado = string.Empty;
    [ObservableProperty] private bool _hayResultados;
    [ObservableProperty] private string _ultimoVisto = string.Empty;
    public bool HayUltimoVisto => UltimoVisto.Length > 0;
    partial void OnUltimoVistoChanged(string value) => OnPropertyChanged(nameof(HayUltimoVisto));

    // ---- Opciones de búsqueda (el menú «Ajustes de búsqueda»): todo activo al abrir, como los tres CheckOnClick del Designer.
    [ObservableProperty] private bool _buscarEnCajas = true;
    [ObservableProperty] private bool _buscarEnBaseDeDatos = true;
    [ObservableProperty] private bool _buscarEnCopias = true;
    [ObservableProperty] private bool _mostrarLegales = true;
    [ObservableProperty] private bool _mostrarIlegales = true;
    [ObservableProperty] private bool _soloClones;
    [ObservableProperty] private bool _clonesPorPid;
    [ObservableProperty] private bool _verificarFiltroDePalabras = true;

    /// <summary>La fila de ESV solo se ve con «Huevo: Sí» (<c>ToggleESV</c>).</summary>
    public bool EsvVisible => _huevo == 1;
    /// <summary>El formato solo se ve con un comparador distinto de «Cualquiera» (<c>ChangeFormatFilter</c>).</summary>
    public bool FormatoVisible => _comparadorFormato != SearchComparison.None;
    public bool TieneCajas => _sav.HasBox;
    public bool EnGeneral => Pestana == "general";
    public bool EnAvanzado => Pestana == "avanzado";
    public bool EnOpciones => Pestana == "opciones";
    public bool EnAjustes => Pestana == "ajustes";
    partial void OnPestanaChanged(string value) { OnPropertyChanged(nameof(EnGeneral)); OnPropertyChanged(nameof(EnAvanzado)); OnPropertyChanged(nameof(EnOpciones)); OnPropertyChanged(nameof(EnAjustes)); }
    [RelayCommand] private void VerPestana(string? p) { if (p is "general" or "avanzado" or "opciones" or "ajustes") Pestana = p; }

    public bool PuedeBuscar => Cargada && !Ocupado;
    partial void OnCargadaChanged(bool value) => OnPropertyChanged(nameof(PuedeBuscar));

    public string TextoPagina => IdiomaInterfaz.F("BaseDatosPkm.MuestranResultadosPaginaCada", Tabla.TamanoPaginaActual, ColumnasCuadricula);
    public string TextoCarpeta => IdiomaInterfaz.T("BaseDatosPkm.CarpetaBaseDatosPrivada");

    [RelayCommand]
    public void AbrirInforme()
    {
        if (!Libre()) return;
        if (Resultados.Count == 0) { Contexto.Informar(IdiomaInterfaz.T("BaseDatosPkm.ResultadosCrearInforme"), "aviso", "CircleAlert"); return; }
        Contexto.Abrir(new Informe.InformeViewModel(Contexto, () => Contexto.Abrir(this), Resultados));
    }

    /// <summary><c>CB_Format</c>: los contextos «Gen…» válidos, con el nombre del Core.</summary>
    private static IEnumerable<OpcionSelector> FormatosDisponibles()
    {
        foreach (var contexto in Enum.GetValues<EntityContext>())
        {
            if (contexto is EntityContext.None or EntityContext.SplitInvalid or EntityContext.MaxInvalid) continue;
            if (!contexto.IsValid || !contexto.ToString().StartsWith("Gen", StringComparison.Ordinal)) continue;
            yield return new OpcionSelector((int)contexto, contexto.ToString());
        }
    }

    /// <summary><c>ChangeLevel</c>: con «Cualquiera» el número se borra.</summary>
    private void CambiarComparadorNivel(SearchComparison c)
    {
        _comparadorNivel = c;
        if (c == SearchComparison.None) { _nivel = 0; Nivel?.Sincronizar(); }
    }

    /// <summary><c>ChangeFormatFilter</c>: con «Cualquiera» el formato se oculta y vuelve a «Cualquiera»; con otro comparador, el de la partida.</summary>
    private void CambiarComparadorFormato(SearchComparison c)
    {
        _comparadorFormato = c;
        _formato = c == SearchComparison.None ? (int)EntityContext.None : (int)_sav.Context;
        Formato?.Avisar();
        OnPropertyChanged(nameof(FormatoVisible));
    }

    /// <summary>Los filtros tal y como están en pantalla (<c>CreateSearchSettings</c>).</summary>
    public FiltrosPkm Filtros => new()
    {
        Especie = _especie, Objeto = _objeto, Habilidad = _habilidad, Naturaleza = (Nature)_naturaleza, TipoPoderOculto = _tipoPoderOculto, Apodo = Apodo,
        ComparadorNivel = _comparadorNivel, Nivel = _nivel > 0 ? (byte)_nivel : null, PotencialIv = _potencialIv, EntrenamientoEv = _entrenamientoEv,
        Movimientos = [.. _movimientos.Where(m => m != 0)],
        Variocolor = _variocolor < 0 ? null : _variocolor == 1, Huevo = _huevo < 0 ? null : _huevo == 1, Esv = _huevo == 1 && EsvActivo ? _esv : null,
        Version = (GameVersion)_version, Generacion = (byte)_generacion, ComparadorFormato = _comparadorFormato, Formato = (EntityContext)_formato, Instrucciones = Instrucciones,
    };

    /// <summary>¿Se filtra por ESV? PKHeX lo hace si el campo tiene un número; aquí es un interruptor porque el campo siempre tiene un valor.</summary>
    [ObservableProperty] private bool _esvActivo;

    /// <summary>Los menús de «Ajustes de búsqueda» y las casillas de legalidad y clones.</summary>
    public OpcionesBusqueda Opciones => new()
    {
        Cajas = BuscarEnCajas, BaseDeDatos = BuscarEnBaseDeDatos, Copias = BuscarEnCopias, Legales = MostrarLegales, Ilegales = MostrarIlegales, SoloClones = SoloClones,
        MetodoClones = ClonesPorPid ? CloneDetectionMethod.HashPID : CloneDetectionMethod.HashDetails, VerificarFiltroDePalabras = VerificarFiltroDePalabras,
    };

    // ---------------------------------------------------------------------------------------------------------------- carga
    private Task? _carga;

    /// <summary>La carga en curso o terminada (las pruebas la esperan; la vista la dispara al mostrarse).</summary>
    public Task Carga => _carga ?? Task.CompletedTask;

    /// <summary>Dispara la carga una sola vez (la vista, al mostrarse).</summary>
    public void IniciarCarga() { if (_carga is null) _carga = Cargar(); }

    /// <summary><c>LoadDatabase</c> en segundo plano: archivos de la carpeta, partidas externas (según <c>SearchBackups</c>, <c>SearchExtraSaves</c> y <c>SearchExtraSavesDeep</c>) y las cajas de la partida.</summary>
    [RelayCommand]
    public async Task Cargar()
    {
        if (Sesion is null) return;
        var a = Ajustes;
        var carpeta = Carpeta;
        var sav = _sav;
        var fuentes = new FuentesExternas(Contexto.Ajustes, Contexto.Documentos(), Contexto.Carpetas());
        var r = await EjecutarAsync(IdiomaInterfaz.T("BaseDatosPkm.CargandoBaseDatos"), (cancelar, avance) =>
        {
            // Las carpetas de SAF se leen con tareas asíncronas que no vuelven al hilo de interfaz (ConfigureAwait(false)): se esperan aquí, ya en el hilo de trabajo.
            var externas = fuentes.CargarAsync(cancelar).GetAwaiter().GetResult();
            return (Lista: ModeloBaseDatosPkm.Cargar(sav, carpeta, externas.Partidas, a.EntityDb, cancelar), externas.Avisos);
        });
        if (r.Lista is null) return;   // cancelado o fallado: EjecutarAsync ya dejó el motivo en Estado y avisó
        Todos = r.Lista;
        Cargada = true;
        AvisoCarga = string.Join(" ", r.Avisos);
        if (!Directory.Exists(carpeta))
        {
            // MainMenuDatabase: PKHeX avisa (MsgDatabase + MsgDatabaseAdvice) y no abre la herramienta. En el móvil la carpeta es privada y se crea con «Añadir»: se avisa y se abre igual.
            var aviso = IdiomaInterfaz.F("BaseDatosPkm.CrearaAnadirPrimerPokemon", MessageStrings.MsgDatabase);
            AvisoCarga = (AvisoCarga + " " + aviso).Trim();
            Contexto.Informar(aviso, "aviso", "CircleAlert");
        }
        MostrarResultados(Todos);   // SetResults(RawDB): al abrir se ven todos
    }

    /// <summary>Carga en este hilo: solo para el gate visual y las pruebas de pantalla.</summary>
    public void CargarSincrono()
    {
        var fuentes = new FuentesExternas(Contexto.Ajustes, Contexto.Documentos(), Contexto.Carpetas());
        var externas = fuentes.CargarAsync().GetAwaiter().GetResult();
        Todos = ModeloBaseDatosPkm.Cargar(_sav, Carpeta, externas.Partidas, Ajustes.EntityDb);
        Cargada = true;
        _carga ??= Task.CompletedTask;
        MostrarResultados(Todos);
    }

    /// <summary>Vuelve a leer todo (la carpeta o las copias cambiaron por fuera).</summary>
    [RelayCommand]
    private async Task Recargar()
    {
        if (Ocupado) { Contexto.Informar(EsperaTrabajo, "aviso", "CircleAlert"); return; }
        Cargada = false;
        await Cargar();
    }

    // ---------------------------------------------------------------------------------------------------------------- búsqueda
    /// <summary><c>B_Search_Click</c>: la búsqueda va en segundo plano y se puede cancelar (con «solo legales» analiza cada Pokémon).</summary>
    [RelayCommand]
    public async Task Buscar()
    {
        if (!PuedeBuscar) return;
        var filtros = Filtros; var opciones = Opciones; var todos = Todos; var sav = _sav; var carpeta = Carpeta;
        var r = await EjecutarAsync(IdiomaInterfaz.T("BaseDatosPkm.Buscando"), (cancelar, avance) => ModeloBaseDatosPkm.Buscar(todos, filtros, opciones, sav, carpeta, cancelar));
        if (r is null) return;
        if (r.Count == 0)
        {
            // MsgDBSearchFail si no queda ningún sitio donde buscar; si no, MsgDBSearchNone.
            var sinOrigen = !opciones.Cajas && !opciones.BaseDeDatos && !opciones.Copias;
            Contexto.Informar(sinOrigen ? MessageStrings.MsgDBSearchFail : MessageStrings.MsgDBSearchNone, "aviso", "CircleAlert");
        }
        MostrarResultados(r);
    }

    /// <summary>Busca en este hilo: solo para el gate visual y las pruebas.</summary>
    public void BuscarSincrono() => MostrarResultados(ModeloBaseDatosPkm.Buscar(Todos, Filtros, Opciones, _sav, Carpeta));

    /// <summary><c>SetResults</c>: la tabla, la cuenta («Cuenta: N», <c>L_Count</c>) y, sin nada, <c>MsgDBSearchNone</c>.</summary>
    public void MostrarResultados(IReadOnlyList<SlotCache> r)
    {
        Resultados = r;
        Tabla.Establecer(r);
        HayResultados = r.Count > 0;
        Resultado = r.Count == 0 ? MessageStrings.MsgDBSearchNone : string.Format(CultureInfo.InvariantCulture, IdiomaInterfaz.T("BaseDatosPkm.Cuenta"), r.Count);
        Estado = Resultado;
    }

    /// <summary>Elige la especie por número (pruebas y gate visual).</summary>
    public void ElegirEspecie(ushort especie) => Especie.Elegir(especie);

    /// <summary><c>ResetFilters</c> (los de la pestaña General y las instrucciones; las opciones de búsqueda y el formato no se tocan, como en PKHeX).</summary>
    [RelayCommand]
    private void ReiniciarFiltros()
    {
        Variocolor.Elegir(-1); Huevo.Elegir(-1); EsvActivo = false; _esv = 0; Esv.Sincronizar();
        Objeto.Elegir(-1); Especie.Elegir(0); Habilidad.Elegir(-1); Naturaleza.Elegir((int)Nature.Random); TipoPoderOculto.Elegir(-1);
        Apodo = string.Empty; ComparadorNivel.Elegir((int)SearchComparison.None); PotencialIv.Elegir(0); EntrenamientoEv.Elegir(0);
        foreach (var m in Movimientos) m.Elegir(0);
        Version.Elegir(0); Generacion.Elegir(0);
        Instrucciones = string.Empty;
    }

    [RelayCommand]
    private void AnadirInstruccion()
    {
        var props = InstruccionesFiltro.Propiedades;
        if ((uint)_propiedad >= props.Count) { Contexto.Informar(MessageStrings.MsgBEPropertyInvalid, "aviso", "CircleAlert"); return; }
        Instrucciones = InstruccionesFiltro.Anadir(Instrucciones, InstruccionesFiltro.Linea(InstruccionesFiltro.Prefijos[_prefijo], props[_propiedad], ValorInstruccion));
        ValorInstruccion = string.Empty;
    }

    public override void Dispose() { Contexto.Ajustes.Cambiado -= _alCambiarAjustes; base.Dispose(); }

    // ---------------------------------------------------------------------------------------------------------------- un resultado
    private bool Libre()
    {
        if (!Ocupado) return true;
        Contexto.Informar(EsperaTrabajo, "aviso", "CircleAlert");
        return false;
    }

    public void VerDetalle(SlotCache e)
    {
        if (!Libre()) return;
        var pk = e.Entity;
        var origen = ModeloBaseDatosPkm.Origen(e, _sav, Carpeta, Sesion!.NombreCaja);
        var motivoEliminar = ModeloBaseDatosPkm.PuedeEliminar(e, _sav) ? null : MessageStrings.MsgDBDeleteFailBackup;
        var yaEnBd = ModeloBaseDatosPkm.EsDeLaBaseDeDatos(e, Carpeta);
        var entradas = new List<EntradaHoja>
        {
            new(IdiomaInterfaz.T("BaseDatosPkm.ColocarPartida"), IdiomaInterfaz.T("BaseDatosPkm.ConvierteFormatoPartidaPone"), "Plus", () => ColocarResultado(e)),
            new(IdiomaInterfaz.T("BaseDatosPkm.GuardarArchivo"), IdiomaInterfaz.T("BaseDatosPkm.PokemonArchivoFormato"), "Download", () => _ = GuardarResultadoAsync(e)),
        };
        if (!yaEnBd) entradas.Add(new(IdiomaInterfaz.T("BaseDatosPkm.AnadirBaseDatos"), IdiomaInterfaz.T("BaseDatosPkm.GuardaCopiaArchivoCarpeta"), "Copy", () => AnadirALaBase(pk)));
        entradas.Add(new(IdiomaInterfaz.T("BaseDatosPkm.Eliminar"), yaEnBd ? IdiomaInterfaz.T("BaseDatosPkm.BorraArchivoCarpetaBase") : IdiomaInterfaz.T("BaseDatosPkm.QuitaCajaPartidaPuedes"), "Trash2", () => EliminarResultado(e), Motivo: motivoEliminar));
        Contexto.AbrirHoja(new MenuHojaViewModel(NombreEspecie(pk), [new SeccionHoja(null, entradas)], origen));
    }

    /// <summary><c>ClickView</c> + colocar: convierte al formato de la partida y la adapta (el Core da el motivo si no se puede) y lo pone en la primera casilla libre (un Deshacer).</summary>
    public InformeOperacionResultado ColocarResultado(SlotCache e)
    {
        if (!Libre()) return new(false, EsperaTrabajo);
        var (pk, error) = ModeloBaseDatosPkm.PrepararParaPartida(e, _sav);
        if (pk is null) { Contexto.Informar(error ?? IdiomaInterfaz.T("BaseDatosPkm.PudoConvertir"), "error", "CircleAlert"); return new(false, error ?? string.Empty); }
        var inf = AccionesResultado.Colocar(Sesion!, pk);
        UltimoVisto = string.Format(CultureInfo.InvariantCulture, IdiomaInterfaz.T("BaseDatosPkm.UltimoVisto"), ModeloBaseDatosPkm.Origen(e, _sav, Carpeta, Sesion!.NombreCaja));   // L_Viewed (SlotCache.Identify)
        Contexto.Informar(inf.Mensaje, inf.Correcto ? "ok" : "error", inf.Correcto ? "CircleCheck" : "CircleAlert");
        return new(inf.Correcto, inf.Mensaje);
    }

    private async Task GuardarResultadoAsync(SlotCache e)
    {
        if (!Libre()) return;
        var texto = await AccionesResultado.GuardarArchivoAsync(Contexto.Documentos(), e.Entity);
        if (texto.Length > 0) Contexto.Informar(texto, "ok", "CircleCheck");
    }

    /// <summary><c>ClickDelete</c>: el archivo de la base de datos o el Pokémon de la caja; en una copia, el motivo del Core. Quita la entrada de la base y de los resultados y refresca la cuenta.</summary>
    public InformeOperacionResultado EliminarResultado(SlotCache e)
    {
        if (!Libre()) return new(false, EsperaTrabajo);
        var r = ModeloBaseDatosPkm.Eliminar(e, Sesion!);
        if (r.Correcto)
        {
            Todos.Remove(e);
            MostrarResultados([.. Resultados.Where(x => !ReferenceEquals(x, e))]);
        }
        Contexto.Informar(r.Mensaje, r.Correcto ? "ok" : "error", r.Correcto ? "CircleCheck" : "CircleAlert");
        return new(r.Correcto, r.Mensaje);
    }

    // ---------------------------------------------------------------------------------------------------------------- añadir a la base de datos
    /// <summary><c>ClickSet</c>: guarda el Pokémon en la carpeta de la base de datos y lo suma a los resultados.</summary>
    public InformeOperacionResultado AnadirALaBase(PKM pk)
    {
        if (!Libre()) return new(false, EsperaTrabajo);
        var (r, entrada) = ModeloBaseDatosPkm.Anadir(Carpeta, pk.Clone());
        if (r.Correcto && entrada is not null)
        {
            Todos.Add(entrada);
            MostrarResultados([.. Resultados, entrada]);
        }
        Contexto.Informar(r.Mensaje, r.Correcto ? "ok" : "aviso", r.Correcto ? "CircleCheck" : "CircleAlert");
        return new(r.Correcto, r.Mensaje);
    }

    [RelayCommand]
    private void AnadirReferencia()
    {
        var r = _referencias.FirstOrDefault(x => x.Id == _referenciaAnadir);
        if (r is null) { Contexto.Informar(IdiomaInterfaz.T("BaseDatosPkm.NingunPokemonPartidaAnadir"), "aviso", "CircleAlert"); return; }
        AnadirALaBase(r.Pokemon);
    }

    public bool HayReferencias => _referencias.Count > 0;

    /// <summary>Añade un archivo de Pokémon (de cualquier formato) a la carpeta de la base de datos.</summary>
    [RelayCommand]
    private async Task AnadirArchivo()
    {
        var docs = Contexto.Documentos();
        if (docs is null) { Contexto.Informar(IdiomaInterfaz.T("Form.SinSelector"), "aviso", "CircleAlert"); return; }
        try
        {
            var doc = await docs.ElegirParaAbrirAsync(CancellationToken.None);
            if (doc is null) return;
            AnadirDatos(await docs.LeerAsync(doc, CancellationToken.None), doc.Nombre);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException) { Contexto.Informar(IdiomaInterfaz.T("Editor.PodidoLeerArchivo") + ex.Message, "error", "CircleAlert"); }
    }

    /// <summary>Lo que se hace con los bytes de un archivo (separado del selector para probarlo).</summary>
    public InformeOperacionResultado AnadirDatos(byte[] datos, string nombre)
    {
        var (pk, error) = ModeloBaseDatosPkm.LeerArchivo(datos, nombre, _sav);
        if (pk is null) { Contexto.Informar(error ?? MessageStrings.MsgFileLoadFail, "error", "CircleAlert"); return new(false, error ?? string.Empty); }
        return AnadirALaBase(pk);
    }

    // ---------------------------------------------------------------------------------------------------------------- resultados: exportar, importar y clones
    /// <summary>«Exportar resultados a carpeta» (<c>Menu_Export_Click</c>): sin resultados, <c>MsgDBCreateReportFail</c>; si no, la pregunta del formulario y la carpeta.</summary>
    [RelayCommand]
    private void ExportarResultados()
    {
        if (!Libre()) return;
        if (Resultados.Count == 0) { Contexto.Informar(MessageStrings.MsgDBCreateReportFail, "aviso", "CircleAlert"); return; }
        Contexto.AbrirHoja(new ConfirmarHojaViewModel(IdiomaInterfaz.T("BaseDatosPkm.ExportarResultadosCarpeta"), [MessageStrings.MsgDBExportResultsPrompt, IdiomaInterfaz.F("BaseDatosPkm.GuardaraArchivoPokemonCarpeta", Resultados.Count)],
            IdiomaInterfaz.T("BaseDatosPkm.ElegirCarpeta"), () => _ = ExportarACarpetaAsync()));
    }

    /// <summary>Escribe los resultados en una carpeta (sin la pregunta: las pruebas la llaman directamente). Devuelve cuántos archivos escribió.</summary>
    public async Task<int> ExportarACarpetaAsync()
    {
        var carpetas = Contexto.Carpetas(); var docs = Contexto.Documentos();
        if (carpetas is null || docs is null) { Contexto.Informar(IdiomaInterfaz.T("ExportarCajas.ExportarCarpetaDisponibleAqui"), "aviso", "CircleAlert"); return 0; }
        try
        {
            var carpeta = await carpetas.ElegirCarpetaAsync(CancellationToken.None);
            if (carpeta is null) return 0;
            var n = 0;
            foreach (var (nombre, datos) in ModeloBaseDatosPkm.ArchivosExportables(Resultados))
            {
                var doc = await carpetas.CrearEnCarpetaAsync(carpeta, nombre, CancellationToken.None);
                await docs.EscribirAsync(doc, datos, CancellationToken.None);
                n++;
            }
            Contexto.Informar(IdiomaInterfaz.F("Cajas.ExportadosArchivos", n, carpeta.Nombre), "ok", "CircleCheck");
            return n;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException) { Contexto.Informar(IdiomaInterfaz.T("Editor.PodidoExportar") + ex.Message, "error", "CircleAlert"); return 0; }
    }

    /// <summary>«Importar resultados a partida» (<c>Menu_Import_Click</c>): la pregunta de <c>GetBulkImportSettings</c> (vaciar las cajas siguientes o no; cerrar la hoja = cancelar).</summary>
    [RelayCommand]
    private void ImportarResultados()
    {
        if (!Libre()) return;
        if (!TieneCajas) { Contexto.Informar(MessageStrings.MsgSaveBoxFailNone, "aviso", "CircleAlert"); return; }
        if (Resultados.Count == 0) { Contexto.Informar(MessageStrings.MsgSaveBoxImportNoFiles, "aviso", "CircleAlert"); return; }
        var caja = Sesion!.NombreCaja(_cajaInicio);
        Contexto.AbrirHoja(new MenuHojaViewModel(IdiomaInterfaz.F("BaseDatosPkm.ImportarPokemon", Resultados.Count),
        [
            new SeccionHoja(MessageStrings.MsgSaveBoxImportClear,
            [
                new EntradaHoja(IdiomaInterfaz.F("BaseDatosPkm.VaciarDesdeEImportar", caja), IdiomaInterfaz.T("BaseDatosPkm.VaciaCajaSiguientesColoca"), "Trash2", () => Importar(vaciar: true)),
                new EntradaHoja(IdiomaInterfaz.T("BaseDatosPkm.ImportarVaciar"), IdiomaInterfaz.T("BaseDatosPkm.ColocaSoloCasillasLibres"), "Plus", () => Importar(vaciar: false)),
            ]),
        ], IdiomaInterfaz.F("BaseDatosPkm.PuedesDeshacerloDeshacer", MessageStrings.MsgSaveBoxImportClearNo)));
    }

    public InformeOperacionResultado Importar(bool vaciar)
    {
        if (!Libre()) return new(false, EsperaTrabajo);
        var inf = ModeloBaseDatosPkm.ImportarACajas(Sesion!, Resultados, _cajaInicio, vaciar);
        Contexto.Informar(inf.Mensaje, inf.Correcto ? "ok" : "aviso", inf.Correcto ? "CircleCheck" : "CircleAlert");
        return new(inf.Correcto, inf.Mensaje);
    }

    /// <summary>«Eliminar clones» (<c>Menu_DeleteClones_Click</c>): con la advertencia del formulario; lo de la carpeta se borra para siempre.</summary>
    [RelayCommand]
    private void EliminarClones()
    {
        if (!Libre()) return;
        Contexto.AbrirHoja(new ConfirmarHojaViewModel(IdiomaInterfaz.T("BaseDatosPkm.EliminarClones"), [MessageStrings.MsgDBDeleteCloneWarning, MessageStrings.MsgDBDeleteCloneAdvice], IdiomaInterfaz.T("BaseDatosPkm.EliminarClones"), () => _ = EliminarClonesAsync()));
    }

    /// <summary>Los archivos repetidos se borran en segundo plano; después se vacían los clones de las cajas (una entrada del historial). Devuelve (archivos, cajas).</summary>
    public async Task<(int Archivos, int Cajas)> EliminarClonesAsync()
    {
        if (!Libre()) return (0, 0);
        var todos = Todos.ToList(); var carpeta = Carpeta;
        var r = await EjecutarAsync(IdiomaInterfaz.T("BaseDatosPkm.BuscandoClones"), (cancelar, avance) => { var (a, e) = ModeloBaseDatosPkm.EliminarClonesDeArchivos(todos, carpeta, cancelar); return new ClonesDeArchivos(a, e); });
        if (r is null) return (0, 0);   // cancelado o fallado: no se toca la partida
        var cajas = ModeloBaseDatosPkm.EliminarClonesDeCajas(Sesion!);
        var borrados = r.Archivos + cajas;
        if (r.Error is not null) Contexto.Informar(r.Error, "error", "CircleAlert");
        if (borrados == 0) { Contexto.Informar(MessageStrings.MsgDBDeleteCloneNone, "aviso", "CircleAlert"); return (0, 0); }
        Contexto.Informar(string.Format(CultureInfo.InvariantCulture, MessageStrings.MsgFileDeleteCount, borrados), "ok", "CircleCheck");
        // Como PKHeX (que cierra la ventana), lo mostrado ya no vale: se vuelve a leer todo.
        Cargada = false;
        await Cargar();
        return (r.Archivos, cajas);
    }

    private sealed record ClonesDeArchivos(int Archivos, string? Error);

    // ---------------------------------------------------------------------------------------------------------------- textos
    private string NombreEspecie(PKM pk)
    {
        var t = Sesion!.Textos;
        var n = (uint)pk.Species < t.Species.Count ? t.Species[pk.Species] : pk.Species.ToString(CultureInfo.InvariantCulture);
        if (pk.Form == 0) return n;
        var formas = FormConverter.GetFormList(pk.Species, t.Types, t.forms, GameInfo.GenderSymbolUnicode, pk.Context);
        return pk.Form < formas.Length && !string.IsNullOrEmpty(formas[pk.Form]) ? $"{n} ({formas[pk.Form]})" : n;
    }
}
