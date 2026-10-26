// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pokekeep.Mobile.Ui.Localizacion;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Ajustes.Pkhex;
using Pokekeep.Mobile.Ui.Partida.Formularios;

namespace Pokekeep.Mobile.Ui.ViewModels;

/// <summary>Un elemento de una lista ordenada de ajustes (un token del set de Showdown): su nombre y los tres botones de 48 dp (subir, bajar, quitar).</summary>
public sealed class ElementoListaPkhex(string etiqueta, int indice, int total, Action<int> subir, Action<int> bajar, Action<int> quitar, Func<string>? etiquetaActual = null) : ObservableObject
{
    public string Etiqueta => etiquetaActual?.Invoke() ?? etiqueta;
    public int Indice { get; } = indice;
    public string Posicion => $"{indice + 1}";
    public bool PuedeSubir => indice > 0;
    public bool PuedeBajar => indice < total - 1;
    public IRelayCommand SubirCommand { get; } = new RelayCommand(() => subir(indice));
    public IRelayCommand BajarCommand { get; } = new RelayCommand(() => bajar(indice));
    public IRelayCommand QuitarCommand { get; } = new RelayCommand(() => quitar(indice));
    public string AccesibleSubir => IdiomaInterfaz.F("Ajustes.Lista.Subir", Etiqueta);
    public string AccesibleBajar => IdiomaInterfaz.F("Ajustes.Lista.Bajar", Etiqueta);
    public string AccesibleQuitar => IdiomaInterfaz.F("Ajustes.Lista.Quitar", Etiqueta);
    public void ActualizarIdioma()
    {
        OnPropertyChanged(nameof(Etiqueta)); OnPropertyChanged(nameof(AccesibleSubir));
        OnPropertyChanged(nameof(AccesibleBajar)); OnPropertyChanged(nameof(AccesibleQuitar));
    }
}

/// <summary>
/// Una lista ORDENADA de valores de una enumeración (los elementos de un set de Showdown en <c>BattleTemplate.*.TokenOrderCustom</c>): cada elemento con subir, bajar y quitar, «Añadir» (abre la hoja con los que faltan)
/// y «Como en Showdown» (el orden por defecto del Core). Los nombres salen de <c>lang_es.txt</c> (<c>BattleTemplateToken.Ability</c> = «Habilidad»). Cada cambio se guarda y se aplica al momento.
/// </summary>
public sealed partial class FilaPkhexListaOpciones : FilaPkhex
{
    private readonly Action<HojaViewModel> _abrirHoja;
    private readonly Type _tipoElemento;

    public FilaPkhexListaOpciones(NodoAjustePkhex nodo, IAjustesPkhex almacen, Action<HojaViewModel> abrirHoja) : base(nodo, almacen)
    {
        _abrirHoja = abrirHoja; _tipoElemento = nodo.Tipo.GetElementType()!;
        Releer();
    }

    [ObservableProperty] private IReadOnlyList<ElementoListaPkhex> _elementos = [];
    public bool PuedeAnadir => Faltan().Count > 0;
    public bool HayElementos => Elementos.Count > 0;
    public override string ValorTexto => string.Join(" · ", Actual().Select(Etiqueta));

    private List<object> Actual() => [.. ((Array)Leer()!).Cast<object>()];

    /// <summary>Nombre en castellano de un valor de la lista (<c>BattleTemplateToken.X</c> de <c>lang_es.txt</c>; si no está, el respaldo y, al final, el nombre del valor).</summary>
    public string Etiqueta(object valor) => FilaPkhexOpcion.Etiqueta(_tipoElemento, valor.ToString()!);

    private List<object> Faltan() => [.. Enum.GetValues(_tipoElemento).Cast<object>().Where(v => !EsInvalido(v) && !Actual().Contains(v))];

    /// <summary>El valor 0 de <c>BattleTemplateToken</c> (<c>None</c>) es un valor mágico del Core que no se exporta: no se ofrece.</summary>
    private static bool EsInvalido(object v) => v is BattleTemplateToken.None;

    private void Guardar(List<object> lista)
    {
        var arr = Array.CreateInstance(_tipoElemento, lista.Count);
        for (var i = 0; i < lista.Count; i++) arr.SetValue(lista[i], i);
        Escribir(arr);
        Releer();
    }

    private void Mover(int i, int delta)
    {
        var l = Actual(); var j = i + delta;
        if ((uint)i >= (uint)l.Count || (uint)j >= (uint)l.Count) return;
        (l[i], l[j]) = (l[j], l[i]);
        Guardar(l);
    }

    private void Quitar(int i) { var l = Actual(); if ((uint)i < (uint)l.Count) { l.RemoveAt(i); Guardar(l); } }

    [RelayCommand]
    private void Anadir()
    {
        var faltan = Faltan();
        if (faltan.Count == 0) return;
        IReadOnlyList<OpcionSelector> Opciones() => [.. faltan.Select((v, i) => new OpcionSelector(i, Etiqueta(v)))];
        _abrirHoja(new SelectorHojaViewModel(IdiomaInterfaz.T("Ajustes.Lista.AnadirOrden"), Opciones(), -1, o => { var l = Actual(); l.Add(faltan[o.Id]); Guardar(l); },
            actualizarOpciones: Opciones, tituloActual: () => IdiomaInterfaz.T("Ajustes.Lista.AnadirOrden")));
    }

    /// <summary>Vuelve al orden del formato Showdown del Core (<c>BattleTemplateConfig.Showdown</c>), el valor de fábrica de esta lista.</summary>
    [RelayCommand]
    private void ComoShowdown()
    {
        if (_tipoElemento != typeof(BattleTemplateToken)) return;
        Guardar([.. BattleTemplateConfig.Showdown.ToArray().Cast<object>()]);
    }

    public override void ActualizarIdioma()
    {
        base.ActualizarIdioma();
        foreach (var elemento in Elementos) elemento.ActualizarIdioma();
    }

    public override void Releer()
    {
        var l = Actual();
        Elementos = [.. l.Select((v, i) => new ElementoListaPkhex(Etiqueta(v), i, l.Count, a => Mover(a, -1), a => Mover(a, 1), Quitar, () => Etiqueta(v)))];
        OnPropertyChanged(nameof(PuedeAnadir)); OnPropertyChanged(nameof(HayElementos)); OnPropertyChanged(nameof(ValorTexto));
    }
}

public static partial class FabricaFilasPkhex
{
    static partial void PersonalizadaB10_9(NodoAjustePkhex n, IAjustesPkhex a, Action<HojaViewModel> h, ref FilaPkhex? fila)
    {
        if (RegistroAjustesPkhex.ClaseEfectiva(n) == TipoAjustePkhex.ListaOpciones) fila ??= new FilaPkhexListaOpciones(n, a, h);
    }
}
