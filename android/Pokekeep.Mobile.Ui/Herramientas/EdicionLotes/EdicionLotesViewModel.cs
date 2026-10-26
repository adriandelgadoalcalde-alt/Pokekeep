// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Almacen;
using Pokekeep.Mobile.Ui.ViewModels;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Herramientas.EdicionLotes;

/// <summary>
/// «Más &gt; Herramientas &gt; Editor por lotes» (<c>Menu_BatchEditor</c>, PK-PKME-001, PK-MENU-022). Lo mismo que el formulario de PKHeX: fuente (cajas, equipo o carpeta), instrucciones con su contador de
/// coincidencias, el constructor de líneas (formato, propiedad, prefijo) y «Ejecutar». Diferencias de móvil, sin cambiar lo que se edita:
/// <list type="bullet">
/// <item>En la partida cada «Ejecutar» se escribe al momento como UNA entrada del historial (PKHeX acumula hasta «Guardar»): «Deshacer el lote» la revierte exacta, que es lo que hacía «Restablecer».</item>
/// <item>Sin cuadros de diálogo (regla 4): los errores de las instrucciones salen en la pantalla; con valores vacíos PKHeX pregunta «¿Continuar?» y aquí aparece «Ejecutar igualmente».</item>
/// <item>En la carpeta los resultados se escriben en la carpeta que se elige (como PKHeX), pero como archivos NUEVOS: el sistema nunca deja sobrescribir un documento sin elegirlo, así que si se elige la misma
/// carpeta el proveedor añade un sufijo al nombre en vez de pisar el original.</item>
/// </list>
/// </summary>
public sealed partial class EdicionLotesViewModel : HerramientaViewModel
{
    /// <summary>Las últimas instrucciones, para volver a ponerlas al reabrir (<c>BatchEditor._lastUsedCommands</c>).</summary>
    private static string _ultimas = string.Empty;

    private IReadOnlyList<SlotCache>? _datos;
    private CarpetaRef? _carpeta;
    private IReadOnlyList<(string Nombre, byte[] Datos)>? _archivosCarpeta;
    private IReadOnlyList<SlotCache>? _datosCarpeta;   // los Pokémon de la carpeta ya reconocidos (en segundo plano, al elegirla): nunca se reparsea en el hilo de la interfaz
    private CancellationTokenSource? _conteo;
    private int _generacion;
    private int _versionTrasLote = -1;

    public EdicionLotesViewModel(ContextoHerramienta contexto, Action volver) : base(contexto, volver)
    {
        _instrucciones = _ultimas;
        ElegirFormato(0);
        ElegirReferencia();
        _ = ActualizarCoincidenciasAsync();
    }

    public override string Titulo => IdiomaInterfaz.T("CatalogoHerramientas.EditorLotes");
    public string Subtitulo => IdiomaInterfaz.T("EdicionLotes.CambiaVezTodosPokemon");
    public string Ayuda => IdiomaInterfaz.T("EdicionLotes.InstruccionLineaFiltrosEmpiezan");

    // ------------------------------------------------------------------------------------------------- fuente
    [ObservableProperty] private FuenteLotes _fuente = FuenteLotes.Cajas;
    public bool EsCajas { get => Fuente == FuenteLotes.Cajas; set { if (value) Fuente = FuenteLotes.Cajas; } }
    public bool EsEquipo { get => Fuente == FuenteLotes.Equipo; set { if (value) Fuente = FuenteLotes.Equipo; } }
    public bool EsCarpeta { get => Fuente == FuenteLotes.Carpeta; set { if (value) Fuente = FuenteLotes.Carpeta; } }
    public bool HayCajas => Sesion?.Guardado.HasBox == true;
    public bool HayEquipo => Sesion?.Guardado.HasParty == true;
    public bool PuedeLeerCarpetas => Contexto.LeerCarpeta is not null && Contexto.Carpetas() is not null;
    public string TextoCarpeta => _carpeta is null
        ? (PuedeLeerCarpetas ? IdiomaInterfaz.T("EdicionLotes.NingunaCarpetaElegida") : IdiomaInterfaz.T("EdicionLotes.VersionTodaviaPuedeLeer"))
        : IdiomaInterfaz.F("EdicionLotes.Archivos", _carpeta.Nombre, _archivosCarpeta?.Count ?? 0);

