// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Cajas;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.ViewModels;

// Seleccion multiple (pulsacion larga + toques, «Seleccionar caja») y modo de colocar (mover, intercambiar y copiar entre casillas y cajas: el arrastrar y soltar de PKHeX, adaptado al tacto).
public sealed partial class CajasViewModel
{
    private readonly List<CasillaRef> _marcadas = [];
    private List<CasillaRef> _origenes = [];

    [ObservableProperty] private bool _seleccionando;
    [ObservableProperty] private int _numeroMarcadas;
    [ObservableProperty] private bool _colocando;
    [ObservableProperty] private bool _colocandoCopia;
    [ObservableProperty] private string _textoColocar = string.Empty;
    [ObservableProperty] private string _ayudaColocar = string.Empty;

    public IReadOnlyList<CasillaRef> Marcadas => _marcadas;
    /// <summary>Con texto grande (x1,3 o mas) las cuatro acciones de la seleccion pasan a dos filas: las etiquetas no caben en un cuarto del ancho.</summary>

    public string TextoSeleccion => NumeroMarcadas == 1 ? IdiomaInterfaz.T("CajasViewModelSeleccion.PokemonSeleccionado") : IdiomaInterfaz.F("Cajas.PokemonSeleccionados", NumeroMarcadas);
    public bool NoSeleccionando => !Seleccionando;
    public bool HayMarcadas => NumeroMarcadas > 0;
    public bool MuestraCabeceraNormal => !Seleccionando && !Colocando;
    // La cabecera tiene cuatro estados que comparten sitio (la rejilla no se mueve): normal, seleccion, colocar y busqueda; la barra de acciones va abajo, en lugar de la de destinos.
    public bool CabeceraNormal => !Seleccionando && !Colocando && !Buscando;
    public bool CabeceraSeleccion => Seleccionando;
    public bool CabeceraColocar => Colocando && !Seleccionando;
    public bool CabeceraBusqueda => Buscando && !Seleccionando && !Colocando;
    /// <summary>Hay una barra de acciones contextual (seleccion o colocar) que sustituye a la barra de destinos.</summary>
    public bool BarraContextual => Seleccionando || Colocando;
    public bool BarraSeleccion => Seleccionando;
    public bool BarraColocar => Colocando && !Seleccionando;
    /// <summary>Con texto grande (x1,3 o mas) las acciones de la barra pierden la etiqueta (queda el icono y el nombre accesible).</summary>
    public bool EtiquetasEnBarra => Design.Movimiento.EscalaTexto < 1.3;
    private void CambioDeEstadoDeCabecera()
    {
        foreach (var n in new[] { nameof(CabeceraNormal), nameof(CabeceraSeleccion), nameof(CabeceraColocar), nameof(CabeceraBusqueda), nameof(BarraContextual), nameof(BarraSeleccion), nameof(BarraColocar), nameof(MuestraCabeceraNormal) }) OnPropertyChanged(n);
    }
    public bool VariosOrigenes => _origenes.Count > 1;
    public string TextoBotonColocarAqui => ColocandoCopia ? IdiomaInterfaz.T("Cajas.CopiarCaja") : IdiomaInterfaz.T("Cajas.MoverCaja");

    partial void OnSeleccionandoChanged(bool value) { OnPropertyChanged(nameof(NoSeleccionando)); CambioDeEstadoDeCabecera(); AplicarMarcas(); }
    partial void OnNumeroMarcadasChanged(int value) { OnPropertyChanged(nameof(TextoSeleccion)); OnPropertyChanged(nameof(HayMarcadas)); }
    partial void OnColocandoChanged(bool value) { CambioDeEstadoDeCabecera(); AplicarMarcas(); }
    partial void OnColocandoCopiaChanged(bool value) => OnPropertyChanged(nameof(TextoBotonColocarAqui));

    private static bool Misma(ISlotInfo a, ISlotInfo b) => (a, b) switch
    {
        (SlotInfoBox x, SlotInfoBox y) => x.Box == y.Box && x.Slot == y.Slot,
        (SlotInfoParty x, SlotInfoParty y) => x.Slot == y.Slot,
        _ => ReferenceEquals(a, b),
    };

