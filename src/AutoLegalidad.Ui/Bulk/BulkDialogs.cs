// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Ui.Operation;
using PKHeX.Core;

namespace AutoLegalidad.Ui.Bulk;

/// <summary>
/// Orquesta las ventanas de las operaciones masivas (Hito 4b): vista previa / diálogo de edición (modal) -> progreso (modal con Cancelar) -> resumen en la barra y informe (no modal).
/// La lógica vive en <see cref="BulkCoordinator"/>; aquí solo se enlazan las ventanas. Los puntos de integración de PKHeX.WinForms (menú Herramientas, clic derecho de caja y de casilla) llaman a estos métodos.
/// </summary>
public sealed class BulkDialogs(BulkCoordinator coordinator)
{
    private BulkReportForm? _report;

    public BulkCoordinator Coordinator { get; } = coordinator;
    public BulkReportForm? ReportForm => _report;
    /// <summary>Última ventana modal abierta por el flujo (para pruebas y capturas).</summary>
    public Form? CurrentDialog { get; private set; }
    public event Action<Form>? DialogShown;

    // ============================================================================================ Completar el juego
    public async Task<BulkOutcome?> OpenLivingDexAsync(IWin32Window? owner)
    {
        var av = Coordinator.GetLivingDexAvailability();
        if (!av.Enabled)
        {
            MessageBox.Show(owner, av.Reason ?? Textos.SinPartida, Textos.CompletarElJuego, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return null;
        }
        var settings = Coordinator.Settings;
        IReadOnlySet<(int, int)>? owned = null;
        try
        {
            using (new WaitCursor())
                owned = await Coordinator.ScanOwnedAsync().ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            owned = null;      // sin análisis de lo que ya hay: «Omitir lo que ya tengo» no omite nada (se genera igual)
        }
        var initial = new LivingDexOptions { Forms = settings.LivingDexForms, Order = settings.LivingDexOrder, SkipOwned = settings.LivingDexSkipOwned, PreferEvent = settings.PreferEventVersion, IncludeHome = settings.AllowHomeTrackerless, StartBox = Math.Max(0, Coordinator.Host.CurrentBox) };
        initial = initial with { StartBox = 0 };    // por defecto la caja 1 (diseño §1.6)
        using var form = new LivingDexPreviewForm(Coordinator, owned, initial, settings.BulkWorkers);
        Show(form);
        if (form.ShowDialog(owner) != DialogResult.OK || form.Plan is null) { CurrentDialog = null; return null; }
        CurrentDialog = null;
        var opts = form.Options;
        settings.LivingDexForms = opts.Forms;
        settings.LivingDexOrder = opts.Order;
        settings.LivingDexSkipOwned = opts.SkipOwned;
        settings.PreferEventVersion = opts.PreferEvent;
        settings.BulkWorkers = form.Workers;
        return await RunWithProgressAsync(owner, Textos.CompletarElJuego, edit: false, workers => Coordinator.RunLivingDexAsync(form.Plan, workers, n => AskPlacement(owner, n, edit: false)), form.Workers).ConfigureAwait(true);
    }

    // ============================================================================================ Edición masiva
    /// <param name="initial">Ámbito preseleccionado: Todo (Herramientas), Cajas (clic derecho de caja) o Single (clic derecho de casilla).</param>
    public async Task<BulkOutcome?> OpenEditAsync(IWin32Window? owner, BulkScopeKind initial = BulkScopeKind.All, ISlotInfo? slot = null, int? box = null)
    {
        var av = Coordinator.GetEditAvailability();
        if (!av.Enabled)
        {
            MessageBox.Show(owner, av.Reason ?? Textos.SinPartida, Textos.EdicionMasiva, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return null;
        }
        using var form = new BulkEditForm(Coordinator, initial, slot, box, Coordinator.Settings.BulkWorkers);
        Show(form);
        if (form.ShowDialog(owner) != DialogResult.OK) { CurrentDialog = null; return null; }
        CurrentDialog = null;
        Coordinator.Settings.BulkWorkers = form.Workers;
        EventSwapPlan? swaps = null;
        if (form.PreferEvent)
        {
            // Hito 5d (ADR 0046): primero se BUSCA la versión de evento de cada legendario/singular (con el motor real, sin tocar la partida) y se enseña una vista previa donde se desmarca uno a uno.
            swaps = await PlanEventSwapsWithProgressAsync(owner, form.Selection, form.Operations, form.Workers).ConfigureAwait(true);
            if (swaps is null || swaps.Cancelled) return null;
            if (swaps.Problem is { Length: > 0 } problem)
            {
                MessageBox.Show(owner, problem, Textos.EdicionMasiva, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return null;
            }
            using var preview = new EventSwapPreviewForm(swaps, form.Operations.Any);
            Show(preview);
            var accepted = preview.ShowDialog(owner) == DialogResult.OK;
            CurrentDialog = null;
            if (!accepted) return null;
        }
        var plan = Coordinator.PlanEdit(form.Selection, form.Operations, swaps);
        return await RunWithProgressAsync(owner, Textos.EdicionMasiva, edit: true, workers => Coordinator.RunEditAsync(plan, workers, n => AskPlacement(owner, n, edit: true)), form.Workers).ConfigureAwait(true);
    }

    private async Task<EventSwapPlan?> PlanEventSwapsWithProgressAsync(IWin32Window? owner, BulkSelection selection, Contracts.AdjustOptions operations, int workers)
    {
        using var progress = new BulkProgressForm(Coordinator, "Preferir la versión de evento");
        Show(progress);
        var task = Coordinator.PlanEventSwapsAsync(selection, operations, workers);
        _ = task.ContinueWith(_ => { if (!progress.IsDisposed) progress.Finish(); }, TaskScheduler.FromCurrentSynchronizationContext());
        if (!task.IsCompleted)
            progress.ShowDialog(owner);
        CurrentDialog = null;
        return await task.ConfigureAwait(true);
    }

    // ============================================================================================ Común
    private async Task<BulkOutcome?> RunWithProgressAsync(IWin32Window? owner, string title, bool edit, Func<int, Task<BulkOutcome>> start, int workers)
    {
        using var progress = new BulkProgressForm(Coordinator, title);
        Show(progress);
        var task = start(workers);
        _ = task.ContinueWith(_ => { if (!progress.IsDisposed) progress.Finish(); }, TaskScheduler.FromCurrentSynchronizationContext());
        if (!task.IsCompleted)
            progress.ShowDialog(owner);
        CurrentDialog = null;
        var outcome = await task.ConfigureAwait(true);
        return outcome;
    }

    private Task<bool> AskPlacement(IWin32Window? owner, int count, bool edit)
    {
        using var dialog = new BulkPlacementDialog(count, edit);
        Show(dialog);
        var answer = dialog.ShowDialog(CurrentOwner(owner)) == DialogResult.Yes;
        return Task.FromResult(answer);
    }

    private IWin32Window? CurrentOwner(IWin32Window? fallback) => Form.ActiveForm ?? fallback;

    /// <summary>Muestra (o reutiliza) la ventana del informe, no modal.</summary>
    public void ShowReport(BulkOutcome outcome, IWin32Window? owner)
    {
        if (_report is null || _report.IsDisposed) _report = new BulkReportForm();
        _report.Present(outcome.Report, owner as Form);
    }

    private void Show(Form f)
    {
        CurrentDialog = f;
        f.Shown += (_, _) => DialogShown?.Invoke(f);
    }

    private sealed class WaitCursor : IDisposable
    {
        private readonly Cursor? _previous = Cursor.Current;
        public WaitCursor() => Cursor.Current = Cursors.WaitCursor;
        public void Dispose() => Cursor.Current = _previous;
    }
}
