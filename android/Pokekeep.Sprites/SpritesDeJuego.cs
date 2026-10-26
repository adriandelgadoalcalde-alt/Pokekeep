// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;

namespace Pokekeep.Sprites;

/// <summary>
/// Sprites PROPIOS de cada juego para la vista previa grande (Gen. 1 a 5 pixelados, X51; Let's Go X58 y Gen. 6 a 9 X71, renders 3D) y, en paralelo, los iconos de caja (X49). Un paquete por juego y por clase de imagen, en la carpeta
/// <see cref="Carpeta"/> (<c>datos/android/sprites-juego/</c> en el repo, <c>Assets/sprites-juego/</c> en el APK):
/// <list type="bullet">
/// <item><c>manifest.json</c> + <c>vista-&lt;juego&gt;.pkpack</c>: la vista previa (X51). Nombres <c>vista_&lt;juego&gt;_&lt;especie:0000&gt;_&lt;forma:00&gt;[_h][_s]</c> (<c>_h</c> hembra con grafico propio, <c>_s</c> variocolor; especie 0 = huevo, forma 1 = huevo de Manaphy).</item>
/// <item>Gancho de X49: cualquier otro <c>manifest-&lt;clase&gt;.json</c> de la carpeta (por ejemplo <c>manifest-caja.json</c> con <c>caja-&lt;juego&gt;.pkpack</c> y recursos <c>caja_&lt;juego&gt;_...</c>) se carga solo y se suma al mismo
/// <see cref="RecursosJuego"/>; el formato es el de <see cref="Manifiesto"/> (PNG sin perdida concatenados) y se pide con <see cref="Nombre"/>. Cada clase se genera y se verifica por separado.</item>
/// </list>
/// Un recurso ausente nunca es un error: quien llama cae al sprite de PKHeX que ya habia.
/// </summary>
public static class SpritesDeJuego
{
    public const string Carpeta = "sprites-juego";
    public const string ClaseVista = "vista";
    public const string ClaseCaja = "caja";

    /// <summary>
    /// Juego de la entrega del que salen los sprites de una partida, o null si el juego no tiene vista propia (Colosseum, XD y Battle Revolution). Los que no tienen juego propio usan el mas cercano de su generacion:
    /// Oro usa Plata; Rubi y Zafiro, Esmeralda; Verde Hoja, Rojo Fuego; Diamante y Perla, Platino; Oro HeartGold, SoulSilver; Negro, Blanco y Negro 2, Blanco 2. Gen. 1: Rojo y Verde japones usan Rojo; Azul, Azul;
    /// una partida «RB» (PKHeX no distingue Rojo de Azul al leer el guardado) usa Rojo. Colosseum, XD y Battle Revolution (modelos 3D) no tienen sprite de cartucho: se quedan con PKHeX.
    /// </summary>
    public static string? JuegoDe(GameVersion v) => v switch
    {
        GameVersion.RD or GameVersion.GN or GameVersion.RB => "rojo",
        GameVersion.BU => "azul",
        GameVersion.YW => "amarillo",
        GameVersion.GD or GameVersion.SI or GameVersion.GS => "plata",
        GameVersion.C => "cristal",
        GameVersion.R or GameVersion.S or GameVersion.E or GameVersion.RS or GameVersion.RSE => "esmeralda",
        GameVersion.FR or GameVersion.LG or GameVersion.FRLG => "rojo-fuego",
        GameVersion.D or GameVersion.P or GameVersion.Pt or GameVersion.DP => "platino",
        GameVersion.HG or GameVersion.SS or GameVersion.HGSS => "soulsilver",
        GameVersion.B or GameVersion.W or GameVersion.B2 or GameVersion.W2 or GameVersion.BW or GameVersion.B2W2 => "blanco-2",
        GameVersion.GP or GameVersion.GE or GameVersion.GG => "lgpe",   // X58: Let's Go, Pikachu! y Eevee! (renders 3D del juego)
        // X71: Gen. 6 a 9, renders 3D de la Pokedex y la caja de cada juego (capturas con RenderDoc, X70). X e Y comparten paquete, Sol/Luna con Ultrasol/Ultraluna, Espada con Escudo,
        // Diamante Brillante con Perla Reluciente y Escarlata con Purpura: el modelo es el mismo en los dos juegos de cada par.
        GameVersion.X or GameVersion.Y or GameVersion.XY => JuegoY,
        GameVersion.OR or GameVersion.AS or GameVersion.ORAS => JuegoOr,
        GameVersion.SN or GameVersion.MN or GameVersion.US or GameVersion.UM or GameVersion.SM or GameVersion.USUM => JuegoUs,
        GameVersion.SW or GameVersion.SH or GameVersion.SWSH => JuegoSwsh,
        GameVersion.BD or GameVersion.SP or GameVersion.BDSP => JuegoBdsp,
        GameVersion.PLA => JuegoPla,
        GameVersion.SL or GameVersion.VL or GameVersion.SV => JuegoSv,
        GameVersion.ZA => JuegoZa,
        _ => null,
    };

