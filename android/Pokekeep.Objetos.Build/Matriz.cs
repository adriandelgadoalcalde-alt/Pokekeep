// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Text;
using PKHeX.Core;
using Pokekeep.Sprites;

namespace Pokekeep.Objetos.Build;

/// <summary>Las cinco categorías de la matriz final (X18). Un icono genérico o una coincidencia de nombre nunca es «arte exacto».</summary>
public enum Categoria { ArteExacto, ArteOtraVersion, Aproximacion, SinArte, SinIconoAplicable }

/// <summary>Una fila de la matriz: un objeto de un juego, la imagen que sale, de dónde, su huella y la categoría con su prueba.</summary>
public sealed record FilaMatriz(ObjetoJuego Objeto, string Imagen, string Procedencia, string Hash, Categoria Categoria, string Prueba, Categoria AntesX14, Categoria TrasX14);

/// <summary>
/// Matriz final de cobertura de iconos de objetos: una fila por cada entrada objeto-juego del Core (<c>InventoryPouch.GetAllItems</c>; el DENOMINADOR), con tres instantáneas: antes de X14 (solo PKHeX), tras X14 y tras X18.
/// Reglas de categoría (fijas, ver <see cref="Clasificar"/>):
/// <list type="bullet">
/// <item><b>arte exacto</b>: imagen sacada del propio juego (o de su misma generación) con procedencia que lo acredita: el arte de Gen. 3 de PokeAPI en Rubí/Zafiro/Esmeralda/Rojo Fuego/Verde Hoja, la carpeta gen5 en Blanco/Negro (1 y 2),
///   gen8 en Espada/Escudo, gen9 y los iconos HD de pokesprites/all en Escarlata/Púrpura.</item>
/// <item><b>arte de otra versión</b>: un icono real de la serie principal que no se acredita como el de ESE juego (PKHeX: icono moderno unificado; PokeAPI de otra generación; PokéSprite; discos por tipo fuera de Gen. 5).</item>
/// <item><b>aproximación</b>: no es el icono del objeto: el disco de MT del tipo para las DR, el icono común de MT/DR cuando no se sabe el movimiento (Z-A).</item>
/// <item><b>sin arte</b>: el juego tiene icono pero no hay fuente verificable.</item>
/// <item><b>sin icono aplicable</b>: el juego original no dibuja iconos de objeto (Gen. 1 y 2: la mochila es una lista de texto) o el objeto es un marcador interno sin icono.</item>
/// </list>
/// </summary>
public static class Matriz
{
    private static string Nombre(Categoria c) => c switch
    {
        Categoria.ArteExacto => "arte exacto", Categoria.ArteOtraVersion => "arte de otra version", Categoria.Aproximacion => "aproximacion",
        Categoria.SinArte => "sin arte", _ => "sin icono aplicable",
    };

    public static readonly Categoria[] Todas = [Categoria.ArteExacto, Categoria.ArteOtraVersion, Categoria.Aproximacion, Categoria.SinArte, Categoria.SinIconoAplicable];

    /// <summary>Recursos del paquete de objetos restringidos a un conjunto de claves (la instantánea «tras X14»).</summary>
    private sealed class Filtrado(IRecursosSprites inner, HashSet<string> permitidas) : IRecursosSprites
    {
        public SpriteImage? Obtener(string nombre) => permitidas.Contains(nombre) ? inner.Obtener(nombre) : null;
    }

    internal sealed record Proc(string Clave, string Tipo, string Nota, string Identificador, string Carpeta, string Sha, string Revision, string Fuente, string Url, string Evidencia);

    private static Dictionary<string, Proc> LeerProcedencia(string tsv)
        => File.ReadAllLines(tsv).Skip(1).Select(l => l.Split('\t')).ToDictionary(v => v[0],
            v => new Proc(v[0], v[1], v[2], v[3], v[4], v[8], v[10], v.Length > 13 ? v[13] : "PokeAPI/sprites", v[12], v.Length > 14 ? v[14] : ""), StringComparer.Ordinal);

