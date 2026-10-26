// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using PKHeX.Core;

namespace Pokekeep.Mobile.Ui.Cajas;

/// <summary>
/// Reversion de un lote de casillas para el historial del Core (<see cref="SlotChangelog.ISlotReversion"/>): el contenido de cada casilla y, ademas, los punteros de equipos de batalla de las cajas
/// que las ordenaciones de <c>SaveFile.SortBoxes</c> reordenan (<c>SlotPointerUtil.UpdateRepointFrom</c>). Sin los punteros, Deshacer devolveria los Pokemon a su sitio pero los equipos de combate
/// de Gen. 7-9 apuntarian a otra casilla: no seria un Deshacer exacto.
/// </summary>
internal sealed class ReversionLote : SlotChangelog.ISlotReversion
{
    private readonly IReadOnlyList<ISlotInfo> _casillas;
    private readonly PKM[] _contenido;
    private readonly int[][] _punteros;
    // Nombres, fondos, cajas desbloqueadas y banderas: lo que cambian «Nombre y fondo» y mover una caja (junto con su contenido).
    private readonly string[]? _nombres;
    private readonly int[]? _fondos;
    private readonly int _desbloqueadas;
    private readonly byte[] _banderas;

    public ReversionLote(SaveFile sav, IReadOnlyList<ISlotInfo> casillas)
    {
        _casillas = casillas;
        _contenido = [.. casillas.Select(c => c.Read(sav).Clone())];
        _punteros = [.. Punteros(sav).Select(a => a.ToArray())];
        if (sav.HasBox)
        {
            if (sav is IBoxDetailNameRead r) _nombres = [.. Enumerable.Range(0, sav.BoxCount).Select(r.GetBoxName)];
            if (sav is IBoxDetailWallpaper w) _fondos = [.. Enumerable.Range(0, sav.BoxCount).Select(w.GetBoxWallpaper)];
            _desbloqueadas = sav.BoxesUnlocked;
            _banderas = sav.BoxFlags.ToArray();
        }
        else _banderas = [];
    }

    public IReadOnlyList<ISlotInfo> Slots => _casillas;
    public SlotChangelog.ISlotReversion CreateInverse(SaveFile parent) => new ReversionLote(parent, _casillas);

    public void Revert(SaveFile parent)
    {
        // Solo se le pasan casillas de caja (el equipo lo restaura el Core con su propia reversion): el orden da igual.
        for (var i = 0; i < _casillas.Count; i++)
            _casillas[i].WriteTo(parent, _contenido[i].Clone(), EntityImportSettings.None);
        var vivos = Punteros(parent);
        for (var i = 0; i < vivos.Count && i < _punteros.Length; i++)
            Array.Copy(_punteros[i], vivos[i], Math.Min(_punteros[i].Length, vivos[i].Length));
        if (!parent.HasBox) return;
        if (_nombres is not null && parent is IBoxDetailName n) for (var i = 0; i < _nombres.Length; i++) if (n.GetBoxName(i) != _nombres[i]) n.SetBoxName(i, _nombres[i]);
        if (_fondos is not null && parent is IBoxDetailWallpaper w) for (var i = 0; i < _fondos.Length; i++) if (w.GetBoxWallpaper(i) != _fondos[i]) w.SetBoxWallpaper(i, _fondos[i]);
        if (_desbloqueadas >= 0 && parent.BoxesUnlocked != _desbloqueadas) parent.BoxesUnlocked = _desbloqueadas;
        if (_banderas.Length > 0 && !parent.BoxFlags.AsSpan().SequenceEqual(_banderas)) parent.BoxFlags = [.. _banderas];
    }

    [DynamicDependency(DynamicallyAccessedMemberTypes.NonPublicFields, typeof(PokeListHeader))]
    private static int[]? PunterosLetsGo(SAV7b sav)
    {
        try { return typeof(PokeListHeader).GetField("PokeListInfo", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(sav.Blocks.Storage) as int[]; }
        catch { return null; }
    }

    /// <summary>Los arreglos VIVOS de punteros de equipo de batalla a casillas de caja del guardado (los mismos que el Core reasigna al ordenar).</summary>
    public static IReadOnlyList<int[]> Punteros(SaveFile sav) => sav switch
    {
        SAV7 s7 => [s7.BoxLayout.TeamSlots],
        SAV8BS bs => [bs.BoxLayout.TeamSlots],
        SAV8SWSH sw => [sw.TeamIndexes.TeamSlots],
        SAV9SV sv => [sv.TeamIndexes.TeamSlots],
        SAV9ZA za => [za.TeamIndexes.TeamSlots],
        SAV7b lg => PunterosLetsGo(lg) is { } p ? [p] : [],
        _ => [],
    };
}
