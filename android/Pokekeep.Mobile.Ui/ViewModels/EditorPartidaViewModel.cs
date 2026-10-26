// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pokekeep.Mobile.Ui.Partida;
using Pokekeep.Mobile.Ui.Localizacion;
using SesionM = Pokekeep.Mobile.Ui.Modelo.Sesion;

namespace Pokekeep.Mobile.Ui.ViewModels;

/// <summary>Un campo de un editor de partida en pantalla: escribe en el BORRADOR del modelo y vuelve a leer a los demas (las listas dependientes, los textos derivados).</summary>
public sealed partial class CampoPVm : ObservableObject
{
    private readonly EditorPartidaViewModel _ed;
    private bool _releyendo;
    private string? _textoRechazado;

    public CampoPVm(EditorPartidaViewModel ed, CampoP def)
    {
        _ed = ed; Def = def;
        Releer();
    }

    public CampoP Def { get; private set; }
    internal void ActualizarDefinicion(CampoP def)
    {
        Def = def;
        Releer(conservarTexto: EsTexto || EsNumero);
        if (_textoRechazado is { } rechazado) Error = M.ValidarTexto(Def, rechazado);
        OnPropertyChanged(string.Empty);
    }
    public string Clave => Def.Id;
    private ModeloEditorPartida M => _ed.Modelo;

    [ObservableProperty] private string _etiqueta = string.Empty;
    [ObservableProperty] private string _valor = string.Empty;
    [ObservableProperty] private bool _marcado;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string? _detalle;
    [ObservableProperty] private string? _motivo;
    [ObservableProperty] private bool _visible = true;
    [ObservableProperty] private bool _modificado;

    public bool EsTexto => Def.Tipo is TipoP.Texto or TipoP.FechaHora;
    public bool EsNumero => Def.Tipo is TipoP.Entero or TipoP.Decimal;
    public bool EsEnteroSinSigno => Def.Tipo == TipoP.Entero && Def.Min >= 0 && !Def.Hex;
    public bool EsNumeroLibre => EsNumero && !EsEnteroSinSigno;
    public bool EsOpcion => Def.Tipo == TipoP.Opcion;
    public bool EsMarca => Def.Tipo == TipoP.Marca;
    public bool EsInfo => Def.Tipo == TipoP.Info;
    public bool EsAccion => Def.Tipo == TipoP.Accion;
    public bool EsArchivo => Def.Tipo == TipoP.Archivo;
    public bool PuedeExportar => Def.Exporta is not null;
    /// <summary>Un campo de archivo que solo exporta (volcar todos los bloques) no ensena el boton de importar.</summary>
    public bool PuedeImportar => !Def.SoloExporta;
    public string TextoImportar => Def.TextoBoton ?? IdiomaInterfaz.T("Form.Importar");
    [ObservableProperty] private string? _nombreArchivo;
    /// <summary>Vista previa de la imagen del campo (<see cref="CampoP.Vista"/>), o null.</summary>
    [ObservableProperty] private Bitmap? _vista;
    public bool HayVista => Vista is not null;
    partial void OnVistaChanged(Bitmap? value) => OnPropertyChanged(nameof(HayVista));

    /// <summary>Cuantos <see cref="WriteableBitmap"/> de vista previa se han creado en toda la ejecucion (la prueba comprueba que releer un campo sin cambios no crea ninguno).</summary>
    internal static int BitmapsDeVistaCreados;
    private byte[]? _pixelesDeLaVista;

