// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using AutoLegalidad.Catalog.Core;
using AutoLegalidad.Contracts;
using AutoLegalidad.Engine.Catalog;
using AutoLegalidad.Engine.Context;
using AutoLegalidad.Engine.Infra;
using PKHeX.Core;

namespace AutoLegalidad.Engine.Validation;

/// <summary>Resultado de la revalidacion independiente de una entidad.</summary>
public sealed record ValidationOutcome(bool Valid, ValidationProof Proof, string? EncounterKey, IReadOnlyList<string> Failures, bool BlockedByHomeTracker = false, bool TrackerMissing = false,
    AutoLegalidad.Engine.Generation.OriginAssessment? Origin = null)
{
    public bool HasFailure(string check) => Failures.Contains(check);
}

public static class CheckNames
{
    public const string OfficialAnalysis = "OfficialAnalysis";
    public const string DestinationFormat = "DestinationFormat";
    public const string SpeciesInDestination = "SpeciesInDestination";
    public const string SpeciesIdentity = "SpeciesIdentity";
    public const string RoundTripBytes = "RoundTripBytes";
    public const string RoundTripSave = "RoundTripSave";
    public const string CatalogGate = "CatalogGate";
    public const string OriginPreserved = "OriginPreserved";
    public const string EggStatePreserved = "EggStatePreserved";
    public const string TrackerOnly = "TrackerOnly";
}

public interface IOfficialValidator
{
    ValidationOutcome Validate(PKM entity, PKM original, Destination dest, bool requireCatalogGate);
}

/// <summary>
/// Validador OFICIAL e INDEPENDIENTE (plan 7, fase E): nunca se acepta un candidato por el bool de ALM ni por la ultima comprobacion del generador.
/// Reconstruye la entidad desde sus bytes con el formato correcto, repite <see cref="LegalityAnalysis"/> con el Core fijado y los datos personales del
/// DESTINO, comprueba que la especie original se conserva y existe en el destino, que el formato es el del destino, el round-trip por bytes y por una
/// casilla del destino virtual, y (si se pide) la barrera de generacion del catalogo.
/// </summary>
public sealed class OfficialValidator(CatalogReader? catalog) : IOfficialValidator
{
    private readonly CatalogGate? _gate = catalog is null ? null : new CatalogGate(catalog);

