// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Almacen;
using Pokekeep.Mobile.Ui.Modelo;
using Pokekeep.Mobile.Ui.Partida;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.ViewModels;

public sealed record EncabezadoRegalos(RegalosViewModel Vm);
public sealed record CabeceraRecibidas(RegalosViewModel Vm);

/// <summary>Una ranura del album de regalos misteriosos: lo que hay (o vacia), su detalle y las acciones sobre ella.</summary>
public sealed partial class FilaRegaloVm : ObservableObject
{
    private readonly RegalosViewModel _vm;
    private bool _releyendo;

    public FilaRegaloVm(RegalosViewModel vm, int indice) { _vm = vm; Indice = indice; Releer(); }

    public int Indice { get; }
    private DataMysteryGift Regalo => _vm.Modelo.Album[Indice];
    public string Etiqueta => _vm.Modelo.EtiquetaRanura(Indice);
    public bool Vacia => Regalo.IsEmpty;
    public bool Llena => !Vacia;
    public string Contenido => DescripcionRegalo.Contenido(Regalo, _vm.Textos, _vm.Modelo.Guardado);
    public string Titulo => Vacia ? IdiomaInterfaz.T("Regalos.RanuraLibre") : DescripcionRegalo.Titulo(Regalo) is { Length: > 0 } t ? t : Regalo.Type;
    public string Detalle => string.Join("\n", DescripcionRegalo.Detalle(Regalo, _vm.Textos, _vm.Modelo.Guardado));
    public Bitmap? Imagen => Design.Sprites.Regalo(Regalo);
    public bool HayImagen => Imagen is not null;
    public bool AdmiteUsada => _vm.Modelo.AdmiteUsada && Llena;
    public bool Modificada => _vm.Modelo.Modificada(Indice);
    public string Accesible => Vacia ? IdiomaInterfaz.F("GoPark.Libre", Etiqueta) : $"{Etiqueta}, {Contenido}, {Titulo}";

    [ObservableProperty] private bool _usada;
    [ObservableProperty] private bool _detalleVisible;

    partial void OnUsadaChanged(bool value) { if (_releyendo) return; Regalo.GiftUsed = value; _vm.AlCambiar(); }

    public void Releer()
    {
        _releyendo = true;
        try { Usada = Regalo.GiftUsed; } finally { _releyendo = false; }
        foreach (var n in new[] { nameof(Vacia), nameof(Llena), nameof(Contenido), nameof(Titulo), nameof(Detalle), nameof(Imagen), nameof(HayImagen), nameof(AdmiteUsada), nameof(Modificada), nameof(Accesible) }) OnPropertyChanged(n);
    }

    [RelayCommand] private void AlternarDetalle() => DetalleVisible = !DetalleVisible;
    [RelayCommand] private void Importar() => _vm.ImportarEn(Indice);
    [RelayCommand] private void Exportar() => _vm.Exportar(Indice);
    [RelayCommand] private void Qr() => _vm.MostrarQr(Indice);
    [RelayCommand] private void Borrar() => _vm.Borrar(Indice);
}

/// <summary>Una tarjeta marcada como recibida (el juego no la volvera a dar).</summary>
public sealed class RecibidaVm(RegalosViewModel vm, int id)
{
    public int Id { get; } = id;
    public string Texto => IdiomaInterfaz.F("Regalos.TarjetaN", Id);
    public string Accesible => IdiomaInterfaz.F("Regalos.TarjetaRecibidaNumero", Id);
    public ICommand QuitarCommand { get; } = new RelayCommand(() => vm.QuitarRecibida(id));
}

/// <summary>
/// Regalos misteriosos (<c>SAV_Wondercard</c>): el album de la partida con su contenido, importar/exportar tarjetas, borrar, marcar usadas y la lista de tarjetas recibidas. Trabaja sobre un borrador
/// (<see cref="ModeloRegalos"/>); «Aplicar» lo escribe como UNA entrada del historial.
/// </summary>
public sealed partial class RegalosViewModel : ObservableObject
{
    private readonly Sesion _sesion;
    private readonly Func<IAlmacenDocumentos?> _documentos;
    private readonly Action<HojaViewModel> _abrirHoja;
    private readonly Action<string, string, string, string?, ICommand?> _informar;
    private readonly Action _volver;
    private bool _aplicando;
    private readonly EncabezadoRegalos _encabezado;
    private readonly CabeceraRecibidas _cabeceraRecibidas;

