// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Cajas;
using Pokekeep.Mobile.Ui.Herramientas.Showdown;
using Pokekeep.Mobile.Ui.Editor;
using Pokekeep.Mobile.Ui.Almacen;
using Pokekeep.Mobile.Ui.Modelo;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.ViewModels;

/// <summary>
/// Editor de entidad a pantalla completa (paridad de <c>PKMEditor</c> de PKHeX): seis pestanas (Principal, Encuentro, Estadisticas, Movimientos, Cosmetico, EO y varios) construidas del catalogo de campos
/// por formato, subeditores en hoja inferior y un borrador (clon del Pokemon de la casilla) al que se escribe por el <see cref="ModeloEntidad"/>. «Aplicar a la casilla» lleva el borrador a la sesion por el
/// historial del Core (un solo Deshacer, regla 13: nada se exporta). Sin acciones del motor, sin portapapeles o sin documentos, esas funciones lo dicen en lugar de ofrecer un boton muerto.
/// </summary>
public sealed partial class EditorViewModel : ObservableObject, IAnfitrionEditor
{
    private readonly Sesion _sesion;
    private readonly CasillaRef _casilla;
    private readonly Action<InformeOperacion>? _aplicado;
    private readonly Action? _cerrar;
    private readonly IAccionesPokekeep? _acciones;
    private readonly Action<HojaViewModel>? _abrirHoja;
    private readonly Func<IPortapapeles?> _portapapeles;
    private readonly Func<IAlmacenDocumentos?> _documentos;
    private SubeditorHojaViewModel? _subeditor;
    private int _versionLegalidad;

    /// <summary>Los ajustes de PKHeX VIGENTES (formato de Showdown y del resumen, bloque 10-9); sin ellos, los de fábrica.</summary>
    public Func<Ajustes.Pkhex.AjustesPkhex>? AjustesDePkhex { get; set; }
    private Ajustes.Pkhex.AjustesPkhex AjustesActuales => AjustesDePkhex?.Invoke() ?? new Ajustes.Pkhex.AjustesPkhex();
    // ADR 0074, D1 aprobada por el propietario el 14-oct: HaX permite editar fuera de rango, pero conserva el indicador y el informe de legalidad.
    public bool LegalidadVisible => true;
    public void ActualizarIdioma()
    {
        Modelo.ActualizarIdioma();
        ActualizarCabecera();
        // Releer ya respeta EnFoco/Pendiente: no aplicar ni perder texto a medio escribir.
        foreach (var campo in Activas.SelectMany(s => s.TodosLosCampos)) campo.Releer();
    }
    public void ActualizarPresentacion()
    {
        OnPropertyChanged(nameof(Modo));
        ActualizarCabecera();
        foreach (var campo in Activas.SelectMany(s => s.TodosLosCampos)) { campo.Releer(); campo.RefrescarColores(); }
    }
    /// <summary>La hoja de compartir del sistema (bloque 10-9); sin ella «Compartir» lo dice.</summary>
    public Func<ICompartirSistema?>? Compartir { get; set; }

    public EditorViewModel(Sesion sesion, PKM original, CasillaRef casilla, Action? cerrar = null, Action<InformeOperacion>? aplicado = null,
        IAccionesPokekeep? acciones = null, Action<HojaViewModel>? abrirHoja = null, Func<IPortapapeles?>? portapapeles = null, Func<IAlmacenDocumentos?>? documentos = null)
    {
        _sesion = sesion; _casilla = casilla; Caja = casilla.Caja; _cerrar = cerrar; _aplicado = aplicado; _acciones = acciones; _abrirHoja = abrirHoja;
        _portapapeles = portapapeles ?? (() => null); _documentos = documentos ?? (() => null);
        PuedeAplicar = sesion.Casillas.EsEscribible(casilla.Info);
        Original = original;
        Modelo = new ModeloEntidad(original.Clone(), sesion.Guardado) { Anfitrion = this };
        _alCambiarMovimiento = AlCambiarMovimiento;
        Design.Movimiento.SuscribirDebil(_alCambiarMovimiento);
        ConstruirPestanas();
        ActualizarCabecera();
        Reevaluar();
        Modelo.Cambiado += Tras;
    }

    private readonly Action _alCambiarMovimiento;   // referencia fuerte de la suscripcion debil a Movimiento.Cambiado
    public ModeloEntidad Modelo { get; }
    public PKM Original { get; }
    public PKM Borrador => Modelo.Borrador;
    public int VersionJuego => (int)Design.Sprites.VersionDe(_sesion.Guardado);
    public Pokekeep.Sprites.ModoSprites Modo => _sesion.ModoSprites;
    public int Caja { get; }
    public bool PuedeAplicar { get; }
    public bool SoloLectura => !PuedeAplicar;
    public string TextoSoloLectura => IdiomaInterfaz.T("Editor.CasillaSoloLecturaCambios");

    // ---- Cabecera (el Pokemon que se esta editando; cambia con la especie, la forma, el sexo...) ----
    [ObservableProperty] private string _nombreEspecie = string.Empty;
    [ObservableProperty] private string _subtitulo = string.Empty;
    /// <summary>Los tipos de la especie como etiquetas de color (cabecera de la ficha) y el juego de la partida a su lado; <see cref="Subtitulo"/> es lo mismo en texto, para el lector de pantalla y el menú.</summary>
    [ObservableProperty] private IReadOnlyList<TipoVm> _tipos = [];
    [ObservableProperty] private string _juego = string.Empty;
    [ObservableProperty] private int _contexto;
    [ObservableProperty] private int _especie;
    [ObservableProperty] private int _forma;
    [ObservableProperty] private uint _argumentoForma;
    [ObservableProperty] private int _genero;
    [ObservableProperty] private bool _brillante;
    [ObservableProperty] private bool _huevo;

