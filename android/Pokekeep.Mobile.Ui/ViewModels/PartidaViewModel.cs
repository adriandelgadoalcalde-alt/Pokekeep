// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Windows.Input;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;
using Pokekeep.Mobile.Ui.Partida;
using Pokekeep.Mobile.Ui.Partida.Catalogos;
using Pokekeep.Mobile.Ui.Partida.Formularios;
using SesionM = Pokekeep.Mobile.Ui.Modelo.Sesion;

namespace Pokekeep.Mobile.Ui.ViewModels;

public sealed record ResultadoPartida(string Titulo, string Descripcion, ICommand Abrir);

/// <summary>Un editor de la lista de Partida: titulo, que hace y, si no existe en este juego, POR QUE. Nunca un boton muerto sin explicacion.</summary>
public sealed class EntradaEditorVm(string id, string titulo, string descripcion, string glifo, string? motivo, ICommand comando) : ObservableObject
{
    public string Id { get; } = id;
    public string Titulo { get; private set; } = titulo;
    public string Descripcion { get; private set; } = descripcion;
    public string Glifo { get; } = glifo;
    public string? Motivo { get; private set; } = motivo;
    public ICommand Comando { get; } = comando;
    public bool Aplica => Motivo is null;
    public bool NoAplica => Motivo is not null;
    public string Linea => Motivo ?? Descripcion;
    public string Accesible => Aplica ? $"{Titulo}. {Descripcion}" : IdiomaInterfaz.F("Partida.NoDisponibleAccesible", Titulo, Motivo);

    internal void ActualizarTextos(EntradaEditorVm nueva)
    {
        if (Id != nueva.Id || Glifo != nueva.Glifo) throw new InvalidOperationException("El identificador del editor cambió al traducir");
        Titulo = nueva.Titulo; Descripcion = nueva.Descripcion; Motivo = nueva.Motivo;
        foreach (var propiedad in new[] { nameof(Titulo), nameof(Descripcion), nameof(Motivo), nameof(Aplica), nameof(NoAplica), nameof(Linea), nameof(Accesible) }) OnPropertyChanged(propiedad);
    }
}

/// <summary>
/// Destino «Partida»: la tarjeta de cambios sin guardar (con Guardar y Exportar), las acciones de archivo y los editores del progreso del juego que aplican al guardado abierto (Entrenador, Pokedex, Regalos misteriosos,
/// Eventos y progreso...). Cada editor se abre dentro de este mismo destino y vuelve al menu con «Volver».
/// </summary>
public sealed partial class PartidaViewModel : ObservableObject
{
    [ObservableProperty] private string _filtroHerramientas = string.Empty;
    [ObservableProperty] private IReadOnlyList<ResultadoPartida> _resultadosBusqueda = [];
    partial void OnFiltroHerramientasChanged(string value)
    {
        var q = SelectorHojaViewModel.Normalizar(value.Trim());
        if (q.Length == 0) { ResultadosBusqueda = []; return; }
        var resultados = Editores.Concat(Especificos).Where(e => e.Aplica && SelectorHojaViewModel.Normalizar(e.Titulo + " " + e.Descripcion).Contains(q))
            .Select(e => new ResultadoPartida(e.Titulo, e.Descripcion, new RelayCommand(() => e.Comando.Execute(e.Id)))).ToList();
        if (_sesion?.Guardado is SAV4Sinnoh && NombreReloj.Coincide(q))
            resultados.Insert(0, new ResultadoPartida(NombreReloj.Oficial, IdiomaInterfaz.T("Partida.RelojDescripcion"), new RelayCommand(() =>
            {
                if (AbrirEditor("esp:misc")) Editor?.IrAPestana("poketch");
            })));
        ResultadosBusqueda = resultados;
    }
    private readonly SesionM? _sesion;
    private readonly Func<Almacen.IAlmacenDocumentos?> _documentos;
    private readonly Func<IPortapapeles?> _portapapeles;
    private readonly Func<string?>? _borradores;
    private readonly Action<HojaViewModel> _abrirHoja;

