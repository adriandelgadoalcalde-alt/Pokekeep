// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen4;

/// <summary>
/// PK-SAV-034: <c>SAV_Geonet4</c>, el Geonet de la 4.ª generación (D/P/Pt/HG/SS): un punto (ninguno, azul, amarillo o rojo) por país y región, y «Todo el globo visible». El formulario es una rejilla de 233 países (los que no tienen
/// regiones llevan una sola fila) con tres botones que cambian la rejilla (<c>SetAll</c>, <c>SetAllLegal</c>, <c>ClearAll</c>), y al guardar hace SIEMPRE: <c>ClearAll()</c>, un <c>SetCountrySubregion</c> por fila, <c>SetSAVCountry()</c> (el punto
/// del propio país y región del guardado pasa a rojo), <c>Save()</c> y, por último, <c>GlobalFlag = casilla</c>.
/// <para>En el móvil cada país es un elemento de una lista con un campo por región, y cada campo que cambia hace lo que haría el guardado entero con SU fila: pone el punto, <c>SetSAVCountry()</c> y <c>Save()</c> (así el propio país queda
/// siempre en rojo, como en el formulario; el campo «globo» se escribe aparte). Los tres botones actúan de golpe como acciones inmediatas (no hay «Cancelar» que deshaga la rejilla; el historial lo sustituye). Divergencias
/// declaradas en el ADR 0062: la app no repasa las 233 filas al guardar, solo las que cambian; los bits de regiones inexistentes de un país se conservan.</para>
/// </summary>
internal static class GeonetGen4
{
    public const string Form = "SAV_Geonet4";
    public static readonly string[] Censo = ["PK-SAV-034"];

    private static string L(string control) => EtiquetasLang.Requerir(Form, control);
    private static SAV4 S(ContextoP c) => (SAV4)c.Sav;

    // MessageStrings_es.txt (MsgGeonetPointNone/Blue/Yellow/Red): «Ningún / Azul / Amarillo / Rojo»; «Ninguno» porque aqui la palabra va sola.
    private static IReadOnlyList<OpcionP> Puntos => P.Lista(IdiomaInterfaz.T("Form.Ninguno"), IdiomaInterfaz.T("Catalogo.Azul"), IdiomaInterfaz.T("GeonetGen4.Amarillo"), IdiomaInterfaz.T("GeonetGen4.Rojo"));

    /// <summary>Los países en el orden de la rejilla (alfabético en castellano, sin el vacío) y las regiones de cada uno; un país sin regiones lleva una sola fila con la región 0.</summary>
    private static readonly PorIdioma<IReadOnlyList<(int Pais, string Nombre, IReadOnlyList<(int Region, string Nombre)> Regiones)>> Paises = new(() =>
    {
        // El ORDEN es el del castellano en cualquier idioma (la estructura del formulario no cambia con el idioma); solo cambian los nombres.
        var paises = Util.GetCountryRegionList("gen4_countries", "es"); var nombresPais = Util.GetCountryRegionList("gen4_countries", Pokekeep.Mobile.Ui.Ajustes.Pkhex.ProgramaPkhex.Idioma).ToDictionary(x => x.Value, x => x.Text);
        var porDefecto = Util.GetCountryRegionList("gen4_sr_default", "es"); var nombresDefecto = Util.GetCountryRegionList("gen4_sr_default", Pokekeep.Mobile.Ui.Ajustes.Pkhex.ProgramaPkhex.Idioma).ToDictionary(x => x.Value, x => x.Text);
        var l = new List<(int, string, IReadOnlyList<(int, string)>)>();
        for (var i = 1; i <= LocaleNDS4.CountryCount; i++)
        {
            var pais = paises[i].Value;
            var n = LocaleNDS4.GetSubregionCount((byte)pais);
            if (n == 0) { l.Add((pais, nombresPais[pais], [(porDefecto[0].Value, nombresDefecto[porDefecto[0].Value])])); continue; }
            var regiones = Util.GetCountryRegionList($"gen4_sr_{pais:000}", "es"); var nombresRegion = Util.GetCountryRegionList($"gen4_sr_{pais:000}", Pokekeep.Mobile.Ui.Ajustes.Pkhex.ProgramaPkhex.Idioma).ToDictionary(x => x.Value, x => x.Text);
            l.Add((pais, nombresPais[pais], [.. Enumerable.Range(1, n).Select(j => (regiones[j].Value, nombresRegion[regiones[j].Value]))]));
        }
        return l;
    });

    private static GeonetPoint Punto(ContextoP c, int pais, int region) => new Geonet4(S(c)).GetCountrySubregion((byte)pais, (byte)region);

