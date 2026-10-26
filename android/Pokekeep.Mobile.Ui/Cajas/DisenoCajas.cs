// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Modelo;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Cajas;

/// <summary>
/// «Nombre y fondo de caja» (<c>SAV_BoxLayout</c> de PKHeX): el nombre de cada caja, su fondo, las cajas desbloqueadas y mover una caja de sitio. Cada cambio es UNA entrada del historial
/// (Deshacer exacto: PKHeX trabaja sobre una copia y no se puede deshacer). Solo se ofrece lo que la partida guarda (<c>IBoxDetailName</c>, <c>IBoxDetailWallpaper</c>).
/// </summary>
public sealed class DisenoCajas(Sesion sesion)
{
    private SaveFile Sav => sesion.Guardado;
    private EditorCasillas Editor => sesion.Casillas;

    public bool AdmiteNombres => Sav is IBoxDetailName;
    public bool MuestraNombres => Sav is IBoxDetailNameRead;
    public bool AdmiteFondos => Sav is IBoxDetailWallpaper;
    public bool AdmiteDesbloqueo => Sav.HasBox && Sav.BoxesUnlocked > 0;
    public bool AdmiteMover => Sav.HasBox && Sav.BoxCount > 1;
    public int Desbloqueadas => Sav.BoxesUnlocked;

    /// <summary>Longitud maxima del nombre segun la generacion (tabla de <c>SAV_BoxLayout</c>).</summary>
    public int MaxNombre => Sav.Generation switch
    {
        2 when Sav is SAV2 { Japanese: false, Korean: false } => 8 * 2,
        3 when Sav is SAV3RSBox => 8 + SAV3RSBox.BoxNamePrefix,
        6 or 7 => 14,
        >= 8 => 16,
        _ => 8,
    };

    /// <summary>
    /// Posición de cada fondo en <c>wallpapernames</c> (la lista de texto de PKHeX, 64 líneas): es el enum <c>WallpaperName</c> de <c>SAV_BoxLayout</c> de PKHeX 26.10.18. Los doce primeros (bosque a cielo) y los cuatro
    /// siguientes (Centro Pokémon, máquina, cuadros, sencillo) son comunes; después van los de cada juego.
    /// </summary>
    internal static class Fondo
    {
        public const int CentroPokemon = 12, Sencillo = 15;
        public const int Lunares = 24, Maquinaria3 = 25, Liso = 26, Amigos = 27, Estrellas = 28, Losas = 29, Flores = 30, Moqueta = 31, Ruinas = 32, MiFondo = 33;
        public const int Espacio = 34, Patio = 35, Nostalgia = 36, Torchic = 37, Trio = 38, Pikapika = 39, Leyenda = 40, EquipoGalaxia = 41, Distorsion = 42, Concurso = 43, Croagunk = 44;
        public const int Corazon = 45, Alma = 46, Hermano = 47, Pokeathlon = 48, PichuPika = 49, ChicaKimono = 50, Retorno = 51;
        public const int Reshiram = 52, Zekrom = 53, Monocromo = 54, EquipoPlasma = 55, Munna = 56, Zoroark = 57, Metro = 58, Musical = 59, Pelicula = 60, PWT = 61, Kyurem1 = 62, Kyurem2 = 63;
    }

    /// <summary>Los fondos propios de cada juego de Gen. 3 y 4/5 (después de los comunes), tal como los lista <c>SAV_BoxLayout.LoadWallpapers</c>.</summary>
    private static readonly int[] PropiosDP = [Fondo.Espacio, Fondo.Patio, Fondo.Nostalgia, Fondo.Torchic, Fondo.Trio, Fondo.Pikapika, Fondo.Leyenda, Fondo.EquipoGalaxia];
    private static readonly int[] PropiosPt = [Fondo.Distorsion, Fondo.Concurso, Fondo.Nostalgia, Fondo.Croagunk, Fondo.Trio, Fondo.Pikapika, Fondo.Leyenda, Fondo.EquipoGalaxia];

