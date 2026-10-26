// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;

namespace Pokekeep.Mobile.Ui.Partida.Formularios;

public static partial class CatalogoEspecificos
{
    /// <summary>
    /// Editores de Gen. 6-7 (bloque 8b-1): un <c>case</c> por entrada de navegacion, que devuelve el editor declarativo del guardado o null si no es de esta generacion / aun no esta hecho.
    /// Este fichero lo edita unicamente el bloque 8b-1.
    /// </summary>
    private static EditorDefP? EditoresGen6y7(string id, SaveFile sav) => id switch
    {
        "bayas6" => Gen6y7.BayasXY.Crear(sav),
        "roamer" => Gen6y7.Errante6.Crear(sav),
        "poderes6" => Gen6y7.PoderO.Crear(sav),
        "pokeblocks6" => Gen6y7.PokecubosORAS.Crear(sav),
        "pokeflautas" => Gen6y7.Pokelitos.Crear(sav),
        "link6" => Gen6y7.NexoPokemon.Crear(sav),
        "supertrain" => Gen6y7.SuperEntrenamiento.Crear(sav),
        "habichuelas" => Gen6y7.Pokehabas.Crear(sav),
        "hof" => Gen6y7.SalonFama7.Crear(sav) ?? Gen6y7.SalonFama6.Crear(sav),
        "basessecretas" => Gen6y7.BasesSecretas6.Crear(sav),
        "capturas7gg" => Gen6y7.Capturas7GG.Crear(sav),
        "celulas" => Gen6y7.CelulasZygarde.Crear(sav),
        "festival" => Gen6y7.FestiPlaza.Crear(sav),
        _ => null,
    };

    /// <summary>
    /// Predicado BARATO de <see cref="EditoresGen6y7"/>: dice si la entrada tiene editor de esta generacion para este guardado SIN construirlo (un <c>case</c> por entrada, con el <c>Aplica(sav)</c> de su formulario).
    /// La pantalla Partida lo llama para las 40 entradas al abrirse; construir los editores grandes (tablas de Misc de Gen. 4/5) solo para saber si existen seria lento. Tiene que coincidir con <c>Crear(sav) is not null</c> (lo comprueba una prueba).
    /// </summary>
    private static bool AplicaGen6y7(string id, SaveFile sav) => id switch
    {
        "bayas6" => Gen6y7.BayasXY.Aplica(sav),
        "roamer" => Gen6y7.Errante6.Aplica(sav),
        "poderes6" => Gen6y7.PoderO.Aplica(sav),
        "pokeblocks6" => Gen6y7.PokecubosORAS.Aplica(sav),
        "pokeflautas" => Gen6y7.Pokelitos.Aplica(sav),
        "link6" => Gen6y7.NexoPokemon.Aplica(sav),
        "supertrain" => Gen6y7.SuperEntrenamiento.Aplica(sav),
        "habichuelas" => Gen6y7.Pokehabas.Aplica(sav),
        "hof" => Gen6y7.SalonFama7.Aplica(sav) || Gen6y7.SalonFama6.Aplica(sav),
        "basessecretas" => Gen6y7.BasesSecretas6.Aplica(sav),
        "capturas7gg" => Gen6y7.Capturas7GG.Aplica(sav),
        "celulas" => Gen6y7.CelulasZygarde.Aplica(sav),
        "festival" => Gen6y7.FestiPlaza.Aplica(sav),
        _ => false,
    };
}
