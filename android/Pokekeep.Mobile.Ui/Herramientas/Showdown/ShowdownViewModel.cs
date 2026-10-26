// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Ajustes.Pkhex;
using Pokekeep.Mobile.Ui.ViewModels;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Herramientas.Showdown;

/// <summary>
/// «Más &gt; Herramientas &gt; Showdown» (PK-MENU-008, el submenú <c>Menu_Showdown</c> de PKHeX): importar el primer set de un texto (<c>Menu_ShowdownImportPKM</c>) y aplicarlo a un Pokémon de la partida,
/// y exportar el equipo, una caja o todas las cajas (<c>Menu_ShowdownExportParty</c>, <c>Menu_ShowdownExportCurrentBox</c>, con Ctrl = todas). Sin diálogos de alternativas (regla 4): donde PKHeX pregunta
/// «¿Importar este set?» la pantalla enseña el set reformateado y el botón. El set se aplica por <c>Sesion.Casillas</c> (un Deshacer exacto) sin forzar legalidad (regla 1); nada sale del dispositivo salvo el texto
/// que el usuario manda al portapapeles (reglas 13 y 15) y las direcciones de Showdown o Pokepaste NO se descargan.
/// </summary>
public sealed partial class ShowdownViewModel : HerramientaViewModel
{
    public ShowdownViewModel(ContextoHerramienta contexto, Action volver) : base(contexto, volver)
    {
        TextosNucleo.Preparar();
        _cajaExportar = Math.Clamp(contexto.CajaActual?.Invoke() ?? 0, 0, Math.Max(0, (Sesion?.Guardado.BoxCount ?? 1) - 1));
    }

    public override string Titulo => IdiomaInterfaz.T("Ajustes.Pkhex.Grupo.16");
    public string Subtitulo => IdiomaInterfaz.T("Showdown.ImportaSetTextoExporta");
    public bool HayPartida => Sesion is not null;
    public bool TieneEquipo => Sesion?.Guardado.HasParty == true;
    public bool TieneCajas => Sesion?.Guardado.HasBox == true;

    // ---------------------------------------------------------------------------------------------------------------- importar
    [ObservableProperty, NotifyPropertyChangedFor(nameof(PuedeRevisar))] private string _textoSet = string.Empty;
    [ObservableProperty] private string _avisoSet = string.Empty;
    [ObservableProperty] private string _vistaPrevia = string.Empty;
    [ObservableProperty] private string _lineasNoValidas = string.Empty;
    [ObservableProperty] private string _cabeceraSet = string.Empty;
    public ResultadoImportarSet? Analisis { get; private set; }
    public bool PuedeRevisar => !string.IsNullOrWhiteSpace(TextoSet);
    public bool HayAvisoSet => AvisoSet.Length > 0;
    public bool HayVistaPrevia => Analisis?.Correcto == true;
    public bool HayLineasNoValidas => LineasNoValidas.Length > 0;
    partial void OnAvisoSetChanged(string value) => OnPropertyChanged(nameof(HayAvisoSet));
    partial void OnLineasNoValidasChanged(string value) => OnPropertyChanged(nameof(HayLineasNoValidas));

    /// <summary>Pega el texto del portapapeles (<c>Clipboard.GetText()</c>) y lo revisa. Un portapapeles sin texto dice lo que dice PKHeX (<c>MsgClipboardFailRead</c>).</summary>
    [RelayCommand]
    public async Task Pegar()
    {
        var clip = Contexto.Portapapeles();
        var texto = clip is null ? null : await clip.LeerAsync();
        if (string.IsNullOrWhiteSpace(texto)) { LimpiarSet(); AvisoSet = clip is null ? IdiomaInterfaz.T("Editor.PortapapelesDisponible") : MessageStrings.MsgClipboardFailRead; return; }
        TextoSet = texto;
        Revisar();
    }

    /// <summary>Lee el primer set del texto y enseña cómo quedaría reformateado y qué líneas no entendió (<c>ClickShowdownImportPKM</c> sin el cuadro de confirmación).</summary>
    [RelayCommand]
    public void Revisar()
    {
        LimpiarSet();
        var r = ModeloShowdown.Analizar(TextoSet, Ajustes);
        Analisis = r;
        if (!r.Correcto) { AvisoSet = r.Error!; OnPropertyChanged(nameof(HayVistaPrevia)); return; }
        CabeceraSet = r.SetsEnElTexto > 1 ? IdiomaInterfaz.F("Showdown.SetsTextoSoloUsa", r.SetsEnElTexto) : MessageStrings.MsgSimulatorLoad;
        VistaPrevia = r.TextoReformateado;
        LineasNoValidas = r.LineasNoValidas.Count == 0 ? string.Empty : ModeloShowdown.TextoLineasNoValidas(r);
        OnPropertyChanged(nameof(HayVistaPrevia));
        Estado = IdiomaInterfaz.T("Showdown.SetLeidoEligePokemon");
    }

    private void LimpiarSet()
    {
        Analisis = null; AvisoSet = string.Empty; VistaPrevia = string.Empty; LineasNoValidas = string.Empty; CabeceraSet = string.Empty;
        OnPropertyChanged(nameof(HayVistaPrevia));
    }

