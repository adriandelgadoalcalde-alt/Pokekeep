// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Reflection;

namespace Pokekeep.Mobile.Ui.Partida;

/// <summary>
/// Los textos en castellano que PKHeX ya trae para sus formularios (<c>lang_es.txt</c>: etiquetas «SAV_Trainer7.L_BP=PB:», pestanas, grupos y nombres de enumeraciones): los editores de partida los usan tal cual
/// para que cada dato se llame como en PKHeX en castellano. Lo que no esta traducido en el original se queda como esta (nunca se inventa una traduccion).
/// </summary>
public static class TextosPkhex
{
    private static readonly Lazy<Dictionary<string, string>> Espanol = new(() => Cargar("es"));
    private static readonly Lazy<Dictionary<string, string>> Ingles = new(() => Cargar("en"));
    private static IReadOnlyDictionary<string, string> Tabla => DiccionarioDe(Pokekeep.Mobile.Ui.Ajustes.Pkhex.ProgramaPkhex.Idioma);
    internal static IReadOnlyDictionary<string, string> DiccionarioDe(string idioma) => idioma switch { "es" => Espanol.Value, "en" => Ingles.Value, _ => throw new ArgumentOutOfRangeException(nameof(idioma)) };

    private static Dictionary<string, string> Cargar(string idioma)
    {
        var d = new Dictionary<string, string>(StringComparer.Ordinal);
        using var s = typeof(TextosPkhex).Assembly.GetManifestResourceStream("lang_" + idioma + ".txt");
        if (s is null) return d;
        using var r = new StreamReader(s);
        while (r.ReadLine() is { } linea)
        {
            var i = linea.IndexOf('=');
            if (i <= 0) continue;
            d.TryAdd(linea[..i], linea[(i + 1)..]);   // como TranslationContext de PKHeX: la primera linea de una clave manda
        }
        return d;
    }

    public static bool Existe(string clave) => Tabla.ContainsKey(clave);

    /// <summary>Todas las lineas de <c>lang_es.txt</c> (clave exacta de PKHeX, texto tal cual). Una sola carga para todo el codigo de la aplicacion.</summary>
    internal static IReadOnlyDictionary<string, string> Diccionario => Tabla;

    /// <summary>Texto de una clave (<c>SAV_Trainer7.Tab_Overview</c>), o <paramref name="alternativa"/> si no existe. Se quitan los dos puntos finales de las etiquetas.</summary>
    public static string Texto(string clave, string? alternativa = null)
        => Tabla.TryGetValue(clave, out var t) && !string.IsNullOrWhiteSpace(t) ? t.Trim().TrimEnd(':').Trim() : alternativa ?? clave;

    /// <summary>Nombre en castellano de un miembro de enumeracion (<c>Stamp7.AkalaPokedexCompletion</c>); si PKHeX no lo traduce, el nombre del miembro con espacios.</summary>
    public static string Enum<T>(T valor) where T : struct, Enum
        => Tabla.TryGetValue($"{typeof(T).Name}.{valor}", out var t) && !string.IsNullOrWhiteSpace(t) ? t : valor.ToString().Replace('_', ' ');

    /// <summary>Todos los miembros de una enumeracion como opciones (valor numerico y nombre en castellano).</summary>
    public static IReadOnlyList<OpcionP> EnumLista<T>(Func<T, bool>? filtro = null) where T : struct, Enum
        => [.. System.Enum.GetValues<T>().Where(v => filtro?.Invoke(v) ?? true).Select(v => new OpcionP(Convert.ToInt32(v), Enum(v)))];

    /// <summary>Un archivo de datos de PKHeX incrustado (<c>Assets\Datos</c>), por ejemplo <c>fashion_m_sm</c>.</summary>
    public static byte[] Datos(string nombre)
    {
        using var s = typeof(TextosPkhex).Assembly.GetManifestResourceStream("datos/" + nombre) ?? throw new FileNotFoundException("Falta el recurso " + nombre);
        using var m = new MemoryStream(); s.CopyTo(m); return m.ToArray();
    }
}
