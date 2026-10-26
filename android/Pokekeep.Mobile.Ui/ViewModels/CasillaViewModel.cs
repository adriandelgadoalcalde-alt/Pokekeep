// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using CommunityToolkit.Mvvm.ComponentModel;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Cajas;
using Pokekeep.Mobile.Ui.Modelo;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.ViewModels;

/// <summary>Con que se dibuja el ultimo toque de una casilla (los colores de <c>SlotTouchType</c> de PKHeX: ver = azul, colocar/intercambiar = verde, borrar/deshacer = rojo).</summary>
public enum Toque { Ninguno, Ver, Colocar, Borrar }

/// <summary>
/// Una casilla de la cuadricula (caja, equipo o zona). El OBJETO se reutiliza al cambiar de caja: <see cref="Asignar"/> cambia su contenido y los enlaces se actualizan sin recrear los controles
/// (deslizar entre cajas a 120 Hz no reconstruye 30 plantillas). Muestra sprite, estado de legalidad (con la insignia nativa, nunca solo por color), marcas de seleccion y de filtro, y el ultimo toque.
/// </summary>
public sealed partial class CasillaViewModel : ObservableObject
{
    private readonly Sesion _sesion;
    private Pokekeep.Sprites.CasillaCapas? _capas;

    public CasillaViewModel(Sesion sesion, int indice)
    {
        _sesion = sesion;
        Indice = indice;
    }

    /// <summary>Posicion en la cuadricula (0..n-1): el numero que se ve es <see cref="Numero"/>.</summary>
    public int Indice { get; }
    public int Numero => Indice + 1;

    /// <summary>De donde es la casilla ahora (caja y casilla, equipo o zona). Null si la cuadricula tiene mas posiciones que la zona.</summary>
    public CasillaRef? Ref { get; private set; }
    public PKM? Entidad { get; private set; }
    public bool Vacia => Entidad is null;
    public bool Ocupada => Entidad is not null;

    public Pokekeep.Sprites.ModoSprites Modo { get; private set; }
    public Avalonia.Media.Imaging.Bitmap? Sprite { get; private set; }
    public Avalonia.Media.Imaging.Bitmap? SpriteFondo { get; private set; }
    public Avalonia.Rect SpriteRecuadro { get; private set; }
    /// <summary>Marcas de la casilla (objeto, Gigamax, alfa, equipo, bloqueo, franja de Teratipo...) ancladas a la celda: el recuadro del Pokemon es solo el suyo (uso real 2).</summary>
    /// <summary>El sprite de la casilla es la version ampliada por el escalador de pixel art (ajuste «Suavizar sprites de la caja»).</summary>
    public bool SpriteSuave { get; private set; }
    /// <summary>X53: juego del que sale el icono de la casilla cuando es el icono de caja propio de la partida (Gen. III a V, Espada/Escudo, Diamante Brillante/Perla Reluciente); null = sprite de PKHeX.</summary>
    public string? JuegoIcono { get; private set; }
    public IReadOnlyList<Design.Sprites.MarcaVista> SpriteMarcas { get; private set; } = [];
    public bool SpriteEsSilueta { get; private set; }
    public int Contexto { get; private set; }
    public int Especie { get; private set; }
    public int Forma { get; private set; }
    public uint ArgumentoForma => Entidad is IFormArgument f ? f.FormArgument : 0;
    public int VersionJuego => (int)Design.Sprites.VersionDe(_sesion.Guardado);
    public int Genero { get; private set; }
    public bool Brillante { get; private set; }
    public bool Huevo { get; private set; }
    public int Objeto { get; private set; }
    public int Nivel { get; private set; }
    public string NombreEspecie { get; private set; } = string.Empty;
    /// <summary>Los datos de la casilla no son un Pokemon valido (huevo malo): PKHeX la pinta de rojo y sin sprite.</summary>
    public bool DatosInvalidos { get; private set; }
    /// <summary>La casilla no se puede escribir (bloqueada, de solo lectura o de guion).</summary>
    public bool SoloLectura { get; private set; }
    /// <summary>Analisis de legalidad de la casilla: no se hace para zonas sin datos de entrenador (Subsuelo...).</summary>
    public bool SinAnalisis { get; private set; }

