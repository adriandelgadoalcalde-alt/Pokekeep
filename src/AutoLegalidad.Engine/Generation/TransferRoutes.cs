// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Engine.Context;
using AutoLegalidad.Engine.Infra;
using PKHeX.Core;

namespace AutoLegalidad.Engine.Generation;

/// <summary>
/// Hito 3b: rutas de TRANSFERENCIA con la forma pedida. Una forma que no tiene encuentro nativo en el juego de destino solo se consigue transfiriendo desde otro juego
/// (Pal Park Gen3-&gt;4, Poke Transfer Gen4-&gt;5, Banco/Gen6-&gt;7, Time Capsule, regalos de evento...). El Core ya enumera esas rutas: <c>EncounterMovesetGenerator.GenerateEncounters</c>
/// recorre TODAS las generaciones de origen alcanzables para la especie y la forma en el destino. Aqui se recorren una a una, se crea el Pokemon con la plantilla del propio
/// Core, se convierte al formato del destino (<c>EntityConverter</c>: es la transferencia) y, si la forma de la plantilla no es la pedida pero el Core admite que cambie
/// (<see cref="FormInfo.IsFormChangeable"/>: Deoxys, Arceus, Rotom, Giratina...), se aplica el cambio de forma que el propio juego permite (con su objeto cuando lo exige).
/// Nada de esto decide el exito: todo candidato pasa despues por el validador oficial independiente del motor (regla 2) y por la barrera del catalogo (regla 8).
/// Fuentes del Core fijado: Legality/Encounters/Generator/Moveset/EncounterMovesetGenerator.cs:370-389 (IsSane), Legality/Tables/FormInfo.cs (IsFormChangeable),
/// Legality/Tables/FormItem.cs (objetos de forma), Legality/Verifiers/FormVerifier.cs.
/// </summary>
public static class TransferRoutes
{
    /// <summary>Muestras por encuentro de forma aleatoria (Unown de Gen2/3: la forma sale del PID/DV): 1/28 por intento, 400 deja el fallo en e^-14.</summary>
    public const int RandomFormAttempts = 400;

    /// <summary>Casillas por clave de catalogo que se prueban al GENERAR (la evaluacion de imposibilidad recorre todas).</summary>
    public const int MaxSlotsPerKey = 3;

    /// <summary>
    /// La ruta se usa cuando el analisis oficial no identifico un encuentro de origen que conservar y (a) la forma pedida no es la base, o (b) es el ULTIMO escalon de la relajacion
    /// (red de seguridad: los rasgos ya estan al minimo y los generadores por muestreo no hallaron nada, p. ej. un evento de Gen4 como Manaphy). Es una ESTRATEGIA, no un atajo del motor:
    /// sus candidatos pasan por el mismo validador oficial y la misma barrera del catalogo que los de ALM.
    /// </summary>
    public static bool ShouldUse(GenerationContext ctx)
        => ctx.OriginalEncounter is null or EncounterInvalid && (ctx.Original.Form != 0 || ctx.Step.Code == "Defaults");

    /// <summary>Encuentros que el Core enumera para la especie y la forma en el destino (todas las generaciones de origen). Se materializa dentro del cerrojo del Core.</summary>
    public static List<IEncounterable> Enumerate(PKM wish, Destination dest)
    {
        var blank = dest.Sav.BlankPKM;
        blank.Species = wish.Species;
        blank.Form = wish.Form;
        blank.Gender = blank.GetSaneGender();
        List<IEncounterable> list;
        lock (EngineGlobalState.Gate)
        {
            dest.ApplyParseSettings();
            list = [.. CoreVersions.Encounters(blank, dest.Trainer, ReadOnlyMemory<ushort>.Empty)];
        }
        // Primero los que ya traen la forma pedida (sin cambio de forma); despues los demas, de la generacion mas reciente a la mas antigua.
        return [.. list.OrderBy(e => e.Form == wish.Form ? 0 : 1).ThenBy(e => e.Species == wish.Species ? 0 : 1).ThenByDescending(e => e.Generation)];
    }

