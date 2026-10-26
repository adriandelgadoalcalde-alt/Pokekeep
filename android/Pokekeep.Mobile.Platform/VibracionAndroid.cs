// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Android.Views;
using Pokekeep.Mobile.Ui.Design;

namespace Pokekeep.Mobile.Platform;

/// <summary>
/// Toque háptico al levantar un Pokémon con la pulsación larga (X10): el mismo «clic largo» del sistema (<c>PerformHapticFeedback(LongPress)</c>), que respeta el ajuste del usuario de vibración al tocar y no pide permisos.
/// PENDIENTE DE DISPOSITIVO: solo se siente en un teléfono.
/// </summary>
public sealed class VibracionAndroid : IVibracion
{
    public void PulsoCorto()
    {
        var actividad = PuenteActividad.Actividad;
        if (actividad is null) return;
        actividad.RunOnUiThread(() =>
        {
            try { actividad.Window?.DecorView?.PerformHapticFeedback(FeedbackConstants.LongPress); }
            catch (Java.Lang.Exception) { /* sin ventana: sin vibración */ }
        });
    }
}
