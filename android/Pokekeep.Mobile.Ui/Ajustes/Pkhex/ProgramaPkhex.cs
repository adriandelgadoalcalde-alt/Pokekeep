// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Avalonia.Styling;
using PKHeX.Core;

namespace Pokekeep.Mobile.Ui.Ajustes.Pkhex;

/// <summary>
/// Lo que PKHeX tiene como estado del PROGRAMA (bloque 10-1): <c>Main.Settings</c>, <c>Program.HaX</c>, el idioma y el tema. PKHeX los guarda en estáticos del proceso y cada formulario los lee en el momento
/// (<c>Main.Settings.Advanced.GetExclusionList8()</c>...); aquí igual, para que un formulario de la F8 o una vista lean el ajuste VIGENTE sin que haya que pasárselo por diez constructores.
/// Lo pone el arranque (<see cref="ArranquePkhex.Iniciar"/>); sin arranque (pruebas, capturas) valen los ajustes de fábrica en memoria y nada toca el Core.
/// </summary>
public static class ProgramaPkhex
{
    /// <summary>Una lista de recientes antigua puede conservar el nombre inglés. Solo cambia su presentación, nunca el documento ni su metadato guardado.</summary>
    public static string NombreJuegoGuardado(string nombre)
    {
        var ingles = GameInfo.GetStrings("en").gamelist;
        var actual = GameInfo.GetStrings(Idioma).gamelist;
        for (var i = 0; i < ingles.Length && i < actual.Length; i++)
            if (!string.IsNullOrEmpty(ingles[i]) && nombre == ingles[i]) return actual[i];
        return nombre;
    }
    private static IAjustesPkhex _ajustes = new AjustesPkhexEnMemoria(aplicar: false);

    /// <summary>Los ajustes de PKHeX vigentes (<c>Main.Settings</c>).</summary>
    public static IAjustesPkhex Ajustes
    {
        get => _ajustes;
        set
        {
            if (ReferenceEquals(_ajustes, value)) return;
            _ajustes.Cambiado -= AlCambiar;
            _ajustes = value;
            _ajustes.Cambiado += AlCambiar;
            AlCambiar();
        }
    }

    /// <summary>
    /// Modo HaX de este arranque (<c>Program.HaX</c>): el editor abre con las listas completas, la Mochila admite cantidades fuera de rango y la conversión entre formatos admite lo incompatible
    /// (<c>Main.ReloadProgramSettings</c>: <c>EntityConverter.AllowIncompatibleConversion = AllowIncompatibleAll</c>, que se vuelve a imponer tras cada aplicación de ajustes). Solo cambia al arrancar, como en PKHeX.
    /// </summary>
    public static bool HaX
    {
        get => _hax;
        set { _hax = value; AplicarHaX(); }
    }
    private static bool _hax;

    /// <summary>Tras <c>StartupUtil.ReloadSettings</c> (que pone la conversión del ajuste) vuelve a imponer la del modo HaX, dentro del mismo cerrojo de la aplicación (<see cref="AplicadorPkhex"/>).</summary>
    internal static void ReimponerHaX() { if (_hax) EntityConverter.AllowIncompatibleConversion = EntityCompatibilitySetting.AllowIncompatibleAll; }

    private static void AplicarHaX()
        => EntityConverter.AllowIncompatibleConversion = _hax ? EntityCompatibilitySetting.AllowIncompatibleAll : _ajustes.Actual.Converter.AllowIncompatibleConversion;

    /// <summary>Las carpetas de trabajo (<c>LocalResources</c>) con su enlace a carpetas del usuario (SAF); null sin arranque.</summary>
    public static CarpetasTrabajoPkhex? Carpetas { get; set; }

    /// <summary>
    /// Idioma de los datos del juego, coordinado con es-ES/en de la interfaz (ADR 0100).
    /// Las listas proceden del Core; los códigos antiguos de otros idiomas se presentan en español.
    /// </summary>
    public static string Idioma => Localizacion.ServicioIdioma.DelSistema(Ajustes.Actual.Startup.Language) == "en" ? "en" : "es";

    /// <summary>Las listas del Core en el idioma vigente (el Core las guarda en caché por idioma: pedirlas cada vez no cuesta).</summary>
    public static GameStrings Textos => GameInfo.GetStrings(Idioma);

    /// <summary>
    /// Se dispara tras cada cambio de ajustes (idioma, tema...) para que lo que se pinta se refresque. La Shell visible lo escucha: si cambió el idioma de los datos, vuelve a leer los nombres de las cajas, la ficha
    /// y la Mochila (<c>ShellViewModel.AlCambiarAjustesPkhex</c>). <c>GameInfo.Strings</c> del Core NO se cambia a propósito (ver ADR 0074): lo que pinte nombres los toma de <see cref="Textos"/>.
    /// </summary>
    public static event Action? Cambiado;

    internal static void AlCambiar()
    {
        Localizacion.IdiomaInterfaz.Actual.Vincular(_ajustes);
        if (_hax) AplicarHaX();   // StartupUtil.ReloadSettings acaba de poner la conversión del ajuste: en HaX manda «todo», como en PKHeX
        TemaPkhex.Aplicar(_ajustes.Actual);
        Cambiado?.Invoke();
    }

