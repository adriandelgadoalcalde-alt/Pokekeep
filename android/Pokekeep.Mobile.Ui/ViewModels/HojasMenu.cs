// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.ViewModels;

/// <summary>
/// Una fila de un menu en hoja: icono, titulo y una linea de ayuda. Con <see cref="Accion"/> es accionable; <see cref="Destructiva"/> se pinta como aviso de que borra;
/// <see cref="Pronto"/> dice honestamente que llega en otra fase (nunca un boton muerto sin explicacion); <see cref="Motivo"/> explica por que esta desactivada.
/// </summary>
public sealed record EntradaHoja(string Titulo, string? Descripcion, string Glifo, Action? Accion, bool Destructiva = false, bool Pronto = false, string? Fase = null, string? Motivo = null, bool Elegida = false)
{
    public GameVersion Juego { get; init; }
    public bool HayJuego => Juego.IsValidSavedVersion();
    public bool Accionable => Accion is not null && Motivo is null && !Pronto;
    public bool Desactivada => !Accionable;
    public bool TieneDescripcion => !string.IsNullOrWhiteSpace(Descripcion) || Motivo is not null || Pronto;
    public string Linea => Motivo ?? (Pronto ? IdiomaInterfaz.T("Menu.Pronto") : Descripcion ?? string.Empty);
    public string Accesible => $"{Titulo}{(TieneDescripcion ? ". " + Linea : string.Empty)}{(Elegida ? IdiomaInterfaz.T("Menu.Elegida") : string.Empty)}{(Destructiva ? IdiomaInterfaz.T("Menu.Destructiva") : string.Empty)}";
}

public sealed record SeccionHoja(string? Titulo, IReadOnlyList<EntradaHoja> Entradas)
{
    public bool TieneTitulo => !string.IsNullOrWhiteSpace(Titulo);
}

public sealed record TextosMenuHoja(string Titulo, IReadOnlyList<SeccionHoja> Secciones, string? Subtitulo = null, string? Pie = null);
public sealed record TextosConfirmarHoja(string Titulo, IReadOnlyList<string> Parrafos, string Confirmar, string? Cancelar = null);

/// <summary>Un interruptor del menu (por ejemplo «Todas las cajas» y «Invertir» del menu de caja, los equivalentes de Mayus y Ctrl de PKHeX).</summary>
public sealed partial class InterruptorHoja(string texto, string descripcion, bool valor = false) : ObservableObject
{
    public string Texto { get; } = texto;
    public string Descripcion { get; } = descripcion;
    [ObservableProperty] private bool _activo = valor;
    public string Accesible => $"{Texto}. {Descripcion}";
}

/// <summary>Hoja de menu: secciones de filas con icono, titulo y ayuda, interruptores opcionales y un «Volver» a otro menu. La usan el menu de casilla, el de caja, «Exportar» y «Elegir caja».</summary>
public sealed partial class MenuHojaViewModel : HojaViewModel
{
    private TextosMenuHoja _textos;
    private readonly Func<TextosMenuHoja>? _actualizarTextos;

    public MenuHojaViewModel(string titulo, IReadOnlyList<SeccionHoja> secciones, string? subtitulo = null, IReadOnlyList<InterruptorHoja>? interruptores = null, Action? volver = null, string? pie = null, Func<TextosMenuHoja>? actualizarTextos = null)
    {
        _textos = new(titulo, secciones, subtitulo, pie); _actualizarTextos = actualizarTextos;
        Interruptores = interruptores ?? []; Volver = volver;
    }

