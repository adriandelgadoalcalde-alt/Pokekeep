// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Runtime.CompilerServices;
using AutoLegalidad.Catalog.Library;
using AutoLegalidad.Ui;
using AutoLegalidad.Ui.Bulk;
using AutoLegalidad.Ui.Controls;
using AutoLegalidad.Ui.Library;
using AutoLegalidad.Ui.Operation;
using PKHeX.Core;
using Pokekeep.Mobile.Ui;
using Pokekeep.Mobile.Ui.Ajustes;
using Pokekeep.Mobile.Ui.Ajustes.Pkhex;
using Pokekeep.Mobile.Ui.Cajas;
using Pokekeep.Mobile.Ui.Modelo;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Operaciones;

/// <summary>
/// «Legalizar automáticamente», «Optimizar stats» y «Añadir a la caja» en el móvil: el MISMO <see cref="PokekeepCoordinator"/> del escritorio sobre un entorno de edición del Core
/// (<see cref="CoreEditEnvironment"/>) y el <see cref="IEngineGateway"/> que se le dé (en Android, el proceso <c>:motor</c>). El coordinador fija el destino, aplica con el historial del Core, relee, revalida y deshace
/// si la validación falla; los textos de cada estado son los de <c>ResultMessages</c> y <c>Textos</c> (no se reescribe ninguno aquí).
/// <para>
/// Lo propio del móvil: (1) el anfitrión mira cuál es la sesión ACTIVA de la app, de modo que abrir otra partida mientras trabaja el motor da «Resultado descartado» (regla 7) y la respuesta tardía no se
/// escribe; (2) lo aplicado se anota en el historial único de la sesión (<see cref="EditorCasillas"/>) y su «Deshacer» es EXACTO: si lo último del historial es esa entrada, se deshace ESA entrada (nada
/// de una escritura nueva que ensucie los «cambios pendientes»); si pasó algo después pero la casilla sigue igual, se escribe el original como una entrada más, igual que en el escritorio.
/// </para>
/// </summary>
public sealed class AccionesPokekeep(IEngineGateway motor, IAjustesPokekeep ajustes, LibraryData? datos = null, Action? nuevaGeneracion = null) : IAccionesPokekeep
{
    private readonly SemaphoreSlim _uno = new(1, 1);
    private EntornosPokekeep? _entornos;

    /// <summary>Los entornos de edición de cada sesión y la sesión activa: LOS MISMOS para las acciones de una casilla y las operaciones masivas (<see cref="MasivasPokekeep"/>), de modo que comparten historial único y detección de «otra partida».</summary>
    internal EntornosPokekeep Entornos => _entornos ??= new EntornosPokekeep(nuevaGeneracion);

    /// <summary>Solo se admite UNA operación de Pokekeep a la vez, sea de una casilla o masiva: el semáforo es común.</summary>
    internal SemaphoreSlim Exclusion => _uno;

    public string? NoDisponible => motor.UnavailableReason;
    public bool Ocupado => _uno.CurrentCount == 0;

    /// <summary>Los ajustes vigentes como los entiende el núcleo (se relee en cada operación: un cambio en Ajustes vale desde la siguiente).</summary>
    public PokekeepSettings Ajustes() => Convertir(ajustes.Actual);

    internal static PokekeepSettings Convertir(AjustesPokekeep a) => new()
    {
        OptimizeStatsOnLegalize = a.OptimizeStatsOnLegalize,
        CoherentHistory = a.CoherentHistory,
        LegalizeTimeoutSeconds = a.LegalizeTimeoutSeconds,
        OptimizeTimeoutSeconds = a.OptimizeTimeoutSeconds,
        AllowHomeTrackerless = a.AllowHomeTrackerless,
        BulkWorkers = a.BulkWorkers,
        LivingDexForms = Enum.Parse<LivingDexForms>(a.LivingDexForms.ToString()),
        LivingDexOrder = Enum.Parse<LivingDexOrder>(a.LivingDexOrder.ToString()),
        LivingDexSkipOwned = a.LivingDexSkipOwned,
        PreferEventVersion = a.PreferEventVersion,
        RegisterInPokedex = a.RegisterInPokedex,
        ShowSummary = a.ShowSummary,
    };

    // ============================================================================================ sesión activa
    /// <summary>La partida que la app tiene delante ahora: si cambia, las respuestas del motor pendientes quedan descartadas (nueva generación de sesión) y las operaciones en curso acaban en «Resultado descartado».</summary>
    public void SesionActiva(Sesion? sesion)
    {
        Entornos.SesionActiva(sesion);
    }

