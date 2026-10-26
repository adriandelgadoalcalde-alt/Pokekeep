// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Text;

namespace AutoLegalidad.Catalog.Library;

/// <summary>
/// Hito 3c (la Libreria solo ofrece lo que el motor genera): entradas de la Libreria que el banco del Hito 3b DEMOSTRO imposibles de generar (<c>searchComplete=true</c>, con el motivo exacto
/// del Core). Se leen de <c>docs/datos/libreria-no-generables.tsv</c> (columnas <c>caseKey, categoria, motivo</c>), que genera <c>pokekeep-bench libreria-no-generables</c> a partir de los TSV del
/// banco; <see cref="LibraryBuilder"/> las deja atenuadas con ese motivo, en castellano. Nada de esto cambia la legalidad (regla 12): solo evita OFRECER lo que el motor no sabe generar.
/// </summary>
public static class LibraryBlocks
{
    public const string FileName = "libreria-no-generables.tsv";
    /// <summary>Hito 5c: lo que la interfaz real (motor real, ruta real de «Añadir a la caja») demostró que no genera; se lee junto a <see cref="FileName"/>.</summary>
    public const string ExtraFileName = "libreria-no-generables-5c.tsv";

    public const string NotInCoreMgdb = "Esta tarjeta de EventsGallery no está en la base de eventos de PKHeX: PKHeX no la reconoce y Pokekeep no puede generarla ni validarla.";
    public const string NotEnumerated = "PKHeX no enumera este encuentro para esta especie y forma en este juego: Pokekeep no puede generarlo ni validarlo.";
    public const string KoreanClassicRibbon = "Tarjeta coreana con la Cinta Clásica: PKHeX no admite que exista en una partida no coreana (esa cinta no puede viajar entre juegos coreanos e internacionales).";
    public const string FixedGender = "El origen fija el sexo del Pokémon y esta especie o forma solo existe en el otro sexo.";
    public const string Pokewalker = "No existe un Wurmple del Pokéwalker que dé esta evolución con los datos de esta partida (sus PID posibles no la producen).";
    public const string EvolveOnTrade = "Este intercambio evoluciona al Pokémon al recibirlo y PKHeX rechaza la especie sin evolucionar.";
    public const string FormNotStorable = "El formato de guardado de este juego no puede guardar esa forma.";
    public const string FancyForm = "Esta forma de Vivillon (Fantasía o Poké Ball) solo es válida como regalo misterioso.";
    public const string NeedsHome = "En este juego esta especie solo se consigue pasando por Pokémon HOME, que le asigna su matrícula; Pokekeep no puede crearla.";
    public const string NeedsHomeEvenWithSetting = "Necesita pasar por Pokémon HOME y, aun con el ajuste «sin matrícula», PKHeX no da por válido ningún Pokémon con este origen sin su matrícula real (evolución, encuentro o forma que solo admite tras pasar por HOME).";
    public const string OtherFormWithSetting = "Con el ajuste «sin matrícula» solo se obtiene una forma o un origen distinto del pedido, así que Pokekeep no lo ofrece.";
    public const string RelaxedOnly = "PKHeX solo admite esta forma con un origen distinto del de esta entrada: no se puede generar sin cambiarla.";
    public const string JapaneseTrade = "Este intercambio no existe en una partida japonesa (los motes de PKHeX para ese idioma no lo admiten).";
    public const string Generic = "PKHeX demuestra que no se puede generar con los datos de esta entrada (el detalle técnico está en el informe de cobertura).";

    /// <summary>Lo que dice el fichero: entradas BLOQUEADAS (clave de caso -> motivo en castellano) y entradas que solo existen pasando por HOME y el motor SI genera con el ajuste «sin matricula».</summary>
    public sealed record Data(IReadOnlyDictionary<string, string> Blocks, IReadOnlySet<string> HomeRequired)
    {
        public static readonly Data Empty = new(new Dictionary<string, string>(StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal));
    }

    /// <summary>Columnas: <c>caseKey, categoria, motivo, tipo</c> (<c>tipo</c>: <c>bloqueo</c> -por defecto- o <c>home</c>). Un fichero ausente equivale a ninguna entrada.</summary>
    public static Data Load(string? path)
    {
        if (path is null || !File.Exists(path)) return Data.Empty;
        var blocks = new Dictionary<string, string>(StringComparer.Ordinal);
        var home = new HashSet<string>(StringComparer.Ordinal);
        // Hito 5c: junto al fichero del banco del 3b, el de lo que NO se genera medido con la INTERFAZ real (mismo formato; el primero se regenera con el banco, este no).
        var files = new List<string> { path };
        var extra = Path.Combine(Path.GetDirectoryName(path) ?? string.Empty, ExtraFileName);
        if (File.Exists(extra)) files.Add(extra);
        foreach (var file in files)
            foreach (var line in File.ReadLines(file, Encoding.UTF8).Skip(1))
            {
                var parts = line.Split((char)9);
                if (parts.Length < 3 || parts[0].Length == 0) continue;
                if (parts.Length >= 4 && parts[3] == "home") home.Add(parts[0]);
                else if (parts[2].Length > 0) blocks[parts[0]] = parts[2];
            }
        return new Data(blocks, home);
    }

    /// <summary>
    /// Convierte el motivo tecnico de una fila del banco (texto del motor, sin tildes) en la frase en castellano de la Libreria. <paramref name="withSetting"/> indica que la fila viene de la
    /// medicion con el ajuste «sin matricula» (R-009): el motivo es entonces el de lo que sigue sin generarse AUN con el ajuste.
    /// </summary>
    public static string SpanishReason(string motivo, bool withSetting)
    {
        // Sin tildes ni mayusculas: el mismo motivo se reconoce se escriba como se escriba (los textos del motor ganan tildes con el tiempo).
        var m = Fold(motivo ?? string.Empty);
        if (m.Contains("no enumera la entrada", StringComparison.Ordinal) || m.Contains("no enumera la tarjeta", StringComparison.Ordinal)) return m.Contains("eventsgallery", StringComparison.Ordinal) ? NotInCoreMgdb : NotEnumerated;
        if (m.Contains("cinta clasica", StringComparison.Ordinal)) return KoreanClassicRibbon;
        if (m.Contains("fija el sexo", StringComparison.Ordinal)) return FixedGender;
        if (m.Contains("pokewalker", StringComparison.Ordinal)) return Pokewalker;
        if (m.Contains("evolveontrade", StringComparison.Ordinal) || m.Contains("evoluciona al pokemon al recibirlo", StringComparison.Ordinal)) return EvolveOnTrade;
        if (m.Contains("no guarda esa forma", StringComparison.Ordinal)) return FormNotStorable;
        if (m.Contains("fancy/poke ball", StringComparison.Ordinal)) return FancyForm;
        if (m.Contains("partida japonesa", StringComparison.Ordinal)) return JapaneseTrade;
        if (m.StartsWith("rasgos relajados hasta", StringComparison.Ordinal) || m.Contains("successneedshomedistinto", StringComparison.Ordinal)) return withSetting ? OtherFormWithSetting : RelaxedOnly;
        if (m.Contains("rastreador", StringComparison.Ordinal) || m.Contains("servidor de home", StringComparison.Ordinal)) return withSetting ? NeedsHomeEvenWithSetting : NeedsHome;
        return Generic;
    }

    /// <summary>Minusculas y sin marcas diacriticas (para reconocer un motivo con o sin tildes).</summary>
    public static string Fold(string text)
    {
        var d = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var ch in d)
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark) sb.Append(char.ToLowerInvariant(ch));
        return sb.ToString();
    }
}
