// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;
using Pokekeep.Mobile.Ui.Ajustes.Pkhex;
using Pokekeep.Mobile.Ui.Modelo;

namespace Pokekeep.Mobile.Ui.ViewModels;

/// <summary>
/// Lo que la Shell hace por el ARRANQUE de PKHeX (bloque 10-1), aparte para no pisar <c>ShellViewModel.cs</c>: los avisos del arranque (modo HaX, registro de cambios; <c>Program.Main</c>) y la partida en blanco
/// (<c>Main.LoadBlankSaveFile</c> con <c>Startup.DefaultSaveVersion</c>).
/// </summary>
public sealed partial class ShellViewModel
{
    /// <summary>La Shell que se ve ahora: la Shell se rehace con cada partida y solo la última atiende los cambios de ajustes que la afectan.</summary>
    private static WeakReference<ShellViewModel>? _activa;

    static ShellViewModel()
    {
        FilaPkhexVersionBlanco.VersionCambiada += v => { if (_activa?.TryGetTarget(out var s) == true) s.AlCambiarVersionBlanco(v); };
        ProgramaPkhex.Cambiado += () => { if (_activa?.TryGetTarget(out var s) == true) s.AlCambiarAjustesPkhex(); };
        HuellaLegalidad.Cambiado += () => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (_activa?.TryGetTarget(out var s) == true) s.AlCambiarAjustesPkhex();
        });
    }

    /// <summary>El idioma de los datos con el que están pintados los nombres de esta Shell.</summary>
    private string _idiomaPintado = ProgramaPkhex.Idioma;
    private string _huellaLegalidadVista = HuellaLegalidad.Vigente;
    private string _presentacionPintada = PresentacionPkhex.Huella(ProgramaPkhex.Ajustes.Actual.Display);
    private string _visualPintado = PresentacionPkhex.HuellaVisual(ProgramaPkhex.Ajustes.Actual);

    /// <summary>
    /// Tras un cambio de ajustes de PKHeX: si cambió el idioma de los datos (<c>Startup.Language</c>), lo que ya está pintado con nombres del juego se vuelve a leer al momento (las casillas y la ficha de la pestaña
    /// Pokémon, la Mochila sin borrador). Lo que se abre después (editor, formularios de la Partida, hojas) ya nace con el idioma nuevo, porque lee <see cref="ProgramaPkhex.Textos"/> al crearse.
    /// </summary>
    public void AlCambiarAjustesPkhex()
    {
        OnPropertyChanged(nameof(OcultarDetallesPartida)); OnPropertyChanged(nameof(TituloPartida)); OnPropertyChanged(nameof(ResumenPartida)); OnPropertyChanged(nameof(SubtituloPartida)); OnPropertyChanged(nameof(TituloCompactoPartida));
        var visual = PresentacionPkhex.HuellaVisual(AjustesPkhex.Actual);
        if (_visualPintado != visual)
        {
            _visualPintado = visual;
            Design.Sprites.ActualizarPreferencias();
            Sesion?.ActualizarSprites(AjustesPkhex.Actual.Sprite);
            Cajas?.Refrescar();
            Editor?.ActualizarPresentacion();
        }
        var presentacion = PresentacionPkhex.Huella(AjustesPkhex.Actual.Display);
        if (_presentacionPintada != presentacion)
        {
            _presentacionPintada = presentacion;
            Cajas?.Refrescar();
            Editor?.ActualizarPresentacion();
        }
        if (_huellaLegalidadVista != HuellaLegalidad.Vigente)
        {
            _huellaLegalidadVista = HuellaLegalidad.Vigente;
            Cajas?.Refrescar();
        }
        var idioma = ProgramaPkhex.Idioma;
        if (idioma == _idiomaPintado) return;
        _idiomaPintado = idioma;
        foreach (var entrada in MenuMas.Concat(MenuPartida)) entrada.ActualizarIdioma();
        Cajas?.Refrescar();
        MochilaVm?.ReleerNombres();
        Libreria?.ActualizarIdioma();
        PartidaVm?.ActualizarIdioma();
        Editor?.ActualizarIdioma();
        Pantalla?.ActualizarIdioma();
        Hoja?.ActualizarIdioma();
        OnPropertyChanged(nameof(TextoPartidaEnBlanco));
        OnPropertyChanged(nameof(TextoDetalleAviso)); OnPropertyChanged(nameof(TextoCambios)); OnPropertyChanged(nameof(TextoCopiaLocal));
        // No se vuelve a validar el almacenamiento ni se cambia la sesión por traducir rótulos.
        Recientes = [.. Recientes.Select(f => new FilaReciente(f.Referencia, $"{JuegoDeReciente(f.Referencia.Juego)} · {Hace(f.Referencia.Fecha)}"))];
        Recuperables = [.. Recuperables.Select(f => new FilaRecuperable(f.Referencia, $"{JuegoDeReciente(f.Referencia.Juego)} · {IdiomaInterfaz.F("Inicio.CambiosSinGuardar", Hace(f.Referencia.Fecha))}"))];
    }

    /// <summary>Marca esta Shell como la visible (la llama quien la pone en pantalla).</summary>
    public void Activar() => _activa = new WeakReference<ShellViewModel>(this);

    public static void RevalidarRecientesEnPrimerPlano() => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
    {
        if (_activa?.TryGetTarget(out var shell) == true) _ = shell.ValidarRecientesAsync();
    });

    /// <summary>
    /// Tras el arranque (<see cref="ArranquePkhex.Iniciar"/>), lo mismo que PKHeX al enseñar la ventana principal: en modo HaX el aviso de modo ilegal (<c>WarnBehavior</c>, con los textos del Core en castellano);
    /// si no, con una versión nueva y «Mostrar cambios tras actualizar», «Acerca de» con las novedades (<c>ShowAboutDialog(AboutPage.Changelog)</c>).
    /// </summary>
    public void AlArrancar(InicioPrograma inicio)
    {
        Activar();
        if (inicio.HaX)
        {
            TextosNucleo.Preparar();
            Informar(IdiomaInterfaz.F("Shell.EditorAbreModoAvanzado", MessageStrings.MsgProgramIllegalModeActive, MessageStrings.MsgProgramIllegalModeBehave), "aviso", "TriangleAlert");
        }
        else if (inicio.MostrarNovedades && ProveedorAcercaDe is not null)
        {
            Destino = Destino.Mas;
            AbrirAcercaDe();
        }
    }

    /// <summary>La versión de la partida en blanco de los ajustes, con su nombre en el idioma de los datos.</summary>
    public GameVersion VersionEnBlanco => AjustesPkhex.Actual.Startup.DefaultSaveVersion;
    public string TextoPartidaEnBlanco => IdiomaInterfaz.F("Inicio.Blanco", FilaPkhexVersionBlanco.NombreDe(VersionEnBlanco));

    /// <summary>
    /// «Empezar con una partida en blanco»: <c>BlankSaveFile.Get(DefaultSaveVersion, partida actual)</c>, como <c>Main.LoadBlankSaveFile</c>. Es una sesión SIN documento original: «Guardar» pide usar «Exportar copia».
    /// La partida que hubiera abierta sigue en «Recuperar» si tenía cambios (igual que al abrir otro guardado).
    /// </summary>
    [RelayCommand]
    public void AbrirPartidaEnBlanco()
    {
        var sav = BlankSaveFile.Get(VersionEnBlanco, Sesion?.Guardado);
        sav.State.Edited = false;
        var nombre = IdiomaInterfaz.F("Inicio.NombreBlanco", FilaPkhexVersionBlanco.NombreDe(sav.Version), sav.Metadata.GetSuggestedExtension());
        SesionNueva?.Invoke(new Sesion(sav, nombre));
    }

    /// <summary>
    /// <c>SettingsEditor.BlankChanged</c>: PKHeX carga en el acto una partida en blanco de la versión elegida. Sin partida abierta se hace lo mismo; con una partida abierta NO se cambia sin preguntar
    /// (se perdería de vista lo que el usuario estaba editando): el aviso trae el botón «Abrir ahora».
    /// </summary>
    private void AlCambiarVersionBlanco(GameVersion v)
    {
        OnPropertyChanged(nameof(VersionEnBlanco)); OnPropertyChanged(nameof(TextoPartidaEnBlanco));
        if (Sesion is null) { AbrirPartidaEnBlanco(); return; }
        Informar(IdiomaInterfaz.F("Shell.PartidaBlancoSera", FilaPkhexVersionBlanco.NombreDe(v)), "ok", "CircleCheck", IdiomaInterfaz.T("Shell.AbrirAhora"), AbrirPartidaEnBlancoCommand);
    }
}
