// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Globalization;
using AutoLegalidad.Catalog.Core;
using AutoLegalidad.Catalog.Db;
using Microsoft.Data.Sqlite;
using PKHeX.Core;

namespace AutoLegalidad.Catalog.Events;

/// <summary>
/// R-014 («Añadir evento»): construye <c>event_offer</c>, qué eventos ofrece cada juego. Para cada juego de destino (no los de solo almacenamiento ni los de solo origen) y cada tarjeta aprobada
/// de Pokémon (<c>kind = 'Event'</c>, más los repartos de Gen 1-2: <c>Encounters1GBEra</c>, <c>Encounters1VC</c>, <c>Encounters2GBEra</c> y <c>CelebiVC</c>) se crea el Pokémon con un entrenador de ese juego:
/// <list type="bullet">
/// <item><b>nativo</b>: el juego puede recibir la tarjeta (<see cref="GiftReceivers"/>: bits de <c>PCD.CardCompatibility</c>, <c>CanBeReceivedByVersion</c>, versión de la plantilla del Core) y el Pokémon creado
/// con ese entrenador es válido para <see cref="LegalityAnalysis"/>.</item>
/// <item><b>transferencia</b>: el destino es de una generación posterior (o de otro contexto de la misma, como Leyendas o Diamante Brillante respecto de Espada y Escudo); el Pokémon se crea en un juego que
/// SÍ recibe la tarjeta (el más antiguo primero), se convierte al formato del destino con <see cref="EntityConverter"/> (Pal Park, Poké Transfer, Banco, Consola Virtual, HOME) y es válido para el Core.
/// Dos juegos de la misma generación y contexto NO se pasan eventos por esta vía: una tarjeta exclusiva de un juego no sale en su pareja.</item>
/// </list>
/// El Core decide siempre (ninguna tabla escrita a mano). Cuando solo es válido con el ajuste R-009 (matrícula de HOME ausente, <c>Severity.Fishy</c>) queda marcado <c>home_required</c>, igual que la Librería.
/// </summary>
public static class EventOfferBuilder
{
    public const string Native = "nativo";
    public const string Transfer = "transferencia";

    /// <summary>Entrenador con el que se crean los Pokémon de la prueba (nunca una partida real).</summary>
    private const string TrainerName = "Prueba";
    private const int MaxOriginAttempts = 4;

    /// <summary>Estado estático del Core (<c>ParseSettings</c>): un solo hilo a la vez, también entre pruebas del mismo proceso.</summary>
    public static readonly object CoreGate = new();

    public sealed record Offer(int Game, string Key, string Via, bool HomeRequired, int OriginGame);

    public sealed class Result
    {
        public List<Offer> Offers { get; } = [];
        public int Cards { get; set; }
        public int CardsWithoutTemplate { get; set; }
        public int Destinations { get; set; }
        public List<string> DestinationsSkipped { get; } = [];
        /// <summary>Tarjetas que el destino puede recibir de forma nativa y que el Core NO da por válidas, por motivo (primer fallo de <see cref="LegalityAnalysis"/>).</summary>
        public SortedDictionary<string, int> NativeInvalidReasons { get; } = new(StringComparer.Ordinal);
        public int NativeInvalid { get; set; }
        /// <summary>El mismo recuento con el juego y el tipo de tarjeta: «motivo | juego | tipo».</summary>
        public SortedDictionary<string, int> NativeInvalidDetail { get; } = new(StringComparer.Ordinal);
        public int TransferAttempts { get; set; }
        public double Seconds { get; set; }
    }

    private sealed record Wanted(string Key, int Species, int Form, string Kind);
    private sealed record Template(IEncounterTemplate Encounter, EntityContext Context, byte Generation);

    private sealed class Dest
    {
        public required GameDef Def { get; init; }
        public required SaveFile Sav { get; init; }
        public int Id => Def.Id;
        public GameVersion Version => Def.Version;
        public EntityContext Context => Def.Context;
        public int Generation => Def.Generation ?? 0;
        public Type PkType => Sav.PKMType;

