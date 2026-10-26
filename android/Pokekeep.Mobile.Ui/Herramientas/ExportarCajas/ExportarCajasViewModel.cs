// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Ajustes.Pkhex;
using Pokekeep.Mobile.Ui.Almacen;
using Pokekeep.Mobile.Ui.ViewModels;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Herramientas.ExportarCajas;

/// <summary>Con qué alcance se abre el exportador (<c>BoxExporter.ExportOverride</c>): el de los ajustes, todas las cajas («Exportar cajas») o la caja que se está viendo («Exportar caja»).</summary>
public enum AlcanceExportador { SegunAjustes, Todas, CajaActual }

/// <summary>Una opción del exportador (una fila del <c>PropertyGrid</c> de <c>BoxExporter</c>): su etiqueta de PKHeX en castellano, el valor y una hoja para elegir otro.</summary>
public sealed partial class OpcionExportacion : ObservableObject
{
    private readonly Func<string> _leer;
    private readonly Action _abrir;
    public OpcionExportacion(string ruta, Func<string> leer, Action abrir)
    {
        Ruta = ruta; _leer = leer; _abrir = abrir;
        var nodo = RegistroAjustesPkhex.Buscar(ruta) ?? throw new InvalidOperationException(IdiomaInterfaz.F("ExportarCajasViewModel.NoEsUnAjuste", ruta));
        Nombre = RegistroAjustesPkhex.Etiqueta(nodo) ?? nodo.Nombre;
        Descripcion = RegistroAjustesPkhex.Descripcion(nodo) ?? string.Empty;
    }
    public string Ruta { get; }
    public string Nombre { get; }
    public string Descripcion { get; }
    public string Valor => _leer();
    public string Accesible => $"{Nombre}: {Valor}. {Descripcion}";
    public void Refrescar() { OnPropertyChanged(nameof(Valor)); OnPropertyChanged(nameof(Accesible)); }
    [RelayCommand] private void Abrir() => _abrir();
}

/// <summary>
/// «Exportar cajas» (<c>BoxExporter</c>; PK-SUB-001, PK-IO-036, PK-IO-037, PK-MENU-015, PK-MENU-016): las opciones de <c>BoxExportSettings</c> y el nombrador, una vista previa de lo que se va a escribir y «Exportar»,
/// que pide una carpeta al sistema y escribe allí los <c>.pk*</c> como <c>BoxExport.Export</c>. Solo lee la partida.
/// <para>Ajustes, como PKHeX: sin alcance forzado el exportador edita los ajustes de exportación GUARDADOS (PKHeX enlaza el <c>PropertyGrid</c> al mismo objeto); con alcance forzado («Exportar caja» y
/// «Exportar cajas» del menú) trabaja sobre una copia con ese alcance. Al cerrar, PKHeX guarda la copia y el nombrador en los ajustes (<c>BoxExporter_FormClosing</c>); aquí se guardan al abrir y en cada cambio,
/// que deja el mismo estado final sin depender de cómo se cierre la pantalla (botón «Volver» o el «Atrás» del sistema).</para>
/// </summary>
public sealed partial class ExportarCajasViewModel : HerramientaViewModel
{
    public const int MaxEjemplos = 8;
    private readonly BoxExportSettings _opciones;
    private readonly int _cajaActual;
    private int _nombrador;

    public ExportarCajasViewModel(ContextoHerramienta contexto, Action volver, AlcanceExportador alcance = AlcanceExportador.SegunAjustes, int? cajaActual = null) : base(contexto, volver)
    {
        Alcance = alcance;
        var global = Ajustes.SlotExport.BoxExport;
        _opciones = alcance == AlcanceExportador.SegunAjustes ? global : global with { Scope = alcance == AlcanceExportador.Todas ? BoxExportScope.All : BoxExportScope.Current };
        _cajaActual = cajaActual ?? Sesion?.Guardado.CurrentBox ?? 0;
        _nombrador = ModeloExportarCajas.IndiceNombrador(Ajustes.SlotExport.DefaultBoxExportNamer);
        Opciones =
        [
            Opcion("SlotExport.BoxExport.Scope", () => _opciones.Scope, v => _opciones.Scope = v),
            Opcion("SlotExport.BoxExport.FolderCreation", () => _opciones.FolderCreation, v => _opciones.FolderCreation = v),
            Opcion("SlotExport.BoxExport.FolderPrefix", () => _opciones.FolderPrefix, v => _opciones.FolderPrefix = v),
            Opcion("SlotExport.BoxExport.EmptySlots", () => _opciones.EmptySlots, v => _opciones.EmptySlots = v),
            Opcion("SlotExport.BoxExport.FileIndexPrefix", () => _opciones.FileIndexPrefix, v => _opciones.FileIndexPrefix = v),
            Opcion("SlotExport.BoxExport.Notify", () => _opciones.Notify, v => _opciones.Notify = v),
        ];
        Nombrador = new OpcionExportacion("SlotExport.DefaultBoxExportNamer", () => NombreNombrador(ModeloExportarCajas.Nombradores[_nombrador]), ElegirNombrador);
        GuardarEnAjustes();
        Actualizar();
    }

