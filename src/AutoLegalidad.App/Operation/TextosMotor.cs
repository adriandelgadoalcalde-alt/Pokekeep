// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using PKHeX.Core;

namespace AutoLegalidad.Ui.Operation;

/// <summary>
/// X36e: los textos que escribe el motor (<c>AutoLegalidad.Engine</c>: motivos, resúmenes de lo que hizo, avisos, nombres de campo de «Ver cambios») viajan en el contrato en castellano. El motor no se toca
/// (su hash liga la caché de soluciones); aquí se les pone el texto inglés cuando <see cref="AppIdioma"/> está en inglés. En castellano se devuelve el texto tal cual.
/// Las plantillas salen de los literales reales del motor (<c>Recursos/motor-textos-x36e.tsv</c>, que una prueba compara con el código fuente), con «{n}» donde el motor interpola; los mensajes de legalidad del Core
/// (que el motor pide en «es») se traducen con las plantillas inglesas del propio Core.
/// </summary>
public static class TextosMotor
{
    private sealed record Plantilla(Regex Es, string En, bool Entera, int Longitud, string Literal)
    {
        public string Aplicar(string texto) => Entera ? (texto == Literal ? En : texto) : Es.Replace(texto, m => Rellenar(En, m));
    }

    private static readonly Regex Marcador = new(@"\{(\d+)(?::[^}]*)?\}", RegexOptions.CultureInvariant);

    private static string Rellenar(string ingles, Match m) => Marcador.Replace(ingles, x => m.Groups["g" + x.Groups[1].Value] is { Success: true } g ? g.Value : x.Value);

    private static readonly Lazy<(IReadOnlyList<Plantilla> Plantillas, IReadOnlyDictionary<string, string> Campos)> Tablas = new(CargarMotor);
    private static readonly Lazy<IReadOnlyList<Plantilla>> Legalidad = new(CargarLegalidad);

    public static string? Traducir(string? textoMotor)
    {
        if (string.IsNullOrEmpty(textoMotor) || !AppIdioma.Ingles) return textoMotor;
        var r = textoMotor;
        foreach (var p in Tablas.Value.Plantillas) r = p.Aplicar(r);
        foreach (var p in Legalidad.Value) r = p.Aplicar(r);
        r = Severidad.Replace(r, m => m.Value switch { "Inválido" => "Invalid", "Sospechoso" => "Fishy", _ => "Valid" });
        return Estadisticas.Replace(r, m => m.Groups[1].Value + " " + NombreEstadistica(m.Groups[2].Value));
    }

    /// <summary>El nombre de un campo de «Ver cambios» (identificador del motor en castellano) en el idioma de la interfaz.</summary>
    public static string Campo(string campo)
    {
        if (!AppIdioma.Ingles) return campo;
        if (Tablas.Value.Campos.TryGetValue(campo, out var en)) return en;
        var m = Estadisticas.Match(campo);
        return m.Success && m.Length == campo.Length ? m.Groups[1].Value + " " + NombreEstadistica(m.Groups[2].Value) : campo;
    }

    // Las líneas de legalidad del Core empiezan por la severidad: «Inválido: …», «Sospechoso: …».
    private static readonly Regex Severidad = new(@"(?m)^(Inválido|Sospechoso|Válido)(?=: )", RegexOptions.CultureInvariant);

    // «IV Ataque», «EV PS»…: el motor nombra así las estadísticas en los campos y en las listas de sus avisos.
    private static readonly Regex Estadisticas = new(@"\b(IV|EV|DV|AV|GV) (PS|Ataque|Defensa|At\. Esp\.|Def\. Esp\.|Velocidad)(?![\p{L}])", RegexOptions.CultureInvariant);

    private static string NombreEstadistica(string es) => es switch
    {
        "PS" => "HP", "Ataque" => "Attack", "Defensa" => "Defense", "At. Esp." => "Sp. Atk", "Def. Esp." => "Sp. Def", "Velocidad" => "Speed", _ => es,
    };

    // ------------------------------------------------------------------------------------------------------------- carga
    private static Plantilla Construir(string es, string en)
    {
        if (!Marcador.IsMatch(es)) return new Plantilla(new Regex(Regex.Escape(es), RegexOptions.CultureInvariant), en, es.Length < 12, es.Length, es);
        var patron = new StringBuilder(); var vistos = new HashSet<string>(); var pos = 0; var ultimo = 0;
        foreach (Match m in Marcador.Matches(es))
        {
            patron.Append(Regex.Escape(es[pos..m.Index]));
            var n = m.Groups[1].Value; var final = m.Index + m.Length == es.Length;
            patron.Append(vistos.Add(n) ? (final ? $"(?<g{n}>[^\\r\\n]+?)(?=$|[\\r\\n])" : $"(?<g{n}>.+?)") : $"\\k<g{n}>");
            pos = m.Index + m.Length; ultimo = pos;
        }
        patron.Append(Regex.Escape(es[ultimo..]));
        return new Plantilla(new Regex(patron.ToString(), RegexOptions.CultureInvariant | RegexOptions.Singleline), en, false, es.Length, es);
    }

    private static string Unesc(string s)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < s.Length; i++)
            if (s[i] == '\\' && i + 1 < s.Length) { i++; sb.Append(s[i] switch { 't' => '\t', 'n' => '\n', 'r' => '\r', _ => s[i] }); }
            else sb.Append(s[i]);
        return sb.ToString();
    }

    private static (IReadOnlyList<Plantilla>, IReadOnlyDictionary<string, string>) CargarMotor()
    {
        var plantillas = new List<Plantilla>(); var campos = new Dictionary<string, string>(StringComparer.Ordinal);
        using var flujo = typeof(TextosMotor).Assembly.GetManifestResourceStream("motor-textos-x36e.tsv");
        if (flujo is null) return (plantillas, campos);
        using var lector = new StreamReader(flujo, Encoding.UTF8);
        var vistas = new HashSet<string>(StringComparer.Ordinal);
        while (lector.ReadLine() is { } linea)
        {
            if (linea.Length == 0 || linea[0] == '#') continue;
            var p = linea.Split('\t');
            if (p.Length < 4 || p[3].Length == 0) continue;
            var es = Unesc(p[2]); var en = Unesc(p[3]);
            if (p[1] == "campo") campos[es] = en;
            else if (p[1] == "usuario" && vistas.Add(es)) plantillas.Add(Construir(es, en));
        }
        // las más largas primero: «No se ha modificado nada. {0}: {1}» antes que «No se ha modificado nada.»
        plantillas.Sort((a, b) => b.Longitud.CompareTo(a.Longitud));
        return (plantillas, campos);
    }

    private static IReadOnlyList<Plantilla> CargarLegalidad()
    {
        var lista = new List<Plantilla>();
        try
        {
            var es = LegalityCheckLocalization.Get("es"); var en = LegalityCheckLocalization.Get("en");
            var vistas = new HashSet<string>(StringComparer.Ordinal);
            foreach (var prop in typeof(LegalityCheckLocalization).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(x => x.PropertyType == typeof(string)))
            {
                var a = (string?)prop.GetValue(es); var b = (string?)prop.GetValue(en);
                if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b) || a == b || a.Length < 8 || !vistas.Add(a)) continue;
                lista.Add(Construir(a, b));
            }
            lista.Sort((x, y) => y.Longitud.CompareTo(x.Longitud));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { /* sin plantillas del Core: los textos de legalidad quedan como estaban */ }
        return lista;
    }
}
