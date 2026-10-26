// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Text.Json;
using AutoLegalidad.Catalog.Db;
using AutoLegalidad.Catalog.Model;
using Microsoft.Data.Sqlite;

namespace AutoLegalidad.Catalog.Library;

/// <summary>
/// Construye <c>library_entry</c> (la Libreria de Pokekeep, requisito R-003) a partir de la matriz de cobertura, la disponibilidad y los encuentros:
/// una fila por tupla {destino, especie, forma, clase de origen, variante}. Es un indice DERIVADO y reconstruible: la verdad sigue en el Core y en las
/// fuentes aprobadas. Lo que no existe en el destino aparece <c>Dimmed</c> con su motivo en español; solo es <c>addable</c> lo que tiene ancla generable.
/// </summary>
public static class LibraryBuilder
{
    public sealed record Result(int Entries, int Addable, int Dimmed, int WithEventTitleEs, int HomeRequired = 0, int BlockedByRule = 0, int BlockedByMeasurement = 0);

    private sealed record Enc(string Key, string Kind, string SourceVersionId, string? ArtifactSha256, string DescriptorJson, string SourceRef, string? CoreCorroboration, int SpeciesId = 0, int FormId = 0);

    /// <summary>Tipos de tarjeta cuyo identificador >= 9000 es un regalo de Pokemon HOME (<c>IsHOMEGift</c> del Core: <c>CardID &gt;= 9000</c>; <c>HomeTrackerUtil.IsRequired</c>).</summary>
    private static readonly HashSet<string> HomeGiftCardTypes = new(StringComparer.Ordinal) { "WC8", "WB8", "WA8", "WC9", "WA9" };
    private const int HomeGiftFirstCardId = 9000;

