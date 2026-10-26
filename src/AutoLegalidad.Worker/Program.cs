// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Text;
using AutoLegalidad.Contracts;
using AutoLegalidad.Registro;

namespace AutoLegalidad.Worker;

/// <summary>
/// Proceso oculto de AutoLegalidad (plan 5). Protocolo: UN documento JSON UTF-8 por solicitud en stdin y UNO por respuesta en stdout; los
/// diagnosticos van a stderr. No recibe rutas ni bytes de guardados, no tiene funciones para exportar partidas y nunca escribe en un save.
/// Uso: <c>pokekeep-worker [--db &lt;ruta&gt;] [--soluciones &lt;ruta&gt;] [--hilos &lt;n&gt;] [--manifest]</c> (fuera del paquete, ademas <c>--core-sha/--engine-sha/--adapter-sha/--alm-sha</c>) (sin <c>--soluciones</c> usa <c>soluciones.sqlite</c> junto a la base de datos, si existe).
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        // ADR 0040: registro de fallos compartido con PKHeX-Pokekeep (carpeta registros\ junto al ejecutable). Un Worker es un proceso por solicitud: no escribe nada si todo va bien.
        RegistroFallos.Configurar("Worker");
        RegistroFallos.InstalarCapturaGlobal();
        try
        {
            var code = Run(args);
            if (code != Protocol.ExitOk) RegistroFallos.Error($"El Worker termina con código de salida {code}.", contexto: "Program.Main");
            return code;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            RegistroFallos.Error("Excepción sin controlar en el Worker; termina con error de infraestructura.", ex, "Program.Main");
            return Protocol.ExitInfrastructure;
        }
    }

    private static int Run(string[] args)
    {
#if !POKEKEEP_PORTABLE
        // Herramienta de construccion (no del paquete): --core-sha/--engine-sha/--adapter-sha/--alm-sha ligan la cache de soluciones a la identidad de OTROS binarios (p. ej. los del movil).
        // Tambien por variables de entorno POKEKEEP_CORE_SHA/POKEKEEP_ENGINE_SHA/POKEKEEP_ADAPTER_SHA/POKEKEEP_ALM_SHA (las que fija construir-soluciones.ps1 para los Workers que lanza el banco); los argumentos mandan.
        static string? Env(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } v ? v : null;
        var fromEnv = BinaryIdentity.WithOverrides(BinaryIdentity.Current, Env("POKEKEEP_CORE_SHA"), Env("POKEKEEP_ENGINE_SHA"), Env("POKEKEEP_ADAPTER_SHA"), Env("POKEKEEP_ALM_SHA"));
        BinaryIdentity.Current = BinaryIdentity.FromArguments(args, fromEnv);
#endif
#if POKEKEEP_PORTABLE
        var db = ArgValue(args, "--db") ?? WorkerHost.DefaultDbPath();   // paquete portable: sin variables de entorno de desarrollo
#else
        var db = ArgValue(args, "--db") ?? Environment.GetEnvironmentVariable("POKEKEEP_DB") ?? WorkerHost.DefaultDbPath();
#endif
#if !POKEKEEP_PORTABLE
        // Solo desarrollo: medir «antes y despues» de la cache universal con el mismo binario (ADR 0041/0042; el motor no lee variables de entorno).
        if (Environment.GetEnvironmentVariable("POKEKEEP_UNIVERSO") == "0") AutoLegalidad.Engine.Optimization.OptimizerSwitches.EncounterUniverse = false;
#endif
#if !POKEKEEP_PORTABLE
        // Herramienta de construccion (no del paquete): deja un soluciones.sqlite recien precalculado listo para entregar (sin WAL, compactado).
        if (ArgValue(args, "--sellar") is { } toSeal)
        {
            AutoLegalidad.Engine.SolutionCache.SqliteSolutionCache.Seal(toSeal);
            return 0;
        }
#endif
#if !POKEKEEP_PORTABLE
        // ADR 0097: herramientas de construccion (no del paquete) del formato v2 de la cache. Una linea JSON por stdout; codigo distinto de 0 si algo difiere o no se puede leer.
        if (ArgValue(args, "--convertir-soluciones") is { } convSrc)
        {
            if (ArgValue(args, "--destino") is not { } convDst) { Console.Error.WriteLine("Falta --destino <ruta>."); return 2; }
            try
            {
                Console.Out.WriteLine(System.Text.Json.JsonSerializer.Serialize(AutoLegalidad.Engine.SolutionCache.SolutionCacheConverter.ConvertToV2(convSrc, convDst)));
                return 0;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException) { Console.Error.WriteLine("convertir-soluciones: " + ex.GetType().Name + ": " + ex.Message); return 1; }
        }
        if (ArgValue(args, "--verificar-soluciones") is { } verA)
        {
            if (ArgValue(args, "--con") is not { } verB) { Console.Error.WriteLine("Falta --con <ruta>."); return 2; }
            try
            {
                Console.Out.WriteLine("{\"filas_iguales\":" + AutoLegalidad.Engine.SolutionCache.SolutionCacheConverter.VerifyEqual(verA, verB) + "}");
                return 0;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException) { Console.Error.WriteLine("verificar-soluciones: " + ex.GetType().Name + ": " + ex.Message); return 1; }
        }
#endif
#if !POKEKEEP_PORTABLE
        // ADR 0042: herramienta de construccion, precalculo de las plantillas de Teratipo (no existe en el paquete).
        if (args.Contains("--precalcular-teratipo", StringComparer.Ordinal)) return WorkerPrecompute.Run(args, db);
#endif
        // Android F1: hilos de la busqueda exhaustiva de semillas (por defecto, nucleos - 1). Es un ajuste de usuario, tambien en el paquete.
        if (ArgValue(args, "--hilos") is { } hilos && int.TryParse(hilos, out var nHilos) && nHilos > 0)
            AutoLegalidad.Engine.Optimization.OptimizerSwitches.SeedSearchThreads = nHilos;
        var utf8 = new UTF8Encoding(false);
        Console.InputEncoding = utf8;
        Console.OutputEncoding = utf8;

        if (args.Contains(Protocol.ManifestArgument, StringComparer.Ordinal))
        {
            using var outManifest = Console.OpenStandardOutput();
            return WorkerHost.WriteManifest(db, outManifest);
        }

        using var stdin = Console.OpenStandardInput();
        using var stdout = Console.OpenStandardOutput();
        return WorkerHost.Run(stdin, stdout, db, Console.Error, ArgValue(args, "--soluciones"));
    }

    private static string? ArgValue(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
}
