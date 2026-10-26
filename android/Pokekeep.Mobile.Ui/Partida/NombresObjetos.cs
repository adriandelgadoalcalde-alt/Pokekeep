// SPDX-License-Identifier: GPL-3.0-or-later
using PKHeX.Core;

namespace Pokekeep.Mobile.Ui.Partida;

/// <summary>Nombres de máquinas con su movimiento: las tablas de aprendizaje pertenecen al Core.</summary>
public static class NombresObjetos
{
    public static string[] Lista(GameStrings textos, SaveFile sav)
    {
        var lista = textos.GetItemStrings(sav.Context, sav.Version).ToArray();
        var movimientos = Movimientos(sav);
        if (movimientos.Length == 0) return lista;
        HashSet<ushort> permitidos;
        try { permitidos = sav.Inventory.Pouches.SelectMany(p => p.GetAllItems().ToArray()).ToHashSet(); }
        catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException or ArgumentOutOfRangeException or IndexOutOfRangeException) { return lista; }
        for (var id = 0; id < lista.Length; id++)
        {
            if (!permitidos.Contains((ushort)id)) continue;
            var nombre = lista[id];
            // Solo nombres formados por el prefijo localizado de máquina y su número;
            // si el juego ya da un nombre de movimiento, no se duplica.
            if (nombre.Length < 3 || !char.IsLetter(nombre[0]) || !char.IsLetter(nombre[1]) || !nombre[2..].All(char.IsDigit)) continue;
            var num = 0;
            foreach (var c in nombre[2..]) num = num * 10 + (int)char.GetNumericValue(c);
            var hm = sav.Generation <= 6 && (sav.Generation switch
            {
                1 => ItemConverter.IsItemHM1((ushort)id),
                2 => ItemConverter.IsItemHM2((ushort)id),
                3 => ItemConverter.IsItemHM3((ushort)id),
                _ => ItemConverter.IsItemHM4((ushort)id),
            });
            var indice = num - (sav.Context == EntityContext.Gen8 ? 0 : 1);
            if (sav.Context == EntityContext.Gen9) indice = num; // posición 0 reservada en la tabla SV del Core
            if (sav.Context == EntityContext.Gen9a) indice = ItemStorage9ZA.TM.IndexOf((ushort)id); // el nombre Z-A está permutado
            if (hm) indice = num - 1 + (sav.Generation switch { 1 or 2 or 3 => 50, 4 => PersonalInfo4.MachineMovesTechnical.Length, 5 => 95, _ => 100 });
            var tabla = movimientos;
            if (sav.Context == EntityContext.Gen8 && ItemStorage8SWSH.IsTechRecord((ushort)id))
                tabla = PersonalInfo8SWSH.MachineMovesRecord.ToArray();
            if ((uint)indice < tabla.Length && tabla[indice] < textos.movelist.Length)
                lista[id] = nombre + " — " + textos.movelist[tabla[indice]];
        }
        return lista;
    }

    public static ushort[] Movimientos(SaveFile sav) => sav.Context switch
    {
        EntityContext.Gen1 => PersonalInfo1.MachineMoves.ToArray().Select(x => (ushort)x).ToArray(),
        EntityContext.Gen2 => PersonalInfo2.MachineMoves.ToArray().Select(x => (ushort)x).ToArray(),
        EntityContext.Gen3 => [.. PersonalInfo3.MachineMovesTechnical, .. PersonalInfo3.MachineMovesHidden],
        EntityContext.Gen4 => [.. PersonalInfo4.MachineMovesTechnical, .. (GameVersion.HGSS.Contains(sav.Version) ? PersonalInfo4.MachineMovesHiddenHGSS : PersonalInfo4.MachineMovesHiddenDPPt)],
        EntityContext.Gen5 => PersonalInfo5BW.MachineMoves.ToArray(),
        EntityContext.Gen6 => GameVersion.ORAS.Contains(sav.Version) ? LearnSource6AO.MachineMoves.ToArray() : LearnSource6XY.MachineMoves.ToArray(),
        EntityContext.Gen7 => PersonalInfo7.MachineMoves.ToArray(),
        EntityContext.Gen7b => LearnSource7GG.MachineMoves.ToArray(),
        EntityContext.Gen8 => PersonalInfo8SWSH.MachineMovesTechnical.ToArray(),
        EntityContext.Gen8b => PersonalInfo8BDSP.MachineMoves.ToArray(),
        EntityContext.Gen9 => PersonalInfo9SV.MachineMoves.ToArray(),
        EntityContext.Gen9a => PersonalInfo9ZA.MachineMoves.ToArray(),
        _ => [],
    };
}
