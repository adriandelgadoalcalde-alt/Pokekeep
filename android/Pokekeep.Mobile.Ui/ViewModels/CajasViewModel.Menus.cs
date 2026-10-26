// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using CommunityToolkit.Mvvm.Input;
using PKHeX.Core;
using Pokekeep.Mobile.Ui.Cajas;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Ui.ViewModels;

// Menu de casilla (ContextMenuSAV de PKHeX mas las dos acciones Keep), menu de la caja (BoxMenuStrip) y selector de cajas.
public sealed partial class CajasViewModel
{
    /// <summary>Un nombre de casilla para los textos: «Garchomp (Caja 1, casilla 5)».</summary>
    private string Descripcion(CasillaViewModel c)
        => c.Ocupada ? $"{(c.Huevo ? IdiomaInterfaz.T("Informe.Columna.IsEgg") : c.NombreEspecie)} · {c.Ref?.Descripcion}" : c.Ref?.Descripcion ?? IdiomaInterfaz.T("Cajas.Casilla");

    [RelayCommand]
    public void AbrirMenuCasillaElegida() { if (Seleccionada is { } c) AbrirMenuCasilla(c); }

    /// <summary>
    /// El menu de la casilla. Con Pokemon: Editar, las dos acciones Keep (Legalizar automaticamente y Optimizar stats), mover/copiar/clonar, legalidad, resumen, exportar (archivo, Showdown, QR),
    /// seleccionar varios y borrar. Vacia: importar un archivo, anadir desde la Libreria y seleccionar. Las casillas de solo lectura (guarderia, GTS, fusion...) ofrecen lo que no escribe.
    /// </summary>
    public void AbrirMenuCasilla(CasillaViewModel c)
    {
        if (c.Ref is not { } r || Seleccionando || Colocando) return;
        var sav = Sesion.Guardado;
        var escribible = Sesion.Casillas.EsEscribible(r.Info);
        string? MotivoAccion(AccionPokemon a) => !escribible ? IdiomaInterfaz.T("Cajas.SoloLecturaCopiaPokemon") : _anfitrion?.MotivoAccion(a, r);
        var secciones = new List<SeccionHoja>();

        if (c.Ocupada)
        {
            var pk = c.Entidad!;
            secciones.Add(new SeccionHoja(IdiomaInterfaz.T("Cajas.AccionesPokekeep"),
            [
                new(IdiomaInterfaz.T("Cajas.LegalizarAutomaticamente"), IdiomaInterfaz.T("Cajas.HaceLegalPokemonPolitica"), "WandSparkles", () => _anfitrion?.LanzarAccion(AccionPokemon.Legalizar, r), Motivo: MotivoAccion(AccionPokemon.Legalizar)),
                new(IdiomaInterfaz.T("Editor.OptimizarStats"), IdiomaInterfaz.T("Cajas.SubeEstadisticasDentroLegal"), "Sparkles", () => _anfitrion?.LanzarAccion(AccionPokemon.Optimizar, r), Motivo: MotivoAccion(AccionPokemon.Optimizar)),
                .. EntradaMasivaDeEste(r, MotivoAccion(AccionPokemon.Optimizar)),
            ]));
            secciones.Add(new SeccionHoja(IdiomaInterfaz.T("Nav.Pokemon"),
            [
                new(IdiomaInterfaz.T("Cajas.Editar"), escribible ? IdiomaInterfaz.T("Cajas.AbreEditorCompleto") : IdiomaInterfaz.T("Cajas.AbreEditorCambiosPueden"), "SlidersHorizontal", () => _anfitrion?.AbrirEditor(r, pk)),
                new(IdiomaInterfaz.T("Cajas.VerResumen"), IdiomaInterfaz.T("Cajas.TextoEspecieMovimientosEstadisticas"), "ScrollText", () => MostrarResumen(c)),
                new(IdiomaInterfaz.T("Ajustes.Pkhex.Grupo.12"), IdiomaInterfaz.T("Cajas.InformeAnalisisCore"), "ShieldCheck", () => MostrarLegalidad(c)),
                new(IdiomaInterfaz.T("Form.Vista.CampoPView.6"), IdiomaInterfaz.T("Cajas.ArchivoPkSetShowdown"), "FileDown", () => AbrirMenuExportar(c)),
            ]));
            secciones.Add(SeccionBanco([r]));
            var mover = new List<EntradaHoja>
            {
                new(IdiomaInterfaz.T("Cajas.Mover"), IdiomaInterfaz.T("Cajas.EligeCasillaDestinoOcupada"), "ArrowLeftRight", () => IniciarColocacion([r], copia: false), Motivo: escribible ? null : IdiomaInterfaz.T("Cajas.SoloLecturaUsaDuplicar")),
                new(IdiomaInterfaz.T("Cajas.Duplicar"), IdiomaInterfaz.T("Cajas.CopiaPokemonOtraCasilla"), "Copy", () => IniciarColocacion([r], copia: true)),
            };
            if (sav.HasBox) mover.Add(new(IdiomaInterfaz.T("Cajas.ClonarTodaCaja"), IdiomaInterfaz.T("Cajas.RellenaCajaActualCopias"), "Layers", () => ConfirmarClonarACaja(c), Destructiva: true));
            secciones.Add(new SeccionHoja(IdiomaInterfaz.T("Cajas.MoverCopiar"), mover));
            secciones.Add(new SeccionHoja(null,
            [
                new(IdiomaInterfaz.T("Cajas.SeleccionarVarios"), IdiomaInterfaz.T("Cajas.MarcaPokemonMoverlosBorrarlos"), "ListChecks", () => IniciarSeleccion(c)),
                new(IdiomaInterfaz.T("Seleccion.Borrar"), escribible ? IdiomaInterfaz.T("Cajas.VaciaCasillaPuedeDeshacer") : null, "Trash2", () => ConfirmarBorrar(c), Destructiva: true, Motivo: escribible ? null : IdiomaInterfaz.T("Cajas.SoloLecturaCasillaPuede")),
            ]));
        }
        else
        {
            var libre = escribible ? null : IdiomaInterfaz.T("Cajas.CasillaPuedeEscribir");
            secciones.Add(new SeccionHoja(IdiomaInterfaz.T("Cajas.CasillaVacia"),
            [
                new(IdiomaInterfaz.T("Cajas.NuevoPokemon"), IdiomaInterfaz.T("Cajas.AbreEditorPlantillaPKHeX"), "Plus", () => _anfitrion?.AbrirEditor(r, Pokekeep.Mobile.Ui.Editor.PlantillaEditor.Crear(sav)), Motivo: libre),   // PK-IO-018 (bloque 10-3)
                new(IdiomaInterfaz.T("Cajas.ImportarArchivo"), IdiomaInterfaz.T("Cajas.PoneAquiPokemonArchivo"), "FileUp", () => ImportarArchivo(r), Motivo: libre ?? (_anfitrion?.Documentos is null ? IdiomaInterfaz.T("Cajas.ImportarDisponibleAqui") : null)),
                new(IdiomaInterfaz.T("Cajas.AnadirDesdeLibreria"), IdiomaInterfaz.T("Cajas.EligePokemonJuegoPuede"), "BookOpen", () => _anfitrion?.IrALibreria(), Motivo: libre),
                new(IdiomaInterfaz.T("Cajas.SeleccionarCaja"), IdiomaInterfaz.T("Cajas.MarcaTodosPokemonCaja"), "ListChecks", SeleccionarCaja),
            ]));
        }
        Mostrar(new MenuHojaViewModel(c.Ocupada ? Descripcion(c) : IdiomaInterfaz.T("Cajas.CasillaVacia"), secciones, c.Ocupada ? null : r.Descripcion));
    }