        public ITrainerInfo Trainer(LanguageID language)
        {
            Sav.Language = (int)language;
            return new SimpleTrainerInfo(Version) { OT = TrainerName, TID16 = 12345, SID16 = 54321, Gender = 0, Language = (int)language };
        }
    }

    /// <summary>Tablas del Core que reparten Pokémon de evento fuera del MGDB: Gen 3 entero (WC3), los repartos de Gen 1-2 y los dos de la Consola Virtual.</summary>
    private static bool IsEventTable(string table)
        => table.StartsWith("EncountersWC3.", StringComparison.Ordinal) || table is "Encounters1GBEra.Gifts" or "Encounters2GBEra.Gifts"
           || CoreEncounterReader.VirtualConsoleSingles.Any(v => table == $"{v.Class}.{v.Field}");

    public static Result Compute(SqliteConnection c, string releaseId, Action<string>? log = null)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = new Result();
        var wanted = LoadWanted(c, releaseId);
        result.Cards = wanted.Count;
        var present = LoadPresent(c, releaseId);
        var dests = LoadDestinations(c, result);
        result.Destinations = dests.Count;
        var templates = LoadTemplates(c, wanted);
        result.CardsWithoutTemplate = wanted.Count(w => !templates.ContainsKey(w.Key));
        log?.Invoke($"Ofertas de eventos: {wanted.Count} tarjetas ({result.CardsWithoutTemplate} sin plantilla del Core), {dests.Count} juegos de destino.");

