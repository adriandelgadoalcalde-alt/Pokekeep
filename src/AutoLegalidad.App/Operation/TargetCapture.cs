// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Catalog.Library;
using AutoLegalidad.Contracts;
using PKHeX.Core;

namespace AutoLegalidad.Ui.Operation;

/// <summary>De dónde viene la operación.</summary>
public enum OperationOrigin
{
    Editor,
    Slot,
    Library,
    /// <summary>Hito 4b: operación masiva («Completar el juego», «Edición masiva»).</summary>
    Bulk,
}

/// <summary>Resultado de comparar el destino capturado con el estado actual de la interfaz.</summary>
public enum Stability
{
    Stable,
    /// <summary>Se abrió o se cerró otra partida (el entorno de edición ya no es el mismo).</summary>
    SaveChanged,
    /// <summary>El contenido de la casilla cambió.</summary>
    SlotChanged,
    /// <summary>El Pokémon del editor cambió.</summary>
    EditorChanged,
    /// <summary>«Añadir a la caja»: la casilla elegida se ocupó.</summary>
    SlotOccupied,
}

/// <summary>
/// Instantánea del destino de una operación, tomada en el hilo de la interfaz al empezar (plan 7, fase A). Todo lo que decide el resultado
/// (qué casilla, qué partida, qué bytes) se fija AQUÍ: ni el último clic ni el Pokémon abierto en el editor lo cambian después. La
/// comparación con el estado actual es lo que produce <c>TargetChanged</c> (el Worker nunca lo decide).
/// </summary>
public abstract class TargetCapture
{
    public required OperationOrigin Origin { get; init; }
    public required IEditEnvironment Env { get; init; }
    /// <summary>Copia de la entidad tal como estaba al capturar.</summary>
    public required PKM Original { get; init; }
    public required string OriginalSha256 { get; init; }
    public SaveFile Sav => Env.SAV;

    /// <summary>Entidad que se manda al motor (la capturada; en la Librería, la plantilla de la especie).</summary>
    public virtual PKM RequestEntity => Original;
    public virtual bool RequestPartySize => EntityBytes.UsesPartySize(Original, SlotType);
    public abstract StorageSlotType SlotType { get; }
    public abstract SelectionInfo SelectionForRequest { get; }

    /// <summary>Descripción corta para mensajes («Caja 2, casilla 5»).</summary>
    public abstract string Describe();

    public abstract Stability Check(IPokekeepHost host);

    protected string HashOf(PKM pk, bool partySize) => EntityBytes.Sha256(EntityBytes.Export(pk, partySize));
}

public static class SelectionInfoFactory
{
    public static SelectionInfo Editor() => new() { Kind = "Editor" };
    public static SelectionInfo Box(int box, int slot) => new() { Kind = "Box", Box = box, Slot = slot };
}

/// <summary>Pokémon abierto en el editor.</summary>
public sealed class EditorCapture : TargetCapture
{
    public override StorageSlotType SlotType => StorageSlotType.None;
    public override SelectionInfo SelectionForRequest => SelectionInfoFactory.Editor();
    public override bool RequestPartySize => Original.SIZE_PARTY != Original.SIZE_STORED;
    public override string Describe() => AppIdioma.T("el editor", "the editor");

    public override Stability Check(IPokekeepHost host)
    {
        if (!ReferenceEquals(host.Env, Env))
            return Stability.SaveChanged;
        var current = Env.Editor.PreparePKM(false);
        return HashOf(current, RequestPartySize) == OriginalSha256 ? Stability.Stable : Stability.EditorChanged;
    }
}

/// <summary>Casilla fijada al abrir el menú contextual (o al soltar una entrada de la Librería).</summary>
public class SlotCapture : TargetCapture
{
    public required ISlotInfo Slot { get; init; }
    public override StorageSlotType SlotType => Slot.Type;

    public override SelectionInfo SelectionForRequest => Slot is SlotInfoBox b ? SelectionInfoFactory.Box(b.Box, b.Slot) : new SelectionInfo { Kind = "Box", Slot = Slot.Slot };

    public override string Describe() => Slot is SlotInfoBox b ? Textos.CajaCasilla(b.Box, b.Slot) : $"{Slot.Type} {Slot.Slot + 1}";

    public override Stability Check(IPokekeepHost host)
    {
        if (!ReferenceEquals(host.Env, Env))
            return Stability.SaveChanged;
        var current = Slot.Read(Sav);
        return HashOf(current, EntityBytes.UsesPartySize(current, SlotType)) == OriginalSha256 ? Stability.Stable : Stability.SlotChanged;
    }
}

/// <summary>«Añadir a la caja»: la casilla elegida debe estar y seguir VACÍA (nunca se sobrescribe); lo que se manda al motor es la plantilla de la especie.</summary>
public sealed class LibrarySlotCapture : SlotCapture
{
    public required PKM Template { get; init; }
    public required LibraryRow Row { get; init; }
    public string EntryKey => Row.EntryKey;
    public override PKM RequestEntity => Template;
    public override bool RequestPartySize => false;

    public override Stability Check(IPokekeepHost host)
    {
        if (!ReferenceEquals(host.Env, Env))
            return Stability.SaveChanged;
        return Slot.Read(Sav).Species == 0 ? Stability.Stable : Stability.SlotOccupied;
    }
}
