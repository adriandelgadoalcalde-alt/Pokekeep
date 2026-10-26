// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Herramientas;

public static partial class CatalogoHerramientas
{
    // Bloque 10-5: «Base de datos de PKM» (SAV_Database, Ctrl+D) y «Estadísticas base» (KChart, Mayús + Ctrl+D). Los títulos son los de PKHeX en castellano (Main.Menu_Database: «Base de datos PKM»,
    // aquí «Base de datos de PKM» como el resto de la app) y la tabla de especies, con el nombre que le da la propia app («Estadísticas base»).
    static partial void AnadirB10_5(List<DefHerramienta> l)
    {
        l.Add(new DefHerramienta(
            Id: "base-datos-pkm", Titulo: IdiomaInterfaz.T("BaseDatosPkm.BaseDatosPKM"), Descripcion: IdiomaInterfaz.T("CatalogoHerramientas.BuscaEntrePokemonCajas"), Glifo: "HardDrive", Categoria: IdiomaInterfaz.T("CatalogoHerramientas.BuscarGenerar"), Bloque: "10-5",
            Filas: ["PK-SUB-007", "PK-SUB-008", "PK-SUB-009", "PK-MENU-018", "PK-IO-041", "PK-SET-018", "PK-SET-143", "PK-SET-144", "PK-SET-145", "PK-SET-146", "PK-SET-147", "PK-SET-148"],
            Motivo: DefHerramienta.ExigePartida, Crear: (ctx, volver) => new BaseDatosPkm.BaseDatosPkmViewModel(ctx, volver), ConCampos: true,
            Estados:
            [
                new EstadoPantalla("resultados", vm => { var b = (BaseDatosPkm.BaseDatosPkmViewModel)vm; b.CargarSincrono(); b.ElegirEspecie(25); b.BuscarSincrono(); }),
                new EstadoPantalla("avanzado", vm => ((BaseDatosPkm.BaseDatosPkmViewModel)vm).VerPestanaCommand.Execute("avanzado")),
                new EstadoPantalla("opciones", vm => ((BaseDatosPkm.BaseDatosPkmViewModel)vm).VerPestanaCommand.Execute("opciones")),
                new EstadoPantalla("ajustes", vm => ((BaseDatosPkm.BaseDatosPkmViewModel)vm).VerPestanaCommand.Execute("ajustes")),
            ]));
        l.Add(new DefHerramienta(
            Id: "estadisticas-base", Titulo: IdiomaInterfaz.T("CatalogoHerramientas.EstadisticasBase"), Descripcion: IdiomaInterfaz.T("CatalogoHerramientas.TotalRatioCapturaTipos"), Glifo: "Grid3x3", Categoria: IdiomaInterfaz.T("CatalogoHerramientas.BuscarGenerar"), Bloque: "10-5",
            Filas: ["PK-SUB-005", "PK-MENU-019"],
            Motivo: DefHerramienta.ExigePartida, Crear: (ctx, volver) => new EstadisticasBase.EstadisticasBaseViewModel(ctx, volver), ConCampos: true));
    }
}

public sealed partial class VistasHerramientas
{
    static partial void RegistrarB10_5(RegistroVistas r)
    {
        r.Registrar<BaseDatosPkm.BaseDatosPkmViewModel>(vm => new Views.BaseDatosPkmView { DataContext = vm });
        r.Registrar<EstadisticasBase.EstadisticasBaseViewModel>(vm => new Views.EstadisticasBaseView { DataContext = vm });
    }
}
