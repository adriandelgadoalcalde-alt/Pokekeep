// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.ViewModels;

/// <summary>Registros de fallos de la app (un fichero por proceso, interfaz y motor): ver y COMPARTIR a mano, nunca de forma automatica (regla 15). Rutas saneadas y sin OT/TID/SID desde el origen.</summary>
public interface IRegistros
{
    IReadOnlyList<(string Nombre, long Bytes, DateTime Fecha)> Ficheros();
    /// <summary>Ultimos <paramref name="maxBytes"/> del registro (lo mas reciente).</summary>
    string Leer(string nombre, int maxBytes);
    /// <summary>Hoja de compartir del sistema con el texto; lo decide el usuario.</summary>
    void Compartir(string titulo, string texto);
}

public sealed record FilaRegistro(string Nombre, string Detalle)
{
    public string Accesible => IdiomaInterfaz.F("Registros.Registro", Nombre, Detalle);
}

public sealed partial class RegistrosViewModel : ObservableObject
{
    private readonly IRegistros _registros;
    private readonly Action _cerrar;

    public RegistrosViewModel(IRegistros registros, Action cerrar)
    {
        _registros = registros; _cerrar = cerrar;
        var f = registros.Ficheros();
        Filas = [.. f.Select(x => new FilaRegistro(x.Nombre, $"{x.Bytes / 1024.0:N1} KB · {x.Fecha.ToLocalTime():dd/MM/yyyy HH:mm}"))];
        if (Filas.Count > 0) Seleccionar(Filas[0]);
    }

    public IReadOnlyList<FilaRegistro> Filas { get; }
    public bool HayFilas => Filas.Count > 0;
    public bool NoHayFilas => Filas.Count == 0;
    [ObservableProperty] private FilaRegistro? _seleccionada;
    [ObservableProperty] private string _texto = string.Empty;
    /// <summary>Aviso tras «Compartir» (recorte o fallo de la hoja del sistema); vacio si no hay nada que decir.</summary>
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HayAviso))] private string _aviso = string.Empty;
    public bool HayAviso => Aviso.Length > 0;

    /// <summary>Tope de lo que se manda por la hoja de compartir: un Intent viaja por Binder (limite ~1 MB para todo el proceso) y 400 KB podian tumbar la app con TransactionTooLargeException.</summary>
    public const int MaxBytesCompartir = 100 * 1024;

    [RelayCommand]
    private void Seleccionar(FilaRegistro? fila)
    {
        Seleccionada = fila;
        Aviso = string.Empty;
        Texto = fila is null ? string.Empty : _registros.Leer(fila.Nombre, 24 * 1024);
    }

    [RelayCommand]
    private void Compartir()
    {
        if (Seleccionada is null) return;
        var fila = Seleccionada;
        var recortado = _registros.Ficheros().Any(f => f.Nombre == fila.Nombre && f.Bytes > MaxBytesCompartir);
        try
        {
            _registros.Compartir(IdiomaInterfaz.T("Registros.RegistroPokekeep") + fila.Nombre, _registros.Leer(fila.Nombre, MaxBytesCompartir));
            Aviso = recortado ? IdiomaInterfaz.T("Registros.RegistroLargoCompartidoSolo") : string.Empty;
        }
        catch (Exception)   // en Android: Java.Lang.RuntimeException (p. ej. TransactionTooLargeException); nunca debe tumbar la app
        {
            Aviso = IdiomaInterfaz.T("Registros.PodidoAbrirHojaCompartir");
        }
    }

    [RelayCommand] private void Volver() => _cerrar();
}
