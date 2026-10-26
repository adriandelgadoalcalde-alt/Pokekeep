// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using PKHeX.Core;

namespace Pokekeep.Mobile.Ui.Partida;

/// <summary>
/// Las filas de la Pokedex y de otras tablas nombran la especie al principio («Absol (Nevada Corona n.º 107) · Capturado», «025 Pikachu», «Pikachu · Mostrar variocolor»): de ahi se saca el numero para
/// dibujar su sprite sin tocar los catalogos de cada generacion. Solo vale si el nombre es EXACTAMENTE el de una especie (nunca por parecido).
/// </summary>
public static partial class EspecieEnTexto
{
    private static readonly ConditionalWeakTable<GameStrings, Dictionary<string, int>> Cache = new();

    [GeneratedRegex(@"^\s*(?:(?:n\.º\s*)?\d+\s*[·:\-–.]?\s+)?(.+?)(?:\s+\(|\s+·|\s*:|\s+-\s|$)", RegexOptions.CultureInvariant)]
    private static partial Regex Prefijo();

    private static Dictionary<string, int> Tabla(GameStrings t) => Cache.GetValue(t, s =>
    {
        var d = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 1; i < s.Species.Count; i++) if (!string.IsNullOrWhiteSpace(s.Species[i])) d.TryAdd(s.Species[i].Trim(), i);
        return d;
    });

    /// <summary>Numero de la especie con la que empieza el texto, o 0.</summary>
    public static int De(GameStrings textos, string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return 0;
        var m = Prefijo().Match(nombre);
        return m.Success && Tabla(textos).TryGetValue(m.Groups[1].Value.Trim(), out var sp) ? sp : 0;
    }
}