    // ---- Estado y mensajes ----
    [ObservableProperty] private bool _hayCambios;
    [ObservableProperty] private string _mensaje = string.Empty;
    [ObservableProperty] private string _claseMensaje = "aviso";
    [ObservableProperty] private EstadoLegal _estado;
    [ObservableProperty] private string _textoEstado = string.Empty;
    [ObservableProperty] private string _claseEstado = string.Empty;
    [ObservableProperty] private string _glifoEstado = "Info";
    public bool HayMensaje => !string.IsNullOrEmpty(Mensaje);
    public bool EsMensajeOk => ClaseMensaje == "ok";
    public bool EsMensajeError => ClaseMensaje == "error";
    public bool EsMensajeAviso => ClaseMensaje == "aviso";
    partial void OnClaseMensajeChanged(string value) { OnPropertyChanged(nameof(EsMensajeOk)); OnPropertyChanged(nameof(EsMensajeError)); OnPropertyChanged(nameof(EsMensajeAviso)); }
    public bool EsLegal => Estado == EstadoLegal.Legal;
    public bool EsIlegal => Estado == EstadoLegal.Ilegal;
    public bool Avanzado => Modelo.Fuentes.Avanzado;
    partial void OnMensajeChanged(string value) => OnPropertyChanged(nameof(HayMensaje));
    partial void OnEstadoChanged(EstadoLegal value) { OnPropertyChanged(nameof(EsLegal)); OnPropertyChanged(nameof(EsIlegal)); }

    // Con texto grande (x1,3 o mas) las etiquetas de la barra se acortan: las completas partirian palabras. El nombre accesible sigue siendo el completo.
    private static bool EtiquetasCortas => Design.Movimiento.EscalaTexto >= 1.3;
    public string TextoLegalizar => EtiquetasCortas ? IdiomaInterfaz.T("Editor.Legalizar") : IdiomaInterfaz.T("Cajas.LegalizarAutomaticamente");
    public string TextoOptimizar => EtiquetasCortas ? IdiomaInterfaz.T("Editor.Optimizar") : IdiomaInterfaz.T("Editor.OptimizarStats");
    public string TextoAplicar => EtiquetasCortas ? IdiomaInterfaz.T("Editor.Aplicar") : IdiomaInterfaz.T("Editor.AplicarCasilla");
    private void AlCambiarMovimiento() { OnPropertyChanged(nameof(TextoLegalizar)); OnPropertyChanged(nameof(TextoOptimizar)); OnPropertyChanged(nameof(TextoAplicar)); }

    // ---- Pestanas y secciones ----
    public ObservableCollection<PestanaVm> Pestanas { get; } = [];
    public ObservableCollection<SeccionBaseVm> Secciones { get; } = [];
    [ObservableProperty] private PestanaVm? _pestanaActual;

    public bool EnStats
    {
        get => PestanaActual?.Id == "estadisticas";
        set { if (value) SeleccionarPorId("estadisticas"); }
    }

    private void ConstruirPestanas()
    {
        var anterior = PestanaActual?.Id ?? "principal";
        Pestanas.Clear();
        foreach (var p in Modelo.PestanasVisibles) Pestanas.Add(new PestanaVm(p.Id, p.Titulo, this));
        SeleccionarPorId(Pestanas.Any(p => p.Id == anterior) ? anterior : "principal");
    }

    /// <summary>El campo del editor con esa clave (id del censo, p. ej. <c>PK-ED-018</c>); si esta en otra pestana, la abre. Sirve a las pruebas y a las capturas de dispositivo.</summary>
    public CampoVm? Campo(string clave)
    {
        CampoVm? Buscar() => Activas.SelectMany(s => s.TodosLosCampos).FirstOrDefault(c => c.Clave == clave && c.Visible);
        if (Buscar() is { } c) return c;
        foreach (var p in Pestanas.ToList())
        {
            SeleccionarPestana(p);
            if (Buscar() is { } d) return d;
        }
        return null;
    }

    /// <summary>Abre el selector de un campo por su clave (las pruebas lo usan en lugar de tocar la fila).</summary>
    public bool ElegirCampo(string clave)
    {
        if (Campo(clave) is not { } c) return false;
        AbrirSelector(c);
        return true;
    }

    public void SeleccionarPorId(string id)
    {
        var p = Pestanas.FirstOrDefault(x => x.Id == id) ?? Pestanas.FirstOrDefault();
        if (p is not null) SeleccionarPestana(p);
    }

    public void SeleccionarPestana(PestanaVm p)
    {
        if (PestanaActual?.Id == p.Id && Secciones.Count > 0) { p.Fijar(true); return; }
        foreach (var x in Pestanas) x.Fijar(x.Id == p.Id);
        PestanaActual = p;
        ReconstruirSecciones();
        OnPropertyChanged(nameof(EnStats));
    }

    private void ReconstruirSecciones()
    {
        Secciones.Clear();
        var def = ModeloEntidad.Catalogo.First(x => x.Id == PestanaActual!.Id);
        foreach (var s in def.Secciones)
        {
            if (!s.EsVisible(Modelo.Contexto)) continue;
            var vm = ConstruirSeccion(s);
            vm.Releer();
            ReleerEspecial(vm);
            if (vm.Visible) Secciones.Add(vm);
        }
    }

