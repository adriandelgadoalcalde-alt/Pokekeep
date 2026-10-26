// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Ajustes.Pkhex;
using Pokekeep.Mobile.Ui.Cajas;
using Pokekeep.Mobile.Ui.ViewModels;
using Pokekeep.Sprites;

namespace Pokekeep.Mobile.Ui.Herramientas.Equipo;

/// <summary>
/// El icono de caja de un Pokémon con su texto opcional (X45: nombre o movimiento). Misma medida y alineación en TODAS las filas de la pantalla (<see cref="Ancho"/> × <see cref="Alto"/>); el modo y la versión de sprites son los de la partida
/// abierta, como en las cajas. Sin recurso de imagen, el propio sprite dibuja la silueta con el número de especie (el marcador común de la app).
/// </summary>
public sealed record IconoPokemon(string Texto, int Especie, int Forma, uint ArgumentoForma, int Genero, bool Brillante, bool Huevo, int Contexto, ModoSprites Modo, int VersionJuego)
{
    public const double Ancho = 48, Alto = 44;   // X53: lado util de ~38 dp (86 % del alto) para que se reconozcan junto al nombre
    /// <summary>Hueco de las tiras de «Qué equipo mirar» (equipo de la partida y plantillas): el mismo para las dos, de 48 dp como minimo; seis caben en una linea en el compacto.</summary>
    public const double AnchoTira = 48, AltoTira = 48;
    public bool HayTexto => Texto.Length > 0;
    public static IconoPokemon De(DatosSprite? d, string texto, ModoSprites modo, int version)
        => d is null ? new(texto, 0, 0, 0, 0, false, false, 0, modo, version) : new(texto, d.Especie, d.Forma, d.ArgumentoForma, d.Genero, d.Brillante, d.Huevo, d.Contexto, modo, version);
}

/// <summary>Un tipo suelto para pintar como etiqueta rectangular (puntos débiles, tipos sin cubrir), con los Pokémon del equipo a los que afecta si los hay.</summary>
public sealed record ChipTipo(string Texto, IReadOnlyList<IconoPokemon>? Iconos = null, int TipoIndice = -1, string NombreTipo = "", string Detalle = "")
{
    /// <summary>El chip lleva el tipo como etiqueta de color (índice del Core + nombre); si no, solo el texto.</summary>
    public bool HayTipo => TipoIndice >= 0 && NombreTipo.Length > 0;
    public bool HayDetalle => Detalle.Length > 0;
    public bool HayIconos => Iconos is { Count: > 0 };
}

/// <summary>Una fuente que se puede mirar: el equipo de la partida o una plantilla guardada. Pulsarla la elige.</summary>
public sealed partial class FuenteItem : ObservableObject
{
    public FuenteItem(string? idPlantilla, string titulo, string datos, Action<FuenteItem> elegir, IReadOnlyList<IconoPokemon>? iconos = null)
    {
        IdPlantilla = idPlantilla; Titulo = titulo; Datos = datos; Iconos = iconos ?? [];
        Elegir = new RelayCommand(() => elegir(this));
    }

    /// <summary>null = el equipo de la partida.</summary>
    public string? IdPlantilla { get; }
    public string Titulo { get; }
    public string Datos { get; }
    /// <summary>X45: los Pokémon de este equipo o plantilla, cada uno con su icono.</summary>
    public IReadOnlyList<IconoPokemon> Iconos { get; }
    public bool HayIconos => Iconos.Count > 0;
    public ICommand Elegir { get; }
    [ObservableProperty] private bool _seleccionada;
    public string Accesible => Titulo + ". " + Datos + (Seleccionada ? ". " + TextosEquipo.Viendo : string.Empty);
    partial void OnSeleccionadaChanged(bool value) => OnPropertyChanged(nameof(Accesible));
}

