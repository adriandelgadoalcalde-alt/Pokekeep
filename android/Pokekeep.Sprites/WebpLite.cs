// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Buffers.Binary;

namespace Pokekeep.Sprites;

/// <summary>
/// Lector de WebP SIN PERDIDA (VP8L) 100 % gestionado, como <see cref="PngLite"/>: sin SkiaSharp ni dependencia nativa, igual en el PC y en Android. Implementa la especificacion «WebP Lossless Bitstream» completa
/// (transformadas de predictor, color cruzado, restar verde e indexado de color; codigos de prefijo con meta-prefijos, LZ77 y cache de colores). Devuelve BGRA no premultiplicado y, como el resto de sprites de juego, con el color de un
/// pixel invisible a 0. El generador (<c>Pokekeep.Sprites.Build</c>) comprueba pixel a pixel que cada recurso empaquetado decodifica igual que el original.
/// </summary>
public static class WebpLite
{
    /// <summary>Es un contenedor RIFF/WEBP con un fotograma VP8L.</summary>
    public static bool EsWebp(ReadOnlySpan<byte> datos) => datos.Length > 20 && datos[..4].SequenceEqual("RIFF"u8) && datos.Slice(8, 4).SequenceEqual("WEBP"u8);

    public static SpriteImage Decodificar(ReadOnlySpan<byte> webp)
    {
        if (!EsWebp(webp)) throw new InvalidDataException("No es un WebP.");
        var p = 12;
        while (p + 8 <= webp.Length)
        {
            var len = (int)BinaryPrimitives.ReadUInt32LittleEndian(webp.Slice(p + 4, 4));
            if (webp.Slice(p, 4).SequenceEqual("VP8L"u8)) return DecodificarVp8L(webp.Slice(p + 8, len));
            p += 8 + len + (len & 1);
        }
        throw new InvalidDataException("El WebP no es VP8L (sin perdida).");
    }

    private sealed class Bits(byte[] datos)
    {
        private int _pos; private ulong _buf; private int _n;
        public uint Leer(int n)
        {
            if (n == 0) return 0;
            while (_n < n) { _buf |= (ulong)(_pos < datos.Length ? datos[_pos] : (byte)0) << _n; _pos++; _n += 8; }
            var v = (uint)(_buf & ((1UL << n) - 1)); _buf >>= n; _n -= n;
            if (_pos > datos.Length + 8) throw new InvalidDataException("VP8L truncado.");
            return v;
        }
        public int Bit() => (int)Leer(1);
        /// <summary>Los proximos <paramref name="n"/> bits sin consumirlos (ceros pasado el final).</summary>
        public uint Ver(int n)
        {
            while (_n < n) { _buf |= (ulong)(_pos < datos.Length ? datos[_pos] : (byte)0) << _n; _pos++; _n += 8; }
            return (uint)(_buf & ((1UL << n) - 1));
        }
        public void Saltar(int n) { _buf >>= n; _n -= n; }
    }

    /// <summary>Codigo de prefijo canonico (decodificacion bit a bit; las imagenes son pequenas).</summary>
    private sealed class Huffman
    {
        private readonly int[] _cuenta = new int[16];
        private readonly int[] _simbolos;
        private readonly int _unico = -1;
        private const int BitsRapidos = 8;
        private readonly short[]? _rapida;   // por los 8 proximos bits del flujo: (simbolo << 4) | longitud, 0 = codigo largo

        public Huffman(int[] longitudes)
        {
            var n = 0; var ultimo = -1;
            foreach (var l in longitudes) if (l > 0) { _cuenta[l]++; n++; }
            for (var i = 0; i < longitudes.Length; i++) if (longitudes[i] > 0) ultimo = i;
            _simbolos = new int[Math.Max(n, 1)];
            if (n == 1) { _unico = ultimo; return; }
            if (n == 0) { _unico = 0; return; }
            var offs = new int[17];
            for (var l = 1; l < 16; l++) offs[l + 1] = offs[l] + _cuenta[l];
            var inicio = (int[])offs.Clone();
            for (var s = 0; s < longitudes.Length; s++) if (longitudes[s] > 0) _simbolos[offs[longitudes[s]]++] = s;
            // Tabla rapida para los codigos de hasta 8 bits: el primer bit leido es el MAS significativo del codigo canonico.
            if (longitudes.Length > 2048) return;
            _rapida = new short[1 << BitsRapidos];
            var primero = 0;
            for (var l = 1; l <= BitsRapidos; l++)
            {
                for (var k = 0; k < _cuenta[l]; k++)
                {
                    var simbolo = _simbolos[inicio[l] + k]; var c = primero + k;
                    var invertido = 0; for (var i = 0; i < l; i++) invertido |= ((c >> (l - 1 - i)) & 1) << i;   // el bit 0 del flujo es el primero del codigo
                    for (var rest = 0; rest < 1 << (BitsRapidos - l); rest++) _rapida[invertido | (rest << l)] = (short)((simbolo << 4) | l);
                }
                primero = (primero + _cuenta[l]) << 1;
            }
        }

