// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Reflection;
using AutoLegalidad.Contracts;
using PKHeX.Core;

namespace AutoLegalidad.Ui.Operation;

/// <summary>
/// Comprobación de la pareja de versiones antes de generar (plan 5: «no mezclar un plugin antiguo con DLL nuevas»): el Worker declara su manifiesto
/// y la interfaz lo compara con el PKHeX.Core que tiene cargado. Si no coinciden, no se genera nada.
/// </summary>
public static class PairCheck
{
    public sealed record Result(bool Compatible, string? Problem, bool IdenticalCoreBinary);

    /// <summary>
    /// Informe de la identidad del Core cargado en este proceso: versión y commit (del propio ensamblado, que viajan en sus metadatos) y SHA-256 (de <paramref name="identity"/>; por defecto
    /// <see cref="BinaryIdentity.Current"/>: el fichero en escritorio, la constante incrustada en Android). Sin SHA-256 conocido, cadena vacía.
    /// </summary>
    public static (string Version, string Commit12, string Sha256) LocalCore(IBinaryIdentity? identity = null)
    {
        var asm = typeof(PKM).Assembly;
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? string.Empty;
        var plus = info.IndexOf('+');
        var commit = plus >= 0 ? info[(plus + 1)..] : string.Empty;
        var sha = (identity ?? BinaryIdentity.Current).CoreSha256;
        if (sha == FileBinaryIdentity.Absent) sha = string.Empty;
        return (plus >= 0 ? info[..plus] : info, commit, sha);
    }

    public static Result Evaluate(WorkerManifest m, (string Version, string Commit12, string Sha256)? local = null)
    {
        var core = local ?? LocalCore();
        if (m.ProtocolVersion != Protocol.Version)
            return new(false, AppIdioma.F("El motor habla el protocolo {0} y esta interfaz el {1}.", "The engine speaks protocol {0} and this interface speaks {1}.", m.ProtocolVersion, Protocol.Version), false);
        if (m.PolicyVersion != Protocol.PolicyVersion)
            return new(false, AppIdioma.F("El motor usa la política '{0}' y esta interfaz '{1}'.", "The engine uses policy '{0}' and this interface '{1}'.", m.PolicyVersion, Protocol.PolicyVersion), false);
        if (string.IsNullOrWhiteSpace(m.DatasetSnapshotId))
            return new(false, AppIdioma.T("La base de datos del motor no tiene ningún snapshot verificado.", "The engine’s database has no verified snapshot."), false);
        if (core.Commit12.Length == 0 || !m.CoreCommit.StartsWith(core.Commit12, StringComparison.OrdinalIgnoreCase))
            return new(false, AppIdioma.F("El PKHeX.Core de esta ventana ({0}+{1}) no es el del motor ({2}, {3}).", "This window’s PKHeX.Core ({0}+{1}) isn’t the engine’s ({2}, {3}).", core.Version, core.Commit12, m.CoreVersion, m.CoreCommit[..Math.Min(12, m.CoreCommit.Length)]), false);
        var identical = core.Sha256.Length > 0 && string.Equals(core.Sha256, m.CoreDllSha256, StringComparison.OrdinalIgnoreCase);
        // Paquete portable (Hito 6, R-008): hay UN solo PKHeX.Core.dll, compartido por la interfaz y el Worker; si el motor declara otro binario no se genera nada.
        if (!identical && Portable.PortableMode.IsActive)
            return new(false, AppIdioma.T("El motor y la interfaz no usan el mismo PKHeX.Core.dll (en el paquete portable debe ser el mismo fichero). Vuelve a descomprimir el paquete original.", "The engine and the interface don’t use the same PKHeX.Core.dll (in the portable package it must be the same file). Unzip the original package again."), false);
        return new(true, null, identical);
    }
}
