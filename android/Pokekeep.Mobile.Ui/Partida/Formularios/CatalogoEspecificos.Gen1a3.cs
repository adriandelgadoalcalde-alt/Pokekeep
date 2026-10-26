// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;

namespace Pokekeep.Mobile.Ui.Partida.Formularios;

public static partial class CatalogoEspecificos
{
    /// <summary>
    /// Editores de Gen. 1-3 (bloque 8a-1): un <c>case</c> por entrada de navegacion, que devuelve el editor declarativo del guardado o null si no es de esta generacion / aun no esta hecho.
    /// Este fichero lo edita unicamente el bloque 8a-1.
    /// </summary>
    private static EditorDefP? EditoresGen1a3(string id, SaveFile sav) => id switch
    {
        "eventos1" => Gen1a3.EventosGen1.Crear(sav),
        "hof" => Gen1a3.SalonFama1.Crear(sav) ?? Gen1a3.SalonFama3.Crear(sav),
        "misc" => Gen1a3.VariosGen2.Crear(sav) ?? Gen1a3.VariosGen3.Crear(sav),
        "roamer" => Gen1a3.ErranteGen3.Crear(sav),
        "rtc3" => Gen1a3.RelojGen3.Crear(sav),
        "basessecretas" => Gen1a3.BasesSecretas3.Crear(sav),
        _ => null,
    };

    /// <summary>
    /// Predicado BARATO de <see cref="EditoresGen1a3"/>: dice si la entrada tiene editor de esta generacion para este guardado SIN construirlo (un <c>case</c> por entrada, con el <c>Aplica(sav)</c> de su formulario).
    /// La pantalla Partida lo llama para las 40 entradas al abrirse; construir los editores grandes (tablas de Misc de Gen. 4/5) solo para saber si existen seria lento. Tiene que coincidir con <c>Crear(sav) is not null</c> (lo comprueba una prueba).
    /// </summary>
    private static bool AplicaGen1a3(string id, SaveFile sav) => id switch
    {
        "eventos1" => Gen1a3.EventosGen1.Aplica(sav),
        "hof" => Gen1a3.SalonFama1.Aplica(sav) || Gen1a3.SalonFama3.Aplica(sav),
        "misc" => Gen1a3.VariosGen2.Aplica(sav) || Gen1a3.VariosGen3.Aplica(sav),
        "roamer" => Gen1a3.ErranteGen3.Aplica(sav),
        "rtc3" => Gen1a3.RelojGen3.Aplica(sav),
        "basessecretas" => Gen1a3.BasesSecretas3.Aplica(sav),
        _ => false,
    };
}
