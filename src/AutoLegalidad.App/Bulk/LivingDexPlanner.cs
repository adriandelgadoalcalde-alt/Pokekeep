// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Catalog.Library;
using AutoLegalidad.Ui.Operation;
using PKHeX.Core;

namespace AutoLegalidad.Ui.Bulk;

/// <summary>Qué formas genera «Completar el juego» (diseño §1.5).</summary>
public enum LivingDexForms
{
    /// <summary>La forma 0 si es generable; si no, la primera forma generable por índice. Por defecto: cabe en todos los juegos menos XD.</summary>
    OnePerSpecies,
    /// <summary>Cada especie-forma almacenable. En Gen 4, Gen 5 y Gen 7 no cabe entero en una partida vacía.</summary>
    AllStorable,
    /// <summary>Forma 0 + las formas regionales (Alola, Galar, Hisui, Paldea) que existan en el juego.</summary>
    OnePlusRegional,
}

public enum LivingDexOrder
{
    /// <summary>Pokédex nacional ascendente; dentro de cada especie, forma 0 y después por índice de forma.</summary>
    National,
    /// <summary>Pokédex regional del juego cuando el Core la expone (SV, SWSH, BDSP, LA, Z-A); lo que no tiene número regional va al final en orden nacional.</summary>
    Regional,
}

public sealed record LivingDexOptions
{
    public LivingDexForms Forms { get; init; } = LivingDexForms.OnePerSpecies;
    public LivingDexOrder Order { get; init; } = LivingDexOrder.National;
    /// <summary>Caja (0 = la primera) desde la que se buscan casillas libres.</summary>
    public int StartBox { get; init; }
    /// <summary>«Omitir lo que ya tengo»: si en cajas o equipo hay un Pokémon LEGAL de esa especie-forma, no se genera otro.</summary>
    public bool SkipOwned { get; init; } = true;
    /// <summary>R-009: se pueden intentar los orígenes que exigen la matrícula de HOME (resultado «Necesita pasar por HOME», nunca «legal»).</summary>
    public bool IncludeHome { get; init; }
    /// <summary>Máximo de orígenes distintos que se prueban por especie-forma; 0 = TODOS los ofrecibles, en orden de prioridad (Hito 4b: una forma que el primer origen no admite la da otro).</summary>
    public int MaxOriginsPerSpecies { get; init; }
    /// <summary>Hito 5d (ADR 0046): para legendarios, sublegendarios y singulares se prueban PRIMERO sus tarjetas de evento (las mejores primero) y, si ninguna sale legal, el resto de orígenes como siempre. Desactivado por defecto.</summary>
    public bool PreferEvent { get; init; }
}

public enum LivingDexSkipKind
{
    AlreadyHad,
    CannotGenerate,
    OnlyWithHome,
    NotStorable,
    /// <summary>El Core demostró que esa forma no existe en este juego (formato de guardado que no la guarda, o ninguna ruta legal): no se planifica y se informa con la prueba.</summary>
    ProvenImpossible,
    /// <summary>Hito 4c: existe en el juego pero SOLO con un DLC que esta partida no tiene (La máscara turquesa, El disco índigo, Megadimensión...).</summary>
    RequiresExpansion,
}

public sealed record LivingDexSkip(LivingDexCandidate Candidate, LivingDexSkipKind Kind, string Reason);

/// <summary>Una especie-forma que se va a generar, con sus orígenes (en orden de prueba) y la casilla libre que le toca (null si ya no caben).</summary>
public sealed record LivingDexTarget(LivingDexCandidate Candidate, IReadOnlyList<LivingDexOrigin> Origins, ISlotInfo? Slot)
{
    public bool Fits => Slot is not null;
    public bool NeedsHome => Origins.Count > 0 && Origins[0].HomeRequired;
}

public sealed class LivingDexPlan
{
    public required LivingDexOptions Options { get; init; }
    /// <summary>Lo que se generaría, en el orden elegido (los primeros <see cref="Fit"/> tienen casilla).</summary>
    public required IReadOnlyList<LivingDexTarget> Targets { get; init; }
    public required IReadOnlyList<LivingDexSkip> Skips { get; init; }
    /// <summary>Casillas libres desde la caja de inicio (vacías, escribibles, de caja).</summary>
    public required IReadOnlyList<ISlotInfo> FreeSlots { get; init; }
    public required int SlotsPerBox { get; init; }

