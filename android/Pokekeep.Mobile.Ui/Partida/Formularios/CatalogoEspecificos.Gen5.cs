// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;

namespace Pokekeep.Mobile.Ui.Partida.Formularios;

public static partial class CatalogoEspecificos
{
    /// <summary>
    /// Editores de Gen. 5 (bloque 8a-3): un <c>case</c> por entrada de navegacion, que devuelve el editor declarativo del guardado o null si no es de esta generacion / aun no esta hecho.
    /// Este fichero lo edita unicamente el bloque 8a-3.
    /// </summary>
    private static EditorDefP? EditoresGen5(string id, SaveFile sav) => id switch
    {
        "torreunion" => Gen5.TorreUnion.Crear(sav),
        "globallink" => Gen5.EnlaceGlobal.Crear(sav),
        "medallas5" => Gen5.Insignias5.Crear(sav),
        "dlc5" => Gen5.ContenidoDescargable.Crear(sav),
        "misc" => Gen5.Varios5.Crear(sav),
        "avenida" => Gen5.GaleriaUnion.Crear(sav),
        _ => null,
    };

    /// <summary>
    /// Predicado BARATO de <see cref="EditoresGen5"/>: dice si la entrada tiene editor de esta generacion para este guardado SIN construirlo (un <c>case</c> por entrada, con el <c>Aplica(sav)</c> de su formulario).
    /// La pantalla Partida lo llama para las 40 entradas al abrirse; construir los editores grandes (tablas de Misc de Gen. 4/5) solo para saber si existen seria lento. Tiene que coincidir con <c>Crear(sav) is not null</c> (lo comprueba una prueba).
    /// </summary>
    private static bool AplicaGen5(string id, SaveFile sav) => id switch
    {
        "torreunion" => Gen5.TorreUnion.Aplica(sav),
        "globallink" => Gen5.EnlaceGlobal.Aplica(sav),
        "medallas5" => Gen5.Insignias5.Aplica(sav),
        "dlc5" => Gen5.ContenidoDescargable.Aplica(sav),
        "misc" => Gen5.Varios5.Aplica(sav),
        "avenida" => Gen5.GaleriaUnion.Aplica(sav),
        _ => false,
    };
}
