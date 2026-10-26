// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Herramientas.Equipo;
using Pokekeep.Mobile.Ui.Modelo;
using Pokekeep.Mobile.Ui.ViewModels;

namespace Pokekeep.Mobile.Ui.Herramientas.Banco;

/// <summary>Un Pokémon del plan de «Sacar a la partida» ya listo para pintar: a dónde va, el veredicto del Core y, si es ilegal, el botón «Legalizar».</summary>
public sealed partial class ItemSacarVm : ObservableObject
{
    private readonly BancoViewModel _padre;

    public ItemSacarVm(ItemSacar item, BancoViewModel padre)
    {
        _item = item; _padre = padre;
        LegalizarCommand = new AsyncRelayCommand(() => _padre.LegalizarAsync(this), () => PuedeLegalizar);
    }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(Nombre), nameof(Destino), nameof(Colocable), nameof(TextoVeredicto), nameof(ClaseVeredicto), nameof(GlifoVeredicto), nameof(Informe), nameof(HayInforme), nameof(NecesitaLegalizar), nameof(PuedeLegalizar), nameof(Accesible), nameof(HayAvisos), nameof(Avisos), nameof(VeredictoOk), nameof(VeredictoError), nameof(VeredictoAviso), nameof(VeredictoNeutro), nameof(Icono))]
    private ItemSacar _item;

    public IAsyncRelayCommand LegalizarCommand { get; }

    public string Nombre => Item.Nombre;
    public bool Colocable => Item.Colocable;
    public string Destino => Item.Colocable ? TextosBanco.VaA(Item.Casilla!.Descripcion) : TextosBanco.NoSeColocaMotivo(Item.Motivo ?? string.Empty);
    public bool NecesitaLegalizar => Item.NecesitaLegalizar;
    public string Informe => Item.InformeLegal;
    public bool HayInforme => Item.Colocable && Item.Veredicto != EstadoLegal.Legal && Item.InformeLegal.Length > 0;
    public string Avisos => string.Join("\n", Item.Avisos.Take(3));
    public bool HayAvisos => Item.Avisos.Count > 0;
    public bool PuedeLegalizar => Item.NecesitaLegalizar && _padre.MotivoLegalizar is null && !_padre.Ocupado;
    public IconoPokemon Icono => _padre.IconoDe(Item);

    public string TextoVeredicto => !Item.Colocable ? TextosBanco.NoSeColoca
        : Item.Legalizado && Item.Veredicto == EstadoLegal.Legal ? TextosBanco.LegalizadoYLegal
        : Item.Legalizado ? TextosBanco.SigueIlegal
        : LegalidadUi.Texto(Item.Veredicto);
    public string ClaseVeredicto => !Item.Colocable ? "aviso" : LegalidadUi.ClaseInsignia(Item.Veredicto);
    public string GlifoVeredicto => !Item.Colocable ? "CircleAlert" : LegalidadUi.Glifo(Item.Veredicto);
    public string Accesible => TextosBanco.ItemSacarAccesible(Nombre, Destino, TextoVeredicto);
    public bool VeredictoOk => Item.Colocable && ClaseVeredicto == "ok";
    public bool VeredictoError => Item.Colocable && ClaseVeredicto == "error";
    public bool VeredictoAviso => !Item.Colocable || ClaseVeredicto == "aviso";
    public bool VeredictoNeutro => Item.Colocable && ClaseVeredicto.Length == 0;

    internal void Reintentar() => LegalizarCommand.NotifyCanExecuteChanged();
}

public sealed partial class BancoViewModel
{
    // ---------------------------------------------------------------------------------------------------------------- sacar a la partida (M1)
    public ObservableCollection<ItemSacarVm> ItemsSacar { get; } = [];
    [ObservableProperty, NotifyPropertyChangedFor(nameof(NoSacando))] private bool _sacando;
    [ObservableProperty] private string _resumenSacar = string.Empty;
    [ObservableProperty] private string? _bloqueoSacar;
    [ObservableProperty] private bool _moverAlSacar;
    public bool NoSacando => !Sacando;
    public bool HayBloqueoSacar => !string.IsNullOrEmpty(BloqueoSacar);
    partial void OnBloqueoSacarChanged(string? value) => OnPropertyChanged(nameof(HayBloqueoSacar));
    public PlanSacar? PlanActual { get; private set; }
    public string TextoMoverAlSacar => TextosBanco.MoverAlSacar;
    public string AyudaMoverAlSacar => TextosBanco.MoverAlSacarAyuda;

    /// <summary>Por qué «Legalizar» no se puede usar (null si se puede): sin motor en esta ventana o el motor no disponible.</summary>
    public string? MotivoLegalizar => Contexto.Acciones is not { } a ? TextosBanco.LegalizarNoDisponible : a.NoDisponible;

