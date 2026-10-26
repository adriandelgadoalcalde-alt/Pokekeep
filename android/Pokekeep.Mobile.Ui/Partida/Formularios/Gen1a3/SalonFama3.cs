// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen1a3;

/// <summary>
/// PK-SAV-005: <c>SAV_HallOfFame3</c>, el Hall de la Fama de Rubí, Zafiro, Esmeralda, Rojo Fuego y Verde Hoja: 50 entradas de hasta 6 Pokémon (ID, IDS, PID, mote, nivel y especie). WinForms guarda el miembro abierto
/// con <c>SaveEntry</c> en este orden —ID, IDS, PID, mote (solo si cambió, para conservar la basura), nivel, especie— cada vez que lo cambia o cambia de entrada, y al cerrar vuelca las 50 entradas con
/// <see cref="HallFame3Entry.SetEntries"/>. Aquí cada miembro es un GRUPO de escritura (si cambia un campo se escriben los seis, en ese orden); «Limpiar» (<c>ClearFields</c>) y «Importar todo desde el equipo»
/// (<c>B_ImportParty</c>, con Mayús = a todas las entradas) son acciones inmediatas porque actúan de golpe sobre la entrada.
/// Fuera de alcance, declarado: el visor de caracteres especiales del mote (<c>TrashEditor</c>, Control+clic) y el dibujo del sprite no tienen equivalente en el móvil.
/// </summary>
internal static class SalonFama3
{
    public const string Form = "SAV_HallOfFame3";
    public static readonly string[] Censo = ["PK-SAV-005"];
    private const int Entradas = 50, Miembros = 6;

    private static string L(string control) => EtiquetasLang.Requerir(Form, control);

    /// <summary>Predicado barato de <see cref="Crear"/>: el editor existe para este guardado (<c>SAV3 { IsMisconfiguredSize: false }</c>, como el botón de PKHeX).</summary>
    public static bool Aplica(SaveFile sav) => sav is SAV3 { IsMisconfiguredSize: false };

    private static HallFame3PKM Miembro(HallFame3Entry[] fama, int entrada, int miembro) => fama[entrada].GetMember(miembro);

    /// <summary>Lee, cambia y vuelve a volcar las 50 entradas (como <c>SetEntries</c> al guardar el formulario).</summary>
    private static void Cambiar(SaveFile sav, Action<HallFame3Entry[]> cambia)
    {
        var s = (SAV3)sav;
        var fama = HallFame3Entry.GetEntries(s);
        cambia(fama);
        HallFame3Entry.SetEntries(s, fama);
    }

    private static HallFame3PKM Leer(SaveFile sav, int entrada, int miembro) => Miembro(HallFame3Entry.GetEntries((SAV3)sav), entrada, miembro);

    /// <summary>Un byte del Hall de la Fama: los dos sectores (0x1C000 y 0x1D000, 0xF80 usados cada uno) vistos como un solo bloque, sin copiarlos (<see cref="SAV3.GetHallOfFameData"/> reserva 7.936 bytes por llamada).</summary>
    private static byte ByteDeLaFama(SAV3 sav, int desplazamiento)
        => sav.Data[(desplazamiento < SAV3.SIZE_SECTOR_USED ? 0x1C000 + desplazamiento : 0x1D000 + desplazamiento - SAV3.SIZE_SECTOR_USED)];

    private static string Resumen(SaveFile sav, int entrada)
    {
        // Se lee la especie de cada miembro directamente (los 9 bits bajos de los 2 bytes en el desplazamiento 8): el listado pide este resumen de las 50 entradas cada vez que se pinta, asi que no se copia nada.
        var s = (SAV3)sav;
        var n = 0;
        for (var m = 0; m < Miembros; m++)
        {
            var desplazamiento = (entrada * HallFame3Entry.SIZE) + (m * HallFame3PKM.SIZE) + 8;
            if (SpeciesConverter.GetNational3((ushort)((ByteDeLaFama(s, desplazamiento) | (ByteDeLaFama(s, desplazamiento + 1) << 8)) & 0x1FF)) != 0) n++;
        }
        return n switch { 0 => IdiomaInterfaz.T("Form.Fama3.Vacio"), Miembros => IdiomaInterfaz.T("Form.Fama3.Completo"), _ => IdiomaInterfaz.F("Form.Fama.Miembros", n, Miembros) };
    }

