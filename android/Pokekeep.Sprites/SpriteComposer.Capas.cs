// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;

namespace Pokekeep.Sprites;

/// <summary>Donde se ancla una marca de la casilla (objeto, Gigamax, alfa, equipo, bloqueo, inicial, franja de Teratipo) respecto a la celda.</summary>
public enum EsquinaMarca { AbajoDerecha, ArribaDerecha, ArribaIzquierda, ArribaCentro, FranjaInferior }

/// <summary>Una marca de la casilla: su imagen (en el lienzo de 68x56 de PKHeX, con su posicion propia) y la esquina de la celda donde se dibuja.</summary>
public sealed record MarcaCasilla(SpriteImage Imagen, EsquinaMarca Esquina);

/// <summary>La casilla separada en capas: el Pokemon (especie, huevo, brillo, halo de sombra) y las marcas que PKHeX cose en el mismo lienzo.</summary>
public sealed record CasillaCapas(SpriteImage Pokemon, IReadOnlyList<MarcaCasilla> Marcas)
{
    public SpriteImage? Fondo { get; init; }
    /// <summary>Juego del que sale <see cref="Pokemon"/> cuando es el icono de caja propio de la partida (X53, <see cref="SpritesDeJuego.JuegoDeCaja"/>); null si es el sprite de PKHeX.</summary>
    public string? JuegoIcono { get; init; }
}

public sealed partial class SpriteComposer
{
    /// <summary>
    /// La casilla en capas (uso real 2). <see cref="GetSlotSprite(PKM, SaveFile, int, int, SlotVisibilityType, StorageSlotType, LegalityAnalysis?)"/> mete en UN lienzo de 68x56 el Pokemon y todas las marcas;
    /// la vista dibuja el recuadro visible de ese lienzo ampliado hasta llenar la casilla, asi que una marca lejos del Pokemon (el emblema Gigamax arriba, el objeto abajo a la derecha, la franja de Teratipo
    /// en todo el ancho) ensanchaba el recuadro y el Pokemon quedaba diminuto. Aqui el recuadro es el del Pokemon solo y las marcas se anclan a la celda. Los pixeles son los mismos que los de la casilla compuesta.
    /// </summary>
    public CasillaCapas GetSlotCapas(PKM pk, SaveFile sav, int box = -1, int slot = -1, StorageSlotType storage = StorageSlotType.None, bool indicarLegalidad = true)
    {
        _ = storage;
        var empty = pk.Species == 0;
        if (empty) return new CasillaCapas(Recurso("b_0") ?? Silueta, []);
        var formarg = pk is IFormArgument f ? f.FormArgument : 0u;
        var shiny = ShinyExtensions.GetType(pk);
        var icono = IconoDeCaja(pk.Species, pk.Form, pk.Gender, shiny.IsShiny(), pk.IsEgg, pk.Context);   // X53: el icono del propio juego (sin capas de huevo ni de brillo: el icono ya es el del huevo, y el brillo lo marca la vista)
        var pokemon = icono?.Imagen ?? GetSprite(pk.Species, pk.Form, pk.Gender, formarg, 0, pk.IsEgg, shiny, pk.Context);
        if (icono is null && pk is IShadowCapture { IsShadow: true })
        {
            if (pk.Species is (ushort)Species.Lugia && Recurso("b_249x") is { } sombra) pokemon = GetSprite(sombra, pk.Species, 0, pk.IsEgg, shiny, pk.Context);
            pokemon = PixelOps.Layer(Halo(pk, 75, 0, 130, true), pokemon, 0, 0);
        }
        var marcas = new List<MarcaCasilla>();
        if (pk.SpriteItem > 0 && GetItemSprite(pk.SpriteItem, pk.Context) is { } objeto)
            marcas.Add(new MarcaCasilla(PixelOps.Layer(new SpriteImage(Ancho, Alto), objeto, Ancho - objeto.Width - ((ItemMaxSize - objeto.Width) / 4) - ItemShiftX, Alto - objeto.Height - ItemShiftY), EsquinaMarca.AbajoDerecha));
        if (pk is IGigantamaxReadOnly { CanGigantamax: true } && Imprescindible("dyna") is { } gm)
            marcas.Add(new MarcaCasilla(PixelOps.Layer(new SpriteImage(Ancho, Alto), gm, (Ancho - gm.Width) / 2, 0), EsquinaMarca.ArribaCentro));
        if (pk is IAlphaReadOnly { IsAlpha: true } && Imprescindible("alpha_alt") is { } alpha)
            marcas.Add(new MarcaCasilla(PixelOps.Layer(new SpriteImage(Ancho, Alto), alpha, SlotTeamShiftX, 0), EsquinaMarca.ArribaDerecha));
        SpriteImage? fondo = null;
        if (Opciones.TipoTera != FondoSprite.Ninguno && pk is ITeraType t && TeraTypeUtil.IsOverrideValid((byte)t.TeraType))
        {
            var tera = new SpriteImage(Ancho, Alto);
            AplicarColor(tera, Opciones.TipoTera, ColorTipo.DeTera((byte)t.TeraType), Opciones.GrosorFranjaTera, Opciones.OpacidadFranjaTera, Opciones.OpacidadFondoTera);
            if (Opciones.TipoTera == FondoSprite.FranjaInferior) marcas.Add(new MarcaCasilla(tera, EsquinaMarca.FranjaInferior));
            else fondo = tera;
        }
        LegalityAnalysis? la = null;
        if (indicarLegalidad && Opciones.ColorEncuentroPkm != FondoSprite.Ninguno)
        {
            la = Analizar(pk, sav, storage);
            fondo ??= new SpriteImage(Ancho, Alto);
            AplicarColorEncuentro(la.EncounterOriginal, fondo, Opciones.ColorEncuentroPkm);
        }
        if (Opciones.PorcentajeExperiencia)
        {
            var experiencia = new SpriteImage(Ancho, Alto);
            AplicarExperiencia(pk, experiencia, la?.EncounterMatch);
            marcas.Add(new MarcaCasilla(experiencia, EsquinaMarca.FranjaInferior));
        }
        if ((uint)slot < MaxSlotCount && box >= 0)
        {
            var flags = sav.GetBoxSlotFlags(box, slot);
            if (flags.IsBattleTeam() >= 0 && Imprescindible("team") is { } team) marcas.Add(new MarcaCasilla(PixelOps.Layer(new SpriteImage(Ancho, Alto), team, SlotTeamShiftX, 0), EsquinaMarca.ArribaDerecha));
            if (flags.HasFlag(StorageSlotSource.Locked) && Imprescindible("locked") is { } locked) marcas.Add(new MarcaCasilla(PixelOps.Layer(new SpriteImage(Ancho, Alto), locked, SlotLockShiftX, 0), EsquinaMarca.ArribaDerecha));
            var party = flags.IsParty();
            if (party >= 0 && Imprescindible($"party{party + 1}") is { } pa) marcas.Add(new MarcaCasilla(PixelOps.Layer(new SpriteImage(Ancho, Alto), pa, PartyMarkShiftX, 0), EsquinaMarca.ArribaDerecha));
            if (flags.HasFlag(StorageSlotSource.Starter) && Imprescindible("starter") is { } st) marcas.Add(new MarcaCasilla(PixelOps.Layer(new SpriteImage(Ancho, Alto), st, 0, 0), EsquinaMarca.ArribaIzquierda));
        }
        return new CasillaCapas(pokemon, marcas) { Fondo = fondo, JuegoIcono = icono?.Juego };
    }
}
