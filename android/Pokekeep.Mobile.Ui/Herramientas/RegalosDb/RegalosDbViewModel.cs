// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Cajas;
using Pokekeep.Mobile.Ui.Herramientas.BaseDatos;
using Pokekeep.Mobile.Ui.Herramientas.Encuentros;
using Pokekeep.Mobile.Ui.ViewModels;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Herramientas.RegalosDb;

/// <summary>
/// «Más &gt; Herramientas &gt; Base de datos de regalos misteriosos» (<c>SAV_MysteryGiftDB</c>, PK-SUB-018 a PK-SUB-020, PK-MENU-020): pestañas General, Avanzado y Ajustes; «¡Buscar!»; resultados en la tabla de la F10
/// (página = 6 columnas × <c>MysteryDb.ResultsGridRowCount</c> filas, como la cuadrícula de PKHeX) y, al tocar uno, su descripción con «Colocar en la partida» (<c>ClickView</c>), «Guardar el Pokémon como archivo»
/// (<c>ClickSavePK</c>) y «Guardar la tarjeta» (<c>ClickSaveMG</c>). El menú «Herramientas» del formulario es la sección «Resultados»: exportarlos a una carpeta, importarlos a las cajas y, además, abrir un archivo de
/// regalo de cualquier formato (PK-IO-016, PK-IO-025). Los ajustes <c>MysteryDb</c> se leen en el momento (nunca una copia).
/// <para>Diferencias con el escritorio, sin efecto en el resultado: la base se carga al abrir (PKHeX la carga en otro hilo con «Loading…»; aquí tarda lo mismo que abrir cualquier pantalla); la caja donde se
/// empieza a importar se elige (PKHeX usa la caja que está a la vista); «Abrir carpeta de la base de datos» no existe (Android no tiene explorador de archivos que abrir).</para>
/// </summary>
public sealed partial class RegalosDbViewModel : HerramientaViewModel
{
    public const int ColumnasCuadricula = 6;   // GridWidth de SAV_MysteryGiftDB
    /// <summary>Código ESTABLE del grupo (el de RegistroAjustesPkhex.OrdenGrupos, nunca traducido): es la clave de HojasVisibles; traducido, en inglés la sección salía vacía (auditoría uso10, A3). Para mostrarlo, RegistroAjustesPkhex.NombreGrupo.</summary>
    public const string GrupoAjustes = "Base de datos de regalos";
    private readonly FilteredGameDataSource _fuentes;
    private ComparadorFormato _comparador = ComparadorFormato.MenorOIgual;
    private byte _formato;
    private int _especie = -1, _objeto = -1;
    private readonly int[] _movimientos = [-1, -1, -1, -1];
    private int _variocolor = -1, _huevo = -1;
    private int _prefijo, _propiedad;
    private int _cajaInicio;
    private readonly Action _alCambiarAjustes;

