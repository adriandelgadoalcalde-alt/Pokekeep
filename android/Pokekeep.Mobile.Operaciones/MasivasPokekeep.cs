// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Diagnostics;
using AutoLegalidad.Catalog.Library;
using AutoLegalidad.Contracts;
using AutoLegalidad.Ui;
using AutoLegalidad.Ui.Bulk;
using AutoLegalidad.Ui.Library;
using AutoLegalidad.Ui.Operation;
using PKHeX.Core;
using Pokekeep.Mobile.Ui;
using Pokekeep.Mobile.Ui.Ajustes;
using Pokekeep.Mobile.Ui.Ajustes.Pkhex;
using Pokekeep.Mobile.Ui.Cajas;
using Pokekeep.Mobile.Ui.Masivas;
using Pokekeep.Mobile.Ui.Modelo;
using Pokekeep.Mobile.Ui.Localizacion;

namespace Pokekeep.Mobile.Operaciones;

/// <summary>
/// Las operaciones masivas en el móvil (F9-0, ADR 0069): el MISMO <see cref="BulkCoordinator"/> del escritorio sobre el entorno de edición del Core de la sesión (el mismo historial único que usan las cajas, el editor y las
/// acciones de una casilla) y el <see cref="IEngineGateway"/> que se le dé (en Android, el proceso <c>:motor</c>). El coordinador fija el destino, pide al motor un Pokémon cada vez, hace la segunda opinión del Core y escribe todo
/// en UNA transacción con un único Deshacer; los textos del informe y de cada estado son los del núcleo (<c>BulkStatusText</c>, <c>Textos</c>), no se reescribe ninguno aquí.
/// <para>
/// Lo propio del móvil: (1) comparte con <see cref="AccionesPokekeep"/> el semáforo de «una operación a la vez» y la sesión activa, de modo que abrir otra partida mientras trabaja el motor da «Resultado descartado» (regla 7);
/// (2) lo escrito se anota como UNA entrada del historial de la sesión y su Deshacer es EXACTO (si lo último del historial es el lote se deshace ESA entrada; si no, solo si todas las casillas siguen como las dejó Pokekeep);
/// (3) los procesos en paralelo nunca superan <see cref="ProcesosMaximos"/> (1 mientras no haya pool de procesos del motor: ADR 0070).
/// </para>
/// </summary>
public sealed partial class MasivasPokekeep(AccionesPokekeep acciones, IEngineGateway motor, IAjustesPokekeep ajustes, LibraryData? datos = null, Func<int>? procesosMaximos = null) : IMasivas
{
    public string? NoDisponible => motor.UnavailableReason;
    public bool Ocupado => acciones.Ocupado;
    public int ProcesosMaximos => Math.Max(1, procesosMaximos?.Invoke() ?? 1);

    public void SesionActiva(Sesion? sesion) => acciones.SesionActiva(sesion);

    public IReadOnlyList<OperacionMasivaInfo> Operaciones() => [.. BulkOperationCatalog.All.Select(o => new OperacionMasivaInfo(o.Key, o.Label, o.DefaultOn, o.Help, o.ShortName))];

    public IReadOnlyList<string> OrdenDeEjecucion() => AdjustOperationNames.Order;

    public TimeSpan Estimar(int unidades, int procesos) => BulkEditPlan.EstimateFor(unidades, Math.Min(procesos, ProcesosMaximos));

    private BulkCoordinator Coordinador(Sesion sesion) => new(acciones.Entornos.Anfitrion(sesion, acciones.Entornos.De(sesion).Env, 0), motor, acciones.Ajustes, datos, dataLanguage: () => ProgramaPkhex.Idioma);

    public string? MotivoEdicion(Sesion sesion)
    {
        if (acciones.Ocupado) return Textos.OperacionEnCurso;
        using var coord = Coordinador(sesion);
        var av = coord.GetEditAvailability();
        return av.Enabled ? null : av.Reason;
    }

