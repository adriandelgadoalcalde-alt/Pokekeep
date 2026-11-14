// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Desafios;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen4;

/// <summary>
/// R-013, «Elegir Pokémon…» del participante de un curso del Pokéathlon: una lista con el equipo y todas las cajas (sprite, nombre, caja y casilla, con búsqueda por nombre) que rellena especie, forma, género, variocolor, PID, TID y SID del participante
/// con <see cref="ParticipantePokeathlon.Desde"/>. No escribe nada por sí sola: solo rellena el BORRADOR del participante y se aplica con el resto de la pantalla («Aplicar»), como si se hubieran escrito esos siete campos a mano.
/// </summary>
internal static partial class PokeathlonGen4
{
    private const int InicioCajas = 1000;

    /// <summary>Opciones de la lista: 0 = sin elegir; 1-6 = el equipo; 1000 + caja × 30 + casilla = una caja. Solo Pokémon que existen (casillas vacías fuera).</summary>
    internal static IReadOnlyList<OpcionP> PokemonDeLaPartida(ContextoP c)
    {
        var lista = new List<OpcionP> { new(0, "—") };
        var sav = c.Sav;
        if (sav.HasParty)
            for (var i = 0; i < 6; i++)
                if (sav.GetPartySlotAtIndex(i) is { Species: > 0 } pk) lista.Add(new(1 + i, IdiomaInterfaz.F("PokeathlonGen4.ElegirEquipo", Nombre(c, pk), i + 1)));
        if (sav.HasBox)
            for (var b = 0; b < sav.BoxCount; b++)
            {
                var caja = sav.GetBoxData(b);
                for (var s = 0; s < caja.Length; s++)
                    if (caja[s] is { Species: > 0 } pk) lista.Add(new(InicioCajas + (b * sav.BoxSlotCount) + s, IdiomaInterfaz.F("PokeathlonGen4.ElegirCaja", Nombre(c, pk), c.Sesion.NombreCaja(b), s + 1)));
            }
        return lista;
    }

    private static string Nombre(ContextoP c, PKM pk)
    {
        var especie = pk.Species < c.Textos.specieslist.Length ? c.Textos.specieslist[pk.Species] : pk.Species.ToString();
        return pk.IsNicknamed && !string.IsNullOrWhiteSpace(pk.Nickname) && pk.Nickname != especie ? $"{pk.Nickname} ({especie})" : especie;
    }

    /// <summary>El Pokémon de una opción de <see cref="PokemonDeLaPartida"/>, o null.</summary>
    internal static PKM? PokemonDe(ContextoP c, long id)
    {
        var sav = c.Sav;
        if (id is >= 1 and <= 6) return sav.HasParty ? sav.GetPartySlotAtIndex((int)id - 1) : null;
        if (id < InicioCajas || !sav.HasBox) return null;
        var n = (int)(id - InicioCajas);
        var (caja, casilla) = (n / sav.BoxSlotCount, n % sav.BoxSlotCount);
        return caja < sav.BoxCount ? sav.GetBoxData(caja)[casilla] : null;
    }

    private static CampoP ElegirPokemon(string id, string idEspecie, string idForma, string idGenero, string idVariocolor, string idPid, string idTid, string idSid, int prioridad)
        => new()
        {
            Id = id, Etiqueta = IdiomaInterfaz.T("PokeathlonGen4.ElegirPokemon"), Tipo = TipoP.Opcion, SinEscritura = true, SoloAccion = true,
            TituloSelector = IdiomaInterfaz.T("PokeathlonGen4.ElegirPokemon"), Leer = _ => 0L, Opciones = PokemonDeLaPartida, Prioridad = prioridad,
            Ayuda = IdiomaInterfaz.T("PokeathlonGen4.ElegirPokemonAyuda"),
            EspecieDeOpcion = (c, o) => PokemonDe(c, o)?.Species ?? 0,
            Deriva = c =>
            {
                if (PokemonDe(c, c.Entero(id)) is not { } pk) return new Dictionary<string, object?>();
                var p = ParticipantePokeathlon.Desde(pk);
                return new Dictionary<string, object?>
                {
                    [idEspecie] = (long)p.Especie, [idForma] = (long)p.Forma, [idGenero] = (long)Math.Min((byte)2, p.Sexo), [idVariocolor] = p.Variocolor,
                    [idPid] = (long)p.ClaveCifrado, [idTid] = (long)p.TID16, [idSid] = (long)p.SID16,
                };
            },
        };
}
