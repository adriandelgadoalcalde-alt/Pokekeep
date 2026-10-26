// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace Pokekeep.Mobile.Ui.Herramientas;

/// <summary>
/// X37: «Cobertura y plantillas» no cuelga de «Más &gt; Herramientas» (no es una herramienta de PKHeX y el catálogo exige una fila del censo por herramienta): se abre desde Pokémon &gt; Equipo. Aquí solo se
/// registra su vista para que la Shell la pinte como el resto de pantallas de herramienta.
/// </summary>
public sealed partial class VistasHerramientas
{
    static partial void RegistrarX37(RegistroVistas r) => r.Registrar<Equipo.CoberturaYPlantillasViewModel>(vm => new Views.CoberturaYPlantillasView { DataContext = vm });
}