    /// <summary>
    /// Vuelve a pedir la imagen del campo, pero SOLO crea un bitmap nuevo (y libera el anterior) si los pixeles han cambiado: una relectura (cada <c>Releer</c>, importacion, Deshacer...) con la misma imagen deja el mismo bitmap.
    /// </summary>
    private void ActualizarVista()
    {
        if (Def.Vista is null) return;
        var d = Def.Vista(M.Ctx);
        if (d is null) { var v0 = Vista; Vista = null; _pixelesDeLaVista = null; (v0 as IDisposable)?.Dispose(); return; }
        if (Vista is not null && _pixelesDeLaVista is not null && Vista.PixelSize.Width == d.Ancho && Vista.PixelSize.Height == d.Alto && d.Pixeles.AsSpan().SequenceEqual(_pixelesDeLaVista)) return;
        var wb = new WriteableBitmap(new PixelSize(d.Ancho, d.Alto), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
        System.Threading.Interlocked.Increment(ref BitmapsDeVistaCreados);
        using (var fb = wb.Lock())
            for (var y = 0; y < d.Alto; y++) System.Runtime.InteropServices.Marshal.Copy(d.Pixeles, y * d.Ancho * 4, fb.Address + y * fb.RowBytes, d.Ancho * 4);
        var vieja = Vista;
        _pixelesDeLaVista = (byte[])d.Pixeles.Clone();
        Vista = wb;
        (vieja as IDisposable)?.Dispose();
    }
    public bool HayArchivoElegido => !string.IsNullOrEmpty(Valor);
    public string ResumenArchivo => HayArchivoElegido ? $"{NombreArchivo ?? IdiomaInterfaz.T("Form.Archivo")} ({Valor})" : IdiomaInterfaz.T("Form.SinArchivo");
    public bool EsFecha => Def.Tipo == TipoP.FechaHora;
    public bool EsRejilla => EsTexto && Def.Rejilla is not null;
    public int AnchoRejilla => Def.Rejilla?.Ancho ?? 0;
    public int AltoRejilla => Def.Rejilla?.Alto ?? 0;
    public IReadOnlyList<uint> PaletaRejilla => Def.Rejilla?.Paleta ?? [];
    public bool EsTextoLinea => EsTexto && !Def.Multilinea && !EsRejilla;
    public bool EsTextoVarias => EsTexto && Def.Multilinea && !EsRejilla;
    public bool EsEditable => Motivo is null && !EsInfo;
    public bool EsFijo => Motivo is not null && !EsInfo;
    public bool HayError => !string.IsNullOrEmpty(Error);
    public bool HayDetalle => !string.IsNullOrEmpty(Detalle);
    /// <summary>El campo es una especie (el selector y la fila llevan su sprite).</summary>
    public bool EsEspecie => Def.Tipo == TipoP.Opcion && Def.EsEspecie;
    public int EspecieSprite => EsEspecie && M.Borrador(Def.Id) is long l && l > 0 && l < 2000 ? (int)l : 0;
    public bool HayEspecieSprite => EspecieSprite > 0;
    /// <summary>Juego de la partida (X53): la fila de la especie lleva el icono de caja del propio juego cuando esta integrado.</summary>
    public int VersionJuego => (int)Design.Sprites.VersionDe(M.Sesion.Guardado);
    public bool ValorLargo => (Valor?.Length ?? 0) > 22;
    public bool ValorCorto => !ValorLargo;
    public string Marcador => Def.Tipo == TipoP.FechaHora ? IdiomaInterfaz.T("Form.MarcadorFecha") : string.Empty;
    public int Longitud => Def.Tipo switch { TipoP.Texto => Def.LongitudEn(M.Ctx), TipoP.Entero => Def.Hex ? 16 : 20, TipoP.FechaHora => 19, _ => 0 };
    public string TextoMarca => EsAccion ? (Marcado && !Def.Inmediata ? IdiomaInterfaz.T("Form.SeHara") : Def.TextoBoton ?? IdiomaInterfaz.T("Form.Hacer")) : Marcado ? IdiomaInterfaz.T("Comun.Si") : IdiomaInterfaz.T("Comun.No");
    public string Ayuda => Motivo ?? Def.Ayuda ?? string.Empty;
    public string Accesible => Def.Tipo switch
    {
        TipoP.Opcion => IdiomaInterfaz.F("Form.ElegirAccesible", Etiqueta, Valor),
        TipoP.Marca => $"{Etiqueta}: {TextoMarca}",
        TipoP.Accion => $"{Etiqueta}: {IdiomaInterfaz.T(Def.Inmediata ? "Form.AccionInmediataAccesible" : Marcado ? "Form.AccionPendienteAccesible" : "Form.AccionAccesible")}",
        TipoP.Archivo => $"{Etiqueta}: {ResumenArchivo}",
        TipoP.Info => $"{Etiqueta}: {Valor}",
        _ => Modificado ? IdiomaInterfaz.F("Form.Modificado", Etiqueta) : Etiqueta,
    };

    partial void OnMotivoChanged(string? value) { OnPropertyChanged(nameof(EsEditable)); OnPropertyChanged(nameof(EsFijo)); OnPropertyChanged(nameof(Ayuda)); }
    partial void OnErrorChanged(string? value) => OnPropertyChanged(nameof(HayError));
    partial void OnDetalleChanged(string? value) => OnPropertyChanged(nameof(HayDetalle));
    partial void OnEtiquetaChanged(string value) => OnPropertyChanged(nameof(Accesible));
    partial void OnModificadoChanged(bool value) => OnPropertyChanged(nameof(Accesible));
    partial void OnMarcadoChanged(bool value)
    {
        OnPropertyChanged(nameof(TextoMarca)); OnPropertyChanged(nameof(Accesible));
        if (_releyendo) return;
        Error = M.Poner(Def, value);
        _ed.AlCambiar(this);
    }

    partial void OnValorChanged(string value)
    {
        OnPropertyChanged(nameof(ValorLargo)); OnPropertyChanged(nameof(ValorCorto)); OnPropertyChanged(nameof(Accesible)); OnPropertyChanged(nameof(EspecieSprite)); OnPropertyChanged(nameof(HayEspecieSprite));
        OnPropertyChanged(nameof(HayArchivoElegido)); OnPropertyChanged(nameof(ResumenArchivo));
        if (_releyendo || EsOpcion || EsInfo || EsArchivo) return;
        var rechazo = M.PonerTexto(Def, value);
        Error = rechazo; _textoRechazado = rechazo is null ? null : value;
        if (rechazo is null) _ed.AlCambiar(this);
        // Un numero bien escrito pero fuera del limite se rechaza de inmediato: el campo vuelve al ultimo valor valido y el motivo sigue a la vista (como un campo con mascara).
        else if (EsNumero && value.Trim().Length > 0 && double.TryParse(value.Trim().Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _))
        {
            _releyendo = true;
            try { Valor = M.Formatear(Def, M.Borrador(Def.Id)); } finally { _releyendo = false; }
        }
    }

    /// <summary>Al salir del campo, si lo escrito se rechazo vuelve el ultimo valor valido (el motivo sigue a la vista).</summary>
    public void PerderFoco()
    {
        if (Error is null) return;
        _releyendo = true;
        try { Valor = M.Formatear(Def, M.Borrador(Def.Id)); } finally { _releyendo = false; }
    }

    /// <summary>Vuelve a leer el borrador (tras escribir otro campo).</summary>
    public void Releer(bool conservarTexto = false)
    {
        _releyendo = true;
        try
        {
            var m = M;
            Visible = Def.EsVisible(m.Ctx);
            Etiqueta = Def.TextoEtiqueta(m.Ctx);
            Motivo = m.NoLegibles.TryGetValue(Def.Id, out var nl) ? nl : Def.Motivo?.Invoke(m.Ctx);
            Detalle = Def.Detalle?.Invoke(m.Ctx);
            Modificado = m.Cambiado(Def.Id);
            var v = m.Borrador(Def.Id);
            if (Def.Tipo is TipoP.Marca or TipoP.Accion) Marcado = v is true;
            else if (!conservarTexto) Valor = m.Formatear(Def, v);
            ActualizarVista();
        }
        finally { _releyendo = false; }
    }

    /// <summary>Una accion: queda pendiente (o se cancela). Si pide confirmacion, la primera vez pregunta.</summary>
    [RelayCommand]
    private void Alternar()
    {
        if (!EsAccion || !EsEditable) return;
        // Una accion inmediata actua de golpe sobre la partida: primero el aviso de los cambios pendientes y solo despues (si no hay) la confirmacion destructiva.
        if (Def.Inmediata && _ed.AvisarSiHayPendientes()) return;
        if (!Marcado && Def.Confirmacion is { } aviso)
        {
            TextosConfirmarHoja Textos() => new(Etiqueta, [Def.Confirmacion ?? aviso, IdiomaInterfaz.T(Def.Inmediata ? "Form.AhoraDeshacer" : "Form.HastaAplicar")], Def.TextoBoton ?? IdiomaInterfaz.T("Form.Hacer"), IdiomaInterfaz.T("Form.MejorNo"));
            var t = Textos();
            _ed.AbrirHoja(new ConfirmarHojaViewModel(t.Titulo, t.Parrafos, t.Confirmar, () => { if (Def.Inmediata) _ed.EjecutarInmediata(this); else Marcado = true; }, destructiva: true, actualizarTextos: Textos, textoCancelar: t.Cancelar));
            return;
        }
        if (Def.Inmediata) { _ed.EjecutarInmediata(this); return; }
        Marcado = !Marcado;
    }

    [RelayCommand]
    private async Task Importar()
    {
        if (!EsArchivo || !EsEditable) return;
        var docs = _ed.Documentos();
        if (docs is null) { Error = IdiomaInterfaz.T("Form.SinSelector"); return; }
        try
        {
            var doc = await docs.ElegirParaAbrirAsync(CancellationToken.None);
            if (doc is null) return;
            var datos = await docs.LeerAsync(doc, CancellationToken.None);
            ImportarBytes(datos, doc.Nombre);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException) { Error = IdiomaInterfaz.F("Form.ErrorLeerArchivo", ex.Message); }
    }

    /// <summary>Lo que se hace con los bytes elegidos (separado del selector para poder probarlo).</summary>
    public void ImportarBytes(byte[] datos, string nombre)
    {
        Error = M.Poner(Def, datos);
        if (Error is null) { NombreArchivo = nombre; M.Ctx.NombresDeArchivo[Def.Id] = nombre; ActualizarVista(); _ed.AlCambiar(this); OnPropertyChanged(nameof(ResumenArchivo)); }
    }

    [RelayCommand]
    private void Quitar()
    {
        if (!EsArchivo) return;
        M.Poner(Def, null); NombreArchivo = null; M.Ctx.NombresDeArchivo.Remove(Def.Id); Error = null; ActualizarVista();
        _ed.AlCambiar(this); OnPropertyChanged(nameof(ResumenArchivo));
    }

    [RelayCommand]
    private async Task Exportar()
    {
        if (!EsArchivo || Def.Exporta is null) return;
        var docs = _ed.Documentos();
        if (docs is null) { Error = IdiomaInterfaz.T("Form.SinSelector"); return; }
        try
        {
            if (Def.Exporta(M.Ctx) is not { } x) { Error = IdiomaInterfaz.T("Form.SinExportar"); return; }
            var destino = await docs.ElegirDestinoAsync(x.Nombre, CancellationToken.None);
            if (destino is null) return;
            await docs.EscribirAsync(destino, x.Datos, CancellationToken.None);
            Error = null;
            _ed.Informar(IdiomaInterfaz.F("Form.Exportado", destino.Nombre), "ok", "CircleCheck");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException) { Error = IdiomaInterfaz.F("Form.ErrorExportar", ex.Message); }
    }

    [RelayCommand]
    private void Elegir()
    {
        var m = M;
        var esEspecie = EsEspecie;
        IReadOnlyList<OpcionSelector> Opciones() => [.. m.Opciones(Def).Select(o => new OpcionSelector(o.Id, o.Texto, null, esEspecie ? o.Id : 0))];
        var actual = m.Borrador(Def.Id) is long l ? (int)l : -1;
        _ed.AbrirHoja(new SelectorHojaViewModel(Def.TituloSelector ?? Def.Etiqueta, Opciones(), actual, o =>
        {
            Error = m.Poner(Def, (long)o.Id);
            _releyendo = true; try { Valor = m.Formatear(Def, m.Borrador(Def.Id)); } finally { _releyendo = false; }
            _ed.AlCambiar(this);
        }, actualizarOpciones: Opciones, tituloActual: () => Def.TituloSelector ?? Def.TextoEtiqueta(m.Ctx)));
    }
}

public sealed class SeccionPVm(string titulo, IReadOnlyList<CampoPVm> campos, Func<bool>? visible = null) : ObservableObject
{
    public string Titulo { get; private set; } = titulo;
    internal void ActualizarTitulo(string texto) { Titulo = texto; OnPropertyChanged(nameof(Titulo)); OnPropertyChanged(nameof(HayTitulo)); }
    public bool HayTitulo => !string.IsNullOrWhiteSpace(Titulo);
    public IReadOnlyList<CampoPVm> Campos { get; } = campos;
    public bool Visible => (visible?.Invoke() ?? true) && Campos.Any(c => c.Visible);
    public void Refrescar() => OnPropertyChanged(nameof(Visible));
}

public sealed partial class PestanaPVm(PestanaP def) : ObservableObject
{
    public PestanaP Def { get; private set; } = def;
    internal void ActualizarDefinicion(PestanaP nueva) { Def = nueva; OnPropertyChanged(nameof(Titulo)); OnPropertyChanged(nameof(Accesible)); }
    public string Titulo => Def.Titulo;
    [ObservableProperty] private bool _elegida;
    public string Accesible => IdiomaInterfaz.F("Form.Pestana", Titulo);
}

/// <summary>
/// Editor de partida generico en pantalla (Entrenador, Estadisticas, Eventos...): pestanas, secciones de campos y la barra de «cambios sin aplicar». Trabaja sobre el borrador de
/// <see cref="ModeloEditorPartida"/>; «Aplicar a la partida» escribe solo lo cambiado como una entrada del historial. Con cambios sin aplicar, salir pide confirmacion.
/// </summary>
public sealed partial class EditorPartidaViewModel : ObservableObject
{
    private readonly Action _volver;
    private readonly Action<HojaViewModel> _abrirHoja;
    private readonly Action<string, string, string, string?, ICommand?> _informar;
    private bool _aplicando;
    private readonly Func<EditorDefP?>? _actualizarDefinicion;

