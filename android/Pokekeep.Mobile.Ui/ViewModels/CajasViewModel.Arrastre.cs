// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using CommunityToolkit.Mvvm.ComponentModel;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Cajas;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.ViewModels;

/// <summary>Que paso al soltar el Pokemon que se arrastraba: si se cambio algo, si fue un intercambio (y el Pokemon que se desplazo, para animarlo hacia el origen) y, si no, por que.</summary>
public sealed record ResultadoSoltar(bool Aplicado, bool Intercambio, CasillaRef? Origen, CasillaRef? Destino, CasillaViewModel? Desplazado, string? Motivo)
{
    public static readonly ResultadoSoltar Nada = new(false, false, null, null, null, null);
}

/// <summary>
/// «Mantener pulsado y arrastrar» (X10): el modelo del gesto, sin pantalla. La vista traduce el dedo a estas operaciones (<see cref="Levantar"/>, <see cref="EmpezarMovimiento"/>, <see cref="Sobrevolar"/>, <see cref="SoltarEn"/>,
/// <see cref="CancelarArrastre"/>) y el modelo decide con las MISMAS reglas que «Mover a…» (<see cref="EditorCasillas.Mover"/>: intercambia si el destino esta ocupado, el equipo no queda vacio ni con huecos, las casillas
/// bloqueadas no se tocan, un solo Deshacer). Los caminos accesibles de siempre («Mover a…» y la seleccion) siguen donde estaban.
/// </summary>
public sealed partial class CajasViewModel
{
    /// <summary>Alternativa de presentación persistente; desactivada adapta la zona.</summary>
    [ObservableProperty] private bool _centrarEquipoAlArrastrar;
    partial void OnCentrarEquipoAlArrastrarChanged(bool value)
    {
        if (_anfitrion?.CarpetaPreferenciasInterfaz is not { } carpeta) return;
        try { new Ajustes.PreferenciasArrastre(carpeta).Guardar(value); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { _anfitrion.Informar(IdiomaInterfaz.T("Cajas.NoGuardarArrastre"), "aviso", "CircleAlert"); }
    }
    public bool MostrarEquipoEnPagina => MostrarEquipo && !MostrarApoyoEquipo;
    partial void OnMostrarApoyoEquipoChanged(bool value) => OnPropertyChanged(nameof(MostrarEquipoEnPagina));
    private List<CasillaRef> _arrastreRefs = [];
    private CasillaRef? _arrastreOrigen;
    private CasillaViewModel? _sobreArrastre;

    /// <summary>Hay un Pokemon levantado (en la mano del usuario) que aun no se ha soltado ni cancelado.</summary>
    [ObservableProperty] private bool _arrastrando;
    /// <summary>Copia del Pokemon levantado (su propio sprite): el fantasma que sigue al dedo. Sobrevive a un cambio de caja, donde las casillas se reutilizan, y a la animacion final.</summary>
    [ObservableProperty] private CasillaViewModel? _fantasma;
    [ObservableProperty] private int _arrastreCantidad;
    /// <summary>Zona de destino visible durante el arrastre: el equipo, si estas viendo las cajas (el equipo esta en otra pestana).</summary>
    [ObservableProperty] private bool _mostrarApoyoEquipo;
    /// <summary>Zona de destino visible durante el arrastre: la caja actual, si estas viendo el equipo.</summary>
    [ObservableProperty] private bool _mostrarApoyoCaja;

    /// <summary>El Pokemon que estaba en el destino y se intercambio: la vista lo anima hacia el origen. Valido hasta <see cref="LiberarArrastre"/>.</summary>
    public CasillaViewModel? Desplazado { get; private set; }
    public CasillaViewModel? SobreArrastre => _sobreArrastre;
    public bool ArrastreEsGrupo => _arrastreRefs.Count > 1;
    public CasillaRef? OrigenArrastre => _arrastreOrigen;

    /// <summary>Texto para quien no ve la pantalla: lo que se esta haciendo con el Pokemon en la mano.</summary>
    public string TextoArrastre => ArrastreCantidad > 1 ? IdiomaInterfaz.F("CajasViewModelArrastre.LlevandoPokemon", ArrastreCantidad) : Fantasma is { } f ? IdiomaInterfaz.F("CajasViewModelArrastre.LlevandoA", f.NombreEspecie) : string.Empty;

    partial void OnArrastrandoChanged(bool value) => AplicarMarcas();
    partial void OnArrastreCantidadChanged(int value) => OnPropertyChanged(nameof(TextoArrastre));
    partial void OnFantasmaChanged(CasillaViewModel? value) => OnPropertyChanged(nameof(TextoArrastre));

    /// <summary>
    /// ¿Se puede levantar el Pokemon de esta casilla? En cajas, equipo y las ranuras de Otros que el Core permite escribir, con Pokemon, sin otro modo en marcha y, con la
    /// seleccion multiple activa, solo uno de los marcados (arrastrarlo lleva todo el grupo). <paramref name="motivo"/>: si es de solo lectura, el aviso que se da (el mismo de los menus).
    /// </summary>
    public bool PuedeLevantar(CasillaViewModel? casilla, out string? motivo)
    {
        motivo = null;
        if (casilla is not { Ocupada: true, Ref: { } r } || Colocando || Arrastrando) return false;
        if (!Sesion.Casillas.EsEscribible(r.Info)) { motivo = IdiomaInterfaz.T("Cajas.SoloLecturaPuedeMover"); return false; }
        if (Seleccionando && !casilla.Marcada) return false;
        return true;
    }

    /// <summary>El Pokemon se «levanta» (pulsacion larga). Todavia no se mueve nada: soltar sin haber movido el dedo es la pulsacion larga de siempre (la vista llama entonces a <see cref="PulsacionLarga"/>).</summary>
    public bool Levantar(CasillaViewModel casilla)
    {
        if (!PuedeLevantar(casilla, out _)) return false;
        var grupo = Seleccionando ? MarcadasOcupadas().ToList() : [casilla.Ref!];
        if (grupo.Count == 0) return false;
        LiberarArrastre();
        _arrastreRefs = grupo;
        _arrastreOrigen = casilla.Ref;
        ArrastreCantidad = grupo.Count;
        Fantasma = Instantanea(casilla);
        _ignorarToqueHasta = DateTime.UtcNow.AddMilliseconds(600);
        Arrastrando = true;   // atenua el origen (AplicarMarcas)
        return true;
    }

    /// <summary>El dedo ha empezado a moverse: aparece la zona de destino que no esta a la vista (el equipo desde las cajas, la caja desde el equipo) con su contenido al dia.</summary>
    public void EmpezarMovimiento()
    {
        if (!Arrastrando) return;
        // La variante se cambia en Ajustes, con la caja quizá ya abierta: se relee al empezar cada arrastre.
        if (_anfitrion?.CarpetaPreferenciasInterfaz is { } carpetaPref)
            _centrarEquipoAlArrastrar = new Ajustes.PreferenciasArrastre(carpetaPref).Leer();
        // Release (QA de uso10, prueba 02, decisión del propietario): el modo arrastre tiene SIEMPRE el montaje de las cajas: la caja arriba y el equipo
        // debajo, centrados. Desde Equipo, el equipo de la página se sustituye por su zona de apoyo (las mismas casillas) y la caja entra encima.
        if (EsCajas && TieneEquipo)
        {
            MostrarApoyoEquipo = true;
            CargarEquipo();
        }
        if ((EsEquipo || EsOtros) && TieneCajas) { MostrarApoyoCaja = true; CargarCaja(Caja, seleccionar: false); }
        if (EsEquipo && TieneCajas && TieneEquipo) MostrarApoyoEquipo = true;   // sus casillas ya están cargadas (es la pestaña)
        if (EsOtros && TieneEquipo) { MostrarApoyoEquipo = true; CargarEquipo(); }
        AplicarMarcas();
    }

    /// <summary>Otra caja de destino durante el arrastre desde Equipo/Otros, sin cambiar de pestaña, de origen ni escribir la partida.</summary>
    public void CambiarCajaDeApoyo(int direccion)
    {
        if (!Arrastrando || !MostrarApoyoCaja || TotalCajas < 2 || direccion == 0) return;
        LimpiarSobrevuelo();
        CargarCaja(CajaEnDireccion(direccion), seleccionar: false);
        AplicarMarcas();
    }

    private CasillaViewModel Instantanea(CasillaViewModel c)
    {
        var f = new CasillaViewModel(Sesion, c.Indice);
        f.Asignar(c.Ref, c.Entidad, c.SinAnalisis, false);
        f.PonerEstado(c.Estado);
        return f;
    }

    /// <summary>Por que NO se puede soltar lo que se lleva en esa casilla (null si se puede). Una casilla que es el propio origen no se rechaza: soltar ahi es cancelar.</summary>
    public string? MotivoNoSoltar(CasillaViewModel? destino)
    {
        if (!Arrastrando || _arrastreOrigen is null) return IdiomaInterfaz.T("Cajas.NadaMano");
        if (destino?.Ref is not { } d) return IdiomaInterfaz.T("Cajas.AquiPuedeSoltar");
        if (Misma(_arrastreOrigen.Info, d.Info)) return null;
        if (ArrastreEsGrupo)
        {
            if (d.Info is not SlotInfoBox) return IdiomaInterfaz.T("Cajas.VariosPokemonSoloSueltan");
            return Sesion.Casillas.EsEscribible(d.Info) ? null : MessageStrings.MsgSaveSlotLocked;
        }
        return Sesion.Casillas.ComprobarMover(Fresca(_arrastreOrigen.Info), Fresca(d.Info), ModoMover.MoverOIntercambiar, out _, out _, out _)?.Mensaje;
    }

    /// <summary>El aviso de «solo lectura» de siempre (mismo texto que los menus) cuando se intenta levantar un Pokemon que no se puede mover.</summary>
    public void AvisarSoloLectura(string motivo) => _anfitrion?.Informar(motivo, "aviso", "Info");

    /// <summary>El dedo con el fantasma esta encima de una casilla (o de ninguna): la resalta en verde si se puede soltar, en rojo si no.</summary>
    public void Sobrevolar(CasillaViewModel? casilla)
    {
        if (ReferenceEquals(casilla, _sobreArrastre) && casilla is not null && (casilla.ObjetivoSoltar || casilla.ObjetivoRechazado || EsElOrigen(casilla))) return;
        LimpiarSobrevuelo();
        if (!Arrastrando || casilla is null) return;
        _sobreArrastre = casilla;
        if (EsElOrigen(casilla)) return;
        var motivo = MotivoNoSoltar(casilla);
        casilla.ObjetivoSoltar = motivo is null;
        casilla.ObjetivoRechazado = motivo is not null;
    }

    private bool EsElOrigen(CasillaViewModel c) => c.Ref is { } r && _arrastreOrigen is { } o && Misma(o.Info, r.Info);

    private void LimpiarSobrevuelo()
    {
        if (_sobreArrastre is { } s) { s.ObjetivoSoltar = false; s.ObjetivoRechazado = false; }
        _sobreArrastre = null;
        // Las casillas se reutilizan al cambiar de caja: por si quedo alguna marcada en otra.
        foreach (var c in TodasLasVisibles) { if (c.ObjetivoSoltar) c.ObjetivoSoltar = false; if (c.ObjetivoRechazado) c.ObjetivoRechazado = false; }
    }

    private void TerminarMano()
    {
        LimpiarSobrevuelo();
        _arrastreRefs = [];
        _arrastreOrigen = null;
        _ignorarToqueHasta = DateTime.UtcNow.AddMilliseconds(600);
        Arrastrando = false;
    }

    /// <summary>Suelta el Pokemon sobre una casilla: la mueve o la intercambia con <see cref="EditorCasillas.Mover"/> (varios, a los huecos libres de esa caja con <see cref="EditorCasillas.MoverAHuecos"/>). Sin casilla, sobre el propio origen o en un sitio que no admite, no cambia nada.</summary>
    public ResultadoSoltar SoltarEn(CasillaViewModel? destino)
    {
        if (!Arrastrando || _arrastreOrigen is not { } origen) return ResultadoSoltar.Nada;
        var grupo = _arrastreRefs;
        Desplazado = null;
        if (destino?.Ref is not { } d || Misma(origen.Info, d.Info)) { TerminarMano(); return ResultadoSoltar.Nada; }

        if (grupo.Count > 1)
        {
            if (d.Info is not SlotInfoBox caja) { TerminarMano(); _anfitrion?.Informar(IdiomaInterfaz.T("Cajas.VariosPokemonSoloSueltan"), "aviso", "Info"); return new(false, false, origen, d, null, IdiomaInterfaz.T("Cajas.VariosPokemonSoloSueltan")); }
            var refs = grupo.Select(g => Fresca(g.Info)).ToArray();
            TerminarMano();
            var rg = Sesion.Casillas.MoverAHuecos(refs, caja.Box, false);
            if (rg.Correcto) CancelarSeleccion();
            Resultado(rg);
            return new(rg.Correcto, false, origen, d, null, rg.Correcto ? null : rg.Mensaje);
        }

        var desplazado = destino.Ocupada ? Instantanea(destino) : null;
        var o = Fresca(origen.Info); var t = Fresca(d.Info);
        TerminarMano();   // antes de escribir: el refresco que sigue al cambio no debe volver a atenuar el origen
        var r = Sesion.Casillas.Mover(o, t, ModoMover.MoverOIntercambiar);
        Resultado(r);
        if (!r.Correcto) return new(false, false, origen, d, null, r.Mensaje);
        Desplazado = desplazado;
        return new(true, desplazado is not null, origen, d, desplazado, null);
    }

    /// <summary>Se deja el Pokemon donde estaba (soltar fuera de todo destino o cancelar): no se escribe nada.</summary>
    public void CancelarArrastre()
    {
        if (!Arrastrando) return;
        TerminarMano();
    }

    /// <summary>La vista ha terminado de animar: suelta el fantasma, el Pokemon desplazado y las zonas de destino extra.</summary>
    public void LiberarArrastre()
    {
        Fantasma = null;
        Desplazado = null;
        ArrastreCantidad = 0;
        var habia = MostrarApoyoEquipo || MostrarApoyoCaja;
        MostrarApoyoEquipo = false; MostrarApoyoCaja = false;
        if (habia) AplicarMarcas();
    }
}
