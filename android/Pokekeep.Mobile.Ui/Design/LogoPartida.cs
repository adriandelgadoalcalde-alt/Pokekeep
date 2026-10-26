using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using PKHeX.Core;

namespace Pokekeep.Mobile.Ui.Design;

/// <summary>Punto de conexión del cargador de datos nativos de C17, compartido por cabecera y listas.</summary>
public static class LogosPartida
{
    private static Func<GameVersion, IImage?>? _cargador;
    public static event Action? Cambiado;

    public static void Configurar(Func<GameVersion, IImage?>? cargador)
    {
        _cargador = cargador;
        Cambiado?.Invoke();
    }

    public static IImage? Obtener(GameVersion juego)
    {
        // COLO, XD y RSBOX quedan fuera de IsValidSavedVersion, pero sus guardados se resuelven a ellas (LogosDeJuego.VersionDe).
        if (!juego.IsValidSavedVersion() && juego is not (GameVersion.COLO or GameVersion.XD or GameVersion.RSBOX)) return null;
        try { return _cargador is { } cargar ? cargar(juego) : LogosDeJuego.Actual?.Obtener(juego); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException or NotSupportedException) { return null; }
    }

    /// <summary>Rótulo del juego en el idioma de la partida ("es"/"en"). Con un cargador de pruebas configurado no hay rótulos.</summary>
    public static IImage? ObtenerTitulo(GameVersion juego, string? idioma)
    {
        if (idioma is null || _cargador is not null) return null;
        try { return LogosDeJuego.Actual?.ObtenerTitulo(juego, idioma); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException or NotSupportedException) { return null; }
    }

    /// <summary>uso10: carátula del juego para las listas de partidas; sin carátula en el paquete (Switch, Verde), su icono.</summary>
    public static IImage? ObtenerCaratula(GameVersion juego)
    {
        if (_cargador is not null) return Obtener(juego);
        if (!juego.IsValidSavedVersion() && juego is not (GameVersion.COLO or GameVersion.XD or GameVersion.RSBOX)) return null;
        try { return LogosDeJuego.Actual?.ObtenerCaratula(juego) ?? Obtener(juego); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException or NotSupportedException) { return Obtener(juego); }
    }

    /// <summary>Si la cabecera pintará un logo (rótulo o icono) para este juego, sin cargar la imagen: con logo, la segunda línea ya no repite el juego.</summary>
    public static bool HayLogo(GameVersion juego, string? idioma)
    {
        if (_cargador is { } cargar) { try { return cargar(juego) is not null; } catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException or NotSupportedException) { return false; } }
        if (LogosDeJuego.Actual is not { } logos) return false;
        return (idioma is not null && logos.InformacionTitulo(juego, idioma) is not null) || logos.Informacion(juego) is { Estado: "extraido" };
    }

    public static GameVersion VersionDeNombre(string nombre)
    {
        foreach (var idioma in new[] { "en", "es" })
        {
            var nombres = GameInfo.GetStrings(idioma).gamelist;
            foreach (var version in Enum.GetValues<GameVersion>().Where(v => v.IsValidSavedVersion()))
                if ((int)version < nombres.Length && string.Equals(nombres[(int)version], nombre, StringComparison.OrdinalIgnoreCase)) return version;
        }
        return Enum.TryParse<GameVersion>(nombre, true, out var id) && id.IsValidSavedVersion() ? id : GameVersion.Invalid;
    }
}

/// <summary>Logo proporcionado por los datos del juego, sin deformar, o icono de partida si no está disponible.</summary>
public sealed class LogoPartida : Grid
{
    public static readonly StyledProperty<GameVersion> JuegoProperty = AvaloniaProperty.Register<LogoPartida, GameVersion>(nameof(Juego));
    /// <summary>Idioma de la partida ("es"/"en"). Con idioma se prefiere el rótulo del juego; sin él (o sin rótulo), el icono.</summary>
    public static readonly StyledProperty<string?> IdiomaProperty = AvaloniaProperty.Register<LogoPartida, string?>(nameof(Idioma));
    /// <summary>uso10 (QA del propietario): en las listas de partidas va la carátula; el rótulo del título queda para la cabecera.</summary>
    public static readonly StyledProperty<bool> CaratulaProperty = AvaloniaProperty.Register<LogoPartida, bool>(nameof(Caratula));
    public static readonly StyledProperty<IBrush?> ColorRespaldoProperty = AvaloniaProperty.Register<LogoPartida, IBrush?>(nameof(ColorRespaldo), Brushes.Goldenrod);
    private readonly Image _imagen = new() { Stretch = Stretch.Uniform };
    // Los iconos grandes (Switch) son cuadrados opacos: con las esquinas redondeadas, como en la consola y en las insignias de la app.
    private readonly Border _marco = new() { ClipToBounds = true, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
    private readonly Icono _respaldo = new() { Glifo = "HardDrive", Width = 24, Height = 24, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };

    public GameVersion Juego { get => GetValue(JuegoProperty); set => SetValue(JuegoProperty, value); }
    public string? Idioma { get => GetValue(IdiomaProperty); set => SetValue(IdiomaProperty, value); }
    public IBrush? ColorRespaldo { get => GetValue(ColorRespaldoProperty); set => SetValue(ColorRespaldoProperty, value); }
    public bool Caratula { get => GetValue(CaratulaProperty); set => SetValue(CaratulaProperty, value); }
    public bool TieneLogo => _imagen.Source is not null;
    public bool EsTitulo { get; private set; }

    public LogoPartida()
    {
        _marco.Child = _imagen;
        // El logo es decorativo: el nombre accesible de la tarjeta (ResumenPartida) y la fila del reciente ya dicen el juego; TalkBack no lo lee dos veces.
        AutomationProperties.SetAccessibilityView(_imagen, AccessibilityView.Raw);
        Children.Add(_marco); Children.Add(_respaldo);
        Actualizar();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs cambio)
    {
        base.OnPropertyChanged(cambio);
        if (cambio.Property == JuegoProperty || cambio.Property == IdiomaProperty || cambio.Property == ColorRespaldoProperty || cambio.Property == CaratulaProperty) Actualizar();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e); LogosPartida.Cambiado += Actualizar; Actualizar();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        LogosPartida.Cambiado -= Actualizar; base.OnDetachedFromVisualTree(e);
    }

    private void Actualizar()
    {
        var titulo = Caratula ? null : LogosPartida.ObtenerTitulo(Juego, Idioma);
        EsTitulo = titulo is not null;
        _imagen.Source = titulo ?? (Caratula ? LogosPartida.ObtenerCaratula(Juego) : LogosPartida.Obtener(Juego));
        _imagen.MaxWidth = _imagen.Source?.Size.Width ?? double.PositiveInfinity;
        _imagen.MaxHeight = _imagen.Source?.Size.Height ?? double.PositiveInfinity;
        _imagen.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
        _imagen.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
        RenderOptions.SetBitmapInterpolationMode(_imagen, _imagen.Source?.Size.Width <= 64 ? BitmapInterpolationMode.None : BitmapInterpolationMode.HighQuality);
        // Un rótulo tiene su propia silueta transparente: sin esquinas redondeadas.
        _marco.CornerRadius = new CornerRadius(!EsTitulo && _imagen.Source?.Size.Width > 64 ? 8 : 0);
        _imagen.IsVisible = TieneLogo;
        _marco.IsVisible = TieneLogo;
        _respaldo.IsVisible = !TieneLogo;
        _respaldo.Foreground = ColorRespaldo;
    }
}
