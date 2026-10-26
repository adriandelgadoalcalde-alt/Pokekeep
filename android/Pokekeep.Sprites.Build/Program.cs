// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Pokekeep.Sprites;

// Generador de paquetes de sprites (F3, ADR 0057).
//   dotnet run --project Pokekeep.Sprites.Build -- generar --resx <Resources.resx> --salida <dir> [--origen <texto>]
//   dotnet run --project Pokekeep.Sprites.Build -- verificar --resx <Resources.resx> --salida <dir>
//   dotnet run --project Pokekeep.Sprites.Build -- generar-juegos|verificar-juegos --entrega <datos-nuevos X47> --salida <dir>   (X51)
//   dotnet run --project Pokekeep.Sprites.Build -- generar-caja|verificar-caja --entrega <iconos-caja-originales X49> --salida <dir>   (X53)
//   dotnet run --project Pokekeep.Sprites.Build -- generar-lgpe|verificar-lgpe --entrega <carpeta android de la entrega X57> --salida <dir>   (X58)
//   dotnet run --project Pokekeep.Sprites.Build -- generar-fondos|verificar-fondos --entrega <fondos-originales X52> --salida <dir>   (X54)
//   dotnet run --project Pokekeep.Sprites.Build -- generar-vistas3d|verificar-vistas3d --entrega <entrega de extraer-vistas-gen6-9-x71.py> --salida <dir>   (X71)
//   dotnet run --project Pokekeep.Sprites.Build -- comparar-paquetes --antes <datos/android> --despues <carpeta recomprimida> [--tsv <salida>]   (release: pixeles identicos tras recomprimir, con los decodificadores de la app)
//   dotnet run --project Pokekeep.Sprites.Build -- catalogo-vistas3d --salida <catalogo-vistas3d.json>   (X71: especies y formas guardables por juego, del Core)
// Lee Resources.resx de PKHeX.Drawing.PokeSprite (6.411 recursos) y empaqueta los PNG ORIGINALES, byte a byte, en un paquete por familia
// (los nombres de recurso son los de PKHeX), mas manifest.json y procedencia.tsv. Determinista: la misma entrada da los mismos bytes.
return Programa.Ejecutar(args);

internal static class Programa
{
    // Carpeta del PNG en Resources\img -> familia (paquete).
    private static readonly (string Carpeta, string Familia)[] Familias =
    [
        ("Artwork Pokemon Sprites", "artwork"), ("Artwork Shiny Sprites", "artwork_shiny"),
        ("Big Pokemon Sprites", "big"), ("Big Shiny Sprites", "big_shiny"),
        ("Legends Arceus Sprites", "arceus"), ("Legends Arceus Shiny Sprites", "arceus_shiny"),
        ("Artwork Items", "items_a"), ("Big Items", "items_b"),
        ("ball", "ball"), ("Pokemon Sprite Overlays", "overlays"), ("Status", "status"), ("accents", "accents"),
        ("", "misc"), // hint.png, valid.png, warn.png en la raiz de img\
        ("fondos", "fondos"), // fondos de caja de PKHeX.Drawing.Misc (F4)
    ];

    public static int Ejecutar(string[] args)
    {
        if (args.Length > 0 && args[0] is "comparar-paquetes") return ComparadorPaquetes.Ejecutar(args);
        if (args.Length > 2 && args[0] is "medir-vistas3d" && args[1] == "--salida") return Vistas3D.Medir(Path.GetFullPath(args[2]));
        if (args.Length > 0 && args[0] is "catalogo-vistas3d") return CatalogoVistas3D.Ejecutar(args.Length > 2 && args[1] == "--salida" ? Path.GetFullPath(args[2]) : "catalogo-vistas3d.json");
        if (args.Length > 0 && args[0] is "generar-juegos" or "verificar-juegos" or "generar-caja" or "verificar-caja" or "generar-fondos" or "verificar-fondos" or "generar-lgpe" or "verificar-lgpe" or "generar-lgpe-caja" or "verificar-lgpe-caja" or "generar-vistas3d" or "verificar-vistas3d") return EjecutarJuegos(args);
        if (args.Length == 0 || args[0] is not ("generar" or "verificar")) { Console.Error.WriteLine("Uso: generar|verificar --resx <Resources.resx> --salida <dir> [--origen <texto>]"); return 2; }
        string? resx = null, salida = null, origen = "PKHeX.Drawing.PokeSprite", misc = null;
        for (var i = 1; i + 1 < args.Length; i += 2)
            switch (args[i]) { case "--resx": resx = args[i + 1]; break; case "--resx-misc": misc = args[i + 1]; break; case "--salida": salida = args[i + 1]; break; case "--origen": origen = args[i + 1]; break; default: Console.Error.WriteLine($"Opcion desconocida {args[i]}"); return 2; }
        if (resx is null || salida is null) { Console.Error.WriteLine("Faltan --resx y --salida."); return 2; }
        try { return args[0] == "generar" ? Generar(resx, misc, salida, origen!) : Verificar(resx, misc, salida); }
        catch (Exception e) { Console.Error.WriteLine("ERROR: " + e.Message); return 1; }
    }

