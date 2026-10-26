// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;

namespace Pokekeep.Sprites;

/// <summary>
/// Nombres de los recursos del paquete de objetos adicionales (X14), generado por <c>herramientas\generar-objetos-android.ps1</c> desde PokeAPI/sprites (revisión fijada).
/// Solo se consulta DESPUÉS de PKHeX, salvo las MT/MO/DR, donde el icono por tipo gana al genérico de PKHeX (<c>bitem_tm</c>). Los nombres no chocan con los de PKHeX (todos empiezan por <c>x</c>).
/// </summary>
public static class ObjetosExtra
{
    /// <summary>Estilo del conjunto de iconos: <c>g5</c> = carpeta gen5 de PokeAPI (juegos de Gen. 1-5, donde cubre) y <c>mo</c> = el moderno (raíz y gen8/gen9). Nunca se inventa uno: es solo qué carpeta de la fuente toca.</summary>
    public static string Estilo(EntityContext ctx) => ctx is >= EntityContext.Gen1 and <= EntityContext.Gen5 ? "g5" : "mo";

    /// <summary>El otro estilo, por si el elegido no tiene ese recurso.</summary>
    public static string Otro(string estilo) => estilo == "g5" ? "mo" : "g5";

    /// <summary>Nombres de tipo de PokeAPI en el orden de <see cref="MoveType"/> (0 = normal ... 17 = hada).</summary>
    public static readonly string[] Tipos =
        ["normal", "fighting", "flying", "poison", "ground", "rock", "bug", "ghost", "steel", "fire", "water", "grass", "electric", "psychic", "ice", "dragon", "dark", "fairy"];

    public static string NombreTipo(byte tipo) => tipo < Tipos.Length ? Tipos[tipo] : "normal";

    /// <summary>MT/MO/DR por tipo: <c>xmt_g5_electric</c>, <c>xmo_mo_water</c>, <c>xdr_mo_fire</c>.</summary>
    public static string ClaveMaquina(ClaseMaquina clase, string estilo, byte tipo)
        => $"{clase switch { ClaseMaquina.MT => "xmt", ClaseMaquina.MO => "xmo", _ => "xdr" }}_{estilo}_{NombreTipo(tipo)}";

    /// <summary>Objeto sin sprite en PKHeX, por su id modernizado (el de <c>ItemConverter.GetItemDisplay</c>): <c>xitem_mo_93</c>.</summary>
    public static string ClaveObjeto(string estilo, int idMostrado) => $"xitem_{estilo}_{idMostrado}";

    /// <summary>Objeto de Gen. 1-3 sin equivalente moderno (id nativo): <c>xnat_3_123</c>.</summary>
    public static string ClaveNativo(EntityContext ctx, int idNativo) => $"xnat_{(int)ctx}_{idNativo}";

    /// <summary>
    /// Arte de UN juego, por su id NATIVO: <c>xj_SV_1888</c> (rellena un hueco: va después de PKHeX) o <c>xjp_RS_187</c> (arte propio de ese juego que gana a PKHeX, p. ej. el de Gen. 3 de PokeAPI, que difiere del moderno).
    /// <paramref name="grupo"/> es el identificador de <see cref="Grupo"/> (el mismo que el censo de cobertura).
    /// </summary>
    public static string ClaveJuego(string grupo, int idNativo, bool prioritaria) => $"{(prioritaria ? "xjp" : "xj")}_{grupo}_{idNativo}";

    /// <summary>Gráfico y paleta de pret para E/FRLG; antes que el arte moderno o genérico.</summary>
    public static string ClavePret(string grupo, int idNativo) => $"xpret_{grupo}_{idNativo}";

    public static string ClaveOriginal(string grupo, int idNativo) => $"xorig_{grupo}_{idNativo}";

    /// <summary>Objeto sin arte en las demás fuentes, de PokéSprite (revisión fijada), verificado PARA ESE JUEGO (id nativo): <c>xps_SWSH_1081</c>.</summary>
    public static string ClavePokeSprite(string grupo, int idNativo) => $"xps_{grupo}_{idNativo}";

