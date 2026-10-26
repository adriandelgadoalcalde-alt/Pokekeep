// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Reflection;
using AutoLegalidad.Contracts;

namespace Pokekeep.Mobile.Engine;

/// <summary>
/// Identidad de los cuatro binarios de ESTE paquete, calculada al compilar (target <c>GenerarIdentidad</c>) e incrustada como metadatos del ensamblado: en Android <c>Assembly.Location</c> esta vacio y los
/// ensamblados no son ficheros. Se instala como <see cref="BinaryIdentity.Current"/> al arrancar cada proceso (interfaz y motor). Un valor ausente es <see cref="FileBinaryIdentity.Absent"/> (VersionMismatch).
/// </summary>
public sealed class EmbeddedBinaryIdentity : IBinaryIdentity
{
    public static readonly EmbeddedBinaryIdentity Instancia = new();

    private static string Meta(string clave)
        => typeof(EmbeddedBinaryIdentity).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == clave)?.Value ?? FileBinaryIdentity.Absent;

    public string CoreSha256 => Meta("Pokekeep.CoreSha256");
    public string EngineSha256 => Meta("Pokekeep.EngineSha256");
    public string AdapterSha256 => Meta("Pokekeep.AdapterSha256");
    public string AlmSha256 => Meta("Pokekeep.AlmSha256");

    /// <summary>Instala esta identidad como la del proceso (llamar al arrancar, antes de abrir el catalogo o la cache).</summary>
    public static void Instalar() => BinaryIdentity.Current = Instancia;
}
