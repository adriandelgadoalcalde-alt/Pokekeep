// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using PKHeX.Core;

namespace AutoLegalidad.Ui;

/// <summary>
/// <see cref="IEditEnvironment"/> directo sobre el <c>SlotEditor</c> del Core, sin ningún tipo gráfico: el entorno de las pruebas y de los bancos, y la base del modelo de cajas de Android
/// (donde el guardado abierto, su historial y el borrador del editor viven en el modelo, no en controles). Es SIEMPRE el mismo objeto mientras el guardado sea el mismo.
/// </summary>
public sealed class CoreEditEnvironment : IEditEnvironment, ISlotEditor
{
    private readonly SlotEditor<object> _slots;

    public CoreEditEnvironment(SaveFile sav, IPKMView editor) : this(sav, editor, new SlotEditor<object>(sav)) { }

    /// <summary>Con el historial de casillas YA existente de la partida (Android, F4: el editor, el menu de caja y las acciones del motor comparten un unico historial, asi hay un solo Deshacer coherente).</summary>
    public CoreEditEnvironment(SaveFile sav, IPKMView editor, SlotEditor<object> slots)
    {
        SAV = sav;
        Editor = editor;
        _slots = slots;
    }

    public SaveFile SAV { get; }
    public IPKMView Editor { get; }

    /// <summary>El mismo objeto como <see cref="ISlotEditor"/> (con el historial y el Rehacer del Core a mano para quien necesite más que el contrato).</summary>
    public CoreEditEnvironment Slots => this;
    ISlotEditor IEditEnvironment.Slots => this;

    /// <summary>Historial del Core (¿se puede deshacer / rehacer?).</summary>
    public SlotChangelog Changelog => _slots.Changelog;

    public SlotTouchResult Set(ISlotInfo slot, PKM pk) => _slots.Set(slot, pk, SlotWrites.Exact);   // sin importar al guardado (SlotWrites)
    public bool Batch(IEnumerable<ISlotInfo> slots, Action<IReadOnlyList<ISlotInfo>> action) => _slots.Batch(slots, action);
    public void Undo() => _slots.Undo();
    public void Redo() => _slots.Redo();
}
