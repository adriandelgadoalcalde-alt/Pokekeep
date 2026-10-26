// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;

namespace Pokekeep.Mobile.Ui.Ajustes.Pkhex;

/// <summary>
/// Los consumidores de los ajustes «Avanzado» de PKHeX (bloque 10-1). Cada uno lee el ajuste VIGENTE (<see cref="ProgramaPkhex.Ajustes"/>) en el momento en que se usa, como PKHeX con <c>Main.Settings.Advanced</c>.
/// </summary>
public static class AvanzadoPkhex
{
    private static AdvancedSettings Ajuste => ProgramaPkhex.Ajustes.Actual.Advanced;

    /// <summary>
    /// <c>SAV_BlockDump8.GetExtraKeyNames</c> calcado: los nombres de bloque de <c>&lt;PathBlockKeyList&gt;/&lt;tipo de guardado&gt;.txt</c> (clave hexadecimal, tabulador, nombre). Si la carpeta indicada no
    /// existe, ninguno; si no hay archivo para este tipo de guardado, ninguno.
    /// </summary>
    public static IEnumerable<string> NombresExtraDeBloques(ISCBlockArray sav)
    {
        var extra = Ajuste.PathBlockKeyList;
        if (extra.Length != 0 && !Directory.Exists(extra)) return [];
        var archivo = $"{Path.Combine(extra, sav.GetType().Name)}.txt";
        if (!File.Exists(archivo)) return [];
        try { return File.ReadAllLines(archivo); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return []; }
    }

    /// <summary>Subcadenas de los nombres de bloque que no se enseñan (<c>Advanced.GetExclusionList8()</c>, la misma llamada que <c>SAV_BlockDump8</c>).</summary>
    public static string[] ExclusionesDeBloques() => Ajuste.GetExclusionList8();

    /// <summary>
    /// <c>DragStartThreshold</c> en táctil. En PKHeX es la distancia que el ratón tiene que recorrer desde una casilla para que empiece a arrastrar (y no se tome por un clic). En el móvil el único arrastre que
    /// empieza sobre las casillas es el deslizamiento que cambia de caja: su umbral es el de siempre (56 dp o la quinta parte del ancho, lo menor) y nunca menos que el ajuste, para que un toque impreciso no cambie de caja.
    /// </summary>
    public static double UmbralDeslizamiento(double anchoCaja, double umbralBase = 56)
        => Math.Max(Math.Min(umbralBase, Math.Max(24, anchoCaja * 0.2)), Ajuste.DragStartThreshold);
}
