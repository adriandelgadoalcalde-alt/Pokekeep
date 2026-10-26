// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Security.Cryptography;
using Android.Content;
using Android.Content.PM;
using AutoLegalidad.Ui.Operation;
using Pokekeep.Mobile.Engine;
using Pokekeep.Mobile.Operaciones;
using Pokekeep.Mobile.Ui.Mas;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Platform;

/// <summary>
/// Lo propio del paquete de Android para «Acerca de»: versión y código del APK (de <c>PackageManager</c>), SHA-256 del certificado de firma, manifiesto de datos (los hashes que generó la compilación) y la
/// pareja de versiones (PKHeX.Core y los hashes de los binarios incrustados). Todo se lee del propio paquete; nada se consulta fuera del dispositivo.
/// </summary>
public static class AcercaDeAndroid
{
    public static IAcercaDe Crear(Context contexto, AlmacenDatos datos)
        => new AcercaDePokekeep(() => Paquete(contexto, datos), () => Pareja(), () => CambiosDePkhex(contexto));

    public static InfoPaquete? Paquete(Context contexto, AlmacenDatos datos)
    {
        try
        {
            var pm = contexto.PackageManager!;
            var info = pm.GetPackageInfo(contexto.PackageName!, PackageInfoFlags.SigningCertificates)!;
            string? firma = null;
            if (info.SigningInfo?.GetApkContentsSigners() is { Length: > 0 } firmantes)
                firma = Convert.ToHexString(SHA256.HashData(firmantes[0].ToByteArray()!));
            return new InfoPaquete(info.VersionName ?? "?", info.LongVersionCode, firma, [.. datos.LeerManifiestoDelApk().Select(e => (e.Nombre, e.Bytes, e.Sha256))]);
        }
        catch (Exception ex) when (ex is PackageManager.NameNotFoundException or Java.Lang.Exception) { return null; }
    }

    public static IReadOnlyList<DatoPaquete> Pareja()
    {
        var (version, commit, sha) = PairCheck.LocalCore(EmbeddedBinaryIdentity.Instancia);
        var id = EmbeddedBinaryIdentity.Instancia;
        string Corto(string h) => h.Length >= 12 ? h[..12] : h;
        return
        [
            new DatoPaquete("PKHeX.Core", $"{version} · commit {commit}" + (sha.Length > 0 ? $" · SHA-256 {Corto(sha)}…" : string.Empty)),
            new DatoPaquete(IdiomaInterfaz.T("AcercaDeAndroid.MotorYAdaptador"), $"SHA-256 {Corto(id.EngineSha256)}… · {Corto(id.AdapterSha256)}…"),
            new DatoPaquete("Auto Legality Mod", $"SHA-256 {Corto(id.AlmSha256)}…"),
        ];
    }

    /// <summary>El registro de cambios original de PKHeX, empaquetado como asset (<c>pkhex/changelog.txt</c>); vacío si el paquete no lo lleva.</summary>
    public static string? CambiosDePkhex(Context contexto)
    {
        try
        {
            using var s = contexto.Assets!.Open("pkhex/changelog.txt");
            using var r = new StreamReader(s, System.Text.Encoding.UTF8);
            return r.ReadToEnd();
        }
        catch (Java.IO.FileNotFoundException) { return null; }
    }
}
