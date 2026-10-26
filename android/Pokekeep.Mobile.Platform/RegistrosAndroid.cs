// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Text;
using Android.Content;
using Pokekeep.Mobile.Engine.Plataforma;
using Pokekeep.Mobile.Ui.ViewModels;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Platform;

/// <summary>Pantalla Registros: los ficheros de <c>FilesDir/registros/</c> y «Compartir» por la hoja del sistema, SOLO cuando el usuario lo pide (regla 15: nada sale solo).</summary>
public sealed class RegistrosAndroid(Context contexto) : IRegistros
{
    static RegistrosAndroid() => AccionHojaViewModel.FalloNoControlado = (contexto, ex) => AutoLegalidad.Registro.RegistroFallos.Error(contexto, ex, "AccionHojaViewModel");

    public IReadOnlyList<(string Nombre, long Bytes, DateTime Fecha)> Ficheros()
        => [.. RegistroAndroid.Ficheros().Select(f => (f.Name, f.Length, f.LastWriteTimeUtc))];

    public string Leer(string nombre, int maxBytes)
    {
        var ruta = RegistroAndroid.Ficheros().FirstOrDefault(f => f.Name == nombre)?.FullName;   // por nombre, nunca por ruta
        if (ruta is null) return string.Empty;
        using var fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var desde = Math.Max(0, fs.Length - maxBytes);
        fs.Seek(desde, SeekOrigin.Begin);
        var buf = new byte[fs.Length - desde];
        fs.ReadExactly(buf);
        var texto = Encoding.UTF8.GetString(buf);
        return desde > 0 ? "[…]\n" + texto[(texto.IndexOf('\n') + 1)..] : texto;
    }

    public void Compartir(string titulo, string texto)
    {
        var envio = new Intent(Intent.ActionSend).SetType("text/plain").PutExtra(Intent.ExtraSubject, titulo).PutExtra(Intent.ExtraText, texto);
        var selector = Intent.CreateChooser(envio, IdiomaInterfaz.T("RegistrosAndroid.CompartirRegistro"))!.AddFlags(ActivityFlags.NewTask);
        contexto.StartActivity(selector);
    }
}