    /// <summary>Aviso de la tarjeta de cambios: lo que tiene un borrador sin aplicar (la Mochila) y por tanto NO entra en Guardar ni en Exportar; null si no hay.</summary>
    [ObservableProperty] private string? _textoBorradores;
    public bool HayBorradores => TextoBorradores is not null;
    partial void OnTextoBorradoresChanged(string? value) => OnPropertyChanged(nameof(HayBorradores));
    public void ActualizarBorradores() => TextoBorradores = _borradores?.Invoke() is { } b ? IdiomaInterfaz.F("Partida.Borradores", b) : null;
    private readonly Action<string, string, string, string?, ICommand?> _informar;

    public PartidaViewModel(SesionM? sesion, IReadOnlyList<EntradaMenu> accionesArchivo, ICommand? guardar, ICommand? exportar, Action<HojaViewModel> abrirHoja, Action<string, string, string, string?, ICommand?> informar, Func<Almacen.IAlmacenDocumentos?>? documentos = null, Func<IPortapapeles?>? portapapeles = null, Func<string?>? borradores = null)
    {
        _borradores = borradores;
        _documentos = documentos ?? (() => null);
        _portapapeles = portapapeles ?? (() => null);
        _sesion = sesion; AccionesArchivo = accionesArchivo; GuardarCommand = guardar; ExportarCommand = exportar; _abrirHoja = abrirHoja; _informar = informar;
        if (sesion is not null)
        {
            sesion.PropertyChanged += (_, e) => { if (e.PropertyName is nameof(SesionM.CambiosPendientes) or nameof(SesionM.EstaLimpia)) RefrescarCambios(); };
        }
        Editores = sesion is null ? [] : new(Construir(sesion));
        Especificos = sesion is null ? [] : new(ConstruirEspecificos(sesion));
    }

    public void ActualizarIdioma()
    {
        if (_sesion is not null)
        {
            ActualizarEntradas(Editores, Construir(_sesion));
            ActualizarEntradas(Especificos, ConstruirEspecificos(_sesion));
        }
        foreach (var accion in AccionesArchivo) accion.ActualizarIdioma();
        Editor?.ActualizarIdioma();
        ActualizarBorradores(); OnFiltroHerramientasChanged(FiltroHerramientas);
        OnPropertyChanged(nameof(Archivo)); OnPropertyChanged(nameof(Plataforma)); OnPropertyChanged(nameof(TextoCambios)); OnPropertyChanged(nameof(HayEspecificos));
    }

    private static void ActualizarEntradas(ObservableCollection<EntradaEditorVm> destino, IReadOnlyList<EntradaEditorVm> nuevas)
    {
        var ids = nuevas.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        for (var i = destino.Count - 1; i >= 0; i--) if (!ids.Contains(destino[i].Id)) destino.RemoveAt(i);
        for (var i = 0; i < nuevas.Count; i++)
        {
            var existente = destino.FirstOrDefault(e => e.Id == nuevas[i].Id);
            if (existente is null) destino.Insert(i, nuevas[i]);
            else
            {
                existente.ActualizarTextos(nuevas[i]);
                var antes = destino.IndexOf(existente);
                if (antes != i) destino.Move(antes, i);
            }
        }
    }

    public IReadOnlyList<EntradaMenu> AccionesArchivo { get; }
    public ICommand? GuardarCommand { get; }
    public ICommand? ExportarCommand { get; }
    public ObservableCollection<EntradaEditorVm> Editores { get; }
    /// <summary>«Específicos del juego»: SOLO los formularios que PKHeX enseña para este formato (regla de <c>ToggleViewSubEditors</c>) y que la app ya sabe editar.</summary>
    public ObservableCollection<EntradaEditorVm> Especificos { get; }
    public bool HayEspecificos => Especificos.Count > 0;
    public bool HaySesion => _sesion is not null;
    public bool NoHaySesion => _sesion is null;
    public string Archivo => _sesion?.NombreArchivo ?? IdiomaInterfaz.T("Partida.SinPartida");
    public string Plataforma => _sesion is null ? string.Empty : $"{_sesion.JuegoCorto} · {_sesion.Plataforma}";
    public int Cambios => _sesion?.CambiosPendientes ?? 0;
    public bool HayCambios => Cambios > 0;
    public bool NoHayCambios => !HayCambios;
    public string TextoCambios => Cambios == 0 ? IdiomaInterfaz.T("Partida.TodoGuardado") : IdiomaInterfaz.P("Partida.Cambios", Cambios);