    partial void OnFuenteChanged(FuenteLotes value)
    {
        _datos = null;
        OnPropertyChanged(nameof(EsCajas)); OnPropertyChanged(nameof(EsEquipo)); OnPropertyChanged(nameof(EsCarpeta)); OnPropertyChanged(nameof(PuedeEjecutar));
        ElegirReferencia();
        ProgramarConteo();
    }

    /// <summary>Los Pokémon de la fuente (copias). Se vuelven a leer tras cada lote: la partida ya lleva lo escrito.</summary>
    public IReadOnlyList<SlotCache> Datos => _datos ??= Fuente switch
    {
        FuenteLotes.Carpeta => _datosCarpeta ?? [],
        _ => Sesion is null ? [] : ModeloLotes.DatosPartida(Sesion.Guardado, Fuente),
    };

    /// <summary>Elige la carpeta de origen («Carpeta…», <c>B_Open_Click</c>) y lee sus archivos.</summary>
    [RelayCommand]
    private async Task ElegirCarpeta()
    {
        if (Contexto.Carpetas() is not { } carpetas || Contexto.LeerCarpeta is not { } leer) { Mensaje(IdiomaInterfaz.T("EdicionLotes.VersionTodaviaPuedeLeer"), true); return; }
        var c = await carpetas.ElegirCarpetaAsync(CancellationToken.None);
        if (c is null) return;
        await UsarCarpetaAsync(c, leer);
    }

    /// <summary>Usa una carpeta ya elegida (las pruebas la pasan sin selector).</summary>
    public async Task UsarCarpetaAsync(CarpetaRef carpeta, Func<CarpetaRef, CancellationToken, Task<IReadOnlyList<(string Nombre, byte[] Datos)>>> leer)
    {
        var sav = Sesion?.Guardado;
        // Leer Y reconocer los archivos (FileUtil.TryGetPKM por cada uno) va en segundo plano: con carpetas grandes congelaría la pantalla (M-2 de la auditoría de Fable).
        var leidos = await EjecutarAsync(IdiomaInterfaz.T("EdicionLotes.LeyendoCarpeta"), (cancelar, _) =>
        {
            var a = leer(carpeta, cancelar).GetAwaiter().GetResult();
            IReadOnlyList<SlotCache> d = sav is null ? [] : ModeloLotes.DatosCarpeta(a, sav);
            return (Archivos: a, Datos: d);
        });
        if (leidos.Archivos is not { } archivos) return;
        _carpeta = carpeta; _archivosCarpeta = archivos; _datosCarpeta = leidos.Datos; _datos = null;
        Fuente = FuenteLotes.Carpeta;
        OnPropertyChanged(nameof(TextoCarpeta)); OnPropertyChanged(nameof(PuedeEjecutar));
        ElegirReferencia();
        ProgramarConteo();
    }

    // ------------------------------------------------------------------------------------------------- instrucciones y contador
    [ObservableProperty] private string _instrucciones;
    [ObservableProperty] private string _coincidencias = string.Empty;

    partial void OnInstruccionesChanged(string value)
    {
        _ultimas = value;
        AvisoVacias = string.Empty;
        OnPropertyChanged(nameof(PuedeEjecutar));
        ProgramarConteo();
    }

    /// <summary>Como <c>UpdateFilterCountDebounced</c>: espera un momento a que se deje de escribir y cuenta en segundo plano; un conteo nuevo cancela el anterior.</summary>
    private void ProgramarConteo() => _ = ActualizarCoincidenciasAsync(TimeSpan.FromMilliseconds(250));

    /// <summary>Recalcula «Coincidencias: m / t» (las pruebas lo esperan con <paramref name="espera"/> nula).</summary>
    public async Task ActualizarCoincidenciasAsync(TimeSpan espera = default)
    {
        _conteo?.Cancel();
        var cts = new CancellationTokenSource();
        _conteo = cts;
        var generacion = ++_generacion;
        try
        {
            if (espera > TimeSpan.Zero) await Task.Delay(espera, cts.Token).ConfigureAwait(true);
            if (Sesion is null) { Coincidencias = string.Format(ModeloLotes.FormatoCoincidencias, 0, 0); return; }
            var datos = Datos; var sav = Sesion.Guardado; var texto = Instrucciones;
            var protegidas = ModeloLotes.Protegidas(datos, sav);   // lo único que se lee de la partida viva, aquí en el hilo de la interfaz; el conteo trabaja sobre copias
            var r = await Task.Run(() => ModeloLotes.Coincidencias(datos, sav, texto, cts.Token, protegidas), cts.Token).ConfigureAwait(true);
            if (r is not null && generacion == _generacion) Coincidencias = r;
        }
        catch (OperationCanceledException) { }
    }