    public RegalosDbViewModel(ContextoHerramienta contexto, Action volver) : base(contexto, volver)
    {
        var sesion = Sesion ?? throw new InvalidOperationException(DefHerramienta.SinPartida);
        var sav = sesion.Guardado;
        Pokekeep.Mobile.Ui.Ajustes.Pkhex.TextosNucleo.Preparar();
        _fuentes = new FilteredGameDataSource(sav, new GameDataSource(sesion.Textos));
        var abrir = contexto.AbrirHoja;

        // LoadDatabase con el ajuste vigente al abrir (como PKHeX: cambiar «Ocultar regalos no disponibles» vale para la próxima vez que se abra).
        Todos = ModeloRegalosDb.Cargar(sav, Ajustes.MysteryDb);
        _formato = ModeloRegalosDb.FormatoInicial(sav);

        // ---- General (PopulateComboBoxes): especies de la partida con algún regalo, objetos y movimientos de la partida; «Cualquiera» = -1.
        OpcionSelector[] comparadores =
        [
            new((int)ComparadorFormato.Cualquiera, AccionesResultado.Cualquiera),
            new((int)ComparadorFormato.MayorOIgual, IdiomaInterfaz.T("BaseDatosPkm.FormatoAdelante")),
            new((int)ComparadorFormato.Igual, IdiomaInterfaz.T("BaseDatosPkm.SoloFormato")),
            new((int)ComparadorFormato.MenorOIgual, IdiomaInterfaz.T("BaseDatosPkm.HastaFormato")),
        ];
        Comparador = new FilaCriterioOpcion("comparador", IdiomaInterfaz.T("BaseDatosPkm.Formato"), IdiomaInterfaz.T("RegalosDb.ComparaGeneracionTarjetaFormato"), comparadores, () => (int)_comparador, v => CambiarComparador((ComparadorFormato)v), abrir);
        Formato = new FilaCriterioOpcion("formato", IdiomaInterfaz.T("RegalosDb.FormatoTarjeta"), IdiomaInterfaz.T("RegalosDb.GeneracionCompara"), [.. ModeloRegalosDb.Formatos.Select(f => new OpcionSelector(f.Formato, f.Texto))], () => _formato, v => _formato = (byte)v, abrir);
        Especie = new FilaCriterioOpcion("especie", IdiomaInterfaz.T("Cajas.Especie"), IdiomaInterfaz.T("RegalosDb.EspecieDaTarjeta"), AccionesResultado.ConCualquiera(ModeloRegalosDb.Especies(_fuentes.Species, Todos), -1, especies: true), () => _especie, v => _especie = v, abrir);
        Objeto = new FilaCriterioOpcion("objeto", IdiomaInterfaz.T("Cajas.ObjetoEquipado"), IdiomaInterfaz.T("RegalosDb.ObjetoLlevaPokemonTarjeta"), AccionesResultado.ConCualquiera(_fuentes.Items, -1), () => _objeto, v => _objeto = v, abrir);
        var movs = AccionesResultado.ConCualquiera(_fuentes.Moves.Skip(1), -1);   // moves.RemoveAt(0): fuera «(Ninguno)»
        Movimientos = [.. Enumerable.Range(0, 4).Select(i => new FilaCriterioOpcion($"mov{i + 1}", IdiomaInterfaz.F("BaseDatosPkm.Mov2", i + 1), IdiomaInterfaz.T("RegalosDb.MovimientoDebeTraerTarjeta"), movs, () => _movimientos[i], v => _movimientos[i] = v, abrir))];
        OpcionSelector[] tres = [new(-1, IdiomaInterfaz.T("BaseDatosPkm.Indiferente")), new(1, IdiomaInterfaz.T("Comun.Si")), new(0, IdiomaInterfaz.T("Comun.No"))];
        Variocolor = new FilaCriterioOpcion("variocolor", IdiomaInterfaz.T("Catalogo.Variocolor"), IdiomaInterfaz.T("BaseDatosPkm.SoloVariocolorNingunoIndiferente"), tres, () => _variocolor, v => _variocolor = v, abrir);
        Huevo = new FilaCriterioOpcion("huevo", IdiomaInterfaz.T("Informe.Columna.IsEgg"), IdiomaInterfaz.T("BaseDatosPkm.SoloHuevosNingunHuevo"), tres, () => _huevo, v => _huevo = v, abrir);

        // ---- Avanzado: instrucciones y su constructor (solo prefijos de filtro).
        Prefijo = new FilaCriterioOpcion("prefijo", IdiomaInterfaz.T("BaseDatosPkm.Condicion"), IdiomaInterfaz.T("BaseDatosPkm.ComparaPropiedad"), [.. InstruccionesFiltro.Prefijos.Select((p, i) => new OpcionSelector(i, $"{p}  {InstruccionesFiltro.Significado(p)}"))], () => _prefijo, v => _prefijo = v, abrir);
        Propiedad = new FilaCriterioOpcion("propiedad", IdiomaInterfaz.T("MenuCaja.Propiedad"), IdiomaInterfaz.T("RegalosDb.PropiedadTarjetaCompara"), [.. InstruccionesFiltro.Propiedades.Select((p, i) => new OpcionSelector(i, p))], () => _propiedad, v => _propiedad = v, abrir);

        // ---- Resultados: la caja donde empieza «Importar resultados a partida».
        CajaInicio = new FilaCriterioOpcion("caja", IdiomaInterfaz.T("BaseDatosPkm.ImportarDesdeCaja"), IdiomaInterfaz.T("RegalosDb.RegalosColocanDesdeCaja"), [.. Enumerable.Range(0, sav.HasBox ? sav.BoxCount : 0).Select(b => new OpcionSelector(b, sesion.NombreCaja(b)))],
            () => _cajaInicio, v => _cajaInicio = v, abrir);

        Tabla = new TablaResultadosViewModel(
        [
            new ColumnaTabla("especie", IdiomaInterfaz.T("Cajas.Especie"), o => NombreEspecie((MysteryGift)o), o => (int)((MysteryGift)o).Species),
            new ColumnaTabla("tarjeta", IdiomaInterfaz.T("RegalosDb.Tarjeta"), o => ((MysteryGift)o).CardHeader),
            new ColumnaTabla("tipo", IdiomaInterfaz.T("MenuCaja.Tipo2"), o => ((MysteryGift)o).Type),
            new ColumnaTabla("generacion", IdiomaInterfaz.T("Libreria.Generacion"), o => ((MysteryGift)o).Generation.ToString(CultureInfo.InvariantCulture), o => (int)((MysteryGift)o).Generation),
            new ColumnaTabla("nivel", IdiomaInterfaz.T("Cajas.Nivel"), o => ((MysteryGift)o).Level.ToString(CultureInfo.InvariantCulture), o => (int)((MysteryGift)o).Level, Visible: false),
            new ColumnaTabla("variocolor", IdiomaInterfaz.T("Catalogo.Variocolor"), o => ((MysteryGift)o).IsShiny ? IdiomaInterfaz.T("Comun.Si") : IdiomaInterfaz.T("Comun.No"), Visible: false),
            new ColumnaTabla("huevo", IdiomaInterfaz.T("Informe.Columna.IsEgg"), o => ((MysteryGift)o).IsEgg ? IdiomaInterfaz.T("Comun.Si") : IdiomaInterfaz.T("Comun.No"), Visible: false),
        ], o => VerDetalle((MysteryGift)o)) { Pagina = () => ColumnasCuadricula * Ajustes.MysteryDb.ResultsGridRowCount };
        Tabla.SolicitarOrden = h => Contexto.AbrirHoja(h);
        FilasAjustes = [.. AjustesPkhexCatalogo.HojasVisibles(GrupoAjustes).Select(n => FabricaFilasPkhex.Crear(n, contexto.Ajustes, abrir))];
        _alCambiarAjustes = () => OnPropertyChanged(nameof(TextoPagina));
        contexto.Ajustes.Cambiado += _alCambiarAjustes;

        MostrarResultados(Todos);   // SetResults(RawDB): al abrir se ven todos
    }

