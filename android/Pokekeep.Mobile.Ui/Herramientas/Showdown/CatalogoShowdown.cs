// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Herramientas;

public static partial class CatalogoHerramientas
{
    /// <summary>Un set de ejemplo con una línea que Showdown no entiende, para dejar la pantalla «con set leído» en el gate visual y en las pruebas.</summary>
    public static string SetDeEjemplo => "Garchomp @ Choice Scarf\nAbility: Rough Skin\nLevel: 50\nEVs: 252 Atk / 4 SpD / 252 Spe\nAdamant Nature\n- Earthquake\n- Outrage\n- Swords Dance\n- Stone Edge\n" + IdiomaInterfaz.T("CatalogoShowdown.LineaNoValida");

    static partial void AnadirB10_9(List<DefHerramienta> l) => l.Add(new DefHerramienta(
        Id: "showdown", Titulo: IdiomaInterfaz.T("Ajustes.Pkhex.Grupo.16"), Descripcion: IdiomaInterfaz.T("CatalogoShowdown.ImportarSetTextoExportar"), Glifo: "ClipboardCopy", Categoria: IdiomaInterfaz.T("Herramientas.ImportarExportar"), Bloque: "10-9",
        Filas: ["PK-MENU-008"], Motivo: DefHerramienta.ExigePartida, Crear: (ctx, volver) => new Showdown.ShowdownViewModel(ctx, volver), ConCampos: true,
        Estados:
        [
            new EstadoPantalla("set", vm => { var s = (Showdown.ShowdownViewModel)vm; s.TextoSet = SetDeEjemplo; s.Revisar(); }, ConTeclado: true),
            new EstadoPantalla("exportado", vm => ((Showdown.ShowdownViewModel)vm).MostrarExportacion(equipo: false)),
        ]));
}

public sealed partial class VistasHerramientas
{
    static partial void RegistrarB10_9(RegistroVistas r) => r.Registrar<Showdown.ShowdownViewModel>(vm => new Views.ShowdownView { DataContext = vm });
}