    /// <summary>
    /// El juego (o pareja/familia de juegos con la misma mochila) al que pertenece una versión: <c>RBY</c>, <c>GS</c>, <c>C</c>, <c>RS</c>, <c>E</c>, <c>FRLG</c>, <c>COLO</c>, <c>XD</c>, <c>DP</c>, <c>Pt</c>, <c>HGSS</c>, <c>BW</c>, <c>B2W2</c>,
    /// <c>XY</c>, <c>ORAS</c>, <c>SM</c>, <c>USUM</c>, <c>LGPE</c>, <c>SWSH</c>, <c>BDSP</c>, <c>PLA</c>, <c>SV</c> y <c>ZA</c>. Null si la versión no es de ninguno (el arte por juego no se aplica).
    /// </summary>
    public static string? Grupo(GameVersion v) => v switch
    {
        GameVersion.RD or GameVersion.GN or GameVersion.BU or GameVersion.YW or GameVersion.RB or GameVersion.RBY => "RBY",
        GameVersion.GD or GameVersion.SI or GameVersion.GS => "GS",
        GameVersion.C => "C",
        GameVersion.R or GameVersion.S or GameVersion.RS => "RS",
        GameVersion.E => "E",
        GameVersion.FR or GameVersion.LG or GameVersion.FRLG => "FRLG",
        GameVersion.COLO => "COLO",
        GameVersion.XD or GameVersion.CXD => "XD",
        GameVersion.D or GameVersion.P or GameVersion.DP => "DP",
        GameVersion.Pt => "Pt",
        GameVersion.HG or GameVersion.SS or GameVersion.HGSS => "HGSS",
        GameVersion.B or GameVersion.W or GameVersion.BW => "BW",
        GameVersion.B2 or GameVersion.W2 or GameVersion.B2W2 => "B2W2",
        GameVersion.X or GameVersion.Y or GameVersion.XY => "XY",
        GameVersion.OR or GameVersion.AS or GameVersion.ORAS => "ORAS",
        GameVersion.SN or GameVersion.MN or GameVersion.SM => "SM",
        GameVersion.US or GameVersion.UM or GameVersion.USUM => "USUM",
        GameVersion.GP or GameVersion.GE or GameVersion.GG => "LGPE",
        GameVersion.SW or GameVersion.SH or GameVersion.SWSH => "SWSH",
        GameVersion.BD or GameVersion.SP or GameVersion.BDSP => "BDSP",
        GameVersion.PLA => "PLA",
        GameVersion.SL or GameVersion.VL or GameVersion.SV => "SV",
        GameVersion.ZA => "ZA",
        _ => null,
    };
}

public sealed partial class SpriteComposer
{
    /// <summary>El objeto equipado (id moderno) del paquete de objetos adicionales, si PKHeX no lo dibuja. Con solo los recursos de PKHeX no encuentra nada, así que no cambia lo que PKHeX compone.</summary>
    private SpriteImage? ObjetoAdicional(int idMostrado, EntityContext context)
        => idMostrado <= 0 ? null : Recurso(ObjetosExtra.ClaveObjeto(ObjetosExtra.Estilo(context), idMostrado)) ?? Recurso(ObjetosExtra.ClaveObjeto(ObjetosExtra.Otro(ObjetosExtra.Estilo(context)), idMostrado));

    /// <summary>
    /// Icono de un objeto de la mochila con TODO el arte disponible. Primero, el gráfico y paleta acreditados del propio juego (pret en E/FRLG, y suplementos prioritarios de Gen. 3).
    /// Si no existe ese recurso, se conserva este orden:
    /// 1) MT/MO/DR con el icono de su TIPO (el del movimiento que enseña esa máquina en ese juego; si no hay icono para él, el genérico de PKHeX);
    /// 2) el sprite de PKHeX (<c>bitem_N</c>, o el artwork <c>aitem_N</c>): PKHeX siempre va primero;
    /// 3) los objetos adicionales de PokeAPI (<see cref="ObjetosExtra"/>), por id moderno y, si el id no tiene equivalente moderno, por id nativo.
    /// Null si nadie tiene arte para ese objeto (la interfaz dibuja entonces su propio glifo). <paramref name="idNativo"/> es el del formato (el de la mochila).
    /// </summary>
    public SpriteImage? GetItemSpriteJuego(int idNativo, EntityContext context, GameVersion version) => ResolverObjeto(idNativo, context, version).Imagen;

