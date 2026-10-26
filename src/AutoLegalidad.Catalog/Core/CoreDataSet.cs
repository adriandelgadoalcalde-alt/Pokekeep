// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Globalization;
using System.Text;
using AutoLegalidad.Catalog.Db;
using AutoLegalidad.Catalog.Model;
using PKHeX.Core;

namespace AutoLegalidad.Catalog.Core;

/// <summary>
/// Todo lo que el catalogo extrae del Core oficial, calculado EN MEMORIA y sin tocar la base de datos:
/// especies, nombres, formas, juegos, disponibilidad por juego/forma (con almacenabilidad) e indice de encuentros del Core.
/// Es determinista: el mismo Core da el mismo <see cref="ContentDigest"/>.
/// </summary>
public sealed class CoreDataSet
{
    public required CoreSourceInfo Source { get; init; }
    public required IReadOnlyList<SpeciesRow> Species { get; init; }
    public required IReadOnlyList<FormRow> Forms { get; init; }
    public required IReadOnlyList<AvailabilityRow> Availability { get; init; }
    public required IReadOnlyList<EncounterRow> Encounters { get; init; }
    /// <summary>Tipos de plantilla del Core clasificados como <c>Other</c> (no encajan en ninguna clase): se informan, no se ocultan.</summary>
    public required IReadOnlyList<string> UnclassifiedTypes { get; init; }
    /// <summary>Plantillas de encuentro cuya <c>Version</c> no se resolvio a ningun juego concreto.</summary>
    public required int UnresolvedEncounters { get; init; }
    public required int RawEncounterItems { get; init; }
    public required int MgdbGiftsSkippedNotPokemon { get; init; }
    public required int MgdbGiftsDuplicate { get; init; }
    public required string ContentDigest { get; init; }

    public static readonly (string Code, LanguageID Id)[] Languages =
    [
        ("ja", LanguageID.Japanese), ("en", LanguageID.English), ("fr", LanguageID.French), ("it", LanguageID.Italian),
        ("de", LanguageID.German), ("es", LanguageID.Spanish), ("ko", LanguageID.Korean),
        ("zh-Hans", LanguageID.ChineseS), ("zh-Hant", LanguageID.ChineseT), ("es-419", LanguageID.SpanishL),
    ];

    public static CoreDataSet Build(CoreSourceInfo source)
    {
        GameCatalog.VerifyAgainstCore();

        // ---- Tablas personales estaticas del Core, para el universo de formas ----
        var tablas = typeof(PersonalTable)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => typeof(IPersonalTable).IsAssignableFrom(f.FieldType))
            .Select(f => (f.Name, Tabla: (IPersonalTable)f.GetValue(null)!))
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToList();
        var maxSpecies = tablas.Max(t => (int)t.Tabla.MaxSpeciesID);
        var strings = GameInfo.GetStrings("en");

        var forms = new List<FormRow>();
        var formSet = new HashSet<(int, int)>();
        var speciesIds = new List<int>();
        for (var sp = 1; sp <= maxSpecies; sp++)
        {
            var s = (ushort)sp;
            if (!tablas.Any(t => t.Tabla.IsSpeciesInGame(s)))
                continue;
            speciesIds.Add(sp);
            var nombres = FormConverter.GetFormList(s, strings.Types, strings.forms, EntityContext.Gen9a);
            for (var f = 0; f < 256; f++)
            {
                var donde = tablas.Where(t => t.Tabla.IsPresentInGame(s, (byte)f)).Select(t => t.Name).ToList();
                if (donde.Count == 0) continue;
                var name = f < nombres.Length && !string.IsNullOrEmpty(nombres[f]) ? nombres[f] : null;
                forms.Add(new FormRow(sp, f, name, CanonicalJson.Serialize(new Dictionary<string, object?> { ["tables"] = donde })));
                formSet.Add((sp, f));
            }
        }

        // ---- Indice de encuentros del Core (se necesita antes que la disponibilidad: GO se deduce de ahi) ----
        var items = CoreEncounterReader.Read();
        var goPresent = items.Where(i => i.Version == GameVersion.GO).Select(i => (i.Species, i.Form)).ToHashSet();

