// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia.Media.Imaging;
using PKHeX.Core;

namespace Pokekeep.Mobile.Ui.Design;

/// <summary>Iconos nativos de menú por versión. Sin recurso verificado devuelve null para conservar el icono de partida.</summary>
public sealed class LogosDeJuego
{
    public const string Carpeta = "logos-juegos";
    public const string Manifiesto = "manifest.json";
    /// <summary>Configurar antes de construir las vistas. La cabecera y el gestor comparten este catálogo.</summary>
    public static LogosDeJuego? Actual { get; set; }

    public sealed record Entrada
    {
        public required string Version { get; init; }
        public required string Estado { get; init; }
        public string? Archivo { get; init; }
        public long Bytes { get; init; }
        public int Ancho { get; init; }
        public int Alto { get; init; }
        public string? Sha256 { get; init; }
        public string? Procedencia { get; init; }
        public string? MotivoEs { get; init; }
        public string? MotivoEn { get; init; }
    }

    /// <summary>Logo de título (el rótulo del juego) en un idioma: "es" o "en". Opcional por versión.</summary>
    public sealed record Titulo(GameVersion Version, string Idioma, string Archivo, long Bytes, int Ancho, int Alto, string Sha256, string Procedencia);

    private readonly Dictionary<GameVersion, Entrada> _entradas = [];
    private readonly Dictionary<GameVersion, Bitmap> _imagenes = [];
    private readonly Dictionary<(GameVersion, string), Titulo> _titulos = [];
    private readonly Dictionary<string, Bitmap> _imagenesTitulo = [];
    /// <summary>uso10: carátula del juego (recientes, «Abiertos hace poco», gestor de partidas). Opcional por versión: en Switch la carátula es el icono.</summary>
    public sealed record Caratula(GameVersion Version, string Archivo, long Bytes, int Ancho, int Alto, string Sha256, string Procedencia);
    private readonly Dictionary<GameVersion, Caratula> _caratulas = [];
    private readonly Dictionary<GameVersion, Bitmap> _imagenesCaratula = [];
    public IReadOnlyCollection<Caratula> Caratulas => _caratulas.Values;
    public IReadOnlyCollection<Titulo> Titulos => _titulos.Values;
    private readonly Func<string, byte[]> _abrir;
    private readonly object _cierre = new();
    public IReadOnlyCollection<Entrada> Entradas => _entradas.Values;

