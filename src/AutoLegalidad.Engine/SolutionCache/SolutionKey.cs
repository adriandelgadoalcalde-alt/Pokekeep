// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using AutoLegalidad.Engine.Generation;
using AutoLegalidad.Engine.Optimization;
using PKHeX.Core;

namespace AutoLegalidad.Engine.SolutionCache;

/// <summary>
/// Clave de la cache de soluciones (R-011, ADR 0037): SHA-256 (hex mayusculas) de una cadena CANONICA con la familia, la identidad del modelo/encuentro y los CRITERIOS del Pokemon que la
/// busqueda conserva. NO entran el OT, TID, SID, apodo, requestId, plazo ni los IV actuales (la suma actual solo decide el veredicto «sin mejora»). El id32 del entrenador entra HASHEADO dentro
/// de la cadena y solo cuando el resultado depende de el (variocolor, PID forzado, regalos con SID en la semilla): nunca en claro y la cache de producto no precalcula esos casos.
/// </summary>
internal static class SolutionKey
{
    /// <summary>Version del formato de la cadena canonica: subirla invalida todas las claves (por si cambia lo que entra).</summary>
    public const int FormatVersion = 3;

    /// <summary>Clave de <see cref="SeedSpaceSearch"/>.</summary>
    public static string ForSeed(ISeedModel model, PKM pk, ITrainerInfo trainer, bool knowsHp, int[] cur, int[] relevance)
    {
        var sb = Start(SolutionFamily.Seed);
        Add(sb, "model", model.CacheIdentity);
        Common(sb, pk, knowsHp, cur, relevance, model.SeedFixesNatureGenderAbility, !model.SeedIndependentOfOrigin, model.RelevanceInKey);
        Add(sb, "hpType", knowsHp ? pk.HPType : -1);
        var ecRule = EvolutionEc.Applies(pk.Species) && pk.Format >= 6;
        Add(sb, "ecRule", ecRule ? (EvolutionEc.IsRare(pk.EncryptionConstant) ? "rare" : "common") : "none");
        if (pk is PK8 p8 && !model.SeedIndependentOfOrigin) Add(sb, "dynamax", p8.DynamaxLevel);
        if (model.DependsOnTrainerId || pk.IsShiny)
        {
            Add(sb, "idHash", IdHash(pk.ID32));
            Add(sb, "recvHash", IdHash(trainer.ID32));
        }
        return Hash(sb);
    }

    /// <summary>
    /// Clave de la entrada UNIVERSAL de un encuentro (ADR 0041): identifica el ENCUENTRO (huella del Core, IV perfectos...) y lo poco del Pokemon que condiciona la busqueda (especie y forma, que fijan la
    /// relevancia de los stats, formato y maximo de IV). NO lleva ningun rasgo del candidato (naturaleza, sexo, habilidad, Teratipo, nivel, lugar, version): cada uno es una casilla DENTRO de la entrada.
    /// </summary>
    public static string ForUniverse(IEncounterUniverseModel model, PKM pk)
    {
        var sb = Start(SolutionFamily.EncounterUniverse);
        Add(sb, "model", model.UniverseIdentity);
        Add(sb, "species", pk.Species);
        Add(sb, "form", pk.Form);
        Add(sb, "format", pk.Format);
        Add(sb, "maxIv", pk.MaxIV);
        Add(sb, "type", pk.GetType().Name);
        return Hash(sb);
    }

    /// <summary>Clave de <see cref="ClassicLcrngSearch"/>.</summary>
    public static string ForLcrng(IEncounterable enc, PIDType type, PKM pk, bool knowsHp, int[] cur, int[] relevance)
    {
        var sb = Start(SolutionFamily.Lcrng);
        Add(sb, "pidiv", type);
        Add(sb, "enc", EncounterFingerprint.Of(enc));
        Common(sb, pk, knowsHp, cur, relevance);
        // BACD_AX usa el TID del Pokemon para el PID antivariocolor; los variocolor dependen del id32.
        if (pk.IsShiny || type == PIDType.BACD_AX) Add(sb, "idHash", IdHash(pk.ID32));
        return Hash(sb);
    }

    private static StringBuilder Start(string family)
    {
        var sb = new StringBuilder(512);
        Add(sb, "fmt", FormatVersion);
        Add(sb, "family", family);
        return sb;
    }

