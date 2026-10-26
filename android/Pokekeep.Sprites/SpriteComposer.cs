// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;

namespace Pokekeep.Sprites;

/// <summary>
/// Compositor de sprites que calca <c>PKHeX.Drawing.PokeSprite</c> (<c>SpriteBuilder</c>, <c>SpriteBuilder5668*</c> y <c>SpriteUtil</c>) sobre <see cref="SpriteImage"/>:
/// base por especie/forma/sexo/brillo con la cadena de respaldo de PKHeX, huevo (como objeto o transparente al 33 %), objeto equipado con su posicion y los «lump» de MT/RT,
/// estrella de brillo, Gigamax, alfa, Lugia oscuro, equipo/bloqueo/starter/compañero, indicador de legalidad, color de encuentro, Teratipo, experiencia y
/// atenuado de filtro. Cada mezcla pasa por <see cref="PixelOps"/>, que reproduce el redondeo de GDI+, de modo que el resultado es IGUAL pixel a pixel
/// (<c>Pokekeep.Sprites.Tests</c> lo comprueba contra PKHeX).
/// <para>Lo devuelto es independiente del llamante salvo que se indique lo contrario; los recursos que entrega <see cref="IRecursosSprites"/> son compartidos y nunca se modifican.</para>
/// </summary>
public sealed partial class SpriteComposer
{
    public const int Ancho = 68;
    public const int Alto = 56;
    private const int ItemShiftX = 2, ItemShiftY = 2, ItemMaxSize = 32, EggItemShiftX = 18, EggItemShiftY = 1;
    private const double UnknownFormTransparency = 0.5, ShinyTransparency = 0.7, EggUnderLayerTransparency = 0.33;
    private const int MaxSlotCount = 30;
    private const int SlotTeamShiftX = Ancho - 19, SlotLockShiftX = Ancho - 14, PartyMarkShiftX = Ancho - 16, FlagIllegalShiftY = Alto - 16;

    private readonly IRecursosSprites _res;
    private readonly object _cierreCache = new();
    private readonly Dictionary<ClaveSprite, LinkedListNode<(ClaveSprite Clave, SpriteImage Img)>> _cache = new();
    private readonly LinkedList<(ClaveSprite Clave, SpriteImage Img)> _lru = new();
    private readonly int _topeCache;
    private GameVersion _version;
    private GameVersion _versionIcono;
    private SpriteImage? _silueta;

    public SpriteComposer(IRecursosSprites recursos, OpcionesSprites? opciones = null, int capacidadCache = 512, GameVersion version = GameVersion.Any)
    {
        _res = recursos; Opciones = opciones ?? new OpcionesSprites(); _topeCache = capacidadCache; _version = version; _versionIcono = version;
    }

    /// <summary>
    /// Fuente de los iconos de caja de cada juego (X53, <see cref="RecursosJuego"/>); null = la casilla usa siempre el sprite de PKHeX. Es una funcion porque los paquetes de juego se cargan (o se cambian) despues de crear el compositor.
    /// Solo la usan <see cref="IconoDeCaja"/> y <see cref="GetSlotCapas"/>: <c>GetSprite</c> y <c>GetSlotSprite</c> siguen calcando a PKHeX pixel a pixel.
    /// </summary>
    public Func<IRecursosSprites?>? FuenteIconos { get; set; }

    /// <summary>
    /// Icono de caja del propio juego de la partida para un Pokemon (X53), o null (PKHeX): solo con el juego de sprites clasico, un juego integrado (<see cref="SpritesDeJuego.JuegoDeCaja"/>) y el Pokemon existente en ese juego.
    /// La forma de Arceus de Gen. IV se traduce igual que en <see cref="GetSprite(ushort, byte, byte, uint, int, bool, Shiny, EntityContext, bool)"/>. Con la hembra de grafico propio y el variocolor solo si el juego los trae.
    /// </summary>
    public SpritesDeJuego.IconoCaja? IconoDeCaja(ushort species, byte form, byte gender, bool shiny, bool isEgg, EntityContext context)
    {
        if (species == 0 || FuenteIconos?.Invoke() is not { } fuente) return null;
        // Solo con el juego de sprites clasico, salvo Leyendas Arceus (mugshot), Escarlata/Purpura y Leyendas Z-A (artwork): su modo SUGERIDO es otro y los iconos del juego son lo que se espera; una eleccion explicita del usuario manda.
        if (Modo != ModoSprites.Clasico && !(Opciones.Preferencia == PreferenciaSprites.Sugerida && SpritesDeJuego.JuegoDeCaja(_versionIcono) is "pla" or "sv" or "za")) return null;
        if (context == EntityContext.Gen4 && species == (int)Species.Arceus) form = ArceusForm4(form);
        return SpritesDeJuego.Caja(fuente, _versionIcono, species, form, gender == 1, shiny, isEgg);
    }