    /// <param name="abrir">Lee un archivo relativo a logos-juegos (APK o carpeta); nunca recibe una ruta del volcado.</param>
    public LogosDeJuego(byte[] manifiesto, Func<string, byte[]> abrir)
    {
        _abrir = abrir;
        // Explicit reader also works in trimmed Android builds without
        // reflection metadata for a private deserialization DTO.
        using var doc = JsonDocument.Parse(manifiesto);
        var raiz = doc.RootElement;
        if (raiz.GetProperty("version").GetInt32() != 1)
            throw new InvalidDataException("Versión de logos no admitida / Unsupported logo manifest version.");
        foreach (var fila in raiz.GetProperty("versiones").EnumerateArray())
        {
            static string? Texto(JsonElement f, string nombre) => f.TryGetProperty(nombre, out var v) ? v.GetString() : null;
            var e = new Entrada
            {
                Version = Texto(fila, "version") ?? "", Estado = Texto(fila, "estado") ?? "",
                Archivo = Texto(fila, "archivo"), Sha256 = Texto(fila, "sha256"), Procedencia = Texto(fila, "procedencia"),
                Bytes = fila.TryGetProperty("bytes", out var b) ? b.GetInt64() : 0,
                Ancho = fila.TryGetProperty("ancho", out var a) ? a.GetInt32() : 0,
                Alto = fila.TryGetProperty("alto", out var h) ? h.GetInt32() : 0,
                MotivoEs = Texto(fila, "motivo_es"), MotivoEn = Texto(fila, "motivo_en"),
            };
            if (!Enum.TryParse<GameVersion>(e.Version, out var version) || Enum.GetName(version) != e.Version || !_entradas.TryAdd(version, e))
                throw new InvalidDataException("Versión de juego desconocida o repetida / Unknown or duplicate game version.");
            if (e.Estado == "extraido")
            {
                if (e.Archivo is null || (e.Archivo != e.Version + ".png" && e.Archivo != e.Version + ".jpg") ||
                    e.Bytes is <= 0 or > 16 * 1024 * 1024 || e.Ancho is <= 0 or > 2048 || e.Alto is <= 0 or > 2048 ||
                    e.Sha256 is not { Length: 64 } || !e.Sha256.All(Uri.IsHexDigit) || string.IsNullOrWhiteSpace(e.Procedencia))
                    throw new InvalidDataException("Entrada de logo inválida / Invalid logo entry.");
            }
            else if (string.IsNullOrWhiteSpace(e.MotivoEs) || string.IsNullOrWhiteSpace(e.MotivoEn))
                throw new InvalidDataException("Falta el motivo del icono de respaldo / Missing fallback reason.");
        }
        // Campos opcionales del mismo manifiesto v1: los paquetes sin carátulas o sin títulos siguen valiendo tal cual.
        if (raiz.TryGetProperty("caratulas", out var caratulas))
            foreach (var fila in caratulas.EnumerateArray())
            {
                static string? Texto(JsonElement f, string nombre) => f.TryGetProperty(nombre, out var v) ? v.GetString() : null;
                var nombre = Texto(fila, "version") ?? "";
                var archivo = Texto(fila, "archivo") ?? "";
                var sha = Texto(fila, "sha256") ?? "";
                var c = new Caratula(Enum.TryParse<GameVersion>(nombre, out var v) && Enum.GetName(v) == nombre ? v : GameVersion.Invalid, archivo,
                    fila.TryGetProperty("bytes", out var b) ? b.GetInt64() : 0, fila.TryGetProperty("ancho", out var a) ? a.GetInt32() : 0,
                    fila.TryGetProperty("alto", out var h) ? h.GetInt32() : 0, sha, Texto(fila, "procedencia") ?? "");
                if (c.Version == GameVersion.Invalid || archivo != $"caratulas/{nombre}.png" || c.Bytes is <= 0 or > 16 * 1024 * 1024 ||
                    c.Ancho is <= 0 or > 2048 || c.Alto is <= 0 or > 2048 || sha.Length != 64 || !sha.All(Uri.IsHexDigit) ||
                    string.IsNullOrWhiteSpace(c.Procedencia) || !_caratulas.TryAdd(c.Version, c))
                    throw new InvalidDataException("Carátula inválida o repetida / Invalid or duplicate cover.");
            }
        if (!raiz.TryGetProperty("titulos", out var titulos)) return;
        foreach (var fila in titulos.EnumerateArray())
        {
            static string? Texto(JsonElement f, string nombre) => f.TryGetProperty(nombre, out var v) ? v.GetString() : null;
            var nombre = Texto(fila, "version") ?? "";
            var idioma = Texto(fila, "idioma") ?? "";
            var archivo = Texto(fila, "archivo") ?? "";
            var sha = Texto(fila, "sha256") ?? "";
            var t = new Titulo(Enum.TryParse<GameVersion>(nombre, out var v) && Enum.GetName(v) == nombre ? v : GameVersion.Invalid, idioma, archivo,
                fila.TryGetProperty("bytes", out var b) ? b.GetInt64() : 0, fila.TryGetProperty("ancho", out var a) ? a.GetInt32() : 0,
                fila.TryGetProperty("alto", out var h) ? h.GetInt32() : 0, sha, Texto(fila, "procedencia") ?? "");
            if (t.Version == GameVersion.Invalid || idioma is not ("es" or "en") || !archivo.StartsWith("titulos/", StringComparison.Ordinal) ||
                archivo.Contains("..", StringComparison.Ordinal) || !archivo.EndsWith(".png", StringComparison.Ordinal) ||
                t.Bytes is <= 0 or > 16 * 1024 * 1024 || t.Ancho is <= 0 or > 2048 || t.Alto is <= 0 or > 2048 ||
                sha.Length != 64 || !sha.All(Uri.IsHexDigit) || string.IsNullOrWhiteSpace(t.Procedencia) || !_titulos.TryAdd((t.Version, idioma), t))
                throw new InvalidDataException("Logo de título inválido o repetido / Invalid or duplicate title logo.");
        }
    }

    /// <summary>"es" si la partida está en español (también el latinoamericano de Gen. 9); "en" para todo lo demás.</summary>
    public static string IdiomaDe(SaveFile partida) => IdiomaDe(partida.Language);
    public static string IdiomaDe(int idioma) => idioma is (int)LanguageID.Spanish or (int)LanguageID.SpanishL ? "es" : "en";

    public Titulo? InformacionTitulo(GameVersion version, string idioma) => _titulos.GetValueOrDefault((version, idioma));

