// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Almacen;
using Pokekeep.Mobile.Ui.Modelo;
using Pokekeep.Mobile.Ui.Partida;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.ViewModels;

public sealed record EncabezadoGoPark(GoParkViewModel Vm);

/// <summary>Un hueco del Parque GO: lo que hay y sus acciones (importar, exportar, vaciar).</summary>
public sealed partial class FilaGoVm : ObservableObject
{
    private readonly GoParkViewModel _vm;
    public FilaGoVm(GoParkViewModel vm, int indice) { _vm = vm; Indice = indice; }

    public int Indice { get; }
    private GP1 Datos => _vm.Modelo.Leer(Indice);
    public string Etiqueta => ModeloGoPark.Etiqueta(Indice);
    public bool Vacio => Datos.Species == 0;
    public bool Ocupado => !Vacio;
    public string Titulo => Vacio ? IdiomaInterfaz.T("Mochila.HuecoLibre") : $"{(Datos.Species < _vm.Textos.Species.Count ? _vm.Textos.Species[Datos.Species] : IdiomaInterfaz.F("DescripcionRegalo.Especie", Datos.Species))} Nv. {Datos.Level}{(Datos.IsShiny ? " ★" : string.Empty)}";
    public string Detalle => Vacio ? string.Empty : $"PC {Datos.CP} · {(Datos.Gender == 0 ? IdiomaInterfaz.T("Form.Trainer8.Male") : Datos.Gender == 1 ? IdiomaInterfaz.T("Form.Trainer8.Female") : IdiomaInterfaz.T("GoPark.Genero"))} · {Datos.Username1}";
    public bool Modificado => _vm.Modelo.Modificado(Indice);
    public string Accesible => Vacio ? IdiomaInterfaz.F("GoPark.Libre", Etiqueta) : $"{Etiqueta}, {Titulo}";

    public void Releer() { foreach (var n in new[] { nameof(Vacio), nameof(Ocupado), nameof(Titulo), nameof(Detalle), nameof(Modificado), nameof(Accesible) }) OnPropertyChanged(n); }

    [RelayCommand] private void Importar() => _vm.ImportarEn(Indice);
    [RelayCommand] private void Exportar() => _vm.Exportar(Indice);
    [RelayCommand] private void Vaciar() => _vm.Vaciar(Indice);
}

/// <summary>Complejo GO de Let's Go: los huecos del Parque GO con importar/exportar .gp1, vaciar y el resumen. Trabaja sobre un borrador (<see cref="ModeloGoPark"/>).</summary>
public sealed partial class GoParkViewModel : ObservableObject
{
    private readonly Sesion _sesion;
    private readonly Func<IAlmacenDocumentos?> _documentos;
    private readonly Func<IPortapapeles?> _portapapeles;
    private readonly Action<HojaViewModel> _abrirHoja;
    private readonly Action<string, string, string, string?, ICommand?> _informar;
    private readonly Action _volver;
    private bool _aplicando;
    private readonly EncabezadoGoPark _encabezado;
    private readonly List<FilaGoVm> _filas = [];

    private readonly Action<Pokekeep.Mobile.Ui.Cajas.EditorCasillas.TipoCambio> _alCambiarCasillas;   // referencia fuerte de la suscripcion debil a Sesion.CasillasCambiadas
    public GoParkViewModel(Sesion sesion, Func<IAlmacenDocumentos?> documentos, Func<IPortapapeles?> portapapeles, Action<HojaViewModel> abrirHoja, Action<string, string, string, string?, ICommand?> informar, Action volver)
    {
        _sesion = sesion; _documentos = documentos; _portapapeles = portapapeles; _abrirHoja = abrirHoja; _informar = informar; _volver = volver;
        _encabezado = new(this);
        Modelo = new ModeloGoPark(sesion);
        for (var i = 0; i < ModeloGoPark.Huecos; i++) _filas.Add(new FilaGoVm(this, i));
        Elementos.PonerCabecera(_encabezado);
        Rellenar();
        _alCambiarCasillas = _ => { if (!_aplicando) Recargar(); };
        sesion.SuscribirCasillasDebil(_alCambiarCasillas);
    }

