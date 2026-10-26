// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Sprites;

namespace Pokekeep.Mobile.Ui.Libreria;

/// <summary>Qué sprite pintar para una entrada: el juego de sprites, el contexto con el que se nombra el recurso y la forma (los Pokémon Totem usan la de su forma base, sin su brillo).</summary>
public readonly record struct SpriteElegido(ModoSprites Modo, int Contexto, int Forma);

/// <summary>
/// La regla de sprites de la Librería del escritorio (<c>LibrarySprites</c>): la Librería lista TODO (también lo que no existe en el juego abierto), así que el sprite se busca en el juego de sprites activo y, si no
/// está, en los otros dos (clásicos <c>b</c>, arte <c>a</c>, circulares <c>c</c>; orden de preferencia del modo activo) y con el contexto del juego y, si no, con el más reciente. Solo si ningún juego de sprites lo trae
/// se usa el sprite de reserva de PKHeX (el compositor dibuja <c>b_unknown</c> con el número de especie encima). Paridad exacta con PKHeX: ver <c>PruebasSpritesLibreria</c>.
/// </summary>
public static class SpriteLibreria
{
    private static char Prefijo(ModoSprites m) => m switch { ModoSprites.Artwork => 'a', ModoSprites.Mugshot => 'c', _ => 'b' };

    /// <summary>Orden de preferencia de los juegos de sprites (clásicos: b, c; arte: a, b; circulares: c, b), seguidos del que falte.</summary>
    public static IReadOnlyList<ModoSprites> Orden(ModoSprites activo)
    {
        ModoSprites[] preferidos = activo switch { ModoSprites.Artwork => [ModoSprites.Artwork, ModoSprites.Clasico], ModoSprites.Mugshot => [ModoSprites.Mugshot, ModoSprites.Clasico], _ => [ModoSprites.Clasico, ModoSprites.Mugshot] };
        return [.. preferidos.Concat(new[] { ModoSprites.Clasico, ModoSprites.Mugshot, ModoSprites.Artwork }.Where(m => !preferidos.Contains(m)))];
    }

    public static byte FormaDeSprite(ushort especie, byte forma) => FormInfo.IsTotemForm(especie, forma, EntityContext.Gen7) ? FormInfo.GetTotemBaseForm(especie, forma) : forma;

    public static SpriteElegido Elegir(int especie, int forma, int genero, int juegoId, ModoSprites activo)
    {
        var contextoJuego = juegoId > 0 ? ((GameVersion)juegoId).Context : EntityContext.None;
        var f = FormaDeSprite((ushort)especie, (byte)forma);
        if (!Design.Sprites.Configurado) return new(activo, (int)contextoJuego, f);
        var recursos = Design.Sprites.Recursos;
        IEnumerable<EntityContext> contextos = contextoJuego == Latest.Context ? [contextoJuego] : [contextoJuego, Latest.Context];
        foreach (var contexto in contextos)
        {
            var nombre = SpriteName.GetResourceStringSprite((ushort)especie, f, (byte)genero, 0, contexto, false, true);
            foreach (var modo in Orden(activo))
                if (recursos.Contiene(Prefijo(modo) + nombre)) return new(modo, (int)contexto, f);
        }
        return new(activo, (int)contextoJuego, f);
    }
}