    /// <summary>Vuelve al estado sin arranque (pruebas).</summary>
    public static void Restablecer()
    {
        Ajustes = new AjustesPkhexEnMemoria(aplicar: false);
        HaX = false;                // la conversión vuelve a la de fábrica
        Carpetas = null;
    }
}

/// <summary>
/// Modo oscuro (<c>Startup.DarkMode</c>; PKHeX: <c>Application.SetColorMode(Dark)</c> al arrancar e inversión de los iconos del menú). En el móvil el cambio vale al momento: el tema de Avalonia cambia y los iconos,
/// que se pintan con los colores del tema, se invierten solos. La primera vez que se ejecuta la app el ajuste toma el modo del sistema, como el valor por defecto de PKHeX (<c>Application.SystemColorMode</c>).
/// </summary>
public static class TemaPkhex
{
    /// <summary>Quién cambia el tema (por defecto, la aplicación Avalonia). Las pruebas lo sustituyen para observar el efecto sin pantalla.</summary>
    public static Action<ThemeVariant> Fijar { get; set; } = FijarEnLaApp;

    private static void FijarEnLaApp(ThemeVariant v)
    {
        if (Avalonia.Application.Current is null) return;   // sin aplicación (pruebas del núcleo): no hay tema que cambiar
        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess()) PokekeepApp.FijarTema(v);
        else Avalonia.Threading.Dispatcher.UIThread.Post(() => PokekeepApp.FijarTema(v));
    }

    public static ThemeVariant Variante(AjustesPkhex a) => a.Startup.DarkMode ? ThemeVariant.Dark : ThemeVariant.Light;

    public static void Aplicar(AjustesPkhex a) => Fijar(Variante(a));
}

/// <summary>Lo que el arranque tiene que hacer después de preparar los ajustes (<c>ProgramInit</c> de PKHeX).</summary>
/// <param name="MostrarNovedades">Primera vez con una versión nueva y «Mostrar el registro de cambios» activado: se abre «Acerca de» con las novedades.</param>
/// <param name="HaX">Modo HaX forzado al arrancar: se avisa de que el programa permite datos ilegales (<c>WarnBehavior</c>).</param>
/// <param name="PreguntarCopias">PKHeX preguntaría por la carpeta de copias de seguridad (<c>BAKPrompt</c>); en Android esa pregunta no existe (el selector de carpeta está siempre en Ajustes), así que nadie la atiende.</param>
public sealed record InicioPrograma(bool MostrarNovedades, bool HaX, bool PreguntarCopias);

/// <summary>
/// El arranque de PKHeX (<c>Program.Main</c> + <c>StartupUtil.FormLoadInitialActions</c>) en el móvil: con la MISMA llamada del Core decide si se enseña el registro de cambios (<c>Startup.Version</c> anterior a la
/// versión actual y <c>ShowChangelogOnUpdate</c>), si se arranca en modo HaX (<c>ForceHaXOnLaunch</c>; el argumento «HaX» y el ejecutable terminado en HaX no existen en Android) y recuerda la versión ejecutada.
/// </summary>
public static class ArranquePkhex
{
    /// <summary>
    /// La versión del programa para el registro de cambios (<c>Startup.Version</c>) a partir del texto de la versión (la de <c>PokekeepVersion.props</c>, «0.7.0»; también «0.7.0-beta» o «0.7.0+abc»): se toma la parte
    /// numérica. Un texto que no sea una versión da 0.0, con lo que nunca se enseñan novedades por error.
    /// </summary>
    public static Version VersionDelPrograma(string? texto)
    {
        var n = (texto ?? string.Empty).Split('-', '+', ' ')[0];
        return Version.TryParse(n, out var v) ? v : new Version(0, 0);
    }

    /// <param name="ajustes">Los ajustes ya leídos de disco (<see cref="AlmacenAjustesPkhex"/>).</param>
    /// <param name="version">La versión de Pokekeep que se ejecuta (el registro de cambios que se enseña es el suyo).</param>
    /// <param name="sistemaOscuro">El sistema está en modo oscuro (solo cuenta la primera vez que se ejecuta la app).</param>
    public static InicioPrograma Iniciar(IAjustesPkhex ajustes, Version version, bool sistemaOscuro, CarpetasTrabajoPkhex? carpetas = null)
    {
        var a = ajustes.Actual;
        if (string.IsNullOrEmpty(a.Startup.Version)) a.Startup.DarkMode = sistemaOscuro;   // primera ejecución: el valor por defecto de PKHeX es el modo del sistema
        var init = StartupUtil.FormLoadInitialActions([], a, version);   // marca BAKPrompt como respondido, como PKHeX: en Android no hay pregunta de la carpeta de copias (PK-SET-038 y PK-MENU-050, «no aplica»)
        ProgramaPkhex.HaX = init.HaX;
        ProgramaPkhex.Carpetas = carpetas;
        ajustes.Persistir();                   // la versión ejecutada queda escrita (PKHeX la escribe al cerrar; Android puede matar la app sin cerrarla); el Core no la lee, así que no se recarga nada
        if (ReferenceEquals(ProgramaPkhex.Ajustes, ajustes)) ProgramaPkhex.AlCambiar(); else ProgramaPkhex.Ajustes = ajustes;       // aplica el tema y avisa a quien pinta
        return new InicioPrograma(init.ShowChangelog, init.HaX, init.BackupPrompt);
    }
}
