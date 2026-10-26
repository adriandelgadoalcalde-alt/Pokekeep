// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AutoLegalidad.Catalog.Library;

/// <summary>Qué demostró el Core de una especie-forma que la Librería ofrece solo como «transferencia sin ancla nativa».</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FormReachKind
{
    /// <summary>Ningún origen legal: el Core no enumera ningún encuentro válido para esa forma en ese juego (o el formato de guardado no la admite).</summary>
    Impossible,
    /// <summary>Todos los orígenes que el Core enumera exigen la matrícula de HOME: solo se puede tener con el ajuste R-009 (resultado «Necesita pasar por HOME»).</summary>
    HomeOnly,
}

/// <summary>Una especie-forma de un juego con su veredicto y la prueba (texto del veredicto de <c>FormReachability.Prove</c>).</summary>
public sealed record FormReachEntry(int Game, string Version, int Species, int Form, FormReachKind Kind, string Evidence);

/// <summary>
/// Veredictos demostrados con el Core (Hito 4b, regla 11): especie-formas que la Librería lista como «Available» solo por la vía de transferencia, pero que el motor no puede dar con esa forma.
/// El fichero <c>FormReach/formas-sin-ruta.json</c> lo genera <c>pokekeep-bench-completar clasificar</c> (que ejecuta <c>FormReachability.Prove</c> sobre cada caso) y una prueba de integración
/// vuelve a demostrar cada entrada con el Core en cada ejecución: si el Core cambia, la entrada deja de pasar y hay que regenerarla.
/// </summary>
public static class FormReach
{
    private static readonly Lazy<IReadOnlyDictionary<(int Game, int Species, int Form), FormReachEntry>> Table = new(Load);

    public static IReadOnlyCollection<FormReachEntry> All => [.. Table.Value.Values];

    /// <summary>SOLO PARA EL CLASIFICADOR (<c>pokekeep-bench-completar clasificar</c>): ignora la tabla para ver los candidatos tal como los ofrece la Librería.</summary>
    public static volatile bool Bypass;

    public static FormReachEntry? Lookup(int game, int species, int form) => !Bypass && Table.Value.TryGetValue((game, species, form), out var e) ? e : null;

    private static Dictionary<(int, int, int), FormReachEntry> Load()
    {
        var asm = typeof(FormReach).Assembly;
        using var stream = asm.GetManifestResourceStream("FormReach/formas-sin-ruta.json");
        if (stream is null) return [];
        var list = JsonSerializer.Deserialize<List<FormReachEntry>>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        return list.ToDictionary(e => (e.Game, e.Species, e.Form));
    }
}
