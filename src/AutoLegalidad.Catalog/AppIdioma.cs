// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace AutoLegalidad.Ui;

/// <summary>
/// X36e: idioma de los textos de esta capa compartida. El ESCRITORIO no lo toca y sigue siempre en castellano ("es", el valor por defecto): cada texto se escribe como la pareja (castellano, inglés) y
/// <see cref="T"/> / <see cref="F"/> devuelven el castellano tal cual estaba. La aplicación móvil enchufa <see cref="Proveedor"/> (el idioma de los datos de la interfaz) y, desde entonces, los textos
/// siguen al idioma elegido sin que nadie tenga que avisar del cambio.
/// </summary>
public static class AppIdioma
{
    private static string _actual = "es";

    /// <summary>Quien dice el idioma vigente ("es" o "en"); null en el escritorio.</summary>
    public static Func<string>? Proveedor { get; set; }

    /// <summary>"es" (por defecto) o "en".</summary>
    public static string Actual
    {
        get => Proveedor?.Invoke() ?? _actual;
        set => _actual = value;
    }

    public static bool Ingles => Actual == "en";

    public static string T(string es, string en) => Ingles ? en : es;

    /// <summary>Texto con huecos <c>{0}</c>…: se rellena con la cultura actual, igual que una cadena interpolada.</summary>
    public static string F(string es, string en, params object?[] argumentos) => string.Format(Ingles ? en : es, argumentos);
}
