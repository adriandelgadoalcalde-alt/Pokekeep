// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Desafios;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios;

/// <summary>
/// Los cinco formularios nuevos de R-013 (lo que PKHeX no edita): Colina Entrenador de Esmeralda, puntos del Metro Batalla de Gen. 5, Copa Mundial Pokémon (PWT) de Negro 2 y Blanco 2, Torre de Batalla de Diamante Brillante y Perla Reluciente
/// y Royale de Z-A. Cada uno enseña los datos de la instalación en cada modo y los botones «Máximo» y «Todo al máximo» (y «Ir al jefe» donde la biblioteca lo admite), todo a través de <see cref="DesafiosP"/>.
/// No tienen equivalente en el PKHeX oficial: no hay paridad que comprobar (ver la marca de A4).
/// </summary>
internal static class FormulariosDesafios
{
    private static readonly Dictionary<string, (Func<SaveFile, bool> Aplica, Func<InstalacionDesafio, bool> Filtro, DesafiosP.Opciones Opciones)> Formularios = new()
    {
        ["colina"] = (s => s is SAV3E, i => i is ColinaEntrenador3, new(PorModo: true, ConCampos: true)),
        ["metropb"] = (s => s is SAV5, i => i is PuntosMetro5, new(PorModo: true, ConCampos: true)),
        ["pwt"] = (s => s is SAV5B2W2, i => i is Pwt5, new(PorModo: false, ConCampos: true)),
        ["torredbpr"] = (s => s is SAV8BS, i => i is Torre8b, new(PorModo: true, ConCampos: true)),
        ["royale"] = (s => s is SAV9ZA, i => i is Royale9a, new(PorModo: true, ConCampos: true)),
    };

    public static bool Aplica(string id, SaveFile sav) => Formularios.TryGetValue(id, out var f) && f.Aplica(sav);

    public static EditorDefP? Crear(string id, SaveFile sav)
    {
        if (!Formularios.TryGetValue(id, out var f) || !f.Aplica(sav)) return null;
        var pestanas = DesafiosP.Pestanas(sav, f.Filtro, f.Opciones);
        return pestanas.Count == 0 ? null : new EditorDefP(id, IdiomaInterfaz.T($"Partida.Especifico.{id}.Titulo"), pestanas, [], IdiomaInterfaz.T($"Partida.Especifico.{id}.Descripcion"));
    }
}

/// <summary>Los desafíos que se añaden a formularios que ya existían (Varios de Gen. 3, 4 y 5, Pokéathlon).</summary>
internal static class DesafiosForm
{
    public static EditorDefP? ConDesafios(string id, SaveFile sav, EditorDefP? def) => id switch
    {
        "misc" => DesafiosP.ConDesafios(def, sav, i => i is Frente3Esmeralda or Frente4 or Metro5),
        "pokeathlon" => DesafiosP.ConDesafios(def, sav, i => i is Pokeathlon4Maximo, new DesafiosP.Opciones(PorModo: false)),
        _ => def,
    };
}