    public static IEnumerable<PKM> Generate(GenerationContext ctx)
    {
        var wish = ctx.Original;
        var perKey = new Dictionary<string, int>();
        foreach (var enc in Enumerate(wish, ctx.Dest))
        {
            if (ctx.Expired) { ctx.Report.TimedOut = DateTime.UtcNow >= ctx.DeadlineUtc; yield break; }
            if (enc is not IEncounterConvertible conv) continue;
            // Una ruta que exige el rastreador de HOME nunca produce un candidato aceptable (regla 6): no se gasta tiempo en construirla.
            if (HomeTrackerRule.IsRequired(enc, ctx.Dest.Context) && ctx.Dest.TrackerSeverity != Severity.Fishy) continue; // R-009: con el ajuste explicito (Fishy) tambien se prueban las rutas por HOME (rastreador 0)
            // Cientos de casillas de una misma zona comparten clave de catalogo (tipo, version, especie y forma) y dan el mismo resultado: se prueban pocas de cada una.
            var key = AutoLegalidad.Engine.Catalog.EncounterKeys.For(enc) ?? enc.GetType().Name;
            if (perKey.GetValueOrDefault(key) >= MaxSlotsPerKey) continue;
            perKey[key] = perKey.GetValueOrDefault(key) + 1;
            foreach (var pk in Build(conv, wish, ctx.Dest, ctx.Traits, () => ctx.Expired))
            {
                ctx.Report.Produced++;
                yield return pk;
            }
        }
    }

    /// <summary>
    /// Crea los candidatos de UN encuentro con la forma pedida: la plantilla del Core, convertida al formato del destino, con la forma pedida aplicada si el Core la admite,
    /// el entrenador actual fijado como lo hace el juego al recibirlo, los EV del usuario y la reparacion minima.
    /// </summary>
    public static IEnumerable<PKM> Build(IEncounterConvertible conv, PKM wish, Destination dest, Traits traits, Func<bool> expired, Action<string>? whyNot = null)
    {
        var enc = (IEncounterTemplate)conv;
        // Sin crear nada: si la plantilla no trae la forma, el Core no permite cambiarla y no es una forma de Mega/Primigenia de Z-A en el equipo, esta ruta no puede dar la forma pedida
        // (FormInfo.IsFormChangeable; el comprobador de encuentros del Core exige Form == evo.Form salvo esos casos).
        if (enc.Form != wish.Form && enc.Form < 30 && enc is not IEncounterFormRandom { IsRandomUnspecificForm: true } && !IsBattleFormInParty(wish, dest)
            && !FormInfo.IsFormChangeable(wish.Species, enc.Form, wish.Form, enc.Context, dest.Context))
        {
            whyNot?.Invoke(FormNotChangeable);
            yield break;
        }
        var criteria = Criteria(traits, wish);
        // Solo se muestrea cuando la forma de la PLANTILLA es aleatoria (30/31: dinamica o al azar). Un Unown de Gen3/4 con forma concreta ya trae su letra (SetRandomUnown busca el PID de enc.Form).
        var random = enc.Form >= 30 || enc is IEncounterFormRandom { IsRandomUnspecificForm: true };
        var attempts = random ? RandomFormAttempts : 1;
        for (var i = 0; i < attempts; i++)
        {
            if (expired()) yield break;
            PKM? raw;
            lock (EngineGlobalState.Gate)
            {
                dest.ApplyParseSettings();
                raw = Create(conv, dest, criteria);
            }
            if (raw is null) { if (!random) { whyNot?.Invoke(CreationFailed); yield break; } continue; }
            var pk = raw;
            // Una plantilla de una etapa anterior (huevo, Type: Null, Pichu...) se evoluciona hasta la especie pedida; la especie final NUNCA cambia (regla 14: la evolucion legitima esta admitida).
            var evolve = pk.Species != wish.Species;
            if (pk.Form != wish.Form || evolve)
            {
                if (random && !evolve && pk.Form != wish.Form) continue; // la forma de este encuentro es aleatoria: se vuelve a muestrear
                if (pk.Form != wish.Form && !IsBattleFormInParty(wish, dest) && !FormInfo.IsFormChangeable(wish.Species, enc.Form, wish.Form, enc.Context, dest.Context)) { whyNot?.Invoke(FormNotChangeable); yield break; }
            }
            foreach (var level in evolve ? EvolutionLevels(pk) : [pk.CurrentLevel])
            {
                var cand = pk.Clone();
                if (evolve || cand.Form != wish.Form)
                {
                    Evolve(cand, wish, level);
                    // Argumento de forma (dias de Furfrou, contadores de Hoopa...): el valor que sugiere el Core segun el historial evolutivo, como hace Auto Legality Mod.
                    if (cand is IFormArgument)
                        lock (EngineGlobalState.Gate)
                        {
                            dest.ApplyParseSettings();
                            var la = new LegalityAnalysis(cand, dest.Personal, dest.SlotType);
                            cand.SetSuggestedFormArgument(cand.Species, cand.Form, cand.Context, la.Info.EvoChainsAllGens, enc.Species);
                        }
                }
                // El juego fija el «entrenador actual» al recibir el Pokemon en la partida del jugador (API publica del Core; no toca el rastreador de HOME, regla 6).
                if (cand is IHandlerUpdate hu && !cand.IsEgg) hu.UpdateHandler(dest.Trainer);
                if (cand is PB8 { CurrentHandler: 1 } pb) PKH.UpdateHandler(pb, dest.Trainer);
                var (perStat, total) = Repair.EvNormalizer.Limits(cand);
                Repair.EvNormalizer.Set(cand, Repair.EvNormalizer.Normalize(traits.EVs, perStat, total));
                yield return Repair.MinimalRepair.Run(cand, dest, new Changes.ChangeLog(), keepForm: true);
            }
            yield break;
        }
    }