    public override string Titulo => _textos.Titulo;
    public string? Subtitulo => _textos.Subtitulo;
    public string? Pie => _textos.Pie;
    public IReadOnlyList<SeccionHoja> Secciones => _textos.Secciones;
    public override void ActualizarIdioma()
    {
        if (_actualizarTextos is not { } actualizar) return;
        _textos = actualizar();
        OnPropertyChanged(nameof(Titulo)); OnPropertyChanged(nameof(Subtitulo)); OnPropertyChanged(nameof(Pie));
        OnPropertyChanged(nameof(Secciones)); OnPropertyChanged(nameof(Entradas));
        OnPropertyChanged(nameof(HaySubtitulo)); OnPropertyChanged(nameof(HayPie));
    }
    public IReadOnlyList<InterruptorHoja> Interruptores { get; }
    public Action? Volver { get; }
    public bool HayVolver => Volver is not null;
    public bool HaySubtitulo => !string.IsNullOrWhiteSpace(Subtitulo);
    public bool HayPie => !string.IsNullOrWhiteSpace(Pie);
    public bool HayInterruptores => Interruptores.Count > 0;
    public IEnumerable<EntradaHoja> Entradas => Secciones.SelectMany(s => s.Entradas);

    /// <summary>Elegir una fila: la hoja se cierra y se ejecuta la accion (que suele abrir otra hoja, un selector del sistema o aplicar algo).</summary>
    [RelayCommand]
    public void Elegir(EntradaHoja? entrada)
    {
        if (entrada is not { Accionable: true }) return;
        Cerrar();
        entrada.Accion!();
    }

    [RelayCommand] private void VolverAtras() => Volver?.Invoke();
}

/// <summary>Confirmacion antes de una operacion que borra o cambia muchas casillas (como los avisos de PKHeX): dice exactamente que va a pasar y que se puede deshacer.</summary>
public sealed partial class ConfirmarHojaViewModel : HojaViewModel
{
    private TextosConfirmarHoja _textos;
    private readonly Func<TextosConfirmarHoja>? _actualizarTextos;
    private readonly string? _textoCancelar;
    private readonly Action _confirmar;
    private bool _confirmada;

    public ConfirmarHojaViewModel(string titulo, IReadOnlyList<string> parrafos, string textoConfirmar, Action confirmar, bool destructiva = false, string? textoCancelar = null, Action? alDescartar = null, Func<TextosConfirmarHoja>? actualizarTextos = null)
    {
        _textos = new(titulo, parrafos, textoConfirmar); _actualizarTextos = actualizarTextos;
        _confirmar = confirmar; Destructiva = destructiva; _textoCancelar = textoCancelar; AlDescartar = alDescartar;
    }

    /// <summary>Lo que hay que deshacer si la hoja se cierra SIN confirmar (el botón de cancelar, el fondo o el gesto de volver). Lo invoca la Shell al retirarla.</summary>
    public Action? AlDescartar { get; }
    /// <summary>La Shell llama a esto al retirar la hoja: corre <see cref="AlDescartar"/> una sola vez y solo si no se confirmó.</summary>
    public void Retirada() { if (_confirmada) return; _confirmada = true; AlDescartar?.Invoke(); }

    public override string Titulo => _textos.Titulo;
    public IReadOnlyList<string> Parrafos => _textos.Parrafos;
    public string TextoConfirmar => _textos.Confirmar;
    public string TextoCancelar => _textos.Cancelar ?? _textoCancelar ?? IdiomaInterfaz.T("Comun.Cancelar");
    public override void ActualizarIdioma()
    {
        if (_actualizarTextos is { } actualizar) _textos = actualizar();
        OnPropertyChanged(nameof(Titulo)); OnPropertyChanged(nameof(Parrafos));
        OnPropertyChanged(nameof(TextoConfirmar)); OnPropertyChanged(nameof(TextoCancelar));
    }
    public bool Destructiva { get; }

    [RelayCommand] private void Confirmar() { _confirmada = true; Cerrar(); _confirmar(); }
    [RelayCommand] private void Cancelar() => Cerrar();
}

/// <summary>Un texto largo en una hoja (informe de legalidad, resumen del Pokemon...), con «Copiar» y «Cerrar».</summary>
public sealed partial class InformeHojaViewModel : HojaViewModel
{
    private readonly string _titulo;
    private readonly Func<string, Task>? _copiar;
    private readonly string _resumen;
    private readonly string? _detalle;
    private readonly string _textoParaCopiar;
    private readonly IReadOnlyList<string> _lineas;
    private readonly IReadOnlyList<string>? _lineasDetalle;