    private readonly Action<Pokekeep.Mobile.Ui.Cajas.EditorCasillas.TipoCambio> _alCambiarCasillas;   // referencia fuerte de la suscripcion debil a Sesion.CasillasCambiadas
    private readonly Func<Pokekeep.Mobile.Ui.Almacen.IAlmacenDocumentos?> _documentos;
    public EditorPartidaViewModel(ModeloEditorPartida modelo, Action volver, Action<HojaViewModel> abrirHoja, Action<string, string, string, string?, ICommand?> informar, Func<Pokekeep.Mobile.Ui.Almacen.IAlmacenDocumentos?>? documentos = null, Func<EditorDefP?>? actualizarDefinicion = null)
    {
        _actualizarDefinicion = actualizarDefinicion;
        Modelo = modelo; _volver = volver; _abrirHoja = abrirHoja; _informar = informar; _documentos = documentos ?? (() => null);
        Construir();
        _alCambiarCasillas = _ => { if (!_aplicando) Recargar(); };
        modelo.Sesion.SuscribirCasillasDebil(_alCambiarCasillas);
    }

    public bool ActualizarIdioma()
    {
        if (_actualizarDefinicion?.Invoke() is not { } nueva || !Modelo.ActualizarMetadatos(nueva)) return false;
        foreach (var c in Modelo.Campos) if (_cache.TryGetValue(c.Id, out var vm)) vm.ActualizarDefinicion(c);
        foreach (var p in Pestanas) p.ActualizarDefinicion(nueva.Pestanas.Single(n => n.Id == p.Def.Id));
        if (Actual is { } actual)
        {
            var secciones = actual.Def.Secciones.Where(s => s.Campos.Count > 0).ToArray();
            for (var i = 0; i < Secciones.Count; i++) Secciones[i].ActualizarTitulo(secciones[i].Titulo);
            if (actual.Def.Tabla is { } tabla) Tabla?.ActualizarIdioma(tabla);
            if (actual.Def.Coleccion is { } coleccion) Coleccion?.ActualizarIdioma(coleccion);
        }
        OnPropertyChanged(nameof(Titulo)); OnPropertyChanged(nameof(Descripcion)); OnPropertyChanged(nameof(TextoCambios));
        return true;
    }

