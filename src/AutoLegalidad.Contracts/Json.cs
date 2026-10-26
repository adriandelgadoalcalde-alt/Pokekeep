// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;

namespace AutoLegalidad.Contracts;

/// <summary>Serializacion JSON unica del protocolo (UTF-8, camelCase, enums como texto, sin campos desconocidos) y esquema JSON derivado de los tipos. Los metadatos se generan en compilacion (<see cref="ContractsJsonContext"/>).</summary>
public static class Json
{
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var o = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, // un campo desconocido es un error de contrato, no se ignora en silencio
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
            // Metadatos generados en compilacion (ContractsJsonContext): sin reflexion. Los tipos del contrato son los unicos que se serializan.
            TypeInfoResolver = ContractsJsonContext.Default,
        };
        o.NumberHandling = JsonNumberHandling.Strict;
        o.Converters.Add(new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false)); // los estados se escriben tal cual los fija el plan (Success, AlreadyLegal...)
        return o;
    }

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static byte[] SerializeToUtf8<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, Options);

    public static T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Options) ?? throw new JsonException("El documento JSON es null.");

    public static T Deserialize<T>(ReadOnlySpan<byte> utf8) => JsonSerializer.Deserialize<T>(utf8, Options) ?? throw new JsonException("El documento JSON es null.");

    /// <summary>Esquema JSON (draft 2020-12) derivado de los tipos. Se versiona en <c>schema/</c>; una prueba falla si deja de coincidir.</summary>
    public static string BuildSchema<T>()
    {
        JsonNode node = JsonSchemaExporter.GetJsonSchemaAsNode(Options, typeof(T));
        // NewLine fijo: el repo guarda el esquema con LF (ADR 0057, eol=lf) y el valor por defecto es Environment.NewLine (CRLF en Windows).
        return node.ToJsonString(new JsonSerializerOptions { WriteIndented = true, NewLine = "\n", Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n";
    }
}