    public OpcionesSprites Opciones { get; }
    public ModoSprites Modo { get; private set; } = ModoSprites.Clasico;

    /// <summary>Se llama (una vez por nombre) cuando falta un recurso que PKHeX tiene. El llamante lo registra como AVISO.</summary>
    public Action<string>? AvisoAusente { get; set; }

    /// <summary>Cambia el juego de sprites (<c>SpriteUtil.ChangeMode</c>) y vacia la cache de composiciones.</summary>
    public void CambiarModo(ModoSprites modo)
    {
        if (modo == Modo) return;
        Modo = modo; VaciarCache();
    }

    /// <summary><c>SpriteUtil.Initialize(sav)</c>: elige el modo segun el guardado y su preferencia y recuerda la version de Gen. III (FR/LG/Esmeralda cambian la forma de Deoxys).</summary>
    public void Iniciar(SaveFile sav)
    {
        if (ModoSpritesRegla.Sugerido(sav, Opciones.Preferencia) is { } m) CambiarModo(m);
        _versionIcono = sav.Version;
        if (sav.Generation != 3) return;
        var v = sav.Version;
        if (v == GameVersion.FRLG) v = ReferenceEquals(sav.Personal, PersonalTable.FR) ? GameVersion.FR : GameVersion.LG;
        if (v != _version) { _version = v; VaciarCache(); }
    }

    /// <summary>Tras cambiar <see cref="Opciones"/>: las composiciones guardadas ya no valen.</summary>
    public void VaciarCache() { lock (_cierreCache) { _cache.Clear(); _lru.Clear(); } }

    public int ComposicionesEnCache { get { lock (_cierreCache) return _cache.Count; } }

    // ---- Nombres segun el modo (SpriteBuilder5668s / 5668a / 5668c) ----
    private char Primario => Modo switch { ModoSprites.Artwork => 'a', ModoSprites.Mugshot => 'c', _ => 'b' };
    private char Secundario => Modo switch { ModoSprites.Artwork => 'b', ModoSprites.Mugshot => 'b', _ => 'c' };
    private string NombreSoloEspecie(ushort species) => $"{Primario}_{species}";
    private string NombreTodo(char prefijo, ushort species, byte form, byte gender, uint formarg, bool shiny, EntityContext context)
        => prefijo + SpriteName.GetResourceStringSprite(species, form, gender, formarg, context, shiny, Opciones.SpritesBrillantes);
    private string NombreObjeto(int item) => (Modo == ModoSprites.Artwork ? 'a' : 'b') + $"item_{item}";
    private string NombreHuevo(ushort species) => species == (int)Species.Manaphy ? (Modo == ModoSprites.Artwork ? "a_490_e" : "b_490_e") : (Modo == ModoSprites.Artwork ? "a_egg" : "b_egg");
    private string NombreTm => Modo == ModoSprites.Artwork ? "aitem_tm" : "bitem_tm";
    private string NombreTr => "bitem_tr";

    private SpriteImage? Recurso(string nombre) => _res.Obtener(nombre);

    /// <summary>Recurso que PKHeX da por seguro (Resources.b_unknown, b_egg...): si falta, se avisa y se sigue sin ese detalle.</summary>
    private SpriteImage? Imprescindible(string nombre)
    {
        var r = Recurso(nombre);
        if (r is null) Avisar(nombre);
        return r;
    }

    private readonly HashSet<string> _avisados = new(StringComparer.Ordinal);

    private void Avisar(string nombre)
    {
        bool nuevo;
        lock (_avisados) nuevo = _avisados.Add(nombre);
        if (nuevo) AvisoAusente?.Invoke(nombre);
    }

    /// <summary>Recursos que faltaron y se avisaron (una vez cada uno).</summary>
    public IReadOnlyCollection<string> Avisados { get { lock (_avisados) return _avisados.ToArray(); } }

    private SpriteImage Silueta
    {
        get
        {
            if (_silueta is not null) return _silueta;
            // Silueta neutra (elipse gris con la mitad de opacidad): solo se usa si falta incluso b_unknown, nunca con un APK completo.
            var img = new SpriteImage(Ancho, Alto);
            double cx = (Ancho - 1) / 2.0, cy = (Alto - 1) / 2.0, rx = Ancho * 0.30, ry = Alto * 0.40;
            for (var y = 0; y < Alto; y++)
                for (var x = 0; x < Ancho; x++)
                {
                    var dx = (x - cx) / rx; var dy = (y - cy) / ry;
                    if (dx * dx + dy * dy > 1) continue;
                    var o = (y * Ancho + x) * 4;
                    img.Pixels[o] = img.Pixels[o + 1] = img.Pixels[o + 2] = 128; img.Pixels[o + 3] = 160;
                }
            return _silueta = img;
        }
    }

