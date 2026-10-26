// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Modelo;

/// <summary>Estado de legalidad de una entidad. Siempre se muestra con color, icono y texto (nunca solo por color) y nunca como porcentaje.</summary>
public enum EstadoLegal { SinDatos, Legal, Ilegal, NecesitaHome }

public static class LegalidadUi
{
    /// <summary>Analisis de legalidad bajo <see cref="Pokekeep.Sprites.LegalityGate"/> (el estado de <c>ParseSettings</c> del Core es estatico). Null si no hay entidad o el Core falla.</summary>
    public static LegalityAnalysis? Analizar(PKM pk)
    {
        if (pk.Species == 0) return null;
        try { lock (Pokekeep.Sprites.LegalityGate.Cerrojo) return new LegalityAnalysis(pk); }
        catch { return null; }
    }

    public static EstadoLegal Evaluar(PKM pk) => Estado(Analizar(pk));

    public static EstadoLegal Estado(LegalityAnalysis? la) => la is null ? EstadoLegal.SinDatos : la.Valid ? EstadoLegal.Legal : EstadoLegal.Ilegal;

    public static string Texto(EstadoLegal e) => e switch
    {
        EstadoLegal.Legal => IdiomaInterfaz.T("Informe.Columna.Legal"),
        EstadoLegal.Ilegal => IdiomaInterfaz.T("ModeloInformeLegalidad.RevisarLegalidad"),
        EstadoLegal.NecesitaHome => IdiomaInterfaz.T("Libreria.Estado.PasarHome"),
        _ => IdiomaInterfaz.T("EstadoLegal.Analizar"),
    };

    /// <summary>Clase de insignia del sistema de diseno (ok, error, aviso o ninguna).</summary>
    public static string ClaseInsignia(EstadoLegal e) => e switch
    {
        EstadoLegal.Legal => "ok",
        EstadoLegal.Ilegal => "error",
        EstadoLegal.NecesitaHome => "aviso",
        _ => "",
    };

    public static string Glifo(EstadoLegal e) => e switch
    {
        EstadoLegal.Legal => "CircleCheck",
        EstadoLegal.Ilegal => "TriangleAlert",
        EstadoLegal.NecesitaHome => "CircleAlert",
        _ => "Info",
    };
}
