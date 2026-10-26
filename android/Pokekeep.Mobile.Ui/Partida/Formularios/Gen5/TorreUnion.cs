// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen5;

/// <summary>
/// PK-SAV-103 · <c>SAV_UnityTower</c>, la Torre Unión de Negro/Blanco y Negro 2/Blanco 2. El formulario enseña dos rejillas (los puntos del globo de cada país y región, y el piso de cada país) y dos casillas
/// («Todo el globo visible», «Torre Unión desbloqueada»), con tres botones que cambian los controles con una llamada del Core (<c>SetAll</c>, <c>SetAllLegal</c>, <c>ClearAll</c>). Aquí todo vive en UNA tabla
/// (las acciones masivas de una tabla solo ven sus filas, y los botones del original cambian rejillas y casillas a la vez): los puntos y los pisos son filas, las dos casillas son filas de marca, y cada botón calcula
/// el nuevo borrador ejecutando la misma llamada del Core sobre una COPIA del guardado y leyendo de ella todas las filas (el borrador se ve, se puede corregir y se deshace como cualquier cambio).
/// Reglas calcadas de <c>B_Save_Click</c>: lo primero que hace el formulario es <c>ClearAll</c> y vuelve a escribir todas las filas (cubren todos los bits que limpia), así que el resultado solo difiere de escribir
/// las filas cambiadas en la marca roja de la ubicación propia (<c>SetSAVCountry</c>, que el formulario escribe siempre al final): se hace después de escribir, en cada «Aplicar».
/// </summary>
internal static class TorreUnion
{
    public const string Form = "SAV_UnityTower";
    public static readonly string[] Censo = ["PK-SAV-103"];

    private static string L(string control) => EtiquetasLang.Requerir(Form, control);
    private static UnityTower5 Torre(SaveFile sav) => ((SAV5)sav).UnityTower;

    /// <summary>Predicado barato de <see cref="Crear"/>: el editor existe para este guardado.</summary>
    public static bool Aplica(SaveFile sav) => sav is SAV5;

    private const int FilaGlobo = 1, FilaTorre = 2, BasePiso = 1_000_000;

    /// <summary>Una ubicación del globo: país y región (la región 0 es "el país entero" de los que no tienen regiones).</summary>
    private sealed record Ubicacion(int Pais, int Region, string NombrePais, string NombreRegion);

    private static readonly PorIdioma<(IReadOnlyList<(int Pais, string Nombre)> Paises, IReadOnlyList<Ubicacion> Ubicaciones)> Mapa = new(() =>
    {
        // Las mismas listas, en el mismo orden (ordenadas por nombre), que construye InitializeDGVGeonet / InitializeDGVUnityTower.
        // El ORDEN es el del castellano en cualquier idioma (la estructura del formulario no cambia con el idioma); solo cambian los nombres.
        var paises = Util.GetCountryRegionList("gen5_countries", "es"); var nombresPais = Util.GetCountryRegionList("gen5_countries", Pokekeep.Mobile.Ui.Ajustes.Pkhex.ProgramaPkhex.Idioma).ToDictionary(x => x.Value, x => x.Text);
        var porDefecto = Util.GetCountryRegionList("gen5_sr_default", "es"); var nombresDefecto = Util.GetCountryRegionList("gen5_sr_default", Pokekeep.Mobile.Ui.Ajustes.Pkhex.ProgramaPkhex.Idioma).ToDictionary(x => x.Value, x => x.Text);
        var ubicaciones = new List<Ubicacion>();
        var lista = new List<(int, string)>();
        for (var i = 1; i <= LocaleNDS5.CountryCount; i++)
        {
            var pais = paises[i].Value; var nombrePais = nombresPais[pais];
            lista.Add((pais, nombrePais));
            var cuantas = UnityTower5.GetSubregionCount((byte)pais);
            var regiones = cuantas == 0 ? porDefecto : Util.GetCountryRegionList($"gen5_sr_{pais:000}", "es");
            var nombresRegion = cuantas == 0 ? nombresDefecto : Util.GetCountryRegionList($"gen5_sr_{pais:000}", Pokekeep.Mobile.Ui.Ajustes.Pkhex.ProgramaPkhex.Idioma).ToDictionary(x => x.Value, x => x.Text);
            if (cuantas == 0) ubicaciones.Add(new Ubicacion(pais, regiones[0].Value, nombrePais, string.Empty));
            for (var j = 1; j <= cuantas; j++) ubicaciones.Add(new Ubicacion(pais, regiones[j].Value, nombrePais, nombresRegion[regiones[j].Value]));
        }
        return (lista, ubicaciones);
    });

    private static string NombreUbicacion(Ubicacion u) => u.NombreRegion.Length == 0 ? u.NombrePais : $"{u.NombrePais} · {u.NombreRegion}";

