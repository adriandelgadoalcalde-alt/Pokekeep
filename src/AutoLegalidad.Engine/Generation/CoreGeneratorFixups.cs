// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;

namespace AutoLegalidad.Engine.Generation;

/// <summary>
/// Defectos del GENERADOR del Core (<c>ConvertToPKM</c>) que dejan un Pokemon que el propio analisis oficial del Core rechaza aunque la plantilla sea valida. Hito 3b (cola de clases): tres plantillas de
/// intercambio no se podian crear con el entrenador de prueba (el barrido las marcaba «SinFixture»). Cada arreglo usa SOLO API publica del Core y se aplica en el mismo punto para el motor
/// (<see cref="CoreTemplateStrategy.Create"/>) y para los fixtures de las pruebas, de modo que la legalidad la sigue decidiendo el Core.
/// <list type="bullet">
/// <item><see cref="EncounterTrade4PID"/> (HG/SS): el inicializador asigna <c>Ball</c> ANTES que <c>Version</c> (EncounterTrade4PID.cs, <c>new PK4 { ... Ball = (byte)FixedBall, ... Version = version ...}</c>) y el setter
/// <c>G4PKM.Ball</c> solo escribe el byte HG/SS si <c>WasCreatedInHGSS</c> (G4PKM.cs:286-298, 301-323), que depende de la version: queda <c>BallHGSS = 0</c> con <c>BallDPPt = 4</c> y
/// <c>MiscVerifierG4.IsValidBallHGSS</c> (MiscVerifierG4.cs:78-99) lo da por invalido («Ball extendida de HG/SS fuera de rango»). Volver a asignar <c>Ball</c> con la version ya puesta lo corrige.</item>
/// <item><see cref="EncounterTrade7b"/> (Let's Go): <c>ResetHeight/ResetWeight/ResetCP</c> se llaman ANTES de <c>SetPINGA</c> (EncounterTrade7b.cs, justo tras <c>SetEncounterMoves</c>), que fija despues IV y escalas:
/// los valores calculados (altura, peso, CP) quedan obsoletos y el Core los rechaza. <c>PB7.ResetCalculatedValues</c> (PB7.cs:633-637) los recalcula.</item>
/// <item><see cref="EncounterTrade8b"/> (BD/SP): fija el nombre del entrenador actual pero no <c>CurrentHandler = 1</c> (EncounterTrade8b.cs, inicializador de PB8); <c>HistoryVerifier</c> (HistoryVerifier.cs:120-121)
/// exige el flag de manejador cuando el EO de la plantilla nunca es el del jugador («El entrenador actual no puede ser EO»).</item>
/// <item><see cref="EncounterSlot8GO"/>/<see cref="EncounterSlot7GO"/>: la plantilla de una forma con objeto obligatorio (Giratina Origen...) sale sin el objeto (ver el caso del <c>switch</c>).</item>
/// </list>
/// </summary>
public static class CoreGeneratorFixups
{
    /// <summary>Corrige in situ lo que el generador del Core dejo mal para esa plantilla. No hace nada para el resto de plantillas.</summary>
    public static void Apply(PKM? pk, object? encounter)
    {
        if (pk is null) return;
        switch (encounter)
        {
            case EncounterTrade4PID when pk is G4PKM g4:
                g4.Ball = g4.Ball;
                break;
            case EncounterTrade7b when pk is PB7 pb7:
                pb7.ResetCalculatedValues();
                break;
            case EncounterTrade8b when pk is PB8 pb8:
                pb8.CurrentHandler = 1;
                break;
            // GO: la plantilla crea Giratina Origen (y Dialga/Palkia Origen en Gen9) sin su objeto de forma y el Core lo rechaza (FormVerifier.cs:65-69: el objeto es obligatorio para la forma 1). Con la
            // optimizacion de IV activa lo ponia ALM (otra estrategia), con ella apagada nadie: Orbe Griseo/Nucleo Griseo/Cristal/Esfera via la tabla del Core (TransferRoutes.RequiredFormItem).
            case EncounterSlot8GO or EncounterSlot7GO when pk.Form != 0 && pk.HeldItem == 0 && TransferRoutes.RequiredFormItem(pk, pk.Form) is { } formItem and not 0:
                pk.HeldItem = formItem;
                break;
            default:
                return;
        }
        pk.RefreshChecksum();
    }
}
