// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Pokekeep.Sprites;
using SkiaSharp;

/// <summary>
/// Generador de los sprites propios de cada juego (X51): lee la entrega X47 (<c>indice-sprites.json</c> + <c>sprites-pixelados\*.webp</c>), VERIFICA todos los ficheros contra su <c>SHA256SUMS</c> y escribe un paquete por juego
/// (<c>vista-&lt;juego&gt;.pkpack</c>, PNG sin perdida concatenados con <see cref="PngLite"/>) mas <c>manifest.json</c> (formato <see cref="Manifiesto"/>) y <c>procedencia-vista.tsv</c> (huella del WebP de cada recurso; no viaja en el APK).
/// Determinista: la misma entrega da los mismos bytes. <c>verificar</c> vuelve a generar en memoria y compara byte a byte con la salida y con las dimensiones del indice.
/// </summary>
internal static class Juegos
{
    private sealed record Item(string Nombre, string Juego, string Archivo, int Ancho, int Alto);

    /// <summary>Los diez juegos de la entrega X47 (Let's Go tiene la suya, X57).</summary>
    internal static readonly string[] JuegosX47 = [.. SpritesDeJuego.JuegosClasicos];

    public static int Ejecutar(string modo, string entrega, string salida)
    {
        var r = Construir(entrega, JuegosX47, 0, "sprites-pixelados");
        var salidas = new SortedDictionary<string, byte[]>(r.Paquetes, StringComparer.Ordinal);
        var manifiesto = new Manifiesto { Origen = "Sprites pixelados exactos de cada juego (entrega X47, volcados del propietario)", Paquetes = r.Info, Recursos = r.Entradas };
        salidas["manifest.json"] = manifiesto.Escribir();
        salidas["procedencia-vista.tsv"] = new UTF8Encoding(false).GetBytes(r.Tsv);
        return IconosCaja.Escribir(modo == "generar-juegos", salida, salidas, $"{r.Entradas.Count} sprites de juego");
    }