    public override string Titulo => IdiomaInterfaz.T("CatalogoHerramientas.BaseDatosRegalosMisteriosos");
    public string Subtitulo => IdiomaInterfaz.T("RegalosDb.BuscaEntreRegalosEvento");

    /// <summary>La base cargada (<c>RawDB</c>).</summary>
    public IReadOnlyList<MysteryGift> Todos { get; }
    public IReadOnlyList<MysteryGift> Resultados { get; private set; } = [];

    public FilaCriterioOpcion Comparador { get; }
    public FilaCriterioOpcion Formato { get; }
    public FilaCriterioOpcion Especie { get; }
    public FilaCriterioOpcion Objeto { get; }
    public IReadOnlyList<FilaCriterioOpcion> Movimientos { get; }
    public FilaCriterioOpcion Variocolor { get; }
    public FilaCriterioOpcion Huevo { get; }
    public FilaCriterioOpcion Prefijo { get; }
    public FilaCriterioOpcion Propiedad { get; }
    public FilaCriterioOpcion CajaInicio { get; }
    public TablaResultadosViewModel Tabla { get; }
    public IReadOnlyList<FilaPkhex> FilasAjustes { get; }

    [ObservableProperty] private string _instrucciones = string.Empty;
    [ObservableProperty] private string _valorInstruccion = string.Empty;
    [ObservableProperty] private string _pestana = "general";
    [ObservableProperty] private string _resultado = string.Empty;
    [ObservableProperty] private bool _hayResultados;
    [ObservableProperty] private string _ultimoVisto = string.Empty;
    public bool HayUltimoVisto => UltimoVisto.Length > 0;
    partial void OnUltimoVistoChanged(string value) => OnPropertyChanged(nameof(HayUltimoVisto));

