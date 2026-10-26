// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Ajustes.Pkhex;
using Pokekeep.Mobile.Ui.Almacen;
using Pokekeep.Mobile.Ui.Guardados;
using Pokekeep.Mobile.Ui.ViewModels;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.Herramientas.ListaCarpetas;

/// <summary>
/// «Partida &gt; Lista de carpetas» (<c>SAV_FolderList</c>, Ctrl+F en PKHeX; bloque 10-4: PK-SUB-014 a 017, PK-MENU-023, PK-MENU-042, PK-BOX-078, PK-BOX-029, PK-IO-034, PK-IO-061). Tres pestañas como en PKHeX:
/// <list type="bullet">
/// <item>«Reciente»: los guardados abiertos hace poco (<c>Startup.RecentlyLoaded</c>) y los de las «Otras rutas de copias»;</item>
/// <item>«Copia de seg.»: lo que hay en la carpeta de copias de la app (las copias automáticas al cargar y las de antes de guardar);</item>
/// <item>«Carpetas»: la carpeta de copias y cada «Otra ruta de copias»; tocar una enseña sus guardados (en PKHeX, «Abrir» la abre en el Explorador).</item>
/// </list>
/// Tocar un guardado abre su menú (lo que en PKHeX es el menú contextual): «Abrir», «Borrar» (solo copias de la app, con la pregunta <c>MsgFileDelete</c>) y «Quitar de recientes». Además, «Cargar el guardado
/// más reciente» (<c>ClickSaveFileName</c> y el doble toque de la pestaña SAV) y «Exportar copia (BAK)» (<c>Menu_ExportBAK</c>). La rejilla filtra por todas las columnas, como el filtro de PKHeX.
/// </summary>
public sealed partial class ListaCarpetasViewModel : HerramientaViewModel
{
    public const string Reciente = "reciente", Copias = "copias", CarpetasPestana = "carpetas";
    private readonly ModeloCarpetas _modelo;

    public ListaCarpetasViewModel(ContextoHerramienta contexto, Action volver, bool cargar = true) : base(contexto, volver)
    {
        _modelo = new ModeloCarpetas(contexto.Ajustes, contexto.Documentos(), contexto.Carpetas(), contexto.Servicios is { } s ? s.Recientes : null);
        // Las columnas de PKHeX (DGV_*), en su orden; la tarjeta enseña el nombre, el juego, el EO y la fecha. ID e IDS solo salen en el detalle y en la exportación.
        Tabla = new TablaResultadosViewModel(
        [
            new ColumnaTabla("nombre", IdiomaInterfaz.T("EstadisticasBase.Nombre"), o => ((GuardadoListado)o).Nombre),
            new ColumnaTabla("juego", IdiomaInterfaz.T("Form.Juego"), o => ((GuardadoListado)o).JuegoCorto),
            new ColumnaTabla("ot", IdiomaInterfaz.T("Informe.Columna.OT"), o => ((GuardadoListado)o).OT),
            new ColumnaTabla("fecha", IdiomaInterfaz.T("ListaCarpetas.Fecha"), o => ((GuardadoListado)o).Fecha, o => ((GuardadoListado)o).FechaUtc),
            new ColumnaTabla("carpeta", IdiomaInterfaz.T("ListaCarpetas.Carpeta"), o => ((GuardadoListado)o).Carpeta, Visible: false),
            new ColumnaTabla("g", "G", o => ((GuardadoListado)o).G.ToString(System.Globalization.CultureInfo.InvariantCulture), o => ((GuardadoListado)o).G, Visible: false),
            new ColumnaTabla("jugado", "T. Jugado", o => ((GuardadoListado)o).Jugado, Visible: false),
            new ColumnaTabla("sid", IdiomaInterfaz.T("Form.Respaldo.SAV_SecretBase3.L_SID"), o => ((GuardadoListado)o).SID, Visible: false),
            new ColumnaTabla("tid", IdiomaInterfaz.T("ListaCarpetas.ID"), o => ((GuardadoListado)o).TID, Visible: false),
        ], o => MenuGuardado((GuardadoListado)o));
        Tabla.SolicitarOrden = h => Contexto.AbrirHoja(h);
        Tabla.LogoDe = o => Design.LogosDeJuego.VersionDe(((GuardadoListado)o).Partida);
        if (cargar) _ = RecargarAsync();
    }

