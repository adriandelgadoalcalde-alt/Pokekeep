// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Reflection;
using PKHeX.Core;

namespace Pokekeep.Mobile.Ui.Ajustes.Pkhex;

/// <summary>
/// Cierra los huecos de los mensajes de legalidad del Core (X36e). El Core rellena «{0}» y «{1}» de algunas plantillas con el nombre de un idioma o de una región de consola
/// buscado en una lista corta; si el valor del Pokémon no está en ella (idioma 0 «sin establecer», 6 «sin usar», un valor corrupto) devuelve una cadena VACÍA y el informe dice
/// «El ID de idioma debería ser &lt;= ES-ES (Español), no .» (PKHeX de escritorio muestra lo mismo: el fallo es del Core y es anterior a X36). Además, «AwakenedCap» se emite sin
/// argumento y enseñaba el «{0}» crudo. Se corrige sobre los DATOS que el Core lee (listas de nombres y plantilla), para que valga en todos los caminos (informe, informe masivo, Integridad)
/// sin tocar <c>fuentes\</c>. Solo cambia lo que antes salía roto: los informes de los Pokémon correctos son idénticos al escritorio.
/// </summary>
public static class ParcheTextosLegalidad
{
    private static readonly Lock _cerrojo = new();
    private static bool _hecho;
    private const int Tamano = 256;   // los identificadores del Core son un byte

    /// <summary>Idempotente y seguro entre hilos; se llama al aplicar el idioma de la interfaz.</summary>
    public static void Aplicar()
    {
        if (_hecho) return;
        lock (_cerrojo)
        {
            if (_hecho) return;
            _hecho = true;
            foreach (var codigo in new[] { "es", "en" })
            {
                try { Parchear(codigo); } catch { /* si el Core cambia, quedan los textos de siempre: nunca peor que antes */ }
            }
        }
    }

    private static void Parchear(string codigo)
    {
        var es = codigo == "es";
        var set = LegalityLocalizationSet.GetLocalization(codigo);
        Rellenar(set.Strings, "languageNames", es ? "Idioma desconocido ({0})" : "Unknown language ({0})", es ? "Sin idioma" : "No language", es ? "ninguno" : "none");
        Rellenar(set.Strings, "console3ds", es ? "Región desconocida ({0})" : "Unknown region ({0})", null);
        // AwakenedCap: la plantilla pide un tope y el Core no lo pasa; es siempre el máximo de AV (AwakeningUtil.AwakeningMax).
        var campo = typeof(LegalityCheckLocalization).GetField("<AwakenedCap>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        if (campo?.GetValue(set.Lines) is string plantilla && plantilla.Contains("{0}"))
            campo.SetValue(set.Lines, plantilla.Replace("{0}", AwakeningUtil.AwakeningMax.ToString(System.Globalization.CultureInfo.InvariantCulture)));
    }

    /// <summary>255 es el «sin máximo» del Core (-1 como byte): la generación del origen no se pudo determinar. Alarga la lista del Core hasta 256 entradas y pone un nombre a cada hueco; el campo es de solo lectura, así que se reasigna por reflexión (si no se puede, solo se rellenan los huecos de la lista original).</summary>
    private static void Rellenar(GameStrings strings, string campo, string plantillaDesconocido, string? cero, string? ultimo = null)
    {
        var f = typeof(GameStrings).GetField(campo, BindingFlags.Instance | BindingFlags.Public);
        if (f?.GetValue(strings) is not string[] original) return;
        var lista = new string[Math.Max(Tamano, original.Length)];
        for (var i = 0; i < lista.Length; i++)
        {
            var valor = i < original.Length ? original[i] : string.Empty;
            lista[i] = string.IsNullOrWhiteSpace(valor) ? (i == 0 && cero is not null ? cero : i == Tamano - 1 && ultimo is not null ? ultimo : string.Format(System.Globalization.CultureInfo.InvariantCulture, plantillaDesconocido, i)) : valor;
        }
        try { f.SetValue(strings, lista); }
        catch { for (var i = 0; i < original.Length; i++) original[i] = lista[i]; }
    }
}
