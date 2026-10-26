// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;

namespace AutoLegalidad.Ui.Bulk;

/// <summary>Qué casillas entran en una operación masiva (diseño §2.1).</summary>
public enum BulkScopeKind
{
    /// <summary>Todas las cajas y el equipo (las zonas extra de PKHeX son de solo lectura y no se tocan: diseño §2.5).</summary>
    All,
    /// <summary>Las cajas elegidas.</summary>
    Boxes,
    /// <summary>Los Pokémon marcados en el diálogo.</summary>
    Marked,
    /// <summary>La casilla del clic derecho.</summary>
    Single,
}

public enum BulkSkipReason
{
    /// <summary>Los huevos no se editan (su legalidad depende del estado de huevo).</summary>
    Egg,
    /// <summary>Casilla bloqueada o protegida: PKHeX no deja escribir en ella.</summary>
    Locked,
}

public sealed record BulkSkippedSlot(ISlotInfo Slot, BulkSkipReason Reason, string Description);

/// <summary>Una zona extra de PKHeX que Pokekeep NO modifica, con el motivo (se lista para que el usuario sepa que no se olvidó).</summary>
public sealed record BulkUntouchedZone(string Name, int Pokemon, string Reason);

public sealed record BulkSelectedSlot(ISlotInfo Slot, PKM Pokemon);

/// <summary>Resultado de resolver un ámbito: qué se procesa y qué se omite, con motivo.</summary>
public sealed class BulkSelection
{
    public required BulkScopeKind Kind { get; init; }
    public required IReadOnlyList<BulkSelectedSlot> Included { get; init; }
    public required IReadOnlyList<BulkSkippedSlot> Skipped { get; init; }
    public required IReadOnlyList<BulkUntouchedZone> UntouchedZones { get; init; }
    /// <summary>Cajas que contienen algún Pokémon incluido.</summary>
    public int BoxesTouched { get; init; }
    public int PartyIncluded { get; init; }

    public int SkippedEggs => Skipped.Count(s => s.Reason == BulkSkipReason.Egg);
    public int SkippedLocked => Skipped.Count(s => s.Reason == BulkSkipReason.Locked);

    /// <summary>«147 Pokémon en 6 cajas + equipo (6). Se omiten: 3 huevos, 2 casillas bloqueadas.» (diseño §2.4).</summary>
    public string Describe()
    {
        var parts = new List<string>();
        var inBoxes = Included.Count - PartyIncluded;
        var main = Kind == BulkScopeKind.Single ? (Included.Count == 1 ? "1 Pokémon" : AppIdioma.T("Ningún Pokémon", "No Pokémon"))
            : AppIdioma.F("{0} Pokémon en {1}", "{0} Pokémon in {1}", inBoxes, Plural(BoxesTouched, AppIdioma.T("caja", "box"), AppIdioma.T("cajas", "boxes"))) + (PartyIncluded > 0 ? AppIdioma.F(" + equipo ({0})", " + party ({0})", PartyIncluded) : string.Empty);
        if (SkippedEggs > 0) parts.Add(Plural(SkippedEggs, AppIdioma.T("huevo", "egg"), AppIdioma.T("huevos", "eggs")));
        if (SkippedLocked > 0) parts.Add(Plural(SkippedLocked, AppIdioma.T("casilla bloqueada", "locked slot"), AppIdioma.T("casillas bloqueadas", "locked slots")));
        var text = main + ".";
        if (parts.Count > 0) text += AppIdioma.T(" Se omiten: ", " Skipped:") + string.Join(", ", parts) + ".";
        var zones = UntouchedZones.Where(z => z.Pokemon > 0).ToList();
        if (zones.Count > 0 && Kind == BulkScopeKind.All)
            text += AppIdioma.T(" Sin tocar (PKHeX no permite escribir ahí): ", " Untouched (PKHeX doesn’t allow writing there):") + string.Join(", ", zones.Select(z => $"{z.Name} ({z.Pokemon})")) + ".";
        return text;
    }

    private static string Plural(int n, string one, string many) => $"{n} {(n == 1 ? one : many)}";
}