    /// <summary>Elige la casilla: primero la caja (o el equipo) y luego el Pokémon. Solo se ofrecen casillas con Pokémon, que es lo que el set sustituye (PKHeX aplica el set al Pokémon abierto en el editor).</summary>
    [RelayCommand]
    private void AplicarA()
    {
        if (Sesion is not { } sesion || Analisis?.Correcto != true) return;
        var sav = sesion.Guardado;
        var opciones = new List<OpcionSelector>();
        if (sav.HasParty && Enumerable.Range(0, Math.Min(6, sav.PartyCount)).Any(i => sav.GetPartySlotAtIndex(i).Species != 0)) opciones.Add(new OpcionSelector(-1, IdiomaInterfaz.T("Form.Equipo")));
        if (sav.HasBox)
            for (var b = 0; b < sav.BoxCount; b++)
            {
                var n = sav.GetBoxData(b).Count(p => p.Species != 0);
                if (n > 0) opciones.Add(new OpcionSelector(b, sesion.NombreCaja(b), n == 1 ? "1 Pokémon" : IdiomaInterfaz.F("Cajas.Pokemon2", n)));
            }
        if (opciones.Count == 0) { Estado = IdiomaInterfaz.T("Showdown.PartidaTieneNingunPokemon"); return; }
        Contexto.AbrirHoja(new SelectorHojaViewModel(IdiomaInterfaz.T("Showdown.CajaPokemon"), opciones, -2, o => ElegirPokemon(o.Id), IdiomaInterfaz.T("Showdown.BuscarCaja")));
    }

    private void ElegirPokemon(int caja)
    {
        if (Sesion is not { } sesion) return;
        var sav = sesion.Guardado;
        var casillas = new List<(ISlotInfo Info, PKM Pk)>();
        if (caja < 0) for (var i = 0; i < Math.Min(6, sav.PartyCount); i++) { var info = new SlotInfoParty(i); var pk = info.Read(sav); if (pk.Species != 0) casillas.Add((info, pk)); }
        else for (var i = 0; i < sav.BoxSlotCount; i++) { var info = new SlotInfoBox(caja, i, sav); var pk = info.Read(sav); if (pk.Species != 0) casillas.Add((info, pk)); }
        var opciones = casillas.Select((c, i) => new OpcionSelector(i, IdiomaInterfaz.F("Showdown.Nivel", sesion.Textos.Species[c.Pk.Species], c.Pk.CurrentLevel), IdiomaInterfaz.F("Qr.CasillaAccesible", c.Info.Slot + 1), c.Pk.Species, c.Pk.Form)).ToList();
        Contexto.AbrirHoja(new SelectorHojaViewModel(caja < 0 ? IdiomaInterfaz.T("Showdown.PokemonEquipo") : sesion.NombreCaja(caja), opciones, -1, o => ConfirmarAplicar(casillas[o.Id].Info, casillas[o.Id].Pk), IdiomaInterfaz.T("Showdown.BuscarPokemon")));
    }

    private void ConfirmarAplicar(ISlotInfo info, PKM pk)
    {
        if (Sesion is not { } sesion || Analisis?.Set is null) return;
        var nombre = sesion.Textos.Species[pk.Species];
        List<string> parrafos =
        [
            MessageStrings.MsgSimulatorLoad,
            Analisis.TextoReformateado,
            IdiomaInterfaz.F("Showdown.AplicaraSetDigaQueda", nombre, Descripcion(info)),
            IdiomaInterfaz.T("Cajas.PuedesDeshacerloDeshacer"),
        ];
        Contexto.AbrirHoja(new ConfirmarHojaViewModel(IdiomaInterfaz.T("Showdown.AplicarSet"), parrafos, IdiomaInterfaz.T("Editor.Aplicar"), () => _ = AplicarAsync(info, pk), destructiva: true));
    }

    private static string Descripcion(ISlotInfo info) => info switch { SlotInfoBox b => IdiomaInterfaz.F("Showdown.CajaCasilla", b.Box + 1, b.Slot + 1), SlotInfoParty p => IdiomaInterfaz.F("Showdown.EquipoCasilla", p.Slot + 1), _ => IdiomaInterfaz.F("Showdown.Casilla", info.Slot + 1) };

