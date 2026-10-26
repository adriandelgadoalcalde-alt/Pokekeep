// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace AutoLegalidad.Engine.Infra;

/// <summary>
/// El Core y ALM guardan estado ESTATICO (ParseSettings, TrainerSettings, APILegality...). Una sola operacion a la vez por proceso: todo
/// acceso que lo toque pasa por este cerrojo. El Worker ademas usa un proceso por solicitud, asi que nada se comparte entre solicitudes.
/// </summary>
public static class EngineGlobalState
{
    public static readonly object Gate = new();
}
