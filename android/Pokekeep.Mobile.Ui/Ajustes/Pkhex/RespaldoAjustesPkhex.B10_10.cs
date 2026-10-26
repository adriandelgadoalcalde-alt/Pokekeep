// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace Pokekeep.Mobile.Ui.Ajustes.Pkhex;

public static partial class RespaldoAjustesPkhex
{
    // Etiquetas y valores que lang_es.txt no trae (PKHeX de escritorio los enseña con el nombre de la propiedad en inglés). La clave es el NOMBRE de la propiedad o Tipo.Valor.
    static partial void AnadirEtiquetasB10_10(Action<string, string> a)
    {
        // Los valores de la severidad (en PKHeX salen en inglés: Invalid, Fishy, Valid). Los usa también el bloque 10-11 (apodos): NO repetirlos allí.
        a("Severity.Invalid", "Inválido");
        a("Severity.Fishy", "Sospechoso");
        a("Severity.Valid", "Válido");

        // El mismo nombre de propiedad sale en «Verificar todos» (Legality.Bulk) y en el análisis de cada Pokémon (Legality.Handler): la ruta distingue la etiqueta de este último.
        a("Legality.Handler.CheckActiveHandler", "Comprobar el entrenador actual al analizar cada Pokémon");
        a("Legality.Handler.CurrentHandlerMismatch", "Severidad si el entrenador actual no es el esperado");
        a("RNGFrameNotFound3", "Cuadro RNG no encontrado (Gen 3)");
        a("RNGFrameNotFound4", "Cuadro RNG no encontrado (Gen 4)");
        a("EggRandomAnyType3", "Huevos de Gen 3 con cualquier tipo de PID/IV");
        a("EggRandomAnyType4", "Huevos de Gen 4 con cualquier tipo de PID/IV");
        a("AllowBattleTowerTeamSwap", "Permitir el cambio de equipo de la Torre Batalla");
        a("Gen7TransferStarPID", "PID variocolor estrella al pasar de Gen 1/2 a Gen 7");
        a("HOMETransferTrackerNotPresent", "Falta la matrícula de HOME");
        a("ZeroHeightWeight", "Altura y peso a cero");
        a("Gen8MemoryMissingHT", "Recuerdo de Gen 8 ausente (entrenador actual)");
        foreach (var (g, nombre) in GeneracionesManejador) a("AllowHandleOTGen" + g, $"El entrenador original maneja el Pokémon ({nombre})");
    }

    static partial void AnadirDescripcionesB10_10(Action<string, string> a)
    {
        // PKHeX de escritorio no trae descripción para estas ocho reglas (solo la etiqueta en inglés): se escribe aquí, con lo que hace el Core en HistoryVerifier y MemoryVerifier.
        foreach (var (g, nombre) in GeneracionesManejador)
            a("AllowHandleOTGen" + g, $"Permite que, en un Pokémon de {nombre} cuyo encuentro fija al entrenador original (un regalo misterioso con entrenador propio), el manejador actual sea ese mismo entrenador original. Sin esto, el comprobador exige que el manejador sea otro entrenador y marca el Pokémon como no válido.");
    }

    private static readonly (string Gen, string Nombre)[] GeneracionesManejador =
    [
        ("6", "Gen 6"), ("7", "Gen 7"), ("7b", "Gen 7 (Let's Go, Pikachu! y Eevee!)"), ("8", "Gen 8"), ("8a", "Gen 8 (Leyendas Pokémon: Arceus)"), ("8b", "Gen 8 (Diamante Brillante y Perla Reluciente)"), ("9", "Gen 9"), ("9a", "Gen 9 (Leyendas Pokémon: Z-A)"),
    ];
}
