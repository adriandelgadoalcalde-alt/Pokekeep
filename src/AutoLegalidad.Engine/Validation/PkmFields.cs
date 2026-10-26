// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Globalization;
using System.Reflection;
using System.Text;
using PKHeX.Core;

namespace AutoLegalidad.Engine.Validation;

/// <summary>Campos curados de un Pokemon: su "firma semantica" (round-trip) y los valores legibles del resumen de cambios.</summary>
public static class PkmFields
{
    /// <summary>Campos que identifican a la entidad. Si el round-trip altera uno, el candidato se rechaza.</summary>
    public static IReadOnlyList<KeyValuePair<string, string>> Signature(PKM pk)
    {
        var l = new List<KeyValuePair<string, string>>(64);
        void Add(string k, object? v) => l.Add(new(k, Convert.ToString(v, CultureInfo.InvariantCulture) ?? ""));
        Add("Species", pk.Species); Add("Form", pk.Form); Add("PID", pk.PID); Add("EncryptionConstant", pk.EncryptionConstant);
        Add("Nature", pk.Nature); Add("Ability", pk.Ability); Add("AbilityNumber", pk.AbilityNumber);
        Add("Gender", pk.Gender); Add("IsShiny", pk.IsShiny);
        for (var i = 0; i < 6; i++) { Add("IV" + i, pk.GetIV(i)); Add("EV" + i, pk.GetEV(i)); }
        Add("Move1", pk.Move1); Add("Move2", pk.Move2); Add("Move3", pk.Move3); Add("Move4", pk.Move4);
        Add("Relearn1", pk.RelearnMove1); Add("Relearn2", pk.RelearnMove2); Add("Relearn3", pk.RelearnMove3); Add("Relearn4", pk.RelearnMove4);
        Add("Ball", pk.Ball); Add("Level", pk.CurrentLevel); Add("EXP", pk.EXP);
        Add("OT", pk.OriginalTrainerName); Add("TID16", pk.TID16); Add("SID16", pk.SID16); Add("OTGender", pk.OriginalTrainerGender);
        Add("Language", pk.Language); Add("Version", pk.Version);
        Add("MetLocation", pk.MetLocation); Add("MetLevel", pk.MetLevel); Add("EggLocation", pk.EggLocation); Add("IsEgg", pk.IsEgg);
        Add("MetDate", pk.MetDate); Add("EggMetDate", pk.EggMetDate);
        Add("HeldItem", pk.HeldItem); Add("Nickname", pk.Nickname); Add("IsNicknamed", pk.IsNicknamed); Add("Fateful", pk.FatefulEncounter);
        return l;
    }

    public static bool SignatureEquals(PKM a, PKM b, out string? firstDifference)
    {
        var x = Signature(a);
        var y = Signature(b);
        for (var i = 0; i < x.Count; i++)
        {
            if (x[i].Value == y[i].Value) continue;
            firstDifference = $"{x[i].Key}: {x[i].Value} != {y[i].Value}";
            return false;
        }
        firstDifference = null;
        return true;
    }

    /// <summary>Todas las claves de la firma que difieren (para distinguir una adaptacion de importacion benigna de un cambio de identidad).</summary>
    public static IReadOnlyList<string> SignatureDifferences(PKM a, PKM b)
    {
        var x = Signature(a);
        var y = Signature(b);
        var list = new List<string>();
        for (var i = 0; i < x.Count; i++)
            if (x[i].Value != y[i].Value) list.Add(x[i].Key);
        return list;
    }

    public static int[] IVs(PKM pk) => [pk.IV_HP, pk.IV_ATK, pk.IV_DEF, pk.IV_SPA, pk.IV_SPD, pk.IV_SPE];
    public static int[] EVs(PKM pk) => [pk.EV_HP, pk.EV_ATK, pk.EV_DEF, pk.EV_SPA, pk.EV_SPD, pk.EV_SPE];
    public static string IvText(IReadOnlyList<int> iv) => string.Join('/', iv);

    private static readonly HashSet<string> SkipProperties = new(StringComparer.Ordinal)
    {
        "Data", "Raw", "ExtraBytes", "Checksum", "ChecksumValid", "Valid", "Sanity", "SIZE_PARTY", "SIZE_STORED",
    };

    /// <summary>
    /// Compara TODAS las propiedades publicas legibles (numeros, texto, enums, fechas y los bytes "trash" de las cadenas) de dos entidades del mismo tipo
    /// y devuelve las que difieren. Es la red de seguridad de la guarda de origen: no depende de una lista de campos escrita a mano.
    /// </summary>
    public static IReadOnlyDictionary<string, (string Before, string After)> ReflectDiff(PKM a, PKM b)
    {
        var result = new SortedDictionary<string, (string, string)>(StringComparer.Ordinal);
        if (a.GetType() != b.GetType())
        {
            result["<tipo>"] = (a.GetType().Name, b.GetType().Name);
            return result;
        }
        foreach (var p in a.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (p.GetIndexParameters().Length != 0 || p.GetMethod is null || SkipProperties.Contains(p.Name)) continue;
            var t = p.PropertyType;
            string? va, vb;
            try
            {
                if (t == typeof(ReadOnlySpan<byte>) || t == typeof(Span<byte>))
                {
                    if (!p.Name.EndsWith("Trash", StringComparison.Ordinal)) continue;
                    va = TrashHex(a, p); vb = TrashHex(b, p);
                }
                else if (t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(DateOnly) || t == typeof(DateOnly?) || t == typeof(decimal))
                {
                    va = Convert.ToString(p.GetValue(a), CultureInfo.InvariantCulture);
                    vb = Convert.ToString(p.GetValue(b), CultureInfo.InvariantCulture);
                }
                else continue;
            }
            catch (TargetInvocationException) { continue; }
            if (!string.Equals(va, vb, StringComparison.Ordinal))
                result[p.Name] = (va ?? "", vb ?? "");
        }
        return result;
    }

    private static string TrashHex(PKM pk, PropertyInfo p)
    {
        // Las propiedades *Trash devuelven Span<byte>: se leen por su metodo de acceso con una copia.
        return p.Name switch
        {
            nameof(PKM.OriginalTrainerTrash) => Convert.ToHexString(pk.OriginalTrainerTrash),
            nameof(PKM.NicknameTrash) => Convert.ToHexString(pk.NicknameTrash),
            nameof(PKM.HandlingTrainerTrash) => Convert.ToHexString(pk.HandlingTrainerTrash),
            _ => string.Empty,
        };
    }

    public static string Describe(PKM pk)
    {
        var sb = new StringBuilder();
        sb.Append($"esp {pk.Species}/{pk.Form} lv{pk.CurrentLevel} ver {pk.Version} IV {IvText(IVs(pk))} EV {IvText(EVs(pk))}");
        return sb.ToString();
    }
}
