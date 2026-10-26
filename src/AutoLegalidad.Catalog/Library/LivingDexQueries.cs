// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace AutoLegalidad.Catalog.Library;

/// <summary>
/// Prioridad de un origen para «Completar el juego» (diseño del Hito 4b §1.2). Menor = antes. Un origen de <see cref="HomeRequired"/> solo se ofrece con el ajuste R-009.
/// </summary>
public enum LivingDexClass
{
    /// <summary>Nativo del juego: salvaje, estático, regalo, cría, intercambio con entrenador del juego, incursión.</summary>
    Native = 1,
    /// <summary>Tarjeta de evento (con tarjeta en español primero).</summary>
    Event = 2,
    /// <summary>Llegada por transferencia desde un juego anterior (la Librería todavía no la ofrece: se prueba sin origen fijado, con la plantilla de la especie).</summary>
    Transfer = 3,
    /// <summary>Pokémon GO sin HOME (Let's Go).</summary>
    Go = 4,
    /// <summary>Exige la matrícula de HOME: solo con el ajuste explícito, resultado <c>SuccessNeedsHome</c>, nunca «legal».</summary>
    HomeRequired = 5,
}

/// <summary>Un origen ofrecible de una especie-forma. <see cref="EncounterKey"/> es null en las crías (el origen lo da el Core) y en la transferencia (sin origen fijado).</summary>
public sealed record LivingDexOrigin(string EntryKey, string Kind, string Variant, string? EncounterKey, LivingDexClass Class, bool HomeRequired, bool SpanishCard, int MinLevel, int IvRank)
{
    /// <summary>Hito 5d (ADR 0046): datos de la tarjeta cuando el origen es un evento (<c>Kind == "Event"</c>); null en el resto. Sirve para «preferir la versión de evento».</summary>
    public EventInfo? Event { get; init; }

    /// <summary>La solicitud no fija un origen concreto (transferencia): el motor elige entre todas las rutas del Core.</summary>
    public bool Unpinned => Class == LivingDexClass.Transfer;

    /// <summary>0.6.5 (ADR 0052): el encuentro fija el entrenador original (intercambio con un personaje, regalo con entrenador, tarjeta de evento con entrenador propio): el EO no puede ser el jugador.</summary>
    public bool OwnerFixed { get; init; }

    /// <summary>Rango de origen propio (ver <see cref="OwnerRanks"/>): menor = el EO es el jugador y nació en este juego.</summary>
    public int OwnerRank => OwnerRanks.Of(Class, OwnerFixed);
}

/// <summary>
/// 0.6.5 (ADR 0052), «origen propio primero»: orden de preferencia de los orígenes de una especie-forma según quién figure como entrenador original (EO).
/// 0 = capturado/criado/recibido en este juego con el EO del jugador; 1 = el EO es el jugador pero nació en otro juego (transferencia, GO); 2 = tarjeta de evento sin entrenador propio (EO = jugador);
/// 3 = intercambio o regalo del juego con EO fijo; 4 = tarjeta de evento con entrenador propio; 5 = exige HOME.
/// </summary>
public static class OwnerRanks
{
    public static int Of(LivingDexClass cls, bool ownerFixed) => cls switch
    {
        LivingDexClass.HomeRequired => 5,
        LivingDexClass.Event => ownerFixed ? 4 : 2,
        LivingDexClass.Native => ownerFixed ? 3 : 0,
        _ => 1,
    };

    /// <summary>Rango de origen propio de una entrada de la Librería por su clase de origen (<c>library_entry.kind</c>) y si el encuentro fija el EO.</summary>
    public static int OfKind(string kind, bool ownerFixed) => kind switch
    {
        "Event" => ownerFixed ? 4 : 2,
        "Transfer" or "GO" => 1,
        _ => ownerFixed ? 3 : 0,
    };

    /// <summary>Tipos de encuentro del Core que implementan <c>IFixedTrainer</c> (el EO lo fija el encuentro). <c>EncounterGift3</c>, <c>EncounterGift9a</c> y <c>EncounterGift2</c> lo deciden por instancia y no entran aquí.</summary>
    private static readonly Lazy<HashSet<string>> FixedTypes = new(() => [.. typeof(PKHeX.Core.PKM).Assembly.GetTypes()
        .Where(t => !t.IsAbstract && t.Name.StartsWith("Encounter", StringComparison.Ordinal) && typeof(PKHeX.Core.IFixedTrainer).IsAssignableFrom(t))
        .Select(t => t.Name).Where(n => n is not ("EncounterGift3" or "EncounterGift9a"))]);