    [ObservableProperty] private EstadoLegal _estado;
    [ObservableProperty] private int _pulsoBusqueda;
    [ObservableProperty] private bool _seleccionada;       // la casilla de la ficha
    [ObservableProperty] private bool _marcada;            // dentro de una seleccion multiple
    [ObservableProperty] private bool _enSeleccion;        // hay una seleccion multiple en curso: se pintan las casillas como marcables
    [ObservableProperty] private bool _atenuada;           // no coincide con la busqueda de cajas
    [ObservableProperty] private bool _coincide;           // coincide con la busqueda (se resalta)
    [ObservableProperty] private Toque _toque;             // ultimo cambio sobre la casilla
    [ObservableProperty] private bool _esOrigen;           // origen de un movimiento en curso
    [ObservableProperty] private bool _esArrastrada;       // el Pokemon se ha levantado de aqui (X10): la casilla queda atenuada mientras el fantasma va con el dedo
    [ObservableProperty] private bool _objetivoSoltar;     // el dedo con el fantasma esta encima y aqui se puede soltar
    [ObservableProperty] private bool _objetivoRechazado;  // el dedo esta encima pero aqui no se puede soltar (bloqueada, de solo lectura, equipo que quedaria invalido)

    public bool EsIlegal => Estado == EstadoLegal.Ilegal;
    public bool EsHome => Estado == EstadoLegal.NecesitaHome;
    public bool MostrarAviso => Ocupada && !SinAnalisis && (DatosInvalidos || (!Ajustes.Pkhex.ProgramaPkhex.HaX && Ajustes.Pkhex.ProgramaPkhex.Ajustes.Actual.Display.FlagIllegal && Estado is EstadoLegal.Ilegal or EstadoLegal.NecesitaHome));
    public string GlifoAviso => DatosInvalidos ? "TriangleAlert" : LegalidadUi.Glifo(Estado);
    public bool TocoVer => Toque == Toque.Ver;
    public bool TocoColocar => Toque == Toque.Colocar;
    public bool TocoBorrar => Toque == Toque.Borrar;
    public bool Bloqueada => SoloLectura && Ocupada;
    /// <summary>Casilla marcable de una seleccion en curso que aun no esta marcada (se dibuja un aro, sin relleno).</summary>
    public bool ParaMarcar => EnSeleccion && !Marcada && Ocupada;
    partial void OnEnSeleccionChanged(bool value) => OnPropertyChanged(nameof(ParaMarcar));

    partial void OnEstadoChanged(EstadoLegal value) { OnPropertyChanged(nameof(EsIlegal)); OnPropertyChanged(nameof(EsHome)); OnPropertyChanged(nameof(MostrarAviso)); OnPropertyChanged(nameof(GlifoAviso)); OnPropertyChanged(nameof(NombreAccesible)); }
    partial void OnToqueChanged(Toque value) { OnPropertyChanged(nameof(TocoVer)); OnPropertyChanged(nameof(TocoColocar)); OnPropertyChanged(nameof(TocoBorrar)); }
    partial void OnMarcadaChanged(bool value) { OnPropertyChanged(nameof(NombreAccesible)); OnPropertyChanged(nameof(ParaMarcar)); }
    public double Atenuacion => EsArrastrada ? Design.GestoArrastre.OpacidadOrigen : Atenuada ? Math.Clamp(Ajustes.Pkhex.ProgramaPkhex.Ajustes.Actual.Sprite.FilterMismatchOpacity, 0, 1) : 1;
    partial void OnEsArrastradaChanged(bool value) => OnPropertyChanged(nameof(Atenuacion));
    partial void OnAtenuadaChanged(bool value)
    {
        ActualizarImagenDeFiltro();
        OnPropertyChanged(nameof(NombreAccesible));
        OnPropertyChanged(nameof(Atenuacion));
    }

