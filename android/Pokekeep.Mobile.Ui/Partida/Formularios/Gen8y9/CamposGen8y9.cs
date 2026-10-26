// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Globalization;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen8y9;

/// <summary>Campos que comparten varios formularios del bloque 8b-2.</summary>
internal static class CamposGen8y9
{
    private static bool EsHex(string s, int cifras) => s.Length >= 1 && s.Length <= cifras && s.All(Uri.IsHexDigit);

    /// <summary>
    /// Un valor de 64 (o 32) bits en hexadecimal sin prefijo y con ceros a la izquierda: como lo enseñan el cuadro de propiedades de PKHeX (<c>TypeConverterU64</c> / <c>TypeConverterU32</c>, 16 u 8 cifras) y las semillas
    /// de las teraincursiones. Un texto que no es hexadecimal se rechaza con su motivo (el convertidor de PKHeX lo tomaría por 0 y escribiría un cero sin avisar).
    /// </summary>
    public static CampoP Hex(string id, string etiqueta, Func<ContextoP, ulong> leer, Action<ContextoP, ulong> escribir, bool de32Bits = false, string? ayuda = null, Func<ContextoP, bool>? visible = null)
    {
        var cifras = de32Bits ? 8 : 16;
        return new CampoP
        {
            Id = id, Etiqueta = etiqueta, Tipo = TipoP.Texto, Ayuda = ayuda ?? IdiomaInterfaz.F("CamposGen8y9.CifrasHexadecimales", cifras), Visible = visible, MaxLongitud = cifras,
            Leer = c => leer(c).ToString(de32Bits ? "X8" : "X16", CultureInfo.InvariantCulture),
            Escribir = (c, v) => escribir(c, ulong.Parse((string)v!, NumberStyles.HexNumber, CultureInfo.InvariantCulture)),
            Validar = (_, t) => EsHex(t.Trim(), cifras) ? null : IdiomaInterfaz.F("CamposGen8y9.EscribeCifrasHexadecimalesF", cifras),
            // Lo que llegue al borrador se queda siempre en cifras hexadecimales validas (una orden que no pasa por el teclado, como la de las pruebas, tambien): se descartan los caracteres que no lo son y se rellena con ceros.
            Normaliza = t => (ulong.TryParse(new string([.. t.Where(Uri.IsHexDigit).Take(cifras)]), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var n) ? n : 0).ToString(de32Bits ? "X8" : "X16", CultureInfo.InvariantCulture),
        };
    }
}
