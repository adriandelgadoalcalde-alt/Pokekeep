// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Registro;
using AutoLegalidad.Ui.Operation;
using PKHeX.Core;

namespace AutoLegalidad.Ui.Bulk;

/// <summary>Una escritura prevista de una operación masiva: qué Pokémon va a qué casilla y qué debe cumplir esa casilla en el momento de aplicar (regla 7, casilla a casilla).</summary>
public sealed class BulkWrite
{
    /// <summary>Identificador del elemento del plan (para casar los resultados con el informe).</summary>
    public required int Id { get; init; }
    public required ISlotInfo Slot { get; init; }
    /// <summary>Resultado ya validado por el motor (nunca un Pokémon sin validar). uso10 (decisión del propietario): o el mismo Pokémon con solo los bytes de basura borrados, validado por el análisis oficial del Core (<c>LegalityCheck.CleanedOfTrash</c>).</summary>
    public required PKM Candidate { get; init; }
    /// <summary>R-009: el candidato carece de la matrícula de HOME (<c>SuccessNeedsHome</c>): la segunda opinión tolera solo esa falta.</summary>
    public bool NeedsHome { get; init; }
    /// <summary>Edición masiva: la casilla debe seguir conteniendo exactamente estos bytes (huella SHA-256). <c>null</c> = «Completar el juego»: la casilla debe seguir VACÍA.</summary>
    public string? ExpectedOriginalSha256 { get; init; }
    /// <summary>La segunda opinión del Core ya se hizo (en otro hilo, antes de aplicar): no se repite dentro de la transacción.</summary>
    public bool PreValidated { get; init; }
}

public enum BulkWriteStatus
{
    /// <summary>Escrito, releído y revalidado.</summary>
    Written,
    /// <summary>«Completar»: la casilla se ocupó mientras se generaba y no quedó ninguna libre para recolocarlo.</summary>
    SlotTaken,
    /// <summary>Edición masiva: el usuario cambió la casilla mientras se procesaba (los bytes ya no son los planificados): no se aplica.</summary>
    SlotChanged,
    /// <summary>La casilla está bloqueada o ya no admite escritura.</summary>
    SlotLocked,
    /// <summary>La segunda opinión del Core o la relectura lo dieron por inválido: la casilla quedó como estaba.</summary>
    FailedFinalCheck,
    /// <summary>El guardado rechazó la escritura.</summary>
    WriteRejected,
    /// <summary>Se abrió o cerró otra partida: no se escribió nada.</summary>
    TargetChanged,
}

public sealed record BulkWriteResult(BulkWrite Write, BulkWriteStatus Status, ISlotInfo? FinalSlot, string? Detail, bool Relocated = false)
{
    public bool Written => Status == BulkWriteStatus.Written;
}

public sealed class BulkApplyResult(IReadOnlyList<BulkWriteResult> results, BulkUndoToken? undo)
{
    public IReadOnlyList<BulkWriteResult> Results { get; } = results;
    /// <summary>Cómo deshacer todo el lote de una vez (null si no se escribió nada).</summary>
    public BulkUndoToken? Undo { get; } = undo;
    public int WrittenCount => Results.Count(r => r.Written);
}

/// <summary>
/// Transacción de una operación masiva (ADR 0025, punto 6): TODAS las escrituras en UNA entrada del historial de PKHeX mediante <c>SlotEditor.Batch</c>
/// (<c>CompositeSlotReversion</c>), con regla 7 casilla a casilla, segunda opinión y relectura revalidada por cada casilla. Nunca sobrescribe lo que no esté
/// exactamente como se planificó y nunca deja un Pokémon que no supere la comprobación final.
/// </summary>
public static class BulkTransaction
{

