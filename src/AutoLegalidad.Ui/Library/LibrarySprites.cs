// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using PKHeX.Drawing.PokeSprite;
using PKHeX.Drawing.PokeSprite.Properties;

namespace AutoLegalidad.Ui.Library;

/// <summary>
/// Sprites de la Librería (Hito 3c). PKHeX trae tres juegos de sprites de 68x56 (clásicos <c>b</c>, arte <c>a</c> y circulares <c>c</c>) y elige uno según el guardado abierto; cada uno solo tiene las
/// especies y formas de su época, y PKHeX cae en un «?» cuando falta una. La Librería lista TODO (también lo que no existe en el juego abierto, atenuado), así que aquí se busca el sprite en el juego
/// de sprites activo y, si no está, en los otros dos, y con el contexto del juego y, si no, con el más reciente (los nombres de recurso dependen del contexto: Pikachu con gorra en 6.ª generación...).
/// Solo si ningún juego de sprites lo trae se usa el sprite de reserva de PKHeX. Los Pokémon Totem (que no tienen sprite propio) usan el de su forma base, como hace PKHeX (sin su brillo).
/// </summary>
public static class LibrarySprites
{
    /// <summary>De dónde sale un sprite: el recurso de PKHeX, su prefijo (<c>a</c>, <c>b</c> o <c>c</c>) y el contexto con el que se nombró.</summary>
    public readonly record struct Source(string Resource, char Prefix, EntityContext Context);

    /// <summary>Prefijos por orden de preferencia del modo activo de PKHeX (clásicos: b, c; arte: a, b; circulares: c, b), seguidos del resto.</summary>
    public static IReadOnlyList<char> PrefixOrder()
    {
        var spriter = SpriteUtil.Spriter;
        char[] preferred = ReferenceEquals(spriter, SpriteUtil.SB8a) ? ['a', 'b'] : ReferenceEquals(spriter, SpriteUtil.SB8c) ? ['c', 'b'] : ['b', 'c'];
        return [.. preferred.Concat("abc".Where(p => !preferred.Contains(p)))];
    }

    /// <summary>Prefijos que PKHeX usa de verdad para un guardado concreto (artwork en Escarlata/Púrpura y Z-A, circulares en Leyendas Arceus, clásicos en el resto).</summary>
    public static IReadOnlyList<char> PrefixesPkhexUsesFor(SpriteBuilderMode mode) => mode switch
    {
        SpriteBuilderMode.SpritesArtwork5668 => ['a', 'b'],
        SpriteBuilderMode.CircleMugshot5668 => ['c', 'b'],
        _ => ['b', 'c'],
    };

    /// <summary>Contextos con los que se prueba el nombre del recurso: el del juego y, si falta, el más reciente.</summary>
    private static IEnumerable<EntityContext> Contexts(EntityContext game)
    {
        yield return game;
        if (game != Latest.Context) yield return Latest.Context;
    }

    public static byte SpriteForm(ushort species, byte form) => FormInfo.IsTotemForm(species, form, EntityContext.Gen7) ? FormInfo.GetTotemBaseForm(species, form) : form;

    public static bool TryFind(ushort species, byte form, byte gender, EntityContext game, IReadOnlyList<char> prefixes, out Source source, out Bitmap? bitmap)
    {
        var f = SpriteForm(species, form);
        foreach (var context in Contexts(game))
        {
            var name = SpriteName.GetResourceStringSprite(species, f, gender, 0, context, shiny: false);
            foreach (var prefix in prefixes)
            {
                if (Resources.ResourceManager.GetObject(prefix + name) is Bitmap found)
                {
                    source = new Source(prefix + name, prefix, context);
                    bitmap = found;
                    return true;
                }
            }
        }
        source = default;
        bitmap = null;
        return false;
    }

    /// <summary>Sprite del Pokémon con el variocolor pedido, o null si ningún juego de sprites lo trae (entonces se usa el de reserva de PKHeX).</summary>
    public static Bitmap? Build(ushort species, byte form, byte gender, EntityContext game, Shiny shiny)
    {
        if (!TryFind(species, form, gender, game, PrefixOrder(), out var source, out var bitmap) || bitmap is null)
            return null;
        return SpriteUtil.Spriter.GetSprite(new Bitmap(bitmap), species, 0, false, shiny, source.Context);
    }
}