    public int Planned => Targets.Count;
    public int Fit => Targets.Count(t => t.Fits);
    public int Missing => Planned - Fit;
    public bool AllFit => Missing == 0;
    public int BoxesNeeded => SlotsPerBox == 0 ? 0 : (Planned + SlotsPerBox - 1) / SlotsPerBox;
    public int AlreadyHad => Skips.Count(s => s.Kind == LivingDexSkipKind.AlreadyHad);
    public int CannotGenerate => Skips.Count(s => s.Kind is LivingDexSkipKind.CannotGenerate or LivingDexSkipKind.NotStorable or LivingDexSkipKind.ProvenImpossible or LivingDexSkipKind.RequiresExpansion);
    public int RequiresExpansion => Skips.Count(s => s.Kind == LivingDexSkipKind.RequiresExpansion);
    public int ProvenImpossible => Skips.Count(s => s.Kind == LivingDexSkipKind.ProvenImpossible);
    public int OnlyWithHome => Skips.Count(s => s.Kind == LivingDexSkipKind.OnlyWithHome);

    /// <summary>Los que se generarían de verdad (con casilla), para «Generar hasta donde quepa».</summary>
    public IReadOnlyList<LivingDexTarget> ToGenerate => [.. Targets.Where(t => t.Fits)];

    /// <summary>«Caben 540 de 555. Faltan 15 casillas libres (media caja).» / «Caben todos.» (diseño §1.6).</summary>
    public string FitText()
    {
        if (Planned == 0) return AppIdioma.T("No hay nada que generar.", "There is nothing to generate.");
        if (AllFit) return AppIdioma.T("Caben todos.", "They all fit.");
        var m = Missing;
        var cajas = SlotsPerBox == 0 ? string.Empty : m * 2 == SlotsPerBox ? AppIdioma.T(" (media caja)", " (half a box)") : m >= SlotsPerBox ? AppIdioma.F(" ({0:0.#} cajas)", " ({0:0.#} boxes)", (double)m / SlotsPerBox) : string.Empty;
        return AppIdioma.F("Caben {0} de {1}. Faltan {2} casillas libres{3}.", "{0} of {1} fit. {2} free slots are missing{3}.", Fit, Planned, m, cajas);
    }

    /// <summary>Segundos por Pokémon con un proceso por solicitud (diseño §1.10; se sustituye por la cifra medida en el informe del hito).</summary>
    public static double SecondsPerPokemon(GameVersion version) => version == GameVersion.PLA ? 3.3 : 0.8;

    public TimeSpan Estimate(GameVersion version, int workers) => TimeSpan.FromSeconds(Math.Max(1, Fit) * SecondsPerPokemon(version) / Math.Max(1, workers));
}