    public IconoPokemon IconoDe(ItemSacar item)
        => item.Pokemon is { } pk && Sesion is { } s ? IconoPokemon.De(DatosSprite.De(pk), string.Empty, ModoIconos, (int)Design.Sprites.VersionDe(s.Guardado)) : new IconoPokemon(string.Empty, item.Entrada.Especie, item.Entrada.Forma, 0, 0, item.Entrada.Variocolor, item.Entrada.Huevo, item.Entrada.Contexto, SpritesBanco.Modo(ModoIconos, item.Entrada.Especie, item.Entrada.Forma, item.Entrada.Contexto, item.Entrada.Variocolor), 0);

    public bool PuedeLegalizarAlgunos => ItemsSacar.Any(i => i.PuedeLegalizar);
    public int IlegalesEnElPlan => ItemsSacar.Count(i => i.NecesitaLegalizar);
    public bool HayIlegalesEnElPlan => IlegalesEnElPlan > 0;
    public string TextoLegalizarTodos => TextosBanco.LegalizarTodos(IlegalesEnElPlan);
    public bool PuedeColocar => Sacando && PlanActual is { Aplicable: true } && !Ocupado;

    /// <summary>
    /// «Sacar a la partida»: lee los Pokémon elegidos, los convierte al formato de la partida abierta, mira su legalidad con el Core y enseña el plan (a qué casilla vacía va cada uno, cuáles son ilegales y cuáles no se
    /// pueden colocar y por qué). No escribe nada hasta «Colocar en la partida».
    /// </summary>
    [RelayCommand(CanExecute = nameof(PuedeSacar))]
    public void Sacar()
    {
        if (Sesion is not { } sesion) { Avisar(TextosBanco.SacarSinPartida, false); return; }
        var e = Elegidas;
        if (e.Count == 0) { Avisar(TextosBanco.ElegirAntes, false); return; }
        var leidos = new List<(EntradaBanco, PKM)>(); var fallos = new List<string>();
        foreach (var x in e)
        {
            var l = Banco.Leer(x.Id);
            if (l.Pokemon is { } pk) leidos.Add((x, pk)); else fallos.Add(l.Error ?? TextosBanco.ArchivoIlegible(x.Archivo));
        }
        if (leidos.Count == 0) { Avisar(string.Join(" ", fallos), false); return; }
        PrepararPlan(SacadorBanco.Planificar(sesion, leidos, Contexto.CajaActual?.Invoke() ?? 0, Textos));
        if (fallos.Count > 0) Estado = string.Join(" ", fallos);
    }

    private void PrepararPlan(PlanSacar plan)
    {
        PlanActual = plan;
        ItemsSacar.Clear();
        foreach (var i in plan.Items) ItemsSacar.Add(new ItemSacarVm(i, this));
        BloqueoSacar = plan.Bloqueo;
        ResumenSacar = TextosBanco.ResumenPlan(plan.Colocables, plan.Ilegales, plan.NoColocados, plan.CasillasLibres);
        Sacando = true;
        ActualizarPlan();
    }

    private void ActualizarPlan()
    {
        foreach (var n in new[] { nameof(PuedeLegalizarAlgunos), nameof(IlegalesEnElPlan), nameof(HayIlegalesEnElPlan), nameof(TextoLegalizarTodos), nameof(PuedeColocar) }) OnPropertyChanged(n);
        if (PlanActual is { } plan0) PlanActual = plan0 with { Items = [.. ItemsSacar.Select(i => i.Item)] };   // el plan sigue a lo legalizado
        foreach (var i in ItemsSacar) i.Reintentar();
        ColocarCommand.NotifyCanExecuteChanged(); LegalizarTodosCommand.NotifyCanExecuteChanged();
        if (PlanActual is { } p) ResumenSacar = TextosBanco.ResumenPlan(p.Colocables, p.Ilegales, p.NoColocados, p.CasillasLibres);
    }

    [RelayCommand]
    public void CancelarSacar() => SalirDeSacar();

    private void SalirDeSacar()
    {
        if (!Sacando) return;
        _cancelarLegalizar?.Cancel();
        Sacando = false; PlanActual = null; ItemsSacar.Clear(); BloqueoSacar = null;
    }

    // ---------------------------------------------------------------------------------------------------------------- legalizar antes de colocar
    private CancellationTokenSource? _cancelarLegalizar;

    [ObservableProperty] private string _progresoLegalizar = string.Empty;

