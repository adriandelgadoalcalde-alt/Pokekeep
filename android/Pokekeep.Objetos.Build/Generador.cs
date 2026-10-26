// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Reflection;
using System.Text;
using PKHeX.Core;
using Pokekeep.Sprites;

namespace Pokekeep.Objetos.Build;

/// <summary>Resultado de generar: lo que se escribe y las cifras.</summary>
public sealed record ResultadoObjetos(byte[] Paquete, byte[] Manifiesto, string Procedencia, IReadOnlyList<string> SinResolver, int Claves, int Archivos);

/// <summary>
/// Paquete de objetos adicionales. Claves (ver <see cref="ObjetosExtra"/>): <c>xmt_g5_electric</c> (MT por tipo), <c>xmo_*</c> (MO), <c>xdr_*</c> (DR), <c>xitem_mo_93</c> (objeto sin sprite en PKHeX, por id moderno) y
/// <c>xnat_3_123</c> (objeto de Gen. 1-3 sin id moderno). Los PNG de PokeAPI/sprites se conservan byte a byte.
/// X27: <c>xpret_E_13</c> y <c>xpret_FRLG_13</c> usan los índices PNG y la paleta acreditados por el juego (solo PLTE/CRC, sin escalar). Dos ejecuciones con la misma caché dan bytes idénticos.
/// PKHeX va siempre primero: solo entra un objeto si PKHeX no lo dibuja. Las MT/MO/DR sí entran siempre, porque el icono por tipo gana al genérico.
/// </summary>
public static partial class Generador
{
    public const string IdPaquete = "objetos";
    public const string ArchivoPaquete = "objetos.pkpack";

    /// <summary>Carpetas de PokeAPI por estilo, en orden de preferencia (la raíz es «moderno»; gen5 es el conjunto de Gen. 5; gen8 y gen9 solo traen novedades de esas generaciones).</summary>
    public static IReadOnlyList<string> Carpetas(string estilo) => estilo == "g5" ? ["gen5", ""] : ["", "gen9", "gen8"];

    /// <summary>De dónde sale una imagen: la carpeta (etiqueta), la URL pinneada, el fichero de la caché, la fuente y la revisión de esa fuente.</summary>
    private sealed record Origen(string Carpeta, string Url, string Cache, string Fuente, string Revision);

    /// <summary><paramref name="Evidencia"/> explica por qué ese identificador es ese objeto (nombre, alias revisado, bolsillo, id nativo...). <paramref name="Origenes"/> se prueban en orden; null = las carpetas de PokeAPI del estilo.</summary>
    private sealed record Pedido(string Clave, string Identificador, string Estilo, string Tipo, string Nota, string Evidencia = "", IReadOnlyList<Func<string, Origen>>? Origenes = null);

    private static Origen DePokeApi(string cache, string carpeta, string identificador)
        => new(carpeta.Length == 0 ? "(raiz)" : carpeta, Fuente.UrlSprite(carpeta, identificador), Path.Combine(cache, "sprites", carpeta.Length == 0 ? "raiz" : carpeta, identificador + ".png"), Fuente.FuentePokeApi, Fuente.RevisionSprites);

