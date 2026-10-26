// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pokekeep.Mobile.Ui.Modelo;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.ViewModels;

/// <summary>Ficha compacta de la casilla elegida: sprite grande, nombre, especie, forma, nivel, tipos, objeto y estado verbal, con Editar / Legalizar automaticamente / Optimizar stats.</summary>
public sealed partial class FichaViewModel : ObservableObject
{
    private readonly Action _editar;
    private readonly Action<AccionPokemon>? _accion;
    private readonly Action? _menu;
    private readonly Action? _legalidad;
    private int _detallesCancelados;
    private readonly Func<IPortapapeles?>? _portapapeles;
    private readonly Action<string, bool>? _aviso;

    public FichaViewModel(CasillaViewModel casilla, Sesion sesion, Action editar, Action<AccionPokemon>? accion = null, Action? menu = null, string? ubicacion = null, Action? legalidad = null, bool accesoLegalidad = false, Ajustes.Pkhex.AjustesPkhex? ajustes = null, Func<IPortapapeles?>? portapapeles = null, Action<string, bool>? aviso = null)
    {
        _editar = editar;
        _accion = accion;
        _menu = menu;
        _legalidad = legalidad;
        _portapapeles = portapapeles;
        _aviso = aviso;
        MostrarAccesoLegalidad = accesoLegalidad && legalidad is not null;
        Ubicacion = ubicacion ?? string.Empty;
        var pk = casilla.Entidad!;
        ajustes ??= Ajustes.Pkhex.ProgramaPkhex.Ajustes.Actual;
        Casilla = casilla;
        var t = sesion.Textos;
        NombreEspecie = t.Species[pk.Species];
        Apodo = pk.IsNicknamed ? pk.Nickname : NombreEspecie;
        Numero = $"N.º {pk.Species:0000}";
        Nivel = IdiomaInterfaz.F("Cajas.Nivel2", pk.CurrentLevel);
        Especie = pk.Species; Forma = pk.Form; Genero = pk.Gender; Brillante = pk.IsShiny; Huevo = pk.IsEgg; Objeto = ObjetosPkm.IdIcono(pk); Contexto = (int)pk.Context;
        NombreObjeto = ObjetosPkm.Nombre(t, pk, sesion.Guardado.Version) is { Length: > 0 } nombreObjeto ? nombreObjeto : IdiomaInterfaz.T("Ficha.Objeto");
        TieneObjeto = pk.HeldItem > 0;
        Tipos = TiposUi.De(pk, t);
        Etiquetas = TiposUi.Etiquetas(pk, t);
        Estado = casilla.Estado;
        TextoEstado = LegalidadUi.Texto(Estado);
        ClaseEstado = LegalidadUi.ClaseInsignia(Estado);
        GlifoEstado = LegalidadUi.Glifo(Estado);
        Resumen = $"{Apodo}, {NombreEspecie}, {Nivel.ToLowerInvariant()}, {TextoEstado.ToLowerInvariant()}";
        // «Ilegal con motivos»: las primeras lineas del informe del Core (solo cuando es ilegal; el informe completo esta en «Legalidad»).
        TextoResumen = ajustes.Hover.HoverSlotShowText ? Cajas.Intercambio.Resumen(pk, casilla.TipoAlmacen, encuentro: false, ajustes: ajustes) : string.Empty;
        TextoShowdown = ajustes.Hover.PreviewShowPaste ? Herramientas.Showdown.ModeloShowdown.Exportar(pk, ajustes) : string.Empty;
        // El set sale con BattleTemplate.Export (Showdown, en inglés de serie), igual que «Exportar a Showdown» de PKHeX: es el único formato que entiende Showdown. El resumen de arriba sí va en el idioma del programa (BattleTemplate.Hover).
        NotaShowdown = TextoShowdown.Length > 0 && Herramientas.Showdown.ModeloShowdown.IdiomaExportacion(ajustes) == LanguageID.English
            ? IdiomaInterfaz.T("Ficha.VaInglesPorqueFormato") : string.Empty;
        if (!casilla.SinAnalisis && (ajustes.Hover.HoverSlotShowEncounter || (Estado == EstadoLegal.Ilegal && ajustes.Hover.HoverSlotShowLegalityHint)))
        {
            var copia = pk.Clone();
            var mostrarEncuentro = ajustes.Hover.HoverSlotShowEncounter;
            var propiedades = ajustes.Hover.HoverSlotShowEncounterVerbose;
            var mostrarMotivos = ajustes.Hover.HoverSlotShowLegalityHint && Estado == EstadoLegal.Ilegal;
            (string Encuentro, IReadOnlyList<string> Motivos) Calcular()
            {
                if (Volatile.Read(ref _detallesCancelados) != 0) return (string.Empty, []);
                var la = AnalizadorLegalidad.AnalizarPara(copia, casilla.TipoAlmacen);
                if (la is null) return (string.Empty, []);
                var lineas = new List<string>();
                if (mostrarEncuentro)
                {
                    if (propiedades) lineas.AddRange(la.EncounterOriginal.GetTextLines(true));
                    else LegalityFormatting.AddEncounterInfo(LegalityLocalizationContext.Create(la, Pokekeep.Mobile.Ui.Ajustes.Pkhex.ProgramaPkhex.Idioma), lineas);
                }
                IReadOnlyList<string> motivos = mostrarMotivos ? [.. la.Report(Pokekeep.Mobile.Ui.Ajustes.Pkhex.ProgramaPkhex.Idioma, false).Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).Take(3)] : [];
                return (Cajas.TextosEncuentro.Traducir(string.Join(Environment.NewLine, lineas)), motivos);
            }
            void Poner((string Encuentro, IReadOnlyList<string> Motivos) datos)
            {
                if (Volatile.Read(ref _detallesCancelados) != 0) return;
                TextoEncuentro = datos.Encuentro;
                Motivos = datos.Motivos;
            }
            if (AnalizadorLegalidad.Asincrono) _ = Task.Run(() =>
            {
                var datos = Calcular();
                Avalonia.Threading.Dispatcher.UIThread.Post(() => Poner(datos));
            });
            else Poner(Calcular());
        }
        SoloLectura = casilla.SoloLectura;
        PuedeAcciones = !casilla.SoloLectura && accion is not null;
    }

    /// <summary>Donde esta («Caja 3, casilla 5»).</summary>
    public string Ubicacion { get; }
    [ObservableProperty] private IReadOnlyList<string> _motivos = [];
    partial void OnMotivosChanged(IReadOnlyList<string> value) => OnPropertyChanged(nameof(HayMotivos));
    [ObservableProperty] private string _textoEncuentro = string.Empty;
    partial void OnTextoEncuentroChanged(string value) => OnPropertyChanged(nameof(HayEncuentro));
    public string TextoResumen { get; }
    public string TextoShowdown { get; }
    /// <summary>Por qué el set sale en inglés (vacía si sale en otro idioma).</summary>
    public string NotaShowdown { get; } = string.Empty;
    public bool HayNotaShowdown => NotaShowdown.Length > 0;
    /// <summary>Hay set y un portapapeles al que copiarlo.</summary>
    public bool PuedeCopiarShowdown => HayShowdown && _portapapeles?.Invoke() is not null;
    public bool HayResumen => !string.IsNullOrEmpty(TextoResumen);
    public bool HayShowdown => !string.IsNullOrEmpty(TextoShowdown);
    public bool HayEncuentro => !string.IsNullOrEmpty(TextoEncuentro);
    public void CancelarDetalles() => Interlocked.Exchange(ref _detallesCancelados, 1);
    public bool HayMotivos => Motivos.Count > 0;
    public bool SoloLectura { get; }
    /// <summary>Las dos acciones Keep se pueden lanzar sobre esta casilla (hay motor y la casilla es escribible).</summary>
    public bool PuedeAcciones { get; }
    public bool PuedeLanzar => PuedeAcciones;

    public CasillaViewModel Casilla { get; }
    public string Apodo { get; }
    public string NombreEspecie { get; }
    public string Numero { get; }
    public string Nivel { get; }
    public int Contexto { get; }
    public int Especie { get; }
    public int Forma { get; }
    public uint ArgumentoForma => Casilla.ArgumentoForma;
    public int VersionJuego => Casilla.VersionJuego;
    public Pokekeep.Sprites.ModoSprites Modo => Casilla.Modo;
    public int Genero { get; }
    public bool Brillante { get; }
    public bool Huevo { get; }
    public int Objeto { get; }
    public string NombreObjeto { get; }
    public bool TieneObjeto { get; }
    public Avalonia.Media.Imaging.Bitmap? ImagenObjeto => Design.Sprites.ObjetoEquipado(Objeto, (PKHeX.Core.EntityContext)Contexto, (PKHeX.Core.GameVersion)VersionJuego);
    public bool EsIlegal => Estado == EstadoLegal.Ilegal;
    public IReadOnlyList<string> Tipos { get; }
    /// <summary>Los mismos tipos como etiquetas de color.</summary>
    public IReadOnlyList<TipoVm> Etiquetas { get; }
    public EstadoLegal Estado { get; }
    public string TextoEstado { get; }
    public string ClaseEstado { get; }
    public string GlifoEstado { get; }
    public string Resumen { get; }

    /// <summary>Copia el set de Showdown (el texto exacto que se ve) al portapapeles del sistema y lo avisa.</summary>
    [RelayCommand]
    private async Task CopiarShowdownAsync()
    {
        if (_portapapeles?.Invoke() is not { } p || !HayShowdown) return;
        var ok = await p.CopiarAsync(TextoShowdown);
        _aviso?.Invoke(ok ? IdiomaInterfaz.T("Ficha.CopiadoPortapapeles") : MessageStrings.MsgClipboardFailWrite, ok);
    }

    [RelayCommand] private void Editar() => _editar();
    [RelayCommand] private void Menu() => _menu?.Invoke();
    public bool HayMenu => _menu is not null;
    public bool MostrarAccesoLegalidad { get; }
    [RelayCommand] private void VerLegalidad() => _legalidad?.Invoke();
    [RelayCommand] private void Legalizar() => _accion?.Invoke(AccionPokemon.Legalizar);
    [RelayCommand] private void Optimizar() => _accion?.Invoke(AccionPokemon.Optimizar);
}
