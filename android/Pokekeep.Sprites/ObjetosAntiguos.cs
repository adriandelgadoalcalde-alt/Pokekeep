// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;

namespace Pokekeep.Sprites;

/// <summary>Las listas de objetos antiguas de PKHeX: Gen. 1 y 2, Gen. 3 (Rubí/Zafiro/Esmeralda/Rojo Fuego/Verde Hoja) y las dos de GameCube, que numeran distinto.</summary>
public enum FamiliaAntigua : byte { Gen1, Gen2, Gen3, Colosseum, XD }

/// <summary>
/// Objetos de Gen. 1-3 cuyo id nativo <c>ItemConverter.GetItemDisplay</c> deja SIN id moderno (devuelve el marcador 128, «no existe»: casi todos los objetos clave, las cartas...) o, en Gen. 1, lo convierte MAL, aunque el
/// MISMO objeto existe en las listas modernas de PKHeX y PKHeX tiene su sprite (<c>bitem_N</c>). La tabla de abajo da ese id moderno. NO está escrita a mano: la genera
/// <c>Pokekeep.Objetos.Build</c> cruzando el nombre inglés de cada objeto antiguo con la lista moderna del Core (solo coincidencias únicas y los renombrados verificados de <c>alias-modernos.tsv</c>) y una
/// prueba comprueba que sigue siendo la que sale hoy del Core (<c>MapaObjetosAntiguosTests</c>).
/// </summary>
public static partial class ObjetosAntiguos
{
    /// <summary>Marcador de «sin id moderno» de <c>ItemConverter</c> (su constante privada <c>NaN</c>).</summary>
    public const int SinIdModerno = 128;

    public static FamiliaAntigua? Familia(EntityContext ctx, GameVersion version) => ctx switch
    {
        EntityContext.Gen1 => FamiliaAntigua.Gen1,
        EntityContext.Gen2 => FamiliaAntigua.Gen2,
        EntityContext.Gen3 => version == GameVersion.COLO ? FamiliaAntigua.Colosseum : version is GameVersion.XD or GameVersion.CXD ? FamiliaAntigua.XD : FamiliaAntigua.Gen3,
        _ => null,
    };

    /// <summary>El id moderno del objeto antiguo <paramref name="idNativo"/> cuando <c>GetItemDisplay</c> no lo conoce; 0 si no hay equivalencia (o no es un formato antiguo).</summary>
    public static int IdModerno(EntityContext ctx, GameVersion version, int idNativo)
    {
        if (Familia(ctx, version) is not { } f) return 0;
        var t = Tabla(f);
        var i = Array.BinarySearch(t, new Par((ushort)idNativo, 0), ComparadorNativo.Instancia);
        return i < 0 ? 0 : t[i].Moderno;
    }

    /// <summary>
    /// Id moderno EFECTIVO del objeto de la mochila: el de <c>GetItemDisplay</c> si es de verdad y, si no, el de la tabla (por nombre). 0 si ninguno.
    /// Gen. 1 es la excepción: <c>GetItemDisplay(Gen1)</c> aplica <c>GetItemFuture1</c>, la conversión de la «tasa de captura» (el objeto que el Pokémon de Gen. 1 llevaba escondido en ese byte), y la mochila de Gen. 1 NO usa esos
    /// ids (su Poké Ball es la 4 y no la de Gen. 2): da OTRO objeto (la Poké Ball sale como Gran Ball). Ahí manda siempre la tabla.
    /// </summary>
    public static int IdEfectivo(EntityContext ctx, GameVersion version, int idNativo)
    {
        if (ctx == EntityContext.Gen1) return IdModerno(ctx, version, idNativo);
        var m = ItemConverter.GetItemDisplay(idNativo, ctx);
        if (m <= 0 || m >= ushort.MaxValue) return 0;
        if (m == SinIdModerno && ctx <= EntityContext.Gen3) return IdModerno(ctx, version, idNativo);
        return m;
    }

    internal readonly record struct Par(ushort Nativo, ushort Moderno);

    private sealed class ComparadorNativo : IComparer<Par>
    {
        public static readonly ComparadorNativo Instancia = new();
        public int Compare(Par x, Par y) => x.Nativo.CompareTo(y.Nativo);
    }

    private static Par[] Tabla(FamiliaAntigua f) => f switch
    {
        FamiliaAntigua.Gen1 => TablaGen1,
        FamiliaAntigua.Gen2 => TablaGen2,
        FamiliaAntigua.Gen3 => TablaGen3,
        FamiliaAntigua.Colosseum => TablaColosseum,
        _ => TablaXD,
    };
}
