// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace Pokekeep.Sprites;

/// <summary>
/// Origen de los recursos de PKHeX por nombre (<c>Resources.ResourceManager.GetObject(nombre)</c> en el original). Lo devuelto es COMPARTIDO e inmutable:
/// quien quiera modificarlo debe clonarlo antes (<see cref="SpriteImage.Clone"/>). Un nombre sin recurso devuelve null (el compositor lo trata como PKHeX).
/// </summary>
public interface IRecursosSprites
{
    SpriteImage? Obtener(string nombre);
}

/// <summary>
/// Recursos leidos de los paquetes generados por <c>herramientas\generar-sprites-android.ps1</c>. Los paquetes se cargan enteros la primera vez que se pide uno de
/// sus recursos (3-5 MB, PNG comprimidos; en Android los entrega el AssetManager) y cada sprite se decodifica bajo demanda con <see cref="PngLite"/>.
/// Los sprites decodificados se guardan en una cache LRU con tope de bytes (no se retienen los ~75 MB que ocuparian todos).
/// </summary>
public sealed class RecursosEmpaquetados : IRecursosSprites
{
    private readonly Manifiesto _manifiesto;
    private readonly Func<string, byte[]> _abrirPaquete;
    private readonly byte[]?[] _paquetes;
    private readonly long _tope;
    private readonly object _cierre = new();
    private readonly Dictionary<string, LinkedListNode<(string Nombre, SpriteImage? Imagen, long Bytes)>> _cache = new(StringComparer.Ordinal);
    private readonly LinkedList<(string Nombre, SpriteImage? Imagen, long Bytes)> _orden = new();
    private long _bytesCache;

    public RecursosEmpaquetados(Manifiesto manifiesto, Func<string, byte[]> abrirPaquete, long topeCacheBytes = 24L * 1024 * 1024)
    {
        _manifiesto = manifiesto; _abrirPaquete = abrirPaquete; _tope = topeCacheBytes;
        _paquetes = new byte[]?[manifiesto.Paquetes.Count];
    }

    public Manifiesto Manifiesto => _manifiesto;

    public bool Contiene(string nombre) => _manifiesto.Recursos.ContainsKey(nombre);

    public long BytesEnCache { get { lock (_cierre) return _bytesCache; } }
    public int SpritesEnCache { get { lock (_cierre) return _cache.Count; } }
    public long BytesPaquetesCargados { get { lock (_cierre) return _paquetes.Sum(p => p?.LongLength ?? 0); } }

    /// <summary>Descarta los paquetes cargados y la cache (presion de memoria).</summary>
    public void Liberar()
    {
        lock (_cierre) { Array.Clear(_paquetes); _cache.Clear(); _orden.Clear(); _bytesCache = 0; }
    }

    /// <summary>Los bytes del PNG ORIGINAL de PKHeX (sin recodificar), o null si no existe. Para <c>ISpriteSource.TryGetPng</c>.</summary>
    public byte[]? ObtenerPng(string nombre)
    {
        lock (_cierre)
        {
            if (!_manifiesto.Recursos.TryGetValue(nombre, out var e)) return null;
            var paquete = _paquetes[e.Paquete] ??= _abrirPaquete(_manifiesto.Paquetes[e.Paquete].Archivo);
            return paquete.AsSpan(checked((int)e.Desplazamiento), e.Longitud).ToArray();
        }
    }

    public SpriteImage? Obtener(string nombre)
    {
        lock (_cierre)
        {
            if (_cache.TryGetValue(nombre, out var nodo))
            {
                _orden.Remove(nodo); _orden.AddFirst(nodo);
                return nodo.Value.Imagen;
            }
            if (!_manifiesto.Recursos.TryGetValue(nombre, out var e)) return null; // los candidatos que no existen son normales (cadena de respaldo de PKHeX): no es un aviso
            var paquete = _paquetes[e.Paquete] ??= _abrirPaquete(_manifiesto.Paquetes[e.Paquete].Archivo);
            var crudo = paquete.AsSpan(checked((int)e.Desplazamiento), e.Longitud);
            var img = WebpLite.EsWebp(crudo) ? WebpLite.Decodificar(crudo) : PngLite.Decodificar(crudo);   // X53b: los paquetes de PLA y SV llevan WebP sin perdida
            var bytes = (long)img.Pixels.Length;
            var n = _orden.AddFirst((nombre, img, bytes));
            _cache[nombre] = n;
            _bytesCache += bytes;
            while (_bytesCache > _tope && _orden.Count > 1)
            {
                var ultimo = _orden.Last!;
                _orden.RemoveLast(); _cache.Remove(ultimo.Value.Nombre); _bytesCache -= ultimo.Value.Bytes;
            }
            return img;
        }
    }
}