        // ---- Disponibilidad por juego CONCRETO, forma a forma ----
        var avail = new List<AvailabilityRow>(GameCatalog.Concrete.Count * forms.Count);
        foreach (var g in GameCatalog.Concrete)
        {
            foreach (var fr in forms)
            {
                var sp = (ushort)fr.Species;
                var f = (byte)fr.Form;
                switch (g.Basis)
                {
                    case AvailabilityBasis.PersonalTable:
                    {
                        var present = g.PersonalTable!.IsPresentInGame(sp, f);
                        avail.Add(new AvailabilityRow(g.Id, sp, f, present ? "Present" : "Absent",
                            $"Core:PersonalTable.{g.PersonalTableName}.IsPresentInGame({sp},{f})", present ? FormStorage(g, sp, f) : "NotApplicable"));
                        break;
                    }
                    case AvailabilityBasis.CoreEncounters:
                    {
                        var present = goPresent.Contains((sp, f));
                        avail.Add(new AvailabilityRow(g.Id, sp, f, present ? "Present" : "Absent",
                            $"Core:EncountersGO(species={sp},form={f})", present ? FormStorage(g, sp, f) : "NotApplicable"));
                        break;
                    }
                    default:
                        avail.Add(new AvailabilityRow(g.Id, sp, f, "Unknown",
                            $"Core {source.Tag}: sin datos de presencia para {g.Version}", "NotApplicable"));
                        break;
                }
            }
        }

        // ---- Especies (1025 desde el Core; introduccion = primera generacion de un juego concreto donde esta Present) ----
        var intro = avail.Where(a => a.State == "Present" && GameCatalog.Get((GameVersion)a.Game).Generation is not null)
            .GroupBy(a => a.Species).ToDictionary(g => g.Key, g => g.Min(a => GameCatalog.Get((GameVersion)a.Game).Generation));
        var species = new List<SpeciesRow>(speciesIds.Count);
        foreach (var sp in speciesIds)
        {
            var names = new List<(string, string)>();
            foreach (var (code, id) in Languages)
            {
                var n = SpeciesName.GetSpeciesName((ushort)sp, (int)id);
                if (!string.IsNullOrWhiteSpace(n)) names.Add((code, n));
            }
            species.Add(new SpeciesRow(sp, SpeciesName.GetSpeciesName((ushort)sp, (int)LanguageID.English), intro.GetValueOrDefault(sp), names));
        }

        // ---- Encuentros del Core agregados, mas eventos del MGDB embebido ----
        var enc = new List<EncounterRow>();
        var unresolved = AggregateTables(source, items, formSet, enc, out var unclassified);
        var (skipped, dup) = AddMgdb(source, formSet, enc);