    private static BulkSelection Seleccion(SaveFile sav, AlcanceMasivo alcance) => alcance.Ambito switch
    {
        AmbitoMasivo.Todo => BulkScope.ForAll(sav),
        AmbitoMasivo.Ilegales => BulkScope.ForMarked(sav, BulkScope.ForAll(sav).Included
            .Where(s => EsIlegalActual(s.Pokemon, s.Slot.Type)).Select(s => s.Slot)),
        AmbitoMasivo.Cajas => BulkScope.ForBoxes(sav, alcance.Cajas ?? [], alcance.IncluirEquipo),
        AmbitoMasivo.Marcados => BulkScope.ForMarked(sav, alcance.Casillas ?? []),
        _ => alcance.Casillas?.FirstOrDefault() is { } una ? BulkScope.ForSingle(sav, una) : BulkScope.ForMarked(sav, []),
    };

    /// <summary>El filtro destructivo se calcula con los datos actuales, sin reutilizar insignias en caché.</summary>
    internal static bool EsIlegalActual(PKM pokemon, StorageSlotType tipo)
        => AnalizadorLegalidad.AnalizarPara(pokemon, tipo) is { Valid: false };

    internal static ResumenAmbito ResumenDe(AmbitoMasivo ambito, BulkSelection s)
        => new(ambito, s.Included.Count, s.SkippedEggs, s.SkippedLocked, s.BoxesTouched, s.PartyIncluded, s.Describe(), [.. s.UntouchedZones.Select(z => new ZonaSinTocar(z.Name, z.Pokemon, z.Reason))]);

    public ResumenAmbito Resolver(Sesion sesion, AlcanceMasivo alcance) => ResumenDe(alcance.Ambito, Seleccion(sesion.Guardado, alcance));

    // ============================================================================================ ejecución
    private static ClaseMasiva Clase(BulkOutcome o) => o.Result switch
    {
        OutcomeKind.Applied => ClaseMasiva.Aplicado,
        OutcomeKind.AppliedNeedsHome => ClaseMasiva.AplicadoNecesitaHome,
        OutcomeKind.Cancelled => ClaseMasiva.Cancelado,
        OutcomeKind.TargetChanged => ClaseMasiva.CambioDeDestino,
        OutcomeKind.EngineFailure => ClaseMasiva.FalloDelMotor,
        OutcomeKind.Busy => ClaseMasiva.Ocupado,
        // Sin nada escrito: «ya estaba todo bien» (todos óptimos, no aplicables...) no es un fallo; si alguna fila falló, sí.
        OutcomeKind.NotApplied => o.Report.Rows.Any(r => EstadosMasivos.EsError(r.Status.ToString())) ? ClaseMasiva.NoSePudo : ClaseMasiva.SinCambios,
        _ => ClaseMasiva.NoSePudo,
    };

    /// <summary>El informe del núcleo a filas de la interfaz: mismos estados, mismas etiquetas, mismos símbolos; el texto plano y el CSV son los del núcleo (<c>ToText</c>, <c>ToCsv</c>).</summary>
    internal static InformeMasivo Mapear(BulkReport r) => new(r.Title, r.IsEdit, r.StatWord, [.. r.Rows.Select(f => new FilaInformeMasivo(
        f.Number, f.Pokemon, f.Form, f.Status.ToString(), (int)f.Status, BulkStatusText.Label(f.Status), f.Slot, f.Origin, f.Level, f.Ivs, f.Language, f.Reason,
        [.. f.Operations.Select(o => new OperacionDeFila(o.Operation, BulkStatusText.Name(o.Operation), BulkStatusText.Symbol(o.Status), o.Status.ToString(), TextosMotor.Traducir(o.Message) ?? o.Message, TextosMotor.Traducir(o.Reason)))], f.Details))],
        r.ToText(), r.ToCsv());

    internal static ProgresoMasivo Mapear(BulkProgressInfo p, TimeSpan reloj) => p.Progress is { } b
        ? new ProgresoMasivo(p.Phase, b.Done, b.Total, b.InFlight, b.Current, b.Workers, b.Elapsed, b.Remaining, p.Recent)
        : new ProgresoMasivo(p.Phase, 0, 0, 0, string.Empty, 1, reloj, null, p.Recent);