    /// <summary>Segunda opinión del Core sobre los candidatos (puede ejecutarse en otro hilo, antes de <see cref="Apply"/>, para no bloquear la interfaz). Devuelve las escrituras con <c>PreValidated</c> o sin ellas si fallan.</summary>
    public static (IReadOnlyList<BulkWrite> Valid, IReadOnlyList<BulkWrite> Rejected) Validate(SaveFile sav, IEnumerable<BulkWrite> writes, CancellationToken cancellation = default)
    {
        var valid = new List<BulkWrite>();
        var rejected = new List<BulkWrite>();
        foreach (var w in writes)
        {
            cancellation.ThrowIfCancellationRequested();
            var resolved = w.Candidate.Species == 0 ? null : LegalityCheck.ResolveForSave(w.Candidate, sav, w.Slot.Type, w.NeedsHome);
            var ok = resolved is not null;
            (ok ? valid : rejected).Add(ok ? Mark(w, resolved!) : w);
        }
        return (valid, rejected);
    }

    private static BulkWrite Mark(BulkWrite w, PKM candidate) => new()
    {
        Id = w.Id, Slot = w.Slot, Candidate = candidate, NeedsHome = w.NeedsHome, ExpectedOriginalSha256 = w.ExpectedOriginalSha256, PreValidated = true,
    };

