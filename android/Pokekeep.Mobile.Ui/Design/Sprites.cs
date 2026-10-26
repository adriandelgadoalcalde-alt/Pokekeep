// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PKHeX.Core;
using Pokekeep.Sprites;

namespace Pokekeep.Mobile.Ui.Design;

/// <summary>
/// Punto unico de los sprites de PKHeX en la interfaz (F3, ADR 0057): los paquetes de recursos, los compositores compartidos por juego de sprites y la conversion de
/// <see cref="SpriteImage"/> a bitmap de Avalonia. La plataforma llama a <see cref="Configurar"/> al arrancar (Android: AssetManager; PC y pruebas: carpeta de paquetes).
/// Un recurso ausente nunca se oculta: el compositor dibuja una silueta, avisa por <see cref="Avisos"/> y la vista pone el numero encima.
/// </summary>
public static class Sprites
{
    static Sprites() => Ajustes.Pkhex.ProgramaPkhex.Cambiado += ActualizarPreferencias;
    private static RecursosEmpaquetados? _recursos;
    private static IRecursosSprites? _presentacion;
    private static readonly Dictionary<(ModoSprites Modo, GameVersion Version), SpriteComposer> Compartidos = new();
    private static readonly ConditionalWeakTable<SpriteImage, Bitmap> Bitmaps = new();
    private static readonly object Cierre = new();
    private static ModoSprites _modoVigente;
    public static ModoSprites ModoVigente
    {
        get { lock (Cierre) return _modoVigente; }
        set { lock (Cierre) _modoVigente = value; }
    }

    /// <summary>Recursos que faltaron (AVISO), una vez cada uno; el registro de la app los recoge.</summary>
    public static readonly HashSet<string> Avisos = new(StringComparer.Ordinal);

    /// <summary>Se invoca (en el hilo que compone) la primera vez que falta un recurso.</summary>
    public static event Action<string>? AvisoRecurso;

