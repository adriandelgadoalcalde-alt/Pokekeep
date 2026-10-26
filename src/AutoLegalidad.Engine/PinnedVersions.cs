// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace AutoLegalidad.Engine;

/// <summary>
/// Pareja de versiones FIJADA de esta entrega (fuentes.lock.json, ADR 0001/0002). Una prueba comprueba que coincide con el fichero de bloqueo:
/// si cambia la pareja hay que actualizar fuentes.lock.json, el catalogo y estas constantes a la vez.
/// </summary>
public static class PinnedVersions
{
    public const string CoreTag = "26.10.18";
    public const string CoreCommit = "bfe89c74a3f5265805e364b79bc04ad38ebbc5ea";
    public const string AlmTag = "26.10.18";
    public const string AlmCommit = "b13c011bb0c4c3d475ca7f0a30b880f5b24dbed7";
    public const string EngineVersion = "3.2.0";
    public const string AdapterVersion = "2.0.0";
    public static string CoreCommit12 => CoreCommit[..12];
    public static string ReleaseId => $"core-{CoreTag}-{CoreCommit12}";
}
