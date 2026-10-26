// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Windows.Input;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pokekeep.Mobile.Ui.Almacen;
using Pokekeep.Mobile.Ui.Modelo;
using Pokekeep.Mobile.Ui.Partida;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.ViewModels;

/// <summary>Una foto o un icono de la partida, con su exportacion a PNG.</summary>
public sealed partial class ImagenJugadorVm : ObservableObject
{
    private readonly ImagenesViewModel _vm;
    public ImagenJugadorVm(ImagenesViewModel vm, ImagenJugador datos)
    {
        _vm = vm; Datos = datos;
        var wb = new WriteableBitmap(new PixelSize(datos.Ancho, datos.Alto), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
        using (var fb = wb.Lock())
            for (var y = 0; y < datos.Alto; y++) System.Runtime.InteropServices.Marshal.Copy(datos.Pixeles, y * datos.Ancho * 4, fb.Address + y * fb.RowBytes, datos.Ancho * 4);
        Imagen = wb;
    }

    public ImagenJugador Datos { get; }
    public Bitmap Imagen { get; }
    public string Titulo => Datos.Titulo;
    public string Tamano => IdiomaInterfaz.F("Imagenes.Pixeles", Datos.Ancho, Datos.Alto);
    public string Accesible => $"{Titulo}, {Tamano}";
    public string NombreArchivo => $"{Titulo.Split('(')[0].Trim().Replace(' ', '_').ToLowerInvariant()}.png";

    [RelayCommand] private void Exportar() => _vm.Exportar(this);
}

/// <summary>Fotos e iconos del jugador de Escarlata/Púrpura y Z-A: se ven y se guardan como PNG (no se editan, como en PKHeX).</summary>
public sealed partial class ImagenesViewModel : ObservableObject
{
    private readonly Func<IAlmacenDocumentos?> _documentos;
    private readonly Action<string, string, string, string?, ICommand?> _informar;
    private readonly Action _volver;

    public ImagenesViewModel(Sesion sesion, Func<IAlmacenDocumentos?> documentos, Action<string, string, string, string?, ICommand?> informar, Action volver)
    {
        _documentos = documentos; _informar = informar; _volver = volver;
        foreach (var i in ImagenesJugador.Leer(sesion.Guardado)) Imagenes.Add(new ImagenJugadorVm(this, i));
    }

    public List<ImagenJugadorVm> Imagenes { get; } = [];
    public bool HayImagenes => Imagenes.Count > 0;
    public bool NoHayImagenes => !HayImagenes;
    public string Resumen => HayImagenes ? IdiomaInterfaz.P("Imagenes.EnLaPartida", Imagenes.Count) : IdiomaInterfaz.T("Imagenes.PartidaTieneImagenesJugador");

    public void Exportar(ImagenJugadorVm imagen) => _ = ExportarAsync(imagen);

    private async Task ExportarAsync(ImagenJugadorVm imagen)
    {
        var docs = _documentos();
        if (docs is null) { _informar(IdiomaInterfaz.T("Form.SinSelector"), "aviso", "CircleAlert", null, null); return; }
        try
        {
            var destino = await docs.ElegirDestinoAsync(imagen.NombreArchivo, CancellationToken.None);
            if (destino is null) return;
            using var ms = new MemoryStream();
            imagen.Imagen.Save(ms);
            await docs.EscribirAsync(destino, ms.ToArray(), CancellationToken.None);
            _informar(IdiomaInterfaz.F("Form.Exportado", destino.Nombre), "ok", "CircleCheck", null, null);
        }
        catch (Exception ex) { _informar(IdiomaInterfaz.T("Editor.PodidoExportar") + ex.Message, "error", "TriangleAlert", null, null); }
    }

    [RelayCommand] public void Volver() => _volver();
    public bool Atras() { _volver(); return true; }
}
