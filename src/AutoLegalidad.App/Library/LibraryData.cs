// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Text.Json;
using AutoLegalidad.Catalog.Db;
using AutoLegalidad.Catalog.Library;
using AutoLegalidad.Ui.Operation;
using Microsoft.Data.Sqlite;
using PKHeX.Core;

namespace AutoLegalidad.Ui.Library;

/// <summary>
/// Acceso de la interfaz a la Librería del catálogo (<see cref="LibraryQueries"/>, Hito 1, ADR 0008): conexión de SOLO LECTURA abierta cuando hace falta.
/// La Librería solo ofrece lo que el catálogo marca <c>addable=1</c> (la barrera de la regla 8 vive en la propia base de datos).
/// </summary>
public sealed class LibraryData(string? catalogPath) : IDisposable
{
    private SqliteConnection? _connection;
    private IReadOnlyList<string>? _languages;

    public string? CatalogPath { get; } = catalogPath;
    public bool IsAvailable => !string.IsNullOrEmpty(CatalogPath) && File.Exists(CatalogPath);

    private SqliteConnection Connection => _connection ??= CatalogDatabase.Open(CatalogPath!, readOnly: true);

    public IReadOnlyList<LibraryRow> Query(LibraryFilter filter) => IsAvailable ? LibraryQueries.Query(Connection, filter) : [];
    public long Count(LibraryFilter filter) => IsAvailable ? LibraryQueries.Count(Connection, filter) : 0;
    /// <summary>Hito 4b: candidatos de «Completar el juego» del juego <paramref name="gameId"/> (orígenes ofrecibles por especie-forma, en orden de prioridad).</summary>
    public IReadOnlyList<LivingDexCandidate> LivingDexCandidates(int gameId, bool includeHomeRequired, int gcDestination = 0) => IsAvailable ? LivingDexQueries.LivingDexCandidates(Connection, gameId, includeHomeRequired, gcDestination) : [];

    public string? Detail(string entryKey) => IsAvailable ? LibraryQueries.Detail(Connection, entryKey) : null;

    /// <summary>Hito 5d (ADR 0047): la ficha exportable de una entrada (solo datos del catálogo y versiones; nada del usuario). Null si no hay catálogo.</summary>
    public IDictionary<string, object?>? Sheet(LibraryRow row, SheetEngineVersions? engine) => IsAvailable ? LibrarySheet.Build(Connection, row, engine) : null;

    /// <summary>Idiomas de evento presentes en la Librería (nombres del enum, en inglés), para el filtro.</summary>
    public IReadOnlyList<string> EventLanguages()
    {
        if (_languages is not null || !IsAvailable) return _languages ?? [];
        var list = new List<string>();
        using var cmd = Connection.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT event_language FROM library_entry WHERE event_language IS NOT NULL AND event_language <> '' ORDER BY 1";
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(r.GetString(0));
        return _languages = list;
    }

    /// <summary>Formas distintas de una especie en un juego (para el filtro «Forma»).</summary>
    public IReadOnlyList<(int Form, string Name)> FormsOf(int gameId, int species)
    {
        var rows = Query(new LibraryFilter { GameId = gameId, Species = species, Limit = 2000 });
        return [.. rows.GroupBy(r => r.FormId).OrderBy(g => g.Key).Select(g => (g.Key, g.First().FormName ?? (g.Key == 0 ? AppIdioma.T("forma normal", "normal form") : AppIdioma.F("forma {0}", "form {0}", g.Key))))];
    }

    /// <summary>Busca una fila por su clave de entrada (para soltar desde una lista arrastrada).</summary>
    public LibraryRow? Find(int gameId, string entryKey)
    {
        // entry_key = release|game|species-form|kind|variant: se acota por juego y especie y se compara la clave.
        var parts = entryKey.Split('|');
        if (parts.Length < 5 || !int.TryParse(parts[2].Split('-')[0], out var species)) return null;
        return Query(new LibraryFilter { GameId = gameId, Species = species, Limit = 2000 }).FirstOrDefault(r => r.EntryKey == entryKey);
    }

    /// <summary>Juego de la Librería correspondiente al guardado (el concreto del destino, ver <see cref="RequestBuilder.ResolveVersion"/>), o 0.</summary>
    public static int GameIdOf(SaveFile sav) => sav is FakeSaveFile ? 0 : (int)RequestBuilder.ResolveVersion(sav);

    /// <summary>
    /// Colosseum (<c>GameVersion.COLO</c>) y XD comparten juego en la Librería (CXD) pero no encuentros: el origen de la entrada decide si vale para este guardado. 0 en cualquier otro juego.
    /// </summary>
    public static int GcDestinationOf(SaveFile sav) => sav switch { SAV3Colosseum => (int)GameVersion.COLO, SAV3XD => (int)GameVersion.XD, _ => 0 };

    /// <summary>
    /// R-009: lo que solo existe pasando por Pokémon HOME (<see cref="LibraryRow.HomeRequired"/>: Pokémon GO hacia los juegos de Switch y los regalos de HOME) se ofrece SOLO con el ajuste
    /// explícito «sin matrícula» (desactivado por defecto): el motor lo genera con la matrícula en blanco y nunca se inventa una (regla 6). Sin el ajuste devuelve el motivo del bloqueo; con él, null.
    /// </summary>
    public static string? HomeBlockedReason(LibraryRow row, bool allowHomeTrackerless)
        => row.HomeRequired && !allowHomeTrackerless ? Textos.NecesitaHomeSinAjuste : null;

    public static string TraitList(JsonElement list)
        => list.ValueKind == JsonValueKind.Array ? string.Join(", ", list.EnumerateArray().Select(e => LibraryNames.Trait(e.GetString() ?? string.Empty))) : string.Empty;

    public void Dispose()
    {
        _connection?.Dispose();
        _connection = null;
    }
}
