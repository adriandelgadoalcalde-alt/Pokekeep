// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Text;
using System.Text.Json;
using Pokekeep.Sprites;

/// <summary>X66: adapta la entrega R1 al generador único de X53. Solo hashes y nombres lógicos en la procedencia.</summary>
internal static class LgpeCaja
{
    public static int Ejecutar(string modo, string entrega, string salida)
    {
        var temporal = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(salida))!, ".lgpe-caja-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(temporal, "iconos"));
        try
        {
            Preparar(entrega, temporal);
            var caja = IconosCaja.Construir(temporal, [SpritesDeJuego.Lgpe], 0, forzarPng: true);
            var salidas = new SortedDictionary<string, byte[]>(caja.Paquetes, StringComparer.Ordinal);
            salidas["manifest-lgpe-caja.json"] = new Manifiesto
            {
                Origen = "Let's Go Pikachu/Eevee: iconos nativos 68x56 de la entrega privada R1 (X66). Sin iconos variocolor propios.",
                Recortados = caja.Recortados, Paquetes = caja.Info, Recursos = caja.Entradas,
            }.Escribir();
            salidas["procedencia-lgpe-caja.tsv"] = new UTF8Encoding(false).GetBytes(caja.Tsv);
            // X58 agrupaba caja y vista en un manifiesto. Conservamos todas las
            // entradas de vista y sus bytes; retiramos únicamente las de caja.
            var anterior = Path.Combine(salida, "manifest-lgpe.json");
            if (File.Exists(anterior))
            {
                var m = Manifiesto.Leer(File.ReadAllBytes(anterior));
                var paquete = m.Paquetes.Single(p => p.Id == "vista-lgpe");
                var bytes = File.ReadAllBytes(Path.Combine(salida, paquete.Archivo));
                if (bytes.LongLength != paquete.Bytes || Manifiesto.Sha256Hex(bytes) != paquete.Sha256) throw new InvalidDataException("La vista previa existente no coincide con su huella.");
                salidas["manifest-lgpe.json"] = new Manifiesto
                {
                    Origen = Lgpe.OrigenVista,
                    Paquetes = [paquete], Recursos = m.Recursos.Where(x => x.Key.StartsWith("vista_lgpe_", StringComparison.Ordinal)).ToDictionary(x => x.Key, x => x.Value with { Paquete = 0 }, StringComparer.Ordinal),
                }.Escribir();
                var procedencia = Path.Combine(salida, "procedencia-lgpe.tsv");
                if (File.Exists(procedencia))
                {
                    var lineas = File.ReadAllLines(procedencia);
                    salidas["procedencia-lgpe.tsv"] = new UTF8Encoding(false).GetBytes("recurso\tpaquete\tficheroEntrega\tancho\talto\tsha256Webp\n" + string.Join('\n', lineas.Skip(1).Where(l => l.StartsWith("vista_lgpe_", StringComparison.Ordinal))) + "\n");
                }
            }
            return IconosCaja.Escribir(modo == "generar-lgpe-caja", salida, salidas, $"{caja.Entradas.Count} iconos nativos LGPE; vista previa intacta");
        }
        finally { Directory.Delete(temporal, recursive: true); }
    }

    internal static void Preparar(string entrega, string destino)
    {
        var catalogo = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "lgpe-caja-r1.tsv"));
        var indiceSha = catalogo[0].Split('\t')[1];
        if (!string.Equals(Manifiesto.Sha256Hex(File.ReadAllBytes(Path.Combine(entrega, "indice.tsv"))), indiceSha, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("SHA-256 distinto del índice R1 fijado.");
        var iconos = new List<object>(); var sumas = new List<string>();
        foreach (var linea in catalogo.Skip(2).Where(l => l.Length > 0))
        {
            var p = linea.Split('\t');
            var archivo = p[0];
            if (Path.GetFileName(archivo) != archivo) throw new InvalidDataException("Ruta no admitida en el catálogo de iconos.");
            var bytes = File.ReadAllBytes(Path.Combine(entrega, "iconos", archivo));
            if (!string.Equals(Manifiesto.Sha256Hex(bytes), p[5], StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("SHA-256 distinto del icono R1: " + archivo);
            if (p[4] != "pokemon") continue; // ocho marcadores y un huevo no almacenable en LGPE
            var img = Juegos.Decodificar(bytes);
            if (img.Width != 68 || img.Height != 56) throw new InvalidDataException("El icono nativo debe medir 68x56: " + archivo);
            for (var i = 3; i < img.Pixels.Length; i += 4)
                if (img.Pixels[i] is not (0 or 255)) throw new InvalidDataException("Alfa intermedio en un icono de 1 bit: " + archivo);
            var relativo = "iconos/" + archivo;
            Directory.CreateDirectory(Path.Combine(destino, "iconos"));
            File.WriteAllBytes(Path.Combine(destino, relativo), bytes);
            sumas.Add(p[5] + "  " + relativo);
            iconos.Add(new { juego = "lgpe", especie = int.Parse(p[1]), forma = int.Parse(p[2]), genero = p[3] is "s" or "n" ? "" : p[3], variocolor = false, huevo = false, forma_nativa = int.Parse(p[2]), archivo = relativo, ancho = 68, alto = 56, pixeles_sha256 = Manifiesto.Sha256Hex(img.Pixels) });
        }
        if (iconos.Count != 212) throw new InvalidDataException("Se esperaban 212 iconos de Pokémon nativos de R1.");
        var indice = JsonSerializer.SerializeToUtf8Bytes(new { iconos });
        File.WriteAllBytes(Path.Combine(destino, "indice.json"), indice);
        sumas.Add(Manifiesto.Sha256Hex(indice) + "  indice.json");
        File.WriteAllLines(Path.Combine(destino, "SHA256SUMS"), sumas, new UTF8Encoding(false));
    }
}
