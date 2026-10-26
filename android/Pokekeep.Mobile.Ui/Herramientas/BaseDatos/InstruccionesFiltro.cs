// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Herramientas.BaseDatos;

/// <summary>
/// La pestaña «Avanzado» de <c>SAV_Encounters</c> y <c>SAV_MysteryGiftDB</c> (PK-SUB-012, PK-SUB-020): un texto con instrucciones de filtro del editor por lotes (<c>=Species=25</c>, <c>!IsShiny=True</c>…) y el
/// constructor de líneas (<c>EntityInstructionBuilder</c> en modo solo lectura: solo prefijos de FILTRO, nunca <c>.</c> de asignar). Las MISMAS llamadas del Core que el formulario: <c>StringInstructionSet.HasEmptyLine</c>,
/// <c>StringInstruction.GetFilters</c>, <c>EntityBatchEditor.ScreenStrings</c> y <c>BatchEditingUtil.IsFilterMatch</c>.
/// </summary>
public static class InstruccionesFiltro
{
    /// <summary>Como el formulario: el texto filtra si no está vacío y no tiene líneas en blanco (con una línea en blanco PKHeX no filtra nada).</summary>
    public static bool Activas(string? texto) => !string.IsNullOrEmpty(texto) && !StringInstructionSet.HasEmptyLine(texto);

    /// <summary>
    /// El filtro de PKHeX para el texto, o null si no filtra (<see cref="Activas"/> es falso). <c>ScreenStrings</c> convierte los nombres («Pikachu») en números con las listas del Core, igual que en el formulario.
    /// </summary>
    public static Func<T, bool>? Crear<T>(string? texto) where T : notnull
    {
        if (!Activas(texto)) return null;
        var filtros = StringInstruction.GetFilters(texto!);
        EntityBatchEditor.ScreenStrings(filtros);
        return o => BatchEditingUtil.IsFilterMatch(filtros, o);
    }

    /// <summary>Los prefijos que ofrece el constructor en modo solo lectura (los de filtro, en el orden de <c>StringInstruction.Prefixes</c>).</summary>
    public static IReadOnlyList<char> Prefijos { get; } = [.. StringInstruction.Prefixes.ToArray().Where(StringInstruction.IsFilterInstruction)];

    /// <summary>Qué significa cada prefijo, en castellano (el formulario enseña solo el símbolo).</summary>
    public static string Significado(char prefijo) => prefijo switch
    {
        '=' => IdiomaInterfaz.T("InstruccionesFiltro.Igual"),
        '!' => IdiomaInterfaz.T("InstruccionesFiltro.Igual2"),
        '>' => IdiomaInterfaz.T("InstruccionesFiltro.Mayor"),
        '<' => IdiomaInterfaz.T("InstruccionesFiltro.Menor"),
        '≥' => IdiomaInterfaz.T("InstruccionesFiltro.MayorIgual"),
        '≤' => IdiomaInterfaz.T("InstruccionesFiltro.MenorIgual"),
        _ => prefijo.ToString(),
    };

    /// <summary>Las propiedades del constructor con el formato «Cualquiera» (<c>EntityBatchEditor.Instance.Properties[0]</c>, lo que el formulario carga al abrir).</summary>
    public static IReadOnlyList<string> Propiedades => EntityBatchEditor.Instance.Properties[0];

    /// <summary>La línea que añade el constructor: <c>{prefijo}{propiedad}=</c> como <c>EntityInstructionBuilder.Create</c>, más el valor si se escribe (vacío = idéntica a PKHeX).</summary>
    public static string Linea(char prefijo, string propiedad, string? valor = null) => $"{prefijo}{propiedad}{StringInstruction.SplitInstruction}{valor?.Trim()}";

    /// <summary>Añade una línea al texto como <c>B_Add_Click</c>: si ya hay texto y no acaba en salto de línea, pone uno antes.</summary>
    public static string Anadir(string? texto, string linea)
    {
        texto ??= string.Empty;
        if (texto.Length != 0 && !texto.EndsWith('\n')) texto += "\n";
        return texto + linea;
    }
}
