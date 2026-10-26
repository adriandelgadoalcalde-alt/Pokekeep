// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace AutoLegalidad.Contracts;

/// <summary>Constantes del protocolo UI &lt;-&gt; Worker (PLAN-TECNICO.md 6). Un documento JSON UTF-8 por solicitud por stdin y uno por respuesta por stdout.</summary>
public static class Protocol
{
    /// <summary>Version del protocolo. Una solicitud con otra version se rechaza como <see cref="ResponseStatus.VersionMismatch"/>.</summary>
    public const int Version = 2;

    /// <summary>Valor de <c>OriginEventKey</c> de «Añadir a la caja» para una CRÍA de la Librería (las entradas de cría no llevan clave de encuentro): el motor fija el huevo que el Core enumera para la especie.</summary>
    public const string LibraryEggKey = "library:egg";

    /// <summary>Version de la politica de legalizacion (se incluye en la clave de cache y en el perfil de reglas).</summary>
    public const string PolicyVersion = "auto-v1";

    /// <summary>Version de este ensamblado de contratos (se fija en el manifiesto de entrega).</summary>
    public const string ContractsVersion = "3.0.0";

    /// <summary>Tamano maximo de una entidad (un PK9 de caja son 328 bytes; el mayor formato conocido es de 0x1A0).</summary>
    public const int MaxEntityBytes = 4096;

    /// <summary>Tamano maximo de un documento de solicitud (el Worker no lee mas de esto de stdin).</summary>
    public const int MaxRequestBytes = 64 * 1024;

    /// <summary>Argumento del Worker que imprime su <see cref="WorkerManifest"/> y termina (para comprobar versiones antes de generar).</summary>
    public const string ManifestArgument = "--manifest";

    /// <summary>Codigo de salida del Worker cuando devuelve una respuesta bien formada (incluidos los estados de fallo del dominio).</summary>
    public const int ExitOk = 0;

    /// <summary>Codigo de salida del Worker cuando ni siquiera pudo producir una respuesta JSON (error de infraestructura).</summary>
    public const int ExitInfrastructure = 70;
}
