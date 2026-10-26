// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Collections.Concurrent;
using System.Security.Cryptography;
using Avalonia.Threading;
using PKHeX.Core;

namespace Pokekeep.Mobile.Ui.Modelo;

/// <summary>
/// Estado de legalidad de las casillas de la pantalla: el analisis del Core (<c>LegalityAnalysis</c>, con el tipo de almacenamiento de la casilla como hace PKHeX) bajo
/// <see cref="Pokekeep.Sprites.LegalityGate"/>, con una cache por contenido (un Pokemon que no cambia no se vuelve a analizar al deslizar entre cajas) y, en el movil, en SEGUNDO PLANO:
/// la caja aparece al instante con sus sprites y los avisos de legalidad se rellenan segun llegan, sin bloquear el hilo de la interfaz (120 Hz).
/// </summary>
public static class AnalizadorLegalidad
{
    /// <summary>La plataforma lo activa al arrancar; las pruebas lo dejan en falso y el analisis es sincrono y determinista.</summary>
    public static bool Asincrono { get; set; }

    private static readonly ConcurrentDictionary<(Guid, byte, int, string), EstadoLegal> Cache = new();

    public static void VaciarCache() => Cache.Clear();
    public static int EnCache => Cache.Count;

    private static (Guid, byte, int, string) Clave(PKM pk, StorageSlotType tipo)
    {
        var hash = SHA256.HashData(pk.Data);
        return (new Guid(hash.AsSpan(0, 16)), (byte)tipo, (int)pk.Context, HuellaLegalidad.Vigente);   // la huella de los ajustes de legalidad: cambiar uno invalida las insignias ya calculadas
    }

    /// <summary>Estado ya conocido (cache), o null si hay que analizar.</summary>
    public static EstadoLegal? Conocido(PKM pk, StorageSlotType tipo) => pk.Species == 0 ? EstadoLegal.SinDatos : Cache.TryGetValue(Clave(pk, tipo), out var e) ? e : null;

    /// <summary>Analiza (bajo el cerrojo del Core) y recuerda el estado. Un Pokemon con datos invalidos (huevo malo) cuenta como ilegal.</summary>
    public static EstadoLegal Evaluar(PKM pk, StorageSlotType tipo)
    {
        if (pk.Species == 0) return EstadoLegal.SinDatos;
        var clave = Clave(pk, tipo);
        if (Cache.TryGetValue(clave, out var e)) return e;
        var la = AnalizarPara(pk, tipo);
        var estado = LegalidadUi.Estado(la);
        Cache[clave] = estado;
        return estado;
    }

    /// <summary>Analisis completo (para el informe de legalidad): como PKHeX, con el tipo de almacenamiento de la casilla.</summary>
    public static LegalityAnalysis? AnalizarPara(PKM pk, StorageSlotType tipo)
    {
        if (pk.Species == 0) return null;
        try { lock (Pokekeep.Sprites.LegalityGate.Cerrojo) return new LegalityAnalysis(pk, tipo); }
        catch { return null; }
    }

    /// <summary>Analiza <paramref name="pendientes"/> en un hilo del grupo y llama a <paramref name="alTerminar"/> en el hilo de la interfaz por cada uno (solo si <paramref name="sigueVigente"/> sigue siendo cierto).</summary>
    public static void EvaluarEnSegundoPlano(IReadOnlyList<(PKM Pk, StorageSlotType Tipo, object Destino)> pendientes, Func<bool> sigueVigente, Action<object, EstadoLegal> alTerminar)
    {
        if (pendientes.Count == 0) return;
        _ = Task.Run(() =>
        {
            foreach (var (pk, tipo, destino) in pendientes)
            {
                if (!sigueVigente()) return;
                var estado = Evaluar(pk, tipo);
                if (!sigueVigente()) return;
                Dispatcher.UIThread.Post(() => { if (sigueVigente()) alTerminar(destino, estado); });
            }
        });
    }
}