    public const string FormNotChangeable = "form-not-changeable";
    public const string CreationFailed = "creation-failed";

    /// <summary>Z-A: las Mega/Primigenias existen en el EQUIPO con su piedra (FormVerifier.VerifyBattleForms9a, Legality/Verifiers/FormVerifier.cs:171-190); en una caja el Core las da por invalidas.</summary>
    private static bool IsBattleFormInParty(PKM wish, Destination dest)
        => dest.Context == EntityContext.Gen9a && dest.SlotType == StorageSlotType.Party && FormInfo.IsBattleOnlyForm(wish.Species, wish.Form, dest.Generation);

    /// <summary>Niveles que se prueban al evolucionar (el validador oficial decide cual cumple el nivel de evolucion): el actual y escalones habituales hasta el 100.</summary>
    private static IEnumerable<byte> EvolutionLevels(PKM pk)
    {
        yield return pk.CurrentLevel;
        foreach (var l in new byte[] { 16, 20, 25, 30, 32, 36, 40, 45, 50, 55, 100 })
            if (l > pk.CurrentLevel) yield return l;
    }

    /// <summary>Lleva la plantilla a la especie y la forma pedidas: especie, forma (con su objeto), habilidad de la forma, apodo de especie, tipos de Gen1/2, tamano y estadisticas.</summary>
    private static void Evolve(PKM pk, PKM wish, byte level)
    {
        var abilityIndex = pk.AbilityNumber switch { 2 => 1, 4 => 2, _ => 0 };
        var evolved = pk.Species != wish.Species;
        pk.Species = wish.Species;
        if (pk.Form != wish.Form) ApplyForm(pk, wish.Form);
        if (level > pk.CurrentLevel) pk.CurrentLevel = level;
        // Z-A: la Mega lleva la habilidad de su forma, pero la Primigenia (Kyogre/Groudon) conserva la de la forma base (medido con el Core: de otro modo EncounterStatic9a no encaja).
        if (pk.Format >= 3 && !(pk is PA9 && FormInfo.IsPrimalForm(pk.Species, pk.Form))) pk.RefreshAbility(abilityIndex);
        if (!pk.IsNicknamed) pk.Nickname = SpeciesName.GetSpeciesNameGeneration(pk.Species, pk.Language, pk.Format);
        if (pk is PK1 p1) p1.SetTypes(p1.PersonalInfo);
        if (pk is IScaledSizeValue sv && (evolved || pk.Form != 0)) { sv.HeightAbsolute = sv.CalcHeightAbsolute; sv.WeightAbsolute = sv.CalcWeightAbsolute; }
        if (pk.PartyStatsPresent) pk.ResetPartyStats();
    }

    private static PKM? Create(IEncounterConvertible conv, Destination dest, EncounterCriteria criteria)
    {
        try
        {
            if (!GuardedCore.TryCall(() => conv.ConvertToPKM(dest.Trainer, criteria), TimeSpan.FromSeconds(3), out var raw) || raw is null) return null;
            if (raw.GetType() == dest.EntityType) return raw;
            return EntityConverter.ConvertToType(raw, dest.EntityType, out _);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return null; }
    }

    private static EncounterCriteria Criteria(Traits t, PKM wish)
    {
        var c = EncounterCriteria.Unrestricted with
        {
            Shiny = t.Shiny ? Shiny.Always : Shiny.Never,
            Nature = t.Nature,
            Gender = t.Gender switch { 0 => Gender.Male, 1 => Gender.Female, _ => Gender.Random },
            Form = (sbyte)wish.Form,
        };
        return c;
    }