    public static async Task<ResultadoObjetos> Generar(string cache, string carpetaSprites, bool sinDescargas, string? carpetaSaves = null, HttpClient? http = null)
    {
        Universo.CarpetaSaves = carpetaSaves;
        http ??= new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var csvItems = await Fuente.Obtener(http, Fuente.UrlCsv("items"), Path.Combine(cache, "pokeapi", "items.csv"), sinDescargas) ?? throw new InvalidDataException("Falta items.csv fijado.");
        var csvNombres = await Fuente.Obtener(http, Fuente.UrlCsv("item_names"), Path.Combine(cache, "pokeapi", "item_names.csv"), sinDescargas) ?? throw new InvalidDataException("Falta item_names.csv fijado.");
        var identificadores = Fuente.Filas(csvItems).ToDictionary(v => int.Parse(v[0]), v => v[1]);
        var porNombre = new Dictionary<string, string>(StringComparer.Ordinal);   // nombre normalizado inglés -> identificador
        foreach (var v in Fuente.Filas(csvNombres).Where(v => v[1] == "9").OrderBy(v => int.Parse(v[0])))
            if (identificadores.TryGetValue(int.Parse(v[0]), out var ident)) porNombre.TryAdd(Fuente.Norm(v[2]), ident);
        // Evidencia por id nativo (item_game_indices): no se usa para decidir, pero se descarga para que la caché la traiga y las pruebas comprueben los alias contra ella.
        _ = await Fuente.Obtener(http, Fuente.UrlCsv("item_game_indices"), Path.Combine(cache, "pokeapi", "item_game_indices.csv"), sinDescargas);
        var bolsa = new Dictionary<string, string>(StringComparer.Ordinal);   // «Normalium Z» normalizado -> normalium-z--bag (los cristales Z de la bolsa)
        foreach (var ident in identificadores.Values.Where(i => i.EndsWith("--bag", StringComparison.Ordinal))) bolsa.TryAdd(Fuente.Norm(ident[..^5]), ident);
        var alias = Alias();

        var manifiestoPkhex = Manifiesto.Leer(File.ReadAllBytes(Path.Combine(carpetaSprites, "manifest.json")));
        var pkhex = new RecursosEmpaquetados(manifiestoPkhex, f => File.ReadAllBytes(Path.Combine(carpetaSprites, f)));
        var compositor = new SpriteComposer(pkhex);
        var ingles = GameInfo.GetStrings("en");

        // 1) Objetos que PKHeX no dibuja (ni como bitem ni como aitem) y que no son máquinas: por id moderno, o por id nativo en Gen. 1-3 sin equivalente.
        var pedidos = new SortedDictionary<string, Pedido>(StringComparer.Ordinal);
        var usos = new Dictionary<string, List<(string Juego, int Nativo, string Nombre, Juego J, ObjetoJuego O)>>(StringComparer.Ordinal);
        var pendientes = new List<(Juego J, ObjetoJuego O, string Motivo)>();   // objetos que PKHeX no dibuja y que ninguna fuente de PokeAPI resuelve (todavía)
        void Sin(Juego j, ObjetoJuego o, string motivo) => pendientes.Add((j, o, motivo));
        var evidencias = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var j in Universo.Juegos)
            foreach (var o in Universo.Objetos(j, ingles))
            {
                if (Maquinas.De(o.Version, o.Contexto, o.IdNativo) is not null) continue;
                if (HeldItemLumpUtil.GetIsLump(o.IdMostrado, o.Contexto).IsLump()) continue;
                if (compositor.ResolverObjeto(o.IdNativo, o.Contexto, o.Version).Imagen is not null) continue;   // PKHeX (y su equivalencia por nombre de ObjetosAntiguos) ya lo dibuja
                var estilo = ObjetosExtra.Estilo(o.Contexto);
                var efectivo = ObjetosAntiguos.IdEfectivo(o.Contexto, o.Version, o.IdNativo);
                string? clave = efectivo > 0 ? ObjetosExtra.ClaveObjeto(estilo, efectivo)
                    : o.Contexto <= EntityContext.Gen3 ? ObjetosExtra.ClaveNativo(o.Contexto, o.IdNativo) : null;
                if (clave is null) { Sin(j, o, "id sin equivalente moderno y no es de Gen. 1-3"); continue; }
                // El nombre moderno del id (el de la lista que PKHeX comparte entre Gen. 5-8) manda sobre el del juego: una misma clave sale de varios juegos y así todos piden el mismo identificador.
                var nombreModerno = efectivo > 0 && efectivo < ingles.Item.Count ? ingles.Item[efectivo] : "";
                var cristalZ = IdentificarCristalZ(o, bolsa);
                var ident = cristalZ ?? Identificar(nombreModerno, alias, porNombre) ?? Identificar(o.NombreEn, alias, porNombre);
                if (ident is null) { Sin(j, o, "PokeAPI no tiene ningún objeto con ese nombre"); continue; }
                if (cristalZ is not null) evidencias[clave] = "cristal Z del bolsillo ZCrystals: el nombre del Core termina en « [Z]» y PokeAPI lo distingue de la versión equipada con el sufijo --bag";
                else if (alias.ContainsKey(o.NombreEn) || alias.ContainsKey(nombreModerno)) evidencias[clave] = "alias revisado (alias-nombres.tsv)";
                if (!usos.TryGetValue(clave, out var lu)) usos[clave] = lu = [];
                lu.Add((j.Id, o.IdNativo, o.NombreEn, j, o));
                if (pedidos.TryGetValue(clave, out var previo))
                {
                    if (previo.Identificador != ident) Sin(j, o, $"conflicto: la clave {clave} ya es «{previo.Identificador}» y aquí saldría «{ident}»");
                    continue;
                }
                pedidos[clave] = new Pedido(clave, ident, estilo, clave.StartsWith("xnat_", StringComparison.Ordinal) ? "nativo" : "objeto", o.NombreEn, evidencias.GetValueOrDefault(clave) ?? "nombre inglés del Core = nombre inglés de PokeAPI");
            }

