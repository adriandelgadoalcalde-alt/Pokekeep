// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pokekeep.Mobile.Ui.Localizacion;
using Pokekeep.Mobile.Ui.Ajustes.Pkhex;
using Pokekeep.Mobile.Ui.Almacen;
using Pokekeep.Mobile.Ui.Guardados;
using Pokekeep.Mobile.Ui.Herramientas.ListaCarpetas;

namespace Pokekeep.Mobile.Ui.ViewModels;

/// <summary>Una carpeta de «Otras rutas de copias» en su fila: el nombre que se ve y la ubicación guardada (URI de SAF o ruta).</summary>
public sealed record CarpetaExtra(string Nombre, string Ubicacion, IRelayCommand Quitar);

/// <summary>
/// «Otras rutas de copias» (<c>Backup.OtherBackupPaths</c>, bloque 10-4). En PKHeX es una lista de rutas que se escribe a mano en la rejilla de propiedades; en Android no hay rutas, así que la fila lista las carpetas
/// elegidas con el selector del sistema (permiso persistente) y deja añadir y quitar. Una ruta absoluta que ya estuviera (ajustes traídos del PC) se conserva y se puede quitar. La consumen la «Lista de carpetas»,
/// «Cargar el guardado más reciente», «Cargar partida automáticamente al iniciar» y el Core (<c>SaveFinder.CustomBackupPaths</c>, solo rutas que existen).
/// </summary>
public sealed partial class FilaPkhexCarpetasExtra : FilaPkhex
{
    /// <summary>Quién da el selector de carpetas del sistema: el de la Shell que abrió la pantalla (<see cref="AjustesPkhexGrupoViewModel"/>); sin él, esta ventana no puede elegir carpetas y el botón lo dice.</summary>
    public Func<IAlmacenCarpetas?> ProveedorCarpetas { get; set; } = () => null;

    public FilaPkhexCarpetasExtra(NodoAjustePkhex nodo, IAjustesPkhex almacen) : base(nodo, almacen) => Releer();

    [ObservableProperty] private IReadOnlyList<CarpetaExtra> _carpetas = [];
    [ObservableProperty] private string _aviso = string.Empty;
    private Func<string>? _textoAviso;
    private void MostrarAviso(Func<string> texto) { _textoAviso = texto; Aviso = texto(); }
    public override void ActualizarIdioma() { base.ActualizarIdioma(); Aviso = _textoAviso?.Invoke() ?? string.Empty; }
    public bool HayCarpetas => Carpetas.Count > 0;
    public bool HayAviso => !string.IsNullOrEmpty(Aviso);
    partial void OnCarpetasChanged(IReadOnlyList<CarpetaExtra> value) { OnPropertyChanged(nameof(HayCarpetas)); OnPropertyChanged(nameof(ValorTexto)); }
    partial void OnAvisoChanged(string value) => OnPropertyChanged(nameof(HayAviso));

    public override string ValorTexto => Carpetas.Count == 0 ? IdiomaInterfaz.T("Ajustes.Carpetas.Ninguna") : IdiomaInterfaz.P("Ajustes.Carpetas.Cantidad", Carpetas.Count);

    public override void Releer()
        => Carpetas = [.. Almacen.Actual.Backup.OtherBackupPaths.Select(u => new CarpetaExtra(ModeloCarpetas.EsLocal(u) ? u : ModeloCarpetas.NombreDeUri(u), u, new RelayCommand(() => QuitarRuta(u))))];

    public void QuitarRuta(string ubicacion) { OtrasRutas.Quitar(Almacen, ubicacion); Releer(); }

    /// <summary>Añade una ubicación (lo que devuelve el selector). False si ya estaba o está vacía.</summary>
    public bool AnadirRuta(string ubicacion) { var ok = OtrasRutas.Anadir(Almacen, ubicacion); Releer(); return ok; }

    [RelayCommand]
    private async Task Anadir()
    {
        MostrarAviso(() => string.Empty);
        if (ProveedorCarpetas() is not { } carpetas) { MostrarAviso(() => IdiomaInterfaz.T("Ajustes.Carpetas.NoDisponible")); return; }
        CarpetaRef? c;
        try { c = await carpetas.ElegirCarpetaAsync(CancellationToken.None); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException) { MostrarAviso(() => IdiomaInterfaz.F("Ajustes.Carpetas.ErrorElegir", ex.Message)); return; }
        if (c is null) return;
        if (!AnadirRuta(c.Uri)) MostrarAviso(() => IdiomaInterfaz.F("Ajustes.Carpetas.YaAnadida", c.Nombre));
    }
}

/// <summary>
/// «Otras extensiones de partidas» (<c>Backup.OtherSaveFileExtensions</c>, bloque 10-4): las extensiones separadas por comas, sin punto (como las guarda PKHeX: «srm, sa1»). Al confirmar se normalizan
/// (<see cref="ExtensionesPkhex.Normalizar"/>), se guardan y se añaden a <c>SaveFileMetadata.CustomSaveExtensions</c> (lo que hace PKHeX al recargar los ajustes; solo añade).
/// </summary>
public sealed partial class FilaPkhexExtensiones : FilaPkhex
{
    public FilaPkhexExtensiones(NodoAjustePkhex nodo, IAjustesPkhex almacen) : base(nodo, almacen) => _texto = Unir();

    [ObservableProperty] private string _texto;
    public override string ValorTexto => Unir();

    private string Unir() => string.Join(", ", Almacen.Actual.Backup.OtherSaveFileExtensions);

    public void Confirmar()
    {
        var nuevas = Texto.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries).Select(ExtensionesPkhex.Normalizar).Where(e => e.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var lista = Almacen.Actual.Backup.OtherSaveFileExtensions;
        if (!lista.SequenceEqual(nuevas))
        {
            lista.Clear(); lista.AddRange(nuevas);
            Almacen.Guardar();
            ExtensionesPkhex.Aplicar(Almacen.Actual);
        }
        Texto = Unir();
        OnPropertyChanged(nameof(ValorTexto));
    }

    public override void Releer() { Texto = Unir(); OnPropertyChanged(nameof(ValorTexto)); }
}

public static partial class FabricaFilasPkhex
{
    static partial void PersonalizadaB10_4(NodoAjustePkhex n, IAjustesPkhex a, Action<HojaViewModel> h, ref FilaPkhex? fila)
    {
        if (n.Ruta == "Backup.OtherBackupPaths") fila = new FilaPkhexCarpetasExtra(n, a);
        else if (n.Ruta == "Backup.OtherSaveFileExtensions") fila = new FilaPkhexExtensiones(n, a);
    }
}
