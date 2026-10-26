// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Android.Content;
using Pokekeep.Mobile.Ui;
using Pokekeep.Mobile.Ui.Cajas;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Platform;

/// <summary>
/// «Compartir» por la hoja del sistema (<c>ACTION_SEND</c> con selector): el texto del set de Showdown, el archivo <c>.pk*</c>/<c>.ek*</c> o la imagen del QR (bloque 10-9, PK-MENU-038 y PK-IO-040). SOLO lo dispara una
/// acción explícita del usuario (regla 13) y el usuario elige la app (regla 15: Pokekeep no tiene permiso de internet). El archivo se escribe en <c>cache/compartido/</c> (privado de la app), se entrega con un
/// <c>content://</c> de solo lectura del <c>CompartirProvider</c> (un permiso por envío, no una ruta) y se borra a los <see cref="ModeloCompartir.VidaArchivo"/> o al siguiente envío.
/// Sin <c>Java.*</c> fuera de aquí: toda excepción del sistema se devuelve como el motivo en castellano. PENDIENTE DE DISPOSITIVO: la hoja y la entrega del archivo a otra app solo se ven en un teléfono.
/// </summary>
public sealed class CompartirAndroid(Context contexto) : ICompartirSistema
{
    /// <summary>Autoridad del proveedor de contenido que sirve los archivos temporales (la declara <c>CompartirProvider</c>).</summary>
    public const string Autoridad = "com.incredibad.pokekeep.compartir";
    public const string Carpeta = "compartido";

    /// <summary>Un texto largo viaja en el Intent por Binder (≈1 MB para todo el proceso): un set mide menos de 1 KB, un equipo de cajas completo puede pasarse, y entonces se pide copiarlo.</summary>
    public const int MaxCaracteresTexto = 100 * 1024;

    public static string RutaCarpeta(Context c) => System.IO.Path.Combine(c.CacheDir!.AbsolutePath, Carpeta);

    public string? CompartirTexto(string titulo, string texto)
    {
        if (texto.Length > MaxCaracteresTexto) return IdiomaInterfaz.T("CompartirAndroid.ElTextoEsDemasiado");
        try
        {
            var envio = new Intent(Intent.ActionSend).SetType("text/plain").PutExtra(Intent.ExtraSubject, titulo).PutExtra(Intent.ExtraText, texto);
            contexto.StartActivity(Intent.CreateChooser(envio, titulo)!.AddFlags(ActivityFlags.NewTask));
            return null;
        }
        catch (Java.Lang.Exception) { return IdiomaInterfaz.T("CompartirAndroid.NoSeHaPodido"); }
    }

    public string? CompartirArchivo(string titulo, string nombre, byte[] datos, string tipoMime)
    {
        try
        {
            var ruta = ModeloCompartir.EscribirTemporal(RutaCarpeta(contexto), new ArchivoParaCompartir(nombre, datos, tipoMime));
            var uri = Android.Net.Uri.Parse($"content://{Autoridad}/{Uri.EscapeDataString(System.IO.Path.GetFileName(ruta))}")!;
            var envio = new Intent(Intent.ActionSend).SetType(tipoMime)!.PutExtra(Intent.ExtraStream, uri)!.PutExtra(Intent.ExtraSubject, nombre)!.AddFlags(ActivityFlags.GrantReadUriPermission)!;
            envio.ClipData = ClipData.NewRawUri(nombre, uri);   // el permiso de lectura viaja con el ClipData del selector
            contexto.StartActivity(Intent.CreateChooser(envio, titulo)!.AddFlags(ActivityFlags.NewTask | ActivityFlags.GrantReadUriPermission));
            _ = ModeloCompartir.BorrarTrasAsync(ruta, ModeloCompartir.VidaArchivo);   // PKHeX borra el temporal a los 20 s (DeleteAsync); la hoja del sistema espera a que el usuario elija
            return null;
        }
        catch (Java.Lang.Exception) { return IdiomaInterfaz.T("CompartirAndroid.NoSeHaPodido"); }
        catch (IOException) { return IdiomaInterfaz.T("CompartirAndroid.NoSeHaPodido3"); }
        catch (UnauthorizedAccessException) { return IdiomaInterfaz.T("CompartirAndroid.NoSeHaPodido3"); }
    }
}
