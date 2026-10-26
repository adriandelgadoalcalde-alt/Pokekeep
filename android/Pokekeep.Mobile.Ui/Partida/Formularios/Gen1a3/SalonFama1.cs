// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen1a3;

/// <summary>
/// PILOTO de la F8-0 (PK-SAV-002): <c>SAV_HallOfFame1</c>, el Salon de la Fama de Rojo, Azul y Amarillo: 50 equipos de hasta 6 Pokemon (especie, nivel y mote) y el numero de equipos registrados.
/// Plantilla de los formularios «de lista»: una <see cref="ColeccionDefP"/> con un elemento por equipo, escritura por GRUPO (el formulario escribe especie, nivel y mote del Pokemon juntos, en ese orden) y las acciones que
/// reordenan la lista (<c>Delete</c>, <c>RegisterParty</c>, <c>Clear</c>) como acciones <see cref="CampoP.Inmediata"/>.
/// </summary>
internal static class SalonFama1
{
    public const string Form = "SAV_HallOfFame1";
    public static readonly string[] Censo = ["PK-SAV-002"];
    private const int Equipos = HallOfFameReader1.TeamCount, Huecos = HallOfFameReader1.SlotsPerTeam;

    private static HallOfFameReader1 Fama(SaveFile sav) => ((SAV1)sav).HallOfFame;
    private static string L(string control) => EtiquetasLang.Requerir(Form, control);

    /// <summary>Estado de un equipo como el cuadro de la izquierda del formulario: vacio, completo o «3 de 6».</summary>
    private static string Estado(SaveFile sav, int equipo)
    {
        var n = Fama(sav).GetTeamMemberCount(equipo);
        return n switch { 0 => IdiomaInterfaz.T("Form.Fama1.Vacio"), Huecos => IdiomaInterfaz.T("Form.Fama1.Completo"), _ => IdiomaInterfaz.F("Form.Fama.Miembros", n, Huecos) };
    }

    private static bool TieneMote(SaveFile sav, ushort especie, string mote)
        => mote != SpeciesName.GetSpeciesNameGeneration(especie, sav.Language, 1);

    private static string NombreDeEspecie(SaveFile sav, long especie)
        => especie is 0 or > (int)Species.Mew ? string.Empty : SpeciesName.GetSpeciesNameGeneration((ushort)especie, sav.Language, 1);

    private static IEnumerable<CampoP> CamposDelHueco(ColeccionDefP col, int equipo, int hueco)
    {
        var ide = col.IdCampo(equipo, $"h{hueco}.especie");
        var idn = col.IdCampo(equipo, $"h{hueco}.nivel");
        var idm = col.IdCampo(equipo, $"h{hueco}.mote");
        var ida = col.IdCampo(equipo, $"h{hueco}.apodado");
        var grupo = $"hof1:{equipo}:{hueco}";

        // El formulario escribe los tres juntos cada vez que cambia uno (SaveEntity): especie primero y, si esta vacia o fuera de la 1.ª generacion, el hueco se limpia y no se escribe mas.
        yield return P.Opcion(ide, L("Label_Species"), c => Fama(c.Sav).GetEntity(equipo, hueco).Species,
            (c, v) =>
            {
                var e = Fama(c.Sav).GetEntity(equipo, hueco);
                if (v is 0 or > (int)Species.Mew) e.Clear(); else e.Species = (ushort)v;
            },
            c => P.Lista(c.Datos.SpeciesDataSource.Where(x => x.Value <= (int)Species.Mew)), titulo: L("Label_Species"), prioridad: 0, grupo: grupo,
            deriva: c => c.Marca(ida) ? new Dictionary<string, object?>() : new Dictionary<string, object?> { [idm] = NombreDeEspecie(c.Sav, c.Entero(ide)) }, especie: true);
        yield return P.Entero(idn, L("L_Level"), c => Fama(c.Sav).GetEntity(equipo, hueco).Level,
            (c, v) => { if (c.Entero(ide) is not (0 or > (int)Species.Mew)) { var e = Fama(c.Sav).GetEntity(equipo, hueco); e.Level = (byte)v; } }, 0, 255, prioridad: 1, grupo: grupo);
        // El mote solo se edita con «Mote» marcado; sin marcar sigue a la especie (CB_Species_SelectedIndexChanged).
        yield return P.Marca(ida, L("CHK_Nicknamed"), c => { var e = Fama(c.Sav).GetEntity(equipo, hueco); return TieneMote(c.Sav, e.Species, e.Nickname); }, null,
            sinEscritura: true, ayuda: IdiomaInterfaz.T("Form.Fama1.MoteAyuda"),
            deriva: c => c.Marca(ida) ? new Dictionary<string, object?>() : new Dictionary<string, object?> { [idm] = NombreDeEspecie(c.Sav, c.Entero(ide)) });
        yield return P.Texto(idm, L("TB_Nickname"), c => Fama(c.Sav).GetEntity(equipo, hueco).Nickname,
            (c, v) =>
            {
                if (c.Entero(ide) is 0 or > (int)Species.Mew) return;
                var e = Fama(c.Sav).GetEntity(equipo, hueco);
                if (e.Nickname != v) e.Nickname = v;   // se conserva la basura del mote si no cambia
            },
            maxFn: c => ((SAV1)c.Sav).Japanese ? 5 : 10, prioridad: 2, grupo: grupo,
            motivo: c => c.Marca(ida) ? null : IdiomaInterfaz.T("Form.Fama1.MoteMotivo"));
    }