    // ------------------------------------------------------------------------------------------------- constructor de líneas
    public IReadOnlyList<string> Formatos => ModeloLotes.Formatos;
    [ObservableProperty] private int _formato;
    [ObservableProperty] private string _propiedad = string.Empty;
    [ObservableProperty] private char _prefijo = '.';
    [ObservableProperty] private string _valor = string.Empty;
    [ObservableProperty] private string _tipoPropiedad = string.Empty;
    [ObservableProperty] private string _valorReferencia = string.Empty;
    private PKM? _referencia;
    private string _nombreReferencia = string.Empty;

    public string TextoFormato => Formatos[Formato];
    public string TextoPrefijo => ModeloLotes.Significado(Prefijo);
    public bool PrefijoEsFiltro => StringInstruction.IsFilterInstruction(Prefijo);
    public string LineaPrevista => ModeloLotes.Linea(Prefijo, Propiedad, Valor);

    partial void OnFormatoChanged(int value) => OnPropertyChanged(nameof(TextoFormato));
    partial void OnPropiedadChanged(string value) { DescribirPropiedad(); OnPropertyChanged(nameof(LineaPrevista)); }
    partial void OnPrefijoChanged(char value) { OnPropertyChanged(nameof(TextoPrefijo)); OnPropertyChanged(nameof(PrefijoEsFiltro)); OnPropertyChanged(nameof(LineaPrevista)); }
    partial void OnValorChanged(string value) => OnPropertyChanged(nameof(LineaPrevista));

    /// <summary>Cambia el formato (<c>CB_Format</c>): la lista de propiedades pasa a la de ese formato y se elige la primera, como PKHeX.</summary>
    public void ElegirFormato(int formato)
    {
        Formato = Math.Clamp(formato, 0, Formatos.Count - 1);
        var props = ModeloLotes.Propiedades(Formato);
        Propiedad = props.Count > 0 ? props[0] : string.Empty;
        DescribirPropiedad();
    }

    private void ElegirReferencia()
    {
        var r = Datos.FirstOrDefault(d => d.Entity.Species != 0);
        _referencia = r?.Entity;
        _nombreReferencia = r is null ? string.Empty : $"{(Sesion?.Textos.specieslist is { } l && r.Entity.Species < l.Length ? l[r.Entity.Species] : r.Entity.Species.ToString())} ({Lugar(r.Source)})";
        DescribirPropiedad();
    }

    private static string Lugar(ISlotInfo s) => s is SlotInfoFileSingle f ? Path.GetFileName(f.Path) : Cajas.EditorCasillas.Describir(s);

    /// <summary><c>L_PropType</c> y <c>L_PropValue</c>: el tipo de la propiedad y su valor en el primer Pokémon de la fuente (en PKHeX, el del editor).</summary>
    private void DescribirPropiedad()
    {
        if (string.IsNullOrEmpty(Propiedad)) { TipoPropiedad = string.Empty; ValorReferencia = string.Empty; return; }
        TipoPropiedad = IdiomaInterfaz.T("EdicionLotes.Tipo") + ModeloLotes.Tipo(Propiedad, Formato);
        if (_referencia is null) { ValorReferencia = string.Empty; return; }
        var v = ModeloLotes.ValorEn(_referencia, Propiedad);
        ValorReferencia = v is null ? IdiomaInterfaz.F("EdicionLotes.TienePropiedad", _nombreReferencia) : IdiomaInterfaz.F("EdicionLotes.Valor", _nombreReferencia, v);
    }

    [RelayCommand]
    private void AbrirFormatos() => Contexto.AbrirHoja(new SelectorHojaViewModel(IdiomaInterfaz.T("BaseDatosPkm.Formato"), [.. Formatos.Select((f, i) => new OpcionSelector(i, f))], Formato, o => ElegirFormato(o.Id), IdiomaInterfaz.T("EdicionLotes.BuscarFormato")));

