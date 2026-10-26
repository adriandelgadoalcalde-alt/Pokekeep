// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Reflection;
using AutoLegalidad.Engine.SolutionCache;
using PKHeX.Core;

namespace AutoLegalidad.Engine.Optimization;

/// <summary>
/// Precalculo de las plantillas de Teratipo de la cache de soluciones (R-011, ADR 0042): enumera TODAS las incursiones de Teratipo, distribuciones y 7 estrellas que el Core conoce y deja en
/// <c>soluciones.sqlite</c> la tabla universal de cada una (ADR 0041), con las mismas claves que usaria «Legalizar» en el producto. Solo se ejecuta en una compilacion de desarrollo con la cache en modo REGISTRO
/// (el Worker del paquete es de solo lectura y no tiene este mandato).
/// </summary>
public static class TeraUniversePrecompute
{
    /// <summary>Una plantilla de incursion con la que se precalcula una tabla (una por especie y forma que «Legalizar» puede pedir).</summary>
    public sealed record Template(string Id, string Group, int Stars, ushort Species, byte Form, string Description)
    {
        internal ITeraRaid9 Encounter { get; init; } = null!;
    }

    /// <summary>Resumen de una ejecucion.</summary>
    public sealed record Summary(int Templates, int Recorded, int AlreadyPresent, int NotEligible, int Incomplete, int NoBuckets, long Bytes, long ScanMs, TimeSpan Elapsed);

    /// <summary>Grupos de plantillas, de mas a menos util para el usuario: 7 estrellas, distribuciones (eventos), 6 estrellas, el resto.</summary>
    public static readonly string[] AllGroups = ["siete", "dist", "base", "kitakami", "arandano"];

    // Encounters9 es interna en el Core: sus tablas se leen por reflexion (solo lectura; el Core no se toca).
    private static readonly (string Field, string Group)[] Tables =
    [
        ("Might", "siete"), ("Dist", "dist"), ("TeraBase", "base"), ("TeraDLC1", "kitakami"), ("TeraDLC2", "arandano"),
    ];

    /// <summary>Todas las plantillas cuya semilla busca el motor (las que no fijan los IV; <see cref="FixedIvSets"/>), con una entrada por especie y forma posible.</summary>
    public static List<Template> Enumerate(IReadOnlyCollection<string>? groups = null, IReadOnlyCollection<int>? stars = null)
    {
        var type = typeof(PKM).Assembly.GetType("PKHeX.Core.Encounters9") ?? throw new InvalidOperationException("El Core no tiene Encounters9.");
        var list = new List<Template>();
        foreach (var (field, group) in Tables)
        {
            if (groups is { Count: > 0 } && !groups.Contains(group)) continue;
            var f = type.GetField(field, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public) ?? throw new InvalidOperationException($"Encounters9.{field} no existe en este Core.");
            var n = 0;
            foreach (var o in (System.Collections.IEnumerable)f.GetValue(null)!)
            {
                var enc = (ITeraRaid9)o;
                n++;
                if (FixedIvSets.Specified(enc)) continue; // los IV los fija el encuentro: no hay busqueda de semillas
                if (stars is { Count: > 0 } && !stars.Contains(enc.Stars)) continue;
                foreach (var form in FormsOf(enc))
                    list.Add(new Template($"{group}#{n}", group, enc.Stars, enc.Species, form, $"{enc.Name} sp{enc.Species}-{form} tera{enc.TeraType}") { Encounter = enc });
            }
        }
        return list;
    }

    /// <summary>Formas que «Legalizar» puede pedir: la del encuentro, o todas las del juego si el encuentro deja la forma libre (Tatsugiri, Maushold...).</summary>
    private static IEnumerable<byte> FormsOf(ITeraRaid9 enc)
    {
        if (enc is not IEncounterFormRandom { IsRandomUnspecificForm: true })
        {
            yield return enc.Form;
            yield break;
        }
        var count = Math.Max(1, (int)PersonalTable.SV[enc.Species, 0].FormCount);
        for (byte form = 0; form < count; form++)
            if (PersonalTable.SV.IsPresentInGame(enc.Species, form)) yield return form;
    }