    /// <summary>«Edición masiva de este Pokémon…» (parche 0006: clic derecho de la casilla), con la ayuda y la disponibilidad de la casilla del escritorio. Nada si la ventana no tiene operaciones masivas.</summary>
    private IEnumerable<EntradaHoja> EntradaMasivaDeEste(CasillaRef r, string? motivoCasilla)
    {
        if (_anfitrion?.TextosMasivas is not { } t) yield break;
        yield return new(t.MenuEstePokemon, t.AyudaEstePokemon, "Layers", () => _anfitrion.AbrirEdicionMasiva([r]), Motivo: motivoCasilla ?? _anfitrion.MotivoEdicionMasiva);
    }

    /// <summary>El submenú «Pokekeep» del menú de la caja (parche 0006): Edición masiva de esta caja / de todas las cajas / Completar el juego. Sin descripción propia: el escritorio solo pone ayuda cuando están desactivadas (el motivo).</summary>
    private IEnumerable<SeccionHoja> SeccionPokekeepCaja()
    {
        if (_anfitrion?.TextosMasivas is not { } t) yield break;
        var motivo = _anfitrion.MotivoEdicionMasiva;
        var caja = Caja;
        yield return new SeccionHoja(t.GrupoMenu,
        [
            new(t.MenuEstaCaja, null, "Layers", () => _anfitrion.AbrirEdicionMasivaDeCaja(caja), Motivo: motivo),
            new(t.MenuTodasLasCajas, null, "Layers", () => _anfitrion.AbrirEdicionMasivaDeCaja(null), Motivo: motivo),
            new(IdiomaInterfaz.T("Cajas.SoloIlegales"), IdiomaInterfaz.T("Cajas.TodasCajasEquipoVeras"), "TriangleAlert", () => _anfitrion.AbrirEdicionMasivaIlegales(), Motivo: motivo),
            new(t.MenuCompletar, null, "BookOpen", () => _anfitrion.AbrirCompletarJuego(), Motivo: _anfitrion.MotivoCompletarJuego),
        ]);
    }