    public override string Titulo => Alcance switch { AlcanceExportador.Todas => IdiomaInterfaz.T("ExportarCajas.ExportarCajas"), AlcanceExportador.CajaActual => IdiomaInterfaz.T("ExportarCajas.ExportarCaja"), _ => IdiomaInterfaz.T("ExportarCajas.ExportarCajas") };
    public string Subtitulo => IdiomaInterfaz.T("ExportarCajas.GuardaCadaPokemonCajas");
    public AlcanceExportador Alcance { get; }

    /// <summary>Las opciones de exportación en el orden del <c>PropertyGrid</c> de PKHeX (por categorías: Exportación, Carpeta, Archivo).</summary>
    public IReadOnlyList<OpcionExportacion> Opciones { get; }
    public OpcionExportacion Nombrador { get; }

    /// <summary>Las opciones vigentes de ESTA pantalla (para las pruebas y para escribir).</summary>
    public BoxExportSettings OpcionesVigentes => _opciones;
    public IFileNamer<PKM> NombradorElegido => ModeloExportarCajas.Nombradores[_nombrador];
    public int CajaActual => _cajaActual;

    [ObservableProperty] private string _resumen = string.Empty;
    [ObservableProperty] private IReadOnlyList<string> _ejemplos = [];
    [ObservableProperty] private string _mensaje = string.Empty;
    [ObservableProperty] private bool _mensajeEsError;
    public bool HayMensaje => !string.IsNullOrEmpty(Mensaje);
    partial void OnMensajeChanged(string value) => OnPropertyChanged(nameof(HayMensaje));
    public bool HayEjemplos => Ejemplos.Count > 0;
    partial void OnEjemplosChanged(IReadOnlyList<string> value) => OnPropertyChanged(nameof(HayEjemplos));

    /// <summary>Por qué no se puede exportar ahora (sin partida, sin cajas o sin carpetas del sistema); null si se puede.</summary>
    public string? MotivoExportar => Sesion is null ? DefHerramienta.SinPartida
        : !Sesion.Guardado.HasBox ? MessageStrings.MsgSaveBoxFailNone
        : Contexto.Carpetas() is not IAlmacenCarpetasCajas || Contexto.Documentos() is null ? IdiomaInterfaz.T("ExportarCajas.ExportarCarpetaDisponibleAqui") : null;
    public bool PuedeExportar => MotivoExportar is null;

    /// <summary>El plan de lo que se escribiría con las opciones de ahora.</summary>
    public PlanVolcado Plan() => Sesion is null ? new PlanVolcado([], 0, 0) : ModeloExportarCajas.Preparar(Sesion.Guardado, _cajaActual, _opciones, NombradorElegido);

    private void Actualizar()
    {
        foreach (var o in Opciones) o.Refrescar();
        Nombrador.Refrescar();
        if (Sesion is null) { Resumen = DefHerramienta.SinPartida; Ejemplos = []; return; }
        var plan = Plan();
        var cajas = plan.Cajas == 1 ? IdiomaInterfaz.F("ExportarCajas.Caja", NombreCaja(plan.CajaInicio)) : IdiomaInterfaz.F("ExportarCajas.Cajas", plan.Cajas);
        Resumen = plan.Archivos.Count == 0 ? IdiomaInterfaz.F("ExportarCajas.NingunPokemonExportar", cajas)
            : IdiomaInterfaz.P("ExportarCajas.SeExportaran", plan.Archivos.Count, cajas) + (plan.ArchivosDistintos < plan.Archivos.Count ? IdiomaInterfaz.F("ExportarCajas.NombreRepetidoUltimoSustituye", plan.Archivos.Count - plan.ArchivosDistintos) : ".");
        Ejemplos = [.. plan.Archivos.Take(MaxEjemplos).Select(a => a.Subcarpeta is null ? a.Nombre : $"{a.Subcarpeta} / {a.Nombre}")];
        OnPropertyChanged(nameof(MotivoExportar)); OnPropertyChanged(nameof(PuedeExportar));
    }

    private string NombreCaja(int caja) => Sesion!.Guardado is IBoxDetailNameRead r ? r.GetBoxName(caja) : BoxDetailNameExtensions.GetDefaultBoxName(caja);

    private OpcionExportacion Opcion<T>(string ruta, Func<T> leer, Action<T> poner) where T : struct, Enum
    {
        OpcionExportacion? fila = null;
        fila = new OpcionExportacion(ruta, () => FilaPkhexOpcion.Etiqueta(typeof(T), leer().ToString()), () =>
        {
            var valores = Enum.GetValues<T>();
            Contexto.AbrirHoja(new SelectorHojaViewModel(fila!.Nombre, [.. valores.Select((v, i) => new OpcionSelector(i, FilaPkhexOpcion.Etiqueta(typeof(T), v.ToString())))], Array.IndexOf(valores, leer()),
                o => Poner(() => poner(valores[o.Id]))));
        });
        return fila;
    }

    private static string NombreNombrador(IFileNamer<PKM> n) => n.Name == "Default" ? IdiomaInterfaz.T("Ajustes.Nombrador.Defecto") : n.Name;