/// <summary>
/// Resuelve el ámbito de una operación masiva. Se excluyen siempre: casillas vacías, huevos, casillas bloqueadas o protegidas (<c>CanWriteTo</c> falso) y las zonas extra de PKHeX
/// (<see cref="SupportedExtraTypes"/> está vacío a propósito: con PKHeX 26.10.18 todas son de solo lectura o casillas de guion del juego; si una versión futura las hace escribibles se
/// revisan una a una con un ADR, no se activan solas).
/// </summary>
public static class BulkScope
{
    /// <summary>Tipos de zona extra que la edición masiva admite. Hoy ninguno (diseño §2.5).</summary>
    public static readonly IReadOnlySet<StorageSlotType> SupportedExtraTypes = new HashSet<StorageSlotType>();

    public static BulkSelection ForAll(SaveFile sav) => Resolve(sav, BulkScopeKind.All, BoxSlots(sav, Enumerable.Range(0, sav.HasBox ? sav.BoxCount : 0)).Concat(PartySlots(sav)), listZones: true);

    public static BulkSelection ForBoxes(SaveFile sav, IEnumerable<int> boxes, bool includeParty = false)
        => Resolve(sav, BulkScopeKind.Boxes, BoxSlots(sav, boxes.Where(b => sav.HasBox && b >= 0 && b < sav.BoxCount).Distinct().Order()).Concat(includeParty ? PartySlots(sav) : []), listZones: false);

    public static BulkSelection ForMarked(SaveFile sav, IEnumerable<ISlotInfo> marked) => Resolve(sav, BulkScopeKind.Marked, marked, listZones: false);

    public static BulkSelection ForSingle(SaveFile sav, ISlotInfo slot) => Resolve(sav, BulkScopeKind.Single, [slot], listZones: false);

    private static IEnumerable<ISlotInfo> BoxSlots(SaveFile sav, IEnumerable<int> boxes)
    {
        foreach (var b in boxes)
            for (var i = 0; i < sav.BoxSlotCount; i++)
                yield return new SlotInfoBox(b, i, sav);
    }

    private static IEnumerable<ISlotInfo> PartySlots(SaveFile sav)
    {
        if (!sav.HasParty) yield break;
        for (var i = 0; i < sav.PartyCount; i++)
            yield return new SlotInfoParty(i);
    }

    private static BulkSelection Resolve(SaveFile sav, BulkScopeKind kind, IEnumerable<ISlotInfo> candidates, bool listZones)
    {
        var included = new List<BulkSelectedSlot>();
        var skipped = new List<BulkSkippedSlot>();
        var boxes = new HashSet<int>();
        var party = 0;
        foreach (var slot in candidates)
        {
            // Zonas extra (SlotInfoMisc, GTS, guardería...): nunca entran salvo que esté en la lista explícita de admitidas.
            if (slot is SlotInfoMisc misc && !SupportedExtraTypes.Contains(misc.Type)) continue;
            PKM pk;
            try { pk = slot.Read(sav); }
            catch (Exception ex) when (ex is not OutOfMemoryException) { continue; }
            if (pk.Species == 0) continue;                                    // vacía: no es nada que editar ni se cuenta como omitida
            if (!slot.CanWriteTo(sav)) { skipped.Add(new BulkSkippedSlot(slot, BulkSkipReason.Locked, Describe(slot))); continue; }
            if (pk.IsEgg) { skipped.Add(new BulkSkippedSlot(slot, BulkSkipReason.Egg, Describe(slot))); continue; }
            included.Add(new BulkSelectedSlot(slot, pk));
            if (slot is SlotInfoBox b && slot.Type == StorageSlotType.Box) boxes.Add(b.Box);
            else party++;
        }
        return new BulkSelection
        {
            Kind = kind,
            Included = included,
            Skipped = skipped,
            UntouchedZones = listZones ? Zones(sav) : [],
            BoxesTouched = boxes.Count,
            PartyIncluded = party,
        };
    }

    public static string Describe(ISlotInfo slot) => slot is SlotInfoBox b ? Textos.CajaCasilla(b.Box, b.Slot) : AppIdioma.F("Equipo, casilla {0}", "Party, slot {0}", slot.Slot + 1);

