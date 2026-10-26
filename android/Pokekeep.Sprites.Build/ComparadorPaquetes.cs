// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Security.Cryptography;
using System.Text;
using Pokekeep.Sprites;

/// <summary>
/// Release (compactacion del APK sin perdida): compara dos juegos de paquetes ANTES/DESPUES de recomprimirlos, recurso por recurso, con los MISMOS decodificadores que usa la app
/// (<see cref="WebpLite"/> y <see cref="PngLite"/>, no los de Pillow con los que se recomprimio): mismo ancho, mismo alto y los mismos bytes RGBA (alfa incluido, tambien el RGB de los pixeles
/// invisibles). Comprueba ademas que cada paquete nuevo cuadra con su manifiesto (longitud y SHA-256) y que los recursos que antes compartian bytes (alias) los siguen compartiendo.
/// Escribe la huella del contenido por paquete (SHA-256 de las huellas RGBA de sus recursos por orden de nombre; los recursos con los mismos bytes cuentan por la huella de sus bytes): es la misma antes y despues si y solo si los pixeles son identicos.
/// Uso: <c>comparar-paquetes --antes &lt;datos\android&gt; --despues &lt;carpeta con los ficheros recomprimidos&gt; [--tsv &lt;salida&gt;]</c>. Los paquetes que no estan en --despues no se comparan.
/// </summary>
internal static class ComparadorPaquetes
{
    public static int Ejecutar(string[] args)
    {
        string? antes = null, despues = null, tsv = null;
        for (var i = 1; i + 1 < args.Length; i += 2)
            switch (args[i]) { case "--antes": antes = args[i + 1]; break; case "--despues": despues = args[i + 1]; break; case "--tsv": tsv = args[i + 1]; break; default: Console.Error.WriteLine($"Opcion desconocida {args[i]}"); return 2; }
        if (antes is null || despues is null) { Console.Error.WriteLine("Faltan --antes y --despues."); return 2; }
        antes = Path.GetFullPath(antes); despues = Path.GetFullPath(despues);
        var fallos = 0; long recursos = 0, bytesAntes = 0, bytesDespues = 0;
        var filas = new StringBuilder("paquete\trecursos\tbytes_antes\tbytes_despues\thuella_contenido_antes\thuella_contenido_despues\tigual\n");

        foreach (var mfDespues in Directory.EnumerateFiles(despues, "manifest*.json", SearchOption.AllDirectories).Where(f => !f.Contains($"{Path.DirectorySeparatorChar}logos-juegos{Path.DirectorySeparatorChar}", StringComparison.Ordinal)).OrderBy(f => f, StringComparer.Ordinal))
        {
            var rel = Path.GetRelativePath(despues, mfDespues);
            var mfAntes = Path.Combine(antes, rel);
            var mA = Manifiesto.Leer(File.ReadAllBytes(mfAntes));
            var mD = Manifiesto.Leer(File.ReadAllBytes(mfDespues));
            var dirA = Path.GetDirectoryName(mfAntes)!; var dirD = Path.GetDirectoryName(mfDespues)!;
            if (mA.Recursos.Count != mD.Recursos.Count || !mA.Recursos.Keys.ToHashSet().SetEquals(mD.Recursos.Keys)) { Console.Error.WriteLine($"{rel}: los nombres de recurso no son los mismos."); fallos++; continue; }
            if (mA.Paquetes.Count != mD.Paquetes.Count) { Console.Error.WriteLine($"{rel}: distinto numero de paquetes."); fallos++; continue; }
            var bytesA = new byte[mA.Paquetes.Count][]; var bytesD = new byte[mD.Paquetes.Count][];
            for (var i = 0; i < mA.Paquetes.Count; i++)
            {
                if (mA.Paquetes[i].Id != mD.Paquetes[i].Id) { Console.Error.WriteLine($"{rel}: el paquete {i} cambia de id."); fallos++; }
                bytesA[i] = File.ReadAllBytes(Path.Combine(dirA, mA.Paquetes[i].Archivo));
                bytesD[i] = File.ReadAllBytes(Path.Combine(dirD, mD.Paquetes[i].Archivo));
                if (bytesD[i].Length != mD.Paquetes[i].Bytes || Manifiesto.Sha256Hex(bytesD[i]) != mD.Paquetes[i].Sha256) { Console.Error.WriteLine($"{rel}: {mD.Paquetes[i].Id} no cuadra con su manifiesto (longitud o SHA-256)."); fallos++; }
                if (Manifiesto.Sha256Hex(bytesA[i]) != mA.Paquetes[i].Sha256) { Console.Error.WriteLine($"{rel}: {mA.Paquetes[i].Id} (antes) no cuadra con su manifiesto."); fallos++; }
            }
            // Un paquete por vez: decodifica sus recursos en paralelo (hasta 3 hilos) y acumula la huella por orden de nombre.
            for (var pi = 0; pi < mA.Paquetes.Count; pi++)
            {
                var nombres = mA.Recursos.Where(k => k.Value.Paquete == pi).Select(k => k.Key).OrderBy(n => n, StringComparer.Ordinal).ToArray();
                var huellasA = new string[nombres.Length]; var huellasD = new string[nombres.Length];
                var malos = 0;
                Parallel.For(0, nombres.Length, new ParallelOptions { MaxDegreeOfParallelism = 3 }, k =>
                {
                    var ea = mA.Recursos[nombres[k]]; var ed = mD.Recursos[nombres[k]];
                    var crudoA = bytesA[ea.Paquete].AsSpan((int)ea.Desplazamiento, ea.Longitud);
                    var crudoD = bytesD[ed.Paquete].AsSpan((int)ed.Desplazamiento, ed.Longitud);
                    if (crudoA.SequenceEqual(crudoD)) { huellasA[k] = huellasD[k] = Hex(SHA256.HashData(crudoA)); return; }   // sin tocar (o no es una imagen, p. ej. el audio de los gritos): mismos bytes, nada que decodificar
                    var ia = Decodificar(crudoA);
                    var id = Decodificar(crudoD);
                    huellasA[k] = Huella(ia); huellasD[k] = Huella(id);
                    if (ia.Width != id.Width || ia.Height != id.Height || ia.Width != ea.Ancho || id.Width != ed.Ancho || id.Height != ed.Alto || !ia.Pixels.AsSpan().SequenceEqual(id.Pixels))
                    { Console.Error.WriteLine($"{rel}: {nombres[k]} NO tiene los mismos pixeles."); Interlocked.Increment(ref malos); }
                });
                // Alias: los recursos que antes apuntaban a los mismos bytes siguen haciendolo.
                foreach (var g in nombres.GroupBy(n => (mA.Recursos[n].Paquete, mA.Recursos[n].Desplazamiento)))
                    if (g.Select(n => (mD.Recursos[n].Paquete, mD.Recursos[n].Desplazamiento)).Distinct().Count() != 1) { Console.Error.WriteLine($"{rel}: {g.First()} y sus alias ya no comparten bytes."); malos++; }
                fallos += malos; recursos += nombres.Length;
                bytesAntes += bytesA[pi].Length; bytesDespues += bytesD[pi].Length;
                var hA = Hex(SHA256.HashData(Encoding.ASCII.GetBytes(string.Join('\n', nombres.Zip(huellasA, (n, h) => n + "=" + h)))));
                var hD = Hex(SHA256.HashData(Encoding.ASCII.GetBytes(string.Join('\n', nombres.Zip(huellasD, (n, h) => n + "=" + h)))));
                filas.Append(mA.Paquetes[pi].Id).Append('\t').Append(nombres.Length).Append('\t').Append(bytesA[pi].Length).Append('\t').Append(bytesD[pi].Length).Append('\t').Append(hA).Append('\t').Append(hD).Append('\t').Append(hA == hD && malos == 0 ? "SI" : "NO").Append('\n');
                Console.WriteLine($"{mA.Paquetes[pi].Id,-20} {nombres.Length,6} recursos  {bytesA[pi].Length,12:N0} -> {bytesD[pi].Length,12:N0} B  {(hA == hD && malos == 0 ? "pixeles identicos" : "DIFERENCIAS")}");
            }
        }

        // Logos sueltos (PNG): mismos pixeles que los de --antes, y su manifest.json (bytes y SHA-256) al dia.
        var logosD = Path.Combine(despues, "logos-juegos");
        if (Directory.Exists(logosD))
        {
            var logosA = Path.Combine(antes, "logos-juegos");
            var mf = File.ReadAllText(Path.Combine(logosD, "manifest.json"), Encoding.UTF8);
            long la = 0, ld = 0; var n = 0;
            foreach (var f in Directory.EnumerateFiles(logosD, "*.png", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
            {
                var relLogo = Path.GetRelativePath(logosD, f).Replace('\\', '/');
                var a = File.ReadAllBytes(Path.Combine(logosA, relLogo)); var d = File.ReadAllBytes(f);
                var ia = PngLite.Decodificar(a); var id = PngLite.Decodificar(d);
                if (ia.Width != id.Width || ia.Height != id.Height || !ia.Pixels.AsSpan().SequenceEqual(id.Pixels)) { Console.Error.WriteLine($"logos-juegos/{relLogo}: pixeles distintos."); fallos++; }
                if (!mf.Contains(Manifiesto.Sha256Hex(d), StringComparison.Ordinal) || mf.Contains(Manifiesto.Sha256Hex(a), StringComparison.Ordinal)) { Console.Error.WriteLine($"logos-juegos/{relLogo}: el manifest.json no lleva el SHA-256 del fichero nuevo."); fallos++; }
                la += a.Length; ld += d.Length; n++; recursos++;
            }
            bytesAntes += la; bytesDespues += ld;
            Console.WriteLine($"{"logos-juegos(png)",-20} {n,6} ficheros   {la,12:N0} -> {ld,12:N0} B  {(fallos == 0 ? "pixeles identicos" : "DIFERENCIAS")}");
            filas.Append("logos-juegos(png)\t").Append(n).Append('\t').Append(la).Append('\t').Append(ld).Append("\t-\t-\t").Append(fallos == 0 ? "SI" : "NO").Append('\n');
        }

        if (tsv is not null) File.WriteAllText(tsv, filas.ToString(), new UTF8Encoding(false));
        Console.WriteLine(fallos == 0 ? $"OK: {recursos:N0} recursos con los mismos pixeles RGBA (alfa incluido); {bytesAntes:N0} -> {bytesDespues:N0} B ({bytesAntes - bytesDespues:N0} menos)" : $"FALLOS: {fallos}");
        return fallos == 0 ? 0 : 1;
    }

    private static SpriteImage Decodificar(ReadOnlySpan<byte> crudo) => WebpLite.EsWebp(crudo) ? WebpLite.Decodificar(crudo) : PngLite.Decodificar(crudo);
    private static string Huella(SpriteImage i) => Hex(SHA256.HashData(i.Pixels)) + $"{i.Width}x{i.Height}";
    private static string Hex(byte[] b) => Convert.ToHexString(b).ToLowerInvariant();
}
