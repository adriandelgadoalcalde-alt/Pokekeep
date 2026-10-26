// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Engine.Changes;
using AutoLegalidad.Engine.Context;
using AutoLegalidad.Engine.Infra;
using PKHeX.Core;

namespace AutoLegalidad.Engine.Repair;

/// <summary>
/// Fase B del plan: reparacion MINIMA. Solo corrige lo que el analisis oficial marca como invalido y que se puede arreglar sin cambiar el origen
/// (EV, movimientos, recordables, Ball, nivel, forma, sexo, variocolor bloqueado), usando las utilidades del Core. No toca PID/IV correlacionados:
/// eso es regeneracion (fase C). Trabaja sobre una copia; si el resultado no es valido el motor vuelve a la entidad original.
/// </summary>
public static class MinimalRepair
{
    public const int MaxPasses = 3;

    /// <param name="keepForm">Hito 3b: las rutas de transferencia piden una forma concreta; si el Core la da por invalida, la reparacion NO la cambia a la base (que el validador diga por que).</param>
    public static PKM Run(PKM original, Destination dest, ChangeLog log, bool keepForm = false)
    {
        var pk = original.Clone();
        for (var pass = 0; pass < MaxPasses; pass++)
        {
            LegalityAnalysis la;
            lock (EngineGlobalState.Gate)
            {
                dest.ApplyParseSettings();
                la = new LegalityAnalysis(pk, dest.Personal, dest.SlotType);
            }
            if (la.Valid) break;
            var invalid = la.Results.Where(r => !r.Valid).Select(r => r.Identifier).ToHashSet();
            if (!ApplyFixes(pk, la, invalid, dest, log, keepForm)) break;
        }
        return pk;
    }

