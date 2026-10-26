// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Text;
using Pokekeep.Sprites;

namespace Pokekeep.Gritos.Build;

/// <summary>C15: reemplazo privado sobre el formato X11, sin recodificar ni cambiar otros sonidos.</summary>
public static class PikachuUltrasol
{
    public static bool EsPikachu(string clave) => clave is "latest/25" or "legacy/25"
        || clave.StartsWith("latest/25-", StringComparison.Ordinal) || clave.StartsWith("legacy/25-", StringComparison.Ordinal);

    public static (byte[] Paquete, byte[] Manifiesto, string Procedencia) Sustituir(byte[] paquete, byte[] manifiesto, string procedencia, byte[] wav)
    {
        if (Generador.Formato(wav) != "wav") throw new InvalidDataException("Se requiere el WAV privado de Ultrasol.");
        var m = Manifiesto.Leer(manifiesto);
        if (m.Paquetes.Count != 1 || !m.Recursos.ContainsKey("latest/25") || !m.Recursos.ContainsKey("legacy/25"))
            throw new InvalidDataException("Se requiere el paquete X11 completo.");
        if (m.Paquetes[0].Sha256 != Manifiesto.Sha256Hex(paquete)) throw new InvalidDataException("Huella del paquete de origen incorrecta.");
        var filas = procedencia.Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(l => l.TrimEnd('\r').Split('\t')).ToDictionary(f => f[0]);
        using var salida = new MemoryStream();
        var sitios = new Dictionary<string, (long Pos, int Len)>();
        var recursos = new Dictionary<string, EntradaRecurso>();
        var tsv = new StringBuilder(procedencia.Split('\n')[0].TrimEnd('\r') + "\n");
        foreach (var (clave, e) in m.Recursos.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            var sonido = EsPikachu(clave) ? wav : paquete.AsSpan(checked((int)e.Desplazamiento), e.Longitud).ToArray();
            var sha = Manifiesto.Sha256Hex(sonido);
            if (!sitios.TryGetValue(sha, out var sitio)) { sitio = (salida.Position, sonido.Length); salida.Write(sonido); sitios.Add(sha, sitio); }
            recursos.Add(clave, new EntradaRecurso(0, sitio.Pos, sitio.Len, 0, 0));
            var fila = filas[clave];
            if (EsPikachu(clave))
            {
                fila[4] = ""; fila[5] = "pikachu-ultrasol"; fila[6] = "wav"; fila[7] = sonido.Length.ToString(System.Globalization.CultureInfo.InvariantCulture);
                fila[8] = sha; fila[9] = "audio-juego-ultrasol"; fila[10] = "C15"; fila[11] = "privado:audio-del-juego";
            }
            tsv.AppendJoin('\t', fila).Append('\n');
        }
        var bytes = salida.ToArray();
        var nuevo = new Manifiesto { Origen = m.Origen + "; C15: Pikachu, audio del juego Pokemon Ultrasol (c) The Pokemon Company; extraccion privada del propietario",
            Paquetes = [new PaqueteInfo("gritos", Generador.ArchivoPaquete, bytes.LongLength, Manifiesto.Sha256Hex(bytes), recursos.Count)], Recursos = recursos };
        return (bytes, nuevo.Escribir(), tsv.ToString());
    }
}
