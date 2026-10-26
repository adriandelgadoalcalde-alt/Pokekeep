// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pokekeep.Mobile.Engine.Ipc;

/// <summary>Tipos de marco del protocolo entre la interfaz y el proceso <c>:motor</c> (plan 5.3). Un documento JSON del contrato v2 por solicitud y uno por respuesta, tal cual; aqui solo el marco.</summary>
public static class TipoMarco
{
    /// <summary>Servidor -> cliente, primero de todos: <see cref="Saludo"/> en JSON.</summary>
    public const byte Saludo = (byte)'H';
    /// <summary>Cliente -> servidor: <c>LegalizeRequest</c> (JSON UTF-8).</summary>
    public const byte Solicitud = (byte)'Q';
    /// <summary>Servidor -> cliente: <c>LegalizeResponse</c> (JSON UTF-8).</summary>
    public const byte Respuesta = (byte)'R';
    /// <summary>Servidor -> cliente: una linea de diagnostico del motor (<c>POKEKEEP-FASE ...</c>, <c>POKEKEEP-PROGRESO ...</c>).</summary>
    public const byte Registro = (byte)'L';
    /// <summary>Cliente -> servidor: cancelacion cooperativa de la solicitud en curso.</summary>
    public const byte Cancelar = (byte)'C';
    /// <summary>Cliente -> servidor: pide el <c>WorkerManifest</c>.</summary>
    public const byte PedirManifiesto = (byte)'M';
    /// <summary>Servidor -> cliente: el <c>WorkerManifest</c> (JSON) o un documento <c>{"error":"..."}</c>.</summary>
    public const byte Manifiesto = (byte)'N';
    /// <summary>Cliente -> servidor: cierre ordenado del motor (sesion cerrada).</summary>
    public const byte Apagar = (byte)'X';
    /// <summary>Servidor -> cliente, justo ANTES de la respuesta con la que el vigilante duro del motor termina su propio proceso: esa conexion ya no sirve para otra solicitud. Va antes para que el cliente lo sepa
    /// cuando recibe la respuesta (los marcos llegan en orden) y no dependa de cuándo se entere del corte del socket (con la máquina cargada podía tardar y la solicitud siguiente se enviaba a un motor muerto).</summary>
    public const byte Cierre = (byte)'Z';
}

/// <summary>
/// Saludo del motor (primer marco): su PID (la interfaz lo necesita para matarlo si no obedece), el <c>versionCode</c> del paquete, la generacion de sesion con la que se arranco y la version del protocolo.
/// Un saludo con otro <c>versionCode</c> o protocolo se rechaza: nunca se habla con un motor de otra version.
/// </summary>
public sealed record Saludo(int Pid, int VersionCode, long Generacion, int Protocolo, string Nombre);

/// <summary>Marco de 4 bytes de longitud (little endian, cuenta el byte de tipo mas la carga) + 1 byte de tipo + carga. Las escrituras de un mismo flujo se serializan.</summary>
public static class Marcos
{
    /// <summary>Carga maxima aceptada: una respuesta con candidato y cambios es de unos KB; 16 MB es holgura, no un limite de diseno.</summary>
    public const int CargaMaxima = 16 * 1024 * 1024;

    public static void Escribir(Stream s, byte tipo, ReadOnlySpan<byte> carga)
    {
        if (carga.Length > CargaMaxima) throw new InvalidDataException($"Marco demasiado grande ({carga.Length} bytes).");
        Span<byte> cab = stackalloc byte[5];
        BinaryPrimitives.WriteInt32LittleEndian(cab, carga.Length + 1);
        cab[4] = tipo;
        lock (s)
        {
            s.Write(cab);
            s.Write(carga);
            s.Flush();
        }
    }

    public static void EscribirTexto(Stream s, byte tipo, string texto) => Escribir(s, tipo, Encoding.UTF8.GetBytes(texto));

    public static void EscribirSaludo(Stream s, Saludo saludo) => Escribir(s, TipoMarco.Saludo, JsonSerializer.SerializeToUtf8Bytes(saludo, IpcJsonContext.Default.Saludo));

    public static Saludo LeerSaludo(ReadOnlySpan<byte> carga) => JsonSerializer.Deserialize(carga, IpcJsonContext.Default.Saludo) ?? throw new InvalidDataException("Saludo vacio.");

    /// <summary>Lee un marco. Devuelve null en un fin de flujo LIMPIO (justo entre dos marcos); un flujo cortado a mitad de marco o con una longitud imposible lanza <see cref="InvalidDataException"/>.</summary>
    public static (byte Tipo, byte[] Carga)? Leer(Stream s)
    {
        Span<byte> cab = stackalloc byte[4];
        var n = LeerExacto(s, cab, permitirFinLimpio: true);
        if (n == 0) return null;
        var longitud = BinaryPrimitives.ReadInt32LittleEndian(cab);
        if (longitud < 1 || longitud - 1 > CargaMaxima) throw new InvalidDataException($"Longitud de marco imposible: {longitud}.");
        var cuerpo = new byte[longitud];
        LeerExacto(s, cuerpo, permitirFinLimpio: false);
        return (cuerpo[0], cuerpo.AsSpan(1).ToArray());
    }

    private static int LeerExacto(Stream s, Span<byte> destino, bool permitirFinLimpio)
    {
        var total = 0;
        while (total < destino.Length)
        {
            var n = s.Read(destino[total..]);
            if (n == 0)
            {
                if (total == 0 && permitirFinLimpio) return 0;
                throw new InvalidDataException("El flujo se corto en mitad de un marco.");
            }
            total += n;
        }
        return total;
    }
}

/// <summary>JSON generado (sin reflexion: el recorte de Android no puede romperlo).</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Saludo))]
internal sealed partial class IpcJsonContext : JsonSerializerContext;