    /// <summary>Predicado barato de <see cref="Crear"/>: el editor existe para este guardado.</summary>
    public static bool Aplica(SaveFile sav) => sav is SAV1;

    public static EditorDefP? Crear(SaveFile sav)
    {
        if (sav is not SAV1) return null;
        ColeccionDefP? col = null;
        col = new ColeccionDefP
        {
            Id = "equipos", Elemento = IdiomaInterfaz.T("Form.Equipo"), Cantidad = _ => Equipos,
            Titulo = (_, i) => $"{i + 1:00}",
            Resumen = (c, i) => Estado(c.Sav, i),
            Secciones = (c, i) =>
            {
                var secciones = new List<SeccionP>();
                for (var h = 0; h < Huecos; h++)
                {
                    secciones.Add(new SeccionP($"{L("GB_Entry")} {h + 1}", [.. CamposDelHueco(col!, i, h)]));
                }
                // «Limpiar equipo» (B_Delete): quita este equipo y los siguientes suben; el primero no se puede quitar (B_Delete.Enabled = team > 0).
                secciones.Add(new SeccionP(string.Empty, [P.Accion(col!.IdCampo(i, "quitar"), L("B_Delete"), c => Fama(c.Sav).Delete(i),
                    motivo: c => i == 0 ? IdiomaInterfaz.T("Form.Fama1.Primero") : null, inmediata: true, textoBoton: IdiomaInterfaz.T("Form.Quitar"),
                    confirmacion: IdiomaInterfaz.T("Form.Fama1.QuitarConfirmar"),
                    ayuda: IdiomaInterfaz.T("Form.Fama1.QuitarAyuda"))]));
                return secciones;
            },
        };
        var general = new SeccionP(string.Empty,
        [
            P.Entero("equipos", L("L_Clears"), c => ((SAV1)c.Sav).HallOfFameCount, (c, v) => ((SAV1)c.Sav).HallOfFameCount = (byte)v, 0, 255, ayuda: IdiomaInterfaz.T("Form.Fama1.Cuenta")),
            P.Accion("anadir", L("B_SetParty"), c => { var s = (SAV1)c.Sav; s.HallOfFameCount = Fama(s).RegisterParty(s, s.HallOfFameCount); }, inmediata: true, textoBoton: IdiomaInterfaz.T("Form.Anadir"),
                ayuda: IdiomaInterfaz.T("Form.Fama1.AnadirAyuda")),
            P.Accion("limpiar", L("B_ClearAll"), c => { Fama(c.Sav).Clear(); ((SAV1)c.Sav).HallOfFameCount = 0; }, inmediata: true, textoBoton: IdiomaInterfaz.T("Form.Limpiar"),
                confirmacion: IdiomaInterfaz.T("Form.Fama1.LimpiarConfirmar"), ayuda: IdiomaInterfaz.T("Form.Fama1.LimpiarAyuda")),
        ]);
        return new EditorDefP("hof", IdiomaInterfaz.T("Form.Fama"),
            [new PestanaP("general", IdiomaInterfaz.T("Form.General"), [general]), new PestanaP("equipos", IdiomaInterfaz.T("Form.Equipos"), [], Coleccion: col)],
            Censo, IdiomaInterfaz.T("Form.Fama1.Descripcion"));
    }
}