    public InformeHojaViewModel(string titulo, string texto, IReadOnlyList<string>? lineas = null, Func<string, Task>? copiar = null, string? estado = null, string claseEstado = "", string? detalle = null, string? textoParaCopiar = null, bool detalleInicial = false)
    {
        _titulo = titulo; _resumen = texto; _lineas = lineas ?? [.. texto.Split('\n').Select(l => l.TrimEnd('\r'))]; _copiar = copiar; Estado = estado; ClaseEstado = claseEstado;
        _detalle = detalle; _textoParaCopiar = textoParaCopiar ?? texto;
        _lineasDetalle = detalle is null ? null : [.. detalle.Split('\n').Select(l => l.TrimEnd('\r'))];
        _detalleActivo = detalleInicial && detalle is not null;
    }

    public override string Titulo => _titulo;
    public string Texto => DetalleActivo && _detalle is not null ? _detalle : _resumen;
    public IReadOnlyList<string> Lineas => DetalleActivo && _lineasDetalle is not null ? _lineasDetalle : _lineas;
    public bool HayDetalle => _detalle is not null;
    public string TextoDetalleBoton => DetalleActivo ? IdiomaInterfaz.T("Cajas.VerResumen") : IdiomaInterfaz.T("HojasMenu.VerInformeDetallado");
    [ObservableProperty] private bool _detalleActivo;
    partial void OnDetalleActivoChanged(bool value) { OnPropertyChanged(nameof(Texto)); OnPropertyChanged(nameof(Lineas)); OnPropertyChanged(nameof(TextoDetalleBoton)); }
    [RelayCommand] private void AlternarDetalle() { if (HayDetalle) DetalleActivo = !DetalleActivo; }
    public string? Estado { get; }
    public string ClaseEstado { get; }
    public bool HayEstado => !string.IsNullOrEmpty(Estado);
    public bool EstadoOk => ClaseEstado == "ok";
    public bool EstadoError => ClaseEstado == "error";
    public bool EstadoAviso => ClaseEstado == "aviso";
    public bool PuedeCopiar => _copiar is not null;
    public int ColumnaCerrar => PuedeCopiar ? 1 : 0;
    public int ColumnasCerrar => PuedeCopiar ? 1 : 2;
    [ObservableProperty] private string _copiado = string.Empty;

    [RelayCommand]
    private async Task CopiarTexto()
    {
        if (_copiar is null) return;
        try
        {
            await _copiar(_textoParaCopiar);
            Copiado = IdiomaInterfaz.T("Ficha.CopiadoPortapapeles");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Copiado = IdiomaInterfaz.T("HojasMenu.PodidoCopiarInformeVuelve");
        }
    }
}

/// <summary>
/// Imagen de un codigo QR en una hoja (exportar el Pokemon como QR, <c>QR.cs</c> de PKHeX). Con un Pokémon (<see cref="QrHojaViewModel(string, PKM, string, Func{IPortapapeles?}, Func{ICompartirSistema?})"/>) la hoja
/// sabe regenerar el mensaje: en un Pokémon de la 7.ª generación ofrece caja, casilla y copias (<c>QREncode.GenerateQRCode7</c>, el QR que escanea el lector de la 3DS), copiar la imagen al portapapeles
/// (<c>PB_QR_Click</c>) y Compartir por la hoja del sistema. Nada se envía solo (reglas 13 y 15).
/// </summary>
public sealed partial class QrHojaViewModel : HojaViewModel
{
    private readonly string _titulo;
    private readonly string _pieBase;
    private readonly PKM? _pk;
    private readonly Func<IPortapapeles?>? _portapapeles;
    private readonly Func<ICompartirSistema?>? _compartir;
    private Avalonia.Media.Imaging.Bitmap? _imagen;

    public const int MaxCaja = 32, MaxCasilla = 30, MaxCopias = 960;   // los límites de los NumericUpDown de PKHeX (QR.Designer.cs)

    /// <summary>Un QR ya hecho con su pie (las hojas de otros bloques y las pruebas de pantalla).</summary>
    public QrHojaViewModel(string titulo, Avalonia.Media.Imaging.Bitmap imagen, string pie)
    {
        _titulo = titulo; _imagen = imagen; _pieBase = pie; _pie = pie;
    }