    /// <summary>El formato solo se ve con un comparador distinto de «Cualquiera» (<c>CB_Format.Visible</c>).</summary>
    public bool FormatoVisible => _comparador != ComparadorFormato.Cualquiera;
    public bool TieneCajas => Sesion?.Guardado.HasBox == true;

    public bool EnGeneral => Pestana == "general";
    public bool EnAvanzado => Pestana == "avanzado";
    public bool EnAjustes => Pestana == "ajustes";
    partial void OnPestanaChanged(string value) { OnPropertyChanged(nameof(EnGeneral)); OnPropertyChanged(nameof(EnAvanzado)); OnPropertyChanged(nameof(EnAjustes)); }
    [RelayCommand] private void VerPestana(string? p) { if (p is "general" or "avanzado" or "ajustes") Pestana = p; }

    /// <summary><c>ChangeFormatFilter</c>: con «Cualquiera» el formato se oculta y vuelve a «Cualquiera»; con otro comparador, el de la partida.</summary>
    private void CambiarComparador(ComparadorFormato c)
    {
        _comparador = c;
        _formato = c == ComparadorFormato.Cualquiera ? ModeloRegalosDb.FormatoCualquiera : ModeloRegalosDb.FormatoInicial(Sesion!.Guardado);
        Formato?.Avisar();
        OnPropertyChanged(nameof(FormatoVisible));
    }

    /// <summary>Los filtros tal y como están en pantalla.</summary>
    public FiltrosRegalos Filtros => new(_comparador, _formato, _especie, _objeto, [.. _movimientos.Where(m => m >= 0).Select(m => (ushort)m)], _variocolor < 0 ? null : _variocolor == 1, _huevo < 0 ? null : _huevo == 1, Instrucciones);

