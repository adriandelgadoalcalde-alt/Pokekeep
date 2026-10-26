// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;

namespace Pokekeep.Mobile.Ui.Editor;

/// <summary>Una opcion de una lista del editor (especie, objeto, lugar...). <see cref="Id"/> es el valor que el Core guarda; <see cref="Texto"/> el nombre en castellano.</summary>
public sealed record OpcionCampo(int Id, string Texto, string? Detalle = null, int Tipo = -1, string? TipoTexto = null)
{
    public override string ToString() => Texto;
}

/// <summary>
/// Las listas del editor (como <c>GameInfo.FilteredSources</c> de PKHeX) construidas con INSTANCIAS propias en castellano de Espana, sin tocar el estado global del Core
/// (<c>GameInfo.Strings</c> sigue en el idioma por defecto): los demas modulos de la aplicacion siguen viendo lo mismo que antes. Las listas se filtran por la partida (especies que el juego
/// admite, movimientos del formato, objetos que el guardado acepta...) salvo en el modo avanzado, que da las listas completas (HaX de PKHeX).
/// </summary>
public sealed class FuentesEditor
{
    private readonly Dictionary<(GameVersion, EntityContext, bool), IReadOnlyList<OpcionCampo>> _lugares = [];
    private readonly Dictionary<int, IReadOnlyList<OpcionCampo>> _subregiones = [];

    private FuentesEditor(SaveFile sav, bool avanzado)
    {
        Guardado = sav;
        Avanzado = avanzado;
        Textos = Ajustes.Pkhex.ProgramaPkhex.Textos;   // el idioma de los datos (Startup.Language, «es» de fábrica; bloque 10-1)
        Datos = new GameDataSource(Textos);
        Filtro = new FilteredGameDataSource(sav, Datos, avanzado);
        Memorias = new MemoryStrings(Textos);
        Especies = Convertir(Filtro.Species);
        Movimientos = Convertir(Filtro.Moves);
        Recordados = Convertir(Filtro.Relearn);
        var nombresObjetos = _nombresObjetos = Partida.NombresObjetos.Lista(Textos, sav);
        // PKHeX 26.10.18: text_ItemsG2_es.txt termina en salto de línea y el Core lee al final una entrada 255 sin nombre que el inglés no tiene.
        // Solo se quitan las entradas sin nombre del FINAL de la lista; las de en medio son objetos reales del juego y se quedan como en PKHeX.
        var ultimoConNombre = Filtro.Items.Where(i => !string.IsNullOrWhiteSpace(i.Text)).Select(i => i.Value).DefaultIfEmpty(int.MaxValue).Max();
        Objetos = [.. Filtro.Items.Where(i => (i.Value <= ultimoConNombre || !string.IsNullOrWhiteSpace(i.Text)) && (avanzado || !Partida.ObjetosRelleno.EsRelleno(sav, i.Value))).Select(i => new OpcionCampo(i.Value, (uint)i.Value < nombresObjetos.Length ? nombresObjetos[i.Value] : i.Text))];
        Juegos = Convertir(Filtro.Games);
        Idiomas = Convertir(Filtro.Languages);
        Bolas = Convertir(Filtro.Balls);
        Habilidades = Convertir(avanzado ? Datos.AbilityDataSource : Filtro.Abilities);
        Naturalezas = Convertir(Filtro.Natures);
        Terrenos = Convertir(Filtro.G4GroundTiles);
        RegionesConsola = Convertir(Filtro.ConsoleRegions);
        Paises = Convertir(Util.GetCountryRegionList("countries", Pokekeep.Mobile.Ui.Ajustes.Pkhex.ProgramaPkhex.Idioma));
        Tipos = Convertir(Util.GetCBList(Textos.types.AsSpan(1, HiddenPower.TypeCount)));
        TiposTera = CrearTiposTera(Textos);
        Vacio = new OpcionCampo(0, string.Empty);
    }

    public static FuentesEditor Crear(SaveFile sav, bool avanzado = false) => new(sav, avanzado);

    public SaveFile Guardado { get; }
    public bool Avanzado { get; }
    public GameStrings Textos { get; }
    public GameDataSource Datos { get; }
    public FilteredGameDataSource Filtro { get; }
    public MemoryStrings Memorias { get; }

