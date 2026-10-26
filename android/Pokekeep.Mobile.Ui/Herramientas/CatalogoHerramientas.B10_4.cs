// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Herramientas;

/// <summary>Bloque 10-4: «Lista de carpetas» (recientes, copias de seguridad y carpetas) y «Forzar carga de SAV» (el solucionador del gestor de guardado).</summary>
public static partial class CatalogoHerramientas
{
    static partial void AnadirB10_4(List<DefHerramienta> l)
    {
        l.Add(new DefHerramienta(
            Id: "carpetas", Titulo: IdiomaInterfaz.T("CatalogoHerramientas.ListaCarpetas"), Descripcion: IdiomaInterfaz.T("CatalogoHerramientas.PartidasRecientesCopiasSeguridad"), Glifo: "FolderOpen",
            Categoria: IdiomaInterfaz.T("CatalogoHerramientas.Partida"), Bloque: "10-4",
            Filas: ["PK-SUB-014", "PK-SUB-015", "PK-SUB-016", "PK-SUB-017", "PK-MENU-023", "PK-MENU-042", "PK-BOX-078", "PK-BOX-029", "PK-IO-034", "PK-IO-061"],
            Motivo: DefHerramienta.SiempreDisponible, Crear: (ctx, volver) => new ListaCarpetas.ListaCarpetasViewModel(ctx, volver),
            Estados:
            [
                new EstadoPantalla("reciente", vm => ((ListaCarpetas.ListaCarpetasViewModel)vm).EstadoConGuardados(ListaCarpetas.ListaCarpetasViewModel.Reciente)),
                new EstadoPantalla("copias", vm => ((ListaCarpetas.ListaCarpetasViewModel)vm).EstadoConGuardados(ListaCarpetas.ListaCarpetasViewModel.Copias)),
                new EstadoPantalla("carpetas", vm => ((ListaCarpetas.ListaCarpetasViewModel)vm).EstadoCarpetas()),
            ]));
        l.Add(new DefHerramienta(
            Id: "forzar-carga", Titulo: IdiomaInterfaz.T("CatalogoHerramientas.ForzarCargaSAV"), Descripcion: IdiomaInterfaz.T("CatalogoHerramientas.SolucionProblemasAbrirGuardado"), Glifo: "HardDrive",
            Categoria: IdiomaInterfaz.T("CatalogoHerramientas.Partida"), Bloque: "10-4",
            Filas: ["PK-SUB-021", "PK-MENU-024", "PK-MENU-025", "PK-MENU-047"],
            Motivo: DefHerramienta.SiempreDisponible, Crear: (ctx, volver) => new ForzarCarga.ForzarCargaViewModel(ctx, volver),
            Estados: [new EstadoPantalla("error", vm => ((ForzarCarga.ForzarCargaViewModel)vm).EstadoConError())]));
    }
}

public sealed partial class VistasHerramientas
{
    static partial void RegistrarB10_4(RegistroVistas r)
    {
        r.Registrar<ListaCarpetas.ListaCarpetasViewModel>(vm => new Views.ListaCarpetasView { DataContext = vm });
        r.Registrar<ForzarCarga.ForzarCargaViewModel>(vm => new Views.ForzarCargaView { DataContext = vm });
    }
}
