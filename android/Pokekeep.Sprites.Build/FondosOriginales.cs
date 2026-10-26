// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Text;
using System.Text.Json;
using Pokekeep.Sprites;

/// <summary>
/// Generador de los fondos de caja ORIGINALES de cada juego (X54): lee la entrega X52 (<c>indice.json</c> + <c>caja/&lt;clave&gt;.png</c>), VERIFICA todos sus ficheros contra su <c>SHA256SUMS</c> y escribe un unico paquete
/// <c>fondos-originales.pkpack</c> (PNG sin perdida concatenados) mas <c>manifest-fondos.json</c> (formato <see cref="Manifiesto"/>) y <c>procedencia-fondos.tsv</c> (no viaja en el APK). Las claves son las MISMAS <c>box_wp*</c> de PKHeX
/// y cada imagen mide lo mismo que la de PKHeX (lo dice el indice de la entrega y se comprueba aqui contra los PNG); la app busca primero esta clave y, si no esta, cae al fondo de PKHeX. Solo entran los recursos con estado «exacto»:
/// los «sin_volcado» (Rubi/Zafiro 13-16, Blanco/Negro 17-24) y los «no_localizado» (Espada/Escudo, Escarlata/Purpura, Diamante Brillante/Perla Reluciente) siguen con PKHeX. Determinista: la misma entrega da los mismos bytes.
/// </summary>
internal static class FondosOriginales
{
    public const string IdPaquete = "fondos-originales";

    public static int Ejecutar(string modo, string entrega, string salida)
    {
        var sumas = Juegos.LeerSumas(entrega);
        var fallos = 0;
        foreach (var (ruta, huella) in sumas)
        {
            var f = Path.Combine(entrega, ruta.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(f)) { Console.Error.WriteLine($"Falta {ruta}"); fallos++; continue; }
            if (!string.Equals(Manifiesto.Sha256Hex(File.ReadAllBytes(f)), huella, StringComparison.OrdinalIgnoreCase)) { Console.Error.WriteLine($"SHA-256 distinto: {ruta}"); fallos++; }
        }
        if (fallos > 0) { Console.Error.WriteLine($"La entrega no coincide con su SHA256SUMS ({fallos} fallos): no se genera nada."); return 1; }
        Console.WriteLine($"Entrega verificada: {sumas.Count} ficheros con su SHA-256.");

        var cubiertos = sumas.Select(x => x.Ruta).ToHashSet(StringComparer.Ordinal);
        var raiz = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(entrega, "indice.json"))).RootElement.GetProperty("recursos");
        var exactos = new SortedDictionary<string, JsonElement>(StringComparer.Ordinal);
        var otros = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var r in raiz.EnumerateObject())
        {
            var estado = r.Value.GetProperty("estado").GetString()!;
            if (estado == "exacto") exactos[r.Name] = r.Value;
            else otros[estado] = otros.GetValueOrDefault(estado) + 1;
        }
        if (exactos.Count == 0) throw new InvalidDataException("El indice no trae ningun fondo exacto.");

        using var ms = new MemoryStream();
        var entradas = new Dictionary<string, EntradaRecurso>(StringComparer.Ordinal);
        var tsv = new StringBuilder("recurso\tjuego\tindiceJuego\tancho\talto\tsha256Entrega\n");
        foreach (var (clave, e) in exactos)
        {
            if (!clave.StartsWith("box_wp", StringComparison.Ordinal)) throw new InvalidDataException("Clave que no es de fondo de caja: " + clave);
            var ruta = e.GetProperty("caja").GetString()!;
            if (!cubiertos.Contains(ruta)) throw new InvalidDataException("Un fondo del indice no esta en SHA256SUMS: " + ruta);
            var bytes = File.ReadAllBytes(Path.Combine(entrega, ruta.Replace('/', Path.DirectorySeparatorChar)));
            var huellaIndice = e.GetProperty("sha256").GetString()!;
            if (!string.Equals(Manifiesto.Sha256Hex(bytes), huellaIndice, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException($"{ruta}: su huella no es la del indice.");
            var img = PngLite.Decodificar(bytes);
            int w = e.GetProperty("ancho").GetInt32(), h = e.GetProperty("alto").GetInt32();
            if (img.Width != w || img.Height != h) throw new InvalidDataException($"{ruta}: mide {img.Width}x{img.Height} y el indice dice {w}x{h}.");
            var png = Juegos.CodificarIndexado(img) ?? PngLite.Codificar(img);
            if (!PngLite.Decodificar(png).Pixels.AsSpan().SequenceEqual(img.Pixels)) throw new InvalidDataException($"{ruta}: el PNG no devuelve los mismos pixeles.");
            entradas[clave] = new EntradaRecurso(0, ms.Position, png.Length, w, h);
            ms.Write(png);
            tsv.Append(clave).Append('\t').Append(e.GetProperty("juego").GetString()).Append('\t').Append(e.GetProperty("indice_juego").GetInt32()).Append('\t').Append(w).Append('\t').Append(h).Append('\t').Append(huellaIndice).Append('\n');
        }
        var paquete = ms.ToArray();
        var archivo = IdPaquete + ".pkpack";
        var manifiesto = new Manifiesto
        {
            Origen = "Fondos de caja exactos de cada juego (entrega X52, volcados del propietario)",
            Paquetes = [new PaqueteInfo(IdPaquete, archivo, paquete.Length, Manifiesto.Sha256Hex(paquete), exactos.Count)],
            Recursos = entradas,
        };
        var salidas = new SortedDictionary<string, byte[]>(StringComparer.Ordinal)
        {
            [archivo] = paquete,
            ["manifest-fondos.json"] = manifiesto.Escribir(),
            ["procedencia-fondos.tsv"] = new UTF8Encoding(false).GetBytes(tsv.ToString()),
        };
        Console.WriteLine($"{archivo}: {exactos.Count} fondos, {paquete.Length:N0} bytes; sin fondo original: {string.Join(", ", otros.Select(kv => $"{kv.Key} {kv.Value}"))}");

        if (modo == "generar-fondos")
        {
            Directory.CreateDirectory(salida);
            foreach (var (n, b) in salidas) File.WriteAllBytes(Path.Combine(salida, n), b);
            return 0;
        }
        foreach (var (n, b) in salidas)
        {
            var f = Path.Combine(salida, n);
            if (!File.Exists(f)) { Console.Error.WriteLine("Falta " + n); fallos++; }
            else if (!File.ReadAllBytes(f).AsSpan().SequenceEqual(b)) { Console.Error.WriteLine("Distinto de lo regenerado: " + n); fallos++; }
        }
        Console.WriteLine(fallos == 0 ? $"OK: {exactos.Count} fondos de caja identicos a lo regenerado de la entrega" : $"FALLOS: {fallos}");
        return fallos == 0 ? 0 : 1;
    }
}