    public ModeloEditorPartida Modelo { get; }
    public string Titulo => Modelo.Def.Titulo;
    public string Descripcion => Modelo.Def.Descripcion;
    public ObservableCollection<PestanaPVm> Pestanas { get; } = [];
    public ObservableCollection<SeccionPVm> Secciones { get; } = [];
    public ObservableCollection<CampoPVm> Todos { get; } = [];
    [ObservableProperty] private PestanaPVm? _actual;
    [ObservableProperty] private bool _hayCambios;
    [ObservableProperty] private int _numCambios;
    [ObservableProperty] private TablaPVm? _tabla;
    public bool HayTabla => Tabla is not null;
    public bool HaySecciones => Tabla is null && Coleccion is null;
    public bool VariasPestanas => Pestanas.Count > 1;
    public string TextoCambios => IdiomaInterfaz.P("Form.Cambios", NumCambios);

    partial void OnNumCambiosChanged(int value) => OnPropertyChanged(nameof(TextoCambios));
    partial void OnTablaChanged(TablaPVm? value) { OnPropertyChanged(nameof(HayTabla)); OnPropertyChanged(nameof(HaySecciones)); }

    internal void AbrirHoja(HojaViewModel h) => _abrirHoja(h);
    internal Pokekeep.Mobile.Ui.Almacen.IAlmacenDocumentos? Documentos() => _documentos();
    internal void Informar(string texto, string nivel, string glifo) => _informar(texto, nivel, glifo, null, null);

