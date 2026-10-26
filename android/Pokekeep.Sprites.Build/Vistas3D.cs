// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Text;
using System.Text.Json;
using Pokekeep.Sprites;

/// <summary>
/// X71: vistas previas de Gen. 6 a 9 (renders 3D de las capturas de los juegos originales, X70). Lee la ENTREGA que prepara <c>herramientas\extraer-vistas-gen6-9-x71.py</c> (<c>indice-vistas.json</c> + un PNG sin perdida por
/// imagen + <c>SHA256SUMS</c>), la VERIFICA entera contra su <c>SHA256SUMS</c> y escribe un paquete por grupo de juegos (<c>vista-&lt;juego&gt;.pkpack</c>, WebP sin perdida), <c>manifest-vista3d.json</c> y
/// <c>procedencia-vista3d.tsv</c> (archivo de origen y SHA-256 de cada imagen). Los alias (formas iguales por diseno, Senores de Hisui) comparten los bytes de su imagen. Determinista; <c>verificar</c> regenera en memoria y
/// compara byte a byte con la salida. NUNCA reescala ni baja el color: cada pixel del PNG de la entrega es el del WebP.
/// </summary>
internal static class Vistas3D
{
    internal const string Origen = "Vistas previas de Gen. 6 a 9: renders 3D de las capturas de los juegos originales (X70/X71), recortados a la silueta con margen fijo, sin perdida.";

    public static int Ejecutar(string modo, string entrega, string salida)
    {
        var r = Construir(entrega);
        var manifiesto = new Manifiesto { Origen = Origen, Paquetes = r.Info, Recursos = r.Entradas };
        var salidas = new SortedDictionary<string, byte[]>(r.Paquetes, StringComparer.Ordinal)
        {
            ["manifest-vista3d.json"] = manifiesto.Escribir(),
            ["procedencia-vista3d.tsv"] = new UTF8Encoding(false).GetBytes(r.Tsv),
        };
        Console.WriteLine($"manifest-vista3d.json: {r.Entradas.Count} vistas previas en {r.Info.Count} paquetes, {r.Info.Sum(p => p.Bytes):N0} bytes.");
        return IconosCaja.Escribir(modo == "generar-vistas3d", salida, salidas, $"{r.Entradas.Count} vistas previas de Gen. 6 a 9");
    }

    /// <summary>
    /// Mide en este PC lo que cuesta abrir y usar cada paquete (lo que hace la app la primera vez que se pide una vista de ese juego): leer y verificar el SHA-256 del paquete entero, y decodificar vistas sueltas.
    /// En el movil sera mas lento; el numero vale para comparar paquetes y para ver la memoria que ocupa el paquete cargado.
    /// </summary>
    public static int Medir(string carpeta)
    {
        var json = File.ReadAllBytes(Path.Combine(carpeta, "manifest-vista3d.json"));
        var m = Manifiesto.Leer(json);
        Console.WriteLine("juego   paquete(MB)  abrir+SHA(ms)  decodificar(ms/vista)  vistas  cache(MB)  memoria(MB)");
        foreach (var j in SpritesDeJuego.JuegosVista3D)
        {
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            var antes = GC.GetTotalMemory(true);
            var r = new RecursosJuego([json], f => File.ReadAllBytes(Path.Combine(carpeta, f)));
            var nombres = m.Recursos.Where(kv => kv.Key.StartsWith($"vista_{j}_", StringComparison.Ordinal)).Select(kv => kv.Key).OrderBy(n => n, StringComparer.Ordinal).ToList();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            r.Obtener(nombres[0]);
            var abrir = sw.Elapsed.TotalMilliseconds;
            var paso = Math.Max(1, nombres.Count / 300); var cuenta = 0;
            sw.Restart();
            for (var i = 1; i < nombres.Count; i += paso) { r.Obtener(nombres[i]); cuenta++; }
            var cada = sw.Elapsed.TotalMilliseconds / Math.Max(1, cuenta);
            var fuente = r.Fuentes.Single();
            var despues = GC.GetTotalMemory(true);
            Console.WriteLine($"{j,-7} {m.Paquetes.Single(p => p.Id == "vista-" + j).Bytes / 1048576.0,11:0.0} {abrir,14:0} {cada,22:0.0} {cuenta,7} {fuente.BytesEnCache / 1048576.0,10:0.0} {(despues - antes) / 1048576.0,12:0.0}");
        }
        return 0;
    }

