// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Text.RegularExpressions;

namespace AutoLegalidad.Registro;

/// <summary>
/// Privacidad del registro (ADR 0040): ninguna entrada lleva la ruta del perfil del usuario (<c>&lt;unidad&gt;:\Users\&lt;nombre&gt;</c> pasa a <c>%USERPROFILE%</c>, tambien con barras normales) ni el
/// nombre de usuario de Windows suelto. El registro NUNCA recibe OT, TID/SID ni nombre de entrenador: quien registra no los pasa (ver las llamadas).
/// </summary>
public static partial class Saneador
{
    [GeneratedRegex(@"(?i)[A-Z]:[\\/]+Users[\\/]+[^\\/\s""'<>|:*?]+")]
    private static partial Regex PerfilGenerico();

    private static readonly List<(string Ruta, string Alias)> Raices = [];

    /// <summary>
    /// Registra una carpeta que ninguna entrada debe mostrar tal cual y el nombre con el que se sustituye (Android: la carpeta privada de la aplicación pasa a <c>&lt;paquete&gt;</c>, ADR 0040 en el móvil).
    /// Se aplica ademas de la sustitución del perfil de usuario.
    /// </summary>
    public static void RegistrarRaiz(string ruta, string alias)
    {
        if (string.IsNullOrWhiteSpace(ruta)) return;
        lock (Raices)
            if (!Raices.Exists(r => string.Equals(r.Ruta, ruta, StringComparison.OrdinalIgnoreCase))) Raices.Add((ruta.TrimEnd('\\', '/'), alias));
    }

    public static string Sanear(string? texto)
    {
        if (string.IsNullOrEmpty(texto)) return string.Empty;
        var t = texto;
        (string Ruta, string Alias)[] raices;
        lock (Raices) raices = [.. Raices];
        foreach (var (ruta, alias) in raices) t = t.Replace(ruta, alias, StringComparison.OrdinalIgnoreCase);   // primero las raices registradas (pueden estar dentro del perfil)
        return Sanear(t, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), Environment.UserName);
    }

    /// <summary>Version determinista para pruebas: <paramref name="perfil"/> es la ruta real del perfil y <paramref name="usuario"/> el nombre de usuario.</summary>
    public static string Sanear(string? texto, string? perfil, string? usuario)
    {
        if (string.IsNullOrEmpty(texto)) return string.Empty;
        var t = texto;
        if (!string.IsNullOrWhiteSpace(perfil))
        {
            var p = perfil.TrimEnd('\\', '/');
            t = t.Replace(p, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
            t = t.Replace(p.Replace('\\', '/'), "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
        }
        t = PerfilGenerico().Replace(t, "%USERPROFILE%");
        // El nombre de usuario suelto solo si es lo bastante largo para no destrozar palabras comunes.
        if (!string.IsNullOrWhiteSpace(usuario) && usuario.Length >= 4)
            t = Regex.Replace(t, @"(?<![\p{L}\p{N}_])" + Regex.Escape(usuario) + @"(?![\p{L}\p{N}_])", "<usuario>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return t;
    }
}