    /// <summary>Carga el manifiesto y la forma de abrir cada paquete (<c>big.pkpack</c>...). Se puede repetir (pruebas): descarta compositores y cache.</summary>
    public static void Configurar(byte[] manifiestoJson, Func<string, byte[]> abrirPaquete, long topeCacheBytes = 24L * 1024 * 1024, byte[]? complementoJson = null, byte[]? objetosJson = null, Func<string, byte[]>? abrirPaqueteObjetos = null)
    {
        lock (Cierre)
        {
            _recursos = new RecursosEmpaquetados(Manifiesto.Leer(manifiestoJson), abrirPaquete, topeCacheBytes);
            RecursosEmpaquetados? adicionales = null;
            if (complementoJson is not null)
            {
                var m = Manifiesto.Leer(complementoJson);
                adicionales = new RecursosEmpaquetados(m, nombre =>
                {
                    var b = abrirPaquete(nombre);
                    var p = m.Paquetes.Single(p => p.Archivo == nombre);
                    if (b.LongLength != p.Bytes || !string.Equals(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(b)), p.Sha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("El paquete adicional de imágenes no coincide con su huella.");
                    return b;
                }, topeCacheBytes);
            }
            // Objetos adicionales (X14): MT/MO/DR por tipo y los objetos que PKHeX no dibuja, de PokeAPI/sprites en una revisión fijada. Siempre DESPUÉS de los originales (PKHeX primero); el huella del paquete se comprueba al abrirlo.
            RecursosEmpaquetados? objetos = null;
            if (objetosJson is not null)
            {
                var mo = Manifiesto.Leer(objetosJson);
                var abrir = abrirPaqueteObjetos ?? abrirPaquete;
                objetos = new RecursosEmpaquetados(mo, nombre =>
                {
                    var b = abrir(nombre);
                    var p = mo.Paquetes.Single(p => p.Archivo == nombre);
                    if (b.LongLength != p.Bytes || !string.Equals(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(b)), p.Sha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("El paquete de objetos adicionales no coincide con su huella.");
                    return b;
                }, topeCacheBytes);
            }
            IRecursosSprites? extra = adicionales is null ? objetos : objetos is null ? adicionales : new RecursosPresentacion(adicionales, objetos);
            _presentacion = new RecursosPresentacion(_recursos, extra);
            Compartidos.Clear();
        }
        lock (Referencias) Referencias.Clear();
        lock (CacheFondos) CacheFondos.Clear();
    }

    /// <summary>Carga los paquetes de una carpeta. El paquete de objetos adicionales (X14) se busca en la carpeta hermana <c>objetos</c> (<c>datos/android/objetos</c>) si existe y <paramref name="conObjetos"/> lo permite.</summary>
    public static void ConfigurarDesdeCarpeta(string carpeta, bool conComplemento = false, bool conObjetos = true, bool conJuegos = true)
    {
        var carpetaLogos = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(carpeta).TrimEnd(Path.DirectorySeparatorChar))!, LogosDeJuego.Carpeta);
        LogosDeJuego.Actual = File.Exists(Path.Combine(carpetaLogos, LogosDeJuego.Manifiesto)) ? LogosDeJuego.DesdeCarpeta(carpetaLogos) : null;
        // X51: los sprites propios de cada juego (carpeta hermana sprites-juego); sin ella, o con conJuegos = false, la vista previa sigue con PKHeX.
        var carpetaJuegos = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(carpeta).TrimEnd(Path.DirectorySeparatorChar))!, SpritesDeJuego.Carpeta);
        if (conJuegos && Directory.Exists(carpetaJuegos) && Directory.GetFiles(carpetaJuegos, "manifest*.json").Order(StringComparer.Ordinal).ToArray() is { Length: > 0 } manifiestos)
            ConfigurarJuegos(manifiestos.Select(File.ReadAllBytes).ToList(), f => File.ReadAllBytes(Path.Combine(carpetaJuegos, f)));
        else QuitarJuegos();
        var complemento = Path.Combine(carpeta, "pixel-extra.json");
        var carpetaObjetos = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(carpeta).TrimEnd(Path.DirectorySeparatorChar))!, "objetos");
        var hayObjetos = conObjetos && File.Exists(Path.Combine(carpetaObjetos, "manifest.json"));
        Configurar(File.ReadAllBytes(Path.Combine(carpeta, "manifest.json")), f => File.ReadAllBytes(Path.Combine(carpeta, f)),
            complementoJson: conComplemento && File.Exists(complemento) ? File.ReadAllBytes(complemento) : null,
            objetosJson: hayObjetos ? File.ReadAllBytes(Path.Combine(carpetaObjetos, "manifest.json")) : null,
            abrirPaqueteObjetos: hayObjetos ? f => File.ReadAllBytes(Path.Combine(carpetaObjetos, f)) : null);
    }

    public static bool Configurado => _recursos is not null;

    private static RecursosJuego? _juegos;

    /// <summary>Sprites propios de cada juego (X51 la vista previa, X49 las cajas): todos los <c>manifest*.json</c> de <c>sprites-juego</c>. Null si la app se compiló sin ellos. Cada paquete se verifica al abrirlo.</summary>
    public static RecursosJuego? RecursosDeJuego { get { lock (Cierre) return _juegos; } }

    public static void ConfigurarJuegos(IReadOnlyList<byte[]> manifiestos, Func<string, byte[]> abrirPaquete)
    {
        lock (Cierre) _juegos = new RecursosJuego(manifiestos, abrirPaquete);
        lock (CacheFondos) CacheFondos.Clear();
        lock (Ampliados) { Ampliados.Clear(); AmpliadosLru.Clear(); }
        lock (ReferenciasIconos) ReferenciasIconos.Clear();
    }

    public static void QuitarJuegos() { lock (Cierre) _juegos = null; lock (ReferenciasIconos) ReferenciasIconos.Clear(); lock (CacheFondos) CacheFondos.Clear(); }

    /// <summary>
    /// Imagen de la vista previa grande en el juego de la partida (Gen. 1 a 5, tabla en <see cref="SpritesDeJuego.JuegoDe"/>), solo con el juego de sprites clasico (artwork y mugshot son una eleccion del usuario). Null: se usa el de PKHeX.
    /// </summary>
    public static SpriteImage? VistaDeJuego(ModoSprites modo, GameVersion version, int especie, int forma, int genero, bool variocolor, bool huevo)
        => RecursosDeJuego is { } r && UsaVistaDelJuego(modo, version) ? SpritesDeJuego.Vista(r, version, especie, forma, genero == 1, variocolor, huevo) : null;

    /// <summary>
    /// X71: la vista grande es la del juego con el juego de sprites clasico y, en los juegos de Gen. 6 a 9 con vista propia, tambien cuando el modo es el que PKHeX SUGIERE para ese guardado (Leyendas Arceus = mugshot,
    /// Escarlata/Purpura y Leyendas Z-A = artwork): eso no es una eleccion del usuario y el artwork oficial seria un respaldo de PKHeX en una vista 3D. Con una preferencia explicita (forzar artwork o mugshots,
    /// o «no cambiar» con otro modo) manda el usuario.
    /// </summary>
    public static bool UsaVistaDelJuego(ModoSprites modo, GameVersion version)
        => ModoEfectivo(modo) == ModoSprites.Clasico
           || (Ajustes.Pkhex.ProgramaPkhex.Ajustes.Actual.Sprite.SpritePreference == PreferenciaSprites.Sugerida && SpritesDeJuego.JuegoDe(version) is { } juego && SpritesDeJuego.SinRespaldoDePkhex(juego));

    /// <summary>
    /// X71: true si la vista previa grande de esta partida es de un juego de Gen. 6 a 9 con vista propia y SIN respaldo de PKHeX (regla del propietario del 17-oct: todo fiel al juego en las vistas 3D), con el juego de sprites
    /// clasico (o el modo sugerido por PKHeX, ver <see cref="UsaVistaDelJuego"/>) y con los paquetes cargados. Si es true y <see cref="VistaDeJuego"/> devuelve null, la combinacion NO esta en el paquete: la vista dibuja el aviso de «recurso ausente» (silueta y numero) y nunca el sprite de PKHeX.
    /// </summary>
    public static bool VistaSinRespaldoDePkhex(ModoSprites modo, GameVersion version)
        => RecursosDeJuego is not null && UsaVistaDelJuego(modo, version) && SpritesDeJuego.JuegoDe(version) is { } juego && SpritesDeJuego.SinRespaldoDePkhex(juego);

    /// <summary>X71: anota (una vez cada una) una vista de Gen. 6 a 9 que falta en el paquete, con el mismo registro de avisos que el resto de recursos ausentes.</summary>
    public static void AvisarVistaAusente(GameVersion version, int especie, int forma, int genero, bool variocolor, bool huevo)
    {
        var juego = SpritesDeJuego.JuegoDe(version) ?? "?";
        var nombre = huevo ? SpritesDeJuego.Nombre(SpritesDeJuego.ClaseVista, juego, 0, 0, false, false) : SpritesDeJuego.Nombre(SpritesDeJuego.ClaseVista, juego, especie, forma, genero == 1, variocolor);
        bool nuevo; lock (Avisos) nuevo = Avisos.Add(nombre);
        if (nuevo) AvisoRecurso?.Invoke(nombre);
    }

    /// <summary>
    /// Icono de caja del propio juego de la partida (X53) para listas y filas sin imagen ya compuesta: Gen. III a V, Espada/Escudo y Diamante Brillante/Perla Reluciente (tabla en <see cref="SpritesDeJuego.JuegoDeCaja"/>), solo con el
    /// juego de sprites clasico. Null: se usa el sprite de PKHeX de siempre.
    /// </summary>
    public static SpritesDeJuego.IconoCaja? IconoDeCaja(ModoSprites modo, GameVersion version, int especie, int forma, int genero, bool variocolor, bool huevo, EntityContext contexto)
        => RecursosDeJuego is not null && especie > 0 && Compartido(modo, version).IconoDeCaja((ushort)especie, (byte)forma, (byte)genero, variocolor, huevo, contexto) is { } i ? i : null;

    /// <summary>X53d: los iconos de ese juego vienen recortados por el lienzo (anotado en el manifiesto): donde no hay casilla con marco se dibujan sobre una baldosa.</summary>
    public static bool IconoRecortado(string juego) => RecursosDeJuego?.EsRecortado(juego) == true;

    private static readonly Dictionary<string, double> ReferenciasIconos = new(StringComparer.Ordinal);

    /// <summary>
    /// Referencia de tamano de la regla comun de escala (X48, opcion C) para los iconos de caja de UN juego (X53): percentil 90 del LADO MAYOR de la parte visible de sus iconos de Pokemon (sin huevo, sin variocolor), en pixeles de
    /// ese icono (el real del juego), calculado del paquete de cada juego y nunca escrito a mano. Con ella k = lado util / referencia es la escala comun de todas las casillas de esa partida.
    /// </summary>
    public static double ReferenciaIcono(string juego)
    {
        lock (ReferenciasIconos)
        {
            if (ReferenciasIconos.TryGetValue(juego, out var r)) return r;
            if (RecursosDeJuego is not { } fuente) return 40;
            var prefijo = SpritesDeJuego.ClaseCaja + "_" + juego + "_";
            var lados = new List<int>();
            foreach (var f in fuente.Fuentes)
                foreach (var nombre in f.Manifiesto.Recursos.Keys.Order(StringComparer.Ordinal))
                {
                    if (!nombre.StartsWith(prefijo, StringComparison.Ordinal)) continue;
                    var partes = nombre[prefijo.Length..].Split('_');
                    if (partes[0] == "0000" || partes.Skip(2).Contains("s")) continue;   // sin huevo y sin variocolor (misma silueta)
                    if (f.Obtener(nombre) is not { } img) continue;
                    var v = Recuadro(img);
                    lados.Add((int)Math.Max(v.Width, v.Height));
                }
            if (lados.Count == 0) return ReferenciasIconos[juego] = 40;
            lados.Sort();
            return ReferenciasIconos[juego] = lados[(int)Math.Ceiling(0.9 * lados.Count) - 1];
        }
    }

    /// <summary>Las composiciones compartidas se rehacen tras cambiar los ajustes de sprites.</summary>
    public static void ActualizarPreferencias()
    {
        lock (Cierre) Compartidos.Clear();
    }

    public static RecursosEmpaquetados Recursos => _recursos ?? throw new InvalidOperationException("Sprites.Configurar no se ha llamado: faltan los paquetes de sprites.");

    /// <summary>Compositor con los ajustes por defecto de PKHeX para un juego de sprites (listas, ficha y editor, sin guardado). Compartido: no cambiar su <c>Opciones</c>.</summary>
    public static SpriteComposer Compartido(ModoSprites modo, GameVersion version = GameVersion.Any)
    {
        modo = ModoEfectivo(modo);
        lock (Cierre)
        {
            if (!Compartidos.TryGetValue((modo, version), out var c))
            {
                c = Nuevo(version: version);
                c.CambiarModo(modo);
                Compartidos[(modo, version)] = c;
            }
            return c;
        }
    }

    /// <summary>Compositor propio (con sus ajustes y su cache) para una sesion.</summary>
    public static SpriteComposer Nuevo(OpcionesSprites? opciones = null, bool conEstrella = false, GameVersion version = GameVersion.Any)
    {
        // Sin estrella de brillo cosida: la dibuja SpritePokemon encima (ver OpcionesSprites.EstrellaBrillo). Solo las imagenes sueltas (regalos) la llevan cosida.
        var vigentes = opciones ?? Ajustes.Pkhex.ProgramaPkhex.Ajustes.Actual.Sprite.ComoOpciones();
        vigentes.EstrellaBrillo = conEstrella;
        var c = new SpriteComposer(_presentacion ?? Recursos, vigentes, version: version);
        c.AvisoAusente = n => { lock (Avisos) Avisos.Add(n); AvisoRecurso?.Invoke(n); };
        c.FuenteIconos = () => RecursosDeJuego;   // X53: iconos de caja del propio juego (solo GetSlotCapas e IconoDeCaja; GetSprite sigue igual que PKHeX)
        return c;
    }

    public static GameVersion VersionDe(SaveFile sav) => sav.Version == GameVersion.FRLG
        ? ReferenceEquals(sav.Personal, PersonalTable.FR) ? GameVersion.FR : GameVersion.LG : sav.Version;

    /// <summary>Consulta solo de presentación; no modifica ni amplía el manifiesto canónico de PKHeX.</summary>
    public static bool TieneRecursoPresentacion(string nombre) => (_presentacion ?? Recursos).Obtener(nombre) is not null;

    /// <summary>Bitmap de Avalonia de una imagen del compositor. Las imagenes cacheadas del compositor se convierten una sola vez.</summary>
    public static Bitmap Bitmap(SpriteImage img)
    {
        lock (Bitmaps)
        {
            if (Bitmaps.TryGetValue(img, out var b)) return b;
            b = Crear(img);
            Bitmaps.Add(img, b);
            return b;
        }
    }

    /// <summary>Convierte sin guardar en la tabla (para imagenes de un solo uso, como la de una casilla).</summary>
    public static Bitmap Crear(SpriteImage img)
    {
        var wb = new WriteableBitmap(new PixelSize(img.Width, img.Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
        using var fb = wb.Lock();
        for (var y = 0; y < img.Height; y++)
            System.Runtime.InteropServices.Marshal.Copy(img.Pixels, y * img.Width * 4, fb.Address + y * fb.RowBytes, img.Width * 4);
        return wb;
    }

    private static readonly Dictionary<SpriteImage, LinkedListNode<(SpriteImage Img, Bitmap Bmp, Rect Rec)>> Ampliados = new(ReferenceEqualityComparer.Instance);
    private static readonly LinkedList<(SpriteImage Img, Bitmap Bmp, Rect Rec)> AmpliadosLru = new();
    private const int TopeAmpliados = 6;   // X48: el lienzo entero x16 son ~3,9 MB (antes, recortado a lo visible, ~2 MB)

    /// <summary>Pasos del escalador para la vista ampliada (x16): la ficha mide ~12 a 16 pixeles fisicos por pixel del sprite, asi que el reescalado final desde x16 es casi 1:1 y no desenfoca.</summary>
    public const int PasosAmpliado = 4;

    /// <summary>
    /// Amplia <paramref name="img"/> ENTERA (el lienzo completo) 2^<paramref name="pasos"/> veces con el escalador y devuelve el bitmap y la parte visible en sus pixeles. X48: no se recorta el bitmap; lo que decide la escala
    /// es la regla comun de <see cref="SpritePokemon"/> (misma escala para todos, calculada con <see cref="ReferenciaVisible"/>), no el tamano de cada recorte.
    /// </summary>
    private static (Bitmap, Rect) AmpliarLienzo(SpriteImage img, int pasos)
    {
        var k = 1 << pasos; var r = Recuadro(img);
        return (Crear(EscaladorPixelArt.Ampliar(img, pasos)), new Rect(r.X * k, r.Y * k, r.Width * k, r.Height * k));
    }

    /// <summary>
    /// Bitmap del sprite ampliado x16 con el escalador de pixel art (<see cref="EscaladorPixelArt"/>), del lienzo entero (X48), para la vista ampliada (ficha, editor, Librería): bordes redondeados y nitidos, sin cuadros. Se
    /// guardan los 6 ultimos (cada uno ~3,9 MB; el compositor ya cachea la imagen por especie/forma/sexo/brillo, asi que repetir un Pokemon no recalcula).
    /// </summary>
    public static (Bitmap Bitmap, Rect Recuadro) Ampliado(SpriteImage img)
    {
        lock (Ampliados)
        {
            if (Ampliados.TryGetValue(img, out var nodo)) { AmpliadosLru.Remove(nodo); AmpliadosLru.AddFirst(nodo); return (nodo.Value.Bmp, nodo.Value.Rec); }
            var (b, rec) = AmpliarLienzo(img, PasosAmpliado);
            Ampliados[img] = AmpliadosLru.AddFirst((img, b, rec));
            while (Ampliados.Count > TopeAmpliados) { var u = AmpliadosLru.Last!; AmpliadosLru.RemoveLast(); Ampliados.Remove(u.Value.Img); }
            return (b, rec);
        }
    }

    /// <summary>Ajuste «Suavizar sprites de la caja» (desactivado por defecto): la casilla usa el escalador de pixel art en vez del factor entero. Lo fija la Shell desde los ajustes.</summary>
    public static bool SuavizarCaja { get; set; }

    private static readonly ConditionalWeakTable<SpriteImage, Tuple<Bitmap, Rect>> AmpliadosCasilla = new();

    /// <summary>Pixeles del bitmap por pixel del sprite en <see cref="AmpliadoDeCasilla"/> (dos pasos del escalador: x4).</summary>
    public const int FactorCasilla = 4;

    /// <summary>
    /// Como <see cref="Ampliado"/> pero a x4 (lienzo entero, ~240 KB por casilla). Devuelve el bitmap y la parte visible (en pixeles del bitmap).
    /// </summary>
    public static (Bitmap Bitmap, Rect Recuadro) AmpliadoDeCasilla(SpriteImage img)
    {
        lock (AmpliadosCasilla)
        {
            if (AmpliadosCasilla.TryGetValue(img, out var t)) return (t.Item1, t.Item2);
            var (bmp, rec) = AmpliarLienzo(img, 2);   // la casilla usa dos pasos (x4)
            AmpliadosCasilla.Add(img, Tuple.Create(bmp, rec));
            return (bmp, rec);
        }
    }

    /// <summary>Marca de casilla lista para dibujar: su imagen recortada a lo visible y la esquina donde va.</summary>
    public sealed record MarcaVista(Bitmap Bitmap, int Ancho, int Alto, EsquinaMarca Esquina);

    public static MarcaVista? VistaDe(MarcaCasilla m)
    {
        var r = Recuadro(m.Imagen);
        int x0 = (int)r.X, y0 = (int)r.Y, w = (int)r.Width, h = (int)r.Height;
        if (w <= 0 || h <= 0) return null;
        var recorte = new SpriteImage(w, h);
        for (var y = 0; y < h; y++) Buffer.BlockCopy(m.Imagen.Pixels, ((y0 + y) * m.Imagen.Width + x0) * 4, recorte.Pixels, y * w * 4, w * 4);
        return new MarcaVista(Crear(recorte), w, h, m.Esquina);
    }

    private static readonly ConditionalWeakTable<SpriteImage, object> Recuadros = new();

    /// <summary>
    /// Caja minima (en pixeles de la imagen) que contiene todo lo que se ve (alfa &gt; 8) de una imagen compuesta. Los sprites de PKHeX llevan un lienzo de 68x56 con mucho margen vacio
    /// (en escritorio no importa); en una casilla de movil de ~50 dp la vista dibuja SOLO esta caja, ampliada hasta llenar la casilla (presentacion aprobada en la F2). La composicion no cambia.
    /// </summary>
    public static Rect Recuadro(SpriteImage img)
    {
        if (Recuadros.TryGetValue(img, out var o)) return (Rect)o;
        int x0 = img.Width, y0 = img.Height, x1 = -1, y1 = -1;
        var d = img.Pixels;
        for (var y = 0; y < img.Height; y++)
            for (var x = 0; x < img.Width; x++)
                if (d[(y * img.Width + x) * 4 + 3] > 8) { if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; }
        var r = x1 >= x0 && y1 >= y0 ? new Rect(x0, y0, x1 - x0 + 1, y1 - y0 + 1) : new Rect(0, 0, img.Width, img.Height);
        try { Recuadros.Add(img, r); } catch (ArgumentException) { }
        return r;
    }

    private static SpriteImage? _estrellaBrillo;

    /// <summary>
    /// Estrella de variocolor hecha a mano en pixeles (11 x 11: destello de cuatro puntas rojo con borde blanco, sin vectores ni suavizado) para los sprites propios de cada juego (X51). Solo tiene dos colores
    /// opacos (rojo E5383B y blanco): se dibuja con el MISMO factor entero que el sprite, en la esquina superior izquierda.
    /// </summary>
    public static SpriteImage EstrellaBrilloPixeles()
    {
        if (_estrellaBrillo is { } e) return e;
        const int n = 11, c = 5;
        bool Nucleo(int x, int y) { int dx = Math.Abs(x - c), dy = Math.Abs(y - c); return (dx == 0 && dy <= 4) || (dy == 0 && dx <= 4) || dx + dy <= 2; }
        var img = new SpriteImage(n, n);
        for (var y = 0; y < n; y++)
            for (var x = 0; x < n; x++)
            {
                var rojo = Nucleo(x, y);
                var borde = !rojo && (Nucleo(x - 1, y) || Nucleo(x + 1, y) || Nucleo(x, y - 1) || Nucleo(x, y + 1) || Nucleo(x - 1, y - 1) || Nucleo(x + 1, y - 1) || Nucleo(x - 1, y + 1) || Nucleo(x + 1, y + 1));
                if (!rojo && !borde) continue;
                var o = (y * n + x) * 4;
                if (rojo) { img.Pixels[o] = 0x3B; img.Pixels[o + 1] = 0x38; img.Pixels[o + 2] = 0xE5; }   // BGRA de E5383B
                else { img.Pixels[o] = 0xFF; img.Pixels[o + 1] = 0xFF; img.Pixels[o + 2] = 0xFF; }
                img.Pixels[o + 3] = 0xFF;
            }
        return _estrellaBrillo = img;
    }

    private static readonly Dictionary<ModoSprites, double> Referencias = new();

    /// <summary>Juego de sprites que de verdad se usa (respeta el ajuste de preferencia de sprites de PKHeX).</summary>
    public static ModoSprites ModoEfectivo(ModoSprites modo) => Ajustes.Pkhex.ProgramaPkhex.Ajustes.Actual.Sprite.SpritePreference switch
    {
        PreferenciaSprites.ForzarSprites => ModoSprites.Clasico,
        PreferenciaSprites.ForzarMugshots => ModoSprites.Mugshot,
        PreferenciaSprites.ForzarArtwork => ModoSprites.Artwork,
        PreferenciaSprites.NoCambiar => ModoVigente,
        _ => modo,
    };

    /// <summary>
    /// Referencia de tamano de la regla comun de escala (X48, opcion C del propietario): percentil 90 del LADO MAYOR de la parte visible de los sprites de Pokemon del juego de sprites en uso (big ≈ 40, artwork ≈ 54
    /// pixeles), calculado de los paquetes (nunca escrito a mano). Una escala k = lado util / referencia es comun a todos los Pokemon de un contexto: el grande normal llena la casilla, el pequeno sale mas pequeno y todos
    /// con el mismo grosor de pixel; solo el que no cabe con esa k se reduce lo justo.
    /// </summary>
    public static double ReferenciaVisible(ModoSprites modo)
    {
        modo = ModoEfectivo(modo);
        lock (Referencias)
        {
            if (Referencias.TryGetValue(modo, out var r)) return r;
            if (_recursos is null) return 40;
            var prefijo = modo switch { ModoSprites.Artwork => "a_", ModoSprites.Mugshot => "c_", _ => "b_" };
            var lados = new List<int>();
            foreach (var nombre in _recursos.Manifiesto.Recursos.Keys)
            {
                if (!nombre.StartsWith(prefijo, StringComparison.Ordinal) || nombre.Length < 3 || nombre[2] is < '1' or > '9') continue;   // solo especies (no b_egg, b_unknown ni b_0)
                if (_recursos.Obtener(nombre) is not { } img) continue;
                var v = Recuadro(img);
                lados.Add((int)Math.Max(v.Width, v.Height));
            }
            if (lados.Count == 0) return Referencias[modo] = 40;
            lados.Sort();
            return Referencias[modo] = lados[(int)Math.Ceiling(0.9 * lados.Count) - 1];
        }
    }

    // ---- Fondos de caja (los de PKHeX.Drawing.Misc, paquete «fondos») ----
    private static readonly Dictionary<string, Bitmap?> CacheFondos = new();

    /// <summary>Nombre del recurso del fondo, igual que <c>WallpaperUtil.GetWallpaperResourceName</c> de PKHeX (<c>box_wp03dp</c>...).</summary>
    public static string NombreFondo(GameVersion version, int indice)
    {
        indice++;
        var sufijo = SufijoFondo(version, indice);
        var variante = version switch { GameVersion.SL when indice is 20 => "_n", GameVersion.VL when indice is 20 => "_u", _ => string.Empty };
        return $"box_wp{indice:00}{sufijo}{variante}";
    }

    private static string SufijoFondo(GameVersion version, int index) => version.Context switch
    {
        EntityContext.Gen3 when version == GameVersion.E => "e",
        EntityContext.Gen3 when GameVersion.FRLG.Contains(version) && index > 12 => "frlg",
        EntityContext.Gen3 => "rs",
        _ when version == GameVersion.COLO => "colo",   // PKHeX 26.10.18: Colosseum y XD tienen sus propios fondos, uno por caja
        _ when version == GameVersion.XD => "xd",
        EntityContext.Gen4 when GameVersion.DP.Contains(version) || index <= 16 => "dp",
        EntityContext.Gen4 when version == GameVersion.Pt => "pt",
        EntityContext.Gen4 when GameVersion.HGSS.Contains(version) => "hgss",
        EntityContext.Gen5 => GameVersion.B2W2.Contains(version) && index > 16 ? "b2w2" : "bw",
        EntityContext.Gen6 => GameVersion.ORAS.Contains(version) && index > 16 ? "ao" : "xy",
        EntityContext.Gen7 => "xy",
        EntityContext.Gen8b => "bdsp",
        EntityContext.Gen8 => "swsh",
        EntityContext.Gen9 => "sv",
        _ => string.Empty,
    };

    private static Bitmap? Fondo(string nombre)
    {
        lock (CacheFondos)
        {
            if (CacheFondos.TryGetValue(nombre, out var b)) return b;
            Bitmap? r = null;
            // X54: el fondo ORIGINAL del juego (paquete fondos-originales de sprites-juego, la misma clave y el mismo tamano) y, si no lo trae (juegos sin volcado o sin imagen), el de PKHeX.
            if (_juegos is { } orig && orig.Contiene(nombre) && orig.Obtener(nombre) is { } propio) r = Crear(propio);
            else if (_recursos is { } rec && rec.Contiene(nombre) && rec.Obtener(nombre) is { } img) r = Crear(img);
            return CacheFondos[nombre] = r;
        }
    }

    /// <summary>X54: true si el fondo de esa clave sale del paquete de fondos originales del juego y no del de PKHeX (para las pruebas y el informe).</summary>
    public static bool FondoEsOriginal(string nombre) { lock (Cierre) return _juegos is { } j && j.Contiene(nombre); }

    /// <summary>Fondo de la caja como lo elige PKHeX (<c>WallpaperImage</c>): el de la caja, el de ciudad de Z-A, el de pradera de Leyendas Arceus o el predeterminado. Null si los paquetes no lo traen.</summary>
    public static Bitmap? FondoDeCaja(SaveFile sav, int caja)
    {
        if (!Configurado) return null;
        if (sav is SAV3RSBox rs && rs.GetBoxWallpaper(caja) == MiFondoRsBox.Indice && FondoConMiFondo(rs, caja) is { } propio) return propio;
        return Fondo(NombreFondoDeCaja(sav, caja)) ?? Fondo(FondoPredeterminado);
    }

    /// <summary>El fondo «Mi fondo» de Pokémon Box Rubí y Zafiro con la imagen de la partida dibujada encima (ver <see cref="MiFondoRsBox"/>). Se recuerda por mitad (caja par o impar) y contenido de la imagen.</summary>
    private static Bitmap? FondoConMiFondo(SAV3RSBox sav, int caja)
    {
        var nombre = NombreFondoDeCaja(sav, caja);
        var imagen = ImagenFondo(nombre);
        if (imagen is null) return null;
        var clave = $"{nombre}|mifondo|{caja % 2}|{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(sav.MyWallpaper))}";
        lock (CacheFondos)
        {
            if (CacheFondos.TryGetValue(clave, out var b)) return b;
            if (CacheFondos.Count > 64) CacheFondos.Clear();
            return CacheFondos[clave] = Crear(MiFondoRsBox.Componer(imagen, sav.MyWallpaper, caja));
        }
    }

    /// <summary>La imagen de un fondo (el original del juego o, si no lo trae, el de PKHeX), sin convertirla a bitmap.</summary>
    internal static SpriteImage? ImagenFondo(string nombre)
    {
        lock (Cierre)
        {
            if (_juegos is { } orig && orig.Contiene(nombre) && orig.Obtener(nombre) is { } propio) return propio;
            if (_recursos is { } rec && rec.Contiene(nombre) && rec.Obtener(nombre) is { } img) return img;
            return null;
        }
    }

    /// <summary>El fondo que PKHeX usa cuando el juego no tiene fondos propios (Gen. 1-2, Let's Go) o el recurso elegido no existe.</summary>
    public const string FondoPredeterminado = "box_wp16xy";

    /// <summary>Recurso de fondo que elige <c>WallpaperUtil.WallpaperImage</c> de PKHeX para esa caja (sin la recaida al predeterminado, que hace <see cref="FondoDeCaja"/> si el paquete no lo trae).</summary>
    public static string NombreFondoDeCaja(SaveFile sav, int caja)
    {
        if (sav is SAV3Colosseum or SAV3XD) return NombreFondo(sav.Version, caja);   // PKHeX 26.10.18: un fondo fijo por caja
        if (sav is SAV4BR) return NombreFondo(GameVersion.D, caja % 16);              // Battle Revolution usa los de Diamante y Perla
        if (sav is SAV9ZA) return "box_wp02bdsp";   // ciudad (Lumiose) en Z-A
        if (sav is SAV8LA) return "box_wp01bdsp";   // pradera en Leyendas Arceus
        if (sav is not IBoxDetailWallpaper w) return FondoPredeterminado;
        return NombreFondo(sav.Version, w.GetBoxWallpaper(caja));
    }

    /// <summary>Tamano en pixeles del recurso de fondo, o null si los paquetes no lo traen (para elegir el muestreo y para el informe de fondos por juego).</summary>
    public static (int Ancho, int Alto)? TamanoFondo(string nombre) => Configurado && Fondo(nombre) is { } b ? (b.PixelSize.Width, b.PixelSize.Height) : null;

    /// <summary>Miniatura de un fondo concreto (para elegir fondo).</summary>
    public static Bitmap? MiniaturaFondo(SaveFile sav, int indice) => Configurado && indice >= 0 ? Fondo(NombreFondo(sav.Version, indice)) : null;

    /// <summary>Icono del objeto equipado de una ficha: <paramref name="idPantalla"/> es el id ya modernizado (<c>pk.SpriteItem</c>); el sprite de PKHeX y, si PKHeX no lo tiene, el de PokeAPI (<see cref="ObjetosExtra"/>).</summary>
    public static Bitmap? ObjetoEquipado(int idPantalla, EntityContext contexto, GameVersion version = GameVersion.Any)
        => idPantalla <= 0 || _recursos is null ? null : Compartido(ModoSprites.Clasico).GetItemSpriteMostrado(idPantalla, contexto, version) is { } i ? Bitmap(i) : null;

    /// <summary>
    /// Icono de un objeto de la mochila: id NATIVO del formato y juego de la partida. Las MT/MO/DR salen con el icono de su TIPO (el del movimiento que enseñan en ESE juego), el resto con el sprite de PKHeX y,
    /// si PKHeX no lo tiene (o en Gen. 1-3 no convierte su id), con el de PokeAPI. Null si nadie tiene arte: la vista pone entonces su glifo.
    /// </summary>
    public static Bitmap? ObjetoDeBolsa(int idNativo, EntityContext contexto, GameVersion version)
        => idNativo <= 0 || _recursos is null ? null : Compartido(ModoSprites.Clasico).GetItemSpriteJuego(idNativo, contexto, version) is { } i ? Bitmap(i) : null;

    /// <summary>Imagen de un regalo misterioso (el Pokemon, el huevo o el objeto que da); null si la ranura esta vacia o no hay sprites.</summary>
    public static Bitmap? Regalo(DataMysteryGift g)
    {
        if (_recursos is null || g.IsEmpty) return null;
        if (g.IsItem) return ObjetoEquipado(g.ItemID, g.Context);
        if (!g.IsEntity || g.Species == 0) return null;   // sin Pokemon ni objeto no hay imagen: la columna se colapsa
        try { return Bitmap((_regalos ??= Nuevo(null, true)).GetMysteryGiftPreviewPoke(g)); } catch { return null; }
    }

    private static SpriteComposer? _regalos;

    public static Bitmap? Imagen(string fichero)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(fichero, out var b)) return b;
            Bitmap? r = null;
            try { using var s = AssetLoader.Open(new Uri($"avares://Pokekeep.Mobile.Ui/Assets/Imagenes/{fichero}")); r = new Bitmap(s); } catch { }
            return Cache[fichero] = r;
        }
    }

    private static readonly Dictionary<string, Bitmap?> Cache = new();
}
