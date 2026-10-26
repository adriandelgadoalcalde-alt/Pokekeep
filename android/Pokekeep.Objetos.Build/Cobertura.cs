// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Text;
using PKHeX.Core;
using Pokekeep.Sprites;

namespace Pokekeep.Objetos.Build;

/// <summary>Estado de un objeto respecto a su icono.</summary>
public enum EstadoIcono { Pkhex, PkhexArtwork, GenericoMaquina, IconoPorTipo, Pokeapi, SinSprite, IconoEquivocado, ArteJuego, PokeSprite }

/// <summary>Una fila del informe: cómo se veía el objeto antes (solo PKHeX) y cómo se ve ahora (PKHeX + paquete de objetos).</summary>
public sealed record FilaCobertura(ObjetoJuego Objeto, EstadoIcono Antes, string MotivoAntes, EstadoIcono Despues, string? RecursoDespues, string MotivoDespues, int Partidas);

public static class Cobertura
{
    /// <summary>
    /// Calcula la cobertura de todos los juegos. «Antes» = lo que dibujaba la app con solo los paquetes de PKHeX (<c>GetItemSpriteBolsa</c>); «después» = con el paquete de objetos
    /// (<c>ResolverObjeto</c>). Los dos se calculan con el código real de la app, no con una réplica.
    /// </summary>
    public static IReadOnlyList<FilaCobertura> Calcular(string carpetaSprites, string? carpetaObjetos, string? carpetaSaves, string? tsvSinResolver = null)
    {
        var manifiestoPkhex = Manifiesto.Leer(File.ReadAllBytes(Path.Combine(carpetaSprites, "manifest.json")));
        var pkhex = new RecursosEmpaquetados(manifiestoPkhex, f => File.ReadAllBytes(Path.Combine(carpetaSprites, f)));
        IRecursosSprites completo = pkhex;
        if (carpetaObjetos is not null && File.Exists(Path.Combine(carpetaObjetos, "manifest.json")))
        {
            var m = Manifiesto.Leer(File.ReadAllBytes(Path.Combine(carpetaObjetos, "manifest.json")));
            completo = new RecursosPresentacion(pkhex, new RecursosEmpaquetados(m, f => File.ReadAllBytes(Path.Combine(carpetaObjetos, f))));
        }
        var soloPkhex = new SpriteComposer(pkhex);
        var todo = new SpriteComposer(completo);
        var ingles = GameInfo.GetStrings("en");
        Universo.CarpetaSaves = carpetaSaves;
        var (_, porObjeto) = carpetaSaves is null ? (new Dictionary<string, int>(), new Dictionary<(string, int), int>()) : Universo.ContarEnPartidas(carpetaSaves);
        var motivos = new Dictionary<(string, int), string>();
        if (tsvSinResolver is not null && File.Exists(tsvSinResolver))
            foreach (var l in File.ReadAllLines(tsvSinResolver).Skip(1)) { var v = l.Split('\t'); if (v.Length >= 4 && int.TryParse(v[1], out var n)) motivos[(v[0], n)] = v[3]; }
        var filas = new List<FilaCobertura>();
        foreach (var j in Universo.Juegos)
            foreach (var o in Universo.Objetos(j, ingles))
            {
                var (antes, motivoAntes) = Antes(soloPkhex, pkhex, o);
                var (img, recurso) = todo.ResolverObjeto(o.IdNativo, o.Contexto, o.Version);
                var despues = Clasificar(img, recurso);
                var motivoDespues = despues == EstadoIcono.SinSprite ? motivos.GetValueOrDefault((j.Id, o.IdNativo)) ?? MotivoSin(o, completo) : despues == EstadoIcono.GenericoMaquina ? MotivoGenerico(o) : "";
                filas.Add(new FilaCobertura(o, antes, motivoAntes, despues, recurso, motivoDespues, porObjeto.GetValueOrDefault((j.Id, o.IdNativo))));
            }
        return filas;
    }

