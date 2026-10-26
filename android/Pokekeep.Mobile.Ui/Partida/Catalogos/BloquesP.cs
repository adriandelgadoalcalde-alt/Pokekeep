// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Catalogos;

/// <summary>
/// Bloques del guardado (<c>SAV_Accessor</c>, Gen. 5 a 7): una pestana por bloque con sus propiedades editables, como el PropertyGrid de PKHeX. Las propiedades se descubren por reflexion sobre los bloques reales de la partida (nada inventado)
/// y los nombres son los tecnicos de PKHeX. Cada cambio se escribe directamente en el bloque, como en el formulario (que no tiene boton de guardar).
/// </summary>
internal static class BloquesP
{
    public static readonly string[] Censo = ["PK-SAV-207", "PK-SAV-208", "PK-SAV-209"];

    public static string? NoAplica(SaveFile sav) => Accesor(sav) is not null ? null : sav switch
    {
        SAV8SWSH or SAV8LA or SAV9SV or SAV9ZA => IdiomaInterfaz.T("Form.Bloques.Cifrados"),
        _ => IdiomaInterfaz.T("Form.Bloques.NoAplica"),
    };

    private static object? Accesor(SaveFile sav) => sav switch
    {
        SAV5BW s => s.Blocks,
        SAV5B2W2 s => s.Blocks,
        SAV6XY s => s.Blocks,
        SAV6AO s => s.Blocks,
        SAV6AODemo s => s.Blocks,
        SAV7SM s => s.Blocks,
        SAV7USUM s => s.Blocks,
        SAV7b s => s.Blocks,
        _ => null,
    };

    internal static bool Simple(Type t) => t == typeof(bool) || t == typeof(byte) || t == typeof(sbyte) || t == typeof(short) || t == typeof(ushort) || t == typeof(int) || t == typeof(uint) || t == typeof(long)
        || t == typeof(float) || t == typeof(double) || t == typeof(string) || t.IsEnum;