    private void ActualizarImagenDeFiltro()
    {
        if (_capas is null) return;
        Pokekeep.Sprites.SpriteImage Imagen(Pokekeep.Sprites.SpriteImage original)
        {
            if (!Atenuada) return original;
            var copia = original.Clone();
            Pokekeep.Sprites.PixelOps.ToGrayscale(copia, Ajustes.Pkhex.ProgramaPkhex.Ajustes.Actual.Sprite.FilterMismatchGrayscale);
            return copia;
        }
        var pokemon = Imagen(_capas.Pokemon);
        JuegoIcono = _capas.JuegoIcono;
        // El suavizado de la caja es del escalador de pixel art: no se aplica a las ilustraciones suaves de Diamante Brillante y Perla Reluciente.
        if (Design.Sprites.SuavizarCaja && (JuegoIcono is null || Pokekeep.Sprites.SpritesDeJuego.EsPixelArt(JuegoIcono))) { (Sprite, SpriteRecuadro) = Design.Sprites.AmpliadoDeCasilla(pokemon); SpriteSuave = true; }
        else { Sprite = Design.Sprites.Crear(pokemon); SpriteRecuadro = Design.Sprites.Recuadro(pokemon); SpriteSuave = false; }
        SpriteFondo = _capas.Fondo is null ? null : Design.Sprites.Crear(Imagen(_capas.Fondo));
        SpriteMarcas = _capas.Marcas.Select(m => Design.Sprites.VistaDe(m with { Imagen = Imagen(m.Imagen) })).OfType<Design.Sprites.MarcaVista>().ToList();
        OnPropertyChanged(nameof(Sprite)); OnPropertyChanged(nameof(SpriteRecuadro)); OnPropertyChanged(nameof(SpriteFondo));
        OnPropertyChanged(nameof(SpriteMarcas)); OnPropertyChanged(nameof(SpriteSuave)); OnPropertyChanged(nameof(JuegoIcono));
    }
    partial void OnCoincideChanged(bool value) => OnPropertyChanged(nameof(NombreAccesible));

