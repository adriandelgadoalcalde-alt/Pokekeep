// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Buffers.Binary;
using PKHeX.Core;
using Pokekeep.Sprites;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen5;

/// <summary>
/// PK-SAV-061 y PK-SAV-079 a PK-SAV-087 · <c>SAV_DLC5</c> (+ <c>CGearImage</c>), la E/S de contenido descargable de Negro/Blanco y Negro 2/Blanco 2. Es un formulario de archivos: cada pestaña importa y exporta un bloque de datos descargado
/// y el original actúa sobre su copia del guardado en cuanto se elige el archivo (se guarda con «Guardar»). Aquí cada importación queda pendiente hasta «Aplicar» (campo de archivo) y exportar saca siempre lo que hay en la PARTIDA,
/// no el borrador. Pestañas: «Apariencia C-Gear» (importar PNG de 256×192 de 32 bits o archivo .psk/.cgb, exportar ambos, con vista previa del resultado), «Apariencia Pokédex» (importar/exportar .pds; los botones de imagen de fondo
/// y primer plano de la ventana original no tienen manejador en PKHeX y no existen aquí), «Musical» (.pms; toma el nombre del archivo, como <c>GetImportedMusicalName</c>), «Vídeos de combate» (4 ranuras; importar cifra y firma,
/// exportar y exportar sin cifrar), «Pokéwood» y «PWT» (solo Negro 2/Blanco 2: 8 películas y 3 torneos), «Nexo Recuerdo» (2 ranuras) y «Test de combate» (que PKHeX marca como «no funciona, hace falta investigarlo»).
/// Reglas calcadas: los tamaños admitidos (el exacto y, en PWT, Pokédex y Musical, el del programa original de 4 bytes menos que se rellena con ceros); un PNG tiene que ser exactamente 256×192 en 32 bits ARGB; el .cgb de un
/// juego se convierte al formato del otro (desplazamiento de paleta). Divergencias (ADR 0062): el original escribe un PNG con demasiados colores o teselas y solo avisa; aquí se rechaza antes de escribir. Un PNG entrelazado
/// no se admite. Las etiquetas «Cargar imagen / Guardar imagen» de fondo y primer plano de la Pokédex no se portan porque no hacen nada en PKHeX.
/// </summary>
internal static class ContenidoDescargable
{
    public const string Form = "SAV_DLC5";
    public static readonly string[] Censo = ["PK-SAV-061", "PK-SAV-079", "PK-SAV-080", "PK-SAV-081", "PK-SAV-082", "PK-SAV-083", "PK-SAV-084", "PK-SAV-085", "PK-SAV-086", "PK-SAV-087"];

    private static string L(string control) => EtiquetasLang.Requerir(Form, control);
    private static SAV5 S(ContextoP c) => (SAV5)c.Sav;

    /// <summary>Predicado barato de <see cref="Crear"/>: el editor existe para este guardado.</summary>
    public static bool Aplica(SaveFile sav) => sav is SAV5;

    // ---------------- ayudas de archivos ----------------
    private static string Vacio => IdiomaInterfaz.T("Form.Fama1.Vacio");

    /// <summary>Valida el tamaño como <c>ImportFile</c>: el exacto o, si lo hay, el del programa original (<paramref name="otro"/>), que luego se rellena con ceros hasta el exacto.</summary>
    private static Func<ContextoP, byte[], string?> Tamano(Func<ContextoP, int> esperado, int otro = -1) => (c, b) =>
    {
        var n = esperado(c);
        return b.Length == n || b.Length == otro ? null : IdiomaInterfaz.F("ContenidoDescargable.TamanoNoValido", n, otro > 0 ? IdiomaInterfaz.F("ContenidoDescargable.OTambien", otro) : string.Empty, b.Length);
    };

    private static byte[] Ajustar(byte[] b, int esperado) { var r = (byte[])b.Clone(); if (r.Length != esperado) Array.Resize(ref r, esperado); return r; }

    // ---------------- C-Gear ----------------
    private static CGearBackground CGear(SaveFile sav, Memory<byte> datos) => sav is SAV5BW ? new CGearBackgroundBW(datos) : new CGearBackgroundB2W2(datos);
    private static string ExtensionCGear(SaveFile sav) => sav is SAV5BW ? CGearBackgroundBW.Extension : CGearBackgroundB2W2.Extension;
    private static int CuentaColores<T>() where T : ITiledImage => T.ColorCount;
    private static int CuentaTeselas<T>() where T : ITiledImage => T.TilePoolCount;
    private static readonly byte[] FirmaPng = [137, 80, 78, 71, 13, 10, 26, 10];