    /// <summary>Let's Go: caja pixel art nativa (X66) y vista previa renderizada (X58), en manifiestos separados.</summary>
    public const string Lgpe = "lgpe";

    // X71: identificadores de las vistas 3D de Gen. 6 a 9 (clase «vista»; no confundir con los de caja: xy, oras, usum...).
    public const string JuegoY = "y";
    public const string JuegoOr = "or";
    public const string JuegoUs = "us";
    public const string JuegoSwsh = "swsh";
    public const string JuegoBdsp = "bdsp";
    public const string JuegoPla = "pla";
    public const string JuegoSv = "sv";
    public const string JuegoZa = "za";

    /// <summary>Los diez juegos de la entrega X47 (sprites pixelados de Gen. 1 a 5), en el orden de las generaciones.</summary>
    public static readonly IReadOnlyList<string> JuegosClasicos = ["rojo", "azul", "amarillo", "plata", "cristal", "rojo-fuego", "esmeralda", "platino", "soulsilver", "blanco-2"];

    /// <summary>Los ocho grupos de Gen. 6 a 9 con vista previa propia (X71, renders 3D de la captura de cada juego), en el orden de las generaciones.</summary>
    public static readonly IReadOnlyList<string> JuegosVista3D = [JuegoY, JuegoOr, JuegoUs, JuegoSwsh, JuegoBdsp, JuegoPla, JuegoSv, JuegoZa];

    /// <summary>Todos los juegos con vista previa propia: los diez clasicos, Let's Go (X58) y los ocho de Gen. 6 a 9 (X71).</summary>
    public static readonly IReadOnlyList<string> Juegos = [.. JuegosClasicos, Lgpe, .. JuegosVista3D];

    /// <summary>
    /// X71: la vista previa de ese juego NUNCA cae al sprite de PKHeX (regla del propietario del 17-oct: todo fiel al juego en las vistas 3D de Switch y 3DS). Si falta una combinacion, la vista dibuja el aviso
    /// de «recurso ausente» (silueta y numero) y lo anota en los avisos. Let's Go sigue como estaba (X58): lo que no trae cae a PKHeX.
    /// </summary>
    public static bool SinRespaldoDePkhex(string juego) => JuegosVista3D.Contains(juego);

    /// <summary>Id del paquete de una clase para un juego (<c>vista-rojo</c>); su archivo es <c>&lt;id&gt;.pkpack</c>.</summary>
    public static string IdPaquete(string clase, string juego) => clase + "-" + juego;

    /// <summary>Nombre de recurso de una imagen de juego: <c>&lt;clase&gt;_&lt;juego&gt;_&lt;especie:0000&gt;_&lt;forma:00&gt;[_h][_s]</c>.</summary>
    public static string Nombre(string clase, string juego, int especie, int forma, bool hembra, bool variocolor)
        => $"{clase}_{juego}_{especie:0000}_{forma:00}{(hembra ? "_h" : "")}{(variocolor ? "_s" : "")}";

    /// <summary>
    /// Forma de Deoxys que ensena cada juego de Gen. III (<c>SpriteBuilder.GetDeoxysForm</c> de PKHeX): Rojo Fuego, Ataque (1); Verde Hoja, Defensa (2); Esmeralda, Velocidad (3); Rubi y Zafiro (y una partida
    /// de Gen. III sin concretar), la Normal (0).
    /// </summary>
    public static byte DeoxysDeGen3(GameVersion v) => v switch { GameVersion.FR => 1, GameVersion.LG => 2, GameVersion.E => 3, _ => 0 };

    public static bool EsGen3(GameVersion v) => v is GameVersion.R or GameVersion.S or GameVersion.E or GameVersion.RS or GameVersion.RSE or GameVersion.FR or GameVersion.LG or GameVersion.FRLG;

    /// <summary>
    /// Forma que MUESTRA el juego para un Pokemon, que no siempre es la guardada. Gen. I a V, contrastado con el Core de PKHeX: (1) en Gen. III el formato PK3 no guarda la forma de Deoxys (llega siempre con forma 0) y la decide el
    /// juego que lo ensena (<c>SpriteBuilder.GetSprite</c> y <c>HallFame3PKM.DisplayForm</c>); (2) Mothim guarda el manto de su Burmy (forma 1 o 2) pero ensena siempre un solo sprite (<c>SpriteName.SpeciesDefaultFormSprite</c>).
    /// Las demas formas viajan en el Pokemon: Unown de Gen. III se deduce del PID dentro del propio PKHeX y la de Arceus de Gen. IV se guarda con la numeracion del juego, la misma de los paquetes (10 = Fuego).
    /// </summary>
    public static int FormaVisible(GameVersion version, int especie, int forma) => especie switch
    {
        (int)Species.Deoxys when EsGen3(version) => DeoxysDeGen3(version),
        (int)Species.Mothim => 0,
        _ => forma,
    };