        public int Leer(Bits b)
        {
            if (_unico >= 0) return _unico;
            if (_rapida is not null)
            {
                var e = _rapida[b.Ver(BitsRapidos)];
                if (e != 0) { b.Saltar(e & 15); return e >> 4; }
            }
            int code = 0, first = 0, index = 0;
            for (var len = 1; len < 16; len++)
            {
                code |= b.Bit();
                var count = _cuenta[len];
                if (code - count < first) return _simbolos[index + (code - first)];
                index += count; first += count; first <<= 1; code <<= 1;
            }
            throw new InvalidDataException("Codigo de prefijo no valido.");
        }
    }

    private static readonly int[] OrdenLongitudes = [17, 18, 0, 1, 2, 3, 4, 5, 16, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15];

    private static readonly (int Dx, int Dy)[] Vecinos =
    [
        (0, 1), (1, 0), (1, 1), (-1, 1), (0, 2), (2, 0), (1, 2), (-1, 2), (2, 1), (-2, 1), (2, 2), (-2, 2), (0, 3), (3, 0), (1, 3), (-1, 3), (3, 1), (-3, 1), (2, 3), (-2, 3), (3, 2), (-3, 2), (0, 4), (4, 0), (1, 4), (-1, 4), (4, 1), (-4, 1),
        (3, 3), (-3, 3), (2, 4), (-2, 4), (4, 2), (-4, 2), (0, 5), (3, 4), (-3, 4), (4, 3), (-4, 3), (5, 0), (1, 5), (-1, 5), (5, 1), (-5, 1), (2, 5), (-2, 5), (5, 2), (-5, 2), (4, 4), (-4, 4), (3, 5), (-3, 5), (5, 3), (-5, 3), (0, 6), (6, 0),
        (1, 6), (-1, 6), (6, 1), (-6, 1), (2, 6), (-2, 6), (6, 2), (-6, 2), (4, 5), (-4, 5), (5, 4), (-5, 4), (3, 6), (-3, 6), (6, 3), (-6, 3), (0, 7), (7, 0), (1, 7), (-1, 7), (5, 5), (-5, 5), (7, 1), (-7, 1), (4, 6), (-4, 6), (6, 4), (-6, 4),
        (2, 7), (-2, 7), (7, 2), (-7, 2), (3, 7), (-3, 7), (7, 3), (-7, 3), (5, 6), (-5, 6), (6, 5), (-6, 5), (8, 0), (4, 7), (-4, 7), (7, 4), (-7, 4), (8, 1), (8, 2), (6, 6), (-6, 6), (8, 3), (5, 7), (-5, 7), (7, 5), (-7, 5), (8, 4), (6, 7),
        (-6, 7), (7, 6), (-7, 6), (8, 5), (7, 7), (-7, 7), (8, 6), (8, 7),
    ];

    private static Huffman LeerCodigo(Bits b, int alfabeto)
    {
        var longitudes = new int[alfabeto];
        if (b.Bit() == 1)
        {
            var num = b.Bit() + 1;
            var s0 = (int)b.Leer(b.Bit() == 1 ? 8 : 1);
            longitudes[s0 % alfabeto] = 1;
            if (num == 2) longitudes[(int)b.Leer(8) % alfabeto] = 1;
            return new Huffman(longitudes);
        }
        var cl = new int[19];
        var n = (int)b.Leer(4) + 4;
        for (var i = 0; i < n; i++) cl[OrdenLongitudes[i]] = (int)b.Leer(3);
        var codigoLongitudes = new Huffman(cl);
        int maximo = alfabeto;
        if (b.Bit() == 1) { var bitsLongitud = 2 + 2 * (int)b.Leer(3); maximo = 2 + (int)b.Leer(bitsLongitud); }
        int sim = 0, previo = 8;
        while (sim < alfabeto && maximo-- > 0)
        {
            var c = codigoLongitudes.Leer(b);
            if (c < 16) { longitudes[sim++] = c; if (c != 0) previo = c; continue; }
            int rep, val = 0;
            if (c == 16) { rep = 3 + (int)b.Leer(2); val = previo; }
            else if (c == 17) rep = 3 + (int)b.Leer(3);
            else rep = 11 + (int)b.Leer(7);
            if (sim + rep > alfabeto) throw new InvalidDataException("Longitudes de codigo desbordadas.");
            while (rep-- > 0) longitudes[sim++] = val;
        }
        return new Huffman(longitudes);
    }