    /// <param name="measured">Lo que midio el banco del Hito 3b (<see cref="LibraryBlocks.Load"/>): entradas imposibles (con su motivo en castellano) y entradas que solo existen pasando por HOME.</param>
    public static Result Build(SqliteConnection c, string releaseId, LibraryBlocks.Data? measured = null)
    {
        measured ??= LibraryBlocks.Data.Empty;
        var measuredBlocks = measured.Blocks;
        var species = new Dictionary<int, (string Canonical, string Es, string All)>();
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = """
                SELECT s.species_id, s.canonical_name,
                       COALESCE((SELECT name FROM species_name n WHERE n.species_id=s.species_id AND n.language_code='es'), s.canonical_name),
                       COALESCE((SELECT group_concat(name, ' ') FROM (SELECT name FROM species_name n WHERE n.species_id=s.species_id ORDER BY language_code)), s.canonical_name)
                  FROM species s
                """;
            using var r = cmd.ExecuteReader();
            while (r.Read()) species[r.GetInt32(0)] = (r.GetString(1), r.GetString(2), r.GetString(3));
        }
        var formNames = new Dictionary<(int, int), string?>();
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "SELECT species_id, form_id, canonical_form_name FROM species_form";
            using var r = cmd.ExecuteReader();
            while (r.Read()) formNames[(r.GetInt32(0), r.GetInt32(1))] = r.IsDBNull(2) ? null : r.GetString(2);
        }
        var games = new Dictionary<int, (string Version, string NameEs, int? Generation)>();
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "SELECT game_id, version_name, display_name_es, generation FROM game WHERE is_group=0";
            using var r = cmd.ExecuteReader();
            while (r.Read()) games[r.GetInt32(0)] = (r.GetString(1), r.GetString(2), r.IsDBNull(3) ? null : r.GetInt32(3));
        }
        var avail = new Dictionary<(int, int, int), (string State, string Storage, string Ref)>();
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "SELECT game_id, species_id, form_id, state, form_storage, reason_ref FROM availability WHERE release_id=$r";
            cmd.Parameters.AddWithValue("$r", releaseId);
            using var r = cmd.ExecuteReader();
            while (r.Read()) avail[(r.GetInt32(0), r.GetInt32(1), r.GetInt32(2))] = (r.GetString(3), r.GetString(4), r.GetString(5));
        }
        var encounters = new Dictionary<string, Enc>(StringComparer.Ordinal);
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = """
                SELECT e.encounter_key, e.kind, e.source_version_id, e.artifact_sha256, e.descriptor_json, e.source_ref, e.species_id, e.form_id,
                       (SELECT ao.core_corroboration FROM artifact_origin ao WHERE ao.artifact_sha256 = e.artifact_sha256 AND ao.source_version_id = e.source_version_id LIMIT 1)
                  FROM encounter e WHERE e.release_id=$r
                """;
            cmd.Parameters.AddWithValue("$r", releaseId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                encounters[r.GetString(0)] = new Enc(r.GetString(0), r.GetString(1), r.GetString(2), r.IsDBNull(3) ? null : r.GetString(3), r.GetString(4), r.GetString(5), r.IsDBNull(8) ? null : r.GetString(8), r.GetInt32(6), r.GetInt32(7));
        }
        // Hito 5d (ADR 0047): región de cada tarjeta (solo si el Core o la documentación de la tarjeta la aportan).
        var regionByKey = new Dictionary<string, (string Regions, string Source)>(StringComparer.Ordinal);
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "SELECT encounter_key, regions, source_ref FROM event_region";
            using var r = cmd.ExecuteReader();
            while (r.Read()) regionByKey[r.GetString(0)] = (r.GetString(1), r.GetString(2));
        }
        var generable = new HashSet<string>(StringComparer.Ordinal);
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "SELECT encounter_key FROM v_encounter_generable WHERE release_id=$r";
            cmd.Parameters.AddWithValue("$r", releaseId);
            using var r = cmd.ExecuteReader();
            while (r.Read()) generable.Add(r.GetString(0));
        }

        // Titulos de eventos en español de fuente fiable: la tarjeta del MISMO evento (mismo tipo, id, especie y forma) que sí está en español.
        var spanishByEvent = new Dictionary<(string Type, int CardId, int Species, int Form), string>();
        foreach (var e in encounters.Values.Where(x => x.Kind == "Event"))
        {
            using var d0 = JsonDocument.Parse(e.DescriptorJson);
            var r0 = d0.RootElement;
            if (r0.TryGetProperty("cardTitleEs", out var es0) && es0.ValueKind == JsonValueKind.String && es0.GetString() is { Length: > 0 } t0
                && r0.TryGetProperty("type", out var ty0) && ty0.GetString() is { } type0 && r0.TryGetProperty("cardId", out var ci0) && ci0.ValueKind == JsonValueKind.Number && ci0.GetInt32() > 0)   // el id 0 es «sin id»: mezclaria eventos distintos
                spanishByEvent.TryAdd((type0, ci0.GetInt32(), e.SpeciesId, e.FormId), t0);
        }

        int total = 0, addableCount = 0, dimmedCount = 0, titleEs = 0, homeCount = 0, ruleBlocks = 0, measuredBlocksCount = 0;
        using var tx = c.BeginTransaction();
        try
        {
            using var ins = new PreparedInsert(c, tx, "library_entry", "entry_key", "release_id", "game_id", "game_name", "species_id", "form_id", "dex_number", "name_es",
                "names_all", "form_name", "generation", "kind", "variant_key", "availability", "dim_reason", "addable", "form_storage", "status", "expected", "encounter_key",
                "source_label", "event_title", "event_title_es", "event_card_id", "event_language", "event_restrict_language", "event_start_date", "home_required", "dim_source", "event_title_es_source", "shiny_rule", "fixed_gender", "gigantamax",
                "event_region", "event_region_source");
            using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                SELECT case_key, game_id, species_id, form_id, encounter_kind, variant_key, expected, evidence_ref, status, scope_note
                  FROM coverage_case WHERE release_id=$r ORDER BY game_id, species_id, form_id, encounter_kind, variant_key
                """;
            cmd.Parameters.AddWithValue("$r", releaseId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var caseKey = r.GetString(0);
                var game = r.GetInt32(1);
                var sp = r.GetInt32(2);
                var f = r.GetInt32(3);
                var kind = r.GetString(4);
                var variant = r.GetString(5);
                var expected = r.GetString(6);
                var evidence = r.GetString(7);
                var status = r.GetString(8);
                var note = r.IsDBNull(9) ? null : r.GetString(9);
                var (gv, gname, gen) = games[game];
                var (_, nameEs, namesAll) = species[sp];
                var a = avail[(game, sp, f)];

                string? anchor = null;
                if (evidence.StartsWith("encounter:", StringComparison.Ordinal))
                {
                    var k = evidence["encounter:".Length..];
                    var plus = k.IndexOf("(+", StringComparison.Ordinal);
                    anchor = plus >= 0 ? k[..plus] : k;
                }
                encounters.TryGetValue(anchor ?? string.Empty, out var enc);

                var present = a.State == "Present";
                var availability = present && kind != "FormNormalization" ? "Available" : "Dimmed";
                var addable = availability == "Available" && expected == "Legalizable" && status == "Untested"
                              && ((anchor is not null && generable.Contains(anchor)) || kind == "Egg");
                string? dim = kind switch
                {
                    "Absent" => variant == "species-absent" ? $"No está en {gname}" : $"Esta forma no está en {gname}",
                    "FormNormalization" => $"Forma no almacenable en {gname}: se guarda como forma base",
                    "Transfer" => $"En {gname} solo se consigue por transferencia desde otro juego o desde HOME; la Librería todavía no la ofrece.",
                    _ when status == "Planned" => $"Origen pendiente de verificar para {gname} ({kind})",
                    _ => null,
                };
                if (!addable && dim is null) dim = "Pendiente de poder generarse con el motor";

                string? evTitle = null, evTitleEs = null, evCard = null, evLang = null, evRestrict = null, evDate = null, evTitleSource = null, evShiny = null, evRegion = null, evRegionSource = null;
                int? evGender = null;
                var evGmax = false;
                var eventIsHomeGift = false;
                var sourceLabel = kind switch { "Absent" or "FormNormalization" or "Transfer" => "Core PKHeX (tabla personal)", "Egg" => "Core PKHeX (cría)", _ => "Core PKHeX" };
                if (enc is not null)
                {
                    sourceLabel = SourceLabel(enc.SourceVersionId, enc.SourceRef);
                    if (kind == "Event")
                    {
                        if (regionByKey.TryGetValue(enc.Key, out var reg)) (evRegion, evRegionSource) = (reg.Regions, reg.Source);
                        using var d = JsonDocument.Parse(enc.DescriptorJson);
                        var root = d.RootElement;
                        string? Str(string p) => root.TryGetProperty(p, out var e) && e.ValueKind == JsonValueKind.String && e.GetString() is { Length: > 0 } s ? s : null;
                        evTitle = Str("cardTitle");
                        evTitleEs = Str("cardTitleEs");
                        var cardId = root.TryGetProperty("cardId", out var ci) && ci.ValueKind == JsonValueKind.Number ? ci.GetInt32() : 0;
                        evCard = cardId > 0 ? cardId.ToString(System.Globalization.CultureInfo.InvariantCulture) : null;
                        evLang = Str("eventLanguage");
                        evRestrict = Str("restrictLanguage");
                        evDate = Str("startDate");
                        evShiny = Str("shinyRule");
                        if (root.TryGetProperty("genderValue", out var genderEl) && genderEl.ValueKind == JsonValueKind.Number && genderEl.GetInt32() is >= 0 and <= 2) evGender = genderEl.GetInt32();   // 3 = al azar
                        evGmax = root.TryGetProperty("gigantamax", out var gmaxEl) && gmaxEl.ValueKind == JsonValueKind.True;
                        var cardType0 = Str("type");
                        if (evTitleEs is not null) evTitleSource = "card";
                        else if (HasSpanishMarks(evTitle)) { evTitleEs = evTitle; evTitleSource = "spanish-marks"; }
                        else if (cardType0 is not null && cardId > 0 && spanishByEvent.TryGetValue((cardType0, cardId, enc.SpeciesId, enc.FormId), out var sameEvent)) { evTitleEs = sameEvent; evTitleSource = "same-event"; }
                        if (evTitleEs is not null) titleEs++;
                        // Regalo de Pokemon HOME (HomeTrackerUtil.IsRequired: WC8/WB8/WA8/WC9/WA9 con CardID >= 9000): solo existe tras pasar por HOME.
                        eventIsHomeGift = Str("type") is { } cardType && HomeGiftCardTypes.Contains(cardType) && cardId >= HomeGiftFirstCardId;
                    }
                }

                // Hito 3c: la Libreria no ofrece nada que el motor no genere. (a) Tarjetas de EventsGallery que el Core no enumera: se dejan atenuadas. (b) Lo que solo existe pasando por HOME (GO hacia los
                // juegos de Switch y los regalos de HOME) se marca home_required: el motor lo genera SOLO con el ajuste explicito R-009 y la interfaz lo ofrece solo con ese ajuste. (c) Lo que el banco del
                // Hito 3b demostro imposible (docs/datos/libreria-no-generables.tsv) se atenua con su motivo.
                string? blockReason = null, dimSource = null;
                var homeRequired = false;
                if (addable && enc is not null)
                {
                    if (kind == "Event" && enc.CoreCorroboration == "NotInCoreMgdb") { blockReason = LibraryBlocks.NotInCoreMgdb; dimSource = "catalog"; ruleBlocks++; }
                    else if (kind == "GO" && gen is >= 8) homeRequired = true;
                    else if (kind == "Event" && eventIsHomeGift) homeRequired = true;
                }
                if (addable && blockReason is null && measuredBlocks.TryGetValue(caseKey, out var measuredReason)) { blockReason = measuredReason; dimSource = "measured"; measuredBlocksCount++; homeRequired = false; }
                // p. ej. una cría de Escarlata/Púrpura (sin ancla de encuentro) que solo existe pasando por HOME: lo midió el banco con el ajuste «sin matrícula».
                if (addable && blockReason is null && measured.HomeRequired.Contains(caseKey)) homeRequired = true;
                if (blockReason is not null)
                {
                    addable = false;
                    availability = "Dimmed";
                    dim = blockReason;
                }
                if (homeRequired) homeCount++;

                ins.Run(caseKey, releaseId, game, gname, sp, f, sp, nameEs, namesAll, formNames.GetValueOrDefault((sp, f)), gen, kind, variant, availability, dim,
                    addable ? 1 : 0, a.Storage, status, expected, anchor is not null && encounters.ContainsKey(anchor) ? anchor : null, sourceLabel, evTitle, evTitleEs, evCard, evLang, evRestrict, evDate,
                    homeRequired ? 1 : 0, dimSource, evTitleSource, evShiny, evGender, evGmax ? 1 : 0, evRegion, evRegionSource);
                total++;
                if (addable) addableCount++;
                if (availability == "Dimmed") dimmedCount++;
            }
            CatalogDatabase.Exec(c, "INSERT INTO library_search(library_search) VALUES('rebuild')", tx);
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
        return new Result(total, addableCount, dimmedCount, titleEs, homeCount, ruleBlocks, measuredBlocksCount);
    }

    /// <summary>Un título con los signos propios del castellano (¡ ¿) está en castellano: es la única señal que se usa sin que la tarjeta declare idioma.</summary>
    private static bool HasSpanishMarks(string? title) => title is not null && (title.Contains('¡', StringComparison.Ordinal) || title.Contains('¿', StringComparison.Ordinal));

    private static string SourceLabel(string sourceVersionId, string sourceRef)
    {
        var id = sourceVersionId.Split('@')[0];
        var rev = sourceVersionId.Contains('@', StringComparison.Ordinal) ? sourceVersionId[(sourceVersionId.IndexOf('@', StringComparison.Ordinal) + 1)..] : string.Empty;
        return id switch
        {
            "pkhex-core" => sourceRef.Contains("MGDB", StringComparison.Ordinal) ? "Core PKHeX (eventos MGDB)" : "Core PKHeX (encuentros)",
            "eventsgallery" => $"EventsGallery (Project Pokémon) {rev[..Math.Min(12, rev.Length)]}",
            "pogoenctool" => $"PoGoEncTool (Project Pokémon) {rev[..Math.Min(12, rev.Length)]}",
            _ => id,
        };
    }
}