    /// <summary>Una referencia nueva a la misma casilla: las operaciones del Core pueden modificar el <c>SlotInfoParty</c> que reciben (al borrar se reajusta), asi que nunca se les da el de la vista.</summary>
    private static ISlotInfo Fresca(ISlotInfo i) => i switch { SlotInfoParty p => new SlotInfoParty(p.Slot), _ => i };

    private void AplicarMarcas() => AplicarMarcas(TodasLasVisibles);

    /// <summary>Marcas de seleccion, origen y busqueda sobre unas casillas (las de la caja que se ve o las de la vecina que entra en el carrusel: asi las dos se dibujan igual).</summary>
    private void AplicarMarcas(IEnumerable<CasillaViewModel> casillas)
    {
        foreach (var c in casillas)
        {
            c.EnSeleccion = Seleccionando;
            c.Marcada = Seleccionando && c.Ref is { } r && _marcadas.Any(m => Misma(m.Info, r.Info));
            c.EsOrigen = Colocando && c.Ref is { } o && _origenes.Any(m => Misma(m.Info, o.Info));
            c.EsArrastrada = Arrastrando && c.Ref is { } a && _arrastreRefs.Any(m => Misma(m.Info, a.Info));
        }
        AplicarBusqueda(casillas);
    }

    private DateTime _ignorarToqueHasta;

    /// <summary>
    /// Pulsacion larga sobre una casilla: con Pokemon, empieza la seleccion multiple con esa casilla marcada; ya seleccionando, la marca o desmarca; vacia, abre su menu. El toque que llega al soltar el dedo
    /// se ignora un instante (si no, volveria a alternar la marca recien puesta).
    /// </summary>
    public void PulsacionLarga(CasillaViewModel casilla)
    {
        if (Colocando) return;
        _ignorarToqueHasta = DateTime.UtcNow.AddMilliseconds(600);
        if (Seleccionando) { AlternarMarca(casilla); return; }
        if (casilla.Ocupada) IniciarSeleccion(casilla);
        else AbrirMenuCasilla(casilla);
    }

    /// <summary>El menu de «Mas» de la seleccion: la edicion masiva sobre lo seleccionado (llega con la fase 9) y marcar o quitar la caja entera.</summary>
    [RelayCommand]
    public void AbrirMenuSeleccion()
    {
        Mostrar(new MenuHojaViewModel(IdiomaInterfaz.T("Cajas.Seleccion"), [new SeccionHoja(null,
        [
            new(IdiomaInterfaz.T("Cajas.EdicionMasiva"), IdiomaInterfaz.T("Cajas.AplicaCambiosTodosSeleccionados"), "Layers", _anfitrion is null || MotivoMasiva is not null || MarcadasOcupadas().Count == 0 ? null : EdicionMasivaDeLaSeleccion, Pronto: _anfitrion is null, Fase: "9", Motivo: _anfitrion is null ? null : MotivoMasiva ?? (MarcadasOcupadas().Count == 0 ? IdiomaInterfaz.T("Cajas.MarcaMenosPokemon") : null)),
            .. EntradasBancoDeLaSeleccion(),
            new(IdiomaInterfaz.T("Cajas.SeleccionarTodaCaja"), IdiomaInterfaz.T("Cajas.MarcaTodosPokemonCaja2"), "ListChecks", SeleccionarCaja),
            new(IdiomaInterfaz.T("Cajas.QuitarSeleccionCaja"), IdiomaInterfaz.T("Cajas.DesmarcaPokemonCajaVes"), "Square", QuitarSeleccionDeLaCaja),
            new(IdiomaInterfaz.T("Cajas.SalirSeleccion"), IdiomaInterfaz.T("Cajas.CambiaNada"), "X", CancelarSeleccion),
        ])], IdiomaInterfaz.F("Cajas.Seleccionados", NumeroMarcadas)));
    }

    private string? MotivoMasiva => _anfitrion?.MotivoEdicionMasiva;

    private void EdicionMasivaDeLaSeleccion() => _anfitrion?.AbrirEdicionMasiva(MarcadasOcupadas());

    // ---- Seleccion multiple ----
    /// <summary>Empieza la seleccion (pulsacion larga sobre una casilla) con esa casilla marcada.</summary>
    [RelayCommand]
    public void IniciarSeleccion(CasillaViewModel? primera = null)
    {
        if (Colocando) return;
        _marcadas.Clear();
        Seleccionando = true;
        if (primera is { Ocupada: true, Ref: { } r }) _marcadas.Add(r);
        ActualizarMarcadas();
    }