    private static int Prefijo(Bits b, int codigo)
    {
        if (codigo < 4) return codigo + 1;
        var extra = (codigo - 2) >> 1;
        var offset = (2 + (codigo & 1)) << extra;
        return offset + (int)b.Leer(extra) + 1;
    }

    /// <summary>Imagen codificada por entropia (principal o sub-imagen). Principal: con meta-prefijos. Devuelve ARGB.</summary>
    private static uint[] LeerImagen(Bits b, int w, int h, bool principal)
    {
        var bitsCache = 0;
        if (b.Bit() == 1) { bitsCache = (int)b.Leer(4); if (bitsCache is < 1 or > 11) throw new InvalidDataException("Cache de colores no valida."); }
        var bitsMeta = 0; uint[]? meta = null; var grupos = 1; int metaAncho = 0;
        if (principal && b.Bit() == 1)
        {
            bitsMeta = (int)b.Leer(3) + 2;
            metaAncho = (w + (1 << bitsMeta) - 1) >> bitsMeta;
            meta = LeerImagen(b, metaAncho, (h + (1 << bitsMeta) - 1) >> bitsMeta, false);
            foreach (var px in meta) grupos = Math.Max(grupos, (int)((px >> 8) & 0xffff) + 1);
        }
        var cacheTam = bitsCache > 0 ? 1 << bitsCache : 0;
        var codigos = new Huffman[grupos][];
        for (var g = 0; g < grupos; g++)
            codigos[g] = [LeerCodigo(b, 256 + 24 + cacheTam), LeerCodigo(b, 256), LeerCodigo(b, 256), LeerCodigo(b, 256), LeerCodigo(b, 40)];
        var total = w * h; var pix = new uint[total]; var cache = cacheTam > 0 ? new uint[cacheTam] : null;
        var grupo = codigos[0];
        int pos = 0;
        void Insertar(uint c) { if (cache is not null) cache[(int)((c * 0x1e35a7bdu) >> (32 - bitsCache))] = c; }
        void ElegirGrupo()
        {
            if (meta is null || pos >= total) return;
            int x = pos % w, y = pos / w;
            grupo = codigos[(int)((meta[(y >> bitsMeta) * metaAncho + (x >> bitsMeta)] >> 8) & 0xffff)];
        }
        ElegirGrupo();
        while (pos < total)
        {
            var s = grupo[0].Leer(b);
            if (s < 256)
            {
                var r = (uint)grupo[1].Leer(b); var bl = (uint)grupo[2].Leer(b); var a = (uint)grupo[3].Leer(b);
                var c = a << 24 | r << 16 | (uint)s << 8 | bl;
                pix[pos++] = c; Insertar(c);
            }
            else if (s < 256 + 24)
            {
                var largo = Prefijo(b, s - 256);
                var d = Prefijo(b, grupo[4].Leer(b));
                int dist;
                if (d > 120) dist = d - 120;
                else { var (dx, dy) = Vecinos[d - 1]; dist = dx + dy * w; if (dist < 1) dist = 1; }
                if (dist > pos || pos + largo > total) throw new InvalidDataException("Copia LZ77 fuera de la imagen.");
                for (var i = 0; i < largo; i++) { var c = pix[pos - dist]; pix[pos++] = c; Insertar(c); }
            }
            else
            {
                if (cache is null) throw new InvalidDataException("Simbolo de cache sin cache.");
                var c = cache[s - 256 - 24]; pix[pos++] = c; Insertar(c);
            }
            if (meta is not null) ElegirGrupo();   // tras cada simbolo (una copia LZ77 puede cruzar el limite del bloque)
        }
        return pix;
    }