    public override string Titulo => IdiomaInterfaz.T("CatalogoHerramientas.ListaCarpetas");
    public string Subtitulo => IdiomaInterfaz.T("ListaCarpetas.PartidasAbiertasHacePoco");
    public TablaResultadosViewModel Tabla { get; }

    // ------------------------------------------------------------------------------------------------ pestañas
    [ObservableProperty] private string _pestana = Reciente;
    [ObservableProperty] private IReadOnlyList<CarpetaListada> _carpetas = [];
    /// <summary>La carpeta abierta desde la pestaña «Carpetas» (null = se ve la lista de carpetas).</summary>
    [ObservableProperty] private CarpetaListada? _carpetaAbierta;
    [ObservableProperty] private string _textoVacio = string.Empty;

    public bool EsReciente => Pestana == Reciente;
    public bool EsCopias => Pestana == Copias;
    public bool EsCarpetas => Pestana == CarpetasPestana;
    public bool VerCarpetas => EsCarpetas && CarpetaAbierta is null;
    public bool VerTabla => !VerCarpetas;
    public bool HayCarpetaAbierta => EsCarpetas && CarpetaAbierta is not null;
    public string TituloCarpetaAbierta => CarpetaAbierta?.Nombre ?? string.Empty;
    public bool HayVacio => VerTabla && !Ocupado && Tabla.Total == 0 && !string.IsNullOrEmpty(TextoVacio);
    public bool HayPartida => Sesion is not null;
    public string? MotivoBak => Sesion is null ? DefHerramienta.SinPartida : Sesion.CarpetaPrivada is null ? IdiomaInterfaz.T("ListaCarpetas.PartidaVieneArchivoArchivo") : null;
    public bool PuedeExportarBak => MotivoBak is null;

    partial void OnPestanaChanged(string value) => Avisar();
    partial void OnCarpetaAbiertaChanged(CarpetaListada? value) => Avisar();
    partial void OnTextoVacioChanged(string value) => OnPropertyChanged(nameof(HayVacio));

    private void Avisar()
    {
        OnPropertyChanged(nameof(EsReciente)); OnPropertyChanged(nameof(EsCopias)); OnPropertyChanged(nameof(EsCarpetas));
        OnPropertyChanged(nameof(VerCarpetas)); OnPropertyChanged(nameof(VerTabla)); OnPropertyChanged(nameof(HayCarpetaAbierta)); OnPropertyChanged(nameof(TituloCarpetaAbierta)); OnPropertyChanged(nameof(HayVacio));
    }

    [RelayCommand]
    public Task VerPestana(string? pestana)
    {
        if (pestana is null || Ocupado) return Task.CompletedTask;
        Pestana = pestana; CarpetaAbierta = null;
        return RecargarAsync();
    }

    /// <summary>Vuelve a leer la pestaña que se ve (los archivos cambian fuera de la app).</summary>
    [RelayCommand]
    public async Task RecargarAsync()
    {
        var pestana = Pestana; var carpeta = CarpetaAbierta;
        if (pestana == CarpetasPestana && carpeta is null)
        {
            var c = await EjecutarAsync(IdiomaInterfaz.T("ListaCarpetas.BuscandoCarpetas"), (ct, _) => _modelo.CarpetasAsync(ct).GetAwaiter().GetResult());
            if (c is not null) MostrarCarpetas(c);
            return;
        }
        var g = await EjecutarAsync(IdiomaInterfaz.T("ListaCarpetas.LeyendoGuardados"), (ct, _) => (pestana, carpeta) switch
        {
            (Reciente, _) => _modelo.RecientesAsync(ct).GetAwaiter().GetResult(),
            (Copias, _) => _modelo.CopiasAsync(ct).GetAwaiter().GetResult(),
            (_, { } dentro) => _modelo.DeCarpetaAsync(dentro, ct).GetAwaiter().GetResult(),
            _ => [],
        });
        if (g is not null && pestana == Pestana && carpeta == CarpetaAbierta) MostrarGuardados(g);
        OnPropertyChanged(nameof(HayVacio));
    }

