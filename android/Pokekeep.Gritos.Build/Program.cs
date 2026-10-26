// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Pokekeep.Gritos.Build;

// Generador del paquete de gritos de serie (X11).
//   dotnet Pokekeep.Gritos.Build.dll generar  --cache <dir> --salida <dir> [--procedencia <tsv>] [--sin-grito <tsv>] [--sin-descargas]
//   dotnet Pokekeep.Gritos.Build.dll verificar --salida <dir> --procedencia <tsv>
//   dotnet Pokekeep.Gritos.Build.dll formas   --cache <dir>      (informe de la traducción PKHeX -> PokeAPI, para revisar formas-manuales.tsv)
return await Programa.Ejecutar(args);

internal static class Programa
{
    public static async Task<int> Ejecutar(string[] args)
    {
        if (args.Length == 0 || args[0] is not ("generar" or "verificar" or "formas" or "pikachu-ultrasol")) { Console.Error.WriteLine("Uso: generar|verificar|formas --cache <dir> --salida <dir> [--procedencia <tsv>] [--sin-grito <tsv>] [--sin-descargas]"); return 2; }
        string? cache = null, salida = null, proc = null, sin = null, wav = null, origen = null; var sinDescargas = false; var sinPikachu = false;
        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--sin-descargas": sinDescargas = true; break;
                case "--sin-pikachu-ultrasol": sinPikachu = true; break;   // compilacion desde el codigo publico: el grito de Pikachu es el de PokeAPI como el de las demas especies
                case "--pikachu-ultrasol" when i + 1 < args.Length: wav = args[++i]; break;
                case "--origen" when i + 1 < args.Length: origen = args[++i]; break;
                case "--cache" when i + 1 < args.Length: cache = args[++i]; break;
                case "--salida" when i + 1 < args.Length: salida = args[++i]; break;
                case "--procedencia" when i + 1 < args.Length: proc = args[++i]; break;
                case "--sin-grito" when i + 1 < args.Length: sin = args[++i]; break;
                default: Console.Error.WriteLine("Opción desconocida " + args[i]); return 2;
            }
        }
        try
        {
            switch (args[0])
            {
                case "pikachu-ultrasol":
                {
                    if (origen is null || salida is null || proc is null || wav is null) { Console.Error.WriteLine("Faltan --origen, --salida, --procedencia y --pikachu-ultrasol."); return 2; }
                    if (Path.GetFullPath(origen) == Path.GetFullPath(salida)) throw new InvalidDataException("La entrega debe estar separada del paquete instalado.");
                    var r = PikachuUltrasol.Sustituir(File.ReadAllBytes(Path.Combine(origen, Generador.ArchivoPaquete)), File.ReadAllBytes(Path.Combine(origen, "manifest.json")), File.ReadAllText(proc), File.ReadAllBytes(wav));
                    Directory.CreateDirectory(salida);
                    File.WriteAllBytes(Path.Combine(salida, Generador.ArchivoPaquete), r.Paquete);
                    File.WriteAllBytes(Path.Combine(salida, "manifest.json"), r.Manifiesto);
                    File.WriteAllText(Path.Combine(salida, "procedencia.tsv"), r.Procedencia, new System.Text.UTF8Encoding(false));
                    return Verificador.Verificar(salida, Path.Combine(salida, "procedencia.tsv"));
                }
                case "formas":
                {
                    if (cache is null) { Console.Error.WriteLine("Falta --cache."); return 2; }
                    var r = await Generador.Generar(cache, sinDescargas);
                    foreach (var a in r.Formas.Where(a => a.Pokemon is null)) Console.WriteLine($"PKHEX\t{a.Especie}\t{a.Forma}\t{a.NombrePkhx}\t{a.Motivo}");
                    foreach (var a in r.Formas.Where(a => a.Pokemon is not null && a.Motivo == "nombre")) Console.WriteLine($"NOMBRE	{a.Especie}	{a.Forma}	{a.NombrePkhx}	{a.Pokemon!.Identificador}");
                    foreach (var p in r.PokeApiSinForma) Console.WriteLine($"POKEAPI\t{p.Especie}\t{p.Id}\t{p.Identificador}");
                    return 0;
                }
                case "generar":
                {
                    if (cache is null || salida is null) { Console.Error.WriteLine("Faltan --cache y --salida."); return 2; }
                    var r = await Generador.Generar(cache, sinDescargas);
                    if (wav is null && !sinPikachu) throw new InvalidDataException("C15: generar requiere --pikachu-ultrasol (WAV privado del propietario) o --sin-pikachu-ultrasol.");
                    if (wav is not null)
                    {
                        var nuevo = PikachuUltrasol.Sustituir(r.Paquete, r.Manifiesto, r.Procedencia, File.ReadAllBytes(wav));
                        r = r with { Paquete = nuevo.Paquete, Manifiesto = nuevo.Manifiesto, Procedencia = nuevo.Procedencia };
                    }
                    Generador.Escribir(r, salida, proc, sin);
                    Console.WriteLine($"gritos.pkpack: {r.Paquete.Length} bytes, {r.Claves} claves, {r.Archivos} sonidos distintos");
                    return 0;
                }
                default:
                {
                    if (salida is null || proc is null) { Console.Error.WriteLine("Faltan --salida y --procedencia."); return 2; }
                    return Verificador.Verificar(salida, proc);
                }
            }
        }
        catch (Exception e) when (e is IOException or InvalidDataException or HttpRequestException) { Console.Error.WriteLine("ERROR: " + e.Message); return 1; }
    }
}
