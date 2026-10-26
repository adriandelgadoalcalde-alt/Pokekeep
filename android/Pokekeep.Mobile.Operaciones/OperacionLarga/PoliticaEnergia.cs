// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Pokekeep.Mobile.Ui.Localizacion;
namespace Pokekeep.Mobile.Operaciones.OperacionLarga;

/// <summary>Estado térmico del dispositivo, con los MISMOS valores que <c>PowerManager.THERMAL_STATUS_*</c> de Android (0 a 6): la plataforma lo copia tal cual.</summary>
public enum EstadoTermico { Ninguno = 0, Leve = 1, Moderado = 2, Grave = 3, Critico = 4, Emergencia = 5, Apagado = 6 }

/// <summary>Lo que la plataforma lee del dispositivo: «Ahorro de energía» (<c>PowerManager.IsPowerSaveMode</c>) y la temperatura (<c>PowerManager.CurrentThermalStatus</c>).</summary>
public sealed record EstadoEnergia(bool AhorroActivo, EstadoTermico Termico)
{
    public static readonly EstadoEnergia Normal = new(false, EstadoTermico.Ninguno);
}

/// <summary>Qué hacer con la energía actual: cuántos procesos del motor usar, si hay que detener la operación y el aviso que se enseña (null si no hay nada que decir).</summary>
public sealed record DecisionEnergia(int Procesos, bool Detener, string? Aviso);

/// <summary>
/// Política de batería y temperatura de las operaciones largas (F9X-07, ADR 0070, decisión 5). Función pura, probada en el PC; la lectura del dispositivo es de la plataforma.
/// <list type="bullet">
/// <item>Sin ahorro de energía y con temperatura normal (hasta <see cref="EstadoTermico.Leve"/>): el ajuste «Procesos en paralelo» recortado a lo que admite el dispositivo.</item>
/// <item>Con ahorro de energía activo o temperatura <see cref="EstadoTermico.Moderado"/>: 1 proceso, con aviso.</item>
/// <item>Con temperatura <see cref="EstadoTermico.Grave"/> o peor: se detiene (lo ya preparado se puede colocar con la pregunta de siempre; nada se escribe sin ella).</item>
/// </list>
/// </summary>
public static class PoliticaEnergia
{
    public static string AvisoAhorro => IdiomaInterfaz.T("PoliticaEnergia.AhorroDeEnergiaActivo");
    public static string AvisoCalor => IdiomaInterfaz.T("PoliticaEnergia.ElMovilEstaCaliente");
    public static string AvisoDetener => IdiomaInterfaz.T("PoliticaEnergia.ElMovilEstaDemasiado");

    public static DecisionEnergia Decidir(int procesosDelAjuste, int procesosMaximos, EstadoEnergia energia)
    {
        var normal = Math.Clamp(procesosDelAjuste, 1, Math.Max(1, procesosMaximos));
        if (energia.Termico >= EstadoTermico.Grave) return new DecisionEnergia(1, true, AvisoDetener);
        if (energia.Termico == EstadoTermico.Moderado) return new DecisionEnergia(1, false, normal > 1 ? AvisoCalor : null);
        if (energia.AhorroActivo) return new DecisionEnergia(1, false, normal > 1 ? AvisoAhorro : null);
        return new DecisionEnergia(normal, false, null);
    }
}