    private readonly Action<Pokekeep.Mobile.Ui.Cajas.EditorCasillas.TipoCambio> _alCambiarCasillas;   // referencia fuerte de la suscripcion debil a Sesion.CasillasCambiadas
    public RegalosViewModel(Sesion sesion, Func<IAlmacenDocumentos?> documentos, Action<HojaViewModel> abrirHoja, Action<string, string, string, string?, ICommand?> informar, Action volver)
    {
        _sesion = sesion; _documentos = documentos; _abrirHoja = abrirHoja; _informar = informar; _volver = volver;
        _encabezado = new(this); _cabeceraRecibidas = new(this);
        Modelo = new ModeloRegalos(sesion);
        Reconstruir();
        _alCambiarCasillas = _ => { if (!_aplicando) Recargar(); };
        sesion.SuscribirCasillasDebil(_alCambiarCasillas);
    }

    public ModeloRegalos Modelo { get; private set; }
    public GameStrings Textos => _sesion.Textos;
    public ObservableCollection<object> Elementos { get; } = [];
    [ObservableProperty] private bool _hayCambios;
    [ObservableProperty] private string _numeroNuevo = string.Empty;
    [ObservableProperty] private string? _errorNumero;

    public bool TieneBanderas => Modelo.TieneBanderas;
    public bool AdmiteUsada => Modelo.AdmiteUsada;
    public string Resumen
    {
        get
        {
            var llenas = Modelo.Album.Count(g => !g.IsEmpty);
            return IdiomaInterfaz.F("Regalos.RanurasTarjeta", llenas, Modelo.Album.Length) + (Modelo.TieneBanderas ? IdiomaInterfaz.F("Regalos.Recibidas", Modelo.Recibidas.Count) : string.Empty);
        }
    }
    public string TextoCambios => HayCambios ? IdiomaInterfaz.T("Regalos.CambiosRegalosAplicarPartida") : IdiomaInterfaz.T("Mochila.SinCambios");
    public bool HayErrorNumero => !string.IsNullOrEmpty(ErrorNumero);
    public string MaximoTexto => TieneBanderas ? IdiomaInterfaz.F("Regalos.Entre", Modelo.MaximoBanderas - 1) : string.Empty;
    partial void OnHayCambiosChanged(bool value) => OnPropertyChanged(nameof(TextoCambios));
    partial void OnErrorNumeroChanged(string? value) => OnPropertyChanged(nameof(HayErrorNumero));

    private void Reconstruir()
    {
        Elementos.Clear();
        Elementos.Add(_encabezado);
        for (var i = 0; i < Modelo.Album.Length; i++) Elementos.Add(new FilaRegaloVm(this, i));
        if (Modelo.TieneBanderas)
        {
            Elementos.Add(_cabeceraRecibidas);
            foreach (var id in Modelo.Recibidas) Elementos.Add(new RecibidaVm(this, id));
        }
        HayCambios = Modelo.HayCambios;
        OnPropertyChanged(nameof(Resumen)); OnPropertyChanged(nameof(TieneBanderas)); OnPropertyChanged(nameof(MaximoTexto)); OnPropertyChanged(nameof(AdmiteUsada));
    }

    private void RefrescarFilas()
    {
        foreach (var f in Elementos.OfType<FilaRegaloVm>()) f.Releer();
        foreach (var r in Elementos.OfType<RecibidaVm>().ToList()) Elementos.Remove(r);
        if (Modelo.TieneBanderas) foreach (var id in Modelo.Recibidas) Elementos.Add(new RecibidaVm(this, id));
        HayCambios = Modelo.HayCambios;
        OnPropertyChanged(nameof(Resumen)); OnPropertyChanged(nameof(AdmiteUsada));
    }