    /// <summary>Nombre del recurso que se usara como base (o null si no hay ninguno y sale la silueta), y si hay que avisar de que falta el exacto.</summary>
    public string? NombreRecursoBase(ushort species, byte form, byte gender, uint formarg, bool shiny, EntityContext context, bool grande = false)
    {
        if (species == 0) return "b_0";
        var (nombre, _) = ResolverBase(species, form, gender, formarg, shiny, context, grande);
        return nombre;
    }

    private (string? Nombre, SpriteImage Img) ResolverBase(ushort species, byte form, byte gender, uint formarg, bool shiny, EntityContext context, bool grande = false)
    {
        // Vista ampliada (ficha, editor): en el juego de sprites de artwork se prefiere SIEMPRE la imagen grande. PKHeX, si no hay artwork brillante (solo hay 418 de 1.487), cae al sprite pequeno brillante
        // de la «caja»; aqui se usa antes el artwork normal (la estrella de brillo que pone GetSprite sigue marcando que es variocolor). La casilla de la caja no usa esto: sigue igual que PKHeX.
        if (grande && Modo == ModoSprites.Artwork && !FormInfo.IsTotemForm(species, form, context))
        {
            // Un brillante NUNCA se ve con colores normales si existe cualquier imagen brillante (uso real 2): artwork brillante -> ilustracion «c_» brillante -> (sigue abajo) sprite pequeno brillante, que la vista
            // amplia con el escalador de pixel art. Solo sin ninguna imagen brillante se usa la normal. Sin brillo: artwork -> «c_» -> sprite pequeno.
            var (ng, bg) = ImagenPorDefecto(species, form, gender, formarg, shiny, context, soloPrimario: true);
            if (bg is not null) return (ng, bg);
            foreach (var brillo in shiny ? new[] { true } : new[] { false })
            {
                ng = NombreTodo('c', species, form, gender, formarg, brillo, context);
                if (Recurso(ng) is { } c) return (ng, c);
            }
            if (shiny)
            {
                // Sprite pequeno brillante (primario y secundario) antes que cualquier imagen normal.
                var (nb, bb) = ImagenPorDefecto(species, form, gender, formarg, true, context);
                if (bb is not null && nb != NombreTodo(Primario, species, form, gender, formarg, false, context) && nb != NombreTodo(Secundario, species, form, gender, formarg, false, context)) return (nb, bb);
                // Ninguna imagen brillante: la normal (artwork, «c_»), y la estrella marca el variocolor.
                (ng, bg) = ImagenPorDefecto(species, form, gender, formarg, false, context, soloPrimario: true);
                if (bg is not null) return (ng, bg);
                ng = NombreTodo('c', species, form, gender, formarg, false, context);
                if (Recurso(ng) is { } c2) return (ng, c2);
            }
        }
        // GetBaseImage: totem -> base + halo; el resto, el recurso primario y el secundario; si no, sin brillo, sin forma y por ultimo Unknown.
        if (FormInfo.IsTotemForm(species, form, context))
        {
            var baseform = FormInfo.GetTotemBaseForm(species, form);
            var (n0, b0) = ImagenPorDefecto(species, baseform, gender, formarg, shiny, context);
            if (b0 is not null)
            {
                var halo = Halo(b0, 0, 165, 255, true);
                return (n0, PixelOps.Layer(b0, halo, 0, 0));
            }
        }
        else
        {
            var (n, b) = ImagenPorDefecto(species, form, gender, formarg, shiny, context);
            if (b is not null) return (n, b);
        }
        if (shiny)
        {
            var (n, b) = ImagenPorDefecto(species, form, gender, formarg, false, context);
            if (b is not null) return (n, b);
        }
        var soloEspecie = NombreSoloEspecie(species);
        var baseImage = Recurso(soloEspecie);
        if (baseImage is null) Avisar(NombreTodo(Primario, species, form, gender, formarg, shiny, context)); // sin recurso para esta clave: sale Unknown (como PKHeX)
        var unknown = Imprescindible("b_unknown");
        if (baseImage is null) return unknown is null ? (null, Silueta) : ("b_unknown", unknown);
        return unknown is null ? (soloEspecie, baseImage) : (soloEspecie, PixelOps.Layer(baseImage, unknown, 0, 0, UnknownFormTransparency));
    }

