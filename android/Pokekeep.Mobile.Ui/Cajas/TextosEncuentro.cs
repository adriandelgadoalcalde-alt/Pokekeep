// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Cajas;

public static class TextosEncuentro
{
    /// <summary>Los nombres de clase de las plantillas del Core siguen en inglés incluso con localizador español. Solo presentación: la exportación conserva el original.</summary>
    public static string Traducir(string texto) => texto
        .Replace("Static Encounter", IdiomaInterfaz.T("TextosEncuentro.EncuentroFijo"), StringComparison.Ordinal)
        .Replace("Wild Encounter", IdiomaInterfaz.T("TextosEncuentro.EncuentroSalvaje"), StringComparison.Ordinal)
        .Replace("In-game Trade", IdiomaInterfaz.T("TextosEncuentro.IntercambioJuego"), StringComparison.Ordinal)
        .Replace("Mystery Gift", IdiomaInterfaz.T("TextosEncuentro.RegaloMisterioso"), StringComparison.Ordinal)
        .Replace("Tera Raid Encounter", IdiomaInterfaz.T("TextosEncuentro.EncuentroTeraincursion"), StringComparison.Ordinal)
        .Replace("a crystal cavern", IdiomaInterfaz.T("TextosEncuentro.CavernaCristal"), StringComparison.Ordinal);
}