    /// <summary>
    /// Objeto que el Core exige para la forma de esa especie (<c>FormVerifier.cs:63-69,71-73,83-85,124-126</c>): placa de Arceus, Drive de Genesect, memoria de Silvally, Orbe Griseo de Giratina (hasta Gen8),
    /// Nucleo Griseo/Cristal Adamantino/Esfera Lustrosa (Gen9). <c>null</c> si esa especie no tiene objeto de forma (o es Leyendas Arceus, donde el cambio se hace con objetos clave).
    /// </summary>
    public static ushort? RequiredFormItem(PKM pk, byte form)
    {
        if (pk is PA8) return null;
        return (Species)pk.Species switch
        {
            Species.Arceus => (ushort)FormItem.GetItemArceus(form, pk.Format),
            Species.Genesect => (ushort)FormItem.GetItemGenesect(form),
            Species.Silvally => (ushort)FormItem.GetItemSilvally(form),
            Species.Giratina when pk.Format <= 8 => (ushort)(form == 1 ? 112 : 0),     // Griseous Orb
            Species.Giratina when pk.Format >= 9 => (ushort)(form == 1 ? 1779 : 0),    // Griseous Core
            Species.Dialga when pk.Format >= 9 => (ushort)(form == 1 ? 1777 : 0),      // Adamant Crystal
            Species.Palkia when pk.Format >= 9 => (ushort)(form == 1 ? 1778 : 0),      // Lustrous Globe
            _ => null,
        };
    }

    /// <summary>
    /// Cambio de forma que permite el juego (el Core lo declara en <see cref="FormInfo.IsFormChangeable"/>), con el objeto que exige la forma:
    /// Arceus (placa), Giratina (Griseous Orb hasta Gen8), Genesect (Drive), Silvally (memoria). El resto cambia sin objeto. La validez la decide el analisis oficial.
    /// </summary>
    public static void ApplyForm(PKM pk, byte form)
    {
        pk.Form = form;
        if (pk is IFormArgument fa) fa.FormArgument = 0;
        // Legends: Arceus, el cambio de forma de Dialga/Palkia/Giratina/Arceus se hace con objetos clave: FormVerifier.cs:63-64 los da por validos sin objeto equipado.
        if (pk is PA8) return;
        if (RequiredFormItem(pk, form) is { } formItem) pk.HeldItem = formItem;
        switch ((Species)pk.Species)
        {
            case Species.Ogerpon when pk is PK9 pk9:
                pk9.HeldItem = FormItem.GetItemOgerpon(form);
                pk9.TeraTypeOverride = (form & 3) switch { 1 => MoveType.Water, 2 => MoveType.Fire, 3 => MoveType.Rock, _ => (MoveType)TeraTypeUtil.OverrideNone };
                break;
            case Species.Keldeo:
                // Keldeo Habitual/Espada va con el movimiento Secret Sword (FormVerifier: isSword == hasSword).
                if (form == 1 && !pk.HasMove((ushort)Move.SecretSword)) pk.Move1 = (ushort)Move.SecretSword;
                break;
        }
        if (pk is PA9)
        {
            var stone = ItemStorage9ZA.GetExpectedMegaStoneOrPrimalOrb(pk.Species, form);
            if (stone != 0) pk.HeldItem = stone;
        }
    }

    // ------------------------------------------------------------------------------------------------------------------------------------------------------------
    // Evaluacion exhaustiva de las rutas del Core para una tupla (prueba de imposibilidad o hallazgo del candidato)
    // ------------------------------------------------------------------------------------------------------------------------------------------------------------

    public enum RouteVerdict { Reachable, ProvenUnreachable, Unproven }

    /// <param name="Routes">Encuentros que el Core enumera para la especie y la forma en el destino.</param>
    /// <param name="Tracker">Rutas que exigen el rastreador de HOME (regla 6: no se crea).</param>
    /// <param name="FormClosed">Rutas cuya plantilla no puede dar la forma (el Core no admite el cambio de forma).</param>
    /// <param name="FormRule">Rutas cuyo candidato el Core invalida por una norma de FORMA (no por un defecto del candidato).</param>
    /// <param name="Unexplained">Rutas que fallan por algo que no se atribuye a la forma: si hay alguna, no se declara imposible.</param>
    public sealed record RouteEvaluation(RouteVerdict Verdict, string Reason, PKM? Candidate, int Routes, int Tracker, int FormClosed, int FormRule, int Unexplained);