    // X51: sprites propios de cada juego. generar-juegos|verificar-juegos --entrega <datos-nuevos de X47> --salida <datos/android/sprites-juego>
    private static int EjecutarJuegos(string[] args)
    {
        string? entrega = null, salida = null;
        for (var i = 1; i + 1 < args.Length; i += 2)
            switch (args[i]) { case "--entrega": entrega = args[i + 1]; break; case "--salida": salida = args[i + 1]; break; default: Console.Error.WriteLine($"Opcion desconocida {args[i]}"); return 2; }
        if (entrega is null || salida is null) { Console.Error.WriteLine("Faltan --entrega y --salida."); return 2; }
        try { if (args[0].EndsWith("-vistas3d", StringComparison.Ordinal)) return Vistas3D.Ejecutar(args[0], Path.GetFullPath(entrega), Path.GetFullPath(salida)); if (args[0].EndsWith("-lgpe-caja", StringComparison.Ordinal)) return LgpeCaja.Ejecutar(args[0], Path.GetFullPath(entrega), Path.GetFullPath(salida)); if (args[0].EndsWith("-lgpe", StringComparison.Ordinal)) return Lgpe.Ejecutar(args[0], Path.GetFullPath(entrega), Path.GetFullPath(salida)); if (args[0].EndsWith("-fondos", StringComparison.Ordinal)) return FondosOriginales.Ejecutar(args[0], Path.GetFullPath(entrega), Path.GetFullPath(salida)); return args[0].EndsWith("-caja", StringComparison.Ordinal) ? IconosCaja.Ejecutar(args[0], Path.GetFullPath(entrega), Path.GetFullPath(salida)) : Juegos.Ejecutar(args[0], Path.GetFullPath(entrega), Path.GetFullPath(salida)); }
        catch (Exception e) { Console.Error.WriteLine("ERROR: " + e.Message); return 1; }
    }

    private sealed record Recurso(string Nombre, string Familia, string Ruta, byte[] Png, int Ancho, int Alto, string Sha);

    /// <summary>Los recursos de PokeSprite mas, si se da, los fondos de caja de PKHeX.Drawing.Misc (solo <c>img/box/**</c>, familia «fondos»; F4).</summary>
    private static List<Recurso> LeerTodos(string resx, string? misc)
    {
        var lista = Leer(resx, null);
        if (misc is not null) lista.AddRange(Leer(misc, "/box/"));
        if (lista.Select(r => r.Nombre).Distinct(StringComparer.Ordinal).Count() != lista.Count) throw new InvalidDataException("Nombres de recurso repetidos entre los .resx.");
        return lista;
    }

    private static List<Recurso> Leer(string resx, string? soloRutasCon)
    {
        var raiz = Path.GetDirectoryName(Path.GetFullPath(resx))!;
        var doc = XDocument.Load(resx);
        var lista = new List<Recurso>();
        foreach (var d in doc.Root!.Elements("data"))
        {
            var tipo = (string?)d.Attribute("type");
            if (tipo is null || !tipo.StartsWith("System.Resources.ResXFileRef", StringComparison.Ordinal)) continue;
            var nombre = (string)d.Attribute("name")!;
            var valor = d.Element("value")!.Value;
            var ruta = valor[..valor.IndexOf(';')].Replace('\\', '/');
            if (soloRutasCon is not null && !ruta.Contains(soloRutasCon, StringComparison.Ordinal)) continue;
            var completa = Path.GetFullPath(Path.Combine(raiz, ruta));
            var png = File.ReadAllBytes(completa);
            var img = PngLite.Decodificar(png); // falla si el PNG no se entiende: ningun recurso se empaqueta sin poder leerse
            var carpeta = Path.GetFileName(Path.GetDirectoryName(completa)!);
            var familia = soloRutasCon is not null ? "fondos" : carpeta == "img" ? "misc" : Familias.FirstOrDefault(f => f.Carpeta == carpeta).Familia
                ?? throw new InvalidDataException($"Carpeta de recursos sin familia: {carpeta} ({nombre})");
            var rel = ruta[(ruta.IndexOf("img/", StringComparison.Ordinal) + 4)..];
            lista.Add(new Recurso(nombre, familia, rel, png, img.Width, img.Height, Manifiesto.Sha256Hex(png)));
        }
        if (lista.Select(r => r.Nombre).Distinct(StringComparer.Ordinal).Count() != lista.Count) throw new InvalidDataException("Nombres de recurso repetidos en el .resx.");
        return lista;
    }

