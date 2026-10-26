// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Android.Views;
using Pokekeep.Mobile.Ui;

namespace Pokekeep.Mobile.Platform;

/// <summary>
/// Brillo de la ventana al máximo mientras se enseña un código QR (bloque 10-9, PK-IO-052): la cámara de la 3DS lee mejor un código en una pantalla brillante. Solo toca el brillo DE LA VENTANA de Pokekeep
/// (<c>WindowManager.LayoutParams.ScreenBrightness</c>), no el ajuste del sistema, y lo devuelve (<c>BrightnessOverrideNone</c>) al cerrar la hoja. Sin permisos. PENDIENTE DE DISPOSITIVO: solo se ve en un teléfono.
/// </summary>
public sealed class BrilloAndroid : IBrilloPantalla
{
    public void Maximo(bool activo)
    {
        var actividad = PuenteActividad.Actividad;
        if (actividad is null) return;
        actividad.RunOnUiThread(() =>
        {
            try
            {
                var ventana = actividad.Window;
                var atributos = ventana?.Attributes;
                if (ventana is null || atributos is null) return;
                atributos.ScreenBrightness = activo ? 1f : WindowManagerLayoutParams.BrightnessOverrideNone;
                ventana.Attributes = atributos;
            }
            catch (Java.Lang.Exception) { /* sin ventana: el brillo se queda como estaba */ }
        });
    }
}
