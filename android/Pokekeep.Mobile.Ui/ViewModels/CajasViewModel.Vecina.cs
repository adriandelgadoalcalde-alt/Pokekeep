// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Cajas;
using Pokekeep.Mobile.Ui.Modelo;

namespace Pokekeep.Mobile.Ui.ViewModels;

/// <summary>
/// La caja VECINA del carrusel (X6): al empezar a deslizar, o al pulsar «Caja anterior/siguiente», la caja contigua (con su fondo y sus Pokemon reales) se carga UNA vez en estas casillas, que la vista dibuja
/// a un lado mientras la actual se mueve. No toca el guardado ni cambia la caja que se ve: al terminar la animacion la vista llama a <see cref="IrACaja"/> como siempre y la caja real pasa a mostrar lo mismo que ya se veia.
/// </summary>
public sealed partial class CajasViewModel
{
    private ObservableCollection<CasillaViewModel>? _casillasVecina;

    /// <summary>Las casillas de la caja vecina (misma cantidad y mismas columnas que la caja que se ve). Se crean la primera vez que se necesitan.</summary>
    public ObservableCollection<CasillaViewModel> CasillasVecina => _casillasVecina ??= new ObservableCollection<CasillaViewModel>(Enumerable.Range(0, TieneCajas ? Sesion.CasillasPorCaja : 0).Select(i => new CasillaViewModel(Sesion, i)));

    [ObservableProperty] private Avalonia.Media.Imaging.Bitmap? _imagenFondoVecina;
    [ObservableProperty] private int _cajaVecina = -1;
    public bool HayFondoVecina => ImagenFondoVecina is not null;
    partial void OnImagenFondoVecinaChanged(Avalonia.Media.Imaging.Bitmap? value) => OnPropertyChanged(nameof(HayFondoVecina));

    /// <summary>Hay otra caja a la que pasar (con una sola no hay vecina: el gesto solo «responde» y vuelve).</summary>
    public bool HayCajaVecina => EsCajas && TieneCajas && TotalCajas > 1;

    /// <summary>La caja a la que lleva un deslizamiento o un boton en esa direccion (+1 siguiente, -1 anterior); da la vuelta por los extremos, igual que <see cref="CajaSiguiente"/> y <see cref="CajaAnterior"/>.</summary>
    public int CajaEnDireccion(int direccion) => TotalCajas <= 0 ? Caja : ((Caja + Math.Sign(direccion)) % TotalCajas + TotalCajas) % TotalCajas;

    /// <summary>Carga la caja vecina en la direccion dada. Devuelve false (y no hace nada) si no hay vecina. No cambia la caja actual, ni la seleccion, ni el guardado.</summary>
    public bool PrepararVecina(int direccion)
    {
        if (!HayCajaVecina) return false;
        var caja = CajaEnDireccion(direccion);
        var sav = Sesion.Guardado;
        var datos = sav.GetBoxData(caja);
        var vecinas = CasillasVecina;
        for (var i = 0; i < vecinas.Count; i++)
        {
            var pk = i < datos.Length ? datos[i] : null;
            // Sin analisis propio: lo que el analizador ya conoce (la caja suele estar analizada) y el resto llega al aterrizar, como cada vez que se cambia de caja.
            vecinas[i].Asignar(new CasillaRef(new SlotInfoBox(caja, i, sav), caja), pk, false, false);
        }
        AplicarMarcas(vecinas);
        // X35: navegar nunca equivale a tocar una casilla.
        foreach (var c in vecinas) c.Seleccionada = false;
        ImagenFondoVecina = Design.Sprites.FondoDeCaja(sav, caja);
        CajaVecina = caja;
        return true;
    }

    /// <summary>Suelta lo que ocupa la vecina cuando no hace falta (libera los sprites; la caja vecina se vuelve a cargar en el siguiente gesto).</summary>
    public void DescartarVecina()
    {
        CajaVecina = -1;
        ImagenFondoVecina = null;
    }
}
