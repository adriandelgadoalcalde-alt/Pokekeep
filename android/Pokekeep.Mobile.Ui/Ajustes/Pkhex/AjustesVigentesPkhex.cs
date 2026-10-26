// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace Pokekeep.Mobile.Ui.Ajustes.Pkhex;

/// <summary>
/// Los ajustes de PKHeX VIGENTES para quien no recibe un <see cref="IAjustesPkhex"/> por constructor (bloque 10-3): el editor de entidad lee aquí <c>EntityEditor.*</c> y <c>Privacy.HideSecretDetails</c>
/// y la plantilla del editor (<c>LocalResources.TemplatePath</c>), igual que <c>PKMEditor</c> lee <c>Main.Settings</c> en el momento. Se lee <see cref="Actual"/> en cada uso (nunca una copia guardada): un cambio
/// en «Más &gt; Ajustes» se nota en el siguiente campo que se pinta. Lo fija el arranque con el almacén real; sin arranque (pruebas, capturas) valen los de fábrica en memoria, que no tocan el Core.
/// Delega en <c>ProgramaPkhex.Ajustes</c> del bloque 10-1: un único almacén vigente para el programa y el editor tras integrar los bloques.
/// </summary>
public static class AjustesVigentesPkhex
{
    /// <summary>De dónde salen los ajustes (el almacén de la app; en pruebas, uno en memoria). Nunca null.</summary>
    public static IAjustesPkhex Fuente
    {
        get => ProgramaPkhex.Ajustes;
        set => ProgramaPkhex.Ajustes = value ?? new AjustesPkhexEnMemoria(aplicar: false);
    }

    /// <summary>Los ajustes en este momento (el almacén puede sustituir el objeto al restablecer: por eso se pide cada vez).</summary>
    public static AjustesPkhex Actual => Fuente.Actual;

    /// <summary>Vuelve a los de fábrica en memoria (pruebas).</summary>
    public static void Restablecer() => Fuente = new AjustesPkhexEnMemoria(aplicar: false);
}