public sealed record FilaMiembroVm(string Titulo, string Habilidad, string Movimientos, IconoPokemon? Icono = null, string Nombre = "", IReadOnlyList<Modelo.TipoVm>? Tipos = null)
{
    /// <summary>Nombre del Pokémon sin los tipos (que salen como etiquetas de color); <see cref="Titulo"/> sigue siendo la línea completa para el lector de pantalla.</summary>
    public string NombreVisible => Nombre.Length > 0 ? Nombre : Titulo;
    public bool HayTipos => Tipos is { Count: > 0 };
    public bool HayHabilidad => Habilidad.Length > 0;
}

/// <summary>Los Pokémon que reciben un tipo atacante con un mismo multiplicador (×4, ×2, ×½, ×0), cada uno con su icono.</summary>
public sealed record GrupoDefensaVm(string Etiqueta, IReadOnlyList<IconoPokemon> Miembros);

public sealed record FilaDefensaVm(string Tipo, bool EsPuntoDebil, string Debiles, string Resisten, string Inmunes, string Detalle, string Accesible, IReadOnlyList<GrupoDefensaVm>? Grupos = null, int TipoIndice = -1)
{
    public bool HayGrupos => Grupos is { Count: > 0 };
    public bool HayDebiles => Debiles.Length > 0;
    public bool HayResisten => Resisten.Length > 0;
    public bool HayInmunes => Inmunes.Length > 0;
    public bool TodoNeutro => !HayDebiles && !HayResisten && !HayInmunes;
    public bool HayDetalle => Detalle.Length > 0;
}

public sealed record FilaOfensivaVm(string Tipo, bool Cubierto, string Estado, string Movimientos, string Accesible, IReadOnlyList<IconoPokemon>? Golpes = null, int TipoIndice = -1)
{
    public bool HayGolpes => Cubierto && Golpes is { Count: > 0 };
    public bool SoloTexto => !HayGolpes;
    public bool NoCubierto => !Cubierto;
}

/// <summary>
/// «Cobertura y plantillas» (X37, decisión del propietario del 15-oct-2026): defensa y ofensiva por tipos del equipo de la partida o de una plantilla (<see cref="ModeloCobertura"/>), y plantillas de equipo con nombre
/// (guardar el equipo, ver su cobertura, renombrar, borrar y «Aplicar a la partida» con <see cref="AplicadorPlantilla"/>). Calcula en local, sin red. Lo único que escribe en la partida es «Aplicar» (con confirmación y un Deshacer);
/// las plantillas viven en la carpeta privada de la app.
/// </summary>
public sealed partial class CoberturaYPlantillasViewModel : HerramientaViewModel
{
    private readonly IAlmacenPlantillas? _almacen;
    private readonly Dictionary<string, PlantillaEquipo> _plantillas = [];

    public CoberturaYPlantillasViewModel(ContextoHerramienta contexto, Action volver, IAlmacenPlantillas? almacen) : base(contexto, volver)
    {
        _almacen = almacen;
        TextosNucleo.Preparar();
        Refrescar();
        Elegir(Fuentes[0]);
    }

    public override string Titulo => TextosEquipo.Titulo;
    public string Subtitulo => TextosEquipo.Subtitulo;
    public GameStrings Textos => Sesion?.Textos ?? ProgramaPkhex.Textos;

    public bool HayPartida => Sesion is not null;
    public bool TieneEquipoEnLaPartida => Sesion?.Zonas.TieneEquipo == true;
    public bool PuedeGuardarPlantillas => _almacen is not null;

