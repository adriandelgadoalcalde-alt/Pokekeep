// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pokekeep.Mobile.Ui.Abrir;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Herramientas.ImportarHex;

/// <summary>
/// «Más &gt; Herramientas &gt; Importar hexadecimal» (PK-MENU-026, PK-IO-021). PKHeX lee el portapapeles al pulsar el menú; aquí el texto se pega (botón «Pegar del portapapeles» o a mano) y se ve antes de
/// abrirlo. «Abrir» lo convierte (<see cref="ModeloImportarHex"/>) y lo pasa al despachador de archivos de la Shell: una partida se abre, un Pokémon enseña su ficha, lo no reconocido lo dice. No toca la partida.
/// </summary>
public sealed partial class ImportarHexViewModel(ContextoHerramienta contexto, Action volver) : HerramientaViewModel(contexto, volver)
{
    public override string Titulo => IdiomaInterfaz.T("CatalogoImportarHex.ImportarHexadecimal");
    public string Subtitulo => IdiomaInterfaz.T("ImportarHex.PegaBytesArchivoEscritos");

    [ObservableProperty] private string _texto = string.Empty;
    [ObservableProperty] private string _mensaje = string.Empty;
    [ObservableProperty] private bool _mensajeEsError;
    public bool HayMensaje => !string.IsNullOrEmpty(Mensaje);
    partial void OnMensajeChanged(string value) => OnPropertyChanged(nameof(HayMensaje));
    partial void OnTextoChanged(string value) { Mensaje = string.Empty; OnPropertyChanged(nameof(Resumen)); }

    /// <summary>Cuántos bytes salen del texto tal y como está (o por qué no salen).</summary>
    public string Resumen
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Texto)) return IdiomaInterfaz.T("ImportarHex.TextoTodavia");
            var datos = ModeloImportarHex.Convertir(Texto, out var error);
            return datos is null ? error! : $"{datos.Length:N0} bytes.";
        }
    }

    public bool PuedePegar => Contexto.Portapapeles() is not null;

    [RelayCommand]
    private async Task Pegar()
    {
        if (Contexto.Portapapeles() is not { } clip) return;
        var t = await clip.LeerAsync();
        Texto = t ?? string.Empty;
        if (string.IsNullOrWhiteSpace(Texto)) { _ = ModeloImportarHex.Convertir(Texto, out var error); MostrarError(error ?? string.Empty); }
    }

    [RelayCommand]
    private Task Abrir() => AbrirSincronoOAsync();

    /// <summary>Convierte y abre. Devuelve la tarea del despachador (las pruebas la esperan).</summary>
    public async Task AbrirSincronoOAsync()
    {
        var datos = ModeloImportarHex.Convertir(Texto, out var error);
        if (datos is null) { MostrarError(error!); return; }
        if (Contexto.AbrirBytes is not { } abrir) { MostrarError(IdiomaInterfaz.T("ImportarHex.AbrirDisponibleVentana")); return; }
        Mensaje = $"Abriendo {datos.Length:N0} bytes…"; MensajeEsError = false;
        await abrir(datos, string.Empty);
        Mensaje = IdiomaInterfaz.F("ImportarHex.LeidosN0Bytes", datos.Length, Apertura.SinNombre);
    }

    /// <summary>Deja el mensaje de error en pantalla (gate visual y pruebas, sin trabajo en segundo plano).</summary>
    public void MostrarError(string mensaje) { Mensaje = mensaje; MensajeEsError = mensaje.Length > 0; }
}