    private (string? Nombre, SpriteImage? Img) ImagenPorDefecto(ushort species, byte form, byte gender, uint formarg, bool shiny, EntityContext context, bool soloPrimario = false)
    {
        var n = NombreTodo(Primario, species, form, gender, formarg, shiny, context);
        var r = Recurso(n);
        if (r is null && !soloPrimario)
        {
            n = NombreTodo(Secundario, species, form, gender, formarg, shiny, context);
            r = Recurso(n);
        }
        return r is null ? (null, null) : (n, r);
    }

    private static byte DeoxysForm(GameVersion version) => SpritesDeJuego.DeoxysDeGen3(version);

    private static byte ArceusForm4(byte form) => form switch
    {
        > 9 => --form,
        9 => byte.MaxValue,
        _ => form,
    };

    // ---- API publica: sprite de una especie ----

    /// <summary><c>SpriteUtil.GetSprite(species, form, gender, formarg, item, isegg, shiny, context)</c>.</summary>
    public SpriteImage GetSprite(ushort species, byte form, byte gender, uint formarg, int heldItem, bool isEgg, Shiny shiny = Shiny.Never, EntityContext context = EntityContext.None, bool grande = false)
    {
        if (species == 0) return Recurso("b_0") ?? Silueta;
        var clave = new ClaveSprite(Modo, _version, species, form, gender, formarg, heldItem, isEgg, shiny, context, grande);
        lock (_cierreCache)
            if (_cache.TryGetValue(clave, out var nodo)) { _lru.Remove(nodo); _lru.AddFirst(nodo); return nodo.Value.Img; }

        if (context == EntityContext.Gen3 && species == (int)Species.Deoxys) form = DeoxysForm(_version);
        else if (context == EntityContext.Gen4 && species == (int)Species.Arceus) form = ArceusForm4(form);

        var (_, baseImage) = ResolverBase(species, form, gender, formarg, shiny.IsShiny(), context, grande);
        var img = GetSprite(baseImage, species, heldItem, isEgg, shiny, context);

        lock (_cierreCache)
        {
            if (!_cache.ContainsKey(clave))
            {
                _cache[clave] = _lru.AddFirst((clave, img));
                while (_cache.Count > _topeCache) { var u = _lru.Last!; _lru.RemoveLast(); _cache.Remove(u.Value.Clave); }
            }
        }
        return img;
    }

    /// <summary><c>SpriteBuilder.GetSprite(Bitmap baseSprite, ...)</c>: capas de huevo, objeto y brillo sobre una base ya resuelta. NO modifica <paramref name="baseSprite"/>.</summary>
    public SpriteImage GetSprite(SpriteImage baseSprite, ushort species, int heldItem, bool isEgg, Shiny shiny, EntityContext context = EntityContext.None)
    {
        if (isEgg) baseSprite = CapaHuevo(baseSprite, species, heldItem != 0);
        if (heldItem > 0) baseSprite = CapaObjeto(baseSprite, heldItem, context);
        if (shiny.IsShiny())
        {
            if (shiny == Shiny.AlwaysSquare && !context.IsSquareShinyDifferentiated) shiny = Shiny.Always;
            baseSprite = CapaBrillo(baseSprite, shiny);
        }
        return baseSprite;
    }

    private SpriteImage CapaObjeto(SpriteImage baseImage, int item, EntityContext context)
    {
        var itemimg = GetItemSprite(item, context);
        if (itemimg is null) return baseImage;
        // Abajo a la derecha; las imagenes vienen recortadas, asi que se separan un poco del borde.
        var x = baseImage.Width - itemimg.Width - ((ItemMaxSize - itemimg.Width) / 4) - ItemShiftX;
        var y = baseImage.Height - itemimg.Height - ItemShiftY;
        return PixelOps.Layer(baseImage, itemimg, x, y);
    }

    /// <summary><c>SpriteBuilder.GetItemSprite</c>: sprite del objeto del modo actual; las MT/MR usan una imagen comun («lump»); desconocido = <c>bitem_unk</c>.</summary>
    public SpriteImage? GetItemSprite(int item, EntityContext context)
    {
        var lump = HeldItemLumpUtil.GetIsLump(item, context);
        return lump switch
        {
            HeldItemLumpImage.TechnicalMachine => Imprescindible(NombreTm),
            HeldItemLumpImage.TechnicalRecord => Imprescindible(NombreTr),
            _ => Recurso(NombreObjeto(item)) ?? ObjetoAdicional(item, context) ?? Imprescindible("bitem_unk"),
        };
    }

    private SpriteImage CapaBrillo(SpriteImage baseImage, Shiny shiny)
    {
        if (!Opciones.EstrellaBrillo) return baseImage;
        var rare = Imprescindible(shiny is Shiny.AlwaysSquare ? "rare_icon_alt_2" : "rare_icon_alt");
        return rare is null ? baseImage : PixelOps.Layer(baseImage, rare, 0, 0, ShinyTransparency);
    }