    private SeccionBaseVm ConstruirSeccion(SeccionDef s)
    {
        if (s.Campos.Any(c => c.Grupo == "iv")) return ConstruirStats(s);
        if (s.Campos.Any(c => c.Grupo == "mov")) return ConstruirMovs(s);
        var plana = new SeccionPlanaVm { Titulo = s.Titulo };
        foreach (var c in s.Campos) plana.Campos.Add(new CampoVm(this, c));
        return plana;
    }

    private SeccionStatsVm ConstruirStats(SeccionDef s)
    {
        var v = new SeccionStatsVm { Titulo = string.Empty };
        var pk = Modelo.Borrador;
        CampoVm? Buscar(string grupo, int i) => s.Campos.FirstOrDefault(c => c.Grupo == grupo && c.Indice == i) is { } d ? new CampoVm(this, d) : null;
        foreach (var i in Catalogo.OrdenStats(pk))
            v.Filas.Add(new FilaStatVm(i, Catalogo.NombreStat(pk, i)) { Final = Buscar("final", i), Iv = Buscar("iv", i), Ev = Buscar("ev", i), Av = Buscar("av", i), Gv = Buscar("gv", i), Ht = Buscar("ht", i), Base = Buscar("base", i) });
        foreach (var c in s.Campos.Where(c => c.Grupo is null)) v.Resto.Add(new CampoVm(this, c));
        return v;
    }

    private SeccionMovsVm ConstruirMovs(SeccionDef s)
    {
        var v = new SeccionMovsVm { Titulo = s.Titulo };
        CampoVm? Buscar(string grupo, int i) => s.Campos.FirstOrDefault(c => c.Grupo == grupo && c.Indice == i) is { } d ? new CampoVm(this, d) : null;
        for (var i = 0; i < 4; i++) v.Filas.Add(new FilaMovVm(i, Buscar("mov", i)!, Buscar("pp", i), Buscar("ppups", i)));
        return v;
    }

    /// <summary>Datos de una seccion que no son campos: la senal de la naturaleza, las columnas visibles y el aviso de EV de la tabla de estadisticas.</summary>
    private void ReleerEspecial(SeccionBaseVm s)
    {
        if (s is not SeccionStatsVm t) return;
        var pk = Modelo.Borrador;
        var stats = pk.GetStats(pk.PersonalInfo);
        var nat = pk.Format >= 8 ? pk.StatAlignment : pk.Nature;
        var (sube, baja) = pk.Format >= 3 ? nat.GetNatureModification() : (-1, -1);
        foreach (var f in t.Filas)
        {
            f.FinalTexto = Modelo.Contexto.StatsManuales && f.Final is not null ? f.Final.Valor : stats[f.Indice].ToString();
            var natIdx = f.Indice switch { 1 => 0, 2 => 1, 3 => 2, 4 => 3, 5 => 4, _ => -1 };
            f.Senal = natIdx >= 0 && sube != baja ? (natIdx == sube ? "+" : natIdx == baja ? "−" : "") : "";
        }
        t.PorEscala = Design.Movimiento.EscalaTexto >= 1.3;
        t.HayEv = t.Filas.Any(f => f.Ev?.Visible == true);
        t.HayAv = t.Filas.Any(f => f.Av?.Visible == true);
        t.HayGv = t.Filas.Any(f => f.Gv?.Visible == true);
        t.HayHt = t.Filas.Any(f => f.Ht?.Visible == true);
        t.Recalcular();   // la columna IV no tiene propiedad propia: cambia con las filas
        t.HayFinalEditable = Modelo.Contexto.StatsManuales;
        var ev = pk.EVTotal;
        t.HayAvisoEv = pk.Format >= 3 && t.HayEv && ev > EffortValues.Max510;
        t.AvisoEv = t.HayAvisoEv ? IdiomaInterfaz.F("Editor.EVTotalesMaximoJuego", ev, EffortValues.Max510) : string.Empty;
    }

    private IEnumerable<SeccionBaseVm> Activas => _subeditor is null ? Secciones : Secciones.Concat(_subeditor.Secciones);

    /// <summary>Tras una escritura: relee los campos visibles, la cabecera y la legalidad.</summary>
    private void Tras()
    {
        HayCambios = Modelo.HayCambios;
        foreach (var s in Activas.ToList()) { s.Releer(); ReleerEspecial(s); }
        // Una seccion o pestana puede aparecer o desaparecer (Pokerus, forma, huevo...): se comprueba sin reconstruir mientras haya texto en edicion.
        ActualizarCabecera();
        Reevaluar();
    }

    /// <summary>Reconstruye lo visible desde cero (cambio de modo avanzado, importar, accion del motor); solo si no hay texto en edicion.</summary>
    private void Reconstruir()
    {
        HayCambios = Modelo.HayCambios;
        ConstruirPestanas();
        _subeditor?.Reconstruir();
        ActualizarCabecera();
        Reevaluar();
    }

    private void ActualizarCabecera()
    {
        var pk = Modelo.Borrador;
        var t = _sesion.Textos;
        NombreEspecie = pk.Species < t.Species.Count ? t.Species[pk.Species] : "—";
        Especie = pk.Species; Forma = pk.Form; Genero = pk.Gender; Brillante = pk.IsShiny; Huevo = pk.IsEgg; Contexto = (int)pk.Context;
        ArgumentoForma = pk is IFormArgument f ? f.FormArgument : 0;
        OnPropertyChanged(nameof(Modo));
        Tipos = TiposUi.Etiquetas(pk, t);
        Juego = NombresJuego.Corto(_sesion.Guardado.Version);
        Subtitulo = $"{string.Join(" / ", TiposUi.De(pk, t))} · {Juego}";
    }