    [RelayCommand]
    private void AbrirPropiedades()
    {
        var props = ModeloLotes.Propiedades(Formato);
        Contexto.AbrirHoja(new SelectorHojaViewModel(IdiomaInterfaz.T("MenuCaja.Propiedad"), [.. props.Select((p, i) => new OpcionSelector(i, p))], props.ToList().IndexOf(Propiedad), o => Propiedad = props[o.Id], IdiomaInterfaz.T("Ajustes.Propiedades.Buscar")));
    }

    [RelayCommand]
    private void AbrirPrefijos()
    {
        var l = ModeloLotes.Prefijos;
        Contexto.AbrirHoja(new SelectorHojaViewModel(IdiomaInterfaz.T("EdicionLotes.HaceLinea"), [.. l.Select((p, i) => new OpcionSelector(i, ModeloLotes.Significado(p)))], l.ToList().IndexOf(Prefijo), o => Prefijo = l[o.Id], IdiomaInterfaz.T("Comun.Buscar")));
    }

    /// <summary>«Añadir» (<c>B_Add_Click</c>): añade la línea del constructor a las instrucciones.</summary>
    [RelayCommand]
    public void AnadirLinea()
    {
        if (string.IsNullOrEmpty(Propiedad)) { Mensaje(MessageStrings.MsgBEPropertyInvalid, true); return; }
        Instrucciones = ModeloLotes.Anadir(Instrucciones, ModeloLotes.Linea(Prefijo, Propiedad, Valor));
        Valor = string.Empty;
    }

    // ------------------------------------------------------------------------------------------------- ejecutar
    [ObservableProperty] private string _resultado = string.Empty;
    [ObservableProperty] private bool _resultadoEsError;
    [ObservableProperty] private string _avisoVacias = string.Empty;
    [ObservableProperty] private bool _puedeDeshacerLote;
    public bool HayResultado => !string.IsNullOrEmpty(Resultado);
    public bool HayAvisoVacias => !string.IsNullOrEmpty(AvisoVacias);
    partial void OnResultadoChanged(string value) => OnPropertyChanged(nameof(HayResultado));
    partial void OnAvisoVaciasChanged(string value) => OnPropertyChanged(nameof(HayAvisoVacias));

    /// <summary><c>B_Run.Enabled</c>: hay instrucciones y, si no es una carpeta, algún Pokémon en la fuente.</summary>
    public bool PuedeEjecutar => Instrucciones.Length != 0 && (Fuente == FuenteLotes.Carpeta ? _archivosCarpeta is not null : Datos.Any(z => z.Entity.Species != 0));

    private void Mensaje(string texto, bool error) { Resultado = texto; ResultadoEsError = error; Estado = texto; }

    [RelayCommand]
    public Task Ejecutar() => EjecutarConAsync(confirmarVacias: false);

    /// <summary>«Ejecutar igualmente»: la respuesta «Sí» al «¿Continuar?» de PKHeX cuando hay asignaciones sin valor.</summary>
    [RelayCommand]
    public Task EjecutarIgualmente() => EjecutarConAsync(confirmarVacias: true);

    private async Task EjecutarConAsync(bool confirmarVacias)
    {
        if (Sesion is null) return;
        var v = ModeloLotes.Validar(Instrucciones);
        if (!v.Valida) { if (v.Error!.Length > 0) Mensaje(v.Error, true); return; }
        if (v.HayVacias && !confirmarVacias) { AvisoVacias = v.AvisoVacias; Mensaje(v.AvisoVacias, true); return; }
        AvisoVacias = string.Empty;
        if (Fuente == FuenteLotes.Carpeta) { await EjecutarEnCarpetaAsync(v.Conjuntos); return; }

        var datos = ModeloLotes.DatosPartida(Sesion.Guardado, Fuente);   // copias recién leídas: lo de un lote anterior ya está en la partida
        var sav = Sesion.Guardado;
        var versionAntes = Sesion.Casillas.Version;   // regla 7: si la partida cambia mientras se calcula, no se escribe
        var protegidas = ModeloLotes.Protegidas(datos, sav);
        var r = await EjecutarAsync(IdiomaInterfaz.T("EdicionLotes.AplicandoInstrucciones"), (cancelar, _) => ModeloLotes.Ejecutar(datos, sav, v.Conjuntos, cancelar, protegidas));
        if (r is null) return;
        if (Sesion is null || !ReferenceEquals(Sesion.Guardado, sav) || Sesion.Casillas.Version != versionAntes)
        {
            _datos = null;
            Mensaje(IdiomaInterfaz.T("EdicionLotes.PartidaCambiadoMientrasCalculaba"), true);
            return;
        }
        if (r.Modificadas.Count > 0)
        {
            var casillas = Sesion.Casillas;
            var anotadas = ModeloLotes.Escribir(sav, casillas.Historial.Changelog, r.Modificadas);
            casillas.Notificar(Cajas.EditorCasillas.TipoCambio.Masivo, anotadas, IdiomaInterfaz.F("EdicionLotes.EditorLotesPokemon", r.Modificadas.Count));
            _versionTrasLote = casillas.Version;
        }
        _datos = null;
        ElegirReferencia();
        Mensaje(r.Mensaje, false);
        ActualizarDeshacer();
        await ActualizarCoincidenciasAsync();
    }