    /// <summary>El juego original no dibuja iconos de objeto (Gen. 1 y 2) o el objeto es un marcador interno. Devuelve el motivo, o null.</summary>
    public static string? SinIcono(ObjetoJuego o)
    {
        if (o.Contexto is EntityContext.Gen1 or EntityContext.Gen2) return "Gen. 1 y 2: la mochila del juego original es una lista de texto, sin icono por objeto";
        var n = o.NombreEn.Trim();
        if (n.Length == 0 || n.All(c => c == '?') || n.Equals("unused", StringComparison.OrdinalIgnoreCase) || n.All(c => c == '-')) return "marcador interno sin icono";
        return null;
    }

    /// <summary>La categoría de una fila con imagen (<paramref name="recurso"/> no nulo) y su prueba.</summary>
    internal static (Categoria, string) Clasificar(ObjetoJuego o, string recurso, Proc? p)
    {
        var juego = o.Juego.Id;
        if (recurso.StartsWith("xorig_", StringComparison.Ordinal) && p is not null)
            return (Categoria.ArteExacto, p.Evidencia);
        if (recurso.StartsWith("xpret_", StringComparison.Ordinal) && p is not null)
            return (Categoria.ArteExacto, p.Evidencia);
        if (juego == "RS" && recurso.StartsWith("xjp_", StringComparison.Ordinal))
            return (Categoria.ArteOtraVersion, "Rubí/Zafiro no tiene catálogo de iconos por objeto (pret/pokeruby); gen3 de PokeAPI no acredita arte de RS");
        if (recurso is "bitem_tm" or "bitem_tr") return (Categoria.Aproximacion, "icono común de MT/DR de PKHeX: no se sabe qué movimiento enseña la máquina en este juego (Z-A)");
        if (recurso.StartsWith("xdr_", StringComparison.Ordinal)) return (Categoria.Aproximacion, "DR: PokeAPI no trae arte de DR; se usa el disco de MT del tipo del movimiento (tipo sacado de las tablas del Core)");
        if (recurso.StartsWith("bitem_", StringComparison.Ordinal) || recurso.StartsWith("aitem_", StringComparison.Ordinal))
            return (Categoria.ArteOtraVersion, $"PKHeX: {recurso} del paquete de sprites fijado (icono moderno unificado; no se acredita como el dibujo de este juego)");
        if (p is null) return (Categoria.ArteOtraVersion, "recurso sin fila de procedencia");
        var prueba = p.Evidencia.Length > 0 ? p.Evidencia : $"{p.Fuente}@{p.Revision[..8]} {p.Carpeta}/{p.Identificador}";
        if (recurso.StartsWith("xmt_", StringComparison.Ordinal) || recurso.StartsWith("xmo_", StringComparison.Ordinal))
            return (p.Carpeta == "gen5" && juego is "BW" or "B2W2" ? Categoria.ArteExacto : Categoria.ArteOtraVersion, $"disco por tipo ({p.Fuente} {p.Carpeta}/{p.Identificador}); movimiento y tipo de las tablas del Core");
        if (recurso.StartsWith("xjp_", StringComparison.Ordinal) || recurso.StartsWith("xj_", StringComparison.Ordinal)) return (Categoria.ArteExacto, prueba);
        if (recurso.StartsWith("xps_", StringComparison.Ordinal)) return (Categoria.ArteOtraVersion, prueba + "; PokéSprite no acredita el juego de origen del icono");
        // xitem_ / xnat_: PokeAPI por nombre; es exacto solo si la carpeta es la de la generación del juego.
        var exacto = (p.Carpeta == "gen5" && juego is "BW" or "B2W2") || (p.Carpeta == "gen8" && juego == "SWSH") || (p.Carpeta == "gen9" && juego == "SV");
        return (exacto ? Categoria.ArteExacto : Categoria.ArteOtraVersion, prueba);
    }