    /// <summary>Zonas extra que PKHeX expone en este guardado (<c>GetExtraSlots(all: true)</c>), con cuántos Pokémon contienen y por qué no se modifican.</summary>
    public static IReadOnlyList<BulkUntouchedZone> Zones(SaveFile sav)
    {
        List<SlotInfoMisc> extra;
        try { extra = sav.GetExtraSlots(all: true); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return []; }
        return [.. extra.GroupBy(e => e.Type).Where(g => !SupportedExtraTypes.Contains(g.Key)).Select(g =>
        {
            var count = g.Count(s => { try { return s.Read(sav).Species != 0; } catch (Exception ex) when (ex is not OutOfMemoryException) { return false; } });
            return new BulkUntouchedZone(ZoneName(g.Key), count, ZoneReason(g.Key, g.Any(s => s.CanWriteTo(sav))));
        })];
    }

    /// <summary>Nombre de una caja para las listas de la edición masiva: «3. Caja de agua», o «Caja 3» si no tiene nombre (o el formato no lo guarda).</summary>
    public static string BoxName(SaveFile sav, int box)
    {
        try
        {
            var name = sav is IBoxDetailNameRead named ? named.GetBoxName(box) : null;
            return string.IsNullOrWhiteSpace(name) ? AppIdioma.F("Caja {0}", "Box {0}", box + 1) : $"{box + 1}. {name}";
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return AppIdioma.F("Caja {0}", "Box {0}", box + 1); }
    }

    public static string ZoneName(StorageSlotType t) => t switch
    {
        StorageSlotType.Daycare => AppIdioma.T("Guardería", "Daycare"),
        StorageSlotType.GTS => "GTS",
        StorageSlotType.PGL => "Global Link",
        StorageSlotType.BattleBox => AppIdioma.T("Caja de combate", "Battle Box"),
        StorageSlotType.Scripted => AppIdioma.T("Regalo del juego pendiente", "Pending game gift"),
        StorageSlotType.SurpriseTrade => AppIdioma.T("Intercambio Prodigioso", "Wonder Trade"),
        StorageSlotType.Shiny => AppIdioma.T("Caché de variocolores", "Shiny cache"),
        StorageSlotType.Underground => AppIdioma.T("Subsuelo", "Underground"),
        StorageSlotType.Fused or StorageSlotType.FusedKyurem or StorageSlotType.FusedNecrozmaS or StorageSlotType.FusedNecrozmaM or StorageSlotType.FusedCalyrex => AppIdioma.T("Pokémon fusionado", "Fused Pokémon"),
        StorageSlotType.Resort => "Poké Resort",
        StorageSlotType.Ride => AppIdioma.T("Montura legendaria", "Legendary ride"),
        StorageSlotType.BattleAgency => AppIdioma.T("Agencia de combate", "Battle Agency"),
        StorageSlotType.Pokéwalker => "Pokéwalker",
        _ => t.ToString(),
    };

    public static string ZoneReason(StorageSlotType t, bool writableInPkhex) => t switch
    {
        StorageSlotType.GTS or StorageSlotType.SurpriseTrade or StorageSlotType.PGL => AppIdioma.T("es un dato de servidor y PKHeX no deja escribir ahí", "it is server data and PKHeX doesn’t allow writing there"),
        StorageSlotType.Scripted or StorageSlotType.Ride => AppIdioma.T("es una casilla de guion del juego (cambiarla puede romper la historia)", "it is a story slot of the game (changing it can break the story)"),
        StorageSlotType.Underground or StorageSlotType.Shiny => AppIdioma.T("no son Pokémon del jugador (caché del juego)", "they are not the player’s Pokémon (game cache)"),
        StorageSlotType.Pokéwalker => AppIdioma.T("su origen lo fija el Pokéwalker y PKHeX no deja escribir ahí", "its origin is fixed by the Pokéwalker and PKHeX doesn’t allow writing there"),
        _ => writableInPkhex ? AppIdioma.T("Pokekeep no la modifica a propósito", "Pokekeep doesn’t modify it on purpose") : AppIdioma.T("PKHeX no deja escribir ahí", "PKHeX doesn’t allow writing there"),
    };
}