    private EntornoSesion De(Sesion s) => Entornos.De(s);

    private PokekeepCoordinator Coordinador(Sesion sesion, CoreEditEnvironment env, int caja) => new(Entornos.Anfitrion(sesion, env, caja), motor, Ajustes, dataLanguage: () => ProgramaPkhex.Idioma);

    private static ISlotInfo Casilla(Sesion sesion, bool equipo, int caja, int indice) => equipo ? new SlotInfoParty(indice) : new SlotInfoBox(caja, indice, sesion.Guardado);

    // ============================================================================================ disponibilidad
    public string? MotivoCasilla(AccionPokemon accion, Sesion sesion, bool equipo, int caja, int indice)
    {
        if (Ocupado) return Textos.OperacionEnCurso;
        var coord = Coordinador(sesion, De(sesion).Env, caja);
        var av = coord.GetSlotAvailability(Casilla(sesion, equipo, caja, indice));
        return av.Enabled ? null : av.Reason;
    }

    public string? MotivoBorrador(AccionPokemon accion, Sesion sesion, PKM borrador)
    {
        if (Ocupado) return Textos.OperacionEnCurso;
        var env = new CoreEditEnvironment(sesion.Guardado, new BorradorEditor(borrador), sesion.Casillas.Historial);
        var av = Coordinador(sesion, env, 0).GetEditorAvailability();
        return av.Enabled ? null : av.Reason;
    }

    private LibraryRow? Fila(Sesion sesion, string clave) => datos is null ? null : datos.Find(LibraryData.GameIdOf(sesion.Guardado), clave);

    public string? MotivoAnadir(Sesion sesion, string claveEntrada, int cajaActiva, DestinoCasilla? destino)
    {
        if (Ocupado) return Textos.OperacionEnCurso;
        if (datos is null || !datos.IsAvailable) return Textos.CatalogoNoEncontrado;
        var fila = Fila(sesion, claveEntrada);
        var coord = Coordinador(sesion, De(sesion).Env, cajaActiva);
        var av = coord.GetLibraryAvailability(fila);
        if (!av.Enabled) return av.Reason;
        var sav = sesion.Guardado;
        if (destino is { } d)
        {
            if (d.Equipo) return IdiomaInterfaz.T("AccionesPokekeep.AnadirALaCaja");
            ISlotInfo info = new SlotInfoBox(d.Caja, d.Indice, sav);
            if (!info.CanWriteTo(sav) || sav.IsBoxSlotOverwriteProtected(d.Caja, d.Indice)) return Textos.CasillaBloqueada;
            if (!info.IsEmpty(sav)) return Textos.CasillaOcupada;
            return null;
        }
        return PokekeepCoordinator.FindFreeSlot(sav, cajaActiva) is null ? Textos.NoHayHueco : null;
    }

    public string? MotivoElegirCasilla(Sesion sesion, string claveEntrada, int cajaActiva)
    {
        var motivo = MotivoAnadir(sesion, claveEntrada, cajaActiva, null);
        return motivo == Textos.NoHayHueco ? null : motivo;
    }

    // ============================================================================================ ejecución
    private sealed class DeshacedorExacto(UndoToken token, Sesion? sesion, int version) : IAccionDeshacer
    {
        public bool PuedeDeshacer() => token.CanUndo();

        public bool Deshacer()
        {
            if (!token.CanUndo()) return false;
            // Exacto: si lo último que se anotó en el historial es la escritura de esta operación, se deshace ESA entrada del Core (los «cambios pendientes» vuelven a donde estaban).
            if (sesion is not null && sesion.Casillas.Version == version && sesion.Casillas.PuedeDeshacer)
                return sesion.Casillas.Deshacer().Correcto;
            // Después hubo otros cambios pero la casilla sigue como la dejó Pokekeep: se escribe el original (una entrada más del historial, como en el escritorio).
            var hecho = token.Undo();
            if (hecho && sesion is not null) sesion.Casillas.Notificar(EditorCasillas.TipoCambio.Escritura, [], IdiomaInterfaz.T("AccionesPokekeep.DeshacerUnaAccionDe"));
            return hecho;
        }
    }

