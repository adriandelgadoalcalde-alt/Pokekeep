// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.ViewModels;

/// <summary>Contenido de la hoja inferior (bottom sheet): la Shell muestra una a la vez sobre un velo; se cierra con el velo, el boton Atras o «Cerrar» salvo que el contenido lo impida.</summary>
public abstract partial class HojaViewModel : ObservableObject
{
    public abstract string Titulo { get; }
    /// <summary>Falso mientras el contenido necesita una decision explicita (p. ej. una operacion en curso se cancela con «Cancelar», no con un toque suelto).</summary>
    public virtual bool CierraConFondo => true;
    /// <summary>Verdadero si la hoja enseña algo que otro aparato tiene que leer con su cámara (un código QR): la Shell pone el brillo al máximo mientras esté abierta (<see cref="IBrilloPantalla"/>).</summary>
    public virtual bool PideBrilloMaximo => false;
    public virtual void ActualizarIdioma() { }
    public event Action? CierreSolicitado;
    [RelayCommand] public void Cerrar() => CierreSolicitado?.Invoke();
}

/// <summary>Una opcion de un selector con buscador.</summary>
public sealed record OpcionSelector(int Id, string Texto, string? Detalle = null, int Especie = 0, int Forma = 0,
    Func<Avalonia.Media.Imaging.Bitmap?>? ObtenerIcono = null, int Tipo = -1, string? TipoTexto = null)
{
    /// <summary>La opcion es un movimiento (u otra cosa con tipo): lleva la etiqueta de color del tipo; el resto del detalle («Legal»...) va a su lado.</summary>
    public bool HayTipo => Tipo >= 0 && !string.IsNullOrEmpty(TipoTexto);
    public string? DetalleSinTipo => !HayTipo ? Detalle : Detalle is null || Detalle == TipoTexto ? null : Detalle.StartsWith(TipoTexto + " · ", StringComparison.Ordinal) ? Detalle[(TipoTexto!.Length + 3)..] : Detalle;
    /// <summary>La opcion nombra una especie: la fila lleva su sprite (pixelado nitido, como en las cajas).</summary>
    public bool HayEspecie => Especie > 0;
    public bool TieneDetalle => !string.IsNullOrEmpty(Detalle);
    public string Accesible => TieneDetalle ? $"{Texto}, {Detalle}" : Texto;
}

/// <summary>Una fila del selector ya pintada: la opcion y si es la elegida.</summary>
public sealed record FilaSelector(OpcionSelector Opcion, bool Elegida)
{
    private readonly Lazy<Avalonia.Media.Imaging.Bitmap?> _icono = new(() => Opcion.ObtenerIcono?.Invoke());
    public bool TieneIconoObjeto => Opcion.ObtenerIcono is not null;
    public Avalonia.Media.Imaging.Bitmap? IconoObjeto => _icono.Value;
    public bool FaltaIconoObjeto => TieneIconoObjeto && IconoObjeto is null;
    public string Texto => Opcion.Texto;
    public string? Detalle => Opcion.Detalle;
    public bool TieneDetalle => Opcion.TieneDetalle;
    public bool HayTipo => Opcion.HayTipo;
    /// <summary>Solo el texto de detalle de siempre (sin etiqueta de tipo): visible únicamente si hay detalle, para que una fila sin él no gane una línea vacía.</summary>
    public bool SoloDetalle => TieneDetalle && !HayTipo;
    public int Tipo => Opcion.Tipo;
    public string? TipoTexto => Opcion.TipoTexto;
    public string? DetalleSinTipo => Opcion.DetalleSinTipo;
    public bool HayDetalleSinTipo => !string.IsNullOrEmpty(DetalleSinTipo);
    public int Especie => Opcion.Especie;
    public int Forma => Opcion.Forma;
    public bool HayEspecie => Opcion.HayEspecie;
    public string Accesible => Elegida ? IdiomaInterfaz.F("Selector.Elegido", Opcion.Accesible) : Opcion.Accesible;
}

/// <summary>Selector con buscador para listas largas (objetos, naturalezas, habilidades...): filtra al escribir, sin tildes ni mayusculas, y marca la opcion actual.</summary>
public sealed partial class SelectorHojaViewModel : HojaViewModel
{
    private IReadOnlyList<OpcionSelector> _opciones;
    private readonly Func<IReadOnlyList<OpcionSelector>>? _actualizarOpciones;
    private readonly Func<string>? _tituloActual;
    private readonly Func<string>? _marcadorActual;
    private readonly int _elegidaActual;
    private readonly Action<OpcionSelector> _elegir;

    public SelectorHojaViewModel(string titulo, IReadOnlyList<OpcionSelector> opciones, int elegidaActual, Action<OpcionSelector> elegir, string? marcadorBusqueda = null, Func<IReadOnlyList<OpcionSelector>>? actualizarOpciones = null, Func<string>? tituloActual = null, Func<string>? marcadorActual = null)
    {
        _titulo = titulo; _opciones = opciones; _elegidaActual = elegidaActual; _elegir = elegir; _marcadorBusqueda = marcadorBusqueda;
        _actualizarOpciones = actualizarOpciones;
        _tituloActual = tituloActual;
        _marcadorActual = marcadorActual;
        Filtrar();
    }

    private readonly string _titulo;
    public override string Titulo => _tituloActual?.Invoke() ?? _titulo;
    private readonly string? _marcadorBusqueda;
    public string MarcadorBusqueda => _marcadorActual?.Invoke() ?? _marcadorBusqueda ?? IdiomaInterfaz.T("Comun.Buscar");

    [ObservableProperty] private string _filtro = string.Empty;
    [ObservableProperty] private IReadOnlyList<FilaSelector> _visibles = [];
    [ObservableProperty] private string _resumen = string.Empty;
    public bool SinResultados => Visibles.Count == 0;
    public override void ActualizarIdioma()
    {
        if (_actualizarOpciones is { } opciones) _opciones = opciones();
        OnPropertyChanged(nameof(Titulo)); OnPropertyChanged(nameof(MarcadorBusqueda));
        Filtrar(); // conserva el filtro y la selección por ID
    }

    partial void OnFiltroChanged(string value) => Filtrar();
    partial void OnVisiblesChanged(IReadOnlyList<FilaSelector> value) => OnPropertyChanged(nameof(SinResultados));

    /// <summary>Minusculas y sin diacriticos: «helice» encuentra «Hélice».</summary>
    public static string Normalizar(string s)
    {
        var d = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (var c in d) if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(char.ToLowerInvariant(c));
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private void Filtrar()
    {
        var f = Normalizar(Filtro.Trim());
        var coinciden = f.Length == 0 ? _opciones : [.. _opciones.Where(o => Normalizar(o.Texto).Contains(f))];
        // Primero las que EMPIEZAN por lo escrito: en una lista de 700 objetos es lo que el usuario espera ver arriba.
        if (f.Length > 0) coinciden = [.. coinciden.OrderBy(o => Normalizar(o.Texto).StartsWith(f) ? 0 : 1)];
        // ListBox virtualiza los controles. No ocultar objetos válidos detrás de un tope arbitrario.
        Visibles = [.. coinciden.Select(o => new FilaSelector(o, o.Id == _elegidaActual))];
        Resumen = coinciden.Count == 0 ? IdiomaInterfaz.T("Selector.SinResultados") : IdiomaInterfaz.P("Selector.Cantidad", coinciden.Count);
    }

    [RelayCommand]
    private void Elegir(FilaSelector? fila)
    {
        if (fila is null) return;
        _elegir(fila.Opcion);
        Cerrar();
    }
}
