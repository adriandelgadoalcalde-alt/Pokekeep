// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
namespace Pokekeep.Mobile.Ui.Ajustes.Pkhex;

/// <summary>
/// Textos en castellano de España de los ajustes de PKHeX que <c>lang_es.txt</c> no trae, los deja en inglés o los traduce con un latinoamericanismo. Clave: la RUTA del ajuste (<c>Legality.Handler.Restrictions.AllowHandleOTGen6</c>)
/// o el NOMBRE de la propiedad (vale para todas las rutas que acaben así). Cada entrada lleva su motivo en un comentario; <c>LocalizacionAjustesPkhexTests</c> rechaza las que tapan una traducción buena.
/// Un bloque de la F10 añade aquí lo que su grupo necesite, en el fichero parcial de su bloque (<c>RespaldoAjustesPkhex.B10_N.cs</c>) para no pisarse al fusionar.
/// </summary>
public static partial class RespaldoAjustesPkhex
{
    private static readonly Lazy<IReadOnlyDictionary<string, string>> _etiquetas = new(() => Unir(AnadirEtiquetas));
    private static readonly Lazy<IReadOnlyDictionary<string, string>> _descripciones = new(() => Unir(AnadirDescripciones));

    public static IReadOnlyDictionary<string, string> Etiquetas => _etiquetas.Value;
    public static IReadOnlyDictionary<string, string> Descripciones => _descripciones.Value;

    private static IReadOnlyDictionary<string, string> Unir(Action<Action<string, string>> origen)
    {
        var d = new Dictionary<string, string>(StringComparer.Ordinal);
        origen((clave, texto) => { if (!d.TryAdd(clave, texto)) throw new InvalidOperationException($"El respaldo de ajustes repite la clave «{clave}»."); });
        return d;
    }

    private static void AnadirEtiquetas(Action<string, string> a)
    {
        AnadirEtiquetasPiloto(a);
        AnadirEtiquetasB10_1(a); AnadirEtiquetasB10_2(a); AnadirEtiquetasB10_3(a); AnadirEtiquetasB10_4(a); AnadirEtiquetasB10_5(a); AnadirEtiquetasB10_6(a); AnadirEtiquetasB10_7(a);
        AnadirEtiquetasB10_8(a); AnadirEtiquetasB10_9(a); AnadirEtiquetasB10_10(a); AnadirEtiquetasB10_11(a); AnadirEtiquetasB10_12(a); AnadirEtiquetasB10_13(a);
    }

    private static void AnadirDescripciones(Action<string, string> a)
    {
        AnadirDescripcionesPiloto(a);
        AnadirDescripcionesB10_1(a); AnadirDescripcionesB10_2(a); AnadirDescripcionesB10_3(a); AnadirDescripcionesB10_4(a); AnadirDescripcionesB10_5(a); AnadirDescripcionesB10_6(a); AnadirDescripcionesB10_7(a);
        AnadirDescripcionesB10_8(a); AnadirDescripcionesB10_9(a); AnadirDescripcionesB10_10(a); AnadirDescripcionesB10_11(a); AnadirDescripcionesB10_12(a); AnadirDescripcionesB10_13(a);
    }

    // Un método parcial sin implementación desaparece al compilar: cada bloque implementa SOLO los suyos, en su propio fichero.
    static partial void AnadirEtiquetasPiloto(Action<string, string> a);
    static partial void AnadirDescripcionesPiloto(Action<string, string> a);
    static partial void AnadirEtiquetasB10_1(Action<string, string> a);
    static partial void AnadirDescripcionesB10_1(Action<string, string> a);
    static partial void AnadirEtiquetasB10_2(Action<string, string> a);
    static partial void AnadirDescripcionesB10_2(Action<string, string> a);
    static partial void AnadirEtiquetasB10_3(Action<string, string> a);
    static partial void AnadirDescripcionesB10_3(Action<string, string> a);
    static partial void AnadirEtiquetasB10_4(Action<string, string> a);
    static partial void AnadirDescripcionesB10_4(Action<string, string> a);
    static partial void AnadirEtiquetasB10_5(Action<string, string> a);
    static partial void AnadirDescripcionesB10_5(Action<string, string> a);
    static partial void AnadirEtiquetasB10_6(Action<string, string> a);
    static partial void AnadirDescripcionesB10_6(Action<string, string> a);
    static partial void AnadirEtiquetasB10_7(Action<string, string> a);
    static partial void AnadirDescripcionesB10_7(Action<string, string> a);
    static partial void AnadirEtiquetasB10_8(Action<string, string> a);
    static partial void AnadirDescripcionesB10_8(Action<string, string> a);
    static partial void AnadirEtiquetasB10_9(Action<string, string> a);
    static partial void AnadirDescripcionesB10_9(Action<string, string> a);
    static partial void AnadirEtiquetasB10_10(Action<string, string> a);
    static partial void AnadirDescripcionesB10_10(Action<string, string> a);
    static partial void AnadirEtiquetasB10_11(Action<string, string> a);
    static partial void AnadirDescripcionesB10_11(Action<string, string> a);
    static partial void AnadirEtiquetasB10_12(Action<string, string> a);
    static partial void AnadirDescripcionesB10_12(Action<string, string> a);
    static partial void AnadirEtiquetasB10_13(Action<string, string> a);
    static partial void AnadirDescripcionesB10_13(Action<string, string> a);
}