        // 1b) Arte PROPIO de Gen. 3 (carpeta gen3 de PokeAPI: solo cinco iconos, distintos de los modernos): gana a PKHeX en Rubí/Zafiro, Esmeralda y Rojo Fuego/Verde Hoja (por id nativo de cada juego).
        var gen3 = new Dictionary<string, string>(StringComparer.Ordinal);   // identificador -> existe en gen3/
        var candidatosGen3 = new List<(Juego Juego, int Nativo, string Nombre, string Ident, ObjetoJuego Objeto)>();
        foreach (var j in Universo.Juegos.Where(j => j.Id is "RS" or "E" or "FRLG"))
            foreach (var o in Universo.Objetos(j, ingles))
            {
                if (Maquinas.De(o.Version, o.Contexto, o.IdNativo) is not null) continue;
                var efectivo = ObjetosAntiguos.IdEfectivo(o.Contexto, o.Version, o.IdNativo);
                var nombreModerno = efectivo > 0 && efectivo < ingles.Item.Count ? ingles.Item[efectivo] : "";
                var ident = Identificar(nombreModerno, alias, porNombre) ?? Identificar(o.NombreEn, alias, porNombre);
                if (ident is not null) candidatosGen3.Add((j, o.IdNativo, o.NombreEn, ArchivosGen3.GetValueOrDefault(ident, ident), o));
            }
        await Parallel.ForEachAsync(candidatosGen3.Select(c => c.Ident).Distinct().ToList(), new ParallelOptions { MaxDegreeOfParallelism = 6 }, async (ident, _) =>
        {
            var o = DePokeApi(cache, "gen3", ident);
            var b = await Fuente.Obtener(http, o.Url, o.Cache, sinDescargas);
            if (b is not null) lock (gen3) gen3[ident] = ident;
        });
        foreach (var (j, nativo, nombre, ident, objeto) in candidatosGen3.Where(c => gen3.ContainsKey(c.Ident)))
        {
            var clave = ObjetosExtra.ClaveJuego(j.Id, nativo, prioritaria: true);
            pedidos[clave] = new Pedido(clave, ident, "gen3", "juego", nombre, "arte de Gen. 3 de PokeAPI (gen3/) para un juego de Gen. 3; nombre inglés del Core = nombre de PokeAPI", [id => DePokeApi(cache, "gen3", id)]);
            if (!usos.TryGetValue(clave, out var lu)) usos[clave] = lu = [];
            lu.Add((j.Id, nativo, nombre, j, objeto));
        }