    public ValidationOutcome Validate(PKM entity, PKM original, Destination dest, bool requireCatalogGate)
    {
        var checks = new List<ProofCheck>();
        var failures = new List<string>();
        void Check(string name, bool ok, string? detail = null)
        {
            checks.Add(new ProofCheck { Name = name, Passed = ok, Detail = detail });
            if (!ok) failures.Add(name);
        }

        string[] report = [];
        string? encounterKey = null;
        string? encounterType = null;
        var officialValid = false;
        var blockedByTracker = false;
        var trackerMissing = false;
        var destOk = false;
        var roundTrip = false;
        var speciesOk = false;
        AutoLegalidad.Engine.Generation.OriginAssessment? origin = null;

        lock (EngineGlobalState.Gate)
        {
            dest.ApplyParseSettings();

            // 1) Identidad de la especie: contra la entidad ORIGINAL de la solicitud, no contra el estado del motor.
            speciesOk = entity.Species == original.Species && entity.Species is >= 1 and <= 1025;
            Check(CheckNames.SpeciesIdentity, speciesOk, speciesOk ? null : $"especie {original.Species} -> {entity.Species}");

            // 2) Formato del destino.
            var formatOk = entity.GetType() == dest.EntityType;
            Check(CheckNames.DestinationFormat, formatOk, formatOk ? null : $"{entity.GetType().Name} != {dest.EntityType.Name}");

            // 2b) Un huevo sigue siendo un huevo y un Pokemon eclosionado no vuelve a ser huevo (nunca se hace eclosionar a nadie).
            var eggOk = entity.IsEgg == original.IsEgg;
            Check(CheckNames.EggStatePreserved, eggOk, eggOk ? null : $"IsEgg {original.IsEgg} -> {entity.IsEgg}");

            // 3) La especie/forma existe en el destino (tabla personal del destino).
            var present = false;
            try { present = dest.Personal.IsPresentInGame(entity.Species, entity.Form); }
            catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException) { present = false; }
            Check(CheckNames.SpeciesInDestination, present, present ? null : $"{entity.Species}/{entity.Form} no existe en {dest.Version}");
            destOk = formatOk && present;

            // 4) Analisis oficial con la tabla personal y el tipo de casilla del destino.
            var la = new LegalityAnalysis(entity, dest.Personal, dest.SlotType);
            officialValid = la.Valid && la.Parsed;
            report = [.. la.Report("es").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
            Check(CheckNames.OfficialAnalysis, officialValid, officialValid ? null : "el análisis oficial de PKHeX (LegalityAnalysis) no lo da por válido");
            // Regla 6: lo UNICO que impide validarlo es el rastreador de transferencia de HOME, que el producto no puede crear (el Core: «no te lo inventes»).
            blockedByTracker = !officialValid && la.Parsed && la.Results.Any(r => !r.Valid && r.Result == LegalityCheckResultCode.TransferTrackerMissing)
                && la.Results.All(r => r.Valid || r.Result == LegalityCheckResultCode.TransferTrackerMissing)
                && la.Info.Moves.All(m => m.Valid) && la.Info.Relearn.All(m => m.Valid);
            // R-009: el Core registra la falta del rastreador con la severidad configurada (Invalid por defecto, Fishy solo con el ajuste explicito del usuario).
            trackerMissing = la.Parsed && la.Results.Any(r => r.Result == LegalityCheckResultCode.TransferTrackerMissing);
            // R-009 (0.6.6, hallazgos A1 y A2 de la auditoria): con el ajuste explicito (Fishy) el Core da por «visitado HOME» a TODO Pokemon sin rastreador (IgnoreTransferIfNoTracker:
            // movimientos, evoluciones, cintas y verificadores de Leyendas que solo existen tras HOME), tambien a uno NATIVO que no exige matricula y no registra ningun TransferTrackerMissing.
            // Por eso, con Fishy y un candidato valido, SIEMPRE se repite el analisis con la severidad POR DEFECTO (Invalid): si es valido, es legal de verdad; si lo UNICO que marca es el
            // rastreador ausente (resultados, movimientos y recordables), necesita pasar por HOME; cualquier otra cosa se rechaza (TrackerOnly).
            if (officialValid && dest.TrackerSeverity == Severity.Fishy)
            {
                var before = ParseSettings.Settings.HOMETransfer.HOMETransferTrackerNotPresent;
                ParseSettings.Settings.HOMETransfer.HOMETransferTrackerNotPresent = Severity.Invalid;
                try
                {
                    var strict = new LegalityAnalysis(entity, dest.Personal, dest.SlotType);
                    if (strict.Parsed && strict.Valid)
                    {
                        trackerMissing = false;   // legal con la severidad por defecto: no depende del ajuste
                    }
                    else
                    {
                        var invalid = strict.Parsed ? strict.Results.Where(r => !r.Valid).ToList() : [];
                        var onlyTracker = strict.Parsed && invalid.Count > 0 && invalid.All(r => r.Result == LegalityCheckResultCode.TransferTrackerMissing)
                            && strict.Info.Moves.All(m => m.Valid) && strict.Info.Relearn.All(m => m.Valid);
                        trackerMissing = onlyTracker;
                        Check(CheckNames.TrackerOnly, onlyTracker, onlyTracker ? null : "con la severidad por defecto PKHeX marca algo más que la matrícula (rastreador) de HOME: " + string.Join(" / ", strict.Report("es").Split((char)10, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(l => l.Contains("nvalid", StringComparison.OrdinalIgnoreCase) || l.Contains("nválid", StringComparison.OrdinalIgnoreCase)).Take(3)));
                    }
                }
                finally { ParseSettings.Settings.HOMETransfer.HOMETransferTrackerNotPresent = before; }
            }
            var enc = la.EncounterMatch;
            encounterType = enc.GetType().Name;
            encounterKey = EncounterKeys.For(enc) ?? EncounterKeys.For(la.EncounterOriginal);
            if (la.Parsed) origin = AutoLegalidad.Engine.Generation.OriginPreference.Assess(entity, enc, dest);

            // 5) Barrera de generacion del catalogo (regla 8): solo se exige a lo que el motor GENERA.
            if (requireCatalogGate)
            {
                var ok = _gate is not null && (_gate.IsGenerable(la.EncounterMatch, out _) || _gate.IsGenerable(la.EncounterOriginal, out _));
                Check(CheckNames.CatalogGate, ok, ok ? null : $"{encounterType} no está entre los orígenes aprobados del catálogo (v_encounter_generable)");
            }
            else
            {
                var ok = _gate is not null && (_gate.IsGenerable(la.EncounterMatch, out _) || _gate.IsGenerable(la.EncounterOriginal, out _));
                checks.Add(new ProofCheck { Name = CheckNames.CatalogGate, Passed = ok, Detail = "informativo (entidad del usuario)" });
            }

            // 6) Round-trip por bytes (formato correcto) y por una casilla del destino virtual.
            roundTrip = RoundTrip(entity, dest, checks, failures);
        }

        var proof = new ValidationProof
        {
            ValidatorVersion = $"PKHeX.Core {PinnedVersions.CoreTag}+{PinnedVersions.CoreCommit12}",
            ValidatorCommit = PinnedVersions.CoreCommit,
            Report = report,
            Checks = checks,
            EncounterKey = encounterKey,
            EncounterType = encounterType,
            SourceRefs = [$"PKHeX.Core@{PinnedVersions.CoreCommit12}:LegalityAnalysis"],
            RuleProfileHash = RuleProfile.Sha256,
            RoundTripValid = roundTrip,
            DestinationCompatible = destOk,
            SpeciesIdentity = speciesOk,
            OfficialValid = officialValid,
        };
        return new ValidationOutcome(failures.Count == 0, proof, encounterKey, failures, blockedByTracker, trackerMissing, origin);
    }

    /// <summary>
    /// Escribe <paramref name="clone"/> en la casilla de EQUIPO 0 del destino virtual y la vuelve a leer, con el formato y las reglas de importación de equipo del Core.
    /// En Let's Go (<see cref="SAV7b"/>) el equipo no es un bloque aparte: son seis punteros a casillas de la lista de cajas (<c>PokeListHeader.GetPartyOffset</c>) y en un destino virtual
    /// recién creado todos valen «vacío» (1001), así que <see cref="SaveFile.SetPartySlotAtIndex"/> lanzaba <see cref="ArgumentOutOfRangeException"/> y todo el viaje de ida y vuelta por la casilla
    /// del destino fallaba. El mismo cuerpo que ejecuta el Core (<c>SetPartySlot</c> / <c>GetPartySlot</c>: <c>UpdatePKM(isParty: true)</c>, <c>SetPartyValues</c>, <c>WriteSlotParty</c>) se aplica
    /// sobre la casilla 0 de la lista de cajas, que en ese formato guarda en formato de equipo (<c>SAV7b.GetBoxSlot</c>: «party format in boxes!»). Solo el puntero cambia; el contenido y las reglas no.
    /// </summary>
    private static PKM WritePartySlotAndRead(SaveFile sav, PKM clone)
    {
        if (sav is SAV7b lg)
        {
            var offset = lg.GetBoxSlotOffset(0, 0);
            lg.SetPartySlot(clone, lg.Data[offset..]);
            return lg.GetPartySlot(lg.Data[offset..]);
        }
        sav.SetPartySlotAtIndex(clone, 0);
        return sav.GetPartySlotAtIndex(0);
    }

    private static bool RoundTrip(PKM entity, Destination dest, List<ProofCheck> checks, List<string> failures)
    {
        // a) bytes -> entidad nueva -> misma firma y nuevo analisis valido.
        var bytesOk = false;
        string? detail = null;
        try
        {
            // Una entidad del EQUIPO se exporta con sus datos de equipo (Furfrou, Hoopa... guardan ahi el argumento de forma): Hito 3b, formas que solo existen en el equipo.
            var bytes = EntityIo.Export(entity, partySize: dest.SlotType == StorageSlotType.Party && entity.SIZE_PARTY != entity.SIZE_STORED);
            var re = EntityIo.Parse(bytes, dest.Context);
            if (re is null) detail = "los bytes exportados no se pueden volver a leer";
            else if (re.GetType() != entity.GetType()) detail = $"el tipo cambia: {entity.GetType().Name} -> {re.GetType().Name}";
            else if (!PkmFields.SignatureEquals(entity, re, out var diff)) detail = "la firma cambia: " + diff;
            else
            {
                var la = new LegalityAnalysis(re, dest.Personal, dest.SlotType);
                bytesOk = la.Valid && la.Parsed;
                if (!bytesOk) detail = "la entidad releída deja de ser válida";
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { detail = "excepción: " + ex.Message; }
        checks.Add(new ProofCheck { Name = CheckNames.RoundTripBytes, Passed = bytesOk, Detail = detail });
        if (!bytesOk) failures.Add(CheckNames.RoundTripBytes);

        // b) casilla del destino virtual: escribir y leer (captura las mutaciones de importacion del guardado).
        var saveOk = false;
        detail = null;
        try
        {
            var sav = dest.Sav;
            var clone = entity.Clone();
            PKM back;
            if (sav.HasBox && dest.SlotType != StorageSlotType.Party)
            {
                sav.SetBoxSlotAtIndex(clone, 0, 0);
                back = sav.GetBoxSlotAtIndex(0, 0);
            }
            else back = WritePartySlotAndRead(sav, clone);
            // Hito 5: en XD el estado «purificado» NO vive en los bytes del Pokémon sino en una tabla del guardado (SAV3XD.ShadowInfo, privada); el guardado virtual nuevo no la tiene y al releer
            // devolvía un Pokémon legal y purificado como Pokémon Oscuro, así que todo Pokémon purificado salía «ilegal» (y Legalizar lo reconstruía). Si como Oscuro no valida, se evalúa como purificado,
            // que es el otro estado posible en un guardado real (la bandera es de solo escritura interna: se fija por reflexión; no toca ningún byte del Pokémon).
            if (back is XK3 { IsShadow: true } backXd && backXd.ShadowID != 0 && !new LegalityAnalysis(backXd, dest.Personal, dest.SlotType).Valid)
            {
                var purified = backXd.Clone();
                typeof(XK3).GetProperty(nameof(XK3.IsShadow))!.GetSetMethod(nonPublic: true)!.Invoke(purified, [false]);
                if (new LegalityAnalysis(purified, dest.Personal, dest.SlotType).Valid) back = purified;
            }
            if (back.GetType() != entity.GetType()) detail = $"el tipo cambia al guardarlo: {back.GetType().Name}";
            else if (!PkmFields.SignatureEquals(entity, back, out var diff))
            {
                // PKHeX adapta al guardar («como si se hubiera intercambiado»: lugar y fecha de captura de un huevo, p. ej.). Eso es una mutacion LEGITIMA de importacion si solo toca
                // esos campos y la entidad leida de la casilla sigue siendo valida (plan 7: validar las mutaciones de importacion); cualquier otro cambio rechaza el candidato.
                var diffs = PkmFields.SignatureDifferences(entity, back);
                var benign = diffs.Count > 0 && diffs.All(k => k is "MetLocation" or "EggLocation" or "MetLevel" or "MetDate" or "EggMetDate");
                if (!benign) detail = "el guardado altera la entidad: " + diff;
                else
                {
                    var laBack = new LegalityAnalysis(back, dest.Personal, dest.SlotType);
                    saveOk = laBack.Valid && laBack.Parsed;
                    detail = saveOk ? "PKHeX adapta la entidad al guardarla (" + string.Join(", ", diffs) + ") y sigue siendo legal" : "el guardado la adapta (" + string.Join(", ", diffs) + ") y la deja ilegal";
                }
            }
            else
            {
                var la = new LegalityAnalysis(back, dest.Personal, dest.SlotType);
                saveOk = la.Valid && la.Parsed;
                if (!saveOk) detail = "la entidad leída de la casilla deja de ser válida (Parsed=" + la.Parsed + ", Valid=" + la.Valid + "): "
                    + string.Join(" / ", la.Report("es").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(l => !l.StartsWith("Valid", StringComparison.OrdinalIgnoreCase)).Take(4));
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { detail = "excepción: " + ex.Message; }
        checks.Add(new ProofCheck { Name = CheckNames.RoundTripSave, Passed = saveOk, Detail = detail });
        if (!saveOk) failures.Add(CheckNames.RoundTripSave);
        return bytesOk && saveOk;
    }
}