    /// <summary>Deja la tabla con estos guardados (también la usan el gate visual y las pruebas, sin trabajo en segundo plano).</summary>
    public void MostrarGuardados(IReadOnlyList<GuardadoListado> guardados)
    {
        Tabla.Establecer(guardados);
        TextoVacio = (Pestana, CarpetaAbierta) switch
        {
            (Reciente, _) => IdiomaInterfaz.T("ListaCarpetas.TodaviaPartidasRecientesAbras"),
            (Copias, _) => Ajustes.Backup.BAKEnabled
                ? IdiomaInterfaz.T("ListaCarpetas.CopiasSeguridadHaceCada")
                : IdiomaInterfaz.T("ListaCarpetas.CopiasSeguridadCopiaAutomatica"),
            _ => IdiomaInterfaz.T("ListaCarpetas.GuardadosCarpeta"),
        };
        Estado = guardados.Count == 1 ? IdiomaInterfaz.T("ListaCarpetas.Guardado") : IdiomaInterfaz.F("ListaCarpetas.Guardados", guardados.Count);
        OnPropertyChanged(nameof(HayVacio));
    }

    public void MostrarCarpetas(IReadOnlyList<CarpetaListada> carpetas)
    {
        Carpetas = carpetas;
        Estado = carpetas.Count == 1 ? IdiomaInterfaz.T("ListaCarpetas.Carpeta2") : IdiomaInterfaz.F("Ajustes.Carpetas.Cantidad.otro", carpetas.Count);
    }

    [RelayCommand]
    private Task AbrirCarpeta(CarpetaListada? c)
    {
        if (c is null || Ocupado) return Task.CompletedTask;
        if (!c.Existe) { Contexto.Informar(IdiomaInterfaz.F("ListaCarpetas.Disponible", c.Nombre, c.Detalle), "aviso", "CircleAlert"); return Task.CompletedTask; }
        CarpetaAbierta = c;
        return RecargarAsync();
    }

    [RelayCommand]
    private Task VolverACarpetas() { CarpetaAbierta = null; return RecargarAsync(); }

    /// <summary>«Añadir carpeta…»: una carpeta del móvil (selector del sistema, permiso persistente) a las «Otras rutas de copias» (<c>Backup.OtherBackupPaths</c>), la misma lista que en Ajustes.</summary>
    [RelayCommand]
    private async Task AnadirCarpeta()
    {
        if (Contexto.Carpetas() is not { } almacen) { Contexto.Informar(IdiomaInterfaz.T("Ajustes.Carpetas.NoDisponible"), "aviso", "CircleAlert"); return; }
        CarpetaRef? c;
        try { c = await almacen.ElegirCarpetaAsync(CancellationToken.None); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException) { Contexto.Informar(IdiomaInterfaz.T("ListaCarpetas.PudoElegirCarpeta") + ex.Message, "error", "TriangleAlert"); return; }
        if (c is null) return;
        if (!OtrasRutas.Anadir(Contexto.Ajustes, c.Uri)) { Contexto.Informar(IdiomaInterfaz.F("Ajustes.Carpetas.YaAnadida", c.Nombre), "aviso", "CircleAlert"); return; }
        Contexto.Informar(IdiomaInterfaz.F("ListaCarpetas.AnadidaOtrasRutasCopias", c.Nombre), "ok", "CircleCheck");
        await RecargarAsync();
    }

