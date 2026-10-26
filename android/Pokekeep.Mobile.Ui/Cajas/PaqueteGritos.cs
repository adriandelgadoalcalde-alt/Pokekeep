// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Pokekeep.Sprites;

namespace Pokekeep.Mobile.Ui.Cajas;

/// <summary>
/// Los gritos que trae la app (X11): un paquete <c>gritos.pkpack</c> + <c>manifest.json</c> con el mismo formato que los sprites (<see cref="Manifiesto"/>), generado por
/// <c>herramientas\generar-gritos-android.ps1</c> a partir de PokeAPI/cries (revisión fijada; procedencia y huellas en <c>docs/android/gritos-procedencia.tsv</c>). Las claves son
/// <c>latest/25</c>, <c>latest/6-1</c> (especie y especie-forma de PKHeX) y lo mismo con <c>legacy/</c> (gritos clásicos de Gen. 1-5). Nada de red: todo viaja dentro del APK.
/// El reproductor de Android necesita un archivo: el sonido pedido se saca del paquete a la carpeta de caché, UNA vez (si ya está con el mismo tamaño no se vuelve a escribir).
/// </summary>
public sealed class PaqueteGritos
{
    private readonly Manifiesto _manifiesto;
    private readonly Func<string, long, int, byte[]> _leer;
    private readonly string _cache;
    private readonly object _cierre = new();

    /// <param name="leer">Lee <c>longitud</c> bytes desde <c>desplazamiento</c> del archivo del paquete (nombre tal y como lo da el manifiesto). En Android: el asset sin comprimir, sin cargarlo entero.</param>
    /// <param name="carpetaCache">Carpeta donde se dejan los sonidos sacados del paquete (puede borrarla el sistema: se vuelven a sacar).</param>
    public PaqueteGritos(Manifiesto manifiesto, Func<string, long, int, byte[]> leer, string carpetaCache)
    {
        _manifiesto = manifiesto; _leer = leer;
        _cache = Path.Combine(carpetaCache, manifiesto.Paquetes.Count > 0 ? manifiesto.Paquetes[0].Sha256[..Math.Min(12, manifiesto.Paquetes[0].Sha256.Length)] : "sin-paquete");   // un paquete distinto (app nueva) no reutiliza sonidos viejos
    }

    public static PaqueteGritos Cargar(ReadOnlySpan<byte> manifiestoJson, Func<string, long, int, byte[]> leer, string carpetaCache)
        => new(Manifiesto.Leer(manifiestoJson), leer, carpetaCache);

    private static readonly string[] Extensiones = [".ogg", ".mp3", ".wav"];

    /// <summary>La extensión que corresponde al contenido: «.ogg» (OggS), «.wav» (RIFF…WAVE) o «.mp3» (ID3 o trama MPEG).</summary>
    public static string ExtensionDe(ReadOnlySpan<byte> b)
        => b.Length >= 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' && b[8] == 'W' ? ".wav"
         : b.Length >= 4 && b[0] == 'O' && b[1] == 'g' && b[2] == 'g' && b[3] == 'S' ? ".ogg"
         : ".mp3";

    public Manifiesto Manifiesto => _manifiesto;
    public int Sonidos => _manifiesto.Recursos.Count;

    public bool Contiene(string clave) => _manifiesto.Recursos.ContainsKey(clave);

    /// <summary>Los bytes del sonido original (Ogg, MP3 o WAV: PokeAPI guarda algún MP3 y WAV con extensión .ogg), o null si la clave no existe.</summary>
    public byte[]? Bytes(string clave)
        => _manifiesto.Recursos.TryGetValue(clave, out var e) ? _leer(_manifiesto.Paquetes[e.Paquete].Archivo, e.Desplazamiento, e.Longitud) : null;

    /// <summary>El archivo del sonido en la caché (lo saca del paquete si falta o tiene otro tamaño), o null si la clave no existe o no se pudo escribir.</summary>
    public string? Extraer(string clave)
    {
        if (!_manifiesto.Recursos.TryGetValue(clave, out var e)) return null;
        try
        {
            lock (_cierre)
            {
                var nombre = Path.Combine(_cache, clave.Replace('/', '-'));
                foreach (var ext in Extensiones)
                    if (File.Exists(nombre + ext) && new FileInfo(nombre + ext).Length == e.Longitud) return nombre + ext;
                var bytes = _leer(_manifiesto.Paquetes[e.Paquete].Archivo, e.Desplazamiento, e.Longitud);
                var destino = nombre + ExtensionDe(bytes);   // el reproductor decide por contenido, pero con la extensión verdadera no hay dudas
                Directory.CreateDirectory(_cache);
                var tmp = destino + ".tmp";
                File.WriteAllBytes(tmp, bytes);
                File.Move(tmp, destino, overwrite: true);
                return destino;
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return null; }   // un grito es opcional: sin poder sacarlo, silencio
    }
}