    /// <summary>
    /// Registra la tabla universal de cada plantilla. Las plantillas que comparten la tirada de incursion (misma dificultad y mapa, o misma distribucion) se barren JUNTAS en un unico recorrido de las 2^32 semillas
    /// (<see cref="UniverseSelector"/>, comprobado contra el Core antes de usarlo); si un grupo no admite selector se barre plantilla a plantilla. <paramref name="limitPerGroup"/> acota cada recorrido; uno incompleto no
    /// registra nada. <paramref name="log"/> recibe una linea por grupo y otra por plantilla.
    /// </summary>
    public static Summary Run(ISolutionCache cache, IReadOnlyList<Template> templates, TimeSpan limitPerGroup, TextWriter log, CancellationToken ct)
    {
        var started = DateTime.UtcNow;
        int recorded = 0, present = 0, notEligible = 0, incomplete = 0, noBuckets = 0, done = 0;
        long bytes = 0, scanMs = 0;

        // Grupos: dificultad y mapa (Tera9), distribucion (Dist9) o la plantilla sola.
        var groups = templates.GroupBy(GroupKey).ToList();

        foreach (var group in groups)
        {
            if (ct.IsCancellationRequested) break;
            var members = group.ToList();
            var models = new IEncounterUniverseModel[members.Count];
            var pks = new PKM[members.Count];
            for (var i = 0; i < members.Count; i++) (models[i], pks[i]) = Prepare(members[i]);

            UniverseSelector? selector = null;
            var how = "plantilla a plantilla";
            if (members[0].Encounter is EncounterTera9 or EncounterDist9)
            {
                try
                {
                    selector = BuildSelector(members);
                    selector.Verify([.. members.Select(m => m.Encounter)], 300_000, group.Key);
                    how = "barrido compartido";
                }
                catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
                {
                    log.WriteLine($"[aviso] {group.Key}: sin selector compartido ({ex.Message}); se barre plantilla a plantilla.");
                    selector = null;
                }
            }

            var t0 = DateTime.UtcNow;
            log.WriteLine($"[grupo] {group.Key}: {members.Count} plantillas, {how}");
            log.Flush();
            (SeedSpaceSearch.PrecomputeOutcome Outcome, int Buckets, int Bytes, long Ms)[] results;
            if (selector is not null)
                results = SeedSpaceSearch.PrecomputeUniverseGroup(cache, models, pks, selector, DateTime.UtcNow + limitPerGroup, ct);
            else
            {
                results = new (SeedSpaceSearch.PrecomputeOutcome, int, int, long)[members.Count];
                for (var i = 0; i < members.Count && !ct.IsCancellationRequested; i++)
                    results[i] = SeedSpaceSearch.PrecomputeUniverseGroup(cache, [models[i]], [pks[i]], null, DateTime.UtcNow + limitPerGroup, ct)[0];
            }

            for (var i = 0; i < members.Count; i++)
            {
                var (outcome, buckets, size, ms) = results[i];
                switch (outcome)
                {
                    case SeedSpaceSearch.PrecomputeOutcome.Recorded: recorded++; bytes += size; scanMs += ms; break;
                    case SeedSpaceSearch.PrecomputeOutcome.AlreadyPresent: present++; bytes += size; break;
                    case SeedSpaceSearch.PrecomputeOutcome.NotEligible: notEligible++; break;
                    case SeedSpaceSearch.PrecomputeOutcome.Incomplete: incomplete++; break;
                    default: noBuckets++; scanMs += ms; break;
                }
                log.WriteLine($"[{++done}/{templates.Count}] {members[i].Id} {members[i].Description}: {outcome}, {buckets} casillas, {size} bytes");
            }
            log.WriteLine($"[grupo] {group.Key}: {(DateTime.UtcNow - t0).TotalSeconds:0.0} s");
            log.Flush();
        }
        return new Summary(templates.Count, recorded, present, notEligible, incomplete, noBuckets, bytes, scanMs, DateTime.UtcNow - started);
    }

    /// <summary>Plantillas con la misma clave comparten TODAS las tiradas de seleccion: dificultad y mapa (Tera9) o distribucion (mismo <c>Index</c> y mismos totales por fase, Dist9).</summary>
    internal static string GroupKey(Template t) => t.Encounter switch
    {
        EncounterTera9 e => $"tera|{e.Stars}|{e.Map}",
        EncounterDist9 e => $"dist|{e.Index}|{Dist9GroupSelector.SignatureOf(e)}",
        _ => "solo|" + t.Id,
    };

    private static readonly SimpleTrainerInfo Trainer = new(GameVersion.SL) { OT = "KEEP", TID16 = 12345, SID16 = 54321, Language = (int)LanguageID.Spanish };

    /// <summary>Modelo universal y entidad sintetica (de la especie y forma de la plantilla) con los que se precalcula y se calcula la clave de la tabla.</summary>
    internal static (IEncounterUniverseModel Model, PKM Pk) Prepare(Template t)
    {
        var pk = t.Encounter.ConvertToPKM(Trainer);
        pk.Form = t.Form;
        return (new Tera9Model(t.Encounter, (byte)pk.TeraTypeOriginal), pk);
    }

    /// <summary>Selector del grupo: una entrada por encuentro DISTINTO (varias plantillas pueden compartir encuentro con otra forma) con los indices de sus variantes.</summary>
    internal static UniverseSelector BuildSelector(IReadOnlyList<Template> members)
    {
        if (members.Count > UniverseSelector.MaxOwners) throw new InvalidOperationException($"El grupo tiene {members.Count} plantillas y el selector admite {UniverseSelector.MaxOwners}.");
        var distinct = new List<ITeraRaid9>();
        var variants = new List<List<int>>();
        for (var i = 0; i < members.Count; i++)
        {
            var at = distinct.FindIndex(e => ReferenceEquals(e, members[i].Encounter));
            if (at < 0) { distinct.Add(members[i].Encounter); variants.Add([]); at = distinct.Count - 1; }
            variants[at].Add(i);
        }
        var v = variants.Select(l => l.ToArray()).ToArray();
        return members[0].Encounter switch
        {
            EncounterTera9 => new Tera9GroupSelector([.. distinct.Cast<EncounterTera9>()], v),
            EncounterDist9 => new Dist9GroupSelector([.. distinct.Cast<EncounterDist9>()], v),
            _ => throw new InvalidOperationException("Tipo de plantilla sin selector."),
        };
    }
}