    [RelayCommand]
    private void MenuCarpeta(CarpetaListada? c)
    {
        if (c is null) return;
        var entradas = new List<EntradaHoja>
        {
            new(IdiomaInterfaz.T("ListaCarpetas.VerGuardados"), c.Detalle, "FolderOpen", c.Existe ? () => _ = AbrirCarpeta(c) : null, Motivo: c.Existe ? null : c.Detalle),
            new(IdiomaInterfaz.T("ListaCarpetas.QuitarLista"), IdiomaInterfaz.T("ListaCarpetas.DejaBuscarGuardadosAqui"), "Trash2", c.EnLaApp ? null : () => QuitarCarpeta(c),
                Motivo: c.EnLaApp ? IdiomaInterfaz.T("ListaCarpetas.CarpetaCopiasAppPuede") : null),
        };
        Contexto.AbrirHoja(new MenuHojaViewModel(c.Nombre, [new SeccionHoja(null, entradas)]));
    }

    private void QuitarCarpeta(CarpetaListada c)
    {
        if (OtrasRutas.Quitar(Contexto.Ajustes, c.Ubicacion)) Contexto.Informar(IdiomaInterfaz.F("ListaCarpetas.QuitadaOtrasRutasCopias", c.Nombre), "ok", "CircleCheck");
        _ = RecargarAsync();
    }

    // ------------------------------------------------------------------------------------------------ un guardado
    /// <summary>El menú de un guardado (el menú contextual de la rejilla de PKHeX: Abrir, Buscar…, Borrar) con lo que se ve de él.</summary>
    public MenuHojaViewModel MenuGuardado(GuardadoListado g)
    {
        var enRecientes = Ajustes.Startup.RecentlyLoaded.Contains(g.Ubicacion);
        var entradas = new List<EntradaHoja>
        {
            new(IdiomaInterfaz.T("ListaCarpetas.Abrir"), IdiomaInterfaz.T("ListaCarpetas.AbreGuardadoCopiaTrabajo"), "FolderOpen", () => _ = AbrirGuardado(g),
                Motivo: Contexto.AbrirSesion is null && Contexto.AbrirDocumento is null ? IdiomaInterfaz.T("ForzarCarga.AbrirPartidasDisponibleVentana") : null),
            new(IdiomaInterfaz.T("ListaCarpetas.QuitarRecientes"), IdiomaInterfaz.T("ListaCarpetas.QuitaListaPartidasRecientes"), "Clock", enRecientes ? () => QuitarReciente(g) : null,
                Motivo: enRecientes ? null : IdiomaInterfaz.T("ListaCarpetas.ListaRecientes")),
            new(IdiomaInterfaz.T("Seleccion.Borrar"), IdiomaInterfaz.T("ListaCarpetas.BorraCopiaSeguridadApp"), "Trash2", _modelo.PuedeBorrar(g) ? () => ConfirmarBorrar(g) : null, Destructiva: true,
                Motivo: _modelo.PuedeBorrar(g) ? null : IdiomaInterfaz.T("ListaCarpetas.DesdeAquiSoloBorran")),
        };
        var detalle = $"{g.JuegoCorto} · G{g.G} · EO {g.OT} · ID {g.TID} · IDS {g.SID} · {g.Jugado} · {g.Carpeta} · {g.Fecha}";
        var hoja = new MenuHojaViewModel(g.Nombre, [new SeccionHoja(null, entradas)], detalle);
        Contexto.AbrirHoja(hoja);
        return hoja;
    }

