// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pokekeep.Mobile.Ui.Localizacion;
using Pokekeep.Mobile.Ui.Ajustes.Pkhex;
using Pokekeep.Mobile.Ui.Herramientas.Informe;

namespace Pokekeep.Mobile.Ui.ViewModels;

/// <summary>Una propiedad elegida en una lista del informe: el nombre de PKHeX, su título en castellano (si lo tiene) y «Quitar».</summary>
public sealed class PropiedadElegida(string propiedad, string titulo, IRelayCommand quitar, Func<string>? tituloActual = null) : ObservableObject
{
    public string Propiedad { get; } = propiedad;
    public string Titulo => tituloActual?.Invoke() ?? titulo;
    public IRelayCommand Quitar { get; } = quitar;
    public void ActualizarIdioma() { OnPropertyChanged(nameof(Titulo)); OnPropertyChanged(nameof(Texto)); }
    public string Texto => Titulo == Propiedad ? Propiedad : $"{Titulo} ({Propiedad})";
}

/// <summary>
/// «Propiedades adicionales» y «Propiedades ocultas» del informe (<c>Report.ExtraProperties</c> y <c>Report.HiddenProperties</c>, bloque 10-7). En PKHeX son listas de texto que se escriben a mano en la rejilla de
/// propiedades; aquí se eligen de la lista de lo que existe (las adicionales, entre las propiedades del editor por lotes; las ocultas, entre las columnas del informe), se quitan con un toque y nada se escribe mal.
/// Una propiedad que ya estuviera (ajustes traídos del PC, aunque no exista) se conserva y se puede quitar. Las consume «Informe de la caja» al generar.
/// </summary>
public sealed partial class FilaPkhexPropiedadesInforme : FilaPkhex
{
    private readonly Action<HojaViewModel> _abrirHoja;

    public FilaPkhexPropiedadesInforme(NodoAjustePkhex nodo, IAjustesPkhex almacen, Action<HojaViewModel> abrirHoja) : base(nodo, almacen) { _abrirHoja = abrirHoja; Releer(); }

    private bool EsExtra => Nodo.Ruta == "Report.ExtraProperties";
    private List<string> Lista => EsExtra ? Almacen.Actual.Report.ExtraProperties : Almacen.Actual.Report.HiddenProperties;

    /// <summary>Lo que se puede elegir: las propiedades del editor por lotes (adicionales) o las columnas del informe (ocultas), sin las ya elegidas.</summary>
    public IReadOnlyList<string> Posibles => [.. (EsExtra ? ModeloInforme.PropiedadesExtraPosibles : ModeloInforme.PropiedadesResumen.Select(p => p.Name)).Where(p => !Lista.Contains(p, StringComparer.OrdinalIgnoreCase))];

    [ObservableProperty] private IReadOnlyList<PropiedadElegida> _elegidas = [];
    public bool HayElegidas => Elegidas.Count > 0;
    partial void OnElegidasChanged(IReadOnlyList<PropiedadElegida> value) { OnPropertyChanged(nameof(HayElegidas)); OnPropertyChanged(nameof(ValorTexto)); }

    public override string ValorTexto => Elegidas.Count == 0 ? IdiomaInterfaz.T("Ajustes.Propiedades.Ninguna") : IdiomaInterfaz.P("Ajustes.Propiedades.Cantidad", Elegidas.Count);
    public string TextoAnadir => IdiomaInterfaz.T(EsExtra ? "Ajustes.Propiedades.Anadir" : "Ajustes.Propiedades.Ocultar");

    public override void ActualizarIdioma()
    {
        base.ActualizarIdioma(); OnPropertyChanged(nameof(TextoAnadir));
        foreach (var propiedad in Elegidas) propiedad.ActualizarIdioma();
    }

    public override void Releer() => Elegidas = [.. Lista.Select(p => new PropiedadElegida(p, ModeloInforme.TituloDe(p), new RelayCommand(() => Quitar(p)), () => ModeloInforme.TituloDe(p)))];

    /// <summary>Añade una propiedad (al final, como una línea más de la lista de PKHeX). False si ya estaba o está vacía.</summary>
    public bool Anadir(string propiedad)
    {
        propiedad = propiedad.Trim();
        if (propiedad.Length == 0 || Lista.Contains(propiedad, StringComparer.OrdinalIgnoreCase)) return false;
        Lista.Add(propiedad);
        Almacen.Guardar();
        Releer();
        return true;
    }

    public void Quitar(string propiedad)
    {
        if (Lista.Remove(propiedad)) Almacen.Guardar();
        Releer();
    }

    [RelayCommand]
    private void AbrirAnadir()
    {
        var posibles = Posibles;
        IReadOnlyList<OpcionSelector> Opciones() => [.. posibles.Select((p, i) => new OpcionSelector(i, p, ModeloInforme.TituloDe(p) is var t && t != p ? t : null))];
        _abrirHoja(new SelectorHojaViewModel(Nombre, Opciones(), -1, o => Anadir(posibles[o.Id]), IdiomaInterfaz.T("Ajustes.Propiedades.Buscar"),
            actualizarOpciones: Opciones, tituloActual: () => Nombre, marcadorActual: () => IdiomaInterfaz.T("Ajustes.Propiedades.Buscar")));
    }
}

public static partial class FabricaFilasPkhex
{
    static partial void PersonalizadaB10_7(NodoAjustePkhex n, IAjustesPkhex a, Action<HojaViewModel> h, ref FilaPkhex? fila)
    {
        if (n.Ruta is "Report.ExtraProperties" or "Report.HiddenProperties") fila = new FilaPkhexPropiedadesInforme(n, a, h);
    }
}
