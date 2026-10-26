// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AutoLegalidad.Registro;

/// <summary>
/// Cerrojo del fichero de registro entre quienes escriben en la misma carpeta. Escritorio: <see cref="MutexRegistroLock"/> (un <c>Mutex</c> con nombre, porque escriben a la vez varios procesos:
/// la interfaz y los Workers). Android F1 (ADR 0054): <see cref="ProcesoRegistroLock"/> (cerrojo en proceso) con un fichero de registro por proceso (<see cref="RegistroRutas.SufijoFichero"/>),
/// porque la interfaz y el servicio <c>:motor</c> son procesos distintos y un <c>Mutex</c> con nombre no atraviesa procesos en Android.
/// </summary>
public interface IRegistroLock : IDisposable
{
    /// <summary>Intenta adquirirlo durante <paramref name="espera"/>. Si devuelve <c>true</c>, el llamante DEBE llamar a <see cref="Liberar"/>.</summary>
    bool Adquirir(TimeSpan espera);

    void Liberar();
}

/// <summary>Cerrojo entre procesos de Windows: un <see cref="Mutex"/> con el nombre dado. Un propietario muerto (<see cref="AbandonedMutexException"/>) cuenta como adquirido: el fichero sigue siendo válido.</summary>
public sealed class MutexRegistroLock(string nombre) : IRegistroLock
{
    private readonly Mutex _mutex = new(false, nombre);

    public bool Adquirir(TimeSpan espera)
    {
        try { return _mutex.WaitOne(espera); }
        catch (AbandonedMutexException) { return true; }
    }

    public void Liberar() => _mutex.ReleaseMutex();
    public void Dispose() => _mutex.Dispose();
}

/// <summary>Cerrojo dentro de UN proceso (varios hilos escribiendo el mismo fichero). Para Android, junto a un fichero de registro por proceso.</summary>
public sealed class ProcesoRegistroLock : IRegistroLock
{
    private readonly SemaphoreSlim _semaforo = new(1, 1);
    public bool Adquirir(TimeSpan espera) => _semaforo.Wait(espera);
    public void Liberar() => _semaforo.Release();
    public void Dispose() => _semaforo.Dispose();
}

/// <summary>
/// De dónde escribe el registro y con qué datos de cabecera: carpeta preferida y alternativa, versión de Pokekeep, hash del Core, fábrica del cerrojo y sufijo del fichero. El registro NO adivina nada
/// del entorno por su cuenta: quien lo configura (el escritorio con <see cref="Escritorio"/>, Android con sus rutas privadas) se lo da. Si nadie lo configura, se usa <see cref="Escritorio"/>.
/// </summary>
public sealed record RegistroRutas(
    string CarpetaPreferida,
    string? CarpetaAlternativa,
    string Version,
    Func<string?> HashCore,
    Func<string, IRegistroLock>? FabricaLock = null,
    string SufijoFichero = "")
{
    /// <summary>Cerrojo por carpeta del escritorio: un <see cref="Mutex"/> con nombre derivado de la ruta.</summary>
    public static IRegistroLock CerrojoDeEscritorio(string carpeta)
        => new MutexRegistroLock("Pokekeep.Registro." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(carpeta).ToLowerInvariant())))[..16]);

    /// <summary>
    /// Las rutas de siempre en Windows: <c>registros\</c> junto al ejecutable (<paramref name="baseDirectory"/>; por defecto la carpeta del programa) y, si ahí no se puede escribir,
    /// <c>%LOCALAPPDATA%\Pokekeep\registros</c>; la versión, del <c>Pokekeep\manifiesto.json</c> del paquete; el hash del <c>PKHeX.Core.dll</c> de esa carpeta (<paramref name="hashCore"/> lo sustituye).
    /// </summary>
    public static RegistroRutas Escritorio(Func<string?>? hashCore = null, string? baseDirectory = null)
    {
        var baseDir = baseDirectory ?? AppContext.BaseDirectory;
        string preferida;
#if !POKEKEEP_PORTABLE
        // Solo desarrollo y pruebas (el binario del paquete no lleva esta variable): carpeta de registro de una prueba.
        preferida = Environment.GetEnvironmentVariable("POKEKEEP_REGISTROS_DIR") is { Length: > 0 } fija ? fija : Path.Combine(baseDir, "registros");
#else
        preferida = Path.Combine(baseDir, "registros");
#endif
        return new RegistroRutas(
            preferida,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pokekeep", "registros"),
            VersionDelPaquete(baseDir),
            hashCore ?? (() => HashCoreJunto(baseDir)),
            CerrojoDeEscritorio);
    }

    private static string VersionDelPaquete(string baseDir)
    {
        try
        {
            var p = Path.Combine(baseDir, "Pokekeep", "manifiesto.json");
            if (File.Exists(p))
            {
                using var doc = JsonDocument.Parse(File.ReadAllBytes(p));
                if (doc.RootElement.TryGetProperty("version", out var v) && v.ValueKind == JsonValueKind.String) return v.GetString() ?? "?";
            }
        }
        catch (Exception) { }
        return "desarrollo";
    }

    private static string? HashCoreJunto(string baseDir)
    {
        try
        {
            var p = Path.Combine(baseDir, "PKHeX.Core.dll");
            if (!File.Exists(p)) return null;
            using var s = File.OpenRead(p);
            return Convert.ToHexString(SHA256.HashData(s))[..12];
        }
        catch (Exception) { return null; }
    }
}