    private static ClaseResultado Clase(OutcomeKind k) => k switch
    {
        OutcomeKind.Applied => ClaseResultado.Aplicado,
        OutcomeKind.AppliedNeedsHome => ClaseResultado.AplicadoNecesitaHome,
        OutcomeKind.UnchangedNeedsHome => ClaseResultado.SinCambios,
        OutcomeKind.AlreadyLegal or OutcomeKind.AlreadyOptimal => ClaseResultado.YaCorrecto,
        OutcomeKind.NotApplied => ClaseResultado.NoSePudo,
        OutcomeKind.Cancelled => ClaseResultado.Cancelado,
        OutcomeKind.TargetChanged => ClaseResultado.CambioDeDestino,
        OutcomeKind.ValidationFailed => ClaseResultado.ValidacionFallida,
        OutcomeKind.Busy => ClaseResultado.Ocupado,
        _ => ClaseResultado.FalloDelMotor,
    };

    /// <summary>De un resultado del núcleo al de la interfaz: mismos textos, mismas filas de «Ver cambios» (valores legibles con <see cref="ChangeFormatter"/>) y mismas notas.</summary>
    internal static ResultadoAccion Mapear(AccionPokemon accion, OperationResult r, IAccionDeshacer? deshacer, DestinoCasilla? casilla = null) => new(accion, Clase(r.Kind), r.Headline, r.Detail, [.. r.Summary], r.Elapsed, deshacer)
    {
        Origen = r.Origin switch { OperationOrigin.Editor => OrigenAccion.Editor, OperationOrigin.Library => OrigenAccion.Libreria, _ => OrigenAccion.Casilla },
        Objetivo = r.TargetDescription,
        Clasificacion = r.Kind.ToString(),
        Estado = r.Status.ToString(),
        Cambios = [.. r.Changes.Select(c => new CambioCampo(TextosMotor.Campo(c.Field), ChangeFormatter.Describe(c.Field, c.Before, ProgramaPkhex.Idioma), ChangeFormatter.Describe(c.Field, c.After, ProgramaPkhex.Idioma), TextosMotor.Traducir(c.Reason) ?? c.Reason, c.ReasonCode.EndsWith("_RELAXED", StringComparison.Ordinal)))],
        Notas = ChangeNotes.Lines(r),
        Casilla = r.Applied ? casilla : null,
        Intentos = r.Response?.Attempts ?? 0,
    };

    private async Task<ResultadoAccion> Ejecutar(AccionPokemon accion, Sesion sesion, PokekeepCoordinator coord, Func<Task<OperationResult>> lanzar, Func<OperationResult, DestinoCasilla?> destino, bool escribeCasilla,
        IProgress<ProgresoAccion>? progreso, CancellationToken cancelar)
    {
        if (!await _uno.WaitAsync(0, CancellationToken.None))
            return new ResultadoAccion(accion, ClaseResultado.Ocupado, Textos.OperacionEnCurso, null, [], TimeSpan.Zero, null) { Clasificacion = nameof(OutcomeKind.Busy), Estado = nameof(AutoLegalidad.Contracts.ResponseStatus.InvalidInput) };
        void Al(object? remitente, OperationProgress p) => progreso?.Report(new ProgresoAccion(p.Text, p.Fraction));
        coord.ProgressChanged += Al;
        using var registro = cancelar.Register(coord.Cancel);
        try
        {
            var r = await lanzar();
            if (cancelar.IsCancellationRequested && r.Kind is OutcomeKind.Applied or OutcomeKind.AppliedNeedsHome)
            {
                // Cancelar pulsado cuando el resultado ya se estaba aplicando: sigue valiendo lo aplicado (el motor ya terminó); Deshacer lo revierte.
            }
            IAccionDeshacer? deshacer = null;
            var dest = destino(r);
            if (r.Applied && escribeCasilla)
            {
                // Una entrada MÁS del historial único de la sesión (con su texto), de modo que Historial, «cambios pendientes» y Deshacer cuentan lo mismo.
                sesion.Casillas.Notificar(EditorCasillas.TipoCambio.Escritura, [], $"{TituloAccion(accion, r)}: {r.TargetDescription}");
                if (r.Undo is { } t) deshacer = new DeshacedorExacto(t, sesion, sesion.Casillas.Version);
            }
            else if (r.Applied && r.Undo is { } t2) deshacer = new DeshacedorExacto(t2, null, 0);
            return Mapear(accion, r, deshacer, dest);
        }
        finally { coord.ProgressChanged -= Al; coord.Dispose(); _uno.Release(); }
    }

    private static string TituloAccion(AccionPokemon accion, OperationResult r)
        => r.Origin == OperationOrigin.Library ? Textos.AnadidoALaCaja : accion == AccionPokemon.Optimizar ? Textos.OptimizarStats : Textos.LegalizarAutomaticamente;

