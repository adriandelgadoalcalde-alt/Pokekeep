// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Engine.Catalog;
using AutoLegalidad.Engine.Context;
using PKHeX.Core;

namespace AutoLegalidad.Engine.Generation;

/// <summary>
/// Hito 3, propuesta 3 del Hito 2: antes de generar, comprobar que la FORMA pedida existe en el destino y se puede guardar. El generador de ALM sustituye la
/// forma en silencio cuando no existe (docs/alm-fiabilidad.md); aqui se decide ANTES, de forma explicita y con razon, la forma que se pide. La especie NUNCA cambia.
/// Fuentes: tabla personal del destino (<c>IsPresentInGame</c>) y <c>availability.form_storage</c> del catalogo (Hito 1, que sale del mismo Core).
/// </summary>
public static class FormGuard
{
    /// <param name="Changed">La forma pedida no existe o no es almacenable en el destino.</param>
    /// <param name="NewForm">Forma que se pide en su lugar (la base si existe; si no, la primera presente y almacenable).</param>
    /// <param name="NoEquivalent">Ninguna forma de la especie existe y se puede guardar en el destino.</param>
    public sealed record Verdict(bool Changed, byte NewForm, string? Reason, bool NoEquivalent);

    public static Verdict Check(PKM pk, Destination dest, CatalogReader? catalog)
    {
        if (IsUsable(pk.Species, pk.Form, dest, catalog, out var why))
            return new Verdict(false, pk.Form, null, false);

        // La forma base primero (la mas probable); despues cualquier otra forma presente y almacenable.
        var count = 1;
        try { count = Math.Max(1, (int)dest.Personal.GetFormEntry(pk.Species, 0).FormCount); }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException) { }
        for (var f = 0; f < count; f++)
        {
            if (f == pk.Form) continue;
            if (IsUsable(pk.Species, (byte)f, dest, catalog, out _))
                return new Verdict(true, (byte)f, $"La forma {pk.Form} no se puede guardar en {dest.Version} ({why}): se pide la forma {f}, que sí existe.", false);
        }
        return new Verdict(false, pk.Form, $"Ninguna forma de la especie {pk.Species} se puede guardar en {dest.Version}.", true);
    }

    private static bool IsUsable(ushort species, byte form, Destination dest, CatalogReader? catalog, out string why)
    {
        why = string.Empty;
        try
        {
            if (!dest.Personal.IsPresentInGame(species, form)) { why = "no existe en ese juego"; return false; }
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException) { why = "fuera de la tabla del juego"; return false; }

        // La tabla personal del destino no tiene esa forma (p. ej. Pichu de orejas puntiagudas, solo de HG/SS, en Platino): FormVerifier.cs:34-36 la da por invalida (FormInvalidRangeLEQ_0F).
        try
        {
            var pi = dest.Personal[species];
            if (!pi.IsFormWithinRange(form) && !FormInfo.IsValidOutOfBoundsForm(species, form, dest.Generation)) { why = $"fuera del rango de formas de la tabla personal ({pi.FormCount} formas)"; return false; }
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException) { why = "fuera de la tabla del juego"; return false; }

        // El FORMATO de la entidad no guarda la forma: PK3 deriva la forma solo para Unown (PID) y devuelve 0 para el resto (PKM/Shared/G3PKM.cs:66), asi que Deoxys Ataque/Defensa/Velocidad
        // no se pueden representar en Gen3 (en esa generacion la forma depende de la VERSION del juego). Se comprueba asignandola en una entidad en blanco del destino.
        if (form != 0 && dest.Generation <= 3 && species != (ushort)Species.Unown)
        {
            var probe = dest.Sav.BlankPKM;
            probe.Species = species;
            probe.Form = form;
            if (probe.Form != form) { why = $"el formato {dest.EntityType.Name} no guarda esa forma"; return false; }
        }
        // Hito 3b: la forma debe poder guardarse en el formato de entidad del destino (Deoxys en PK3, formas en PK1/PK2...).
        if (form != 0 && !FormStorageProof.CanStore(dest, species, form, out var storageWhy)) { why = storageWhy; return false; }

        var av = catalog?.Availability((int)dest.Version, species, form);
        if (av is { } a)
        {
            if (a.State == "Absent") { why = "ausente en el catálogo de disponibilidad"; return false; }
            if (a.FormStorage is "BattleOnly" or "TotemOnly" or "LordOnly") { why = $"forma solo de combate ({a.FormStorage})"; return false; }
        }
        return true;
    }
}