    [ObservableProperty] private EditorPartidaViewModel? _editor;
    [ObservableProperty] private RegalosViewModel? _regalos;
    [ObservableProperty] private EventosViewModel? _eventos;
    [ObservableProperty] private GoParkViewModel? _goPark;
    public bool EnGoPark => GoPark is not null;
    partial void OnGoParkChanged(GoParkViewModel? value) => NotificarPantalla();
    [ObservableProperty] private ImagenesViewModel? _imagenes;
    public bool EnImagenes => Imagenes is not null;
    partial void OnImagenesChanged(ImagenesViewModel? value) => NotificarPantalla();
    public bool EnMenu => Editor is null && Regalos is null && Eventos is null && GoPark is null && Imagenes is null;
    public bool EnEditor => !EnMenu;
    public bool EnEventos => Eventos is not null;
    public bool EnEditorDeCampos => Editor is not null;
    public bool EnRegalos => Regalos is not null;
    partial void OnEditorChanged(EditorPartidaViewModel? value) => NotificarPantalla();
    partial void OnRegalosChanged(RegalosViewModel? value) => NotificarPantalla();
    partial void OnEventosChanged(EventosViewModel? value) => NotificarPantalla();
    private void NotificarPantalla() { OnPropertyChanged(nameof(EnMenu)); OnPropertyChanged(nameof(EnEditor)); OnPropertyChanged(nameof(EnEditorDeCampos)); OnPropertyChanged(nameof(EnRegalos)); OnPropertyChanged(nameof(EnEventos)); OnPropertyChanged(nameof(EnGoPark)); OnPropertyChanged(nameof(EnImagenes)); }