    public sealed record Resultado(IReadOnlyList<FilaMatriz> Filas, string Detalle, string Resumen);

    public static Resultado Calcular(string carpetaSprites, string carpetaObjetos, string? carpetaSaves, string tsvProcedencia, string? tsvSinResolver, string tsvClavesX14)
    {
        var manifiestoPkhex = Manifiesto.Leer(File.ReadAllBytes(Path.Combine(carpetaSprites, "manifest.json")));
        var pkhex = new RecursosEmpaquetados(manifiestoPkhex, f => File.ReadAllBytes(Path.Combine(carpetaSprites, f)));
        var mo = Manifiesto.Leer(File.ReadAllBytes(Path.Combine(carpetaObjetos, "manifest.json")));
        var objetos = new RecursosEmpaquetados(mo, f => File.ReadAllBytes(Path.Combine(carpetaObjetos, f)));
        var claves14 = File.ReadAllLines(tsvClavesX14).Where(l => l.Length > 0).ToHashSet(StringComparer.Ordinal);
        var proc = LeerProcedencia(tsvProcedencia);
        var motivos = new Dictionary<(string, int), string>();
        if (tsvSinResolver is not null && File.Exists(tsvSinResolver))
            foreach (var l in File.ReadAllLines(tsvSinResolver).Skip(1)) { var v = l.Split('\t'); if (v.Length >= 4 && int.TryParse(v[1], out var n)) motivos[(v[0], n)] = v[3]; }

        var soloPkhex = new SpriteComposer(pkhex);
        var x14 = new SpriteComposer(new RecursosPresentacion(pkhex, new Filtrado(objetos, claves14)));
        var x18 = new SpriteComposer(new RecursosPresentacion(pkhex, objetos));
        var ingles = GameInfo.GetStrings("en");
        Universo.CarpetaSaves = carpetaSaves;
        var filas = new List<FilaMatriz>();
        foreach (var j in Universo.Juegos)
            foreach (var o in Universo.Objetos(j, ingles))
            {
                var (_, antesRecurso, antesEquivocado) = Antes(soloPkhex, o);
                var antes = Categoria_(o, antesEquivocado ? null : antesRecurso, proc, motivos).Categoria;
                var (img14, rec14) = x14.ResolverObjeto(o.IdNativo, o.Contexto, o.Version);
                var tras14 = Categoria_(o, img14 is null ? null : rec14, proc, motivos).Categoria;
                var (img18, rec18) = x18.ResolverObjeto(o.IdNativo, o.Contexto, o.Version);
                var (cat, prueba) = Categoria_(o, img18 is null ? null : rec18, proc, motivos);
                string imagen = "", procedencia = "", hash = "";
                if (img18 is not null && rec18 is not null)
                {
                    imagen = rec18;
                    if (pkhex.Contiene(rec18)) { procedencia = "PKHeX " + rec18; hash = Fuente.Sha(pkhex.ObtenerPng(rec18)!); }
                    else
                    {
                        var p = proc[rec18];
                        procedencia = $"{p.Fuente}@{p.Revision[..8]} {(p.Carpeta == "(raiz)" ? "" : p.Carpeta + "/")}{p.Identificador}";
                        hash = Fuente.Sha(objetos.ObtenerPng(rec18)!);
                        if (!string.Equals(hash, p.Sha, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException($"{rec18}: el SHA-256 del paquete no es el de la procedencia");
                    }
                }
                filas.Add(new FilaMatriz(o, imagen, procedencia, hash, cat, prueba, antes, tras14));
            }
        return new Resultado(filas, Detalle(filas), Resumen(filas));
    }

    private static (Categoria Categoria, string Prueba) Categoria_(ObjetoJuego o, string? recurso, Dictionary<string, Proc> proc, Dictionary<(string, int), string> motivos)
    {
        if (recurso is not null)
            return Clasificar(o, recurso, proc.GetValueOrDefault(recurso));
        if (SinIcono(o) is { } sinIcono) return (Categoria.SinIconoAplicable, sinIcono);
        var motivo = motivos.GetValueOrDefault((o.Juego.Id, o.IdNativo)) ?? "sin fuente verificable de arte para este objeto";
        var limite = o.Juego.Id is "SWSH" or "SV"
            ? "; LIMITE REAL: el arte original está en The Spriters Resource (assets 123706 y 187089), que responde con un desafío de Cloudflare (403) y no se descarga de forma automatizada"
            : "; sin fuente verificable accesible (PokeAPI, PokéSprite y pokesprites/all no lo tienen; Bulbapedia, Serebii y The Spriters Resource devuelven un desafío de Cloudflare)";
        return (Categoria.SinArte, motivo + limite);
    }

    /// <summary>Lo que dibujaba la app con solo PKHeX: (imagen, recurso, icono equivocado). En Gen. 1 la conversión del Core da OTRO objeto, así que su imagen cuenta como ausente.</summary>
    private static (SpriteImage?, string?, bool) Antes(SpriteComposer c, ObjetoJuego o)
    {
        var lump = HeldItemLumpUtil.GetIsLump(o.IdMostrado, o.Contexto);
        if (lump.IsLump()) return (null, lump == HeldItemLumpImage.TechnicalMachine ? "bitem_tm" : "bitem_tr", false);
        var img = c.GetItemSpriteBolsa(o.IdNativo, o.Contexto);
        if (img is null) return (null, null, false);
        if (o.Contexto == EntityContext.Gen1) return (img, null, true);
        return (img, $"bitem_{o.IdMostrado}", false);
    }

    private static string Detalle(IReadOnlyList<FilaMatriz> filas)
    {
        var sb = new StringBuilder("juego\tversion\tid_nativo\tnombre_en\tbolsillo\timagen\tprocedencia\thash_sha256\tcategoria\tprueba\tantes_x14\ttras_x14\n");
        foreach (var f in filas)
        {
            var o = f.Objeto;
            sb.Append(o.Juego.Id).Append('\t').Append(o.Version).Append('\t').Append(o.IdNativo).Append('\t').Append(o.NombreEn).Append('\t').Append(o.Bolsillos)
                .Append('\t').Append(f.Imagen).Append('\t').Append(f.Procedencia).Append('\t').Append(f.Hash).Append('\t').Append(Nombre(f.Categoria)).Append('\t').Append(f.Prueba)
                .Append('\t').Append(Nombre(f.AntesX14)).Append('\t').Append(Nombre(f.TrasX14)).Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>Resumen por juego: objetos (denominador) y cuántos hay en cada categoría antes de X14, tras X14 y tras X18, con la fila TOTAL.</summary>
    public static string Resumen(IReadOnlyList<FilaMatriz> filas)
    {
        var sb = new StringBuilder("juego\tnombre\tobjetos");
        foreach (var etapa in new[] { "antes_x14", "tras_x14", "tras_x18" })
            foreach (var c in Todas) sb.Append('\t').Append(etapa).Append('_').Append(Nombre(c).Replace(' ', '_'));
        sb.Append('\n');
        void Fila(string id, string nombre, IReadOnlyList<FilaMatriz> g)
        {
            sb.Append(id).Append('\t').Append(nombre).Append('\t').Append(g.Count);
            foreach (var sel in new Func<FilaMatriz, Categoria>[] { f => f.AntesX14, f => f.TrasX14, f => f.Categoria })
                foreach (var c in Todas) sb.Append('\t').Append(g.Count(f => sel(f) == c));
            sb.Append('\n');
        }
        foreach (var g in filas.GroupBy(f => f.Objeto.Juego)) Fila(g.Key.Id, g.Key.Nombre, g.ToList());
        Fila("TOTAL", "Los 23 juegos", filas.ToList());
        return sb.ToString();
    }
}
