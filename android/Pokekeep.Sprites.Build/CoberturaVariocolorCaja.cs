// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors

/// <summary>C17: rechaza entregas parciales de los juegos con textura variocolor nativa.</summary>
internal static class CoberturaVariocolorCaja
{
    private static readonly HashSet<string> ExcepcionesPla =
    [
        "caja_pla_0059_02", "caja_pla_0101_02", "caja_pla_0549_02",
        "caja_pla_0713_02", "caja_pla_0900_01",
    ];

    internal static void Validar(IEnumerable<string> nombres)
    {
        var claves = nombres.ToHashSet(StringComparer.Ordinal);
        foreach (var nombre in claves)
        {
            var partes = nombre.Split('_');
            if (partes.Length < 4 || partes[0] != "caja" || partes[2] == "0000") continue;
            var propio = partes[1] is "bdsp" or "pla" or "za";
            if (nombre.EndsWith("_s", StringComparison.Ordinal))
            {
                if (!claves.Contains(nombre[..^2])) throw new InvalidDataException("C17: variocolor sin icono normal: " + nombre);
            }
            else if (propio && !ExcepcionesPla.Contains(nombre) && !claves.Contains(nombre + "_s"))
                throw new InvalidDataException("C17: falta el icono de caja variocolor: " + nombre);
        }
    }
}