    /// <summary>Si hay cambios sin aplicar, avisa (una accion inmediata los perderia) y devuelve true.</summary>
    internal bool AvisarSiHayPendientes()
    {
        if (!Modelo.HayCambios) return false;
        _informar(ModeloEditorPartida.MotivoPendientes, "aviso", "CircleAlert", null, null);
        return true;
    }

    /// <summary>Una accion inmediata: sobre la partida real, con el borrador vacio. Despues se vuelve a leer todo (conservando la pestana y el elemento abierto).</summary>
    internal void EjecutarInmediata(CampoPVm campo)
    {
        if (AvisarSiHayPendientes()) return;
        _aplicando = true;
        try
        {
            var r = Modelo.EjecutarInmediata(campo.Def);
            Reconstruir();
            if (r.SinCambios) _informar(IdiomaInterfaz.T("Form.NadaCambio"), "aviso", "Info", null, null);
            else _informar(IdiomaInterfaz.F("Form.Hecho", campo.Etiqueta), "ok", "CircleCheck", null, null);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IndexOutOfRangeException or FormatException or OverflowException or KeyNotFoundException or NotSupportedException)
        {
            _informar(IdiomaInterfaz.F("Form.ErrorAccion", ex.Message), "error", "TriangleAlert", null, null);
        }
        finally { _aplicando = false; }
    }

