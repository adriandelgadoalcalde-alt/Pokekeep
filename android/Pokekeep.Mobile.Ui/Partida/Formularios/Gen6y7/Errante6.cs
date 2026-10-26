// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen6y7;

/// <summary>
/// PK-SAV-116 · <c>SAV_Roamer6</c>, el Pokémon errante de Kalos (solo X/Y): especie (Articuno, Zapdos o Moltres), estado y veces visto. Mismos limites que el formulario
/// (las veces visto llegan a 11 en el control; si la partida trae mas, el limite es el valor actual para no perderlo). Si la partida aun no tiene la especie escrita (no se ha vencido a la Liga),
/// el formulario la deduce de la eleccion del inicial (<c>EventWork.GetWork(48)</c>) y al guardar la escribe siempre; aqui se enseña igual la deducida y solo se escribe lo que el usuario cambia.
/// </summary>
internal static class Errante6
{
    public const string Form = "SAV_Roamer6";
    public static readonly string[] Censo = ["PK-SAV-116"];
    private const int OffsetEspecie = 144, EleccionInicial = 48, MaxVeces = 11;

    private static string L(string control) => EtiquetasLang.Requerir(Form, control);
    private static Roamer6 Errante(SaveFile sav) => ((SAV6XY)sav).Encount.Roamer;

    /// <summary>La especie que ve el formulario al abrir: la escrita o, si es 0, la que corresponde al inicial elegido.</summary>
    internal static long EspecieInicial(SaveFile sav)
    {
        var r = Errante(sav);
        if (r.Species != 0) return r.Species;
        return OffsetEspecie + Math.Clamp((int)((SAV6XY)sav).EventWork.GetWork(EleccionInicial), 0, 2);
    }

    /// <summary>Predicado barato de <see cref="Crear"/>: el editor existe para este guardado.</summary>
    public static bool Aplica(SaveFile sav) => sav is SAV6XY;

    public static EditorDefP? Crear(SaveFile sav)
    {
        if (sav is not SAV6XY) return null;
        var campos = new List<CampoP>
        {
            P.Opcion("especie", L("L_Species"), c => EspecieInicial(c.Sav), (c, v) => Errante(c.Sav).Species = (ushort)v,
                c => [new(OffsetEspecie, c.Textos.specieslist[(int)Species.Articuno]), new(OffsetEspecie + 1, c.Textos.specieslist[(int)Species.Zapdos]), new(OffsetEspecie + 2, c.Textos.specieslist[(int)Species.Moltres])],
                titulo: L("L_Species"), ayuda: IdiomaInterfaz.T("Errante6.CualTresAvesLegendarias")),
            P.Opcion("estado", L("L_RoamState"), c => (long)Errante(c.Sav).RoamStatus, (c, v) => Errante(c.Sav).RoamStatus = (Roamer6State)v,
                _ => P.Lista(IdiomaInterfaz.T("Errante6.Inactivo"), IdiomaInterfaz.T("Errante6.Errante"), IdiomaInterfaz.T("Errante6.Estacionario"), IdiomaInterfaz.T("Varios5Inicio.Derrotado"), IdiomaInterfaz.T("Pokedex8.Capturado")), titulo: L("L_RoamState")),
            P.Entero("veces", L("L_TimesEncountered"), c => Errante(c.Sav).TimesEncountered, (c, v) => Errante(c.Sav).TimesEncountered = (uint)v, 0, MaxVeces,
                maxFn: c => Math.Max(MaxVeces, Errante(c.Sav).TimesEncountered)),
        };
        return new EditorDefP("roamer", IdiomaInterfaz.T("Errante6.PokemonErrante"), [new PestanaP("errante", EtiquetasLang.RequerirFormulario(Form), [new SeccionP(string.Empty, campos)])],
            Censo, IdiomaInterfaz.T("Errante6.EspecieEstadoVecesVisto"));
    }
}
