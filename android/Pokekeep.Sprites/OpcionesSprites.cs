// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;

namespace Pokekeep.Sprites;

/// <summary>Juego de sprites de la caja: las tres opciones de <c>SpriteBuilderMode</c> de PKHeX (clasico = Big «b_», mugshot = Leyendas Arceus «c_», artwork = «a_»).</summary>
public enum ModoSprites { Clasico, Mugshot, Artwork }

/// <summary>Preferencia del usuario (<c>SpriteBuilderPreference</c>). Por defecto PKHeX elige el juego de sprites segun el guardado.</summary>
public enum PreferenciaSprites { Sugerida, NoCambiar, ForzarSprites, ForzarMugshots, ForzarArtwork }

/// <summary>Fondo que distingue un tipo (<c>SpriteBackgroundType</c>): ninguno, franja inferior, fondo completo o franja superior.</summary>
public enum FondoSprite { Ninguno, FranjaInferior, FondoCompleto, FranjaSuperior }

public static class ModoSpritesRegla
{
    /// <summary>Copia de <c>SpriteBuilderUtil.GetSuggestedMode</c>: Leyendas Arceus = mugshot; Escarlata/Purpura y Leyendas Z-A = artwork; el resto, clasico. Null = no cambiar.</summary>
    public static ModoSprites? Sugerido(SaveFile sav, PreferenciaSprites preferencia = PreferenciaSprites.Sugerida) => preferencia switch
    {
        PreferenciaSprites.ForzarMugshots => ModoSprites.Mugshot,
        PreferenciaSprites.ForzarSprites => ModoSprites.Clasico,
        PreferenciaSprites.ForzarArtwork => ModoSprites.Artwork,
        PreferenciaSprites.NoCambiar => null,
        _ => sav switch
        {
            SAV8LA => ModoSprites.Mugshot,
            SAV9SV or SAV9ZA => ModoSprites.Artwork,
            _ => ModoSprites.Clasico,
        },
    };

    /// <summary>La regla por defecto sin preferencia (siempre devuelve un modo).</summary>
    public static ModoSprites Sugerido(SaveFile sav) => Sugerido(sav, PreferenciaSprites.Sugerida)!.Value;
}

/// <summary>
/// Ajustes de aspecto que PKHeX guarda en <c>SpriteSettings</c>, con sus mismos valores por defecto (los que ve quien abre PKHeX por primera vez).
/// Instancia propia en vez de estaticos: dos compositores pueden convivir con ajustes distintos.
/// </summary>
public sealed class OpcionesSprites
{
    public PreferenciaSprites Preferencia { get; set; } = PreferenciaSprites.Sugerida;
    /// <summary>Sprites de brillo hechos por aficionados cuando el Pokemon es variocolor (<c>SpriteName.AllowShinySprite</c>).</summary>
    public bool SpritesBrillantes { get; set; } = true;
    public bool HuevoComoObjeto { get; set; } = true;
    /// <summary>Estrella de brillo cosida en el sprite (<c>rare_icon_alt</c>, como PKHeX). La app la desactiva y dibuja una estrella propia sobre la casilla: la estrella cosida ensanchaba la caja visible del sprite y el Pokemon quedaba diminuto en su esquina (uso real 1).</summary>
    public bool EstrellaBrillo { get; set; } = true;
    public bool BolaDeEncuentro { get; set; } = true;
    public FondoSprite ColorEncuentro { get; set; } = FondoSprite.FondoCompleto;
    public FondoSprite ColorEncuentroPkm { get; set; } = FondoSprite.Ninguno;
    public byte OpacidadFondoEncuentro { get; set; } = 0x3F;
    public byte OpacidadFranjaEncuentro { get; set; } = 0x5F;
    public int GrosorFranjaEncuentro { get; set; } = 4;
    public bool PorcentajeExperiencia { get; set; }
    public FondoSprite TipoTera { get; set; } = FondoSprite.FranjaInferior;
    public int GrosorFranjaTera { get; set; } = 4;
    public byte OpacidadFondoTera { get; set; } = 0xFF;
    public byte OpacidadFranjaTera { get; set; } = 0xAF;
    public float OpacidadFiltroNoCoincide { get; set; } = 0.40f;
    public float GrisFiltroNoCoincide { get; set; } = 0.70f;

    /// <summary>
    /// Entero que se usa como semilla del color del tipo de encuentro. PKHeX usa <c>string.GetHashCode()</c>, que .NET aleatoriza en cada proceso: el color de «EncounterSlot4»
    /// cambia en cada arranque. Aqui el valor por defecto es un hash FNV-1a ESTABLE (el mismo tipo, el mismo color siempre); las pruebas de igualdad lo sustituyen por
    /// <c>GetHashCode</c> para comparar con PKHeX en el mismo proceso.
    /// </summary>
    public Func<string, int> HashTipoEncuentro { get; set; } = HashEstable;

    public static int HashEstable(string s)
    {
        var h = 2166136261u;
        foreach (var c in s) { h ^= c; h *= 16777619u; }
        return unchecked((int)h);
    }

    public OpcionesSprites Clonar() { var o = (OpcionesSprites)MemberwiseClone(); return o; }
}
