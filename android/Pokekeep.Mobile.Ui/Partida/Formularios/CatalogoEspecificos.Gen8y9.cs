// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;

namespace Pokekeep.Mobile.Ui.Partida.Formularios;

public static partial class CatalogoEspecificos
{
    /// <summary>
    /// Editores de Gen. 8-9 (bloque 8b-2): un <c>case</c> por entrada de navegacion, que devuelve el editor declarativo del guardado o null si no es de esta generacion / aun no esta hecho.
    /// Este fichero lo edita unicamente el bloque 8b-2.
    /// </summary>
    private static EditorDefP? EditoresGen8y9(string id, SaveFile sav) => id switch
    {
        "pozoffins8b" => Gen8y9.Pokochos8b.Crear(sav),
        "pegatinas8b" => Gen8y9.SellosBDSP.Crear(sav),
        "subsuelo" => Gen8y9.SubsueloBDSP.Crear(sav),
        "misc" => Gen8y9.VariosBDSP.Crear(sav),
        "eventos8b" => Gen8y9.EventosBDSP.Crear(sav),
        "incursiones" => Gen8y9.IncursionesGalar.Crear(sav, MaxRaidOrigin.Galar) ?? Gen8y9.IncursionesPaldea.Crear(sav, TeraRaidOrigin.Paldea),
        "incursiones-dlc1" => Gen8y9.IncursionesGalar.Crear(sav, MaxRaidOrigin.IsleOfArmor) ?? Gen8y9.IncursionesPaldea.Crear(sav, TeraRaidOrigin.Kitakami),
        "incursiones-dlc2" => Gen8y9.IncursionesGalar.Crear(sav, MaxRaidOrigin.CrownTundra) ?? Gen8y9.IncursionesPaldea.Crear(sav, TeraRaidOrigin.BlueberryAcademy),
        "incursiones7" => Gen8y9.IncursionesSieteEstrellas.Crear(sav),
        "moda" => Gen8y9.Moda9.Crear(sav),
        "donuts" => Gen8y9.Donuts9a.Crear(sav),
        "eventos9a" => Gen8y9.EventosZA.Crear(sav),
        "bloques8" => Gen8y9.BloquesSC.Crear(sav),
        _ => null,
    };

    /// <summary>
    /// Predicado BARATO de <see cref="EditoresGen8y9"/>: dice si la entrada tiene editor de esta generacion para este guardado SIN construirlo (un <c>case</c> por entrada, con el <c>Aplica(sav)</c> de su formulario).
    /// La pantalla Partida lo llama para las 40 entradas al abrirse; construir los editores grandes (tablas de Misc de Gen. 4/5) solo para saber si existen seria lento. Tiene que coincidir con <c>Crear(sav) is not null</c> (lo comprueba una prueba).
    /// </summary>
    private static bool AplicaGen8y9(string id, SaveFile sav) => id switch
    {
        "pozoffins8b" => Gen8y9.Pokochos8b.Aplica(sav),
        "pegatinas8b" => Gen8y9.SellosBDSP.Aplica(sav),
        "subsuelo" => Gen8y9.SubsueloBDSP.Aplica(sav),
        "misc" => Gen8y9.VariosBDSP.Aplica(sav),
        "eventos8b" => Gen8y9.EventosBDSP.Aplica(sav),
        "incursiones" => Gen8y9.IncursionesGalar.Aplica(sav, MaxRaidOrigin.Galar) || Gen8y9.IncursionesPaldea.Aplica(sav, TeraRaidOrigin.Paldea),
        "incursiones-dlc1" => Gen8y9.IncursionesGalar.Aplica(sav, MaxRaidOrigin.IsleOfArmor) || Gen8y9.IncursionesPaldea.Aplica(sav, TeraRaidOrigin.Kitakami),
        "incursiones-dlc2" => Gen8y9.IncursionesGalar.Aplica(sav, MaxRaidOrigin.CrownTundra) || Gen8y9.IncursionesPaldea.Aplica(sav, TeraRaidOrigin.BlueberryAcademy),
        "incursiones7" => Gen8y9.IncursionesSieteEstrellas.Aplica(sav),
        "moda" => Gen8y9.Moda9.Aplica(sav),
        "donuts" => Gen8y9.Donuts9a.Aplica(sav),
        "eventos9a" => Gen8y9.EventosZA.Aplica(sav),
        "bloques8" => Gen8y9.BloquesSC.Aplica(sav),
        _ => false,
    };
}