    /// <summary>
    /// «Abrir» (<c>OpenSAV</c>): un documento del usuario por el despachador de la Shell (bloque 10-2: comprobación de versión, tarjeta de GameCube, .zip, avisos); una copia de la app como sesión sin documento
    /// (<see cref="ModeloCarpetas.AbrirAsync"/>) con el cerrojo de la Shell. Devuelve si se pidió la apertura.
    /// </summary>
    public async Task<bool> AbrirGuardado(GuardadoListado g)
    {
        try
        {
            if (g.Documento is { } d && Contexto.AbrirDocumento is { } despachar) { await despachar(d); return true; }
            if (Contexto.AbrirSesion is not { } abrir) return false;
            return await abrir(async () =>
            {
                var s = await ModeloCarpetas.AbrirAsync(g, Contexto.Servicios);
                if (s is null) { TextosNucleo.Preparar(); Contexto.Informar($"{MessageStrings.MsgFileLoadSaveLoadFail} «{g.Nombre}»", "aviso", "CircleAlert"); }
                return s;
            });
        }
        catch (AlmacenException ex) { Contexto.Informar(ex.Message, "aviso", "CircleAlert"); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Contexto.Informar(IdiomaInterfaz.T("ListaCarpetas.PudoLeerArchivo") + ex.Message, "error", "TriangleAlert"); }
        return false;
    }

    private void QuitarReciente(GuardadoListado g)
    {
        if (Ajustes.Startup.RecentlyLoaded.Remove(g.Ubicacion)) Contexto.Ajustes.Persistir();
        Contexto.Informar(IdiomaInterfaz.F("ListaCarpetas.QuitadoRecientes", g.Nombre), "ok", "CircleCheck");
        _ = RecargarAsync();
    }

    private void ConfirmarBorrar(GuardadoListado g)
    {
        TextosNucleo.Preparar();
        Contexto.AbrirHoja(new ConfirmarHojaViewModel(IdiomaInterfaz.T("ListaCarpetas.BorrarCopia"), [MessageStrings.MsgFileDelete, g.Nombre], IdiomaInterfaz.T("Seleccion.Borrar"), () => Borrar(g), destructiva: true));
    }

    /// <summary>Borra una copia de la app (ya confirmado). Devuelve si se borró.</summary>
    public bool Borrar(GuardadoListado g)
    {
        if (_modelo.Borrar(g) is { } error) { Contexto.Informar(error, "error", "TriangleAlert"); return false; }
        Contexto.Informar(IdiomaInterfaz.F("ListaCarpetas.Borrada", g.Nombre), "ok", "CircleCheck");
        _ = RecargarAsync();
        return true;
    }

    // ------------------------------------------------------------------------------------------------ acciones de la partida
    /// <summary>
    /// «Cargar el guardado más reciente» (<c>ClickSaveFileName</c>; el doble toque de la pestaña SAV, <c>RequestReloadSave</c>): busca en las «Otras rutas de copias» el guardado más reciente con las sumas
    /// correctas (tope de 5 s) y pregunta <c>MsgFileLoadSaveDetectReload</c> antes de abrirlo.
    /// </summary>
    [RelayCommand]
    public async Task CargarMasReciente()
    {
        var g = await EjecutarAsync(IdiomaInterfaz.T("ListaCarpetas.BuscandoGuardadoReciente"), (ct, _) => _modelo.MasRecienteAsync(ct).GetAwaiter().GetResult());
        if (g is null)
        {
            if (!Ocupado && !Estado.StartsWith(IdiomaInterfaz.T("ListaCarpetas.Cancelado"), StringComparison.Ordinal))
                Estado = Ajustes.Backup.OtherBackupPaths.Count == 0
                    ? IdiomaInterfaz.T("ListaCarpetas.OtrasRutasCopiasDonde")
                    : IdiomaInterfaz.T("ListaCarpetas.EncontradoNingunGuardadoSumas");
            return;
        }
        PreguntarRecarga(g);
    }

    public void PreguntarRecarga(GuardadoListado g)
    {
        TextosNucleo.Preparar();
        Contexto.AbrirHoja(new ConfirmarHojaViewModel(IdiomaInterfaz.T("ListaCarpetas.CargarGuardadoReciente"), [MessageStrings.MsgFileLoadSaveDetectReload, $"{g.Carpeta} › {g.Nombre}", $"{g.Juego} · {g.Fecha}"], IdiomaInterfaz.T("ListaCarpetas.Abrir"), () => _ = AbrirGuardado(g)));
    }

