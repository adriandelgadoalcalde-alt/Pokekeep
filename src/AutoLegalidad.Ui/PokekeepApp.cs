// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Ui.Bulk;
using AutoLegalidad.Ui.Controls;
using AutoLegalidad.Ui.Library;
using AutoLegalidad.Ui.Operation;

namespace AutoLegalidad.Ui;

/// <summary>
/// Raíz de composición de Pokekeep dentro de PKHeX.WinForms: crea el acceso al motor, el coordinador, la barra de operación y la Librería a partir de un
/// <see cref="IPokekeepHost"/>. El parche de WinForms solo tiene que crear esto una vez (en <c>Main</c>) y llamar a sus métodos desde los puntos de integración.
/// </summary>
public sealed class PokekeepApp : IDisposable
{
    public PokekeepApp(IWinFormsHost winFormsHost, PokekeepSettings settings, IEngineGateway? gateway = null, string? catalogPath = null)
    {
        Settings = settings;
        var host = new WinFormsHostAdapter(winFormsHost);
        // 0.6.6 (A3): los analisis de legalidad de las operaciones masivas, que corren en el pool, se ejecutan en este hilo (el de la interfaz de PKHeX), uno a uno.
        if (SynchronizationContext.Current is WindowsFormsSynchronizationContext uiContext) LegalityCheck.BindUiThread(uiContext, Environment.CurrentManagedThreadId);
        var catalog = catalogPath ?? PokekeepPaths.ResolveCatalog(settings);
        Gateway = gateway ?? new WorkerGateway(PokekeepPaths.ResolveWorker(settings), catalog);
        Coordinator = new PokekeepCoordinator(host, Gateway, () => Settings);
        Bar = new OperationBar(Coordinator);
        Data = new LibraryData(catalog);
        // Hito 4b: operaciones masivas («Completar el juego», «Edición masiva») con sus ventanas; la barra muestra su resumen.
        Bulk = new BulkCoordinator(host, Gateway, () => Settings, Data, Coordinator);
        Coordinator.ExternalBusy = () => Bulk.IsBusy;
        Dialogs = new BulkDialogs(Bulk);
        Bar.AttachBulk(Bulk, Dialogs);
    }

    public PokekeepSettings Settings { get; }
    public IEngineGateway Gateway { get; }
    public PokekeepCoordinator Coordinator { get; }
    public OperationBar Bar { get; }
    public LibraryData Data { get; }
    public BulkCoordinator Bulk { get; }
    public BulkDialogs Dialogs { get; }

    /// <summary>Una vista de la Librería enlazada a este coordinador (la pestaña de PKHeX o una ventana).</summary>
    public LibraryView CreateLibraryView() => new(Coordinator, Data);

    public LibraryWindow OpenLibraryWindow(IWin32Window? owner)
    {
        var window = new LibraryWindow(Coordinator, Data);
        if (owner is Form f) window.Show(f); else window.Show();
        return window;
    }

    public void Dispose()
    {
        Bulk.Dispose();
        Coordinator.Dispose();
        Bar.Dispose();
        Data.Dispose();
    }
}