    /// <summary>Atajos sobre la casilla elegida (pruebas de pantalla y TalkBack: el mismo camino que el menu de la casilla).</summary>
    public void VerLegalidadElegida() { if (Seleccionada is { } c) MostrarLegalidad(c); }
    public void VerResumenElegida() { if (Seleccionada is { } c) MostrarResumen(c); }
    public void VerQrElegida() { if (Seleccionada is { Entidad: { } pk }) MostrarQr(pk); }
    public void VerExportarElegida() { if (Seleccionada is { } c) AbrirMenuExportar(c); }
    public void BorrarElegida() { if (Seleccionada is { } c) ConfirmarBorrar(c); }

    private void ConfirmarBorrar(CasillaViewModel c)
    {
        if (c.Ref is not { } r) return;
        Mostrar(new ConfirmarHojaViewModel(IdiomaInterfaz.T("Cajas.BorrarPokemon"), [IdiomaInterfaz.F("Cajas.BorraraPartida", Descripcion(c)), IdiomaInterfaz.T("Cajas.PuedesDeshacerloDeshacer")], IdiomaInterfaz.T("Seleccion.Borrar"), () => Resultado(Sesion.Casillas.Borrar(Fresca(r.Info))), destructiva: true));
    }

    private void ConfirmarClonarACaja(CasillaViewModel c)
    {
        if (c.Entidad is not { } pk) return;
        var caja = Caja;
        Mostrar(new ConfirmarHojaViewModel(IdiomaInterfaz.T("Cajas.ClonarTodaCaja"), [string.Format(MessageStrings.MsgSaveBoxCloneFromTabs, NombreCaja), IdiomaInterfaz.T("Cajas.HayaCajaSustituyeCopias")], IdiomaInterfaz.T("Cajas.Clonar"), () => Resultado(Sesion.Casillas.ClonarACaja(pk, caja)), destructiva: true));
    }