    private static int Generar(string resx, string? misc, string salida, string origen)
    {
        var recursos = LeerTodos(resx, misc);
        Directory.CreateDirectory(salida);
        var paquetes = new List<PaqueteInfo>();
        var entradas = new Dictionary<string, EntradaRecurso>(StringComparer.Ordinal);
        var tsv = new StringBuilder("recurso\tpaquete\tficheroOriginal\tbytes\tancho\talto\tsha256\n");
        foreach (var (_, familia) in Familias)
        {
            var delPaquete = recursos.Where(r => r.Familia == familia).OrderBy(r => r.Nombre, StringComparer.Ordinal).ToList();
            if (delPaquete.Count == 0) continue;
            var idx = paquetes.Count;
            using var ms = new MemoryStream();
            foreach (var r in delPaquete)
            {
                entradas[r.Nombre] = new EntradaRecurso(idx, ms.Position, r.Png.Length, r.Ancho, r.Alto);
                ms.Write(r.Png);
                tsv.Append(r.Nombre).Append('\t').Append(familia).Append('\t').Append(r.Ruta).Append('\t').Append(r.Png.Length).Append('\t').Append(r.Ancho).Append('\t').Append(r.Alto).Append('\t').Append(r.Sha).Append('\n');
            }
            var bytes = ms.ToArray();
            var archivo = familia + ".pkpack";
            File.WriteAllBytes(Path.Combine(salida, archivo), bytes);
            paquetes.Add(new PaqueteInfo(familia, archivo, bytes.Length, Manifiesto.Sha256Hex(bytes), delPaquete.Count));
            Console.WriteLine($"{archivo,-22} {delPaquete.Count,5} recursos {bytes.Length,10:N0} bytes");
        }
        var manifiesto = new Manifiesto { Origen = origen, Paquetes = paquetes, Recursos = entradas };
        File.WriteAllBytes(Path.Combine(salida, "manifest.json"), manifiesto.Escribir());
        File.WriteAllText(Path.Combine(salida, "procedencia.tsv"), tsv.ToString(), new UTF8Encoding(false));
        Console.WriteLine($"manifest.json: {entradas.Count} recursos en {paquetes.Count} paquetes, {paquetes.Sum(p => p.Bytes):N0} bytes en total");
        return 0;
    }

    private static int Verificar(string resx, string? misc, string salida)
    {
        var recursos = LeerTodos(resx, misc);
        var m = Manifiesto.Leer(File.ReadAllBytes(Path.Combine(salida, "manifest.json")));
        var fallos = 0;
        var cargados = new byte[m.Paquetes.Count][];
        for (var i = 0; i < m.Paquetes.Count; i++)
        {
            cargados[i] = File.ReadAllBytes(Path.Combine(salida, m.Paquetes[i].Archivo));
            if (Manifiesto.Sha256Hex(cargados[i]) != m.Paquetes[i].Sha256) { Console.Error.WriteLine($"Paquete {m.Paquetes[i].Id}: SHA-256 distinto"); fallos++; }
        }
        if (m.Recursos.Count != recursos.Count) { Console.Error.WriteLine($"Recursos: manifiesto {m.Recursos.Count}, resx {recursos.Count}"); fallos++; }
        foreach (var r in recursos)
        {
            if (!m.Recursos.TryGetValue(r.Nombre, out var e)) { Console.Error.WriteLine($"Falta {r.Nombre}"); fallos++; continue; }
            if (!cargados[e.Paquete].AsSpan((int)e.Desplazamiento, e.Longitud).SequenceEqual(r.Png)) { Console.Error.WriteLine($"Bytes distintos en {r.Nombre}"); fallos++; }
        }
        Console.WriteLine(fallos == 0 ? $"OK: {recursos.Count} recursos identicos a los PNG de PKHeX" : $"FALLOS: {fallos}");
        return fallos == 0 ? 0 : 1;
    }
}