    private void Reevaluar()
    {
        var copia = Modelo.Borrador.Clone();
        void Poner(EstadoLegal e)
        {
            Estado = e; TextoEstado = LegalidadUi.Texto(e); ClaseEstado = LegalidadUi.ClaseInsignia(e); GlifoEstado = LegalidadUi.Glifo(e);
        }
        if (!AnalizadorLegalidad.Asincrono) { Poner(LegalidadUi.Evaluar(copia)); return; }
        var version = ++_versionLegalidad;
        _ = Task.Run(() =>
        {
            var e = LegalidadUi.Evaluar(copia);
            Avalonia.Threading.Dispatcher.UIThread.Post(() => { if (version == _versionLegalidad) Poner(e); });
        });
    }

    // ---- Escrituras desde los campos ----
    public void EscribirTexto(CampoVm c, string texto, bool final)
    {
        var def = c.Def;
        var inmediato = def.Tipo is TipoCampo.Texto or TipoCampo.Entero or TipoCampo.Barra;
        if (!inmediato && !final) { c.MarcarPendiente(true); return; }
        if (def.Tipo == TipoCampo.Entero && string.IsNullOrWhiteSpace(texto)) { c.PonerError(null); return; }
        var error = Modelo.Escribir(def, texto);
        c.PonerError(error);
        if (error is null) c.MarcarPendiente(false);
    }

    public void EscribirMarca(CampoVm c, bool valor)
    {
        var error = Modelo.EscribirMarca(c.Def, valor);
        c.PonerError(error);
        if (error is not null) Tras();
    }

    public void EscribirId(CampoVm c, int id)
    {
        var error = Modelo.EscribirId(c.Def, id);
        c.PonerError(error);
        if (error is not null) Informar(error, "error");
        if (error is null && c.Def.Id is "PK-ED-012" or "PK-ED-020" or "PK-ED-026" or "PK-ED-032") Reconstruir();
    }

    public void AbrirSelector(CampoVm c)
    {
        if (_abrirHoja is null || c.Motivo is not null) return;
        var esEspecie = c.Def.Clave == "PK-ED-012";
        IReadOnlyList<OpcionSelector> Opciones() => [.. Modelo.Opciones(c.Def).Select(o => new OpcionSelector(o.Id, o.Texto, o.Detalle, esEspecie ? o.Id : 0, 0, null, o.Tipo, o.TipoTexto))];
        var etiqueta = c.Etiqueta;
        _abrirHoja(new SelectorHojaViewModel(etiqueta, Opciones(), c.Def.SoloAccion ? int.MinValue : Modelo.IdActual(c.Def), o => EscribirId(c, o.Id), IdiomaInterfaz.F("Editor.Buscar", etiqueta), Opciones));
    }

    public void EjecutarAccion(CampoVm c)
    {
        if (c.Motivo is not null) return;
        var error = Modelo.Ejecutar(c.Def);
        if (error is not null) Informar(error, "error");
        if (c.Def.Id is "PK-ED-010" or "PK-ED-009") Reconstruir();
    }

    public void Informar(string texto, string clase = "aviso") { ClaseMensaje = clase; Mensaje = texto; }

    [RelayCommand] private void CerrarMensaje() => Mensaje = string.Empty;

    // ---- IAnfitrionEditor: lo que los campos piden a la pantalla ----
    void IAnfitrionEditor.AbrirSubeditor(string id) => AbrirSubeditor(id, "mote");
    void IAnfitrionEditor.Aviso(string texto) => Informar(texto, "aviso");
    void IAnfitrionEditor.IrAPestana(string id) => SeleccionarPorId(id);

    public void AbrirSubeditor(string id, string objetivo)
    {
        var def = Subeditores.Crear(id, Modelo.Contexto, objetivo);
        if (def is null) { Informar(IdiomaInterfaz.T("Editor.PokemonTieneVentana"), "aviso"); return; }
        if (_abrirHoja is null) return;
        var hoja = new SubeditorHojaViewModel(this, def, id, objetivo);
        _subeditor = hoja;
        hoja.CierreSolicitado += () => { if (ReferenceEquals(_subeditor, hoja)) _subeditor = null; Tras(); };
        _abrirHoja(hoja);
    }

    /// <summary>Los caracteres especiales se anaden al texto de mote, EO o entrenador actual.</summary>
    public void InsertarGlifo(string objetivo, string glifo)
    {
        var clave = objetivo switch { "eo" => "PK-ED-084", "ea" => "PK-ED-089", _ => "PK-ED-014" };
        var def = Modelo.Buscar(clave);
        if (def is null) return;
        var error = Modelo.Escribir(def, Modelo.Valor(def) + glifo);
        if (error is not null) Informar(error, "error");
    }