    private void MostrarResumen(CasillaViewModel c)
    {
        if (c.Entidad is not { } pk) return;
        var texto = Intercambio.Resumen(pk, c.TipoAlmacen, encuentro: true, ajustes: AjustesPkhexActuales);   // BattleTemplate.Hover vigente
        Mostrar(new InformeHojaViewModel(IdiomaInterfaz.F("Cajas.Resumen", c.NombreEspecie), texto, copiar: _anfitrion?.Portapapeles is { } p ? async t => { await p.CopiarAsync(t); } : null));
    }

    private void MostrarLegalidad(CasillaViewModel c)
    {
        if (c.Entidad is not { } pk) return;
        var datos = ModeloInformeLegalidad.Crear(pk, c.TipoAlmacen, AjustesPkhexActuales);
        if (ModeloInformeLegalidad.Hoja(IdiomaInterfaz.F("Cajas.Legalidad", c.NombreEspecie), datos, _anfitrion?.Portapapeles) is { } hoja) Mostrar(hoja);
    }

    // ---- Menu de la caja (BoxMenuStrip) y de la pestana ----
    /// <summary>El menu ⋮ de la pestana: Deshacer y Rehacer del Core, las operaciones de la caja (ordenar, limpiar, modificar, nombre y fondo, volcar y cargar), la busqueda y la vista de todas las cajas.</summary>
    [RelayCommand]
    public void AbrirMenuMas()
    {
        var sav = Sesion.Guardado;
        var secciones = new List<SeccionHoja>
        {
            new(IdiomaInterfaz.T("Cajas.Cambios"),
            [
                new(IdiomaInterfaz.T("SubeditorHoja.Deshacer"), PuedeDeshacer ? IdiomaInterfaz.T("Cajas.DevuelveUltimoCambioCasillas") : null, "Undo2", Deshacer, Motivo: PuedeDeshacer ? null : IdiomaInterfaz.T("EditorCasillas.NadaDeshacer")),
                new(IdiomaInterfaz.T("Cajas.Rehacer"), PuedeRehacer ? IdiomaInterfaz.T("Cajas.VuelveAplicarAcabasDeshacer") : null, "Redo2", Rehacer, Motivo: PuedeRehacer ? null : IdiomaInterfaz.T("EditorCasillas.NadaRehacer")),
            ]),
        };
        secciones.AddRange(SeccionShowdown());
        if (TieneCajas)
        {
            secciones.Add(new SeccionHoja(IdiomaInterfaz.F("Cajas.Caja", NombreCaja),
            [
                new(IdiomaInterfaz.T("Cajas.Ordenar"), IdiomaInterfaz.T("Cajas.EspecieNivelFechaTipo"), "ArrowUpDown", () => AbrirCategoriaManip(CategoriaCaja.Ordenar)),
                new(IdiomaInterfaz.T("Cajas.OrdenarAvanzado"), IdiomaInterfaz.T("Cajas.PotencialEntrenamientoVersionLegalidad"), "ArrowUpDown", () => AbrirCategoriaManip(CategoriaCaja.OrdenarAvanzado)),
                new(IdiomaInterfaz.T("Cajas.Limpiar"), IdiomaInterfaz.T("Cajas.VaciarCajaBorrarSolo"), "Eraser", () => AbrirCategoriaManip(CategoriaCaja.Limpiar), Destructiva: true),
                new(IdiomaInterfaz.T("Cajas.Modificar"), IdiomaInterfaz.T("Cajas.NivelMaximoCurarQuitar"), "Wand", () => AbrirCategoriaManip(CategoriaCaja.Modificar)),
                new(IdiomaInterfaz.T("Cajas.NombreFondo"), IdiomaInterfaz.T("Cajas.CambiaNombreFondoCajas"), "Pencil", AbrirNombresYFondos),
                new(IdiomaInterfaz.T("Cajas.SeleccionarCaja"), IdiomaInterfaz.T("Cajas.MarcaTodosPokemonCaja"), "ListChecks", SeleccionarCaja),
                new(IdiomaInterfaz.T("Cajas.VolcarCaja"), IdiomaInterfaz.T("Cajas.GuardaCajaEnteraArchivo"), "Download", () => VolcarCaja(todas: false), Motivo: _anfitrion?.Documentos is null ? IdiomaInterfaz.T("Cajas.ExportarDisponibleAqui") : null),
                new(IdiomaInterfaz.T("Cajas.CargarCaja2"), IdiomaInterfaz.T("Cajas.PoneCajaContenidoArchivo"), "Upload", () => CargarVolcado(), Motivo: _anfitrion?.Documentos is null ? IdiomaInterfaz.T("Cajas.ImportarDisponibleAqui") : null),
                EntradaExportarCaja(),
            ]));
            secciones.AddRange(SeccionPokekeepCaja());
            secciones.Add(new SeccionHoja(IdiomaInterfaz.T("Cajas.TodasCajas"),
            [
                new(IdiomaInterfaz.T("Cajas.ElegirCaja"), IdiomaInterfaz.T("Cajas.ListaTodasCajasNombre"), "Grid3x3", AbrirSelectorCaja),
                new(IdiomaInterfaz.T("Cajas.BuscarCajas2"), IdiomaInterfaz.T("Cajas.ResaltaPokemonCumplenCriterios"), "Search", AbrirBusqueda),
                new(IdiomaInterfaz.T("Cajas.VolcarTodasCajas"), IdiomaInterfaz.T("Cajas.GuardaTodoPCArchivo"), "Download", () => VolcarCaja(todas: true), Motivo: _anfitrion?.Documentos is null ? IdiomaInterfaz.T("Cajas.ExportarDisponibleAqui") : null),
                new(IdiomaInterfaz.T("Cajas.ImportarArchivo"), IdiomaInterfaz.T("Cajas.PokemonCajasGruposArchivo"), "FileUp", () => ImportarArchivo(null), Motivo: _anfitrion?.Documentos is null ? IdiomaInterfaz.T("Cajas.ImportarDisponibleAqui") : null),
                EntradaExportarCajas(),
                EntradaCargarCajas(),
            ]));
        }
        else
        {
            secciones.Add(new SeccionHoja(IdiomaInterfaz.T("Cajas.Partida"), [new(IdiomaInterfaz.T("Cajas.Buscar2"), IdiomaInterfaz.T("Cajas.ResaltaPokemonCumplenCriterios"), "Search", AbrirBusqueda)]));
        }
        Mostrar(new MenuHojaViewModel(IdiomaInterfaz.T("Cajas.Opciones"), secciones, sav.HasBox ? null : IdiomaInterfaz.T("Cajas.PartidaTieneCajas")));
    }