    /// <summary>Lo que hace el guardado del formulario con una fila: el punto, el propio país en rojo y volcar el bloque.</summary>
    private static void Poner(ContextoP c, int pais, int region, GeonetPoint punto)
    {
        var g = new Geonet4(S(c));
        g.SetCountrySubregion((byte)pais, (byte)region, punto);
        g.SetSAVCountry();
        g.Save();
    }

    /// <summary>El campo del propio país y región del guardado (el que <c>SetSAVCountry</c> pone en rojo), para avisar de que cambia a la vez.</summary>
    private static IEnumerable<string> PropioPais(ColeccionDefP col, ContextoP c)
    {
        var s = S(c);
        if (s.Country <= 0) return [];
        var i = Paises.Value.ToList().FindIndex(p => p.Pais == s.Country);
        return i < 0 ? [] : [col.IdCampo(i, $"r{s.Region}")];
    }

    private static string Resumen(ContextoP c, int i)
    {
        var (pais, _, regiones) = Paises.Value[i];
        var g = new Geonet4(S(c));
        var n = regiones.Count(r => g.GetCountrySubregion((byte)pais, (byte)r.Region) != GeonetPoint.None);
        return n == 0 ? IdiomaInterfaz.T("GeonetGen4.Puntos") : IdiomaInterfaz.F("GeonetGen4.Punto", n, regiones.Count);
    }

    /// <summary>Predicado barato de <see cref="Crear"/>: <c>B_OpenGeonetEditor</c> existe en todo guardado de la 4.ª generación de consola portátil.</summary>
    public static bool Aplica(SaveFile sav) => sav is SAV4;

    public static EditorDefP? Crear(SaveFile sav)
    {
        if (sav is not SAV4) return null;
        ColeccionDefP? col = null;
        col = new ColeccionDefP
        {
            Id = "paises", Elemento = L("DGV_Item_Country"), Cantidad = _ => Paises.Value.Count,
            Titulo = (_, i) => Paises.Value[i].Nombre, Resumen = Resumen,
            Secciones = (_, i) =>
            {
                var (pais, _, regiones) = Paises.Value[i];
                var campos = regiones.Select(r => (CampoP)P.Opcion(col!.IdCampo(i, $"r{r.Region}"), regiones.Count == 1 ? L("DGV_Item_Point") : r.Nombre,
                    c => (long)Punto(c, pais, r.Region), (c, v) => Poner(c, pais, r.Region, (GeonetPoint)v), _ => Puntos, titulo: regiones.Count == 1 ? L("DGV_Item_Point") : r.Nombre,
                    afectaA: c => PropioPais(col!, c))).ToList();
                return [new SeccionP(Paises.Value[i].Nombre, campos)];
            },
        };
        var general = new SeccionP(string.Empty,
        [
            P.Marca("globo", L("CHK_GlobalFlag"), c => S(c).GeonetGlobalFlag, (c, v) => S(c).GeonetGlobalFlag = v, prioridad: 10),
            // Los tres botones rehacen la rejilla entera (y la casilla): actuan de golpe sobre la partida. Despues el formulario guarda lo mismo que dejan, con el propio pais en rojo.
            P.Accion("todas", L("B_SetAllLocations"), c => { var g = new Geonet4(S(c)); g.SetAll(); g.Save(); }, inmediata: true, textoBoton: IdiomaInterfaz.T("GeonetGen4.PonerTodas"),
                confirmacion: IdiomaInterfaz.T("GeonetGen4.MarcanAmarilloTodosPaises"), ayuda: IdiomaInterfaz.T("GeonetGen4.PonePuntoAmarilloTodos")),
            P.Accion("legales", L("B_SetAllLegalLocations"), c => { var g = new Geonet4(S(c)); g.SetAllLegal(); g.Save(); }, inmediata: true, textoBoton: IdiomaInterfaz.T("GeonetGen4.PonerLegales"),
                confirmacion: IdiomaInterfaz.T("GeonetGen4.MarcanAmarilloPaisesJuego"), ayuda: IdiomaInterfaz.T("GeonetGen4.PonePuntoAmarilloSolo")),
            P.Accion("limpiar", L("B_ClearLocations"), c => { var g = new Geonet4(S(c)); g.ClearAll(); g.Save(); }, inmediata: true, textoBoton: IdiomaInterfaz.T("Form.Limpiar"),
                confirmacion: IdiomaInterfaz.T("GeonetGen4.QuitanTodosPuntosPropio"), ayuda: IdiomaInterfaz.T("GeonetGen4.QuitaTodosPuntos")),
        ]);
        return new EditorDefP("geonet", TituloGen4.De("geonet"),
            [new PestanaP("general", IdiomaInterfaz.T("Cajas.General"), [general]), new PestanaP("paises", L("DGV_Item_Country"), [], Coleccion: col)],
            Censo, IdiomaInterfaz.T("Partida.Especifico.geonet.Descripcion"));
    }
}