        // 2) MT/MO/DR por tipo. MT y MO en los dos estilos (gen5 solo trae 17 tipos: sin Hada, que Gen. 1-5 no tiene); DR solo en el moderno y con el disco de MT (PokeAPI no trae arte de DR).
        foreach (var t in ObjetosExtra.Tipos)
        {
            pedidos[$"xmt_g5_{t}"] = new Pedido($"xmt_g5_{t}", $"tm-{t}", "g5", "maquina", "MT");
            pedidos[$"xmt_mo_{t}"] = new Pedido($"xmt_mo_{t}", $"tm-{t}", "mo", "maquina", "MT");
            pedidos[$"xmo_g5_{t}"] = new Pedido($"xmo_g5_{t}", $"hm-{t}", "g5", "maquina", "MO");
            pedidos[$"xmo_mo_{t}"] = new Pedido($"xmo_mo_{t}", $"hm-{t}", "mo", "maquina", "MO");
            pedidos[$"xdr_mo_{t}"] = new Pedido($"xdr_mo_{t}", $"tm-{t}", "mo", "maquina", "DR (aproximación: PokeAPI no trae arte de DR; se usa el disco de MT del tipo)");
        }

        // 3) Descargar (con caché) todo lo pedido, probando las carpetas del estilo en orden.
        var hallados = new Dictionary<string, (Origen Origen, string Identificador, byte[] Png)>(StringComparer.Ordinal);
        await Parallel.ForEachAsync(pedidos.Values.ToList(), new ParallelOptions { MaxDegreeOfParallelism = 6 }, async (p, _) =>
        {
            var origenes = p.Origenes is not null ? p.Origenes.Select(f => f(p.Identificador)).ToList()
                : (p.Tipo == "maquina" ? MaquinaCarpetas(p) : Carpetas(p.Estilo)).Select(c => DePokeApi(cache, c, p.Identificador)).ToList();
            foreach (var origen in origenes)
            {
                var b = await Fuente.Obtener(http, origen.Url, origen.Cache, sinDescargas);
                if (b is null) continue;
                if (!EsPng(b)) throw new InvalidDataException($"{origen.Carpeta}/{p.Identificador}.png no es un PNG.");
                lock (hallados) hallados[p.Clave] = (origen, p.Identificador, b);
                return;
            }
        });
        foreach (var p in pedidos.Values.Where(p => p.Tipo != "maquina" && !hallados.ContainsKey(p.Clave)))
            foreach (var u in usos[p.Clave])
                Sin(u.J, u.O, $"PokeAPI conoce «{p.Identificador}» pero PokeAPI/sprites no trae su PNG en la revisión fijada (carpetas probadas: {string.Join(", ", (p.Origenes is not null ? ["gen3"] : Carpetas(p.Estilo)).Select(c => c.Length == 0 ? "raíz" : c))})");