    private void RefrescarCambios()
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            OnPropertyChanged(nameof(Cambios)); OnPropertyChanged(nameof(HayCambios)); OnPropertyChanged(nameof(NoHayCambios)); OnPropertyChanged(nameof(TextoCambios));
        });
    }

    private IReadOnlyList<EntradaEditorVm> Construir(SesionM sesion)
    {
        var sav = sesion.Guardado;
        var l = new List<EntradaEditorVm>();
        var abrir = new RelayCommand<string>(id => AbrirEditor(id));
        EntradaEditorVm E(string id, string titulo, string desc, string glifo, string? motivo = null) => new(id, titulo, desc, glifo, motivo, abrir);
        l.Add(E("entrenador", IdiomaInterfaz.T("Partida.Editor.entrenador.Titulo"), IdiomaInterfaz.T("Partida.Editor.entrenador.Descripcion"), "User", CatalogoPartida.NoAplicaEntrenador(sav)));
        l.Add(E("pokedex", IdiomaInterfaz.T("Partida.Editor.pokedex.Titulo"), IdiomaInterfaz.T("Partida.Editor.pokedex.Descripcion"), "BookOpen", CatalogoPartida.NoAplicaPokedex(sav)));
        l.Add(E("cartas", IdiomaInterfaz.T("Partida.Editor.cartas.Titulo"), IdiomaInterfaz.T("Partida.Editor.cartas.Descripcion"), "ScrollText", CatalogoPartida.NoAplicaCartas(sav)));
        l.Add(E("bloques", IdiomaInterfaz.T("Partida.Editor.bloques.Titulo"), IdiomaInterfaz.T("Partida.Editor.bloques.Descripcion"), "Layers", CatalogoPartida.NoAplicaBloques(sav)));
        // Los guardados de Switch (bloques de clave cifrados): el volcado de bloques de PKHeX (SAV_BlockDump8), una herramienta avanzada que no es un boton de «Especificos del juego».
        if (sav is ISCBlockArray) l.Add(E("esp:bloques8", IdiomaInterfaz.T("Partida.Editor.bloques8.Titulo"), IdiomaInterfaz.T("Partida.Editor.bloques8.Descripcion"), "Layers"));
        l.Add(E("imagenes", IdiomaInterfaz.T("Partida.Editor.imagenes.Titulo"), IdiomaInterfaz.T("Partida.Editor.imagenes.Descripcion"), "Image", ImagenesJugador.NoAplica(sav)));
        l.Add(E("gopark", IdiomaInterfaz.T("Partida.Editor.gopark.Titulo"), IdiomaInterfaz.T("Partida.Editor.gopark.Descripcion"), "Smartphone", ModeloGoPark.NoAplica(sav)));
        l.Add(E("eventos", IdiomaInterfaz.T("Partida.Editor.eventos.Titulo"), IdiomaInterfaz.T("Partida.Editor.eventos.Descripcion"), "ListChecks", ModeloEventos.NoAplica(sav)));
        l.Add(E("regalos", IdiomaInterfaz.T("Partida.Editor.regalos.Titulo"), IdiomaInterfaz.T("Partida.Editor.regalos.Descripcion"), "Gift", ModeloRegalos.NoAplica(sav)));
        l.AddRange(Herramientas(sav));
        return l;
    }

    private IReadOnlyList<EntradaEditorVm> ConstruirEspecificos(SesionM sesion)
    {
        var abrir = new RelayCommand<string>(id => AbrirEditor(id));
        return [.. CatalogoEspecificos.Disponibles(sesion.Guardado).Select(e => new EntradaEditorVm("esp:" + e.Id, e.Titulo, e.Descripcion, e.Glifo, null, abrir))];
    }

    /// <summary>Abre un editor por su id (menu y pruebas de dispositivo). Devuelve false si no existe o no aplica.</summary>
    public bool AbrirEditor(string? id)
    {
        if (_sesion is null || id is null) return false;
        if (AbrirHerramientaGuardado(id)) return true;
        if (id == "imagenes")
        {
            if (ImagenesJugador.NoAplica(_sesion.Guardado) is not null) { _informar(IdiomaInterfaz.T("Partida.Error.Imagenes"), "aviso", "CircleAlert", null, null); return false; }
            Imagenes = new ImagenesViewModel(_sesion, _documentos, _informar, () => Imagenes = null);
            return true;
        }
        if (id == "gopark")
        {
            if (ModeloGoPark.NoAplica(_sesion.Guardado) is not null) { _informar(IdiomaInterfaz.T("Partida.Error.GoPark"), "aviso", "CircleAlert", null, null); return false; }
            GoPark = new GoParkViewModel(_sesion, _documentos, _portapapeles, _abrirHoja, _informar, () => GoPark = null);
            return true;
        }
        if (id == "eventos")
        {
            if (ModeloEventos.NoAplica(_sesion.Guardado) is not null) { _informar(IdiomaInterfaz.T("Partida.Error.Eventos"), "aviso", "CircleAlert", null, null); return false; }
            Eventos = new EventosViewModel(_sesion, _documentos, _portapapeles, _abrirHoja, _informar, () => Eventos = null);
            return true;
        }
        if (id == "regalos")
        {
            if (ModeloRegalos.NoAplica(_sesion.Guardado) is not null) { _informar(IdiomaInterfaz.T("Partida.Error.Regalos"), "aviso", "CircleAlert", null, null); return false; }
            Regalos = new RegalosViewModel(_sesion, _documentos, _abrirHoja, _informar, () => Regalos = null);
            return true;
        }
        EditorDefP? CrearDefinicion() => id.StartsWith("esp:", StringComparison.Ordinal) ? CatalogoEspecificos.Editor(id[4..], _sesion.Guardado) : id switch
        {
            "entrenador" => CatalogoPartida.Entrenador(_sesion.Guardado),
            "pokedex" => CatalogoPartida.Pokedex(_sesion.Guardado),
            "cartas" => CatalogoPartida.Cartas(_sesion.Guardado),
            "bloques" => CatalogoPartida.Bloques(_sesion.Guardado),
            _ => null,
        };
        var def = CrearDefinicion();
        if (def is null) { _informar(IdiomaInterfaz.T("Partida.Error.Editor"), "aviso", "CircleAlert", null, null); return false; }
        Editor = new EditorPartidaViewModel(new ModeloEditorPartida(_sesion, def), () => Editor = null, _abrirHoja, _informar, _documentos, CrearDefinicion);
        return true;
    }

    /// <summary>Atras del sistema: cierra el editor (con su confirmacion si hay cambios) y devuelve true si lo gestiono.</summary>
    public bool Atras() => Imagenes is { } im ? im.Atras() : GoPark is { } gp ? gp.Atras() : Eventos is { } ev ? ev.Atras() : Regalos is { } r ? r.Atras() : Editor is { } e && e.Atras();
}
