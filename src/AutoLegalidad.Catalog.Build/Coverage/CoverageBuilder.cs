// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Globalization;
using AutoLegalidad.Catalog.Core;
using AutoLegalidad.Catalog.Db;
using Microsoft.Data.Sqlite;
using PKHeX.Core;

namespace AutoLegalidad.Catalog.Coverage;

/// <summary>Una tupla de la matriz de cobertura: {release, juego, especie, forma, origen/clase} (+ variante) con su estado.</summary>
public sealed record CoverageCase(string CaseKey, string ReleaseId, int Game, int Species, int Form, string Kind, string Variant, string Expected,
    string EvidenceRef, string Status, string? ScopeNote);

/// <summary>
/// Matriz de cobertura del Hito 1. NO certifica nada: mide el tamano real del alcance. Todas las tuplas nacen <c>Planned</c> o <c>Untested</c>
/// (el resultado de ejecutarlas llegara en <c>coverage_result</c>, Hitos 2-5).
///   * <c>Untested</c>: la tupla tiene ancla concreta en el catalogo (encuentro del Core/evento aprobado, o ausencia comprobada en la disponibilidad):
///     ya se puede construir su fixture; aun no se ha ejecutado.
///   * <c>Planned</c>: esta en el alcance pero le falta definicion (ruta de transferencia sin determinar, ruta GO sin verificar): Hito 3.
/// Clases (<c>encounter_kind</c>): las de <c>encounter.kind</c> + <c>Transfer</c> (sin ancla nativa), <c>Absent</c> (caso negativo) y
/// <c>FormNormalization</c> (forma no almacenable que hay que normalizar conservando la especie).
/// </summary>
public static class CoverageBuilder
{
    public sealed record Result(IReadOnlyList<CoverageCase> Cases, IReadOnlyList<string> SkippedGames, int GamesInMatrix);

    private sealed record Anchor(string Kind, string Key, int Species, int Form);

    public static Result Build(SqliteConnection c, string releaseId)
    {
        // ---- juegos que son destino real (no origen-solo) y con disponibilidad calculable ----
        var games = new List<(int Id, string Name, string Basis, bool OriginOnly, EntityContext Ctx, int Generation)>();
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "SELECT game_id, version_name, availability_basis, is_transfer_origin_only, context, COALESCE(generation, 0) FROM game WHERE is_group=0 ORDER BY game_id";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                games.Add((r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetInt32(3) == 1, Enum.Parse<EntityContext>(r.GetString(4)), r.GetInt32(5)));
        }
        var skipped = games.Where(g => g.Basis == "Unknown").Select(g => g.Name).ToList();
        var dest = games.Where(g => !g.OriginOnly && g.Basis != "Unknown").ToList();
        var gameById = games.ToDictionary(g => g.Id);

        // ---- disponibilidad ----
        var avail = new Dictionary<(int Game, int Species, int Form), (string State, string Storage, string Ref)>();
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "SELECT game_id, species_id, form_id, state, form_storage, reason_ref FROM availability WHERE release_id=$r";
            cmd.Parameters.AddWithValue("$r", releaseId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                avail[(r.GetInt32(0), r.GetInt32(1), r.GetInt32(2))] = (r.GetString(3), r.GetString(4), r.GetString(5));
        }
        var forms = avail.Keys.Select(k => (k.Species, k.Form)).Distinct().OrderBy(k => k.Species).ThenBy(k => k.Form).ToList();
        var speciesForms = forms.GroupBy(x => x.Species).ToDictionary(g => g.Key, g => g.Select(x => x.Form).ToList());
        var presentSpecies = avail.Where(kv => kv.Value.State == "Present").Select(kv => (kv.Key.Game, kv.Key.Species)).ToHashSet();

