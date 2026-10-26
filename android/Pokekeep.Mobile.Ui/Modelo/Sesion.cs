// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using CommunityToolkit.Mvvm.ComponentModel;
using PKHeX.Core;
using Cajas_ = Pokekeep.Mobile.Ui.Cajas;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Modelo;

/// <summary>
/// Sesion de trabajo: la copia privada del guardado ya leida por el Core, su nombre original y el estado de cambios. Los textos salen de las listas del Core en espanol (GameInfo).
/// El original del usuario no se toca nunca (regla 13): aqui solo vive la copia de sesion.
/// </summary>
public sealed partial class Sesion : ObservableObject
{
    public Sesion(SaveFile sav, string nombreArchivo)
    {
        Guardado = sav;
        NombreArchivo = nombreArchivo;
        // Como PKHeX al abrir una partida: el entrenador activo y los datos del guardado que el Core usa al analizar (indicador de legalidad de las casillas, informe, editor). Una vez, bajo el cerrojo de legalidad (ADR 0060).
        lock (Pokekeep.Sprites.LegalityGate.Cerrojo) ParseSettings.InitFromSaveFileData(sav);
        if (Design.Sprites.Configurado)
        {
            Compositor = Design.Sprites.Nuevo();
            Compositor.CambiarModo(Design.Sprites.ModoVigente);
            Compositor.Iniciar(sav);
            ModoSprites = Compositor.Modo;
        }
        else ModoSprites = Pokekeep.Sprites.ModoSpritesRegla.Sugerido(sav, Ajustes.Pkhex.ProgramaPkhex.Ajustes.Actual.Sprite.SpritePreference) ?? Design.Sprites.ModoVigente;
        Design.Sprites.ModoVigente = ModoSprites;
        Zonas = Cajas_.ZonasGuardado.De(sav);
        Casillas = new Cajas_.EditorCasillas(this);
        // Como Main.OpenSAV (PK-MENU-060): el último entrenador cargado queda para lo que el Core rellena sin partida delante (el entrenador actual al convertir de Gen. 1-2 y 5, la región de la 3DS, el contexto de un set de Showdown).
        RecentTrainerCache.SetRecentTrainer(sav);
        // Cada cambio real (escribir, borrar, mover, lote, Deshacer, Rehacer) cuenta para «cambios pendientes»; nada sale del dispositivo sin la orden explicita de Guardar o Exportar (regla 13).
        Casillas.Cambiado += (tipo, _) =>
        {
            switch (tipo)
            {
                case Cajas_.EditorCasillas.TipoCambio.Deshacer: _posicion--; break;
                case Cajas_.EditorCasillas.TipoCambio.Rehacer: _posicion++; break;
                default:
                    // Una escritura nueva descarta lo deshecho: si el punto guardado estaba en lo descartado, ya no se puede volver a el (el guardado nunca vuelve a estar «limpio» sin guardar).
                    if (_posicion < _posicionGuardada) _posicionGuardada = PuntoPerdido;
                    _posicion++;
                    break;
            }
            AvisarCambiosPendientes();
            NotificarCasillas(tipo);
        };
    }

    /// <summary>Operaciones sobre las casillas de esta partida (colocar, borrar, mover, lote, Deshacer...) con el historial del Core.</summary>
    public Cajas_.EditorCasillas Casillas { get; }
    /// <summary>Zonas de almacenamiento del guardado (cajas, equipo, guarderia, Caja de combate, GTS, fusion...), calculadas una vez.</summary>
    public Cajas_.ZonasGuardado Zonas { get; }
    /// <summary>Se dispara tras cada cambio hecho por <see cref="Casillas"/> (la interfaz refresca la vista).</summary>
    public event Action<Cajas_.EditorCasillas.TipoCambio>? CasillasCambiadas;

    // Suscripciones DEBILES para los editores que se abren y se cierran muchas veces dentro de una misma sesion: con «+=» y una lambda cada apertura dejaba una copia viva (con su modelo y sus filas) hasta cerrar la partida.
    // Quien se suscribe guarda su Action en un campo (la referencia fuerte); aqui solo se nombra de forma debil y desaparece cuando el editor se libera.
    private readonly List<WeakReference<Action<Cajas_.EditorCasillas.TipoCambio>>> _debiles = [];

    /// <summary>Suscribe sin retener al suscriptor: <paramref name="accion"/> debe vivir en un campo suyo.</summary>
    public void SuscribirCasillasDebil(Action<Cajas_.EditorCasillas.TipoCambio> accion) { lock (_debiles) _debiles.Add(new WeakReference<Action<Cajas_.EditorCasillas.TipoCambio>>(accion)); }

