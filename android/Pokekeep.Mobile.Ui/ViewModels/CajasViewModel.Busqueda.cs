// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using Pokekeep.Mobile.Ui.Modelo;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;
using PKHeX.Core.Searching;
using Pokekeep.Mobile.Ui.Cajas;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.ViewModels;

// «Buscar en cajas» (EntitySearchSetup de PKHeX): los criterios, la busqueda en todas las cajas y el equipo, las casillas que no coinciden atenuadas y Siguiente/Anterior.
public sealed partial class CajasViewModel
{
    private readonly CriteriosBusqueda _criterios = new();
    private IReadOnlyList<Coincidencia> _coincidencias = [];
    private Func<PKM, bool>? _filtro;
    private int _posicionBusqueda = -1;

    [ObservableProperty] private bool _buscando;
    [ObservableProperty] private string _textoBusqueda = string.Empty;

    public CriteriosBusqueda Criterios => _criterios;
    public int NumeroCoincidencias => _coincidencias.Count;
    public bool HayCoincidencias => _coincidencias.Count > 0;

    partial void OnBuscandoChanged(bool value) => CambioDeEstadoDeCabecera();

    /// <summary>Las casillas visibles que no coinciden se atenuan y las que coinciden se resaltan (el «filtro» de PKHeX).</summary>
    private void AplicarBusqueda() => AplicarBusqueda(TodasLasVisibles);

    private void AplicarBusqueda(IEnumerable<CasillaViewModel> casillas)
    {
        foreach (var c in casillas)
        {
            if (_filtro is null || c.Entidad is null) { c.Atenuada = false; c.Coincide = false; continue; }
            var coincide = _coincidencias.Any(m => c.Ref is { } r && Misma(m.Casilla.Info, r.Info));
            c.Coincide = coincide; c.Atenuada = !coincide;
        }
    }

    [RelayCommand]
    public void AbrirBusqueda()
    {
        // Elegir un Pokemon es navegacion; el selector antiguo solo elegia criterios.
        _filtro = null;
        _coincidencias = BuscadorCajas.Buscar(Sesion.Guardado, _ => true);
        _posicionBusqueda = -1;
        MostrarResultadosBusqueda(conFiltros: true);
    }

    private void MostrarResultadosBusqueda(bool conFiltros = false)
    {
        IReadOnlyList<OpcionSelector> Opciones()
        {
            var opciones = new List<OpcionSelector>();
            if (conFiltros) opciones.Add(new(-1, IdiomaInterfaz.T("BusquedaC15.Filtros")));
            for (var i = 0; i < _coincidencias.Count; i++)
            {
                var m = _coincidencias[i];
                var pk = m.Casilla.Info.Read(Sesion.Guardado);
                var especie = Sesion.Textos.Species[pk.Species];
                opciones.Add(new(i, string.IsNullOrWhiteSpace(pk.Nickname) || pk.Nickname == especie ? especie : $"{pk.Nickname} · {especie}", m.Casilla.Descripcion, pk.Species, pk.Form));
            }
            return opciones;
        }
        SelectorHojaViewModel? selector = null;
        selector = new SelectorHojaViewModel(IdiomaInterfaz.T("Showdown.BuscarPokemon"), Opciones(), _posicionBusqueda, o =>
        {
            if (o.Id < 0) { MostrarFormularioBusqueda(); return; }
            var elegida = _coincidencias[o.Id];
            var texto = SelectorHojaViewModel.Normalizar(selector!.Filtro.Trim());
            if (texto.Length > 0)
            {
                var filtradas = Opciones().Where(o => o.Id >= 0 && SelectorHojaViewModel.Normalizar(o.Texto).Contains(texto)).Select(o => _coincidencias[o.Id]).ToList();
                _coincidencias = filtradas;
                var previo = _filtro;
                _filtro = pk => (previo?.Invoke(pk) ?? true) && SelectorHojaViewModel.Normalizar($"{pk.Nickname} {Sesion.Textos.Species[pk.Species]}").Contains(texto);
                IrAResultadoBusqueda(filtradas.IndexOf(elegida));
            }
            else IrAResultadoBusqueda(o.Id);
        }, IdiomaInterfaz.T("BusquedaC15.EspecieOMote"), actualizarOpciones: Opciones,
            tituloActual: () => IdiomaInterfaz.T("Showdown.BuscarPokemon"), marcadorActual: () => IdiomaInterfaz.T(Design.Movimiento.EscalaTexto >= 1.3 ? "BusquedaC15.EspecieOMoteCorto" : "BusquedaC15.EspecieOMote"));   // en ingles a x2 la frase larga se recortaba en 360 dp (ADR 0105)
        Mostrar(selector);
    }