        // ---- anclas: SOLO encuentros aprobados (la vista es la unica via de lectura para generar) ----
        var anchors = new Dictionary<int, Dictionary<(int, int), List<Anchor>>>();
        var goDestinations7b = dest.Where(g => g.Ctx == EntityContext.Gen7b).Select(g => g.Id).ToList();
        var goDestinations8 = dest.Where(g => g.Generation >= 8 && g.Ctx != EntityContext.Gen7b).Select(g => g.Id).ToList();
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = """
                SELECT e.encounter_key, e.species_id, e.form_id, e.kind, e.origin_game, e.context, g.game_id,
                       instr(e.constraints_json, '"randomForm":true') > 0
                  FROM v_encounter_generable e JOIN encounter_game g ON g.encounter_key = e.encounter_key
                 WHERE e.release_id = $r
                 ORDER BY e.encounter_key, g.game_id
                """;
            cmd.Parameters.AddWithValue("$r", releaseId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var key = r.GetString(0);
                var kind = r.GetString(3);
                var a = new Anchor(kind, key, r.GetInt32(1), r.GetInt32(2));
                IEnumerable<int> targets = r.GetInt32(4) == (int)GameVersion.GO
                    ? (r.GetString(5) == nameof(EntityContext.Gen7b) ? goDestinations7b : goDestinations8)
                    : [r.GetInt32(6)];
                var randomForm = r.GetInt32(7) == 1;
                foreach (var t in targets)
                {
                    if (!anchors.TryGetValue(t, out var byForm)) anchors[t] = byForm = [];
                    // Encuentros de forma dinamica (Unown, Vivillon...): valen para TODAS las formas presentes y almacenables de la especie en ese juego.
                    var anchorForms = randomForm && speciesForms.TryGetValue(a.Species, out var sf)
                        ? sf.Where(f => avail.TryGetValue((t, a.Species, f), out var av) && av.State == "Present" && av.Storage is not ("BattleOnly" or "TotemOnly" or "LordOnly")).ToList()
                        : [a.Form];
                    foreach (var af in anchorForms)
                    {
                        var anchor = a with { Form = af };
                        if (!byForm.TryGetValue((a.Species, af), out var l)) byForm[(a.Species, af)] = l = [];
                        if (!l.Any(x => x.Key == key)) l.Add(anchor);
                    }
                }
            }
        }

        var eggContexts = typeof(PKM).Assembly.GetTypes()
            .Where(t => t.Namespace == "PKHeX.Core" && t.Name.StartsWith("EncounterEgg", StringComparison.Ordinal) && t.Name.Length > "EncounterEgg".Length && !t.IsInterface)
            .Select(t => t.Name["EncounterEgg".Length..])
            .Select(s => s switch { "8b" => EntityContext.Gen8b, _ when int.TryParse(s, out var n) => (EntityContext)n, _ => EntityContext.None })
            .Where(x => x != EntityContext.None).ToHashSet();

        var cases = new List<CoverageCase>(200_000);
        void Add(int game, int sp, int f, string kind, string variant, string expected, string evidence, string status, string? note = null)
            => cases.Add(new CoverageCase($"{releaseId}|{game}|{sp}-{f}|{kind}|{variant}", releaseId, game, sp, f, kind, variant, expected, evidence, status, note));

        foreach (var g in dest)
        {
            var tree = EvolutionTree.GetEvolutionTree(g.Ctx);
            anchors.TryGetValue(g.Id, out var byForm);
            foreach (var (sp, f) in forms)
            {
                var a = avail[(g.Id, sp, f)];
                if (a.State == "Absent")
                {
                    var speciesAbsentEverywhere = !presentSpecies.Contains((g.Id, sp));
                    Add(g.Id, sp, f, "Absent", speciesAbsentEverywhere ? "species-absent" : "form-absent", "Rejected", "availability:" + a.Ref, "Untested",
                        speciesAbsentEverywhere ? "Especie ausente del juego: caso negativo (resultado esperado: sin cambio y explicación), no fallo de cobertura" : "Forma ausente del juego (la especie sí está)");
                    continue;
                }
                if (a.State != "Present") continue;

                if (a.Storage is "BattleOnly" or "TotemOnly" or "LordOnly")
                {
                    Add(g.Id, sp, f, "FormNormalization", "to-base-form", "Legalizable", "availability:" + a.Ref + ";form_storage=" + a.Storage, "Untested",
                        "Forma no almacenable en este juego: se normaliza conservando la especie");
                    continue;
                }

                var note = a.Storage == "PartyOnly" ? "Forma solo válida en el equipo (no en caja)" : null;
                var lineage = new List<(int Species, int Form)>();
                foreach (var (ps, pf) in tree.Reverse.GetPreEvolutions((ushort)sp, (byte)f)) lineage.Add((ps, pf));
                var any = false;

                // anclas nativas: (kind, direct|evolution)
                var found = new List<(Anchor A, bool Direct)>();
                if (byForm is not null)
                {
                    if (byForm.TryGetValue((sp, f), out var own)) found.AddRange(own.Select(x => (x, true)));
                    foreach (var pre in lineage)
                        if (byForm.TryGetValue(pre, out var l)) found.AddRange(l.Select(x => (x, false)));
                }
                foreach (var grp in found.GroupBy(x => (x.A.Kind, x.Direct)).OrderBy(x => x.Key.Kind, StringComparer.Ordinal).ThenByDescending(x => x.Key.Direct))
                {
                    any = true;
                    if (grp.Key.Kind == EncounterKinds.Event && grp.Key.Direct)
                    {
                        foreach (var x in grp.OrderBy(x => x.A.Key, StringComparer.Ordinal))
                            Add(g.Id, sp, f, EncounterKinds.Event, "card:" + x.A.Key[(x.A.Key.IndexOf(':', StringComparison.Ordinal) + 1)..], "Legalizable", "encounter:" + x.A.Key, "Untested", note);
                        continue;
                    }
                    var first = grp.OrderBy(x => x.A.Key, StringComparer.Ordinal).First();
                    var isGo = grp.Key.Kind == EncounterKinds.GO;
                    Add(g.Id, sp, f, grp.Key.Kind, grp.Key.Direct ? "direct" : "evolution", "Legalizable",
                        $"encounter:{first.A.Key}" + (grp.Count() > 1 ? $"(+{grp.Count() - 1})" : string.Empty),
                        "Untested",
                        isGo ? GoRouteNote(g.Ctx) + (note is null ? string.Empty : "; " + note)
                             : (grp.Key.Direct ? note : $"Evolución desde {first.A.Species}-{first.A.Form}" + (note is null ? string.Empty : "; " + note)));
                }

                // huevo: el Core tiene plantillas EncounterEgg para este contexto y la especie base puede eclosionar
                if (eggContexts.Contains(g.Ctx))
                {
                    var (bs, bf) = tree.GetBaseSpeciesForm((ushort)sp, (byte)f);
                    if (avail.TryGetValue((g.Id, bs, bf), out var baseAvail) && baseAvail.State == "Present" && baseAvail.Storage == "Storable"
                        && Breeding.CanHatchAsEgg((ushort)bs) && Breeding.CanHatchAsEgg((ushort)bs, (byte)bf, g.Ctx))
                    {
                        any = true;
                        var direct = bs == sp && bf == f;
                        Add(g.Id, sp, f, EncounterKinds.Egg, direct ? "direct" : "evolution", "Legalizable",
                            $"core:Breeding.CanHatchAsEgg({bs},{bf},{g.Ctx})+EncounterEgg{(g.Ctx == EntityContext.Gen8b ? "8b" : ((int)g.Ctx).ToString(CultureInfo.InvariantCulture))}",
                            "Untested", direct ? note : $"Eclosiona {bs}-{bf} y evoluciona" + (note is null ? string.Empty : "; " + note));
                    }
                }

                if (!any)
                    Add(g.Id, sp, f, "Transfer", "no-native-anchor", "Unknown", "availability:" + a.Ref + ";sin ancla nativa en el catálogo", "Untested",
                        TransferRouteNote(g.Ctx));
            }
        }
        return new Result(cases, skipped, dest.Count);
    }

    /// <summary>Ruta de una tupla GO segun el destino (Hito 3). La ruta GO->HOME necesita un rastreador de HOME que NO se puede crear (regla 6).</summary>
    public static string GoRouteNote(EntityContext destination) => destination == EntityContext.Gen7b
        ? "Ruta GO -> Let's Go (PB7, sin matrícula de HOME): el motor genera el origen GO con el entrenador del destino"
        : "Ruta GO -> HOME -> juego: PKHeX exige la matrícula (rastreador) de transferencia de HOME, que el producto NO puede crear (regla 6); solo se conserva una real si la entidad ya la trae";

    /// <summary>Ruta de una tupla de transferencia segun el destino (Hito 3). Cada salto lo decide el Core; nunca se inventa un identificador de servidor.</summary>
    public static string TransferRouteNote(EntityContext destination) => destination switch
    {
        EntityContext.Gen2 => "Sin encuentro nativo: alcanzable por la Cápsula del Tiempo (Gen 1 -> Gen 2); PKHeX decide la ruta",
        EntityContext.Gen3 => "Sin encuentro nativo anclado en el catálogo: ruta por determinar con PKHeX (no hay transferencia hacia Gen 3)",
        EntityContext.Gen4 => "Sin encuentro nativo: alcanzable por Pal Park (Gen 3 -> Gen 4); PKHeX decide la ruta",
        EntityContext.Gen5 => "Sin encuentro nativo: alcanzable por Poké Transfer (Gen 4 -> Gen 5); PKHeX decide la ruta",
        EntityContext.Gen6 => "Sin encuentro nativo: alcanzable por el Banco Pokémon (Gen 5 -> Gen 6); PKHeX decide la ruta",
        EntityContext.Gen7 => "Sin encuentro nativo: alcanzable por el Banco / Consola Virtual (Gen 1-6 -> Gen 7); PKHeX decide la ruta",
        EntityContext.Gen7b => "Sin encuentro nativo: Let's Go solo recibe de GO o de Gen 7b; ruta por determinar con PKHeX",
        _ => "Sin encuentro nativo: alcanzable por HOME desde generaciones anteriores; PKHeX exige la matrícula (rastreador) de HOME, que el producto NO puede crear (regla 6)",
    };

    public static void Persist(SqliteConnection c, IReadOnlyList<CoverageCase> cases)
    {
        using var tx = c.BeginTransaction();
        try
        {
            using (var ins = new PreparedInsert(c, tx, "coverage_case", "case_key", "release_id", "game_id", "species_id", "form_id", "encounter_kind", "variant_key",
                       "expected", "evidence_ref", "fixture_sha256", "status", "scope_note"))
                foreach (var k in cases)
                    ins.Run(k.CaseKey, k.ReleaseId, k.Game, k.Species, k.Form, k.Kind, k.Variant, k.Expected, k.EvidenceRef, null, k.Status, k.ScopeNote);
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }
}