    private static (EstadoIcono, string) Antes(SpriteComposer c, RecursosEmpaquetados pkhex, ObjetoJuego o)
    {
        var mostrado = o.IdMostrado;
        if (HeldItemLumpUtil.GetIsLump(mostrado, o.Contexto).IsLump()) return (EstadoIcono.GenericoMaquina, "MT/MO/DR: PKHeX solo trae un icono común (bitem_tm / bitem_tr), sin distinguir el tipo");
        var img = c.GetItemSpriteBolsa(o.IdNativo, o.Contexto);
        if (img is not null && o.Contexto == EntityContext.Gen1)
            return (EstadoIcono.IconoEquivocado, "Gen. 1: ItemConverter.GetItemDisplay trata el id de la mochila como «tasa de captura» y da OTRO objeto, con su icono equivocado (la Poké Ball saldría como Gran Ball)");
        if (img is not null) return (pkhex.Contiene($"bitem_{mostrado}") ? EstadoIcono.Pkhex : EstadoIcono.PkhexArtwork, "");
        return (EstadoIcono.SinSprite, MotivoSinPkhex(o));
    }

    private static string MotivoSinPkhex(ObjetoJuego o)
        => o.IdMostrado is <= 0 or >= ushort.MaxValue ? $"id sin mapear: el id nativo {o.IdNativo} de {o.Juego.Id} no tiene equivalente en Gen. 4+ (ItemConverter lo deja sin id)"
         : $"PKHeX no trae bitem_{o.IdMostrado} ni aitem_{o.IdMostrado}";

    private static string MotivoGenerico(ObjetoJuego o)
        => Maquinas.Soportado(o.Contexto) ? "no se ha podido averiguar el movimiento de esta máquina" : "Z-A: PKHeX renombra las MT respecto al id y el Core no dice qué movimiento enseña cada una; no se adivina el tipo";

    private static string MotivoSin(ObjetoJuego o, IRecursosSprites r)
    {
        var estilo = ObjetosExtra.Estilo(o.Contexto);
        if (string.IsNullOrWhiteSpace(o.NombreEn)) return "sin nombre en el Core";
        var existe = o.IdMostrado is > 0 and < ushort.MaxValue ? r.Obtener(ObjetosExtra.ClaveObjeto(estilo, o.IdMostrado)) ?? r.Obtener(ObjetosExtra.ClaveObjeto(ObjetosExtra.Otro(estilo), o.IdMostrado)) : null;
        return existe is null ? $"PokeAPI/sprites no trae arte de «{o.NombreEn}» (sin coincidencia de nombre, o sin PNG en la revisión fijada)" : "";
    }

    private static EstadoIcono Clasificar(SpriteImage? img, string? recurso)
    {
        if (img is null || recurso is null) return EstadoIcono.SinSprite;
        if (recurso is "bitem_tm" or "bitem_tr") return EstadoIcono.GenericoMaquina;
        if (recurso.StartsWith("bitem_", StringComparison.Ordinal)) return EstadoIcono.Pkhex;
        if (recurso.StartsWith("aitem_", StringComparison.Ordinal)) return EstadoIcono.PkhexArtwork;
        if (recurso.StartsWith("xj_", StringComparison.Ordinal) || recurso.StartsWith("xjp_", StringComparison.Ordinal) || recurso.StartsWith("xpret_", StringComparison.Ordinal)) return EstadoIcono.ArteJuego;
        if (recurso.StartsWith("xorig_", StringComparison.Ordinal)) return EstadoIcono.ArteJuego;
        if (recurso.StartsWith("xps_", StringComparison.Ordinal)) return EstadoIcono.PokeSprite;
        if (recurso.StartsWith("xmt_", StringComparison.Ordinal) || recurso.StartsWith("xmo_", StringComparison.Ordinal) || recurso.StartsWith("xdr_", StringComparison.Ordinal)) return EstadoIcono.IconoPorTipo;
        return EstadoIcono.Pokeapi;
    }

