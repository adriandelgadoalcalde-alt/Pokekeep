// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace Pokekeep.Mobile.Ui;

/// <summary>Aplicacion Avalonia compartida por la cabecera de Android y la capa headless. El tema (claro, oscuro o el del sistema) se cambia con <see cref="FijarTema"/>.</summary>
public partial class PokekeepApp : Application
{
    /// <summary>Lo pone la cabecera de la plataforma antes de que arranque Avalonia: devuelve la vista raiz (arranque y, despues, la Shell).</summary>
    public static Func<Control>? FabricaRaiz { get; set; }

    public override void Initialize()
    {
        CulturaApp.Fijar();
        AvaloniaXamlLoader.Load(this);
        Localizacion.IdiomaInterfaz.InstalarRecursos();
        Design.BarraAlBorde.Registrar();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        PrecargarLetraDeReserva();
        if (ApplicationLifetime is ISingleViewApplicationLifetime vista && FabricaRaiz is { } fabrica)
            vista.MainView = fabrica();
        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Mide una vez un texto en japonés, coreano y chino con la letra de la app. Roboto no trae esos caracteres y Avalonia busca la letra de reserva del sistema
    /// la primera vez que los encuentra: esa primera medida sale con otra altura de línea (0,33 px menos, medido el 14-nov-2026 con las tarjetas japonesas de la base
    /// de datos de regalos) y la misma fila cambiaba de alto según fuera la primera o no. Precargada aquí, todas las medidas salen iguales.
    /// </summary>
    private void PrecargarLetraDeReserva()
    {
        try
        {
            var familia = TryGetResource("FuenteRoboto", null, out var r) && r is Avalonia.Media.FontFamily f ? f : Avalonia.Media.FontFamily.Default;
            using var texto = new Avalonia.Media.TextFormatting.TextLayout("「プレゼント」をおぼえた ピカチュウ 한국어 中文", new Avalonia.Media.Typeface(familia), 14, null);
            _ = texto.Height;
        }
        catch (Exception) { /* sin letra de reserva no hay nada que precargar */ }
    }

    public static void FijarTema(ThemeVariant? variante)
    {
        if (Current is { } app) app.RequestedThemeVariant = variante ?? ThemeVariant.Default;
    }
}