    /// <summary>«¡Buscar!» (<c>B_Search_Click</c>): la búsqueda es en memoria sobre la base ya cargada, tan rápida como en PKHeX, que tampoco la manda a otro hilo.</summary>
    [RelayCommand]
    public void Buscar()
    {
        // Las instrucciones son texto libre del usuario: el filtro del Core (StringInstruction/ScreenStrings/IsFilterMatch) puede lanzar con entradas raras y esto corre en el hilo de interfaz.
        try { MostrarResultados(ModeloRegalosDb.Buscar(Todos, Filtros)); }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Estado = IdiomaInterfaz.T("RegalosDb.PudoBuscarRevisaInstrucciones");
            Contexto.Informar(IdiomaInterfaz.F("RegalosDb.PudoBuscarEsasInstrucciones", ex.GetType().Name), "error", "CircleAlert");
        }
    }

    /// <summary><c>SetResults</c>: la tabla, la cuenta («Cuenta: N», <c>L_Count</c>) y, sin nada, <c>MsgDBSearchNone</c>.</summary>
    public void MostrarResultados(IReadOnlyList<MysteryGift> r)
    {
        Resultados = r;
        Tabla.Establecer(r);
        HayResultados = r.Count > 0;
        Resultado = r.Count == 0 ? MessageStrings.MsgDBSearchNone : string.Format(CultureInfo.InvariantCulture, IdiomaInterfaz.T("BaseDatosPkm.Cuenta"), r.Count);
        Estado = Resultado;
    }

    /// <summary>Elige la especie por número (pruebas y gate visual).</summary>
    public void ElegirEspecie(ushort especie) => Especie.Elegir(especie);

    /// <summary><c>ResetFilters</c>: variocolor y huevo indiferentes, objeto, especie y movimientos a «Cualquiera» e instrucciones vacías (el formato NO se toca, como en PKHeX).</summary>
    [RelayCommand]
    private void ReiniciarFiltros()
    {
        Variocolor.Elegir(-1); Huevo.Elegir(-1); Objeto.Elegir(-1); Especie.Elegir(-1);
        foreach (var m in Movimientos) m.Elegir(-1);
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

    public string TextoPagina => IdiomaInterfaz.F("BaseDatosPkm.MuestranResultadosPaginaCada", Tabla.TamanoPaginaActual, ColumnasCuadricula);

    public override void Dispose() { Contexto.Ajustes.Cambiado -= _alCambiarAjustes; base.Dispose(); }

    // ---- Un resultado ----
    public void VerDetalle(MysteryGift g)
    {
        var lineas = ModeloEncuentros.Lineas(g);
        var (_, sinTarjeta) = ModeloRegalosDb.Tarjeta(g);
        Contexto.AbrirHoja(new MenuHojaViewModel(NombreEspecie(g),
        [
            new SeccionHoja(null,
            [
                new EntradaHoja(IdiomaInterfaz.T("BaseDatosPkm.ColocarPartida"), IdiomaInterfaz.T("BaseDatosPkm.ConvierteFormatoPartidaPone"), "Plus", () => ColocarRegalo(g)),
                new EntradaHoja(IdiomaInterfaz.T("RegalosDb.GuardarPokemonArchivo"), IdiomaInterfaz.T("RegalosDb.PokemonTarjetaFormatoGeneracion"), "Download", () => _ = GuardarPokemonAsync(g)),
                new EntradaHoja(IdiomaInterfaz.T("RegalosDb.GuardarTarjeta"), IdiomaInterfaz.F("RegalosDb.ArchivoTarjeta", g.Extension), "FileDown", () => _ = GuardarTarjetaAsync(g), Motivo: sinTarjeta),
            ]),
        ], string.Join("\n", lineas)));
    }

    /// <summary>«Ver» + colocar: <c>ClickView</c> y la primera casilla libre (un Deshacer).</summary>
    public InformeOperacionResultado ColocarRegalo(MysteryGift g)
    {
        var r = ModeloRegalosDb.Generar(g, Sesion!.Guardado);
        if (r.Pokemon is null) { Contexto.Informar(r.Error ?? IdiomaInterfaz.T("Encuentros.PudoGenerar"), "error", "CircleAlert"); return new(false, r.Error ?? string.Empty); }
        var inf = AccionesResultado.Colocar(Sesion!, r.Pokemon);
        UltimoVisto = string.Format(CultureInfo.InvariantCulture, IdiomaInterfaz.T("BaseDatosPkm.UltimoVisto"), g.FileName);   // L_Viewed
        Contexto.Informar(inf.Mensaje, inf.Correcto ? "ok" : "error", inf.Correcto ? "CircleCheck" : "CircleAlert");
        return new(inf.Correcto, inf.Mensaje);
    }

    private async Task GuardarPokemonAsync(MysteryGift g)
    {
        var texto = await AccionesResultado.GuardarArchivoAsync(Contexto.Documentos(), ModeloRegalosDb.PokemonDeLaTarjeta(g, Sesion!.Guardado));
        if (texto.Length > 0) Contexto.Informar(texto, "ok", "CircleCheck");
    }

    private async Task GuardarTarjetaAsync(MysteryGift g)
    {
        var (tarjeta, motivo) = ModeloRegalosDb.Tarjeta(g);
        if (tarjeta is null) { Contexto.Informar(motivo!, "aviso", "CircleAlert"); return; }
        var docs = Contexto.Documentos();
        if (docs is null) { Contexto.Informar(IdiomaInterfaz.T("AccionesResultado.PuedenGuardarArchivosVentana"), "aviso", "CircleAlert"); return; }
        try
        {
            var destino = await docs.ElegirDestinoAsync(FormatosRegalo.NombreArchivo(tarjeta), CancellationToken.None);
            if (destino is null) return;
            await docs.EscribirAsync(destino, tarjeta.Write().ToArray(), CancellationToken.None);
            Contexto.Informar(IdiomaInterfaz.F("RegalosDb.Guardada", destino.Nombre), "ok", "CircleCheck");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException) { Contexto.Informar(IdiomaInterfaz.T("RegalosDb.PodidoGuardar") + ex.Message, "error", "CircleAlert"); }
    }

    // ---- Resultados: exportar, importar y abrir ----
    /// <summary>«Exportar resultados a carpeta» (<c>Menu_Export_Click</c>): sin resultados, <c>MsgDBCreateReportFail</c>; si no, la pregunta del formulario y la carpeta.</summary>
    [RelayCommand]
    private void ExportarResultados()
    {
        if (Resultados.Count == 0) { Contexto.Informar(MessageStrings.MsgDBCreateReportFail, "aviso", "CircleAlert"); return; }
        var n = ModeloRegalosDb.ArchivosExportables(Resultados).Count();
        Contexto.AbrirHoja(new ConfirmarHojaViewModel(IdiomaInterfaz.T("BaseDatosPkm.ExportarResultadosCarpeta"), [MessageStrings.MsgDBExportResultsPrompt, IdiomaInterfaz.F("RegalosDb.GuardaraArchivoTarjetaCarpeta", n)],
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
            foreach (var (nombre, datos) in ModeloRegalosDb.ArchivosExportables(Resultados))
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
        if (!TieneCajas) { Contexto.Informar(MessageStrings.MsgSaveBoxFailNone, "aviso", "CircleAlert"); return; }
        if (Resultados.Count == 0) { Contexto.Informar(MessageStrings.MsgSaveBoxImportNoFiles, "aviso", "CircleAlert"); return; }
        var caja = Sesion!.NombreCaja(_cajaInicio);
        Contexto.AbrirHoja(new MenuHojaViewModel(IdiomaInterfaz.F("RegalosDb.ImportarRegalos", Resultados.Count),
        [
            new SeccionHoja(MessageStrings.MsgSaveBoxImportClear,
            [
                new EntradaHoja(IdiomaInterfaz.F("BaseDatosPkm.VaciarDesdeEImportar", caja), IdiomaInterfaz.T("RegalosDb.VaciaCajaSiguientesColoca"), "Trash2", () => Importar(vaciar: true)),
                new EntradaHoja(IdiomaInterfaz.T("BaseDatosPkm.ImportarVaciar"), IdiomaInterfaz.T("BaseDatosPkm.ColocaSoloCasillasLibres"), "Plus", () => Importar(vaciar: false)),
            ]),
        ], IdiomaInterfaz.F("BaseDatosPkm.PuedesDeshacerloDeshacer", MessageStrings.MsgSaveBoxImportClearNo)));
    }

    public InformeOperacionResultado Importar(bool vaciar)
    {
        var inf = ModeloRegalosDb.ImportarACajas(Sesion!, Resultados, _cajaInicio, vaciar);
        Contexto.Informar(inf.Mensaje, inf.Correcto ? "ok" : "aviso", inf.Correcto ? "CircleCheck" : "CircleAlert");
        return new(inf.Correcto, inf.Mensaje);
    }

    /// <summary>Abrir un archivo de regalo de cualquier formato (<c>Main.OpenMysteryGift</c>): si tiene Pokémon, su descripción con las mismas acciones que un resultado.</summary>
    [RelayCommand]
    private async Task AbrirArchivo()
    {
        var docs = Contexto.Documentos();
        if (docs is null) { Contexto.Informar(IdiomaInterfaz.T("Form.SinSelector"), "aviso", "CircleAlert"); return; }
        try
        {
            var doc = await docs.ElegirParaAbrirAsync(CancellationToken.None);
            if (doc is null) return;
            AbrirDatos(await docs.LeerAsync(doc, CancellationToken.None), doc.Nombre);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException) { Contexto.Informar(IdiomaInterfaz.T("Editor.PodidoLeerArchivo") + ex.Message, "error", "CircleAlert"); }
    }

    /// <summary>Lo que se hace con los bytes de un archivo de regalo (separado del selector para probarlo).</summary>
    public RegaloGenerado AbrirDatos(byte[] datos, string nombre)
    {
        var (tarjeta, generado) = ModeloRegalosDb.Abrir(datos, nombre, Sesion!.Guardado);
        if (tarjeta is null || !generado.Correcto) { Contexto.Informar(generado.Error ?? IdiomaInterfaz.T("RegalosDb.PodidoLeerTarjeta"), "error", "CircleAlert"); return generado; }
        VerDetalle(tarjeta);
        return generado;
    }

    public string TextoFormatosArchivo => IdiomaInterfaz.F("RegalosDb.Admite", string.Join(", ", FormatosRegalo.Todas));

    // ---- Textos ----
    private string NombreEspecie(MysteryGift g)
    {
        var t = Sesion!.Textos;
        var n = (uint)g.Species < t.Species.Count ? t.Species[g.Species] : g.Species.ToString(CultureInfo.InvariantCulture);
        if (g.Form == 0) return n;
        var formas = FormConverter.GetFormList(g.Species, t.Types, t.forms, GameInfo.GenderSymbolUnicode, g.Context);
        return g.Form < formas.Length && !string.IsNullOrEmpty(formas[g.Form]) ? $"{n} ({formas[g.Form]})" : n;
    }
}