    private static string Nombre(EstadoIcono e) => e switch
    {
        EstadoIcono.Pkhex => "pkhex", EstadoIcono.PkhexArtwork => "pkhex-artwork", EstadoIcono.GenericoMaquina => "generico-maquina",
        EstadoIcono.IconoPorTipo => "icono-por-tipo", EstadoIcono.Pokeapi => "pokeapi", EstadoIcono.IconoEquivocado => "icono-equivocado", EstadoIcono.ArteJuego => "arte-del-juego", EstadoIcono.PokeSprite => "pokesprite", _ => "sin-sprite",
    };

    /// <summary>TSV con una fila por cada objeto que NO tenía su propio sprite de PKHeX (sin sprite o icono genérico de máquina), con su estado antes y después y el motivo.</summary>
    public static string Detalle(IReadOnlyList<FilaCobertura> filas)
    {
        var sb = new StringBuilder("juego\tcontexto\tid_nativo\tid_mostrado\tnombre_en\tbolsillos\tpartidas_publicas\tantes\tmotivo_antes\tdespues\trecurso_despues\tmotivo_despues\n");
        foreach (var f in filas.Where(f => f.Antes is EstadoIcono.SinSprite or EstadoIcono.GenericoMaquina or EstadoIcono.IconoEquivocado))
        {
            var o = f.Objeto;
            sb.Append(o.Juego.Id).Append('\t').Append(o.Contexto).Append('\t').Append(o.IdNativo).Append('\t').Append(o.IdMostrado is >= 0 and < ushort.MaxValue ? o.IdMostrado.ToString() : "-")
                .Append('\t').Append(o.NombreEn).Append('\t').Append(o.Bolsillos).Append('\t').Append(f.Partidas)
                .Append('\t').Append(Nombre(f.Antes)).Append('\t').Append(f.MotivoAntes)
                .Append('\t').Append(Nombre(f.Despues)).Append('\t').Append(f.RecursoDespues ?? "").Append('\t').Append(f.MotivoDespues).Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>TSV resumen: por juego, cuántos objetos admite la mochila y cuántos hay en cada estado antes y después.</summary>
    public static string Resumen(IReadOnlyList<FilaCobertura> filas)
    {
        var sb = new StringBuilder("juego\tnombre\tobjetos\tantes_pkhex\tantes_artwork\tantes_generico_maquina\tantes_icono_equivocado\tantes_sin_sprite\tdespues_pkhex\tdespues_artwork\tdespues_icono_por_tipo\tdespues_pokeapi\tdespues_arte_juego\tdespues_pokesprite\tdespues_generico_maquina\tdespues_sin_sprite\n");
        foreach (var g in filas.GroupBy(f => f.Objeto.Juego))
        {
            int N(Func<FilaCobertura, EstadoIcono> sel, EstadoIcono e) => g.Count(f => sel(f) == e);
            sb.Append(g.Key.Id).Append('\t').Append(g.Key.Nombre).Append('\t').Append(g.Count())
                .Append('\t').Append(N(f => f.Antes, EstadoIcono.Pkhex)).Append('\t').Append(N(f => f.Antes, EstadoIcono.PkhexArtwork)).Append('\t').Append(N(f => f.Antes, EstadoIcono.GenericoMaquina)).Append('\t').Append(N(f => f.Antes, EstadoIcono.IconoEquivocado)).Append('\t').Append(N(f => f.Antes, EstadoIcono.SinSprite))
                .Append('\t').Append(N(f => f.Despues, EstadoIcono.Pkhex)).Append('\t').Append(N(f => f.Despues, EstadoIcono.PkhexArtwork)).Append('\t').Append(N(f => f.Despues, EstadoIcono.IconoPorTipo)).Append('\t').Append(N(f => f.Despues, EstadoIcono.Pokeapi))
                .Append('\t').Append(N(f => f.Despues, EstadoIcono.ArteJuego)).Append('\t').Append(N(f => f.Despues, EstadoIcono.PokeSprite))
                .Append('\t').Append(N(f => f.Despues, EstadoIcono.GenericoMaquina)).Append('\t').Append(N(f => f.Despues, EstadoIcono.SinSprite)).Append('\n');
        }
        return sb.ToString();
    }
}
