// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Collections;
using System.Reflection;
using PKHeX.Core;

namespace AutoLegalidad.Catalog.Core;

/// <summary>Clase de origen de un encuentro (valores de <c>encounter.kind</c> del esquema).</summary>
public static class EncounterKinds
{
    public const string Wild = "Wild", Static = "Static", Egg = "Egg", Trade = "Trade", Gift = "Gift", Event = "Event", Raid = "Raid", GO = "GO", Other = "Other";

    /// <summary>Clasifica por el nombre del tipo del Core y la tabla que lo contiene. Lo que no encaja queda en <see cref="Other"/> y se informa.</summary>
    public static string Classify(string typeName, string table)
    {
        if (typeName.Contains("GO", StringComparison.Ordinal) && typeName.StartsWith("EncounterSlot", StringComparison.Ordinal)) return GO;
        if (table.StartsWith("EncountersWC3", StringComparison.Ordinal)) return Event;
        if (typeName.StartsWith("EncounterSlot", StringComparison.Ordinal)) return Wild;
        if (typeName.StartsWith("EncounterTrade", StringComparison.Ordinal)) return Trade;
        if (typeName.StartsWith("EncounterEgg", StringComparison.Ordinal)) return Egg;
        if (typeName.StartsWith("EncounterTera", StringComparison.Ordinal) || typeName.StartsWith("EncounterDist", StringComparison.Ordinal)
            || typeName.StartsWith("EncounterMight", StringComparison.Ordinal)
            || typeName is "EncounterStatic8N" or "EncounterStatic8ND" or "EncounterStatic8NC" or "EncounterStatic8U") return Raid;
        if (typeName.StartsWith("EncounterOutbreak", StringComparison.Ordinal)) return Wild;
        if (typeName.StartsWith("EncounterGift", StringComparison.Ordinal) || typeName.StartsWith("EncounterStarter", StringComparison.Ordinal)) return Gift;
        if (typeName is "EncounterStatic4Pokewalker" or "EncounterStatic5Entree" or "EncounterStatic5Radar") return Gift;
        if (typeName.StartsWith("EncounterStatic", StringComparison.Ordinal) || typeName.StartsWith("EncounterFixed", StringComparison.Ordinal)
            || typeName.StartsWith("EncounterShadow", StringComparison.Ordinal)) return Static;
        return Other;
    }
}

/// <summary>Una plantilla de encuentro del Core, sin agrupar (una por elemento de tabla o por casilla de área).</summary>
public sealed record CoreEncounterItem(string Table, int Ordinal, string TypeName, string Kind, ushort Species, byte Form,
    GameVersion Version, EntityContext Context, byte Generation, byte LevelMin, byte LevelMax, bool IsEgg);

/// <summary>
/// Lee las tablas de encuentros del Core fijado. Son <c>internal static</c> (clases <c>Encounters*</c>), así que se leen por reflexión sobre el
/// ensamblado compilado del commit fijado; <see cref="Read"/> falla en voz alta si no encuentra ninguna tabla (p. ej. si el Core cambiara).
/// </summary>
public static class CoreEncounterReader
{
    private const BindingFlags All = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    /// <summary>
    /// Los dos repartos de la Consola Virtual que el Core guarda como un campo suelto (no como una tabla) y que por eso la lectura general no ve: el Mew de GF
    /// (<c>Encounters1VC.Gift</c>, un <c>EncounterGift1</c>) y el Celebi del Bosque Ilex (<c>Encounters2.CelebiVC</c>, un <c>EncounterStatic2</c>). Entran como <c>Gift</c>, como el resto de los
    /// repartos de Gen 1-2 (R-014). El Mew comparte clave con los Mew de <c>Encounters1GBEra.Gifts</c> (misma plantilla de tipo, versión, contexto y especie).
    /// </summary>
    public static readonly IReadOnlyList<(string Class, string Field)> VirtualConsoleSingles = [("Encounters1VC", "Gift"), ("Encounters2", "CelebiVC")];

    public static IReadOnlyList<CoreEncounterItem> Read() => [.. Enumerate(null).Select(x => x.Item)];

