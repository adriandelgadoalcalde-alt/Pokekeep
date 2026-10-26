// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace AutoLegalidad.Engine.Infra;

/// <summary>
/// Algunos generadores del Core (<c>IEncounterConvertible.ConvertToPKM</c> con criterios) buscan semillas en un bucle SIN cota cuando los criterios no tienen solucion
/// (p. ej. seis IV 31 en un errante de Gen3, cuyo metodo solo produce IV de 8 bits) y no admiten cancelacion: un solo intento puede girar para siempre. Aqui se llama
/// a esas rutinas en otro hilo con un plazo; si se pasa, se ABANDONA el hilo (no existe otra forma de pararlo sin Thread.Abort) y se informa para que el motor dé el
/// criterio por imposible para ese encuentro. El Worker corre un proceso por solicitud, asi que un hilo abandonado muere con el proceso.
/// </summary>
public static class GuardedCore
{
    private static int _abandoned;

    /// <summary>Hilos abandonados en este proceso (diagnostico y pruebas).</summary>
    public static int Abandoned => Volatile.Read(ref _abandoned);

    /// <summary>Tras tantos abandonos el proceso deja de intentar generacion por criterios (cada hilo abandonado gasta un nucleo).</summary>
    public const int MaxAbandoned = 3;

    public static bool Exhausted => Abandoned >= MaxAbandoned;

    /// <summary>
    /// Pone a cero el contador de abandonos. El Worker del producto corre UN PROCESO POR SOLICITUD (ADR 0011), asi que cada solicitud empieza con el contador a cero; los bancos y las pruebas
    /// que atienden miles de solicitudes en un mismo proceso lo llaman antes de cada una para reproducir esa condicion (si no, una solicitud sin solucion agota el cupo de las siguientes).
    /// </summary>
    public static void Reset() => Interlocked.Exchange(ref _abandoned, 0);

    /// <summary>Alias de <see cref="Reset"/> (nombre usado por la sonda de tuplas del Hito 3b): solo para bancos que reutilizan un proceso para tuplas independientes.</summary>
    public static void ResetForIsolatedBench() => Reset();

    public static bool TryCall<T>(Func<T> work, TimeSpan timeout, out T? result)
    {
        result = default;
        if (Exhausted) return false;
        var task = Task.Factory.StartNew(work, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        if (task.Wait(timeout))
        {
            result = task.Result; // las excepciones se propagan al llamador, que ya las trata
            return true;
        }
        Interlocked.Increment(ref _abandoned);
        // Si el hilo termina algun dia con una excepcion, se observa para que no tumbe el proceso.
        _ = task.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
        return false;
    }
}
