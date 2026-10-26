// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Text.Json;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Ajustes.Pkhex;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Herramientas.Showdown;

/// <summary>Lo que PKHeX saca al importar un set de texto: el primer set, su texto reformateado con los ajustes de exportación y las líneas que no entendió (ya en castellano).</summary>
/// <param name="Error">Por qué no hay set (el texto de <c>MessageStrings</c> de PKHeX); null si hay set.</param>
public sealed record ResultadoImportarSet(ShowdownSet? Set, string? Error, string TextoReformateado, IReadOnlyList<string> LineasNoValidas, int SetsEnElTexto)
{
    public bool Correcto => Set is not null && Error is null;
}

/// <summary>
/// Showdown de PKHeX sin pantalla (bloque 10-9): importar el primer set de un texto, exportar un Pokémon, el equipo, una caja o todas las cajas, y el texto de resumen de una casilla («hover»).
/// Llama al Core COMO LO LLAMA PKHeX (<c>Main.ClickShowdownImportPKM</c>, <c>ClickShowdownExportPKM</c>, <c>SAVEditor.ExportShowdownText</c>, <c>SummaryPreviewer.Show</c>) con los ajustes
/// <c>BattleTemplate.Export</c> y <c>BattleTemplate.Hover</c> VIGENTES; los textos de aviso son los <c>MessageStrings</c> del Core (en castellano con <see cref="TextosNucleo"/>).
/// Dos diferencias, ambas por la regla 15 (nada sale del dispositivo): una dirección de Showdown o de Pokepaste NO se descarga (PKHeX la pediría por internet) y se pide el texto; y el estilo de estadísticas
/// «Personalizado», que en PKHeX lanza una excepción al exportar, se trata como el abreviado.
/// </summary>
public static class ModeloShowdown
{
    /// <summary>El texto que se enseña cuando el usuario pega una dirección: PKHeX la descargaría (<c>BattleTemplateTeams.TryGetSetLines</c>), Pokekeep no se conecta nunca.</summary>
    public static string AvisoDireccion => IdiomaInterfaz.T("ModeloShowdown.DireccionesShowdownPokepasteNecesitan");

    public const string SeparadorSets = "\r\n\r\n";

    /// <summary>El idioma del programa (<c>Language.GetLanguageValue(Settings.Startup.Language)</c>).</summary>
    public static LanguageID IdiomaPrograma(AjustesPkhex a) => Language.GetLanguageValue(a.Startup.Language);

    /// <summary>Ajustes de exportación de un set (<c>Settings.BattleTemplate.Export.GetSettings(programLanguage, context)</c>), con el estilo «Personalizado» de estadísticas normalizado.</summary>
    public static BattleTemplateExportSettings Exportacion(AjustesPkhex a, EntityContext contexto) => Normalizado(a.BattleTemplate.Export).GetSettings(IdiomaPrograma(a), contexto);

    /// <summary>El idioma con que sale un set exportado: el elegido en <c>BattleTemplate.Export.Language</c> o, si no hay, el del programa (inglés si tampoco), como <c>BattleTemplateTypeSetting.GetLanguage</c>.</summary>
    public static LanguageID IdiomaExportacion(AjustesPkhex a)
    {
        var elegido = a.BattleTemplate.Export.Language;
        if (elegido != LanguageID.None) return elegido;
        var programa = IdiomaPrograma(a);
        return programa == LanguageID.None ? LanguageID.English : programa;
    }

    /// <summary>Ajustes del resumen de una casilla (<c>Settings.BattleTemplate.Hover.GetSettings(...)</c>).</summary>
    public static BattleTemplateExportSettings Vista(AjustesPkhex a, EntityContext contexto) => Normalizado(a.BattleTemplate.Hover).GetSettings(IdiomaPrograma(a), contexto);

