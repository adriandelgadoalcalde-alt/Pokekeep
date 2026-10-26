// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Catalog.Core;
using AutoLegalidad.Catalog.Db;
using PKHeX.Core;

namespace AutoLegalidad.Engine.Catalog;

/// <summary>Clave de catalogo (la misma que escribe el Hito 1) de un encuentro del Core. <c>null</c> si el encuentro no es un tipo que el catalogo indexe.</summary>
public static class EncounterKeys
{
    public static string? For(IEncounterable enc)
    {
        if (enc is DataMysteryGift g)
            return CoreMgdbIndex.KeyFor(g.GetType().Name, CoreMgdbIndex.StableSha256(g));
        if (enc is not IEncounterTemplate t)
            return null;
        var context = enc is IContext c ? c.Context : EntityContext.None;
        return $"core:{enc.GetType().Name}:{t.Version}:{context}:{t.Species:D4}-{t.Form}:{(t.IsEgg ? "egg" : "std")}";
    }

    private static readonly string[][] TwinVersions =
    [
        ["SW", "SH"], ["X", "Y"], ["OR", "AS"], ["SN", "MN"], ["US", "UM"], ["B", "W"], ["B2", "W2"], ["D", "P", "Pt"], ["HG", "SS"],
        ["R", "S", "E"], ["FR", "LG"], ["GP", "GE"], ["BD", "SP"], ["SL", "VL"], ["GD", "SI"],
    ];

    /// <summary>
    /// ¿Son la misma version a efectos de origen? Igual, gemelas (Espada/Escudo...) o una etiqueta de grupo del Core que contiene a la otra (BW contiene B y W; Gen5 contiene a todas las de
    /// la generacion): el Core etiqueta algunas plantillas con el grupo (p. ej. EncounterStatic5Entree es «BW» o «Gen5» segun la entrada) y el analisis puede devolver cualquiera de las etiquetas.
    /// </summary>
    private static bool VersionsMatch(string a, string b)
    {
        if (string.Equals(a, b, StringComparison.Ordinal)) return true;
        if (TwinVersions.Any(g => g.Contains(a, StringComparer.Ordinal) && g.Contains(b, StringComparer.Ordinal))) return true;
        return Enum.TryParse<GameVersion>(a, false, out var va) && Enum.TryParse<GameVersion>(b, false, out var vb) && (va.Contains(vb) || vb.Contains(va));
    }

    /// <summary>
    /// ¿Son la misma entrada de origen? Igual clave, o claves que solo difieren en la version de un par de juegos gemelos (Espada/Escudo...): el Core reparte la misma plantilla
    /// de incursion/estatico en una copia por version y el analisis oficial puede elegir la gemela; el Pokemon resultante es el mismo encuentro (misma especie, forma, tipo y contexto).
    /// </summary>
    public static bool SameOrigin(string? a, string? b)
    {
        if (string.Equals(a, b, StringComparison.Ordinal)) return true;
        if (a is null || b is null) return false;
        var pa = a.Split(':'); var pb = b.Split(':');
        if (pa.Length != 6 || pb.Length != 6) return false;
        for (var i = 0; i < 6; i++)
            if (i != 2 && !string.Equals(pa[i], pb[i], StringComparison.Ordinal)) return false;
        return VersionsMatch(pa[2], pb[2]);
    }

    /// <summary>
    /// ¿Mismo origen dentro de una linea evolutiva? Como <see cref="SameOrigin"/>, pero ademas la especie/forma del encuentro (campo 4 de la clave) puede ser otra: un Pokemon
    /// pedido como Nidoking con origen «GO» es legal si el Core lo valida con el hueco GO de Nidorino (regla 14: no se exige que la especie del encuentro sea la final cuando hay
    /// una evolucion legitima). Es seguro porque el Core ya ha validado el candidato CON ese encuentro y evolucionando hasta la especie final: solo se acepta mismo tipo de
    /// encuentro, mismo contexto, mismo huevo/no huevo y version gemela.
    /// </summary>
    public static bool SameOriginLine(string? a, string? b)
    {
        if (SameOrigin(a, b)) return true;
        if (a is null || b is null) return false;
        var pa = a.Split(':'); var pb = b.Split(':');
        if (pa.Length != 6 || pb.Length != 6) return false;
        for (var i = 0; i < 6; i++)
            if (i != 2 && i != 4 && !string.Equals(pa[i], pb[i], StringComparison.Ordinal)) return false;
        return VersionsMatch(pa[2], pb[2]);
    }

    /// <summary>Los huevos del Core no son plantillas fijas (se construyen por especie); el catalogo los ancla en el propio Core (Hito 1, ADR 0008).</summary>
    public static bool IsCoreEgg(IEncounterable enc) => enc.IsEgg && enc.GetType().Name.StartsWith("EncounterEgg", StringComparison.Ordinal);
}

/// <summary>Barrera de generacion (regla 8): solo se genera desde encuentros aprobados en <c>v_encounter_generable</c> (o huevos anclados en el Core).</summary>
public sealed class CatalogGate(CatalogReader catalog)
{
    public bool IsGenerable(IEncounterable enc, out string? key)
    {
        key = EncounterKeys.For(enc);
        if (key is not null && catalog.GenerableKeys.Contains(key))
            return true;
        return EncounterKeys.IsCoreEgg(enc);
    }
}
