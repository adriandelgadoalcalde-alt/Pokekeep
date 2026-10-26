// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Engine.Context;
using PKHeX.Core;

namespace AutoLegalidad.Engine.Generation;

/// <summary>
/// Hito 3b: demostracion de que la especie y la forma deseadas NO son alcanzables por evolucion desde el encuentro fijado, con el arbol de evoluciones del Core para el destino
/// (<c>EvolutionTree.GetEvolutionTree</c>, <c>Forward.GetEvolutions</c>). Es un dominio FINITO (el grafo de evoluciones), no un muestreo: si recorrido entero no aparece (especie, forma),
/// ninguna generacion puede ser valida (el Core la marcaria «Evolucion invalida»). Solo se consulta DESPUES de que la busqueda no encontrara nada (nunca bloquea una generacion posible).
/// </summary>
public static class EvolutionProof
{
    /// <returns>True si queda demostrado que (especie, forma) deseadas no se alcanzan desde la plantilla.</returns>
    public static bool TryDisprove(IEncounterable enc, PKM wish, Destination dest, out string evidence)
    {
        evidence = string.Empty;
        if (enc is not IEncounterTemplate tpl || tpl.Species == wish.Species) return false;
        EvolutionTree tree;
        try { tree = EvolutionTree.GetEvolutionTree(dest.Context); }
        catch (ArgumentOutOfRangeException) { return false; }

        // Formas de partida: la de la plantilla, o todas las de la especie si la plantilla es de forma dinamica (EncounterUtil.FormDynamic/FormRandom).
        var starts = new List<(ushort Species, byte Form)>();
        if (tpl.Form >= EncounterUtil.FormDynamic)
        {
            var count = 1;
            try { count = Math.Max(1, (int)dest.Personal.GetFormEntry(tpl.Species, 0).FormCount); } catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException) { }
            for (byte f = 0; f < count; f++) starts.Add((tpl.Species, f));
        }
        else starts.Add((tpl.Species, tpl.Form));

        var seen = new HashSet<(ushort, byte)>(starts);
        var queue = new Queue<(ushort Species, byte Form)>(starts);
        var reachedSpecies = false;
        var reachedForms = new List<byte>();
        while (queue.Count > 0)
        {
            var (s, f) = queue.Dequeue();
            foreach (var next in tree.Forward.GetEvolutions(s, f))
            {
                if (!seen.Add(next)) continue;
                if (next.Species == wish.Species)
                {
                    reachedSpecies = true;
                    reachedForms.Add(next.Form);
                    if (next.Form == wish.Form || FormInfo.IsFormChangeable(next.Species, next.Form, wish.Form, tpl.Context, dest.Context)) return false;
                }
                queue.Enqueue(next);
            }
        }
        var where = $"EvolutionTree.GetEvolutionTree({dest.Context}).Forward.GetEvolutions (PKHeX.Core: Legality/Evolutions/EvolutionTree.cs:13-58, Forward/IEvolutionForward.cs)";
        evidence = reachedSpecies
            ? $"Desde la plantilla {tpl.Species}-{tpl.Form} el árbol de evoluciones del destino solo llega a la especie {wish.Species} con la(s) forma(s) {string.Join(",", reachedForms.Distinct())}, nunca con la forma {wish.Form}, y esa forma no se puede cambiar (FormInfo.IsFormChangeable). Referencia técnica: {where}."
            : $"La especie {wish.Species} no está en la línea evolutiva de la plantilla {tpl.Species}-{tpl.Form} según el árbol de evoluciones del destino. Referencia técnica: {where}.";
        return true;
    }
}
