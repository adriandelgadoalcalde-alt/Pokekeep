// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Herramientas;

public static partial class CatalogoHerramientas
{
    // Bloque 10-6: «Base de datos de regalos misteriosos» (SAV_MysteryGiftDB, Ctrl+G) y «Base de datos de encuentros» (SAV_Encounters, Ctrl+N). Los títulos son los de los menús de PKHeX en castellano
    // (Main.Menu_EncDatabase y Main.Menu_MGDatabase de lang_es.txt; el segundo, «de Regalo Misterioso», en plural y minúscula como lo escribe el resto de la app: «Partida > Regalos misteriosos»).
    static partial void AnadirB10_6(List<DefHerramienta> l)
    {
        l.Add(new DefHerramienta(
            Id: "encuentros", Titulo: IdiomaInterfaz.T("Ajustes.Pkhex.Grupo.18"), Descripcion: IdiomaInterfaz.T("CatalogoHerramientas.GeneradorEncuentrosBuscaEncuentros"), Glifo: "ScanSearch", Categoria: IdiomaInterfaz.T("CatalogoHerramientas.BuscarGenerar"), Bloque: "10-6",
            Filas: ["PK-SUB-010", "PK-SUB-011", "PK-SUB-012", "PK-SUB-013", "PK-MENU-021", "PK-SET-019", "PK-SET-149", "PK-SET-150", "PK-SET-151", "PK-SET-152", "PK-SET-153"],
            Motivo: DefHerramienta.ExigePartida, Crear: (ctx, volver) => new Encuentros.EncuentrosViewModel(ctx, volver), ConCampos: true,
            Estados:
            [
                new EstadoPantalla("resultados", vm => { var e = (Encuentros.EncuentrosViewModel)vm; e.ElegirEspecie(25); e.BuscarSincrono(); }),
                new EstadoPantalla("avanzado", vm => ((Encuentros.EncuentrosViewModel)vm).VerPestanaCommand.Execute("avanzado")),
                new EstadoPantalla("criterios", vm => ((Encuentros.EncuentrosViewModel)vm).VerPestanaCommand.Execute("criterios")),
                new EstadoPantalla("ajustes", vm => ((Encuentros.EncuentrosViewModel)vm).VerPestanaCommand.Execute("ajustes")),
            ]));
        l.Add(new DefHerramienta(
            Id: "regalos-db", Titulo: IdiomaInterfaz.T("CatalogoHerramientas.BaseDatosRegalosMisteriosos"), Descripcion: IdiomaInterfaz.T("CatalogoHerramientas.BuscaEntreRegalosEvento"), Glifo: "Gift", Categoria: IdiomaInterfaz.T("CatalogoHerramientas.BuscarGenerar"), Bloque: "10-6",
            Filas: ["PK-SUB-018", "PK-SUB-019", "PK-SUB-020", "PK-MENU-020", "PK-SET-020", "PK-SET-154", "PK-SET-155", "PK-IO-016", "PK-IO-025"],
            Motivo: DefHerramienta.ExigePartida, Crear: (ctx, volver) => new RegalosDb.RegalosDbViewModel(ctx, volver), ConCampos: true,
            Estados:
            [
                new EstadoPantalla("resultados", vm => { var r = (RegalosDb.RegalosDbViewModel)vm; r.ElegirEspecie(25); r.Buscar(); }),
                new EstadoPantalla("avanzado", vm => ((RegalosDb.RegalosDbViewModel)vm).VerPestanaCommand.Execute("avanzado")),
                new EstadoPantalla("ajustes", vm => ((RegalosDb.RegalosDbViewModel)vm).VerPestanaCommand.Execute("ajustes")),
            ]));
    }
}

public sealed partial class VistasHerramientas
{
    static partial void RegistrarB10_6(RegistroVistas r)
    {
        r.Registrar<Encuentros.EncuentrosViewModel>(vm => new Views.EncuentrosView { DataContext = vm });
        r.Registrar<RegalosDb.RegalosDbViewModel>(vm => new Views.RegalosDbView { DataContext = vm });
    }
}
