// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Pokekeep.Mobile.Ui.Design;

/// <summary>
/// La regla de «palabra partida» (revisión de B+, ADR 0105) en un solo sitio: la usan la invariante de la matriz de geometría y los paneles que cambian de disposición
/// (<see cref="RejillaSinPartir"/>, <see cref="EtiquetaYValor"/>) para no dejar que Avalonia corte una palabra por dentro con la letra grande.
/// </summary>
public static class PalabrasEnteras
{
    /// <summary>
    /// Si el <see cref="TextLayout"/> real del control corta una palabra entre dos líneas, devuelve el trozo («Pokémo|n»); si no, null. Corta una palabra cuando el último carácter de una línea y el primero de la siguiente son
    /// letras o dígitos: entre dos palabras siempre queda un espacio (que se queda al final de la línea) y un guion cuenta como final de palabra.
    /// </summary>
    public static string? PalabraPartida(TextBlock t)
    {
        var texto = t.Text ?? "";
        var lineas = t.TextLayout?.TextLines;
        if (lineas is null || lineas.Count < 2) return null;
        for (var i = 1; i < lineas.Count; i++)
        {
            var inicio = lineas[i].FirstTextSourceIndex;
            if (inicio <= 0 || inicio >= texto.Length) continue;
            if (!char.IsLetterOrDigit(texto[inicio - 1]) || !char.IsLetterOrDigit(texto[inicio])) continue;
            int a = inicio, b = inicio;
            while (a > 0 && char.IsLetterOrDigit(texto[a - 1])) a--;
            while (b < texto.Length && char.IsLetterOrDigit(texto[b])) b++;
            var palabra = texto[a..b];
            // Lo que no es una palabra de un idioma con espacios no cuenta: los identificadores con cifras (hashes, hexadecimales, extensiones como «wc8full») y la escritura sin espacios (japonés, chino, coreano),
            // que se parte entre caracteres por diseño.
            if (palabra.Any(char.IsDigit) || palabra.Any(ch => ch >= 'ᄀ')) continue;
            // Un identificador técnico (nombre de archivo, clave interna del juego, ruta, nombre de ajuste de PKHeX) se corta por cualquier carácter y es lo correcto: no tiene dónde partirse mejor.
            var (ini, fin) = (a, b);
            while (ini > 0 && !char.IsWhiteSpace(texto[ini - 1])) ini--;
            while (fin < texto.Length && !char.IsWhiteSpace(texto[fin])) fin++;
            if (EsIdentificadorTecnico(texto[ini..fin])) continue;
            return $"{texto[a..inicio]}|{texto[inicio..b]}";
        }
        return null;
    }

    /// <summary>
    /// Si una «palabra» sin espacios (con la puntuación que lleve pegada) es un identificador técnico por su forma: contiene «_», «/» o «\», una extensión («.pgf,») o nombre con punto entre letras («archivo.gci», «CommunityToolkit.Mvvm»),
    /// letras y cifras mezcladas, o minúscula ASCII seguida de mayúscula ASCII («AutoLoadSaveOnStartup»; «PokéMiles» no, porque «é» no es ASCII). Una palabra normal de un idioma («Pokémon», «Colosseum») no lo es.
    /// </summary>
    public static bool EsIdentificadorTecnico(string token)
    {
        if (token.Length > 2 && token[0] == '.' && char.IsLetter(token[1])) return true;   // una extensión suelta («.pgf,»)
        var t = token.Trim('(', ')', '[', ']', '«', '»', '"', '\'', ',', ';', ':', '.', '…', '!', '?');
        if (t.Length < 2) return false;
        if (t.IndexOfAny(['_', '/', '\\']) >= 0) return true;
        for (var i = 1; i < t.Length - 1; i++) if (t[i] == '.' && char.IsLetter(t[i - 1]) && char.IsLetter(t[i + 1])) return true;
        if (t.Any(char.IsDigit) && t.Any(char.IsLetter)) return true;
        for (var i = 1; i < t.Length; i++) if (t[i - 1] is >= 'a' and <= 'z' && t[i] is >= 'A' and <= 'Z') return true;
        return false;
    }

    /// <summary>True si algún texto visible bajo <paramref name="raiz"/> (ya medido) tiene una palabra partida por dentro.</summary>
    public static bool HayPalabraPartida(Visual raiz)
    {
        if (raiz is TextBlock propio && Partido(propio)) return true;
        foreach (var d in raiz.GetVisualDescendants())
            if (d is TextBlock t && Partido(t)) return true;
        return false;
    }

    private static bool Partido(TextBlock t) => !string.IsNullOrEmpty(t.Text) && t.IsEffectivelyVisible && t.Opacity > 0.001 && PalabraPartida(t) is not null;
}
