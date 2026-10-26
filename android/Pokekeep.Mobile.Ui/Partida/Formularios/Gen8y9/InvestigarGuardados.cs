// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Runtime.CompilerServices;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Partida.Formularios.Gen8y9;

/// <summary>
/// La «investigación» de los editores de banderas de Gen. 8-9 (<c>GB_Research</c> de <c>SAV_FlagWork8b</c> y <c>SAV_FlagWork9a</c>, y <c>Tab_Compare</c> de <c>SAV_BlockDump8</c>): se eligen dos guardados del mismo juego, el de
/// antes y el de después de hacer algo en la partida, y se enseña lo que cambió. No escribe nada en la partida abierta: los dos campos de archivo son de ayuda (<see cref="CampoP.SinEscritura"/>) y el resultado sale como el
/// texto secundario del segundo. Los guardados se leen de los bytes que da el selector (en WinForms se arrastran archivos o se eligen con un cuadro de abrir).
/// </summary>
internal static class InvestigarGuardados
{
    /// <summary>Tamaño máximo de un guardado de BDSP en la investigación (el mismo tope de 1 MB de <c>EventWorkDiff8b</c>).</summary>
    public const int MaximoBytes = 0x10_0000;
    /// <summary>Tamaño máximo de un guardado de Switch (el de Z-A mide unos 3 MB; <c>SAV_FlagWork9a</c> no pone ningún tope).</summary>
    public const int MaximoBytesSwitch = 0x100_0000;
    /// <summary>Líneas máximas del resumen (el resto se cuenta, no se enseña).</summary>
    public const int MaximoLineas = 400;

    private static readonly ConditionalWeakTable<byte[], SaveFile?> Leidos = new();

    /// <summary>El guardado que son esos bytes, o null si no lo reconoce el Core (se lee una sola vez por archivo elegido).</summary>
    public static SaveFile? Leer(byte[]? datos, int maximo = MaximoBytes)
    {
        if (datos is null || datos.Length == 0 || datos.Length > maximo) return null;
        return Leidos.GetValue(datos, d => { try { return SaveUtil.TryGetSaveFile(d.ToArray(), out var s) ? s : null; } catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IndexOutOfRangeException or NotSupportedException) { return null; } });
    }

    /// <summary>Un campo de archivo de «investigación»: elegir y quitar, sin escribir en la partida. <paramref name="validar"/> rechaza con su motivo lo que no sirve.</summary>
    public static CampoP Campo(string id, string etiqueta, Func<SaveFile, bool> admite, string rechazo, string ayuda, Func<ContextoP, string?>? detalle = null, int maximo = MaximoBytes)
        => new()
        {
            Id = id, Etiqueta = etiqueta, Tipo = TipoP.Archivo, Leer = _ => null, Escribir = null, SinEscritura = true, TextoBoton = etiqueta, Ayuda = ayuda, Detalle = detalle,
            ValidarArchivo = (_, b) => b.Length > maximo ? IdiomaInterfaz.T("ModeloEventos.ArchivoDemasiadoGrandeSer") : Leer(b, maximo) is { } s && admite(s) ? null : rechazo,
        };

    /// <summary>Una lista de números para el texto: los primeros y cuántos más hay.</summary>
    public static string Numeros(IReadOnlyList<int> valores, string formato = "0000")
    {
        if (valores.Count == 0) return "ninguna";
        var primeros = string.Join(", ", valores.Take(MaximoLineas).Select(v => v.ToString(formato)));
        return valores.Count > MaximoLineas ? IdiomaInterfaz.F("InvestigarGuardados.0Y1Mas", primeros, valores.Count - MaximoLineas) : primeros;
    }
}
