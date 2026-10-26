// SPDX-License-Identifier: GPL-3.0-or-later
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Ajustes.Pkhex;
using Pokekeep.Mobile.Ui.Modelo;

namespace Pokekeep.Mobile.Ui.Localizacion;

/// <summary>Solo cuando el original carece de descripción inglesa. Motivos por ruta en respaldos-ajustes-x36.tsv.</summary>
internal static class RespaldosAjustesIngles
{
    public static string? Descripcion(NodoAjustePkhex nodo)
    {
        var clave = "Ajustes.Pkhex.Respaldo." + nodo.Ruta;
        if (!ServicioIdioma.Recursos("en").ContainsKey(clave)) return null;
        if (nodo.Nombre.StartsWith("AllowHandleOTGen", StringComparison.Ordinal))
        {
            var contexto = nodo.Nombre["AllowHandleOTGen".Length..];
            var generacion = IdiomaInterfaz.F("Ajustes.Pkhex.Respaldo.Gen", contexto[..1]);
            GameVersion? version = contexto switch { "7b" => GameVersion.GG, "8a" => GameVersion.PLA, "8b" => GameVersion.BDSP, "9a" => GameVersion.ZA, _ => null };
            if (version is { } v) generacion += " (" + NombresJuego.Oficial(v) + ")";
            return IdiomaInterfaz.F(clave, generacion);
        }
        if (nodo.Raiz == "BattleTemplate")
            return IdiomaInterfaz.F(clave, IdiomaInterfaz.T(nodo.Ruta.StartsWith("BattleTemplate.Hover.", StringComparison.Ordinal) ? "Ajustes.Pkhex.Respaldo.Hover" : "Ajustes.Pkhex.Respaldo.Export"), NombresJuego.Oficial(GameVersion.ZA));
        return IdiomaInterfaz.T(clave);
    }
}