    private void ElegirNombrador()
    {
        var l = ModeloExportarCajas.Nombradores;
        Contexto.AbrirHoja(new SelectorHojaViewModel(Nombrador.Nombre, [.. l.Select((n, i) => new OpcionSelector(i, NombreNombrador(n)))], _nombrador, o => Poner(() => _nombrador = o.Id)));
    }

    /// <summary>Cambia una opción por código (las pruebas y el gate visual usan el mismo camino que la hoja).</summary>
    public void Poner(Action cambio) { cambio(); GuardarEnAjustes(); Actualizar(); }

    public void ElegirNombrador(int indice) => Poner(() => _nombrador = Math.Clamp(indice, 0, ModeloExportarCajas.Nombradores.Count - 1));

    private void GuardarEnAjustes()
    {
        var s = Ajustes.SlotExport;
        s.BoxExport = _opciones;
        s.DefaultBoxExportNamer = NombradorElegido.Name;
        Contexto.Ajustes.Guardar();
    }

    [RelayCommand]
    private Task Exportar() => ExportarAsync();

    /// <summary>Pide la carpeta al sistema y exporta. Las pruebas pasan la carpeta directamente con <see cref="ExportarEnAsync"/>.</summary>
    public async Task ExportarAsync()
    {
        if (MotivoExportar is { } m) { MostrarError(m); return; }
        var carpetas = (IAlmacenCarpetasCajas)Contexto.Carpetas()!;
        CarpetaRef? carpeta;
        try { carpeta = await Contexto.Carpetas()!.ElegirCarpetaAsync(CancellationToken.None); }
        catch (Exception ex) { MostrarError(IdiomaInterfaz.T("ExportarCajas.PodidoAbrirSelectorCarpetas") + ex.Message); return; }
        if (carpeta is null) return;
        await ExportarEnAsync(carpeta, carpetas);
    }

    private volatile int _escritosAntes;
    private int _totalPlan;

    // Si la exportación se corta a mitad, la partida no ha cambiado (verdad) pero la carpeta sí: se dice cuántos archivos llegaron a escribirse (M-5 de la auditoría de Fable).
    protected override string TextoCancelado() => _escritosAntes > 0 ? IdiomaInterfaz.F("ExportarCajas.CanceladoEscribieronArchivosCarpeta", _escritosAntes, _totalPlan) : base.TextoCancelado();
    protected override string TextoFallo(Exception ex) => _escritosAntes > 0 ? IdiomaInterfaz.F("ExportarCajas.PudoTerminarEscribieronArchivos", ex.GetType().Name, _escritosAntes, _totalPlan) : base.TextoFallo(ex);

    public async Task<int?> ExportarEnAsync(CarpetaRef carpeta, IAlmacenCarpetasCajas carpetas)
    {
        if (Sesion is null) { MostrarError(DefHerramienta.SinPartida); return null; }
        var docs = Contexto.Documentos() ?? throw new InvalidOperationException(IdiomaInterfaz.T("ExportarCajasViewModel.SinAlmacenDeDocumentos"));
        var sav = Sesion!.Guardado;
        var opciones = _opciones with { };
        var nombrador = NombradorElegido;
        Mensaje = string.Empty;
        // El plan (las COPIAS de los Pokémon) se prepara aquí, en el hilo de la interfaz, que es el que cambia la partida: en segundo plano solo se escribe en la carpeta y la partida viva no se lee.
        var plan = ModeloExportarCajas.Preparar(sav, _cajaActual, opciones, nombrador);
        _escritosAntes = 0; _totalPlan = plan.Archivos.Count;
        var escritos = await EjecutarAsync(IdiomaInterfaz.T("ExportarCajas.ExportandoCajas"), (token, avance) =>
            (int?)ModeloExportarCajas.EscribirAsync(plan, carpeta, carpetas, docs, token, avance, n => _escritosAntes = n).GetAwaiter().GetResult());
        if (escritos is not { } n) { if (Estado.Length > 0) MostrarError(Estado); return null; }
        var aviso = ModeloExportarCajas.Aviso(opciones.Notify, n, carpeta.Nombre);
        Estado = string.Empty;
        Mensaje = aviso ?? IdiomaInterfaz.F("ExportarCajasViewModel.Listo", Plural.De(n, "archivo", "archivos"));
        MensajeEsError = false;
        if (aviso is not null) Contexto.Informar(aviso.Replace(Environment.NewLine, " "), "ok", "CircleCheck");
        return n;
    }

    /// <summary>Deja en pantalla el mensaje de una exportación terminada (gate visual), el mismo texto que pone <see cref="ExportarEnAsync"/>.</summary>
    public void MostrarExportado(int escritos, string carpeta) { Mensaje = ModeloExportarCajas.Aviso(BoxExportNofify.NotifyResult, escritos, carpeta)!; MensajeEsError = false; }

    /// <summary>Deja un mensaje de error en pantalla (gate visual y pruebas).</summary>
    public void MostrarError(string mensaje) { Mensaje = mensaje; MensajeEsError = mensaje.Length > 0; }
}