    /// <summary>Deshacer de un lote: exacto por el historial si lo último es el lote; si no, solo si todas las casillas siguen como las dejó Pokekeep (y entonces es una entrada más, como en el escritorio).</summary>
    private sealed class DeshacedorLote(BulkUndoToken token, Sesion sesion, int version) : IDeshacerMasivo
    {
        public int Casillas => token.SlotCount;
        public string? Motivo => token.RefusalReason();
        public bool PuedeDeshacer() => token.CanUndo();

        public bool Deshacer()
        {
            if (!token.CanUndo()) return false;
            if (sesion.Casillas.Version == version && sesion.Casillas.PuedeDeshacer)
                return sesion.Casillas.Deshacer().Correcto;
            var hecho = token.Undo();
            if (hecho) sesion.Casillas.Notificar(EditorCasillas.TipoCambio.Masivo, [], IdiomaInterfaz.T("MasivasPokekeep.DeshacerUnaEdicionMasiva"));
            return hecho;
        }
    }

    public Task<ResultadoMasivo> EjecutarEdicionAsync(Sesion sesion, AlcanceMasivo alcance, IReadOnlyCollection<string> operaciones, int procesos,
        IProgress<ProgresoMasivo>? progreso, Func<int, Task<bool>>? colocarGenerados, CancellationToken cancelar)
        => EjecutarEdicionAsync(sesion, alcance, operaciones, procesos, null, [], progreso, colocarGenerados, cancelar);

    // ============================================================================================ bloque 9a: ámbitos, textos y versión de evento
    public TextosEdicionMasiva TextosEdicion => _textosEdicion.Value;
    private readonly PorIdioma<TextosEdicionMasiva> _textosEdicion = new(() => new(
        BulkEditTexts.ScopeLabel(BulkScopeKind.All), BulkEditTexts.ScopeLabel(BulkScopeKind.Boxes), BulkEditTexts.ScopeLabel(BulkScopeKind.Marked), BulkEditTexts.ScopeLabel(BulkScopeKind.Single),
        BulkEditTexts.AllBoxes, BulkEditTexts.NoBoxes, BulkEditTexts.PickPokemon, BulkEditTexts.MarkAll, BulkEditTexts.UnmarkAll, BulkEditTexts.Accept,
        BulkEditTexts.PreferEventGroup, BulkEditTexts.PreferEventLabel, BulkEditTexts.PreferEventHelp, BulkEditTexts.EventSwapTitle, BulkEditTexts.EventSwapWarning,
        BulkEditTexts.Workers, BulkEditTexts.MinWorkers, BulkEditTexts.MaxWorkers, BulkEditTexts.EventSwapColumns, BulkEditTexts.NothingToEdit, BulkEditTexts.ZonesPrefix,
        Textos.MenuPokekeep, BulkEditTexts.MenuThisBox, BulkEditTexts.MenuAllBoxes, BulkEditTexts.MenuThisPokemon, BulkEditTexts.MenuThisPokemonHelp, BulkEditTexts.MenuCompleteGame,
        BulkEditTexts.ScopeGroup, BulkEditTexts.PickerTitle, BulkEditTexts.PickerLevel, Textos.Cancelar));

    public IReadOnlyList<CajaMasiva> Cajas(Sesion sesion)
    {
        var sav = sesion.Guardado;
        if (!sav.HasBox) return [];
        // El recuento es lo que la edición tocaría (sin huevos ni casillas bloqueadas): cuadra con el resumen del ámbito que sale debajo.
        return [.. Enumerable.Range(0, sav.BoxCount).Select(b => new CajaMasiva(b, BulkScope.BoxName(sav, b), BulkScope.ForBoxes(sav, [b], includeParty: false).Included.Count))];
    }