    /// <summary>
    /// Claves de catálogo de los regalos cuyo entrenador fija el encuentro y se decide por INSTANCIA, no por tipo: los regalos de GB (<c>EncounterGift2</c>: Stadium y Pokémon Center de Nueva York frente al
    /// receptor) y los de Z-A con personaje. Se leen de las tablas del Core (pocas decenas de entradas), con el mismo formato de clave que el catálogo.
    /// </summary>
    private static readonly Lazy<HashSet<string>> FixedGiftKeys = new(() =>
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        void Add(PKHeX.Core.IEncounterTemplate t, PKHeX.Core.EntityContext ctx)
            => keys.Add($"core:{t.GetType().Name}:{t.Version}:{ctx}:{t.Species:D4}-{t.Form}:{(t.IsEgg ? "egg" : "std")}");
        const System.Reflection.BindingFlags any = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
        foreach (var t in typeof(PKHeX.Core.PKM).Assembly.GetTypes().Where(t => t.Name.StartsWith("Encounters", StringComparison.Ordinal)))
            foreach (var field in t.GetFields(any))
            {
                switch (field.GetValue(null))
                {
                    case PKHeX.Core.EncounterGift1[] g1:
                        foreach (var g in g1) if (g.Trainer != PKHeX.Core.EncounterGift1.TrainerType.Recipient) Add(g, PKHeX.Core.EntityContext.Gen1);
                        break;
                    case PKHeX.Core.EncounterGift2[] g2:
                        foreach (var g in g2) if (g.Trainer != PKHeX.Core.EncounterGift2.TrainerType.Recipient) Add(g, PKHeX.Core.EntityContext.Gen2);
                        break;
                    case PKHeX.Core.EncounterGift9a[] g9:
                        foreach (var g in g9) if (g.IsFixedTrainer) Add(g, PKHeX.Core.EntityContext.Gen9a);
                        break;
                }
            }
        return keys;
    });

    /// <summary>¿Fija el encuentro el EO? <paramref name="type"/> es el tipo de plantilla del catálogo; <paramref name="fixedTraitsJson"/>, los rasgos fijos de una tarjeta de evento.</summary>
    public static bool IsOwnerFixed(string kind, string? type, string? fixedTraitsJson, string? encounterKey = null)
    {
        if (kind == "Event")
            return type is not null && type.StartsWith("EncounterGift3", StringComparison.Ordinal) || (fixedTraitsJson?.Contains("\"trainer-name\"", StringComparison.Ordinal) ?? false);
        if (type is "EncounterGift1" or "EncounterGift2" or "EncounterGift9a") return encounterKey is not null && FixedGiftKeys.Value.Contains(encounterKey);
        return type is not null && FixedTypes.Value.Contains(type);
    }
}

/// <summary>Una especie-forma que existe en el juego (según el catálogo, que sale del Core: R-004) con sus orígenes ofrecibles ordenados por prioridad.</summary>
public sealed record LivingDexCandidate(int Species, int Form, int DexNumber, string NameEs, string? FormName, string FormStorage, IReadOnlyList<LivingDexOrigin> Origins, string? UnavailableReason)
{
    /// <summary>Hay al menos un origen que se puede intentar (sin contar los que exigen HOME si no se permiten).</summary>
    public bool Generable => Origins.Count > 0;
    public bool StorableInBox => FormStorage == "Storable";
    /// <summary>Todos los orígenes exigen la matrícula de HOME (con el ajuste desactivado no se puede generar).</summary>
    public bool OnlyWithHome { get; init; }
    /// <summary>El Core demostró que esta forma no existe en este juego por ninguna ruta (<see cref="FormReach"/>): no se planifica. El motivo lleva la prueba.</summary>
    public bool ProvenImpossible { get; init; }
    /// <summary>Forma regional (Alola, Galar, Hisui, Paldea): para la opción «Una por especie + formas regionales».</summary>
    public bool Regional => FormName is { Length: > 0 } f && (f.StartsWith("Alola", StringComparison.Ordinal) || f.StartsWith("Galar", StringComparison.Ordinal)
        || f.StartsWith("Hisui", StringComparison.Ordinal) || f.StartsWith("Paldea", StringComparison.Ordinal));
}

