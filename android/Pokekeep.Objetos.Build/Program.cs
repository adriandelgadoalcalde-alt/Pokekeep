// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Pokekeep.Objetos.Build;

// Generador del paquete de objetos adicionales (X14).
//   dotnet Pokekeep.Objetos.Build.dll generar   --cache <dir> --sprites <dir> --salida <dir> [--procedencia <tsv>] [--sin-resolver <tsv>] [--sin-descargas]
//   dotnet Pokekeep.Objetos.Build.dll verificar --salida <dir> --procedencia <tsv>
//   dotnet Pokekeep.Objetos.Build.dll mapa-antiguos --salida <ObjetosAntiguosTabla.cs> [--sin-resolver <tsv>]
//   dotnet Pokekeep.Objetos.Build.dll matriz --sprites <dir> --objetos <dir> [--saves <dir>] [--sin-resolver <tsv>] --procedencia <tsv> --claves-x14 <txt> --detalle <tsv> --resumen <tsv>
//   dotnet Pokekeep.Objetos.Build.dll cobertura --sprites <dir> --objetos <dir> [--saves <dir>] [--sin-resolver <tsv>] --detalle <tsv> --resumen <tsv>
return await Programa.Ejecutar(args);

internal static class Programa
{
    public static async Task<int> Ejecutar(string[] args)
    {
        if (args.Length == 0 || args[0] is not ("generar" or "verificar" or "cobertura" or "mapa-antiguos" or "matriz")) { Console.Error.WriteLine("Uso: generar|verificar|cobertura ..."); return 2; }
        string? cache = null, salida = null, proc = null, sin = null, sprites = null, objetos = null, saves = null, detalle = null, resumen = null, claves14 = null; var sinDescargas = false;
        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--sin-descargas": sinDescargas = true; break;
                case "--sin-partidas": Universo.OmitirSinPartida = true; break;
                case "--cache" when i + 1 < args.Length: cache = args[++i]; break;
                case "--salida" when i + 1 < args.Length: salida = args[++i]; break;
                case "--procedencia" when i + 1 < args.Length: proc = args[++i]; break;
                case "--sin-resolver" when i + 1 < args.Length: sin = args[++i]; break;
                case "--sprites" when i + 1 < args.Length: sprites = args[++i]; break;
                case "--objetos" when i + 1 < args.Length: objetos = args[++i]; break;
                case "--saves" when i + 1 < args.Length: saves = args[++i]; break;
                case "--detalle" when i + 1 < args.Length: detalle = args[++i]; break;
                case "--resumen" when i + 1 < args.Length: resumen = args[++i]; break;
                case "--claves-x14" when i + 1 < args.Length: claves14 = args[++i]; break;
                default: Console.Error.WriteLine("Opción desconocida " + args[i]); return 2;
            }
        }
        try
        {
            switch (args[0])
            {
                case "generar":
                {
                    if (cache is null || salida is null || sprites is null) { Console.Error.WriteLine("Faltan --cache, --sprites y --salida."); return 2; }
                    var r = await Generador.Generar(cache, sprites, sinDescargas, saves);
                    Generador.Escribir(r, salida, proc, sin);
                    Console.WriteLine($"objetos.pkpack: {r.Paquete.Length} bytes, {r.Claves} claves, {r.Archivos} imágenes distintas, {r.SinResolver.Count} objetos sin resolver");
                    return 0;
                }
                case "mapa-antiguos":
                {
                    if (salida is null) { Console.Error.WriteLine("Falta --salida (el .cs de la tabla)."); return 2; }
                    var (eq, sinIgualar) = MapaAntiguos.Calcular(PKHeX.Core.GameInfo.GetStrings("en"));
                    File.WriteAllText(salida, MapaAntiguos.GenerarFuente(eq), new System.Text.UTF8Encoding(false));
                    if (sin is not null) File.WriteAllText(sin, "familia\tid_nativo\tnombre\tmotivo\n" + string.Concat(sinIgualar.Select(l => l + "\n")), new System.Text.UTF8Encoding(false));
                    Console.WriteLine($"mapa-antiguos: {eq.Count} equivalencias, {sinIgualar.Count} sin igualar");
                    return 0;
                }
                case "matriz":
                {
                    if (sprites is null || objetos is null || proc is null || claves14 is null || detalle is null || resumen is null) { Console.Error.WriteLine("Faltan --sprites, --objetos, --procedencia, --claves-x14, --detalle y --resumen."); return 2; }
                    var m = Matriz.Calcular(sprites, objetos, saves, proc, sin, claves14);
                    var sinBomM = new System.Text.UTF8Encoding(false);
                    File.WriteAllText(detalle, m.Detalle, sinBomM);
                    File.WriteAllText(resumen, m.Resumen, sinBomM);
                    Console.WriteLine($"matriz: {m.Filas.Count} filas; " + string.Join(", ", Matriz.Todas.Select(c => $"{c}={m.Filas.Count(f => f.Categoria == c)}")));
                    return 0;
                }
                case "cobertura":
                {
                    if (sprites is null || detalle is null || resumen is null) { Console.Error.WriteLine("Faltan --sprites, --detalle y --resumen."); return 2; }
                    var filas = Cobertura.Calcular(sprites, objetos, saves, sin);
                    var sinBom = new System.Text.UTF8Encoding(false);
                    File.WriteAllText(detalle, Cobertura.Detalle(filas), sinBom);
                    File.WriteAllText(resumen, Cobertura.Resumen(filas), sinBom);
                    Console.WriteLine($"cobertura: {filas.Count} objetos en {filas.Select(f => f.Objeto.Juego).Distinct().Count()} juegos");
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
