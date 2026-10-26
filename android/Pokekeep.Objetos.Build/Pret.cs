// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Buffers.Binary;
using System.Text;

namespace Pokekeep.Objetos.Build;

/// <summary>Arte de Esmeralda y FR/LG: id nativo → tabla del juego → gráfico Y paleta.
/// El mapa y la referencia independiente de píxeles (Pillow) salen de generar-mapa-pret.py.
/// Rubí/Zafiro no tiene ese catálogo: nunca se le atribuye el arte de Esmeralda.</summary>
public static class Pret
{
    public sealed record Entrada(string Juego, string Fuente, string Revision, int Id, string Constante,
        string Grafico, string Paleta, string ShaGrafico, string ShaPaleta, string ShaResultado, string ShaPixeles);

    public static IReadOnlyList<Entrada> Entradas { get; } = Leer();

    private static List<Entrada> Leer()
    {
        using var s = typeof(Pret).Assembly.GetManifestResourceStream("pret-gen3.tsv")!;
        using var r = new StreamReader(s);
        _ = r.ReadLine();
        var filas = new List<Entrada>();
        while (r.ReadLine() is { } l)
        {
            var v = l.Split('\t');
            filas.Add(new(v[0], v[1], v[2], int.Parse(v[3]), v[4], v[5], v[6], v[7], v[8], v[9], v[10]));
        }
        return filas;
    }

    public static string Url(Entrada e, string ruta) => $"https://raw.githubusercontent.com/{e.Fuente}/{e.Revision}/{ruta}";
    public static string Cache(string cache, Entrada e, string ruta) => Path.Combine(cache, "pret", e.Fuente.Split('/')[1], e.Revision, ruta.Replace('/', Path.DirectorySeparatorChar));

    internal static async Task<List<HallazgoJuego>> Buscar(HttpClient http, string cache, bool sinDescargas, IReadOnlyList<(Juego J, ObjetoJuego O)> objetos)
    {
        var salida = new List<HallazgoJuego>();
        var porId = Entradas.ToDictionary(e => (e.Juego, e.Id));
        foreach (var (j, o) in objetos.Where(x => x.J.Id is "E" or "FRLG"))
        {
            var e = porId[(j.Id, o.IdNativo)];
            var png = await Fuente.Obtener(http, Url(e, e.Grafico), Cache(cache, e, e.Grafico), sinDescargas) ?? throw new InvalidDataException($"Falta {e.Grafico} de pret.");
            var pal = await Fuente.Obtener(http, Url(e, e.Paleta), Cache(cache, e, e.Paleta), sinDescargas) ?? throw new InvalidDataException($"Falta {e.Paleta} de pret.");
            // Las huellas de las paletas de pret se tomaron con saltos de línea CRLF; raw.githubusercontent.com las sirve con LF. Se compara siempre la versión CRLF (misma paleta, mismo contenido).
            if (Fuente.Sha(png) != e.ShaGrafico || Fuente.Sha(ComoCrLf(pal)) != e.ShaPaleta) throw new InvalidDataException($"Fuente pret alterada: {j.Id} {o.IdNativo}");
            var resultado = AplicarPaleta(png, Encoding.UTF8.GetString(pal));
            if (Fuente.Sha(resultado) != e.ShaResultado) throw new InvalidDataException($"Render de pret distinto del mapa: {j.Id} {o.IdNativo}");
            salida.Add(new(j, o, Path.GetFileName(e.Grafico), Url(e, e.Grafico), "pret-gen3", e.Fuente, e.Revision, resultado,
                $"id nativo {e.Id} = {e.Constante} (include/constants/items.h); src/data/item_icon_table.h + src/data/graphics/items.h: {e.Grafico} + {e.Paleta} (SHA {e.ShaGrafico}/{e.ShaPaleta}); índices originales, paleta JASC y alfa 0, sin escalar; píxeles contrastados con Pillow ({e.ShaPixeles})"));
        }
        return salida;
    }

    /// <summary>El mismo texto con todos los saltos de línea como CRLF (los bytes ASCII de una paleta JASC no cambian en nada más).</summary>
    internal static byte[] ComoCrLf(byte[] texto)
    {
        var salida = new List<byte>(texto.Length + 32);
        for (var i = 0; i < texto.Length; i++)
        {
            if (texto[i] == (byte)'\r' && i + 1 < texto.Length && texto[i + 1] == (byte)'\n') { salida.Add((byte)'\r'); salida.Add((byte)'\n'); i++; }
            else if (texto[i] == (byte)'\n') { salida.Add((byte)'\r'); salida.Add((byte)'\n'); }
            else salida.Add(texto[i]);
        }
        return [.. salida];
    }

    /// <summary>Solo sustituye PLTE y su CRC; IDAT, índices y transparencia originales no cambian.
    /// Las paletas compartidas son esenciales: Hyper Potion no lleva la paleta de Potion.</summary>
    public static byte[] AplicarPaleta(byte[] png, string jasc)
    {
        var lineas = jasc.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lineas.Length != 19 || lineas[0] != "JASC-PAL" || lineas[1] != "0100" || lineas[2] != "16") throw new InvalidDataException("Paleta pret JASC de 16 colores requerida.");
        var colores = lineas.Skip(3).SelectMany(l => l.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(byte.Parse)).ToArray();
        if (colores.Length != 48) throw new InvalidDataException("Paleta RGB inválida.");
        var resultado = (byte[])png.Clone();
        for (var p = 8; p + 12 <= resultado.Length;)
        {
            var n = checked((int)BinaryPrimitives.ReadUInt32BigEndian(resultado.AsSpan(p, 4)));
            if (n < 0 || n > resultado.Length - p - 12) throw new InvalidDataException("Chunk PNG truncado.");
            if (resultado.AsSpan(p + 4, 4).SequenceEqual("PLTE"u8))
            {
                if (n != 48) throw new InvalidDataException("PNG pret con paleta distinta de 16 colores.");
                colores.CopyTo(resultado, p + 8);
                uint crc = uint.MaxValue;
                foreach (var b in resultado.AsSpan(p + 4, n + 4))
                {
                    crc ^= b;
                    for (var i = 0; i < 8; i++) crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0 : 0xedb88320u);
                }
                BinaryPrimitives.WriteUInt32BigEndian(resultado.AsSpan(p + 8 + n, 4), ~crc);
                return resultado;
            }
            p += n + 12;
        }
        throw new InvalidDataException("PNG pret sin PLTE.");
    }
}