    private static IEnumerable<CampoP> CamposDelMiembro(ColeccionDefP col, int e, int m)
    {
        var idTid = col.IdCampo(e, $"m{m}.tid"); var idSid = col.IdCampo(e, $"m{m}.sid"); var idPid = col.IdCampo(e, $"m{m}.pid");
        var grupo = $"hof3:{e}:{m}";

        yield return P.Entero(idTid, L("L_TID"), c => Leer(c.Sav, e, m).TID16, (c, v) => Cambiar(c.Sav, f => Miembro(f, e, m).TID16 = (ushort)v), 0, 65535, prioridad: 0, grupo: grupo);
        yield return P.Entero(idSid, L("L_SID"), c => Leer(c.Sav, e, m).SID16, (c, v) => Cambiar(c.Sav, f => Miembro(f, e, m).SID16 = (ushort)v), 0, 65535, prioridad: 1, grupo: grupo);
        yield return P.Entero(idPid, L("L_PID"), c => Leer(c.Sav, e, m).PID, (c, v) => Cambiar(c.Sav, f => Miembro(f, e, m).PID = (uint)v), 0, uint.MaxValue, hex: true, prioridad: 2, grupo: grupo,
            detalle: c => $"{L("CHK_Shiny")}: {(ShinyUtil.GetIsShiny3(((uint)c.Entero(idSid) << 16) | (uint)c.Entero(idTid), (uint)c.Entero(idPid)) ? IdiomaInterfaz.T("Comun.Si").ToLowerInvariant() : IdiomaInterfaz.T("Comun.No").ToLowerInvariant())}", ayuda: IdiomaInterfaz.T("Form.AyudaHex8"));
        yield return P.Texto(col.IdCampo(e, $"m{m}.mote"), L("L_Nickname"), c => Leer(c.Sav, e, m).Nickname,
            (c, v) => Cambiar(c.Sav, f => { var pk = Miembro(f, e, m); if (pk.Nickname != v) pk.Nickname = v; }),   // se conserva la basura del mote si no cambia
            maxLongitud: 10, prioridad: 3, grupo: grupo);
        yield return P.Entero(col.IdCampo(e, $"m{m}.nivel"), L("L_Level"), c => Leer(c.Sav, e, m).Level, (c, v) => Cambiar(c.Sav, f => Miembro(f, e, m).Level = (int)v), 0, Experience.MaxLevel, prioridad: 4, grupo: grupo);
        yield return P.Opcion(col.IdCampo(e, $"m{m}.especie"), EtiquetasLang.Requerir("SAV_HallOfFame1", "Label_Species"), c => Leer(c.Sav, e, m).Species,
            (c, v) => Cambiar(c.Sav, f => Miembro(f, e, m).Species = (ushort)v), AyudasGen3.Especies, titulo: EtiquetasLang.Requerir("SAV_HallOfFame1", "Label_Species"), prioridad: 5, grupo: grupo, especie: true);
        // «Limpiar» (B_Clear): ClearFields pone ID, IDS y PID a 0, el mote vacío, el nivel 0 y la especie «---»; el formulario lo escribe con SaveEntry.
        yield return P.Accion(col.IdCampo(e, $"m{m}.limpiar"), L("B_Clear"), c => Cambiar(c.Sav, f =>
            {
                var pk = Miembro(f, e, m);
                pk.TID16 = 0; pk.SID16 = 0; pk.PID = 0;
                if (pk.Nickname != string.Empty) pk.Nickname = string.Empty;
                pk.Level = 0; pk.Species = 0;
            }), inmediata: true, textoBoton: IdiomaInterfaz.T("Form.Limpiar"), confirmacion: IdiomaInterfaz.T("Form.Fama3.LimpiarConfirmar"), ayuda: IdiomaInterfaz.T("Form.Fama3.LimpiarAyuda"));
    }

    public static EditorDefP? Crear(SaveFile sav)
    {
        if (!Aplica(sav)) return null;
        ColeccionDefP? col = null;
        col = new ColeccionDefP
        {
            Id = "entradas", Elemento = IdiomaInterfaz.T("Form.Entrada"), Cantidad = _ => Entradas,
            Titulo = (_, i) => $"{i + 1:00}",
            Resumen = (c, i) => Resumen(c.Sav, i),
            Secciones = (c, i) =>
            {
                var secciones = new List<SeccionP>();
                for (var m = 0; m < Miembros; m++) secciones.Add(new SeccionP(IdiomaInterfaz.F("Form.Fama.Miembro", m + 1), [.. CamposDelMiembro(col!, i, m)]));
                // «Importar todo desde el equipo» (B_ImportParty): copia los seis huecos del equipo (los vacíos, a cero) a esta entrada; con Mayús, a todas las demás también.
                secciones.Add(new SeccionP(string.Empty,
                [
                    P.Accion(col!.IdCampo(i, "importar"), L("B_ImportParty"), c => Importar(c.Sav, i, aTodas: false), inmediata: true, textoBoton: IdiomaInterfaz.T("Form.Fama3.Importar"),
                        confirmacion: IdiomaInterfaz.T("Form.Fama3.ImportarConfirmar"), ayuda: IdiomaInterfaz.T("Form.Fama3.ImportarAyuda")),
                    P.Accion(col!.IdCampo(i, "importar_todas"), IdiomaInterfaz.F("Form.Fama3.TodasEtiqueta", L("B_ImportParty")), c => Importar(c.Sav, i, aTodas: true), inmediata: true, textoBoton: IdiomaInterfaz.T("Form.Fama3.ImportarTodas"),
                        confirmacion: IdiomaInterfaz.T("Form.Fama3.TodasConfirmar"), ayuda: IdiomaInterfaz.T("Form.Fama3.TodasAyuda")),
                ]));
                return secciones;
            },
        };
        return new EditorDefP("hof", IdiomaInterfaz.T("Form.Fama"), [new PestanaP("entradas", IdiomaInterfaz.T("Form.Entradas"), [], Coleccion: col)], Censo, IdiomaInterfaz.T("Form.Fama3.Descripcion"));
    }

    /// <summary>El cuerpo de <c>B_ImportParty_Click</c>: equipo de 6 huecos (los que sobran, un <see cref="PK3"/> en blanco), copiado a la entrada abierta y, con Mayús, a todas las demás.</summary>
    private static void Importar(SaveFile sav, int entrada, bool aTodas)
    {
        var s = (SAV3)sav;
        var equipo = new PK3[Miembros];
        for (var i = 0; i < s.PartyCount; i++) equipo[i] = (PK3)s.GetPartySlotAtIndex(i);
        for (var i = s.PartyCount; i < equipo.Length; i++) equipo[i] = new PK3();
        Cambiar(sav, fama =>
        {
            fama[entrada].CopyFrom(equipo);
            if (!aTodas) return;
            for (var i = 0; i < fama.Length; i++) if (i != entrada) fama[i].CopyFrom(equipo);
        });
    }
}
