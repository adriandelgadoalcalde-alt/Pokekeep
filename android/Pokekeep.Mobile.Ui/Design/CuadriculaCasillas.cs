// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Avalonia;
using Avalonia.Controls;

namespace Pokekeep.Mobile.Ui.Design;

/// <summary>
/// Cuadricula de casillas CUADRADAS con N columnas que ocupa todo el ancho disponible (la caja real del formato: 6×5 desde la 3.ª generacion).
/// La casilla mide (ancho - separaciones) / columnas, tambien a 384 dp y con escala de fuente grande: el objetivo tactil de la casilla es la celda entera.
/// </summary>
public sealed class CuadriculaCasillas : Panel
{
    public static readonly StyledProperty<int> ColumnasProperty = AvaloniaProperty.Register<CuadriculaCasillas, int>(nameof(Columnas), 6);
    public static readonly StyledProperty<double> EspacioProperty = AvaloniaProperty.Register<CuadriculaCasillas, double>(nameof(Espacio), 4);

    public static readonly StyledProperty<double> MaxCeldaProperty = AvaloniaProperty.Register<CuadriculaCasillas, double>(nameof(MaxCelda), double.PositiveInfinity);

    static CuadriculaCasillas() => AffectsMeasure<CuadriculaCasillas>(ColumnasProperty, EspacioProperty, MaxCeldaProperty);

    /// <summary>Lado maximo de la casilla (dp): en una zona con pocas casillas (GTS, fusion...) no ocupan todo el ancho; la cuadricula queda alineada a la izquierda.</summary>
    public double MaxCelda { get => GetValue(MaxCeldaProperty); set => SetValue(MaxCeldaProperty, value); }

    public int Columnas { get => GetValue(ColumnasProperty); set => SetValue(ColumnasProperty, value); }
    public double Espacio { get => GetValue(EspacioProperty); set => SetValue(EspacioProperty, value); }

    /// <summary>Objetivo tactil minimo (dp): una casilla es un boton y no baja de aqui si la separacion puede ceder lo que falta.</summary>
    private const double ObjetivoTactil = 48;
    /// <summary>La separacion nunca baja de esto aunque la casilla no llegue a 48 dp (anchos por debajo de ~330 dp).</summary>
    private const double EspacioMinimo = 1;

    /// <summary>
    /// La separacion que se usa: la pedida, salvo que con ella la casilla quede por debajo de 48 dp. En un movil de 360 dp (el ancho mas comun) la cuadricula de seis columnas dentro de una tarjeta con sus margenes
    /// daba casillas de 47,3 dp; con la separacion en 3 dp salen de 48 dp. A 384 dp o mas no cambia nada (la casilla ya pasa de 48 dp con la separacion de siempre).
    /// </summary>
    private double EspacioEfectivo(double ancho)
    {
        var c = Math.Max(1, Columnas);
        if (c < 2 || Espacio <= EspacioMinimo) return Espacio;
        var natural = (ancho - (c - 1) * Espacio) / c;
        if (natural >= ObjetivoTactil || MaxCelda < ObjetivoTactil) return Espacio;   // ya llega, o la cuadricula limita la casilla por debajo de 48 dp a proposito
        var necesario = (ancho - c * ObjetivoTactil) / (c - 1);   // la separacion con la que la casilla mide justo 48 dp (puede salir negativa si ni sin separacion llega)
        return Math.Clamp(necesario, EspacioMinimo, Espacio);
    }

    private double Celda(double ancho)
    {
        var c = Math.Max(1, Columnas);
        return Math.Min(MaxCelda, Math.Max(0, (ancho - (c - 1) * EspacioEfectivo(ancho)) / c));
    }

    protected override Size MeasureOverride(Size disponible)
    {
        var ancho = double.IsInfinity(disponible.Width) ? 6 * 56 : disponible.Width;
        var celda = Celda(ancho);
        var espacio = EspacioEfectivo(ancho);
        foreach (var hijo in Children) hijo.Measure(new Size(celda, celda));
        var filas = (Children.Count + Columnas - 1) / Math.Max(1, Columnas);
        var columnasUsadas = Math.Min(Math.Max(1, Columnas), Math.Max(1, Children.Count));
        return new Size(double.IsInfinity(MaxCelda) ? ancho : Math.Min(ancho, columnasUsadas * celda + (columnasUsadas - 1) * espacio), filas == 0 ? 0 : filas * celda + (filas - 1) * espacio);
    }

    protected override Size ArrangeOverride(Size final)
    {
        var celda = Celda(final.Width);
        var espacio = EspacioEfectivo(final.Width);
        for (var i = 0; i < Children.Count; i++)
        {
            var fila = i / Columnas; var col = i % Columnas;
            Children[i].Arrange(new Rect(col * (celda + espacio), fila * (celda + espacio), celda, celda));
        }
        return final;
    }
}