    internal static IconosCaja.Resultado Construir(string entrega, IReadOnlyList<string> juegosModo, int primerPaquete, string carpetaImagenes)
    {
        var sumas = LeerSumas(entrega);
        var fallos = 0;
        foreach (var (ruta, huella) in sumas)
        {
            var f = Path.Combine(entrega, ruta.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(f)) { Console.Error.WriteLine($"Falta {ruta}"); fallos++; continue; }
            if (!string.Equals(Manifiesto.Sha256Hex(File.ReadAllBytes(f)), huella, StringComparison.OrdinalIgnoreCase)) { Console.Error.WriteLine($"SHA-256 distinto: {ruta}"); fallos++; }
        }
        if (fallos > 0) throw new InvalidDataException($"La entrega no coincide con su SHA256SUMS ({fallos} fallos): no se genera nada.");
        Console.WriteLine($"Entrega verificada: {sumas.Count} ficheros con su SHA-256.");

        var indice = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(entrega, "indice-sprites.json"))).RootElement;
        var items = new List<Item>();
        foreach (var i in indice.GetProperty("imagenes").EnumerateArray())
        {
            var juego = i.GetProperty("juego").GetString()!;
            if (!juegosModo.Contains(juego)) throw new InvalidDataException("Juego desconocido en el indice: " + juego);
            var nombre = SpritesDeJuego.Nombre(SpritesDeJuego.ClaseVista, juego, i.GetProperty("especie").GetInt32(), i.GetProperty("forma").GetInt32(), i.GetProperty("hembra").GetBoolean(), i.GetProperty("variocolor").GetBoolean());
            items.Add(new Item(nombre, juego, i.GetProperty("archivo").GetString()!, i.GetProperty("ancho").GetInt32(), i.GetProperty("alto").GetInt32()));
        }
        if (items.Select(x => x.Nombre).Distinct(StringComparer.Ordinal).Count() != items.Count) throw new InvalidDataException("Nombres de recurso repetidos en el indice.");

        var salidas = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
        var paquetes = new List<PaqueteInfo>();
        var entradas = new Dictionary<string, EntradaRecurso>(StringComparer.Ordinal);
        var tsv = new StringBuilder("recurso\tpaquete\tficheroEntrega\tancho\talto\tsha256Webp\n");
        var decodificadas = new Dictionary<string, (SpriteImage Img, byte[] Origen)>(StringComparer.Ordinal);
        foreach (var it in items.Where(x => IconosCaja.UsaWebp(x.Juego)))
        {
            var origen = File.ReadAllBytes(Path.Combine(entrega, carpetaImagenes, it.Archivo.Replace('/', Path.DirectorySeparatorChar)));
            decodificadas[it.Nombre] = (Decodificar(origen), origen);
        }
        IconosCaja.PrepararWebp(decodificadas.Values.Select(d => d.Img));
        foreach (var juego in juegosModo)
        {
            var delJuego = items.Where(x => x.Juego == juego).OrderBy(x => x.Nombre, StringComparer.Ordinal).ToList();
            if (delJuego.Count == 0) throw new InvalidDataException("El indice no trae ninguna imagen de " + juego);
            var id = SpritesDeJuego.IdPaquete(SpritesDeJuego.ClaseVista, juego);
            using var ms = new MemoryStream();
            foreach (var it in delJuego)
            {
                var webp = decodificadas.TryGetValue(it.Nombre, out var d) ? d.Origen : File.ReadAllBytes(Path.Combine(entrega, carpetaImagenes, it.Archivo.Replace('/', Path.DirectorySeparatorChar)));
                var img = d.Img ?? Decodificar(webp);
                if (img.Width != it.Ancho || img.Height != it.Alto) throw new InvalidDataException($"{it.Archivo}: mide {img.Width}x{img.Height} y el indice dice {it.Ancho}x{it.Alto}.");
                byte[] png;
                if (IconosCaja.UsaWebp(juego))
                {
                    png = IconosCaja.WebpDe(img);   // render 3D: WebP sin perdida, comprobado pixel a pixel con el lector gestionado
                    var vuelta = WebpLite.Decodificar(png);
                    if (vuelta.Width != img.Width || vuelta.Height != img.Height || !vuelta.Pixels.AsSpan().SequenceEqual(img.Pixels)) throw new InvalidDataException($"{it.Archivo}: el WebP no devuelve los mismos pixeles.");
                }
                else
                {
                    png = CodificarIndexado(img) ?? PngLite.Codificar(img);
                    var vuelta = PngLite.Decodificar(png);
                    if (!vuelta.Pixels.AsSpan().SequenceEqual(img.Pixels)) throw new InvalidDataException($"{it.Archivo}: el PNG no devuelve los mismos pixeles.");
                }
                entradas[it.Nombre] = new EntradaRecurso(primerPaquete + paquetes.Count, ms.Position, png.Length, img.Width, img.Height);
                ms.Write(png);
                tsv.Append(it.Nombre).Append('\t').Append(id).Append('\t').Append(it.Archivo).Append('\t').Append(img.Width).Append('\t').Append(img.Height).Append('\t').Append(Manifiesto.Sha256Hex(webp)).Append('\n');
            }
            var bytes = ms.ToArray();
            var archivo = id + ".pkpack";
            salidas[archivo] = bytes;
            paquetes.Add(new PaqueteInfo(id, archivo, bytes.Length, Manifiesto.Sha256Hex(bytes), delJuego.Count));
            Console.WriteLine($"{archivo,-26} {delJuego.Count,5} recursos {bytes.Length,10:N0} bytes");
        }
        Console.WriteLine($"{items.Count(x => juegosModo.Contains(x.Juego))} sprites en {paquetes.Count} paquetes, {paquetes.Sum(p => p.Bytes):N0} bytes en total");
        return new IconosCaja.Resultado(salidas, paquetes, entradas, tsv.ToString(), [], []);
    }

    internal static List<(string Ruta, string Huella)> LeerSumas(string entrega)
    {
        var lista = new List<(string, string)>();
        foreach (var linea in File.ReadAllLines(Path.Combine(entrega, "SHA256SUMS")))
        {
            if (string.IsNullOrWhiteSpace(linea)) continue;
            var corte = linea.IndexOf("  ", StringComparison.Ordinal);
            if (corte != 64) throw new InvalidDataException("Linea de SHA256SUMS no entendida.");
            lista.Add((linea[66..].Trim(), linea[..64]));
        }
        return lista;
    }

    /// <summary>
    /// PNG indexado (color tipo 3 con PLTE y tRNS) cuando la imagen tiene 256 colores o menos (los sprites de cartucho tienen 4 a ~100): sin perdida y ~3 veces menor que el RGBA. Paleta ordenada por (alfa, B, G, R) para que sea
    /// determinista. Null si hay mas de 256 colores (se usa el PNG RGBA de <see cref="PngLite"/>).
    /// </summary>
    internal static byte[]? CodificarIndexado(SpriteImage img)
    {
        var colores = new SortedSet<uint>();
        var px = img.Pixels;
        for (var i = 0; i < px.Length; i += 4)
        {
            uint c = (uint)(px[i + 3] << 24 | px[i] << 16 | px[i + 1] << 8 | px[i + 2]);   // alfa, B, G, R
            if (px[i + 3] == 0) c = 0;                                                      // transparente = un solo color (el RGB de un pixel invisible no se ve)
            colores.Add(c);
            if (colores.Count > 256) return null;
        }
        var paleta = colores.ToList();
        var indice = new Dictionary<uint, byte>();
        for (var i = 0; i < paleta.Count; i++) indice[paleta[i]] = (byte)i;
        var crudo = new byte[(img.Width + 1) * img.Height];
        for (var y = 0; y < img.Height; y++)
            for (var x = 0; x < img.Width; x++)
            {
                var o = (y * img.Width + x) * 4;
                uint c = px[o + 3] == 0 ? 0 : (uint)(px[o + 3] << 24 | px[o] << 16 | px[o + 1] << 8 | px[o + 2]);
                crudo[y * (img.Width + 1) + 1 + x] = indice[c];
            }
        using var sal = new MemoryStream();
        sal.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        var cab = new byte[13];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(cab, img.Width); System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(cab.AsSpan(4), img.Height);
        cab[8] = 8; cab[9] = 3;
        Trozo(sal, "IHDR"u8, cab);
        var plte = new byte[paleta.Count * 3];
        for (var i = 0; i < paleta.Count; i++) { plte[i * 3] = (byte)(paleta[i] >> 0); plte[i * 3 + 1] = (byte)(paleta[i] >> 8); plte[i * 3 + 2] = (byte)(paleta[i] >> 16); }
        Trozo(sal, "PLTE"u8, plte);
        var ultimoNoOpaco = paleta.FindLastIndex(c => (c >> 24) != 255);
        if (ultimoNoOpaco >= 0) Trozo(sal, "tRNS"u8, paleta.Take(ultimoNoOpaco + 1).Select(c => (byte)(c >> 24)).ToArray());
        using (var z = new MemoryStream())
        {
            using (var zl = new System.IO.Compression.ZLibStream(z, System.IO.Compression.CompressionLevel.SmallestSize, true)) zl.Write(crudo);
            Trozo(sal, "IDAT"u8, z.ToArray());
        }
        Trozo(sal, "IEND"u8, []);
        return sal.ToArray();
    }

    private static void Trozo(Stream s, ReadOnlySpan<byte> nombre, ReadOnlySpan<byte> datos)
    {
        Span<byte> n = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(n, datos.Length);
        s.Write(n); s.Write(nombre); s.Write(datos);
        uint crc = 0xFFFFFFFF;
        foreach (var b in nombre) crc = TablaCrc[(crc ^ b) & 0xFF] ^ (crc >> 8);
        foreach (var b in datos) crc = TablaCrc[(crc ^ b) & 0xFF] ^ (crc >> 8);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(n, ~crc);
        s.Write(n);
    }

    private static readonly uint[] TablaCrc = Enumerable.Range(0, 256).Select(i => { var c = (uint)i; for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1; return c; }).ToArray();

    /// <summary>WebP sin perdida a BGRA no premultiplicado (el alfa real del sprite, sin tocar).</summary>
    internal static SpriteImage Decodificar(byte[] webp)
    {
        using var datos = SKData.CreateCopy(webp);
        using var codec = SKCodec.Create(datos) ?? throw new InvalidDataException("WebP no entendido.");
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        using var bmp = new SKBitmap(info);
        if (codec.GetPixels(info, bmp.GetPixels()) != SKCodecResult.Success) throw new InvalidDataException("WebP no decodificable.");
        var px = bmp.GetPixelSpan().ToArray();
        for (var i = 0; i < px.Length; i += 4) if (px[i + 3] == 0) { px[i] = 0; px[i + 1] = 0; px[i + 2] = 0; }   // el color de un pixel invisible no se ve: se normaliza a 0
        return new SpriteImage(info.Width, info.Height, px);
    }
}