    /// <summary>
    /// Imagen de la vista previa del Pokemon en el juego de la partida: la hembra con grafico propio si existe (si no, el comun), la forma EXACTA y el variocolor exacto. Null si el juego no es de Gen. 1-5, si el Pokemon no
    /// existe en ese juego (de una generacion posterior), si la forma o el variocolor no existen o si los paquetes no estan: entonces se usa el sprite de PKHeX.
    /// </summary>
    public static SpriteImage? Vista(IRecursosSprites recursos, GameVersion version, int especie, int forma, bool hembra, bool variocolor, bool huevo)
    {
        if (JuegoDe(version) is not { } juego) return null;
        forma = FormaVisible(version, especie, forma);
        if (huevo) { forma = especie == (int)Species.Manaphy ? 1 : 0; especie = 0; variocolor = false; hembra = false; }
        if (especie < 0 || forma < 0) return null;
        if (hembra && recursos.Obtener(Nombre(ClaseVista, juego, especie, forma, true, variocolor)) is { } h) return h;
        return recursos.Obtener(Nombre(ClaseVista, juego, especie, forma, false, variocolor));
    }

    // ---- X53: iconos de caja del propio juego ----

    /// <summary>
    /// Los siete juegos cuyos iconos de caja se integran (X53), con TODAS sus especies: un juego se integra entero o no se integra, para no mezclar imagenes de fuentes distintas en una misma caja. Gen. 1-2, 6-7, LGPE, PLA, SV, ZA,
    /// Colosseum, XD y Battle Revolution siguen con PKHeX.
    /// </summary>
    public static readonly IReadOnlyList<string> JuegosCaja = ["e", "frlg", "pt", "hgss", "b2w2", "swsh", "bdsp", "pla", "sv", "xy", "oras", "usum", "za", Lgpe];

    /// <summary>
    /// Juego del que salen los iconos de caja de una partida, o null (PKHeX): Rubi, Zafiro y Esmeralda -> e; Rojo Fuego y Verde Hoja -> frlg; Diamante, Perla y Platino -> pt; HeartGold y SoulSilver -> hgss; Negro, Blanco, Negro 2 y
    /// Blanco 2 -> b2w2; Espada y Escudo -> swsh; Diamante Brillante y Perla Reluciente -> bdsp; Leyendas Arceus -> pla; Escarlata y Purpura -> sv (X53b); X e Y -> xy; Rubi Omega y Zafiro Alfa -> oras; Sol, Luna, Ultrasol y Ultraluna -> usum; Leyendas Z-A -> za (X53c).
    /// </summary>
    public static string? JuegoDeCaja(GameVersion v) => v switch
    {
        GameVersion.R or GameVersion.S or GameVersion.E or GameVersion.RS or GameVersion.RSE => "e",
        GameVersion.FR or GameVersion.LG or GameVersion.FRLG => "frlg",
        GameVersion.D or GameVersion.P or GameVersion.Pt or GameVersion.DP => "pt",
        GameVersion.HG or GameVersion.SS or GameVersion.HGSS => "hgss",
        GameVersion.B or GameVersion.W or GameVersion.B2 or GameVersion.W2 or GameVersion.BW or GameVersion.B2W2 => "b2w2",
        GameVersion.SW or GameVersion.SH or GameVersion.SWSH => "swsh",
        GameVersion.BD or GameVersion.SP or GameVersion.BDSP => "bdsp",
        GameVersion.PLA => "pla",
        GameVersion.SL or GameVersion.VL or GameVersion.SV => "sv",
        GameVersion.X or GameVersion.Y or GameVersion.XY => "xy",
        GameVersion.OR or GameVersion.AS or GameVersion.ORAS => "oras",
        GameVersion.SN or GameVersion.MN or GameVersion.US or GameVersion.UM or GameVersion.SM or GameVersion.USUM => "usum",   // USUM trae todas las especies y formas de Sol/Luna
        GameVersion.ZA => "za",
        GameVersion.GP or GameVersion.GE or GameVersion.GG => Lgpe,   // X58
        _ => null,
    };

