// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Pokekeep.Sprites;

namespace Pokekeep.Gritos.Build;

/// <summary>Vuelve a leer la salida y comprueba el SHA-256 del paquete, que cada recurso es un Ogg, MP3 o WAV y que su SHA-256 es el de la procedencia.</summary>
public static class Verificador
{
    public static int Verificar(string salida, string tsvProcedencia)
    {
        var errores = Comprobar(salida, tsvProcedencia);
        foreach (var e in errores) Console.Error.WriteLine("FALLO: " + e);
        Console.WriteLine(errores.Count == 0 ? "gritos verificados" : $"{errores.Count} fallos");
        return errores.Count == 0 ? 0 : 1;
    }

    public static List<string> Comprobar(string salida, string tsvProcedencia)
    {
        var errores = new List<string>();
        var m = Manifiesto.Leer(File.ReadAllBytes(Path.Combine(salida, "manifest.json")));
        var info = m.Paquetes.Single();
        var bytes = File.ReadAllBytes(Path.Combine(salida, info.Archivo));
        if (bytes.LongLength != info.Bytes) errores.Add($"tamaño del paquete {bytes.LongLength} != manifiesto {info.Bytes}");
        if (!string.Equals(Manifiesto.Sha256Hex(bytes), info.Sha256, StringComparison.OrdinalIgnoreCase)) errores.Add("SHA-256 del paquete distinto del manifiesto");
        var filas = File.ReadAllLines(tsvProcedencia).Skip(1).Select(l => l.Split('\t')).ToDictionary(v => v[0], StringComparer.Ordinal);
        if (filas.Count != m.Recursos.Count) errores.Add($"procedencia {filas.Count} filas, manifiesto {m.Recursos.Count} recursos");
        foreach (var (clave, e) in m.Recursos)
        {
            if (e.Desplazamiento < 0 || e.Desplazamiento + e.Longitud > bytes.Length) { errores.Add(clave + ": fuera del paquete"); continue; }
            var s = bytes.AsSpan(checked((int)e.Desplazamiento), e.Longitud);
            if (Generador.Formato(s) is null) errores.Add(clave + ": no es Ogg, MP3 ni WAV");
            if (!filas.TryGetValue(clave, out var f)) errores.Add(clave + ": sin fila de procedencia");
            else if (!string.Equals(Fuente.Sha(s), f[8], StringComparison.OrdinalIgnoreCase)) errores.Add(clave + ": SHA-256 distinto de la procedencia");
        }
        return errores;
    }
}
