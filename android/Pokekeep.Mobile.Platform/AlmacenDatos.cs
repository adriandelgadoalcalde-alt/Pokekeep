// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Android.Content;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Platform;

/// <summary>Lee el manifiesto del APK y materializa solo las bases SQLite comprimidas. Imágenes y audio permanecen en el APK.</summary>
public sealed class AlmacenDatos(Context contexto)
{
    public const string NombreManifiesto = "datos-manifiesto.json";
    public string Carpeta => Path.Combine(contexto.FilesDir!.AbsolutePath, "datos");
    public string Ruta(string fichero) => Path.Combine(Carpeta, fichero);

    public IReadOnlyList<PreparadorDatosProducto.Entrada> LeerManifiestoDelApk()
    {
        try
        {
            using var manifiesto = contexto.Assets!.Open(NombreManifiesto);
            return PreparadorDatosProducto.Analizar(manifiesto);
        }
        catch (Java.IO.FileNotFoundException) { return []; }
    }

    public Task<int> PrepararAsync(IProgress<(string Mensaje, double? Fraccion)>? progreso, CancellationToken ct = default)
        => new PreparadorDatosProducto(Carpeta, nombre => contexto.Assets!.Open(nombre, Android.Content.Res.Access.Streaming), new(
                nombre => IdiomaInterfaz.F("PreparadorDatosProducto.PreparandoLosDatosDe", nombre),
                nombre => IdiomaInterfaz.F("PreparadorDatosProducto.LaCopiaDeNo", nombre),
                () => IdiomaInterfaz.T("PreparadorDatosProducto.DatosListos")))
            .PrepararAsync(LeerManifiestoDelApk(), progreso, ct);
}