    private static IReadOnlyList<FilaTablaP> Filas()
    {
        var l = new List<FilaTablaP>
        {
            new() { Id = FilaGlobo, Nombre = L("CHK_GlobalFlag"), EsMarca = true, Leer = c => Torre(c.Sav).GlobalFlag, Escribir = (c, v) => Torre(c.Sav).GlobalFlag = (bool)v },
            new() { Id = FilaTorre, Nombre = L("CHK_UnityTowerFlag"), EsMarca = true, Leer = c => Torre(c.Sav).UnityTowerFlag, Escribir = (c, v) => Torre(c.Sav).UnityTowerFlag = (bool)v },
        };
        // Por pais: su piso y despues sus ubicaciones (el orden de las filas no cambia lo que se escribe: pisos y puntos son bits distintos).
        var porPais = Mapa.Value.Ubicaciones.ToLookup(u => u.Pais);
        foreach (var (pais, nombre) in Mapa.Value.Paises)
        {
            var p = pais;
            l.Add(new FilaTablaP { Id = BasePiso + p, Nombre = $"{nombre} · {L("DGV_Item_Floor")}", EsMarca = true, Grupo = nombre,
                Leer = c => Torre(c.Sav).GetUnityTowerFloor((byte)p), Escribir = (c, v) => Torre(c.Sav).SetUnityTowerFloor((byte)p, (bool)v) });
            foreach (var u in porPais[p])
            {
                var x = u;
                l.Add(new FilaTablaP { Id = (x.Pais * 1000) + x.Region, Nombre = NombreUbicacion(x), Min = 0, Max = 3, Grupo = x.NombrePais,
                    Leer = c => (long)Torre(c.Sav).GetCountrySubregion((byte)x.Pais, (byte)x.Region),
                    Escribir = (c, v) => Torre(c.Sav).SetCountrySubregion((byte)x.Pais, (byte)x.Region, (GeonetPoint)(int)(long)v) });
            }
        }
        return l;
    }

    /// <summary>El nuevo borrador de un botón: la llamada del Core sobre una copia del guardado y todas las filas leídas de ella (lo que el formulario enseña en las rejillas tras pulsarlo).</summary>
    private static IReadOnlyList<(FilaTablaP Fila, object Valor)> Cambios(ContextoP c, IReadOnlyList<FilaTablaP> filas, Action<UnityTower5> boton)
    {
        var copia = (SAV5)c.Sav.Clone();
        boton(copia.UnityTower);
        var t = copia.UnityTower;
        var l = new List<(FilaTablaP, object)>(filas.Count);
        foreach (var f in filas)
        {
            if (f.Id == FilaGlobo) l.Add((f, t.GlobalFlag));
            else if (f.Id == FilaTorre) l.Add((f, t.UnityTowerFlag));
            else if (f.Id >= BasePiso) l.Add((f, t.GetUnityTowerFloor((byte)(f.Id - BasePiso))));
            else l.Add((f, (long)t.GetCountrySubregion((byte)(f.Id / 1000), (byte)(f.Id % 1000))));
        }
        return l;
    }

    public static EditorDefP? Crear(SaveFile sav)
    {
        if (!Aplica(sav)) return null;
        var tabla = new TablaDefP
        {
            Id = "ubicaciones", Marcador = IdiomaInterfaz.T("TorreUnion.BuscarPaisRegion"), SinNumero = true,
            Ayuda = IdiomaInterfaz.F("TorreUnion.AyudaUbicaciones", AyudasGen5.MensajeCore("MsgGeonetPointNone"), AyudasGen5.MensajeCore("MsgGeonetPointBlue"), AyudasGen5.MensajeCore("MsgGeonetPointYellow"), AyudasGen5.MensajeCore("MsgGeonetPointRed")),
            Filas = _ => Filas(),
            Acciones =
            [
                new AccionTablaP("todas", L("B_SetAllLocations"), (c, f) => Cambios(c, f, t => t.SetAll()), IdiomaInterfaz.T("TorreUnion.MarcaTodasUbicacionesDesbloquea")),
                new AccionTablaP("legales", L("B_SetAllLegalLocations"), (c, f) => Cambios(c, f, t => t.SetAllLegal()), IdiomaInterfaz.T("TorreUnion.MarcaSoloUbicacionesPueden")),
                new AccionTablaP("limpiar", L("B_ClearLocations"), (c, f) => Cambios(c, f, t => t.ClearAll()), IdiomaInterfaz.T("TorreUnion.QuitaTodasUbicacionesPisos")),
            ],
        };
        var pestanas = new List<PestanaP> { new("ubicaciones", L("Tab_Locations"), [], Tabla: tabla) };
        return new EditorDefP("torreunion", IdiomaInterfaz.T("Partida.Especifico.torreunion.Titulo"), pestanas, Censo, IdiomaInterfaz.T("Partida.Especifico.torreunion.Descripcion"))
        {
            // B_Save_Click acaba con SetSAVCountry (la marca roja de la ubicación del propio guardado) tras escribir las filas.
            PrepararAplicar = _ => c => Torre(c.Sav).SetSAVCountry(),
        };
    }
}
