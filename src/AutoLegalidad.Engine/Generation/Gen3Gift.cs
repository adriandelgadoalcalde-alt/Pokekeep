// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;

namespace AutoLegalidad.Engine.Generation;

/// <summary>
/// Regalos de las maquinas de distribucion de Gen3 (PC de Nueva York, <see cref="EncounterGift3NY"/>, y de Japon, <see cref="EncounterGift3JPN"/>). Hito 3b.
/// Su identidad (OT, TID, SID, idioma) sale de unas reglas finitas del Core y NO de una muestra aleatoria de <c>ConvertToPKM</c>:
/// <list type="bullet">
/// <item>NY: <c>Legality/Encounters/Templates/Gen3/Gifts/Distribution3NY.cs</c> - TID aleatorio en [1, 2999] (<c>GetTrainerID</c>, lineas 33-34), nombre B/C/D segun la distribucion (<c>IsValidTrainerName</c>), SID 0, idioma distinto de japones (<c>EncounterGift3NY.cs:IsMatchExact</c>).</item>
/// <item>JPN: <c>Distribution3JPN.cs</c> - TID FIJO por distribucion (<c>GetTrainerID</c>), nombre de seis ciudades (<c>IsTrainerNameValid</c>), SID 0, idioma japones (<c>EncounterGift3JPN.cs:IsMatchExact</c>).</item>
/// </list>
/// </summary>
public static class Gen3Gift
{
    /// <summary>True si la identidad del Pokemon (OT, TID, SID, idioma) es una que esta tarjeta puede repartir.</summary>
    public static bool Matches(IEncounterable enc, PKM pk)
    {
        if (pk.SID16 != 0) return false;
        var name = pk.OriginalTrainerName;
        switch (enc)
        {
            case EncounterGift3NY ny:
                return pk.Language != (int)LanguageID.Japanese && ny.Distribution.IsValidTrainerID(pk.TID16) && name.Length > 4 && ny.Distribution.IsValidTrainerName(name);
            case EncounterGift3JPN jp:
                return pk.Language == (int)LanguageID.Japanese && pk.TID16 == jp.Distribution.GetTrainerID() && name.Length > 2 && jp.Distribution.IsTrainerNameValid(name);
            default:
                return false;
        }
    }

    /// <summary>
    /// Crea un regalo de Nueva York con el entrenador (OT/TID) que ya tiene el Pokemon del usuario. El Core sortea el TID entre 1 y 2999 en cada llamada
    /// (<c>Distribution.GetTrainerID</c>, Util.Rand) y la semilla es independiente de el, asi que cualquier TID valido es una generacion posible; pero el PID antivariocolor
    /// <c>((a ^ idXor ^ b) &lt;&lt; 16) | b</c> depende del TID (<c>CommonEvent3.GetAntishiny</c>), por eso no se puede cambiar el TID de un PID ya generado: se repite el sorteo de
    /// semilla de <c>EncounterGift3NY.SetPINGA</c> (copia fiel, mismas llamadas publicas) con el TID pedido. Devuelve <c>null</c> si esta tarjeta no puede repartir esa identidad.
    /// El resultado SIEMPRE pasa despues por el validador oficial del motor.
    /// </summary>
    public static PK3? CreateNy(EncounterGift3NY ny, ITrainerInfo tr, in EncounterCriteria criteria, PKM want, Func<bool>? expired = null)
    {
        if (!Matches(ny, want)) return null;
        var pk = ny.ConvertToPKM(tr, EncounterCriteria.Unrestricted); // la capa comun (nivel, lugar, version, movimientos...) la pone el Core
        pk.TID16 = want.TID16;
        pk.SID16 = 0;
        pk.OriginalTrainerName = want.OriginalTrainerName;
        if (pk.Language != want.Language)
        {
            pk.Language = want.Language;
            pk.Nickname = SpeciesName.GetSpeciesNameGeneration(ny.Species, want.Language, 3);
        }

        var pi = PersonalTable.RS[ny.Species];
        var rnd = Util.Rand32();
        var seed = rnd;
        var idXor = (uint)pk.TID16;
        for (var guard = 0; guard < 2_000_000; guard++)
        {
            if ((guard & 0xFFF) == 0xFFF && expired?.Invoke() == true) return null;
            var pid = CommonEvent3.GetAntishiny(ref seed, idXor);
            if (criteria.IsSpecifiedNature() && !criteria.IsSatisfiedNature(pid)) continue;
            if (criteria.IsSpecifiedGender() && !criteria.IsSatisfiedGender(EntityGender.GetFromPIDAndRatio(pid, pi.Gender))) continue;
            var iv32 = ClassicEraRNG.GetSequentialIVs(ref seed);
            if (criteria.IsSpecifiedHiddenPower() && !criteria.IsSatisfiedHiddenPower(iv32)) continue;
            pk.PID = pid;
            pk.IV32 = iv32;
            pk.RefreshAbility((int)(pid & 1));
            pk.RefreshChecksum();
            return pk;
        }
        return null;
    }

    /// <summary>
    /// Todos los Pokemon posibles de un regalo del arreglo de bayas (Zigzagoon, metodo <see cref="PIDType.BACD_RBCD"/>): las semillas son la suma de digitos de la hora BCD, 0 a 213
    /// (<c>EncounterGift3.GetSaneSeed</c>), y cada una da su PID/IV por <see cref="IGenerateSeed32.GenerateSeed32"/>. El Core al CREAR usa siempre la 213, que solo cuadra con la variante
    /// de Rubi (genero de entrenador 1); la de Zafiro exige una semilla cuyo bit de genero sea 0 (<c>IsMatchGender</c>). Nada se fuerza: el validador oficial decide cuales valen.
    /// </summary>
    public static IEnumerable<PK3> BerryFixCandidates(EncounterGift3 enc, ITrainerInfo tr)
    {
        if (enc.Method != PIDType.BACD_RBCD) yield break;
        for (uint seed = 0; seed <= 213; seed++)
        {
            var pk = (PK3)enc.ConvertToPKM(tr, EncounterCriteria.Unrestricted);
            if (!enc.GenerateSeed32(pk, seed)) continue;
            pk.RefreshAbility((int)(pk.PID & 1));
            pk.RefreshChecksum();
            yield return pk;
        }
    }
}
