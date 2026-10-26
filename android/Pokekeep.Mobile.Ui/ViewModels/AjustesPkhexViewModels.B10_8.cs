// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using CommunityToolkit.Mvvm.Input;
using Pokekeep.Mobile.Ui.Localizacion;
using Pokekeep.Mobile.Ui.Ajustes.Pkhex;
using Pokekeep.Mobile.Ui.Herramientas.ExportarCajas;

namespace Pokekeep.Mobile.Ui.ViewModels;

/// <summary>
/// «Nomenclatura de exportación por defecto» (<c>SlotExport.DefaultBoxExportNamer</c>, bloque 10-8): en PKHeX es un texto que el exportador compara con los nombradores disponibles
/// (<c>EntityFileNamer.AvailableNamers</c>); escribirlo a mano no tiene sentido, así que es una lista de esos nombradores. Un valor guardado que ya no existe se enseña como el primero, igual que lo elige <c>BoxExporter</c>.
/// </summary>
public sealed partial class FilaPkhexNombrador(NodoAjustePkhex nodo, IAjustesPkhex almacen, Action<HojaViewModel> abrirHoja) : FilaPkhex(nodo, almacen)
{
    public static string Etiqueta(string nombre) => nombre == "Default" ? IdiomaInterfaz.T("Ajustes.Nombrador.Defecto") : nombre;
    public override string ValorTexto => Etiqueta(ModeloExportarCajas.Nombradores[ModeloExportarCajas.IndiceNombrador((string?)Leer() ?? string.Empty)].Name);
    public void Elegir(int indice) { Escribir(ModeloExportarCajas.Nombradores[Math.Clamp(indice, 0, ModeloExportarCajas.Nombradores.Count - 1)].Name); OnPropertyChanged(nameof(ValorTexto)); }
    [RelayCommand]
    private void Abrir()
    {
        var l = ModeloExportarCajas.Nombradores;
        IReadOnlyList<OpcionSelector> Opciones() => [.. l.Select((n, i) => new OpcionSelector(i, Etiqueta(n.Name)))];
        abrirHoja(new SelectorHojaViewModel(Nombre, Opciones(), ModeloExportarCajas.IndiceNombrador((string?)Leer() ?? string.Empty), o => Elegir(o.Id),
            actualizarOpciones: Opciones, tituloActual: () => Nombre));
    }
    public override void Releer() => OnPropertyChanged(nameof(ValorTexto));
}

public static partial class FabricaFilasPkhex
{
    static partial void PersonalizadaB10_8(NodoAjustePkhex n, IAjustesPkhex a, Action<HojaViewModel> h, ref FilaPkhex? fila)
    {
        if (n.Ruta == "SlotExport.DefaultBoxExportNamer") fila = new FilaPkhexNombrador(n, a, h);
    }
}
