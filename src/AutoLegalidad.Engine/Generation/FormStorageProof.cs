// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Engine.Context;
using PKHeX.Core;

namespace AutoLegalidad.Engine.Generation;

/// <summary>
/// Hito 3b: ¿puede el formato de entidad del destino GUARDAR esa forma? La tabla personal de un juego puede listar formas que su formato no guarda (Deoxys de Gen3: <c>G3PKM.Form</c> solo
/// distingue a Unown y devuelve 0 para el resto, <c>PKM/Shared/G3PKM.cs:64-76</c>; las formas de Gen1/2 solo existen para Unown, <c>PKM/Shared/GBPKM.cs:151-178</c>). Se comprueba con la propia
/// entidad en blanco del Core: se asigna la forma y se lee. Dato finito y estatico: es una demostracion, no un muestreo.
/// </summary>
public static class FormStorageProof
{
    public static bool CanStore(Destination dest, ushort species, byte form, out string why)
    {
        why = string.Empty;
        try
        {
            var blank = dest.Sav.BlankPKM;
            blank.Species = species;
            if (blank is GBPKM && species == (ushort)Species.Unown && form >= 26)
            {
                why = $"el formato {blank.GetType().Name} deriva la forma de Unown de los DV (máximo 25) y no admite la forma {form} (referencia técnica: GBPKM.cs:151-178)";
                return false;
            }
            blank.Form = form;
            var back = blank.Form;
            if (back == form) return true;
            why = $"el formato {blank.GetType().Name} guarda la forma {back} al asignarle la {form} de la especie {species}: no tiene campo para esa forma (referencia técnica, p. ej.: PKM/Shared/G3PKM.cs:64-76, PKM/Shared/GBPKM.cs:151-170)";
            return false;
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException) { return true; } // sin informacion: no se descarta nada
    }
}