    /// <summary>
    /// X71: en la CLASE VISTA solo son pixel art los diez juegos clasicos; Let's Go y los ocho de Gen. 6 a 9 son renders 3D.
    /// Los iconos de GBA, DS y Espada/Escudo son pixel art (los de Espada/Escudo vienen ampliados 2x exactos y el generador los guarda a su tamano real): se escalan con factor entero y vecino mas proximo. Los de Diamante Brillante y
    /// Perla Reluciente son ilustraciones suaves (88x88): se reescalan con filtro de calidad.
    /// Let's Go usa pixel art en caja y render suave en la vista previa.
    /// </summary>
    public static bool EsPixelArt(string juego, string clase = ClaseCaja)
        => clase == ClaseVista ? juego != Lgpe && !JuegosVista3D.Contains(juego) : juego is not ("bdsp" or "pla" or "sv" or "za");

    /// <summary>
    /// X58: si el juego, ante una forma que no trae, enseña la forma base DE ESE JUEGO. Let's Go no: sus formas fuera de juego (Totem de Alola, gorras de Pikachu...) no existen en el juego y un icono base las
    /// disfrazaria de otro Pokemon, asi que caen a PKHeX.
    /// </summary>
    public static bool UsaFormaBaseDeRespaldo(string juego) => juego != Lgpe;

    /// <summary>Un icono de caja del juego de la partida y el juego del que sale.</summary>
    public sealed record IconoCaja(SpriteImage Imagen, string Juego);

    /// <summary>
    /// Icono de caja del Pokemon en el juego de la partida (X53). Busca la forma EXACTA (hembra con grafico propio si existe y variocolor si el juego lo trae: Diamante Brillante/Perla Reluciente, Leyendas Arceus y Leyendas Z-A)
    /// y, si el juego no trae esa forma, la forma base DE ESE JUEGO, nunca la de PKHeX. El huevo es el del juego (forma 1 = Manaphy). Null si el juego no esta integrado, el Pokemon no existe en el juego
    /// (o no tiene huevo: Diamante Brillante y Perla Reluciente no traen icono de huevo) o los paquetes no estan: entonces se usa el sprite de PKHeX que ya habia.
    /// </summary>
    public static IconoCaja? Caja(IRecursosSprites recursos, GameVersion version, int especie, int forma, bool hembra, bool variocolor, bool huevo)
    {
        if (JuegoDeCaja(version) is not { } juego) return null;
        forma = FormaVisible(version, especie, forma);
        if (huevo) { forma = especie == (int)Species.Manaphy ? 1 : 0; especie = 0; variocolor = false; hembra = false; }
        if (especie < 0 || forma < 0) return null;
        for (var paso = 0; paso < 2; paso++)
        {
            var f = paso == 0 ? forma : 0;
            if (paso == 1 && (forma == 0 || !UsaFormaBaseDeRespaldo(juego))) break;
            foreach (var (h, s) in new[] { (hembra, variocolor), (false, variocolor), (hembra, false), (false, false) }.Distinct())
                if (recursos.Obtener(Nombre(ClaseCaja, juego, especie, f, h, s)) is { } img) return new IconoCaja(img, juego);
        }
        return null;
    }
}

/// <summary>
/// Todos los paquetes de <see cref="SpritesDeJuego.Carpeta"/> como una sola fuente: uno por manifiesto (vista, y los que sume X49), cada uno verificando al abrir sus paquetes contra su tamano y SHA-256 (un paquete
/// alterado o truncado se rechaza, no se dibuja). Un nombre se busca en todos por orden.
/// </summary>
public sealed class RecursosJuego : IRecursosSprites
{
    private readonly List<RecursosEmpaquetados> _fuentes = [];

    public RecursosJuego(IEnumerable<byte[]> manifiestos, Func<string, byte[]> abrirPaquete, long topeCacheBytes = 24L * 1024 * 1024)
    {
        foreach (var json in manifiestos)
        {
            var m = Manifiesto.Leer(json);
            _fuentes.Add(new RecursosEmpaquetados(m, nombre =>
            {
                var b = abrirPaquete(nombre);
                var p = m.Paquetes.Single(p => p.Archivo == nombre);
                if (b.LongLength != p.Bytes || !string.Equals(Manifiesto.Sha256Hex(b), p.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("El paquete de sprites de juego no coincide con su huella: " + nombre);
                return b;
            }, topeCacheBytes));
        }
    }

    public int Recursos => _fuentes.Sum(f => f.Manifiesto.Recursos.Count);

    /// <summary>X53d: los iconos de ese juego vienen recortados por el lienzo (propiedad del conjunto, anotada en el manifiesto por el generador).</summary>
    public bool EsRecortado(string juego) => _fuentes.Any(f => f.Manifiesto.Recortados.Contains(juego));
    public IReadOnlyList<RecursosEmpaquetados> Fuentes => _fuentes;
    public bool Contiene(string nombre) => _fuentes.Any(f => f.Contiene(nombre));

    public SpriteImage? Obtener(string nombre)
    {
        foreach (var f in _fuentes) if (f.Contiene(nombre)) return f.Obtener(nombre);
        return null;
    }
}
