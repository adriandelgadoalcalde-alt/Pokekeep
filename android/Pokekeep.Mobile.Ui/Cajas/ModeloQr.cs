// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;

namespace Pokekeep.Mobile.Ui.Cajas;

/// <summary>Lo que hay dentro del mensaje de un código QR leído (<c>Main.ImportQRToTabs</c> de PKHeX): un Pokémon o un regalo misterioso que contiene un Pokémon.</summary>
public sealed record QrLeido(PKM Pokemon, bool EsRegalo);

/// <summary>
/// Leer el mensaje de un código QR como lo hace PKHeX (bloque 10-9, PK-IO-055): primero un Pokémon (<c>QRMessageUtil.GetPKM</c>: la 7.ª generación, las direcciones de la 6.ª y los datos en base 64) y, si no lo es, un regalo misterioso
/// (<c>FileUtil.TryGetMysteryGift</c> sobre los datos del mensaje) que se convierte en su Pokémon. PKHeX descarga el mensaje de una DIRECCIÓN de imagen por internet (<c>api.qrserver.com</c>); aquí la imagen la elige el usuario y la
/// lee <c>LectorQr</c> en el dispositivo (regla 15). Los avisos de error son los del Core (<c>MsgQRDecodeFail</c>, <c>MsgQRDecodeSize</c>).
/// </summary>
public static class ModeloQr
{
    public static QrLeido? Leer(string mensaje, SaveFile sav)
    {
        var pk = QRMessageUtil.GetPKM(mensaje, sav.Context);
        if (pk is not null) return new QrLeido(pk, false);
        var datos = DatosBase64(mensaje);
        if (datos is null || datos.Length == 0) return null;
        MysteryGift? regalo;
        try { regalo = MysteryGift.GetMysteryGift(datos); }
        catch (Exception ex) when (ex is ArgumentException or IndexOutOfRangeException or InvalidOperationException) { return null; }
        if (regalo is not { IsEntity: true }) return null;
        return new QrLeido(regalo.ConvertToPKM(sav), true);
    }

    /// <summary>Los datos en base 64 que van tras la <c>#</c> de la dirección del mensaje (<c>QRMessageUtil.DecodeMessageDataBase64</c>, que es privado); null si no hay.</summary>
    internal static byte[]? DatosBase64(string mensaje)
    {
        var i = mensaje.IndexOf('#');
        if (i < 0 || i == mensaje.Length - 1) return null;
        var texto = mensaje.AsSpan(i + 1);
        var tmp = new byte[texto.Length];
        return Convert.TryFromBase64Chars(texto, tmp, out var n) ? tmp[..n] : null;
    }

    /// <summary>El aviso de PKHeX cuando el mensaje no es un Pokémon ni un regalo: «Los datos decodificados no son PKM/Regalo válidos.» y el tamaño de los datos.</summary>
    public static string MensajeNoValido(string mensaje)
    {
        Ajustes.Pkhex.TextosNucleo.Preparar();
        return MessageStrings.MsgQRDecodeFail + " " + string.Format(MessageStrings.MsgQRDecodeSize, mensaje.Length);
    }
}
