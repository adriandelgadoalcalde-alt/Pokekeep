// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Ajustes.Pkhex;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.ViewModels;

/// <summary>
/// Bloque 10-3: las filas de opción de «Conversión» y «Guardados antiguos». En PKHeX son desplegables con TODO el enum (<c>GameVersion</c> tiene decenas de valores y <c>LanguageID</c> incluye «None»
/// o «UNUSED_6»), aunque el Core solo usa unos pocos (<c>SaveLanguageSettings.Apply</c> descarta una versión que no sea del juego; <c>EntityConverter.VirtualConsoleSourceGen1</c> solo admite las de Gen. 1).
/// Aquí la lista ofrece solo esos, con el nombre del juego en castellano: elegir uno que el Core descarta sería un ajuste que no hace nada.
/// </summary>
public static partial class FabricaFilasPkhex
{
    static partial void PersonalizadaB10_3(NodoAjustePkhex n, IAjustesPkhex a, Action<HojaViewModel> h, ref FilaPkhex? fila)
    {
        if (OpcionesB10_3(n.Ruta) is { } valores) fila = new FilaPkhexOpcion(n, a, h, valores, () => OpcionesB10_3(n.Ruta)!);
    }

    /// <summary>Los valores que admite cada ajuste de opción del bloque (null = la fila normal con todo el enum).</summary>
    public static IReadOnlyList<(string Valor, string Etiqueta)>? OpcionesB10_3(string ruta) => ruta switch
    {
        "Converter.VirtualConsoleSourceGen1" => Versiones(GameVersion.RD, GameVersion.GN, GameVersion.BU, GameVersion.YW),
        "Converter.VirtualConsoleSourceGen2" => Versiones(GameVersion.GD, GameVersion.SI, GameVersion.C),
        "SaveLanguage.OverrideGen1.Version" => [SinElegir(GameVersion.RD, GameVersion.BU), .. Versiones(GameVersion.RD, GameVersion.GN, GameVersion.BU, GameVersion.YW)],
        "SaveLanguage.OverrideGen2.Version" => [SinElegir(GameVersion.GD, GameVersion.SI), .. Versiones(GameVersion.GD, GameVersion.SI)],
        "SaveLanguage.OverrideGen3RS.Version" => [SinElegir(GameVersion.R, GameVersion.S), .. Versiones(GameVersion.R, GameVersion.S)],
        "SaveLanguage.OverrideGen3FRLG.Version" => [SinElegir(GameVersion.FR, GameVersion.LG), .. Versiones(GameVersion.FR, GameVersion.LG)],
        "SaveLanguage.OverrideGen1.Language" or "SaveLanguage.OverrideGen2.Language" or "SaveLanguage.OverrideGen3RS.Language" or "SaveLanguage.OverrideGen3FRLG.Language" => IdiomasOccidentales,
        _ => null,
    };

    /// <summary>El valor de fábrica de las versiones (<c>GameVersion.Any</c>): <c>SaveLanguageSettings.Apply</c> no lo aplica y el Core deja la versión que tenga (la suya de fábrica al abrir la app; como en PKHeX). Por eso la etiqueta dice cuál es la DE FÁBRICA y no «la que se usará»: tras elegir otra y volver a «Sin elegir», sigue la elegida hasta reiniciar (auditoría, hallazgo 7).</summary>
    private static (string, string) SinElegir(params GameVersion[] versiones) => (nameof(GameVersion.Any),
        IdiomaInterfaz.F("Ajustes.Pkhex.SinElegir", string.Join(IdiomaInterfaz.T("Comun.ConjuncionY"), Versiones(versiones).Select(v => v.Item2))));

    private static (string, string)[] Versiones(params GameVersion[] v) => [.. v.Select(x => (x.ToString(), ProgramaPkhex.Textos.gamelist[(int)x]))];

    /// <summary>
    /// Idiomas de un guardado de Gen. 1-3 que el Core no sabe deducir. Las partidas japonesas (y las coreanas de Gen. 2) se reconocen solas por su tamaño o su codificación (<c>SaveLanguage.GetFallback</c>):
    /// el ajuste solo decide entre los idiomas occidentales.
    /// </summary>
    /// <remarks>Auditoría uso10 (A4): los nombres del Core («ENG (English)») no están traducidos; en castellano vuelven los de uso8 («Inglés»…) y en inglés los suyos.</remarks>
    private static (string, string)[] IdiomasOccidentales =>
    [
        (nameof(LanguageID.English), IdiomaInterfaz.T("ModeloForzarCarga.Ingles")), (nameof(LanguageID.French), IdiomaInterfaz.T("ModeloForzarCarga.Frances")),
        (nameof(LanguageID.Italian), IdiomaInterfaz.T("ModeloForzarCarga.Italiano")), (nameof(LanguageID.German), IdiomaInterfaz.T("ModeloForzarCarga.Aleman")),
        (nameof(LanguageID.Spanish), IdiomaInterfaz.T("ModeloForzarCarga.Espanol")),
    ];
}