    /// <summary>«Exportar copia (BAK)» (<c>SAVEditor.ExportBackup</c>): el archivo de origen, sin cambios, con el nombre <c>BAKName</c> donde elija el usuario.</summary>
    [RelayCommand]
    public async Task<ExportacionBak?> ExportarBak()
    {
        if (Sesion is null || MotivoBak is { } motivo) { if (MotivoBak is { } m) Contexto.Informar(m, "aviso", "CircleAlert"); return null; }
        if (Contexto.Documentos() is not { } almacen) { Contexto.Informar(IdiomaInterfaz.T("ListaCarpetas.ExportarArchivosDisponibleVentana"), "aviso", "CircleAlert"); return null; }
        try
        {
            var r = await CopiasPkhex.ExportarBakAsync(Sesion, almacen);
            if (r is not null) Contexto.Informar(r.Mensaje, r.Hecha ? "ok" : "error", r.Hecha ? "CircleCheck" : "TriangleAlert");
            return r;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException) { Contexto.Informar(IdiomaInterfaz.T("ListaCarpetas.PudoExportarCopia") + ex.Message, "error", "TriangleAlert"); return null; }
    }

    // ------------------------------------------------------------------------------------------------ gate visual
    /// <summary>Un guardado de ejemplo para los estados del gate visual: la partida abierta como si estuviera en la carpeta de copias (sin leer archivos ni esperar a un trabajo).</summary>
    public IReadOnlyList<GuardadoListado> Ejemplo()
        => Sesion is null ? [] : [new GuardadoListado(Sesion.Guardado, Sesion.NombreArchivo, ModeloCarpetas.NombreCopias, Sesion.NombreArchivo, true, new DateTime(2026, 10, 12, 10, 30, 0, DateTimeKind.Utc))];

    public void EstadoConGuardados(string pestana) { Pestana = pestana; CarpetaAbierta = null; MostrarGuardados(Ejemplo()); }

    public void EstadoCarpetas()
    {
        Pestana = CarpetasPestana; CarpetaAbierta = null;
        MostrarCarpetas(
        [
            new CarpetaListada(ModeloCarpetas.NombreCopias, CopiasPkhex.Carpeta(Ajustes).Ruta, true, true, IdiomaInterfaz.T("CarpetasGuardados.CarpetaCopiasSeguridadApp")),
            new CarpetaListada(IdiomaInterfaz.T("ListaCarpetas.Partidas"), "content://ejemplo/tree/primary%3APartidas", false, true, IdiomaInterfaz.T("CarpetasGuardados.OtraRutaCopias")),
            new CarpetaListada(IdiomaInterfaz.T("ListaCarpetas.Emulador"), "content://ejemplo/tree/primary%3AEmulador", false, false, IdiomaInterfaz.T("CarpetasGuardados.DisponibleMovioBorroRetiro")),
        ]);
    }
}

/// <summary>Altas y bajas en «Otras rutas de copias» (<c>Backup.OtherBackupPaths</c>), compartidas por la «Lista de carpetas» y la fila de Ajustes; guardan los ajustes al cambiar.</summary>
public static class OtrasRutas
{
    public static bool Anadir(IAjustesPkhex ajustes, string ubicacion)
    {
        var l = ajustes.Actual.Backup.OtherBackupPaths;
        if (string.IsNullOrWhiteSpace(ubicacion) || l.Contains(ubicacion)) return false;
        l.Add(ubicacion);
        ajustes.Guardar();
        return true;
    }

    public static bool Quitar(IAjustesPkhex ajustes, string ubicacion)
    {
        if (!ajustes.Actual.Backup.OtherBackupPaths.Remove(ubicacion)) return false;
        ajustes.Guardar();
        return true;
    }
}