    private SpriteImage CapaHuevo(SpriteImage baseImage, ushort species, bool hasItem)
    {
        var egg = Imprescindible(NombreHuevo(species));
        if (egg is null) return baseImage;
        if (Opciones.HuevoComoObjeto && !hasItem)
            return PixelOps.Layer(baseImage, egg, EggItemShiftX, EggItemShiftY); // parecido a un objeto, que no pueden llevar
        // Especie semitransparente con el huevo opaco encima. PKHeX atenua la base EN EL SITIO (aqui la base es compartida: se clona).
        var tenue = baseImage.Clone();
        PixelOps.ChangeOpacity(tenue, EggUnderLayerTransparency);
        return PixelOps.Layer(tenue, egg, 0, 0);
    }

    // ---- Halo (Lugia oscuro, formas totem) ----

    /// <summary>
    /// <c>SpriteUtil.GetSpriteGlow(Bitmap, ...)</c>: solo el halo (con <paramref name="soloHalo"/> se quitan los pixeles opacos del original, dejando unicamente el brillo).
    /// Devuelve una imagen nueva del tamaño de la base.
    /// </summary>
    public static SpriteImage Halo(SpriteImage baseSprite, byte blue, byte green, byte red, bool soloHalo)
    {
        var pixels = (byte[])baseSprite.Pixels.Clone();
        if (!soloHalo)
        {
            PixelOps.GlowEdges(pixels, blue, green, red, baseSprite.Width);
            return new SpriteImage(baseSprite.Width, baseSprite.Height, pixels);
        }
        var original = (byte[])pixels.Clone();
        PixelOps.SetAllUsedPixelsOpaque(pixels);
        PixelOps.GlowEdges(pixels, blue, green, red, baseSprite.Width);
        PixelOps.RemovePixels(pixels, original);
        return new SpriteImage(baseSprite.Width, baseSprite.Height, pixels);
    }

    /// <summary><c>SpriteUtil.GetSpriteGlow(PKM, ...)</c>: halo del Pokemon (sin objeto), hueco si es huevo.</summary>
    public SpriteImage Halo(PKM pk, byte blue, byte green, byte red, bool forceHollow = false)
    {
        var egg = pk.IsEgg;
        var formarg = pk is IFormArgument f ? f.FormArgument : 0;
        var shiny = pk.IsShiny ? Shiny.Always : Shiny.Never;
        var baseSprite = GetSprite(pk.Species, pk.Form, pk.Gender, formarg, 0, egg, shiny, pk.Context);
        return Halo(baseSprite, blue, green, red, forceHollow || egg);
    }

    // ---- Sprite de una entidad (SpriteUtil.GetSprite(PKM)) ----

    /// <summary><c>pk.Sprite()</c>: sprite de la entidad con Lugia oscuro, Gigamax y alfa.</summary>
    public SpriteImage GetSprite(PKM pk)
    {
        var formarg = pk is IFormArgument f ? f.FormArgument : 0;
        var shiny = ShinyExtensions.GetType(pk);

        var img = GetSprite(pk.Species, pk.Form, pk.Gender, formarg, pk.SpriteItem, pk.IsEgg, shiny, pk.Context);
        if (pk is IShadowCapture { IsShadow: true })
        {
            const ushort Lugia = (int)Species.Lugia;
            if (pk.Species is Lugia && Recurso("b_249x") is { } sombra) // el sprite del Lugia de XD
                img = GetSprite(sombra, Lugia, pk.SpriteItem, pk.IsEgg, shiny, pk.Context);
            var glow = Halo(pk, 75, 0, 130, true);
            return PixelOps.Layer(glow, img, 0, 0);
        }
        if (pk is IGigantamaxReadOnly { CanGigantamax: true } && Imprescindible("dyna") is { } gm)
            return PixelOps.Layer(img, gm, (img.Width - gm.Width) / 2, 0);
        if (pk is IAlphaReadOnly { IsAlpha: true } && Imprescindible("alpha_alt") is { } alpha)
            return PixelOps.Layer(img, alpha, SlotTeamShiftX, 0);
        return img;
    }

    // ---- Sprite de una casilla (SpriteUtil.Sprite(this PKM, sav, box, slot, visibility, storage)) ----