    /// <summary>
    /// Aplica el lote. <paramref name="capturedEnv"/> es el entorno de edición que había al empezar: si ya no es el actual no se escribe nada (regla 7).
    /// <paramref name="spareSlots"/>: casillas libres (en orden de recorrido) para recolocar un Pokémon cuya casilla se ocupó mientras se generaba («Completar el juego»).
    /// </summary>
    public static BulkApplyResult Apply(IPokekeepHost host, IEditEnvironment capturedEnv, IReadOnlyList<BulkWrite> writes, IEnumerable<ISlotInfo>? spareSlots = null, bool registerDex = false)
    {
        var results = new BulkWriteResult?[writes.Count];
        if (!ReferenceEquals(host.Env, capturedEnv))
            return new BulkApplyResult([.. writes.Select(w => new BulkWriteResult(w, BulkWriteStatus.TargetChanged, null, Textos.PartidaCambio))], null);

        var sav = capturedEnv.SAV;
        var spare = new Queue<ISlotInfo>(spareSlots ?? []);
        var claimed = new HashSet<(StorageSlotType, int, int)>();
        var plan = new List<(int Index, BulkWrite Write, ISlotInfo Slot, bool Relocated)>();

        // ---- 1) Regla 7 casilla a casilla: ¿sigue la casilla como se planificó? ----
        for (var i = 0; i < writes.Count; i++)
        {
            var w = writes[i];
            var slot = w.Slot;
            var relocated = false;
            if (w.ExpectedOriginalSha256 is null)
            {
                // «Completar el juego»: debe seguir libre; si no, se recoloca en la siguiente libre del recorrido.
                if (!IsUsableFree(sav, slot, claimed))
                {
                    ISlotInfo? alt = null;
                    while (spare.Count > 0)
                    {
                        var c = spare.Dequeue();
                        if (IsUsableFree(sav, c, claimed)) { alt = c; break; }
                    }
                    if (alt is null) { results[i] = new BulkWriteResult(w, BulkWriteStatus.SlotTaken, null, Textos.CasillaOcupadaMientras); continue; }
                    slot = alt;
                    relocated = true;
                }
            }
            else
            {
                if (!slot.CanWriteTo(sav)) { results[i] = new BulkWriteResult(w, BulkWriteStatus.SlotLocked, null, Textos.CasillaBloqueada); continue; }
                var current = slot.Read(sav);
                if (EntityBytes.Sha256(EntityBytes.Export(current, EntityBytes.UsesPartySize(current, slot.Type))) != w.ExpectedOriginalSha256)
                { results[i] = new BulkWriteResult(w, BulkWriteStatus.SlotChanged, null, Textos.CasillaCambioMientras); continue; }
            }
            claimed.Add(Key(slot));
            plan.Add((i, w, slot, relocated));
        }

        // ---- 2) Segunda opinión del Core (si no se hizo ya fuera de la transacción). ----
        var toWrite = new List<(int Index, BulkWrite Write, ISlotInfo Slot, bool Relocated)>();
        foreach (var p0 in plan)
        {
            var p = p0;
            if (!p.Write.PreValidated)
            {
                var resolved = LegalityCheck.ResolveForSave(p.Write.Candidate, sav, p.Slot.Type, p.Write.NeedsHome);
                if (resolved is null)
                {
                    results[p.Index] = new BulkWriteResult(p.Write, BulkWriteStatus.FailedFinalCheck, null, Textos.ValidacionFallida);
                    continue;
                }
                p = (p.Index, Mark(p.Write, resolved), p.Slot, p.Relocated);
            }
            toWrite.Add(p);
        }
        if (toWrite.Count == 0)
            return Finish(writes, results, null);

        // ---- 3) UNA entrada de historial: SlotEditor.Batch. Dentro: escribir, releer, revalidar; si la relectura no valida, esa casilla vuelve a como estaba. ----
        var originals = toWrite.ToDictionary(p => p.Index, p => p.Slot.Read(sav).Clone());
        var written = new Dictionary<int, PKM>();
        var failures = new Dictionary<int, (BulkWriteStatus, string)>();
        try
        {
            var ok = capturedEnv.Slots.Batch(toWrite.Select(p => p.Slot), _ =>
            {
                foreach (var (index, write, slot, _) in toWrite)
                {
                    if (!slot.WriteTo(sav, write.Candidate.Clone(), EntityImportSettings.None))
                    {
                        failures[index] = (BulkWriteStatus.WriteRejected, AppIdioma.T("El guardado rechazó la escritura.", "The save rejected the write."));
                        slot.WriteTo(sav, originals[index].Clone(), EntityImportSettings.None);
                        continue;
                    }
                    var reread = slot.Read(sav);
                    if (reread.Species != write.Candidate.Species || !LegalityCheck.IsValid(reread, sav, slot.Type, write.NeedsHome))
                    {
                        failures[index] = (BulkWriteStatus.FailedFinalCheck, Textos.ValidacionFallida);
                        // 0.6.6 (B4): si la casilla no vuelve a como estaba, NO se sigue con el candidato rechazado dentro: se lanza y SlotEditor.Batch deshace el lote entero.
                        if (!slot.WriteTo(sav, originals[index].Clone(), EntityImportSettings.None))
                            throw new InvalidOperationException(AppIdioma.T("No se pudo restaurar una casilla tras la comprobación final: se deshace el lote entero.", "A slot couldn’t be restored after the final check: the whole batch is undone."));
                        continue;
                    }
                    // Hito 5b: la Pokédex solo después de la comprobación final (y solo «Completar el juego»; la edición masiva no registra nada).
                    if (registerDex) PokedexRegistration.Register(slot, sav, reread);
                    written[index] = reread;
                }
            });
            if (!ok)
                foreach (var p in toWrite)
                    results[p.Index] = new BulkWriteResult(p.Write, BulkWriteStatus.SlotLocked, null, Textos.CasillaBloqueada);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            RegistroFallos.Error(AppIdioma.T("La escritura en bloque en el guardado falló; el lote se deshizo.", "The bulk write to the save failed; the batch was undone."), ex, "BulkTransaction");
            // Batch ya deshizo todo (rollback del historial): ninguna casilla cambió.
            foreach (var p in toWrite)
                results[p.Index] = new BulkWriteResult(p.Write, BulkWriteStatus.WriteRejected, null, $"{Textos.NadaModificado} ({ex.GetType().Name}: {ex.Message})");
            return Finish(writes, results, null);
        }
        host.NotifyUndoRedoChanged();

        var undoEntries = new List<BulkUndoToken.Entry>();
        foreach (var p in toWrite)
        {
            if (results[p.Index] is not null) continue;
            if (failures.TryGetValue(p.Index, out var f))
            {
                results[p.Index] = new BulkWriteResult(p.Write, f.Item1, null, f.Item2);
                continue;
            }
            var reread = written[p.Index];
            results[p.Index] = new BulkWriteResult(p.Write, BulkWriteStatus.Written, p.Slot, null, p.Relocated);
            undoEntries.Add(new BulkUndoToken.Entry(p.Slot, originals[p.Index], EntityBytes.Sha256(EntityBytes.Export(reread, EntityBytes.UsesPartySize(reread, p.Slot.Type)))));
        }
        var token = undoEntries.Count == 0 ? null : new BulkUndoToken(host, capturedEnv, undoEntries);
        return Finish(writes, results, token);
    }

