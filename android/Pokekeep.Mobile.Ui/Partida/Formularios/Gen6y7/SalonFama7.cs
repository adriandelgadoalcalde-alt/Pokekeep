// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen6y7;

/// <summary>
/// PK-SAV-134 · <c>SAV_HallOfFame7</c> («Visor del Hall de la Fama»), el Hall de la Fama de Sol/Luna y Ultrasol/Ultraluna: las 12 especies que guarda el juego (los seis del «Primer» equipo y los seis del «Actual») y,
/// solo en Ultrasol/Ultraluna, la constante de cifrado del inicial en hexadecimal (8 cifras). «Guardar» (<c>B_Close</c>) escribe las 12 especies con <c>SetEntry</c> y la constante; aqui se escribe lo que cambia.
/// Es la entrada <c>hof</c> de los formatos de la 7.ª generacion; los de las otras (1, 3 y 6) la rellenan sus bloques.
/// </summary>
internal static class SalonFama7
{
    public const string Form = "SAV_HallOfFame7";
    public static readonly string[] Censo = ["PK-SAV-134"];

    private static string L(string control) => EtiquetasLang.Requerir(Form, control);
    private static HallOfFame7 Fama(ContextoP c) => ((SAV7)c.Sav).EventWork.Fame;

    /// <summary>Predicado barato de <see cref="Crear"/>: el editor existe para este guardado.</summary>
    public static bool Aplica(SaveFile sav) => sav is SAV7;

    public static EditorDefP? Crear(SaveFile sav)
    {
        if (sav is not SAV7) return null;
        IReadOnlyList<OpcionP> Especies(ContextoP c) => P.Lista(c.Datos.SpeciesDataSource.Where(x => x.Value <= c.Sav.MaxSpeciesID));
        IEnumerable<CampoP> Equipo(string prefijo, int desde) => Enumerable.Range(0, 6).Select(k =>
        {
            var indice = desde + k;
            return P.Opcion($"{prefijo}{k + 1}", L($"L_{(desde == 0 ? "F" : "C")}{k + 1}"), c => Fama(c).GetEntry(indice), (c, v) => Fama(c).SetEntry(indice, (ushort)v), Especies, titulo: L($"L_{(desde == 0 ? "F" : "C")}{k + 1}"));
        });
        var primer = new SeccionP(L("L_First"), [.. Equipo("primer", 0)]);
        var actual = new SeccionP(L("L_Current"), [.. Equipo("actual", 6)]);
        // La constante de cifrado del inicial solo existe en Ultrasol/Ultraluna (en las demas el formulario oculta la caja).
        var secciones = new List<SeccionP> { primer, actual };
        if (sav is SAV7USUM)
            secciones.Add(new SeccionP(string.Empty,
            [
                P.Entero("ec", L("L_EC"), c => ((SAV7USUM)c.Sav).Misc.StarterEncryptionConstant, (c, v) => ((SAV7USUM)c.Sav).Misc.StarterEncryptionConstant = (uint)v, 0, uint.MaxValue, hex: true, ayuda: IdiomaInterfaz.T("SalonFama7.HexadecimalCifras")),
            ]));
        return new EditorDefP("hof", IdiomaInterfaz.T("SalonFama6.HallFama"), [new PestanaP("fama", EtiquetasLang.RequerirFormulario(Form), secciones)], Censo, IdiomaInterfaz.T("SalonFama7.EspeciesHallFamaGeneracion"));
    }
}
