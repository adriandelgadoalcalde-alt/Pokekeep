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
        if (ApplicationLifetime is ISingleViewApplicationLifetime vista && FabricaRaiz is { } fabrica)
            vista.MainView = fabrica();
        base.OnFrameworkInitializationCompleted();
    }

    public static void FijarTema(ThemeVariant? variante)
    {
        if (Current is { } app) app.RequestedThemeVariant = variante ?? ThemeVariant.Default;
    }
}