    // ---------------------------------------------------------------------------------------------------------------- fuentes
    public ObservableCollection<FuenteItem> Fuentes { get; } = [];
    public bool HayPlantillas => _plantillas.Count > 0;
    public bool NoHayPlantillas => !HayPlantillas;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(EsEquipoElegido), nameof(EsPlantillaElegida))] private FuenteItem? _elegida;
    public bool EsEquipoElegido => Elegida is { IdPlantilla: null };
    public bool EsPlantillaElegida => Elegida is { IdPlantilla: not null };
    private PlantillaEquipo? PlantillaElegida => Elegida?.IdPlantilla is { } id && _plantillas.TryGetValue(id, out var p) ? p : null;

    /// <summary>Vuelve a leer las plantillas del almacén y reconstruye la lista de fuentes (la primera es siempre el equipo de la partida).</summary>
    private void Refrescar()
    {
        _plantillas.Clear();
        if (_almacen is not null)
            foreach (var p in _almacen.Listar(out _)) _plantillas[p.Id] = p;
        var anteriorId = Elegida?.IdPlantilla;
        Fuentes.Clear();
        var equipo = PokemonDelEquipo();
        Fuentes.Add(new FuenteItem(null, TextosEquipo.EquipoDeLaPartida, !HayPartida ? TextosEquipo.SinPartida : !TieneEquipoEnLaPartida ? TextosEquipo.PartidaSinEquipo : TextosEquipo.DatosFuenteEquipo(equipo.Count, Sesion!.JuegoCorto), Elegir, IconosDe(equipo, true)));
        foreach (var p in _plantillas.Values.OrderByDescending(p => p.Creada).ThenBy(p => p.Nombre, StringComparer.CurrentCultureIgnoreCase))
            Fuentes.Add(new FuenteItem(p.Id, p.Nombre, TextosEquipo.DatosPlantilla(p.Cantidad, p.Generacion, p.Creada), Elegir, IconosDe(p.Pokemon(out _), false)));
        OnPropertyChanged(nameof(HayPlantillas)); OnPropertyChanged(nameof(NoHayPlantillas));
        var mantener = Fuentes.FirstOrDefault(f => f.IdPlantilla == anteriorId) ?? Fuentes[0];
        Elegida = null;
        Elegir(mantener);
    }

    // ---------------------------------------------------------------------------------------------------------------- iconos (X45)
    private ModoSprites ModoIconos => Sesion?.ModoSprites ?? Design.Sprites.ModoVigente;
    // Una plantilla no es de ningun juego: sus iconos son los de PKHeX (version 0), tambien en la cobertura que se calcula de ella.
    private int VersionIconos => Sesion is null || EsPlantillaElegida ? 0 : (int)Design.Sprites.VersionDe(Sesion.Guardado);
    private IconoPokemon Icono(DatosSprite? d, string texto = "") => IconoPokemon.De(d, texto, ModoIconos, VersionIconos);
    private IReadOnlyList<IconoPokemon> IconosDe(IEnumerable<PKM> pokemon, bool delJuego = true)
        => [.. pokemon.Select(p => IconoPokemon.De(DatosSprite.De(p), string.Empty, ModoIconos, delJuego && Sesion is not null ? (int)Design.Sprites.VersionDe(Sesion.Guardado) : 0))];

    private IReadOnlyList<PKM> PokemonDelEquipo()
    {
        if (Sesion is not { } sesion || !TieneEquipoEnLaPartida) return [];
        var sav = sesion.Guardado;
        var l = new List<PKM>();
        for (var i = 0; i < Math.Min(6, sav.PartyCount); i++) { var pk = sav.GetPartySlotAtIndex(i); if (pk.Species != 0) l.Add(pk); }
        return l;
    }

    /// <summary>Elige qué mirar: calcula la cobertura de esa fuente y deja el nombre listo para renombrar.</summary>
    public void Elegir(FuenteItem fuente)
    {
        foreach (var f in Fuentes) f.Seleccionada = ReferenceEquals(f, fuente);
        Elegida = fuente;
        NombrePlantilla = PlantillaElegida?.Nombre ?? string.Empty;
        Recalcular();
    }

    // ---------------------------------------------------------------------------------------------------------------- cobertura
    public ResultadoCobertura? Resultado { get; private set; }
    [ObservableProperty] private string _encabezado = string.Empty;
    [ObservableProperty] private string _mensajeVacio = string.Empty;
    public bool HayResultado => Resultado is not null;
    public bool HayMensajeVacio => MensajeVacio.Length > 0;
    partial void OnMensajeVacioChanged(string value) => OnPropertyChanged(nameof(HayMensajeVacio));
    public ObservableCollection<ChipTipo> PuntosDebiles { get; } = [];
    public ObservableCollection<ChipTipo> TiposSinCubrir { get; } = [];
    public ObservableCollection<FilaMiembroVm> Miembros { get; } = [];
    public ObservableCollection<FilaDefensaVm> Defensa { get; } = [];
    public ObservableCollection<FilaOfensivaVm> Ofensiva { get; } = [];
    public bool NingunPuntoDebil => HayResultado && PuntosDebiles.Count == 0;
    public bool NingunSinCubrir => HayResultado && TiposSinCubrir.Count == 0 && Resultado!.HayMovimientosConDano;
    public bool HayPuntosDebiles => PuntosDebiles.Count > 0;
    public bool HaySinCubrir => TiposSinCubrir.Count > 0;
    public bool SinMovimientosConDano => HayResultado && !Resultado!.HayMovimientosConDano;
    public string Nota => TextosEquipo.NotaAlcance + (Resultado is { TieneHabilidades: false } ? " " + TextosEquipo.NotaSinHabilidades : string.Empty);

    private void Recalcular()
    {
        PuntosDebiles.Clear(); TiposSinCubrir.Clear(); Miembros.Clear(); Defensa.Clear(); Ofensiva.Clear();
        Resultado = null; Encabezado = string.Empty; MensajeVacio = string.Empty;
        IReadOnlyList<PKM> pokemon; int generacion; string nombre;
        if (Elegida is null) { Notificar(); return; }
        if (PlantillaElegida is { } plantilla)
        {
            pokemon = plantilla.Pokemon(out var danados); generacion = plantilla.Generacion; nombre = plantilla.Nombre;
            if (danados > 0) Estado = TextosEquipo.PlantillaIlegible(plantilla.Nombre);
            if (pokemon.Count == 0) { MensajeVacio = TextosEquipo.PlantillaIlegible(plantilla.Nombre); Notificar(); return; }
        }
        else
        {
            nombre = TextosEquipo.EquipoDeLaPartida;
            if (Sesion is null) { MensajeVacio = TextosEquipo.SinPartida; Notificar(); return; }
            if (!TieneEquipoEnLaPartida) { MensajeVacio = TextosEquipo.PartidaSinEquipo; Notificar(); return; }
            pokemon = PokemonDelEquipo(); generacion = Sesion.Guardado.Generation;
            if (pokemon.Count == 0) { MensajeVacio = TextosEquipo.EquipoVacio; Notificar(); return; }
        }
        var r = ModeloCobertura.Calcular(generacion, pokemon, Textos);
        Resultado = r;
        Encabezado = TextosEquipo.EncabezadoCobertura(nombre, generacion);
        foreach (var f in r.PuntosDebiles) PuntosDebiles.Add(new ChipTipo(TextosEquipo.PuntoDebilLinea(f.NombreTipo, f.Debiles), [.. f.Miembros.Where(m => m.Multiplicador > 1).Select(m => Icono(m.Sprite))], f.Tipo, f.NombreTipo, TextosEquipo.Debiles(f.Debiles)));
        foreach (var f in r.TiposSinCubrir) TiposSinCubrir.Add(new ChipTipo(f.NombreTipo, null, f.Tipo, f.NombreTipo));
        foreach (var m in r.Miembros) Miembros.Add(FilaDeMiembro(m, r.Generacion));
        foreach (var f in r.Defensa) Defensa.Add(FilaDeDefensa(f));
        foreach (var f in r.Ofensiva) Ofensiva.Add(new FilaOfensivaVm(f.NombreTipo, f.Cubierto, f.Cubierto ? TextosEquipo.Cubierto : TextosEquipo.SinCubrir,
            f.Cubierto ? string.Join(", ", f.Movimientos) : TextosEquipo.SinMovimientosCubren, $"{f.NombreTipo}: {(f.Cubierto ? TextosEquipo.Cubierto : TextosEquipo.SinCubrir)}. {(f.Cubierto ? string.Join(", ", f.Movimientos) : string.Empty)}".TrimEnd(),
            [.. (f.Golpes ?? []).Select(g => Icono(g.Sprite, g.Movimiento))], f.Tipo));
        Notificar();
    }

    private void Notificar()
    {
        foreach (var n in new[] { nameof(HayResultado), nameof(NingunPuntoDebil), nameof(NingunSinCubrir), nameof(HayPuntosDebiles), nameof(HaySinCubrir), nameof(SinMovimientosConDano), nameof(Nota) }) OnPropertyChanged(n);
        OnPropertyChanged(nameof(PuedeAplicar)); OnPropertyChanged(nameof(PuedeGuardarEquipo));
    }

    private FilaMiembroVm FilaDeMiembro(MiembroCobertura m, int generacion)
    {
        var tipos = string.Join(" / ", m.Tipos.Select(NombreTipo));
        var movs = m.Movimientos.Count == 0 ? TextosEquipo.Sin : string.Join(", ", m.Movimientos.Select(x => x.HaceDano ? TextosEquipo.MovimientoConTipo(x.Nombre, NombreTipo(x.Tipo)) : TextosEquipo.MovimientoSinDano(x.Nombre)));
        return new FilaMiembroVm(TextosEquipo.MiembroLinea(m.Nombre, tipos), generacion >= 3 && m.NombreHabilidad.Length > 0 ? TextosEquipo.MiembroHabilidad(m.NombreHabilidad) : string.Empty, TextosEquipo.MiembroMovimientos(movs), Icono(m.Sprite), m.Nombre, [.. m.Tipos.Select(t => new Modelo.TipoVm(t, NombreTipo(t)))]);
    }

    private string NombreTipo(int t) => t >= 0 && t < Textos.Types.Count ? Textos.Types[t] : "#" + t;

    private FilaDefensaVm FilaDeDefensa(FilaDefensa f)
    {
        // Agrupa por multiplicador, del peor al mejor, sin los neutros (×1): «×4: A, B · ×2: C · ×½: D · ×0: E».
        var grupos = f.Miembros.Where(m => m.Multiplicador != 1).GroupBy(m => m.Multiplicador).OrderByDescending(g => g.Key)
            .Select(g => TextosEquipo.GrupoMultiplicador(TextosEquipo.Multiplicador(g.Key), string.Join(", ", g.Select(m => m.PorHabilidad ? $"{m.Nombre} ({TextosEquipo.PorHabilidad})" : m.Nombre))));
        return new FilaDefensaVm(f.NombreTipo, f.EsPuntoDebil, f.Debiles > 0 ? TextosEquipo.Debiles(f.Debiles) : string.Empty, f.Resisten > 0 ? TextosEquipo.Resisten(f.Resisten) : string.Empty,
            f.Inmunes > 0 ? TextosEquipo.Inmunes(f.Inmunes) : string.Empty, string.Join(" · ", grupos), TextosEquipo.DefensaAccesible(f.NombreTipo, f.Debiles, f.Resisten, f.Inmunes),
            [.. f.Miembros.Where(m => m.Multiplicador != 1).GroupBy(m => m.Multiplicador).OrderByDescending(g => g.Key)
                .Select(g => new GrupoDefensaVm(TextosEquipo.Multiplicador(g.Key), [.. g.Select(m => Icono(m.Sprite, m.PorHabilidad ? $"{m.Nombre} ({TextosEquipo.PorHabilidad})" : m.Nombre))]))], f.Tipo);
    }

    // ---------------------------------------------------------------------------------------------------------------- plantillas
    [ObservableProperty] private string _nombrePlantilla = string.Empty;
    public bool PuedeGuardarEquipo => PuedeGuardarPlantillas && PokemonDelEquipo().Count > 0;
    public bool PuedeAplicar => PuedeGuardarPlantillas && PlantillaElegida is not null && Sesion is not null;

    private string? ValidarNombre(string nombre, string? idActual)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return TextosEquipo.NombreVacio;
        if (_plantillas.Values.Any(p => p.Id != idActual && string.Equals(p.Nombre.Trim(), nombre.Trim(), StringComparison.CurrentCultureIgnoreCase))) return TextosEquipo.NombreRepetido;
        return null;
    }

    private void Avisar(string texto, bool ok)
    {
        Estado = texto;
        Contexto.Informar(texto, ok ? "ok" : "error", ok ? "CircleCheck" : "TriangleAlert");
    }

    /// <summary>Guarda el equipo de la partida como plantilla con el nombre escrito.</summary>
    [RelayCommand]
    public void GuardarComoPlantilla()
    {
        if (_almacen is null) { Avisar(TextosEquipo.PlantillasNoDisponibles, false); return; }
        var equipo = PokemonDelEquipo();
        if (Sesion is null || equipo.Count == 0) { Avisar(TextosEquipo.SinEquipoQueGuardar, false); return; }
        if (ValidarNombre(NombrePlantilla, null) is { } motivo) { Avisar(motivo, false); return; }
        var p = PlantillaEquipo.Nueva(NombrePlantilla, equipo, Sesion.Guardado.Generation, Sesion.Juego);
        if (_almacen.Guardar(p) is { } fallo) { Avisar(TextosEquipo.FalloAlGuardar(fallo), false); return; }
        Refrescar();
        Elegir(Fuentes.First(f => f.IdPlantilla == p.Id));
        Avisar(TextosEquipo.PlantillaGuardada(p.Nombre), true);
    }

    [RelayCommand]
    public void Renombrar()
    {
        if (PlantillaElegida is not { } p || _almacen is null) { Avisar(TextosEquipo.SinPlantillaSeleccionada, false); return; }
        if (ValidarNombre(NombrePlantilla, p.Id) is { } motivo) { Avisar(motivo, false); return; }
        var nueva = p with { Nombre = NombrePlantilla.Trim() };
        if (_almacen.Guardar(nueva) is { } fallo) { Avisar(TextosEquipo.FalloAlGuardar(fallo), false); return; }
        Refrescar();
        Elegir(Fuentes.First(f => f.IdPlantilla == p.Id));
        Avisar(TextosEquipo.PlantillaRenombrada(nueva.Nombre), true);
    }

    [RelayCommand]
    public void Borrar()
    {
        if (PlantillaElegida is not { } p || _almacen is null) { Avisar(TextosEquipo.SinPlantillaSeleccionada, false); return; }
        Contexto.AbrirHoja(new ConfirmarHojaViewModel(TextosEquipo.BorrarTitulo, [TextosEquipo.BorrarTexto(p.Nombre)], TextosEquipo.BorrarConfirmar, () => BorrarConfirmado(p), destructiva: true));
    }

    /// <summary>Borra la plantilla sin preguntar (la hoja de confirmación ya preguntó).</summary>
    public void BorrarConfirmado(PlantillaEquipo p)
    {
        if (_almacen is null) return;
        if (_almacen.Borrar(p.Id) is { } fallo) { Avisar(TextosEquipo.FalloAlGuardar(fallo), false); return; }
        Elegida = null;
        Refrescar();
        Avisar(TextosEquipo.PlantillaBorrada(p.Nombre), true);
    }

    // ---------------------------------------------------------------------------------------------------------------- aplicar
    /// <summary>El plan que se enseñó en la hoja de confirmación (para la comprobación de la regla 7 al confirmar).</summary>
    public PlanAplicacion? UltimoPlan { get; private set; }

    [RelayCommand]
    public void Aplicar()
    {
        if (Sesion is not { } sesion) { Avisar(TextosEquipo.AplicarSinPartida, false); return; }
        if (PlantillaElegida is not { } p) { Avisar(TextosEquipo.SinPlantillaSeleccionada, false); return; }
        var plan = AplicadorPlantilla.Planificar(sesion, p, Contexto.CajaActual?.Invoke() ?? 0, Textos);
        UltimoPlan = plan;
        var parrafos = new List<string>();
        if (plan.EnEquipo > 0)
        {
            parrafos.Add(TextosEquipo.AplicarEnEquipo(plan.EnEquipo));
            parrafos.Add(string.Join("\n", plan.Items.Where(i => i.Destino == DestinoPlantilla.Equipo).Select((i, n) => TextosEquipo.AplicarEnEquipoLinea(n + 1, i.Nombre))));
        }
        if (plan.Desplazados.Count > 0)
        {
            parrafos.Add(TextosEquipo.AplicarEquipoActualACajas);
            parrafos.Add(string.Join("\n", plan.Desplazados.Select(d => d.Casilla is { } c ? TextosEquipo.AplicarDesplazadoLinea(d.Nombre, c.Descripcion) : TextosEquipo.AplicarDesplazadoLinea(d.Nombre, d.Motivo ?? string.Empty))));
        }
        if (plan.EnCaja > 0)
        {
            var primera = plan.Items.First(i => i.Destino == DestinoPlantilla.Caja).Casilla!;
            parrafos.Add(TextosEquipo.AplicarEnCaja(plan.EnCaja, primera.Descripcion));
        }
        foreach (var i in plan.Items.Where(i => i.Destino == DestinoPlantilla.NoSeColoca)) parrafos.Add(TextosEquipo.AplicarNoColocado(i.Nombre, i.Motivo ?? string.Empty));
        foreach (var i in plan.Items.Where(i => i.Destino != DestinoPlantilla.NoSeColoca)) foreach (var a in i.Avisos.Take(2)) parrafos.Add(TextosEquipo.AplicarAviso(i.Nombre, a));
        if (plan.Bloqueo is not null)
        {
            parrafos.Insert(0, plan.Bloqueo);
            Contexto.AbrirHoja(new InformeHojaViewModel(TextosEquipo.AplicarTitulo, string.Join("\n\n", parrafos), estado: plan.Bloqueo, claseEstado: "aviso"));
            Estado = plan.Bloqueo;
            return;
        }
        if (plan.Colocables == 0)
        {
            parrafos.Insert(0, TextosEquipo.AplicarNadaQueColocar);
            Contexto.AbrirHoja(new InformeHojaViewModel(TextosEquipo.AplicarTitulo, string.Join("\n\n", parrafos), estado: TextosEquipo.AplicadaFallo, claseEstado: "aviso"));
            Estado = TextosEquipo.AplicarNadaQueColocar;
            return;
        }
        parrafos.Add(TextosEquipo.AplicarPuedesDeshacer);
        Contexto.AbrirHoja(new ConfirmarHojaViewModel(TextosEquipo.AplicarTitulo, parrafos, TextosEquipo.AplicarConfirmar, () => AplicarConfirmado(p, plan)));   // solo ocupa huecos libres: no sustituye nada, así que no es destructiva
    }

    /// <summary>
    /// Escribe el plan en la partida. Regla 7: si desde que se enseñó la partida cambió (otro hueco libre, otro equipo), no se escribe el plan viejo: se avisa y hay que volver a pulsar «Aplicar».
    /// </summary>
    public void AplicarConfirmado(PlantillaEquipo p, PlanAplicacion plan)
    {
        if (Sesion is not { } sesion) return;
        var ahora = AplicadorPlantilla.Planificar(sesion, p, Contexto.CajaActual?.Invoke() ?? 0, Textos);
        if (Huella(ahora) != Huella(plan)) { Avisar(TextosEquipo.PartidaCambio, false); return; }
        var n = AplicadorPlantilla.Aplicar(sesion, ahora);
        if (n == 0) { Avisar(TextosEquipo.AplicadaFallo, false); return; }
        Avisar(TextosEquipo.AplicadaSustituyendo(p.Nombre, n, ahora.AlaCaja) + (ahora.NoColocados > 0 ? TextosEquipo.AplicadaOmitidos(ahora.NoColocados) : string.Empty), true);
        Refrescar();
    }

    private static string Huella(PlanAplicacion plan) => string.Join("|", plan.Items.Concat(plan.Desplazados).Select(i => $"{i.Destino}:{(i.Casilla is { } c ? c.Descripcion : "-")}:{i.Pokemon?.Species}:{i.Pokemon?.PID}")) + $"|{plan.Bloqueo}";
}