    private enum Tipo { Predictor, ColorCruzado, RestarVerde, Indexado }
    private sealed record Transformada(Tipo Tipo, int Ancho, int Bits, uint[]? Datos);

    private static SpriteImage DecodificarVp8L(ReadOnlySpan<byte> datos)
    {
        if (datos.Length < 5 || datos[0] != 0x2f) throw new InvalidDataException("VP8L sin firma.");
        var b = new Bits(datos[1..].ToArray());
        var w = (int)b.Leer(14) + 1; var h = (int)b.Leer(14) + 1;
        b.Leer(1); if (b.Leer(3) != 0) throw new InvalidDataException("Version de VP8L no soportada.");
        var transformadas = new List<Transformada>(); var anchoActual = w; var usadas = 0;
        while (b.Bit() == 1)
        {
            var t = (Tipo)b.Leer(2);
            if ((usadas & (1 << (int)t)) != 0) throw new InvalidDataException("Transformada repetida.");
            usadas |= 1 << (int)t;
            switch (t)
            {
                case Tipo.Predictor or Tipo.ColorCruzado:
                    {
                        var bits = (int)b.Leer(3) + 2;
                        var d = LeerImagen(b, (anchoActual + (1 << bits) - 1) >> bits, (h + (1 << bits) - 1) >> bits, false);
                        transformadas.Add(new Transformada(t, anchoActual, bits, d)); break;
                    }
                case Tipo.RestarVerde: transformadas.Add(new Transformada(t, anchoActual, 0, null)); break;
                default:
                    {
                        var tam = (int)b.Leer(8) + 1;
                        var tabla = LeerImagen(b, tam, 1, false);
                        for (var i = 1; i < tam; i++)
                        {
                            var a = tabla[i - 1]; var c = tabla[i];
                            tabla[i] = ((((a >> 24) + (c >> 24)) & 0xff) << 24) | ((((a >> 16 & 0xff) + (c >> 16 & 0xff)) & 0xff) << 16) | ((((a >> 8 & 0xff) + (c >> 8 & 0xff)) & 0xff) << 8) | (((a & 0xff) + (c & 0xff)) & 0xff);
                        }
                        var wb = tam <= 2 ? 3 : tam <= 4 ? 2 : tam <= 16 ? 1 : 0;
                        transformadas.Add(new Transformada(t, anchoActual, wb, tabla));
                        anchoActual = (anchoActual + (1 << wb) - 1) >> wb; break;
                    }
            }
        }
        var pix = LeerImagen(b, anchoActual, h, true);
        for (var k = transformadas.Count - 1; k >= 0; k--)
        {
            var t = transformadas[k];
            switch (t.Tipo)
            {
                case Tipo.Indexado: pix = Expandir(pix, t, h); break;
                case Tipo.RestarVerde:
                    for (var i = 0; i < pix.Length; i++) { var c = pix[i]; var g = c >> 8 & 0xff; pix[i] = (c & 0xff00ff00) | ((((c >> 16 & 0xff) + g) & 0xff) << 16) | (((c & 0xff) + g) & 0xff); }
                    break;
                case Tipo.ColorCruzado: ColorCruzado(pix, t, h); break;
                default: Predictor(pix, t, h); break;
            }
        }
        var img = new SpriteImage(w, h);
        for (var i = 0; i < pix.Length; i++)
        {
            var c = pix[i]; var o = i * 4;
            if (c >> 24 == 0) continue;   // invisible: el color no se ve, se normaliza a 0 (como el resto de sprites de juego)
            img.Pixels[o] = (byte)c; img.Pixels[o + 1] = (byte)(c >> 8); img.Pixels[o + 2] = (byte)(c >> 16); img.Pixels[o + 3] = (byte)(c >> 24);
        }
        return img;
    }

    private static uint[] Expandir(uint[] pix, Transformada t, int h)
    {
        var anchoCompleto = t.Ancho; var wb = t.Bits; var tabla = t.Datos!;
        var salida = new uint[anchoCompleto * h]; var anchoEmpaquetado = (anchoCompleto + (1 << wb) - 1) >> wb;
        var bitsPorPixel = 8 >> wb; var mascaraPx = (1 << bitsPorPixel) - 1; var mascaraX = (1 << wb) - 1;
        for (var y = 0; y < h; y++)
            for (var x = 0; x < anchoCompleto; x++)
            {
                var emp = pix[y * anchoEmpaquetado + (x >> wb)];
                var idx = (int)(emp >> 8 & 0xff) >> ((x & mascaraX) * bitsPorPixel) & mascaraPx;
                salida[y * anchoCompleto + x] = idx < tabla.Length ? tabla[idx] : 0;
            }
        return salida;
    }