    private void NotificarCasillas(Cajas_.EditorCasillas.TipoCambio tipo)
    {
        CasillasCambiadas?.Invoke(tipo);
        List<Action<Cajas_.EditorCasillas.TipoCambio>> vivas = [];
        lock (_debiles) { _debiles.RemoveAll(w => !w.TryGetTarget(out _)); foreach (var w in _debiles) if (w.TryGetTarget(out var a)) vivas.Add(a); }
        foreach (var a in vivas) a(tipo);
    }

    /// <summary>Compositor de sprites de esta partida (modo segun el guardado, Deoxys de Gen. III, cache propia). Null solo si la plataforma no configuro los paquetes de sprites.</summary>
    public Pokekeep.Sprites.SpriteComposer? Compositor { get; private set; }

    public SaveFile Guardado { get; }
    public string NombreArchivo { get; }
    /// <summary>Las listas del Core en el idioma de los datos vigente (<c>Startup.Language</c>, «es» de fábrica; bloque 10-1): se leen en el momento, así que cambiar el idioma se nota sin reabrir la partida.</summary>
    public GameStrings Textos => Ajustes.Pkhex.ProgramaPkhex.Textos;
    /// <summary>Juego de sprites de la caja segun la partida (regla de PKHeX, ajuste «UseSuggested»; el ajuste manual llega con la fase de Ajustes).</summary>
    public Pokekeep.Sprites.ModoSprites ModoSprites { get; private set; }

    public void ActualizarSprites(Ajustes.Pkhex.SpritePkhex ajustes)
    {
        if (Design.Sprites.Configurado)
        {
            var compositor = Design.Sprites.Nuevo(ajustes.ComoOpciones());
            compositor.CambiarModo(ModoSprites);   // «No cambiar» conserva el modo de esta sesión
            compositor.Iniciar(Guardado);
            Compositor = compositor;
            ModoSprites = compositor.Modo;
        }
        else ModoSprites = Pokekeep.Sprites.ModoSpritesRegla.Sugerido(Guardado, ajustes.SpritePreference) ?? ModoSprites;
        Design.Sprites.ModoVigente = ModoSprites;
        OnPropertyChanged(nameof(Compositor));
        OnPropertyChanged(nameof(ModoSprites));
    }

    // ---- «Limpio respecto al disco» ----
    // El estado del guardado es un PUNTO del historial de casillas: cada escritura lo avanza, Deshacer lo retrocede y Rehacer lo adelanta. El punto del ultimo guardado (o de la apertura) se recuerda:
    // Deshacer tras Guardar vuelve a mostrar cambios pendientes y Rehacer hasta ese punto vuelve a «sin cambios». Lo que no esta en el historial (guarderia, sesion recuperada) cuenta aparte y no se deshace.
    private const int PuntoPerdido = int.MinValue;
    private int _posicion, _posicionGuardada, _fueraDelHistorial, _fueraDelHistorialGuardado;

    /// <summary>Cuantos cambios hay sin guardar respecto al disco (0 = limpio). Es la distancia, en entradas del historial, al punto guardado mas lo que no esta en el historial.</summary>
    public int CambiosPendientes
    {
        get
        {
            var fuera = _fueraDelHistorial - _fueraDelHistorialGuardado;
            if (_posicionGuardada == PuntoPerdido) return Math.Max(1, _posicion + fuera);
            return Math.Abs(_posicion - _posicionGuardada) + fuera;
        }
        // Compatibilidad: 0 = «acaba de guardarse» y un valor mayor = cambios que el historial no conoce (sesion recuperada, pruebas). Nada mas debe asignarlo.
        set
        {
            if (value <= 0) MarcarGuardado();
            else if (value > CambiosPendientes) { _fueraDelHistorial += value - CambiosPendientes; AvisarCambiosPendientes(); }
        }
    }

    public bool EstaLimpia => CambiosPendientes == 0;

    /// <summary>El guardado en memoria es ahora el del disco (tras un guardado verificado o una exportacion): este punto del historial pasa a ser «limpio».</summary>
    public void MarcarGuardado()
    {
        _posicionGuardada = _posicion;
        _fueraDelHistorialGuardado = _fueraDelHistorial;
        AvisarCambiosPendientes();
    }

    /// <summary>Un cambio que no pasa por el historial de casillas (datos de la guarderia): cuenta como pendiente y no se puede deshacer con Deshacer.</summary>
    public void MarcarCambioFueraDelHistorial()
    {
        _fueraDelHistorial++;
        AvisarCambiosPendientes();
        NotificarCasillas(Cajas_.EditorCasillas.TipoCambio.Masivo);
    }