    public void AlCambiar() { HayCambios = Modelo.HayCambios; OnPropertyChanged(nameof(Resumen)); }

    private void Recargar() { Modelo = new ModeloRegalos(_sesion); Reconstruir(); }

    // ---- Importar y exportar ----
    /// <summary>Importa la tarjeta de un archivo a esa ranura (o a la primera libre de su tipo, como el formulario).</summary>
    public void ImportarEn(int indice) => _ = ImportarAsync(indice);

    [RelayCommand] private void ImportarPrimera() => _ = ImportarAsync(-1);

    private async Task ImportarAsync(int indice)
    {
        var docs = _documentos();
        if (docs is null) { _informar(IdiomaInterfaz.T("Form.SinSelector"), "aviso", "CircleAlert", null, null); return; }
        TarjetaLeida leida;
        try
        {
            var doc = await docs.ElegirParaAbrirAsync(CancellationToken.None);
            if (doc is null) return;
            var datos = await docs.LeerAsync(doc, CancellationToken.None);
            leida = ImportadorRegalos.Leer(datos, doc.Nombre, _sesion.Guardado);
        }
        catch (Exception ex) { _informar(IdiomaInterfaz.T("Editor.PodidoLeerArchivo") + ex.Message, "error", "TriangleAlert", null, null); return; }
        try { Colocar(leida, indice); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IndexOutOfRangeException or FormatException or OverflowException or KeyNotFoundException or NotSupportedException)
        {
            // La tarea corre sin esperarla nadie: una excepcion aqui se perderia o tumbaria la aplicacion.
            _informar(IdiomaInterfaz.F("Regalos.PodidoColocarTarjetaAlbum", ex.Message), "error", "TriangleAlert", null, null);
        }
    }

    /// <summary>Lo que se hace con una tarjeta leida (separado del selector para poder probarlo): se coloca en el borrador con las mismas comprobaciones del formulario.</summary>
    public void Colocar(TarjetaLeida leida, int indice)
    {
        if (leida.Regalo is not { } g) { _informar(leida.Motivo ?? IdiomaInterfaz.T("Regalos.TarjetaValida"), "error", "TriangleAlert", null, null); return; }
        var destino = indice;
        if (destino < 0)
        {
            destino = Array.FindIndex(Modelo.Album, a => a.IsEmpty && a.Type == g.Type);
            // Una tarjeta grande (PCD) que cabe como pequena (PGT) puede ir a una ranura de PGT, como en el formulario.
            if (destino < 0 && g is PCD { CanConvertToPGT: true }) destino = Array.FindIndex(Modelo.Album, a => a.IsEmpty && a is PGT);
            if (destino < 0) { _informar(IdiomaInterfaz.F("Regalos.QuedaNingunaRanuraLibre", g.Type), "error", "TriangleAlert", null, null); return; }
        }
        var (idx, rechazo) = Modelo.Colocar(g, destino);
        if (rechazo is not null) { _informar(rechazo, "error", "TriangleAlert", null, null); return; }
        RefrescarFilas();
        _informar(IdiomaInterfaz.F("Regalos.TarjetaColocadaAplicaCambios", Modelo.EtiquetaRanura(idx)), "ok", "CircleCheck", null, null);
    }

    public void Exportar(int indice) => _ = ExportarAsync(indice);

    private async Task ExportarAsync(int indice)
    {
        var docs = _documentos();
        if (docs is null) { _informar(IdiomaInterfaz.T("Form.SinSelector"), "aviso", "CircleAlert", null, null); return; }
        try
        {
            var g = Modelo.Album[indice];
            if (g.IsEmpty) return;
            // ExportMGDialog: nombre limpio (PathUtil.CleanFileName) con la extensión de su tipo, que es la del filtro de su contexto (GetMysterGiftFilter, PK-IO-026).
            var destino = await docs.ElegirDestinoAsync(Pokekeep.Mobile.Ui.Herramientas.BaseDatos.FormatosRegalo.NombreArchivo(g), CancellationToken.None);
            if (destino is null) return;
            await docs.EscribirAsync(destino, g.Write().ToArray(), CancellationToken.None);
            _informar(IdiomaInterfaz.F("Regalos.Exportado", destino.Nombre, Pokekeep.Mobile.Ui.Herramientas.BaseDatos.FormatosRegalo.Filtro(g.Context)), "ok", "CircleCheck", null, null);
        }
        catch (Exception ex) { _informar(IdiomaInterfaz.T("Editor.PodidoExportar") + ex.Message, "error", "TriangleAlert", null, null); }
    }

