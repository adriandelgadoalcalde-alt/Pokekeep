// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Pokekeep.Mobile.Ui.Modelo;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Ajustes.Pkhex;
using Pokekeep.Mobile.Ui.Almacen;
using Pokekeep.Mobile.Ui.ViewModels;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Herramientas.ForzarCarga;

/// <summary>
/// «Solución de problemas &gt; Forzar carga de SAV» (<c>SaveHandlerTroubleshooter</c>; bloque 10-4): abrir un guardado que PKHeX no reconoce solo, diciendo de qué tipo es, su subversión, su idioma y su manejador
/// (cabecera o pie de un emulador o de una herramienta de copia). Las listas y la llamada al Core están en <see cref="ModeloForzarCarga"/>. El archivo se elige con el selector del sistema («Examinar…») y la partida se
/// abre SIN documento: se trabaja sobre una copia y se guarda con «Exportar copia…» (un archivo que el Core no reconoce solo no se sobrescribe).
/// </summary>
public sealed partial class ForzarCargaViewModel : HerramientaViewModel
{
    private readonly IReadOnlyList<OpcionForzar<SaveFileType>> _tipos = ModeloForzarCarga.Tipos();
    private readonly IReadOnlyList<OpcionForzar<LanguageID>> _idiomas = ModeloForzarCarga.Idiomas();
    private readonly IReadOnlyList<OpcionForzar<ISaveHandler>> _manejadores = ModeloForzarCarga.Manejadores();
    private IReadOnlyList<OpcionForzar<GameVersion>> _subversiones = [];
    private byte[]? _datos;

    public ForzarCargaViewModel(ContextoHerramienta contexto, Action volver) : base(contexto, volver)
    {
        _tipo = _tipos[0]; _idioma = _idiomas[0]; _manejador = _manejadores[0];
        ActualizarSubversiones();
    }

    public override string Titulo => IdiomaInterfaz.T("CatalogoHerramientas.ForzarCargaSAV");
    public string Subtitulo => IdiomaInterfaz.T("ForzarCarga.SolucionProblemasAbreGuardado");

    [ObservableProperty] private string _nombreArchivo = string.Empty;
    [ObservableProperty] private OpcionForzar<SaveFileType> _tipo;
    [ObservableProperty] private OpcionForzar<GameVersion>? _subversion;
    [ObservableProperty] private OpcionForzar<LanguageID> _idioma;
    [ObservableProperty] private OpcionForzar<ISaveHandler> _manejador;
    /// <summary>La tecla Ctrl de PKHeX al abrir un guardado de Gen. 3: preguntar la versión aunque la Pokédex esté bien.</summary>
    [ObservableProperty] private bool _elegirVersionGen3;
    [ObservableProperty] private string _error = string.Empty;

    public bool HayArchivo => _datos is not null;
    public string TextoArchivo => HayArchivo ? NombreArchivo : IdiomaInterfaz.T("Form.SinArchivo");
    public bool HaySubversiones => _subversiones.Count > 0;
    public bool HayError => !string.IsNullOrEmpty(Error);
    public bool PuedeContinuar => HayArchivo && NoOcupado;
    public string TextoTipo => Tipo.Detalle is null ? Tipo.Texto : $"{Tipo.Texto} · {Tipo.Detalle}";
    public string TextoSubversion => Subversion?.Texto ?? string.Empty;
    public string TextoIdioma => Idioma.Texto;
    public string TextoManejador => Manejador.Texto;

    partial void OnNombreArchivoChanged(string value) { OnPropertyChanged(nameof(TextoArchivo)); OnPropertyChanged(nameof(HayArchivo)); OnPropertyChanged(nameof(PuedeContinuar)); }
    partial void OnTipoChanged(OpcionForzar<SaveFileType> value) { OnPropertyChanged(nameof(TextoTipo)); ActualizarSubversiones(); }
    partial void OnSubversionChanged(OpcionForzar<GameVersion>? value) => OnPropertyChanged(nameof(TextoSubversion));
    partial void OnIdiomaChanged(OpcionForzar<LanguageID> value) => OnPropertyChanged(nameof(TextoIdioma));
    partial void OnManejadorChanged(OpcionForzar<ISaveHandler> value) => OnPropertyChanged(nameof(TextoManejador));
    partial void OnErrorChanged(string value) => OnPropertyChanged(nameof(HayError));

    private void ActualizarSubversiones()
    {
        _subversiones = ModeloForzarCarga.Subversiones(Tipo.Valor);
        Subversion = _subversiones.Count > 0 ? _subversiones[0] : null;   // PKHeX: SelectedIndex = 0, o la lista oculta
        OnPropertyChanged(nameof(HaySubversiones));
    }

    public IReadOnlyList<OpcionForzar<SaveFileType>> TiposDisponibles => _tipos;
    public IReadOnlyList<OpcionForzar<GameVersion>> SubversionesDisponibles => _subversiones;
    public IReadOnlyList<OpcionForzar<LanguageID>> IdiomasDisponibles => _idiomas;
    public IReadOnlyList<OpcionForzar<ISaveHandler>> ManejadoresDisponibles => _manejadores;