    public Task<ResultadoAccion> EjecutarEnCasillaAsync(AccionPokemon accion, Sesion sesion, bool equipo, int caja, int indice, IProgress<ProgresoAccion>? progreso, CancellationToken cancelar)
    {
        var coord = Coordinador(sesion, De(sesion).Env, caja);
        var slot = Casilla(sesion, equipo, caja, indice);
        var action = accion == AccionPokemon.Legalizar ? PokekeepAction.Legalize : PokekeepAction.Optimize;
        return Ejecutar(accion, sesion, coord, () => coord.RunOnSlotAsync(action, slot), _ => null, escribeCasilla: true, progreso, cancelar);
    }

    public Task<ResultadoAccion> EjecutarEnBorradorAsync(AccionPokemon accion, Sesion sesion, PKM borrador, IProgress<ProgresoAccion>? progreso, CancellationToken cancelar)
    {
        var env = new CoreEditEnvironment(sesion.Guardado, new BorradorEditor(borrador), sesion.Casillas.Historial);
        var coord = Coordinador(sesion, env, 0);
        var action = accion == AccionPokemon.Legalizar ? PokekeepAction.Legalize : PokekeepAction.Optimize;
        return Ejecutar(accion, sesion, coord, () => coord.RunOnEditorAsync(action), _ => null, escribeCasilla: false, progreso, cancelar);
    }

    public Task<ResultadoAccion> AnadirDeLibreriaAsync(Sesion sesion, string claveEntrada, int cajaActiva, DestinoCasilla? destino, IProgress<ProgresoAccion>? progreso, CancellationToken cancelar)
    {
        if (destino is { Equipo: true })   // mismo criterio que MotivoAnadir: «Añadir a la caja» solo coloca en las cajas
            return Task.FromResult(new ResultadoAccion(AccionPokemon.Legalizar, ClaseResultado.NoSePudo, IdiomaInterfaz.T("AccionesPokekeep.NoSeHaPodido"), IdiomaInterfaz.T("AccionesPokekeep.AnadirALaCaja"), [], TimeSpan.Zero, null) { Origen = OrigenAccion.Libreria, Clasificacion = nameof(OutcomeKind.NotApplied) });
        var coord = Coordinador(sesion, De(sesion).Env, cajaActiva);
        var fila = Fila(sesion, claveEntrada);
        // La casilla se FIJA aquí (la elegida al soltar o la primera libre de la caja activa): el coordinador comprueba que siga vacía al terminar y nunca sobrescribe.
        ISlotInfo? slot = destino is { } d ? new SlotInfoBox(d.Caja, d.Indice, sesion.Guardado) : PokekeepCoordinator.FindFreeSlot(sesion.Guardado, cajaActiva);
        var donde = slot switch { SlotInfoBox b => new DestinoCasilla(false, b.Box, b.Slot), _ => (DestinoCasilla?)null };
        if (fila is null)
            return Task.FromResult(new ResultadoAccion(AccionPokemon.Legalizar, ClaseResultado.NoSePudo, IdiomaInterfaz.T("AccionesPokekeep.NoSeHaPodido"), IdiomaInterfaz.T("AccionesPokekeep.LaEntradaYaNo"), [], TimeSpan.Zero, null) { Origen = OrigenAccion.Libreria, Clasificacion = nameof(OutcomeKind.NotApplied) });
        return Ejecutar(AccionPokemon.Legalizar, sesion, coord, () => coord.AddLibraryEntryAsync(fila, slot), _ => donde, escribeCasilla: true, progreso, cancelar);
    }

    public async Task<VersionesMotor?> VersionesMotorAsync(CancellationToken cancelar)
    {
        // Consulta corta (15 s) al motor: no es una operación ni toca la partida.
        var m = await new PokekeepCoordinator(new AnfitrionVacio(), motor, Ajustes).TryGetManifestAsync(TimeSpan.FromSeconds(15));
        return m is null ? null : new VersionesMotor(m.EngineVersion, m.AdapterVersion, m.AlmVersion, m.AlmCommit, m.CoreVersion, m.CoreCommit, m.CoreDllSha256);
    }

    private sealed class AnfitrionVacio : IPokekeepHost
    {
        public IEditEnvironment Env { get; } = new CoreEditEnvironment(FakeSaveFile.Default, new BorradorEditor(FakeSaveFile.Default.BlankPKM));
        public int CurrentBox => 0;
        public bool EditorIsCompleteIgnoringStats => true;
        public void ClearSlotHighlights() { }
        public void NotifyUndoRedoChanged() { }
    }
}
