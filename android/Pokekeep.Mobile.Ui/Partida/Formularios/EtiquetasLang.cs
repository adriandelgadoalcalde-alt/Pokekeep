// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Collections.Concurrent;

namespace Pokekeep.Mobile.Ui.Partida.Formularios;

/// <summary>
/// Lector de las etiquetas de los formularios de PKHeX en castellano (<c>lang_es.txt</c>, 3.387 lineas mas las de Pokekeep): sirve <c>Formulario.Control = texto</c> igual que <c>WinFormsTranslator</c>
/// (una linea por clave, separador el primer «=», la primera linea de una clave manda). Reglas de la F8 (ADR 0062):
/// <list type="bullet">
/// <item>Una clave que no esta en el fichero devuelve <c>null</c> en <see cref="Obtener"/>: nunca se inventa una traduccion.</item>
/// <item>Lo que PKHeX no traduce (o deja en ingles: <c>L_Caption=Caption:</c>) se escribe A MANO en los respaldos por generacion (<see cref="RespaldoComun"/> y los de cada bloque), con la misma clave, y manda sobre el fichero.</item>
/// <item>Los formularios piden sus textos con <see cref="Requerir"/>: si no hay ni fichero ni respaldo, el texto sale como el nombre del control (visible a simple vista) y la clave queda en <see cref="Faltantes"/>;
/// la prueba de localizacion exige que esa lista este VACIA para todos los formularios declarados.</item>
/// </list>
/// </summary>
public static class EtiquetasLang
{
    private static readonly ConcurrentDictionary<string, byte> _faltantes = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, byte> _usadas = new(StringComparer.Ordinal);
    private static readonly Lazy<IReadOnlyDictionary<string, string>> _respaldos = new(UnirRespaldos);

    /// <summary>Todos los respaldos a mano; cada bloque de la F8 tiene el suyo (un fichero por bloque, para no pisarse al fusionar).</summary>
    private static IReadOnlyDictionary<string, string> UnirRespaldos()
    {
        var d = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var fuente in new[] { RespaldoComun.Textos, RespaldoGen1a3.Textos, RespaldoGen4.Textos, RespaldoGen5.Textos, RespaldoGen6y7.Textos, RespaldoGen8y9.Textos })
            foreach (var kv in fuente)
                if (!d.TryAdd(kv.Key, kv.Value)) throw new InvalidOperationException($"El respaldo de etiquetas repite la clave «{kv.Key}».");
        return d;
    }

    /// <summary>Numero de claves de <c>lang_es.txt</c> cargadas.</summary>
    public static int Total => TextosPkhex.Diccionario.Count;

    /// <summary>Respaldos escritos a mano (clave y texto).</summary>
    public static IReadOnlyDictionary<string, string> Respaldos => _respaldos.Value;

    /// <summary>La linea exacta del fichero (<c>SAV_Misc3.L_BP</c> da «PB:»), o null si la clave no existe. No mira los respaldos.</summary>
    public static string? Obtener(string clave) => TextosPkhex.Diccionario.TryGetValue(clave, out var t) ? t : null;

    /// <summary>Titulo del formulario (<c>SAV_Misc3</c> da «Editor de Varios»), o null.</summary>
    public static string? Formulario(string formulario) => Limpiar(Obtener(formulario));

    /// <summary>Texto de un control (<c>("SAV_Misc3","L_BP")</c> da «PB»), sin los dos puntos finales; el respaldo a mano manda sobre el fichero. Null si no hay ninguno de los dos.</summary>
    public static string? Control(string formulario, string control)
    {
        var clave = $"{formulario}.{control}";
        return Resolver(clave, Respaldos);
    }

    /// <summary>La regla de <see cref="Control"/> con unos respaldos dados (para probarla): el respaldo manda sobre el fichero y, si no hay ninguno de los dos, null.</summary>
    internal static string? Resolver(string clave, IReadOnlyDictionary<string, string> respaldos)
    {
        if (Pokekeep.Mobile.Ui.Ajustes.Pkhex.ProgramaPkhex.Idioma != "en")
            return Limpiar(respaldos.TryGetValue(clave, out var r) ? r : Obtener(clave));
        // Original English always wins. Only documented mobile additions may supply a missing caption.
        if (Obtener(clave) is { } original) return Limpiar(original);
        return Limpiar(respaldos.ContainsKey(clave) && Localizacion.ServicioIdioma.Recursos("en").TryGetValue("Form.Respaldo." + clave, out var ingles) ? ingles : null);
    }

    /// <summary>Todos los controles que el fichero traduce de un formulario (clave del control y texto sin limpiar), por orden de aparicion.</summary>
    public static IReadOnlyList<KeyValuePair<string, string>> ControlesDe(string formulario)
    {
        var prefijo = formulario + ".";
        return [.. TextosPkhex.Diccionario.Where(kv => kv.Key.StartsWith(prefijo, StringComparison.Ordinal)).Select(kv => new KeyValuePair<string, string>(kv.Key[prefijo.Length..], kv.Value))];
    }

    /// <summary>
    /// El texto que un formulario enseña para un control. Si no esta ni en el fichero ni en el respaldo, devuelve el nombre del control y anota la clave en <see cref="Faltantes"/> (nunca lanza:
    /// la aplicacion no debe caerse por una etiqueta; la prueba de localizacion es quien la detecta).
    /// </summary>
    public static string Requerir(string formulario, string control)
    {
        _usadas.TryAdd($"{formulario}.{control}", 0);
        if (Control(formulario, control) is { } t) return t;
        _faltantes.TryAdd($"{formulario}.{control}", 0);
        return control;
    }

    /// <summary>Igual que <see cref="Requerir(string,string)"/> para el titulo del formulario.</summary>
    public static string RequerirFormulario(string formulario)
    {
        _usadas.TryAdd(formulario, 0);
        if (Formulario(formulario) is { } t) return t;
        _faltantes.TryAdd(formulario, 0);
        return formulario;
    }

    /// <summary>Claves pedidas con <see cref="Requerir(string,string)"/> que no existen en el fichero ni en los respaldos (vacia = todo localizado).</summary>
    public static IReadOnlyCollection<string> Faltantes => [.. _faltantes.Keys.OrderBy(k => k, StringComparer.Ordinal)];

    /// <summary>Todas las claves que algun formulario ha pedido con <see cref="Requerir(string,string)"/> (para que la prueba de localizacion las compare con el original en ingles).</summary>
    public static IReadOnlyCollection<string> Usadas => [.. _usadas.Keys.OrderBy(k => k, StringComparer.Ordinal)];

    public static void LimpiarFaltantes() => _faltantes.Clear();

    private static string? Limpiar(string? t)
    {
        if (string.IsNullOrWhiteSpace(t)) return null;
        t = t.Trim().TrimEnd(':').Trim();
        return t.Length == 0 ? null : t.Replace("&", string.Empty);   // «&Abrir»: el acelerador de Windows no existe en el movil
    }
}