/// <summary>
/// Planificador de «Completar el juego» (diseño §1.1-1.7). Sin tocar la partida ni llamar al motor: parte de los candidatos del catálogo (<see cref="LivingDexQueries"/>), filtra por lo que la
/// tabla personal de la partida admite, aplica las opciones de formas y de orden, descuenta lo que ya se tiene y asigna las casillas LIBRES (nunca sobrescribe; el equipo no se usa).
/// </summary>
public static class LivingDexPlanner
{
    /// <param name="presentInSave">SOLO PRUEBAS: sustituye la comprobación contra la tabla personal de la partida.</param>
    public static LivingDexPlan Plan(SaveFile sav, IReadOnlyList<LivingDexCandidate> candidates, LivingDexOptions options, IReadOnlySet<(int Species, int Form)>? owned = null,
        Func<LivingDexCandidate, bool>? presentInSave = null)
    {
        var skips = new List<LivingDexSkip>();
        var usable = new List<LivingDexCandidate>();
        foreach (var c in candidates)
        {
            if (!(presentInSave?.Invoke(c) ?? PresentInSave(sav, c)))
            {
                if (ExpansionInfo.MissingExpansion(sav, c.Species, c.Form) is { } dlc) skips.Add(new LivingDexSkip(c, LivingDexSkipKind.RequiresExpansion, dlc));
                else skips.Add(new LivingDexSkip(c, LivingDexSkipKind.ProvenImpossible, AppIdioma.T("PKHeX lo demuestra: la tabla de datos de ESTA partida (Personal.IsPresentInGame) no incluye esta especie y forma, así que no puede existir en ella.", "PKHeX proves it: THIS save’s data table (Personal.IsPresentInGame) doesn’t include this species and form, so it can’t exist in it.")));
                continue;
            }
            if (!FormFitsFormat(sav, c, out var formatReason)) { skips.Add(new LivingDexSkip(c, LivingDexSkipKind.ProvenImpossible, formatReason)); continue; }
            usable.Add(c);
        }

        var chosen = new List<LivingDexCandidate>();
        foreach (var species in usable.GroupBy(c => c.Species).OrderBy(g => g.Key))
        {
            var forms = species.OrderBy(c => c.Form).ToList();
            var generable = forms.Where(c => c.Generable && c.StorableInBox).ToList();
            switch (options.Forms)
            {
                case LivingDexForms.AllStorable:
                    chosen.AddRange(generable);
                    foreach (var c in forms.Where(c => !generable.Contains(c))) skips.Add(Unavailable(c, options));
                    break;
                case LivingDexForms.OnePlusRegional:
                {
                    var baseForm = Representative(generable);
                    if (baseForm is not null) chosen.Add(baseForm);
                    chosen.AddRange(generable.Where(c => c.Regional && !ReferenceEquals(c, baseForm)));
                    if (baseForm is null && !generable.Any(c => c.Regional)) skips.Add(Unavailable(Representative(forms)!, options));
                    break;
                }
                default:
                {
                    var one = Representative(generable);
                    if (one is not null) chosen.Add(one);
                    else skips.Add(Unavailable(forms.FirstOrDefault(c => c.Form == 0) ?? forms[0], options));
                    break;
                }
            }
        }

        var targets = new List<(LivingDexCandidate C, IReadOnlyList<LivingDexOrigin> Origins)>();
        foreach (var c in chosen)
        {
            if (options.SkipOwned && owned is not null && owned.Contains((c.Species, c.Form)))
            {
                skips.Add(new LivingDexSkip(c, LivingDexSkipKind.AlreadyHad, AppIdioma.T("Ya hay un Pokémon legal de esta especie y forma en tus cajas o equipo.", "There is already a legal Pokémon of this species and form in your boxes or party.")));
                continue;
            }
            var origins = options.PreferEvent && EventPreference.IsPreferredSpecies(c.Species) ? EventFirst(c.Origins) : c.Origins;
            targets.Add((c, options.MaxOriginsPerSpecies > 0 ? [.. origins.Take(options.MaxOriginsPerSpecies)] : origins));
        }

        var ordered = Order(sav, targets, options.Order);
        var free = FreeSlots(sav, options.StartBox);
        var planned = new List<LivingDexTarget>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++)
            planned.Add(new LivingDexTarget(ordered[i].C, ordered[i].Origins, i < free.Count ? free[i] : null));

