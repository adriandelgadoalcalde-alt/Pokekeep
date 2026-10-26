// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen5;

/// <summary>Utilidades comunes de los formularios de la 5.ª generación (bloque 8a-3).</summary>
internal static class AyudasGen5
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, IReadOnlyDictionary<string, string>> Mensajes = new();

    private static IReadOnlyDictionary<string, string> MensajesDe(string idioma) => Mensajes.GetOrAdd(idioma, i =>
    {
        // MessageStrings_<idioma>.txt del Core: una linea «Propiedad = texto» por mensaje (el original los carga en el idioma del programa con LocalizationUtil).
        var d = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var linea in Util.GetStringList("MessageStrings_" + i))
        {
            var j = linea.IndexOf(" = ", StringComparison.Ordinal);
            if (j > 0) d.TryAdd(linea[..j], linea[(j + 3)..]);
        }
        return d;
    });

    /// <summary>Un texto de <c>MessageStrings</c> del Core en el idioma vigente (<c>MsgGeonetPointRed</c>, <c>MsgFileSizeIncorrect</c>); si no existe, el nombre de la propiedad.</summary>
    public static string MensajeCore(string propiedad) => MensajesDe(Pokekeep.Mobile.Ui.Ajustes.Pkhex.ProgramaPkhex.Idioma).TryGetValue(propiedad, out var t) ? t : propiedad;
    /// <summary>Hoy, como la fecha de la consola DS que usa el Core (<c>EncounterDate.GetDateNDS</c>).</summary>
    public static DateTime HoyNds() => EncounterDate.GetDateNDS().ToDateTime(TimeOnly.MinValue);

    /// <summary>Nombre de archivo sin caracteres que Windows y Android rechazan (el <c>PathUtil.CleanFileName</c> del Core).</summary>
    public static string NombreLimpio(string nombre) => PathUtil.CleanFileName(nombre);
}