    private void ActualizarMarcadas()
    {
        NumeroMarcadas = _marcadas.Count;
        AplicarMarcas();
    }

    private void AlternarMarca(CasillaViewModel? c)
    {
        if (c?.Ref is not { } r || c.Vacia) return;
        var ya = _marcadas.FindIndex(m => Misma(m.Info, r.Info));
        if (ya >= 0) _marcadas.RemoveAt(ya); else _marcadas.Add(r);
        ActualizarMarcadas();
    }

    /// <summary>«Seleccionar caja»: marca todos los Pokemon de la caja o zona que se ve (sin quitar los que ya estaban de otras cajas).</summary>
    [RelayCommand]
    public void SeleccionarCaja()
    {
        if (!Seleccionando) { _marcadas.Clear(); Seleccionando = true; }
        foreach (var c in Casillas.Where(c => c.Ocupada && c.Ref is not null))
            if (!_marcadas.Any(m => Misma(m.Info, c.Ref!.Info))) _marcadas.Add(c.Ref!);
        ActualizarMarcadas();
    }

    [RelayCommand]
    public void QuitarSeleccionDeLaCaja()
    {
        foreach (var c in Casillas.Where(c => c.Ref is not null)) _marcadas.RemoveAll(m => Misma(m.Info, c.Ref!.Info));
        ActualizarMarcadas();
    }

    [RelayCommand]
    public void CancelarSeleccion()
    {
        _marcadas.Clear();
        Seleccionando = false;
        NumeroMarcadas = 0;
    }

    private IReadOnlyList<CasillaRef> MarcadasOcupadas() => [.. _marcadas.Where(m => !Sesion.Casillas.EstaVacia(m.Info))];

    [RelayCommand]
    public void BorrarSeleccion()
    {
        var lista = MarcadasOcupadas();
        if (lista.Count == 0) return;
        Mostrar(new ConfirmarHojaViewModel(IdiomaInterfaz.F("Cajas.BorrarPokemon2", lista.Count), [IdiomaInterfaz.F("Cajas.VanBorrarPokemonPartida", lista.Count), IdiomaInterfaz.T("Cajas.CasillasBloqueadasSaltanPuedes")], IdiomaInterfaz.T("Seleccion.Borrar"), () =>
        {
            var r = Sesion.Casillas.Borrar([.. lista.Select(x => Fresca(x.Info))]);
            if (r.Correcto) CancelarSeleccion();
            Resultado(r);
        }, destructiva: true));
    }

    [RelayCommand] public void MoverSeleccion() => IniciarColocacion(MarcadasOcupadas(), copia: false);
    [RelayCommand] public void CopiarSeleccion() => IniciarColocacion(MarcadasOcupadas(), copia: true);

    // ---- Colocar (mover, intercambiar, copiar) ----
    /// <summary>
    /// Empieza a elegir el destino: se navega por las cajas y se toca la casilla de destino (o, con varios Pokemon, «Mover a esta caja» los lleva a los huecos libres). Vaciar una casilla ocupada intercambia;
    /// copiar sobre una ocupada pide confirmacion. Es el arrastrar y soltar de PKHeX, adaptado al tacto y accesible con TalkBack.
    /// </summary>
    public void IniciarColocacion(IReadOnlyList<CasillaRef> origenes, bool copia)
    {
        if (origenes.Count == 0) return;
        _origenes = [.. origenes];
        ColocandoCopia = copia;
        Seleccionando = false;
        Colocando = true;
        var n = origenes.Count;
        var verbo = copia ? IdiomaInterfaz.T("Seleccion.Copiar") : IdiomaInterfaz.T("Seleccion.Mover");
        var nombre = n == 1 && Sesion.Casillas.Leer(origenes[0].Info) is { Species: > 0 } pk ? Sesion.Textos.Species[pk.Species] : IdiomaInterfaz.F("Cajas.Pokemon2", n);
        TextoColocar = n == 1 ? IdiomaInterfaz.F("Cajas.TocaDestino", verbo, nombre) : IdiomaInterfaz.F("Cajas.PokemonEligeCaja", verbo, n);
        AyudaColocar = n == 1 ? (copia ? IdiomaInterfaz.T("Cajas.DestinoOcupadoPideConfirmar") : IdiomaInterfaz.T("Cajas.DestinoOcupadoDosPokemon")) : IdiomaInterfaz.F("Cajas.PulsaPonerlosHuecosLibres", TextoBotonColocarAqui);
        OnPropertyChanged(nameof(VariosOrigenes));
        AplicarMarcas();
    }