    /// <summary>Lo mismo que el «Elegir Pokémon…» del escritorio (<c>BulkEditForm.PickPokemon</c>): las cajas marcadas o, si no hay ninguna, todas; sin el equipo.</summary>
    public IReadOnlyList<CandidatoMasivo> Candidatos(Sesion sesion, IReadOnlyList<int> cajas)
    {
        var sav = sesion.Guardado;
        var elegidas = cajas.Count > 0 ? cajas : [.. Enumerable.Range(0, sav.HasBox ? sav.BoxCount : 0)];
        return [.. BulkScope.ForBoxes(sav, elegidas, includeParty: false).Included.Select(c => new CandidatoMasivo(c.Slot, BulkScope.Describe(c.Slot), PokekeepCoordinator.SpeciesName(c.Pokemon.Species), c.Pokemon.CurrentLevel))];
    }

    public (string Texto, bool Activo) AplicarEventos(int aceptados, bool otrasOperaciones) => (BulkEditTexts.EventSwapApply(aceptados, otrasOperaciones), BulkEditTexts.EventSwapCanApply(aceptados, otrasOperaciones));

    /// <summary>El plan del núcleo y la selección sobre la que se hizo, detrás de cada plan de la interfaz (la interfaz no referencia el núcleo; la ejecución usa EXACTAMENTE lo que se buscó).</summary>
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<PlanEventosMasivo, PlanNucleo> _planes = new();
    private sealed record PlanNucleo(EventSwapPlan Plan, BulkSelection Seleccion, Sesion Sesion);

    internal static FilaEventoMasivo Mapear(EventSwapItem it) => new(it.Index, BulkScope.Describe(it.Slot), it.PokemonName, it.Category, it.CurrentOrigin, it.Status.ToString(),
        it.Status == EventSwapStatus.Proposed ? it.EventTitle : null, it.Status == EventSwapStatus.Proposed ? it.Year : null, it.Status == EventSwapStatus.Proposed ? it.LanguageDisplay : string.Empty,
        it.PreviewStatus, it.StatusText, it.Describe(), it.DetailLines(), it.NeedsHome);

    internal static PlanEventosMasivo Mapear(EventSwapPlan p) => new([.. p.Items.Select(Mapear)], p.Summary(), p.Cancelled, p.Problem);

    public async Task<PlanEventosMasivo> BuscarVersionesDeEventoAsync(Sesion sesion, AlcanceMasivo alcance, IReadOnlyCollection<string> operaciones, int procesos, IProgress<ProgresoMasivo>? progreso, CancellationToken cancelar)
    {
        if (!await acciones.Exclusion.WaitAsync(0, CancellationToken.None))
            return new PlanEventosMasivo([], string.Empty, Problema: Textos.OperacionEnCurso);
        var reloj = Stopwatch.StartNew();
        try
        {
            var seleccion = Seleccion(sesion.Guardado, alcance);
            using var coord = Coordinador(sesion);
            coord.ProgressChanged += (_, p) => progreso?.Report(Mapear(p, reloj.Elapsed));
            using var registro = cancelar.Register(coord.Cancel);
            var plan = await coord.PlanEventSwapsAsync(seleccion, BulkOperationCatalog.Options(operaciones), Math.Clamp(procesos, 1, ProcesosMaximos));
            var ui = Mapear(plan);
            if (!plan.Cancelled && plan.Problem is null) _planes.AddOrUpdate(ui, new PlanNucleo(plan, seleccion, sesion));
            return ui;
        }
        finally { acciones.Exclusion.Release(); }
    }