    /// <summary>Lo mismo, diciendo de qué recurso salió (el nombre del recurso; null si no hay arte). Para el informe de cobertura y las pruebas.</summary>
    public (SpriteImage? Imagen, string? Recurso) ResolverObjeto(int idNativo, EntityContext context, GameVersion version)
    {
        if (idNativo <= 0) return (null, null);
        var estilo = ObjetosExtra.Estilo(context);
        var otro = ObjetosExtra.Otro(estilo);
        var grupo = ObjetosExtra.Grupo(version);
        (SpriteImage?, string?) Primero(params string[] claves)
        {
            foreach (var k in claves) if (Recurso(k) is { } r) return (r, k);
            return (null, null);
        }
        if (grupo is not null && Recurso(ObjetosExtra.ClaveOriginal(grupo, idNativo)) is { } original)
            return (original, ObjetosExtra.ClaveOriginal(grupo, idNativo));
        if (grupo is "E" or "FRLG" && Recurso(ObjetosExtra.ClavePret(grupo, idNativo)) is { } pret)
            return (pret, ObjetosExtra.ClavePret(grupo, idNativo));
        // Arte propio de ESE juego que debe ganar a PKHeX (p. ej. los cinco iconos de Gen. 3 de PokeAPI, distintos de los modernos).
        if (grupo is not null && Recurso(ObjetosExtra.ClaveJuego(grupo, idNativo, prioritaria: true)) is { } propio) return (propio, ObjetosExtra.ClaveJuego(grupo, idNativo, prioritaria: true));
        var mostrado = context == EntityContext.Gen1 ? 0 : ItemConverter.GetItemDisplay(idNativo, context);   // en Gen. 1 el id mostrado del Core no vale para la mochila (ver ObjetosAntiguos.IdEfectivo)
        if (Maquinas.De(version, context, idNativo) is { } m)
        {
            var (i, k) = Primero(ObjetosExtra.ClaveMaquina(m.Clase, estilo, m.Tipo), ObjetosExtra.ClaveMaquina(m.Clase, otro, m.Tipo));
            if (i is not null) return (i, k);
        }
        var lump = HeldItemLumpUtil.GetIsLump(mostrado, context);
        if (lump == HeldItemLumpImage.TechnicalMachine) return Recurso("bitem_tm") is { } tm ? (tm, "bitem_tm") : (null, null);
        if (lump == HeldItemLumpImage.TechnicalRecord) return Recurso("bitem_tr") is { } tr ? (tr, "bitem_tr") : (null, null);
        // El id moderno efectivo: el de ItemConverter y, en Gen. 1-3 donde este lo deja sin id, el de la tabla de equivalencias por nombre (ObjetosAntiguos).
        var efectivo = ObjetosAntiguos.IdEfectivo(context, version, idNativo);
        if (efectivo > 0)
        {
            var (i, k) = Primero($"bitem_{efectivo}", $"aitem_{efectivo}");
            if (i is not null) return (i, k);
        }
        // PKHeX no lo dibuja: primero el arte de ese juego por su id nativo (rellena el hueco) y luego el por id moderno (PokeAPI y, a falta de este, PokéSprite).
        if (grupo is not null && Recurso(ObjetosExtra.ClaveJuego(grupo, idNativo, prioritaria: false)) is { } deJuego) return (deJuego, ObjetosExtra.ClaveJuego(grupo, idNativo, prioritaria: false));
        if (efectivo > 0)
        {
            var (i, k) = Primero(ObjetosExtra.ClaveObjeto(estilo, efectivo), ObjetosExtra.ClaveObjeto(otro, efectivo));
            if (i is not null) return (i, k);
        }
        if (grupo is not null && Recurso(ObjetosExtra.ClavePokeSprite(grupo, idNativo)) is { } deSprite) return (deSprite, ObjetosExtra.ClavePokeSprite(grupo, idNativo));
        return context <= EntityContext.Gen3 ? Primero(ObjetosExtra.ClaveNativo(context, idNativo)) : (null, null);
    }

    /// <summary>
    /// Lo mismo para un id YA modernizado (<c>PKM.SpriteItem</c>: el objeto equipado de una ficha o un regalo). En Gen. 1-3 el id equipado ya no es el nativo, así que ahí solo vale el arte por id moderno;
    /// desde Gen. 4 el id mostrado es el nativo y las máquinas salen por tipo.
    /// </summary>
    public SpriteImage? GetItemSpriteMostrado(int idMostrado, EntityContext context, GameVersion version)
    {
        if (idMostrado <= 0) return null;
        if (context > EntityContext.Gen3) return GetItemSpriteJuego(idMostrado, context, version);
        var visible = GetItemSpriteVisible(idMostrado, context);
        if (visible is not null) return visible;
        var estilo = ObjetosExtra.Estilo(context);
        return Recurso(ObjetosExtra.ClaveObjeto(estilo, idMostrado)) ?? Recurso(ObjetosExtra.ClaveObjeto(ObjetosExtra.Otro(estilo), idMostrado));
    }
}
