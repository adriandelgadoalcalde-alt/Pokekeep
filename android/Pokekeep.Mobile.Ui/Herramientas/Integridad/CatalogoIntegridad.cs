// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Herramientas;

public static partial class CatalogoHerramientas
{
    static partial void AnadirPiloto(List<DefHerramienta> l) => l.Add(new DefHerramienta(
        Id: "integridad", Titulo: IdiomaInterfaz.T("Partida.Herramienta.integridad.Titulo"), Descripcion: IdiomaInterfaz.T("CatalogoIntegridad.SumasVerificacionGuardadoLegalidad"), Glifo: "ShieldCheck", Categoria: IdiomaInterfaz.T("CatalogoHerramientas.Partida"), Bloque: "10-0",
        Filas: ["PK-BOX-027", "PK-BOX-028", "PK-IO-058"], Motivo: DefHerramienta.ExigePartida, Crear: (ctx, volver) => new Integridad.IntegridadViewModel(ctx, volver),
        Estados:
        [
            new EstadoPantalla("sumas", vm => ((Integridad.IntegridadViewModel)vm).MostrarSumas()),
            new EstadoPantalla("verificado", vm => { var i = (Integridad.IntegridadViewModel)vm; i.MostrarSumas(); i.VerificarSincrono(); }),
        ]));
}

public sealed partial class VistasHerramientas
{
    static partial void RegistrarPiloto(RegistroVistas r) => r.Registrar<Integridad.IntegridadViewModel>(vm => new Views.IntegridadView { DataContext = vm });
}