    /// <summary>
    /// <c>pk.Sprite(sav, box, slot, visibility, storage)</c>: sprite de la casilla con Teratipo, indicador de legalidad (si <c>CheckLegalityIndicate</c>), color de encuentro,
    /// marcas de equipo/bloqueo/compañero/inicial, experiencia y atenuado de filtro. Siempre devuelve una imagen nueva que el llamante puede modificar.
    /// <paramref name="analisis"/>: legalidad ya calculada (evita repetirla); si es null y la casilla lo pide, se calcula aqui bajo <see cref="LegalityGate"/>.
    /// </summary>
    public SpriteImage GetSlotSprite(PKM pk, SaveFile sav, int box = -1, int slot = -1, SlotVisibilityType visibility = SlotVisibilityType.None,
        StorageSlotType storage = StorageSlotType.None, LegalityAnalysis? analisis = null)
    {
        var resultado = Casilla(pk, sav, box, slot, visibility, storage, analisis);
        if (visibility.HasFlag(SlotVisibilityType.FilterMismatch))
        {
            // Se desvanece el sprite.
            PixelOps.ToGrayscale(resultado, Opciones.GrisFiltroNoCoincide);
            PixelOps.ChangeOpacity(resultado, Opciones.OpacidadFiltroNoCoincide);
        }
        return resultado;
    }

    private SpriteImage Casilla(PKM pk, SaveFile sav, int box, int slot, SlotVisibilityType visibility, StorageSlotType storage, LegalityAnalysis? analisis)
    {
        var inBox = (uint)slot < MaxSlotCount;
        var empty = pk.Species == 0;
        // El sprite base puede ser compartido (cache o recurso): siempre se trabaja sobre una copia exacta de los bytes, nunca sobre el original.
        var sprite = empty ? (Recurso("b_0") ?? Silueta).Clone() : GetSprite(pk).Clone();

        if (!empty)
        {
            if (Opciones.TipoTera != FondoSprite.Ninguno && pk is ITeraType t)
            {
                var type = t.TeraType;
                if (TeraTypeUtil.IsOverrideValid((byte)type))
                    AplicarColor(sprite, Opciones.TipoTera, ColorTipo.DeTera((byte)type), Opciones.GrosorFranjaTera, Opciones.OpacidadFranjaTera, Opciones.OpacidadFondoTera);
            }
            if (visibility.HasFlag(SlotVisibilityType.CheckLegalityIndicate))
            {
                var la = analisis ?? Analizar(pk, sav, storage);
                if (!la.Valid)
                    sprite = Superponer(sprite, "warn", 0, FlagIllegalShiftY);
                else if (pk.Format >= 8 && MoveInfo.IsDummiedMoveAny(pk))
                    sprite = Superponer(sprite, "hint", 0, FlagIllegalShiftY);

                if (Opciones.ColorEncuentroPkm != FondoSprite.Ninguno)
                    AplicarColorEncuentro(la.EncounterOriginal, sprite, Opciones.ColorEncuentroPkm);

                if (Opciones.PorcentajeExperiencia)
                    AplicarExperiencia(pk, sprite, la.EncounterMatch);
            }
        }
        if (inBox)
        {
            var flags = sav.GetBoxSlotFlags(box, slot);

            // Equipos de combate de la caja y su bloqueo.
            var team = flags.IsBattleTeam();
            if (team >= 0) sprite = Superponer(sprite, "team", SlotTeamShiftX, 0);
            if (flags.HasFlag(StorageSlotSource.Locked)) sprite = Superponer(sprite, "locked", SlotLockShiftX, 0);

            // Algunos juegos guardan el equipo dentro de la lista de Pokemon (LGP/E).
            var party = flags.IsParty();
            if (party >= 0) sprite = Superponer(sprite, $"party{party + 1}", PartyMarkShiftX, 0);
            if (flags.HasFlag(StorageSlotSource.Starter)) sprite = Superponer(sprite, "starter", 0, 0);
        }

        if (Opciones.PorcentajeExperiencia && !visibility.HasFlag(SlotVisibilityType.CheckLegalityIndicate))
            AplicarExperiencia(pk, sprite);

        return sprite;
    }

    private SpriteImage Superponer(SpriteImage sprite, string recurso, int x, int y)
    {
        var o = Imprescindible(recurso);
        return o is null ? sprite : PixelOps.Layer(sprite, o, x, y);
    }

    /// <summary>La legalidad se calcula bajo <see cref="LegalityGate"/>: <c>ParseSettings</c> del Core es estatico y no admite dos analisis a la vez.</summary>
    public static LegalityAnalysis Analizar(PKM pk, SaveFile sav, StorageSlotType storage)
    {
        lock (LegalityGate.Cerrojo)
            return pk.GetType() == sav.PKMType
                ? new LegalityAnalysis(pk, sav.Personal, storage)
                : new LegalityAnalysis(pk, pk.PersonalInfo, storage);
    }

    // ---- Colores (franjas y fondos) ----