        // 3b) PokéSprite (revisión fijada) para lo que PokeAPI no resuelve: solo entra si coinciden id, nombre y PNG (ver PokeSprite.Buscar).
        var indicesPorGen = IndicesPokeApi(Fuente.Filas(await Fuente.Obtener(http, Fuente.UrlCsv("item_game_indices"), Path.Combine(cache, "pokeapi", "item_game_indices.csv"), sinDescargas) ?? throw new InvalidDataException("Falta item_game_indices.csv fijado.")), identificadores);
        var rescatados = new HashSet<(string, int)>();
        // 3a) Arte PROPIO del juego de otras fuentes verificadas (hoy, Escarlata/Púrpura): va antes que PokéSprite porque es el dibujo del propio juego.
        foreach (var h in await OtrasFuentes.EscarlataPurpura(http, cache, sinDescargas, pendientes.Select(x => (x.J, x.O)).ToList()))
        {
            var clave = ObjetosExtra.ClaveJuego(h.J.Id, h.O.IdNativo, prioritaria: false);
            pedidos[clave] = new Pedido(clave, h.Archivo, "juego", "juego", h.O.NombreEn, h.Evidencia, []);
            hallados[clave] = (new Origen(h.Carpeta, h.Url, "", h.Fuente, h.Revision), h.Archivo, h.Png);
            usos[clave] = [(h.J.Id, h.O.IdNativo, h.O.NombreEn, h.J, h.O)];
            rescatados.Add((h.J.Id, h.O.IdNativo));
        }
        var dinamax = await PokeSprite.CristalesDinamax(http, cache, sinDescargas, pendientes.Where(x => !rescatados.Contains((x.J.Id, x.O.IdNativo))).Select(x => (x.J, x.O)).ToList());
        var (aceptados, rechazos) = await PokeSprite.Buscar(http, cache, sinDescargas, pendientes.Where(x => !rescatados.Contains((x.J.Id, x.O.IdNativo)) && !dinamax.Any(d => d.J.Id == x.J.Id && d.O.IdNativo == x.O.IdNativo)).Select(x => (x.J, x.O, Efectivo: ObjetosAntiguos.IdEfectivo(x.O.Contexto, x.O.Version, x.O.IdNativo))).ToList(), indicesPorGen);
        aceptados.AddRange(dinamax);
        foreach (var a in aceptados)
        {
            var clave = ObjetosExtra.ClavePokeSprite(a.J.Id, a.O.IdNativo);
            pedidos[clave] = new Pedido(clave, a.Ruta, "ps", "pokesprite", a.O.NombreEn, a.Evidencia, []);
            hallados[clave] = (new Origen("items", Fuente.UrlPokeSprite(a.Ruta), "", Fuente.FuentePokeSprite, Fuente.RevisionPokeSprite), a.Ruta, a.Png);
            usos[clave] = [(a.J.Id, a.O.IdNativo, a.O.NombreEn, a.J, a.O)];
            rescatados.Add((a.J.Id, a.O.IdNativo));
        }
        var sinResolver = pendientes.Where(x => !rescatados.Contains((x.J.Id, x.O.IdNativo)))
            .Select(x => Fila(x.J.Id, x.O.IdNativo, x.O.NombreEn, rechazos.TryGetValue((x.J.Id, x.O.IdNativo), out var r) ? x.Motivo + " | " + r : x.Motivo)).ToList();

        // X27: arte acreditado por el id y el par gráfico/paleta del propio juego, incluso MT/MO.
        foreach (var h in await Pret.Buscar(http, cache, sinDescargas, Universo.Juegos.Where(j => j.Id is "E" or "FRLG")
            .SelectMany(j => Universo.Objetos(j, ingles).Select(o => (j, o))).ToList()))
        {
            var clave = ObjetosExtra.ClavePret(h.J.Id, h.O.IdNativo);
            pedidos[clave] = new Pedido(clave, h.Archivo, "pret-gen3", "juego", h.O.NombreEn, h.Evidencia, []);
            hallados[clave] = (new Origen(h.Carpeta, h.Url, "", h.Fuente, h.Revision), h.Archivo, h.Png);
            usos[clave] = [(h.J.Id, h.O.IdNativo, h.O.NombreEn, h.J, h.O)];
        }

        // X28: el índice local contiene exclusivamente iconos y metadatos públicos/genéricos.
        foreach (var h in Originales.Buscar(carpetaSprites, Universo.Juegos.SelectMany(j => Universo.Objetos(j, ingles).Select(o => (j, o))).ToList()))
        {
            var clave = ObjetosExtra.ClaveOriginal(h.J.Id, h.O.IdNativo);
            pedidos[clave] = new Pedido(clave, h.Archivo, "original", "juego", h.O.NombreEn, h.Evidencia, []);
            hallados[clave] = (new Origen(h.Carpeta, h.Url, "", h.Fuente, h.Revision), h.Archivo, h.Png);
            usos[clave] = [(h.J.Id, h.O.IdNativo, h.O.NombreEn, h.J, h.O)];
        }