    private static int[] PropiosDelJuego(SaveFile sav) => sav switch
    {
        SAV3RS => [Fondo.Lunares, Fondo.CentroPokemon, Fondo.Maquinaria3, Fondo.Liso],
        SAV3E => [Fondo.Lunares, Fondo.CentroPokemon, Fondo.Maquinaria3, Fondo.Sencillo, Fondo.Amigos],
        SAV3FRLG => [Fondo.Estrellas, Fondo.CentroPokemon, Fondo.Losas, Fondo.Sencillo],
        SAV3RSBox => [Fondo.Lunares, Fondo.CentroPokemon, Fondo.Maquinaria3, Fondo.Liso, Fondo.Flores, Fondo.Losas, Fondo.Moqueta, Fondo.Ruinas, Fondo.MiFondo],
        SAV4DP => PropiosDP,
        SAV4Pt => PropiosPt,
        SAV4HGSS => [Fondo.Corazon, Fondo.Alma, Fondo.Hermano, Fondo.Pokeathlon, Fondo.Trio, Fondo.PichuPika, Fondo.ChicaKimono, Fondo.Retorno],
        SAV5BW => [Fondo.Reshiram, Fondo.Zekrom, Fondo.Monocromo, Fondo.EquipoPlasma, Fondo.Munna, Fondo.Zoroark, Fondo.Metro, Fondo.Musical],
        SAV5B2W2 => [Fondo.Monocromo, Fondo.EquipoPlasma, Fondo.Pelicula, Fondo.PWT, Fondo.Kyurem1, Fondo.Kyurem2, Fondo.Reshiram, Fondo.Zekrom],
        _ => [],
    };

    /// <summary>
    /// Nombres de los fondos disponibles, en el idioma de la interfaz: los del juego en Gen. 3-7 y Switch (BDSP) como los lista <c>SAV_BoxLayout</c> de PKHeX 26.10.18 (los comunes y, después, los propios de cada juego, que antes
    /// salían mal desde el 13.º) y «Fondo N» donde PKHeX tampoco los tiene.
    /// </summary>
    public IReadOnlyList<string> NombresFondos
    {
        get
        {
            if (Sav is not IBoxDetailWallpaper) return [];
            var n = sesion.Textos.wallpapernames;
            string[] Porciones(int cuantos) => [.. n.Take(cuantos)];
            string[] Marcadores(int cuantos) => [.. Enumerable.Range(1, cuantos).Select(i => IdiomaInterfaz.F("DisenoCajas.Fondo", i))];
            string[] ConPropios(int comunes) => [.. Porciones(comunes), .. PropiosDelJuego(Sav).Select(i => n[i])];
            var sufijoPlatino = IdiomaInterfaz.T("DisenoCajas.SufijoPlatino");
            return Sav.Generation switch
            {
                3 => ConPropios(12),
                4 or 5 => ConPropios(16),
                6 => Porciones(24),
                7 => Porciones(16),
                8 when Sav is SAV8BS => [.. Porciones(16), .. PropiosDP.Select(i => n[i]), .. PropiosPt.Select(i => n[i] + sufijoPlatino)],
                8 => Marcadores(19),
                9 => Marcadores(20),
                _ => [],
            };
        }
    }

    public string Nombre(int caja) => sesion.NombreCaja(caja);

    public InformeOperacion Renombrar(int caja, string nombre)
    {
        if (Sav is not IBoxDetailName n) return InformeOperacion.Falla(TipoResultado.NoAdmitido, IdiomaInterfaz.T("DisenoCajas.PartidaGuardaNombresCajas"));
        nombre = nombre.Trim();
        if (nombre.Length == 0) return InformeOperacion.Falla(TipoResultado.NoAdmitido, IdiomaInterfaz.T("DisenoCajas.NombrePuedeEstarVacio"));
        if (nombre.Length > MaxNombre) nombre = nombre[..MaxNombre];
        if (Sav is IBoxDetailNameRead r && r.GetBoxName(caja) == nombre) return InformeOperacion.Falla(TipoResultado.SinCambios, IdiomaInterfaz.T("DisenoCajas.Nombre"));
        var tocadas = Editor.Lote([], () => { n.SetBoxName(caja, nombre); return 1; });
        return tocadas > 0 ? InformeOperacion.Ok(IdiomaInterfaz.F("DisenoCajas.CajaRenombradaPuedesDeshacerlo", nombre), 0) : InformeOperacion.Falla(TipoResultado.Fallo, IdiomaInterfaz.T("DisenoCajas.PodidoRenombrarCaja"));
    }