    private static bool ApplyFixes(PKM pk, LegalityAnalysis la, HashSet<CheckIdentifier> invalid, Destination dest, ChangeLog log, bool keepForm)
    {
        var changed = false;

        // Un huevo se normaliza PRIMERO: si no, el analisis no lo reconoce como huevo y marca como ilegales cosas que no lo son (movimientos, nivel).
        if (pk.IsEgg && FixEgg(pk, la, dest, log))
        {
            log.OtherRepairs = true;
            pk.RefreshChecksum();
            return true;
        }

        // En Let's Go el defecto de EV lo marca el verificador de AV (CheckIdentifier.AVs), no el de EV.
        if (invalid.Contains(CheckIdentifier.EVs) || (pk is PB7 && invalid.Contains(CheckIdentifier.AVs)))
        {
            var before = Generation.PkmFieldsAccess.EVs(pk);
            foreach (var evs in EvNormalizer.Candidates(pk))
            {
                var trial = pk.Clone();
                EvNormalizer.Set(trial, evs);
                LegalityAnalysis t;
                lock (EngineGlobalState.Gate)
                {
                    dest.ApplyParseSettings();
                    t = new LegalityAnalysis(trial, dest.Personal, dest.SlotType);
                }
                if (t.Results.Any(r => !r.Valid && (r.Identifier == CheckIdentifier.EVs || (pk is PB7 && r.Identifier == CheckIdentifier.AVs && r.Result == LegalityCheckResultCode.EffortShouldBeZero)))) continue;
                EvNormalizer.Set(pk, evs);
                break;
            }
            if (!before.AsSpan().SequenceEqual(Generation.PkmFieldsAccess.EVs(pk)))
            {
                log.ReasonFor(ChangeLog.EvFields, ReasonCodes.EvNormalized, "EV normalizados a los límites del formato y de lo que PKHeX admite, conservando su reparto relativo.");
                changed = true;
            }
        }

        // Una fecha de encuentro con el lugar de encuentro a 0 (un huevo recien recibido de una tarjeta: el Core la rellena al crear el huevo, pero su propio verificador la rechaza):
        // Legality/Verifiers/Misc/MiscDateVerifier.cs:21-28 exige fecha vacia cuando MetLocation es el valor «ninguno». La fecha es un dato derivado del lugar, no un rasgo del usuario.
        if (pk.Format >= 4 && pk.MetLocation == (pk is PB8 ? Locations.Default8bNone : (ushort)0) && (pk.MetYear != 0 || pk.MetMonth != 0 || pk.MetDay != 0))
        {
            pk.MetDate = null;
            log.ReasonFor(["MetDate"], ReasonCodes.MetDateCleared, "Fecha de encuentro vaciada: el lugar de encuentro es «ninguno» (huevo aún sin eclosionar).");
            changed = true;
        }

        // Gen3: Granbull, Vibrava y Flygon tienen dos habilidades IGUALES y el Core exige que el bit de habilidad siga al bit 0 del PID (Legality/Verifiers/Ability/AbilityVerifier.cs:431-447),
        // pero G3PKM.RefreshAbility sale sin hacer nada para esas tres especies (PKM/Shared/G3PKM.cs:80-84): lo que sale de un generador del Core (p. ej. el regalo de Nueva York) queda
        // con el bit sin refrescar la mitad de las veces. El bit es un dato derivado del PID, no un rasgo del usuario: se restablece.
        if (pk is G3PKM g3 && g3.Species is (ushort)Species.Granbull or (ushort)Species.Vibrava or (ushort)Species.Flygon && g3.AbilityBit != ((pk.EncryptionConstant & 1) == 1))
        {
            g3.AbilityBit = (pk.EncryptionConstant & 1) == 1;
            log.ReasonFor(["Ability", "AbilityNumber"], ReasonCodes.AbilityBitFixed, "Bit de habilidad ajustado al bit 0 del PID (regla de Gen3 para esta especie).");
            changed = true;
        }

        // Leyendas Z-A (PA9): la habilidad se compara con la tabla personal de ZA de la especie y forma del ENCUENTRO en el indice de AbilityNumber, no con la de la especie actual
        // (Legality/Verifiers/Ability/AbilityVerifier.cs:503-562, VerifyBirthAbility: `expect = PersonalTable.ZA[enc.Species, enc.Form].GetAbilityAtIndex(index)`; sin rastreador de HOME no hay
        // realineacion). Un Pokemon de GO evolucionado (Eevee -> Sylveon) o cuyo encuentro el Core identifica en la casilla de su preevolucion (Spewpa -> Vivillon) lleva la habilidad de la
        // especie nueva y el Core la rechaza. La habilidad de nacimiento es la que da el propio Core: no se inventa nada, y el analisis oficial decide despues.
        if (pk is PA9 && invalid.Contains(CheckIdentifier.Ability) && la.Parsed && la.EncounterMatch is { } birth and not EncounterInvalid)
        {
            var index = pk.AbilityNumber >> 1;
            var expect = BirthAbility(birth, index);
            if (expect > 0 && pk.Ability != expect)
            {
                pk.Ability = expect;
                log.ReasonFor(["Ability"], ReasonCodes.AbilityBirthKept, "Habilidad de nacimiento conservada: en Leyendas Pokémon Z-A el juego exige la de la especie del encuentro, no la de la evolución.");
                changed = true;
            }
        }

        if (invalid.Contains(CheckIdentifier.CurrentMove) || la.Info.Moves.Any(m => !m.Valid))
            changed |= FixMoves(pk, la, dest, log);

        if (invalid.Contains(CheckIdentifier.RelearnMove) || la.Info.Relearn.Any(m => !m.Valid))
        {
            var before = pk.RelearnMoves.ToArray();
            pk.SetRelearnMoves(la);
            if (!before.AsSpan().SequenceEqual(pk.RelearnMoves.ToArray()))
            {
                log.Reason("Relearn", ReasonCodes.RelearnFixed, "Movimientos recordables ajustados a los que permite el encuentro.");
                changed = true;
            }
        }

        if (invalid.Contains(CheckIdentifier.Ball))
        {
            var before = pk.Ball;
            Span<Ball> legal = stackalloc Ball[32];
            var n = BallApplicator.GetLegalBalls(legal, pk, la);
            if (n > 0)
            {
                var pick = legal[..n].Contains(Ball.Poke) ? Ball.Poke : legal[0];
                pk.Ball = (byte)pick;
            }
            if (pk.Ball != before)
            {
                log.Reason("Ball", ReasonCodes.BallFixed, "Poké Ball cambiada por una legal para este encuentro.");
                changed = true;
            }
        }

        if (invalid.Contains(CheckIdentifier.Level) && pk.CurrentLevel < pk.MetLevel && !pk.IsEgg)
        {
            pk.CurrentLevel = pk.MetLevel;
            log.Reason("Level", ReasonCodes.LevelFixed, "Nivel ajustado al nivel de captura.");
            changed = true;
        }

        // Formas que dependen del objeto equipado (Genesect: Memoria, Silvally: ROM, Arceus: placa, Giratina/Dialga/Palkia de Gen9: objeto de la forma Origen): una plantilla de GO o de HOME con la forma
        // pero sin el objeto la da por invalida el Core (FormVerifier.cs:63-86, FormItemInvalid), y reponer la forma base descartaba la forma pedida. Se pone el objeto que da el Core
        // (FormItem.GetItem*, mismo criterio que TransferRoutes.ApplyForm) y se conserva si el analisis oficial deja de marcar la forma.
        if (invalid.Contains(CheckIdentifier.Form) && pk.Form != 0 && pk is not PA8 && pk.Species is (ushort)Species.Genesect or (ushort)Species.Silvally or (ushort)Species.Arceus or (ushort)Species.Giratina or (ushort)Species.Dialga or (ushort)Species.Palkia)
        {
            var trial = pk.Clone();
            Generation.TransferRoutes.ApplyForm(trial, trial.Form);
            if (trial.HeldItem != pk.HeldItem)
            {
                LegalityAnalysis x;
                lock (EngineGlobalState.Gate)
                {
                    dest.ApplyParseSettings();
                    x = new LegalityAnalysis(trial, dest.Personal, dest.SlotType);
                }
                if (!x.Results.Any(r => !r.Valid && r.Identifier == CheckIdentifier.Form))
                {
                    pk.HeldItem = trial.HeldItem;
                    log.ReasonFor(["HeldItem"], ReasonCodes.FormFixed, "Objeto de la forma puesto: la forma de esta especie la determina el objeto que lleva.");
                    changed = true;
                    invalid.Remove(CheckIdentifier.Form);
                }
            }
        }

        // Keldeo (Gen8+): la forma es Brío si y solo si conoce Espada sagrada (Legality/Verifiers/FormVerifier.cs:74-82, MoveKeldeoMismatch; «Generation 8 patched out the mismatch»).
        // La plantilla de GO con los movimientos iniciales del origen puede traer Espada sagrada con la forma normal (o al reves): se alinean los movimientos con la forma pedida.
        if (invalid.Contains(CheckIdentifier.Form) && pk.Species == (ushort)Species.Keldeo && pk.Format >= 8 && FixKeldeoMoves(pk, la, dest, log))
            changed = true;

        if (invalid.Contains(CheckIdentifier.Form) && pk.Form != 0 && !keepForm)
        {
            pk.Form = 0;
            if (pk is IFormArgument fa) fa.FormArgument = 0;
            log.Reason("Form", ReasonCodes.FormFixed, "Forma no almacenable en este formato: se usa la forma base.");
            changed = true;
        }

        if (invalid.Contains(CheckIdentifier.Gender) && pk.Format >= 5)
        {
            var g = pk.GetSaneGender();
            if (pk.Gender != g)
            {
                pk.Gender = g;
                log.Reason("Gender", ReasonCodes.GenderFixed, "Sexo ajustado al que admite la especie.");
                changed = true;
            }
        }

        if (invalid.Contains(CheckIdentifier.Shiny) && pk.IsShiny && pk.Format >= 6)
        {
            pk.SetIsShiny(false);
            log.Reason("IsShiny", ReasonCodes.ShinyRemoved, "Variocolor no permitido para este encuentro.");
            log.Reason("PID", ReasonCodes.ShinyRemoved, "PID recalculado al quitar el variocolor no permitido.");
            changed = true;
        }

        // uso10/release (decision del propietario, 20-oct-2026): los bytes de basura que deberian estar a cero detras del terminador del apodo, del entrenador original y del ultimo entrenador
        // (cadenas UTF-16: formato 8 o superior y Let's Go) se borran tambien en la legalizacion INDIVIDUAL, con el mismo criterio que el barrido masivo (LegalityCheck.CleanedOfTrash). El texto,
        // su terminador, el PID, los IV y el EO no cambian: solo lo que hay detras del final de cada texto. Sin esto, un Pokemon ilegal SOLO por basura agotaba el motor buscando un origen.
        if (changed) log.OtherRepairs = true;
        if ((pk.Format >= 8 || pk is PB7) && la.Parsed)
        {
            foreach (var r in la.Results.Where(r => !r.Valid && r.Result == LegalityCheckResultCode.TrashBytesShouldBeEmpty))
            {
                var cleared = r.Identifier switch
                {
                    CheckIdentifier.Trainer => ClearAfterTerminator(pk.OriginalTrainerTrash),
                    CheckIdentifier.Nickname => ClearAfterTerminator(pk.NicknameTrash),
                    CheckIdentifier.Handler => ClearAfterTerminator(pk.HandlingTrainerTrash),
                    _ => false,
                };
                if (cleared) { changed = true; log.TrashCleared = true; } // no es un campo comparable en «Ver cambios»: el texto y el resto de datos no cambian
            }
        }

        if (changed)
        {
            if (pk.IsEgg) pk.HealPP(); else pk.Heal(); // un huevo solo recupera los PP por defecto
            pk.RefreshChecksum();
        }
        return changed;
    }

