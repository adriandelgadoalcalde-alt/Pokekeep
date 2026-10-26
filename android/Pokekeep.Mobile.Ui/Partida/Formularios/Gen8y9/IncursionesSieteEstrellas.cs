// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen8y9;

/// <summary>
/// PK-SAV-196 · <c>SAV_RaidSevenStar9</c>, las incursiones de 7 estrellas de Escarlata y Púrpura: por cada una (<c>RaidSevenStar9.CountAll</c>) su identificador (la fecha en que se distribuyó), si se ha capturado al jefe y si se le
/// ha derrotado alguna vez. El formulario las enseña en un cuadro de propiedades (sin ordenar) que escribe sobre una copia que «Guardar» copia al original. El identificador se escribe en las dos copias del dato (la de
/// capturas y la de derrotas, si existe); «derrotado» se lee de la segunda si existe y, si no, de la primera.
/// </summary>
internal static class IncursionesSieteEstrellas
{
    public const string Form = "SAV_RaidSevenStar9";
    public static readonly string[] Censo = ["PK-SAV-196"];

    private static string L(string control) => EtiquetasLang.Requerir(Form, control);
    private static RaidSevenStar9 Lista(ContextoP c) => ((SAV9SV)c.Sav).RaidSevenStar;

    /// <summary>Predicado barato de <see cref="Crear"/>: el editor existe para este guardado.</summary>
    public static bool Aplica(SaveFile sav) => sav is SAV9SV;

    public static EditorDefP? Crear(SaveFile sav)
    {
        if (sav is not SAV9SV) return null;
        ColeccionDefP? col = null;
        col = new ColeccionDefP
        {
            Id = "incursiones7", Elemento = IdiomaInterfaz.T("Libreria.Origen.Raid"), Cantidad = c => Lista(c).CountAll,
            Titulo = (c, i) => { var d = Lista(c).GetRaid(i); return $"{i + 1:0000} · {(d.Identifier == 0 ? IdiomaInterfaz.T("IncursionesSieteEstrellas.Distribuir") : d.Identifier.ToString(System.Globalization.CultureInfo.InvariantCulture))}"; },
            Resumen = (c, i) => { var d = Lista(c).GetRaid(i); return d.Identifier == 0 ? null : d.Captured ? IdiomaInterfaz.T("Pokedex8.Capturado") : d.Defeated ? IdiomaInterfaz.T("Varios5Inicio.Derrotado") : IdiomaInterfaz.T("IncursionesSieteEstrellas.Pendiente"); },
            Secciones = (_, i) =>
            [
                new SeccionP(string.Empty,
                [
                    P.Entero(col!.IdCampo(i, "identificador"), L("Prop_Identifier"), c => Lista(c).GetRaid(i).Identifier, (c, v) => Lista(c).GetRaid(i).Identifier = (uint)v, 0, uint.MaxValue, ayuda: IdiomaInterfaz.T("IncursionesSieteEstrellas.IdentificadorIncursionEstrellasCoincide")),
                    P.Marca(col.IdCampo(i, "capturado"), L("Prop_Captured"), c => Lista(c).GetRaid(i).Captured, (c, v) => Lista(c).GetRaid(i).Captured = v, ayuda: IdiomaInterfaz.T("IncursionesSieteEstrellas.JugadorCapturadoJefeTeratipo")),
                    P.Marca(col.IdCampo(i, "derrotado"), L("Prop_Defeated"), c => Lista(c).GetRaid(i).Defeated, (c, v) => Lista(c).GetRaid(i).Defeated = v, ayuda: IdiomaInterfaz.T("IncursionesSieteEstrellas.JugadorDerrotadoJefeTeratipo")),
                ]),
            ],
        };
        return new EditorDefP("incursiones7", IdiomaInterfaz.T("Partida.Especifico.incursiones7.Titulo"), [new PestanaP("incursiones", EtiquetasLang.RequerirFormulario(Form), [], Coleccion: col)], Censo, IdiomaInterfaz.T("IncursionesSieteEstrellas.IncursionesEstrellas"));
    }
}
