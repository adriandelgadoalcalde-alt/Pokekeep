// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;
using Pokekeep.Mobile.Ui.Ajustes.Pkhex;

namespace Pokekeep.Mobile.Ui.ViewModels;

/// <summary>
/// Una carpeta de trabajo de PKHeX (<c>LocalResources.*</c>, <c>Advanced.PathBlockKeyList</c>; bloque 10-1): en vez de un campo de ruta, la carpeta que se usa ahora y dos botones, «Elegir carpeta…» (selector del
/// sistema, SAF) y «Quitar mi carpeta». La lógica vive en <see cref="CarpetasTrabajoPkhex"/>; sin servicios (pruebas, capturas) la fila lo dice y no ofrece el selector.
/// </summary>
public sealed partial class FilaPkhexCarpeta : FilaPkhex
{
    private readonly Func<CarpetasTrabajoPkhex?> _carpetas;

    public FilaPkhexCarpeta(NodoAjustePkhex nodo, IAjustesPkhex almacen, Func<CarpetasTrabajoPkhex?>? carpetas = null) : base(nodo, almacen)
    {
        _carpetas = carpetas ?? (() => ProgramaPkhex.Carpetas);
        Releer();
    }

    [ObservableProperty] private string _actual = string.Empty;
    [ObservableProperty] private string? _estado;
    private Func<string?>? _textoEstado;
    private void MostrarEstado(Func<string?> texto) { _textoEstado = texto; Estado = texto(); }
    public override void ActualizarIdioma() { base.ActualizarIdioma(); Releer(); Estado = _textoEstado?.Invoke(); }
    [ObservableProperty] private bool _ocupada;
    public override string ValorTexto => Actual;
    public bool PuedeElegir => _carpetas() is { PuedeElegir: true } && !Ocupada;
    public bool HayEnlace => _carpetas()?.Enlace(Ruta) is not null;
    public bool HayEstado => Estado is not null;
    partial void OnEstadoChanged(string? value) => OnPropertyChanged(nameof(HayEstado));
    partial void OnOcupadaChanged(bool value) => OnPropertyChanged(nameof(PuedeElegir));

    public override void Releer()
    {
        var c = _carpetas();
        Actual = c is null ? ((string?)Leer() is { Length: > 0 } v ? v : IdiomaInterfaz.T("Ajustes.Carpetas.Ninguna")) : c.Describir(Ruta);
        OnPropertyChanged(nameof(ValorTexto)); OnPropertyChanged(nameof(HayEnlace)); OnPropertyChanged(nameof(PuedeElegir));
    }

    [RelayCommand]
    public async Task Elegir()
    {
        if (_carpetas() is not { PuedeElegir: true } c) { MostrarEstado(() => IdiomaInterfaz.T("Ajustes.Carpetas.NoDisponible")); return; }
        Ocupada = true;
        try
        {
            var r = await c.ElegirAsync(Ruta);
            MostrarEstado(() => r is null ? null : IdiomaInterfaz.P("Ajustes.Carpetas.Copiados", r.Archivos, r.Carpeta.Nombre));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException) { MostrarEstado(() => IdiomaInterfaz.F("Ajustes.Carpetas.ErrorLeer", ex.Message)); }
        finally { Ocupada = false; Releer(); }
    }

    [RelayCommand]
    public void UsarLaDeLaApp()
    {
        if (_carpetas() is not { } c) { Escribir(CarpetasTrabajoPkhex.Recurso(Ruta)!.Defecto); Releer(); return; }
        c.UsarLaDeLaApp(Ruta);
        MostrarEstado(() => IdiomaInterfaz.T("Ajustes.Carpetas.AppSinTocar"));
        Releer();
    }
}

/// <summary>Idioma de la interfaz y los datos: es-ES/en, decisión del propietario (ADR 0100).</summary>
public sealed partial class FilaPkhexIdioma : FilaPkhex
{
    private readonly Action<HojaViewModel> _abrirHoja;
    public FilaPkhexIdioma(NodoAjustePkhex nodo, IAjustesPkhex almacen, Action<HojaViewModel> abrirHoja) : base(nodo, almacen) => _abrirHoja = abrirHoja;

    /// <summary>Los dos idiomas del producto. No altera los idiomas de los Pokémon.</summary>
    public static IReadOnlyList<(string Codigo, string Nombre)> Idiomas { get; } =
        [.. Localizacion.IdiomaInterfaz.Actual.Opciones.Select(o => (o.Codigo == "en" ? "en" : "es", o.Nombre))];

    private string CodigoNormalizado => Localizacion.ServicioIdioma.DelSistema((string?)Leer()) == "en" ? "en" : "es";
    public override string ValorTexto => Idiomas.Single(x => x.Codigo == CodigoNormalizado).Nombre;

    public void Elegir(string codigo)
    {
        if (!Idiomas.Any(i => i.Codigo == codigo)) throw new ArgumentOutOfRangeException(nameof(codigo));
        Escribir(codigo); OnPropertyChanged(nameof(ValorTexto));
    }