    /// <summary>
    /// «Legalizar» un Pokémon del plan con el motor de Pokekeep (el MISMO que «Legalizar automáticamente» del editor): trabaja sobre la copia ya convertida al formato de la partida, que es lo que se va a colocar. Nada se
    /// escribe en la partida todavía. Si el motor no lo consigue, el Pokémon sigue ilegal y se dice por qué; se puede colocar igualmente porque el usuario lo ve marcado.
    /// </summary>
    public async Task LegalizarAsync(ItemSacarVm fila)
    {
        if (Sesion is not { } sesion || Contexto.Acciones is not { } acciones) { Avisar(TextosBanco.LegalizarNoDisponible, false); return; }
        if (Ocupado) return;
        if (fila.Item.Pokemon is not { } pk || !fila.Item.NecesitaLegalizar) return;
        if (acciones.NoDisponible is { } motivo) { Avisar(motivo, false); return; }
        Ocupado = true; _cancelarLegalizar = new CancellationTokenSource();
        ActualizarPlan();
        try
        {
            var progreso = new Progress<ProgresoAccion>(p => { ProgresoLegalizar = p.Texto; TextoAvance = p.Texto; });
            var r = await acciones.EjecutarEnBorradorAsync(AccionPokemon.Legalizar, sesion, pk, progreso, _cancelarLegalizar.Token).ConfigureAwait(true);
            if (!Sacando) return;   // se cerró el plan mientras trabajaba el motor: el resultado no se usa
            fila.Item = SacadorBanco.Reevaluar(fila.Item, r.Aplicado);
            Avisar(r.Aplicado ? TextosBanco.Legalizado(fila.Nombre, r.Titular) : TextosBanco.NoSeHaLegalizado(fila.Nombre, r.Titular, r.Detalle ?? string.Empty), r.Aplicado && fila.Item.Veredicto == EstadoLegal.Legal);
        }
        catch (OperationCanceledException) { Estado = TextosBanco.LegalizacionCancelada; }
        finally { Ocupado = false; ProgresoLegalizar = string.Empty; _cancelarLegalizar?.Dispose(); _cancelarLegalizar = null; if (Sacando) ActualizarPlan(); Notificar(nameof(PuedeElegirCarpeta)); }
    }

    /// <summary>Legaliza uno detrás de otro los ilegales del plan (el motor admite una operación a la vez).</summary>
    [RelayCommand(CanExecute = nameof(PuedeLegalizarAlgunos))]
    public async Task LegalizarTodosAsync()
    {
        foreach (var fila in ItemsSacar.Where(i => i.NecesitaLegalizar).ToList())
        {
            if (!Sacando) break;
            await LegalizarAsync(fila).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    public void CancelarLegalizacion() => _cancelarLegalizar?.Cancel();

    // ---------------------------------------------------------------------------------------------------------------- colocar
    /// <summary>
    /// «Colocar en la partida»: escribe el plan en casillas vacías (UNA entrada del historial, un Deshacer). Si queda algún ilegal, antes pide una confirmación que lo dice con todas las letras; nada se fuerza. Con «Mover»
    /// activado, los que se han colocado pasan a la papelera del banco.
    /// </summary>
    [RelayCommand(CanExecute = nameof(PuedeColocar))]
    public void Colocar()
    {
        if (Sesion is null || PlanActual is not { Aplicable: true } plan) return;
        if (plan.Ilegales > 0)
        {
            Contexto.AbrirHoja(new ConfirmarHojaViewModel(TextosBanco.ColocarIlegalesTitulo, [TextosBanco.ColocarIlegalesTexto(plan.Ilegales), TextosBanco.ColocarIlegalesAyuda], TextosBanco.ColocarIgualmente, ColocarConfirmado, destructiva: true));
            return;
        }
        ColocarConfirmado();
    }

    /// <summary>Escribe el plan sin preguntar más (las confirmaciones ya se hicieron).</summary>
    public void ColocarConfirmado()
    {
        if (Sesion is not { } sesion || PlanActual is not { Aplicable: true } plan) return;
        var r = SacadorBanco.Aplicar(sesion, plan, TextosBanco.HistorialSacar(plan.Colocables));
        if (r.Colocados.Count == 0)
        {
            Avisar(TextosBanco.NadaColocado(r.Omitidos.Select(o => o.Motivo).Distinct().FirstOrDefault() ?? string.Empty), false);
            return;
        }
        var movidos = 0;
        if (MoverAlSacar && Banco.AlaPapelera(r.Colocados.Select(c => c.Id)) is null) movidos = r.Colocados.Count;
        var omitidos = r.Omitidos.Count;
        SalirDeSacar(); _marcadas.Clear(); Refrescar();
        Avisar(TextosBanco.Colocados(r.Colocados.Count, omitidos, movidos), omitidos == 0);
    }
}
