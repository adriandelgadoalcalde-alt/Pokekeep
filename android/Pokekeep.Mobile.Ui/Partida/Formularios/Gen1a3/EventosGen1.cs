// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;
using Pokekeep.Mobile.Ui.Ajustes.Pkhex;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen1a3;

/// <summary>
/// PILOTO de la F8-0 (PK-SAV-001): <c>SAV_EventReset1</c>, los Pokémon del mapa de la 1.ª generación que se pueden hacer reaparecer (<c>G1OverworldSpawner</c>). Un boton por Pokémon, habilitado solo si ahora esta oculto;
/// al pulsarlo WinForms hace <c>pair.Reset()</c> y, al cerrar, <c>Overworld.Save()</c>. Aqui cada boton es una <see cref="TipoP.Accion"/> pendiente que se ejecuta al aplicar (misma llamada, mismo orden) y se deshace entera.
/// Plantilla de los formularios «de botones» (Misc2, Misc8b, Pokéblocks...).
/// </summary>
internal static class EventosGen1
{
    public const string Form = "SAV_EventReset1";
    public static readonly string[] Censo = ["PK-SAV-001"];

    /// <summary>Nombre del Pokémon de un par de banderas, como el formulario: «FlagVoltorb_1» da «Voltorb 1»; la especie va en el idioma de la aplicacion.</summary>
    internal static string Nombre(string clave, string[] especies)
    {
        var nombre = clave.AsSpan(G1OverworldSpawner.FlagPropertyPrefix.Length);
        var guion = nombre.IndexOf('_');
        var especie = guion == -1 ? nombre : nombre[..guion];
        SpeciesName.TryGetSpecies(especie, (int)LanguageID.English, out var id);
        var local = especies[id];
        return guion == -1 ? local : $"{local} {nombre[(guion + 1)..]}";
    }

    private static FlagPairG1Detail Par(SaveFile sav, string clave) => new G1OverworldSpawner((SAV1)sav).GetFlagPairs().First(p => p.Name == clave);

    /// <summary>Predicado barato de <see cref="Crear"/>: el editor existe para este guardado.</summary>
    public static bool Aplica(SaveFile sav) => sav is SAV1;

    public static EditorDefP? Crear(SaveFile sav)
    {
        if (sav is not SAV1 s1) return null;
        // Los pares dependen del juego (Amarillo tiene otros): se listan una vez, por nombre, como el formulario.
        var claves = new G1OverworldSpawner(s1).GetFlagPairs().OrderBy(z => z.Name).Select(z => z.Name).ToList();
        var campos = claves.Select(clave => P.Accion(
            $"reaparece:{clave}", clave,
            ejecutar: c => { var spawner = new G1OverworldSpawner((SAV1)c.Sav); spawner.GetFlagPairs().First(p => p.Name == clave).Reset(); spawner.Save(); },
            motivo: c => Par(c.Sav, clave).IsHidden ? null : IdiomaInterfaz.T("Form.Eventos1.YaVisible"),
            ayuda: IdiomaInterfaz.T("Form.Eventos1.Ayuda"),
            textoBoton: IdiomaInterfaz.T("Form.Eventos1.Boton"),
            etiquetaFn: c => Nombre(clave, c.Textos.specieslist))).ToList();
        return new EditorDefP("eventos1", ProgramaPkhex.Idioma == "en" ? EtiquetasLang.RequerirFormulario(Form) : IdiomaInterfaz.T("Form.Eventos1.Titulo"),
            [new PestanaP("reaparicion", EtiquetasLang.RequerirFormulario(Form), [new SeccionP(string.Empty, campos)])],
            Censo, IdiomaInterfaz.T("Form.Eventos1.Descripcion"));
    }
}