    /// <summary>
    /// Normas de FORMA del Core que ninguna reparacion del candidato puede cumplir (la forma es lo que se pide): fuera del rango de la tabla personal del destino, solo de combate fuera de
    /// combate, solo en el equipo, de otro juego, patron de Vivillon inalcanzable. Los fallos de objeto o argumento de forma NO estan aqui: pueden ser un defecto del candidato.
    /// Fuente: Legality/Verifiers/FormVerifier.cs:34-36 (FormInvalidRangeLEQ_0F), :49-54 (FormBattle LGP/E), :171-190 (FormBattle/FormParty), :139-155 (Vivillon).
    /// </summary>
    private static bool IsFormRule(LegalityCheckResultCode code) => code is LegalityCheckResultCode.FormInvalidRangeLEQ_0F or LegalityCheckResultCode.FormBattle
        or LegalityCheckResultCode.FormParty or LegalityCheckResultCode.FormInvalidGame or LegalityCheckResultCode.FormVivillonInvalid or LegalityCheckResultCode.FormVivillonEventPre
        or LegalityCheckResultCode.TransferBad
        // Keldeo Espada exige Secret Sword (FormVerifier.cs:73-82) y Morpeko Voraz en el equipo exige poder conocer Aura Wheel (FormVerifier.cs:176-181): el Core fija la forma al aprendizaje.
        or LegalityCheckResultCode.MoveKeldeoMismatch or LegalityCheckResultCode.FormInvalidExpect_0;

    public static RouteEvaluation Evaluate(PKM wish, Destination dest, Validation.IOfficialValidator validator, Traits traits, Func<bool> expired)
    {
        var routes = Enumerate(wish, dest);
        if (routes.Count == 0)
            return new(RouteVerdict.ProvenUnreachable, $"PKHeX no enumera ninguna ruta (encuentro, transferencia o evento) para la especie {wish.Species} con la forma {wish.Form} en {dest.Version} (EncounterMovesetGenerator.GenerateEncounters devuelve 0 encuentros).", null, 0, 0, 0, 0, 0);

        int tracker = 0, closed = 0, rule = 0, unexplained = 0;
        string? sample = null;
        foreach (var enc in routes)
        {
            if (expired()) return new(RouteVerdict.Unproven, "Se agotó el tiempo antes de recorrer todas las rutas.", null, routes.Count, tracker, closed, rule, unexplained);
            if (HomeTrackerRule.IsRequired(enc, dest.Context)) { tracker++; continue; }
            if (enc is not IEncounterConvertible conv) { unexplained++; sample ??= $"{enc.GetType().Name}: la plantilla no es convertible"; continue; }
            string? why = null;
            var any = false;
            var explained = false;
            foreach (var pk in Build(conv, wish, dest, traits, expired, w => why = w))
            {
                any = true;
                var o = validator.Validate(pk, wish, dest, requireCatalogGate: true);
                if (o.Valid && pk.Species == wish.Species && pk.Form == wish.Form)
                    return new(RouteVerdict.Reachable, $"Ruta {enc.GetType().Name} (Gen{enc.Generation}, {enc.Version}).", pk, routes.Count, tracker, closed, rule, unexplained);
                if (o.Valid) { sample ??= $"{enc.GetType().Name}: válido pero con otra especie o forma"; continue; }
                if (o.BlockedByHomeTracker) { explained = true; tracker++; break; }
                LegalityAnalysis la;
                lock (EngineGlobalState.Gate) { dest.ApplyParseSettings(); la = new LegalityAnalysis(pk, dest.Personal, dest.SlotType); }
                if (la.Results.Any(r => !r.Valid && IsFormRule(r.Result))) { explained = true; rule++; break; }
                sample ??= $"{enc.GetType().Name} (Gen{enc.Generation}, {enc.Version}): " + string.Join("; ", la.Results.Where(r => !r.Valid).Select(r => r.Identifier + ":" + r.Result).Take(3));
            }
            if (explained) continue;
            if (!any && why == FormNotChangeable) { closed++; continue; }
            unexplained++;
            sample ??= $"{enc.GetType().Name} (Gen{enc.Generation}, {enc.Version}): {(any ? "el candidato no pasa la validación" : why ?? "sin candidato")}";
        }
        var detail = $"{routes.Count} rutas enumeradas por PKHeX: {tracker} exigen una matrícula (rastreador de HOME) (regla 6), {closed} no pueden dar la forma {wish.Form} (la plantilla no la trae y FormInfo.IsFormChangeable no admite el cambio), {rule} las invalida una norma de forma de PKHeX (FormVerifier)";
        return unexplained == 0
            ? new(RouteVerdict.ProvenUnreachable, detail + ". Ninguna ruta produce la especie " + wish.Species + " con la forma " + wish.Form + " en " + dest.Version + ".", null, routes.Count, tracker, closed, rule, 0)
            : new(RouteVerdict.Unproven, detail + $", {unexplained} fallan por otro motivo (p. ej. {sample}).", null, routes.Count, tracker, closed, rule, unexplained);
    }
}