    /// <summary>El QR de un Pokémon, con sus controles. <paramref name="nombreEspecie"/> va en el pie.</summary>
    public QrHojaViewModel(string titulo, PKM pk, string nombreEspecie, Func<IPortapapeles?> portapapeles, Func<ICompartirSistema?> compartir)
    {
        _titulo = titulo; _pk = pk; _portapapeles = portapapeles; _compartir = compartir;
        TextosNucleoPreparar();
        _pieBase = pk.Format == 6 ? MessageStrings.MsgQRDeprecated + " " + MessageStrings.MsgQRAlternative : $"{nombreEspecie} · {pk.GetType().Name}";   // PKHeX avisa de que el QR de la 6.ª generación está obsoleto (Main.ExportQRFromTabs)
        Regenerar();
    }

    public override string Titulo => _titulo;
    public override bool PideBrilloMaximo => true;
    /// <summary>La imagen del QR. Con un Pokémon se dibuja al pedirla por primera vez (necesita el sistema gráfico), no al crear la hoja.</summary>
    public Avalonia.Media.Imaging.Bitmap? Imagen => _imagen ??= Mensaje is null ? null : Design.CodigoQr.Crear(Mensaje);
    [ObservableProperty] private string _pie;
    /// <summary>El mensaje que codifica el QR (null si la hoja se creó ya con la imagen).</summary>
    public string? Mensaje { get; private set; }
    public byte[]? Png { get; private set; }
    public bool EsQr7 => _pk is PK7;
    public bool HayAcciones => _pk is not null;
    [ObservableProperty] private string _estado = string.Empty;
    public bool HayEstado => Estado.Length > 0;
    partial void OnEstadoChanged(string value) => OnPropertyChanged(nameof(HayEstado));

    [ObservableProperty] private int _caja = 1;
    [ObservableProperty] private int _casilla = 1;
    [ObservableProperty] private int _copias = 1;
    public string CopiasAccesible => IdiomaInterfaz.P("Qr.CopiasAccesible", Copias);
    public string CajaAccesible => IdiomaInterfaz.F("Qr.CajaAccesible", Caja);
    public string CasillaAccesible => IdiomaInterfaz.F("Qr.CasillaAccesible", Casilla);
    partial void OnCajaChanged(int value) { OnPropertyChanged(nameof(CajaAccesible)); var v = Math.Clamp(value, 1, MaxCaja); if (v != value) Caja = v; else Regenerar(); }
    partial void OnCasillaChanged(int value) { OnPropertyChanged(nameof(CasillaAccesible)); var v = Math.Clamp(value, 1, MaxCasilla); if (v != value) Casilla = v; else Regenerar(); }
    partial void OnCopiasChanged(int value) { OnPropertyChanged(nameof(CopiasAccesible)); var v = Math.Clamp(value, 1, MaxCopias); if (v != value) Copias = v; else Regenerar(); }

    /// <summary>
    /// Aviso cuando se piden más copias de las que cabe en el código: <c>QR7.SetQRData</c> de este Core hace <c>num_copies = Math.Min(num_copies, 1)</c> (una sola copia, aunque el control del escritorio llegue a 960). Se muestra el
    /// aviso en vez de dejar creer que el código cambia; el mensaje es el que genera el Core, sin tocarlo (regla 12).
    /// </summary>
    public string AvisoCopias => EsQr7 && Copias > 1 ? IdiomaInterfaz.T("Qr.AvisoCopias") : string.Empty;
    public bool HayAvisoCopias => AvisoCopias.Length > 0;

    [RelayCommand] private void BajarCaja() => Caja--;
    [RelayCommand] private void SubirCaja() => Caja++;
    [RelayCommand] private void BajarCasilla() => Casilla--;
    [RelayCommand] private void SubirCasilla() => Casilla++;
    [RelayCommand] private void BajarCopias() => Copias--;
    [RelayCommand] private void SubirCopias() => Copias++;
    [RelayCommand] private void BajarCopiasDiez() => Copias -= 10;
    [RelayCommand] private void SubirCopiasDiez() => Copias += 10;