/// <summary>
/// Consulta de «Completar el juego» (Hito 4b, T7): por especie-forma, los orígenes ofrecibles del juego destino ordenados por la prioridad del diseño (§1.2). Solo lee la Librería
/// (<c>library_entry</c>, que ya sale de las vistas generables: regla 8), nunca <c>encounter</c> como fuente de qué se ofrece (solo para el nivel mínimo y los rasgos fijos del origen).
/// </summary>
public static class LivingDexQueries
{
    /// <summary>
    /// Candidatos del juego <paramref name="gameId"/>: todas las especie-forma con alguna entrada disponible en el catálogo. Los orígenes se ordenan así (el primero es el que se prueba primero):
    /// clase (<see cref="LivingDexClass"/>), directo antes que por evolución, IV libres antes que IV fijados, tarjeta en español, nivel mínimo y, por último, <c>entry_key</c> (determinista, sin azar).
    /// <paramref name="gcDestination"/> (<see cref="GameCubeIds.Colosseum"/>, <see cref="GameCubeIds.Xd"/> o 0 en otro juego) descarta los orígenes del otro juego del GameCube.
    /// Con <paramref name="includeHomeRequired"/> falso los orígenes que exigen HOME no se devuelven (la especie-forma queda con <see cref="LivingDexCandidate.OnlyWithHome"/> si no tiene otro).
    /// </summary>
    public static IReadOnlyList<LivingDexCandidate> LivingDexCandidates(SqliteConnection c, int gameId, bool includeHomeRequired, int gcDestination = 0)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
            SELECT l.species_id, l.form_id, l.dex_number, l.name_es, l.form_name, l.form_storage, l.entry_key, l.kind, l.variant_key, l.encounter_key, l.home_required, l.addable,
                   l.dim_reason, l.event_language, l.event_restrict_language,
                   json_extract(e.descriptor_json, '$.levelMin'), json_extract(e.descriptor_json, '$.fixedTraits'), COALESCE(e.origin_game, 0),
                   l.event_title, l.event_title_es, l.event_card_id, COALESCE(l.event_start_date, w.start_day), l.shiny_rule, json_extract(e.descriptor_json, '$.type'), l.event_region
              FROM library_entry l
              LEFT JOIN encounter e ON e.encounter_key = l.encounter_key
              LEFT JOIN event_window w ON w.encounter_key = l.encounter_key
             WHERE l.game_id = $game AND l.availability = 'Available'
             ORDER BY l.species_id, l.form_id, l.entry_key
            """;
        cmd.Parameters.AddWithValue("$game", gameId);
        var result = new List<LivingDexCandidate>();
        using var r = cmd.ExecuteReader();

        (int Species, int Form)? current = null;
        string name = string.Empty, storage = string.Empty;
        string? formName = null, firstReason = null;
        int dex = 0;
        var origins = new List<LivingDexOrigin>();
        var homeOnly = false;

        void Flush()
        {
            if (current is not { } cur) return;
            var ordered = origins.OrderBy(o => o.OwnerRank).ThenBy(o => (int)o.Class).ThenBy(o => o.Variant == "direct" ? 0 : 1).ThenBy(o => o.IvRank).ThenBy(o => o.SpanishCard ? 0 : 1)
                .ThenBy(o => o.MinLevel).ThenBy(o => o.EntryKey, StringComparer.Ordinal).ToList();
            var proven = FormReach.Lookup(gameId, cur.Species, cur.Form);
            var impossible = false;
            if (proven is not null)
            {
                // Hito 4b: el Core demostró (FormReachability.Prove) que esta especie-forma no existe, o que TODO lo que enumera exige la matrícula de HOME, aunque la Librería la ofrezca por transferencia
                // sin ancla o por una vía (GO, regalo) que el motor no consigue dar sin HOME. «Imposible» solo se aplica si lo único que se ofrece es la transferencia.
                if (proven.Kind == FormReachKind.Impossible && ordered.All(o => o.Class == LivingDexClass.Transfer)) { ordered = []; impossible = true; firstReason = proven.Evidence; }
                else if (proven.Kind == FormReachKind.HomeOnly)
                {
                    if (!includeHomeRequired && ordered.Count > 0) { ordered = []; homeOnly = true; }
                    else ordered = [.. ordered.Select(o => o with { Class = LivingDexClass.HomeRequired, HomeRequired = true })];
                }
            }
            var generable = ordered.Count > 0;
            result.Add(new LivingDexCandidate(cur.Species, cur.Form, dex, name, formName, storage, ordered, generable ? null : firstReason ?? "La Librería todavía no ofrece ningún origen para esta especie en este juego.")
            { OnlyWithHome = !generable && homeOnly && !impossible, ProvenImpossible = impossible });
        }

        while (r.Read())
        {
            var key = (r.GetInt32(0), r.GetInt32(1));
            if (current != key)
            {
                Flush();
                current = key;
                dex = r.GetInt32(2);
                name = r.GetString(3);
                formName = r.IsDBNull(4) ? null : r.GetString(4);
                storage = r.GetString(5);
                origins = [];
                homeOnly = false;
                firstReason = null;
            }
            var kind = r.GetString(7);
            var variant = r.GetString(8);
            var home = r.GetInt32(10) == 1;
            var addable = r.GetInt32(11) == 1;
            // Colosseum y XD comparten juego en la Librería, no encuentros: un origen del otro juego del GameCube no se puede fijar en este destino.
            var originGame = r.GetInt32(17);
            if (GameCubeIds.IsGameCube(gcDestination) && GameCubeIds.IsGameCube(originGame) && originGame != gcDestination)
            {
                firstReason ??= gcDestination == GameCubeIds.Colosseum ? LibraryQueries.OnlyXdReason : LibraryQueries.OnlyColosseumReason;
                continue;
            }
            if (!addable && kind != "Transfer")
            {
                firstReason ??= r.IsDBNull(12) ? null : r.GetString(12);
                if (home) homeOnly = true;
                continue;
            }
            if (home && !includeHomeRequired) { homeOnly = true; continue; }
            var cls = home ? LivingDexClass.HomeRequired
                : kind switch
                {
                    "Event" => LivingDexClass.Event,
                    "Transfer" => LivingDexClass.Transfer,
                    "GO" => LivingDexClass.Go,
                    _ => LivingDexClass.Native,
                };
            var spanish = !r.IsDBNull(13) && r.GetString(13) == "Spanish";
            var minLevel = r.IsDBNull(15) ? 100 : (int)r.GetInt64(15);
            string? Text(int i) => r.IsDBNull(i) ? null : r.GetString(i) is { Length: > 0 } t ? t : null;
            var info = kind == "Event"
                ? new EventInfo(Text(18), Text(19), Text(23), Text(20) is { } card && int.TryParse(card, out var cardId) ? cardId : 0, Text(13), Text(14), Text(21), Text(22), Text(24))
                : null;
            origins.Add(new LivingDexOrigin(r.GetString(6), kind, variant, r.IsDBNull(9) ? null : r.GetString(9), cls, home, spanish, minLevel, IvRank(kind, r.IsDBNull(16) ? null : r.GetString(16)))
            { Event = info, OwnerFixed = OwnerRanks.IsOwnerFixed(kind, Text(23), r.IsDBNull(16) ? null : r.GetString(16), r.IsDBNull(9) ? null : r.GetString(9)) });
        }
        Flush();
        return result;
    }

    /// <summary>
    /// Estimación DOCUMENTADA de cuánta libertad de IV deja el origen (el catálogo solo registra los rasgos fijos de los eventos): 0 = IV libres (salvaje, cría), 1 = el encuentro puede
    /// fijar IV (estático, regalo, intercambio, incursión, GO, evento sin IV fijos), 2 = evento con IV fijos. El motor optimiza los IV reales; esto solo ordena los intentos.
    /// </summary>
    private static int IvRank(string kind, string? fixedTraitsJson)
    {
        if (kind == "Event" && fixedTraitsJson is { Length: > 0 })
        {
            try
            {
                using var doc = JsonDocument.Parse(fixedTraitsJson);
                if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.EnumerateArray().Any(e => e.ValueKind == JsonValueKind.String && e.GetString() == "ivs"))
                    return 2;
            }
            catch (JsonException) { /* sin rasgos legibles: se trata como «puede fijar» */ }
        }
        return kind is "Wild" or "Egg" ? 0 : 1;
    }
}
