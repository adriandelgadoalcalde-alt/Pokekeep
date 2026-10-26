// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace AutoLegalidad.Engine.SolutionCache;

/// <summary>Familias de busqueda que guardan solucion (R-011, ADR 0037).</summary>
public static class SolutionFamily
{
    /// <summary>Busqueda de semillas de <c>SeedSpaceSearch</c> (modelos de semilla de Gen3, 8 y 9): <c>Seeds</c> son semillas del modelo.</summary>
    public const string Seed = "seed";

    /// <summary>Busqueda LCRNG clasica de Gen3/4 (<c>ClassicLcrngSearch</c>): <c>Seeds</c> = [PID] y <c>Ivs</c> = vector de IV (orden visual PS, Ata, Def, AtE, DeE, Vel).</summary>
    public const string Lcrng = "lcrng";

    /// <summary>
    /// Entrada UNIVERSAL de un encuentro (ADR 0041): una sola fila por encuentro con el mejor resultado de CADA casilla de rasgos (naturaleza, sexo, habilidad, Teratipo...) hallada en un unico
    /// barrido de las 2^32 semillas, en <c>Blob</c> (formato <see cref="UniversePayload"/>). No depende del candidato que genero la consulta.
    /// </summary>
    public const string EncounterUniverse = "universe";
}

/// <summary>
/// Solucion guardada de UNA busqueda. Dos formas:
/// <list type="bullet">
/// <item><b>exacta</b> (<c>Seeds</c> no vacio): las mejores semillas (o el PID de la familia LCRNG) halladas, de mejor a peor, hasta 3;</item>
/// <item><b>cota</b> (<c>Seeds</c> vacio): la busqueda NO hallo nada que supere <c>Sum</c> (suma de IV con la que se lanzo); sirve para dar el veredicto «sin mejora» a quien ya tenga una suma &gt;= <c>Sum</c>.</item>
/// </list>
/// </summary>
/// <param name="Family"><see cref="SolutionFamily"/>.</param>
/// <param name="Seeds">Semillas (mejor primero) o [PID] en la familia LCRNG. Vacio = cota.</param>
/// <param name="Sum">Suma de IV de la mejor solucion (exacta) o cota superior de lo hallable (cota).</param>
/// <param name="Exhaustive">La busqueda fue COMPLETA sobre su dominio (demostro el maximo o demostro que no hay mejora).</param>
/// <param name="Scanned">Semillas recorridas (familia seed) o vectores de IV probados (familia lcrng); entra en el texto del veredicto.</param>
/// <param name="CostMs">Cuanta busqueda costo; una entrada no exhaustiva solo se sirve a quien tenia un presupuesto no mayor.</param>
/// <param name="Ivs">Vector de IV de la mejor solucion (solo familia LCRNG; <c>null</c> en el resto).</param>
/// <param name="Blob">Contenido binario de una entrada <see cref="SolutionFamily.EncounterUniverse"/> (<c>null</c> en el resto).</param>
public sealed record SolutionEntry(string Family, ulong[] Seeds, int Sum, bool Exhaustive, ulong Scanned, long CostMs, int[]? Ivs = null, byte[]? Blob = null)
{
    /// <summary>True si es una cota («no hay nada mejor que <see cref="Sum"/>») y no una solucion.</summary>
    public bool IsBound => Seeds.Length == 0 && Blob is null;
}

/// <summary>Cache persistente de soluciones: el motor la consulta ANTES de buscar y siempre revalida lo que devuelve con el Core.</summary>
public interface ISolutionCache
{
    /// <summary>False si la cache no esta usable (ausente, invalidada por cambio de version, corrupta...): el motor busca como siempre.</summary>
    bool Enabled { get; }

    /// <summary>True si ademas acepta <see cref="Put"/> (solo el modo de REGISTRO del precalculo).</summary>
    bool Recording { get; }

    /// <summary>Estado legible: «activa», «invalidada: cambio X», «error: ...».</summary>
    string StatusText { get; }

    /// <summary>Aciertos validos servidos, solicitudes sin entrada y entradas encontradas pero rechazadas por la revalidacion.</summary>
    int Hits { get; }
    int Misses { get; }
    int Rejected { get; }

    bool TryGet(string key, out SolutionEntry entry);

    /// <summary>Guarda (o mejora) la solucion de una clave. No-op si no es modo de registro. Nunca lanza.</summary>
    void Put(string key, SolutionEntry entry);

    void NoteHit();
    void NoteMiss();
    void NoteRejected();
}

/// <summary>Cache desactivada: lo que usa el motor mientras nadie instale otra (pruebas, Worker sin fichero de soluciones...).</summary>
public sealed class NullSolutionCache : ISolutionCache
{
    public static readonly NullSolutionCache Instance = new();
    public bool Enabled => false;
    public bool Recording => false;
    public string StatusText => "desactivada";
    public int Hits => 0;
    public int Misses => 0;
    public int Rejected => 0;
    public bool TryGet(string key, out SolutionEntry entry) { entry = null!; return false; }
    public void Put(string key, SolutionEntry entry) { }
    public void NoteHit() { }
    public void NoteMiss() { }
    public void NoteRejected() { }
}

/// <summary>
/// Cache ambiente del proceso (como <see cref="Optimization.OptimizerSwitches"/>): el Worker la fija UNA vez al arrancar; las pruebas la cambian con <see cref="Use"/> y la restauran al soltar el resultado.
/// </summary>
public static class SolutionCacheHost
{
    private static volatile ISolutionCache _current = NullSolutionCache.Instance;

    public static ISolutionCache Current
    {
        get => _current;
        set => _current = value ?? NullSolutionCache.Instance;
    }

    /// <summary>Instala <paramref name="cache"/> hasta que se suelte el resultado (entonces vuelve la anterior).</summary>
    public static IDisposable Use(ISolutionCache cache)
    {
        var previous = _current;
        _current = cache ?? NullSolutionCache.Instance;
        return new Restore(previous);
    }

    private sealed class Restore(ISolutionCache previous) : IDisposable
    {
        private int _done;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _done, 1) == 0) _current = previous;
        }
    }
}
