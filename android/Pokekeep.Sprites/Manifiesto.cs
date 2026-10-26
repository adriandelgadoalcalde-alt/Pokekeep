// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Text;
using System.Text.Json;

namespace Pokekeep.Sprites;

/// <summary>Un paquete de sprites (una familia de PKHeX): fichero con los PNG sin perdida concatenados.</summary>
public sealed record PaqueteInfo(string Id, string Archivo, long Bytes, string Sha256, int Recursos);

/// <summary>Posicion de un recurso dentro de su paquete: el PNG completo ocupa [Desplazamiento, Desplazamiento+Longitud).</summary>
public readonly record struct EntradaRecurso(int Paquete, long Desplazamiento, int Longitud, int Ancho, int Alto);

/// <summary>
/// <c>manifest.json</c>: nombre de recurso de PKHeX (<c>b_25_1</c>, <c>bitem_234</c>, <c>_ball4</c>...) a su sitio en un paquete. Los nombres son LOS MISMOS que en
/// <c>PKHeX.Drawing.PokeSprite.Properties.Resources</c> (con «_»; el fichero original lleva «-»). La procedencia (fichero original y SHA-256 de cada PNG) vive en
/// <c>procedencia.tsv</c>, que no viaja en el APK.
/// </summary>
public sealed class Manifiesto
{
    public const int VersionActual = 1;
    public int Version { get; init; } = VersionActual;
    public string Origen { get; init; } = "";
    /// <summary>
    /// X53d: conjuntos (juegos) de iconos cuyo dibujo viene RECORTADO por el lienzo (el juego los enseña dentro de su marco): mas de la mitad de sus iconos tiene un tramo opaco de al menos el 15 % del lado pegado al borde. Lo calcula el generador
    /// (<see cref="IconoRecortadoPorElLienzo"/>); donde la app no pone casilla con marco, dibuja una baldosa de fondo para contener el corte. Vacio en los demas manifiestos.
    /// </summary>
    public IReadOnlyList<string> Recortados { get; init; } = [];

    /// <summary>Un icono esta recortado por el lienzo si algun borde (alto o ancho) tiene un tramo continuo de pixeles opacos (alfa &gt;= 128) de al menos el 15 % de su longitud.</summary>
    public static bool IconoRecortadoPorElLienzo(SpriteImage img)
    {
        static int Tramo(Func<int, bool> opaco, int n) { int mejor = 0, cur = 0; for (var i = 0; i < n; i++) { cur = opaco(i) ? cur + 1 : 0; if (cur > mejor) mejor = cur; } return mejor; }
        byte A(int x, int y) => img.Pixels[(y * img.Width + x) * 4 + 3];
        var h = img.Width; var v = img.Height;
        var largo = Math.Max(Math.Max(Tramo(x => A(x, 0) >= 128, h) / (double)h, Tramo(x => A(x, v - 1) >= 128, h) / (double)h),
                             Math.Max(Tramo(y => A(0, y) >= 128, v) / (double)v, Tramo(y => A(h - 1, y) >= 128, v) / (double)v));
        return largo >= 0.15;
    }
    public IReadOnlyList<PaqueteInfo> Paquetes { get; init; } = [];
    public IReadOnlyDictionary<string, EntradaRecurso> Recursos { get; init; } = new Dictionary<string, EntradaRecurso>();

    public byte[] Escribir()
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = false }))
        {
            w.WriteStartObject();
            w.WriteNumber("version", Version);
            w.WriteString("origen", Origen);
            if (Recortados.Count > 0) { w.WriteStartArray("recortados"); foreach (var j in Recortados) w.WriteStringValue(j); w.WriteEndArray(); }
            w.WriteStartArray("paquetes");
            foreach (var p in Paquetes)
            {
                w.WriteStartObject();
                w.WriteString("id", p.Id); w.WriteString("archivo", p.Archivo); w.WriteNumber("bytes", p.Bytes); w.WriteString("sha256", p.Sha256); w.WriteNumber("recursos", p.Recursos);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteStartObject("recursos");
            foreach (var kv in Recursos.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                w.WriteStartArray(kv.Key);
                w.WriteNumberValue(kv.Value.Paquete); w.WriteNumberValue(kv.Value.Desplazamiento); w.WriteNumberValue(kv.Value.Longitud);
                w.WriteNumberValue(kv.Value.Ancho); w.WriteNumberValue(kv.Value.Alto);
                w.WriteEndArray();
            }
            w.WriteEndObject();
            w.WriteEndObject();
        }
        return ms.ToArray();
    }

    public static Manifiesto Leer(ReadOnlySpan<byte> json)
    {
        var r = new Utf8JsonReader(json);
        var paquetes = new List<PaqueteInfo>();
        var recursos = new Dictionary<string, EntradaRecurso>(7000, StringComparer.Ordinal);
        var version = 0; var origen = ""; var recortados = new List<string>();
        r.Read();
        while (r.Read() && r.TokenType == JsonTokenType.PropertyName)
        {
            var nombre = r.GetString();
            r.Read();
            switch (nombre)
            {
                case "version": version = r.GetInt32(); break;
                case "origen": origen = r.GetString() ?? ""; break;
                case "recortados": while (r.Read() && r.TokenType == JsonTokenType.String) recortados.Add(r.GetString()!); break;
                case "paquetes":
                    while (r.Read() && r.TokenType == JsonTokenType.StartObject)
                    {
                        string id = "", archivo = "", sha = ""; long bytes = 0; var n = 0;
                        while (r.Read() && r.TokenType == JsonTokenType.PropertyName)
                        {
                            var k = r.GetString(); r.Read();
                            switch (k)
                            {
                                case "id": id = r.GetString() ?? ""; break;
                                case "archivo": archivo = r.GetString() ?? ""; break;
                                case "bytes": bytes = r.GetInt64(); break;
                                case "sha256": sha = r.GetString() ?? ""; break;
                                case "recursos": n = r.GetInt32(); break;
                                default: r.Skip(); break;
                            }
                        }
                        paquetes.Add(new PaqueteInfo(id, archivo, bytes, sha, n));
                    }
                    break;
                case "recursos":
                    while (r.Read() && r.TokenType == JsonTokenType.PropertyName)
                    {
                        var clave = r.GetString()!;
                        r.Read(); // [
                        r.Read(); var p = r.GetInt32();
                        r.Read(); var off = r.GetInt64();
                        r.Read(); var len = r.GetInt32();
                        r.Read(); var w = r.GetInt32();
                        r.Read(); var h = r.GetInt32();
                        r.Read(); // ]
                        recursos[clave] = new EntradaRecurso(p, off, len, w, h);
                    }
                    break;
                default: r.Skip(); break;
            }
        }
        if (version != VersionActual) throw new InvalidDataException($"Version de manifiesto de sprites {version} no soportada (se esperaba {VersionActual}).");
        return new Manifiesto { Version = version, Origen = origen, Recortados = recortados, Paquetes = paquetes, Recursos = recursos };
    }

    public static string Sha256Hex(ReadOnlySpan<byte> datos) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(datos)).ToLowerInvariant();

    public override string ToString() => $"Manifiesto v{Version}: {Paquetes.Count} paquetes, {Recursos.Count} recursos";
}
