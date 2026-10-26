// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen8y9;

/// <summary>
/// PK-SAV-195 · <c>SAV_Raid9</c>, las teraincursiones de Escarlata y Púrpura: de Paldea (72 cristales), de Kitakami (DLC 1, 100) y de la Academia Arándano (DLC 2, 80). El formulario elige un cristal («Raid 001»), lo enseña en
/// un cuadro de propiedades (sin ordenar: el orden es el de <c>TeraRaidDetail</c>), enseña las semillas de hoy y de mañana (solo si el bloque las tiene, <c>HasSeeds</c>; se escriben al salir del cuadro de texto) y trae
/// «Copiar a otras incursiones» (<c>Propagate</c>: copia el estado, el contenido y los puntos de Liga reclamados del cristal elegido a todos los que tienen zona, y con Mayúsculas también la semilla). Todo escribe sobre una
/// copia que «Guardar» copia al original. Aquí cada cristal es un elemento de una colección con un campo por propiedad; las semillas son otra pestaña (solo si existen) y «Copiar a otras incursiones» es una acción pendiente de
/// cada cristal que se ejecuta DESPUÉS de los demás cambios (como en el formulario, donde se pulsa tras editar), con una segunda acción «con semilla» para la variante con Mayúsculas.
/// </summary>
internal static class IncursionesPaldea
{
    public const string Form = "SAV_Raid9";
    public static readonly string[] Censo = ["PK-SAV-195"];

    private static string L(string control) => EtiquetasLang.Requerir(Form, control);

    private static RaidSpawnList9? Lista(SaveFile sav, TeraRaidOrigin origen) => sav is SAV9SV s
        ? origen switch { TeraRaidOrigin.Paldea => s.RaidPaldea, TeraRaidOrigin.Kitakami when s.SaveRevision >= 1 => s.RaidKitakami, TeraRaidOrigin.BlueberryAcademy when s.SaveRevision >= 2 => s.RaidBlueberry, _ => null }
        : null;

    private static RaidSpawnList9 Cristales(ContextoP c, TeraRaidOrigin origen) => Lista(c.Sav, origen)!;

    /// <summary>Predicado barato de <see cref="Crear"/>: el editor existe para este guardado.</summary>
    public static bool Aplica(SaveFile sav, TeraRaidOrigin origen) => Lista(sav, origen) is not null;

    private static string Contenido(TeraRaidContentType t) => t switch
    {
        TeraRaidContentType.Base05 => IdiomaInterfaz.T("IncursionesPaldea.BaseEstrellas"), TeraRaidContentType.Black6 => IdiomaInterfaz.T("IncursionesPaldea.CristalNegroEstrellas"), TeraRaidContentType.Distribution => IdiomaInterfaz.T("IncursionesPaldea.DistribucionEvento"), TeraRaidContentType.Might7 => IdiomaInterfaz.T("IncursionesPaldea.PoderosaEstrellas"),
        _ => IdiomaInterfaz.F("Form.SinNombre", (uint)t),
    };

    public static string Titulo(TeraRaidOrigin origen) => origen switch { TeraRaidOrigin.Paldea => IdiomaInterfaz.T("Partida.Especifico.incursiones.Titulo"), TeraRaidOrigin.Kitakami => IdiomaInterfaz.T("Partida.Especifico.incursiones-dlc1.Titulo"), _ => IdiomaInterfaz.T("Partida.Especifico.incursiones-dlc2.Titulo") };
    private static string Id(TeraRaidOrigin origen) => origen switch { TeraRaidOrigin.Paldea => "incursiones", TeraRaidOrigin.Kitakami => "incursiones-dlc1", _ => "incursiones-dlc2" };

