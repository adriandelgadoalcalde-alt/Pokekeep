// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pokekeep.Mobile.Ui.Partida;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.ViewModels;

/// <summary>
/// Una coleccion en pantalla (entradas del Salon de la Fama, visitantes, bases secretas...): el selector del elemento (anterior, siguiente o una lista con buscador) y los campos del elemento elegido.
/// Los campos de cada elemento se crean al abrirlo por primera vez; el borrador de los ya abiertos se conserva al cambiar de uno a otro.
/// </summary>
public sealed partial class ColeccionPVm : ObservableObject
{
    private readonly EditorPartidaViewModel _ed;

    public ColeccionPVm(EditorPartidaViewModel ed, ColeccionDefP def)
    {
        _ed = ed; Def = def;
        Total = Modelo.CantidadElementos(def.Id);
        Abrir(0);
    }

    public ColeccionDefP Def { get; private set; }
    private ModeloEditorPartida Modelo => _ed.Modelo;
    public int Total { get; }
    public bool HayElementos => Total > 0;
    public bool NoHayElementos => Total == 0;
    public string Vacia => IdiomaInterfaz.F("Form.Coleccion.Vacia", Def.Elemento.ToLowerInvariant());
    public ObservableCollection<SeccionPVm> Secciones { get; } = [];

    [ObservableProperty] private int _indice;
    [ObservableProperty] private string _titulo = string.Empty;
    [ObservableProperty] private string _posicion = string.Empty;
    [ObservableProperty] private string? _editados;
    public bool HayEditados => !string.IsNullOrEmpty(Editados);
    public bool PuedeAnterior => Indice > 0;
    public bool PuedeSiguiente => Indice < Total - 1;
    public string Accesible => IdiomaInterfaz.F("Form.Coleccion.Accesible", Posicion);

    partial void OnIndiceChanged(int value) { OnPropertyChanged(nameof(PuedeAnterior)); OnPropertyChanged(nameof(PuedeSiguiente)); }
    partial void OnEditadosChanged(string? value) => OnPropertyChanged(nameof(HayEditados));
    partial void OnPosicionChanged(string value) => OnPropertyChanged(nameof(Accesible));

    /// <summary>Abre el elemento <paramref name="i"/> (si existe): crea los VM de sus campos y los pone en pantalla.</summary>
    public void Abrir(int i)
    {
        Secciones.Clear();
        if (Total == 0) { Titulo = string.Empty; Posicion = string.Empty; RefrescarVista(); return; }
        i = Math.Clamp(i, 0, Total - 1);
        Indice = i;
        Titulo = Def.Titulo(Modelo.Ctx, i);
        Posicion = Def.MostrarPosicion ? IdiomaInterfaz.F("Form.Coleccion.Posicion", Def.Elemento, i + 1, Total) : Titulo;
        foreach (var s in Modelo.AbrirElemento(Def.Id, i).Where(s => s.Campos.Count > 0))
            Secciones.Add(new SeccionPVm(s.Titulo, [.. s.Campos.Select(_ed.VmDe)], () => s.EsVisible(Modelo.Ctx)));
        foreach (var s in Secciones) foreach (var c in s.Campos) c.Releer();
        RefrescarVista();
    }

    /// <summary>Los titulos y resumenes del selector (hasta 20.000 por almacen): se arman una vez y se reutilizan mientras no se edite nada (cada edicion, <see cref="Refrescar"/>, los invalida porque el resumen puede haber cambiado).</summary>
    private List<OpcionSelector>? _opciones;

    /// <summary>Un campo del editor ha cambiado: se invalida la lista cacheada del selector y se refresca la vista.</summary>
    internal void ActualizarIdioma(ColeccionDefP def)
    {
        Def = def; _opciones = null;
        Titulo = HayElementos ? Def.Titulo(Modelo.Ctx, Indice) : string.Empty;
        Posicion = HayElementos ? Def.MostrarPosicion ? IdiomaInterfaz.F("Form.Coleccion.Posicion", Def.Elemento, Indice + 1, Total) : Titulo : string.Empty;
        if (HayElementos)
        {
            var secciones = Modelo.AbrirElemento(Def.Id, Indice).Where(s => s.Campos.Count > 0).ToArray();
            for (var i = 0; i < Secciones.Count; i++) Secciones[i].ActualizarTitulo(secciones[i].Titulo);
        }
        OnPropertyChanged(nameof(Vacia)); RefrescarVista();
    }

    public void Refrescar()
    {
        _opciones = null;
        RefrescarVista();
    }

    private void RefrescarVista()
    {
        foreach (var s in Secciones) s.Refrescar();
        var cambiados = Modelo.ElementosCambiados(Def.Id);
        Editados = cambiados.Count == 0 ? null : IdiomaInterfaz.F("Form.Coleccion.Editados", string.Join(", ", cambiados.Take(8).Select(x => (x + 1).ToString(System.Globalization.CultureInfo.InvariantCulture))), cambiados.Count > 8 ? IdiomaInterfaz.F("Form.Mas", cambiados.Count - 8) : string.Empty);
    }

    [RelayCommand] private void Anterior() { if (PuedeAnterior) Abrir(Indice - 1); }
    [RelayCommand] private void Siguiente() { if (PuedeSiguiente) Abrir(Indice + 1); }

    [RelayCommand]
    private void Elegir()
    {
        IReadOnlyList<OpcionSelector> Opciones() => _opciones ??= [.. Enumerable.Range(0, Total).Select(i => new OpcionSelector(i, Def.Titulo(Modelo.Ctx, i), Def.Resumen?.Invoke(Modelo.Ctx, i)))];
        _ed.AbrirHoja(new SelectorHojaViewModel(Def.Elemento, Opciones(), Indice, o => Abrir(o.Id), actualizarOpciones: Opciones, tituloActual: () => Def.Elemento));
    }
}