    /// <summary><c>SpriteUtil.ApplyEncounterColor</c>: color propio de cada tipo de plantilla de encuentro. Modifica <paramref name="img"/>.</summary>
    public void AplicarColorEncuentro(IEncounterTemplate enc, SpriteImage img, FondoSprite type)
    {
        var index = Opciones.HashTipoEncuentro(enc.GetType().Name) * 0x43FD43FD;
        AplicarColor(img, type, Rgb.DeArgb(index), Opciones.GrosorFranjaEncuentro, Opciones.OpacidadFranjaEncuentro, Opciones.OpacidadFondoEncuentro);
    }

    private static void AplicarColor(SpriteImage img, FondoSprite type, Rgb c, int thick, byte opacStripe, byte opacBack)
    {
        if (type == FondoSprite.FranjaInferior)
        {
            var stripeHeight = thick;
            if ((uint)stripeHeight > img.Height) stripeHeight = img.Height; // negativos y excesos se recortan al alto
            PixelOps.BlendTransparentTo(img, c.R, c.G, c.B, opacStripe, img.Width * 4 * (img.Height - stripeHeight));
        }
        else if (type == FondoSprite.FranjaSuperior)
        {
            var stripeHeight = thick;
            if ((uint)stripeHeight > img.Height) stripeHeight = img.Height;
            PixelOps.BlendTransparentTo(img, c.R, c.G, c.B, opacStripe, 0, img.Width * 4 * stripeHeight);
        }
        else if (type == FondoSprite.FondoCompleto)
        {
            PixelOps.ChangeTransparentTo(img, c.R, c.G, c.B, opacBack);
        }
    }

    private void AplicarExperiencia(PKM pk, SpriteImage img, IEncounterTemplate? enc = null)
    {
        const int bpp = 4;
        var start = bpp * Ancho * (Alto - 1);
        var level = pk.CurrentLevel;
        if (level == Experience.MaxLevel)
        {
            PixelOps.WritePixels(img, 0, 255, 0, start, start + (Ancho * bpp)); // Lime
            return;
        }
        var pct = Experience.GetEXPToLevelUpPercentage(level, pk.EXP, pk.PersonalInfo.EXPGrowth);
        if (pct is not 0)
        {
            PixelOps.WritePixels(img, 30, 144, 255, start, start + (int)(Ancho * pct * bpp)); // DodgerBlue
            return;
        }
        var encLevel = enc is { IsEgg: true } ? enc.LevelMin : pk.MetLevel;
        if (level != encLevel && pk.HasOriginalMetLocation) PixelOps.WritePixels(img, 255, 140, 0, start, start + (Ancho * bpp)); // DarkOrange
        else PixelOps.WritePixels(img, 255, 255, 0, start, start + (Ancho * bpp)); // Yellow
    }

    // ---- Plantillas de encuentro y regalos (SpriteUtil.Sprite(IEncounterTemplate)) ----

    public static byte GetDisplayGender(IEncounterTemplate enc) => enc switch
    {
        IFixedGender { IsFixedGender: true } s => Math.Max((byte)0, s.Gender),
        IPogoSlot g => (byte)((int)g.Gender & 1),
        _ => 0,
    };

    /// <summary>Sprite de una plantilla de encuentro (bola obligatoria, Gigamax, alfa y color del tipo de encuentro).</summary>
    public SpriteImage GetSprite(IEncounterTemplate enc)
    {
        if (enc is MysteryGift g) return GetMysteryGiftPreviewPoke(g);
        var gender = GetDisplayGender(enc);
        var shiny = enc.IsShiny ? Shiny.Always : Shiny.Never;
        var img = GetSprite(enc.Species, enc.Form, gender, 0, 0, enc.IsEgg, shiny, enc.Context);
        if (Opciones.BolaDeEncuentro && enc is { FixedBall: not Ball.None }) img = ConBola(img, (byte)enc.FixedBall);
        if (enc is IGigantamaxReadOnly { CanGigantamax: true } && Imprescindible("dyna") is { } gm)
            img = PixelOps.Layer(img, gm, (img.Width - gm.Width) / 2, 0);
        if (enc is IAlphaReadOnly { IsAlpha: true } && Imprescindible("alpha_alt") is { } alpha)
            img = PixelOps.Layer(img, alpha, SlotTeamShiftX, 0);
        if (Opciones.ColorEncuentro != FondoSprite.Ninguno)
        {
            img = img.Clone(); // el color se pinta en el sitio y la imagen puede venir de la cache
            AplicarColorEncuentro(enc, img, Opciones.ColorEncuentro);
        }
        return img;
    }