    /// <summary>Las propiedades de los bloques que el PropertyGrid de PKHeX ensena y aqui no se editan (compuestas, anidadas, listas, tramos de bytes...): por bloque, cuantas y de que tipo.</summary>
    [RequiresUnreferencedCode("Usa reflexion para recorrer las propiedades de los bloques de la partida.")]
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Omitidas(SaveFile sav)
    {
        var res = new Dictionary<string, IReadOnlyList<string>>();
        if (Accesor(sav) is not ISaveBlockAccessor<BlockInfo> acc) return res;
        var meta = new SaveBlockMetadata<BlockInfo>(acc); var vistos = new List<object>();
        foreach (var nombre in meta.GetSortedBlockList())
        {
            object bloque = meta.GetBlock(nombre);
            if (nombre.Contains('.') || vistos.Any(v => ReferenceEquals(v, bloque))) continue;
            vistos.Add(bloque);
            var l = bloque.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanRead && !p.Name.Contains('.') && p.GetIndexParameters().Length == 0 && !Simple(p.PropertyType)).Select(p => $"{p.Name}: {p.PropertyType.Name}").Order().ToList();
            if (l.Count > 0) res[nombre] = l;
        }
        return res;
    }

    [RequiresUnreferencedCode("Usa reflexion para recorrer las propiedades de los bloques de la partida.")]
    public static EditorDefP Crear(SaveFile sav)
    {
        var acc = (ISaveBlockAccessor<BlockInfo>)Accesor(sav)!;
        var meta = new SaveBlockMetadata<BlockInfo>(acc);
        var pestanas = new List<PestanaP>();
        var todos = new List<CampoP>();
        var vistos = new List<object>();
        foreach (var nombre in meta.GetSortedBlockList())
        {
            object bloque = meta.GetBlock(nombre);
            // PKHeX lista dos veces los bloques que implementan una interfaz de forma explicita (con el nombre completo de la interfaz): aqui solo una.
            if (nombre.Contains('.') || vistos.Any(v => ReferenceEquals(v, bloque))) continue;
            vistos.Add(bloque);
            var campos = new List<CampoP>();
            foreach (var pi in bloque.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                if (!pi.CanRead || pi.Name.Contains('.') || pi.GetIndexParameters().Length != 0 || !Simple(pi.PropertyType)) continue;   // las de nombre con punto son implementaciones explicitas de interfaz: repiten otras
                var c = Campo(nombre, () => bloque, null, pi, () => todos);
                if (c is not null) { campos.Add(c); todos.Add(c); }
            }
            if (campos.Count > 0) pestanas.Add(new(nombre, nombre, [new(nombre, campos)]));
        }
        return new EditorDefP("bloques", IdiomaInterfaz.T("Form.Bloques.Titulo"), pestanas, Censo, IdiomaInterfaz.T("Form.Bloques.Descripcion"));
    }

    /// <summary>Asigna la propiedad; si el bloque no admite ese valor (una cadena de longitud fija, un rango) lo dice y la partida queda como estaba.</summary>
    private static object? Predeterminado(Type t) => t.IsValueType ? Activator.CreateInstance(t) : t == typeof(string) ? string.Empty : null;

    /// <summary>
    /// Asigna la propiedad y, si <paramref name="comprobar"/>, la RELEE: muchos setters del Core hacen un cast sin validar (un byte, un ushort) y guardan otro valor que el escrito (300 en «CurrentBox» queda en 44).
    /// Si lo leido no es lo escrito, lo dice y la partida queda como estaba (<c>EscrituraPartida.Aplicar</c> la restaura al lanzar): nunca se deja creer que se escribio lo tecleado.
    /// </summary>
    private static void Poner(PropertyInfo pi, Func<object> obtener, Action<object>? guardar, object? valor, bool comprobar = false)
    {
        try
        {
            var o = obtener(); if (!pi.DeclaringType!.IsInstanceOfType(o)) throw new InvalidOperationException(IdiomaInterfaz.F("Form.Bloques.PropiedadAusente", pi.Name)); pi.SetValue(o, valor); guardar?.Invoke(o);
            if (comprobar && obtener() is { } relectura && pi.DeclaringType.IsInstanceOfType(relectura) && !Equals(valor, pi.GetValue(relectura)))
                throw new InvalidOperationException(IdiomaInterfaz.F("Form.Bloques.ValorTruncado", pi.Name, Convert.ToString(pi.GetValue(relectura), System.Globalization.CultureInfo.InvariantCulture), Convert.ToString(valor, System.Globalization.CultureInfo.InvariantCulture)));
        }
        catch (TargetInvocationException ex) when (ex.InnerException is ArgumentException or InvalidOperationException or IndexOutOfRangeException or OverflowException or InvalidCastException)   // (InvalidCastException: las cinco propiedades de hora del dia de PlayTime9a —RawDaySeconds y CurrentDayHours/Minutes/Seconds/FractionalSeconds— escriben un double en un bloque Single)
        { throw new InvalidOperationException(IdiomaInterfaz.F("Form.Bloques.ErrorCore", pi.Name, ex.InnerException!.Message), ex.InnerException); }
    }

    /// <summary>Campo de una propiedad de un objeto de la partida (un bloque, o la apariencia de Gen. 6): <paramref name="obtener"/> da el objeto vigente y <paramref name="guardar"/> lo escribe de vuelta si es una copia.</summary>
    internal static CampoP? Campo(string bloqueNombre, Func<object> obtener, Action<object>? guardar, PropertyInfo pi, Func<List<CampoP>> hermanos, Func<string, string>? nombre = null, Func<string, (long Min, long Max)?>? limites = null)
    {
        // Muchas propiedades son partes de los mismos bytes (una bandera suelta de un campo de banderas, o un dato que se repite en dos bloques): cambiar una puede cambiar otras.
        IEnumerable<string> Afecta(ContextoP _) => hermanos().Where(h => h.Id != $"{bloqueNombre}.{pi.Name}").Select(h => h.Id);
        var id = $"{bloqueNombre}.{pi.Name}";
        var rotulo = nombre?.Invoke(pi.Name) ?? pi.Name;
        var t = pi.PropertyType;
        var lectura = pi.GetGetMethod() is not null;
        var escribible = pi.CanWrite && pi.GetSetMethod() is not null;
        object? Leer()
        {
            try { var o = obtener(); return pi.DeclaringType!.IsInstanceOfType(o) ? pi.GetValue(o) : Predeterminado(pi.PropertyType); }   // el objeto cambio de tipo (apariencia masculina <-> femenina)
            catch (TargetInvocationException ex) when (ex.InnerException is not null) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); throw; }   // la excepcion del bloque, no la de la reflexion
        }

        if (!escribible)
            return P.Info(id, rotulo, _ => Convert.ToString(Leer(), System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty);
        if (t == typeof(bool)) return P.Marca(id, rotulo, _ => (bool)Leer()!, (_, v) => Poner(pi, obtener, guardar, v, comprobar: true), afectaA: Afecta, lecturaAsimetrica: true);
        if (t == typeof(string)) return P.Texto(id, rotulo, _ => (string?)Leer() ?? string.Empty, (_, v) => Poner(pi, obtener, guardar, v), afectaA: Afecta, lecturaAsimetrica: true);
        if (t == typeof(float) || t == typeof(double))
            return P.Decimal(id, rotulo, _ => Convert.ToDouble(Leer(), System.Globalization.CultureInfo.InvariantCulture), (_, v) => Poner(pi, obtener, guardar, Convert.ChangeType(v, t, System.Globalization.CultureInfo.InvariantCulture)), -3.4e38, 3.4e38, afectaA: Afecta, lecturaAsimetrica: true);
        if (t.IsEnum)
        {
            var valores = Enum.GetValues(t).Cast<object>().Select(o => (Id: Convert.ToInt64(o, System.Globalization.CultureInfo.InvariantCulture), Nombre: o.ToString()!)).DistinctBy(x => x.Id).ToList();
            return P.Opcion(id, rotulo, _ => Convert.ToInt64(Leer(), System.Globalization.CultureInfo.InvariantCulture), (_, v) => Poner(pi, obtener, guardar, Enum.ToObject(t, v), comprobar: true),
                _ => [.. valores.Select(x => new OpcionP((int)x.Id, x.Nombre))], afectaA: Afecta, lecturaAsimetrica: true);
        }
        var (min, max) = t == typeof(byte) ? (0L, 255L) : t == typeof(sbyte) ? (-128L, 127L) : t == typeof(short) ? (-32768L, 32767L) : t == typeof(ushort) ? (0L, 65535L)
            : t == typeof(int) ? ((long)int.MinValue, (long)int.MaxValue) : t == typeof(uint) ? (0L, (long)uint.MaxValue) : (long.MinValue, long.MaxValue);
        if (limites?.Invoke(pi.Name) is { } lim) (min, max) = (Math.Max(min, lim.Min), Math.Min(max, lim.Max));   // contadores que el Core guarda en un byte sin validar (PartyCount, CurrentBox)
        return P.Entero(id, rotulo, _ => Convert.ToInt64(Leer(), System.Globalization.CultureInfo.InvariantCulture), (_, v) => Poner(pi, obtener, guardar, Convert.ChangeType(v, t, System.Globalization.CultureInfo.InvariantCulture), comprobar: true), min, max, afectaA: Afecta, lecturaAsimetrica: true);
    }
}
