// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using CommunityToolkit.Mvvm.ComponentModel;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.ViewModels;

public sealed record SeccionGuia(string Titulo, string Formato, string Pasos, string Fuente);

/// <summary>Guía disponible sin red. Las rutas se explican por exportación/selector, sin prometer acceso al almacenamiento privado de otra app.</summary>
public sealed partial class GuiaGuardadosViewModel : PantallaMasViewModel
{
    private readonly bool _gestos;
    public GuiaGuardadosViewModel(Action volver, bool gestos = false) : base(volver) => _gestos = gestos;
    public override string Titulo => _gestos ? IdiomaInterfaz.T("AcercaDe.GestosEquivalencias") : IdiomaInterfaz.T("Menu.Mas.Guardados.Titulo");
    public string Introduccion => _gestos ? IdiomaInterfaz.T("GuiaGuardados.AccionesEscritorioTienenBotones") : IdiomaInterfaz.T("GuiaGuardados.GuardaDentroJuegoCierralo");
    [ObservableProperty] private string _busqueda = string.Empty;
    partial void OnBusquedaChanged(string value) => OnPropertyChanged(nameof(Secciones));
    public IReadOnlyList<SeccionGuia> Secciones => [.. (_gestos ? Gestos : Guardados).Where(s => string.IsNullOrWhiteSpace(Busqueda) || (s.Titulo + " " + s.Formato + " " + s.Pasos).Contains(Busqueda, StringComparison.OrdinalIgnoreCase))];

    public static IReadOnlyList<SeccionGuia> Guardados => [
        new(IdiomaInterfaz.T("GuiaGuardadosViewModel.NintendoDsAzaharLime"), IdiomaInterfaz.T("GuiaGuardados.MainGuardadoDescifrado"), IdiomaInterfaz.T("GuiaGuardados.EmuladorPCAbreUbicacion"), "https://github.com/azahar-emu/azahar"),
        new(IdiomaInterfaz.T("GuiaGuardadosViewModel.NintendoSwitchEdenSuyu"), IdiomaInterfaz.T("GuiaGuardados.MainSaveDataBinU"), IdiomaInterfaz.T("GuiaGuardados.LocalizaGuardadoTituloUsuario"), "https://github.com/UzuCore/eden/blob/master/docs/user/SyncthingGuide.md"),
        new(IdiomaInterfaz.T("GuiaGuardadosViewModel.NintendoDsMelondsDesmume"), ".sav · .dsv", IdiomaInterfaz.T("GuiaGuardados.GuardaDesdeMenuJuego"), "https://wiki.desmume.org/index.php?title=Faq"),
        new(IdiomaInterfaz.T("GuiaGuardadosViewModel.GameBoyYGba"), IdiomaInterfaz.T("GuiaGuardados.SavSrmSa1Segun"), IdiomaInterfaz.T("GuiaGuardados.ExportaGuardadoJuegoDesde"), "https://docs.libretro.com/guides/retroarch-cloud-sync/"),
        new(IdiomaInterfaz.T("GuiaGuardadosViewModel.GamecubeYWiiDolphin"), IdiomaInterfaz.T("GuiaGuardados.GciTarjetaRawBin"), IdiomaInterfaz.T("GuiaGuardados.DolphinPuedeGuardarGameCube"), "https://dolphin-emu.org/blog/2014/12/19/best-new-features-2014/"),
        new(IdiomaInterfaz.T("GuiaGuardados.PokemonHOMEPokemonIndividuales"), ".pk* · .pkh · volcados .bin · GP1", IdiomaInterfaz.T("GuiaGuardados.ArchivoPokemonPartidaAbre"), "https://projectpokemon.org/home/files/file/1-pkhex/"),
        new(IdiomaInterfaz.T("GuiaGuardados.DondeGuardadoAndroidCopias"), IdiomaInterfaz.T("GuiaGuardados.ArchivoAccesibleDesdeSelector"), IdiomaInterfaz.T("GuiaGuardados.PCUsaMenuEmulador"), "https://developer.android.com/training/data-storage/shared/documents"),
    ];

    public static IReadOnlyList<SeccionGuia> Gestos => [
        new(IdiomaInterfaz.T("GuiaGuardados.VerEditarLegalizar"), IdiomaInterfaz.T("GuiaGuardados.ClicDerechoMenuCasilla"), IdiomaInterfaz.T("GuiaGuardados.TocaCasillaVerFicha"), "Equivalencia implementada en CajasViewModel y EditorViewModel"),
        new(IdiomaInterfaz.T("GuiaGuardados.MoverCopiarSeleccionarVarios"), IdiomaInterfaz.T("GuiaGuardados.ArrastrarCtrlMayusAcciones"), IdiomaInterfaz.T("GuiaGuardados.MantenPulsadoSeleccionarVarios"), IdiomaInterfaz.T("GuiaGuardados.MenusCasillaCajaPokekeep")),
        new(IdiomaInterfaz.T("GuiaGuardados.CamposEstadisticas"), IdiomaInterfaz.T("GuiaGuardados.ModificadoresTecladoMenuCampo"), IdiomaInterfaz.T("GuiaGuardados.TocaValorEscribirAbrir"), IdiomaInterfaz.T("GuiaGuardados.EditorCamposVmMenusEstadisticas")),
        new(IdiomaInterfaz.T("GuiaGuardados.AplicarGuardarVolver"), IdiomaInterfaz.T("GuiaGuardados.AplicarCambiaSesionGuardar"), IdiomaInterfaz.T("GuiaGuardados.AplicarGuardaBorradorSesion"), IdiomaInterfaz.T("GuiaGuardados.HistorialUnicoGestorSesiones")),
        new(IdiomaInterfaz.T("GuiaGuardados.ReferenciaCompletaEscritorio"), IdiomaInterfaz.T("GuiaGuardados.AtajosOriginalesPKHeXMovil"), LeerAtajosOriginales(), IdiomaInterfaz.T("GuiaGuardados.ShortcutsTxtPKHeXModificar")),
    ];

    public static string LeerAtajosOriginales()
    {
        using var recurso = typeof(GuiaGuardadosViewModel).Assembly.GetManifestResourceStream("shortcuts_es.txt") ?? throw new InvalidOperationException("Falta la referencia de atajos de PKHeX.");
        using var lector = new StreamReader(recurso);
        return lector.ReadToEnd();
    }
}