    public static EditorDefP? Crear(SaveFile sav, TeraRaidOrigin origen)
    {
        if (Lista(sav, origen) is not { } lista) return null;
        ColeccionDefP? col = null;
        col = new ColeccionDefP
        {
            Id = "cristales", Elemento = IdiomaInterfaz.T("IncursionesPaldea.Cristal"), Cantidad = c => Cristales(c, origen).CountUsed,
            Titulo = (c, i) => { var d = Cristales(c, origen).GetRaid(i); return $"{i + 1:000} · {(d.IsEnabled ? Contenido(d.Content) : IdiomaInterfaz.T("IncursionesPaldea.CristalActivo"))}"; },
            Resumen = (c, i) => { var d = Cristales(c, origen).GetRaid(i); return d.IsEnabled ? IdiomaInterfaz.F("Varios5.Zona2", d.AreaID) : null; },
            Secciones = (_, i) =>
            {
                TeraRaidDetail D(ContextoP c) => Cristales(c, origen).GetRaid(i);
                return
                [
                    new SeccionP(string.Empty,
                    [
                        P.Marca(col!.IdCampo(i, "activo"), L("Prop_IsEnabled"), c => D(c).IsEnabled, (c, v) => D(c).IsEnabled = v, ayuda: IdiomaInterfaz.T("IncursionesPaldea.IndicaCristalTeraincursionActivo")),
                        P.Entero(col.IdCampo(i, "zona"), L("Prop_AreaID"), c => D(c).AreaID, (c, v) => D(c).AreaID = (uint)v, 0, uint.MaxValue, ayuda: IdiomaInterfaz.T("IncursionesPaldea.ZonaCristal"), afectaA: c => [col.IdCampo(i, "nombre")]),
                        P.Entero(col.IdCampo(i, "grupo"), L("Prop_LotteryGroup"), c => D(c).LotteryGroup, (c, v) => D(c).LotteryGroup = (uint)v, 0, uint.MaxValue, ayuda: IdiomaInterfaz.T("IncursionesPaldea.GrupoSorteoPerteneceCristal"), afectaA: c => [col.IdCampo(i, "nombre")]),
                        P.Entero(col.IdCampo(i, "punto"), L("Prop_SpawnPointID"), c => D(c).SpawnPointID, (c, v) => D(c).SpawnPointID = (uint)v, 0, uint.MaxValue, ayuda: IdiomaInterfaz.T("IncursionesPaldea.CristalElegidoAzarEntre"), afectaA: c => [col.IdCampo(i, "nombre")]),
                        P.Info(col.IdCampo(i, "nombre"), L("Prop_ScenePointName"), c => D(c).ScenePointName),
                        CamposGen8y9.Hex(col.IdCampo(i, "semilla"), L("Prop_Seed"), c => D(c).Seed, (c, v) => D(c).Seed = (uint)v, de32Bits: true, ayuda: IdiomaInterfaz.T("IncursionesPaldea.SemillaBitsHexadecimalBusca")),
                        P.Entero(col.IdCampo(i, "sin-usar"), L("Prop_Unused"), c => D(c).Unused, (c, v) => D(c).Unused = (uint)v, 0, uint.MaxValue, ayuda: IdiomaInterfaz.T("IncursionesPaldea.SiempreCero")),
                        P.Opcion(col.IdCampo(i, "contenido"), L("Prop_Content"), c => (uint)D(c).Content, (c, v) => D(c).Content = (TeraRaidContentType)(uint)v, _ => [.. Enum.GetValues<TeraRaidContentType>().Select(t => new OpcionP((int)(uint)t, Contenido(t)))], titulo: L("Prop_Content"), ayuda: IdiomaInterfaz.T("IncursionesPaldea.DondeSalenEncuentroRecompensas")),
                        P.Marca(col.IdCampo(i, "puntos-liga"), L("Prop_IsClaimedLeaguePoints"), c => D(c).IsClaimedLeaguePoints, (c, v) => D(c).IsClaimedLeaguePoints = v, ayuda: IdiomaInterfaz.T("IncursionesPaldea.JugadorRecogidoPuntosLiga")),
                    ]),
                    new SeccionP(IdiomaInterfaz.T("Seleccion.Copiar"),
                    [
                        P.Accion(col.IdCampo(i, "copiar"), L("B_CopyToOthers"), c => Cristales(c, origen).Propagate(i, seedToo: false), prioridad: 100, textoBoton: IdiomaInterfaz.T("Seleccion.Copiar"), confirmacion: IdiomaInterfaz.T("IncursionesPaldea.EstadoContenidoPuntosLiga"), ayuda: IdiomaInterfaz.T("IncursionesPaldea.CopiaCristalDemasSemilla")),
                        P.Accion(col.IdCampo(i, "copiar-semilla"), IdiomaInterfaz.F("IncursionesPaldea.PuntoSemilla", L("B_CopyToOthers")), c => Cristales(c, origen).Propagate(i, seedToo: true), prioridad: 101, textoBoton: IdiomaInterfaz.T("Seleccion.Copiar"), confirmacion: IdiomaInterfaz.T("IncursionesPaldea.EstadoContenidoPuntosLiga2"), ayuda: IdiomaInterfaz.T("IncursionesPaldea.CopiaCristalDemasSemilla2")),
                    ]),
                ];
            },
        };
        var semillas = new SeccionP(string.Empty,
        [
            CamposGen8y9.Hex("semilla-hoy", L("L_SeedCurrent"), c => Cristales(c, origen).CurrentSeed, (c, v) => Cristales(c, origen).CurrentSeed = v, ayuda: IdiomaInterfaz.T("IncursionesPaldea.SemillaIncursionesHoyBits")),
            CamposGen8y9.Hex("semilla-manana", L("L_SeedTomorrow"), c => Cristales(c, origen).TomorrowSeed, (c, v) => Cristales(c, origen).TomorrowSeed = v, ayuda: IdiomaInterfaz.T("IncursionesPaldea.SemillaIncursionesMananaBits")),
        ]);
        List<PestanaP> pestanas = [new PestanaP("cristales", EtiquetasLang.RequerirFormulario(Form), [], Coleccion: col)];
        if (lista.HasSeeds) pestanas.Add(new PestanaP("semillas", IdiomaInterfaz.T("IncursionesPaldea.Semillas"), [semillas]));
        return new EditorDefP(Id(origen), Titulo(origen), pestanas, Censo, origen switch { TeraRaidOrigin.Paldea => IdiomaInterfaz.T("IncursionesPaldea.TeraincursionesPaldea"), TeraRaidOrigin.Kitakami => IdiomaInterfaz.T("IncursionesPaldea.TeraincursionesKitakami"), _ => IdiomaInterfaz.T("IncursionesPaldea.TeraincursionesAcademiaArandano") });
    }
}