    /// <summary>Rótulo del juego en el idioma de la partida, a su resolución; null si el paquete no lo trae (se usa el icono).</summary>
    public Bitmap? ObtenerTitulo(GameVersion version, string idioma)
    {
        if (InformacionTitulo(version, idioma) is not { } t) return null;
        lock (_cierre)
        {
            if (_imagenesTitulo.TryGetValue(t.Archivo, out var imagen)) return imagen;
            var datos = _abrir(t.Archivo);
            if (datos.LongLength != t.Bytes || !string.Equals(Convert.ToHexString(SHA256.HashData(datos)), t.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("El logo de título no coincide con su huella / Title logo checksum mismatch.");
            using var flujo = new MemoryStream(datos, writable: false);
            imagen = new Bitmap(flujo);
            if (imagen.PixelSize.Width != t.Ancho || imagen.PixelSize.Height != t.Alto)
            {
                imagen.Dispose();
                throw new InvalidDataException("Dimensiones del logo de título incorrectas / Title logo dimensions mismatch.");
            }
            _imagenesTitulo.Add(t.Archivo, imagen);
            return imagen;
        }
    }

    public Caratula? InformacionCaratula(GameVersion version) => _caratulas.GetValueOrDefault(version);

    /// <summary>Carátula verificada (huella y tamaño) a su resolución; null si el paquete no la trae.</summary>
    public Bitmap? ObtenerCaratula(GameVersion version)
    {
        if (InformacionCaratula(version) is not { } c) return null;
        lock (_cierre)
        {
            if (_imagenesCaratula.TryGetValue(version, out var imagen)) return imagen;
            var datos = _abrir(c.Archivo);
            if (datos.LongLength != c.Bytes || !string.Equals(Convert.ToHexString(SHA256.HashData(datos)), c.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("La carátula no coincide con su huella / Cover checksum mismatch.");
            using var flujo = new MemoryStream(datos, writable: false);
            imagen = new Bitmap(flujo);
            if (imagen.PixelSize.Width != c.Ancho || imagen.PixelSize.Height != c.Alto)
            {
                imagen.Dispose();
                throw new InvalidDataException("Dimensiones de la carátula incorrectas / Cover dimensions mismatch.");
            }
            _imagenesCaratula.Add(version, imagen);
            return imagen;
        }
    }

    public static LogosDeJuego DesdeCarpeta(string carpeta)
        => new(File.ReadAllBytes(Path.Combine(carpeta, Manifiesto)), f => File.ReadAllBytes(Path.Combine(carpeta, f)));

    /// <summary>Los guardados de GameCube comparten CXD en entidades, pero su formato identifica el juego sin adivinarlo.</summary>
    public static GameVersion VersionDe(SaveFile partida) => partida switch
    {
        SAV3XD => GameVersion.XD,
        SAV3Colosseum => GameVersion.COLO,
        SAV3RSBox => GameVersion.RSBOX,
        _ => partida.Version,
    };

    public Entrada? Informacion(GameVersion version) => _entradas.GetValueOrDefault(version);

    /// <summary>Bytes verificados del título exacto. Nunca usa el logo de su pareja ni de un grupo de versiones.</summary>
    public byte[]? Leer(GameVersion version)
    {
        if (Informacion(version) is not { Estado: "extraido" } e) return null;
        var datos = _abrir(e.Archivo!);
        if (datos.LongLength != e.Bytes || !string.Equals(Convert.ToHexString(SHA256.HashData(datos)), e.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("El logo no coincide con su huella / Logo checksum mismatch.");
        return datos;
    }

    /// <summary>Bitmap compartido a resolución nativa; las vistas no deben disponerlo. Requiere plataforma Avalonia inicializada.</summary>
    public Bitmap? Obtener(GameVersion version)
    {
        lock (_cierre)
        {
            if (_imagenes.TryGetValue(version, out var imagen)) return imagen;
            if (Leer(version) is not { } datos) return null;
            using var flujo = new MemoryStream(datos, writable: false);
            imagen = new Bitmap(flujo);
            var e = _entradas[version];
            if (imagen.PixelSize.Width != e.Ancho || imagen.PixelSize.Height != e.Alto)
            {
                imagen.Dispose();
                throw new InvalidDataException("Dimensiones del logo incorrectas / Logo dimensions mismatch.");
            }
            _imagenes.Add(version, imagen);
            return imagen;
        }
    }
}
