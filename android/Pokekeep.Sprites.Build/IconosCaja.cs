// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Text;
using System.Text.Json;
using Pokekeep.Sprites;
using SkiaSharp;

/// <summary>
/// Generador de los iconos de caja de cada juego (X53): lee la entrega X49 (<c>indice.json</c> + un PNG por icono), VERIFICA todos sus ficheros contra su <c>SHA256SUMS</c> y escribe un paquete por juego integrado
/// (<c>caja-&lt;juego&gt;.pkpack</c>, PNG sin perdida concatenados) mas <c>manifest-caja.json</c> (formato <see cref="Manifiesto"/>) y <c>procedencia-caja.tsv</c> (huella de cada icono; no viaja en el APK). Determinista.
/// Reglas: solo los siete juegos de <see cref="SpritesDeJuego.JuegosCaja"/> (cobertura completa); sin Gigamax ni el comodin «?»; el hembra con grafico propio es <c>_h</c> y el resto (sin sexo, sexo 2, macho) es la imagen base;
/// el huevo es la especie 0 (forma 1 = Manaphy); Espada/Escudo vienen ampliados 2x exactos y se guardan a su tamano real (se comprueba bloque a bloque); si una especie no trae su forma 0 se usa como tal la primera forma del juego.
/// </summary>
internal static class IconosCaja
{
    private sealed record Icono(string Juego, int Especie, int Forma, string Genero, bool Variocolor, bool Huevo, int FormaNativa, string Archivo, int Ancho, int Alto, string PixelesSha);

    private static readonly Dictionary<string, string> JuegosEntrega = SpritesDeJuego.JuegosCaja.ToDictionary(j => j, j => j, StringComparer.OrdinalIgnoreCase);

    /// <summary>Lo que produce una pasada del generador: los paquetes (bytes), sus entradas de manifiesto y la huella de procedencia. Quien llama decide el manifiesto en el que van.</summary>
    internal sealed record Resultado(SortedDictionary<string, byte[]> Paquetes, List<PaqueteInfo> Info, Dictionary<string, EntradaRecurso> Entradas, string Tsv, List<string> Recortados, List<string> Avisos);

    /// <summary>Los juegos de la entrega X49 (todos menos Let's Go, que tiene su propia entrega, X57).</summary>
    internal static readonly string[] JuegosX49 = [.. SpritesDeJuego.JuegosCaja.Where(j => j != SpritesDeJuego.Lgpe)];

    /// <summary>PNG sin perdida o, para las ilustraciones grandes y los renders, WebP sin perdida (~30 % menos).</summary>
    internal static bool UsaWebp(string juego) => juego is "pla" or "sv" or "za" or "lgpe";

    public static int Ejecutar(string modo, string entrega, string salida)
    {
        var r = Construir(entrega, JuegosX49, 0);
        var salidas = new SortedDictionary<string, byte[]>(r.Paquetes, StringComparer.Ordinal);
        var manifiesto = new Manifiesto { Origen = "Iconos de caja exactos de cada juego (entrega X49, volcados del propietario)", Recortados = r.Recortados, Paquetes = r.Info, Recursos = r.Entradas };
        salidas["manifest-caja.json"] = manifiesto.Escribir();
        salidas["procedencia-caja.tsv"] = new UTF8Encoding(false).GetBytes(r.Tsv);
        return Escribir(modo == "generar-caja", salida, salidas, $"{r.Entradas.Count} iconos de caja");
    }

    internal static int Escribir(bool generar, string salida, SortedDictionary<string, byte[]> salidas, string que)
    {
        var fallos = 0;
        if (generar)
        {
            Directory.CreateDirectory(salida);
            foreach (var (n, b) in salidas) File.WriteAllBytes(Path.Combine(salida, n), b);
            return 0;
        }
        foreach (var (n, b) in salidas)
        {
            var f = Path.Combine(salida, n);
            if (!File.Exists(f)) { Console.Error.WriteLine("Falta " + n); fallos++; }
            else if (!File.ReadAllBytes(f).AsSpan().SequenceEqual(b)) { Console.Error.WriteLine("Distinto de lo regenerado: " + n); fallos++; }
        }
        Console.WriteLine(fallos == 0 ? $"OK: {que} identicos a lo regenerado de la entrega" : $"FALLOS: {fallos}");
        return fallos == 0 ? 0 : 1;
    }

