// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Engine.Changes;
using AutoLegalidad.Engine.Infra;
using AutoLegalidad.Engine.Repair;
using PKHeX.Core;

namespace AutoLegalidad.Engine.Generation;

/// <summary>
/// Generacion DIRECTA desde la plantilla del propio Core (el «IEncounterFactory» del plan 6): cuando se conoce el encuentro de origen (identidad de un evento, origen
/// fijado por la Libreria o el que reconocio el analisis oficial), se le pide al Core que cree el Pokemon con <c>IEncounterConvertible.ConvertToPKM</c> y los criterios de los
/// rasgos deseados. Es la via EXACTA para los encuentros con algoritmos propios (huevos de regalo de Gen3, sombras, eventos con PID ligado...) que ALM no sabe regenerar,
/// y la mas rapida. Lo creado se completa con la reparacion minima (EV y movimientos del usuario) y SIEMPRE pasa despues por el validador oficial independiente del motor:
/// esta estrategia nunca decide el exito.
/// </summary>
public sealed class CoreTemplateStrategy : IGenerationStrategy
{
    public string Name => "CoreTemplate";

    public IEnumerable<PKM> Generate(GenerationContext ctx)
    {
        if (ctx.OriginalEncounter is EncounterInvalid or null || ctx.OriginalEncounter is not IEncounterConvertible conv)
        {
            // Hito 3b: sin encuentro que conservar y con una forma distinta de la base, el Core sabe por que rutas de transferencia se llega a esa forma: se recorren (TransferRoutes).
            if (TransferRoutes.ShouldUse(ctx))
                foreach (var routed in TransferRoutes.Generate(ctx)) yield return routed;
            yield break;
        }
        // Un encuentro cuyo origen es HOME necesita un rastreador que el producto no crea (regla 6): el Core lo generaria sin rastreador y el motor lo clasificaria como bloqueado.
        var criteria = BuildCriteria(ctx);
        // Si el Pokemon trae un entrenador PROPIO (evento, regalo, intercambio...), se conserva su identidad: algunas plantillas eligen OT/TID al azar entre varios
        // (distribuciones de Gen3 por ciudad) y se repite hasta dar con los suyos. Si el encuentro no los reparte, no se produce nada y siguen las demas estrategias.
        var wantOt = ctx.Original.OriginalTrainerName;
        // Tarjeta de huevo PGF: el entrenador NO es de la tarjeta, lo pone el receptor (PGF.cs:272-278; IsMatchExact no compara OT/TID/SID en la rama de huevo, PGF.cs:431-445), y con el idioma
        // del evento distinto del del receptor el Core pone un nombre de relleno (EncounterUtil.GetTrainerName, EncounterUtil.cs:102-106). No hay identidad de entrenador que reproducir.
        var eggCardReceiverOt = EggCardReceiverOt(conv);
        var ownTrainer = !eggCardReceiverOt && wantOt.Length != 0 && wantOt != ctx.Dest.Trainer.OT;
        var attempts = ownTrainer ? 20_000 : Attempts(ctx.MaxAttempts, ctx.Original); // acotado por el plazo del escalon
        var otSeen = false;
        // Regalos del arreglo de bayas de Gen3 (BACD_RBCD): el generador del Core recorta SIEMPRE la semilla a 213 (EncounterGift3.GetSaneSeed: Math.Clamp(seed, 0, 213) sobre un Rand32) y por eso
        // da el mismo PID cada vez, que solo cuadra con el genero del entrenador de la variante Rubi; la variante Zafiro (RandD3_0) nunca sale valida. Se recorren las 214 semillas
        // posibles con la propia GenerateSeed32 del Core (EncounterGift3.cs:219-239); el validador oficial decide cual vale.
        if (conv is EncounterGift3 berry && berry.Method == PIDType.BACD_RBCD)
        {
            List<PK3> seededAll;
            lock (EngineGlobalState.Gate)
            {
                ctx.Dest.ApplyParseSettings();
                seededAll = [.. Gen3Gift.BerryFixCandidates(berry, ctx.Dest.Trainer)];
            }
            foreach (var seeded in seededAll)
            {
                if (ctx.Expired) { ctx.Report.TimedOut = DateTime.UtcNow >= ctx.DeadlineUtc; yield break; }
                if (ownTrainer && (seeded.OriginalTrainerName != wantOt || seeded.TID16 != ctx.Original.TID16 || seeded.Language != ctx.Original.Language)) yield break;
                ctx.Report.Produced++;
                foreach (var v in Variants(seeded, ctx)) yield return v;
            }
            yield break;
        }
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            // Si tras 100 muestras el encuentro nunca reparte este entrenador, no es de el: se deja a las demas estrategias.
            if (ownTrainer && attempt == 100 && !otSeen) yield break;
            if (ctx.Expired) { ctx.Report.TimedOut = DateTime.UtcNow >= ctx.DeadlineUtc; yield break; }
            PKM? basePk;
            lock (EngineGlobalState.Gate)
            {
                ctx.Dest.ApplyParseSettings();
                var hatchTrainer = HatchedTrainer(conv, ctx);
                // Regalo de Nueva York con entrenador propio: el TID lo sortea el Core (1-2999), asi que se repite el sorteo de semilla con el TID del Pokemon (ver Gen3Gift).
                basePk = ownTrainer && conv is EncounterGift3NY ny && Gen3Gift.Matches(ny, ctx.Original)
                    ? Gen3Gift.CreateNy(ny, ctx.Dest.Trainer, criteria, ctx.Original, () => ctx.Expired)
                    : Create(conv, ctx, criteria, hatchTrainer);
                if (basePk is not null && hatchTrainer is not null)
                    basePk = EvolveHatched(basePk, ctx);
            }
            if (basePk is null) continue;
            // Una tarjeta de huevo (WC6/PGF/WC7...) devuelve el HUEVO; si lo pedido es el Pokemon (p. ej. la evolucion de ese huevo en la Libreria) se eclosiona con la rutina publica del Core (ForceHatchPKM).
            if (basePk.IsEgg && !ctx.Original.IsEgg)
            {
                try { lock (EngineGlobalState.Gate) { ctx.Dest.ApplyParseSettings(); basePk.ForceHatchPKM(ctx.Dest.Trainer); } }
                catch (Exception ex) when (ex is not OutOfMemoryException) { continue; }
            }
            // Hito 3b: el encuentro da la especie del ENCUENTRO; si la pedida es una evolucion suya, se evoluciona (lo que el Core valide, valida; lo que no, se descarta).
            var evolved = basePk.Species != ctx.Original.Species;
            if (evolved) basePk = EvolveToWish(basePk, ctx);
            // Hito 3b: encuentros de forma dinamica (Vivillon por region, Minior, Unown...): la forma deseada se fija como la fijaria el juego, no al azar.
            basePk = AlignDynamicForm(basePk, conv, ctx);
            // Linea de Wurmple: la rama (Silcoon/Cascoon) sale del PID/EC (WurmpleUtil.IsWurmpleEvoValid); se repite la plantilla con otra naturaleza y sexo hasta que coincida (como hace ALM).
            if (evolved && WurmpleUtil.IsWurmpleEvo(ctx.Original.Species))
            {
                for (var i = 0; i < 80 && !WurmpleUtil.IsWurmpleEvoValid(basePk) && !ctx.Expired; i++)
                {
                    PKM? again;
                    lock (EngineGlobalState.Gate)
                    {
                        ctx.Dest.ApplyParseSettings();
                        again = Create(conv, ctx, criteria with { Nature = (Nature)Util.Rand.Next(25), Gender = Util.Rand.Next(2) == 0 ? Gender.Male : Gender.Female });
                    }
                    if (again is null) break;
                    basePk = AlignDynamicForm(EvolveToWish(again, ctx), conv, ctx);
                }
            }
            if (ownTrainer && basePk.OriginalTrainerName == wantOt) otSeen = true;
            if (ownTrainer && (basePk.OriginalTrainerName != wantOt || basePk.TID16 != ctx.Original.TID16 || basePk.Language != ctx.Original.Language)) continue;
            basePk = EvolveToWish(basePk, ctx, conv);
            if (NatureFormMismatch(basePk)) continue;
            ctx.Report.Produced++;

            // Hito 3b: una evolucion por intercambio (o que el Core solo admite tras pasar por otro entrenador: Kanto en SM, p. ej.) exige un manejador distinto del OT. Se ofrece primero la
            // version sin intercambiar y despues la intercambiada; el validador oficial decide.
            var variants = new List<PKM> { basePk };
            // Huevo de tarjeta cuyo entrenador pone el receptor: primero se ofrece el huevo con el entrenador que ya tenia el Pokemon del usuario (OT, TID, SID, genero) y despues el del receptor;
            // el validador oficial decide (ambos son legales para el Core: la rama de huevo de IsMatchExact no los compara).
            if (eggCardReceiverOt && wantOt.Length != 0 && KeepUserTrainer(basePk, ctx.Original) is { } keptTrainer) variants.Insert(0, keptTrainer);
            // 0.6.5 (ADR 0052): primero la version en la que el entrenador anterior es el propio JUGADOR (el Core la admite: sonda `sonda-ht`); la del «amigo» queda de respaldo si el Core la rechaza.
            if (evolved && MarkTradedByPlayer(basePk, ctx) is { } byPlayer) variants.Add(byPlayer);
            if (evolved && MarkTraded(basePk, ctx) is { } traded) variants.Add(traded);
            foreach (var variant in variants)
                foreach (var v in Variants(variant, ctx)) yield return v;
            if (ownTrainer) yield break; // identidad hallada: no hace falta seguir buscando entre miles de muestras
        }
    }

    /// <summary>Tarjetas de huevo cuyo entrenador (OT, TID, SID, genero) lo pone el receptor y no la tarjeta: PGF (PGF.cs:272-278).</summary>
    internal static bool EggCardReceiverOt(IEncounterConvertible conv) => conv is PGF { IsEgg: true };

    /// <summary>Copia del huevo con el entrenador (OT, TID, SID, genero del OT) del Pokemon del usuario; <c>null</c> si ya es el mismo.</summary>
    internal static PKM? KeepUserTrainer(PKM created, PKM user)
    {
        if (created.OriginalTrainerName == user.OriginalTrainerName && created.TID16 == user.TID16 && created.SID16 == user.SID16 && created.OriginalTrainerGender == user.OriginalTrainerGender) return null;
        var kept = created.Clone();
        kept.OriginalTrainerName = user.OriginalTrainerName;
        kept.TID16 = user.TID16;
        kept.SID16 = user.SID16;
        kept.OriginalTrainerGender = user.OriginalTrainerGender;
        kept.RefreshChecksum();
        return kept;
    }

    /// <summary>Toxtricity: la forma sale de la naturaleza (Core <c>ToxtricityUtil.GetAmpLowKeyResult</c>); un candidato cuya naturaleza no da la forma pedida no puede ser legal.</summary>
    internal static bool NatureFormMismatch(PKM pk) => pk.Species == (ushort)Species.Toxtricity && ToxtricityUtil.GetAmpLowKeyResult(pk.Nature) != pk.Form;

    /// <summary>Intentos por encuentro: para Toxtricity se muestrean mas naturalezas hasta dar la forma (12 de 25 dan Low Key).</summary>
    internal static int Attempts(int attempts, PKM wish) => wish.Species == (ushort)Species.Toxtricity ? Math.Max(attempts, 40) : attempts;

    /// <summary>(1) Con lo que el Core crea del encuentro + los EV del usuario; (2) ademas, los movimientos que el usuario pide (la reparacion descarta los ilegales).</summary>
    internal static IEnumerable<PKM> Variants(PKM basePk, GenerationContext ctx)
    {
        var withEv = basePk.Clone();
        var (perStat, total) = EvNormalizer.Limits(withEv);
        EvNormalizer.Set(withEv, EvNormalizer.Normalize(ctx.Traits.EVs, perStat, total));
        yield return Complete(withEv, ctx);

        var wanted = ctx.Traits.Moves.Where(m => m != 0).Distinct().Take(4).ToArray();
        if (wanted.Length > 0)
        {
            var withMoves = withEv.Clone();
            var set = new ushort[4];
            for (var i = 0; i < wanted.Length; i++) set[i] = wanted[i];
            withMoves.SetMoves(set);
            withMoves.HealPP();
            yield return Complete(withMoves, ctx);
        }
    }
    /// <summary>
    /// Regalo-huevo de Gen3 pedido ya ECLOSIONADO (R-004, WC3): <c>EncounterGift3.ConvertToPKM</c> solo devuelve el huevo ya eclosionado cuando el entrenador receptor NO es de Gen3
    /// (<c>hatchedEgg = IsEgg &amp;&amp; tr.Generation != 3</c>, EncounterGift3.cs:111; <c>SetForceHatchDetails</c>, EncounterGift3.cs:154-171) y el analisis oficial acepta ese resultado como
    /// <c>EncounterGift3</c>. Con el entrenador del destino de Gen3 devolvia siempre el huevo y el motor lo descartaba (<c>EggStatePreserved</c>): los 14 regalos del PokePark (DS Download Play,
    /// version R, metodo BACD_R) y sus evoluciones quedaban en NoSolution. Se pasa el MISMO entrenador del destino con solo la generacion cambiada (misma version, OT, TID, SID e idioma);
    /// no se inventa ningun dato: es la ruta publica del Core para pedir el huevo eclosionado.
    /// </summary>
    public static ITrainerInfo? HatchedTrainer(IEncounterConvertible conv, GenerationContext ctx)
        => conv is EncounterGift3 { IsEgg: true } && !ctx.Original.IsEgg && ctx.Dest.Generation == 3
            ? new SimpleTrainerInfo(ctx.Dest.Trainer) with { Generation = 4 }
            : null;

    /// <summary>
    /// El huevo eclosionado nace como la especie de la plantilla; si el usuario pide una evolucion legitima de ella (regla 14), se evoluciona con la tabla del propio Core
    /// (<c>EvolutionTree.IsSpeciesDerivedFrom</c>). Nada se da por bueno aqui: el validador oficial decide despues (el nivel, si no llega a la evolucion, lo resuelve la relajacion).
    /// </summary>
    public static PKM EvolveHatched(PKM pk, GenerationContext ctx)
    {
        var want = ctx.Original;
        if (pk.IsEgg || pk.Species == want.Species) return pk;
        if (!EvolutionTree.GetEvolutionTree(ctx.Dest.Context).IsSpeciesDerivedFrom(want.Species, want.Form, pk.Species, pk.Form, ignoreForm: true)) return pk;
        var evolved = pk.Clone();
        var wasDefaultNick = !evolved.IsNicknamed;
        var level = Math.Max(evolved.CurrentLevel, ctx.Traits.Level);
        evolved.Species = want.Species;
        evolved.Form = want.Form;
        if (wasDefaultNick) evolved.Nickname = SpeciesName.GetSpeciesNameGeneration(want.Species, evolved.Language, evolved.Format);
        evolved.CurrentLevel = (byte)level;
        evolved.RefreshAbility(evolved.PIDAbility);
        evolved.ResetPartyStats();
        evolved.RefreshChecksum();
        return evolved;
    }

    internal static PKM? Create(IEncounterConvertible conv, GenerationContext ctx, EncounterCriteria criteria, ITrainerInfo? trainer = null)
    {
        try
        {
            // Algunos generadores del Core no terminan con criterios imposibles (variocolor en un encuentro que no lo admite...): llamada con plazo (GuardedCore).
            var tr = trainer ?? ctx.Dest.Trainer;
            if (!GuardedCore.TryCall(() => conv.ConvertToPKM(tr, criteria), TimeSpan.FromSeconds(3), out var raw) || raw is null) return null;
            var converted = raw.GetType() == ctx.Dest.EntityType ? raw : EntityConverter.ConvertToType(raw, ctx.Dest.EntityType, out _);
            CoreGeneratorFixups.Apply(converted, conv); // defectos conocidos del generador del Core en 3 intercambios (Trade4PID, Trade7b, Trade8b)
            if (converted is not null && ctx.Dest.TrackerSeverity == Severity.Fishy && conv is IEncounterable en && HomeTrackerRule.IsRequired(en, ctx.Dest.Context)) HandlerUpdate.Apply(converted, ctx.Dest); // R-009: origen HOME/GO sin rastreador (el unico fallo permitido)
            return converted;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return null; }
    }
    /// <summary>Nivel minimo para llegar a <paramref name="target"/> desde la especie dada: el mayor <c>EvolutionMethod.Level</c> del camino (arbol de evoluciones del destino). 0 si no hay requisito de nivel.</summary>
    private static byte MinEvolutionLevel(EntityContext context, ushort from, byte form, ushort target, byte startLevel = 0)
    {
        try
        {
            var tree = EvolutionTree.GetEvolutionTree(context);
            byte Search(ushort s, byte f, byte acc, int depth)
            {
                if (depth > 4) return 255;
                foreach (var m in tree.Forward.GetForward(s, f).ToArray())
                {
                    if (m.Species == 0) continue;
                    // Una evolucion que exige SUBIR de nivel (EvolutionMethod.LevelUp, EvolutionMethod.cs:60: lvl >= levelMin + LevelUp) no vale con el nivel al que se capturo/creo la
                    // plantilla: p. ej. un Combee salvaje de nivel 47 solo es Vespiquen valido a partir del nivel 48.
                    var level = (byte)Math.Min(100, Math.Max(Math.Max(acc, m.Level), acc == 0 ? 0 : acc + m.LevelUp));
                    if (m.Species == target) return (byte)level;
                    var r = Search(m.Species, m.GetDestinationForm(f), (byte)level, depth + 1);
                    if (r != 255) return r;
                }
                return 255;
            }
            var found = Search(from, form, startLevel, 0);
            return found == 255 ? (byte)0 : found;
        }
        catch (Exception ex) when (ex is ArgumentOutOfRangeException or IndexOutOfRangeException) { return 0; }
    }


    /// <summary>
    /// Lleva el Pokemon creado por la plantilla hasta la especie pedida (linea evolutiva): la misma secuencia de campos que aplica ALM (<c>ShowdownEdits.SetSpeciesLevel</c>) con la API
    /// del Core: especie y forma, tamano, argumento de forma, habilidad, nivel (al menos el pedido), apodo de especie y estadisticas de equipo. No decide nada: el validador oficial acepta o descarta.
    /// </summary>
    private static PKM EvolveToWish(PKM basePk, GenerationContext ctx)
    {
        var wish = ctx.Original;
        var pk = basePk.Clone();
        try
        {
            var from = pk.Species;
            var wasNicknamed = pk.IsNicknamed;
            var abilityIndex = Math.Max(0, pk.AbilityNumber >> 1);
            pk.Species = wish.Species;
            pk.Form = wish.Form;
            if (pk is IScaledSizeValue sv)
            {
                sv.HeightAbsolute = sv.CalcHeightAbsolute;
                sv.WeightAbsolute = sv.CalcWeightAbsolute;
            }
            if (pk is IFormArgument)
            {
                lock (EngineGlobalState.Gate)
                {
                    ctx.Dest.ApplyParseSettings();
                    pk.SetSuggestedFormArgument(pk.Species, pk.Form, pk.Context, new LegalityAnalysis(pk).Info.EvoChainsAllGens, from);
                }
            }
            try { pk.RefreshAbility(abilityIndex); } catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException) { pk.RefreshAbility(0); }
            if (pk.CurrentLevel < ctx.Traits.Level) pk.CurrentLevel = ctx.Traits.Level;
            // El nivel minimo de la cadena de evolucion (EvolutionMethod.Level de cada paso): sin el, el candidato a nivel bajo era siempre invalido y la escalera lo relajaba escalon a escalon.
            var minLevel = MinEvolutionLevel(ctx.Dest.Context, basePk.Species, basePk.Form, wish.Species, basePk.CurrentLevel);
            if (pk.CurrentLevel < minLevel) pk.CurrentLevel = minLevel;
            if (pk.MetLevel > pk.CurrentLevel) pk.MetLevel = pk.CurrentLevel;
            if (!wasNicknamed)
            {
                pk.Nickname = SpeciesName.GetSpeciesNameGeneration(pk.Species, pk.Language, pk.Format);
                pk.IsNicknamed = false;
            }
            // Evoluciones por uso repetido de un movimiento (EvolutionRestrictions.IsFormArgEvolution): el Pokemon debe conocerlo (el argumento de forma ya lo fija SetSuggestedFormArgument).
            var usedMove = wish.Species switch
            {
                (ushort)Species.Overqwil => (ushort)Move.BarbBarrage,
                (ushort)Species.Wyrdeer => (ushort)Move.PsyshieldBash,
                (ushort)Species.Annihilape => (ushort)Move.RageFist,
                _ => (ushort)0,
            };
            if (usedMove != 0 && !pk.HasMove(usedMove)) { pk.Move1 = usedMove; pk.HealPP(); }
            // Pikachu y Eevee de Galar (compañeros con Gigamax): el Core prohibe evolucionar a un PK8 con CanGigantamax (EvolutionGroupHOME.cs:267-273); la especie evolucionada ya no lo tiene.
            // SOLO esas tres especies: en el resto la bandera se conserva (una tarjeta de Gigamax, p. ej. el Squirtle/Bulbasaur de regalo con CanGigantamax, exige que el Pokemon evolucionado la mantenga:
            // WC8.IsMatchExact, WC8.cs:722 «g.CanGigantamax != CanGigantamax && !Gigantamax.CanToggle(...)», y MiscVerifierPK8.cs:46-55).
            if (pk is IGigantamax gmax && gmax.CanGigantamax && wish.Species != from && from is (ushort)Species.Pikachu or (ushort)Species.Meowth or (ushort)Species.Eevee) gmax.CanGigantamax = false;
            // Leyendas Z-A: las marcas de Movimiento + dependen de la especie (PlusRecordApplicator.cs:199-202); tras evolucionar se recalculan con la API del Core.
            if (pk is PA9 pa9 && pa9.PersonalInfo is IPermitPlus permit)
            {
                lock (EngineGlobalState.Gate)
                {
                    ctx.Dest.ApplyParseSettings();
                    pa9.SetPlusFlags(permit, PlusRecordApplicatorOption.LegalCurrent);
                }
            }
            // Toxtricity: la forma sale de la naturaleza (ToxtricityUtil.GetAmpLowKeyResult), como hace ALM (ShowdownEdits.SetSpeciesLevel).
            if (wish.Species == (ushort)Species.Toxtricity)
            {
                var guard = 0;
                while (ToxtricityUtil.GetAmpLowKeyResult(pk.Nature) != wish.Form && guard++ < 200) pk.Nature = (Nature)Util.Rand.Next(25);
            }
            // Dunsparce -> Dudunsparce y Tandemaus -> Maushold: la forma sale de EncryptionConstant % 100 y exigen conocer un movimiento (EvolutionMethod.cs:85-86 y EvolutionRestrictions.cs:46-66,
            // internos del Core: se replica la regla, que es una comparacion trivial).
            if (wish.Species is (ushort)Species.Dudunsparce or (ushort)Species.Maushold)
            {
                var rare = wish.Species == (ushort)Species.Dudunsparce ? wish.Form == 1 : wish.Form == 0;
                var rnd = Util.Rand;
                uint ec;
                do ec = rnd.Rand32(); while ((ec % 100 == 0) != rare);
                pk.EncryptionConstant = ec;
                var required = wish.Species == (ushort)Species.Dudunsparce ? (ushort)Move.HyperDrill : (ushort)Move.PopulationBomb;
                if (!pk.HasMove(required)) { pk.Move1 = required; pk.HealPP(); }
            }
            // Let's Go: los valores de despertar minimos legales dependen del nivel y las estadisticas (IAwakened.cs:52-60); el nivel pedido (p. ej. 100) los sube.
            if (pk is PB7 pb7 && pk.CurrentLevel != basePk.CurrentLevel) { pb7.ResetPartyStats(); pb7.AwakeningMinimum(); }
            pk.ResetPartyStats();
            pk.RefreshChecksum();
            return pk;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return basePk; // sin evolucion: el motor lo descartara por la identidad de la especie
        }
    }

    /// <summary>
    /// Copia del Pokemon como si hubiera pasado por otro entrenador (intercambio) y hubiera vuelto a su dueno: el manejador queda registrado con la API publica del Core
    /// (<c>IHandlerUpdate.UpdateHandler</c>) y la entidad vuelve al entrenador del destino. No toca ningun rastreador de HOME (regla 6). Null si el formato no tiene manejador.
    /// </summary>
    private static PKM? MarkTradedByPlayer(PKM pk, GenerationContext ctx)
    {
        if (pk.IsEgg || pk.Format < 6 || pk is not (IHandlerUpdate or PB8)) return null;
        var c = pk.Clone();
        var tr = ctx.Dest.Trainer;
        c.HandlingTrainerName = tr.OT;
        c.HandlingTrainerGender = tr.Gender;
        if (c is IHandlerLanguage hl) hl.HandlingTrainerLanguage = (byte)tr.Language;
        c.CurrentHandler = 0;
        c.RefreshChecksum();
        return c;
    }

    /// <summary>Variante de respaldo: el entrenador anterior es un «amigo» inventado (solo si el Core rechaza que sea el jugador).</summary>
    private static PKM? MarkTraded(PKM pk, GenerationContext ctx)
    {
        // BD/SP: PB8 tiene UpdateHandler (PB8.cs:61-81) pero NO implementa IHandlerUpdate, asi que el intercambio nunca se marcaba (medido: el Magikarp del intercambio de BDSP evolucionado
        // a Gyarados quedaba con «El Entrenador actual no puede ser EO», HistoryVerifier.cs:120-121): se usa la rutina publica PKH.UpdateHandler (PKH.cs), que fija CurrentHandler a 1.
        if (pk.IsEgg || pk is not (IHandlerUpdate or PB8)) return null;
        try
        {
            var c = pk.Clone();
            var partner = new SimpleTrainerInfo(pk.Version) { OT = "Amigo", TID16 = 40404, SID16 = 4040, Gender = (byte)(ctx.Dest.Trainer.Gender ^ 1), Language = pk.Language };
            lock (EngineGlobalState.Gate)
            {
                ctx.Dest.ApplyParseSettings();
                if (c is IHandlerUpdate hu)
                {
                    hu.UpdateHandler(partner);
                    hu.UpdateHandler(ctx.Dest.Trainer);
                }
                else if (c is PB8 pb && !pb.BelongsTo(ctx.Dest.Trainer))
                    PKH.UpdateHandler(pb, ctx.Dest.Trainer);
            }
            c.RefreshChecksum();
            return c;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return null; }
    }

    private static readonly Dictionary<byte, (byte Console, byte Country, byte Region)?> VivillonGeoCache = [];

    /// <summary>
    /// Si la plantilla es de forma dinamica (<c>EncounterUtil.FormDynamic</c>/<c>FormRandom</c>: el Core acepta cualquier forma del rango, <c>IsRandomUnspecificForm</c>) y la forma deseada es otra
    /// de las que el Core admite, se fija. El patron de Vivillon (Scatterbug/Spewpa/Vivillon) lo decide la region de la consola: se elige la region de consola, el pais y la region para los que
    /// <c>Vivillon3DS.IsPatternValid/IsPatternNative</c> dan por buena esa forma (FormVerifier.cs:96-119). El validador oficial decide igual que siempre.
    /// </summary>
    private static PKM AlignDynamicForm(PKM pk, IEncounterConvertible conv, GenerationContext ctx)
    {
        if (conv is not IEncounterTemplate t || pk.Species != ctx.Original.Species) return pk;
        // Forma dinamica (el Core acepta cualquier forma del rango) o forma que el Core permite cambiar desde la de la plantilla (FormInfo.IsFormChangeable: Zygarde en Gen7/8/9, Burmy, Deerling...).
        var changeable = t.Form != ctx.Original.Form && FormInfo.IsFormChangeable(t.Species, t.Form, ctx.Original.Form, t.Context, ctx.Dest.Context);
        if (t.Form < EncounterUtil.FormDynamic && !changeable) return pk;
        var wish = ctx.Original.Form;
        var vivillon = pk.Species is (ushort)Species.Scatterbug or (ushort)Species.Spewpa or (ushort)Species.Vivillon;
        var geoOk = !vivillon || wish > Vivillon3DS.MaxWildFormID || pk is not IRegionOrigin cur
            || (Vivillon3DS.IsPatternValid(wish, cur.ConsoleRegion) && Vivillon3DS.IsPatternNative(wish, cur.Country, cur.Region));
        if (pk.Form == wish && geoOk) return pk;
        try
        {
            var result = pk.Clone();
            if (vivillon && wish <= Vivillon3DS.MaxWildFormID && result is IRegionOrigin ro)
            {
                if (!TryGetVivillonGeo(wish, out var geo)) return pk;
                ro.ConsoleRegion = geo.Console; ro.Country = geo.Country; ro.Region = geo.Region;
            }
            result.Form = wish;
            if (result is IScaledSizeValue sv) { sv.HeightAbsolute = sv.CalcHeightAbsolute; sv.WeightAbsolute = sv.CalcWeightAbsolute; }
            try { result.RefreshAbility(Math.Max(0, result.AbilityNumber >> 1)); } catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException) { result.RefreshAbility(0); }
            result.ResetPartyStats();
            result.RefreshChecksum();
            return result;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return pk; }
    }

    private static bool TryGetVivillonGeo(byte form, out (byte Console, byte Country, byte Region) geo)
    {
        lock (VivillonGeoCache)
        {
            if (!VivillonGeoCache.TryGetValue(form, out var cached))
            {
                cached = null;
                // Region de consola valida para el patron Y pais de esa region (Locale3DS.IsConsoleRegionCountryValid: el Core rechaza «El pais no se encuentra en la region de la 3DS»).
                for (byte console = 0; console < 8 && cached is null; console++)
                {
                    if (!Vivillon3DS.IsPatternValid(form, console)) continue;
                    for (var country = 0; country < 256 && cached is null; country++)
                    {
                        if (!Locale3DS.IsConsoleRegionCountryValid(console, (byte)country)) continue;
                        for (var region = 0; region < 128; region++)
                            if (Vivillon3DS.IsPatternNative(form, (byte)country, (byte)region)) { cached = (console, (byte)country, (byte)region); break; }
                    }
                }
                VivillonGeoCache[form] = cached;
            }
            geo = cached ?? default;
            return cached is not null;
        }
    }

    /// <summary>
    /// Hito 3b: la plantilla del Core crea la especie del ENCUENTRO (p. ej. el Vulpix de una incursion o el Deerling de un huevo); si el usuario pide una especie
    /// evolucionada de esa (segun el arbol de evolucion del propio Core para el destino), se evoluciona aqui y el analisis oficial decide si es legal (nivel, forma, EC).
    /// Sin esto, <c>SpeciesIdentity</c> rechazaba todo lo creado y se caia a la relajacion de rasgos.
    /// </summary>
    internal static PKM EvolveToWish(PKM pk, GenerationContext ctx, IEncounterConvertible? conv = null)
    {
        var wish = ctx.Original;
        if (pk.Species == wish.Species) return pk;
        bool derived;
        try { derived = EvolutionTree.GetEvolutionTree(ctx.Dest.Context).IsSpeciesDerivedFrom(wish.Species, wish.Form, pk.Species, pk.Form); }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException) { return pk; }
        if (!derived) return pk;
        var evolved = pk.Clone();
        var ability = evolved.AbilityNumber;
        var wasNicknamed = evolved.IsNicknamed;
        evolved.Species = wish.Species;
        evolved.Form = wish.Form;
        if (!wasNicknamed && evolved.Format > 3) evolved.ClearNickname(); // en Gen3 (incl. XD/Colosseum) el Core valida el nombre original de las sombras
        evolved.CurrentLevel = Math.Max(evolved.CurrentLevel, Math.Min((byte)100, ctx.Traits.Level));
        // Una especie/forma de un solo sexo (p. ej. Meowstic forma 1) fija el sexo del Pokemon evolucionado.
        var pi = evolved.PersonalInfo;
        if (!pi.IsDualGender) evolved.Gender = pi.Genderless ? (byte)2 : pi.OnlyFemale ? (byte)1 : (byte)0;
        else if (SpeciesCategory.IsFormGenderSpecific(evolved.Species)) evolved.Gender = (byte)(evolved.Form & 1); // el sexo es la forma (Meowstic...)
        if (ability is >= 1 and <= 4) evolved.RefreshAbility(ability == 4 ? 2 : ability - 1);
        if (evolved.PartyStatsPresent) evolved.SetStats(evolved.GetStats(evolved.PersonalInfo));
        return FixEvolutionEc(evolved, ctx, conv);
    }

    /// <summary>
    /// Dudunsparce y Maushold: la forma depende de la EC (<see cref="EvolutionEc"/>). Si la EC esta libre (huevos, salvajes sin semilla) se ajusta; si sale de una semilla de
    /// incursion de Teratipo/distribucion (32 bits), se busca la primera semilla del encuentro con esa clase de EC que conserva naturaleza, sexo, habilidad y variocolor.
    /// El analisis oficial decide despues si el resultado es legal.
    /// </summary>
    private static PKM FixEvolutionEc(PKM evolved, GenerationContext ctx, IEncounterConvertible? conv)
    {
        if (!EvolutionEc.Applies(evolved.Species) || evolved.Format < 6) return evolved;
        var rare = EvolutionEc.WantsRare(evolved.Species, evolved.Form);
        if (EvolutionEc.IsRare(evolved.EncryptionConstant) == rare) return evolved;
        if (conv is ITeraRaid9 raid && evolved is PK9 p9)
        {
            var model = new Optimization.Tera9Model(raid, (byte)p9.TeraTypeOriginal);
            var nature = evolved.Nature;
            var gender = evolved.Gender;
            var ability = evolved.Ability;
            var shiny = evolved.IsShiny;
            var seeded = Optimization.SeedSpaceSearch.FindSeed(model, evolved,
                s => EvolutionEc.IsRare(s.EncryptionConstant) == rare && s.Nature == nature && s.Gender == gender && s.Ability == ability && s.IsShiny == shiny,
                ctx.DeadlineUtc, ctx.Cancellation, seed => EvolutionEc.IsRare(model.EcOf(seed)) == rare);
            return seeded ?? evolved;
        }
        var free = evolved.Clone();
        free.EncryptionConstant = EvolutionEc.Nearest(free.EncryptionConstant, rare);
        return free;
    }

    internal static PKM Complete(PKM pk, GenerationContext ctx)
    {
        // Hito 3b: las utilidades de sugerencia de movimientos del Core pueden lanzar con un candidato raro (LearnGroup8.cs:144, IsDownLeveled de una incursion de SWSH con un nivel de
        // captura fuera de rango): un candidato que no se puede reparar se ofrece SIN reparar y el validador oficial decide; la excepcion no aborta la operacion entera.
        try { return MinimalRepair.Run(pk, ctx.Dest, new ChangeLog()); }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException or InvalidOperationException) { return pk; }
    }

    internal static EncounterCriteria BuildCriteria(GenerationContext ctx)
    {
        // Un huevo de tarjeta (WC6/PGF/WC7...): el Core construye el huevo con sus propios datos; los criterios (variocolor, naturaleza, sexo) de un huevo en blanco lo dejan invalido (EC == PID, relearn).
        if (ctx.OriginalEncounter is DataMysteryGift { IsEgg: true }) return EncounterCriteria.Unrestricted;
        var t = ctx.Traits;
        var c = EncounterCriteria.Unrestricted with
        {
            Shiny = t.Shiny ? Shiny.Always : Shiny.Never,
            Nature = t.Nature,
            Gender = t.Gender switch { 0 => Gender.Male, 1 => Gender.Female, _ => Gender.Random },
        };
        // Habilidad: por el indice que ocupa en la tabla personal del destino.
        IPersonalInfo? pinfo = null;
        try { pinfo = ctx.Dest.Personal.GetFormEntry(t.Species, t.Form); }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException) { }
        if (t.Ability >= 0 && pinfo is IPersonalAbility12 pi)
        {
            var permission = t.Ability == pi.Ability1 ? AbilityPermission.OnlyFirst
                : t.Ability == pi.Ability2 ? AbilityPermission.OnlySecond
                : pinfo is IPersonalAbility12H h && t.Ability == h.AbilityH ? AbilityPermission.OnlyHidden
                : AbilityPermission.Any12H;
            c = c with { Ability = permission };
        }
        if (ctx.Original.Species == (ushort)Species.Unown) c = c with { Form = (sbyte)ctx.Original.Form };
        return c;
    }
}
