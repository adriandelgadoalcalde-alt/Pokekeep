using System.Globalization;
using System.Numerics;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida;

/// <summary>Hacia qué lado se ajustó un número escrito fuera de su rango.</summary>
public enum AjusteTope { Ninguno, AlMaximo, AlMinimo }

/// <summary>
/// Regla única de la versión 1.1.3 (ADR 0108, pedido de Adrian del 15-nov-2026): un número escrito por encima del tope del juego
/// no se rechaza, se queda en el tope («si escribes 999999999 automáticamente te pone que es lo máximo»). Por debajo del mínimo pasa
/// lo mismo, pero solo al terminar de escribir (<c>final</c>), para no cortar a quien teclea «150» en un campo que empieza en 100.
/// Se lee el número entero aunque no quepa en 64 bits (BigInteger), así que 99999999999999999999 también acaba en el tope.
/// </summary>
public static class TopeNumero
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>
    /// Lee un entero (decimal o hexadecimal) y lo deja dentro de [<paramref name="min"/>, <paramref name="max"/>]. Devuelve false si el
    /// texto no es un número. Con <paramref name="final"/> = false un valor por debajo del mínimo se devuelve tal cual (el que llama
    /// decide si lo rechaza mientras se escribe).
    /// </summary>
    public static bool Acotar(string texto, long min, long max, bool hex, bool final, out long valor, out AjusteTope ajuste)
    {
        valor = 0; ajuste = AjusteTope.Ninguno;
        var t = texto.Trim();
        if (!hex) t = t.Replace(".", string.Empty).Replace(" ", string.Empty).Replace(" ", string.Empty).Replace(" ", string.Empty);
        if (t.Length == 0) return false;
        BigInteger n;
        if (hex)
        {
            // AllowHexSpecifier lee «FF» como -1 (bit de signo): el 0 delante lo deja positivo.
            if (t.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) t = t[2..];
            if (t.Length == 0 || !BigInteger.TryParse("0" + t, NumberStyles.AllowHexSpecifier, Inv, out n)) return false;
        }
        else if (!BigInteger.TryParse(t, NumberStyles.AllowLeadingSign, Inv, out n)) return false;
        if (max < min) max = min;
        if (n > max) { valor = max; ajuste = AjusteTope.AlMaximo; return true; }
        if (n < min)
        {
            if (final) { valor = min; ajuste = AjusteTope.AlMinimo; return true; }
            valor = n < long.MinValue ? long.MinValue : (long)n;
            return true;
        }
        valor = (long)n;
        return true;
    }

    /// <summary>Lo mismo para un número con decimales (coma o punto).</summary>
    public static bool Acotar(string texto, double min, double max, bool final, out double valor, out AjusteTope ajuste)
    {
        valor = 0; ajuste = AjusteTope.Ninguno;
        var t = texto.Trim().Replace(',', '.');
        if (!double.TryParse(t, NumberStyles.Float, Inv, out var d) || double.IsNaN(d)) return false;
        if (max < min) max = min;
        if (d > max) { valor = max; ajuste = AjusteTope.AlMaximo; return true; }
        if (d < min)
        {
            if (final) { valor = min; ajuste = AjusteTope.AlMinimo; return true; }
            valor = d; return true;
        }
        valor = d;
        return true;
    }

    /// <summary>Aviso que ve el usuario debajo del campo cuando su número se ajustó (null si no hizo falta).</summary>
    public static string? Aviso(AjusteTope ajuste, object tope) => ajuste switch
    {
        AjusteTope.AlMaximo => IdiomaInterfaz.F("Form.AjustadoMaximo", tope),
        AjusteTope.AlMinimo => IdiomaInterfaz.F("Form.AjustadoMinimo", tope),
        _ => null,
    };
}
