// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Ui.AcercaDe;
using Pokekeep.Mobile.Ui.Mas;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Operaciones;

/// <summary>Lo propio del paquete de Android que «Acerca de» enseña: versión y código del APK, firma y manifiesto de datos (SHA-256 de la base de datos, la caché y el atlas). Lo lee la plataforma del sistema; el PC y las pruebas dan los suyos.</summary>
public sealed record InfoPaquete(string VersionName, long VersionCode, string? FirmaSha256, IReadOnlyList<(string Nombre, long Bytes, string Sha256)> Datos);

/// <summary>
/// «Acerca de Pokekeep» en el móvil: el texto es EXACTAMENTE el de <see cref="AcercaDeModelo"/> del núcleo (autoría, créditos y licencias, avisos, novedades por versión), la versión es la del ensamblado
/// (la misma fuente única <c>src\PokekeepVersion.props</c> que el escritorio y el APK) y se añade lo propio de Android: pareja de versiones (PKHeX.Core, motor, ALM), manifiesto de datos, código y firma del APK,
/// y el «Acerca de PKHeX» original (agradecimiento y registro de cambios de PKHeX).
/// </summary>
public sealed class AcercaDePokekeep(Func<InfoPaquete?> paquete, Func<IReadOnlyList<DatoPaquete>>? pareja = null, Func<string?>? cambiosPkhex = null) : IAcercaDe
{
    public const string AgradecimientoPkhex = "Thanks to all the researchers!";

    /// <summary>«Versión 0.7.0 (Android)»; si el APK declara otra versión que el ensamblado lo dice (no debería pasar: ambas salen de <c>PokekeepVersion.props</c>).</summary>
    public static string LineaVersion(string real, string? delPaquete)
        => delPaquete is null ? IdiomaInterfaz.F("AcercaDePokekeep.VersionDesarrollo", real) : delPaquete == real ? IdiomaInterfaz.F("AcercaDePokekeep.VersionAndroid", real) : IdiomaInterfaz.F("AcercaDePokekeep.VersionElPaqueteDe", real, delPaquete);

    public ContenidoAcercaDe Obtener()
    {
        var real = AcercaDeModelo.VersionEnsamblado();
        var info = paquete();
        var datos = new List<DatoPaquete>();
        if (pareja is not null) datos.AddRange(pareja());
        if (info is not null)
        {
            datos.Add(new DatoPaquete(IdiomaInterfaz.T("AcercaDePokekeep.VersionDelPaquete"), IdiomaInterfaz.F("AcercaDePokekeep.Codigo", info.VersionName, info.VersionCode)));
            datos.Add(new DatoPaquete(IdiomaInterfaz.T("AcercaDePokekeep.FirmaDelPaquete"), info.FirmaSha256 is { Length: > 0 } f ? "SHA-256 " + f : IdiomaInterfaz.T("AcercaDePokekeep.SinFirmaCompilacionDe")));
            foreach (var d in info.Datos) datos.Add(new DatoPaquete(d.Nombre, $"{d.Bytes / (1024.0 * 1024.0):N1} MB · SHA-256 {d.Sha256[..Math.Min(16, d.Sha256.Length)]}…"));
        }
        return new ContenidoAcercaDe(
            AcercaDeModelo.Nombre, AcercaDeModelo.LemaAndroid, real, LineaVersion(real, info?.VersionName), AcercaDeModelo.Autoria,
            [.. AcercaDeModelo.Creditos.Select(c => new CreditoAcerca(c.Componente, c.Licencia, c.Uso))],
            AcercaDeModelo.Licencia, AcercaDeModelo.AvisoEventos, AcercaDeModelo.AvisoMarcas, AcercaDeModelo.AvisoPrivacidad,
            [.. AcercaDeModelo.Novedades.Select(n => new NovedadAcerca(n.Version, n.Fecha, n.Resumen, n.Novedades))],
            datos, IdiomaInterfaz.T("AcercaDePokekeep.AcercaDePkhex"), AgradecimientoPkhex, cambiosPkhex?.Invoke() ?? string.Empty);
    }
}
