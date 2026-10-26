// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Globalization;
using System.Reflection;
using System.Text;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Herramientas.Informe;

/// <summary>
/// Una columna del informe: la propiedad de PKHeX (<see cref="Propiedad"/>: el nombre que PKHeX usa como cabecera de la rejilla y del CSV, y el que se escribe en <c>Report.ExtraProperties</c>/<c>HiddenProperties</c>),
/// su título en castellano y si es una columna extra (<c>Report.ExtraProperties</c>, leída con el <c>BatchPropertyProvider</c> del editor por lotes).
/// </summary>
public sealed record ColumnaInforme(string Propiedad, string Titulo, bool Extra, bool Numerica);

/// <summary>
/// Una fila del informe: el Pokémon (copia leída de la partida), de dónde sale y los valores de cada columna tal y como los da PKHeX (<see cref="Valores"/>, en el orden de <see cref="ResultadoInforme.Columnas"/>;
/// null = la celda vacía de una columna extra que este Pokémon no tiene).
/// </summary>
public sealed record FilaInforme(PKM Entidad, ISlotInfo Origen, string Donde, IReadOnlyList<string?> Valores)
{
    public string? Valor(int columna) => Valores[columna];
}

/// <summary>El informe completo: columnas (las de <c>EntitySummary</c> + las extra que alguna fila tiene), cuáles se ocultan (<c>Report.HiddenProperties</c>) y las filas.</summary>
public sealed record ResultadoInforme(IReadOnlyList<ColumnaInforme> Columnas, IReadOnlySet<string> Ocultas, IReadOnlyList<FilaInforme> Filas)
{
    public int Indice(string propiedad)
    {
        for (var i = 0; i < Columnas.Count; i++) if (Columnas[i].Propiedad == propiedad) return i;
        return -1;
    }

    /// <summary>
    /// Columnas que se ven en pantalla: todas menos las ocultas, como <c>HideSpecifiedColumns</c>, que busca por nombre SIN distinguir mayúsculas (<c>DataGridViewColumnCollection[string]</c>) y se aplica ANTES de
    /// añadir las extra (una columna extra nunca se oculta).
    /// </summary>
    public IEnumerable<int> Visibles => Enumerable.Range(0, Columnas.Count).Where(i => Columnas[i].Extra || !Ocultas.Contains(Columnas[i].Propiedad));
}