    private int _siguientePulsoBusqueda;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayAnuncioBusqueda))]
    private string _anuncioBusqueda = string.Empty;
    public bool HayAnuncioBusqueda => !string.IsNullOrWhiteSpace(AnuncioBusqueda);

    /// <summary>Navega sin ejecutar un toque (ni colocar, ni marcar, ni escribir en el guardado).</summary>
    public void IrAResultadoBusqueda(int indice)
    {
        if ((uint)indice >= _coincidencias.Count) return;
        Ocultar();
        CancelarColocacion(); CancelarSeleccion();
        foreach (var c in CasillasCaja.Concat(CasillasEquipo)) c.PulsoBusqueda = 0;
        _posicionBusqueda = indice;
        var m = _coincidencias[indice];
        switch (m.Casilla.Info)
        {
            case SlotInfoBox b:
                if (!EsCajas) EsCajas = true;
                if (b.Box != Caja) CargarCaja(b.Box, seleccionar: false);
                ElegirCasilla(CasillasCaja.ElementAtOrDefault(b.Slot));
                break;
            case SlotInfoParty p:
                if (!EsEquipo) EsEquipo = true;
                ElegirCasilla(CasillasEquipo.ElementAtOrDefault(p.Slot));
                break;
        }
        Buscando = true;
        OnPropertyChanged(nameof(NumeroCoincidencias)); OnPropertyChanged(nameof(HayCoincidencias));
        RefrescarTextosBusqueda();
        if (Seleccionada is { } elegida) elegida.PulsoBusqueda = ++_siguientePulsoBusqueda;
        Design.Haptica.PulsoCorto();
        AplicarBusqueda();
    }

    private void RefrescarTextosBusqueda()
    {
        if (_posicionBusqueda < 0 || _posicionBusqueda >= _coincidencias.Count) return;
        TextoBusqueda = IdiomaInterfaz.F("Cajas.0De1", _posicionBusqueda + 1, _coincidencias.Count);
        AnuncioBusqueda = _coincidencias[_posicionBusqueda].Casilla.Descripcion;
    }

    private void MostrarFormularioBusqueda()
    {
        var t = Sesion.Textos;
        var objetos = Partida.NombresObjetos.Lista(t, Sesion.Guardado);
        string Nombre(IReadOnlyList<string> lista, int i) => i > 0 && i < lista.Count && !string.IsNullOrEmpty(lista[i]) ? lista[i] : IdiomaInterfaz.T("ModeloLotes.Cualquiera");
        string TriTexto(Tri v) => v switch { Tri.Si => IdiomaInterfaz.T("Comun.Si"), Tri.No => IdiomaInterfaz.T("Comun.No"), _ => IdiomaInterfaz.T("Cajas.DaIgual") };
        Tri Ciclo(Tri v) => v switch { Tri.DaIgual => Tri.Si, Tri.Si => Tri.No, _ => Tri.DaIgual };

        EntradaHoja Fila(string titulo, string valor, string glifo, Action accion) => new(titulo, valor, glifo, accion);
        void Selector(string titulo, IReadOnlyList<string> opciones, int actual, Action<int> asignar, int offset = 0, string? cualquiera = null, bool especies = false)
        {
            var lista = new List<OpcionSelector> { new(-1, cualquiera ?? IdiomaInterfaz.T("ModeloLotes.Cualquiera")) };
            for (var i = offset; i < opciones.Count; i++) if (!string.IsNullOrWhiteSpace(opciones[i])) lista.Add(new OpcionSelector(i, opciones[i], null, especies ? i : 0));
            Mostrar(new SelectorHojaViewModel(titulo, lista, actual, o => { asignar(o.Id); MostrarFormularioBusqueda(); }));
        }

        var c = _criterios;
        var general = new List<EntradaHoja>
        {
            Fila(IdiomaInterfaz.T("Cajas.Especie"), c.Especie == 0 ? IdiomaInterfaz.T("ModeloLotes.Cualquiera") : t.Species[c.Especie], "Search", () => Selector(IdiomaInterfaz.T("Cajas.Especie"), t.Species, c.Especie == 0 ? -1 : c.Especie, i => c.Especie = (ushort)Math.Max(0, i), 1, especies: true)),
            Fila(IdiomaInterfaz.T("Cajas.ObjetoEquipado"), Nombre(objetos, c.Objeto), "Package", () => Selector(IdiomaInterfaz.T("Cajas.ObjetoEquipado"), objetos, c.Objeto, i => c.Objeto = i, 1)),
            Fila(IdiomaInterfaz.T("Informe.Columna.Ability"), Nombre(t.Ability, c.Habilidad), "Sparkles", () => Selector(IdiomaInterfaz.T("Informe.Columna.Ability"), t.Ability, c.Habilidad, i => c.Habilidad = i, 1)),
            Fila(IdiomaInterfaz.T("Informe.Columna.Nature"), c.Naturaleza.IsFixed ? t.Natures[(int)c.Naturaleza] : IdiomaInterfaz.T("ModeloLotes.Cualquiera"), "Star", () => Selector(IdiomaInterfaz.T("Informe.Columna.Nature"), t.Natures, c.Naturaleza.IsFixed ? (int)c.Naturaleza : -1, i => c.Naturaleza = i < 0 ? Nature.Random : (Nature)i)),
            Fila(IdiomaInterfaz.T("Informe.Columna.Version"), c.Version.IsValidSavedVersion() ? NombreVersion(c.Version) : IdiomaInterfaz.T("ModeloLotes.Cualquiera"), "Smartphone", SelectorVersion),
            Fila(IdiomaInterfaz.T("Cajas.Movimiento"), Nombre(t.Move, c.Movimientos[0]), "Swords", () => Selector(IdiomaInterfaz.T("Cajas.Movimiento"), t.Move, c.Movimientos[0] == 0 ? -1 : c.Movimientos[0], i => c.Movimientos[0] = (ushort)Math.Max(0, i), 1)),
            Fila(IdiomaInterfaz.T("Cajas.Movimiento2"), Nombre(t.Move, c.Movimientos[1]), "Swords", () => Selector(IdiomaInterfaz.T("Cajas.Movimiento2"), t.Move, c.Movimientos[1] == 0 ? -1 : c.Movimientos[1], i => c.Movimientos[1] = (ushort)Math.Max(0, i), 1)),
            Fila(IdiomaInterfaz.T("Cajas.Movimiento3"), Nombre(t.Move, c.Movimientos[2]), "Swords", () => Selector(IdiomaInterfaz.T("Cajas.Movimiento3"), t.Move, c.Movimientos[2] == 0 ? -1 : c.Movimientos[2], i => c.Movimientos[2] = (ushort)Math.Max(0, i), 1)),
            Fila(IdiomaInterfaz.T("Cajas.Movimiento4"), Nombre(t.Move, c.Movimientos[3]), "Swords", () => Selector(IdiomaInterfaz.T("Cajas.Movimiento4"), t.Move, c.Movimientos[3] == 0 ? -1 : c.Movimientos[3], i => c.Movimientos[3] = (ushort)Math.Max(0, i), 1)),
            Fila(IdiomaInterfaz.T("Catalogo.Variocolor"), TriTexto(c.Variocolor), "Sparkles", () => { c.Variocolor = Ciclo(c.Variocolor); MostrarFormularioBusqueda(); }),
            Fila(IdiomaInterfaz.T("Informe.Columna.IsEgg"), TriTexto(c.Huevo), "Box", () => { c.Huevo = Ciclo(c.Huevo); MostrarFormularioBusqueda(); }),
            Fila(IdiomaInterfaz.T("Informe.Columna.Legal"), TriTexto(c.Legal), "ShieldCheck", () => { c.Legal = Ciclo(c.Legal); MostrarFormularioBusqueda(); }),
        };
        var avanzado = new List<EntradaHoja>
        {
            Fila(IdiomaInterfaz.T("Cajas.GeneracionOrigen"), c.Generacion == 0 ? IdiomaInterfaz.T("ModeloLotes.Cualquiera") : IdiomaInterfaz.F("Cajas.Generacion", c.Generacion), "Layers", () =>
            {
                var gens = new List<OpcionSelector> { new(0, IdiomaInterfaz.T("ModeloLotes.Cualquiera")) };
                for (var g = 1; g <= 9; g++) gens.Add(new OpcionSelector(g, IdiomaInterfaz.F("Cajas.Generacion", g)));
                Mostrar(new SelectorHojaViewModel(IdiomaInterfaz.T("Cajas.GeneracionOrigen"), gens, c.Generacion, o => { c.Generacion = (byte)o.Id; if (o.Id != 0) c.Version = default; MostrarFormularioBusqueda(); }));
            }),
            Fila(IdiomaInterfaz.T("Cajas.Nivel"), c.Nivel == 0 ? IdiomaInterfaz.T("ModeloLotes.Cualquiera") : $"{TextoComparacion(c.ComparaNivel)} {c.Nivel}", "Clock", SelectorNivel),
            Fila(IdiomaInterfaz.T("Cajas.PotencialIV"), TextoIvs(c.Ivs), "SlidersHorizontal", () => SelectorLista(IdiomaInterfaz.T("Cajas.PotencialIV"), [IdiomaInterfaz.T("ModeloLotes.Cualquiera"), IdiomaInterfaz.T("Cajas.Menos"), "91 a 120", "121 a 150", "151 a 179", IdiomaInterfaz.T("Cajas.180OMas"), IdiomaInterfaz.T("Cajas.Todos")], c.Ivs, i => c.Ivs = i)),
            Fila(IdiomaInterfaz.T("Cajas.EntrenamientoEV"), TextoEvs(c.Evs), "SlidersHorizontal", () => SelectorLista(IdiomaInterfaz.T("Cajas.EntrenamientoEV"), [IdiomaInterfaz.T("ModeloLotes.Cualquiera"), IdiomaInterfaz.T("Cajas.Ninguno"), IdiomaInterfaz.T("Cajas.Algunos"), IdiomaInterfaz.T("Cajas.Mitad"), IdiomaInterfaz.T("Cajas.Completo")], c.Evs, i => c.Evs = i)),
            Fila(IdiomaInterfaz.T("Informe.Columna.HP_Type"), c.TipoPotencialOculto < 0 ? IdiomaInterfaz.T("ModeloLotes.Cualquiera") : t.Types[c.TipoPotencialOculto], "Sparkles", () => Selector(IdiomaInterfaz.T("Informe.Columna.HP_Type"), t.Types, c.TipoPotencialOculto, i => c.TipoPotencialOculto = i)),
            Fila("Clones", c.Clones == CloneDetectionMethod.None ? IdiomaInterfaz.T("Cajas.Buscar") : IdiomaInterfaz.T("Cajas.SoloRepetidos"), "Copy", () => { c.Clones = c.Clones == CloneDetectionMethod.None ? CloneDetectionMethod.HashDetails : CloneDetectionMethod.None; MostrarFormularioBusqueda(); }),
        };
        var acciones = new List<EntradaHoja>
        {
            new(IdiomaInterfaz.T("Comun.Buscar"), HayFiltro ? IdiomaInterfaz.T("Cajas.ResaltaCoincidenciasTodasCajas") : IdiomaInterfaz.T("Cajas.ResaltaCoincidencias"), "Search", EjecutarBusqueda),
            new(IdiomaInterfaz.T("Form.Rtc.Reiniciar"), IdiomaInterfaz.T("Cajas.QuitaTodosCriterios"), "RefreshCw", () => { c.Reiniciar(); QuitarBusqueda(); MostrarFormularioBusqueda(); }),
        };
        Mostrar(new MenuHojaViewModel(IdiomaInterfaz.T("Cajas.BuscarCajas"), [new SeccionHoja(IdiomaInterfaz.T("Cajas.General"), general), new SeccionHoja(IdiomaInterfaz.T("Ajustes.Pkhex.Grupo.22"), avanzado), new SeccionHoja(null, acciones)],
            IdiomaInterfaz.T("Cajas.BuscaTodasCajasEquipo"), pie: Buscando ? IdiomaInterfaz.F("Cajas.AhoraCoincidencias", _coincidencias.Count) : null));
    }

    private bool HayFiltro => !_criterios.EstaVacio;

    private string NombreVersion(GameVersion v) => NombresJuego.Corto(v);

    private void SelectorVersion()
    {
        var lista = new List<OpcionSelector> { new(0, IdiomaInterfaz.T("ModeloLotes.Cualquiera")) };
        foreach (var v in GameInfo.Sources.VersionDataSource.Where(x => x.Value > 0)) lista.Add(new OpcionSelector(v.Value, NombresJuego.Corto((GameVersion)v.Value)));
        Mostrar(new SelectorHojaViewModel(IdiomaInterfaz.T("Informe.Columna.Version"), lista, (int)_criterios.Version, o => { _criterios.Version = (GameVersion)o.Id; if (o.Id != 0) _criterios.Generacion = 0; MostrarFormularioBusqueda(); }));
    }

    private void SelectorLista(string titulo, string[] textos, int actual, Action<int> asignar)
        => Mostrar(new SelectorHojaViewModel(titulo, [.. textos.Select((x, i) => new OpcionSelector(i, x))], actual, o => { asignar(o.Id); MostrarFormularioBusqueda(); }));

    private static string TextoComparacion(SearchComparison s) => s switch { SearchComparison.Equals => IdiomaInterfaz.T("Cajas.Igual"), SearchComparison.GreaterThanEquals => IdiomaInterfaz.T("Cajas.Menos2"), SearchComparison.LessThanEquals => IdiomaInterfaz.T("Cajas.Mucho"), _ => IdiomaInterfaz.T("ModeloLotes.Cualquiera") };
    private static string TextoIvs(int i) => i switch { 1 => IdiomaInterfaz.T("Cajas.Menos"), 2 => "91 a 120", 3 => "121 a 150", 4 => "151 a 179", 5 => IdiomaInterfaz.T("Cajas.180OMas"), 6 => IdiomaInterfaz.T("Cajas.Todos"), _ => IdiomaInterfaz.T("ModeloLotes.Cualquiera") };
    private static string TextoEvs(int i) => i switch { 1 => IdiomaInterfaz.T("Cajas.Ninguno"), 2 => IdiomaInterfaz.T("Cajas.Algunos"), 3 => IdiomaInterfaz.T("Cajas.Mitad"), 4 => IdiomaInterfaz.T("Cajas.Completo"), _ => IdiomaInterfaz.T("ModeloLotes.Cualquiera") };

    private void SelectorNivel()
    {
        var c = _criterios;
        var opciones = new List<OpcionSelector> { new(0, IdiomaInterfaz.T("Cajas.CualquierNivel")) };
        for (var n = 1; n <= 100; n++) opciones.Add(new OpcionSelector(n, IdiomaInterfaz.F("Cajas.Nivel2", n)));
        Mostrar(new SelectorHojaViewModel(IdiomaInterfaz.T("Cajas.Nivel"), opciones, c.Nivel, o =>
        {
            c.Nivel = (byte)o.Id;
            if (o.Id == 0) { c.ComparaNivel = SearchComparison.None; MostrarFormularioBusqueda(); return; }
            Mostrar(new SelectorHojaViewModel(IdiomaInterfaz.T("Cajas.CompararNivel"), [new(1, IdiomaInterfaz.T("Cajas.Igual")), new(2, IdiomaInterfaz.T("Cajas.Menos2")), new(3, IdiomaInterfaz.T("Cajas.Mucho"))], (int)c.ComparaNivel, k => { c.ComparaNivel = (SearchComparison)k.Id; MostrarFormularioBusqueda(); }, IdiomaInterfaz.T("Cajas.Elegir")));
        }));
    }

    /// <summary>Aplica los criterios: busca en todas las cajas y el equipo, atenua lo que no coincide y ofrece resultados para elegir.</summary>
    public void EjecutarBusqueda()
    {
        AnuncioBusqueda = string.Empty;
        foreach (var c in CasillasCaja.Concat(CasillasEquipo)) c.PulsoBusqueda = 0;
        if (_criterios.EstaVacio) { QuitarBusqueda(); Resultado(InformeOperacion.Falla(TipoResultado.SinCambios, IdiomaInterfaz.T("Cajas.EligeMenosCriterioBuscar"))); return; }
        var malas = _criterios.InstruccionesInvalidas();
        if (malas.Count > 0) { Resultado(InformeOperacion.Falla(TipoResultado.Fallo, IdiomaInterfaz.F("Cajas.EntiendoInstruccionAvanzada", malas[0]))); return; }
        _filtro = _criterios.Predicado(Sesion.Guardado);
        _coincidencias = BuscadorCajas.Buscar(Sesion.Guardado, _filtro);
        _posicionBusqueda = -1;
        Buscando = true;
        OnPropertyChanged(nameof(NumeroCoincidencias)); OnPropertyChanged(nameof(HayCoincidencias));
        TextoBusqueda = _coincidencias.Count == 0 ? IdiomaInterfaz.T("Cajas.Coincidencias") : _coincidencias.Count == 1 ? IdiomaInterfaz.T("Cajas.Coincidencia") : IdiomaInterfaz.F("Cajas.Coincidencias2", _coincidencias.Count);
        AplicarBusqueda();
        if (_coincidencias.Count > 0) MostrarResultadosBusqueda();
        else Resultado(InformeOperacion.Falla(TipoResultado.SinCambios, IdiomaInterfaz.T("Cajas.NingunPokemonCumplaCriterios")));
    }

    [RelayCommand]
    public void SiguienteCoincidencia() => SaltarACoincidencia(+1);

    [RelayCommand]
    public void AnteriorCoincidencia() => SaltarACoincidencia(-1);

    private void SaltarACoincidencia(int paso)
    {
        if (_coincidencias.Count == 0) return;
        IrAResultadoBusqueda(((_posicionBusqueda + paso) % _coincidencias.Count + _coincidencias.Count) % _coincidencias.Count);
    }

    [RelayCommand]
    public void QuitarBusqueda()
    {
        AnuncioBusqueda = string.Empty;
        foreach (var c in CasillasCaja.Concat(CasillasEquipo)) c.PulsoBusqueda = 0;
        _filtro = null; _coincidencias = []; _posicionBusqueda = -1; Buscando = false; TextoBusqueda = string.Empty;
        OnPropertyChanged(nameof(NumeroCoincidencias)); OnPropertyChanged(nameof(HayCoincidencias));
        AplicarBusqueda();
    }
}