    [RelayCommand] private void ElegirTipo() => Selector(IdiomaInterfaz.T("ForzarCarga.TipoArchivoGuardado"), _tipos, Tipo, o => Tipo = o);
    [RelayCommand] private void ElegirSubversion() { if (HaySubversiones) Selector(IdiomaInterfaz.T("ForzarCarga.Subversion"), _subversiones, Subversion, o => Subversion = o); }
    [RelayCommand] private void ElegirIdioma() => Selector(IdiomaInterfaz.T("Catalogo.Idioma"), _idiomas, Idioma, o => Idioma = o);
    [RelayCommand] private void ElegirManejador() => Selector(IdiomaInterfaz.T("ForzarCarga.Manejador"), _manejadores, Manejador, o => Manejador = o);

    private void Selector<T>(string titulo, IReadOnlyList<OpcionForzar<T>> opciones, OpcionForzar<T>? actual, Action<OpcionForzar<T>> elegir)
    {
        var lista = opciones.ToList();
        Contexto.AbrirHoja(new SelectorHojaViewModel(titulo, [.. lista.Select((o, i) => new OpcionSelector(i, o.Texto, o.Detalle))], actual is null ? -1 : lista.IndexOf(actual), o => elegir(lista[o.Id])));
    }

    /// <summary>«Examinar…»: el archivo, con el selector del sistema (cualquier tipo, como el «Todos los archivos» de PKHeX).</summary>
    [RelayCommand]
    private async Task Examinar()
    {
        if (Contexto.Documentos() is not { } almacen) { Contexto.Informar(IdiomaInterfaz.T("ForzarCarga.ElegirArchivosDisponibleVentana"), "aviso", "CircleAlert"); return; }
        try
        {
            var doc = await almacen.ElegirParaAbrirAsync(CancellationToken.None);
            if (doc is null) return;
            UsarArchivo(await almacen.LeerAsync(doc, CancellationToken.None), doc.Nombre);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or AlmacenException)
        {
            TextosNucleo.Preparar();
            Error = $"{MessageStrings.MsgFileInUse} {ex.Message}";
        }
    }

    /// <summary>Deja elegido un archivo (también las pruebas y el gate visual).</summary>
    public void UsarArchivo(byte[] datos, string nombre)
    {
        _datos = datos; Error = string.Empty;
        NombreArchivo = nombre;
        OnPropertyChanged(nameof(HayArchivo)); OnPropertyChanged(nameof(TextoArchivo)); OnPropertyChanged(nameof(PuedeContinuar));
    }

    /// <summary>La última sesión abierta por «Continuar» (pruebas).</summary>
    public Modelo.Sesion? Abierta { get; private set; }

    /// <summary>«Continuar»: el Core con lo elegido; si el guardado es de Gen. 3 y hay que elegir versión, la hoja con las versiones; después, la partida se abre.</summary>
    [RelayCommand]
    public async Task Continuar()
    {
        TextosNucleo.Preparar();
        if (_datos is not { } datos) { Error = MessageStrings.MsgFileLoadSelectFileSave; return; }
        var r = ModeloForzarCarga.Cargar(datos, NombreArchivo, Tipo.Valor, Subversion?.Valor ?? GameVersion.Any, Idioma.Valor, Manejador.Valor, ElegirVersionGen3);
        if (!r.Hecho) { Error = r.Error ?? MessageStrings.MsgFileLoadFail; return; }
        Error = string.Empty;
        if (!r.PideVersion) { await Abrir(r.Partida!); return; }
        var aMano = ElegirVersionGen3;
        var versiones = r.VersionesGen3.ToList();
        var mensaje = $"{string.Format(MessageStrings.MsgFileLoadVersionDetect, $"3 ({NombresJuego.Corto(r.Partida!.Version)})")} {MessageStrings.MsgFileLoadSaveSelectVersion}";
        Contexto.AbrirHoja(new SelectorHojaViewModel(mensaje, [.. versiones.Select((v, i) => new OpcionSelector(i, v.Texto))], r.VersionActual, o =>
        {
            if (ModeloForzarCarga.AplicarVersion(r.Partida, versiones[o.Id].Valor, aMano) is { } elegido) _ = Abrir(elegido);
            else Error = MessageStrings.MsgFileLoadSaveLoadFail;
        }));
    }

    private async Task Abrir(SaveFile sav)
    {
        if (Contexto.AbrirSesion is not { } abrir) { Contexto.Informar(IdiomaInterfaz.T("ForzarCarga.AbrirPartidasDisponibleVentana"), "aviso", "CircleAlert"); return; }
        try
        {
            var datos = _datos!;
            await abrir(async () =>
            {
                var s = Contexto.Servicios is { } sv ? await sv.AbrirGuardadoAsync(sav, datos, NombreArchivo, CancellationToken.None) : new Modelo.Sesion(sav, NombreArchivo);
                Abierta = s;
                return s;
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or AlmacenException)
        {
            TextosNucleo.Preparar();
            Error = $"{MessageStrings.MsgFileLoadSaveLoadFail} {ex.Message}";
        }
    }

    /// <summary>Gate visual: un archivo elegido (sin leer nada) y un error de PKHeX, para dibujar los dos textos.</summary>
    public void EstadoConError()
    {
        TextosNucleo.Preparar();
        UsarArchivo(new byte[0x20000], IdiomaInterfaz.T("ForzarCarga.NombreMuestra"));
        Error = IdiomaInterfaz.F("ForzarCarga.PartidaReconocerSav", MessageStrings.MsgFileLoadSaveFail);
    }
}