    private async Task EjecutarEnCarpetaAsync(StringInstructionSet[] sets)
    {
        if (_archivosCarpeta is null || Sesion is null) { Mensaje(IdiomaInterfaz.T("EdicionLotes.EligePrimeroCarpeta"), true); return; }
        if (Contexto.Carpetas() is not { } carpetas || Contexto.Documentos() is not { } docs) { Mensaje(IdiomaInterfaz.T("EdicionLotes.VersionTodaviaPuedeEscribir"), true); return; }
        // PKHeX: «Seleccione la carpeta donde se guardarán los archivos. Esta puede ser la misma carpeta que la fuente de los archivos PKM.»
        Mensaje(MessageStrings.MsgExportFolder, false);
        var destino = await carpetas.ElegirCarpetaAsync(CancellationToken.None);
        if (destino is null) { Mensaje(IdiomaInterfaz.T("EdicionLotes.EscritoNadaEligioCarpeta"), false); return; }
        var archivosCarpeta = _archivosCarpeta; var guardado = Sesion.Guardado;
        // Copias recién reconocidas (Ejecutar las modifica): también en segundo plano.
        var r = await EjecutarAsync(IdiomaInterfaz.T("EdicionLotes.AplicandoInstrucciones"), (cancelar, _) => ModeloLotes.Ejecutar(ModeloLotes.DatosCarpeta(archivosCarpeta, guardado), null, sets, cancelar));
        if (r is null) return;
        var escritos = 0;
        foreach (var m in r.Modificadas)
        {
            var a = ModeloLotes.BytesCarpeta(m);
            var doc = await carpetas.CrearEnCarpetaAsync(destino, a.Nombre, CancellationToken.None);
            await docs.EscribirAsync(doc, a.Datos, CancellationToken.None);
            escritos++;
        }
        Mensaje(r.Mensaje + (escritos > 0 ? IdiomaInterfaz.F("EdicionLotes.EscritosArchivos", escritos, destino.Nombre) : string.Empty), false);
        await ActualizarCoincidenciasAsync();
    }

    /// <summary>«Deshacer el lote»: lo que era «Restablecer» en PKHeX. Solo mientras el último cambio de la partida sea este lote (si se hizo otra cosa después, el Deshacer general de la partida manda).</summary>
    [RelayCommand]
    private async Task DeshacerLote()
    {
        if (Sesion is null || !PuedeDeshacerLote) return;
        var r = Sesion.Casillas.Deshacer();
        _versionTrasLote = -1; _datos = null;
        ElegirReferencia();
        Mensaje(r.Correcto ? IdiomaInterfaz.T("EdicionLotes.LoteDeshechoPokemonVuelven") : r.Mensaje, !r.Correcto);
        ActualizarDeshacer();
        await ActualizarCoincidenciasAsync();
    }

    private void ActualizarDeshacer() => PuedeDeshacerLote = Sesion is not null && _versionTrasLote >= 0 && Sesion.Casillas.Version == _versionTrasLote && Sesion.Casillas.PuedeDeshacer;

    /// <summary>Deja la pantalla con un resultado (gate visual: el estado «ejecutado» sin trabajo en segundo plano y sin tocar la partida).</summary>
    public void MostrarResultado(string texto, bool error = false) => Mensaje(texto, error);

    public override void Dispose()
    {
        _conteo?.Cancel(); _conteo?.Dispose(); _conteo = null;
        base.Dispose();
    }
}