    /// <summary>Menu «mas» (los tres puntos): importar, exportar, legalidad, modo avanzado.</summary>
    [RelayCommand]
    private void Menu()
    {
        if (_abrirHoja is null) return;
        var portapapeles = _portapapeles() is null ? IdiomaInterfaz.T("Editor.PortapapelesDisponible") : null;
        var docs = _documentos() is null ? IdiomaInterfaz.T("Editor.ArchivosEstanDisponiblesAqui") : null;
        var motivoCompartir = Compartir?.Invoke() is null ? IdiomaInterfaz.T("Cajas.CompartirDisponibleAqui") : null;
        _abrirHoja(new MenuHojaViewModel(IdiomaInterfaz.T("Cajas.Opciones"), [
            new SeccionHoja(IdiomaInterfaz.T("Form.Fama3.Importar"),
            [
                new(IdiomaInterfaz.T("Cajas.SetShowdown"), IdiomaInterfaz.T("Editor.PegaSetPortapapelesSobre"), "Clipboard", () => _ = ImportarShowdownAsync(), Motivo: portapapeles),
                new(IdiomaInterfaz.T("Editor.ArchivoPkEk"), IdiomaInterfaz.T("Editor.CargaPokemonGuardadoArchivo"), "FileUp", () => _ = ImportarArchivoAsync(), Motivo: docs),
                new(IdiomaInterfaz.T("Editor.CodigoQRImagen"), IdiomaInterfaz.T("Editor.EligeCapturaFotoQR"), "QrCode", () => _ = ImportarQrAsync(), Motivo: docs),
            ]),
            new SeccionHoja(IdiomaInterfaz.T("Editor.Exportar"),
            [
                new(IdiomaInterfaz.T("Cajas.SetShowdown"), IdiomaInterfaz.T("Editor.CopiaTextoPortapapeles"), "ClipboardCopy", () => _ = ExportarShowdownAsync(), Motivo: portapapeles),
                new(IdiomaInterfaz.T("Editor.ArchivoPk"), IdiomaInterfaz.T("Editor.GuardaPokemonSitioElijas"), "FileDown", () => _ = ExportarArchivoAsync(false), Motivo: docs),
                .. (Modelo.Borrador.Format >= 3 ? new[] { new EntradaHoja(IdiomaInterfaz.T("Editor.ArchivoCifradoEk"), IdiomaInterfaz.T("Cajas.MismoPokemonCifradoGuarda"), "FileDown", () => _ = ExportarArchivoAsync(true), Motivo: docs) } : []),
                new(IdiomaInterfaz.T("Cajas.CodigoQR"), IdiomaInterfaz.T("Cajas.MuestraCodigoQRPokemon"), "QrCode", MostrarQr),
                new(IdiomaInterfaz.T("Editor.Resumen"), IdiomaInterfaz.T("Editor.TextoDatosPokemon"), "FileText", MostrarResumen),
            ]),
            new SeccionHoja(IdiomaInterfaz.T("Editor.Compartir"),
            [
                new(IdiomaInterfaz.T("Editor.CompartirPokemon"), IdiomaInterfaz.T("Cajas.MandaArchivoPkHoja"), "Share2", () => CompartirArchivo(false), Motivo: motivoCompartir),
                .. (Modelo.Borrador.Format >= 3 ? new[] { new EntradaHoja(IdiomaInterfaz.T("Editor.CompartirCifradoEk"), IdiomaInterfaz.T("Cajas.ArchivoCifradoGuardaJuego"), "Share2", () => CompartirArchivo(true), Motivo: motivoCompartir) } : []),
                new(IdiomaInterfaz.T("Cajas.CompartirSetShowdown"), IdiomaInterfaz.T("Cajas.MandaTextoSetHoja"), "Share2", CompartirShowdown, Motivo: motivoCompartir),
            ]),
            new SeccionHoja(IdiomaInterfaz.T("Ajustes.Pkhex.Grupo.12"),
            [
                new(IdiomaInterfaz.T("Editor.InformeLegalidad"), IdiomaInterfaz.T("Editor.DiceCorePokemon"), "ShieldCheck", () => MostrarLegalidad(false)),
                new(IdiomaInterfaz.T("Editor.InformeDetallado"), IdiomaInterfaz.T("Editor.TodasComprobaciones"), "ShieldCheck", () => MostrarLegalidad(true)),
            ]),
            new SeccionHoja(IdiomaInterfaz.T("Ajustes.Pkhex.Grupo.10"),
            [
                new(Avanzado ? IdiomaInterfaz.T("Editor.SalirModoAvanzado") : IdiomaInterfaz.T("Editor.ModoAvanzado"), Avanzado ? IdiomaInterfaz.T("Editor.VuelveListasFiltradasPartida") : IdiomaInterfaz.T("Editor.ListasCompletasHabilidadLibre"), "SlidersHorizontal", CambiarAvanzado),
                new(IdiomaInterfaz.T("Editor.DescartarCambios"), IdiomaInterfaz.T("Editor.VuelveEstabaPokemonAbrir"), "Undo2", DescartarCambios, Destructiva: true, Motivo: HayCambios ? null : IdiomaInterfaz.T("Editor.CambiosDescartar")),
            ]),
        ], Subtitulo));
    }

    private void CambiarAvanzado()
    {
        Modelo.CambiarAvanzado(!Avanzado);
        OnPropertyChanged(nameof(Avanzado));
        Reconstruir();
        Informar(Avanzado ? IdiomaInterfaz.T("Editor.ModoAvanzadoActivadoListas") : IdiomaInterfaz.T("Editor.ModoAvanzadoDesactivado"), "aviso");
    }

    private void DescartarCambios()
    {
        Original.Data.CopyTo(Modelo.Borrador.Data);
        Modelo.Contexto.StatsManuales = false;
        Informar(IdiomaInterfaz.T("Editor.CambiosDescartados"), "ok");
        Reconstruir();
    }

