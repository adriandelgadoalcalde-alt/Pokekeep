// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
#if !POKEKEEP_PORTABLE
using System.Text;
using AutoLegalidad.Engine.SolutionCache;

namespace AutoLegalidad.Worker;

/// <summary>
/// SOLO DESARROLLO (ADR 0048): decorador de la cache de soluciones que anota, por cada consulta, la cadena canonica de la clave y el desenlace (acierto, sin entrada, entrada insuficiente o rechazada)
/// y, en modo registro, cada entrada que se guarda (<c>PUT</c>). Se activa con la variable de entorno <c>POKEKEEP_CACHE_DIAG</c> (carpeta; un fichero por proceso). Analizar con
/// <c>herramientas\analizar-fallos-cache.py</c>. No existe en el Worker del paquete y no cambia ninguna respuesta.
/// </summary>
internal sealed class DiagnosticSolutionCache(ISolutionCache inner, string file) : ISolutionCache
{
    [ThreadStatic] private static string? _canonical;
    [ThreadStatic] private static string? _lookup;
    private readonly object _gate = new();

    public bool Enabled => inner.Enabled;
    public bool Recording => inner.Recording;
    public string StatusText => inner.StatusText;
    public int Hits => inner.Hits;
    public int Misses => inner.Misses;
    public int Rejected => inner.Rejected;

    public bool TryGet(string key, out SolutionEntry entry)
    {
        _canonical = SolutionKeyDiagnostics.LastCanonical;
        var found = inner.TryGet(key, out entry);
        _lookup = found
            ? $"entrada:{entry.Family}:suma={entry.Sum}:exh={(entry.Exhaustive ? 1 : 0)}:cota={(entry.IsBound ? 1 : 0)}:semillas={entry.Seeds.Length}:costeMs={entry.CostMs}"
            : "sin-entrada";
        return found;
    }

    public void Put(string key, SolutionEntry entry)
    {
        var canonical = SolutionKeyDiagnostics.LastCanonical;
        inner.Put(key, entry);
        // En modo registro anota tambien lo que se guarda (misma cadena canonica): permite buscar la clave «casi igual» de un fallo posterior.
        if (inner.Recording)
            Append($"PUT\tsuma={entry.Sum}:exh={(entry.Exhaustive ? 1 : 0)}:cota={(entry.IsBound ? 1 : 0)}:semillas={entry.Seeds.Length}:costeMs={entry.CostMs}\t{(canonical ?? "?").Replace("\n", "|")}\n");
    }

    public void NoteHit() { inner.NoteHit(); Write("ACIERTO"); }
    public void NoteMiss() { inner.NoteMiss(); Write("FALLO"); }
    public void NoteRejected() { inner.NoteRejected(); Write("RECHAZADA"); }

    private void Write(string outcome)
    {
        var line = $"{outcome}\t{_lookup}\t{(_canonical ?? "?").Replace("\n", "|")}\n";
        _canonical = null; _lookup = null;
        Append(line);
    }

    private void Append(string line)
    {
        try { lock (_gate) File.AppendAllText(file, line, new UTF8Encoding(false)); }
        catch (IOException) { }
    }
}
#endif
