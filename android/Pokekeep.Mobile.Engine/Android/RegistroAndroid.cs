// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AutoLegalidad.Registro;

namespace Pokekeep.Mobile.Engine.Plataforma;

/// <summary>
/// Registro de fallos en Android (ADR 0040, plan 5.8): <c>FilesDir/registros/</c> (privado, fuera de la copia de seguridad), UN fichero por proceso (<c>pokekeep-AAAA-MM-DD-ui.log</c> y <c>-motor.log</c>: la
/// interfaz y el servicio <c>:motor</c> son procesos distintos y un Mutex con nombre no los une), cerrojo en proceso, la carpeta privada saneada a <c>&lt;paquete&gt;</c> y nunca OT/TID/SID. «Compartir» es una
/// accion del usuario (nunca sale solo).
/// </summary>
public static class RegistroAndroid
{
    public static string? Carpeta { get; private set; }

    public static void Configurar(Context contexto, string sufijoProceso)
    {
        var privada = contexto.FilesDir!.AbsolutePath;
        var carpeta = Path.Combine(privada, "registros");
        Saneador.RegistrarRaiz(privada, "<" + contexto.PackageName + ">");
        if (contexto.CacheDir?.AbsolutePath is { } cache) Saneador.RegistrarRaiz(cache, "<" + contexto.PackageName + "-cache>");
        if (contexto.GetExternalFilesDir(null)?.AbsolutePath is { } externa) Saneador.RegistrarRaiz(externa, "<" + contexto.PackageName + "-externo>");
        string version;
        try { version = contexto.PackageManager!.GetPackageInfo(contexto.PackageName!, 0)?.VersionName ?? "?"; } catch (Exception) { version = "?"; }
        RegistroFallos.Configurar(sufijoProceso == "motor" ? "Motor" : "Interfaz", () => EmbeddedBinaryIdentity.Instancia.CoreSha256 is { Length: >= 12 } h ? h[..12] : null,
            new RegistroRutas(carpeta, null, version, () => EmbeddedBinaryIdentity.Instancia.CoreSha256 is { Length: >= 12 } h ? h[..12] : null, _ => new ProcesoRegistroLock(), SufijoFichero: "-" + sufijoProceso));
        Carpeta = carpeta;
        RegistroFallos.InstalarCapturaGlobal();
        RegistroFallos.IniciarSesion($"{sufijoProceso} · Android {Build.VERSION.SdkInt}");
    }

    /// <summary>Ficheros de registro de la carpeta privada (mas recientes primero) para la pantalla Registros.</summary>
    public static IReadOnlyList<FileInfo> Ficheros()
        => Carpeta is { } c && Directory.Exists(c) ? [.. new DirectoryInfo(c).GetFiles("pokekeep-*.log").OrderByDescending(f => f.LastWriteTimeUtc)] : [];
}