        lock (CoreGate)
        {
            var severity = ParseSettings.Settings.HOMETransfer.HOMETransferTrackerNotPresent;
            try
            {
                var done = 0;
                var trace = Environment.GetEnvironmentVariable("POKEKEEP_OFERTAS_TRAZA") == "1";
                foreach (var w in wanted.Values.OrderBy(w => w.Key, StringComparer.Ordinal))
                {
                    if (!templates.TryGetValue(w.Key, out var tpls)) continue;
                    if (trace) log?.Invoke($"  > {w.Key}");
                    var card = System.Diagnostics.Stopwatch.StartNew();
                    EvaluateCard(w, tpls, dests, present, result);
                    if (card.Elapsed.TotalSeconds >= 1) log?.Invoke($"  tarjeta lenta: {w.Key} ({card.Elapsed.TotalSeconds:F1} s)");
                    if (++done % 500 == 0) log?.Invoke($"  ... {done}/{wanted.Count} tarjetas, {result.Offers.Count} ofertas");
                }
            }
            finally
            {
                ParseSettings.Settings.HOMETransfer.HOMETransferTrackerNotPresent = severity;
                ParseSettings.ClearActiveTrainer();
            }
        }
        result.Seconds = Math.Round(sw.Elapsed.TotalSeconds, 1);
        return result;
    }

    // ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // Entradas
    // ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static Dictionary<string, Wanted> LoadWanted(SqliteConnection c, string releaseId)
    {
        var res = new Dictionary<string, Wanted>(StringComparer.Ordinal);
        using var cmd = c.CreateCommand();
        // Eventos aprobados (MGDB, Gen 3 del Core y tarjetas de EventsGallery) y los repartos de Gen 1-2. La vista es la unica via de lectura para generar.
        cmd.CommandText = """
            SELECT e.encounter_key, e.species_id, e.form_id, e.kind
              FROM v_encounter_generable e
             WHERE e.release_id = $r
               AND (e.kind = 'Event'
                    OR (e.kind = 'Gift' AND e.context IN ('Gen1', 'Gen2')
                        AND (e.source_ref LIKE '%Encounters1GBEra.Gifts%' OR e.source_ref LIKE '%Encounters2GBEra.Gifts%'
                             OR e.source_ref LIKE '%Encounters1VC.Gift%' OR e.source_ref LIKE '%Encounters2.CelebiVC%')))
            """;
        cmd.Parameters.AddWithValue("$r", releaseId);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            res[r.GetString(0)] = new Wanted(r.GetString(0), r.GetInt32(1), r.GetInt32(2), r.GetString(3));
        return res;
    }

    private static HashSet<(int Game, int Species, int Form)> LoadPresent(SqliteConnection c, string releaseId)
    {
        var res = new HashSet<(int, int, int)>();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT game_id, species_id, form_id FROM availability WHERE release_id = $r AND state = 'Present'";
        cmd.Parameters.AddWithValue("$r", releaseId);
        using var r = cmd.ExecuteReader();
        while (r.Read()) res.Add((r.GetInt32(0), r.GetInt32(1), r.GetInt32(2)));
        return res;
    }

    private static List<Dest> LoadDestinations(SqliteConnection c, Result result)
    {
        var ids = new List<int>();
        using (var cmd = c.CreateCommand())
        {
            // Los mismos destinos que la matriz de cobertura: ni grupos, ni almacenamiento (Battle Revolution), ni solo origen (GO...), ni sin datos de presencia (Champions, Winds, Waves).
            cmd.CommandText = "SELECT game_id FROM game WHERE is_group = 0 AND is_storage = 0 AND is_transfer_origin_only = 0 AND availability_basis <> 'Unknown' ORDER BY game_id";
            using var r = cmd.ExecuteReader();
            while (r.Read()) ids.Add(r.GetInt32(0));
        }
        var res = new List<Dest>();
        foreach (var id in ids)
        {
            var def = GameCatalog.Get((GameVersion)id);
            try
            {
                var sav = BlankSaveFile.Get(def.Version, TrainerName, LanguageID.Spanish);
                sav.OT = TrainerName; sav.TID16 = 12345; sav.SID16 = 54321; sav.Gender = 0; sav.Language = (int)LanguageID.Spanish;
                res.Add(new Dest { Def = def, Sav = sav });
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                result.DestinationsSkipped.Add($"{def.Version}: {ex.Message}");
            }
        }
        return res;
    }

    private static Dictionary<string, List<Template>> LoadTemplates(SqliteConnection c, Dictionary<string, Wanted> wanted)
    {
        var res = new Dictionary<string, List<Template>>(StringComparer.Ordinal);
        void Add(string key, Template t)
        {
            if (!res.TryGetValue(key, out var l)) res[key] = l = [];
            if (!l.Any(x => ReferenceEquals(x.Encounter, t.Encounter))) l.Add(t);
        }

        // Tarjetas del MGDB del Core (las repetidas son una sola fila del catálogo y guardan todas sus plantillas).
        foreach (var gift in CoreMgdbIndex.AllGifts())
        {
            if (!gift.IsEntity || gift.Species == 0) continue;
            var key = CoreMgdbIndex.KeyFor(gift.GetType().Name, CoreMgdbIndex.StableSha256(gift));
            if (wanted.ContainsKey(key))   // tarjetas con el mismo contenido y otra restricción de juego comparten clave: la oferta vale si alguna sirve
                Add(key, new Template(gift, gift.Context, gift.Generation));
        }
        // Gen 3 (WC3) y repartos de Gen 1-2: varias plantillas comparten clave (tipo, version, contexto, especie, forma, huevo): la oferta vale si alguna sirve.
        foreach (var (item, tpl) in CoreEncounterReader.ReadTemplates(IsEventTable))
        {
            var key = $"core:{item.TypeName}:{item.Version}:{item.Context}:{item.Species:D4}-{item.Form}:{(item.IsEgg ? "egg" : "std")}";
            if (wanted.ContainsKey(key)) Add(key, new Template(tpl, item.Context, item.Generation));
        }
        // Tarjetas que solo trae EventsGallery (el Core no las conoce): se leen del fichero guardado en la tabla artifact, con el mismo analizador oficial de la importación.
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = """
                SELECT e.encounter_key, json_extract(e.descriptor_json, '$.relative_path'), a.payload
                  FROM encounter e JOIN artifact a ON a.artifact_sha256 = json_extract(e.descriptor_json, '$.file_sha256')
                 WHERE e.encounter_key LIKE 'eventsgallery:%'
                """;
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var key = r.GetString(0);
                if (!wanted.ContainsKey(key) || res.ContainsKey(key) || r.IsDBNull(1) || r.IsDBNull(2)) continue;
                var ext = Path.GetExtension(r.GetString(1)).ToLowerInvariant();
                var data = (byte[])r.GetValue(2);
                try
                {
                    if (!MysteryGift.IsMysteryGift(data.Length) || MysteryGift.GetMysteryGift((byte[])data.Clone(), ext) is not { IsEntity: true } g || g.Species == 0) continue;
                    Add(key, new Template(g, g.Context, g.Generation));
                }
                catch (Exception ex) when (ex is not OutOfMemoryException) { /* ilegible: se queda sin plantilla y se cuenta como tal */ }
            }
        }
        return res;
    }

    // ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // Evaluacion de una tarjeta contra todos los destinos
    // ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static bool IsNativeTarget(Template t, Dest d)
    {
        if (!t.Context.IsValid || t.Context != d.Context) return false;
        return t.Encounter is DataMysteryGift g ? GiftReceivers.CanReceive(g, d.Version) : t.Encounter.Version.Contains(d.Version);
    }

    private static void EvaluateCard(Wanted w, List<Template> tpls, List<Dest> dests, HashSet<(int, int, int)> present, Result result)
    {
        var cardContext = tpls[0].Context;
        var cardGeneration = tpls[0].Generation;
        // Juegos que reciben alguna plantilla de la tarjeta (orden de juego: el mas antiguo primero): son los origenes de la transferencia.
        var origins = new List<(Template T, Dest O)>();
        foreach (var o in dests)
            foreach (var t in tpls)
                if (IsNativeTarget(t, o)) origins.Add((t, o));

        var nativeInvalid = new HashSet<int>();
        foreach (var d in dests)
        {
            if (!present.Contains((d.Id, w.Species, w.Form))) continue;   // el destino no tiene ni la especie ni la forma: el Core lo diria igual, y ahorra la prueba

            var nativeHere = origins.Where(x => x.O.Id == d.Id).ToList();
            if (nativeHere.Count != 0)
            {
                string? firstReason = null;
                foreach (var (t, o) in nativeHere)
                {
                    if (TryCreate(t, o, o, out _, out var home, out var reason)) { result.Offers.Add(new Offer(d.Id, w.Key, Native, home, d.Id)); firstReason = null; goto nextDest; }
                    firstReason ??= reason;
                }
                result.NativeInvalid++;
                var why = firstReason ?? "sin motivo";
                result.NativeInvalidReasons[why] = result.NativeInvalidReasons.GetValueOrDefault(why) + 1;
                var detail = $"{why} | {d.Def.Version} | {nativeHere[0].T.Encounter.GetType().Name}";
                result.NativeInvalidDetail[detail] = result.NativeInvalidDetail.GetValueOrDefault(detail) + 1;
                nativeInvalid.Add(d.Id);
                continue;   // el destino recibe la tarjeta pero el Core no la da por valida: no se le pasa por transferencia
            }

            // Transferencia: solo desde una generacion anterior, o desde otro contexto de la misma generacion (HOME). Nunca entre juegos gemelos.
            if (!(d.Generation > cardGeneration || (d.Generation == cardGeneration && d.Context != cardContext))) continue;
            var attempts = 0;
            foreach (var (t, o) in origins.OrderBy(x => x.O.Id))
            {
                if (attempts++ >= MaxOriginAttempts) break;
                result.TransferAttempts++;
                if (TryCreate(t, o, d, out _, out var home, out _)) { result.Offers.Add(new Offer(d.Id, w.Key, Transfer, home, o.Id)); break; }
            }
            nextDest:;
        }
    }

    /// <summary>
    /// Crea el Pokémon de la plantilla con un entrenador del juego <paramref name="origin"/>, lo lleva al formato de <paramref name="dest"/> si es otro y lo pasa por <see cref="LegalityAnalysis"/> con el
    /// guardado en blanco del destino. Primero con el idioma por defecto y, si la tarjeta lleva el suyo (p. ej. las coreanas de Gen 4), otra vez con ese.
    /// </summary>
    private static bool TryCreate(Template t, Dest origin, Dest dest, out PKM? created, out bool homeRequired, out string? reason)
    {
        created = null;
        homeRequired = false;
        reason = null;
        if (t.Encounter is not IEncounterConvertible conv) { reason = "la plantilla no es convertible"; return false; }
        LanguageID? retry = null;
        for (var pass = 0; pass < 2; pass++)
        {
            var language = pass == 0 ? LanguageID.Spanish : retry!.Value;
            PKM? pk;
            try { pk = conv.ConvertToPKM(origin.Trainer(language), EncounterCriteria.Unrestricted); }
            catch (Exception ex) when (ex is not OutOfMemoryException) { reason = "no se pudo crear: " + ex.GetType().Name; return false; }
            if (pk is null) { reason = "no se pudo crear"; return false; }
            Normalize(pk, dest);
            if (pk.GetType() != dest.PkType)
            {
                // Sin ruta real (p. ej. Gen 1-2 hacia Gen 3-6, o hacia atrás hasta Gen 7) NO se llama al convertidor: con PK1 -> PK3 el bucle de EntityConverter.ConvertPKM del Core no termina nunca.
                if (!EntityConverter.IsConvertibleToFormat(pk, (byte)dest.Generation)) { reason = "sin ruta de transferencia"; return false; }
                try { pk = EntityConverter.ConvertToType(pk, dest.PkType, out _); }
                catch (Exception ex) when (ex is not OutOfMemoryException) { reason = "no se pudo convertir: " + ex.GetType().Name; return false; }
                if (pk is null) { reason = "sin ruta de transferencia"; return false; }
            }
            Normalize(pk, dest);
            // El guardado del destino se queda en español: el Core mira su idioma en las reglas de la GTS de Gen 4 y una tarjeta coreana con la Cinta Clásica solo vale en un guardado coreano,
            // que el catálogo no puede suponer (el motor tampoco la genera en un Platino no coreano).
            dest.Trainer(LanguageID.Spanish);
            if (Analyze(dest, pk, out homeRequired, out reason))
            {
                created = pk;
                return true;
            }
            // Un Pokémon que cambió de formato o de contexto lleva siempre al jugador como manejador actual (lo fija el juego al recibirlo; el conversor del Core pone «PKHeX»). Como el motor al entregar:
            // se registra el manejador con el entrenador del guardado de destino y se vuelve a mirar.
            if (reason is { } why && why.StartsWith("Memory:Transfer", StringComparison.Ordinal) && pk.Format >= 6 && !pk.IsEgg)
            {
                foreach (var traded in MarkTraded(pk, dest))
                    if (Analyze(dest, traded, out homeRequired, out reason)) { created = traded; return true; }
            }
            var own = (LanguageID)pk.Language;
            if (pass == 0 && own != language && (int)own > 0 && Enum.IsDefined(own)) retry = own; else break;
        }
        return false;
    }

    /// <summary>
    /// Variantes del Pokémon «recibido por el jugador»: con la rutina pública del Core (<see cref="IHandlerUpdate"/>, que además fija amistad y afecto del manejador) y, para los formatos que no la tienen (Gen 6),
    /// con el manejador escrito a mano. El motor hace lo mismo al entregar (<c>MarkTraded</c>).
    /// </summary>
    private static IEnumerable<PKM> MarkTraded(PKM pk, Dest dest)
    {
        if (pk is IHandlerUpdate)
        {
            var c = pk.Clone();
            var partner = new SimpleTrainerInfo(pk.Version) { OT = "Amigo", TID16 = 40404, SID16 = 4040, Gender = (byte)(dest.Sav.Gender ^ 1), Language = pk.Language };
            ((IHandlerUpdate)c).UpdateHandler(partner);
            ((IHandlerUpdate)c).UpdateHandler(dest.Sav);
            c.RefreshChecksum();
            yield return c;
        }
        var m = pk.Clone();
        m.HandlingTrainerName = dest.Sav.OT;
        m.HandlingTrainerGender = dest.Sav.Gender;
        if (m is IHandlerLanguage hl) hl.HandlingTrainerLanguage = (byte)dest.Sav.Language;
        m.CurrentHandler = 1;
        m.RefreshChecksum();
        yield return m;
    }

    /// <summary>Fecha de recepción de las tarjetas que no traen una válida (el Core la deja vacía y el motor la elige con <c>DatePolicy</c>): cualquier día real del calendario, anterior a hoy.</summary>
    private static readonly DateOnly PlausibleDate = new(2020, 6, 1);

    /// <summary>
    /// Lo mismo que hace el motor al entregar un Pokémon (<c>ImportNormalizer</c> y <c>DatePolicy</c>), para no dar por inválido lo que el motor arregla siempre: fecha de captura o de huevo ausente en las tarjetas sin fecha
    /// propia, y en Let's Go la amistad con el manejador del guardado, el valor calculado y Espíritu y Humor en reposo (casilla de caja).
    /// </summary>
    private static void Normalize(PKM pk, Dest d)
    {
        if (pk.Format > 3)
        {
            var none = pk is PB8 ? Locations.Default8bNone : 0;
            if (!pk.IsEgg && pk.MetLocation != none && pk.MetDate is null) pk.MetDate = PlausibleDate;
            if (pk.IsEgg && pk.MetLocation == none && pk.MetDate is not null) pk.MetDate = null;   // un huevo sin eclosionar no tiene fecha de captura (el Core de las tarjetas de huevo la deja con la de la tarjeta)
            if (pk.EggLocation != none && pk.EggMetDate is null) pk.EggMetDate = PlausibleDate;
        }
        if (pk is PB7 pb && !pb.IsEgg && d.Sav is SAV7b)
        {
            pb.UpdateHandler(d.Sav);
            if (pb.Species is (ushort)Species.Meltan or (ushort)Species.Melmetal) pb.ResetCP(); else pb.ResetCalculatedValues();
            if (!pb.IsStarter) pb.ResetSpiritMood();
            pb.RefreshChecksum();
        }
    }

    private static bool Analyze(Dest d, PKM pk, out bool homeRequired, out string? reason)
    {
        homeRequired = false;
        reason = null;
        ParseSettings.InitFromSaveFileData(d.Sav);
        ParseSettings.Settings.HOMETransfer.HOMETransferTrackerNotPresent = Severity.Invalid;
        var la = new LegalityAnalysis(pk, d.Sav.Personal, StorageSlotType.Box);
        if (la.Valid) return true;
        var first = la.Results.FirstOrDefault(r => !r.Valid);
        reason = first.Equals(default(CheckResult)) ? "invalido" : $"{first.Identifier}:{first.Result}";
        if (d.Generation >= 8)
        {
            // R-009: solo se perdona la matricula de HOME ausente; con el ajuste se repite el analisis y, si pasa, la entrada es «solo con HOME».
            ParseSettings.Settings.HOMETransfer.HOMETransferTrackerNotPresent = Severity.Fishy;
            try
            {
                if (new LegalityAnalysis(pk, d.Sav.Personal, StorageSlotType.Box).Valid) { homeRequired = true; return true; }
            }
            finally { ParseSettings.Settings.HOMETransfer.HOMETransferTrackerNotPresent = Severity.Invalid; }
        }
        return false;
    }

    // ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // Persistencia
    // ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>Escribe las ofertas y les busca la fila de la Libreria que las representa (<c>entry_key</c>). Va despues de construir <c>library_entry</c>.</summary>
    public static void Persist(SqliteConnection c, IReadOnlyList<Offer> offers)
    {
        // Filas de la Libreria con ancla: por clave y juego, por clave (el juego mas antiguo) y, para los repartos de Gen 1-2 (una fila por juego, especie y forma), por especie.
        var byKeyGame = new Dictionary<(string, int), string>();
        var byKey = new Dictionary<string, string>(StringComparer.Ordinal);
        var giftBySpecies = new Dictionary<(int Game, int Species, int Form), string>();
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = """
                SELECT entry_key, game_id, species_id, form_id, kind, variant_key, encounter_key, addable
                  FROM library_entry
                 WHERE (kind = 'Event' AND encounter_key IS NOT NULL) OR (kind = 'Gift' AND variant_key = 'direct' AND generation IN (1, 2))
                 ORDER BY addable DESC, game_id, entry_key
                """;
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var entry = r.GetString(0);
                var game = r.GetInt32(1);
                if (!r.IsDBNull(6))
                {
                    var key = r.GetString(6);
                    byKeyGame.TryAdd((key, game), entry);
                    byKey.TryAdd(key, entry);
                }
                if (r.GetString(4) == "Gift") giftBySpecies.TryAdd((game, r.GetInt32(2), r.GetInt32(3)), entry);
            }
        }
        var species = new Dictionary<string, (int Species, int Form)>(StringComparer.Ordinal);
        var gamesOfKey = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "SELECT encounter_key, species_id, form_id FROM encounter";
            using var r = cmd.ExecuteReader();
            while (r.Read()) species[r.GetString(0)] = (r.GetInt32(1), r.GetInt32(2));
        }
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "SELECT encounter_key, game_id FROM encounter_game ORDER BY encounter_key, game_id";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var k = r.GetString(0);
                if (!gamesOfKey.TryGetValue(k, out var l)) gamesOfKey[k] = l = [];
                l.Add(r.GetInt32(1));
            }
        }

        string? Representative(Offer o)
        {
            if (byKeyGame.TryGetValue((o.Key, o.Game), out var a)) return a;
            if (byKeyGame.TryGetValue((o.Key, o.OriginGame), out a)) return a;
            if (byKey.TryGetValue(o.Key, out a)) return a;
            if (species.TryGetValue(o.Key, out var sf))
            {
                if (giftBySpecies.TryGetValue((o.Game, sf.Species, sf.Form), out a)) return a;
                if (giftBySpecies.TryGetValue((o.OriginGame, sf.Species, sf.Form), out a)) return a;
                foreach (var g in gamesOfKey.GetValueOrDefault(o.Key) ?? [])
                    if (giftBySpecies.TryGetValue((g, sf.Species, sf.Form), out a)) return a;
            }
            return null;
        }

        using var tx = c.BeginTransaction();
        try
        {
            using (var ins = new PreparedInsert(c, tx, "event_offer", "game_id", "encounter_key", "via", "home_required", "origin_game", "entry_key"))
                foreach (var o in offers.OrderBy(o => o.Game).ThenBy(o => o.Key, StringComparer.Ordinal))
                    ins.Run(o.Game, o.Key, o.Via, o.HomeRequired ? 1 : 0, o.OriginGame, Representative(o));
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    /// <summary>Cifras por juego y vía de una base ya construida: <c>(version, nativo, transferencia, solo con HOME)</c>.</summary>
    public static IReadOnlyList<(int GameId, string Version, int Native, int Transfer, int HomeRequired)> CountsByGame(SqliteConnection c)
    {
        var res = new List<(int, string, int, int, int)>();
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
            SELECT g.game_id, g.version_name,
                   COALESCE(SUM(o.via = 'nativo'), 0), COALESCE(SUM(o.via = 'transferencia'), 0), COALESCE(SUM(o.home_required), 0)
              FROM game g LEFT JOIN event_offer o ON o.game_id = g.game_id
             WHERE g.is_group = 0
             GROUP BY g.game_id ORDER BY g.game_id
            """;
        using var r = cmd.ExecuteReader();
        while (r.Read()) res.Add((r.GetInt32(0), r.GetString(1), r.GetInt32(2), r.GetInt32(3), r.GetInt32(4)));
        return res;
    }

    public static string Describe(Result r)
    {
        var text = string.Create(CultureInfo.InvariantCulture,
            $"{r.Offers.Count} ofertas ({r.Offers.Count(o => o.Via == Native)} nativas, {r.Offers.Count(o => o.Via == Transfer)} por transferencia, {r.Offers.Count(o => o.HomeRequired)} solo con HOME) de {r.Cards} tarjetas en {r.Destinations} juegos; {r.NativeInvalid} tarjetas que el juego recibe y el Core no da por válidas; {r.TransferAttempts} intentos de transferencia; {r.Seconds} s.");
        return r.DestinationsSkipped.Count == 0 ? text : text + " Destinos omitidos: " + string.Join("; ", r.DestinationsSkipped) + ".";
    }
}