    /// <summary>Una categoria del menu de caja: sus entradas, con los dos interruptores que PKHeX da con Mayus («todas las cajas») y Ctrl («invertir»).</summary>
    public void AbrirCategoriaManip(CategoriaCaja categoria)
    {
        var entradas = MenuCaja.Entradas(categoria);
        var todas = new InterruptorHoja(IdiomaInterfaz.T("Cajas.AplicarTodasCajas"), IdiomaInterfaz.T("Cajas.SoloCajaEstasViendo"));
        var invertir = new InterruptorHoja(categoria == CategoriaCaja.Limpiar ? IdiomaInterfaz.T("Cajas.InvertirCriterio") : IdiomaInterfaz.T("Cajas.OrdenInverso"), categoria == CategoriaCaja.Limpiar ? IdiomaInterfaz.T("Cajas.BorraCumpleElegido") : IdiomaInterfaz.T("Cajas.MayorMenorReves"));
        var inter = categoria is CategoriaCaja.Modificar ? new[] { todas } : new[] { todas, invertir };
        var filas = entradas.Select(e => new EntradaHoja(e.Titulo, e.Descripcion, GlifoManip(e), () => ConfirmarManip(e, todas.Activo, invertir.Activo), e.Destructiva)).ToList();
        if (filas.Count == 0) filas.Add(new EntradaHoja(IdiomaInterfaz.T("Cajas.NadaDisponible"), null, "Info", null, Motivo: IdiomaInterfaz.T("Cajas.PartidaAdmiteNingunaEstas")));
        Mostrar(new MenuHojaViewModel(MenuCaja.NombreCategoria(categoria), [new SeccionHoja(null, filas)], NombreCaja, inter, volver: AbrirMenuMas));
    }

