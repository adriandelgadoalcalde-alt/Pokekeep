// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.ComponentModel;
using System.Reflection;
using System.Text.Json.Serialization;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;
using Pokekeep.Mobile.Ui.Partida;
using Pokekeep.Mobile.Ui.Partida.Formularios;

namespace Pokekeep.Mobile.Ui.Ajustes.Pkhex;

/// <summary>Qué tipo de control necesita un ajuste (sale del tipo de la propiedad, nunca de una lista escrita a mano).</summary>
public enum TipoAjustePkhex
{
    /// <summary>Agrupa a otros (una clase de ajustes): no se edita, tiene fila en el censo.</summary>
    Contenedor,
    Interruptor, Entero, Decimal, Opcion, Texto,
    /// <summary>Lista de textos (rutas extra, extensiones, propiedades del informe).</summary>
    ListaTexto,
    /// <summary>Lista ordenada de valores de una enumeración (el orden de los elementos de un set).</summary>
    ListaOpciones,
    /// <summary>Texto <c>#rrggbb</c> (los colores de <c>DrawConfig</c>).</summary>
    Color,
    Punto,
    /// <summary>Un objeto con editor propio (las etiquetas de estadísticas personalizadas de Showdown): lo implementa el bloque dueño.</summary>
    Especial,
}

/// <summary>Un nodo del árbol de ajustes: una hoja editable o un contenedor. <see cref="Ruta"/> es la del censo (<c>SlotWrite.SetUpdateDex</c>).</summary>
public sealed record NodoAjustePkhex(string Ruta, string Nombre, IReadOnlyList<PropertyInfo> Cadena, Type Tipo, TipoAjustePkhex Clase, string Grupo, bool Oculto)
{
    public string Raiz => Ruta.Contains('.') ? Ruta[..Ruta.IndexOf('.')] : Ruta;
    public bool EsHoja => Clase != TipoAjustePkhex.Contenedor;
    public PropertyInfo Propiedad => Cadena[^1];

    /// <summary>El objeto que posee la propiedad (para leer y escribir), recorriendo la cadena desde la raíz.</summary>
    public object? Padre(AjustesPkhex a)
    {
        object? o = a;
        for (var i = 0; i < Cadena.Count - 1; i++) { o = Cadena[i].GetValue(o); if (o is null) return null; }
        return o;
    }

    public object? Leer(AjustesPkhex a) => Padre(a) is { } p ? Propiedad.GetValue(p) : null;

    public void Escribir(AjustesPkhex a, object? valor)
    {
        if (Padre(a) is not { } p) throw new InvalidOperationException($"No hay objeto para «{Ruta}».");
        Propiedad.SetValue(p, valor);
    }
}

/// <summary>
/// El registro de TODOS los ajustes de PKHeX del móvil, construido por reflexión sobre <see cref="AjustesPkhex"/> (una fila por propiedad, contenedores incluidos). La prueba generada desde el censo
/// (<c>AjustesPkhexTests</c>) exige que cada ruta <c>PK-SET-*</c> exista aquí y que cada hoja tenga grupo de pantalla; los textos salen de <c>lang_es.txt</c> (etiqueta <c>PropertyGrid.&lt;propiedad&gt;</c>,
/// descripción <c>LocalizedDescription.&lt;propiedad&gt;</c>) y del respaldo con motivo de <see cref="RespaldoAjustesPkhex"/>; nunca se inventa una traducción.
/// </summary>
public static class RegistroAjustesPkhex
{
    public const string GrupoInterno = "Interno";

    /// <summary>Orden de los grupos en «Más &gt; Ajustes» (el del censo: lo general primero, lo de cada herramienta después).</summary>
    public static readonly string[] OrdenGrupos =
    [
        "General", "Idioma", "Apariencia", "Almacenamiento", "Copias de seguridad", "Privacidad", "Escritura de ranuras", "Importación", "Conversión", "Guardados antiguos", "Editor",
        "Visualización", "Legalidad", "Sprites", "Detalle de ranura", "Sonido", "Showdown", "Base de datos PKM", "Base de datos de encuentros", "Base de datos de regalos", "Informe",
        "Exportación", "Avanzado",
    ];