    public IReadOnlyList<OpcionCampo> Especies { get; }
    public IReadOnlyList<OpcionCampo> Movimientos { get; }
    public IReadOnlyList<OpcionCampo> Recordados { get; }
    public IReadOnlyList<OpcionCampo> Objetos { get; }
    public IReadOnlyList<OpcionCampo> Juegos { get; }
    public IReadOnlyList<OpcionCampo> Idiomas { get; }
    public IReadOnlyList<OpcionCampo> Bolas { get; }
    public IReadOnlyList<OpcionCampo> Habilidades { get; }
    public IReadOnlyList<OpcionCampo> Naturalezas { get; }
    public IReadOnlyList<OpcionCampo> Terrenos { get; }
    public IReadOnlyList<OpcionCampo> RegionesConsola { get; }
    public IReadOnlyList<OpcionCampo> Paises { get; }
    /// <summary>Los 16 tipos de Poder Oculto (sin Normal ni «???»), con el Id del Core (indice dentro de la lista de tipos menos uno).</summary>
    public IReadOnlyList<OpcionCampo> Tipos { get; }
    /// <summary>«---» (sin teratipo forzado), los 18 tipos y Astral, con el valor que guarda el Core.</summary>
    public IReadOnlyList<OpcionCampo> TiposTera { get; }
    public OpcionCampo Vacio { get; }

    public static IReadOnlyList<OpcionCampo> Convertir(IEnumerable<ComboItem> lista) => [.. lista.Select(c => new OpcionCampo(c.Value, c.Text))];

    private static IReadOnlyList<OpcionCampo> CrearTiposTera(GameStrings s)
    {
        var tipos = s.types.AsSpan();
        var l = new List<OpcionCampo> { new(TeraTypeUtil.OverrideNone, "---") };
        for (var i = 0; i < TeraTypeUtil.StellarTypeDisplayStringIndex; i++) l.Add(new OpcionCampo(i, tipos[i]));
        l.Add(new OpcionCampo(TeraTypeUtil.Stellar, tipos[TeraTypeUtil.StellarTypeDisplayStringIndex]));
        return l;
    }

    /// <summary>Lugares de encuentro (o de huevo) del grupo de juegos de la version y el contexto del formato, como <c>ReloadMetLocations</c> de PKHeX.</summary>
    public IReadOnlyList<OpcionCampo> Lugares(GameVersion version, EntityContext contexto, bool huevo)
    {
        if (_lugares.TryGetValue((version, contexto, huevo), out var l)) return l;
        IReadOnlyList<OpcionCampo> r;
        try { r = Convertir(Datos.Met.GetLocationList(version, contexto, huevo)); }
        catch { r = []; }
        return _lugares[(version, contexto, huevo)] = r;
    }

    /// <summary>La version que PKHeX usa para elegir la lista de lugares (<c>CheckMetLocationChange</c>): el grupo de la version del Pokemon, o el del guardado si no la tiene.</summary>
    public GameVersion VersionDeLugares(PKM pk)
    {
        var version = pk.Version;
        var grupo = GameUtil.GetMetLocationVersionGroup(version);
        if (grupo is GameVersion.Invalid)
        {
            grupo = GameUtil.GetMetLocationVersionGroup(Guardado.Version);
            if (grupo is GameVersion.Invalid || version is GameVersion.Any) version = grupo = pk.Context.GetSingleGameVersion();
        }
        if (pk.Format == 2) return GameVersion.C;          // las partidas de Gen. II no guardan el juego de origen: la lista de lugares es la de Cristal
        return grupo is GameVersion.Invalid ? version : version;
    }

    /// <summary>Subregiones del pais (<c>sr_NNN</c> de PKHeX). Vacia si el pais es 0.</summary>
    public IReadOnlyList<OpcionCampo> Subregiones(int pais)
    {
        if (pais <= 0) return [Vacio];
        if (_subregiones.TryGetValue(pais, out var l)) return l;
        try { l = Convertir(Util.GetCountryRegionList($"sr_{pais:000}", Pokekeep.Mobile.Ui.Ajustes.Pkhex.ProgramaPkhex.Idioma)); } catch { l = [Vacio]; }
        return _subregiones[pais] = l;
    }

    /// <summary>Nombre de una lista por id, o «—» si no esta (un valor que el formato permite pero la lista no recoge).</summary>
    public static string Nombre(IReadOnlyList<OpcionCampo> lista, int id)
    {
        foreach (var o in lista) if (o.Id == id) return o.Texto;
        return "—";
    }

    public string NombreMovimiento(int id) => (uint)id < Textos.movelist.Length ? Textos.movelist[id] : "—";
    private readonly string[] _nombresObjetos;
    public string NombreObjeto(int id) => (uint)id < _nombresObjetos.Length && !string.IsNullOrEmpty(_nombresObjetos[id]) ? _nombresObjetos[id] : "—";
    /// <summary>Nombre del objeto equipado de <paramref name="pk"/> con la lista de SU contexto (id nativo del formato), como el selector del editor.</summary>
    public string NombreObjeto(PKM pk) => pk.HeldItem <= 0 ? "—" : pk.Context == Guardado.Context ? NombreObjeto(pk.HeldItem) : ObjetosPkm.Nombre(Textos, pk, Guardado.Version) is { Length: > 0 } n ? n : "—";
    public string NombreEspecie(int id) => (uint)id < Textos.specieslist.Length ? Textos.specieslist[id] : "—";
}
