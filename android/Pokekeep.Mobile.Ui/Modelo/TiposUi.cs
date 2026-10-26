// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;

namespace Pokekeep.Mobile.Ui.Modelo;

/// <summary>Un tipo para pintar como etiqueta de color (<see cref="Design.EtiquetaTipo"/>): índice del Core y nombre en el idioma de la interfaz.</summary>
public readonly record struct TipoVm(int Indice, string Texto);

/// <summary>Nombres de los tipos de un Pokemon en espanol. Gen. 1-2 numeran los tipos con huecos (tipo «pajaro» sin usar y 10 duplicados de Normal): se traduce como PKHeX (<c>GetMoveTypeGeneration</c>).</summary>
public static class TiposUi
{
    /// <summary>Los tipos de la especie como etiquetas (índice del Core + nombre en el idioma de la interfaz), sin repetir el segundo si es igual al primero.</summary>
    public static IReadOnlyList<TipoVm> Etiquetas(PKM pk, GameStrings textos)
    {
        var lista = new List<TipoVm>();
        if (pk.PersonalInfo is not IPersonalType pt) return lista;
        void Anadir(byte tipo)
        {
            var t = (int)((MoveType)tipo).GetMoveTypeGeneration(pk.Format);
            if (t >= 0 && t < textos.Types.Count && !string.IsNullOrEmpty(textos.Types[t]) && !lista.Any(x => x.Indice == t)) lista.Add(new TipoVm(t, textos.Types[t]));
        }
        Anadir(pt.Type1);
        if (pt.Type2 != pt.Type1) Anadir(pt.Type2);
        return lista;
    }

    /// <summary>Un tipo por su índice del Core; un índice fuera de la lista se queda sin nombre (la etiqueta no sale).</summary>
    public static TipoVm Etiqueta(int indice, GameStrings textos)
        => new(indice, indice >= 0 && indice < textos.Types.Count ? textos.Types[indice] : string.Empty);

    public static IReadOnlyList<string> De(PKM pk, GameStrings textos)
    {
        var lista = new List<string>();
        if (pk.PersonalInfo is not IPersonalType pt) return lista;
        void Anadir(byte tipo)
        {
            var t = (int)((MoveType)tipo).GetMoveTypeGeneration(pk.Format);
            if (t >= 0 && t < textos.Types.Count && !string.IsNullOrEmpty(textos.Types[t]) && !lista.Contains(textos.Types[t])) lista.Add(textos.Types[t]);
        }
        Anadir(pt.Type1);
        if (pt.Type2 != pt.Type1) Anadir(pt.Type2);
        return lista;
    }
}
