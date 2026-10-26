// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Herramientas;

/// <summary>
/// Herramientas del bloque 10-8: «Exportar cajas» (<c>BoxExporter</c>). Desde aquí se abre con el alcance de los ajustes; el menú ⋮ de Cajas lo abre con el alcance forzado («Exportar cajas» y «Exportar caja»,
/// <c>Main.MainMenuBoxDump</c> y <c>MainMenuBoxDumpSingle</c>). Como en PKHeX, solo con una partida que tenga cajas (<c>Menu_DumpBoxes.Enabled = SAV.HasBox</c>).
/// </summary>
public static partial class CatalogoHerramientas
{
    static partial void AnadirB10_8(List<DefHerramienta> l) => l.Add(new DefHerramienta(
        Id: "exportar-cajas", Titulo: IdiomaInterfaz.T("ExportarCajas.ExportarCajas"), Descripcion: IdiomaInterfaz.T("CatalogoExportarCajas.GuardaCadaPokemonCajas"), Glifo: "Download",
        Categoria: IdiomaInterfaz.T("Zonas.Cajas"), Bloque: "10-8", Filas: ["PK-SUB-001", "PK-IO-036", "PK-IO-037"], Motivo: MotivoCajas,
        Crear: (ctx, volver) => new ExportarCajas.ExportarCajasViewModel(ctx, volver),
        Estados:
        [
            new EstadoPantalla("carpetas", vm => ((ExportarCajas.ExportarCajasViewModel)vm).Poner(() =>
            {
                var o = ((ExportarCajas.ExportarCajasViewModel)vm).OpcionesVigentes;
                o.FolderCreation = BoxExportFolderMode.FolderEachBox; o.FolderPrefix = BoxExportFolderNaming.IndexBoxName; o.FileIndexPrefix = BoxExportIndexPrefix.InBoxAndSlot;
            })),
            new EstadoPantalla("exportado", vm => ((ExportarCajas.ExportarCajasViewModel)vm).MostrarExportado(30, IdiomaInterfaz.T("Nav.Pokemon"))),
            new EstadoPantalla("error", vm => ((ExportarCajas.ExportarCajasViewModel)vm).MostrarError(IdiomaInterfaz.T("CatalogoExportarCajas.PodidoEscribirCarpetaProveedor"))),
        ]));

    /// <summary>Sin partida o sin cajas (Gen. 1-2 con cajas sí; la Mochila de un guardado sin PC, no), por qué no.</summary>
    public static string? MotivoCajas(Modelo.Sesion? s) => s is null ? DefHerramienta.SinPartida : s.Guardado.HasBox ? null : MessageStrings.MsgSaveBoxFailNone;
}

public sealed partial class VistasHerramientas
{
    static partial void RegistrarB10_8(RegistroVistas r) => r.Registrar<ExportarCajas.ExportarCajasViewModel>(vm => new Views.ExportarCajasView { DataContext = vm });
}
