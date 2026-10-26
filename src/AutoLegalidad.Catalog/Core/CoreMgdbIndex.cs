// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Catalog.Db;
using PKHeX.Core;

namespace AutoLegalidad.Catalog.Core;

/// <summary>Indice del MGDB EMBEBIDO del Core (<c>EncounterEvent.MGDB_*</c>): lo que el validador ya conoce, por SHA-256 de los bytes de la tarjeta.</summary>
public static class CoreMgdbIndex
{
    public static IEnumerable<DataMysteryGift> AllGifts()
    {
        IEnumerable<DataMysteryGift>[] arrays =
        [
            EncounterEvent.MGDB_G4, EncounterEvent.MGDB_G5, EncounterEvent.MGDB_G6, EncounterEvent.MGDB_G7, EncounterEvent.MGDB_G7GG,
            EncounterEvent.MGDB_G8, EncounterEvent.MGDB_G8A, EncounterEvent.MGDB_G8B, EncounterEvent.MGDB_G9, EncounterEvent.MGDB_G9A,
        ];
        return arrays.SelectMany(a => a);
    }

    /// <summary>
    /// SHA-256 ESTABLE de una tarjeta del MGDB del Core. Las tarjetas de Gen 6/7 que el Core carga de <c>wc6full.pkl</c>/<c>wc7full.pkl</c> no traen fecha: el Core les pone la de HOY (<c>WC6Full</c>/<c>WC7Full.GetArray</c>),
    /// así que sus bytes, y su hash, cambiarían cada día y la clave del catálogo dejaría de encontrarse al día siguiente de construirlo (Hito 4b: 21 eventos de Sol salieron «sin solución» el 5-oct tras construir el 4-oct).
    /// Si la fecha de la tarjeta es la de hoy se calcula el hash con esos 4 bytes a cero; una tarjeta con fecha propia (distinta de hoy) se hashea tal cual.
    /// </summary>
    public static string StableSha256(DataMysteryGift g)
    {
        var data = g.Write();
        var today = EncounterDate.GetDate3DS();
        var dynamicDate = g switch { WC6 w => w.Date == today, WC7 w => w.Date == today, _ => false };
        if (!dynamicDate) return Hashing.Sha256Hex(data);
        var copy = data.ToArray();
        Array.Clear(copy, 0x4C, 4);   // RawDate de WC6 y WC7
        return Hashing.Sha256Hex(copy);
    }

    public static string KeyFor(string typeName, string giftSha256) => $"core-mgdb:{typeName}:{giftSha256[..16]}";

    /// <summary>SHA-256 de los bytes que escribe la tarjeta (<c>Write()</c>) -> clave del encuentro del Core.</summary>
    public static IReadOnlyDictionary<string, string> BySha { get; } = Build();

    private static Dictionary<string, string> Build()
    {
        var d = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var g in AllGifts())
        {
            var sha = StableSha256(g);
            d.TryAdd(sha, KeyFor(g.GetType().Name, sha));
        }
        return d;
    }
}