    public InformeOperacion FijarFondo(int caja, int fondo)
    {
        if (Sav is not IBoxDetailWallpaper w) return InformeOperacion.Falla(TipoResultado.NoAdmitido, IdiomaInterfaz.T("DisenoCajas.PartidaGuardaFondosCajas"));
        if (fondo < 0 || fondo >= Math.Max(1, NombresFondos.Count)) return InformeOperacion.Falla(TipoResultado.NoAdmitido, IdiomaInterfaz.T("DisenoCajas.FondoExistePartida"));
        if (w.GetBoxWallpaper(caja) == fondo) return InformeOperacion.Falla(TipoResultado.SinCambios, IdiomaInterfaz.T("DisenoCajas.CajaTieneFondo"));
        var tocadas = Editor.Lote([], () => { w.SetBoxWallpaper(caja, fondo); return 1; });
        return tocadas > 0 ? InformeOperacion.Ok(IdiomaInterfaz.T("DisenoCajas.FondoCambiadoPuedesDeshacerlo"), 0) : InformeOperacion.Falla(TipoResultado.Fallo, IdiomaInterfaz.T("DisenoCajas.PodidoCambiarFondo"));
    }

    /// <summary>Mueve la caja <paramref name="delta"/> posiciones (intercambia con su vecina, con su contenido, nombre y fondo); no se puede si alguna tiene casillas bloqueadas (Cajas de combate).</summary>
    public InformeOperacion Mover(int caja, int delta)
    {
        var otra = caja + delta;
        if ((uint)otra >= Sav.BoxCount || (uint)caja >= Sav.BoxCount) return InformeOperacion.Falla(TipoResultado.SinCambios, IdiomaInterfaz.T("DisenoCajas.PuedeMoverDireccion"));
        var casillas = new List<ISlotInfo>();
        foreach (var b in new[] { caja, otra })
            for (var s = 0; s < Sav.BoxSlotCount; s++) casillas.Add(new SlotInfoBox(b, s, Sav));
        var movida = false;
        var tocadas = Editor.Lote(casillas, () => { movida = Sav.SwapBox(caja, otra); return movida ? 2 : 0; });
        return tocadas > 0 && movida ? InformeOperacion.Ok(IdiomaInterfaz.T("DisenoCajas.CajaMovidaPuedesDeshacerlo"), 2) : InformeOperacion.Falla(TipoResultado.Bloqueada, IdiomaInterfaz.T("DisenoCajas.CasillasBloqueadasEquipoImpiden"));
    }

    public InformeOperacion FijarDesbloqueadas(int cuantas)
    {
        if (!AdmiteDesbloqueo) return InformeOperacion.Falla(TipoResultado.NoAdmitido, IdiomaInterfaz.T("DisenoCajas.PartidaTieneCajasDesbloquear"));
        cuantas = Math.Clamp(cuantas, 0, Sav.BoxCount);
        if (Sav.BoxesUnlocked == cuantas) return InformeOperacion.Falla(TipoResultado.SinCambios, IdiomaInterfaz.T("DisenoCajas.NumeroCajasDesbloqueadas"));
        var tocadas = Editor.Lote([], () => { Sav.BoxesUnlocked = cuantas; return 1; });
        return tocadas > 0 ? InformeOperacion.Ok(IdiomaInterfaz.F("DisenoCajas.CajasDesbloqueadasPuedesDeshacerlo", cuantas), 0) : InformeOperacion.Falla(TipoResultado.Fallo, IdiomaInterfaz.T("DisenoCajas.PodidoCambiar"));
    }
}