    public static string NombreGrupo(string grupo) => IdiomaInterfaz.T("Ajustes.Pkhex.Grupo." + Array.IndexOf(OrdenGrupos, grupo));

    private static readonly PorIdioma<IReadOnlyList<NodoAjustePkhex>> _nodos = new(Construir);
    private static readonly PorIdioma<IReadOnlyDictionary<string, NodoAjustePkhex>> _porRuta = new(() => Nodos.ToDictionary(n => n.Ruta, StringComparer.Ordinal));

    public static IReadOnlyList<NodoAjustePkhex> Nodos => _nodos.Value;
    public static IReadOnlyList<NodoAjustePkhex> Hojas => [.. Nodos.Where(n => n.EsHoja)];
    public static NodoAjustePkhex? Buscar(string ruta) => _porRuta.Value.GetValueOrDefault(ruta);

    private static IReadOnlyList<NodoAjustePkhex> Construir()
    {
        var lista = new List<NodoAjustePkhex>();
        Recorrer(typeof(AjustesPkhex), [], string.Empty, lista, 0);
        return lista;
    }

    private static void Recorrer(Type tipo, List<PropertyInfo> cadena, string prefijo, List<NodoAjustePkhex> salida, int profundidad)
    {
        foreach (var p in tipo.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!p.CanRead || !p.CanWrite || p.GetIndexParameters().Length > 0 || p.GetCustomAttribute<JsonIgnoreAttribute>() is not null) continue;
            var c = new List<PropertyInfo>(cadena) { p };
            var ruta = prefijo.Length == 0 ? p.Name : prefijo + "." + p.Name;
            var clase = Clasificar(p.PropertyType);
            var oculto = p.GetCustomAttribute<BrowsableAttribute>() is { Browsable: false };
            if (clase == TipoAjustePkhex.Contenedor)
            {
                salida.Add(new NodoAjustePkhex(ruta, p.Name, c, p.PropertyType, clase, GrupoDe(ruta), oculto));
                if (profundidad < 6) Recorrer(p.PropertyType, c, ruta, salida, profundidad + 1);
            }
            else salida.Add(new NodoAjustePkhex(ruta, p.Name, c, p.PropertyType, clase, GrupoDe(ruta), oculto));
        }
    }

    private static string GrupoDe(string ruta) => GruposPkhex.PorRuta.TryGetValue(ruta, out var g) ? g : GrupoInterno;

    private static TipoAjustePkhex Clasificar(Type t)
    {
        if (t == typeof(bool)) return TipoAjustePkhex.Interruptor;
        if (t == typeof(int) || t == typeof(uint) || t == typeof(byte) || t == typeof(ushort) || t == typeof(short) || t == typeof(sbyte)) return TipoAjustePkhex.Entero;
        if (t == typeof(float) || t == typeof(double)) return TipoAjustePkhex.Decimal;
        if (t.IsEnum) return TipoAjustePkhex.Opcion;
        if (t == typeof(string)) return TipoAjustePkhex.Texto;
        if (t == typeof(List<string>) || t == typeof(string[])) return TipoAjustePkhex.ListaTexto;
        if (t.IsArray && t.GetElementType() is { IsEnum: true }) return TipoAjustePkhex.ListaOpciones;
        if (t == typeof(PuntoPkhex)) return TipoAjustePkhex.Punto;
        if (t == typeof(StatDisplayConfig)) return TipoAjustePkhex.Especial;
        return TipoAjustePkhex.Contenedor;
    }

    /// <summary>Un color es un texto con ese nombre de propiedad en <see cref="DibujoPkhex"/> (las tres propiedades de color de <c>DrawConfig</c>).</summary>
    public static bool EsColor(NodoAjustePkhex n) => n.Cadena.Count == 2 && n.Cadena[0].PropertyType == typeof(DibujoPkhex) && n.Nombre is "InvalidSelection" or "MarkBlue" or "MarkPink";

    public static TipoAjustePkhex ClaseEfectiva(NodoAjustePkhex n) => EsColor(n) ? TipoAjustePkhex.Color : n.Clase;

    // ------------------------------------------------------------------------------------------------------ textos
    /// <summary>Etiqueta de la fila en castellano: <c>PropertyGrid.&lt;propiedad&gt;</c> de <c>lang_es.txt</c>, o el respaldo; null si ni el fichero ni el respaldo la traen.</summary>
    public static string? Etiqueta(NodoAjustePkhex n)
        => ProgramaPkhex.Idioma == "en"
            ? Limpio(EtiquetasLang.Obtener("PropertyGrid." + n.Nombre) ?? n.Nombre)
            : Limpio(RespaldoAjustesPkhex.Etiquetas.GetValueOrDefault(n.Ruta) ?? RespaldoAjustesPkhex.Etiquetas.GetValueOrDefault(n.Nombre) ?? EtiquetasLang.Obtener("PropertyGrid." + n.Nombre));

    /// <summary>Descripción en castellano: <c>LocalizedDescription.&lt;propiedad&gt;</c> de <c>lang_es.txt</c>, o el respaldo; null si no hay ninguna.</summary>
    public static string? Descripcion(NodoAjustePkhex n)
        => ProgramaPkhex.Idioma == "en"
            ? Limpio(EtiquetasLang.Obtener("LocalizedDescription." + n.Nombre) ?? DescripcionOriginal(n)) ?? RespaldosAjustesIngles.Descripcion(n)
            : Limpio(RespaldoAjustesPkhex.Descripciones.GetValueOrDefault(n.Ruta) ?? RespaldoAjustesPkhex.Descripciones.GetValueOrDefault(n.Nombre) ?? EtiquetasLang.Obtener("LocalizedDescription." + n.Nombre));

    /// <summary>La descripción en inglés del código de PKHeX (para detectar traducciones que se quedaron en inglés: no se enseña nunca).</summary>
    public static string? DescripcionOriginal(NodoAjustePkhex n) => n.Propiedad.GetCustomAttribute<LocalizedDescriptionAttribute>()?.Fallback;

    private static string? Limpio(string? t) => string.IsNullOrWhiteSpace(t) ? null : t.Trim();

    // ------------------------------------------------------------------------------------------------------ rangos de los enteros
    /// <summary>
    /// Mínimo y máximo de un entero. Se SONDEA la propiedad en un objeto nuevo: si su <c>set</c> recorta (<c>Math.Clamp</c> del Core: filas de la cuadrícula 5-20, recientes 1-1000), el rango es el real;
    /// si no recorta, el del tipo (y <see cref="LimiteTopeEnteros"/> como tope para que un contador sin límite tenga un máximo razonable en el paso a paso).
    /// </summary>
    public static (long Min, long Max) Rango(NodoAjustePkhex n)
    {
        var (tmin, tmax) = n.Tipo == typeof(byte) ? (0L, 255L) : n.Tipo == typeof(uint) ? (0L, uint.MaxValue) : n.Tipo == typeof(ushort) ? (0L, ushort.MaxValue) : (int.MinValue, (long)int.MaxValue);
        var prueba = new AjustesPkhex();
        long min = tmin, max = tmax;
        try
        {
            n.Escribir(prueba, Convert.ChangeType(tmin, n.Tipo)); min = Convert.ToInt64(n.Leer(prueba));
            n.Escribir(prueba, Convert.ChangeType(tmax, n.Tipo)); max = Convert.ToInt64(n.Leer(prueba));
        }
        catch (Exception ex) when (ex is OverflowException or InvalidCastException or TargetInvocationException) { }
        if (min == tmin && n.Tipo == typeof(int)) min = 0;                      // un contador sin límite no baja de cero en el paso a paso
        if (max == tmax) max = Math.Min(max, LimiteTopeEnteros);
        return (min, max);
    }

    public const long LimiteTopeEnteros = 1_000_000;
}