        // 4) Paquete: un PNG por contenido distinto; las claves que comparten contenido apuntan al mismo sitio.
        using var paquete = new MemoryStream();
        var sitios = new Dictionary<string, (long Desp, int Len)>(StringComparer.Ordinal);
        var recursos = new Dictionary<string, EntradaRecurso>(StringComparer.Ordinal);
        var tsv = new StringBuilder("clave\ttipo\tnombre_en\tidentificador_pokeapi\tcarpeta\tancho\talto\tbytes\tsha256\tjuegos\trevision_sprites\trevision_datos\turl\tfuente\tevidencia\tsha256_fuente\tbytes_fuente\n");
        // Release: cada PNG distinto se optimiza sin perdida antes de empaquetarlo (mismos pixeles RGBA). `bytes` y `sha256` de la procedencia son los del PNG DEL PAQUETE; los del PNG de origen tal como se
        // descargo van al final (sha256_fuente, bytes_fuente) para poder volver a comprobarlo contra la fuente.
        var optimizados = OptimizadorPng.Optimizar(hallados.Values.Select(h => h.Png));
        foreach (var (clave, h) in hallados.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            var shaFuente = Fuente.Sha(h.Png);
            var png = optimizados[shaFuente];
            var sha = Fuente.Sha(png);
            var (ancho, alto) = Dimensiones(png);
            if (!sitios.TryGetValue(sha, out var sitio)) { sitio = (paquete.Position, png.Length); paquete.Write(png); sitios[sha] = sitio; }
            recursos[clave] = new EntradaRecurso(0, sitio.Desp, sitio.Len, ancho, alto);
            var p = pedidos[clave];
            tsv.Append(clave).Append('\t').Append(p.Tipo).Append('\t').Append(p.Nota).Append('\t').Append(h.Identificador).Append('\t').Append(h.Origen.Carpeta)
                .Append('\t').Append(ancho).Append('\t').Append(alto).Append('\t').Append(png.Length).Append('\t').Append(sha)
                .Append('\t').Append(string.Join(",", usos.GetValueOrDefault(clave)?.Select(u => u.Juego).Distinct() ?? [])).Append('\t').Append(h.Origen.Revision).Append('\t').Append(Fuente.RevisionDatos).Append('\t').Append(h.Origen.Url).Append('\t').Append(h.Origen.Fuente).Append('\t').Append(p.Evidencia).Append('\t').Append(shaFuente).Append('\t').Append(h.Png.Length).Append('\n');
        }
        var bytesPaquete = paquete.ToArray();
        var manifiesto = new Manifiesto
        {
            Origen = string.Join("; ", hallados.Values.Select(h => $"{h.Origen.Fuente}@{h.Origen.Revision}").Distinct().Order())
                + $"; PokeAPI/pokeapi items.csv+item_names.csv@{Fuente.RevisionDatos}; arte (c) The Pokemon Company",
            Paquetes = [new PaqueteInfo(IdPaquete, ArchivoPaquete, bytesPaquete.LongLength, Manifiesto.Sha256Hex(bytesPaquete), recursos.Count)],
            Recursos = recursos,
        }.Escribir();
        return new ResultadoObjetos(bytesPaquete, manifiesto, tsv.ToString(), sinResolver, recursos.Count, sitios.Count);
    }

    /// <summary>Una fila del informe de objetos sin resolver: juego, id nativo, nombre inglés y motivo.</summary>
    public static string Fila(string juego, int nativo, string nombre, string motivo) => string.Join("\t", juego, nativo, nombre, motivo);

    /// <summary>Identificador de PokeAPI de un nombre inglés de PKHeX: alias a mano, nombre exacto y, si no, el nombre sin su calificativo final entre paréntesis («Card Key (HG/SS)»).</summary>
    private static string? Identificar(string nombre, Dictionary<string, string> alias, Dictionary<string, string> porNombre)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return null;
        if (alias.TryGetValue(nombre, out var a)) return a;
        if (porNombre.TryGetValue(Fuente.Norm(nombre), out var i)) return i;
        var p = nombre.LastIndexOf(" (", StringComparison.Ordinal);
        if (p <= 0 || !nombre.EndsWith(')')) return null;
        // Los objetos propios de los juegos de GameCube (p. ej. «Card Key (COLO)») no son el objeto de GBA que se llama igual: no se les presta su arte.
        if (nombre.EndsWith("(COLO)", StringComparison.Ordinal) || nombre.EndsWith("(XD)", StringComparison.Ordinal)) return null;
        return porNombre.GetValueOrDefault(Fuente.Norm(nombre[..p]));
    }

    /// <summary>
    /// Los cristales Z de la BOLSA (bolsillo ZCrystals): el Core los llama «Normalium Z [Z]» y PokeAPI distingue el objeto de la bolsa (<c>normalium-z--bag</c>) del equipado (<c>--held</c>), con el mismo nombre inglés.
    /// El bolsillo manda: el cristal del bolsillo de la bolsa es siempre el de la bolsa. Null si no lo es o PokeAPI no tiene ese cristal.
    /// </summary>
    private static string? IdentificarCristalZ(ObjetoJuego o, Dictionary<string, string> bolsa)
    {
        const string Sufijo = " [Z]";
        if (!o.Bolsillos.Split('/').Contains("ZCrystals") || !o.NombreEn.EndsWith(Sufijo, StringComparison.Ordinal)) return null;
        return bolsa.GetValueOrDefault(Fuente.Norm(o.NombreEn[..^Sufijo.Length]));
    }

    /// <summary>Identificador de PokeAPI -> nombre del archivo de <c>gen3/</c> cuando difiere: el «Itemfinder» de Gen. 3 es el objeto que PokeAPI llama <c>dowsing-machine</c> (renombrado en Gen. 4; ver alias-modernos.tsv).</summary>
    private static readonly Dictionary<string, string> ArchivosGen3 = new(StringComparer.Ordinal) { ["dowsing-machine"] = "itemfinder" };

    /// <summary>Las MT/MO de estilo g5 solo de gen5; las del moderno de la raíz (los discos de tipo están ahí).</summary>
    private static IEnumerable<string> MaquinaCarpetas(Pedido p) => p.Estilo == "g5" ? ["gen5"] : [""];

    public static bool EsPng(ReadOnlySpan<byte> b) => b.Length > 33 && b[0] == 0x89 && b[1] == 'P' && b[2] == 'N' && b[3] == 'G' && b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A;

    public static (int Ancho, int Alto) Dimensiones(ReadOnlySpan<byte> png)
        => ((png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19], (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23]);

    /// <summary>Alias a mano (nombre de PKHeX en inglés -> identificador de PokeAPI), solo para los que el cruce por nombre no resuelve y se han comprobado a ojo.</summary>
    public static Dictionary<string, string> Alias()
    {
        using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("alias-nombres.tsv")!;
        using var r = new StreamReader(s, Encoding.UTF8);
        var d = new Dictionary<string, string>(StringComparer.Ordinal);
        _ = r.ReadLine();
        while (r.ReadLine() is { } l) { var v = l.Split('\t'); if (v.Length >= 2 && v[0].Length > 0) d[v[0]] = v[1]; }
        return d;
    }

    public static void Escribir(ResultadoObjetos r, string salida, string? tsvProcedencia, string? tsvSinResolver)
    {
        Directory.CreateDirectory(salida);
        File.WriteAllBytes(Path.Combine(salida, ArchivoPaquete), r.Paquete);
        File.WriteAllBytes(Path.Combine(salida, "manifest.json"), r.Manifiesto);
        var sinBom = new UTF8Encoding(false);
        if (tsvProcedencia is not null) File.WriteAllText(tsvProcedencia, r.Procedencia, sinBom);
        if (tsvSinResolver is not null) File.WriteAllText(tsvSinResolver, "juego\tid_nativo\tnombre\tmotivo\n" + string.Concat(r.SinResolver.Select(l => l + "\n")), sinBom);
    }
}