    [ObservableProperty] private ColeccionPVm? _coleccion;
    public bool HayColeccion => Coleccion is not null;
    partial void OnColeccionChanged(ColeccionPVm? value) { OnPropertyChanged(nameof(HayColeccion)); OnPropertyChanged(nameof(HaySecciones)); }

    /// <summary>El VM de un campo; los de los elementos de una coleccion se crean cuando se abre el elemento.</summary>
    internal CampoPVm VmDe(CampoP c)
    {
        if (_cache.TryGetValue(c.Id, out var vm)) return vm;
        vm = new CampoPVm(this, c); _cache[c.Id] = vm; Todos.Add(vm); return vm;
    }

    private void Construir()
    {
        Pestanas.Clear(); Todos.Clear();
        var cache = new Dictionary<string, CampoPVm>();
        foreach (var p in Modelo.PestanasVisibles) Pestanas.Add(new PestanaPVm(p));
        foreach (var c in Modelo.Campos) { var vm = new CampoPVm(this, c); cache[c.Id] = vm; Todos.Add(vm); }
        _cache = cache;
        OnPropertyChanged(nameof(VariasPestanas));
        Actual = Pestanas.FirstOrDefault();
        Refrescar();
    }

    private Dictionary<string, CampoPVm> _cache = [];

    partial void OnActualChanged(PestanaPVm? value)
    {
        foreach (var p in Pestanas) p.Elegida = ReferenceEquals(p, value);
        Secciones.Clear();
        Tabla = null; Coleccion = null;
        if (value is null) return;
        if (value.Def.Coleccion is { } col) Coleccion = new ColeccionPVm(this, col);
        else if (value.Def.Tabla is { } t) Tabla = new TablaPVm(Modelo, t, AlCambiarTabla);
        else
            foreach (var s in value.Def.Secciones.Where(s => s.Campos.Count > 0))
                Secciones.Add(new SeccionPVm(s.Titulo, [.. s.Campos.Select(c => _cache[c.Id])], () => s.EsVisible(Modelo.Ctx)));
    }