    public ModeloGoPark Modelo { get; private set; }
    public GameStrings Textos => _sesion.Textos;
    public ColeccionConCabecera Elementos { get; } = [];
    [ObservableProperty] private string _filtro = string.Empty;
    [ObservableProperty] private bool _soloOcupados = true;
    [ObservableProperty] private bool _hayCambios;
    [ObservableProperty] private string _resumen = string.Empty;
    public string TextoCambios => HayCambios ? IdiomaInterfaz.T("GoPark.CambiosParqueGOAplicar") : IdiomaInterfaz.T("Mochila.SinCambios");
    partial void OnHayCambiosChanged(bool value) => OnPropertyChanged(nameof(TextoCambios));
    partial void OnFiltroChanged(string value) => Rellenar();
    partial void OnSoloOcupadosChanged(bool value) => Rellenar();

    public void Rellenar()
    {
        var q = SelectorHojaViewModel.Normalizar(Filtro.Trim());
        var lista = new List<object>();
        foreach (var f in _filas)
        {
            f.Releer();
            if (SoloOcupados && f.Vacio) continue;
            if (q.Length > 0 && !SelectorHojaViewModel.Normalizar(f.Titulo + " " + f.Etiqueta).Contains(q)) continue;
            lista.Add(f);
        }
        Elementos.ReemplazarCola(lista);
        Resumen = IdiomaInterfaz.F("GoPark.HuecosOcupadosVen", Modelo.Ocupados, ModeloGoPark.Huecos, lista.Count) + (lista.Count > 0 ? string.Empty : Modelo.Ocupados == 0 ? IdiomaInterfaz.T("GoPark.ComplejoGOVacioImportar") : IdiomaInterfaz.T("GoPark.NingunHuecoCoincideFiltro"));
        HayCambios = Modelo.HayCambios;
    }

    private void Recargar() { Modelo = new ModeloGoPark(_sesion); Rellenar(); }

    public void ImportarEn(int indice) => _ = ImportarAsync(indice);
    [RelayCommand] private void ImportarPrimero() => _ = ImportarAsync(-1);

    private async Task ImportarAsync(int indice)
    {
        var docs = _documentos();
        if (docs is null) { _informar(IdiomaInterfaz.T("Form.SinSelector"), "aviso", "CircleAlert", null, null); return; }
        try
        {
            var doc = await docs.ElegirParaAbrirAsync(CancellationToken.None);
            if (doc is null) return;
            var datos = await docs.LeerAsync(doc, CancellationToken.None);
            Colocar(datos, indice);
        }
        catch (Exception ex) { _informar(IdiomaInterfaz.T("Editor.PodidoLeerArchivo") + ex.Message, "error", "TriangleAlert", null, null); }
    }

    /// <summary>Lo que se hace con los bytes de un .gp1 (separado del selector para poder probarlo).</summary>
    public void Colocar(byte[] datos, int indice)
    {
        var destino = indice >= 0 ? indice : Modelo.PrimerLibre();
        if (destino < 0) { _informar(IdiomaInterfaz.T("GoPark.ParqueGOTieneNingun"), "error", "TriangleAlert", null, null); return; }
        var motivo = Modelo.Importar(destino, datos);
        if (motivo is not null) { _informar(motivo, "error", "TriangleAlert", null, null); return; }
        Rellenar();
        _informar(IdiomaInterfaz.F("GoPark.PokemonGOColocadoAplica", ModeloGoPark.Etiqueta(destino)), "ok", "CircleCheck", null, null);
    }

    public void Exportar(int indice) => _ = ExportarAsync(indice);