    private static void Common(StringBuilder sb, PKM pk, bool knowsHp, int[] cur, int[] relevance, bool seedFixesNatureGenderAbility = true, bool originMatters = true, bool relevanceInKey = true)
    {
        Add(sb, "species", pk.Species);
        Add(sb, "form", pk.Form);
        Add(sb, "format", pk.Format);
        Add(sb, "maxIv", pk.MaxIV);
        // Naturaleza, sexo y habilidad solo condicionan la busqueda si la semilla los fija (Teratipo, incursiones, Z-A, LCRNG). En los salvajes de Espada/Escudo la semilla solo da EC, PID, IV y tamano
        // (Overworld8RNG.TryApplyFromSeed): la busqueda es la misma para cualquier naturaleza, sexo y habilidad, y meterlos en la clave solo fragmentaba la cache (Hito 6: un «Completar el juego» de Espada
        // acertaba el 51 % de las entradas precalculadas porque esos rasgos salen al azar en cada Pokemon nuevo).
        if (seedFixesNatureGenderAbility)
        {
            Add(sb, "nature", (int)pk.Nature);
            Add(sb, "gender", pk.Gender);
            Add(sb, "ability", pk.Ability);
        }
        Add(sb, "shiny", pk.IsShiny);
        // Con Poder Oculto conocido la busqueda solo admite cada IV con su paridad actual (el objetivo y el solver dependen de ella): la paridad entra en la clave.
        Add(sb, "knowsHp", knowsHp);
        if (knowsHp)
        {
            var parity = new StringBuilder(6);
            for (var i = 0; i < 6; i++) parity.Append((cur[i] & 1) == 0 ? '0' : '1');
            Add(sb, "parity", parity.ToString());
        }
        // ADR 0042: en los salvajes de Espada/Escudo la relevancia (desempate, depende de la naturaleza al azar de cada Pokemon nuevo) NO entra: la entrada lleva el grupo de empate completo y se reordena al servir.
        if (relevanceInKey) Add(sb, "relevance", string.Join(',', relevance));
        // El nivel, el nivel y el lugar de encuentro y la version de origen solo entran si la busqueda depende de ellos (Raid8: IsPossibleSeed los lee). En los salvajes de Espada/Escudo la semilla
        // no los mira: el resultado es el mismo para cualquier nivel, lugar o version (Espada y Escudo comparten entradas) y el Core decide despues si el candidato vale.
        if (originMatters)
        {
            Add(sb, "level", pk.CurrentLevel);
            Add(sb, "metLevel", pk.MetLevel);
            Add(sb, "metLocation", pk.MetLocation);
            Add(sb, "version", (int)pk.Version);
        }
        Add(sb, "type", pk.GetType().Name);
    }

    private static void Add(StringBuilder sb, string name, object? value)
        => sb.Append(name).Append('=').Append(Convert.ToString(value, CultureInfo.InvariantCulture)).Append('\n');

    private static string Hash(StringBuilder sb)
    {
        var canonical = sb.ToString();
        SolutionKeyDiagnostics.LastCanonical = canonical;
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    /// <summary>Hash (SHA-256 truncado a 128 bits) del id32: el valor en claro no sale de la memoria.</summary>
    private static string IdHash(uint id32) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("id32:" + id32.ToString(CultureInfo.InvariantCulture))))[..32];
}

/// <summary>Solo diagnostico (ADR 0048): la cadena canonica de la ULTIMA clave calculada en este hilo, para que un decorador de la cache (del Worker de desarrollo) explique un fallo de la cache. No afecta a nada.</summary>
public static class SolutionKeyDiagnostics
{
    [ThreadStatic] private static string? _last;
    public static string? LastCanonical { get => _last; internal set => _last = value; }
}

/// <summary>
/// Huella de un encuentro del Core: tipo concreto + valores publicos primitivos/enum/string de la instancia (campos y propiedades; las estructuras, un nivel) y, si existe, los de su
/// <c>Parent</c> (p. ej. el area de un salvaje de SwSh). NO recorre colecciones. Ordenada por nombre: estable entre ejecuciones del mismo binario del Core.
/// </summary>
internal static class EncounterFingerprint
{
    private static readonly ConcurrentDictionary<Type, Member[]> Members = new();

    private readonly record struct Member(string Name, Func<object, object?> Get);

    public static string Of(object? enc)
    {
        if (enc is null) return "null";
        var sb = new StringBuilder(256);
        Describe(sb, enc, structDepth: 1);
        var parent = SafeGet(enc.GetType().GetProperty("Parent", BindingFlags.Public | BindingFlags.Instance), enc);
        if (parent is not null && !parent.GetType().IsValueType)
        {
            sb.Append("|parent:");
            Describe(sb, parent, structDepth: 1);
        }
        return sb.ToString();
    }

    private static void Describe(StringBuilder sb, object obj, int structDepth)
    {
        sb.Append(obj.GetType().FullName).Append('{');
        foreach (var m in Members.GetOrAdd(obj.GetType(), Collect))
        {
            object? v;
            try { v = m.Get(obj); }
            catch (Exception ex) when (ex is not OutOfMemoryException) { v = "!error"; }
            if (v is null) { sb.Append(m.Name).Append("=null;"); continue; }
            var t = v.GetType();
            if (IsSimple(t)) sb.Append(m.Name).Append('=').Append(Convert.ToString(v, CultureInfo.InvariantCulture)).Append(';');
            else if (t.IsValueType && structDepth > 0) { sb.Append(m.Name).Append('='); Describe(sb, v, structDepth - 1); sb.Append(';'); }
        }
        sb.Append('}');
    }

    private static bool IsSimple(Type t) => t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal);

    private static Member[] Collect(Type type)
    {
        var list = new List<Member>();
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
        foreach (var p in type.GetProperties(flags))
        {
            if (p.GetIndexParameters().Length > 0 || !p.CanRead || p.Name == "Parent") continue;
            if (!Eligible(p.PropertyType)) continue;
            var pi = p;
            list.Add(new Member(p.Name, o => pi.GetValue(o)));
        }
        foreach (var f in type.GetFields(flags))
        {
            if (!Eligible(f.FieldType)) continue;
            var fi = f;
            list.Add(new Member(f.Name, o => fi.GetValue(o)));
        }
        return [.. list.OrderBy(m => m.Name, StringComparer.Ordinal)];
    }

    // Primitivos, enum, string y estructuras (no colecciones, ni delegados, ni referencias a otros objetos).
    private static bool Eligible(Type t)
    {
        if (IsSimple(t)) return true;
        if (!t.IsValueType || t.IsGenericType || t.IsPointer || t.IsByRef || t.IsArray) return false;
        return !t.Name.Contains("Span", StringComparison.Ordinal) && !t.Name.Contains("Memory", StringComparison.Ordinal);
    }

    private static object? SafeGet(PropertyInfo? p, object o)
    {
        try { return p?.GetValue(o); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return null; }
    }
}
