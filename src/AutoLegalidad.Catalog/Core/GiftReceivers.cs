// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;

namespace AutoLegalidad.Catalog.Core;

/// <summary>
/// R-014: qué juegos CONCRETOS pueden recibir una tarjeta de regalo misterioso del Core. Una tarjeta no es de «un» juego: el Core guarda una sola <c>Version</c> (la del Pokémon de muestra,
/// p. ej. HG o D en Gen 4) pero la distribución real fue para varios. La decisión sale siempre del propio Core, tarjeta a tarjeta:
/// <list type="bullet">
/// <item><see cref="PCD"/> (Gen 4): los bits de <see cref="PCD.CardCompatibility"/> (0x14C), uno por valor de <see cref="GameVersion"/> (HG = bit 7, SS = 8, D = 10, P = 11, Pt = 12).
/// <see cref="PCD.CanBeReceivedByVersion"/> compara con la versión de la muestra y deja fuera a SoulSilver, Perla y Platino aunque la tarjeta se repartiese para ellos.</item>
/// <item>PGF, WC6, WC7, WC8 y WB7: <c>CanBeReceivedByVersion(GameVersion)</c> (interfaz <see cref="IRestrictVersion"/>).</item>
/// <item>WB8, WA8, WC9 y WA9: su <c>CanBeReceivedByVersion</c> pide además el Pokémon (por su versión y su lugar de encuentro); se le pasa uno en blanco de esa versión.</item>
/// <item>El resto: <see cref="GameVersion.Contains"/> sobre la versión de la tarjeta.</item>
/// </list>
/// </summary>
public static class GiftReceivers
{
    private static readonly GameVersion[] Gen4Games = [GameVersion.D, GameVersion.P, GameVersion.Pt, GameVersion.HG, GameVersion.SS];

    /// <summary>¿Puede el juego <paramref name="version"/> recibir la tarjeta? (No mira la generación: eso lo decide quien llama con el contexto de la tarjeta.)</summary>
    public static bool CanReceive(DataMysteryGift gift, GameVersion version)
    {
        try
        {
            switch (gift)
            {
                case PCD pcd:
                    if (!Gen4Games.Contains(version)) return false;
                    return HasAnyGen4Bit(pcd) ? (pcd.CardCompatibility & (1 << (int)version)) != 0 : pcd.Version == version;
                case PGF pgf:
                    // Igual que PGF.CanBeReceivedByVersion, pero solo con el nibble bajo de RestrictVersion: en las tarjetas «completas» de EventsGallery (.wc5full) el nibble alto trae otro dato (el idioma) y el Core
                    // no la daría a ningún juego. En las del MGDB (valores 0 a 15) el resultado es el mismo que el del Core.
                    if (pgf.OriginGame != 0) return ((GameVersion)pgf.OriginGame).Contains(version);
                    if (version is < GameVersion.W or > GameVersion.B2) return false;
                    var restrict = pgf.RestrictVersion & 0x0F;
                    return restrict == 0 || (restrict & (1 << (int)(version - GameVersion.W))) != 0;
                case WB8 wb8: return wb8.CanBeReceivedByVersion(version, new PB8());
                case WA8 wa8: return wa8.CanBeReceivedByVersion(version, new PA8());
                case WC9 wc9: return wc9.CanBeReceivedByVersion(new PK9 { Version = version });
                case WA9 wa9: return wa9.CanBeReceivedByVersion(new PA9 { Version = version });
                case IRestrictVersion r: return r.CanBeReceivedByVersion(version);
                default: return gift.Version.Contains(version);
            }
        }
        catch (ArgumentOutOfRangeException)
        {
            return gift.Version.Contains(version);   // RestrictVersion con un valor que el Core no contempla: se cae a la versión de la tarjeta
        }
    }

    private static bool HasAnyGen4Bit(PCD pcd) => Gen4Games.Any(v => (pcd.CardCompatibility & (1 << (int)v)) != 0);

    /// <summary>Juegos concretos del catálogo (con el contexto de la tarjeta) que pueden recibirla.</summary>
    public static IReadOnlyList<GameDef> Games(DataMysteryGift gift)
    {
        var inContext = GameCatalog.Concrete.Where(g => g.Context == gift.Context && g.Context.IsValid).ToList();
        if (inContext.Count == 0)
            return CoreDataSet.ResolveGames(gift.Version, gift.Context);   // sin juego propio de ese contexto: lo de siempre
        return [.. inContext.Where(g => CanReceive(gift, g.Version))];
    }
}