    /// <summary>Recalcula el mensaje, el PNG y el pie (<c>ReloadQRData</c> de PKHeX: <c>QREncode.GenerateQRCode7(pk7, box, slot, copies)</c>; para el resto, <c>QRMessageUtil.GetMessage(pk)</c>).</summary>
    private void Regenerar()
    {
        if (_pk is null) return;
        Mensaje = _pk is PK7 pk7 ? QRMessageUtil.GetMessage(pk7, Caja - 1, Casilla - 1, Copias) : QRMessageUtil.GetMessage(_pk);
        Png = Design.QrPng.Crear(Mensaje);
        _imagen = null;
        Pie = EsQr7 ? IdiomaInterfaz.F("Qr.Pie", _pieBase, Caja, Casilla, IdiomaInterfaz.P("Qr.Copias", Copias)) : _pieBase;
        OnPropertyChanged(nameof(AvisoCopias)); OnPropertyChanged(nameof(HayAvisoCopias));
        OnPropertyChanged(nameof(Imagen)); OnPropertyChanged(nameof(Mensaje)); OnPropertyChanged(nameof(Png));
    }

    /// <summary>Copia la imagen del QR al portapapeles. Donde el sistema no admite imágenes ahí (Android) lo dice y ofrece Compartir.</summary>
    [RelayCommand]
    public async Task CopiarImagen()
    {
        if (Png is null) return;
        if (_portapapeles?.Invoke() is not { } clip) { Estado = IdiomaInterfaz.T("Editor.PortapapelesDisponible"); return; }
        var ok = await clip.CopiarImagenAsync(Png);
        Estado = ok ? IdiomaInterfaz.T("HojasMenu.ImagenQRCopiadaPortapapeles") : MessageStrings.MsgQRClipboardFail + IdiomaInterfaz.T("HojasMenu.UsaCompartirMandarla");
    }

    /// <summary>Manda la imagen del QR a la hoja de compartir del sistema; el usuario elige la app.</summary>
    [RelayCommand]
    public void Compartir()
    {
        if (_pk is null || Png is null) return;
        if (_compartir?.Invoke() is not { } sistema) { Estado = IdiomaInterfaz.T("Cajas.CompartirDisponibleAqui"); return; }
        var archivo = Cajas.ModeloCompartir.ImagenQr(_pk, Mensaje!);
        Estado = sistema.CompartirArchivo(IdiomaInterfaz.T("Cajas.CodigoQR"), archivo.Nombre, archivo.Datos, archivo.TipoMime) ?? IdiomaInterfaz.T("HojasMenu.EligeDondeMandarCodigo");
    }

    private static void TextosNucleoPreparar() => Ajustes.Pkhex.TextosNucleo.Preparar();
}

/// <summary>Una caja en la lista de «Elegir caja»: nombre, ocupacion y fondo.</summary>
public sealed partial class FilaCaja(int indice, string nombre, int ocupadas, int total, int fondo, bool actual, Avalonia.Media.Imaging.Bitmap? miniatura) : ObservableObject
{
    public int Indice { get; } = indice;
    public string Nombre { get; } = nombre;
    public int Ocupadas { get; } = ocupadas;
    public int Total { get; } = total;
    public int Fondo { get; } = fondo;
    public bool Actual { get; } = actual;
    public Avalonia.Media.Imaging.Bitmap? Miniatura { get; } = miniatura;
    public bool HayMiniatura => Miniatura is not null;
    public string Ocupacion => $"{Ocupadas} / {Total}";
    public double Fraccion => Total == 0 ? 0 : (double)Ocupadas / Total * 100;
    public string Accesible => IdiomaInterfaz.F("HojasMenu.CajaAccesible", Nombre, Ocupadas, Total, Actual ? IdiomaInterfaz.T("HojasMenu.CajaActual") : string.Empty);
}

