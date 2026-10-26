// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Globalization;

namespace Pokekeep.Mobile.Ui;

/// <summary>
/// Cultura de presentación del idioma elegido (ADR 0100). Español de España mantiene sus separadores explícitos;
/// inglés usa su cultura. Se captura el idioma del sistema antes de fijar la cultura de la app.
/// </summary>
public static class CulturaApp
{
    public static string Sistema { get; } = CultureInfo.CurrentUICulture.Name;
    public static CultureInfo Cultura { get; private set; } = Crear();

    private static CultureInfo Crear()
    {
        var c = new CultureInfo("es-ES");
        c = (CultureInfo)c.Clone();
        c.NumberFormat.NumberGroupSeparator = ".";
        c.NumberFormat.NumberDecimalSeparator = ",";
        c.NumberFormat.NumberGroupSizes = [3];
        c.NumberFormat.CurrencyGroupSeparator = ".";
        c.NumberFormat.CurrencyDecimalSeparator = ",";
        c.NumberFormat.PercentGroupSeparator = ".";
        c.NumberFormat.PercentDecimalSeparator = ",";
        return CultureInfo.ReadOnly(c);
    }

    /// <summary>Fija la cultura por defecto del proceso y la del hilo actual. Llamar lo primero al arrancar (cabecera de Android y pruebas).</summary>
    public static void Fijar(string codigo = "es-ES")
    {
        Cultura = codigo == "en" ? CultureInfo.GetCultureInfo("en") : Crear();
        CultureInfo.DefaultThreadCurrentCulture = Cultura;
        CultureInfo.DefaultThreadCurrentUICulture = Cultura;
        CultureInfo.CurrentCulture = Cultura;
        CultureInfo.CurrentUICulture = Cultura;
    }
}