    /// <summary>
    /// Las plantillas MISMAS (no solo sus datos) de las tablas cuyo nombre (<c>Clase.Campo</c>) cumple el filtro, con el mismo elemento que devuelve <see cref="Read"/>.
    /// Una plantilla que está en varias listas del Core (p. ej. <c>EncountersWC3.Encounter_WC3</c> y <c>EncountersWC3.International</c>) sale una vez por lista.
    /// </summary>
    public static IReadOnlyList<(CoreEncounterItem Item, IEncounterTemplate Template)> ReadTemplates(Func<string, bool> tableFilter)
        => [.. Enumerate(tableFilter).Select(x => (x.Item, x.Template))];

    private static IEnumerable<(CoreEncounterItem Item, IEncounterTemplate Template)> Enumerate(Func<string, bool>? tableFilter)
    {
        var asm = typeof(PKM).Assembly;
        var tables = 0;
        foreach (var t in asm.GetTypes()
                     .Where(t => t is { IsAbstract: true, IsSealed: true, Namespace: "PKHeX.Core" } && t.Name.StartsWith("Encounters", StringComparison.Ordinal))
                     .OrderBy(t => t.Name, StringComparer.Ordinal))
        {
            foreach (var f in t.GetFields(All).OrderBy(f => f.Name, StringComparer.Ordinal))
            {
                if (f.GetValue(null) is not IEnumerable seq || f.FieldType == typeof(string))
                    continue;
                var table = $"{t.Name}.{f.Name}";
                if (tableFilter is not null && !tableFilter(table))
                    continue;
                var any = false;
                var ordinal = 0;
                foreach (var o in seq)
                {
                    if (o is null) { ordinal++; continue; }
                    if (o is IEncounterTemplate template)
                    {
                        any = true;
                        yield return (Make(table, ordinal, template), template);
                    }
                    else if (SlotsOf(o) is { } slots)
                    {
                        var i = 0;
                        foreach (var s in slots)
                            if (s is IEncounterTemplate st)
                            {
                                any = true;
                                yield return (Make(table, (ordinal * 100_000) + i++, st), st);
                            }
                    }
                    ordinal++;
                }
                if (any) tables++;
            }
        }
        if (tables == 0 && tableFilter is null)
            throw new InvalidOperationException("No se encontró ninguna tabla de encuentros en el Core: ¿cambió su estructura interna?");

        // Los repartos sueltos de la Consola Virtual (R-014): ver VirtualConsoleSingles. Fallan en voz alta si el Core los mueve.
        foreach (var (cls, field) in VirtualConsoleSingles)
        {
            var table = $"{cls}.{field}";
            if (tableFilter is not null && !tableFilter(table))
                continue;
            var type = asm.GetType("PKHeX.Core." + cls) ?? throw new InvalidOperationException($"El Core ya no tiene la clase {cls}.");
            var fi = type.GetField(field, All) ?? throw new InvalidOperationException($"El Core ya no tiene el campo {table}.");
            if (fi.GetValue(null) is not IEncounterTemplate single)
                throw new InvalidOperationException($"{table} ya no es una plantilla de encuentro.");
            yield return (Make(table, 0, single), single);
        }
    }

    private static IEnumerable? SlotsOf(object area)
    {
        var t = area.GetType();
        var p = t.GetProperty("Slots", BindingFlags.Public | BindingFlags.Instance);
        if (p?.GetValue(area) is IEnumerable e1) return e1;
        var fi = t.GetField("Slots", BindingFlags.Public | BindingFlags.Instance);
        return fi?.GetValue(area) as IEnumerable;
    }

    private static CoreEncounterItem Make(string table, int ordinal, IEncounterTemplate e)
    {
        var typeName = e.GetType().Name;
        var context = e is IContext c ? c.Context : EntityContext.None;
        var kind = CoreEncounterReader.VirtualConsoleSingles.Any(v => table == $"{v.Class}.{v.Field}") ? EncounterKinds.Gift : EncounterKinds.Classify(typeName, table);
        return new CoreEncounterItem(table, ordinal, typeName, kind, e.Species, e.Form,
            e.Version, context, e.Generation, e.LevelMin, e.LevelMax, e.IsEgg);
    }
}
