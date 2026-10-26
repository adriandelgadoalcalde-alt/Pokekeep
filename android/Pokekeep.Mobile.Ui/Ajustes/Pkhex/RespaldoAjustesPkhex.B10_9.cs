// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace Pokekeep.Mobile.Ui.Ajustes.Pkhex;

public static partial class RespaldoAjustesPkhex
{
    // Bloque 10-9. lang_es.txt solo trae «PropertyGrid.Language» ("Idioma") y las descripciones de Language, IncludeChampionsStatPoints, TokenOrder y TokenOrderCustom (que hablan de «exportar» y valen igual
    // para el resumen); no trae las etiquetas de los demás ni la descripción de los cuatro estilos. Además «Hover» y «Export» comparten nombres de propiedad: en una sola pantalla «Idioma» saldría dos veces, así que
    // cada ajuste lleva su etiqueta por RUTA, con «Resumen» (el texto que PKHeX enseña al pasar el ratón por una casilla) o «Exportar» delante.
    static partial void AnadirEtiquetasB10_9(Action<string, string> a)
    {
        foreach (var (grupo, prefijo) in new[] { ("Hover", "Resumen"), ("Export", "Exportar") })
        {
            var r = "BattleTemplate." + grupo + ".";
            a(r + "Language", prefijo + ": idioma");
            a(r + "StyleStatEVs", prefijo + ": estilo de los EV");
            a(r + "StyleStatIVs", prefijo + ": estilo de los IV");
            a(r + "StyleStatSPs", prefijo + ": estilo de los puntos de Campeones");
            a(r + "StyleStatOther", prefijo + ": estilo de GV y AV");
            a(r + "StyleMove", prefijo + ": disposición de movimientos");
            a(r + "IncludeChampionsStatPoints", prefijo + ": añadir puntos de Campeones");
            a(r + "TokenOrder", prefijo + ": formato");
            a(r + "TokenOrderCustom", prefijo + ": orden propio");
        }

        // Valores de las enumeraciones (clave «Tipo.Valor»): lang_es.txt no los trae y el nombre en inglés del enum no sirve de etiqueta.
        a("BattleTemplateDisplayStyle.Custom", "Personalizado (el orden propio)");
        a("BattleTemplateDisplayStyle.Showdown", "Showdown");
        a("BattleTemplateDisplayStyle.Legacy", "Clásico (el formato de la comunidad)");
        a("BattleTemplateDisplayStyle.Brief", "Breve (el resumen de una casilla)");
        a("StatDisplayStyle.Custom", "Personalizado");
        a("StatDisplayStyle.Abbreviated", "Abreviado (HP / Atk / Def…)");
        a("StatDisplayStyle.Full", "Completo");
        a("StatDisplayStyle.HABCDS", "Letras H A B C D S");
        a("StatDisplayStyle.Raw", "Solo números");
        a("StatDisplayStyle.Raw00", "Solo números de dos cifras");
        a("MoveDisplayStyle.Fill", "En orden (1–4)");
        a("MoveDisplayStyle.Directional", "Cruceta (↑→←↓)");
        a("LanguageID.None", "El idioma del programa");
        a("LanguageID.Japanese", "Japonés");
        a("LanguageID.English", "Inglés");
        a("LanguageID.French", "Francés");
        a("LanguageID.Italian", "Italiano");
        a("LanguageID.German", "Alemán");
        a("LanguageID.Spanish", "Español de España");
        a("LanguageID.Korean", "Coreano");
        a("LanguageID.ChineseS", "Chino simplificado");
        a("LanguageID.ChineseT", "Chino tradicional");
        a("LanguageID.SpanishL", "Español de Latinoamérica");
    }

    static partial void AnadirDescripcionesB10_9(Action<string, string> a)
    {
        // «del» y «al» con su contracción: «Idioma de el resumen» salía así hasta el 25-oct.
        foreach (var (grupo, que, delQue, alQue, formato) in new[]
        {
            ("Hover", "el resumen de una casilla", "del resumen de una casilla", "al resumen de una casilla", "resumen"),
            ("Export", "el set que se exporta a Showdown", "del set que se exporta a Showdown", "al set que se exporta a Showdown", "set"),
        })
        {
            var r = "BattleTemplate." + grupo + ".";
            a(r + "Language", $"Idioma {delQue}. «El idioma del programa» usa el castellano de la aplicación.");
            a(r + "StyleStatEVs", $"Cómo se escriben los nombres de las estadísticas de los puntos de esfuerzo (EV) en {que}.");
            a(r + "StyleStatIVs", $"Cómo se escriben los nombres de las estadísticas de los valores individuales (IV) en {que}.");
            a(r + "StyleStatSPs", $"Cómo se escriben los nombres de las estadísticas de los puntos de Campeones en {que}. Solo se ven si el orden incluye esa línea.");
            a(r + "StyleStatOther", $"Cómo se escriben los nombres de las estadísticas de otros valores (GV de Leyendas y AV de Let's Go) en {que}.");
            a(r + "StyleMove", $"Cómo se colocan los movimientos en {que}: seguidos en las casillas 1–4, sin huecos, o en la cruceta (↑→←↓), que solo usa Leyendas Pokémon: Z-A.");
            a(r + "IncludeChampionsStatPoints", $"Añade siempre {alQue} la línea de puntos de estadística de Campeones.");
            a(r + "TokenOrder", $"Qué líneas lleva y en qué orden: Showdown, Clásico, Breve o el orden propio (Personalizado) de este {formato}.");
            a(r + "TokenOrderCustom", $"El orden propio de las líneas {delQue}. Solo se usa si el formato es Personalizado.");
        }
    }
}