    private static int Delta(sbyte t, sbyte c) => (t * c) >> 5;

    private static void ColorCruzado(uint[] pix, Transformada t, int h)
    {
        var w = t.Ancho; var bw = (w + (1 << t.Bits) - 1) >> t.Bits;
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var e = t.Datos![(y >> t.Bits) * bw + (x >> t.Bits)];
                sbyte gr = (sbyte)(e & 0xff), gb = (sbyte)(e >> 8 & 0xff), rb = (sbyte)(e >> 16 & 0xff);
                var c = pix[y * w + x]; var g = (sbyte)(c >> 8 & 0xff);
                var rojo = (int)(c >> 16 & 0xff) + Delta(gr, g) & 0xff;
                var azul = (int)(c & 0xff) + Delta(gb, g) + Delta(rb, (sbyte)rojo) & 0xff;
                pix[y * w + x] = (c & 0xff00ff00) | (uint)rojo << 16 | (uint)azul;
            }
    }

    private static uint Media(uint a, uint b)
        => (((a >> 24) + (b >> 24)) / 2) << 24 | ((((a >> 16 & 0xff) + (b >> 16 & 0xff)) / 2) << 16) | ((((a >> 8 & 0xff) + (b >> 8 & 0xff)) / 2) << 8) | (((a & 0xff) + (b & 0xff)) / 2);

    private static int Fijar(int v) => v < 0 ? 0 : v > 255 ? 255 : v;

    private static uint Predecir(int modo, uint l, uint t, uint tr, uint tl)
    {
        switch (modo)
        {
            case 1: return l;
            case 2: return t;
            case 3: return tr;
            case 4: return tl;
            case 5: return Media(Media(l, tr), t);
            case 6: return Media(l, tl);
            case 7: return Media(l, t);
            case 8: return Media(tl, t);
            case 9: return Media(t, tr);
            case 10: return Media(Media(l, tl), Media(t, tr));
            case 11:
                {
                    int pl = 0, pt = 0;
                    for (var s = 0; s <= 24; s += 8) { pl += Math.Abs((int)(t >> s & 0xff) - (int)(tl >> s & 0xff)); pt += Math.Abs((int)(l >> s & 0xff) - (int)(tl >> s & 0xff)); }
                    return pl < pt ? l : t;
                }
            case 12:
                {
                    uint r = 0;
                    for (var s = 0; s <= 24; s += 8) r |= (uint)Fijar((int)(l >> s & 0xff) + (int)(t >> s & 0xff) - (int)(tl >> s & 0xff)) << s;
                    return r;
                }
            case 13:
                {
                    var m = Media(l, t); uint r = 0;
                    for (var s = 0; s <= 24; s += 8) { var a = (int)(m >> s & 0xff); var b = (int)(tl >> s & 0xff); r |= (uint)Fijar(a + (a - b) / 2) << s; }
                    return r;
                }
            default: return 0xff000000;
        }
    }

    private static uint Sumar(uint a, uint b)
        => (((a >> 24) + (b >> 24)) & 0xff) << 24 | ((((a >> 16 & 0xff) + (b >> 16 & 0xff)) & 0xff) << 16) | ((((a >> 8 & 0xff) + (b >> 8 & 0xff)) & 0xff) << 8) | (((a & 0xff) + (b & 0xff)) & 0xff);

    private static void Predictor(uint[] pix, Transformada t, int h)
    {
        var w = t.Ancho; var bw = (w + (1 << t.Bits) - 1) >> t.Bits;
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var i = y * w + x;
                uint pred;
                if (y == 0 && x == 0) pred = 0xff000000;
                else if (y == 0) pred = pix[i - 1];
                else if (x == 0) pred = pix[i - w];
                else
                {
                    var modo = (int)(t.Datos![(y >> t.Bits) * bw + (x >> t.Bits)] >> 8 & 0xf);
                    pred = Predecir(modo, pix[i - 1], pix[i - w], pix[i - w + 1], pix[i - w - 1]);
                }
                pix[i] = Sumar(pix[i], pred);
            }
    }
}