/// <summary>Resuelve una clave de encuentro del catalogo (<see cref="EncounterKeys"/>) a la plantilla del Core para especie y destino concretos (origen fijado por el usuario o por la Libreria).</summary>
public static class OriginKeyResolver
{
    /// <summary>
    /// Hito 5c (cría de la Librería): el huevo que el Core enumera para la especie y la forma en el destino, o null si no hay ninguno. Las entradas de cría no llevan clave en el catálogo y sin esto
    /// el generador devolvía el primer origen válido del juego (salvaje, sombra, intercambio...) y la Librería prometía una cría que no podía dar; con el huevo FIJADO el resultado es siempre el mismo
    /// y, si el Core no ofrece ninguno, se dice al momento. Se prefiere el huevo del propio juego (versión del destino) y después el primero que enumere el Core (que sale en su orden estable).
    /// </summary>
    public static IEncounterable? FindEgg(PKM wish, Destination dest)
    {
        var blank = dest.Sav.BlankPKM;
        blank.Species = wish.Species;
        blank.Form = wish.Form;
        blank.Gender = blank.GetSaneGender();
        lock (Infra.EngineGlobalState.Gate)
        {
            dest.ApplyParseSettings();
            IEncounterable? first = null;
            foreach (var enc in CoreVersions.Encounters(blank, dest.Trainer, ReadOnlyMemory<ushort>.Empty))
            {
                if (!enc.IsEgg || enc is not IEncounterConvertible || EncounterKeys.For(enc) is not { } k || !k.EndsWith(":egg", StringComparison.Ordinal)) continue;
                if (enc.Version == dest.Version) return enc;
                first ??= enc;
            }
            return first;
        }
    }

    public static IEncounterable? Resolve(string key, PKM wish, Destination dest)
    {
        var blank = dest.Sav.BlankPKM;
        blank.Species = wish.Species;
        blank.Form = wish.Form;
        blank.Gender = blank.GetSaneGender();
        lock (Infra.EngineGlobalState.Gate)
        {
            dest.ApplyParseSettings();
            IEncounterable? sameLine = null;
            foreach (var enc in CoreVersions.Encounters(blank, dest.Trainer, ReadOnlyMemory<ushort>.Empty))
            {
                var k = EncounterKeys.For(enc);
                if (string.Equals(k, key, StringComparison.Ordinal))
                    return enc;
                // Hito 3b: el Core no siempre enumera la plantilla exacta (Zygarde 10%-C/50%-C de Gen7: la cadena de origen usa la forma base); una plantilla de la misma linea de origen
                // (mismo tipo, contexto y huevo, version gemela, otra especie/forma) vale: el generador ajusta la forma que el Core permite cambiar (FormInfo.IsFormChangeable).
                if (sameLine is null && enc is IEncounterConvertible && EncounterKeys.SameOriginLine(k, key)) sameLine = enc;
            }
            return sameLine;
        }
    }
}

/// <summary>
/// Regla 6 (nunca se crean trackers de HOME): el Core exige el rastreador de transferencia de HOME a toda entidad de Gen8+ cuyo origen no sea el propio juego
/// (<c>HomeTrackerUtil.IsRequired</c>), y no se puede validar (no somos el servidor) ni inventar. Esta clase decide, con la funcion publica del Core, cuando una ruta
/// queda BLOQUEADA por eso: ni se genera un candidato con un identificador fingido ni se presenta como «imposible».
/// </summary>
public static class HomeTrackerRule
{
    public static bool IsRequired(IEncounterable enc, EntityContext destination)
        => destination >= EntityContext.Gen8 && destination != EntityContext.Gen7b && enc is IEncounterTemplate t && HomeTrackerUtil.IsRequired(t, destination);