    private Action<DestinoCasilla>? _colocarLibreria;

    /// <summary>
    /// «Elegir casilla…» de la Librería: la pestaña Pokémon pasa a modo colocar para UNA entrada del catálogo. Se toca una casilla VACÍA de una caja y se genera allí; una ocupada se rechaza sin tocar nada
    /// (Pokekeep nunca sobrescribe) y el modo sigue activo para elegir otra. Es el «arrastrar a una casilla» del escritorio, adaptado al tacto.
    /// </summary>
    public void IniciarColocacionLibreria(EntradaLibreria entrada, Action<DestinoCasilla> colocar)
    {
        _origenes = [];
        _colocarLibreria = colocar;
        ColocandoCopia = false;
        Seleccionando = false;
        Colocando = true;
        TextoColocar = IdiomaInterfaz.F("Cajas.ColocarTocaCasillaVacia", entrada.Nombre);
        AyudaColocar = IdiomaInterfaz.T("Cajas.SoloCasillasVaciasCajas");
        OnPropertyChanged(nameof(VariosOrigenes));
        AplicarMarcas();
    }

    private void ColocarLibreriaEn(CasillaViewModel? destino)
    {
        if (destino?.Ref is not { } d || _colocarLibreria is null) return;
        if (d.Info is not SlotInfoBox caja) { _anfitrion?.Informar(IdiomaInterfaz.T("Cajas.AnadirCajaSoloColoca"), "aviso", "CircleAlert"); return; }
        if (destino.Ocupada) { _anfitrion?.Informar(Textos_CasillaOcupada, "aviso", "CircleAlert"); return; }
        var colocar = _colocarLibreria;
        CancelarColocacion();
        colocar(new DestinoCasilla(false, caja.Box, caja.Slot));
    }

    /// <summary>Texto del núcleo (<c>Textos.CasillaOcupada</c>); una prueba compara las dos cadenas.</summary>
    public static string Textos_CasillaOcupada => IdiomaInterfaz.T("CajasViewModel.CasillaOcupada");

    [RelayCommand]
    public void CancelarColocacion()
    {
        _colocarLibreria = null;
        _origenes = [];
        Colocando = false;
        TextoColocar = string.Empty;
        OnPropertyChanged(nameof(VariosOrigenes));
    }

    private void ColocarEn(CasillaViewModel? destino)
    {
        if (destino?.Ref is not { } d || _origenes.Count != 1) return;
        var origen = _origenes[0];
        if (Misma(origen.Info, d.Info)) { CancelarColocacion(); return; }
        void Hacer()
        {
            var modo = ColocandoCopia ? ModoMover.Copiar : ModoMover.MoverOIntercambiar;
            var r = Sesion.Casillas.Mover(Fresca(origen.Info), Fresca(d.Info), modo);
            if (r.Correcto) CancelarColocacion();
            Resultado(r);
        }
        if (ColocandoCopia && destino.Ocupada)
            Mostrar(new ConfirmarHojaViewModel(IdiomaInterfaz.T("Cajas.SobrescribirCasilla"), [IdiomaInterfaz.F("Cajas.TienePokemon", d.Descripcion), IdiomaInterfaz.T("Cajas.SiguesSustituyeCopiaPuedes")], IdiomaInterfaz.T("Cajas.Sobrescribir"), Hacer, destructiva: true));
        else Hacer();
    }

    /// <summary>Con varios Pokemon: los pone en los huecos libres de la caja actual (y siguientes si no caben).</summary>
    [RelayCommand]
    public void ColocarEnEstaCaja()
    {
        if (!Colocando || _origenes.Count == 0) return;
        var r = Sesion.Casillas.MoverAHuecos([.. _origenes.Select(o => Fresca(o.Info))], Caja, ColocandoCopia);
        if (r.Correcto) { CancelarColocacion(); CancelarSeleccion(); }
        Resultado(r);
    }
}
