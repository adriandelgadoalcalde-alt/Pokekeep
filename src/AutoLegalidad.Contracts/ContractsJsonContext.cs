// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AutoLegalidad.Contracts;

/// <summary>
/// Enumeraciones como TEXTO, sin admitir enteros (el contrato de siempre: un estado desconocido o un número es un error de contrato). Subclase con constructor sin parámetros para poder
/// declararla en <see cref="ContractsJsonContext"/> (un atributo no puede pasar argumentos al constructor); genérica porque el generador no admite el convertidor no genérico.
/// </summary>
internal sealed class StrictEnumConverter<TEnum>() : JsonStringEnumConverter<TEnum>(namingPolicy: null, allowIntegerValues: false) where TEnum : struct, Enum;

/// <summary>
/// Metadatos de serialización del contrato GENERADOS en compilación (Android F1, ADR 0054; plan 5.9): sin reflexión en tiempo de ejecución, lo que permite recortar (trimming) sin riesgo y acelera
/// el primer arranque. Las opciones del atributo son EXACTAMENTE las de <see cref="Json.Options"/> (camelCase, nulos omitidos, campos desconocidos prohibidos, números estrictos, enumeraciones
/// como texto): el JSON es idéntico byte a byte al que producía la reflexión (prueba <c>JsonContextTests</c> sobre 200 solicitudes y respuestas) y el protocolo no cambia (Codex).
/// Modo <c>Metadata</c> a propósito: sin código de serialización rápido por tipo, para que el resultado sea el del serializador de siempre.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    NumberHandling = JsonNumberHandling.Strict,
    WriteIndented = false,
    GenerationMode = JsonSourceGenerationMode.Metadata,
    Converters = [typeof(StrictEnumConverter<LegalizeAction>), typeof(StrictEnumConverter<OperationStatus>), typeof(StrictEnumConverter<ResponseStatus>), typeof(StrictEnumConverter<Phase>)])]
[JsonSerializable(typeof(LegalizeRequest))]
[JsonSerializable(typeof(LegalizeResponse))]
[JsonSerializable(typeof(WorkerManifest))]
internal sealed partial class ContractsJsonContext : JsonSerializerContext;