    // ---- Importar y exportar (Cajas\Intercambio) ----
    /// <summary>
    /// <c>Main.ClickShowdownImportPKM</c>: lee el primer set del portapapeles (<see cref="ModeloShowdown.Analizar"/>), enseña el set reformateado con la pregunta de PKHeX («¿Importar este set?») y, al confirmar, lo aplica
    /// al borrador (<c>LoadShowdownSet</c> = <c>ApplySetDetails</c>) en un hilo aparte; las líneas que no entendió se avisan después, como PKHeX. La casilla no cambia hasta «Aplicar a la casilla».
    /// </summary>
    private async Task ImportarShowdownAsync()
    {
        var p = _portapapeles();
        if (p is null) return;
        var texto = await p.LeerAsync();
        var r = ModeloShowdown.Analizar(texto, AjustesActuales);
        if (!r.Correcto) { Informar(r.Error!, "error"); return; }
        void Hacer() => _ = AplicarSetAsync(r);
        if (_abrirHoja is null) { Hacer(); return; }
        List<string> parrafos = [MessageStrings.MsgSimulatorLoad, r.TextoReformateado, IdiomaInterfaz.T("Editor.HastaPulsesAplicarCasilla")];
        _abrirHoja(new ConfirmarHojaViewModel(IdiomaInterfaz.T("Editor.ImportarSet"), parrafos, IdiomaInterfaz.T("Form.Fama3.Importar"), Hacer, destructiva: HayCambios));
    }

    /// <summary>Aplica un set ya leído al borrador (no fuerza la legalidad). Espera el cerrojo de legalidad fuera del hilo de la interfaz y no pisa un cambio hecho mientras tanto.</summary>
    public async Task AplicarSetAsync(ResultadoImportarSet r)
    {
        if (r.Set is not { } set) return;
        CommitirPendientes();
        var huella = Borrador.Data.ToArray();
        var nuevo = await Task.Run(() => ModeloShowdown.Aplicar(Borrador, set));
        if (!Borrador.Data.SequenceEqual(huella)) { Informar(IdiomaInterfaz.T("Editor.PokemonCambioMientrasAplicaba"), "aviso"); return; }
        var error = Modelo.CargarDesde(nuevo);
        if (error is not null) { Informar(error, "error"); return; }
        Informar(r.LineasNoValidas.Count == 0 ? IdiomaInterfaz.T("Editor.SetShowdownAplicadoBorrador") : ModeloShowdown.TextoLineasNoValidas(r), r.LineasNoValidas.Count == 0 ? "ok" : "aviso");
        Reconstruir();
    }

    private async Task ImportarArchivoAsync()
    {
        var docs = _documentos();
        if (docs is null) return;
        try
        {
            var doc = await docs.ElegirParaAbrirAsync(CancellationToken.None);
            if (doc is null) return;
            var datos = await docs.LeerAsync(doc, CancellationToken.None);
            ProcesarImportado(Intercambio.Analizar(datos, doc.Nombre, _sesion.Guardado));
        }
        catch (Exception ex) { Informar(IdiomaInterfaz.T("Editor.PodidoLeerArchivo") + ex.Message, "error"); }
    }

    private async Task ImportarQrAsync()
    {
        var docs = _documentos();
        if (docs is null) return;
        try
        {
            var doc = await docs.ElegirParaAbrirAsync(CancellationToken.None);
            if (doc is null) return;
            ImportarQrDeImagen(await docs.LeerAsync(doc, CancellationToken.None));
        }
        catch (Exception ex) { Informar(IdiomaInterfaz.T("Editor.PodidoLeerImagen") + ex.Message, "error"); }
    }

    /// <summary>Lee el QR de una imagen y, si es de un Pokemon de este formato, lo ofrece como importacion (como «Leer QR» de PKHeX).</summary>
    public void ImportarQrDeImagen(byte[] imagen)
    {
        var mensaje = Design.LectorQr.Leer(imagen);
        if (mensaje is null) { Informar(IdiomaInterfaz.T("Editor.EncontradoNingunCodigoQR"), "error"); return; }
        var leido = ModeloQr.Leer(mensaje, _sesion.Guardado);
        if (leido is null) { Informar(ModeloQr.MensajeNoValido(mensaje), "error"); return; }
        var pk = leido.Pokemon;
        var convertido = EntityConverter.ConvertToType(pk, _sesion.Guardado.PKMType, out var resultado);
        if (convertido is null) { Informar(resultado.GetDisplayString(pk, _sesion.Guardado.PKMType), "error"); return; }
        ProcesarImportado(new ImportadoPokemon(convertido, _sesion.Guardado.EvaluateCompatibility(convertido, Pokekeep.Mobile.Ui.Ajustes.Pkhex.ProgramaPkhex.Idioma), "qr"));
    }

    /// <summary>Lo que se hace con lo importado (separado del selector para poder probarlo): un Pokemon sustituye al borrador previa confirmacion.</summary>
    public void ProcesarImportado(Importado contenido)
    {
        switch (contenido)
        {
            case ImportadoError e: Informar(e.Mensaje, "error"); break;
            case ImportadoPokemon p:
                void Hacer()
                {
                    var error = Modelo.CargarDesde(p.Pokemon);
                    if (error is not null) { Informar(error, "error"); return; }
                    Informar(IdiomaInterfaz.T("Editor.PokemonImportadoBorradorRevisa"), "ok");
                    Reconstruir();
                }
                if (_abrirHoja is null) { Hacer(); break; }
                List<string> parrafos = [IdiomaInterfaz.T("Editor.SustituiranDatosAhoraEditor"), .. p.Avisos, IdiomaInterfaz.T("Editor.HastaPulsesAplicarCasilla")];
                _abrirHoja(new ConfirmarHojaViewModel(IdiomaInterfaz.T("Editor.ImportarPokemon"), parrafos, IdiomaInterfaz.T("Form.Fama3.Importar"), Hacer, destructiva: HayCambios));
                break;
            default: Informar(IdiomaInterfaz.T("Editor.ArchivoContienePokemon"), "error"); break;
        }
    }