/// <summary>
/// «Informe de la caja» de PKHeX (<c>Menu_Report</c> → <c>ReportGrid</c>, PK-SUB-006, PK-MENU-017, PK-PKME-013, PK-IO-042): una fila por Pokémon de la partida (<c>SlotInfoLoader.AddFromSaveFile</c>: cajas, equipo y
/// casillas extra) con las columnas de <c>EntitySummary</c> (lo que enseña <c>EntitySummaryImage</c> salvo el sprite, que en el móvil pinta la tabla) más las de <c>Report.ExtraProperties</c>, y sin las de
/// <c>Report.HiddenProperties</c>. SOLO LEE: trabaja sobre copias de los Pokémon (<c>ISlotInfo.Read</c>), la partida queda byte a byte igual. Los textos (especie, naturaleza, movimientos, objeto, lugares) salen
/// en castellano, como en PKHeX con el idioma «es».
/// </summary>
public static class ModeloInforme
{
    /// <summary>
    /// Las propiedades de <c>EntitySummary</c> en el orden de PKHeX (la rejilla las genera en el orden de declaración; «Sprite» es la de <c>EntitySummaryImage</c> que el CSV se salta). Se leen por reflexión:
    /// si PKHeX añade una, sale sola y la prueba de títulos exige su nombre en castellano.
    /// </summary>
    public static IReadOnlyList<PropertyInfo> PropiedadesResumen { get; } =
        [.. typeof(EntitySummary).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.GetIndexParameters().Length == 0).OrderBy(p => p.MetadataToken)];

    /// <summary>Títulos en castellano de las columnas de <c>EntitySummary</c> (PKHeX no traduce las cabeceras de esta rejilla: enseña el nombre de la propiedad).</summary>
    // PKHeX ReportGrid exposes the original property identifiers in English.
    // Spanish headings are Pokekeep's terminology; neither language translates game data here.
    private static IReadOnlyDictionary<string, string> ClavesTitulo => new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Position"] = "Informe.Columna.Position",
        ["Nickname"] = "Informe.Columna.Nickname",
        ["Species"] = "Informe.Columna.Species",
        ["Nature"] = "Informe.Columna.Nature",
        ["Gender"] = "Informe.Columna.Gender",
        [IdiomaInterfaz.T("Informe.Columna.ESV")] = "Informe.Columna.ESV",
        ["HP_Type"] = "Informe.Columna.HP_Type",
        ["Ability"] = "Informe.Columna.Ability",
        ["Move1"] = "Informe.Columna.Move1",
        ["Move2"] = "Informe.Columna.Move2",
        ["Move3"] = "Informe.Columna.Move3",
        ["Move4"] = "Informe.Columna.Move4",
        ["HeldItem"] = "Informe.Columna.HeldItem",
        ["HP"] = "Informe.Columna.HP",
        ["ATK"] = "Informe.Columna.ATK",
        ["DEF"] = "Informe.Columna.DEF",
        ["SPA"] = "Informe.Columna.SPA",
        ["SPD"] = "Informe.Columna.SPD",
        ["SPE"] = "Informe.Columna.SPE",
        ["MetLoc"] = "Informe.Columna.MetLoc",
        ["EggLoc"] = "Informe.Columna.EggLoc",
        ["Ball"] = "Informe.Columna.Ball",
        ["OT"] = "Informe.Columna.OT",
        ["Version"] = "Informe.Columna.Version",
        ["OTLang"] = "Informe.Columna.OTLang",
        [IdiomaInterfaz.T("Informe.Columna.Legal")] = "Informe.Columna.Legal",
        ["EncounterType"] = "Informe.Columna.EncounterType",
        ["EC"] = "Informe.Columna.EC",
        [IdiomaInterfaz.T("Catalogo.PID")] = "Informe.Columna.PID",
        ["IV_HP"] = "Informe.Columna.IV_HP",
        ["IV_ATK"] = "Informe.Columna.IV_ATK",
        ["IV_DEF"] = "Informe.Columna.IV_DEF",
        ["IV_SPA"] = "Informe.Columna.IV_SPA",
        ["IV_SPD"] = "Informe.Columna.IV_SPD",
        ["IV_SPE"] = "Informe.Columna.IV_SPE",
        ["EXP"] = "Informe.Columna.EXP",
        ["Level"] = "Informe.Columna.Level",
        ["EV_HP"] = "Informe.Columna.EV_HP",
        ["EV_ATK"] = "Informe.Columna.EV_ATK",
        ["EV_DEF"] = "Informe.Columna.EV_DEF",
        ["EV_SPA"] = "Informe.Columna.EV_SPA",
        ["EV_SPD"] = "Informe.Columna.EV_SPD",
        ["EV_SPE"] = "Informe.Columna.EV_SPE",
        ["Cool"] = "Informe.Columna.Cool",
        ["Beauty"] = "Informe.Columna.Beauty",
        ["Cute"] = "Informe.Columna.Cute",
        ["Smart"] = "Informe.Columna.Smart",
        ["Tough"] = "Informe.Columna.Tough",
        ["Sheen"] = "Informe.Columna.Sheen",
        ["NotOT"] = "Informe.Columna.NotOT",
        ["AbilityNum"] = "Informe.Columna.AbilityNum",
        ["GenderFlag"] = "Informe.Columna.GenderFlag",
        ["Form"] = "Informe.Columna.Form",
        ["PokerusStrain"] = "Informe.Columna.PokerusStrain",
        ["PokerusDays"] = "Informe.Columna.PokerusDays",
        ["MetLevel"] = "Informe.Columna.MetLevel",
        ["OriginalTrainerGender"] = "Informe.Columna.OriginalTrainerGender",
        ["FatefulEncounter"] = "Informe.Columna.FatefulEncounter",
        ["IsEgg"] = "Informe.Columna.IsEgg",
        ["IsNicknamed"] = "Informe.Columna.IsNicknamed",
        ["IsShiny"] = "Informe.Columna.IsShiny",
        ["TID16"] = "Informe.Columna.TID16",
        ["SID16"] = "Informe.Columna.SID16",
        [IdiomaInterfaz.T("Informe.Columna.TSV")] = "Informe.Columna.TSV",
        ["Move1_PP"] = "Informe.Columna.Move1_PP",
        ["Move2_PP"] = "Informe.Columna.Move2_PP",
        ["Move3_PP"] = "Informe.Columna.Move3_PP",
        ["Move4_PP"] = "Informe.Columna.Move4_PP",
        ["Move1_PPUp"] = "Informe.Columna.Move1_PPUp",
        ["Move2_PPUp"] = "Informe.Columna.Move2_PPUp",
        ["Move3_PPUp"] = "Informe.Columna.Move3_PPUp",
        ["Move4_PPUp"] = "Informe.Columna.Move4_PPUp",
        ["Relearn1"] = "Informe.Columna.Relearn1",
        ["Relearn2"] = "Informe.Columna.Relearn2",
        ["Relearn3"] = "Informe.Columna.Relearn3",
        ["Relearn4"] = "Informe.Columna.Relearn4",
        ["Checksum"] = "Informe.Columna.Checksum",
        ["Friendship"] = "Informe.Columna.Friendship",
        ["EggYear"] = "Informe.Columna.EggYear",
        ["EggMonth"] = "Informe.Columna.EggMonth",
        ["EggDay"] = "Informe.Columna.EggDay",
        ["MetYear"] = "Informe.Columna.MetYear",
        ["MetMonth"] = "Informe.Columna.MetMonth",
        ["MetDay"] = "Informe.Columna.MetDay",
    };
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> TitulosPorIdioma =
        new[] { "es-ES", "en" }.ToDictionary(codigo => codigo, codigo => (IReadOnlyDictionary<string, string>)ClavesTitulo.ToDictionary(
            p => p.Key, p => Localizacion.ServicioIdioma.Recursos(codigo)[p.Value], StringComparer.Ordinal), StringComparer.Ordinal);
    public static IReadOnlyDictionary<string, string> Titulos => TitulosPorIdioma[Localizacion.IdiomaInterfaz.Actual.Codigo];

    /// <summary>El proveedor de propiedades extra de <c>ReportGrid</c>: <c>BatchPropertyProvider&lt;EntityBatchEditor, PKM&gt;(EntityBatchEditor.Instance)</c>.</summary>
    private static readonly IPropertyProvider<PKM> Proveedor = new BatchPropertyProvider<EntityBatchEditor, PKM>(EntityBatchEditor.Instance);

    /// <summary>Las propiedades que se pueden pedir como columna extra (las del editor por lotes con el formato «Cualquiera»), para el selector de los ajustes.</summary>
    public static IReadOnlyList<string> PropiedadesExtraPosibles => EntityBatchEditor.Instance.Properties[0];

    /// <summary>
    /// Construye el informe como <c>MainMenuBoxReport</c> + <c>ReportGrid.PopulateData(list, extra, hide)</c>. Caro (un análisis de legalidad por Pokémon): llámalo en un hilo aparte. Toma el cerrojo de legalidad
    /// de la app (<c>LegalityGate</c>) por cada Pokémon, como el resto de lecturas del Core.
    /// </summary>
    public static ResultadoInforme Construir(SaveFile sav, IReadOnlyList<string> extra, IReadOnlyList<string> ocultas, CancellationToken cancelar = default, IProgress<AvanceTrabajo>? avance = null)
        => Construir(Instantanea(sav), extra, ocultas, cancelar, avance);

    /// <summary>
    /// Las COPIAS de todos los Pokémon de la partida (<c>SlotInfoLoader.AddFromSaveFile</c>). Es lo único que lee la partida viva: se llama en el hilo de la interfaz (ADR 0078 §9) y solo las copias pasan al trabajo
    /// en segundo plano, que no vuelve a tocar el guardado.
    /// </summary>
    public static IReadOnlyList<SlotCache> Instantanea(SaveFile sav)
    {
        var lista = new List<SlotCache>();
        SlotInfoLoader.AddFromSaveFile(sav, lista);
        return lista;
    }

    /// <summary>El informe de unas copias ya leídas (<see cref="Instantanea"/>): no toca ninguna partida, así que puede correr en segundo plano.</summary>
    public static ResultadoInforme Construir(IReadOnlyList<SlotCache> lista, IReadOnlyList<string> extra, IReadOnlyList<string> ocultas, CancellationToken cancelar = default, IProgress<AvanceTrabajo>? avance = null)
    {
        Ajustes.Pkhex.TextosNucleo.Preparar();
        var textos = GameInfo.GetStrings(Pokekeep.Mobile.Ui.Ajustes.Pkhex.ProgramaPkhex.Idioma);

        var resumenes = new List<(SlotCache Origen, EntitySummary Resumen)>(lista.Count);
        for (var i = 0; i < lista.Count; i++)
        {
            cancelar.ThrowIfCancellationRequested();
            var entrada = lista[i];
            var pk = entrada.Entity;
            if (pk.Species - 1u >= pk.MaxSpeciesID) continue;   // huecos y especies imposibles: PopulateData se las salta
            pk.Stat_Level = pk.CurrentLevel;   // como PopulateData («recalc Level»): la copia, nunca la partida
            EntitySummary r;
            lock (Pokekeep.Sprites.LegalityGate.Cerrojo) r = new Resumen(pk, textos, entrada.Identify());
            resumenes.Add((entrada, r));
            if (i % 30 == 0) avance?.Report(new AvanceTrabajo(IdiomaInterfaz.F("ModeloInforme.LeyendoPokemon", resumenes.Count), lista.Count == 0 ? null : (double)i / lista.Count * 100));
        }

        var columnas = PropiedadesResumen.Select(p => new ColumnaInforme(p.Name, TituloDe(p.Name), false, EsNumerica(p.PropertyType))).ToList();
        var valores = resumenes.Select(r => PropiedadesResumen.Select(p => ValorDe(p, r.Resumen, textos)).ToList()).ToList();

        // AddExtraColumns: una columna por propiedad extra no vacía, solo si ALGUNA fila la tiene (si no, PKHeX no la añade); el nombre de la columna es la propiedad.
        foreach (var prop in extra)
        {
            if (prop.Length == 0) continue;
            var celdas = new string?[resumenes.Count];
            var alguna = false;
            for (var i = 0; i < resumenes.Count; i++)
            {
                if (!Proveedor.TryGetProperty(resumenes[i].Resumen.Entity, prop, out var s)) continue;
                celdas[i] = s; alguna = true;
            }
            if (!alguna) continue;
            columnas.Add(new ColumnaInforme(prop, prop, true, false));
            for (var i = 0; i < resumenes.Count; i++) valores[i].Add(celdas[i]);
        }

        var filas = resumenes.Select((r, i) => new FilaInforme(r.Resumen.Entity, r.Origen.Source, Cajas.EditorCasillas.Describir(r.Origen.Source), valores[i])).ToList();
        var ocultasSet = new HashSet<string>(ocultas.Where(o => o.Length > 0), StringComparer.OrdinalIgnoreCase);
        return new ResultadoInforme(columnas, ocultasSet, filas);
    }

    public static string TituloDe(string propiedad) => Titulos.TryGetValue(propiedad, out var t) ? t : propiedad;

    private static bool EsNumerica(Type t) => t == typeof(int) || t == typeof(uint) || t == typeof(ushort) || t == typeof(byte);

    /// <summary>
    /// El valor de una propiedad de <c>EntitySummary</c> como lo escribe la rejilla de PKHeX (<c>$"{cell.Value}"</c>). Los lugares de encuentro y del huevo se piden con los textos en castellano: <c>EntitySummary</c>
    /// los saca de <c>GameInfo.Strings</c>, el idioma global del Core, que en el móvil no se cambia (ver <c>Editor/Fuentes.cs</c>); en PKHeX con el idioma «es» ese idioma es el castellano.
    /// </summary>
    private static string ValorDe(PropertyInfo p, EntitySummary r, GameStrings textos) => p.Name switch
    {
        "MetLoc" => Lugar(r.Entity, false, textos),
        "EggLoc" => Lugar(r.Entity, true, textos),
        _ => Convert.ToString(p.GetValue(r), CultureInfo.CurrentCulture) ?? string.Empty,
    };

    private static string Lugar(PKM pk, bool huevo, GameStrings textos)
        => pk.Format < 2 ? string.Empty : textos.GetLocationName(huevo, huevo ? pk.EggLocation : pk.MetLocation, pk.Format, pk.Generation, pk.Version);

    /// <summary>
    /// El CSV de PKHeX («Box Data Dump.csv», <c>ReportGrid.Export_CSV</c>): cabecera con el nombre de cada columna y una línea por fila en el orden que se ve, TODO entre comillas dobles y separado por comas, sin el
    /// sprite y CON las columnas ocultas (PKHeX recorre <c>dgData.Columns</c>, visibles o no). UTF-8 sin BOM y saltos <c>\r\n</c> (<c>WriteLineAsync</c> en Windows). Las comillas de dentro no se escapan, igual que PKHeX.
    /// </summary>
    public static string Csv(ResultadoInforme informe, IEnumerable<FilaInforme> filasEnOrden)
    {
        var sb = new StringBuilder();
        sb.Append(string.Join(",", informe.Columnas.Select(c => $"\"{c.Propiedad}\""))).Append("\r\n");
        foreach (var f in filasEnOrden) sb.Append(string.Join(",", f.Valores.Select(v => $"\"{v}\""))).Append("\r\n");
        return sb.ToString();
    }

    /// <summary>
    /// «¿Copiar como tabla formateada?» → Sí (<c>ReportGrid.ConvertTabbedToRedditTable</c>, línea a línea): los tabuladores pasan a «|» y tras la cabecera va la línea de alineación de PKHeX
    /// (<c>|:--:|:--:|…|</c>, una celda por columna). Las líneas se separan con <c>\r\n</c> (<c>Environment.NewLine</c> en Windows).
    /// </summary>
    public static string TablaReddit(string tabulado)
    {
        var lines = tabulado.Split("\r\n");
        var nuevas = new string[lines.Length + 1];
        var tabs = lines[0].Count(c => c == '\t');
        nuevas[0] = lines[0].Replace('\t', '|');
        nuevas[1] = string.Join(":--:", Enumerable.Repeat('|', tabs + 2));
        for (var i = 1; i < lines.Length; i++) nuevas[i + 1] = lines[i].Replace('\t', '|');
        return string.Join("\r\n", nuevas);
    }

    /// <summary>Los bytes del CSV tal y como los escribe PKHeX: UTF-8 SIN BOM.</summary>
    public static byte[] BytesCsv(string csv) => new UTF8Encoding(false).GetBytes(csv);

    /// <summary>El nombre que propone PKHeX al guardar el CSV.</summary>
    public const string NombreCsv = "Box Data Dump.csv";

    /// <summary><c>EntitySummaryImage</c> sin la imagen: el resumen de PKHeX con la posición de <c>SlotCache.Identify()</c> (el sprite lo pinta la tabla del móvil).</summary>
    private sealed class Resumen(PKM pk, GameStrings strings, string posicion) : EntitySummary(pk, strings)
    {
        public override string Position { get; } = posicion;
    }
}