    /// <summary>Una copia de los ajustes con el estilo «Personalizado» de estadísticas cambiado por el abreviado: PKHeX lo tiene en el enum pero <c>BattleTemplateConfig.GetStatDisplay</c> lanza <c>ArgumentOutOfRangeException</c> al usarlo.</summary>
    private static BattleTemplateTypeSetting Normalizado(BattleTemplateTypeSetting s)
    {
        static StatDisplayStyle Fix(StatDisplayStyle e) => e == StatDisplayStyle.Custom ? StatDisplayStyle.Abbreviated : e;
        if (s.StyleStatEVs != StatDisplayStyle.Custom && s.StyleStatIVs != StatDisplayStyle.Custom && s.StyleStatSPs != StatDisplayStyle.Custom && s.StyleStatOther != StatDisplayStyle.Custom) return s;
        return new BattleTemplateTypeSetting
        {
            Language = s.Language, StyleMove = s.StyleMove, IncludeChampionsStatPoints = s.IncludeChampionsStatPoints, StatsCustom = s.StatsCustom, TokenOrder = s.TokenOrder, TokenOrderCustom = s.TokenOrderCustom,
            StyleStatEVs = Fix(s.StyleStatEVs), StyleStatIVs = Fix(s.StyleStatIVs), StyleStatSPs = Fix(s.StyleStatSPs), StyleStatOther = Fix(s.StyleStatOther),
        };
    }

    // ---------------------------------------------------------------------------------------------------------------- importar
    /// <summary>¿Es una dirección de Showdown o de Pokepaste (que PKHeX descargaría)?</summary>
    public static bool EsDireccion(string texto)
    {
        if (ShowdownTeam.IsURL(texto, out _) || PokepasteTeam.IsURL(texto, out _)) return true;
        var t = texto.Trim();   // cualquier otra dirección suelta: PKHeX la leería como texto y no la entendería; aquí se explica por qué no se descarga
        return (t.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || t.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) && !t.Any(char.IsWhiteSpace);
    }

    /// <summary>
    /// <c>Main.ClickShowdownImportPKM</c>: toma SOLO el primer set del texto (<c>BattleTemplateTeams.TryGetSets(text).FirstOrDefault()</c>), exige que tenga especie, lo reformatea con los ajustes de exportación
    /// y recoge las líneas no válidas ya humanizadas en castellano (<c>BattleTemplateParseErrorLocalization</c>). Nunca toca la red.
    /// </summary>
    public static ResultadoImportarSet Analizar(string? texto, AjustesPkhex a)
    {
        TextosNucleo.Preparar();
        if (string.IsNullOrWhiteSpace(texto)) return new ResultadoImportarSet(null, MessageStrings.MsgClipboardFailRead, string.Empty, [], 0);
        if (EsDireccion(texto)) return new ResultadoImportarSet(null, AvisoDireccion, string.Empty, [], 0);
        var sets = ShowdownParsing.GetShowdownSets(texto).ToList();   // = BattleTemplateTeams.TryGetSets para un texto que no es una dirección
        var set = sets.FirstOrDefault() ?? new ShowdownSet(string.Empty);
        if (set.Species == 0) return new ResultadoImportarSet(null, MessageStrings.MsgSimulatorFailClipboard, string.Empty, [], sets.Count);
        var reformateado = set.GetText(Exportacion(a, set.Context));
        var localizacion = LocalizacionErrores(Pokekeep.Mobile.Ui.Ajustes.Pkhex.ProgramaPkhex.Idioma);
        var invalidas = set.InvalidLines.Select(l => l.Humanize(localizacion)).ToList();
        return new ResultadoImportarSet(set, null, reformateado, invalidas, sets.Count);
    }

    // El Core trae «setparse_es.json» SIN traducir (es idéntico al inglés: «Unrecognized: …»). Pokekeep es en castellano de España: mientras el Core no lo traduzca se usa esta traducción; si el Core la trae,
    // manda la del Core (la prueba compara plantilla a plantilla y avisa cuando el Core cambie).
    private const string JsonErroresEs = """
    {
      "LineLength": "La línea supera la longitud máxima admitida: {0}",
      "TokenUnknown": "No se reconoce: {0}",
      "TokenFailParse": "No se pudo interpretar: {0}",
      "MoveCountTooMany": "Se han indicado demasiados movimientos: {0}",
      "MoveSlotAlreadyUsed": "Esa posición de movimiento ya está ocupada: {0}",
      "MoveDuplicate": "Movimiento repetido: {0}",
      "MoveUnrecognized": "Movimiento no reconocido: {0}",
      "ItemUnrecognized": "Objeto equipado no reconocido: {0}",
      "AbilityDeclaration": "La habilidad ya se había indicado: {0}",
      "AbilityUnrecognized": "Habilidad no reconocida: {0}",
      "AbilityAlreadySpecified": "La habilidad ya estaba especificada: {0}",
      "NatureUnrecognized": "Naturaleza no reconocida: {0}",
      "NatureAlreadySpecified": "La naturaleza ya estaba especificada: {0}",
      "HiddenPowerUnknownType": "Tipo de Poder Oculto no reconocido: {0}",
      "HiddenPowerIncompatibleIVs": "El tipo de Poder Oculto no es compatible con los IV: {0}",
      "NatureEffortAmpDeclaration": "La naturaleza o el refuerzo por esfuerzo ya se había indicado: {0}",
      "NatureEffortAmpUnknown": "Refuerzo de naturaleza por esfuerzo desconocido: {0}",
      "NatureEffortAmpAlreadySpecified": "El refuerzo de naturaleza por esfuerzo ya estaba especificado: {0}",
      "NatureEffortAmpConflictNature": "El refuerzo por esfuerzo indicado choca con la naturaleza especificada antes.",
      "NatureAmpNoPlus": "Falta el refuerzo «+» de la naturaleza.",
      "NatureAmpNoMinus": "Falta el refuerzo «-» de la naturaleza."
    }
    """;