    private async Task ExportarShowdownAsync()
    {
        var p = _portapapeles();
        if (p is null) return;
        TextosNucleoPreparar();
        var texto = ModeloShowdown.Exportar(Modelo.Preparar(), AjustesActuales);
        var ok = await p.CopiarAsync(texto);
        Informar(ok ? ModeloShowdown.AvisoExitoPokemon(texto) : MessageStrings.MsgClipboardFailWrite + " " + MessageStrings.MsgSimulatorExportFail, ok ? "ok" : "error");   // los avisos son los de PKHeX
    }

    private async Task ExportarArchivoAsync(bool cifrado)
    {
        var docs = _documentos();
        if (docs is null) return;
        try
        {
            var pk = Modelo.Preparar();
            var destino = await docs.ElegirDestinoAsync(Intercambio.NombreArchivo(pk, cifrado), CancellationToken.None);
            if (destino is null) return;
            await docs.EscribirAsync(destino, Intercambio.Bytes(pk, cifrado), CancellationToken.None);
            Informar(IdiomaInterfaz.F("Form.Exportado", destino.Nombre), "ok");
        }
        catch (Exception ex) { Informar(IdiomaInterfaz.T("Editor.PodidoExportar") + ex.Message, "error"); }
    }

    private void MostrarQr()
    {
        try
        {
            var pk = Modelo.Preparar();
            _abrirHoja?.Invoke(new QrHojaViewModel(IdiomaInterfaz.T("Cajas.CodigoQR"), pk, _sesion.Textos.Species[pk.Species], _portapapeles, () => Compartir?.Invoke()));
        }
        catch (Exception ex) { Informar(IdiomaInterfaz.T("Cajas.PodidoCrearCodigoQR") + ex.Message, "error"); }
    }

    private void MostrarResumen()
        => _abrirHoja?.Invoke(new InformeHojaViewModel(IdiomaInterfaz.T("Cajas.ResumenPokemon"), Intercambio.Resumen(Modelo.Preparar(), _casilla.Tipo, ajustes: AjustesActuales), copiar: _portapapeles() is { } p ? t => p.CopiarAsync(t) : null));

    private static void TextosNucleoPreparar() => Ajustes.Pkhex.TextosNucleo.Preparar();

    // ---- Compartir (hoja del sistema; el equivalente de arrastrar el sprite, Main.Dragout_MouseDown) ----
    private void CompartirArchivo(bool cifrado)
    {
        if (Compartir?.Invoke() is not { } sistema) { Informar(IdiomaInterfaz.T("Cajas.CompartirDisponibleAqui"), "aviso"); return; }
        try
        {
            var archivo = ModeloCompartir.Pokemon(Modelo.Preparar(), cifrado);
            var motivo = sistema.CompartirArchivo(IdiomaInterfaz.T("Editor.CompartirPokemon2"), archivo.Nombre, archivo.Datos, archivo.TipoMime);
            if (motivo is not null) Informar(motivo, "error");
        }
        catch (Exception ex) { Informar(IdiomaInterfaz.T("Editor.PodidoCompartir") + ex.Message, "error"); }
    }

    private void CompartirShowdown()
    {
        if (Compartir?.Invoke() is not { } sistema) { Informar(IdiomaInterfaz.T("Cajas.CompartirDisponibleAqui"), "aviso"); return; }
        var motivo = sistema.CompartirTexto(IdiomaInterfaz.T("Cajas.SetShowdown"), ModeloShowdown.Exportar(Modelo.Preparar(), AjustesActuales));
        if (motivo is not null) Informar(motivo, "error");
    }

    // ---- Legalidad (informe completo) ----
    [RelayCommand] private void Legalidad() => MostrarLegalidad(false);

    private void MostrarLegalidad(bool detalle)
    {
        if (_abrirHoja is null) return;
        var datos = ModeloInformeLegalidad.Crear(Modelo.Preparar(), _casilla.Tipo, AjustesActuales, detalle);
        if (ModeloInformeLegalidad.Hoja(IdiomaInterfaz.T("Ajustes.Pkhex.Grupo.12"), datos, _portapapeles()) is { } hoja) _abrirHoja(hoja);
    }

    /// <summary>Ajuste «Mostrar resumen» (lo pone la Shell): los éxitos se ocultan si está apagado; los fallos y las cancelaciones se muestran siempre.</summary>
    public Func<bool>? MostrarResumenDeAcciones { get; set; }

    // ---- Acciones del motor sobre el borrador ----
    [RelayCommand] private void Legalizar() => Lanzar(AccionPokemon.Legalizar);
    [RelayCommand] private void Optimizar() => Lanzar(AccionPokemon.Optimizar);

    private void Lanzar(AccionPokemon accion)
    {
        if (_acciones is null || _abrirHoja is null) return;
        CommitirPendientes();
        var objetivo = IdiomaInterfaz.F("Editor.Borrador", NombreEspecie, _casilla.Descripcion);
        var hoja = new AccionHojaViewModel(accion, objetivo,
            (p, ct) => _acciones.EjecutarEnBorradorAsync(accion, _sesion, Modelo.Borrador, p, ct),
            terminada: r => { if (r.Aplicado) Reconstruir(); },
            deshecho: Reconstruir,
            mostrarResumen: MostrarResumenDeAcciones, abrirHoja: _abrirHoja, portapapeles: _portapapeles);
        _abrirHoja(hoja);
        _ = hoja.Lanzar();
    }