    public async Task<ResultadoMasivo> EjecutarEdicionAsync(Sesion sesion, AlcanceMasivo alcance, IReadOnlyCollection<string> operaciones, int procesos, PlanEventosMasivo? eventos, IReadOnlyCollection<int> aceptadas,
        IProgress<ProgresoMasivo>? progreso, Func<int, Task<bool>>? colocarGenerados, CancellationToken cancelar)
    {
        PlanNucleo? nucleo = null;
        if (eventos is not null && (!_planes.TryGetValue(eventos, out nucleo) || !ReferenceEquals(nucleo.Sesion, sesion)))
            return new ResultadoMasivo(ClaseMasiva.NoSePudo, BulkEditTexts.EventSearchStale, BulkEditTexts.EventSearchStaleDetail, InformeMasivo.Vacio(Textos.EdicionMasiva), 0, TimeSpan.Zero, null);
        if (nucleo is not null)
            foreach (var it in nucleo.Plan.Items) it.Include = it.Status == EventSwapStatus.Proposed && aceptadas.Contains(it.Index);

        if (!await acciones.Exclusion.WaitAsync(0, CancellationToken.None))
            return new ResultadoMasivo(ClaseMasiva.Ocupado, Textos.OperacionEnCurso, null, InformeMasivo.Vacio(Textos.EdicionMasiva), 0, TimeSpan.Zero, null);
        var reloj = Stopwatch.StartNew();
        try
        {
            var opciones = BulkOperationCatalog.Options(operaciones);
            // Con versión de evento, la MISMA selección de la búsqueda: sus índices y su huella de cada casilla (regla 7) son los de entonces.
            var seleccion = nucleo?.Seleccion ?? Seleccion(sesion.Guardado, alcance);
            if (!opciones.Any && nucleo?.Plan.Accepted.Count is null or 0)
                return new ResultadoMasivo(ClaseMasiva.SinCambios, IdiomaInterfaz.T("MasivasPokekeep.NoSeHaElegido"), IdiomaInterfaz.T("MasivasPokekeepCompletar.NoSeHaModificado"), InformeMasivo.Vacio(Textos.EdicionMasiva), 0, reloj.Elapsed, null);
            if (seleccion.Included.Count == 0)
                return new ResultadoMasivo(ClaseMasiva.SinCambios, IdiomaInterfaz.T("MasivasPokekeep.NoHayNingunPokemon"), IdiomaInterfaz.T("MasivasPokekeepCompletar.NoSeHaModificado"), InformeMasivo.Vacio(Textos.EdicionMasiva), 0, reloj.Elapsed, null);
            // Las casillas incluidas, con sus bytes de ANTES: tras aplicar se sabe cuáles escribió el lote (para el historial y para refrescar solo lo tocado).
            var antes = seleccion.Included.Select(i => (i.Slot, Datos: i.Pokemon.Data.ToArray())).ToList();

            using var coord = Coordinador(sesion);
            coord.ProgressChanged += (_, p) => progreso?.Report(Mapear(p, reloj.Elapsed));
            using var registro = cancelar.Register(coord.Cancel);
            var plan = coord.PlanEdit(seleccion, opciones, nucleo?.Plan);
            var salida = await coord.RunEditAsync(plan, Math.Clamp(procesos, 1, ProcesosMaximos), colocarGenerados);

            IDeshacerMasivo? deshacer = null;
            if (salida.Applied)
            {
                var tocadas = new List<ISlotInfo>();
                foreach (var (slot, datos0) in antes)
                    if (!slot.Read(sesion.Guardado).Data.SequenceEqual(datos0)) tocadas.Add(slot);
                // UNA entrada MÁS del historial único de la sesión, con su texto: Historial, «cambios pendientes» y Deshacer cuentan lo mismo.
                sesion.Casillas.Notificar(EditorCasillas.TipoCambio.Masivo, tocadas, $"{Textos.EdicionMasiva}: {tocadas.Count} Pokémon");
                if (salida.Undo is { } token) deshacer = new DeshacedorLote(token, sesion, sesion.Casillas.Version);
            }
            else if (salida.Undo is { } t2) deshacer = new DeshacedorLote(t2, sesion, -1);
            return new ResultadoMasivo(Clase(salida), salida.Headline, salida.Detail, Mapear(salida.Report), salida.Written, salida.Elapsed == default ? reloj.Elapsed : salida.Elapsed, deshacer);
        }
        finally { acciones.Exclusion.Release(); }
    }
}
