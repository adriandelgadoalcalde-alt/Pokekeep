// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Pokekeep.Mobile.Ui.Herramientas.EdicionLotes;
using Pokekeep.Mobile.Ui.Herramientas.Informe;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Herramientas;

/// <summary>Herramientas del bloque 10-7: «Informe de la caja» (<c>ReportGrid</c>) y «Editor por lotes» (<c>BatchEditor</c>). Las dos necesitan una partida, como en PKHeX (<c>Menu_Report</c> exige cajas).</summary>
public static partial class CatalogoHerramientas
{
    /// <summary>PKHeX desactiva el informe si la partida no tiene cajas (<c>Menu_Report.Enabled = SAV.HasBox</c>).</summary>
    private static string? MotivoInforme(Modelo.Sesion? s) => s is null ? DefHerramienta.SinPartida : s.Guardado.HasBox ? null : IdiomaInterfaz.T("Cajas.PartidaTieneCajas");

    static partial void AnadirB10_7(List<DefHerramienta> l)
    {
        l.Add(new DefHerramienta(
            Id: "informe-cajas", Titulo: IdiomaInterfaz.T("CatalogoHerramientas.InformeCaja"), Descripcion: IdiomaInterfaz.T("CatalogoHerramientas.TodosPokemonPartidaTabla"), Glifo: "FileText",
            Categoria: IdiomaInterfaz.T("Zonas.Cajas"), Bloque: "10-7", Filas: ["PK-SUB-006", "PK-MENU-017", "PK-PKME-013", "PK-IO-042"], Motivo: MotivoInforme,
            Crear: (ctx, volver) => new InformeViewModel(ctx, volver),
            Estados:
            [
                new EstadoPantalla("con-resultado", vm => ((InformeViewModel)vm).GenerarSincrono()),
            ]));
        l.Add(new DefHerramienta(
            Id: "editor-lotes", Titulo: IdiomaInterfaz.T("CatalogoHerramientas.EditorLotes"), Descripcion: IdiomaInterfaz.T("CatalogoHerramientas.CambiaVezTodosPokemon"), Glifo: "Layers",
            Categoria: IdiomaInterfaz.T("CatalogoHerramientas.EdicionLotes"), Bloque: "10-7", Filas: ["PK-PKME-001", "PK-MENU-022"], Motivo: DefHerramienta.ExigePartida,
            Crear: (ctx, volver) => new EdicionLotesViewModel(ctx, volver), ConCampos: true,
            Estados:
            [
                new EstadoPantalla("con-instrucciones", vm => ((EdicionLotesViewModel)vm).Instrucciones = "=Species=25\n.IsShiny=true"),
                new EstadoPantalla("ejecutado", vm => { var e = (EdicionLotesViewModel)vm; e.Instrucciones = "=Species=25\n.IsShiny=true"; e.MostrarResultado(IdiomaInterfaz.T("CatalogoHerramientas.ModificadosArchivos")); }),
                new EstadoPantalla("error", vm => { var e = (EdicionLotesViewModel)vm; e.Instrucciones = "=Species=\n.IsShiny=true"; e.MostrarResultado(PKHeX.Core.MessageStrings.MsgBEFilterEmpty, error: true); }),
            ]));
    }
}

public sealed partial class VistasHerramientas
{
    static partial void RegistrarB10_7(RegistroVistas r)
    {
        r.Registrar<InformeViewModel>(vm => new Views.InformeView { DataContext = vm });
        r.Registrar<EdicionLotesViewModel>(vm => new Views.EdicionLotesView { DataContext = vm });
    }
}