    /// <summary>Pone a cero lo que queda detras del terminador de una cadena UTF-16 (el texto y su terminador no cambian). Devuelve si cambio algun byte; sin terminador no toca nada.</summary>
    private static bool ClearAfterTerminator(Span<byte> text)
    {
        for (var i = 0; i + 1 < text.Length; i += 2)
        {
            if (text[i] != 0 || text[i + 1] != 0) continue;
            var tail = text[(i + 2)..];
            if (!tail.ContainsAnyExcept((byte)0)) return false;
            tail.Clear();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Alinea los movimientos de un Keldeo con su forma sin cambiar la forma: con la forma normal se quita Espada sagrada (se rellena con lo que el encuentro sugiere); con la forma Brio se
    /// pone Espada sagrada solo si el analisis oficial la admite en esa casilla. Devuelve false si no hay variante que el Core acepte (entonces la forma se queda como esta).
    /// </summary>
    private static bool FixKeldeoMoves(PKM pk, LegalityAnalysis la, Destination dest, ChangeLog log)
    {
        var sword = (ushort)Move.SecretSword;
        var current = new ushort[4];
        pk.GetMoves(current);
        if ((pk.Form == 1) == current.Contains(sword)) return false;
        Span<ushort> suggestedSpan = stackalloc ushort[4];
        la.GetSuggestedCurrentMoves(suggestedSpan, MoveSourceType.Encounter);
        var suggested = suggestedSpan.ToArray();

        var options = new List<ushort[]>();
        if (pk.Form == 0)
        {
            var keep = current.Where(m => m != 0 && m != sword).Distinct().ToList();
            foreach (var m in suggested)
                if (keep.Count < 4 && m != 0 && m != sword && !keep.Contains(m)) keep.Add(m);
            if (keep.Count > 0) options.Add([.. keep, .. new ushort[4 - keep.Count]]);
        }
        else
        {
            for (var slot = 3; slot >= 0; slot--)
            {
                var o = (ushort[])current.Clone();
                o[slot] = sword;
                options.Add(o);
            }
        }
        foreach (var option in options)
        {
            var t = pk.Clone();
            t.SetMoves(option);
            t.HealPP();
            LegalityAnalysis x;
            lock (EngineGlobalState.Gate)
            {
                dest.ApplyParseSettings();
                x = new LegalityAnalysis(t, dest.Personal, dest.SlotType);
            }
            if (x.Results.Any(r => !r.Valid && r.Result == LegalityCheckResultCode.MoveKeldeoMismatch) || x.Info.Moves.Any(m => !m.Valid)) continue;
            pk.SetMoves(option);
            pk.HealPP();
            log.ReasonFor(["Move1", "Move2", "Move3", "Move4"], ReasonCodes.MoveReplaced, pk.Form == 0
                ? "Espada sagrada quitada: el Keldeo de forma normal no puede conocerla (la forma Brío se activa al conocerla)."
                : "Espada sagrada añadida: la forma Brío de Keldeo exige conocerla.");
            return true;
        }
        return false;
    }

    /// <summary>Habilidad que la tabla personal de ZA da a la especie y forma del encuentro en el indice pedido (0, 1 u oculta = 2); 0 si no existe.</summary>
    internal static int BirthAbility(IEncounterTemplate enc, int index)
    {
        try
        {
            var pi = PersonalTable.ZA.GetFormEntry(enc.Species, enc.Form);
            return (uint)index < pi.AbilityCount ? pi.GetAbilityAtIndex(index) : 0;
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException) { return 0; }
    }

    /// <summary>
    /// Sustituye los movimientos ilegales. Se prueban dos criterios y se queda el que deja MENOS apartados invalidos en el analisis oficial: (A) conservar los
    /// movimientos que el analisis acepta y rellenar con los sugeridos para el encuentro; (B) usar solo los sugeridos para el encuentro (en un huevo, el analisis
    /// pierde el encuentro por un solo movimiento ilegal y marca como invalidos tambien los buenos).
    /// </summary>
    private static bool FixMoves(PKM pk, LegalityAnalysis la, Destination dest, ChangeLog log)
    {
        var current = new ushort[4];
        pk.GetMoves(current);
        Span<ushort> suggestedSpan = stackalloc ushort[4];
        la.GetSuggestedCurrentMoves(suggestedSpan, MoveSourceType.Encounter);
        var suggested = suggestedSpan.ToArray();

        var keep = new List<ushort>(4);
        for (var i = 0; i < 4; i++)
            if (current[i] != 0 && la.Info.Moves[i].Valid && !keep.Contains(current[i])) keep.Add(current[i]);
        foreach (var m in suggested)
            if (keep.Count < 4 && m != 0 && !keep.Contains(m)) keep.Add(m);
        var a = new ushort[4];
        for (var i = 0; i < keep.Count; i++) a[i] = keep[i];

        var b = new ushort[4];
        var nb = 0;
        foreach (var m in suggested)
            if (m != 0 && Array.IndexOf(b, m) < 0 && nb < 4) b[nb++] = m;

        // (C) Huevos (o Pokemon criados cuyo encuentro el analisis perdio por un movimiento): el conjunto de HERENCIA del encuentro (EggMoveSets).
        IEnumerable<ushort[]> options = [a, b];
        if (pk.IsEgg || la.EncounterMatch is IEncounterEgg || la.EncounterMatch is EncounterInvalid)
            options = options.Concat(EggMoveSets.Candidates(pk, current, dest));

        ushort[]? best = null;
        var bestInvalid = int.MaxValue;
        foreach (var option in options)
        {
            if (option.AsSpan().SequenceEqual(current)) continue;
            var t = pk.Clone();
            t.SetMoves(option);
            t.HealPP();
            LegalityAnalysis x;
            lock (EngineGlobalState.Gate)
            {
                dest.ApplyParseSettings();
                x = new LegalityAnalysis(t, dest.Personal, dest.SlotType);
            }
            var invalid = x.Results.Count(r => !r.Valid) + x.Info.Moves.Count(m => !m.Valid);
            if (invalid < bestInvalid) { bestInvalid = invalid; best = option; }
        }
        if (best is null) return false;
        pk.SetMoves(best);
        pk.HealPP();
        log.ReasonFor(["Move1", "Move2", "Move3", "Move4"], ReasonCodes.MoveReplaced, "Movimientos ilegales sustituidos por movimientos que el Pokémon puede aprender.");
        return true;
    }

    /// <summary>
    /// Un huevo debe seguir siendo un huevo (nunca se eclosiona para «arreglarlo»): EV a 0, nivel propio de un huevo (5 hasta Gen3, 1 despues) y nombre de huevo,
    /// probando las dos variantes de apodo que usan los formatos. Se aplica la variante que deja MENOS apartados invalidos en el analisis oficial.
    /// </summary>
    private static bool FixEgg(PKM pk, LegalityAnalysis la, Destination dest, ChangeLog log)
    {
        var current = la.Results.Count(r => !r.Valid) + la.Info.Moves.Count(m => !m.Valid) + la.Info.Relearn.Count(m => !m.Valid);
        var eggLevel = pk.Format <= 3 ? (byte)5 : (byte)1;
        PKM? best = null;
        var bestCount = current;
        foreach (var nick in new[] { false, true })
        {
            var t = pk.Clone();
            t.EV_HP = t.EV_ATK = t.EV_DEF = t.EV_SPA = t.EV_SPD = t.EV_SPE = 0;
            t.CurrentLevel = eggLevel;
            t.IsNicknamed = nick;
            t.Nickname = SpeciesName.GetEggName(t is GBPKM gb ? gb.GuessedLanguage() : t.Language, t.Generation);
            LegalityAnalysis a;
            lock (EngineGlobalState.Gate)
            {
                dest.ApplyParseSettings();
                a = new LegalityAnalysis(t, dest.Personal, dest.SlotType);
            }
            var count = a.Results.Count(r => !r.Valid) + a.Info.Moves.Count(m => !m.Valid) + a.Info.Relearn.Count(m => !m.Valid);
            if (count >= bestCount) continue;
            best = t;
            bestCount = count;
            if (count == 0) break;
        }
        if (best is null) return false;
        pk.EV_HP = best.EV_HP; pk.EV_ATK = best.EV_ATK; pk.EV_DEF = best.EV_DEF; pk.EV_SPA = best.EV_SPA; pk.EV_SPD = best.EV_SPD; pk.EV_SPE = best.EV_SPE;
        pk.CurrentLevel = best.CurrentLevel;
        pk.IsNicknamed = best.IsNicknamed;
        pk.Nickname = best.Nickname;
        log.ReasonFor([.. ChangeLog.EvFields, "Level", "Nickname"], ReasonCodes.EggNormalized, "Huevo normalizado: sin EV, nivel y nombre propios de un huevo (sigue siendo un huevo).");
        return true;
    }
}