    [RelayCommand]
    private void Abrir()
        => _abrirHoja(new SelectorHojaViewModel(Nombre, [.. Idiomas.Select((x, i) => new OpcionSelector(i, x.Nombre))], Idiomas.ToList().FindIndex(x => x.Codigo == CodigoNormalizado), o => Elegir(Idiomas[o.Id].Codigo), IdiomaInterfaz.T("Comun.Buscar"), tituloActual: () => Nombre, marcadorActual: () => IdiomaInterfaz.T("Comun.Buscar")));

    public override void Releer() => OnPropertyChanged(nameof(ValorTexto));
}

/// <summary>
/// Versión de la partida en blanco (<c>Startup.DefaultSaveVersion</c>; PKHeX: <c>SettingsEditor.CB_Blank</c>). La lista es la de PKHeX (todas las versiones salvo GO y Let's Go Pikachu/Eevee sin partida, <c>CP</c>) con los
/// nombres del Core; al cambiarla, PKHeX carga una partida en blanco de esa versión (<c>BlankChanged</c>): aquí se avisa a quien hospeda la pantalla (<see cref="VersionCambiada"/>), que no tira una partida abierta.
/// </summary>
public sealed partial class FilaPkhexVersionBlanco : FilaPkhex
{
    private readonly Action<HojaViewModel> _abrirHoja;
    public FilaPkhexVersionBlanco(NodoAjustePkhex nodo, IAjustesPkhex almacen, Action<HojaViewModel> abrirHoja) : base(nodo, almacen) => _abrirHoja = abrirHoja;

    /// <summary>Quien hospeda la pantalla (la Shell) se entera de la versión elegida, como <c>Main</c> con <c>SettingsEditor.BlankChanged</c>.</summary>
    public static event Action<GameVersion>? VersionCambiada;

    /// <summary><c>SettingsEditor.IsInvalidSaveFileVersion</c>: 0, GO y CP no tienen partida.</summary>
    public static bool EsValida(GameVersion v) => v is not (0 or GameVersion.GO or GameVersion.CP);

    public static IReadOnlyList<ComboItem> Versiones() => [.. new GameDataSource(ProgramaPkhex.Textos).VersionDataSource.Where(z => EsValida((GameVersion)z.Value))];

    /// <summary>El nombre del juego en el idioma de los datos: el mismo texto que la lista (<c>GameDataSource.GetVersionList</c> usa <c>gamelist[versión]</c>), sin construir la lista entera.</summary>
    public static string NombreDe(GameVersion v) => ProgramaPkhex.Textos.gamelist is var g && (uint)v < (uint)g.Length && g[(int)v] is { Length: > 0 } n ? n : v.ToString();

    public override string ValorTexto => NombreDe((GameVersion)Leer()!);

    public void Elegir(GameVersion v)
    {
        if (!EsValida(v)) return;
        Escribir(v);
        OnPropertyChanged(nameof(ValorTexto));
        VersionCambiada?.Invoke((GameVersion)Leer()!);
    }

    [RelayCommand]
    private void Abrir()
    {
        // GameVersion is the stable identity: translated names can change the Core's sort order.
        IReadOnlyList<OpcionSelector> Opciones() => [.. Versiones().Select(x => new OpcionSelector(x.Value, x.Text))];
        _abrirHoja(new SelectorHojaViewModel(Nombre, Opciones(), (int)(GameVersion)Leer()!, o => Elegir((GameVersion)o.Id), IdiomaInterfaz.T("Comun.Buscar"),
            actualizarOpciones: Opciones, tituloActual: () => Nombre, marcadorActual: () => IdiomaInterfaz.T("Comun.Buscar")));
    }

    public override void Releer() => OnPropertyChanged(nameof(ValorTexto));
}

public static partial class FabricaFilasPkhex
{
    static partial void PersonalizadaB10_1(NodoAjustePkhex n, IAjustesPkhex a, Action<HojaViewModel> h, ref FilaPkhex? fila)
    {
        if (fila is null && CarpetasTrabajoPkhex.EsCarpeta(n.Ruta)) fila = new FilaPkhexCarpeta(n, a);
    }
}

public static partial class ExtrasAjustesPkhex
{
    static partial void FilasB10_1(string grupo, IAjustesPkhex almacen, Action<HojaViewModel> abrirHoja, List<FilaPkhex> filas)
    {
        if (grupo == "Idioma") filas.Add(new FilaPkhexIdioma(RegistroAjustesPkhex.Buscar("Startup.Language")!, almacen, abrirHoja));
        if (grupo == "General") filas.Add(new FilaPkhexVersionBlanco(RegistroAjustesPkhex.Buscar("Startup.DefaultSaveVersion")!, almacen, abrirHoja));
    }

    static partial void CantidadB10_1(string grupo, ref int cantidad)
    {
        if (grupo is "Idioma" or "General") cantidad++;
    }
}