    /// <summary>
    /// Aplica el set al Pokémon (en un hilo aparte: <c>ApplySetDetails</c> analiza la legalidad y espera el cerrojo) y lo coloca por el historial único. Si la casilla cambió mientras tanto no se escribe nada (regla 7).
    /// </summary>
    public async Task AplicarAsync(ISlotInfo info, PKM original)
    {
        if (Sesion is not { } sesion || Analisis?.Set is not { } set) return;
        var huella = original.Data.ToArray();
        var aplicado = await EjecutarAsync(IdiomaInterfaz.T("Showdown.AplicandoSet"), (_, _) => ModeloShowdown.Aplicar(original, set));
        if (aplicado is null) return;
        if (!info.Read(sesion.Guardado).Data.SequenceEqual(huella))
        {
            Estado = IdiomaInterfaz.T("Showdown.CasillaCambiadoMientrasAplicaba");
            Contexto.Informar(Estado, "aviso", "CircleAlert");
            return;
        }
        var r = sesion.Casillas.Colocar(info, aplicado);
        Estado = r.Correcto ? IdiomaInterfaz.F("Showdown.SetAplicadoPuedesDeshacerlo", sesion.Textos.Species[aplicado.Species]) : r.Mensaje;
        if (r.Correcto && Analisis.LineasNoValidas.Count > 0) AvisoSet = ModeloShowdown.TextoLineasNoValidas(Analisis);   // PKHeX avisa de las líneas no válidas tras importar
        Contexto.Informar(Estado, r.Correcto ? "ok" : "error", r.Correcto ? "CircleCheck" : "TriangleAlert");
    }

    // ---------------------------------------------------------------------------------------------------------------- exportar
    [ObservableProperty, NotifyPropertyChangedFor(nameof(NombreCajaExportar))] private int _cajaExportar;
    [ObservableProperty] private string _textoExportado = string.Empty;
    public bool HayTextoExportado => TextoExportado.Length > 0;
    partial void OnTextoExportadoChanged(string value) => OnPropertyChanged(nameof(HayTextoExportado));
    public string NombreCajaExportar => Sesion is null || !TieneCajas ? string.Empty : Sesion.NombreCaja(CajaExportar);

    /// <summary>Elige la caja que exporta «Exportar la caja» (PKHeX exporta la que se está viendo; aquí empieza en la que se veía en Pokémon).</summary>
    [RelayCommand]
    private void ElegirCaja()
    {
        if (Sesion is not { } sesion || !TieneCajas) return;
        var opciones = Enumerable.Range(0, sesion.Guardado.BoxCount).Select(b => new OpcionSelector(b, sesion.NombreCaja(b))).ToList();
        Contexto.AbrirHoja(new SelectorHojaViewModel(IdiomaInterfaz.T("Showdown.CajaExportar"), opciones, CajaExportar, o => CajaExportar = o.Id, IdiomaInterfaz.T("Showdown.BuscarCaja")));
    }

    [RelayCommand] private Task ExportarEquipo() => ExportarAsync(equipo: true, sav => ModeloShowdown.ExportarEquipo(sav, Ajustes));
    [RelayCommand] private Task ExportarCaja() => ExportarAsync(equipo: false, sav => ModeloShowdown.ExportarCaja(sav, CajaExportar, Ajustes));
    [RelayCommand] private Task ExportarTodas() => ExportarAsync(equipo: false, sav => ModeloShowdown.ExportarTodasLasCajas(sav, Ajustes));

    private async Task ExportarAsync(bool equipo, Func<SaveFile, string?> producir)
    {
        if (Sesion is not { } sesion) return;
        var texto = await EjecutarAsync(IdiomaInterfaz.T("Showdown.PreparandoSets"), (_, _) => producir(sesion.Guardado));
        await TerminarExportacionAsync(texto, equipo);
    }

    /// <summary>Prepara la exportación en este mismo hilo y la deja en pantalla sin tocar el portapapeles (el gate visual y las pruebas la usan para el estado «exportado»).</summary>
    public void MostrarExportacion(bool equipo)
    {
        if (Sesion is not { } sesion) return;
        var texto = equipo ? ModeloShowdown.ExportarEquipo(sesion.Guardado, Ajustes) : ModeloShowdown.ExportarCaja(sesion.Guardado, CajaExportar, Ajustes);
        PonerTexto(texto, equipo);
    }

    private async Task TerminarExportacionAsync(string? texto, bool equipo)
    {
        if (!PonerTexto(texto, equipo)) return;
        var clip = Contexto.Portapapeles();
        if (clip is null) { Estado = IdiomaInterfaz.T("Showdown.PortapapelesDisponibleTextoAbajo"); return; }
        var ok = await clip.CopiarAsync(TextoExportado);
        Estado = ok ? ModeloShowdown.AvisoExito(equipo) : MessageStrings.MsgClipboardFailWrite;
        Contexto.Informar(Estado, ok ? "ok" : "error", ok ? "CircleCheck" : "TriangleAlert");
    }

    private bool PonerTexto(string? texto, bool equipo)
    {
        if (string.IsNullOrWhiteSpace(texto)) { TextoExportado = string.Empty; Estado = equipo ? IdiomaInterfaz.T("Cajas.EquipoTieneNingunPokemon") : IdiomaInterfaz.T("Cajas.NingunPokemonExportar"); return false; }
        TextoExportado = texto;
        Estado = equipo ? ModeloShowdown.AvisoExito(true) : ModeloShowdown.AvisoExito(false);
        return true;
    }

    [RelayCommand]
    private async Task CopiarExportado()
    {
        if (!HayTextoExportado || Contexto.Portapapeles() is not { } clip) return;
        var ok = await clip.CopiarAsync(TextoExportado);
        Estado = ok ? IdiomaInterfaz.T("Ficha.CopiadoPortapapeles") : MessageStrings.MsgClipboardFailWrite;
    }
}