    /// <summary>La skin actual con la conversión de la importación pendiente (un PNG o un .cgb) aplicada, sobre una copia: lo que verá el juego.</summary>
    private static CGearBackground CGearResultante(ContextoP c, out string? problema)
    {
        problema = null;
        var copia = CGear(c.Sav, S(c).CGearSkinData.ToArray());
        if (c.Valor("cgear-png") is byte[] png) problema = AplicarPng(copia, png) ?? problema;
        if (c.Valor("cgear-cgb") is byte[] cgb) problema = AplicarCgb(c.Sav, copia, cgb) ?? problema;
        return copia;
    }

    /// <summary>Convierte un PNG a la skin (<c>CGearImage.GetCGearBackground</c>). Devuelve el motivo de rechazo o null.</summary>
    private static string? AplicarPng(CGearBackground bg, byte[] png)
    {
        if (png.Length < 33 || !png.AsSpan(0, 8).SequenceEqual(FirmaPng)) return IdiomaInterfaz.T("ContenidoDescargable.ArchivoPNG");
        var ancho = (int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(16)); var alto = (int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(20));
        if (ancho != CGearBackground.Width || alto != CGearBackground.Height) return IdiomaInterfaz.F("ContenidoDescargable.DimensionesImagenIncorrectasEsperan", CGearBackground.Width, CGearBackground.Height);
        if (png[24] != 8 || png[25] != 6) return IdiomaInterfaz.T("ContenidoDescargable.FormatoImagenIncorrectoEspera");
        SpriteImage img;
        try { img = PngLite.Decodificar(png); }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or EndOfStreamException) { return IdiomaInterfaz.F("ContenidoDescargable.PodidoLeerImagen", ex.Message); }
        var res = bg.SetImageData(img.Pixels);
        // La cuenta de colores que devuelve el Core se corta en 16 (nunca la supera: PKHeX tampoco avisa de eso), pero se comprueba como el formulario.
        var colores = res.ColorCount > CuentaColores<CGearBackground>(); var teselas = res.TileCount > CuentaTeselas<CGearBackground>();
        if (!colores && !teselas) return null;
        var m = new List<string>();
        if (colores) m.Add(IdiomaInterfaz.F("ContenidoDescargable.DemasiadosColoresMaximoImagen", CuentaColores<CGearBackground>(), res.ColorCount));
        if (teselas) m.Add(IdiomaInterfaz.F("ContenidoDescargable.DemasiadasTeselasMaximoImagen", CuentaTeselas<CGearBackground>(), res.TileCount));
        return string.Join(' ', m);
    }

    /// <summary>Lo que hace <c>B_ImportCGB_Click</c> tras elegir el archivo: lo carga en el formato del juego abierto (convierte el desplazamiento de paleta si es del otro). Devuelve el motivo del error o null.</summary>
    private static string? AplicarCgb(SaveFile sav, CGearBackground destino, byte[] datos)
    {
        if (datos.Length != CGearBackground.SIZE) return IdiomaInterfaz.F("ContenidoDescargable.TamanoIncorrectoArchivoTiene", datos.Length, CGearBackground.SIZE);
        var esBW = sav is SAV5BW;
        var temp = esBW ? new CGearBackgroundBW((byte[])datos.Clone()) : (CGearBackground)new CGearBackgroundB2W2((byte[])datos.Clone());
        try
        {
            var psk = PaletteTileSelection.IsPaletteShiftFormat(temp.Arrange);
            if (esBW && !psk) PaletteTileSelection.ConvertToShiftFormat<CGearBackgroundBW>(temp.Arrange);
            else if (!esBW && psk) PaletteTileSelection.ConvertFromShiftFormat(temp.Arrange);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IndexOutOfRangeException or ArgumentOutOfRangeException) { return ex.Message; }
        temp.Data.CopyTo(destino.Data);
        return null;
    }

    // Una sola entrada de memoria: decodificar el PNG del borrador y convertirlo cuesta, y la pantalla vuelve a pedir la vista en CADA relectura. La clave es todo lo que la vista depende (los dos borradores por referencia,
    // la skin de la partida por contenido y la version); si no cambia nada se devuelve la misma imagen sin decodificar. Internal: la prueba cuenta las decodificaciones.
    private static readonly object CerrojoVista = new();
    private static (object? Png, object? Cgb, byte[] Skin, GameVersion Version, ImagenJugador? Imagen)? _ultimaVista;
    internal static int VistasCalculadas;

    private static ImagenJugador? VistaCGear(ContextoP c)
    {
        var png = c.Valor("cgear-png"); var cgb = c.Valor("cgear-cgb"); var skin = S(c).CGearSkinData.ToArray();
        lock (CerrojoVista)
            if (_ultimaVista is { } u && ReferenceEquals(u.Png, png) && ReferenceEquals(u.Cgb, cgb) && u.Version == c.Sav.Version && u.Skin.AsSpan().SequenceEqual(skin)) return u.Imagen;
        var bg = CGearResultante(c, out _);
        System.Threading.Interlocked.Increment(ref VistasCalculadas);
        var imagen = bg.IsUninitialized ? null : new ImagenJugador(IdiomaInterfaz.T("ContenidoDescargable.AparienciaCGear"), CGearBackground.Width, CGearBackground.Height, bg.GetImageData());
        lock (CerrojoVista) _ultimaVista = (png, cgb, skin, c.Sav.Version, imagen);
        return imagen;
    }

    private static IEnumerable<CampoP> CamposDeCGear()
    {
        yield return P.Archivo("cgear-png", L("L_CGearPng"),
            (c, b) =>
            {
                var bg = CGear(c.Sav, S(c).CGearSkinData.ToArray());   // se convierte sobre una copia y solo entonces se escribe
                if (AplicarPng(bg, b) is { } motivo) throw new InvalidOperationException(motivo);
                S(c).SetCGearSkin(bg.Data);
            },
            validar: (c, b) => AplicarPng(CGear(c.Sav, new byte[CGearBackground.SIZE]), b),
            exporta: c =>
            {
                var bg = CGear(c.Sav, S(c).CGearSkinData);
                return bg.IsUninitialized ? null : ("Fondo.png", PngLite.Codificar(new SpriteImage(CGearBackground.Width, CGearBackground.Height, bg.GetImageData())));
            },
            extension: "png", textoBoton: L("B_ImportPNG"), prioridad: 0, vista: VistaCGear,
            ayuda: IdiomaInterfaz.T("ContenidoDescargable.PNGPixelesBitsTransparencia"));
        yield return P.Archivo("cgear-cgb", L("L_CGearFile"),
            (c, b) =>
            {
                var bg = CGear(c.Sav, S(c).CGearSkinData.ToArray());
                if (AplicarCgb(c.Sav, bg, b) is { } motivo) throw new InvalidOperationException(motivo);
                S(c).SetCGearSkin(bg.Data);
            },
            validar: (c, b) => AplicarCgb(c.Sav, CGear(c.Sav, new byte[CGearBackground.SIZE]), b),
            exporta: c =>
            {
                var bg = CGear(c.Sav, S(c).CGearSkinData);
                return bg.IsUninitialized ? null : ($"{L("Tab_CGear")}.{ExtensionCGear(c.Sav)}", bg.Data.ToArray());
            },
            extension: ExtensionCgbPorDefecto, textoBoton: L("B_ImportCGB"), prioridad: 1,
            ayuda: IdiomaInterfaz.F("ContenidoDescargable.ArchivoPokeStockBytesAdapta", CGearBackgroundBW.Extension, CGearBackgroundB2W2.Extension, CGearBackground.SIZE));
    }

    private const string ExtensionCgbPorDefecto = "cgb";

    // ---------------- Pokédex ----------------
    private static IEnumerable<CampoP> CamposDePokedex()
    {
        yield return P.Archivo("dex-skin", L("Tab_PokeDex"),
            (c, b) => S(c).SetPokeDexSkin(Ajustar(b, S(c).PokedexSkinData.Length)),
            validar: Tamano(c => S(c).PokedexSkinData.Length, otro: 0x6200),   // el programa original guarda 4 bytes menos (la marca de disponible)
            exporta: c => new PokeDexSkin5(S(c).PokedexSkinData).IsUninitialized ? null : ($"{L("Tab_PokeDex")}.{PokeDexSkin5.Extension}", S(c).PokedexSkinData.ToArray()),
            extension: PokeDexSkin5.Extension, textoBoton: L("B_PokeDexSkinLoad"),
            ayuda: IdiomaInterfaz.F("ContenidoDescargable.ArchivoExportarSoloExiste", PokeDexSkin5.Extension));
    }

    // ---------------- Musical ----------------
    /// <summary>El nombre del musical sale del nombre del archivo (<c>GetImportedMusicalName</c>): lo que va tras el último « - », sin la etiqueta de idioma « (ES) » y recortado al máximo.</summary>
    internal static string NombreDelMusical(string ruta)
    {
        var nombre = Path.GetFileNameWithoutExtension(ruta).Trim();
        var corte = nombre.LastIndexOf(" - ", StringComparison.Ordinal);
        if (corte >= 0 && corte + 3 < nombre.Length) nombre = nombre.AsSpan()[(corte + 3)..].Trim().ToString();
        var sufijo = nombre.LastIndexOf(" (", StringComparison.Ordinal);
        if (sufijo > 0 && nombre[^1] == ')' && EsEtiquetaDeIdioma(nombre.AsSpan()[(sufijo + 2)..^1])) nombre = nombre.AsSpan()[..sufijo].TrimEnd().ToString();
        if (nombre.Length > Musical5.MusicalNameMaxLength) nombre = nombre.AsSpan()[..Musical5.MusicalNameMaxLength].TrimEnd().ToString();
        return nombre;
    }

    private static bool EsEtiquetaDeIdioma(ReadOnlySpan<char> v)
    {
        if (v.Length is < 2 or > 5) return false;
        foreach (var ch in v) if ((uint)(ch - 'A') > 'Z' - 'A') return false;
        return true;
    }

    private static IEnumerable<CampoP> CamposDeMusical()
    {
        yield return P.Archivo("musical", L("Tab_Musical"),
            (c, b) =>
            {
                var datos = Ajustar(b, S(c).MusicalDownloadSize);
                var musical = new MusicalShow5(datos);
                S(c).SetMusical(datos);
                if (c.NombresDeArchivo.TryGetValue("musical", out var nombre)) S(c).Musical.MusicalName = musical.IsUninitialized ? string.Empty : NombreDelMusical(nombre);
            },
            validar: Tamano(c => S(c).MusicalDownloadSize, otro: 0x17D78),   // pporg: el tamaño del programa original
            exporta: c =>
            {
                var n = S(c).Musical.MusicalName;
                if (string.IsNullOrWhiteSpace(n)) n = $"{L("Tab_Musical")} {Vacio}";
                return ($"{n}.{MusicalShow5.Extension}", S(c).MusicalDownloadData.ToArray());
            },
            extension: MusicalShow5.Extension, textoBoton: L("B_MusicalImport"),
            ayuda: IdiomaInterfaz.F("ContenidoDescargable.ArchivoNombreMusicalToma", MusicalShow5.Extension));
    }

    // ---------------- Vídeos de combate ----------------
    private static string NombreDelVideo(ContextoP c, int i)
    {
        var v = new BattleVideo5(S(c).GetBattleVideo(i));
        return v.IsUninitialized ? Vacio : v.GetTrainerNames();
    }

    private static ColeccionDefP Videos()
    {
        ColeccionDefP? col = null;
        col = new ColeccionDefP
        {
            Id = "videos", Elemento = IdiomaInterfaz.T("ContenidoDescargable.Video"), Cantidad = _ => 4, Titulo = (_, i) => IdiomaInterfaz.F("ContenidoDescargable.Video2", i + 1), Resumen = NombreDelVideo,
            Secciones = (_, i) =>
            [
                new SeccionP(string.Empty,
                [
                    P.Info(col!.IdCampo(i, "nombre"), IdiomaInterfaz.T("Form.Respaldo.SAV_SecretBase3.L_Trainers"), c => NombreDelVideo(c, i)),
                    P.Archivo(col.IdCampo(i, "archivo"), IdiomaInterfaz.T("ContenidoDescargable.VideoCombate"),
                        (c, b) =>
                        {
                            var datos = (byte[])b.Clone();
                            var descifrado = BattleVideo5.GetIsDecrypted(datos);
                            var video = new BattleVideo5(datos) { IsDecrypted = descifrado };
                            video.Encrypt();
                            if (!video.IsUninitialized) video.RefreshChecksums();
                            S(c).SetBattleVideo(i, datos);
                        },
                        validar: Tamano(_ => BattleVideo5.SIZE),
                        exporta: c => ($"{col.Elemento} {i + 1}.{BattleVideo5.Extension}", S(c).GetBattleVideo(i).ToArray()),
                        extension: BattleVideo5.Extension, textoBoton: L("B_BattleVideoImport"), ayuda: IdiomaInterfaz.T("ContenidoDescargable.ArchivoBv5CombateViene")),
                    P.Archivo(col.IdCampo(i, "descifrado"), L("B_BattleVideoExportDecrypted"), null,
                        exporta: c =>
                        {
                            var copia = S(c).GetBattleVideo(i).ToArray();
                            var video = new BattleVideo5(copia);
                            if (!video.IsUninitialized) video.Decrypt();
                            return ($"{col.Elemento} {i + 1} ({L("B_BattleVideoExportDecrypted")}).{BattleVideo5.Extension}", copia);
                        },
                        extension: BattleVideo5.Extension, ayuda: IdiomaInterfaz.T("ContenidoDescargable.SoloExportaSacaVideo")),
                ]),
            ],
        };
        return col;
    }

    // ---------------- Pokéwood y PWT (solo Negro 2 / Blanco 2) ----------------
    private static SAV5B2W2 B2(ContextoP c) => (SAV5B2W2)c.Sav;

    private static ColeccionDefP Peliculas()
    {
        ColeccionDefP? col = null;
        string Nombre(ContextoP c, int i) => new PokestarMovie5(B2(c).GetPokestarMovie(i)).IsUninitialized ? Vacio : IdiomaInterfaz.T("ContenidoDescargable.Pelicula");
        col = new ColeccionDefP
        {
            Id = "peliculas", Elemento = IdiomaInterfaz.T("ContenidoDescargable.Pelicula"), Cantidad = c => c.Sav is SAV5B2W2 ? SAV5B2W2.PokestarCount : 0, MostrarPosicion = true, Titulo = (_, i) => IdiomaInterfaz.F("ContenidoDescargable.Pelicula2", i + 1), Resumen = Nombre,
            Secciones = (_, i) =>
            [
                new SeccionP(string.Empty,
                [
                    P.Info(col!.IdCampo(i, "estado"), IdiomaInterfaz.T("CartasP.Estado"), c => Nombre(c, i)),
                    P.Archivo(col.IdCampo(i, "archivo"), IdiomaInterfaz.T("ContenidoDescargable.Pelicula"), (c, b) => B2(c).SetPokestarMovie(i, b),
                        validar: Tamano(_ => PokestarMovie5.SIZE),
                        exporta: c => ($"{col.Elemento} {i + 1}.{PokestarMovie5.Extension}", B2(c).GetPokestarMovie(i).ToArray()),
                        extension: PokestarMovie5.Extension, textoBoton: L("B_PokestarImport"), ayuda: IdiomaInterfaz.F("ContenidoDescargable.ArchivoBytesExportarSaca", PokestarMovie5.Extension, PokestarMovie5.SIZE)),
                ]),
            ],
        };
        return col;
    }

    private static ColeccionDefP Torneos()
    {
        ColeccionDefP? col = null;
        string Nombre(ContextoP c, int i) { var n = new WorldTournament5(B2(c).GetPWT(i)).Name; return string.IsNullOrWhiteSpace(n) ? Vacio : n; }
        col = new ColeccionDefP
        {
            Id = "torneos", Elemento = "Torneo", Cantidad = c => c.Sav is SAV5B2W2 ? SAV5B2W2.PWTCount : 0, Titulo = (_, i) => $"Torneo {i + 1}", Resumen = Nombre,
            Secciones = (_, i) =>
            [
                new SeccionP(string.Empty,
                [
                    P.Info(col!.IdCampo(i, "nombre"), IdiomaInterfaz.T("EstadisticasBase.Nombre"), c => Nombre(c, i)),
                    P.Archivo(col.IdCampo(i, "archivo"), "Torneo", (c, b) => B2(c).SetPWT(i, Ajustar(b, WorldTournament5.SIZE)),
                        validar: Tamano(_ => WorldTournament5.SIZE, otro: 0x1314),   // pporg: el tamaño del programa original
                        exporta: c => ($"{Nombre(c, i)}.{WorldTournament5.Extension}", B2(c).GetPWT(i).ToArray()),
                        extension: WorldTournament5.Extension, textoBoton: L("B_PWTImport"), ayuda: IdiomaInterfaz.F("ContenidoDescargable.ArchivoBytesExportarSaca", WorldTournament5.Extension, WorldTournament5.SIZE)),
                ]),
            ],
        };
        return col;
    }

    // ---------------- Nexo Recuerdo y Test de combate ----------------
    private static IEnumerable<CampoP> CamposDeNexo()
    {
        yield return P.Archivo("memoria-1", L("L_MemorySlot1"), (c, b) => S(c).SetLink1Data(b), validar: Tamano(c => S(c).Link1Data.Length),
            exporta: c => ($"{L("Tab_MemoryLink")} 1.{MemoriaExtension}", S(c).Link1Data.ToArray()), extension: MemoriaExtension, textoBoton: L("B_Memory1Import"), ayuda: IdiomaInterfaz.F("ContenidoDescargable.ArchivoBytesExportarSaca2", MemoriaExtension));
        yield return P.Archivo("memoria-2", L("L_MemorySlot2"), (c, b) => S(c).SetLink2Data(b), validar: Tamano(c => S(c).Link2Data.Length),
            exporta: c => ($"{L("Tab_MemoryLink")} 2.{MemoriaExtension}", S(c).Link2Data.ToArray()), extension: MemoriaExtension, textoBoton: L("B_Memory2Import"), ayuda: IdiomaInterfaz.F("ContenidoDescargable.ArchivoBytesExportarSaca2", MemoriaExtension));
    }

    private const string MemoriaExtension = "ml5";

    private static IEnumerable<CampoP> CamposDeTest()
    {
        yield return P.Info("test-aviso", L("L_BattleTestWarning"), _ => IdiomaInterfaz.T("ContenidoDescargable.PKHeXAvisaFuncionVa"));
        yield return P.Archivo("test-combate", L("Tab_BattleTest"),
            (c, b) =>
            {
                var datos = (byte[])b.Clone();
                var test = new BattleTest5(datos);
                if (!test.IsUninitialized) { test.Magic = BattleTest5.Sentinel; test.RefreshChecksums(); }   // la suma de comprobación válida aunque el archivo esté manipulado
                S(c).SetBattleTest(datos);
            },
            validar: Tamano(_ => BattleTest5.SIZE),
            exporta: c => ($"{L("Tab_BattleTest")}.{BattleTest5.Extension}", S(c).BattleTest.ToArray()),
            extension: BattleTest5.Extension, textoBoton: L("B_BattleTestImport"), ayuda: IdiomaInterfaz.F("ContenidoDescargable.ArchivoBytesExportarSaca", BattleTest5.Extension, BattleTest5.SIZE));
    }

    public static EditorDefP? Crear(SaveFile sav)
    {
        if (!Aplica(sav)) return null;
        var pestanas = new List<PestanaP>
        {
            new("cgear", L("Tab_CGear"), [new SeccionP(string.Empty, [.. CamposDeCGear()])]),
            new("pokedex", L("Tab_PokeDex"), [new SeccionP(string.Empty, [.. CamposDePokedex()])]),
            new("musical", L("Tab_Musical"), [new SeccionP(string.Empty, [.. CamposDeMusical()])]),
            new("videos", L("Tab_BattleVideo"), [], Coleccion: Videos()),
            new("nexo", L("Tab_MemoryLink"), [new SeccionP(string.Empty, [.. CamposDeNexo()])]),
            new("test", L("Tab_BattleTest"), [new SeccionP(string.Empty, [.. CamposDeTest()])]),
        };
        // Pokéwood y PWT solo existen en Negro 2/Blanco 2 (el formulario original quita esas pestañas en Negro/Blanco): la pestaña no se crea, no se esconde.
        if (sav is SAV5B2W2)
        {
            pestanas.Insert(4, new("peliculas", L("Tab_Pokestar"), [], Coleccion: Peliculas()));
            pestanas.Insert(5, new("pwt", L("Tab_PWT"), [], Coleccion: Torneos()));
        }
        return new EditorDefP("dlc5", IdiomaInterfaz.T("Partida.Especifico.dlc5.Titulo"), pestanas, Censo, IdiomaInterfaz.T("Partida.Especifico.dlc5.Descripcion"));
    }
}
