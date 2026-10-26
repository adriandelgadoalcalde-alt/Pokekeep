// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Herramientas;

/// <summary>Herramientas del bloque 10-2: «Importar hexadecimal» (no necesita partida: lo que se pega puede ser una partida).</summary>
public static partial class CatalogoHerramientas
{
    static partial void AnadirB10_2(List<DefHerramienta> l) => l.Add(new DefHerramienta(
        Id: "importar-hex", Titulo: IdiomaInterfaz.T("CatalogoImportarHex.ImportarHexadecimal"), Descripcion: IdiomaInterfaz.T("CatalogoImportarHex.AbreArchivoBytesPegados"), Glifo: "Clipboard",
        Categoria: IdiomaInterfaz.T("Herramientas.ImportarExportar"), Bloque: "10-2", Filas: ["PK-MENU-026", "PK-IO-021"], Motivo: DefHerramienta.SiempreDisponible,
        Crear: (ctx, volver) => new ImportarHex.ImportarHexViewModel(ctx, volver), ConCampos: true,
        Estados:
        [
            new EstadoPantalla("con-texto", vm => ((ImportarHex.ImportarHexViewModel)vm).Texto = "AA BB CC DD"),
            new EstadoPantalla("no-valido", vm => { var h = (ImportarHex.ImportarHexViewModel)vm; h.Texto = "ZZ no es hexadecimal"; _ = ImportarHex.ModeloImportarHex.Convertir(h.Texto, out var error); h.MostrarError(error ?? string.Empty); }),
        ]));
}

public sealed partial class VistasHerramientas
{
    static partial void RegistrarB10_2(RegistroVistas r) => r.Registrar<ImportarHex.ImportarHexViewModel>(vm => new Views.ImportarHexView { DataContext = vm });
}
