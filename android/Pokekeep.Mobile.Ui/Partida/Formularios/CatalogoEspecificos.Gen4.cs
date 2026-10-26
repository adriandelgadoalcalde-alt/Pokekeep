// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;

namespace Pokekeep.Mobile.Ui.Partida.Formularios;

public static partial class CatalogoEspecificos
{
    /// <summary>
    /// Editores de Gen. 4 (bloque 8a-2): un <c>case</c> por entrada de navegacion, que devuelve el editor declarativo del guardado o null si no es de esta generacion / aun no esta hecho.
    /// Este fichero lo edita unicamente el bloque 8a-2.
    /// </summary>
    private static EditorDefP? EditoresGen4(string id, SaveFile sav) => id switch
    {
        "apricorn" => Gen4.Bonguri4.Crear(sav),
        "geonet" => Gen4.GeonetGen4.Crear(sav),
        "mielarboles" => Gen4.MielarbolesGen4.Crear(sav),
        "subsuelo" => Gen4.SubsueloGen4.Crear(sav),
        "chatter" => Gen4.ChacharaGen4.Crear(sav),
        "misc" => Gen4.VariosGen4.Crear(sav),
        "pokeathlon" => Gen4.PokeathlonGen4.Crear(sav),
        "equipobr" => Gen4.AccesoriosBr4.Crear(sav),
        "pasesbr" => Gen4.PasesBr4.Crear(sav),
        "dlc4" => Gen4.VideosCombate4.Crear(sav),
        "cintasregalo" => Gen4.CintasRegalo.Crear(sav),   // Gen. 3 (incluidos Colosseum y XD) y Gen. 4 comparten editor: el predicado de WinForms es «sav is IGiftRibbons»
        _ => null,
    };

    /// <summary>
    /// Predicado BARATO de <see cref="EditoresGen4"/>: dice si la entrada tiene editor de esta generacion para este guardado SIN construirlo (un <c>case</c> por entrada, con el <c>Aplica(sav)</c> de su formulario).
    /// La pantalla Partida lo llama para las 40 entradas al abrirse; construir los editores grandes (tablas de Misc de Gen. 4/5) solo para saber si existen seria lento. Tiene que coincidir con <c>Crear(sav) is not null</c> (lo comprueba una prueba).
    /// </summary>
    private static bool AplicaGen4(string id, SaveFile sav) => id switch
    {
        "apricorn" => Gen4.Bonguri4.Aplica(sav),
        "geonet" => Gen4.GeonetGen4.Aplica(sav),
        "mielarboles" => Gen4.MielarbolesGen4.Aplica(sav),
        "subsuelo" => Gen4.SubsueloGen4.Aplica(sav),
        "chatter" => Gen4.ChacharaGen4.Aplica(sav),
        "misc" => Gen4.VariosGen4.Aplica(sav),
        "pokeathlon" => Gen4.PokeathlonGen4.Aplica(sav),
        "equipobr" => Gen4.AccesoriosBr4.Aplica(sav),
        "pasesbr" => Gen4.PasesBr4.Aplica(sav),
        "dlc4" => Gen4.VideosCombate4.Aplica(sav),
        "cintasregalo" => Gen4.CintasRegalo.Aplica(sav),
        _ => false,
    };
}