    /// <summary>
    /// Pone el contenido de la casilla. <paramref name="analisis"/> = el estado de legalidad se calcula aqui mismo (pruebas y listas cortas); si es falso queda el que ya se conocia (cache) o «sin comprobar»
    /// hasta que el analizador en segundo plano lo rellene (<see cref="PonerEstado"/>).
    /// </summary>
    public void Asignar(CasillaRef? referencia, PKM? pk, bool sinAnalisis, bool analisis)
    {
        Ref = referencia;
        var vacia = pk is null || pk.Species == 0;
        var ent = vacia ? null : pk;
        var tipo = referencia?.Tipo ?? StorageSlotType.Box;
        Entidad = ent;
        _capas = null;
        SpriteFondo = null;
        SinAnalisis = sinAnalisis;
        SoloLectura = referencia is not null && !_sesion.Casillas.EsEscribible(referencia.Info);
        DatosInvalidos = false;
        Toque = Toque.Ninguno; Marcada = false; EsOrigen = false; EsArrastrada = false; ObjetivoSoltar = false; ObjetivoRechazado = false;
        if (ent is { } e)
        {
            DatosInvalidos = !e.Valid;
            Especie = e.Species; Forma = e.Form; Genero = e.Gender; Brillante = e.IsShiny; Huevo = e.IsEgg; Objeto = e.HeldItem; Nivel = e.CurrentLevel;
            Contexto = (int)e.Context;
            NombreEspecie = e.Species < _sesion.Textos.Species.Count ? _sesion.Textos.Species[e.Species] : $"#{e.Species}";
            Modo = _sesion.ModoSprites;
            Sprite = null; SpriteRecuadro = default; SpriteEsSilueta = false; SpriteMarcas = []; SpriteSuave = false; JuegoIcono = null;
            if (_sesion.Compositor is { } comp)
            {
                // Sprite de la casilla como PKHeX (equipo/bloqueo/inicial, Teratipo, Gigamax, alfa...). El aviso de legalidad es la insignia nativa del diseno, no el icono cocido en el sprite.
                var (caja, ranura) = referencia?.Info switch { SlotInfoBox b => (b.Box, b.Slot), SlotInfoParty p => (-1, p.Slot), _ => (-1, -1) };
                _capas = comp.GetSlotCapas(e, _sesion.Guardado, caja, ranura, tipo, !sinAnalisis && !Ajustes.Pkhex.ProgramaPkhex.HaX && Ajustes.Pkhex.ProgramaPkhex.Ajustes.Actual.Display.FlagIllegal);
                ActualizarImagenDeFiltro();
                SpriteEsSilueta = _capas.JuegoIcono is null && comp.NombreRecursoBase(e.Species, e.Form, e.Gender, e is IFormArgument fa ? fa.FormArgument : 0u, e.IsShiny, e.Context) is null;
            }
            if (sinAnalisis) Estado = EstadoLegal.SinDatos;
            else if (analisis) Estado = AnalizadorLegalidad.Evaluar(e, tipo);
            else Estado = AnalizadorLegalidad.Conocido(e, tipo) ?? EstadoLegal.SinDatos;
        }
        else
        {
            Especie = 0; Forma = 0; Genero = 0; Brillante = false; Huevo = false; Objeto = 0; Nivel = 0; Contexto = 0;
            NombreEspecie = string.Empty; Sprite = null; SpriteFondo = null; SpriteRecuadro = default; SpriteEsSilueta = false; SpriteMarcas = []; SpriteSuave = false; JuegoIcono = null;
            Estado = EstadoLegal.SinDatos;
        }
        OnPropertyChanged(string.Empty);   // todas las propiedades de solo lectura cambian a la vez
    }

    /// <summary>Estado llegado del analizador en segundo plano.</summary>
    public void PonerEstado(EstadoLegal estado) => Estado = estado;

    /// <summary>¿Hay que analizar esta casilla en segundo plano? (ocupada, con analisis y sin estado conocido).</summary>
    public bool PendienteDeAnalisis => Ocupada && !SinAnalisis && Estado == EstadoLegal.SinDatos && Entidad is not null;

    public StorageSlotType TipoAlmacen => Ref?.Tipo ?? StorageSlotType.Box;

    /// <summary>Nombre accesible completo de la casilla (TalkBack): «Casilla N, especie, nivel, estado».</summary>
    public string NombreAccesible
    {
        get
        {
            var marca = (Marcada ? IdiomaInterfaz.T("Casilla.Seleccionada") : string.Empty) + (Atenuada ? IdiomaInterfaz.T("Casilla.CoincideBusqueda") : Coincide ? IdiomaInterfaz.T("Casilla.CoincideBusqueda2") : string.Empty);
            if (Entidad is null) return IdiomaInterfaz.F("Casilla.CasillaVacia", Numero, marca);
            if (DatosInvalidos) return IdiomaInterfaz.F("Casilla.CasillaDatosValidos", Numero, marca);
            var estado = SinAnalisis ? string.Empty : $", {LegalidadUi.Texto(Estado).ToLowerInvariant()}";
            return IdiomaInterfaz.F("Casilla.NombreAccesible", Numero, Huevo ? IdiomaInterfaz.T("Casilla.Huevo") : NombreEspecie, Nivel, estado, Objeto > 0 ? IdiomaInterfaz.T("Casilla.Objeto") : "", Brillante ? IdiomaInterfaz.T("Casilla.Variocolor") : "", Bloqueada ? IdiomaInterfaz.T("Casilla.Bloqueada") : "", marca);
        }
    }
}