    /// <summary>
    /// El código QR de la tarjeta (<c>ExportQRFromView</c>, PK-IO-056): <c>QRMessageUtil.GetMessage(DataMysteryGift)</c>, el mismo texto que <c>QREncode.GenerateQRCode</c>, con la descripción de PKHeX debajo.
    /// Leer un QR (Alt+clic con una URL en el portapapeles) no se ofrece: PKHeX lo descodifica con un servicio web, y la app no usa la red.
    /// </summary>
    public void MostrarQr(int indice)
    {
        var g = Modelo.Album[indice];
        if (Pokekeep.Mobile.Ui.Herramientas.BaseDatos.FormatosRegalo.MotivoSinQr(g, _sesion.Guardado) is { } motivo) { _informar(motivo, "aviso", "CircleAlert", null, null); return; }
        var imagen = Pokekeep.Mobile.Ui.Design.CodigoQr.TryCrear(Pokekeep.Mobile.Ui.Herramientas.BaseDatos.FormatosRegalo.MensajeQr(g), out var error);
        if (imagen is null) { _informar(IdiomaInterfaz.T("Cajas.PodidoCrearCodigoQR") + error, "error", "TriangleAlert", null, null); return; }
        _abrirHoja(new QrHojaViewModel(IdiomaInterfaz.T("Regalos.CodigoQRTarjeta"), imagen, Pokekeep.Mobile.Ui.Herramientas.BaseDatos.FormatosRegalo.DescripcionQr(g, Textos)));
    }

    public void Borrar(int indice)
    {
        Modelo.Borrar(indice);
        RefrescarFilas();
    }

    [RelayCommand] private void MarcarUsadas() { Modelo.MarcarTodas(true); RefrescarFilas(); }
    [RelayCommand] private void MarcarSinUsar() { Modelo.MarcarTodas(false); RefrescarFilas(); }

    // ---- Recibidas ----
    public void QuitarRecibida(int id) { Modelo.QuitarRecibida(id); RefrescarFilas(); }

    [RelayCommand]
    private void AnadirRecibida()
    {
        if (!int.TryParse(NumeroNuevo.Trim(), out var id)) { ErrorNumero = IdiomaInterfaz.T("Regalos.EscribeNumeroTarjeta"); return; }
        var motivo = Modelo.AnadirRecibida(id);
        if (motivo is not null) { ErrorNumero = motivo; return; }
        ErrorNumero = null; NumeroNuevo = string.Empty;
        RefrescarFilas();
    }

    // ---- Aplicar ----
    [RelayCommand]
    private void Aplicar()
    {
        _aplicando = true;
        try
        {
            var inf = Modelo.Aplicar();
            if (inf.Aplicado) _informar(IdiomaInterfaz.T("Regalos.RegalosAplicadosPartidaPuedes"), "ok", "CircleCheck", null, null);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IndexOutOfRangeException or FormatException or OverflowException or KeyNotFoundException or NotSupportedException)
        {
            _informar(IdiomaInterfaz.F("Eventos.JuegoAdmitioCambioPartida", ex.Message), "error", "TriangleAlert", null, null);
        }
        finally { _aplicando = false; }
        Reconstruir();
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
        _abrirHoja(new ConfirmarHojaViewModel(IdiomaInterfaz.T("Form.Salir.Titulo"), [IdiomaInterfaz.T("Regalos.HasCambiadoRegalosMisteriosos"), IdiomaInterfaz.T("Form.Salir.Explicacion")], IdiomaInterfaz.T("Form.Salir.Confirmar"), _volver, destructiva: true, textoCancelar: IdiomaInterfaz.T("Form.Salir.Cancelar")));
    }

    public bool Atras() { Volver(); return true; }
}
