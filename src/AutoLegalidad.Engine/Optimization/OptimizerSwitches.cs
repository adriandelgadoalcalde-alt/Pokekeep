// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;

namespace AutoLegalidad.Engine.Optimization;

/// <summary>
/// Interruptores de las mejoras del Hito 3 en el optimizador. Existen SOLO para medir «antes y despues» con el mismo codigo (banco de incursiones) y para
/// las pruebas de regresion; el producto los deja siempre activados.
/// </summary>
public static class OptimizerSwitches
{
    /// <summary>Busqueda exhaustiva de semillas de 32 bits en las familias con modelo (ADR 0013).</summary>
    public static bool SeedSpaceSearch { get; set; } = true;

    /// <summary>Busqueda dirigida por restricciones (algebra lineal sobre GF(2)) de semillas de 64 bits en Raid8 y Lumiose (Hito 3b, ADR 0018).</summary>
    public static bool SeedConstraintSolver { get; set; } = true;

    /// <summary>Probar primero si el Core acepta IV mas altos sin tocar la semilla (Leyendas Arceus y otros con correlacion informativa).</summary>
    public static bool OracleFirstForSeeded { get; set; } = true;

    /// <summary>
    /// Cache UNIVERSAL por encuentro (ADR 0041) en las busquedas de semillas de 32 bits cuya semilla fija naturaleza, sexo y habilidad (Teratipo). Existe para medir «antes y despues» con el mismo binario: las
    /// herramientas de DESARROLLO (el banco y el Worker de registro) lo apagan con <c>POKEKEEP_UNIVERSO=0</c>; el motor NO lee variables de entorno (ADR 0042: un <c>#if</c> aqui hacia que el
    /// <c>AutoLegalidad.Engine.dll</c> del registro de la cache no fuera el del paquete y la cache precalculada no valiera). El producto la deja activada.
    /// </summary>
    public static bool EncounterUniverse { get; set; } = true;

    /// <summary>
    /// Hilos de la busqueda exhaustiva de semillas (Android F1, ADR 0054: configurable). <c>null</c> = <see cref="DefaultSeedSearchThreads"/>. En escritorio nadie lo toca; el servicio del movil lo fija segun
    /// el modo (rendimiento / ahorro) y el Worker acepta <c>--hilos N</c>. Los lotes recorren los mismos rangos de semillas sea cual sea el numero de hilos; el numero solo cambia cuanto tarda (y, con plazo, cuanto llega a recorrerse).
    /// </summary>
    public static int? SeedSearchThreads { get; set; }

    /// <summary>Hilos por defecto de la busqueda de semillas: nucleos - 1 (la interfaz sigue respondiendo), entre 1 y 12. Es el valor de siempre.</summary>
    public static int DefaultSeedSearchThreads => Math.Clamp(Environment.ProcessorCount - 1, 1, 12);

    /// <summary>Traza opcional de diagnostico del optimizador (por defecto apagada; las pruebas y bancos la activan con POKEKEEP_TRACE=1).</summary>
    public static Action<string>? Trace { get; set; }
}

/// <summary>Contadores publicos de la ultima busqueda de semillas (diagnostico, banco y pruebas).</summary>
public static class SeedSearchDiagnostics
{
    public static (ulong Scanned, bool Complete, double Seconds, int Hits)? Last
        => SeedSpaceSearch.Last is { } s ? (s.Scanned, s.Complete, s.Seconds, s.Hits) : null;

    /// <summary>True si la ultima busqueda de semillas salio de la cache de soluciones (R-011): no hubo busqueda.</summary>
    public static bool LastFromCache => SeedSpaceSearch.Last?.FromCache ?? false;

    /// <summary>Hilos de la busqueda (por defecto, nucleos - 1 para que la interfaz siga respondiendo).</summary>
    public static int Threads { get => SeedSpaceSearch.Threads; set => SeedSpaceSearch.Threads = Math.Max(1, value); }
}

/// <summary>Encuentros del Core que fijan los IV (no los saca ninguna semilla): no hay nada que optimizar.</summary>
internal static class FixedIvSets
{
    // EncounterStatic5N (Pokemon de N, B2/W2): los seis IV valen siempre 30 (EncounterStatic5N.cs:17 «private const byte IV = 30» y el filtro de IsMatchExact, EncounterStatic5N.cs:114-115): no implementa IFixedIVSet pero fija el vector entero.
    public static bool Specified(IEncounterable enc) => enc is IFixedIVSet { IVs.IsSpecified: true } or EncounterMight9 { IVs.IsSpecified: true } or EncounterDist9 { IVs.IsSpecified: true } or EncounterStatic5N;
}

/// <summary>Acceso de depuracion (banco) al modelo de incursiones de Espada/Escudo.</summary>
public sealed class Raid8ModelProbe(IGenerateSeed64 enc, ITrainerInfo trainer, int flawless)
{
    private readonly Raid8Model _m = new(enc, trainer, flawless);
    public bool Apply(PKM scratch, ulong seed) => _m.Apply(scratch, seed);
    public int FastIvSum(ulong seed) => _m.FastIvSum(seed);
}