        var digest = Digest(species, forms, avail, enc);
        return new CoreDataSet
        {
            Source = source, Species = species, Forms = forms, Availability = avail, Encounters = enc,
            UnclassifiedTypes = unclassified, UnresolvedEncounters = unresolved, RawEncounterItems = items.Count,
            MgdbGiftsSkippedNotPokemon = skipped, MgdbGiftsDuplicate = dup, ContentDigest = digest,
        };
    }

    /// <summary>Almacenabilidad de la forma en el juego, segun las reglas del propio Core (<c>FormInfo</c>/<c>FormVerifier</c>).</summary>
    public static string FormStorage(GameDef g, ushort species, byte form)
    {
        var format = (byte)(g.EntityFormat ?? throw new InvalidOperationException($"{g.Version} no tiene formato de entidad."));
        var ctx = g.RuleContext;
        if (FormInfo.IsBattleOnlyForm(species, form, format))
            return ctx == EntityContext.Gen9a ? "PartyOnly" : "BattleOnly"; // ZA: las formas de combate pueden existir en el equipo (FormVerifier.VerifyBattleForms9a)
        if (form != 0 && species is (ushort)PKHeX.Core.Species.Shaymin or (ushort)PKHeX.Core.Species.Furfrou or (ushort)PKHeX.Core.Species.Hoopa && format <= 6)
            return "PartyOnly"; // FormVerifier: "Party Only Forms"
        if (FormInfo.IsTotemForm(species, form, ctx))
            return "TotemOnly";
        if (FormInfo.IsLordForm(species, form, ctx))
            return "LordOnly";
        return "Storable";
    }

    private static int AggregateTables(CoreSourceInfo source, IReadOnlyList<CoreEncounterItem> items, HashSet<(int, int)> formSet,
        List<EncounterRow> output, out IReadOnlyList<string> unclassified)
    {
        var unresolved = 0;
        var other = new SortedSet<string>(StringComparer.Ordinal);
        var groups = items
            // Formas DINAMICAS del Core (30 = segun la partida, p. ej. Vivillon; 31 = al azar, p. ej. Unown): no son una forma real. El encuentro se guarda con la forma base
            // en las columnas (clave foranea) y marcado «randomForm»; la matriz lo ancla a TODAS las formas presentes del juego (ADR 0014, Hito 3).
            .Where(i => i.Species != 0 && (formSet.Contains((i.Species, i.Form)) || (i.Form >= EncounterUtil.FormDynamic && formSet.Contains((i.Species, 0)))))
            .GroupBy(i => (i.TypeName, i.Kind, i.Version, i.Context, i.Species, i.Form, i.IsEgg, i.Generation))
            .OrderBy(g => g.Key.TypeName, StringComparer.Ordinal).ThenBy(g => (int)g.Key.Version).ThenBy(g => g.Key.Species).ThenBy(g => g.Key.Form);
        foreach (var grp in groups)
        {
            var k = grp.Key;
            if (k.Kind == EncounterKinds.Other) other.Add(k.TypeName);
            var games = ResolveGames(k.Version, k.Context);
            var dynamicForm = k.Form >= EncounterUtil.FormDynamic;
            if (games.Count == 0) unresolved++;
            var tables = grp.GroupBy(i => i.Table).OrderBy(t => t.Key, StringComparer.Ordinal)
                .ToDictionary(t => t.Key, t => (object?)new Dictionary<string, object?> { ["n"] = t.Count(), ["first"] = t.Min(i => i.Ordinal), ["last"] = t.Max(i => i.Ordinal) });
            var lmin = grp.Min(i => (int)i.LevelMin);
            var lmax = grp.Max(i => (int)i.LevelMax);
            var desc = CanonicalJson.Serialize(new Dictionary<string, object?>
            {
                ["type"] = k.TypeName, ["kind"] = k.Kind, ["version"] = k.Version.ToString(), ["context"] = k.Context.ToString(),
                ["generation"] = (int)k.Generation, ["isEgg"] = k.IsEgg, ["levelMin"] = lmin, ["levelMax"] = lmax, ["items"] = grp.Count(), ["tables"] = tables,
                ["randomForm"] = dynamicForm, ["formMarker"] = dynamicForm ? (int)k.Form : null,
            });
            var key = $"core:{k.TypeName}:{k.Version}:{k.Context}:{k.Species:D4}-{k.Form}:{(k.IsEgg ? "egg" : "std")}";
            output.Add(new EncounterRow(
                key, source.SourceVersionId, null, k.Species, dynamicForm ? 0 : k.Form, OriginGame(k.Version, k.Generation), k.Context.ToString(),
                RuntimeTypes.For(k.Context, k.Generation, k.Version), k.Kind, null, Hashing.Sha256Hex(desc), desc,
                CanonicalJson.Serialize(new Dictionary<string, object?> { ["levelMin"] = lmin, ["levelMax"] = lmax, ["isEgg"] = k.IsEgg, ["generation"] = (int)k.Generation, ["randomForm"] = dynamicForm }),
                $"PKHeX.Core@{source.Commit12}:" + string.Join(",", tables.Keys), true, games.Select(g => g.Id).ToList()));
        }
        unclassified = other.ToList();
        return unresolved;
    }

    /// <summary>Eventos del MGDB embebido del Core (<c>EncounterEvent.MGDB_*</c>, publicos): una plantilla por tarjeta distinta.</summary>
    private static (int SkippedNotPokemon, int Duplicate) AddMgdb(CoreSourceInfo source, HashSet<(int, int)> formSet, List<EncounterRow> output)
    {
        var skipped = 0;
        var dup = 0;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var gift in CoreMgdbIndex.AllGifts())
        {
            if (!gift.IsEntity || gift.Species == 0) { skipped++; continue; }
            if (!formSet.Contains((gift.Species, gift.Form))) { skipped++; continue; }
            var sha = CoreMgdbIndex.StableSha256(gift);
            var type = gift.GetType().Name;
            var key = CoreMgdbIndex.KeyFor(type, sha);
            if (!seen.Add(key)) { dup++; continue; }
            var games = ResolveGames(gift.Version, gift.Context);
            // Una tarjeta con RestrictVersion solo la recibe el juego que el Core admite (WC6.cs:25-34 CanBeReceivedByVersion; EncounterPossible6.cs:230-237 la usa al enumerar): la Libreria y la matriz
            // no deben ofrecerla en X/Y si solo es de Omega Rubi/Zafiro Alfa. El motor y el catalogo comparten asi el mismo criterio del Core.
            if (gift is IRestrictVersion restrict)
                games = [.. games.Where(g => restrict.CanBeReceivedByVersion((GameVersion)g.Id))];
            var desc = CanonicalJson.Serialize(GiftInfo.Describe(gift, type, sha, source.ReleaseDate));
            output.Add(new EncounterRow(
                key, source.SourceVersionId, null, gift.Species, gift.Form, OriginGame(gift.Version, gift.Generation), gift.Context.ToString(),
                RuntimeTypes.For(gift.Context, gift.Generation, gift.Version), EncounterKinds.Event, gift.CardID > 0 ? gift.CardID.ToString(CultureInfo.InvariantCulture) : null,
                Hashing.Sha256Hex(desc), desc,
                CanonicalJson.Serialize(new Dictionary<string, object?> { ["levelMin"] = (int)gift.LevelMin, ["levelMax"] = (int)gift.LevelMax, ["isEgg"] = gift.IsEgg, ["generation"] = (int)gift.Generation }),
                $"PKHeX.Core@{source.Commit12}:EncounterEvent.MGDB:{type}", true, games.Select(g => g.Id).ToList()));
        }
        return (skipped, dup);
    }

    /// <summary>Fila de <c>game</c> a la que apunta <c>encounter.origin_game</c>: la version del encuentro, o el grupo de su generacion si el Core usa <c>Any</c>.</summary>
    public static int OriginGame(GameVersion version, byte generation)
        => version is GameVersion.Any or GameVersion.Invalid ? (int)GameUtil.GetVersion(generation) : (int)version;

    /// <summary>Juegos concretos de una version de encuentro: <c>GameVersion.Contains</c> del Core y, si es un grupo, restringido al contexto del encuentro.</summary>
    public static IReadOnlyList<GameDef> ResolveGames(GameVersion version, EntityContext context)
    {
        var all = GameCatalog.Resolve(version);
        if (version.IsValidSavedVersion() || !context.IsValid)
            return all;
        // GameVersion.Any o grupo: el contexto del encuentro (PK1..PA9) acota los juegos reales (p. ej. un WC8 de Gen8 no es de PLA/BDSP).
        var byContext = all.Where(g => g.Context == context).ToList();
        return byContext.Count != 0 ? byContext : all;
    }

    /// <summary>
    /// Digest canonico del contenido del Core (orden fijo, independiente del orden de insercion). Se calcula igual desde memoria (al construir)
    /// y desde la base de datos (al verificar): si alguien toca una fila, deja de coincidir.
    /// </summary>
    public static string Digest(IEnumerable<SpeciesRow> species, IEnumerable<FormRow> forms, IEnumerable<AvailabilityRow> avail, IEnumerable<EncounterRow> enc)
    {
        var sb = new StringBuilder(8 * 1024 * 1024);
        foreach (var s in species.OrderBy(s => s.Id))
            sb.Append("S|").Append(s.Id).Append('|').Append(s.CanonicalName).Append('|').Append(s.IntroducedGeneration).Append('|')
              .AppendJoin(';', s.Names.OrderBy(n => n.Language, StringComparer.Ordinal).Select(n => n.Language + "=" + n.Name)).Append('\n');
        foreach (var f in forms.OrderBy(f => f.Species).ThenBy(f => f.Form))
            sb.Append("F|").Append(f.Species).Append('|').Append(f.Form).Append('|').Append(f.CanonicalFormName).Append('|').Append(f.DescriptorJson).Append('\n');
        foreach (var a in avail.OrderBy(a => a.Game).ThenBy(a => a.Species).ThenBy(a => a.Form))
            sb.Append("A|").Append(a.Game).Append('|').Append(a.Species).Append('|').Append(a.Form).Append('|').Append(a.State).Append('|').Append(a.FormStorage).Append('\n');
        foreach (var e in enc.OrderBy(e => e.Key, StringComparer.Ordinal))
            sb.Append("E|").Append(e.Key).Append('|').Append(e.DescriptorSha256).Append('|').AppendJoin(',', e.Games.Distinct().OrderBy(x => x)).Append('\n');
        return Hashing.Sha256Hex(sb.ToString());
    }
}
