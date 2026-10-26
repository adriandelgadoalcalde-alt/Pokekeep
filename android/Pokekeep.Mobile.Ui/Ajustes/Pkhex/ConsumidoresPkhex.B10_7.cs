// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace Pokekeep.Mobile.Ui.Ajustes.Pkhex;

public static partial class ConsumidoresPkhex
{
    static partial void DeclararB10_7(Action<string, string> d)
    {
        // Informe de la caja: MainMenuBoxReport pasa Settings.Report a ReportGrid.PopulateData(list, extra, hide); el móvil los lee al pulsar «Crear el informe».
        d("Report.ExtraProperties", "Más > Herramientas > Informe de la caja: columnas adicionales (BatchPropertyProvider del editor por lotes)");
        d("Report.HiddenProperties", "Más > Herramientas > Informe de la caja: columnas que no se enseñan (tarjeta, detalle, orden y filtro)");
    }
}
