// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Runtime.CompilerServices;
using PKHeX.Core;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen6y7;

/// <summary>Las listas filtradas por el guardado (<c>GameInfo.FilteredSources</c> de PKHeX) que usan los formularios de Pokémon de Gen. 6-7, creadas una vez por editor abierto.</summary>
internal static class FuentesGen6y7
{
    private static readonly ConditionalWeakTable<ContextoP, FilteredGameDataSource> Filtros = new();

    public static FilteredGameDataSource Filtro(ContextoP c)
    {
        // auditoría uso10 (A7): con el cambio de idioma en vivo la lista se rehace si cambian los textos del contexto (como AyudasGen3).
        if (Filtros.TryGetValue(c, out var f) && ReferenceEquals(f.Source, c.Datos)) return f;
        f = new FilteredGameDataSource(c.Sav, c.Datos, false);
        Filtros.AddOrUpdate(c, f);
        return f;
    }
}