    private async Task ExportarAsync(int indice)
    {
        var docs = _documentos();
        if (docs is null) { _informar(IdiomaInterfaz.T("Form.SinSelector"), "aviso", "CircleAlert", null, null); return; }
        try
        {
            var g = Modelo.Leer(indice);
            if (g.Species == 0) return;
            var destino = await docs.ElegirDestinoAsync(g.FileName, CancellationToken.None);
            if (destino is null) return;
            await docs.EscribirAsync(destino, g.Data.ToArray(), CancellationToken.None);
            _informar(IdiomaInterfaz.F("Form.Exportado", destino.Nombre), "ok", "CircleCheck", null, null);
        }
        catch (Exception ex) { _informar(IdiomaInterfaz.T("Editor.PodidoExportar") + ex.Message, "error", "TriangleAlert", null, null); }
    }

    public void Vaciar(int indice) { Modelo.Vaciar(indice); Rellenar(); }

    /// <summary>R-015 (ADR 0107): una acción se hace al pulsarla, sin «¿Vaciar?»; se aplica en el momento y el aviso lleva «Deshacer».</summary>
    [RelayCommand]
    private void VaciarTodo() { Modelo.VaciarTodo(); Rellenar(); AplicarPendientes(); }

    [RelayCommand]
    private async Task CopiarResumen()
    {
        var p = _portapapeles();
        if (p is null) return;
        var lineas = Enumerable.Range(0, ModeloGoPark.Huecos).Where(Modelo.Ocupado).Select(i => Modelo.Leer(i).Dump(Textos.Species, i)).ToList();
        if (lineas.Count == 0) { _informar(IdiomaInterfaz.T("GoPark.PokemonGOResumir"), "aviso", "CircleAlert", null, null); return; }
        var ok = await p.CopiarAsync(string.Join(Environment.NewLine, lineas));
        _informar(ok ? IdiomaInterfaz.T("Eventos.ResumenCopiadoPortapapeles") : IdiomaInterfaz.T("Eventos.PodidoCopiarPortapapeles"), ok ? "ok" : "error", ok ? "CircleCheck" : "TriangleAlert", null, null);
    }

    /// <summary>R-015: el «Deshacer» de la partida para el aviso de aplicado (lo pone PartidaViewModel).</summary>
    public Func<ICommand?>? Deshacer { get; init; }
    private ICommand? CmdDeshacer => Deshacer?.Invoke();
    private string? TextoDeshacer => CmdDeshacer is null ? null : IdiomaInterfaz.T("SubeditorHoja.Deshacer");

    [RelayCommand]
    private void Aplicar() => AplicarPendientes();

    /// <summary>R-015: aplica el borrador si lo hay. False solo si el juego lo rechaza (el borrador se conserva).</summary>
    public bool AplicarPendientes()
    {
        if (!Modelo.HayCambios) return true;
        var ok = true;
        _aplicando = true;
        try { if (Modelo.Aplicar().Aplicado) _informar(IdiomaInterfaz.T("GoPark.ParqueGOAplicadoPartida"), "ok", "CircleCheck", TextoDeshacer, CmdDeshacer); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IndexOutOfRangeException or FormatException or OverflowException or KeyNotFoundException or NotSupportedException)
        {
            ok = false;
            _informar(IdiomaInterfaz.F("Eventos.JuegoAdmitioCambioPartida", ex.Message), "error", "TriangleAlert", null, null);
        }
        finally { _aplicando = false; }
        Rellenar();
        return ok;
    }

    [RelayCommand]
    private void Descartar() { Modelo.Descartar(); Rellenar(); _informar(IdiomaInterfaz.T("Form.Descartados"), "ok", "CircleCheck", null, null); }

    /// <summary>Vuelve al menu de Partida aplicando antes lo cambiado (R-015, 15-nov-2026: sin preguntar; el aviso lleva «Deshacer»). Si el juego lo rechaza, se queda.</summary>
    [RelayCommand]
    public void Volver() { if (AplicarPendientes()) _volver(); }

    public bool Atras() { Volver(); return true; }
}
