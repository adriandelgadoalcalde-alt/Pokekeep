// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Diagnostics;
using Pokekeep.Sprites;

namespace Pokekeep.Objetos.Build;

/// <summary>
/// Release (compactacion del APK sin perdida): optimiza los PNG de los objetos con <c>herramientas\optimizar-png-lote.py</c> (oxipng + zopfli; solo quita trozos auxiliares que no afectan al dibujo y recodifica el flujo de datos:
/// los pixeles RGBA, alfa incluido, son los mismos, y el propio script lo comprueba antes de escribir). Es la misma funcion que usa <c>herramientas\recomprimir-paquetes-sin-perdida.py</c> sobre un paquete ya generado
/// (<c>herramientas\imagen_sin_perdida.py</c>), asi que los dos caminos dan los mismos bytes. Cache por SHA-256 del PNG de origen en la carpeta temporal.
/// </summary>
internal static class OptimizadorPng
{
    private const string Version = "v1";

    /// <summary>SHA-256 del PNG de origen -> PNG optimizado, para cada PNG distinto de <paramref name="pngs"/>.</summary>
    public static Dictionary<string, byte[]> Optimizar(IEnumerable<byte[]> pngs)
    {
        var cache = Path.Combine(Path.GetTempPath(), "pokekeep-png-cache"); Directory.CreateDirectory(cache);
        var resultado = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var pendientes = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var png in pngs)
        {
            var sha = Fuente.Sha(png);
            if (resultado.ContainsKey(sha) || pendientes.ContainsKey(sha)) continue;
            var f = Path.Combine(cache, $"{Version}-{sha}.png");
            if (File.Exists(f)) resultado[sha] = File.ReadAllBytes(f); else pendientes[sha] = png;
        }
        if (pendientes.Count == 0) return resultado;
        var script = Path.Combine(AppContext.BaseDirectory, "optimizar-png-lote.py");
        if (!File.Exists(script)) script = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "herramientas", "optimizar-png-lote.py"));
        if (!File.Exists(script))
        {
            // Compilación desde el código público: la herramienta de compactación (python, oxipng, zopfli) no se publica. Los PNG entran tal como se descargaron: mismos píxeles, algo más de tamaño.
            Console.Error.WriteLine($"aviso: no está optimizar-png-lote.py; {pendientes.Count} PNG se empaquetan sin optimizar.");
            foreach (var (sha, png) in pendientes) resultado[sha] = png;
            return resultado;
        }
        var entrada = Path.Combine(cache, "entrada"); var salida = Path.Combine(cache, "salida");
        foreach (var d in new[] { entrada, salida }) { if (Directory.Exists(d)) Directory.Delete(d, true); Directory.CreateDirectory(d); }
        var indice = pendientes.Keys.ToList();
        for (var i = 0; i < indice.Count; i++) File.WriteAllBytes(Path.Combine(entrada, i + ".png"), pendientes[indice[i]]);
        var psi = new ProcessStartInfo(Python(script)) { RedirectStandardError = true };
        psi.Environment["POKEKEEP_NUCLEOS"] = (int.TryParse(Environment.GetEnvironmentVariable("POKEKEEP_NUCLEOS"), out var n) && n > 0 ? n : 1).ToString();
        psi.ArgumentList.Add("-I"); psi.ArgumentList.Add(script); psi.ArgumentList.Add(entrada); psi.ArgumentList.Add(salida);
        Console.WriteLine($"Optimizando {pendientes.Count} PNG sin perdida (oxipng + zopfli)...");
        using (var p = Process.Start(psi) ?? throw new InvalidOperationException("No se pudo lanzar python."))
        {
            var err = p.StandardError.ReadToEnd(); p.WaitForExit();
            if (p.ExitCode != 0) throw new InvalidOperationException("optimizar-png-lote.py fallo: " + err);
        }
        for (var i = 0; i < indice.Count; i++)
        {
            var b = File.ReadAllBytes(Path.Combine(salida, i + ".png"));
            // Doble comprobacion con el decodificador de la app: mismas dimensiones y mismos pixeles.
            var a = PngLite.Decodificar(pendientes[indice[i]]); var d = PngLite.Decodificar(b);
            if (a.Width != d.Width || a.Height != d.Height || !a.Pixels.AsSpan().SequenceEqual(d.Pixels)) throw new InvalidDataException($"PNG {indice[i]}: la optimizacion cambia los pixeles.");
            resultado[indice[i]] = b; File.WriteAllBytes(Path.Combine(cache, $"{Version}-{indice[i]}.png"), b);
        }
        return resultado;
    }

    /// <summary>
    /// El Python que tiene pyoxipng: <c>POKEKEEP_PYTHON</c> o, si no, la clave <c>pylibs_oxipng.python</c> del <c>herramientas.json</c> de la carpeta Keep (se busca subiendo desde el script, como
    /// <c>imagen_sin_perdida.py</c> busca <c>dev-tools\pylibs</c>). Hasta el 26-oct-2026 se lanzaba «python» por su nombre: con el Python 3.14 de la Microsoft Store primero en el PATH, el oxipng compilado
    /// para 3.12 no cargaba. Solo si no hay ninguna de las dos se usa el del PATH.
    /// </summary>
    internal static string Python(string script)
    {
        if (Environment.GetEnvironmentVariable("POKEKEEP_PYTHON") is { Length: > 0 } dado) return dado;
        for (var d = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(script))!); d is not null; d = d.Parent)
        {
            var json = Path.Combine(d.FullName, "herramientas.json");
            if (!File.Exists(json)) continue;
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(json));
                if (Buscar(doc.RootElement) is { } ruta && File.Exists(ruta)) return ruta;
            }
            catch (System.Text.Json.JsonException) { }
        }
        return "python";

        static string? Buscar(System.Text.Json.JsonElement e)
        {
            if (e.ValueKind == System.Text.Json.JsonValueKind.Array) { foreach (var x in e.EnumerateArray()) if (Buscar(x) is { } r) return r; return null; }
            if (e.ValueKind != System.Text.Json.JsonValueKind.Object) return null;
            if (e.TryGetProperty("pylibs_oxipng", out var o) && o.ValueKind == System.Text.Json.JsonValueKind.Object && o.TryGetProperty("python", out var p) && p.ValueKind == System.Text.Json.JsonValueKind.String) return p.GetString();
            foreach (var prop in e.EnumerateObject()) if (Buscar(prop.Value) is { } r) return r;
            return null;
        }
    }
}