    internal static Resultado Construir(string entrega, IReadOnlyList<string> juegosModo, int primerPaquete, bool forzarPng = false)
    {
        var sumas = Juegos.LeerSumas(entrega);
        var fallos = 0;
        foreach (var (ruta, huella) in sumas)
        {
            var f = Path.Combine(entrega, ruta.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(f)) { Console.Error.WriteLine($"Falta {ruta}"); fallos++; continue; }
            if (!string.Equals(Manifiesto.Sha256Hex(File.ReadAllBytes(f)), huella, StringComparison.OrdinalIgnoreCase)) { Console.Error.WriteLine($"SHA-256 distinto: {ruta}"); fallos++; }
        }
        if (fallos > 0) throw new InvalidDataException($"La entrega no coincide con su SHA256SUMS ({fallos} fallos): no se genera nada.");
        Console.WriteLine($"Entrega verificada: {sumas.Count} ficheros con su SHA-256.");

        var cubiertos = sumas.Select(x => x.Ruta).ToHashSet(StringComparer.Ordinal);
        var raiz = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(entrega, "indice.json"))).RootElement;
        var todos = new List<Icono>();
        foreach (var i in raiz.GetProperty("iconos").EnumerateArray())
        {
            if (!JuegosEntrega.TryGetValue(i.GetProperty("juego").GetString()!, out var juego) || !juegosModo.Contains(juego)) continue;
            if (i.TryGetProperty("gigamax", out var gm) && gm.GetBoolean()) continue;   // el icono Gigamax no sale en la caja de la consola
            var huevo = i.TryGetProperty("huevo", out var he) && he.GetBoolean() || i.GetProperty("archivo").GetString()!.Contains("huevo", StringComparison.Ordinal);
            var especie = i.GetProperty("especie").GetInt32();
            if (!cubiertos.Contains(i.GetProperty("archivo").GetString()!)) throw new InvalidDataException("Un icono del indice no esta en SHA256SUMS: " + i.GetProperty("archivo").GetString());
            if (especie == 0 && !huevo) continue;                                         // el comodin «?»: sin Pokemon, no es un icono de caja
            var nativa = i.TryGetProperty("forma_nativa", out var fn) ? fn.GetInt32() : i.GetProperty("forma").GetInt32();
            todos.Add(new Icono(juego, huevo ? 0 : especie, huevo ? (especie == (int)PKHeX.Core.Species.Manaphy ? 1 : 0) : i.GetProperty("forma").GetInt32(), i.GetProperty("genero").GetString()!, i.GetProperty("variocolor").GetBoolean(), huevo,
                nativa, i.GetProperty("archivo").GetString()!, i.GetProperty("ancho").GetInt32(), i.GetProperty("alto").GetInt32(), i.GetProperty("pixeles_sha256").GetString()!));
        }

        // Una imagen por (juego, especie, forma, variocolor, hembra): base = sin sexo, «2» (sin sexo en Diamante Brillante) o macho; hembra = «f». Si solo hay hembra, es la base.
        var elegidos = new SortedDictionary<string, (Icono Icono, byte[] Pixeles)>(StringComparer.Ordinal);
        var alias = new List<string>();
        var distintos = new List<string>();
        var reducidos = 0;
        foreach (var grupo in todos.GroupBy(i => (i.Juego, i.Especie, i.Forma, i.Variocolor)))
        {
            var (juego, especie, forma, vario) = grupo.Key;
            Icono Elegir(IEnumerable<Icono> c) => c.OrderBy(i => i.Genero switch { "" => 0, "2" => 1, "m" => 2, _ => 3 }).ThenBy(i => i.FormaNativa != i.Forma).ThenBy(i => i.FormaNativa).ThenBy(i => i.Archivo, StringComparer.Ordinal).First();
            var machos = grupo.Where(i => i.Genero != "f").ToList();
            var hembras = grupo.Where(i => i.Genero == "f").ToList();
            var @base = machos.Count > 0 ? Elegir(machos) : Elegir(hembras);
            Registrar(@base, hembra: false);
            if (machos.Count > 0 && hembras.Count > 0) Registrar(Elegir(hembras), hembra: true);
            var candidatos = (machos.Count > 0 ? machos : hembras);
            if (candidatos.Select(i => i.PixelesSha).Distinct().Count() > 1) distintos.Add($"{juego} {especie}/{forma}{(vario ? " variocolor" : "")}: {candidatos.Select(i => i.PixelesSha).Distinct().Count()} imagenes distintas con la misma clave, se usa la de forma nativa {@base.FormaNativa}");
            void Registrar(Icono ic, bool hembra)
            {
                var img = Juegos.Decodificar(File.ReadAllBytes(Path.Combine(entrega, ic.Archivo.Replace('/', Path.DirectorySeparatorChar))));
                if (img.Width != ic.Ancho || img.Height != ic.Alto) throw new InvalidDataException($"{ic.Archivo}: mide {img.Width}x{img.Height} y el indice dice {ic.Ancho}x{ic.Alto}.");
                if (ic.Juego == "swsh") { img = Reducir2x(img, ic.Archivo); reducidos++; }
                elegidos[SpritesDeJuego.Nombre(SpritesDeJuego.ClaseCaja, juego, especie, forma, hembra, vario)] = (ic, img.Pixels);
                sizes[SpritesDeJuego.Nombre(SpritesDeJuego.ClaseCaja, juego, especie, forma, hembra, vario)] = (img.Width, img.Height);
            }
        }
        // Una especie sin su forma 0 (Gourgeist en Espada/Escudo: sus cuatro tamanos comparten icono) usa como forma 0 la primera forma del juego.
        foreach (var grupo in elegidos.Keys.Select(Analizar).GroupBy(n => (n.Juego, n.Especie, n.Hembra, n.Vario)).ToList())
        {
            if (grupo.Any(n => n.Forma == 0)) continue;
            var primero = grupo.OrderBy(n => n.Forma).First();
            elegidos[SpritesDeJuego.Nombre(SpritesDeJuego.ClaseCaja, primero.Juego, primero.Especie, 0, primero.Hembra, primero.Vario)] = elegidos[primero.Nombre];
            sizes[SpritesDeJuego.Nombre(SpritesDeJuego.ClaseCaja, primero.Juego, primero.Especie, 0, primero.Hembra, primero.Vario)] = sizes[primero.Nombre];
            alias.Add($"{primero.Juego} {primero.Especie}: forma 0 = forma {primero.Forma}{(primero.Hembra ? " hembra" : "")}{(primero.Vario ? " variocolor" : "")}");
        }

        CoberturaVariocolorCaja.Validar(elegidos.Keys);
        if (!forzarPng) PrepararWebp(elegidos.Where(kv => UsaWebp(Analizar(kv.Key).Juego)).Select(kv => new SpriteImage(sizes[kv.Key].Item1, sizes[kv.Key].Item2, kv.Value.Pixeles)));
        var salidas = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
        var paquetes = new List<PaqueteInfo>();
        var entradas = new Dictionary<string, EntradaRecurso>(StringComparer.Ordinal);
        var tsv = new StringBuilder("recurso\tpaquete\tficheroEntrega\tancho\talto\tsha256Entrega\n");
        foreach (var juego in juegosModo)
        {
            var delJuego = elegidos.Where(kv => Analizar(kv.Key).Juego == juego).ToList();
            if (delJuego.Count == 0) throw new InvalidDataException("El indice no trae ningun icono de " + juego);
            var id = SpritesDeJuego.IdPaquete(SpritesDeJuego.ClaseCaja, juego);
            using var ms = new MemoryStream();
            var yaEscritos = new Dictionary<string, EntradaRecurso>(StringComparer.Ordinal);   // el alias de una forma 0 comparte los bytes de la imagen original
            foreach (var (nombre, (ic, pixeles)) in delJuego)
            {
                var (w, h) = sizes[nombre];
                if (yaEscritos.TryGetValue(ic.Archivo + "|" + pixeles.Length + "|" + Manifiesto.Sha256Hex(pixeles), out var previa)) { entradas[nombre] = previa; tsv.Append(nombre).Append('\t').Append(id).Append('\t').Append(ic.Archivo).Append('\t').Append(w).Append('\t').Append(h).Append('\t').Append(ic.PixelesSha).Append('\n'); continue; }
                var img = new SpriteImage(w, h, pixeles);
                var png = Codificar(juego, img, ic.Archivo, forzarPng);
                var entrada = new EntradaRecurso(primerPaquete + paquetes.Count, ms.Position, png.Length, w, h);
                entradas[nombre] = entrada;
                yaEscritos[ic.Archivo + "|" + pixeles.Length + "|" + Manifiesto.Sha256Hex(pixeles)] = entrada;
                ms.Write(png);
                tsv.Append(nombre).Append('\t').Append(id).Append('\t').Append(ic.Archivo).Append('\t').Append(w).Append('\t').Append(h).Append('\t').Append(ic.PixelesSha).Append('\n');
            }
            var bytes = ms.ToArray();
            var archivo = id + ".pkpack";
            salidas[archivo] = bytes;
            paquetes.Add(new PaqueteInfo(id, archivo, bytes.Length, Manifiesto.Sha256Hex(bytes), delJuego.Count));
            Console.WriteLine($"{archivo,-22} {delJuego.Count,5} recursos {bytes.Length,10:N0} bytes");
        }
        // Propiedad del conjunto: mas de la mitad de los iconos de Pokemon (sin huevo ni variocolor) del juego tocan el borde del lienzo con un tramo opaco largo.
        var recortados = juegosModo.Where(j =>
        {
            var l = elegidos.Where(kv => { var n = Analizar(kv.Key); return n.Juego == j && n.Especie != 0 && !n.Vario; }).Select(kv => Manifiesto.IconoRecortadoPorElLienzo(new SpriteImage(sizes[kv.Key].Item1, sizes[kv.Key].Item2, kv.Value.Pixeles))).ToList();
            return l.Count > 0 && l.Count(x => x) * 2 > l.Count;
        }).ToList();
        Console.WriteLine("Recortados por el lienzo: " + (recortados.Count == 0 ? "ninguno" : string.Join(", ", recortados)));
        Console.WriteLine($"{entradas.Count} recursos en {paquetes.Count} paquetes, {paquetes.Sum(p => p.Bytes):N0} bytes en total; {reducidos} iconos de Espada/Escudo reducidos de 2x a su tamano real");
        var avisos = new List<string>();
        foreach (var a in alias) { Console.WriteLine("ALIAS " + a); avisos.Add("ALIAS " + a); }
        foreach (var d in distintos) { Console.WriteLine("AMBIGUO " + d); avisos.Add("AMBIGUO " + d); }
        return new Resultado(salidas, paquetes, entradas, tsv.ToString(), recortados, avisos);
    }

    /// <summary>Formato de cada juego: PNG (indexado si caben 256 colores) y, para los retratos de PLA y SV, WebP SIN PERDIDA (~30 % menos). Se comprueba que el lector gestionado devuelve exactamente los mismos pixeles.</summary>
    private static byte[] Codificar(string juego, SpriteImage img, string origen, bool forzarPng)
    {
        if (!forzarPng && UsaWebp(juego))
        {
            var webp = WebpPorPixeles[Manifiesto.Sha256Hex(img.Pixels) + $"{img.Width}x{img.Height}"];
            var vuelta = WebpLite.Decodificar(webp);
            if (vuelta.Width != img.Width || vuelta.Height != img.Height || !vuelta.Pixels.AsSpan().SequenceEqual(img.Pixels)) throw new InvalidDataException($"{origen}: el WebP no devuelve los mismos pixeles.");
            return webp;
        }
        var png = Juegos.CodificarIndexado(img) ?? PngLite.Codificar(img);
        if (!PngLite.Decodificar(png).Pixels.AsSpan().SequenceEqual(img.Pixels)) throw new InvalidDataException($"{origen}: el PNG no devuelve los mismos pixeles.");
        return png;
    }

    /// <summary>El WebP sin perdida de una imagen ya preparada con <see cref="PrepararWebp"/>.</summary>
    internal static byte[] WebpDe(SpriteImage img) => WebpPorPixeles[Manifiesto.Sha256Hex(img.Pixels) + $"{img.Width}x{img.Height}"];

    private static readonly Dictionary<string, byte[]> WebpPorPixeles = new(StringComparer.Ordinal);

    /// <summary>Esfuerzo de libwebp (0 a 6). 6 = el maximo, el de todos los paquetes anteriores; X71 usa 4 (12.500 imagenes: ~0,17 s cada una frente a ~1,2 s, a cambio de ~3 % de tamano) y deja el 6 como defecto.</summary>
    internal static int MetodoWebp { get; set; } = 6;

    /// <summary>Release (vistas 3D): <c>exact=True</c> de libwebp, que conserva el RGB de los pixeles invisibles (el mismo ajuste que <c>herramientas\imagen_sin_perdida.py</c>, con el que se recomprimieron los paquetes ya generados). Falso por defecto: los demas paquetes no lo usan.</summary>
    internal static bool ExactoWebp { get; set; }

    private static string SufijoCache() => (MetodoWebp == 6 ? "" : $"m{MetodoWebp}-") + (ExactoWebp ? "x-" : "");

    /// <summary>Procesos en paralelo de la codificacion de WebP (variable POKEKEEP_NUCLEOS si esta puesta; si no, 1).</summary>
    internal static int NucleosWebp { get; set; } = int.TryParse(Environment.GetEnvironmentVariable("POKEKEEP_NUCLEOS"), out var nucleosEntorno) && nucleosEntorno > 0 ? nucleosEntorno : 1;

    /// <summary>
    /// Donde vive la cache de WebP, que NO se borra entre ejecuciones (ADR 0105): <c>POKEKEEP_CACHE_WEBP</c> si esta puesta; si no, <c>&lt;raiz del repo&gt;\compilacion\cache-webp</c> (ignorada por git); si no se encuentra la raiz, la carpeta temporal
    /// como siempre. Antes iba SIEMPRE a la carpeta temporal, y las pruebas redirigen la temporal a una carpeta privada que se crea nueva en cada ejecucion: la cache arrancaba fria y el caso de <c>CajaJuegoX53Tests</c> recodificaba unos 3.600 WebP
    /// a esfuerzo maximo (1.309 s).
    /// </summary>
    internal static string CarpetaCacheWebp { get; set; } = CarpetaCachePredeterminada();

    private static string CarpetaCachePredeterminada()
    {
        var env = Environment.GetEnvironmentVariable("POKEKEEP_CACHE_WEBP");
        if (!string.IsNullOrWhiteSpace(env)) return env;
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "Pokekeep.slnx"))) return Path.Combine(d.FullName, "compilacion", "cache-webp");
        return Path.Combine(Path.GetTempPath(), "pokekeep-webp-cache");
    }

    private static string? _etiquetaCodificador;
    /// <summary>
    /// Que codificador produjo los WebP de la cache: version de Pillow, de libwebp y huella de <c>webp-sin-perdida.py</c>. Entra en el NOMBRE de cada entrada: si cambia alguna, las entradas viejas dejan de encontrarse y se recodifica
    /// (el WebP que se compara byte a byte con el paquete sigue siendo el que produciria el codificador actual). Sin poder preguntarlo a Python la etiqueta es «desconocido» y la cache no se usa.
    /// </summary>
    internal static string EtiquetaCodificador(string script)
    {
        if (_etiquetaCodificador is not null) return _etiquetaCodificador;
        string version;
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("python") { RedirectStandardOutput = true, RedirectStandardError = true };
            psi.ArgumentList.Add("-I"); psi.ArgumentList.Add("-c"); psi.ArgumentList.Add("import PIL, PIL.features; print(PIL.__version__ + ' libwebp ' + str(PIL.features.version('webp')))");
            using var p = System.Diagnostics.Process.Start(psi) ?? throw new InvalidOperationException("No se pudo lanzar python.");
            version = p.StandardOutput.ReadToEnd().Trim(); p.StandardError.ReadToEnd(); p.WaitForExit();
            if (p.ExitCode != 0 || version.Length == 0) version = "desconocido";
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception) { version = "desconocido"; }
        var huellaScript = File.Exists(script) ? Manifiesto.Sha256Hex(File.ReadAllBytes(script)) : "sin-script";
        return _etiquetaCodificador = version == "desconocido" ? "desconocido" : Manifiesto.Sha256Hex(Encoding.UTF8.GetBytes(version + "|" + huellaScript))[..12];
    }

    /// <summary>
    /// WebP sin perdida de todos los iconos de PLA y SV de una vez con <c>herramientas\webp-sin-perdida.py</c> (libwebp de Pillow a esfuerzo maximo: SkiaSharp solo llega a ~9 % menos que el PNG y este a ~32 %). Cache por huella de pixeles
    /// en <see cref="CarpetaCacheWebp"/> (persistente; el nombre de cada entrada lleva la huella de los pixeles del sprite de origen, el metodo y el codificador); el resultado se comprueba despues pixel a pixel con <see cref="WebpLite"/>.
    /// </summary>
    internal static void PrepararWebp(IEnumerable<SpriteImage> imagenes)
    {
        var cache = CarpetaCacheWebp; Directory.CreateDirectory(cache);
        // Las carpetas de trabajo del codificador son de ESTE proceso (dos procesos del gate pueden codificar a la vez).
        var trabajo = Path.Combine(cache, "trabajo-" + Environment.ProcessId + "-" + Guid.NewGuid().ToString("N")[..8]);
        var entrada = Path.Combine(trabajo, "entrada"); var salida = Path.Combine(trabajo, "salida");
        Directory.CreateDirectory(entrada); Directory.CreateDirectory(salida);
        try { PrepararWebpEn(imagenes, cache, entrada, salida); }
        finally { try { Directory.Delete(trabajo, true); } catch (IOException) { /* se queda hasta la proxima: no es cache */ } }
    }

    private static void PrepararWebpEn(IEnumerable<SpriteImage> imagenes, string cache, string entrada, string salida)
    {
        var script = Path.Combine(AppContext.BaseDirectory, "webp-sin-perdida.py");
        if (!File.Exists(script)) script = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "herramientas", "webp-sin-perdida.py"));
        var etiqueta = EtiquetaCodificador(script);
        string Entrada(string clave) => Path.Combine(cache, $"v2-{etiqueta}-" + SufijoCache() + clave + ".webp");
        var pendientes = new Dictionary<string, SpriteImage>(StringComparer.Ordinal);
        foreach (var img in imagenes)
        {
            var clave = Manifiesto.Sha256Hex(img.Pixels) + $"{img.Width}x{img.Height}";
            if (WebpPorPixeles.ContainsKey(clave) || pendientes.ContainsKey(clave)) continue;
            var f = Entrada(clave);
            if (etiqueta != "desconocido" && File.Exists(f)) WebpPorPixeles[clave] = File.ReadAllBytes(f); else pendientes[clave] = img;
        }
        if (pendientes.Count == 0) return;
        var indice = pendientes.Keys.ToList();
        for (var i = 0; i < indice.Count; i++) File.WriteAllBytes(Path.Combine(entrada, i + ".png"), PngLite.Codificar(pendientes[indice[i]]));
        var psi = new System.Diagnostics.ProcessStartInfo("python") { RedirectStandardError = true };
        psi.Environment["POKEKEEP_WEBP_METODO"] = MetodoWebp.ToString(); psi.Environment["POKEKEEP_NUCLEOS"] = NucleosWebp.ToString(); psi.Environment["POKEKEEP_WEBP_EXACTO"] = ExactoWebp ? "1" : "0";
        psi.ArgumentList.Add("-I"); psi.ArgumentList.Add(script); psi.ArgumentList.Add(entrada); psi.ArgumentList.Add(salida);
        Console.WriteLine($"Codificando {pendientes.Count} WebP sin perdida con Pillow (metodo {MetodoWebp}{(ExactoWebp ? ", exact" : "")}, {NucleosWebp} procesos)...");
        using (var p = System.Diagnostics.Process.Start(psi) ?? throw new InvalidOperationException("No se pudo lanzar python."))
        {
            var err = p.StandardError.ReadToEnd(); p.WaitForExit();
            if (p.ExitCode != 0) throw new InvalidOperationException("webp-sin-perdida.py fallo: " + err);
        }
        for (var i = 0; i < indice.Count; i++)
        {
            var b = File.ReadAllBytes(Path.Combine(salida, i + ".webp"));
            WebpPorPixeles[indice[i]] = b;
            if (etiqueta == "desconocido") continue;   // sin saber que codificador lo hizo no se guarda nada en la cache
            // Se escribe a un nombre de este proceso y se renombra: otro proceso del gate que la lea a la vez nunca ve un fichero a medias.
            var destino = Entrada(indice[i]); var parcial = destino + "." + Environment.ProcessId + ".parcial";
            File.WriteAllBytes(parcial, b); File.Move(parcial, destino, overwrite: true);
        }
    }

    private static readonly Dictionary<string, (int, int)> sizes = new(StringComparer.Ordinal);

    private sealed record Nom(string Nombre, string Juego, int Especie, int Forma, bool Hembra, bool Vario);

    private static Nom Analizar(string nombre)
    {
        var p = nombre.Split('_');   // caja_<juego>_<especie>_<forma>[_h][_s]
        return new Nom(nombre, p[1], int.Parse(p[2]), int.Parse(p[3]), p.Skip(4).Contains("h"), p.Skip(4).Contains("s"));
    }

    /// <summary>Espada/Escudo: cada pixel del juego es un bloque 2x2 uniforme alineado al origen; se guarda a su tamano real (la mitad). Si algun bloque no es uniforme, la entrega no es lo que se espera y se aborta.</summary>
    private static SpriteImage Reducir2x(SpriteImage img, string archivo)
    {
        if (img.Width % 2 != 0 || img.Height % 2 != 0) throw new InvalidDataException($"{archivo}: {img.Width}x{img.Height} no es doble de nada.");
        var w = img.Width / 2; var h = img.Height / 2;
        var salida = new SpriteImage(w, h);
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var o = ((y * 2) * img.Width + x * 2) * 4;
                for (var dy = 0; dy < 2; dy++)
                    for (var dx = 0; dx < 2; dx++)
                        if (!img.Pixels.AsSpan(o, 4).SequenceEqual(img.Pixels.AsSpan(((y * 2 + dy) * img.Width + x * 2 + dx) * 4, 4)))
                            throw new InvalidDataException($"{archivo}: el bloque 2x2 ({x},{y}) no es uniforme; no es pixel art ampliado 2x.");
                img.Pixels.AsSpan(o, 4).CopyTo(salida.Pixels.AsSpan((y * w + x) * 4, 4));
            }
        return salida;
    }
}