    private void AvisarCambiosPendientes() { OnPropertyChanged(nameof(CambiosPendientes)); OnPropertyChanged(nameof(EstaLimpia)); }

    /// <summary>Documento original del usuario (SAF) del que sale esta sesion; null si se creo sin documento.</summary>
    public Almacen.DocumentoRef? Documento { get; set; }
    /// <summary>SHA-256 de lo leído al abrir o de los últimos bytes enviados al documento (también sin comprobación). Antes de volver a escribir se exige una lectura que coincida; no implica que el trabajo esté limpio.</summary>
    public string Sha256Original { get; set; } = string.Empty;
    /// <summary>Carpeta privada de la sesion (<c>sesiones/&lt;sha8&gt;/</c>): copia del original y copia de recuperacion.</summary>
    public string? CarpetaPrivada { get; set; }
    /// <summary>Contenedor del documento (directo, .zip o tarjeta de memoria de GameCube): Guardar y Exportar lo respetan (F10-2).</summary>
    public Abrir.EnvoltorioPartida Envoltorio { get; set; } = Abrir.EnvoltorioPartida.Directo;

    /// <summary>Nombre del juego en espanol segun el Core (por ejemplo «Platino»).</summary>
    public string Juego => JuegoDe(Guardado.Version);

    /// <summary>Nombre corto del juego para ENSEÑAR en pantalla («Platino»): el mismo de la ficha (<see cref="NombresJuego.Corto"/>). <see cref="Juego"/> es el valor guardado en recientes y plantillas.</summary>
    public string JuegoCorto => NombresJuego.Corto(Guardado.Version);

    /// <summary>Nombre OFICIAL del juego («Pokémon Espada»): lo que enseña la cabecera, porque el nombre del archivo de los guardados modernos («main», «savedata») no dice qué juego es.</summary>
    public string JuegoOficial => NombresJuego.Oficial(Guardado.Version);

    /// <summary>Nombre del juego en español según el Core, sin sesión (lo que la Shell enseña antes de crearla, bloque 10-3).</summary>
    public static string JuegoDe(GameVersion version)
    {
        foreach (var c in GameInfo.Sources.VersionDataSource) if (c.Value == (int)version) return c.Text;
        return version.ToString();
    }

    public string Plataforma => Guardado switch
    {
        SAV3Colosseum or SAV3XD or SAV3RSBox => "GameCube",
        SAV3 { IsVirtualConsole: true } => "Nintendo Switch",   // Rojo Fuego / Verde Hoja de Nintendo Switch Online (el Core lo sabe por el nombre del archivo)
        SAV1 { IsVirtualConsole: true } or SAV2 { IsVirtualConsole: true } => "Nintendo 3DS",
        _ => Guardado.Generation switch
        {
            1 or 2 => "Game Boy",
            3 => "Game Boy Advance",
            4 or 5 => "Nintendo DS",
            6 or 7 when Guardado.Version is GameVersion.GP or GameVersion.GE or GameVersion.GG => "Nintendo Switch",
            6 or 7 => "Nintendo 3DS",
            _ => "Nintendo Switch",
        },
    };

    public int Cajas => Guardado.BoxCount;
    public int CasillasPorCaja => Guardado.BoxSlotCount;
    /// <summary>Columnas de la caja: 6 desde la 3.ª generacion (6×5); 5 en las de 20 casillas (Gen. 1-2) y 5 en las de 25.</summary>
    public int ColumnasCaja => Guardado.BoxSlotCount switch { 20 => 5, 25 => 5, _ => 6 };

    /// <summary>Nombre de la caja como lo guarda el juego; si es el de fabrica («Box 3», «BOX 3», «ボックス3») o esta vacio se dice «Caja 3» en castellano.</summary>
    public string NombreCaja(int caja)
    {
        var n = Guardado is IBoxDetailNameRead r ? r.GetBoxName(caja) : string.Empty;
        if (string.IsNullOrWhiteSpace(n) || n == BoxDetailNameExtensions.GetDefaultBoxName(caja) || n == BoxDetailNameExtensions.GetDefaultBoxNameCaps(caja) || n == BoxDetailNameExtensions.GetDefaultBoxNameJapanese(caja))
            return IdiomaInterfaz.F("Qr.CajaAccesible", caja + 1);
        return n;
    }

    /// <summary>Indice de fondo de la caja (<c>IBoxDetailWallpaper</c>), o -1 si la partida no guarda fondos.</summary>
    public int FondoCaja(int caja) => Guardado is IBoxDetailWallpaper w ? w.GetBoxWallpaper(caja) : -1;
}