/// <summary>Elegir caja: la lista de TODAS las cajas con su nombre, ocupacion y fondo (la vista de «todas las cajas» de PKHeX, adaptada), con el buscador de nombres y, si se pide, para elegir destino.</summary>
public sealed partial class SelectorCajaHojaViewModel : HojaViewModel
{
    /// <summary>
    /// Texto de ejemplo del buscador de cajas: con el texto mas grande de lo normal la frase entera no cabe en una linea (la matriz lo midio a x2: 509 dp), asi que queda corto; el nombre accesible sigue siendo el largo.
    /// Antes el corte estaba en x1,3 y entre x1,0 y x1,3 la frase larga se recortaba en un movil de 360 dp (a x1,29 necesita 329 dp y caben 262): lo cazo la prueba de letra x1,29 (ADR 0105). Ahora el corte es en cuanto la letra pasa de x1,0.
    /// </summary>
    public string MarcadorBusqueda => Design.Movimiento.EscalaTexto > 1.0 ? IdiomaInterfaz.T("HojasMenu.BuscarCaja") : IdiomaInterfaz.T("SelectorCajaHoja.BuscarCajaNombreNumero");

    private readonly string _titulo;
    private readonly Action<int> _elegir;
    private readonly Action? _nombresYFondos;
    private readonly IReadOnlyList<FilaCaja> _todas;

    public SelectorCajaHojaViewModel(string titulo, IReadOnlyList<FilaCaja> cajas, Action<int> elegir, Action? nombresYFondos = null, string? subtitulo = null)
    {
        _titulo = titulo; _todas = cajas; _elegir = elegir; _nombresYFondos = nombresYFondos; Subtitulo = subtitulo;
        Visibles = cajas;
    }

    public override string Titulo => _titulo;
    public string? Subtitulo { get; }
    public bool HaySubtitulo => !string.IsNullOrWhiteSpace(Subtitulo);
    public bool HayNombresYFondos => _nombresYFondos is not null;
    [ObservableProperty] private string _filtro = string.Empty;
    [ObservableProperty] private IReadOnlyList<FilaCaja> _visibles;
    public bool SinResultados => Visibles.Count == 0;

    partial void OnFiltroChanged(string value)
    {
        var f = SelectorHojaViewModel.Normalizar(value.Trim());
        Visibles = f.Length == 0 ? _todas : [.. _todas.Where(c => SelectorHojaViewModel.Normalizar(c.Nombre).Contains(f) || (c.Indice + 1).ToString() == f)];
        OnPropertyChanged(nameof(SinResultados));
    }

    [RelayCommand] private void Elegir(FilaCaja? caja) { if (caja is null) return; Cerrar(); _elegir(caja.Indice); }
    [RelayCommand] private void NombresYFondos() { Cerrar(); _nombresYFondos?.Invoke(); }
}

/// <summary>Una operacion en curso que no se puede cancelar (ordenar o modificar cajas: el Core no es cancelable): hoja con progreso indeterminado que no se cierra con el fondo.</summary>
public sealed class TrabajandoHojaViewModel(string titulo, string texto) : HojaViewModel
{
    public override string Titulo => titulo;
    public override bool CierraConFondo => false;
    public string Texto { get; } = texto;
}

/// <summary>Un fondo de caja a elegir: nombre, miniatura y si es el actual.</summary>
public sealed partial class FilaFondo(int indice, string nombre, Avalonia.Media.Imaging.Bitmap? miniatura, bool elegido) : ObservableObject
{
    public int Indice { get; } = indice;
    public string Nombre { get; } = nombre;
    public Avalonia.Media.Imaging.Bitmap? Miniatura { get; } = miniatura;
    public bool HayMiniatura => Miniatura is not null;
    [ObservableProperty] private bool _elegido = elegido;
    public string Accesible => Elegido ? IdiomaInterfaz.F("HojasMenu.FondoActual", Nombre) : Nombre;
}

/// <summary>«Nombre y fondo de la caja»: renombrar, elegir el fondo, mover la caja de sitio y las cajas desbloqueadas. Cada cambio es una entrada del historial.</summary>
public sealed partial class DisenoCajaHojaViewModel : HojaViewModel
{
    private readonly Cajas.DisenoCajas _diseno;
    private readonly Func<int, Avalonia.Media.Imaging.Bitmap?> _miniatura;
    private readonly Action<Cajas.InformeOperacion> _resultado;
    private readonly Action<int> _cambioDeCaja;
    private readonly int _total;

