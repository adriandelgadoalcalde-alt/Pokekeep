// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;

namespace AutoLegalidad.Engine.Context;

/// <summary>Lectura y escritura canonica de entidades. El Core descifra algunos formatos in situ: siempre se le pasa una COPIA de los bytes.</summary>
public static class EntityIo
{
    public static PKM? Parse(ReadOnlySpan<byte> bytes, EntityContext prefer)
    {
        var copy = bytes.ToArray();
        return EntityFormat.GetFromBytes(copy, prefer);
    }

    /// <summary>
    /// Bytes canonicos de la entidad, descifrados, en el mismo tamano que la entrada (casilla de caja o equipo). Es lo que exporta PKHeX
    /// como archivo de entidad; nunca se reinterpretan como otro formato.
    /// </summary>
    public static byte[] Export(PKM pk, bool partySize = false)
    {
        var size = partySize ? pk.SIZE_PARTY : pk.SIZE_STORED;
        var buffer = new byte[size];
        if (partySize)
            pk.WriteDecryptedDataParty(buffer);
        else
            pk.WriteDecryptedDataStored(buffer);
        return buffer;
    }

    /// <summary>Tamano de caja o de equipo segun lo que vino en la solicitud (los GB usan el mismo para los dos).</summary>
    public static bool IsPartySize(PKM pk, int inputLength) => pk.SIZE_PARTY != pk.SIZE_STORED && inputLength == pk.SIZE_PARTY;
}