    public SpriteImage GetMysteryGiftPreviewPoke(MysteryGift gift)
    {
        if (gift is { IsEgg: true, Species: (int)Species.Manaphy })
            return GetSprite((int)Species.Manaphy, 0, 2, 0, 0, true, Shiny.Never, gift.Context);

        var gender = Math.Max((byte)0, gift.Gender);
        var img = GetSprite(gift.Species, gift.Form, gender, 0, gift.HeldItem, gift.IsEgg, gift.IsShiny ? Shiny.Always : Shiny.Never, gift.Context);
        if (Opciones.BolaDeEncuentro && gift is { FixedBall: not Ball.None }) img = ConBola(img, (byte)gift.FixedBall);
        if (gift is IGigantamaxReadOnly { CanGigantamax: true } && Imprescindible("dyna") is { } gm)
            img = PixelOps.Layer(img, gm, (img.Width - gm.Width) / 2, 0);
        return img;
    }

    private SpriteImage ConBola(SpriteImage img, byte ball)
    {
        var bola = GetBallSprite(ball);
        return bola is null ? img : PixelOps.Layer(img, bola, 0, img.Height - bola.Height);
    }

    /// <summary><c>SpriteUtil.GetBallSprite</c>: la bola, o la Poké Ball (<c>_ball4</c>) si no existe.</summary>
    public SpriteImage? GetBallSprite(byte ball) => Recurso(SpriteName.GetResourceStringBall(ball)) ?? Imprescindible("_ball4");

    /// <summary>Sprite de objeto de la mochila/inventario (<c>bitem_N</c>), tal cual y sin composicion. Null si no existe.</summary>
    public SpriteImage? GetItemSprite(int item) => Recurso($"bitem_{item}");

    /// <summary>
    /// Icono de un objeto de la mochila o de la ficha, con el id que se ENSEÑA (ya modernizado, <c>ItemConverter.GetItemDisplay</c>): las MT/MO/RT con su icono comun («lump», como PKHeX), el sprite pequeno <c>bitem_N</c> y, si no
    /// existe, el de artwork <c>aitem_N</c>. Null si ningun paquete trae arte para ese objeto (PKHeX tampoco: pone el «?» <c>bitem_unk</c>; la interfaz dibuja su propio glifo).
    /// </summary>
    public SpriteImage? GetItemSpriteVisible(int itemDisplay, EntityContext context)
    {
        if (itemDisplay <= 0) return null;
        var lump = HeldItemLumpUtil.GetIsLump(itemDisplay, context);
        if (lump == HeldItemLumpImage.TechnicalMachine) return Recurso("bitem_tm");
        if (lump == HeldItemLumpImage.TechnicalRecord) return Recurso("bitem_tr");
        return Recurso($"bitem_{itemDisplay}") ?? Recurso($"aitem_{itemDisplay}");
    }

    /// <summary>Lo mismo con el id NATIVO del formato (el de la mochila de Gen. I-III), que se traduce primero con <c>ItemConverter.GetItemDisplay</c>.</summary>
    public SpriteImage? GetItemSpriteBolsa(int itemNativo, EntityContext context) => GetItemSpriteVisible(ItemConverter.GetItemDisplay(itemNativo, context), context);
    public SpriteImage? GetItemSpriteA(int item) => Recurso($"aitem_{item}");

    /// <summary>Nombre del recurso del icono de estado (<c>StatusCondition</c>), o null si no hay.</summary>
    public static string? NombreEstado(StatusCondition value)
    {
        if (value == 0) return null;
        if (value < StatusCondition.Poison) return "sicksleep";
        if (value.HasFlag(StatusCondition.PoisonBad)) return "sicktoxic";
        if (value.HasFlag(StatusCondition.Poison)) return "sickpoison";
        if (value.HasFlag(StatusCondition.Burn)) return "sickburn";
        if (value.HasFlag(StatusCondition.Paralysis)) return "sickparalyze";
        if (value.HasFlag(StatusCondition.Freeze)) return "sickfrostbite";
        return null;
    }

    /// <summary>Indicador de legalidad suelto (<c>valid</c> o <c>warn</c>).</summary>
    public SpriteImage? GetLegalIndicator(bool valid) => Imprescindible(valid ? "valid" : "warn");

    private readonly record struct ClaveSprite(ModoSprites Modo, GameVersion Version, ushort Species, byte Form, byte Gender, uint FormArg, int Item, bool Egg, Shiny Shiny, EntityContext Context, bool Grande = false);
}

/// <summary>Cerrojo unico del analisis de legalidad (el estado de <c>ParseSettings</c> del Core es estatico). Todo calculo de legalidad de la interfaz pasa por aqui, tambien el de los iconos de casilla.</summary>
public static class LegalityGate
{
    public static readonly object Cerrojo = new();
}