    private sealed record Item(string Nombre, string Juego, string Archivo, int Ancho, int Alto, string? AliasDe, string Origen, string OrigenSha);

    internal static IconosCaja.Resultado Construir(string entrega)
    {
        var sumas = Juegos.LeerSumas(entrega);
        var fallos = 0;
        foreach (var (ruta, huella) in sumas)
        {
            var f = Path.Combine(entrega, ruta.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(f)) { Console.Error.WriteLine($"Falta {ruta}"); fallos++; continue; }
            if (!string.Equals(Manifiesto.Sha256Hex(File.ReadAllBytes(f)), huella, StringComparison.OrdinalIgnoreCase)) { Console.Error.WriteLine($"SHA-256 distinto: {ruta}"); fallos++; }
        }
        if (fallos > 0) throw new InvalidDataException($"La entrega no coincide con su SHA256SUMS ({fallos} fallos): no se genera nada.");
        var cubiertos = sumas.Select(x => x.Ruta).ToHashSet(StringComparer.Ordinal);
        if (!cubiertos.Contains("indice-vistas.json")) throw new InvalidDataException("indice-vistas.json no esta en SHA256SUMS.");
        Console.WriteLine($"Entrega verificada: {sumas.Count} ficheros con su SHA-256.");

        var raiz = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(entrega, "indice-vistas.json"))).RootElement;
        var items = new List<Item>();
        foreach (var i in raiz.GetProperty("imagenes").EnumerateArray())
        {
            var juego = i.GetProperty("juego").GetString()!;
            if (!SpritesDeJuego.JuegosVista3D.Contains(juego)) throw new InvalidDataException("Juego desconocido en el indice: " + juego);
            var nombre = SpritesDeJuego.Nombre(SpritesDeJuego.ClaseVista, juego, i.GetProperty("especie").GetInt32(), i.GetProperty("forma").GetInt32(), i.GetProperty("hembra").GetBoolean(), i.GetProperty("variocolor").GetBoolean());
            if (nombre != i.GetProperty("nombre").GetString()) throw new InvalidDataException($"El nombre del indice no es el canonico: {i.GetProperty("nombre").GetString()} / {nombre}");
            var archivo = i.GetProperty("archivo").GetString()!;
            if (!cubiertos.Contains(archivo)) throw new InvalidDataException($"{archivo} no esta en SHA256SUMS.");
            var alias = i.TryGetProperty("alias_de", out var a) ? a.GetString() : null;
            string origen = "", sha = "";
            if (i.TryGetProperty("origen", out var o)) { origen = o.GetProperty("carpeta").GetString() + "/" + o.GetProperty("fila").GetString() + "/" + o.GetProperty("png").GetString(); sha = o.GetProperty("sha256").GetString()!; }
            items.Add(new Item(nombre, juego, archivo, i.GetProperty("ancho").GetInt32(), i.GetProperty("alto").GetInt32(), alias, origen, sha));
        }
        if (items.Select(x => x.Nombre).Distinct(StringComparer.Ordinal).Count() != items.Count) throw new InvalidDataException("Nombres de recurso repetidos en el indice.");

        // Cada imagen propia se decodifica de su PNG (sin perdida) y se normaliza el color de los pixeles invisibles a 0 (no se ve).
        var imagenes = new Dictionary<string, SpriteImage>(StringComparer.Ordinal);
        foreach (var it in items.Where(x => x.AliasDe is null))
        {
            var img = PngLite.Decodificar(File.ReadAllBytes(Path.Combine(entrega, it.Archivo.Replace('/', Path.DirectorySeparatorChar))));
            if (img.Width != it.Ancho || img.Height != it.Alto) throw new InvalidDataException($"{it.Archivo}: mide {img.Width}x{img.Height} y el indice dice {it.Ancho}x{it.Alto}.");
            for (var p = 0; p < img.Pixels.Length; p += 4) if (img.Pixels[p + 3] == 0) { img.Pixels[p] = 0; img.Pixels[p + 1] = 0; img.Pixels[p + 2] = 0; }
            imagenes[it.Nombre] = img;
        }
        // 12.500 imagenes. Release (compactacion del APK sin perdida): esfuerzo MAXIMO de libwebp (metodo 6, antes 4: ~2,5 % menos, ~1,2 s por imagen en lugar de ~0,17 s) y exact=True (se conserva el RGB de los pixeles
        // invisibles). Es lo mismo que hace herramientas\recomprimir-paquetes-sin-perdida.py sobre un paquete ya generado: los dos caminos dan los mismos bytes. Hasta POKEKEEP_NUCLEOS procesos (3 por defecto: el PC se reparte con el resto de agentes).
        IconosCaja.MetodoWebp = 6;
        IconosCaja.ExactoWebp = true;
        IconosCaja.NucleosWebp = int.TryParse(Environment.GetEnvironmentVariable("POKEKEEP_NUCLEOS"), out var nucleos) && nucleos > 0 ? nucleos : 3;
        IconosCaja.PrepararWebp(imagenes.Values);

        var salidas = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
        var paquetes = new List<PaqueteInfo>();
        var entradas = new Dictionary<string, EntradaRecurso>(StringComparer.Ordinal);
        var tsv = new StringBuilder("recurso\tpaquete\tficheroOrigen\tancho\talto\tsha256Origen\talias\n");
        foreach (var juego in SpritesDeJuego.JuegosVista3D)
        {
            var delJuego = items.Where(x => x.Juego == juego).OrderBy(x => x.Nombre, StringComparer.Ordinal).ToList();
            if (delJuego.Count == 0) throw new InvalidDataException("El indice no trae ninguna imagen de " + juego);
            var id = SpritesDeJuego.IdPaquete(SpritesDeJuego.ClaseVista, juego);
            var indice = paquetes.Count;
            using var ms = new MemoryStream();
            foreach (var it in delJuego.Where(x => x.AliasDe is null))
            {
                var img = imagenes[it.Nombre];
                var webp = IconosCaja.WebpDe(img);
                var vuelta = WebpLite.Decodificar(webp);
                if (vuelta.Width != img.Width || vuelta.Height != img.Height || !vuelta.Pixels.AsSpan().SequenceEqual(img.Pixels)) throw new InvalidDataException($"{it.Archivo}: el WebP no devuelve los mismos pixeles.");
                entradas[it.Nombre] = new EntradaRecurso(indice, ms.Position, webp.Length, img.Width, img.Height);
                ms.Write(webp);
                tsv.Append(it.Nombre).Append('\t').Append(id).Append('\t').Append(it.Origen).Append('\t').Append(img.Width).Append('\t').Append(img.Height).Append('\t').Append(it.OrigenSha).Append('\t').Append('\n');
            }
            foreach (var it in delJuego.Where(x => x.AliasDe is not null))
            {
                if (!entradas.TryGetValue(it.AliasDe!, out var fuente)) throw new InvalidDataException($"{it.Nombre}: su imagen de origen {it.AliasDe} no esta en el paquete.");
                entradas[it.Nombre] = fuente;
                tsv.Append(it.Nombre).Append('\t').Append(id).Append('\t').Append('\t').Append(fuente.Ancho).Append('\t').Append(fuente.Alto).Append('\t').Append('\t').Append(it.AliasDe).Append('\n');
            }
            var bytes = ms.ToArray();
            var archivo = id + ".pkpack";
            salidas[archivo] = bytes;
            paquetes.Add(new PaqueteInfo(id, archivo, bytes.Length, Manifiesto.Sha256Hex(bytes), delJuego.Count));
            Console.WriteLine($"{archivo,-22} {delJuego.Count,5} recursos {bytes.Length,12:N0} bytes");
        }
        return new IconosCaja.Resultado(salidas, paquetes, entradas, tsv.ToString(), [], []);
    }
}