        return new LivingDexPlan
        {
            Options = options,
            Targets = planned,
            Skips = [.. skips.OrderBy(s => s.Candidate.Species).ThenBy(s => s.Candidate.Form)],
            FreeSlots = free,
            SlotsPerBox = sav.HasBox ? sav.BoxSlotCount : 0,
        };
    }

    /// <summary>Las tarjetas de evento (ordenadas por <see cref="EventPreference.Ordered"/>, como mucho <see cref="EventPreference.MaxAttemptsPerPokemon"/>) delante del resto de orígenes, que conservan su orden.</summary>
    internal static IReadOnlyList<LivingDexOrigin> EventFirst(IReadOnlyList<LivingDexOrigin> origins)
    {
        var events = EventPreference.Ordered(origins).Take(EventPreference.MaxAttemptsPerPokemon).ToList();
        return events.Count == 0 ? origins : [.. events, .. origins.Where(o => !events.Contains(o))];
    }

    /// <summary>
    /// La forma que representa a la especie en «una por especie»: la forma 0 si es generable; si no, la primera. Con el ajuste de HOME se prefiere una forma que NO necesite la matrícula (la especie queda cubierta igual
    /// y no se añade un Pokémon «sin matrícula» que no hace falta): forma 0 sin HOME, otra forma sin HOME, forma 0 con HOME, la primera.
    /// </summary>
    private static LivingDexCandidate? Representative(IReadOnlyList<LivingDexCandidate> list)
    {
        static bool NeedsHome(LivingDexCandidate c) => c.Origins.Count > 0 && c.Origins[0].HomeRequired;
        return list.FirstOrDefault(c => c.Form == 0 && !NeedsHome(c)) ?? list.FirstOrDefault(c => !NeedsHome(c)) ?? list.FirstOrDefault(c => c.Form == 0) ?? list.FirstOrDefault();
    }

    /// <summary>
    /// Prueba con el Core de que el formato de guardado de la partida puede guardar la forma: se crea una entidad en blanco, se le pone la forma y se lee de vuelta. En Gen 3 (<c>G3PKM.Form</c>) solo Unown guarda forma
    /// (se deduce del PID): la forma de Deoxys la decide la versión del juego, así que sus otras formas no existen en una partida de ese juego.
    /// </summary>
    public static bool FormFitsFormat(SaveFile sav, LivingDexCandidate c, out string reason)
    {
        reason = string.Empty;
        if (c.Form == 0) return true;
        try
        {
            var pk = sav.BlankPKM;
            pk.Species = (ushort)c.Species;
            pk.Form = (byte)c.Form;
            if (pk.Form == c.Form) return true;
            reason = AppIdioma.F("Imposible en este juego, lo demuestra PKHeX: el formato {0} de esta partida no guarda la forma {1} de esta especie (se decide por la versión del juego: PKHeX.Core G3PKM.Form), así que no puede existir en una caja de este juego.", "Impossible in this game, PKHeX proves it: this save’s format {0} doesn’t store form {1} of this species (it is decided by the game version: PKHeX.Core G3PKM.Form), so it can’t exist in a box of this game.", sav.PKMType.Name, c.Form);
            return false;
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException) { return true; }
    }

    private static LivingDexSkip Unavailable(LivingDexCandidate c, LivingDexOptions options)
    {
        if (c.ProvenImpossible)
            return new LivingDexSkip(c, LivingDexSkipKind.ProvenImpossible, AppIdioma.T("Imposible en este juego, lo demuestra PKHeX: ", "Impossible in this game, PKHeX proves it:") + c.UnavailableReason);
        if (c.Generable && !c.StorableInBox)
            return new LivingDexSkip(c, LivingDexSkipKind.NotStorable, AppIdioma.T("Esta forma solo existe en el equipo o en combate: no cabe en una caja.", "This form only exists in the party or in battle: it doesn’t fit in a box."));
        if (c.OnlyWithHome)
            return new LivingDexSkip(c, LivingDexSkipKind.OnlyWithHome, options.IncludeHome
                ? AppIdioma.T("Solo existe pasando por Pokémon HOME y PKHeX no da por válido ningún Pokémon con este origen sin su matrícula real: no se puede generar.", "It only exists by passing through Pokémon HOME and PKHeX doesn’t consider any Pokémon with this origin valid without its real tracker: it can’t be generated.")
                : Textos.NecesitaHomeSinAjuste);
        return new LivingDexSkip(c, LivingDexSkipKind.CannotGenerate, c.UnavailableReason ?? AppIdioma.T("No se puede generar.", "It can’t be generated."));
    }

    private static bool PresentInSave(SaveFile sav, LivingDexCandidate c)
    {
        try
        {
            if (c.Species > sav.MaxSpeciesID) return false;
            return sav.Personal.IsPresentInGame((ushort)c.Species, (byte)c.Form);
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException) { return false; }
    }

    // ------------------------------------------------------------------------------------------------ orden
    private static List<(LivingDexCandidate C, IReadOnlyList<LivingDexOrigin> Origins)> Order(SaveFile sav, List<(LivingDexCandidate C, IReadOnlyList<LivingDexOrigin> Origins)> list, LivingDexOrder order)
    {
        var national = list.OrderBy(t => t.C.Species).ThenBy(t => t.C.Form);
        if (order == LivingDexOrder.National || !HasRegionalDex(sav)) return [.. national];
        return [.. list.OrderBy(t => RegionalKey(sav, t.C).Group).ThenBy(t => RegionalKey(sav, t.C).Index).ThenBy(t => t.C.Species).ThenBy(t => t.C.Form)];
    }

    /// <summary>Juegos cuya tabla personal del Core trae el número de la Pokédex regional (comprobado en el Core fijado, diseño §1.4).</summary>
    public static bool HasRegionalDex(SaveFile sav) => sav is SAV9SV or SAV8SWSH or SAV8BS or SAV8LA or SAV9ZA;

    /// <summary>(grupo, número): grupo 0 = Pokédex principal, 1-2 = las ampliaciones (Kitakami, Blueberry / Armadura, Corona), 9 = sin número regional (va al final en orden nacional).</summary>
    public static (int Group, int Index) RegionalKey(SaveFile sav, LivingDexCandidate c)
    {
        try
        {
            var pi = sav.Personal[(ushort)c.Species, (byte)c.Form];
            return pi switch
            {
                PersonalInfo9SV sv => sv.DexPaldea != 0 ? (0, sv.DexPaldea) : sv.DexKitakami != 0 ? (1, sv.DexKitakami) : sv.DexBlueberry != 0 ? (2, sv.DexBlueberry) : (9, 0),
                PersonalInfo8SWSH ss => ss.PokeDexIndex != 0 ? (0, ss.PokeDexIndex) : ss.ArmorDexIndex != 0 ? (1, ss.ArmorDexIndex) : ss.CrownDexIndex != 0 ? (2, ss.CrownDexIndex) : (9, 0),
                PersonalInfo8BDSP bd => bd.PokeDexIndex != 0 ? (0, bd.PokeDexIndex) : (9, 0),
                PersonalInfo8LA la => la.DexIndexHisui != 0 ? (0, la.DexIndexHisui) : (9, 0),
                PersonalInfo9ZA za => za.DexIndex != 0 ? (0, za.DexIndex) : (9, 0),
                _ => (9, 0),
            };
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException) { return (9, 0); }
    }

    // ------------------------------------------------------------------------------------------------ casillas
    /// <summary>
    /// Casillas libres desde la caja <paramref name="startBox"/> (casilla a casilla, caja a caja): vacías, escribibles, de caja y no protegidas (se saltan las bloqueadas, la del inicial y las de los equipos
    /// de combate de Gen 7+, y las que Let's Go marca como equipo). El equipo no se usa NUNCA.
    /// </summary>
    public static IReadOnlyList<ISlotInfo> FreeSlots(SaveFile sav, int startBox)
    {
        var list = new List<ISlotInfo>();
        if (!sav.HasBox) return list;
        for (var b = Math.Max(0, startBox); b < sav.BoxCount; b++)
        {
            for (var i = 0; i < sav.BoxSlotCount; i++)
            {
                ISlotInfo slot = new SlotInfoBox(b, i, sav);
                if (slot.Type == StorageSlotType.Box && slot.CanWriteTo(sav) && !sav.IsBoxSlotOverwriteProtected(b, i) && slot.IsEmpty(sav))
                    list.Add(slot);
            }
        }
        return list;
    }

    /// <summary>
    /// Especie-forma de los Pokémon LEGALES que ya hay en cajas o equipo (análisis oficial de cada uno: puede tardar unos segundos con muchas casillas; llamar fuera del hilo de la interfaz).
    /// </summary>
    public static IReadOnlySet<(int Species, int Form)> OwnedLegal(SaveFile sav, CancellationToken cancellation = default)
    {
        var set = new HashSet<(int, int)>();
        void Check(PKM pk, StorageSlotType type)
        {
            if (pk.Species == 0 || pk.IsEgg) return;
            cancellation.ThrowIfCancellationRequested();
            if (!set.Contains((pk.Species, pk.Form)) && LegalityCheck.Analyze(pk, sav, type).Valid)
                set.Add((pk.Species, pk.Form));
        }
        if (sav.HasBox)
            for (var b = 0; b < sav.BoxCount; b++)
                for (var i = 0; i < sav.BoxSlotCount; i++)
                    Check(sav.GetBoxSlotAtIndex(b, i), StorageSlotType.Box);
        if (sav.HasParty)
            for (var i = 0; i < sav.PartyCount; i++)
                Check(sav.GetPartySlotAtIndex(i), StorageSlotType.Party);
        return set;
    }
}