    /// <summary>True si el Core enumera encuentros de la especie en el destino y TODOS exigen un rastreador de HOME.</summary>
    public static bool BlocksEveryRoute(PKM wish, Destination dest, out int routes)
    {
        routes = 0;
        if (dest.Context < EntityContext.Gen8 || dest.Context == EntityContext.Gen7b) return false;
        var blank = dest.Sav.BlankPKM;
        blank.Species = wish.Species;
        blank.Form = wish.Form;
        blank.Gender = blank.GetSaneGender();
        lock (Infra.EngineGlobalState.Gate)
        {
            dest.ApplyParseSettings();
            foreach (var enc in CoreVersions.Encounters(blank, dest.Trainer, ReadOnlyMemory<ushort>.Empty))
            {
                routes++;
                if (!IsRequired(enc, dest.Context)) return false;
            }
        }
        return routes > 0;
    }

    /// <summary>Referencias EXACTAS de PKHeX (versión fijada bfe89c74a3f5) en las que se apoya la imposibilidad.</summary>
    public const string CoreReferences =
        "Referencia técnica: PKHeX.Core, Legality/Restrictions/HomeTrackerUtil.cs:34-43 (IsRequired: EncounterSlot8GO y regalos HOME siempre, y cualquier origen distinto del contexto actual), " +
        "Legality/Verifiers/TransferVerifier.cs:167-173 (VerifyHOMETracker: matrícula 0 => TransferTrackerMissing con la severidad configurada) y " +
        "Legality/Settings/Specialized/HOMETransferSettings.cs:9 (valor por defecto del PKHeX oficial: «inválido»).";

    private static string SeverityText(string sev) => sev switch { "Invalid" => "inválido", "Fishy" => "sospechoso", "Valid" => "válido", _ => sev };

    /// <summary>
    /// Demostracion con el Core, no solo con su tabla: crea el Pokemon desde la plantilla, lo convierte al formato del destino y lo valida con el analisis oficial.
    /// Es una prueba PLENA cuando lo UNICO invalido es <c>TransferTrackerMissing</c> (el resto del Pokemon es legal): entonces solo falta lo que solo el servidor de HOME puede dar.
    /// </summary>
    public static (bool Empirical, string Evidence) Prove(IEncounterable enc, Destination dest, PKM wish, Validation.IOfficialValidator validator)
    {
        if (enc is not IEncounterConvertible conv) return (false, "La plantilla no es convertible: se apoya solo en la regla estática de PKHeX.");
        try
        {
            PKM? raw = null;
            lock (Infra.EngineGlobalState.Gate)
            {
                dest.ApplyParseSettings();
                Infra.GuardedCore.TryCall(() => conv.ConvertToPKM(dest.Trainer, EncounterCriteria.Unrestricted), TimeSpan.FromSeconds(3), out raw);
            }
            if (raw is null) return (false, "PKHeX no produjo un Pokémon de esa plantilla: se apoya solo en la regla estática de PKHeX.");
            var cand = raw.GetType() == dest.EntityType ? raw : EntityConverter.ConvertToType(raw, dest.EntityType, out _);
            if (cand is null) return (false, "PKHeX no convierte el Pokémon al formato del destino: se apoya solo en la regla estática de PKHeX.");
            HandlerUpdate.Apply(cand, dest); // al entrar en la partida de otro entrenador el juego fija el HT (API publica del Core); no toca el rastreador
            cand = Repair.MinimalRepair.Run(cand, dest, new Changes.ChangeLog()); // la misma reparacion que aplica el motor a lo que crea el Core
            var outcome = validator.Validate(cand, cand, dest, requireCatalogGate: false);
            string sev;
            lock (Infra.EngineGlobalState.Gate) sev = ParseSettings.Settings.HOMETransfer.HOMETransferTrackerNotPresent.ToString();
            return outcome.BlockedByHomeTracker
                ? (true, $"Comprobado con PKHeX: el Pokémon generado desde la plantilla es legal en todo salvo la matrícula (rastreador) de HOME (único fallo: TransferTrackerMissing, severidad {SeverityText(sev)}).")
                : (false, "El candidato de la plantilla falla por algo más que la matrícula (rastreador) de HOME: no es una prueba plena.");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return (false, "No se pudo comprobar con PKHeX (" + ex.GetType().Name + "): se apoya solo en la regla estática de PKHeX."); }
    }
}
