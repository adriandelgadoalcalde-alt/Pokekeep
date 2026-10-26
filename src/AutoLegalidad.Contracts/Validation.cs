// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Pokekeep contributors
using System.Security.Cryptography;

namespace AutoLegalidad.Contracts;

/// <summary>Validacion sintactica y de integridad de una solicitud (Fase A, parte pura: sin Core). Devuelve errores en espanol; vacio = valida.</summary>
public static class RequestValidator
{
    /// <summary>Combinaciones imposibles de acciones y opciones (Hito 4b, contrato v2).</summary>
    private static void ValidateOptions(LegalizeRequest r, List<string> errors)
    {
        var o = r.Options;
        if (r.Action == LegalizeAction.Adjust)
        {
            if (o?.Adjust is null || !o.Adjust.Any) errors.Add("options.adjust es obligatorio y debe pedir al menos una operación cuando la acción es Adjust.");
            if (r.OriginEventKey is not null) errors.Add("la acción Adjust no admite originEventKey (no genera un origen nuevo).");
            if (o is { NewPokemon: true } or { MaxLevel: true }) errors.Add("options.newPokemon y options.maxLevel solo valen con la acción Legalize (Adjust usa options.adjust).");
        }
        else
        {
            if (o?.Adjust is not null) errors.Add("options.adjust solo vale con la acción Adjust.");
            if (r.Action == LegalizeAction.Optimize && o is { NewPokemon: true } or { MaxLevel: true })
                errors.Add("options.newPokemon y options.maxLevel solo valen con la acción Legalize (Optimizar stats nunca cambia el nivel).");
        }
    }

    public static IReadOnlyList<string> Validate(LegalizeRequest r, out byte[]? entity)
    {
        var errors = new List<string>();
        entity = null;

        if (r.ProtocolVersion != Protocol.Version)
            errors.Add($"protocolVersion {r.ProtocolVersion} no soportado (el Worker habla {Protocol.Version}).");
        if (!Guid.TryParse(r.RequestId, out _))
            errors.Add("requestId no es un GUID.");
        if (string.IsNullOrWhiteSpace(r.ReleaseId))
            errors.Add("releaseId vacío.");
        if (r.PolicyVersion != Protocol.PolicyVersion)
            errors.Add($"policyVersion '{r.PolicyVersion}' desconocida (esperada '{Protocol.PolicyVersion}').");
        if (string.IsNullOrWhiteSpace(r.DatasetSnapshotId))
            errors.Add("datasetSnapshotId vacío.");
        if (!Enum.IsDefined(r.Action))
            errors.Add("action no válida (Legalize | Optimize | Adjust).");
        ValidateOptions(r, errors);
        if (string.IsNullOrWhiteSpace(r.EntityContext)) errors.Add("entityContext vacío.");
        if (string.IsNullOrWhiteSpace(r.EntityRuntimeType)) errors.Add("entityRuntimeType vacío.");
        if (string.IsNullOrWhiteSpace(r.Target.GameVersion)) errors.Add("target.gameVersion vacío.");
        if (string.IsNullOrWhiteSpace(r.Target.Context)) errors.Add("target.context vacío.");
        if (string.IsNullOrWhiteSpace(r.Target.Trainer.Ot)) errors.Add("target.trainer.ot vacío.");
        if (r.Target.Trainer.Tid16 is < 0 or > 0xFFFF) errors.Add("target.trainer.tid16 fuera de rango.");
        if (r.Target.Trainer.Sid16 is < 0 or > 0xFFFF) errors.Add("target.trainer.sid16 fuera de rango.");
        if (r.Target.Trainer.Gender is < 0 or > 1) errors.Add("target.trainer.gender debe ser 0 o 1.");
        if (r.Target.Trainer.Language is < 0 or > 12) errors.Add("target.trainer.language fuera de rango.");

        if (string.IsNullOrEmpty(r.OriginalEntityBase64))
        {
            errors.Add("originalEntityBase64 vacío.");
            return errors;
        }
        // Cota antes de decodificar: base64 ocupa 4/3 del binario.
        if (r.OriginalEntityBase64.Length > (Protocol.MaxEntityBytes * 4 / 3) + 8)
        {
            errors.Add($"la entidad supera el máximo de {Protocol.MaxEntityBytes} bytes.");
            return errors;
        }
        try
        {
            entity = Convert.FromBase64String(r.OriginalEntityBase64);
        }
        catch (FormatException)
        {
            errors.Add("originalEntityBase64 no es base64 válido.");
            return errors;
        }
        if (entity.Length == 0) errors.Add("la entidad está vacía.");
        if (entity.Length > Protocol.MaxEntityBytes) errors.Add($"la entidad supera el máximo de {Protocol.MaxEntityBytes} bytes.");
        var sha = Convert.ToHexString(SHA256.HashData(entity));
        if (!string.Equals(sha, r.OriginalEntitySha256, StringComparison.OrdinalIgnoreCase))
            errors.Add("originalEntitySha256 no coincide con los bytes recibidos.");
        return errors;
    }
}

/// <summary>Invariantes de una respuesta (regla 1/5 del plan: un fallo jamas lleva candidato; un candidato siempre lleva prueba).</summary>
public static class ResponseValidator
{
    public static bool CarriesCandidate(ResponseStatus s) => s is ResponseStatus.Success or ResponseStatus.AlreadyLegal or ResponseStatus.SuccessNeedsHome;

    public static IReadOnlyList<string> Validate(LegalizeResponse r)
    {
        var errors = new List<string>();
        var hasCandidate = r.CandidateEntityBase64 is not null || r.CandidateSha256 is not null;
        if (hasCandidate && !CarriesCandidate(r.Status))
            errors.Add($"el estado {r.Status} no puede llevar candidato.");
        if (CarriesCandidate(r.Status))
        {
            if (r.CandidateEntityBase64 is null || r.CandidateSha256 is null) errors.Add($"el estado {r.Status} exige candidato y su SHA-256.");
            else
            {
                try
                {
                    var bytes = Convert.FromBase64String(r.CandidateEntityBase64);
                    if (!string.Equals(Convert.ToHexString(SHA256.HashData(bytes)), r.CandidateSha256, StringComparison.OrdinalIgnoreCase))
                        errors.Add("candidateSha256 no coincide con los bytes del candidato.");
                }
                catch (FormatException) { errors.Add("candidateEntityBase64 no es base64."); }
            }
            if (r.Proof is null) errors.Add($"el estado {r.Status} exige una prueba de validación.");
            else
            {
                if (!r.Proof.OfficialValid || !r.Proof.RoundTripValid || !r.Proof.DestinationCompatible || !r.Proof.SpeciesIdentity)
                    errors.Add("una prueba de éxito debe tener oficial, destino, ida y vuelta (guardar y releer) y especie en verde.");
            }
        }
        if (r.Action != LegalizeAction.Adjust && r.Operations.Count > 0)
            errors.Add("solo la acción Adjust lleva resultados por operación.");
        if (r.Action == LegalizeAction.Adjust && r.Status == ResponseStatus.Success && !r.Operations.Any(o => o.Status == OperationStatus.Changed))
            errors.Add("un Success de Adjust exige que al menos una operación haya cambiado algo.");
        if (r.Status == ResponseStatus.TargetChanged)
            errors.Add("el Worker nunca decide TargetChanged (lo decide la interfaz).");
        if (r.Status == ResponseStatus.Timeout && r.SearchComplete)
            errors.Add("un Timeout no puede declararse searchComplete=true.");
        return errors;
    }
}