    private static string GlifoManip(EntradaManip e) => e.Categoria switch
    {
        CategoriaCaja.Limpiar => "Eraser",
        CategoriaCaja.Modificar => "Wand",
        _ => "ArrowUpDown",
    };

    private void ConfirmarManip(EntradaManip e, bool todas, bool invertir)
    {
        var prompt = MenuCaja.Pregunta(e, todas);
        var detalle = $"{e.Titulo}{(invertir ? " (invertido)" : string.Empty)} · {(todas ? IdiomaInterfaz.T("Cajas.TodasCajas2") : NombreCaja)}.";
        Mostrar(new ConfirmarHojaViewModel(e.Titulo, [string.IsNullOrWhiteSpace(prompt) ? IdiomaInterfaz.T("Cajas.Continuar") : prompt, detalle, IdiomaInterfaz.T("Cajas.PuedesDeshacerloDeshacer")], IdiomaInterfaz.T("Editor.Aplicar"), () => _ = EjecutarManipAsync(e, todas, invertir), destructiva: e.Destructiva));
    }

    private async Task EjecutarManipAsync(EntradaManip e, bool todas, bool invertir)
    {
        var espera = new TrabajandoHojaViewModel(e.Titulo, IdiomaInterfaz.T("Cajas.AplicandoEstoPuedeTardar"));
        Mostrar(espera);
        ResultadoManip r;
        try { r = await MenuCaja.EjecutarAsync(e, Caja, todas, invertir); }
        catch (Exception ex) { r = new ResultadoManip(InformeOperacion.Falla(TipoResultado.Fallo, IdiomaInterfaz.T("Editor.PodidoAplicar") + ex.GetType().Name + IdiomaInterfaz.T("Cajas.CambiadoNada")), Caja, Caja); }
        Ocultar();
        Resultado(r.Informe);
    }

    /// <summary>La lista de todas las cajas (la vista «Todas las cajas» de PKHeX, adaptada): nombre, ocupacion y fondo; tocar una lleva a ella.</summary>
    [RelayCommand]
    public void AbrirSelectorCaja()
    {
        if (!TieneCajas) return;
        var sav = Sesion.Guardado;
        var filas = new List<FilaCaja>();
        for (var b = 0; b < TotalCajas; b++)
        {
            var datos = sav.GetBoxData(b);
            filas.Add(new FilaCaja(b, Sesion.NombreCaja(b), datos.Count(p => p.Species != 0), Sesion.CasillasPorCaja, Sesion.FondoCaja(b), b == Caja, Design.Sprites.MiniaturaFondo(sav, Sesion.FondoCaja(b))));
        }
        Mostrar(new SelectorCajaHojaViewModel(IdiomaInterfaz.T("Cajas.ElegirCaja2"), filas, IrACaja, nombresYFondos: AbrirNombresYFondos, subtitulo: IdiomaInterfaz.F("ExportarCajas.Cajas", TotalCajas)));
    }

    /// <summary>«Nombre y fondo» de la caja actual (<c>SAV_BoxLayout</c>): renombrar, fondo, mover y desbloquear.</summary>
    public void AbrirNombresYFondos()
    {
        if (!TieneCajas) return;
        var diseno = new DisenoCajas(Sesion);
        Mostrar(new DisenoCajaHojaViewModel(diseno, Caja, TotalCajas, i => Design.Sprites.MiniaturaFondo(Sesion.Guardado, i), cajaIdx => Sesion.FondoCaja(cajaIdx),
            r => Resultado(r), n => { if (n != Caja) CargarCaja(n, seleccionar: false); }));
    }
}