    public DisenoCajaHojaViewModel(Cajas.DisenoCajas diseno, int caja, int total, Func<int, Avalonia.Media.Imaging.Bitmap?> miniatura, Func<int, int> fondoActual, Action<Cajas.InformeOperacion> resultado, Action<int> cambioDeCaja)
    {
        _fondoActual = fondoActual; _diseno = diseno; _caja = caja; _total = total; _miniatura = miniatura; _resultado = resultado; _cambioDeCaja = cambioDeCaja;
        Cargar();
    }

    public override string Titulo => IdiomaInterfaz.T("HojasMenu.NombreFondoCaja");
    public bool AdmiteNombres => _diseno.AdmiteNombres;
    public bool AdmiteFondos => _diseno.AdmiteFondos;
    public bool AdmiteDesbloqueo => _diseno.AdmiteDesbloqueo;
    public bool AdmiteMover => _diseno.AdmiteMover;
    public bool SoloLecturaDeNombres => _diseno.MuestraNombres && !_diseno.AdmiteNombres;
    public int MaxNombre => _diseno.MaxNombre;
    public int TotalCajas => _total;
    public string Posicion => IdiomaInterfaz.F("HojasMenu.Caja", Caja + 1, _total);
    public string TextoDesbloqueadas => IdiomaInterfaz.F("HojasMenu.CajasDesbloqueadas", _diseno.Desbloqueadas, _total);

    [ObservableProperty] private int _caja;
    [ObservableProperty] private string _nombre = string.Empty;
    [ObservableProperty] private IReadOnlyList<FilaFondo> _fondos = [];
    [ObservableProperty] private bool _nombreCambiado;

    partial void OnCajaChanged(int value) { OnPropertyChanged(nameof(Posicion)); }
    partial void OnNombreChanged(string value) => NombreCambiado = value.Trim() != _diseno.Nombre(Caja);

    private void Cargar()
    {
        Nombre = _diseno.Nombre(Caja);
        NombreCambiado = false;
        var actual = _diseno.AdmiteFondos ? Sesion_FondoActual() : -1;
        Fondos = [.. _diseno.NombresFondos.Select((n, i) => new FilaFondo(i, n, _miniatura(i), i == actual))];
        OnPropertyChanged(nameof(Posicion)); OnPropertyChanged(nameof(TextoDesbloqueadas));
    }

    private int Sesion_FondoActual() => _fondoActual(Caja);
    private readonly Func<int, int> _fondoActual;

    [RelayCommand] private void CajaAnterior() { Caja = (Caja + _total - 1) % _total; _cambioDeCaja(Caja); Cargar(); }
    [RelayCommand] private void CajaSiguiente() { Caja = (Caja + 1) % _total; _cambioDeCaja(Caja); Cargar(); }

    [RelayCommand]
    private void AplicarNombre()
    {
        var r = _diseno.Renombrar(Caja, Nombre);
        _resultado(r);
        Cargar();
    }

    [RelayCommand]
    private void ElegirFondo(FilaFondo? fondo)
    {
        if (fondo is null) return;
        var r = _diseno.FijarFondo(Caja, fondo.Indice);
        _resultado(r);
        Cargar();
    }

    [RelayCommand] private void MoverArriba() => Mover(-1);
    [RelayCommand] private void MoverAbajo() => Mover(+1);

    private void Mover(int delta)
    {
        var r = _diseno.Mover(Caja, delta);
        _resultado(r);
        if (r.Correcto) { Caja += delta; _cambioDeCaja(Caja); }
        Cargar();
    }

    [RelayCommand] private void MasDesbloqueadas() { _resultado(_diseno.FijarDesbloqueadas(_diseno.Desbloqueadas + 1)); Cargar(); }
    [RelayCommand] private void MenosDesbloqueadas() { _resultado(_diseno.FijarDesbloqueadas(_diseno.Desbloqueadas - 1)); Cargar(); }
}
