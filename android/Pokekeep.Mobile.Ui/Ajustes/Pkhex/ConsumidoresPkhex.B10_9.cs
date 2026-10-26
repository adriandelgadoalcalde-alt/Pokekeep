// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace Pokekeep.Mobile.Ui.Ajustes.Pkhex;

public static partial class ConsumidoresPkhex
{
    // Bloque 10-9 (Showdown, QR y compartir). Las diez propiedades de «Hover» las lee ModeloShowdown.TextoVista (el resumen de una casilla, SummaryPreviewer.Show de PKHeX) y las de «Export»
    // ModeloShowdown.Exportar / ExportarLista (Main.ClickShowdownExportPKM, SAVEditor.ExportShowdownText). Tres de «Export» y una de «Hover» NO tienen consumidor ni en PKHeX 26.x
    // (ver el censo: «no aplica» con su motivo): no se declaran y por tanto no salen en pantalla.
    static partial void DeclararB10_9(Action<string, string> d)
    {
        const string resumen = "Resumen de una casilla (Ver resumen, Más opciones > Resumen del editor): ";
        d("BattleTemplate.Hover.Language", resumen + "idioma del texto");
        d("BattleTemplate.Hover.StyleStatEVs", resumen + "nombres de las estadísticas de los EV");
        d("BattleTemplate.Hover.StyleStatIVs", resumen + "nombres de las estadísticas de los IV");
        d("BattleTemplate.Hover.StyleStatSPs", resumen + "nombres de los puntos de Campeones (con el orden Personalizado o «Incluir puntos de Campeones»)");
        d("BattleTemplate.Hover.StyleStatOther", resumen + "nombres de las estadísticas GV y AV (Pokémon de Let's Go y Leyendas)");
        d("BattleTemplate.Hover.StyleMove", resumen + "movimientos seguidos o en cruceta (Leyendas Z-A)");
        d("BattleTemplate.Hover.IncludeChampionsStatPoints", resumen + "añade siempre la línea de puntos de Campeones");
        d("BattleTemplate.Hover.TokenOrder", resumen + "qué líneas salen y en qué orden");
        d("BattleTemplate.Hover.TokenOrderCustom", resumen + "el orden propio, con el formato Personalizado");

        const string exportar = "Exportar a Showdown (editor, equipo, cajas, Compartir): ";
        d("BattleTemplate.Export.Language", exportar + "idioma del texto");
        d("BattleTemplate.Export.StyleStatEVs", exportar + "nombres de las estadísticas de los EV");
        d("BattleTemplate.Export.StyleStatIVs", exportar + "nombres de las estadísticas de los IV");
        d("BattleTemplate.Export.StyleStatSPs", exportar + "nombres de los puntos de Campeones (con el orden Personalizado)");
        d("BattleTemplate.Export.StyleMove", exportar + "movimientos seguidos o en cruceta (Leyendas Z-A)");
        d("BattleTemplate.Export.TokenOrder", exportar + "qué líneas salen y en qué orden");
        d("BattleTemplate.Export.TokenOrderCustom", exportar + "el orden propio, con el formato Personalizado");
    }
}
