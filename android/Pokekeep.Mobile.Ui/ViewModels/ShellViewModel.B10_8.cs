// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Pokekeep.Mobile.Ui.Almacen;
using Pokekeep.Mobile.Ui.Herramientas.ExportarCajas;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.ViewModels;

/// <summary>Bloque 10-8: el exportador de cajas abierto desde el menú ⋮ de Cajas («Exportar cajas» y «Exportar caja» de PKHeX). Aparte para no tocar la Shell de los demás bloques.</summary>
public sealed partial class ShellViewModel
{
    public string? MotivoExportadorCajas => Carpetas is not IAlmacenCarpetasCajas || Documentos is null ? IdiomaInterfaz.T("ExportarCajas.ExportarCarpetaDisponibleAqui") : null;

    public void AbrirExportadorCajas(AlcanceExportador alcance, int caja)
    {
        if (Sesion is null) return;
        Pantalla = new ExportarCajasViewModel(ContextoDeHerramientas(), CerrarPantalla, alcance, caja);
    }
}