    /// <summary>Los textos de las líneas no válidas (<c>BattleTemplateParseErrorLocalization.Get(CurrentLanguage)</c> en PKHeX). Para «es», la traducción de arriba mientras el Core siga con el inglés.</summary>
    public static BattleTemplateParseErrorLocalization LocalizacionErrores(string idioma)
    {
        var propia = BattleTemplateParseErrorLocalization.Get(idioma);
        if (idioma != "es") return propia;
        var ctx = BattleTemplateParseErrorLocalizationContext.Default.BattleTemplateParseErrorLocalization;
        var ingles = BattleTemplateParseErrorLocalization.Get("en");
        if (JsonSerializer.Serialize(propia, ctx) != JsonSerializer.Serialize(ingles, ctx)) return propia;   // el Core ya lo traduce
        return JsonSerializer.Deserialize(JsonErroresEs, ctx)!;
    }

    /// <summary>El aviso de líneas no válidas tal y como lo da PKHeX: «Líneas no válidas detectadas:» y cada línea en la suya (<c>Alert(MsgSimulatorInvalid, lines)</c>).</summary>
    public static string TextoLineasNoValidas(ResultadoImportarSet r) => MessageStrings.MsgSimulatorInvalid + Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, r.LineasNoValidas);

    /// <summary>
    /// <c>PKMEditor.LoadShowdownSetDefault</c>: <c>pk.ApplySetDetails(set)</c> sobre una COPIA del Pokémon. No fuerza nada: lo que el set no pueda expresar queda como estaba y la legalidad es la que salga
    /// (regla 1). Toma el cerrojo de legalidad (<c>ApplySetDetails</c> analiza el Pokémon): llamar siempre desde un hilo que pueda esperar, nunca desde el de la interfaz.
    /// </summary>
    public static PKM Aplicar(PKM original, ShowdownSet set)
    {
        var copia = original.Clone();
        lock (Pokekeep.Sprites.LegalityGate.Cerrojo) copia.ApplySetDetails(set);
        return copia;
    }

    // ---------------------------------------------------------------------------------------------------------------- exportar
    /// <summary><c>Main.ClickShowdownExportPKM</c>: <c>ShowdownParsing.GetShowdownText(pk, Settings.BattleTemplate.Export.GetSettings(...))</c>.</summary>
    public static string Exportar(PKM pk, AjustesPkhex a) => ShowdownParsing.GetShowdownText(pk, Exportacion(a, pk.Context));

    /// <summary><c>SAVEditor.ExportShowdownText</c>: los sets de una lista (los huecos se saltan), separados por una línea en blanco. Vacío si no hay ninguno.</summary>
    public static string ExportarLista(IEnumerable<PKM> pokemon, EntityContext contexto, AjustesPkhex a)
        => ShowdownParsing.GetShowdownSets(pokemon, Environment.NewLine + Environment.NewLine, Exportacion(a, contexto));

    /// <summary><c>ClickShowdownExportParty</c>: <c>sav.PartyData</c>. Null si la partida no tiene equipo (PKHeX oculta la entrada).</summary>
    public static string? ExportarEquipo(SaveFile sav, AjustesPkhex a) => sav.HasParty ? ExportarLista(sav.PartyData, sav.Context, a) : null;

    /// <summary><c>ClickShowdownExportCurrentBox</c>: <c>sav.GetBoxData(box)</c>. Null si la partida no tiene cajas.</summary>
    public static string? ExportarCaja(SaveFile sav, int caja, AjustesPkhex a) => sav.HasBox ? ExportarLista(sav.GetBoxData(caja), sav.Context, a) : null;

    /// <summary><c>ClickShowdownExportCurrentBox</c> con Ctrl: <c>sav.BoxData</c>, todas las cajas. Null si la partida no tiene cajas.</summary>
    public static string? ExportarTodasLasCajas(SaveFile sav, AjustesPkhex a) => sav.HasBox ? ExportarLista(sav.BoxData, sav.Context, a) : null;

    /// <summary>El aviso de éxito de PKHeX para un equipo (<c>MsgSimulatorExportParty</c>) o una lista de cajas (<c>MsgSimulatorExportList</c>).</summary>
    public static string AvisoExito(bool equipo) { TextosNucleo.Preparar(); return equipo ? MessageStrings.MsgSimulatorExportParty : MessageStrings.MsgSimulatorExportList; }

    /// <summary>El aviso de éxito al exportar UN Pokémon: <c>MsgSimulatorExportSuccess</c> («Set de Showdown exportado al portapapeles:») y el texto debajo, como el cuadro de PKHeX.</summary>
    public static string AvisoExitoPokemon(string texto) { TextosNucleo.Preparar(); return MessageStrings.MsgSimulatorExportSuccess + Environment.NewLine + Environment.NewLine + texto; }

    // ---------------------------------------------------------------------------------------------------------------- resumen de una casilla («hover»)
    /// <summary>
    /// El texto que PKHeX enseña al pasar el ratón por una casilla (<c>SummaryPreviewer.Show</c>, rama de texto): el resumen con los ajustes <c>BattleTemplate.Hover</c>, con la primera línea (especie) siempre, y las
    /// estadísticas extra (<c>PokePreview.TryAppendOtherStats</c>: GV de Let's Go / Leyendas, AV, y los puntos de Campeones si el ajuste lo pide).
    /// </summary>
    public static string TextoVista(PKM pk, AjustesPkhex a)
    {
        if (pk.Species == 0) return string.Empty;
        var ajustes = Vista(a, pk.Context);
        if (!ajustes.IsTokenInExport(BattleTemplateToken.FirstLine))
        {
            var orden = new BattleTemplateToken[ajustes.Order.Length + 1];
            ajustes.Order.CopyTo(orden.AsSpan(1));
            orden[0] = BattleTemplateToken.FirstLine;
            ajustes = ajustes with { Order = orden };
        }
        var texto = ShowdownParsing.GetLocalizedPreviewText(pk, ajustes);
        AnadirOtrasEstadisticas(pk, ref texto, ajustes);
        return texto;
    }

    /// <summary><c>PokePreview.TryAppendOtherStats</c> (PKHeX.WinForms/Controls/Slots/PokePreview.cs:398): GV (Ganbaru), AV (Awakened) y puntos de Campeones.</summary>
    private static void AnadirOtrasEstadisticas(PKM pk, ref string linea, in BattleTemplateExportSettings s)
    {
        if (pk is IGanbaru g)
        {
            Span<byte> v = stackalloc byte[6];
            g.GetGVs(v);
            Anadir<byte>(v, ref linea, s, BattleTemplateToken.GVs, s.StatsOther);
        }
        if (pk is IAwakened aw)
        {
            Span<byte> v = stackalloc byte[6];
            aw.GetAVs(v);
            Anadir<byte>(v, ref linea, s, BattleTemplateToken.AVs, s.StatsOther);
        }
        if (!s.IsTokenInExport(BattleTemplateToken.SPs) && s.IncludeChampionsStatPoints)
        {
            Span<int> evs = stackalloc int[6];
            pk.GetEVs(evs);
            EffortValues.ConvertToChampions(evs, evs);
            Anadir<int>(evs, ref linea, s, BattleTemplateToken.SPs, s.StatsSPs);
        }
    }

    private static void Anadir<T>(ReadOnlySpan<T> estadisticas, ref string linea, in BattleTemplateExportSettings s, BattleTemplateToken token, StatDisplayStyle estilo) where T : unmanaged, IEquatable<T>
    {
        var localizacion = s.Localization;
        var nombres = localizacion.Config.GetStatDisplay(estilo);
        var valor = ShowdownSet.GetStringStats(estadisticas, default, nombres);
        if (valor.Length == 0) return;
        linea += Environment.NewLine + localizacion.Config.Push(token, valor);
    }
}