    /// <summary>Un campo cambio: se vuelven a leer los demas (listas dependientes, textos derivados, visibilidad).</summary>
    internal void AlCambiar(CampoPVm origen)
    {
        foreach (var c in Todos) if (!ReferenceEquals(c, origen)) c.Releer();
        origen.Releer(conservarTexto: origen.EsNumero || origen.EsTexto);
        Refrescar();
    }

    private void AlCambiarTabla() => Refrescar();

    private void Refrescar()
    {
        foreach (var s in Secciones) s.Refrescar();
        Coleccion?.Refrescar();
        HayCambios = Modelo.HayCambios;
        NumCambios = Modelo.NumCambios;
    }

    private void Recargar()
    {
        Modelo.Recargar();
        Reconstruir();
    }

    /// <summary>Vuelve a crear los campos (tras leer la partida de nuevo) SIN perder donde estaba el usuario: la misma pestana y, en una coleccion, el mismo elemento.</summary>
    private void Reconstruir()
    {
        var id = Actual?.Def.Id;
        var indice = Coleccion?.Indice ?? 0;
        // This path follows an explicit apply/reset/history operation, never a language change.
        // Refresh a changed Core schema before the user starts another draft.
        if (!Modelo.HayCambios && _actualizarDefinicion?.Invoke() is { } nueva && !Modelo.ActualizarMetadatos(nueva))
            Modelo.Recargar(nueva);
        Construir();
        if (id is not null && Pestanas.FirstOrDefault(p => p.Def.Id == id) is { } p) Actual = p;
        Coleccion?.Abrir(indice);
    }

    [RelayCommand] private void Elegir(PestanaPVm? p) { if (p is not null) Actual = p; }

    [RelayCommand]
    private void Aplicar()
    {
        if (!Modelo.HayCambios) return;
        _aplicando = true;
        try
        {
            var r = Modelo.Aplicar();
            Reconstruir();
            if (r.SinCambios) _informar(IdiomaInterfaz.T("Form.NadaEscribir"), "aviso", "Info", null, null);
            else if (Modelo.Def.AvisoTrasAplicar?.Invoke(Modelo.Ctx) is { } aviso) _informar(IdiomaInterfaz.F("Form.AplicadoAviso", Titulo, aviso), "aviso", "CircleAlert", null, null);
            else _informar(IdiomaInterfaz.F("Form.Aplicado", Titulo), "ok", "CircleCheck", null, null);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IndexOutOfRangeException or FormatException or OverflowException or KeyNotFoundException or NotSupportedException)
        {
            // El historial ya ha devuelto la partida a como estaba; el borrador se conserva para que el usuario lo corrija.
            _informar(IdiomaInterfaz.F("Form.ErrorAplicar", ex.Message), "error", "TriangleAlert", null, null);
        }
        finally { _aplicando = false; }
    }

    [RelayCommand]
    private void Descartar()
    {
        Recargar();
        _informar(IdiomaInterfaz.T("Form.Descartados"), "ok", "CircleCheck", null, null);
    }

    /// <summary>Vuelve al menu de Partida; con cambios sin aplicar pregunta antes.</summary>
    [RelayCommand]
    public void Volver()
    {
        if (!Modelo.HayCambios) { _volver(); return; }
        TextosConfirmarHoja Textos() => new(IdiomaInterfaz.T("Form.Salir.Titulo"), [IdiomaInterfaz.P("Form.Salir.Cambios", NumCambios, Titulo), IdiomaInterfaz.T("Form.Salir.Explicacion")], IdiomaInterfaz.T("Form.Salir.Confirmar"), IdiomaInterfaz.T("Form.Salir.Cancelar"));
        var t = Textos();
        _abrirHoja(new ConfirmarHojaViewModel(t.Titulo, t.Parrafos, t.Confirmar, _volver, destructiva: true, textoCancelar: t.Cancelar, actualizarTextos: Textos));
    }

    /// <summary>Boton Atras del sistema: true si lo ha gestionado.</summary>
    public bool Atras() { Volver(); return true; }

    /// <summary>Pruebas de dispositivo: va a una pestana por id.</summary>
    public bool IrAPestana(string id) { if (Pestanas.FirstOrDefault(p => p.Def.Id == id) is { } p) { Actual = p; return true; } return false; }
}