    private static BulkApplyResult Finish(IReadOnlyList<BulkWrite> writes, BulkWriteResult?[] results, BulkUndoToken? token)
        => new([.. results.Select((r, i) => r ?? new BulkWriteResult(writes[i], BulkWriteStatus.WriteRejected, null, Textos.NadaModificado))], token);

    private static (StorageSlotType, int, int) Key(ISlotInfo s) => (s.Type, s is SlotInfoBox b ? b.Box : -1, s.Slot);

    private static bool IsUsableFree(SaveFile sav, ISlotInfo slot, HashSet<(StorageSlotType, int, int)> claimed)
        => slot.CanWriteTo(sav) && !claimed.Contains(Key(slot)) && slot.IsEmpty(sav);
}

/// <summary>
/// Cómo deshacer un lote entero (R-006/R-007): solo actúa si TODAS las casillas siguen exactamente como las dejó Pokekeep (si el usuario tocó alguna, se niega y lo dice).
/// Si actúa, escribe los originales con otro <c>SlotEditor.Batch</c> (una entrada más del historial, igual que el <see cref="UndoToken"/> de una casilla del Hito 4) y limpia el resaltado.
/// El Deshacer propio de PKHeX (Edición &gt; Deshacer) también revierte el lote entero de una vez: es UNA entrada.
/// </summary>
public sealed class BulkUndoToken
{
    public sealed record Entry(ISlotInfo Slot, PKM Original, string WrittenSha256);

    private readonly IPokekeepHost _host;
    private readonly IEditEnvironment _env;
    private readonly IReadOnlyList<Entry> _entries;

    public BulkUndoToken(IPokekeepHost host, IEditEnvironment env, IReadOnlyList<Entry> entries)
    {
        _host = host;
        _env = env;
        _entries = entries;
    }

    public int SlotCount => _entries.Count;

    /// <summary>Número de casillas que ya no están como las dejó Pokekeep (se toca el guardado actual; si es otro guardado, todas).</summary>
    public int ChangedSince()
    {
        if (!ReferenceEquals(_host.Env, _env)) return _entries.Count;
        var sav = _env.SAV;
        var n = 0;
        foreach (var e in _entries)
        {
            if (!e.Slot.CanWriteTo(sav)) { n++; continue; }
            var cur = e.Slot.Read(sav);
            if (EntityBytes.Sha256(EntityBytes.Export(cur, EntityBytes.UsesPartySize(cur, e.Slot.Type))) != e.WrittenSha256) n++;
        }
        return n;
    }

    public bool CanUndo() => ChangedSince() == 0;

    /// <summary>Texto de por qué no se puede deshacer (null si se puede).</summary>
    public string? RefusalReason()
    {
        var n = ChangedSince();
        if (n == 0) return null;
        return ReferenceEquals(_host.Env, _env)
            ? Textos.DeshacerLoteNegado(n)
            : Textos.PartidaCambio;
    }

    /// <summary>Deshace todo el lote. Devuelve false (sin tocar nada) si alguna casilla cambió después.</summary>
    public bool Undo()
    {
        if (!CanUndo()) return false;
        var sav = _env.SAV;
        var ok = _env.Slots.Batch(_entries.Select(e => e.Slot), _ =>
        {
            foreach (var e in _entries)
                e.Slot.WriteTo(sav, e.Original.Clone(), EntityImportSettings.None);
        });
        _host.NotifyUndoRedoChanged();
        _host.ClearSlotHighlights();
        return ok;
    }

    /// <summary>Adaptador al <see cref="UndoToken"/> que ya usa la barra de operación del Hito 4.</summary>
    public UndoToken AsUndoToken() => new(CanUndo, Undo);
}