    // ---- Salir y aplicar ----
    [RelayCommand]
    private void Volver()
    {
        if (!HayCambios || _abrirHoja is null) { _cerrar?.Invoke(); return; }
        _abrirHoja(new ConfirmarHojaViewModel(IdiomaInterfaz.T("Editor.SalirAplicar"), [IdiomaInterfaz.T("Editor.HasCambiadoPokemonHas")], IdiomaInterfaz.T("Editor.SalirAplicar"), () => _cerrar?.Invoke(), destructiva: true, textoCancelar: IdiomaInterfaz.T("Form.Salir.Cancelar")));
    }

    /// <summary>Aplica el texto escrito que aun no se ha aplicado (hexadecimal, decimales, fechas) antes de leer el borrador.</summary>
    public void CommitirPendientes()
    {
        foreach (var c in Activas.SelectMany(s => s.TodosLosCampos).Where(c => c.Pendiente).ToList())
            EscribirTexto(c, c.Valor, final: true);
    }

    [RelayCommand]
    private void Aplicar()
    {
        CommitirPendientes();
        if (Modelo.Incompleto() is { } falta) { Informar(falta, "error"); return; }
        // Sobre la copia de sesion en memoria y por el historial del Core (un Deshacer); nada se exporta (regla 13). Si el destino no admite el Pokemon, el editor sigue abierto y lo dice.
        var info = _casilla.Info is SlotInfoParty p ? new SlotInfoParty(p.Slot) : _casilla.Info;
        var r = _sesion.Casillas.Colocar(info, Modelo.Preparar());
        if (!r.Correcto) { Informar(r.Mensaje, "error"); return; }
        Mensaje = string.Empty;
        HayCambios = false;
        _aplicado?.Invoke(r with { Mensaje = IdiomaInterfaz.T("Editor.CambiosAplicadosCasillaPuedes") });
        _cerrar?.Invoke();
    }
}

/// <summary>Un subeditor en hoja inferior: los campos de su definicion (los mismos controles que las pestanas), botones propios y «Deshacer los cambios de esta ventana».</summary>
public sealed partial class SubeditorHojaViewModel : HojaViewModel
{
    private readonly EditorViewModel _ed;
    private readonly SubeditorDef _def;
    private readonly string _id, _objetivo;
    private readonly byte[] _copia;

    public SubeditorHojaViewModel(EditorViewModel ed, SubeditorDef def, string id, string objetivo)
    {
        _ed = ed; _def = def; _id = id; _objetivo = objetivo;
        _copia = ed.Modelo.Borrador.Data.ToArray();
        Construir();
    }

    public override string Titulo => _def.Titulo;
    public ObservableCollection<SeccionBaseVm> Secciones { get; } = [];
    public IReadOnlyList<AccionSubVm> Acciones { get; private set; } = [];
    public IReadOnlyList<string> Glifos => _def.Glifos;
    public bool HayGlifos => _def.Glifos.Count > 0;
    public string? Aviso => _def.Aviso;
    public bool HayAviso => !string.IsNullOrEmpty(_def.Aviso);
    public bool HayAcciones => Acciones.Count > 0;

    private void Construir()
    {
        Secciones.Clear();
        foreach (var s in _def.Secciones.Where(s => s.EsVisible(_ed.Modelo.Contexto)))
        {
            var v = new SeccionPlanaVm { Titulo = s.Titulo };
            foreach (var c in s.Campos) v.Campos.Add(new CampoVm(_ed, c));
            v.Releer();
            Secciones.Add(v);
        }
        Acciones = [.. _def.Acciones.Select(a => new AccionSubVm(a, () => Ejecutar(a)))];
        OnPropertyChanged(nameof(Acciones)); OnPropertyChanged(nameof(HayAcciones));
    }

    /// <summary>Vuelve a crear la definicion con el contexto actual (cambio de modo avanzado).</summary>
    public void Reconstruir()
    {
        var def = Subeditores.Crear(_id, _ed.Modelo.Contexto, _objetivo);
        if (def is null) { Cerrar(); return; }
        Secciones.Clear();
        foreach (var s in def.Secciones.Where(s => s.EsVisible(_ed.Modelo.Contexto)))
        {
            var v = new SeccionPlanaVm { Titulo = s.Titulo };
            foreach (var c in s.Campos) v.Campos.Add(new CampoVm(_ed, c));
            v.Releer();
            Secciones.Add(v);
        }
    }

    private void Ejecutar(AccionSubeditor a)
    {
        try { a.Ejecutar(_ed.Modelo.Contexto); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IndexOutOfRangeException or NullReferenceException) { _ed.Informar(IdiomaInterfaz.T("Editor.PodidoAplicar") + ex.Message, "error"); }
        foreach (var s in Secciones) s.Releer();
        _ed.Modelo.NotificarCambio();
    }

    [RelayCommand] private void Insertar(string? glifo) { if (!string.IsNullOrEmpty(glifo)) _ed.InsertarGlifo(_objetivo, glifo); }

    [RelayCommand]
    private void Deshacer()
    {
        _copia.CopyTo(_ed.Modelo.Borrador.Data);
        foreach (var s in Secciones) s.Releer();
        _ed.Modelo.NotificarCambio();
        Cerrar();
    }

    [RelayCommand] private void Hecho() => Cerrar();
}

/// <summary>Un boton de un subeditor.</summary>
public sealed class AccionSubVm(AccionSubeditor accion, Action ejecutar)
{
    public string Texto => accion.Texto;
    public string Ayuda => accion.Ayuda ?? accion.Texto;
    public System.Windows.Input.ICommand Comando { get; } = new RelayCommand(ejecutar);
}
