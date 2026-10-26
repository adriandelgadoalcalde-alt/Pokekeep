// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using PKHeX.Core;
using PKHeX.Drawing.PokeSprite.Properties;

namespace AutoLegalidad.Ui;

/// <summary>
/// Lo que PKHeX.WinForms ofrece de verdad al adaptador de escritorio (la forma ANTIGUA de <see cref="IPokekeepHost"/>, con los tipos de WinForms). El parche de WinForms
/// (0103) lo implementa sobre <c>SAVEditor</c> y <c>Main</c>; <see cref="WinFormsHostAdapter"/> lo convierte al contrato sin WinForms de <c>AutoLegalidad.App</c>.
/// </summary>
public interface IWinFormsHost
{
    /// <summary>Ventana principal (propietaria de los diálogos). Puede ser null en pruebas.</summary>
    IWin32Window? Window { get; }

    /// <summary>Entorno de edición ACTUAL de PKHeX. Cambia al abrir otro guardado.</summary>
    SaveDataEditor<PictureBox> Env { get; }

    int CurrentBox { get; }
    bool EditorIsCompleteIgnoringStats { get; }
    void ClearSlotHighlights();
    void NotifyUndoRedoChanged();
}

/// <summary>
/// <see cref="IEditEnvironment"/> sobre el <c>SaveDataEditor&lt;PictureBox&gt;</c> de PKHeX. Es SIEMPRE el mismo objeto para el mismo editor (el coordinador compara la referencia
/// para saber si se abrió otro guardado: regla 7), por eso se guarda en una tabla débil.
/// </summary>
public sealed class WinFormsEditEnvironment : IEditEnvironment, ISlotEditor
{
    private static readonly ConditionalWeakTable<SaveDataEditor<PictureBox>, WinFormsEditEnvironment> Cache = [];

    private readonly SaveDataEditor<PictureBox> _env;
    private WinFormsEditEnvironment(SaveDataEditor<PictureBox> env) => _env = env;

    public static WinFormsEditEnvironment Wrap(SaveDataEditor<PictureBox> env) => Cache.GetValue(env, e => new WinFormsEditEnvironment(e));

    /// <summary>El entorno de PKHeX que hay detrás (para la parte de la interfaz de escritorio que sí lo necesita).</summary>
    public SaveDataEditor<PictureBox> Inner => _env;

    public SaveFile SAV => _env.SAV;
    public ISlotEditor Slots => this;
    public IPKMView Editor => _env.PKMEditor;

    SlotTouchResult ISlotEditor.Set(ISlotInfo slot, PKM pk) => _env.Slots.Set(slot, pk, SlotWrites.Exact);   // sin importar al guardado (SlotWrites)
    bool ISlotEditor.Batch(IEnumerable<ISlotInfo> slots, Action<IReadOnlyList<ISlotInfo>> action) => _env.Slots.Batch(slots, action);
    void ISlotEditor.Undo() => _env.Slots.Undo();
}

/// <summary>Adaptador de escritorio: convierte el anfitrión de PKHeX.WinForms al contrato <see cref="IPokekeepHost"/> sin tipos de WinForms.</summary>
public sealed class WinFormsHostAdapter(IWinFormsHost host) : IPokekeepHost
{
    public IWinFormsHost Inner => host;
    public IEditEnvironment Env => WinFormsEditEnvironment.Wrap(host.Env);
    public int CurrentBox => host.CurrentBox;
    public bool EditorIsCompleteIgnoringStats => host.EditorIsCompleteIgnoringStats;
    public void ClearSlotHighlights() => host.ClearSlotHighlights();
    public void NotifyUndoRedoChanged() => host.NotifyUndoRedoChanged();
}

/// <summary>Mensajes de escritorio (<see cref="MessageBox"/>) con la ventana principal como propietaria.</summary>
public sealed class WinFormsDialogs(IWin32Window? owner) : IDialogs
{
    public void Info(string title, string message) => MessageBox.Show(owner, message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
    public void Warning(string title, string message) => MessageBox.Show(owner, message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
    public bool Confirm(string title, string message) => MessageBox.Show(owner, message, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
}

/// <summary>Sprites de PKHeX.Drawing.PokeSprite como PNG por nombre de recurso.</summary>
public sealed class WinFormsSpriteSource : ISpriteSource
{
    public byte[]? TryGetPng(string key)
    {
        if (Resources.ResourceManager.GetObject(key) is not Bitmap bitmap) return null;
        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }
}
